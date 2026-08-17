using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// The slide-out panel for an auto-hidden pane (§5.3), with a grip on its inner
/// edge so the user can resize it.
/// </summary>
/// <remarks>
/// <para>The grip is a <see cref="DockSplitter"/> — the same control that resizes
/// a real split. It already positions absolutely from the pointer, clamps at a
/// floor, and is keyboard-operable, so reusing it keeps one resize behaviour
/// rather than two that could drift apart.</para>
///
/// <para>The resulting size is stored on the entry as a proportion, so it
/// survives save and load exactly as a split ratio does.</para>
///
/// <para><b>Template parts.</b> <c>PART_Content</c> (required — hosts the pane),
/// <c>PART_Resizer</c> (optional; without it the flyout is simply not
/// resizable). <b>Pseudo-classes.</b> <c>:left</c>, <c>:top</c>, <c>:right</c>,
/// <c>:bottom</c>.</para>
/// </remarks>
[TemplatePart(ContentPart, typeof(Decorator))]
[TemplatePart(ResizerPart, typeof(DockSplitter))]
[PseudoClasses(":left", ":top", ":right", ":bottom")]
public class DockAutoHideFlyout : TemplatedControl
{
    public const string ContentPart = "PART_Content";
    public const string ResizerPart = "PART_Resizer";

    public static readonly StyledProperty<DockEdge> EdgeProperty =
        AvaloniaProperty.Register<DockAutoHideFlyout, DockEdge>(nameof(Edge));

    private Decorator? _contentHost;
    private DockSplitter? _resizer;

    static DockAutoHideFlyout()
    {
        EdgeProperty.Changed.AddClassHandler<DockAutoHideFlyout>((flyout, _) => flyout.OnEdgeChanged());
    }

    public DockEdge Edge
    {
        get => GetValue(EdgeProperty);
        set => SetValue(EdgeProperty, value);
    }

    /// <summary>The pane presented inside the flyout.</summary>
    internal Control? PaneContent
    {
        get => _contentHost?.Child;
        set
        {
            if (_contentHost is not null)
            {
                _contentHost.Child = value;
            }
        }
    }

    /// <summary>Requested extent along the edge's axis, in device-independent pixels.</summary>
    internal event Action<double>? ExtentRequested;

    /// <summary>Resizing is a continuous gesture, so the write-back happens here (§9.2).</summary>
    internal event Action? ResizeCompleted;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_resizer is not null)
        {
            _resizer.DragTo -= OnResizerDragTo;
            _resizer.DragCompleted -= OnResizeCompleted;
            _resizer.StepRequested -= OnResizerStep;
        }

        _contentHost = e.NameScope.Find<Decorator>(ContentPart);
        _resizer = e.NameScope.Find<DockSplitter>(ResizerPart);

        if (_resizer is not null)
        {
            _resizer.DragTo += OnResizerDragTo;
            _resizer.DragCompleted += OnResizeCompleted;
            _resizer.StepRequested += OnResizerStep;
        }

        OnEdgeChanged();
    }

    /// <summary>
    /// Converts the grip's position within the flyout into an extent. For an edge
    /// the flyout is anchored to, the grip's offset <i>is</i> the new extent; for
    /// the opposite edge the grip moves the near side, so the extent is what
    /// remains behind it.
    /// </summary>
    internal void RequestExtent(double extent) => ExtentRequested?.Invoke(extent);

    private void OnResizerDragTo(Point position)
    {
        var extent = Edge switch
        {
            DockEdge.Left => position.X,
            DockEdge.Top => position.Y,
            DockEdge.Right => Bounds.Width - position.X,
            _ => Bounds.Height - position.Y,
        };

        ExtentRequested?.Invoke(extent);
    }

    private void OnResizerStep(double step)
    {
        var current = IsHorizontal ? Bounds.Width : Bounds.Height;
        var direction = Edge is DockEdge.Left or DockEdge.Top ? 1d : -1d;

        ExtentRequested?.Invoke(current + (step * current * direction));
        ResizeCompleted?.Invoke();
    }

    private void OnResizeCompleted() => ResizeCompleted?.Invoke();

    private bool IsHorizontal => Edge is DockEdge.Left or DockEdge.Right;

    private void OnEdgeChanged()
    {
        PseudoClasses.Set(":left", Edge == DockEdge.Left);
        PseudoClasses.Set(":top", Edge == DockEdge.Top);
        PseudoClasses.Set(":right", Edge == DockEdge.Right);
        PseudoClasses.Set(":bottom", Edge == DockEdge.Bottom);

        if (_resizer is not null)
        {
            _resizer.Orientation = IsHorizontal ? Orientation.Horizontal : Orientation.Vertical;
        }
    }
}
