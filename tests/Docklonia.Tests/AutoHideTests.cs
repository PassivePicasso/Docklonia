using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Docklonia.Controls;
using Dock = Docklonia.Controls.Dock;
using Docklonia.Descriptors;
using Docklonia.Model;
using Docklonia.Model.Mutations;
using Xunit;

namespace Docklonia.Tests;

/// <summary>
/// What a parked pane owes the layout it left: a flyout that fits the region it
/// covers, a button that goes wherever the pane goes, and a way back that does
/// not depend on that button.
/// </summary>
public class AutoHideTests
{
    private sealed class Doc
    {
        public Doc(string key) => Key = key;

        public string Key { get; }
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

    private static Window Host(Dock dock)
    {
        var window = new Window { Width = 800, Height = 600, Content = dock };
        window.Show();
        Flush();
        return window;
    }

    private static void Flush()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Parks a pane on a chosen edge, so a test does not depend on where it sat.</summary>
    private static AutoHideEntry Park(Dock dock, IDockNode node, DockEdge edge)
    {
        var entry = AutoHideOperations.Hide(dock.EnsureLayout(), node, edge);
        dock.NotifyLayoutChanged();
        Flush();
        return entry;
    }

    private static IDockNode[] Contents(Dock dock)
        => dock.Layout!.AllPanes().OfType<DockContent>().Cast<IDockNode>().ToArray();

    /// <summary>The region a flyout slides over: the content area, inside the strips.</summary>
    private static Panel Content(Window window)
        => window.GetVisualDescendants().OfType<Panel>().Single(panel => panel.Name == Dock.OverlayPart);

    private static DockAutoHideButton ButtonFor(Window window, AutoHideEntry entry)
        => window.GetVisualDescendants().OfType<DockAutoHideButton>().First(button => ReferenceEquals(button.Entry, entry));

    /// <summary>
    /// The strips are chrome outside the content area, so a flyout sized to the
    /// whole control hangs past it — far enough on the top and right edges to
    /// carry the pane's own titlebar buttons off the visible region.
    /// </summary>
    [AvaloniaFact]
    public void AFlyoutFitsInsideTheRegionItCovers()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"), new Doc("c"));
        var window = Host(dock);

        var contents = Contents(dock);
        var right = Park(dock, contents[1], DockEdge.Right);
        var top = Park(dock, contents[2], DockEdge.Top);

        var content = Content(window);
        var flyout = window.GetVisualDescendants().OfType<DockAutoHideFlyout>().Single();

        Assert.True(content.Bounds.Width > 0);

        // The right strip narrows the content area, so a top flyout measured
        // against the Dock overhangs it by the width of that strip.
        dock.ShowAutoHideFlyout(top, ButtonFor(window, top));
        Flush();

        Assert.True(flyout.Bounds.Right <= content.Bounds.Width + 0.5, $"top flyout {flyout.Bounds} escapes {content.Bounds.Size}");

        dock.ShowAutoHideFlyout(right, ButtonFor(window, right));
        Flush();

        Assert.True(flyout.Bounds.Right <= content.Bounds.Width + 0.5, $"right flyout {flyout.Bounds} escapes {content.Bounds.Size}");
        Assert.True(flyout.Bounds.Bottom <= content.Bounds.Height + 0.5, $"right flyout {flyout.Bounds} escapes {content.Bounds.Size}");
    }

    /// <summary>
    /// Moving a parked pane anywhere retires its entry. The entry is the only
    /// record that a strip owns the pane, so leaving it behind put one node in
    /// two places at once.
    /// </summary>
    [AvaloniaFact]
    public void FloatingAParkedPaneTakesItsButtonWithIt()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock);

        var entry = Park(dock, Contents(dock)[0], DockEdge.Left);
        Assert.Single(window.GetVisualDescendants().OfType<DockAutoHideButton>());

        dock.Commands.Float(entry.Pane, new PixelPoint(120, 120));
        Flush();

        Assert.Empty(dock.Layout!.AutoHidden);
        Assert.Empty(window.GetVisualDescendants().OfType<DockAutoHideButton>());
    }

    /// <summary>
    /// The pane is written once, wherever it now lives. Written twice, the
    /// reload produced a second copy no item could be matched to: a button
    /// naming nothing, that would neither open nor close.
    /// </summary>
    [AvaloniaFact]
    public void APaneTornOutOfAStripIsSavedOnlyWhereItLandsNow()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        Host(dock);

        var entry = Park(dock, Contents(dock)[0], DockEdge.Left);
        dock.Commands.Float(entry.Pane, new PixelPoint(120, 120));
        Flush();

        var restored = DockLayout.FromJson(dock.Layout!.ToJson());

        Assert.Empty(restored.AutoHidden);
        Assert.Single(restored.Floats);
        Assert.Equal(2, restored.AllPanes().OfType<DockContent>().Count());
    }

    /// <summary>
    /// Docking a parked pane back into the tree retires the entry by the same
    /// route, so no gesture that moves a pane can leave a button behind.
    /// </summary>
    [AvaloniaFact]
    public void DockingAParkedPaneRetiresItsEntry()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        Host(dock);

        var entry = Park(dock, Contents(dock)[0], DockEdge.Bottom);

        DockMutator.Dock(dock.Layout!, entry.Pane, dock.Layout!.Root!, DockDirection.Right);
        dock.NotifyLayoutChanged();
        Flush();

        Assert.Empty(dock.Layout.AutoHidden);
    }

    /// <summary>
    /// Minimize parks the pane from its titlebar, so unparking is offered there
    /// too rather than only on the button the pane left behind.
    /// </summary>
    [AvaloniaFact]
    public void TheFlyoutsOwnTitlebarRepinsThePane()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock);

        var node = Contents(dock)[0];
        var entry = Park(dock, node, DockEdge.Left);

        dock.ShowAutoHideFlyout(entry, ButtonFor(window, entry));
        Flush();

        var flyout = window.GetVisualDescendants().OfType<DockAutoHideFlyout>().Single();
        var pane = flyout.GetVisualDescendants().OfType<DockPaneControl>().First();

        Assert.Contains(":auto-hidden", pane.Classes);

        var pin = pane.GetVisualDescendants().OfType<Button>().Single(button => button.Name == DockPaneControl.PinButtonPart);
        Assert.True(pin.IsVisible);

        pin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Flush();

        Assert.Empty(dock.Layout!.AutoHidden);
        Assert.Contains(dock.Layout.AllPanes(), candidate => ReferenceEquals(candidate, node));
    }

    /// <summary>A pane that is not parked shows no pin, so the glyph means one thing.</summary>
    [AvaloniaFact]
    public void ADockedPaneOffersNoPin()
    {
        var dock = BuildDock(new Doc("a"));
        Host(dock);

        var pane = dock.PaneControls.First();
        var pin = pane.GetVisualDescendants().OfType<Button>().Single(button => button.Name == DockPaneControl.PinButtonPart);

        Assert.DoesNotContain(":auto-hidden", pane.Classes);
        Assert.False(pin.IsVisible);
    }

    /// <summary>
    /// A side strip runs down the window, so its buttons read down it. The
    /// rotation is a layout transform, so the strip stays as narrow as the
    /// turned label rather than as wide as the untuned one.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(DockEdge.Left, -90d)]
    [InlineData(DockEdge.Right, 90d)]
    public void SideStripButtonsReadDownTheStrip(DockEdge edge, double angle)
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock);

        var entry = Park(dock, Contents(dock)[0], edge);
        var rotation = Rotation(ButtonFor(window, entry));

        Assert.Equal(angle, Assert.IsType<RotateTransform>(rotation.LayoutTransform).Angle);
    }

    /// <summary>A top or bottom strip runs across, so its buttons read across.</summary>
    [AvaloniaTheory]
    [InlineData(DockEdge.Top)]
    [InlineData(DockEdge.Bottom)]
    public void EndStripButtonsReadAcrossTheStrip(DockEdge edge)
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock);

        var entry = Park(dock, Contents(dock)[0], edge);

        Assert.Null(Rotation(ButtonFor(window, entry)).LayoutTransform);
    }

    private static LayoutTransformControl Rotation(DockAutoHideButton button)
    {
        button.ApplyTemplate();
        return button.GetVisualDescendants().OfType<LayoutTransformControl>().Single();
    }
}
