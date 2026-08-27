using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Docklonia.Controls;
using Dock = Docklonia.Controls.Dock;
using Docklonia.Descriptors;
using Docklonia.Model;
using Docklonia.Model.Mutations;
using Xunit;

namespace Docklonia.Tests;

/// <summary>
/// Focus routing around the auto-hide flyout (§5.3).
/// </summary>
/// <remarks>
/// <c>LostFocus</c> and <c>GotFocus</c> are bubbling routed events, so focus
/// movement between a container's own children reaches the container's focus
/// handlers too. The flyout is the focus anchor for its own slide-out: it is
/// focusable, and while it is open, focus lives on it or on a control inside
/// it. Interacting with the flyout's content therefore keeps it open, and
/// only focus that leaves the flyout's subtree dismisses it. The dock's own
/// activation restore (§3.11) must never contend with the flyout, or a
/// parked pane's slide-out would be dismissed the moment the user touched it.
/// </remarks>
public class AutoHideFlyoutFocusTests
{
    private sealed class Doc
    {
        public Doc(string key) => Key = key;

        public string Key { get; }
    }

    /// <summary>
    /// A pane whose content is a list of focusable rows over a replaceable
    /// source — the editor's file tree shape.
    /// </summary>
    private sealed class TreeRow
    {
        public TreeRow(params string[] items)
        {
            Key = items.Length > 0 ? items[0] : "row";
            Items = new System.Collections.Generic.List<string>(items);
        }

        public string Key { get; }
        public System.Collections.Generic.IList<string> Items { get; }
    }

    /// <summary>
    /// Builds each presentation its own list of focusable rows over the
    /// row's model — the editor's file tree shape.
    /// </summary>
    private sealed class TreeRowTemplate : IDataTemplate
    {
        public bool Match(object? data) => data is TreeRow;
        public Control? Build(object? data) => data is TreeRow row
            ? new ListBox { Width = 200, Height = 200, ItemsSource = new System.Collections.Generic.List<string>(row.Items) }
            : null;
    }

    /// <summary>
    /// A dock whose panes present <see cref="TreeRow"/> lists, one pane each.
    /// </summary>
    private static (Window Window, Dock Dock) HostTree(params TreeRow[] rows)
    {
        var dock = new Dock();
        dock.ItemDescriptors.Add(new DockItemDescriptor
        {
            DataType = typeof(TreeRow),
            Title = new Avalonia.Data.Binding(nameof(TreeRow.Key)),
            ContentKey = new Avalonia.Data.Binding(nameof(TreeRow.Key)),
        });
        dock.DataTemplates.Add(new TreeRowTemplate());
        dock.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(rows);
        var window = Host(dock, new Size(800, 600));
        return (window, dock);
    }

    private static DockContent ContentFor(Dock dock, object data)
        => dock.Layout!.AllPanes().OfType<DockContent>().Single(node => ReferenceEquals(node.Content, data));

    private static AutoHideEntry Park(Dock dock, DockContent content, DockEdge edge)
    {
        var entry = AutoHideOperations.Hide(dock.EnsureLayout(), content, edge);
        dock.NotifyLayoutChanged();
        Flush();
        return entry;
    }

    private static DockPaneControl PaneControlFor(Window window, Dock dock, object data)
    {
        var content = ContentFor(dock, data);
        return window.GetVisualDescendants().OfType<DockPaneControl>()
            .Single(control => ReferenceEquals(control.Node, content)
                || (control.Node as DockTabPane)?.Children.Contains(content) == true);
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

    /// <summary>
    /// Simulates the pointer press's focus transfer: the first focusable
    /// element at or above the press point, which is how a real click focuses.
    /// </summary>
    private static void Click(IInputElement element)
    {
        element.Focus(NavigationMethod.Pointer, KeyModifiers.None);
        Flush();
    }

    private static IInputElement? FocusOf(Window window) => window.FocusManager?.GetFocusedElement();

    private static DockContent Content(Dock dock, string key)
        => dock.Layout!.AllPanes().OfType<DockContent>().Single(node => node.ContentKey == key);

    /// <summary>Parks the pane on a chosen edge, so a test does not depend on where it sat.</summary>
    private static AutoHideEntry Park(Dock dock, string key, DockEdge edge)
    {
        var entry = AutoHideOperations.Hide(dock.EnsureLayout(), Content(dock, key), edge);
        dock.NotifyLayoutChanged();
        Flush();
        return entry;
    }

    private static DockAutoHideButton ButtonFor(Window window, AutoHideEntry entry)
        => window.GetVisualDescendants().OfType<DockAutoHideButton>().First(button => ReferenceEquals(button.Entry, entry));

    private static DockAutoHideFlyout FlyoutOf(Window window)
        => window.GetVisualDescendants().OfType<DockAutoHideFlyout>().Single(flyout => flyout.IsVisible);

    private static DockPaneControl PaneControlFor(Window window, Dock dock, string key)
    {
        var content = Content(dock, key);
        return window.GetVisualDescendants().OfType<DockPaneControl>()
            .Single(control => ReferenceEquals(control.Node, content)
                || (control.Node as DockTabPane)?.Children.Contains(content) == true);
    }

    private static DockPaneControl FlyoutPane(DockAutoHideFlyout flyout)
        => flyout.GetVisualDescendants().OfType<DockPaneControl>().First();

    /// <summary>
    /// Opens the parked pane's flyout, the way its strip button would.
    /// </summary>
    private static DockAutoHideFlyout Reveal(Window window, Dock dock, AutoHideEntry entry)
    {
        dock.ShowAutoHideFlyout(entry, ButtonFor(window, entry));
        Flush();
        return FlyoutOf(window);
    }

    [AvaloniaFact]
    public void OpeningTheFlyoutPutsFocusOnTheFlyout()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));

        var entry = Park(dock, "a", DockEdge.Left);
        var flyout = Reveal(window, dock, entry);

        // The flyout is the focus anchor for its own slide-out.
        Assert.True(flyout.IsFocused);
        Assert.Same(flyout, FocusOf(window));
    }

    [AvaloniaFact]
    public void InteractingWithTheFlyoutsContentDoesNotCloseIt()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));

        var entry = Park(dock, "a", DockEdge.Left);
        var flyout = Reveal(window, dock, entry);

        // First interaction: focus moves from the flyout to the pin inside it.
        var pane = FlyoutPane(flyout);
        var pin = pane.GetVisualDescendants().OfType<Button>().Single(button => button.Name == DockPaneControl.PinButtonPart);
        Click(pin);

        Assert.True(flyout.IsVisible);
        Assert.Same(pin, FocusOf(window));

        // Second interaction: focus moves between the flyout's own children.
        // This used to dismiss the flyout, because the bubbled LostFocus of the
        // child that lost focus reached the flyout's handler.
        Click(pane);

        Assert.True(flyout.IsVisible);
        Assert.Same(pane, FocusOf(window));
    }

    [AvaloniaFact]
    public void FocusLeavingTheFlyoutClosesIt()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));

        var entry = Park(dock, "a", DockEdge.Left);
        var flyout = Reveal(window, dock, entry);
        var other = PaneControlFor(window, dock, "b");

        // The user's attention moves to something that is not inside the flyout.
        Click(other);

        Assert.False(flyout.IsVisible);
        Assert.Same(other, FocusOf(window));

        // Dismissal parks the pane; it does not restore it.
        Assert.NotNull(dock.Layout!.AutoHideOf(Content(dock, "a")));
        Assert.NotNull(ButtonFor(window, entry));
    }

    [AvaloniaFact]
    public void APressOnTheFlyoutsOwnChromeKeepsItOpen()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));

        var entry = Park(dock, "a", DockEdge.Left);
        var flyout = Reveal(window, dock, entry);

        // A pointer press on the flyout's non-focusable chrome resolves to the
        // first focusable ancestor, which is now the flyout itself.
        var pane = FlyoutPane(flyout);
        Click(pane);
        Click(flyout);

        Assert.True(flyout.IsVisible);
        Assert.Same(flyout, FocusOf(window));
    }

    [AvaloniaFact]
    public void TheActiveDockedPaneDoesNotContendWithTheFlyout()
    {
        // The editor-shaped case: the active pane is a docked leaf with its own
        // pane control, and a different pane is parked. Opening the parked
        // pane's flyout and touching it must not yank focus to the active pane
        // (which would both dismiss the flyout and move the pointer).
        var dock = BuildDock(new Doc("a"), new Doc("b"), new Doc("c"));
        var window = Host(dock, new Size(1000, 600));

        SplitOut(dock, "c");
        dock.ActivateNode(Content(dock, "c"));
        var entry = Park(dock, "b", DockEdge.Left);
        var flyout = Reveal(window, dock, entry);
        var active = PaneControlFor(window, dock, "c");

        Click(FlyoutPane(flyout));

        Assert.True(flyout.IsVisible);
        Assert.Same(FlyoutPane(flyout), FocusOf(window));
        Assert.False(active.IsKeyboardFocusWithin);
    }

    [AvaloniaFact]
    public void FocusReturningToTheDockFromOutsideStillRestoresTheActivation()
    {
        // §3.11 keeps its full form: the restore fires when focus comes back
        // to the dock from outside, not for movement between its own children.
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var outside = new Button { Content = "outside" };
        var root = new DockPanel();
        root.Children.Add(outside);
        DockPanel.SetDock(outside, Avalonia.Controls.Dock.Top);
        root.Children.Add(dock);
        var window = new Window { Width = 800, Height = 600, Content = root };
        window.Show();
        Flush();

        SplitOut(dock, "b");
        dock.ActivateNode(Content(dock, "b"));

        Click(outside);
        Assert.Same(outside, FocusOf(window));

        var active = PaneControlFor(window, dock, "b");
        var other = PaneControlFor(window, dock, "a");

        Click(other);

        Assert.Same(active, FocusOf(window));
    }

    [AvaloniaFact]
    public void FocusMovingBetweenDockedPanesIsNotContested()
    {
        // Focus navigation between the dock's own children is the user's own;
        // the activation restore no longer fires for it, as it did not before
        // the events started bubbling.
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));

        SplitOut(dock, "b");
        dock.ActivateNode(Content(dock, "b"));

        var active = PaneControlFor(window, dock, "b");
        var other = PaneControlFor(window, dock, "a");

        // Establish focus inside the dock, then move it to another pane.
        Click(active);
        Assert.Same(active, FocusOf(window));

        Click(other);
        Assert.Same(other, FocusOf(window));
        Assert.False(active.IsKeyboardFocusWithin);
    }

    [AvaloniaFact]
    public void RebuildingTheFlyoutsContentWhileItHoldsFocusKeepsItOpen()
    {
        // A pane whose content is a list of focusable rows over a replaceable
        // source — the editor's file tree. Toggling a row replaces the whole
        // source, which tears down the realized row that holds focus. Focus is
        // cleared, not moved; the user is still interacting with the panel,
        // so the flyout must stay open.
        var rowA = new TreeRow("a", "b", "c");
        var (window, dock) = HostTree(rowA);

        var entry = Park(dock, ContentFor(dock, rowA), DockEdge.Left);
        var flyout = Reveal(window, dock, entry);

        // The click: focus moves from the flyout onto a realized row.
        var tree = flyout.GetVisualDescendants().OfType<ListBox>().First();
        var item = tree.GetVisualDescendants().OfType<ListBoxItem>().First();
        Click(item);
        Assert.Same(item, FocusOf(window));

        // The release: the toggle rebuilds the whole list, tearing down the
        // row that holds focus.
        tree.ItemsSource = null;
        tree.ItemsSource = new System.Collections.Generic.List<string> { "a1", "a2", "b", "c" };
        Flush();

        Assert.True(flyout.IsVisible, "a rebuild of the flyout's own content must not dismiss it");
    }

    [AvaloniaFact]
    public void AfterAClearingRebuildFocusLeavingTheFlyoutClosesIt()
    {
        // The follow-on of the previous test: the rebuild cleared focus. The
        // user's next real move of attention — to something outside the panel
        // — still dismisses it.
        var rowA = new TreeRow("a", "b", "c");
        var rowB = new TreeRow("x");
        var (window, dock) = HostTree(rowA, rowB);

        SplitOut(dock, ContentFor(dock, rowB));
        var entry = Park(dock, ContentFor(dock, rowA), DockEdge.Left);
        var flyout = Reveal(window, dock, entry);

        var item = flyout.GetVisualDescendants().OfType<ListBox>().First().GetVisualDescendants().OfType<ListBoxItem>().First();
        Click(item);

        var tree = flyout.GetVisualDescendants().OfType<ListBox>().First();
        tree.ItemsSource = null;
        tree.ItemsSource = new System.Collections.Generic.List<string> { "a1", "a2", "b", "c" };
        Flush();
        Assert.True(flyout.IsVisible);

        // Attention moves to the docked pane that is not the flyout's.
        var other = PaneControlFor(window, dock, rowB);
        Click(other);

        Assert.False(flyout.IsVisible, "focus that lands outside the panel dismisses it");
        Assert.Same(other, FocusOf(window));
    }

    [AvaloniaFact]
    public void ReClickingTheStripButtonClosesTheFlyout()
    {
        // The flyout is the focus anchor, so a second click on its strip
        // button first moves focus from the flyout onto the button. That is
        // the button's own gesture (the toggle), not a dismissal: the flyout
        // must close when the button's reveal then finds its entry already
        // open, not reopen over the dismissal.
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));

        var entry = Park(dock, "a", DockEdge.Left);
        var flyout = Reveal(window, dock, entry);
        Assert.Same(flyout, FocusOf(window));

        var button = ButtonFor(window, entry);

        // The press's focus transfer: flyout -> button, the button of the
        // entry being shown.
        Click(button);
        Assert.Same(button, FocusOf(window));
        Assert.True(flyout.IsVisible, "the button's own gesture must not be treated as leaving the panel");

        // The press completes: the button reveals its entry, which is already
        // open, so the toggle closes it.
        dock.ShowAutoHideFlyout(entry, button);
        Flush();

        Assert.False(flyout.IsVisible, "re-clicking the strip button toggles the flyout shut");
        // The rebuild swaps the clicked button for a new instance; the
        // pointer's focus must ride onto the replacement, not die with it.
        Assert.Same(ButtonFor(window, entry), FocusOf(window));
    }

    [AvaloniaFact]
    public void ClickingAnotherParkedPanesButtonSwapsTheFlyout()
    {
        // The strip button of a different parked pane: the flyout shows that
        // pane's content instead; neither the focus guard nor the press guard
        // may close the flyout that the button's reveal just opened.
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));

        var entryA = Park(dock, "a", DockEdge.Left);
        var entryB = Park(dock, "b", DockEdge.Right);
        var flyout = Reveal(window, dock, entryA);
        Assert.Same(flyout, FocusOf(window));

        var buttonB = ButtonFor(window, entryB);

        // The press's focus transfer lands on the other entry's button; the
        // flyout of the first entry closes, and the reveal opens this one.
        Click(buttonB);

        // The press completes: that button's reveal switches the flyout, and
        // the slide-out re-anchors focus on itself.
        dock.ShowAutoHideFlyout(entryB, buttonB);
        Flush();

        Assert.Same(flyout, FlyoutOf(window));
        Assert.True(flyout.IsVisible);
        Assert.Same(flyout, FocusOf(window));
    }

    /// <summary>Splits <paramref name="content"/> out of the root tab pane into its own leaf.</summary>
    private static void SplitOut(Dock dock, DockContent content)
    {
        var tabs = (DockTabPane)dock.Layout!.Root!;
        DockMutator.Dock(dock.Layout, content, tabs, DockDirection.Right);
        dock.NotifyLayoutChanged();
        Flush();
    }

    /// <summary>Splits the pane of <paramref name="key"/> out of the root tab pane into its own leaf.</summary>
    private static void SplitOut(Dock dock, string key) => SplitOut(dock, Content(dock, key));
}
