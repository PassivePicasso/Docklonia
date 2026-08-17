using Avalonia.Layout;
using Docklonia.Descriptors;
using Docklonia.Model;
using Docklonia.Model.Mutations;
using Xunit;

namespace Docklonia.Tests;

public class PlacementTests
{
    private static readonly IReadOnlyList<DockGroup> Groups = new[]
    {
        new DockGroup { Name = "Tools", Seed = DockDirection.Right, SeedSize = 0.25 },
        new DockGroup { Name = "Output", Seed = DockDirection.Bottom, SeedSize = 0.3 },
    };

    private static DockContent Leaf(string key) => new() { ContentKey = key, Title = key };

    [Fact]
    public void AGroupsFirstMemberSeedsAPaneAgainstTheRoot()
    {
        var layout = new DockLayout { Root = new DockTabPane(Leaf("doc")) };
        var activation = new DockActivation();

        DockPlacement.Place(layout, activation, Leaf("inspector"), "Tools", Groups);

        var split = Assert.IsType<DockSplitPane>(layout.Root);
        Assert.Equal(Orientation.Horizontal, split.Orientation);
        Assert.Equal("Tools", Assert.IsType<DockTabPane>(split.Second).Group);
        Assert.Equal(0.75, split.Ratio, 6);
    }

    [Fact]
    public void LaterMembersJoinTheGroupsPaneWhereverTheUserMovedIt()
    {
        var documents = new DockTabPane(Leaf("doc"));
        var layout = new DockLayout { Root = documents };
        var activation = new DockActivation();

        DockPlacement.Place(layout, activation, Leaf("inspector"), "Tools", Groups);
        var tools = layout.AllPanes().OfType<DockTabPane>().Single(pane => pane.Group == "Tools");

        // The user drags the tool pane to the opposite edge; the seed must not be reconsulted.
        DockMutator.DockToRoot(layout, tools, DockDirection.Left, 0.2);

        DockPlacement.Place(layout, activation, Leaf("outline"), "Tools", Groups);

        Assert.Same(tools, layout.AllPanes().OfType<DockTabPane>().Single(pane => pane.Group == "Tools"));
        Assert.Equal(2, tools.Children.Count);
        Assert.Same(tools, Assert.IsType<DockSplitPane>(layout.Root).First);
    }

    [Fact]
    public void AnUngroupedItemOpensInTheLastActivePaneHoldingUngroupedContent()
    {
        var documents = new DockTabPane(Leaf("doc"));
        var tools = new DockTabPane(Leaf("inspector")) { Group = "Tools" };
        var layout = new DockLayout { Root = new DockSplitPane(Orientation.Horizontal, documents, tools) };

        var activation = new DockActivation();
        activation.Activate(documents);
        activation.Activate(tools); // Focus the Inspector, then open a file.

        DockPlacement.Place(layout, activation, Leaf("new"), null, Groups);

        Assert.Equal(2, documents.Children.Count);
        Assert.Single(tools.Children);
    }

    [Fact]
    public void TheUngroupedPaneTakesTheSideASeededGroupSeededAwayFrom()
    {
        var layout = new DockLayout();
        var activation = new DockActivation();

        DockPlacement.Place(layout, activation, Leaf("inspector"), "Tools", Groups);
        DockPlacement.Place(layout, activation, Leaf("doc"), null, Groups);

        var split = Assert.IsType<DockSplitPane>(layout.Root);
        Assert.Null(Assert.IsType<DockTabPane>(split.First).Group);
        Assert.Equal("Tools", Assert.IsType<DockTabPane>(split.Second).Group);
        Assert.Equal(0.75, split.Ratio, 6);
    }

    [Fact]
    public void AutoHideRestoresToItsAnchorRatherThanItsSeed()
    {
        var documents = new DockTabPane(Leaf("doc"));
        var tools = new DockTabPane(Leaf("inspector")) { Group = "Tools" };

        // Tools sits on the LEFT, which is the opposite of its seed.
        var layout = new DockLayout { Root = new DockSplitPane(Orientation.Horizontal, tools, documents, 0.3) };

        var entry = AutoHideOperations.Hide(layout, tools, DockEdge.Left);
        Assert.Same(documents, layout.Root);
        Assert.Equal(documents.Id, entry.AnchorId);

        AutoHideOperations.Restore(layout, entry, Groups);

        var split = Assert.IsType<DockSplitPane>(layout.Root);
        Assert.Same(tools, split.First);
        Assert.Same(documents, split.Second);
        Assert.Equal(0.3, split.Ratio, 6);
        Assert.Empty(layout.AutoHidden);
    }

    [Fact]
    public void AutoHideFallsBackToTheSeedWhenItsAnchorIsGone()
    {
        var documents = new DockTabPane(Leaf("doc"));
        var tools = new DockTabPane(Leaf("inspector")) { Group = "Tools" };
        var layout = new DockLayout { Root = new DockSplitPane(Orientation.Horizontal, tools, documents, 0.3) };

        var entry = AutoHideOperations.Hide(layout, tools, DockEdge.Left);

        // The anchor is closed while the pane sits hidden — a path would now be invalid.
        DockMutator.Remove(layout, documents);
        var replacement = new DockTabPane(Leaf("other"));
        DockMutator.DockToRoot(layout, replacement, DockDirection.Center);

        AutoHideOperations.Restore(layout, entry, Groups);

        var split = Assert.IsType<DockSplitPane>(layout.Root);
        Assert.Same(tools, split.Second);
        Assert.Equal(0.75, split.Ratio, 6);
    }

    [Fact]
    public void ActivationSelectsThroughEveryAncestorSoAnActivePaneIsAlwaysVisible()
    {
        var buried = Leaf("buried");
        var inner = new DockTabPane(Leaf("visible"), buried);
        var outer = new DockTabPane(inner, Leaf("other"));
        var layout = new DockLayout { Root = outer };

        outer.SelectedChild = outer.Children[1];
        inner.SelectedChild = inner.Children[0];

        new DockActivation().Activate(buried);

        Assert.Same(buried, inner.SelectedChild);
        Assert.Same(inner, outer.SelectedChild);
    }

    [Fact]
    public void ProgrammaticSelectionDoesNotChangeActivation()
    {
        var first = Leaf("a");
        var second = Leaf("b");
        var tabs = new DockTabPane(first, second);
        var layout = new DockLayout { Root = tabs };

        var activation = new DockActivation();
        activation.Activate(first);

        tabs.SelectedChild = second;

        Assert.Same(first, activation.Current);
    }

    [Fact]
    public void ClosingAPaneYieldsADeterministicNextFocusTarget()
    {
        var keep = new DockTabPane(Leaf("a"));
        var closing = new DockTabPane(Leaf("b"));
        var layout = new DockLayout { Root = new DockSplitPane(Orientation.Horizontal, keep, closing) };

        var activation = new DockActivation();
        activation.Activate(keep);
        activation.Activate(closing);

        Assert.Same(keep, activation.NextAfterClosing(layout, closing));
    }
}
