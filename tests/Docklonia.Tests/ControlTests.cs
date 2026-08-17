using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Docklonia.Controls;
using Dock = Docklonia.Controls.Dock;
using Docklonia.Descriptors;
using Docklonia.Model;
using Xunit;

namespace Docklonia.Tests;

/// <summary>
/// Control-level tests against the shipped templates. These cover the seams
/// where model correctness alone is not enough — template realization, hit
/// testing, and pointer arithmetic.
/// </summary>
public class ControlTests
{
    private sealed class Doc
    {
        public Doc(string key) => Key = key;

        public string Key { get; }
    }

    private static Window Host(Dock dock, Size size)
    {
        var window = new Window { Width = size.Width, Height = size.Height, Content = dock };
        window.Show();
        Flush();
        return window;
    }

    /// <summary>Drives layout to completion; headless has no render loop of its own.</summary>
    private static void Flush()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static Dock BuildDock(params Doc[] documents)
    {
        var dock = new Dock();

        dock.ItemDescriptors.Add(new DockItemDescriptor
        {
            DataType = typeof(Doc),
            Title = new Avalonia.Data.Binding(nameof(Doc.Key)),
            ContentKey = new Avalonia.Data.Binding(nameof(Doc.Key)),
        });

        dock.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(documents);
        return dock;
    }

    private static void Settle(Window window) => Flush();

    [AvaloniaFact]
    public void AMinimizedPaneLeavesAButtonOnAnEdgeStrip()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));

        var pane = dock.PaneControls.First();
        dock.Commands.Minimize(pane);
        Settle(window);

        // The pane left the tree...
        Assert.Single(dock.Layout!.AutoHidden);

        // ...and is reachable again, which is what makes auto-hide recoverable.
        var entry = dock.Layout.AutoHidden[0];
        var buttons = window.GetVisualDescendants().OfType<DockAutoHideButton>().ToArray();

        Assert.Single(buttons);
        Assert.Equal(entry.Pane.Title, buttons[0].Title);
        Assert.Same(entry, buttons[0].Entry);

        var strip = window.GetVisualDescendants().OfType<DockAutoHideStrip>().Single(s => s.IsVisible);
        Assert.Equal(dock.Layout.AutoHidden[0].Edge, strip.Edge);
    }

    [AvaloniaFact]
    public void ReminimizedPanesRestoreThroughTheirButton()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));

        var pane = dock.PaneControls.First();
        var node = pane.Node;
        dock.Commands.Minimize(pane);
        Settle(window);

        dock.Commands.Restore(dock.Layout!.AutoHidden[0]);
        Settle(window);

        Assert.Empty(dock.Layout.AutoHidden);
        Assert.Contains(dock.Layout.AllPanes(), candidate => ReferenceEquals(candidate, node));
        Assert.Empty(window.GetVisualDescendants().OfType<DockAutoHideButton>());
    }

    /// <summary>
    /// The flyout's size is stored on the entry as a proportion, so it survives
    /// a re-open and a save/load cycle rather than snapping back each time.
    /// </summary>
    [AvaloniaFact]
    public void ResizingTheFlyoutPersistsOntoTheEntry()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));

        dock.Commands.Minimize(dock.PaneControls.First());
        Settle(window);

        var entry = dock.Layout!.AutoHidden[0];
        var button = window.GetVisualDescendants().OfType<DockAutoHideButton>().Single();

        dock.ShowAutoHideFlyout(entry, button);
        Settle(window);

        var flyout = window.GetVisualDescendants().OfType<DockAutoHideFlyout>().Single();
        var before = entry.Ratio;

        // The proportion is measured along the axis the pane's edge lies on.
        var available = entry.Edge is DockEdge.Left or DockEdge.Right
            ? dock.Bounds.Width
            : dock.Bounds.Height;

        flyout.RequestExtent(available * 0.4);

        Assert.NotEqual(before, entry.Ratio);
        Assert.Equal(0.4, entry.Ratio, 2);

        // ...and it round-trips, because the entry is what serializes (§8).
        var restored = Model.DockLayout.FromJson(dock.Layout.ToJson());
        Assert.Equal(0.4, restored.AutoHidden[0].Ratio, 2);
    }

    /// <summary>
    /// An empty <c>Dock</c> has no panes, but must still accept a drop — outer
    /// guides cover docking into an empty <c>Dock</c> (§9). Without this a second
    /// window could never receive its first pane.
    /// </summary>
    [AvaloniaFact]
    public void AnEmptyDockIsStillADropTarget()
    {
        var dock = BuildDock();
        var window = Host(dock, new Size(400, 300));

        Assert.Empty(dock.PaneControls);

        var centre = dock.PointToScreen(new Point(dock.Bounds.Width / 2, dock.Bounds.Height / 2));

        Assert.True(dock.Drag.ContainsScreenPoint(centre));
        Assert.Null(dock.Drag.HitTest(centre, null));
    }

    [AvaloniaFact]
    public void ADockDoesNotClaimAPointOutsideItself()
    {
        var dock = BuildDock(new Doc("a"));
        var window = Host(dock, new Size(400, 300));

        var outside = dock.PointToScreen(new Point(dock.Bounds.Width + 200, dock.Bounds.Height + 200));

        Assert.False(dock.Drag.ContainsScreenPoint(outside));
    }

    [AvaloniaFact]
    public void HitTestingSkipsTheDraggedSubtreeSoAPaneIsNotItsOwnTarget()
    {
        var dock = BuildDock(new Doc("a"));
        var window = Host(dock, new Size(400, 300));

        var pane = dock.PaneControls.Single();
        var centre = pane.PointToScreen(new Point(pane.Bounds.Width / 2, pane.Bounds.Height / 2));

        Assert.Same(pane, dock.Drag.HitTest(centre, null));
        Assert.Null(dock.Drag.HitTest(centre, pane.Node));
    }

    /// <summary>
    /// The grip is positioned from an absolute pointer position rather than an
    /// accumulated delta, so it cannot drift away from the cursor.
    /// </summary>
    [AvaloniaFact]
    public void DraggingTheSplitterPlacesTheRatioWhereThePointerIs()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));

        var split = SplitInTwo(dock);
        Settle(window);

        var presenter = window.GetVisualDescendants().OfType<DockSplitPresenter>().Single();
        var extent = presenter.Bounds.Width - 6;

        presenter.SetRatioFromPosition(new Point(extent * 0.25, 0));
        Assert.Equal(0.25, split.Ratio, 2);

        presenter.SetRatioFromPosition(new Point(extent * 0.75, 0));
        Assert.Equal(0.75, split.Ratio, 2);
    }

    /// <summary>
    /// Splits through the mutation engine, which is the only supported route —
    /// the view follows the model rather than being poked directly (§13).
    /// </summary>
    private static DockSplitPane SplitInTwo(Dock dock)
    {
        var tabs = (DockTabPane)dock.Layout!.Root!;
        Model.Mutations.DockMutator.Dock(dock.Layout, tabs.Children[1], tabs, DockDirection.Right);
        dock.NotifyLayoutChanged();

        return (DockSplitPane)dock.Layout.Root!;
    }

    [AvaloniaFact]
    public void TheSplitterStopsAtTheFloorRatherThanContinuing()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));

        var split = SplitInTwo(dock);
        dock.MinPaneSize = 200;
        Settle(window);

        var presenter = window.GetVisualDescendants().OfType<DockSplitPresenter>().Single();

        presenter.SetRatioFromPosition(new Point(-500, 0));

        var floor = 200d / (presenter.Bounds.Width - 6);
        Assert.True(split.Ratio >= floor - 0.01, $"ratio {split.Ratio} fell below the floor {floor}");
    }
}
