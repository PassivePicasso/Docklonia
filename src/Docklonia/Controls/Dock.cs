using System.Collections;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using Docklonia.Descriptors;
using Docklonia.Diagnostics;
using Docklonia.Dragging;
using Docklonia.Model;
using Docklonia.Model.Mutations;

using Docklonia.Automation;

namespace Docklonia.Controls;

/// <summary>
/// The single entry point. Placing a <c>&lt;Dock&gt;</c> in a view is the
/// complete integration step (§1).
/// </summary>
/// <remarks>
/// <para><b>Template parts.</b> <c>PART_Root</c> (required — hosts the layout
/// presenter), <c>PART_Overlay</c> (required — hosts guides and the auto-hide
/// flyout), <c>PART_LeftStrip</c>, <c>PART_TopStrip</c>, <c>PART_RightStrip</c>,
/// <c>PART_BottomStrip</c> (optional auto-hide strips; an edge with no entries
/// shows no strip and consumes no space).</para>
///
/// <para><b>Pseudo-classes.</b> <c>:empty</c>, <c>:dragging</c>,
/// <c>:maximized</c>.</para>
///
/// <para>A <c>Dock</c> may not be nested inside another <c>Dock</c>'s content:
/// target resolution hit-tests registered surfaces, so overlapping <c>Dock</c>s
/// would give two valid targets for one point with no principled winner. It is
/// diagnosed rather than left to misbehave (§9).</para>
/// </remarks>
[TemplatePart(RootPart, typeof(Decorator))]
[TemplatePart(OverlayPart, typeof(Panel))]
[PseudoClasses(":empty", ":dragging", ":maximized")]
public class Dock : TemplatedControl
{
    public const string RootPart = "PART_Root";
    public const string OverlayPart = "PART_Overlay";
    public const string LeftStripPart = "PART_LeftStrip";
    public const string TopStripPart = "PART_TopStrip";
    public const string RightStripPart = "PART_RightStrip";
    public const string BottomStripPart = "PART_BottomStrip";

    /// <summary>Default floor on pane size, in device-independent pixels.</summary>
    public const double DefaultMinPaneSize = 80d;

    /// <summary>Travel past which a press becomes a drag rather than a click.</summary>
    internal const double DragThreshold = 4d;

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Dock, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<DockLayout?> LayoutProperty =
        AvaloniaProperty.Register<Dock, DockLayout?>(nameof(Layout), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<object?> ActiveContentProperty =
        AvaloniaProperty.Register<Dock, object?>(nameof(ActiveContent), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinPaneSizeProperty =
        AvaloniaProperty.Register<Dock, double>(nameof(MinPaneSize), DefaultMinPaneSize);

    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<Dock, object?>(nameof(EmptyContent));

    public static readonly StyledProperty<AutoHideTrigger> FlyoutTriggerProperty =
        AvaloniaProperty.Register<Dock, AutoHideTrigger>(nameof(FlyoutTrigger), AutoHideTrigger.Click);

    public static readonly StyledProperty<AutoHideButtonMode> AutoHideButtonsProperty =
        AvaloniaProperty.Register<Dock, AutoHideButtonMode>(nameof(AutoHideButtons), AutoHideButtonMode.PerPane);

    private readonly DockItemCoordinator _coordinator;
    private readonly DockFloatSurfaces _floats;
    private readonly DockGuideOverlay _guides = new();
    private readonly List<DockPanePresenter> _realized = new();

    private Decorator? _rootHost;
    private Panel? _overlayHost;
    private DockPanePresenter? _rootPresenter;
    private bool _writingBack;

    static Dock()
    {
        LayoutProperty.Changed.AddClassHandler<Dock>((dock, e) => dock.OnLayoutChanged(e));
        ItemsSourceProperty.Changed.AddClassHandler<Dock>((dock, e) => dock._coordinator.SetItemsSource(e.NewValue as IEnumerable));
        ActiveContentProperty.Changed.AddClassHandler<Dock>((dock, e) => dock.OnActiveContentChanged(e));
        MinPaneSizeProperty.Changed.AddClassHandler<Dock>((dock, _) => dock.InvalidateMeasure());
        FocusableProperty.OverrideDefaultValue<Dock>(true);
    }

    public Dock()
    {
        _coordinator = new DockItemCoordinator(this);
        _floats = new DockFloatSurfaces(this);
        Activation = new DockActivation();
        Commands = new DockCommands(this);
        Drag = new DockDragController(this, _guides);
        AutoHide = new DockAutoHideSurface(this);
        Keyboard = new DockKeyboard(this);

        DataTemplates.Add(AuthoredContentTemplate.Instance);
        GotFocus += OnGotFocus;
    }

    /// <summary>The consumer's collection of content. The primary path (§9).</summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Per-item-type metadata (§3.7). Mandatory: content with no matching descriptor is never docked.</summary>
    public ObservableCollection<DockItemDescriptor> ItemDescriptors { get; } = new();

    /// <summary>Named layout regions, declared once (§3.9).</summary>
    public ObservableCollection<DockGroup> Groups { get; } = new();

    /// <summary>Statically-authored panes (§9.1). Coexist with <see cref="ItemsSource"/> in one layout tree.</summary>
    [Content]
    public ObservableCollection<DockItem> Items { get; } = new();

    /// <summary>
    /// The layout object, two-way (§9.2). Carries the dock tree itself, not JSON;
    /// serializing is a separate, deliberate act reachable from this object alone.
    /// </summary>
    public DockLayout? Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <summary>
    /// The active pane's content, as the consumer's own object. <c>ActivePane</c>
    /// stays internal, because exposing an <see cref="IDockPane"/> for binding
    /// would put a library type into the consumer's view model (§3.11).
    /// </summary>
    public object? ActiveContent
    {
        get => GetValue(ActiveContentProperty);
        set => SetValue(ActiveContentProperty, value);
    }

    /// <summary>Floor on pane size, applying to every split in this <c>Dock</c> (§3.3).</summary>
    public double MinPaneSize
    {
        get => GetValue(MinPaneSizeProperty);
        set => SetValue(MinPaneSizeProperty, value);
    }

    /// <summary>
    /// Rendered when the layout is empty; unset by default, so the
    /// zero-configuration case is a blank region. Not a drop target in its own
    /// right — outer guides already cover docking into an empty <c>Dock</c> (§9).
    /// </summary>
    public object? EmptyContent
    {
        get => GetValue(EmptyContentProperty);
        set => SetValue(EmptyContentProperty, value);
    }

    public AutoHideTrigger FlyoutTrigger
    {
        get => GetValue(FlyoutTriggerProperty);
        set => SetValue(FlyoutTriggerProperty, value);
    }

    public AutoHideButtonMode AutoHideButtons
    {
        get => GetValue(AutoHideButtonsProperty);
        set => SetValue(AutoHideButtonsProperty, value);
    }

    internal DockActivation Activation { get; }

    /// <summary>Pane and tab operations, shared by menus, the keyboard, and drops (§13).</summary>
    internal DockCommands Commands { get; }

    /// <summary>Gesture handling and this <c>Dock</c>'s half of drag target resolution (§7).</summary>
    internal DockDragController Drag { get; }

    internal DockItemCoordinator Coordinator => _coordinator;

    internal DockGuideOverlay Guides => _guides;

    /// <summary>Platform hosts for this Dock's floating windows, as drag surfaces (§7.2).</summary>
    internal IReadOnlyCollection<Hosting.DockHost> FloatSurfaces => _floats.Surfaces;

    /// <summary>Edge strips and the slide-out flyout for auto-hidden panes (§5.3).</summary>
    internal DockAutoHideSurface AutoHide { get; }

    /// <summary>Keyboard operation of every docking gesture (§11).</summary>
    internal DockKeyboard Keyboard { get; }

    internal DockDescriptorSet Descriptors { get; private set; } = new(new object(), Array.Empty<DockItemDescriptor>());

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        => new DockAutomationPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _rootHost = e.NameScope.Find<Decorator>(RootPart);
        _overlayHost = e.NameScope.Find<Panel>(OverlayPart);

        if (_overlayHost is not null)
        {
            if (!_overlayHost.Children.Contains(AutoHide.Flyout))
            {
                _overlayHost.Children.Add(AutoHide.Flyout);
            }
        }

        // The guides are not hosted here: they move into the overlay layer of
        // whichever TopLevel the drop target lives in, so a floating target
        // draws its guides in its own window rather than in this one (§7.2).

        AutoHide.Register(DockEdge.Left, e.NameScope.Find<DockAutoHideStrip>(LeftStripPart));
        AutoHide.Register(DockEdge.Top, e.NameScope.Find<DockAutoHideStrip>(TopStripPart));
        AutoHide.Register(DockEdge.Right, e.NameScope.Find<DockAutoHideStrip>(RightStripPart));
        AutoHide.Register(DockEdge.Bottom, e.NameScope.Find<DockAutoHideStrip>(BottomStripPart));

        _guides.IsVisible = false;
        RebuildRoot();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (this.GetVisualAncestors().OfType<Dock>().Any())
        {
            DockDiagnostics.Error(
                this,
                "A Dock is nested inside another Dock's content. Target resolution hit-tests registered " +
                "surfaces, so overlapping Docks give two valid targets for one point with no principled " +
                "winner. Splits and tabs already compose arbitrarily within one Dock.");
        }

        RebuildDescriptors();
        DockRegistry.Register(this);
        EnsureLayout();
        _coordinator.Resync();
        _floats.Attach(Layout);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        Drag.CancelGesture();
        DockRegistry.Unregister(this);
        _floats.Dispose();
    }

    /// <summary>Serializes this <c>Dock</c>'s layout. A convenience for code that legitimately holds the control (§9.2).</summary>
    public string SerializeLayout() => EnsureLayout().ToJson();

    /// <summary>
    /// Begins a drag from content plus a screen point, with no originating pane
    /// (§7.4). Opt-in; nothing in the library requires it.
    /// </summary>
    public void BeginExternalDrag(object content, string? title, PixelPoint screen)
    {
        ArgumentNullException.ThrowIfNull(content);

        Drag.BeginExternal(content, title, screen);
    }

    /// <summary>Lets the drag controller reflect gesture state without exposing pseudo-classes.</summary>
    internal void SetDragging(bool dragging) => PseudoClasses.Set(":dragging", dragging);

    /// <summary>Slides an auto-hidden pane out over the content (§5.3).</summary>
    internal void ShowAutoHideFlyout(AutoHideEntry entry, DockAutoHideButton button) => AutoHide.Show(entry, button);

    /// <summary>Removes a node without consulting the consumer's close veto (§3.10).</summary>
    internal void RemoveNode(IDockNode node) => Commands.Close(node);

    internal DockLayout EnsureLayout()
    {
        if (Layout is { } existing)
        {
            return existing;
        }

        var created = new DockLayout();
        _writingBack = true;
        SetCurrentValue(LayoutProperty, created);
        _writingBack = false;

        return created;
    }

    internal void RebuildDescriptors()
    {
        Descriptors = new DockDescriptorSet(this, ItemDescriptors);
        Descriptors.Validate();
    }

    internal bool DescribesContent(object? content) => Descriptors.Describes(content);

    internal void RegisterRealizedView(DockPanePresenter presenter)
    {
        if (!_realized.Contains(presenter))
        {
            _realized.Add(presenter);
        }
    }

    /// <summary>
    /// Records activation and, only when this <c>Dock</c> already holds keyboard
    /// focus, moves focus with it. Without that gate, activating a pane in a
    /// background window would yank focus across windows (§3.11).
    /// </summary>
    internal void ActivateNode(IDockPane? node)
    {
        if (node is null)
        {
            return;
        }

        Activation.Activate(node);

        var layout = EnsureLayout();
        layout.ActivePane = node;

        SetCurrentValue(ActiveContentProperty, ContentOf(node));

        if (IsKeyboardFocusWithin && FindPaneControl(node) is { } control)
        {
            control.Focus();
        }

        foreach (var pane in EnumeratePaneControls())
        {
            pane.IsActive = pane.Node is { } candidate
                && (ReferenceEquals(candidate, node) || DockTree.Contains(candidate, node));
        }

        NotifyLayoutChanged();
    }

    private static object? ContentOf(IDockPane node) => node switch
    {
        DockContent content => content.Content,
        DockTabPane { SelectedChild: DockContent selected } => selected.Content,
        _ => null,
    };

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Drag.OnPointerMoved(this, e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Drag.OnPointerReleased(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape && Drag.IsDragging)
        {
            Drag.CancelGesture();
            e.Handled = true;
            return;
        }

        if (Keyboard.Handle(e))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Activation is logical focus in the WPF sense: it survives this
    /// <c>Dock</c> losing keyboard focus, and is reapplied when focus returns
    /// (§3.11). The library owns that state rather than delegating to Avalonia's
    /// focus scopes, whose public surface is thin and has moved between versions.
    /// </summary>
    private void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (Layout?.ActivePane is { } remembered
            && FindPaneControl(remembered) is { } control
            && !control.IsKeyboardFocusWithin)
        {
            control.Focus();
        }
    }

    internal void NotifyLayoutChanged()
    {
        if (_writingBack)
        {
            return;
        }

        var layout = EnsureLayout();
        layout.MarkChanged();

        PseudoClasses.Set(":empty", layout.Root is null && layout.Floats.Count == 0);
        PseudoClasses.Set(":maximized", layout.MaximizedPane is not null);

        _floats.Sync();
        AutoHide.Refresh();
        RefreshRoot();
    }

    private void OnLayoutChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is DockLayout layout)
        {
            Activation.SeedFrom(layout);
        }

        _floats.Attach(e.NewValue as DockLayout);
        _coordinator.Resync();
        RebuildRoot();
    }

    private void OnActiveContentChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not { } content || Layout is null)
        {
            return;
        }

        var node = Layout.AllPanes().OfType<DockContent>().FirstOrDefault(item => ReferenceEquals(item.Content, content));

        if (node is not null && !ReferenceEquals(Layout.ActivePane, node))
        {
            ActivateNode(node);
        }
    }

    private void RebuildRoot()
    {
        if (_rootHost is null)
        {
            return;
        }

        _rootPresenter = new DockPanePresenter { Owner = this, Pane = PresentedRoot };
        _rootHost.Child = _rootPresenter;

        RefreshRoot();
    }

    /// <summary>
    /// A maximized pane temporarily covers the whole <c>Dock</c> by being the
    /// only thing presented. Its siblings are hidden, not removed — the tree is
    /// untouched, so nothing normalizes and restoring reveals it exactly as it
    /// was (§5.3).
    /// </summary>
    private IDockNode? PresentedRoot => Layout?.MaximizedPane as IDockNode ?? Layout?.Root;

    private void RefreshRoot()
    {
        if (_rootPresenter is not null && !ReferenceEquals(_rootPresenter.Pane, PresentedRoot))
        {
            _rootPresenter.Pane = PresentedRoot;
        }
    }

    private DockPaneControl? FindPaneControl(IDockPane node)
        => EnumeratePaneControls().FirstOrDefault(control => ReferenceEquals(control.Node, node));

    private IEnumerable<DockPaneControl> EnumeratePaneControls()
    {
        foreach (var control in this.GetVisualDescendants().OfType<DockPaneControl>())
        {
            yield return control;
        }

        foreach (var host in _floats.Hosts)
        {
            if (host.RootVisual is null)
            {
                continue;
            }

            foreach (var control in host.RootVisual.GetVisualDescendants().OfType<DockPaneControl>())
            {
                yield return control;
            }
        }
    }

    internal IEnumerable<DockPaneControl> PaneControls => EnumeratePaneControls();
}

/// <summary>How an auto-hide flyout is summoned (§5.3).</summary>
public enum AutoHideTrigger
{
    Hover,
    Click,
}

/// <summary>What an auto-hide strip shows one button for (§5.3).</summary>
public enum AutoHideButtonMode
{
    PerPane,
    PerTab,
}
