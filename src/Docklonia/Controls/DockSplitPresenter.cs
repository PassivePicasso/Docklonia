using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// Lays out a <see cref="DockSplitPane"/>'s two children either side of a
/// <see cref="DockSplitter"/> (§3.3).
/// </summary>
/// <remarks>
/// <para>The split is stored as a proportion, never as a pixel size, so layouts
/// survive window resizing and restore. Custom measure/arrange rather than a
/// <c>Grid</c>, because §3.3's floor has a specific overflow behaviour: when the
/// available extent cannot honour both minimums the panes take their minimum and
/// <b>overflow</b>, rather than being squeezed toward zero.</para>
///
/// <para>Because the ratio is clamped so neither side can reach the floor, a
/// pane can never be dragged out of existence and left unrecoverable.</para>
/// </remarks>
public class DockSplitPresenter : Control
{
    public static readonly StyledProperty<DockSplitPane?> SplitProperty =
        AvaloniaProperty.Register<DockSplitPresenter, DockSplitPane?>(nameof(Split));

    private readonly DockPanePresenter _first = new();
    private readonly DockPanePresenter _second = new();
    private readonly DockSplitter _splitter = new();

    static DockSplitPresenter()
    {
        SplitProperty.Changed.AddClassHandler<DockSplitPresenter>((presenter, e) => presenter.OnSplitChanged(e));
    }

    public DockSplitPresenter()
    {
        LogicalChildren.AddRange(new Control[] { _first, _splitter, _second });
        VisualChildren.AddRange(new Control[] { _first, _splitter, _second });

        _splitter.DragTo += OnSplitterDragTo;
        _splitter.DragCompleted += OnSplitterDragCompleted;
        _splitter.StepRequested += OnSplitterStep;
    }

    public DockSplitPane? Split
    {
        get => GetValue(SplitProperty);
        set => SetValue(SplitProperty, value);
    }

    internal Dock? Owner { get; set; }

    private double MinPaneSize => Owner?.MinPaneSize ?? Dock.DefaultMinPaneSize;

    private bool IsHorizontal => Split?.Orientation != Orientation.Vertical;

    protected override Size MeasureOverride(Size availableSize)
    {
        _splitter.Measure(availableSize);

        var thickness = Thickness(availableSize);
        var extent = Extent(availableSize) - thickness;
        var (firstExtent, secondExtent) = Divide(extent);

        _first.Measure(WithExtent(availableSize, firstExtent));
        _second.Measure(WithExtent(availableSize, secondExtent));

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var thickness = Thickness(finalSize);
        var extent = Extent(finalSize) - thickness;
        var (firstExtent, secondExtent) = Divide(extent);

        if (IsHorizontal)
        {
            _first.Arrange(new Rect(0, 0, firstExtent, finalSize.Height));
            _splitter.Arrange(new Rect(firstExtent, 0, thickness, finalSize.Height));
            _second.Arrange(new Rect(firstExtent + thickness, 0, secondExtent, finalSize.Height));
        }
        else
        {
            _first.Arrange(new Rect(0, 0, finalSize.Width, firstExtent));
            _splitter.Arrange(new Rect(0, firstExtent, finalSize.Width, thickness));
            _second.Arrange(new Rect(0, firstExtent + thickness, finalSize.Width, secondExtent));
        }

        return finalSize;
    }

    /// <summary>
    /// Applies the floor. When the extent cannot hold both minimums the panes
    /// take their minimum and overflow, rather than collapsing.
    /// </summary>
    private (double First, double Second) Divide(double extent)
    {
        var min = MinPaneSize;
        var ratio = Split?.Ratio ?? 0.5;

        if (extent <= 0)
        {
            return (0, 0);
        }

        if (extent < min * 2)
        {
            return (min, min);
        }

        var first = Math.Clamp(extent * ratio, min, extent - min);
        return (first, extent - first);
    }

    private double Thickness(Size size)
        => IsHorizontal ? _splitter.DesiredSize.Width : _splitter.DesiredSize.Height;

    private double Extent(Size size) => IsHorizontal ? size.Width : size.Height;

    private Size WithExtent(Size available, double extent) => IsHorizontal
        ? new Size(extent, available.Height)
        : new Size(available.Width, extent);

    private void OnSplitChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is DockSplitPane previous)
        {
            previous.PropertyChanged -= OnSplitPropertyChanged;
        }

        if (e.NewValue is DockSplitPane current)
        {
            current.PropertyChanged += OnSplitPropertyChanged;
        }

        Sync();
    }

    private void OnSplitPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DockSplitPane.Ratio):
                InvalidateMeasure();
                break;

            case nameof(DockSplitPane.Orientation):
            case nameof(DockSplitPane.First):
            case nameof(DockSplitPane.Second):
                Sync();
                break;
        }
    }

    private void Sync()
    {
        _first.Owner = Owner;
        _second.Owner = Owner;
        _first.Pane = Split?.First;
        _second.Pane = Split?.Second;
        _splitter.Orientation = IsHorizontal ? Orientation.Horizontal : Orientation.Vertical;

        InvalidateMeasure();
    }

    /// <summary>
    /// Converts the grip's requested position straight into a ratio. Absolute
    /// rather than incremental, so no error can accumulate across the gesture
    /// and the grip cannot drift away from the cursor.
    /// </summary>
    internal void SetRatioFromPosition(Point position) => OnSplitterDragTo(position);

    private void OnSplitterDragTo(Point position)
    {
        if (Split is null)
        {
            return;
        }

        var extent = Extent(Bounds.Size) - Thickness(Bounds.Size);

        if (extent <= 0)
        {
            return;
        }

        var offset = IsHorizontal ? position.X : position.Y;
        Split.Ratio = ClampToFloor(offset / extent, extent);
    }

    /// <summary>
    /// Splitter drag is a continuous gesture, so the layout is written back once
    /// on completion rather than per frame (§9.2).
    /// </summary>
    private void OnSplitterDragCompleted() => Owner?.NotifyLayoutChanged();

    private void OnSplitterStep(double step)
    {
        if (Split is null)
        {
            return;
        }

        var extent = Extent(Bounds.Size) - Thickness(Bounds.Size);
        Split.Ratio = ClampToFloor(Split.Ratio + step, extent);
        Owner?.NotifyLayoutChanged();
    }

    /// <summary>Stops at the limit rather than continuing to move.</summary>
    private double ClampToFloor(double ratio, double extent)
    {
        if (extent < MinPaneSize * 2)
        {
            return Split?.Ratio ?? 0.5;
        }

        var floor = MinPaneSize / extent;
        return Math.Clamp(ratio, floor, 1d - floor);
    }
}
