using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Docklonia.Controls;
using Dock = Docklonia.Controls.Dock;
using Docklonia.Descriptors;
using Docklonia.Model;
using Xunit;

namespace Docklonia.Tests;

/// <summary>
/// The navigation operations of §11, and the key map over them.
/// </summary>
/// <remarks>
/// The operations are public so a host can bind its own shortcuts to them;
/// the default key map is a policy over the same operations, which
/// <c>HandlesKeys</c> turns off without taking them away.
/// </remarks>
public class DockKeyboardTests
{
    private sealed class Doc
    {
        public Doc(string key) => Key = key;

        public string Key { get; }
    }

    [AvaloniaFact]
    public void CyclingMostRecentlyUsedMovesToTheOtherPane()
    {
        var (window, dock) = HostSplit();
        var before = ActivePane(dock);

        Assert.True(dock.CycleMostRecentlyUsed(false));
        Settle(window);

        Assert.NotSame(before, ActivePane(dock));

        Assert.True(dock.CycleMostRecentlyUsed(true));
        Settle(window);

        Assert.Same(before, ActivePane(dock));
    }

    [AvaloniaFact]
    public void CyclingPanesMovesToTheOtherPane()
    {
        var (window, dock) = HostSplit();
        var before = ActivePane(dock);

        Assert.True(dock.CyclePanes(false));
        Settle(window);

        Assert.NotSame(before, ActivePane(dock));
    }

    /// <summary>One pane is nowhere to go, and the operation says so.</summary>
    [AvaloniaFact]
    public void CyclingASinglePaneDoesNothing()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        Host(dock, new Size(800, 600));

        Assert.False(dock.CycleMostRecentlyUsed(false));
    }

    [AvaloniaFact]
    public void SteppingTabsWalksTheActivePaneAndWraps()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));
        var tabs = (DockTabPane)dock.Layout!.Root!;

        dock.ActivateNode(tabs.Children[0]);
        Settle(window);

        Assert.True(dock.StepTab(1));
        Assert.Same(tabs.Children[1], tabs.SelectedChild);

        Assert.True(dock.StepTab(1));
        Assert.Same(tabs.Children[0], tabs.SelectedChild);

        Assert.True(dock.StepTab(-1));
        Assert.Same(tabs.Children[1], tabs.SelectedChild);
    }

    /// <summary>Traversal is geometric, so it matches what the user can see.</summary>
    [AvaloniaFact]
    public void TraversingRightReachesThePaneOnTheRight()
    {
        var (window, dock) = HostSplit();
        var left = LeftPane(dock);

        Assert.Same(left, ActivePane(dock));

        Assert.True(dock.Traverse(DockDirection.Right));
        Settle(window);
        Assert.NotSame(left, ActivePane(dock));

        Assert.True(dock.Traverse(DockDirection.Left));
        Settle(window);
        Assert.Same(left, ActivePane(dock));

        Assert.False(dock.Traverse(DockDirection.Left));
    }

    [AvaloniaFact]
    public void OpeningThePaneMenuNeedsAPane()
    {
        var (_, dock) = HostSplit();

        Assert.True(dock.OpenPaneMenu());

        var empty = new Dock();
        Host(empty, new Size(400, 300));

        Assert.False(empty.OpenPaneMenu());
    }

    [AvaloniaFact]
    public void TheKeysPerformTheSameOperations()
    {
        var (window, dock) = HostSplit();
        var left = LeftPane(dock);

        Assert.True(Press(dock, Key.Tab, KeyModifiers.Control));
        Settle(window);
        Assert.NotSame(left, ActivePane(dock));

        Assert.True(Press(dock, Key.F6, KeyModifiers.None));
        Settle(window);
        Assert.Same(left, ActivePane(dock));

        Assert.True(Press(dock, Key.Right, KeyModifiers.Alt));
        Settle(window);
        Assert.NotSame(left, ActivePane(dock));
    }

    [AvaloniaFact]
    public void TabKeysStepTheActivePane()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));
        var tabs = (DockTabPane)dock.Layout!.Root!;

        dock.ActivateNode(tabs.Children[0]);
        Settle(window);

        Assert.True(Press(dock, Key.PageDown, KeyModifiers.Control));
        Assert.Same(tabs.Children[1], tabs.SelectedChild);

        Assert.True(Press(dock, Key.PageUp, KeyModifiers.Control));
        Assert.Same(tabs.Children[0], tabs.SelectedChild);
    }

    /// <summary>
    /// A host that binds its own keys gets the press back unhandled, and still
    /// reaches the operation the key would have performed.
    /// </summary>
    [AvaloniaFact]
    public void AHostThatBindsItsOwnKeysIsLeftThePress()
    {
        var (window, dock) = HostSplit();
        dock.HandlesKeys = false;

        var before = ActivePane(dock);

        Assert.False(Press(dock, Key.Tab, KeyModifiers.Control));
        Settle(window);
        Assert.Same(before, ActivePane(dock));

        Assert.True(dock.CycleMostRecentlyUsed(false));
        Settle(window);
        Assert.NotSame(before, ActivePane(dock));
    }

    /// <summary>Cancelling a drag is the gesture's own, so it ignores the property.</summary>
    [AvaloniaFact]
    public void EscapeStillCancelsADragWhileKeysAreOff()
    {
        var (window, dock) = HostSplit();
        dock.HandlesKeys = false;

        dock.BeginExternalDrag(new Doc("c"), "c", new PixelPoint(10, 10));
        Settle(window);

        Assert.True(dock.Drag.IsDragging);
        Assert.True(Press(dock, Key.Escape, KeyModifiers.None));
        Assert.False(dock.Drag.IsDragging);
    }

    /// <summary>
    /// Raises the press the dock would see, and reports whether the dock itself
    /// took it — read at the dock, because a press left unhandled there goes on
    /// bubbling and the window's own navigation may take it instead.
    /// </summary>
    private static bool Press(Dock dock, Key key, KeyModifiers modifiers)
    {
        var handled = false;

        void Observe(object? sender, KeyEventArgs e) => handled = e.Handled;

        dock.AddHandler(InputElement.KeyDownEvent, Observe, handledEventsToo: true);

        dock.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
        });

        dock.RemoveHandler(InputElement.KeyDownEvent, (EventHandler<KeyEventArgs>)Observe);
        return handled;
    }

    private static DockPaneControl LeftPane(Dock dock)
        => dock.PaneControls.OrderBy(pane => pane.TranslatePoint(default, dock)?.X ?? 0).First();

    /// <summary>The pane the dock reports as active, which is what these operations move.</summary>
    private static DockPaneControl ActivePane(Dock dock) => dock.PaneControls.Single(pane => pane.IsActive);

    /// <summary>
    /// Two panes side by side, split through the mutation engine — the only
    /// supported route (§13).
    /// </summary>
    private static (Window Window, Dock Dock) HostSplit()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        var window = Host(dock, new Size(800, 600));

        var tabs = (DockTabPane)dock.Layout!.Root!;
        Model.Mutations.DockMutator.Dock(dock.Layout, tabs.Children[1], tabs, DockDirection.Right);
        dock.NotifyLayoutChanged();
        Settle(window);

        dock.ActivateNode(LeftPane(dock).Node!);
        Settle(window);

        return (window, dock);
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
        Settle(window);
        return window;
    }

    /// <summary>Drives layout to completion; headless has no render loop of its own.</summary>
    private static void Settle(Window window)
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }
}
