using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Docklonia.Controls;
using Dock = Docklonia.Controls.Dock;
using Docklonia.Descriptors;
using Docklonia.Model;
using Xunit;

namespace Docklonia.Tests;

/// <summary>
/// Moving a node between two <c>Dock</c>s, which is the same operation as a
/// cross-window drag (§7) — only the source of coordinates differs.
/// </summary>
public class CrossDockTests
{
    private sealed class Doc
    {
        public Doc(string key) => Key = key;

        public string Key { get; }

        public bool Closable { get; set; } = true;
    }

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
            CanClose = new Avalonia.Data.Binding(nameof(Doc.Closable)),
        });

        dock.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(documents);
        return dock;
    }

    private static Window Host(Dock dock)
    {
        var window = new Window { Width = 600, Height = 400, Content = dock };
        window.Show();
        Flush();
        return window;
    }

    /// <summary>Performs the drop the drag session would perform, without a pointer.</summary>
    private static void Drop(Dock origin, Dock target, IDockNode node, DockDirection direction)
    {
        var payload = DockTree.ContentsIn(node).Select(content => content.Content).ToArray();
        target.Drag.CompleteDrop(origin, node, payload, null, direction, isOuter: true);
        Flush();
    }

    [AvaloniaFact]
    public void MovingANodeToAnotherDockLeavesTheOriginTreeClean()
    {
        var moving = new Doc("a");
        var origin = BuildDock(moving, new Doc("b"));
        var target = BuildDock();

        var originWindow = Host(origin);
        var targetWindow = Host(target);

        var node = origin.Layout!.AllPanes().OfType<DockContent>().Single(c => ReferenceEquals(c.Content, moving));

        Drop(origin, target, node, DockDirection.Center);

        // The node now lives in the target...
        Assert.Contains(target.Layout!.AllPanes(), pane => ReferenceEquals(pane, node));

        // ...and no longer anywhere in the origin.
        Assert.DoesNotContain(origin.Layout!.AllPanes(), pane => ReferenceEquals(pane, node));
        Assert.DoesNotContain(originWindow.GetVisualDescendants().OfType<DockTab>(), tab => ReferenceEquals(tab.Node, node));
    }

    [AvaloniaFact]
    public void MovingTheLastNodeOutOfADockEmptiesIt()
    {
        var only = new Doc("only");
        var origin = BuildDock(only);
        var target = BuildDock();

        var originWindow = Host(origin);
        var targetWindow = Host(target);

        var node = origin.Layout!.AllPanes().OfType<DockContent>().Single();

        Drop(origin, target, node, DockDirection.Center);

        Assert.Null(origin.Layout!.Root);
        Assert.Empty(originWindow.GetVisualDescendants().OfType<DockTab>());
    }

    [AvaloniaFact]
    public void ANodeMovedBackAndForthLeavesNeitherTreeHoldingIt()
    {
        var moving = new Doc("a");
        var origin = BuildDock(moving, new Doc("b"));
        var target = BuildDock();

        var originWindow = Host(origin);
        var targetWindow = Host(target);

        var node = origin.Layout!.AllPanes().OfType<DockContent>().Single(c => ReferenceEquals(c.Content, moving));

        Drop(origin, target, node, DockDirection.Center);
        Drop(target, origin, node, DockDirection.Center);

        Assert.Contains(origin.Layout!.AllPanes(), pane => ReferenceEquals(pane, node));
        Assert.Null(target.Layout!.Root);
        Assert.Empty(targetWindow.GetVisualDescendants().OfType<DockTab>());
    }

    [AvaloniaFact]
    public void AMovedNodeCanBeClosedInItsNewDockAndLeavesItEmpty()
    {
        var moving = new Doc("a");
        var origin = BuildDock(moving, new Doc("b"));
        var target = BuildDock();

        var originWindow = Host(origin);
        var targetWindow = Host(target);

        var node = origin.Layout!.AllPanes().OfType<DockContent>().Single(c => ReferenceEquals(c.Content, moving));

        Drop(origin, target, node, DockDirection.Center);

        Assert.NotNull(target.Layout!.Root);

        target.Commands.RequestClose(node);
        Flush();

        Assert.Null(target.Layout.Root);
        Assert.Empty(targetWindow.GetVisualDescendants().OfType<DockTab>());
    }

    /// <summary>
    /// Metadata resolves live, per owning <c>Dock</c> (§3.7) — a node moved
    /// between them re-resolves against the destination's descriptors rather than
    /// keeping the originating <c>Dock</c>'s presentation.
    /// </summary>
    [AvaloniaFact]
    public void AMovedNodeReresolvesAgainstTheDestinationsDescriptors()
    {
        var moving = new Doc("a");
        var origin = BuildDock(moving, new Doc("b"));

        var target = new Dock();
        target.ItemDescriptors.Add(new DockItemDescriptor
        {
            DataType = typeof(Doc),
            Title = new Avalonia.Data.Binding("Key") { StringFormat = "tool: {0}" },
            ContentKey = new Avalonia.Data.Binding(nameof(Doc.Key)),
        });
        target.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>();

        Host(origin);
        Host(target);

        var node = origin.Layout!.AllPanes().OfType<DockContent>().Single(c => ReferenceEquals(c.Content, moving));
        Assert.Equal("a", node.Title);

        Drop(origin, target, node, DockDirection.Center);

        Assert.Equal("tool: a", node.Title);
    }
}
