using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Docklonia.Controls;
using Docklonia.Descriptors;
using Docklonia.Model;
using Xunit;
using Dock = Docklonia.Controls.Dock;

namespace Docklonia.Tests;

/// <summary>
/// Closing a pane, as against closing the tabs in it one at a time (§3.10).
/// </summary>
/// <remarks>
/// The two have to mean the same thing. A pane that took its subtree out
/// wholesale left every item in the consumer's collection with no node: the
/// item is still open as far as the application is concerned, so asking to open
/// it again finds it and shows nothing, and there is no gesture that recovers
/// it.
/// </remarks>
public class ClosePaneTests
{
    /// <summary>A document that closes by leaving the collection, as a shell view model does.</summary>
    private sealed class Doc
    {
        public Doc(string key, ObservableCollection<object> items, bool closes = true)
        {
            Key = key;
            CloseCommand = new Relay(() =>
            {
                if (closes)
                {
                    items.Remove(this);
                }
            });
        }

        public string Key { get; }

        public ICommand CloseCommand { get; }

        public override string ToString() => Key;
    }

    private sealed class Tool
    {
        public Tool(string key) => Key = key;

        public string Key { get; }
    }

    private sealed class Relay : ICommand
    {
        private readonly Action _run;

        public Relay(Action run) => _run = run;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _run();
    }

    private const string Documents = "Documents";

    private static void Flush()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static (Dock Dock, ObservableCollection<object> Items) Build()
    {
        var dock = new Dock();

        dock.ItemDescriptors.Add(new DockItemDescriptor
        {
            DataType = typeof(Doc),
            Title = new Avalonia.Data.Binding(nameof(Doc.Key)),
            ContentKey = new Avalonia.Data.Binding(nameof(Doc.Key)),
            CloseCommand = new Avalonia.Data.Binding(nameof(Doc.CloseCommand)),
            Group = Documents,
        });

        dock.ItemDescriptors.Add(new DockItemDescriptor
        {
            DataType = typeof(Tool),
            Title = new Avalonia.Data.Binding(nameof(Tool.Key)),
            ContentKey = new Avalonia.Data.Binding(nameof(Tool.Key)),
            Group = "Explorers",
        });

        dock.Groups.Add(new DockGroup
        {
            Name = Documents,
            Seed = DockDirection.Right,
            SeedSize = 0.8,
            IsPersistent = true,
        });

        dock.Groups.Add(new DockGroup { Name = "Explorers", Seed = DockDirection.Left, SeedSize = 0.2 });

        var items = new ObservableCollection<object>();
        dock.ItemsSource = items;

        var window = new Window { Width = 1200, Height = 800, Content = dock };
        window.Show();
        Flush();

        return (dock, items);
    }

    private static DockTabPane DocumentPane(Dock dock)
        => dock.Layout!.AllPanes().OfType<DockTabPane>().Single(pane => pane.Group == Documents);

    private static bool HasNode(Dock dock, object item)
        => dock.Layout!.AllPanes().OfType<DockContent>().Any(node => ReferenceEquals(node.Content, item));

    /// <summary>Closing the pane is closing each of its documents, through the consumer.</summary>
    [AvaloniaFact]
    public void ClosingAPaneClosesItsContentsThroughTheConsumer()
    {
        var (dock, items) = Build();

        var one = new Doc("one", items);
        var two = new Doc("two", items);

        items.Add(one);
        items.Add(two);
        items.Add(new Tool("explorer"));
        Flush();

        dock.Commands.RequestClose(DocumentPane(dock));
        Flush();

        Assert.DoesNotContain(one, items);
        Assert.DoesNotContain(two, items);
    }

    /// <summary>
    /// The defect this covers: an item still in the collection with no node is
    /// an item the application believes is open and the user cannot reach.
    /// </summary>
    [AvaloniaFact]
    public void NothingIsLeftInTheCollectionWithoutAView()
    {
        var (dock, items) = Build();

        items.Add(new Doc("one", items));
        items.Add(new Doc("two", items));
        items.Add(new Tool("explorer"));
        Flush();

        dock.Commands.RequestClose(DocumentPane(dock));
        Flush();

        Assert.All(items, item => Assert.True(HasNode(dock, item), $"{item} has no view"));
    }

    /// <summary>A document that declines keeps its tab, and the pane keeps it.</summary>
    [AvaloniaFact]
    public void ADocumentThatDeclinesKeepsItsTabAndItsPane()
    {
        var (dock, items) = Build();

        var closing = new Doc("closes", items);
        var declining = new Doc("declines", items, closes: false);

        items.Add(closing);
        items.Add(declining);
        Flush();

        dock.Commands.RequestClose(DocumentPane(dock));
        Flush();

        Assert.DoesNotContain(closing, items);
        Assert.Contains(declining, items);
        Assert.True(HasNode(dock, declining), "the tab that declined should still be there");
    }

    /// <summary>Closing every document leaves the region it was arranged into.</summary>
    [AvaloniaFact]
    public void ClosingEveryDocumentLeavesThePersistentPaneBehind()
    {
        var (dock, items) = Build();

        var one = new Doc("one", items);

        items.Add(one);
        items.Add(new Tool("explorer"));
        Flush();

        var pane = DocumentPane(dock);
        one.CloseCommand.Execute(null);
        Flush();

        Assert.Empty(pane.Children);
        Assert.Same(pane, DocumentPane(dock));
    }

    /// <summary>
    /// An emptied pane still draws: a titlebar, no tabs, no content.
    /// </summary>
    /// <remarks>
    /// A pane with no selection used to fall back to presenting itself, which
    /// no pane could reach while every pane had a child. Emptying one turns
    /// that into unbounded recursion the moment it is realized.
    /// </remarks>
    [AvaloniaFact]
    public void AnEmptiedPersistentPaneStillDraws()
    {
        var (dock, items) = Build();

        var one = new Doc("one", items);

        items.Add(one);
        Flush();

        one.CloseCommand.Execute(null);
        Flush();

        var control = dock.PaneControls.Single(pane => ReferenceEquals(pane.Node, DocumentPane(dock)));

        Assert.Empty(control.Tabs);
    }

    /// <summary>
    /// A persistent pane down to one tab still shows its strip.
    /// </summary>
    /// <remarks>
    /// The strip is hidden at a single tab because the titlebar already shows
    /// the title. On a persistent pane the titlebar's close removes the region
    /// and the tab's closes the document, so hiding the strip leaves the user
    /// one button meaning whichever of the two they did not want.
    /// </remarks>
    [AvaloniaFact]
    public void APersistentPaneKeepsItsStripDownToOneTab()
    {
        var (dock, items) = Build();

        items.Add(new Doc("one", items));
        items.Add(new Tool("explorer"));
        Flush();

        var documents = Control(dock, DocumentPane(dock));
        var tools = Control(dock, dock.Layout!.AllPanes().OfType<DockTabPane>().Single(pane => pane.Group == "Explorers"));

        Assert.True(((IPseudoClasses)documents.Classes).Contains(":persistent"));
        Assert.True(Strip(documents).IsVisible, "the region's own tab must stay reachable");
        Assert.False(Strip(tools).IsVisible, "an ordinary single-tab pane is unchanged");
    }

    private static DockPaneControl Control(Dock dock, IDockNode node)
        => dock.PaneControls.Single(pane => ReferenceEquals(pane.Node, node));

    private static Control Strip(DockPaneControl pane)
        => pane.GetVisualDescendants().OfType<DockTabStripPanel>().Single();

    /// <summary>And the next document opens back into it.</summary>
    [AvaloniaFact]
    public void ADocumentOpenedAfterwardsReturnsToThatPane()
    {
        var (dock, items) = Build();

        var one = new Doc("one", items);

        items.Add(one);
        items.Add(new Tool("explorer"));
        Flush();

        var pane = DocumentPane(dock);
        one.CloseCommand.Execute(null);
        Flush();

        var again = new Doc("one", items);
        items.Insert(0, again);
        Flush();

        Assert.Same(pane, DocumentPane(dock));
        Assert.True(HasNode(dock, again));
    }

    /// <summary>
    /// Persistence is about emptying, not about permanence: an explicit close
    /// takes the pane away, and opening a document seeds a new one.
    /// </summary>
    [AvaloniaFact]
    public void ClosingThePaneExplicitlyTakesItAwayAndOpeningSeedsANewOne()
    {
        var (dock, items) = Build();

        items.Add(new Doc("one", items));
        items.Add(new Tool("explorer"));
        Flush();

        dock.Commands.RequestClose(DocumentPane(dock));
        Flush();

        Assert.DoesNotContain(dock.Layout!.AllPanes().OfType<DockTabPane>(), pane => pane.Group == Documents);

        var opened = new Doc("two", items);
        items.Insert(0, opened);
        Flush();

        Assert.True(HasNode(dock, opened), "opening a document should seed the group's pane again");
        Assert.True(DocumentPane(dock).IsPersistent, "and the pane it seeds is persistent as the group declares");
    }
}
