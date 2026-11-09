using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Docklonia.Controls;
using Docklonia.Descriptors;
using Docklonia.Model;
using Xunit;
using Dock = Docklonia.Controls.Dock;

namespace Docklonia.Tests;

/// <summary>
/// A loaded node whose item has not arrived holds the item's place, hidden,
/// until the item claims it (§8).
/// </summary>
/// <remarks>
/// An item can arrive after its window is built: a plugin's panel comes with
/// a workspace the window opens later. Dropped, the node's place was gone, and
/// the next save wrote the layout without it.
/// </remarks>
public class PlaceholderTests
{
    private sealed class Doc
    {
        public Doc(string key) => Key = key;

        public string Key { get; }
    }

    [AvaloniaFact]
    public void AnItemArrivingLaterIsPlacedWhereTheLayoutSaidAndUntilThenTakesNoRoom()
    {
        var saved = Saved(out var lateId);
        var docs = new ObservableCollection<object> { new Doc("early") };
        var dock = Showing(docs, saved);

        var placeholder = Node(dock, "late");

        Assert.NotNull(placeholder);
        Assert.Null(placeholder!.Content);
        Assert.False(PresenterOf(dock, placeholder.Parent!).IsVisible, "a pane of placeholders is hidden");
        Assert.Equal(dock.Bounds.Width, PresenterOf(dock, Node(dock, "early")!.Parent!).Bounds.Width, 0);

        var late = new Doc("late");
        docs.Add(late);
        Flush();

        var claimed = Node(dock, "late");

        Assert.Same(late, claimed!.Content);
        Assert.Equal(lateId, claimed.Id);
        Assert.True(PresenterOf(dock, claimed.Parent!).IsVisible, "and shown once its item is there");
        Assert.Single(dock.Layout!.AllPanes().OfType<DockContent>(), node => node.ContentKey == "late");
    }

    [AvaloniaFact]
    public void ALayoutSavedBeforeTheItemArrivedStillPlacesIt()
    {
        var first = Showing(new ObservableCollection<object> { new Doc("early") }, Saved(out var lateId));

        var resaved = first.Layout!.ToJson();

        var late = new Doc("late");
        var second = Showing(new ObservableCollection<object> { new Doc("early"), late }, resaved);

        var node = Node(second, "late");

        Assert.Same(late, node!.Content);
        Assert.Equal(lateId, node.Id);
    }

    [AvaloniaFact]
    public void APlaceholderBesideShownTabsHasNoTab()
    {
        var tabs = new DockTabPane(Leaf("early"), Leaf("late"));
        var dock = Showing(
            new ObservableCollection<object> { new Doc("early") },
            new DockLayout { Root = tabs }.ToJson());

        var pane = dock.GetVisualDescendants().OfType<DockPaneControl>().Single();

        Assert.Single(pane.Tabs);
        Assert.Equal("early", ((DockContent)pane.Tabs[0].Node!).ContentKey);
    }

    [AvaloniaFact]
    public void AFloatOfPlaceholdersOpensNoWindowUntilItsItemArrives()
    {
        var layout = new DockLayout { Root = new DockTabPane(Leaf("early")) };

        layout.Floats.Add(new FloatPane(Leaf("late"), new Avalonia.PixelPoint(40, 40), new Avalonia.Size(300, 200)));

        var docs = new ObservableCollection<object> { new Doc("early") };
        var dock = Showing(docs, layout.ToJson());

        Assert.Empty(dock.FloatSurfaces);
        Assert.Single(dock.Layout!.Floats);

        docs.Add(new Doc("late"));
        Flush();

        Assert.Single(dock.FloatSurfaces);
    }

    /// <summary>Two regions side by side, "early" on the left and "late" on the right.</summary>
    private static string Saved(out string lateId)
    {
        var late = Leaf("late");

        lateId = late.Id;

        var root = new DockSplitPane(
            Orientation.Horizontal, new DockTabPane(Leaf("early")), new DockTabPane(late), 0.3);

        return new DockLayout { Root = root }.ToJson();
    }

    private static DockContent Leaf(string key) => new() { ContentKey = key };

    private static DockContent? Node(Dock dock, string key)
        => dock.Layout!.AllPanes().OfType<DockContent>().SingleOrDefault(node => node.ContentKey == key);

    private static DockPanePresenter PresenterOf(Dock dock, IDockPane pane)
        => dock.GetVisualDescendants().OfType<DockPanePresenter>().Single(p => ReferenceEquals(p.Pane, pane));

    /// <summary>A dock in a shown window over these documents, in this saved layout.</summary>
    private static Dock Showing(ObservableCollection<object> documents, string layout)
    {
        var dock = new Dock { ItemDescriptors = Described() };

        new Window { Width = 800, Height = 600, Content = dock }.Show();
        Flush();

        dock.ItemsSource = documents;
        dock.Layout = DockLayout.FromJson(layout);
        Flush();

        return dock;
    }

    private static DockItemDescriptors Described()
        => new DockItemDescriptors
        {
            new DockItemDescriptor
            {
                DataType = typeof(Doc),
                Title = new Avalonia.Data.Binding(nameof(Doc.Key)),
                ContentKey = new Avalonia.Data.Binding(nameof(Doc.Key)),
            },
        };

    /// <summary>Headless has no render loop, so layout is driven here.</summary>
    private static void Flush()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }
}
