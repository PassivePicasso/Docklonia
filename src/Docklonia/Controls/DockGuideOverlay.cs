using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// Arranges both guide scopes and the drop preview over one <c>Dock</c> (§6).
/// </summary>
/// <remarks>
/// <para><b>Guides never overlap</b>, and that is resolved geometrically rather
/// than by a hit-priority rule: pane guides form a compact cluster at the
/// <i>centre</i> of the hovered pane, outer guides sit against the
/// <i>extreme edges</i> of the <c>Dock</c>. Those regions are disjoint by
/// construction, so every point belongs to at most one guide.</para>
///
/// <para>Three rules keep them disjoint without hiding a usable option. The
/// cluster is <b>not clipped to its pane</b> — it is centred on the hovered pane
/// but may overflow it, so a narrow tool pane still gets a full-size cluster and
/// the constraint is the <c>Dock</c>'s size rather than the pane's. Guides
/// <b>scale together</b> when space is genuinely tight, down to a documented
/// minimum hit size that is an accessibility floor rather than a cosmetic one.
/// And a guide is <b>shown only when its operation is permitted</b>, which
/// dissolves the degenerate case instead of special-casing it: where the
/// <c>Dock</c> is too small for both sets, the split guides are already illegal,
/// and the centre guide remains because tabbing has no size implication.</para>
/// </remarks>
public class DockGuideOverlay : Control
{
    /// <summary>Accessibility floor: a guide never shrinks below this and stays a reliable pointer target (§11).</summary>
    public const double MinimumHitSize = 28d;

    /// <summary>Preferred edge length of a single guide.</summary>
    public const double PreferredGuideSize = 44d;

    private const double ClusterCells = 3d;
    private const double OuterMargin = 8d;

    private readonly List<DockGuideButton> _paneGuides = new();
    private readonly List<DockGuideButton> _outerGuides = new();
    private readonly Border _preview = new() { IsHitTestVisible = false, IsVisible = false };

    private Rect _paneBounds;
    private double _guideSize = PreferredGuideSize;

    public DockGuideOverlay()
    {
        IsHitTestVisible = false;

        foreach (var direction in new[] { DockDirection.Left, DockDirection.Top, DockDirection.Right, DockDirection.Bottom, DockDirection.Center })
        {
            _paneGuides.Add(Add(new DockGuideButton { Direction = direction }));
        }

        foreach (var direction in new[] { DockDirection.Left, DockDirection.Top, DockDirection.Right, DockDirection.Bottom })
        {
            _outerGuides.Add(Add(new DockGuideButton { Direction = direction, IsOuter = true }));
        }

        VisualChildren.Add(_preview);
        LogicalChildren.Add(_preview);
    }

    /// <summary>The pane the cluster is centred on, in this overlay's coordinates.</summary>
    internal Rect PaneBounds
    {
        get => _paneBounds;
        set
        {
            _paneBounds = value;
            InvalidateArrange();
        }
    }

    /// <summary>Applies §6's rule 3 — never draw a guide for a drop that would be refused.</summary>
    internal void SetPermitted(Func<DockDirection, bool, bool> isPermitted)
    {
        foreach (var guide in _paneGuides)
        {
            guide.IsVisible = isPermitted(guide.Direction, false);
        }

        foreach (var guide in _outerGuides)
        {
            guide.IsVisible = isPermitted(guide.Direction, true);
        }

        InvalidateArrange();
    }

    /// <summary>Highlights the guide under the cursor and shows the region the drop will occupy.</summary>
    internal void SetHot(DockDirection? direction, bool outer, Rect preview)
    {
        foreach (var guide in _paneGuides.Concat(_outerGuides))
        {
            guide.IsHot = direction is { } hot && guide.Direction == hot && guide.IsOuter == outer;
        }

        _preview.IsVisible = direction is not null;
        _preview.Width = preview.Width;
        _preview.Height = preview.Height;
        Canvas.SetLeft(_preview, preview.X);
        Canvas.SetTop(_preview, preview.Y);
        InvalidateArrange();
    }

    /// <summary>
    /// Resolves a point to a guide. Returns null when the point is over no guide,
    /// which the drag session treats as "no drop here".
    /// </summary>
    internal (DockDirection Direction, bool IsOuter)? HitTest(Point point)
    {
        foreach (var guide in _outerGuides.Concat(_paneGuides))
        {
            if (guide.IsVisible && guide.Bounds.Contains(point))
            {
                return (guide.Direction, guide.IsOuter);
            }
        }

        return null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _guideSize = ChooseGuideSize(availableSize);

        var cell = new Size(_guideSize, _guideSize);

        foreach (var guide in _paneGuides.Concat(_outerGuides))
        {
            guide.Measure(cell);
        }

        _preview.Measure(availableSize);
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ArrangeCluster(finalSize);
        ArrangeOuter(finalSize);

        _preview.Arrange(new Rect(
            Canvas.GetLeft(_preview),
            Canvas.GetTop(_preview),
            _preview.Width,
            _preview.Height));

        return finalSize;
    }

    /// <summary>
    /// Both sets shrink together where the <c>Dock</c> is genuinely small, never
    /// below the accessibility floor.
    /// </summary>
    private static double ChooseGuideSize(Size available)
    {
        if (double.IsInfinity(available.Width) || double.IsInfinity(available.Height))
        {
            return PreferredGuideSize;
        }

        // The cluster needs three cells across, and the outer guides need one cell
        // at each edge plus their margins, so five cells is the tight budget.
        var budget = Math.Min(available.Width, available.Height) - (OuterMargin * 2);
        var fitted = budget / (ClusterCells + 2);

        return Math.Clamp(fitted, MinimumHitSize, PreferredGuideSize);
    }

    /// <summary>
    /// A compact plus centred on the hovered pane. Deliberately not clipped to
    /// that pane, so a narrow tool pane is not a cramped drop target.
    /// </summary>
    private void ArrangeCluster(Size finalSize)
    {
        var centre = _paneBounds.Width <= 0 || _paneBounds.Height <= 0
            ? new Point(finalSize.Width / 2, finalSize.Height / 2)
            : _paneBounds.Center;

        foreach (var guide in _paneGuides)
        {
            var offset = guide.Direction switch
            {
                DockDirection.Left => new Vector(-_guideSize, 0),
                DockDirection.Right => new Vector(_guideSize, 0),
                DockDirection.Top => new Vector(0, -_guideSize),
                DockDirection.Bottom => new Vector(0, _guideSize),
                _ => default,
            };

            guide.Arrange(new Rect(
                centre.X - (_guideSize / 2) + offset.X,
                centre.Y - (_guideSize / 2) + offset.Y,
                _guideSize,
                _guideSize));
        }
    }

    /// <summary>Against the extreme edges of the <c>Dock</c>, disjoint from the centre cluster.</summary>
    private void ArrangeOuter(Size finalSize)
    {
        foreach (var guide in _outerGuides)
        {
            var rect = guide.Direction switch
            {
                DockDirection.Left => new Rect(OuterMargin, (finalSize.Height - _guideSize) / 2, _guideSize, _guideSize),
                DockDirection.Right => new Rect(finalSize.Width - _guideSize - OuterMargin, (finalSize.Height - _guideSize) / 2, _guideSize, _guideSize),
                DockDirection.Top => new Rect((finalSize.Width - _guideSize) / 2, OuterMargin, _guideSize, _guideSize),
                _ => new Rect((finalSize.Width - _guideSize) / 2, finalSize.Height - _guideSize - OuterMargin, _guideSize, _guideSize),
            };

            guide.Arrange(rect);
        }
    }

    private DockGuideButton Add(DockGuideButton guide)
    {
        VisualChildren.Add(guide);
        LogicalChildren.Add(guide);
        return guide;
    }
}
