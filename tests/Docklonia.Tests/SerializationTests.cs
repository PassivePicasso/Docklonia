using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Docklonia.Model;
using Docklonia.Serialization;
using Xunit;

namespace Docklonia.Tests;

public class SerializationTests
{
    private static DockContent Leaf(string key) => new() { ContentKey = key };

    /// <summary>A layout exercising every surface: docked, floating, and auto-hidden together (§8).</summary>
    private static DockLayout SampleLayout()
    {
        var documents = new DockTabPane(Leaf("doc-a"), Leaf("doc-b")) { Group = null };
        documents.SelectedChild = documents.Children[1];

        var tools = new DockTabPane(Leaf("inspector")) { Group = "Tools" };
        var root = new DockSplitPane(Orientation.Horizontal, documents, tools, 0.7);

        var layout = new DockLayout { Root = root };

        var floated = new DockTabPane(Leaf("terminal"));
        layout.Floats.Add(new FloatPane(floated, new PixelPoint(120, 240), new Size(640, 480))
        {
            WindowState = WindowState.Maximized,
        });

        var hidden = new DockTabPane(Leaf("outline")) { Group = "Tools" };
        layout.AutoHidden.Add(new AutoHideEntry(hidden, DockEdge.Bottom, tools.Id, DockDirection.Top, 0.35));

        layout.MaximizedPane = documents;
        layout.ActivePane = documents;

        return layout;
    }

    [Fact]
    public void RoundTripProducesAStructurallyIdenticalTree()
    {
        var original = SampleLayout();

        var restored = DockLayout.FromJson(original.ToJson());

        Assert.Equal(original.ToJson(), restored.ToJson());
    }

    [Fact]
    public void RoundTripPreservesEverySurfaceInOneDocument()
    {
        var restored = DockLayout.FromJson(SampleLayout().ToJson());

        var root = Assert.IsType<DockSplitPane>(restored.Root);
        Assert.Equal(Orientation.Horizontal, root.Orientation);
        Assert.Equal(0.7, root.Ratio, 6);

        var documents = Assert.IsType<DockTabPane>(root.First);
        Assert.Equal("doc-b", Assert.IsType<DockContent>(documents.SelectedChild).ContentKey);

        var tools = Assert.IsType<DockTabPane>(root.Second);
        Assert.Equal("Tools", tools.Group);

        var host = Assert.Single(restored.Floats);
        Assert.Equal(new PixelPoint(120, 240), host.Position);
        Assert.Equal(new Size(640, 480), host.Size);
        Assert.Equal(WindowState.Maximized, host.WindowState);

        var hidden = Assert.Single(restored.AutoHidden);
        Assert.Equal(DockEdge.Bottom, hidden.Edge);
        Assert.Equal(DockDirection.Top, hidden.AnchorDirection);
        Assert.Equal(tools.Id, hidden.AnchorId);
        Assert.Equal(0.35, hidden.Ratio, 6);

        Assert.Same(documents, restored.MaximizedPane);
        Assert.Same(documents, restored.ActivePane);
    }

    [Fact]
    public void NodeIdsSurviveSoAutoHideAnchorsStayResolvable()
    {
        var original = SampleLayout();
        var anchorId = original.AutoHidden[0].AnchorId;

        var restored = DockLayout.FromJson(original.ToJson());

        Assert.NotNull(anchorId);
        Assert.NotNull(DockTree.FindById(restored.Root, anchorId!));
    }

    [Fact]
    public void DuplicatedTabsKeepOneKeyAcrossTwoNodesSoTheyRehydrateTogether()
    {
        var tabs = new DockTabPane(Leaf("shared"), Leaf("shared"));
        var layout = new DockLayout { Root = tabs };

        var restored = DockLayout.FromJson(layout.ToJson());

        var group = Assert.IsType<DockTabPane>(restored.Root);
        var keys = group.Children.Cast<DockContent>().Select(content => content.ContentKey).ToArray();
        var ids = group.Children.Select(child => child.Id).ToArray();

        Assert.Equal(new[] { "shared", "shared" }, keys);
        Assert.Equal(2, ids.Distinct().Count());
    }

    [Fact]
    public void ContentIsNeverFabricatedOnLoad()
    {
        var layout = new DockLayout { Root = new DockContent(new object()) { ContentKey = "k" } };

        var restored = DockLayout.FromJson(layout.ToJson());

        Assert.Null(Assert.IsType<DockContent>(restored.Root).Content);
    }

    [Fact]
    public void ADocumentFromANewerSchemaIsRejectedRatherThanPartiallyApplied()
    {
        var json = new DockLayout { Root = Leaf("a") }.ToJson()
            .Replace($"\"version\":{LayoutSchema.Version}", $"\"version\":{LayoutSchema.Version + 1}");

        var error = Assert.Throws<LayoutFormatException>(() => DockLayout.FromJson(json));
        Assert.Contains("schema version", error.Message);
    }

    [Fact]
    public void MalformedJsonRaisesALayoutFormatExceptionRatherThanEscapingAsJsonException()
    {
        Assert.Throws<LayoutFormatException>(() => DockLayout.FromJson("{ not json"));
    }
}
