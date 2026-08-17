using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Docklonia.Dragging;
using Docklonia.Model;
using Docklonia.Model.Mutations;

namespace Docklonia.Controls;

/// <summary>
/// Turns pointer gestures on tabs and titlebars into drag sessions, and answers
/// the session's target-resolution questions for this <c>Dock</c> (§7).
/// </summary>
/// <remarks>
/// <para>Reorder and tear-out begin with the same gesture, so they are
/// disambiguated by position (§6.1): inside the strip the gesture reorders and
/// shows no guides; leaving the strip turns it into a normal drag with full
/// docking behaviour; returning reverts to reordering.</para>
///
/// <para>The pointer is captured on the <b>control that started the gesture</b>,
/// never on the <c>Dock</c>. A pane inside a <c>FloatPane</c> lives in a
/// different <see cref="TopLevel"/>, and capture does not cross top levels — so
/// capturing on the <c>Dock</c> would silently break every drag that begins in a
/// floating window.</para>
/// </remarks>
internal sealed class DockDragController
{
    private readonly Dock _dock;
    private readonly DockGuideOverlay _guides;

    private DockDragSession? _session;
    private DockTab? _gestureTab;
    private IDockNode? _gestureNode;
    private FloatPane? _gestureFloat;
    private Visual? _gestureSource;
    private Point _gestureOrigin;
    private OverlayLayer? _guideLayer;

    internal DockDragController(Dock dock, DockGuideOverlay guides)
    {
        _dock = dock;
        _guides = guides;
    }

    private DockLayout Layout => _dock.EnsureLayout();

    internal bool IsDragging => _session is not null;

    /// <summary>A tab press. Past the threshold this becomes a reorder or a drag.</summary>
    internal void BeginTabGesture(DockTab tab, PointerPressedEventArgs e)
    {
        Begin(tab, tab.Node, e);
        _gestureTab = tab;
    }

    /// <summary>
    /// A titlebar press. Drags the whole pane; when that pane is the entire
    /// contents of a floating window, the window itself becomes the drag visual.
    /// </summary>
    internal void BeginPaneGesture(DockPaneControl pane, PointerPressedEventArgs e)
    {
        Begin(pane, pane.Node, e);

        var host = DockTree.FloatOf(pane.Node);
        _gestureFloat = host is not null && ReferenceEquals(host.Child, pane.Node) ? host : null;
    }

    private void Begin(Visual source, IDockNode? node, PointerPressedEventArgs e)
    {
        _gestureSource = source;
        _gestureTab = null;
        _gestureFloat = null;
        _gestureNode = node;
        _gestureOrigin = e.GetPosition(source);

        e.Pointer.Capture(source as IInputElement);
    }

    internal void OnPointerMoved(Visual source, PointerEventArgs e)
    {
        var screen = source.PointToScreen(e.GetPosition(source));

        if (_session is not null)
        {
            _session.Update(screen);
            return;
        }

        if (_gestureNode is null || _gestureSource is null)
        {
            return;
        }

        if (Point.Distance(e.GetPosition(_gestureSource), _gestureOrigin) < Dock.DragThreshold)
        {
            return;
        }

        if (_gestureTab is not null && IsInsideStrip(_gestureTab, e))
        {
            Reorder(_gestureTab, e);
            return;
        }

        StartDrag(screen);
    }

    internal void OnPointerReleased(PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);

        _session?.Complete();
        _session = null;
        EndGesture();
    }

    /// <summary>Escape, or loss of capture. Nothing was detached, so nothing is undone (§7.2 step 8).</summary>
    internal void CancelGesture()
    {
        _session?.Cancel();
        _session = null;
        EndGesture();
    }

    /// <summary>
    /// Whether the point falls on any surface this <c>Dock</c> owns — its own
    /// control or any of its floating windows.
    /// </summary>
    /// <remarks>
    /// Separate from pane hit-testing because an <b>empty</b> <c>Dock</c> has no
    /// panes yet still accepts a drop: the outer guides already cover docking
    /// into an empty <c>Dock</c> (§9), and without this a second window could
    /// never receive its first pane.
    /// </remarks>
    internal bool ContainsScreenPoint(PixelPoint screen)
    {
        if (Contains(_dock, screen))
        {
            return true;
        }

        return _dock.FloatSurfaces.Any(host => host.RootVisual is { } visual && Contains(visual, screen));
    }

    /// <summary>
    /// The innermost pane under a screen point, or null when the point is over
    /// the <c>Dock</c> but not over any pane. Floating surfaces are tested first,
    /// since they sit above their owner.
    /// </summary>
    internal DockPaneControl? HitTest(PixelPoint screen, IDockNode? payload)
    {
        var panes = _dock.PaneControls
            .Where(pane => !IsExcluded(pane, payload))
            .OrderByDescending(pane => DockTree.FloatOf(pane.Node) is not null);

        foreach (var pane in panes)
        {
            if (Contains(pane, screen))
            {
                return pane;
            }
        }

        return null;
    }

    /// <summary>
    /// A dragged subtree cannot be its own drop target, and neither can anything
    /// inside the floating window currently being moved — that window is the drag
    /// visual, and hit-testing it would mask the surfaces beneath it.
    /// </summary>
    private bool IsExcluded(DockPaneControl pane, IDockNode? payload)
    {
        if (!pane.IsAttachedToVisualTree() || TopLevel.GetTopLevel(pane) is null)
        {
            return true;
        }

        if (payload is not null && DockTree.Contains(payload, pane.Node))
        {
            return true;
        }

        return _session?.MovingFloat is { } moving && ReferenceEquals(DockTree.FloatOf(pane.Node), moving);
    }

    /// <summary>
    /// Positions the guides over the resolved surface and reports which one the
    /// cursor is on. Exactly one <c>Dock</c> shows guides at a time (§7.2 step 5).
    /// </summary>
    /// <remarks>
    /// The overlay is hosted in the <see cref="OverlayLayer"/> of whichever
    /// <see cref="TopLevel"/> the target lives in, and moves between them as the
    /// cursor does. Anchoring it to the <c>Dock</c>'s own window instead would
    /// draw the guides for a floating target in the wrong window entirely.
    /// </remarks>
    internal (DockDirection Direction, bool IsOuter)? UpdateGuides(IDockNode? payload, DockPaneControl? pane, PixelPoint screen)
    {
        var inFloat = pane is not null && !ReferenceEquals(TopLevel.GetTopLevel(pane), TopLevel.GetTopLevel(_dock));
        var anchor = inFloat ? (Visual)pane! : _dock;
        var layer = OverlayLayer.GetOverlayLayer(anchor);

        if (layer is null)
        {
            HideGuides();
            return null;
        }

        Host(layer);

        var surface = SurfaceBounds(layer, inFloat);

        Canvas.SetLeft(_guides, surface.X);
        Canvas.SetTop(_guides, surface.Y);
        _guides.Width = surface.Width;
        _guides.Height = surface.Height;
        _guides.IsVisible = true;

        _guides.PaneBounds = pane?.TranslatePoint(default, layer) is { } origin
            ? new Rect(origin.X - surface.X, origin.Y - surface.Y, pane.Bounds.Width, pane.Bounds.Height)
            : default;

        _guides.SetPermitted((direction, outer) => IsPermitted(payload, pane, direction, outer, inFloat));

        var local = layer.PointToClient(screen);
        var hit = _guides.HitTest(new Point(local.X - surface.X, local.Y - surface.Y));

        _guides.SetHot(hit?.Direction, hit?.IsOuter ?? false, PreviewFor(hit, pane, surface, layer));
        return hit;
    }

    internal void HideGuides()
    {
        _guides.SetHot(null, false, default);
        _guides.IsVisible = false;

        _guideLayer?.Children.Remove(_guides);
        _guideLayer = null;
    }

    /// <summary>
    /// Detach and insert as one operation, through the same engine used for
    /// same-<c>Dock</c> docking. No separate cross-window code path (§7.2 step 6).
    /// </summary>
    internal void CompleteDrop(IDockNode? node, object?[] payload, DockPaneControl? pane, DockDirection direction, bool isOuter)
    {
        var target = pane?.Node;
        var dropped = node ?? CreateNodeForExternalPayload(payload);

        if (dropped is null)
        {
            return;
        }

        if (isOuter || target is null || ReferenceEquals(target, dropped))
        {
            DockMutator.DockToRoot(Layout, dropped, direction);
        }
        else
        {
            DockMutator.Dock(Layout, dropped, target, direction);
        }

        _dock.ActivateNode(DockTree.ContentsIn(dropped).FirstOrDefault() ?? dropped);
        _dock.NotifyLayoutChanged();
    }

    internal void FloatAt(IDockNode node, PixelPoint screen) => _dock.Commands.FloatAtPointer(node, screen);

    /// <summary>
    /// Rule 3 of §6: a guide is shown only when its operation is permitted, so no
    /// guide is ever offered for a drop that would then be rejected.
    /// </summary>
    private bool IsPermitted(IDockNode? payload, DockPaneControl? pane, DockDirection direction, bool outer, bool inFloat)
    {
        // Outer guides act on the Dock root, which is not the surface being
        // shown when the cursor is over a floating window. Offering them there
        // would point at a region the user cannot see.
        if (outer && inFloat)
        {
            return false;
        }

        if (direction == DockDirection.Center)
        {
            // Tabbing has no size implication, so it survives even where the
            // Dock is too small for any split.
            return !outer && pane is not null && !IsSelfDrop(payload, pane);
        }

        if (!outer && (pane is null || IsSelfDrop(payload, pane)))
        {
            return false;
        }

        var extent = SplitExtent(outer ? _dock.Bounds.Size : pane!.Bounds.Size, direction);
        return extent >= _dock.MinPaneSize * 2;
    }

    private static bool IsSelfDrop(IDockNode? payload, DockPaneControl pane)
        => payload is not null && (ReferenceEquals(payload, pane.Node) || DockTree.Contains(payload, pane.Node));

    private static double SplitExtent(Size size, DockDirection direction)
        => direction is DockDirection.Left or DockDirection.Right ? size.Width : size.Height;

    /// <summary>The region the drop will occupy, in the guide overlay's own coordinates (§6.1).</summary>
    private Rect PreviewFor((DockDirection Direction, bool IsOuter)? hit, DockPaneControl? pane, Rect surface, Visual layer)
    {
        if (hit is not { } guide)
        {
            return default;
        }

        var bounds = new Rect(surface.Size);

        if (!guide.IsOuter && pane is not null && pane.TranslatePoint(default, layer) is { } origin)
        {
            bounds = new Rect(origin.X - surface.X, origin.Y - surface.Y, pane.Bounds.Width, pane.Bounds.Height);
        }

        return guide.Direction switch
        {
            DockDirection.Left => bounds.WithWidth(bounds.Width / 2),
            DockDirection.Right => new Rect(bounds.X + (bounds.Width / 2), bounds.Y, bounds.Width / 2, bounds.Height),
            DockDirection.Top => bounds.WithHeight(bounds.Height / 2),
            DockDirection.Bottom => new Rect(bounds.X, bounds.Y + (bounds.Height / 2), bounds.Width, bounds.Height / 2),
            _ => bounds,
        };
    }

    /// <summary>The area the guides cover: a whole floating window, or this <c>Dock</c> within its window.</summary>
    private Rect SurfaceBounds(Visual layer, bool inFloat)
    {
        if (inFloat)
        {
            return new Rect(layer.Bounds.Size);
        }

        return _dock.TranslatePoint(default, layer) is { } origin
            ? new Rect(origin, _dock.Bounds.Size)
            : new Rect(layer.Bounds.Size);
    }

    private void Host(OverlayLayer layer)
    {
        if (ReferenceEquals(_guideLayer, layer))
        {
            return;
        }

        _guideLayer?.Children.Remove(_guides);
        layer.Children.Add(_guides);
        _guideLayer = layer;
    }

    private static bool Contains(Visual visual, PixelPoint screen)
    {
        if (TopLevel.GetTopLevel(visual) is null || visual.Bounds.Width <= 0)
        {
            return false;
        }

        return new Rect(visual.Bounds.Size).Contains(visual.PointToClient(screen));
    }

    private void StartDrag(PixelPoint screen)
    {
        if (_gestureNode is null)
        {
            return;
        }

        _gestureTab?.SetDragging(true);
        _session = DockDragSession.Begin(_dock, _gestureNode, screen, _gestureFloat);
        _dock.SetDragging(true);
    }

    /// <summary>Starts a source-less drag from content plus a screen point (§7.4).</summary>
    internal void BeginExternal(object content, string? title, PixelPoint screen)
    {
        _session = DockDragSession.BeginSourceless(_dock, content, title, screen);
        _dock.SetDragging(true);
    }

    /// <summary>Pointer inside the strip: reorder, with no docking guides shown.</summary>
    private void Reorder(DockTab tab, PointerEventArgs e)
    {
        if (tab.Node?.Parent is not DockTabPane tabs || tab.Pane is null)
        {
            return;
        }

        var position = e.GetPosition(tab.Pane);
        var hovered = tab.Pane.Tabs.FirstOrDefault(candidate => TabBounds(tab.Pane, candidate).Contains(position));

        if (hovered is null || ReferenceEquals(hovered, tab) || hovered.Node is null)
        {
            return;
        }

        DockMutator.Reorder(Layout, tab.Node, tabs.IndexOf(hovered.Node));
        _dock.NotifyLayoutChanged();
    }

    private static Rect TabBounds(DockPaneControl pane, DockTab tab)
        => tab.TranslatePoint(default, pane) is { } origin ? new Rect(origin, tab.Bounds.Size) : default;

    private static bool IsInsideStrip(DockTab tab, PointerEventArgs e)
    {
        var strip = tab.GetVisualParent();
        return strip is not null && new Rect(strip.Bounds.Size).Contains(e.GetPosition(strip));
    }

    private IDockNode? CreateNodeForExternalPayload(object?[] payload)
    {
        var content = payload.FirstOrDefault(item => item is not null);

        if (content is null || !_dock.DescribesContent(content))
        {
            return null;
        }

        var node = new DockContent(content);
        _dock.Coordinator.Track(content, node);
        return node;
    }

    private void EndGesture()
    {
        _gestureTab?.SetDragging(false);
        _gestureTab = null;
        _gestureNode = null;
        _gestureFloat = null;
        _gestureSource = null;
        _dock.SetDragging(false);
        HideGuides();
    }
}
