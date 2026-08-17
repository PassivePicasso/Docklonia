using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// One edge strip of auto-hide buttons (§5.3). Chrome belonging to the
/// <c>Dock</c>: an edge with no entries shows no strip and consumes no space.
/// </summary>
/// <remarks>
/// <b>Template parts.</b> <c>PART_Items</c> (required — hosts the generated
/// buttons). <b>Pseudo-classes.</b> <c>:left</c>, <c>:top</c>, <c>:right</c>,
/// <c>:bottom</c>.
/// </remarks>
[TemplatePart(ItemsPart, typeof(Panel))]
[PseudoClasses(":left", ":top", ":right", ":bottom")]
public class DockAutoHideStrip : TemplatedControl
{
    public const string ItemsPart = "PART_Items";

    public static readonly StyledProperty<DockEdge> EdgeProperty =
        AvaloniaProperty.Register<DockAutoHideStrip, DockEdge>(nameof(Edge));

    private readonly List<DockAutoHideButton> _buttons = new();
    private Panel? _items;

    static DockAutoHideStrip()
    {
        EdgeProperty.Changed.AddClassHandler<DockAutoHideStrip>((strip, _) => strip.UpdatePseudoClasses());
    }

    public DockEdge Edge
    {
        get => GetValue(EdgeProperty);
        set => SetValue(EdgeProperty, value);
    }

    internal Dock? Owner { get; set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _items = e.NameScope.Find<Panel>(ItemsPart);
        UpdatePseudoClasses();
        Rebuild();
    }

    /// <summary>
    /// Rebuilds from the layout's auto-hidden entries for this edge. One button
    /// per pane or per tab, as <c>Dock.AutoHideButtons</c> selects.
    /// </summary>
    internal void Rebuild()
    {
        if (_items is null || Owner is null)
        {
            return;
        }

        var entries = Owner.Layout?.AutoHidden.Where(entry => entry.Edge == Edge).ToArray()
            ?? Array.Empty<AutoHideEntry>();

        var wanted = entries
            .SelectMany(entry => Owner.AutoHideButtons == AutoHideButtonMode.PerTab
                ? DockTree.ContentsIn(entry.Pane).Select(content => (Entry: entry, Title: content.Title))
                : new[] { (Entry: entry, Title: entry.Pane.Title ?? "Pane") })
            .ToArray();

        _items.Children.Clear();
        _buttons.Clear();

        foreach (var (entry, title) in wanted)
        {
            var button = new DockAutoHideButton { Owner = Owner, Entry = entry, Title = title, Edge = Edge };
            _buttons.Add(button);
            _items.Children.Add(button);
        }

        IsVisible = _buttons.Count > 0;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":left", Edge == DockEdge.Left);
        PseudoClasses.Set(":top", Edge == DockEdge.Top);
        PseudoClasses.Set(":right", Edge == DockEdge.Right);
        PseudoClasses.Set(":bottom", Edge == DockEdge.Bottom);
    }
}

/// <summary>
/// A parked pane's button. Activating it slides the pane out over the content;
/// re-pinning returns it to the tree at its restore anchor (§5.3).
/// </summary>
/// <remarks>
/// <b>Template parts.</b> <c>PART_PinButton</c> (optional — re-pins).
/// <b>Pseudo-classes.</b> <c>:left</c>, <c>:top</c>, <c>:right</c>,
/// <c>:bottom</c>, <c>:open</c>.
/// </remarks>
[TemplatePart(PinButtonPart, typeof(Button))]
[PseudoClasses(":left", ":top", ":right", ":bottom", ":open")]
public class DockAutoHideButton : TemplatedControl
{
    public const string PinButtonPart = "PART_PinButton";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DockAutoHideButton, string?>(nameof(Title));

    public static readonly StyledProperty<DockEdge> EdgeProperty =
        AvaloniaProperty.Register<DockAutoHideButton, DockEdge>(nameof(Edge));

    private Button? _pinButton;

    static DockAutoHideButton()
    {
        EdgeProperty.Changed.AddClassHandler<DockAutoHideButton>((button, _) => button.UpdatePseudoClasses());
        FocusableProperty.OverrideDefaultValue<DockAutoHideButton>(true);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public DockEdge Edge
    {
        get => GetValue(EdgeProperty);
        set => SetValue(EdgeProperty, value);
    }

    internal Dock? Owner { get; set; }

    internal AutoHideEntry? Entry { get; set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_pinButton is not null)
        {
            _pinButton.Click -= OnPinClick;
        }

        _pinButton = e.NameScope.Find<Button>(PinButtonPart);

        if (_pinButton is not null)
        {
            _pinButton.Click += OnPinClick;
        }

        UpdatePseudoClasses();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Owner?.FlyoutTrigger == AutoHideTrigger.Click)
        {
            Reveal();
            e.Handled = true;
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);

        if (Owner?.FlyoutTrigger == AutoHideTrigger.Hover)
        {
            Reveal();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Enter or Key.Space:
                Reveal();
                e.Handled = true;
                break;

            // Re-pinning must be reachable without a pointer (§11).
            case Key.P when e.KeyModifiers == KeyModifiers.Control:
                Pin();
                e.Handled = true;
                break;
        }
    }

    internal void SetOpen(bool open) => PseudoClasses.Set(":open", open);

    private void Reveal()
    {
        if (Entry is not null)
        {
            Owner?.ShowAutoHideFlyout(Entry, this);
        }
    }

    private void Pin()
    {
        if (Entry is not null)
        {
            Owner?.Commands.Restore(Entry);
        }
    }

    private void OnPinClick(object? sender, RoutedEventArgs e)
    {
        Pin();
        e.Handled = true;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":left", Edge == DockEdge.Left);
        PseudoClasses.Set(":top", Edge == DockEdge.Top);
        PseudoClasses.Set(":right", Edge == DockEdge.Right);
        PseudoClasses.Set(":bottom", Edge == DockEdge.Bottom);
    }
}
