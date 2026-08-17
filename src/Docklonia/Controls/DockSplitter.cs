using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

using Docklonia.Automation;

namespace Docklonia.Controls;

/// <summary>
/// The grip between a split's two children. Focusable, so a split can be
/// resized without a pointer (§11).
/// </summary>
/// <remarks>
/// <para>Deliberately <b>not</b> a <see cref="Thumb"/>. A thumb reports drag
/// deltas relative to itself, but this control moves as the ratio it is editing
/// changes — so every delta would be measured from an origin that just shifted,
/// which makes the grip drift away from the cursor and jitter. Instead the
/// gesture reports the pointer in the <i>parent's</i> coordinate space, which
/// does not move, and holds the grab offset within the grip constant. The grip
/// therefore stays exactly under the cursor.</para>
///
/// <para><b>Pseudo-classes.</b> <c>:horizontal</c>, <c>:vertical</c>,
/// <c>:dragging</c>.</para>
/// </remarks>
[PseudoClasses(":horizontal", ":vertical", ":dragging")]
public class DockSplitter : TemplatedControl
{
    /// <summary>Proportion moved per arrow key press.</summary>
    public static readonly StyledProperty<double> KeyboardStepProperty =
        AvaloniaProperty.Register<DockSplitter, double>(nameof(KeyboardStep), 0.02);

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<DockSplitter, Orientation>(nameof(Orientation), Orientation.Horizontal);

    private Point _grabOffset;
    private bool _dragging;

    static DockSplitter()
    {
        OrientationProperty.Changed.AddClassHandler<DockSplitter>((splitter, _) => splitter.UpdatePseudoClasses());
        FocusableProperty.OverrideDefaultValue<DockSplitter>(true);
    }

    public DockSplitter()
    {
        UpdatePseudoClasses();
    }

    public double KeyboardStep
    {
        get => GetValue(KeyboardStepProperty);
        set => SetValue(KeyboardStepProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Where the grip's leading edge should sit, in the parent's coordinates,
    /// with the grab offset already subtracted.
    /// </summary>
    internal event Action<Point>? DragTo;

    internal event Action? DragCompleted;

    /// <summary>Raised with a signed proportion delta, clamped by the presenter.</summary>
    internal event Action<double>? StepRequested;

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        => new DockSplitterAutomationPeer(this);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _grabOffset = e.GetPosition(this);
        _dragging = true;

        e.Pointer.Capture(this);
        PseudoClasses.Set(":dragging", true);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_dragging || this.GetVisualParent() is not Visual parent)
        {
            return;
        }

        var pointer = e.GetPosition(parent);
        DragTo?.Invoke(new Point(pointer.X - _grabOffset.X, pointer.Y - _grabOffset.Y));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        PseudoClasses.Set(":dragging", false);

        DragCompleted?.Invoke();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var step = (e.Key, Orientation) switch
        {
            (Key.Left, Orientation.Horizontal) => -KeyboardStep,
            (Key.Right, Orientation.Horizontal) => KeyboardStep,
            (Key.Up, Orientation.Vertical) => -KeyboardStep,
            (Key.Down, Orientation.Vertical) => KeyboardStep,
            _ => 0d,
        };

        if (step != 0d)
        {
            StepRequested?.Invoke(step);
            e.Handled = true;
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":horizontal", Orientation == Orientation.Horizontal);
        PseudoClasses.Set(":vertical", Orientation == Orientation.Vertical);
    }
}
