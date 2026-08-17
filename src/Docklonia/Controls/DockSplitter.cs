using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;

namespace Docklonia.Controls;

/// <summary>
/// The grip between a split's two children. Focusable, so a split can be
/// resized without a pointer (§11).
/// </summary>
/// <remarks>
/// <b>Pseudo-classes.</b> <c>:horizontal</c>, <c>:vertical</c>,
/// <c>:dragging</c>.
/// </remarks>
[PseudoClasses(":horizontal", ":vertical", ":dragging")]
public class DockSplitter : Thumb
{
    /// <summary>Proportion moved per arrow key press.</summary>
    public static readonly StyledProperty<double> KeyboardStepProperty =
        AvaloniaProperty.Register<DockSplitter, double>(nameof(KeyboardStep), 0.02);

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<DockSplitter, Orientation>(nameof(Orientation), Orientation.Horizontal);

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

    /// <summary>Raised with a signed proportion delta, already clamped by the presenter.</summary>
    internal event Action<double>? StepRequested;

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

    protected override void OnDragStarted(VectorEventArgs e)
    {
        base.OnDragStarted(e);
        PseudoClasses.Set(":dragging", true);
    }

    protected override void OnDragCompleted(VectorEventArgs e)
    {
        base.OnDragCompleted(e);
        PseudoClasses.Set(":dragging", false);
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":horizontal", Orientation == Orientation.Horizontal);
        PseudoClasses.Set(":vertical", Orientation == Orientation.Vertical);
    }
}
