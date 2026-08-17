using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// One tab in a strip. Lookless: the label, icon, and close button all come from
/// the control theme, and every state is a pseudo-class (§12).
/// </summary>
/// <remarks>
/// <para><b>Template parts.</b> <c>PART_CloseButton</c> (optional) closes the
/// tab; when absent the tab is still closable from its context menu and from the
/// keyboard, so a replacement template that omits it degrades rather than
/// crashes.</para>
///
/// <para><b>Pseudo-classes.</b> <c>:selected</c>, <c>:active</c>,
/// <c>:closable</c>, <c>:dragging</c>, <c>:first-in-line</c>,
/// <c>:last-in-line</c>.</para>
/// </remarks>
[TemplatePart(CloseButtonPart, typeof(Button))]
[PseudoClasses(":selected", ":active", ":closable", ":dragging")]
public class DockTab : TemplatedControl
{
    public const string CloseButtonPart = "PART_CloseButton";

    public static readonly StyledProperty<IDockNode?> NodeProperty =
        AvaloniaProperty.Register<DockTab, IDockNode?>(nameof(Node));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<DockTab, bool>(nameof(IsSelected));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<DockTab, bool>(nameof(IsActive));

    private Button? _closeButton;

    static DockTab()
    {
        IsSelectedProperty.Changed.AddClassHandler<DockTab>((tab, _) => tab.UpdatePseudoClasses());
        IsActiveProperty.Changed.AddClassHandler<DockTab>((tab, _) => tab.UpdatePseudoClasses());
        NodeProperty.Changed.AddClassHandler<DockTab>((tab, _) => tab.OnNodeChanged());
        FocusableProperty.OverrideDefaultValue<DockTab>(true);
    }

    /// <summary>The node this tab represents. Tab identity is the node, not the content it wraps (§3.2).</summary>
    public IDockNode? Node
    {
        get => GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Whether the owning pane is the active one (§3.11). Distinct from selection.</summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    internal Dock? Owner { get; set; }

    internal DockPaneControl? Pane { get; set; }

    public bool CanClose => Node is DockContent { CanClose: true } or DockTabPane;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_closeButton is not null)
        {
            _closeButton.Click -= OnCloseClick;
        }

        _closeButton = e.NameScope.Find<Button>(CloseButtonPart);

        if (_closeButton is not null)
        {
            _closeButton.Click += OnCloseClick;
        }

        UpdatePseudoClasses();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Node is null || Owner is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsLeftButtonPressed)
        {
            // Clicking is a focus gesture, so it both selects and activates.
            Owner.ActivateNode(Node);
            Owner.Drag.BeginTabGesture(this, e);
            e.Handled = true;
        }
        else if (point.Properties.IsMiddleButtonPressed && CanClose)
        {
            Owner.Commands.RequestClose(Node);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (Node is null || Owner is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Delete when e.KeyModifiers == KeyModifiers.None && CanClose:
                Owner.Commands.RequestClose(Node);
                e.Handled = true;
                break;

            case Key.Enter or Key.Space:
                Owner.ActivateNode(Node);
                e.Handled = true;
                break;
        }
    }

    internal void SetDragging(bool dragging) => PseudoClasses.Set(":dragging", dragging);

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (Node is not null)
        {
            Owner?.Commands.RequestClose(Node);
        }

        e.Handled = true;
    }

    private void OnNodeChanged()
    {
        UpdatePseudoClasses();
        DataContext = Node;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":selected", IsSelected);
        PseudoClasses.Set(":active", IsActive && IsSelected);
        PseudoClasses.Set(":closable", CanClose);
    }
}
