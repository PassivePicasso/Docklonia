using Avalonia;
using Avalonia.Controls;
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

    /// <summary>Splits <paramref name="key"/> out of the root tab pane into its own leaf.</summary>
    private static void SplitOut(Dock dock, string key)
    {
        var tabs = (DockTabPane)dock.Layout!.Root!;
        DockMutator.Dock(dock.Layout, Content(dock, key), tabs, DockDirection.Right);
        dock.NotifyLayoutChanged();
        Flush();
    }
}
