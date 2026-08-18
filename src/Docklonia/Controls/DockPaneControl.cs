using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Docklonia.Model;

using Docklonia.Automation;

namespace Docklonia.Controls;

/// <summary>
/// A Pane: the dockable unit that owns a titlebar, a tab strip, and a content
/// area (§5.1).
/// </summary>
/// <remarks>
/// <para>Presents a <see cref="DockTabPane"/>, or a bare <see cref="DockContent"/>
/// as a single-tab group — one control and one code path, so a leaf that has not
/// been promoted into a tab group still gets a titlebar.</para>
///
/// <para><b>Template parts.</b> <c>PART_TabStrip</c> (required — hosts the
/// generated tabs), <c>PART_ContentHost</c> (required), <c>PART_TitleBar</c>,
/// <c>PART_MenuButton</c>, <c>PART_MinimizeButton</c>,
/// <c>PART_MaximizeButton</c>, <c>PART_CloseButton</c> (all optional; each
/// absent part simply removes that affordance, and every operation stays
/// reachable from the pane menu and the keyboard).</para>
///
/// <para><b>Pseudo-classes.</b> <c>:active</c>, <c>:floating</c>,
/// <c>:maximized</c>, <c>:drop-target</c>, <c>:single-tab</c>,
/// <c>:persistent</c>.</para>
/// </remarks>
[TemplatePart(TabStripPart, typeof(Panel))]
[TemplatePart(ContentHostPart, typeof(ContentPresenter))]
[PseudoClasses(":active", ":floating", ":maximized", ":drop-target", ":single-tab", ":persistent")]
public class DockPaneControl : TemplatedControl
{
    public const string TabStripPart = "PART_TabStrip";
    public const string ContentHostPart = "PART_ContentHost";
    public const string TitleBarPart = "PART_TitleBar";
    public const string MenuButtonPart = "PART_MenuButton";
    public const string MinimizeButtonPart = "PART_MinimizeButton";
    public const string MaximizeButtonPart = "PART_MaximizeButton";
    public const string CloseButtonPart = "PART_CloseButton";

    public static readonly StyledProperty<IDockNode?> NodeProperty =
        AvaloniaProperty.Register<DockPaneControl, IDockNode?>(nameof(Node));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<DockPaneControl, bool>(nameof(IsActive));

    private readonly List<DockTab> _tabs = new();
    private Panel? _strip;
    private ContentPresenter? _contentHost;
    private Control? _titleBar;
    private Button? _menuButton;
    private Button? _minimizeButton;
    private Button? _maximizeButton;
    private Button? _closeButton;

    static DockPaneControl()
    {
        NodeProperty.Changed.AddClassHandler<DockPaneControl>((pane, e) => pane.OnNodeChanged(e));
        IsActiveProperty.Changed.AddClassHandler<DockPaneControl>((pane, _) => pane.UpdatePseudoClasses());
        FocusableProperty.OverrideDefaultValue<DockPaneControl>(true);
    }

    public IDockNode? Node
    {
        get => GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    /// <summary>Logical focus, not keyboard focus (§3.11).</summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    internal Dock? Owner { get; set; }

    internal IReadOnlyList<DockTab> Tabs => _tabs;

    /// <summary>The tab pane backing this control, or null when it presents a bare leaf.</summary>
    internal DockTabPane? TabPane => Node as DockTabPane;

    /// <summary>The node currently displayed — the tab pane's selection, or the leaf itself.</summary>
    /// <remarks>
    /// A tab pane answers its selection and nothing else, including when it has
    /// none: falling back to the pane would make an emptied persistent pane its
    /// own content, and presenting a pane inside itself does not terminate.
    /// </remarks>
    internal IDockNode? SelectedNode => TabPane is { } tabs ? tabs.SelectedChild : Node;

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        => new DockPaneAutomationPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        DetachParts();

        _strip = e.NameScope.Find<Panel>(TabStripPart);
        _contentHost = e.NameScope.Find<ContentPresenter>(ContentHostPart);
        _titleBar = e.NameScope.Find<Control>(TitleBarPart);
        _menuButton = e.NameScope.Find<Button>(MenuButtonPart);
        _minimizeButton = e.NameScope.Find<Button>(MinimizeButtonPart);
        _maximizeButton = e.NameScope.Find<Button>(MaximizeButtonPart);
        _closeButton = e.NameScope.Find<Button>(CloseButtonPart);

        AttachParts();
        Rebuild();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Node is not null && !e.Handled)
        {
            Owner?.ActivateNode(SelectedNode ?? Node);
        }
    }

    /// <summary>Rebuilds the tab controls and the content host from the model.</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Owner?.Drag.OnPointerMoved(this, e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Owner?.Drag.OnPointerReleased(e);
    }

    internal void Rebuild()
    {
        SyncTabs();
        SyncContent();
        UpdatePseudoClasses();
    }

    internal void SetDropTarget(bool active) => PseudoClasses.Set(":drop-target", active);

    private void OnNodeChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is DockTabPane oldPane)
        {
            oldPane.PropertyChanged -= OnPanePropertyChanged;
            ((INotifyCollectionChanged)oldPane.Children).CollectionChanged -= OnChildrenChanged;
        }

        if (e.NewValue is DockTabPane newPane)
        {
            newPane.PropertyChanged += OnPanePropertyChanged;
            ((INotifyCollectionChanged)newPane.Children).CollectionChanged += OnChildrenChanged;
        }

        Rebuild();
    }

    private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DockTabPane.SelectedChild))
        {
            SyncContent();
            SyncSelection();
        }

        if (e.PropertyName is nameof(DockTabPane.IsPersistent))
        {
            UpdatePseudoClasses();
        }
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void SyncTabs()
    {
        if (_strip is null)
        {
            return;
        }

        var nodes = TabPane?.Children ?? (Node is null ? Array.Empty<IDockNode>() : new[] { Node });

        while (_tabs.Count > nodes.Count)
        {
            var last = _tabs[^1];
            _tabs.RemoveAt(_tabs.Count - 1);
            _strip.Children.Remove(last);
        }

        while (_tabs.Count < nodes.Count)
        {
            var tab = new DockTab { Owner = Owner, Pane = this };
            _tabs.Add(tab);
            _strip.Children.Add(tab);
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            _tabs[i].Owner = Owner;
            _tabs[i].Pane = this;
            _tabs[i].Node = nodes[i];
        }

        SyncSelection();
        PseudoClasses.Set(":single-tab", nodes.Count <= 1);
    }

    private void SyncSelection()
    {
        var selected = SelectedNode;

        foreach (var tab in _tabs)
        {
            tab.IsSelected = ReferenceEquals(tab.Node, selected);
            tab.IsActive = IsActive;
        }
    }

    /// <summary>
    /// Sets the presenter's content to the consumer's own object and lets normal
    /// template resolution find their template (§3.8). A nested composite is
    /// presented through the recursive presenter instead.
    /// </summary>
    private void SyncContent()
    {
        if (_contentHost is null)
        {
            return;
        }

        _contentHost.Content = SelectedNode switch
        {
            DockContent content => content.Content,
            { } composite => new DockPanePresenter { Owner = Owner, Pane = composite },
            _ => null,
        };
    }

    private void AttachParts()
    {
        if (_titleBar is not null)
        {
            _titleBar.PointerPressed += OnTitleBarPressed;
        }

        Wire(_menuButton, OnMenuClick);
        Wire(_minimizeButton, OnMinimizeClick);
        Wire(_maximizeButton, OnMaximizeClick);
        Wire(_closeButton, OnCloseClick);
    }

    private void DetachParts()
    {
        if (_titleBar is not null)
        {
            _titleBar.PointerPressed -= OnTitleBarPressed;
        }

        Unwire(_menuButton, OnMenuClick);
        Unwire(_minimizeButton, OnMinimizeClick);
        Unwire(_maximizeButton, OnMaximizeClick);
        Unwire(_closeButton, OnCloseClick);
    }

    private static void Wire(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click += handler;
        }
    }

    private static void Unwire(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click -= handler;
        }
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Node is null || Owner is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Owner.ActivateNode(SelectedNode ?? Node);
        Owner.Drag.BeginPaneGesture(this, e);
        e.Handled = true;
    }

    private void OnMenuClick(object? sender, RoutedEventArgs e)
    {
        if (Node is not null && Owner is not null && _menuButton is not null)
        {
            var menu = DockMenuBuilder.BuildPaneMenu(Owner, this);
            menu.PlacementTarget = _menuButton;
            menu.Open(_menuButton);
        }

        e.Handled = true;
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        if (Node is not null)
        {
            Owner?.Commands.Minimize(this);
        }

        e.Handled = true;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        if (Node is not null)
        {
            Owner?.Commands.ToggleMaximize(Node);
        }

        e.Handled = true;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (Node is not null)
        {
            Owner?.Commands.RequestClose(Node);
        }

        e.Handled = true;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":active", IsActive);

        // What the pane's own close button means depends on this: a persistent
        // pane outlives its contents, so closing the last one and closing the
        // region are different acts and cannot share one affordance (§6.1).
        PseudoClasses.Set(":persistent", TabPane?.IsPersistent == true);
        PseudoClasses.Set(":floating", DockTree.FloatOf(Node) is not null);
        PseudoClasses.Set(":maximized", IsMaximized);

        SyncSelection();
    }

    /// <summary>
    /// Whether this pane is maximized, which means two things and shows one
    /// glyph: a floated pane is maximized when its window is, a docked one when
    /// the layout is presenting it alone.
    /// </summary>
    private bool IsMaximized
        => DockTree.FloatOf(Node) is { } floating
            ? floating.WindowState == WindowState.Maximized
            : Owner?.Layout?.MaximizedPane is { } maximized && ReferenceEquals(maximized, Node);
}
