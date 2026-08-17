using Avalonia;
using Avalonia.Layout;
using Docklonia.Model;
using Docklonia.Model.Mutations;
using Xunit;

namespace Docklonia.Tests;

public class MutationTests
{
    private static DockContent Leaf(string key) => new(key) { ContentKey = key, Title = key };

    private static DockLayout LayoutWith(IDockNode root) => new() { Root = root };

    [Fact]
    public void DockingIntoAnEmptyLayoutMakesTheNodeTheRoot()
    {
        var layout = new DockLayout();
        var node = Leaf("a");

        DockMutator.DockToRoot(layout, node, DockDirection.Right);

        Assert.Same(node, layout.Root);
        Assert.Null(node.Parent);
    }

    [Fact]
    public void SplittingLeftPlacesTheNodeFirstAndKeepsTheTargetAttached()
    {
        var target = Leaf("a");
        var layout = LayoutWith(target);
        var node = Leaf("b");

        DockMutator.Dock(layout, node, target, DockDirection.Left);

        var split = Assert.IsType<DockSplitPane>(layout.Root);
        Assert.Equal(Orientation.Horizontal, split.Orientation);
        Assert.Same(node, split.First);
        Assert.Same(target, split.Second);
        Assert.Same(split, node.Parent);
        Assert.Same(split, target.Parent);
    }

    [Fact]
    public void SplittingBottomPlacesTheNodeSecondAndOrientsVertically()
    {
        var target = Leaf("a");
        var layout = LayoutWith(target);
        var node = Leaf("b");

        DockMutator.Dock(layout, node, target, DockDirection.Bottom);

        var split = Assert.IsType<DockSplitPane>(layout.Root);
        Assert.Equal(Orientation.Vertical, split.Orientation);
        Assert.Same(target, split.First);
        Assert.Same(node, split.Second);
    }

    [Fact]
    public void CenterDockingPromotesALeafIntoATabPane()
    {
        var target = Leaf("a");
        var layout = LayoutWith(target);
        var node = Leaf("b");

        DockMutator.Dock(layout, node, target, DockDirection.Center);

        var tabs = Assert.IsType<DockTabPane>(layout.Root);
        Assert.Equal(new IDockNode[] { target, node }, tabs.Children);
        Assert.Same(node, tabs.SelectedChild);
    }

    [Fact]
    public void CenterDockingExtendsAnExistingTabPaneRatherThanNesting()
    {
        var first = Leaf("a");
        var tabs = new DockTabPane(first);
        var layout = LayoutWith(tabs);
        var node = Leaf("b");

        DockMutator.Dock(layout, node, tabs, DockDirection.Center);

        Assert.Same(tabs, layout.Root);
        Assert.Equal(new IDockNode[] { first, node }, tabs.Children);
    }

    [Fact]
    public void DroppingATabGroupOnCenterMergesItsChildrenInsteadOfNesting()
    {
        var host = new DockTabPane(Leaf("a"));
        var layout = LayoutWith(host);
        var incoming = new DockTabPane(Leaf("b"), Leaf("c"));
        DockMutator.DockToRoot(layout, incoming, DockDirection.Right);

        DockMutator.Dock(layout, incoming, host, DockDirection.Center);

        Assert.Same(host, layout.Root);
        Assert.Equal(3, host.Children.Count);
        Assert.All(host.Children, child => Assert.IsType<DockContent>(child));
    }

    [Fact]
    public void RemovingOneSideOfASplitCollapsesItIntoTheSurvivor()
    {
        var keep = Leaf("a");
        var drop = Leaf("b");
        var layout = LayoutWith(new DockSplitPane(Orientation.Horizontal, keep, drop));

        DockMutator.Remove(layout, drop);

        Assert.Same(keep, layout.Root);
        Assert.Null(keep.Parent);
    }

    [Fact]
    public void EmptyingATabPanePrunesItAndItsNowEmptyAncestors()
    {
        var keep = Leaf("a");
        var only = Leaf("b");
        var tabs = new DockTabPane(only);
        var layout = LayoutWith(new DockSplitPane(Orientation.Vertical, keep, tabs));

        DockMutator.Remove(layout, only);

        Assert.Same(keep, layout.Root);
    }

    [Fact]
    public void MovingANodeOntoItsOwnSiblingRewiresRatherThanCorruptingTheTree()
    {
        var moving = Leaf("a");
        var sibling = Leaf("b");
        var layout = LayoutWith(new DockSplitPane(Orientation.Horizontal, moving, sibling));

        DockMutator.Dock(layout, moving, sibling, DockDirection.Bottom);

        var split = Assert.IsType<DockSplitPane>(layout.Root);
        Assert.Equal(Orientation.Vertical, split.Orientation);
        Assert.Same(sibling, split.First);
        Assert.Same(moving, split.Second);
        Assert.Same(split, sibling.Parent);
        Assert.Same(split, moving.Parent);
    }

    [Fact]
    public void DockingANodeIntoItsOwnSubtreeIsRejected()
    {
        var inner = Leaf("a");
        var outer = new DockTabPane(inner);
        var layout = LayoutWith(outer);

        Assert.Throws<InvalidOperationException>(() => DockMutator.Dock(layout, outer, inner, DockDirection.Left));
    }

    [Fact]
    public void FloatingDetachesTheSubtreeAndCollapsesTheOrigin()
    {
        var keep = Leaf("a");
        var moving = Leaf("b");
        var layout = LayoutWith(new DockSplitPane(Orientation.Horizontal, keep, moving));

        var host = DockMutator.Float(layout, moving, new PixelPoint(10, 20), new Size(300, 200));

        Assert.Same(keep, layout.Root);
        Assert.Single(layout.Floats);
        Assert.Same(moving, host.Child);
        Assert.Same(host, moving.Parent);
    }

    [Fact]
    public void AFloatWhoseChildLeavesIsClosedAndDropped()
    {
        var target = Leaf("a");
        var layout = LayoutWith(target);
        var moving = Leaf("b");
        DockMutator.DockToRoot(layout, moving, DockDirection.Right);
        var host = DockMutator.Float(layout, moving, default, new Size(300, 200));

        DockMutator.Raft(layout, host, target, DockDirection.Center);

        Assert.Empty(layout.Floats);
        var tabs = Assert.IsType<DockTabPane>(layout.Root);
        Assert.Equal(2, tabs.Children.Count);
    }

    [Fact]
    public void ReorderingMovesATabWithinItsStrip()
    {
        var a = Leaf("a");
        var b = Leaf("b");
        var c = Leaf("c");
        var tabs = new DockTabPane(a, b, c);
        var layout = LayoutWith(tabs);

        DockMutator.Reorder(layout, c, 0);

        Assert.Equal(new IDockNode[] { c, a, b }, tabs.Children);
    }

    [Fact]
    public void DuplicatingSharesTheContentInstanceButNotTheNodeIdentity()
    {
        var document = new object();
        var original = new DockContent(document) { ContentKey = "doc", Title = "Doc" };
        var layout = LayoutWith(original);

        var copy = DockMutator.Duplicate(layout, original, original, DockDirection.Center);

        Assert.Same(document, copy.Content);
        Assert.Equal("doc", copy.ContentKey);
        Assert.NotEqual(original.Id, copy.Id);
        var tabs = Assert.IsType<DockTabPane>(layout.Root);
        Assert.Equal(new IDockNode[] { original, copy }, tabs.Children);
    }

    [Fact]
    public void OuterDockingSplitsTheRootRatherThanTheHoveredPane()
    {
        var left = Leaf("a");
        var right = Leaf("b");
        var layout = LayoutWith(new DockSplitPane(Orientation.Horizontal, left, right));
        var node = Leaf("c");

        DockMutator.DockToRoot(layout, node, DockDirection.Bottom, 0.3);

        var outer = Assert.IsType<DockSplitPane>(layout.Root);
        Assert.Equal(Orientation.Vertical, outer.Orientation);
        Assert.IsType<DockSplitPane>(outer.First);
        Assert.Same(node, outer.Second);
        Assert.Equal(0.7, outer.Ratio, 6);
    }

    [Fact]
    public void RatioNeverReachesZeroSoAPaneCannotBeDraggedOutOfExistence()
    {
        var split = new DockSplitPane(Orientation.Horizontal, Leaf("a"), Leaf("b"));

        split.Ratio = -5;
        Assert.True(split.Ratio > 0);

        split.Ratio = 5;
        Assert.True(split.Ratio < 1);
    }

    [Fact]
    public void RemovingTheMaximizedPaneClearsMaximizeState()
    {
        var keep = Leaf("a");
        var maximized = Leaf("b");
        var layout = LayoutWith(new DockSplitPane(Orientation.Horizontal, keep, maximized));
        layout.MaximizedPane = maximized;

        DockMutator.Remove(layout, maximized);

        Assert.Null(layout.MaximizedPane);
    }

    /// <summary>
    /// A persistent pane is a region the user arranged, so closing everything
    /// in it leaves the region (§6.1).
    /// </summary>
    [Fact]
    public void EmptyingAPersistentTabPaneLeavesItWhereItWas()
    {
        var keep = Leaf("a");
        var only = Leaf("b");
        var tabs = new DockTabPane(only) { Group = "Documents", IsPersistent = true };
        var layout = LayoutWith(new DockSplitPane(Orientation.Vertical, keep, tabs));

        DockMutator.Remove(layout, only);

        var split = Assert.IsType<DockSplitPane>(layout.Root);
        Assert.Same(tabs, split.Second);
        Assert.Empty(tabs.Children);
    }

    /// <summary>Persistence survives being emptied, so the next member joins the same pane.</summary>
    [Fact]
    public void AnEmptiedPersistentPaneStillAcceptsTheNextMember()
    {
        var only = Leaf("b");
        var tabs = new DockTabPane(only) { Group = "Documents", IsPersistent = true };
        var layout = LayoutWith(new DockSplitPane(Orientation.Vertical, Leaf("a"), tabs));

        DockMutator.Remove(layout, only);

        var opened = Leaf("c");
        DockPlacement.Place(layout, new DockActivation(), opened, "Documents", Array.Empty<Descriptors.DockGroup>());

        Assert.Same(tabs, opened.Parent);
    }

    /// <summary>Removing the pane itself is not the same act as emptying it.</summary>
    [Fact]
    public void RemovingAPersistentPaneOutrightStillCollapsesTheSplit()
    {
        var keep = Leaf("a");
        var tabs = new DockTabPane(Leaf("b")) { Group = "Documents", IsPersistent = true };
        var layout = LayoutWith(new DockSplitPane(Orientation.Vertical, keep, tabs));

        DockMutator.Remove(layout, tabs);

        Assert.Same(keep, layout.Root);
    }

    /// <summary>
    /// Merging a persistent pane into another carries the flag with the tabs,
    /// as the group name already travels: the region is where its tabs went.
    /// </summary>
    [Fact]
    public void MergingAPersistentPaneCarriesPersistenceToWhereItsTabsLanded()
    {
        var target = new DockTabPane(Leaf("a"));
        var moving = new DockTabPane(Leaf("b")) { Group = "Documents", IsPersistent = true };
        var layout = LayoutWith(new DockSplitPane(Orientation.Horizontal, target, moving));

        DockMutator.Dock(layout, moving, target, DockDirection.Center);

        Assert.True(target.IsPersistent);
        Assert.Equal("Documents", target.Group);
    }
}
