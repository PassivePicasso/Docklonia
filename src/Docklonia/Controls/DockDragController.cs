using Avalonia;
using Avalonia.Controls;
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
/// </remarks>
internal sealed class DockDragController
{
    private readonly Dock _dock;

    private DockDragSession? _session;
    private DockTab? _gestureTab;
    private IDockNode? _gestureNode;
    private Point _gestureOrigin;
    private bool _reordering;

    internal DockDragController(Dock dock)
    {
        _dock = dock;
    }

    private DockLayout Layout => _dock.EnsureLayout();

    internal bool IsDragging => _session is not null;

    /// <summary>A tab press. Past the threshold this becomes a reorder or a drag.</summary>
    internal void BeginTabGesture(DockTab tab, PointerPressedEventArgs e)
    {
        _gestureTab = tab;
        _gestureNode = tab.Node;
        _gestureOrigin = e.GetPosition(_dock);
        _reordering = false;

        e.Pointer.Capture(_dock);
    }

    /// <summary>A titlebar press. Drags the whole pane, never a single tab.</summary>
    internal void BeginPaneGesture(DockPaneControl pane, PointerPressedEventArgs e)
    {
        _gestureTab = null;
        _gestureNode = pane.Node;
        _gestureOrigin = e.GetPosition(_dock);
        _reordering = false;

        e.Pointer.Capture(_dock);
    }

    internal void OnPointerMoved(PointerEventArgs e)
    {
        var screen = ToScreen(e);

        if (_session is not null)
        {
            _session.Update(screen);
            return;
        }

        if (_gestureNode is null)
        {
            return;
        }

        var position = e.GetPosition(_dock);
        var travelled = Point.Distance(position, _gestureOrigin);

        if (travelled < Dock.DragThreshold)
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

    /// <summary>The innermost pane under a screen point, across this <c>Dock</c>'s own surface and its floats.</summary>
    internal DockPaneControl? HitTest(PixelPoint screen)
    {
        foreach (var pane in _dock.PaneControls)
        {
            if (!pane.IsAttachedToVisualTree() || TopLevel.GetTopLevel(pane) is null)
            {
                continue;
            }

            var local = pane.PointToClient(screen);

            if (new Rect(pane.Bounds.Size).Contains(local))
            {
                return pane;
            }
        }

        return null;
    }

    /// <summary>
    /// Positions the guides over the hovered pane and reports which one the
    /// cursor is on. Rendered by the resolved target <c>Dock</c>, in its own
    /// overlay; exactly one <c>Dock</c> shows guides at a time (§7.2 step 5).
    /// </summary>
    internal (DockDirection Direction, bool IsOuter)? UpdateGuides(IDockNode? payload, DockPaneControl? pane, PixelPoint screen)
    {
        var overlay = _dock.Guides;
        overlay.IsVisible = true;

        overlay.PaneBounds = pane is not null && pane.TranslatePoint(default, _dock) is { } origin
            ? new Rect(origin, pane.Bounds.Size)
            : default;

        overlay.SetPermitted((direction, outer) => IsPermitted(payload, pane, direction, outer));

        var local = _dock.PointToClient(screen);
        var hit = overlay.HitTest(local);

        overlay.SetHot(hit?.Direction, hit?.IsOuter ?? false, PreviewFor(hit, pane));
        return hit;
    }

    internal void HideGuides()
    {
        _dock.Guides.SetHot(null, false, default);
        _dock.Guides.IsVisible = false;
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

        if (isOuter || target is null)
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
    /// guide is ever offered for a drop that would then be rejected. A split is
    /// offered only if both resulting panes would satisfy <c>MinPaneSize</c>.
    /// </summary>
    private bool IsPermitted(IDockNode? payload, DockPaneControl? pane, DockDirection direction, bool outer)
    {
        if (direction == DockDirection.Center)
        {
            // Tabbing has no size implication, so it survives even where the
            // Dock is too small for any split.
            return !outer && pane is not null && !IsSelfDrop(payload, pane);
        }

        var extent = SplitExtent(outer ? _dock.Bounds.Size : pane?.Bounds.Size ?? default, direction);
        return extent >= _dock.MinPaneSize * 2 && (outer || (pane is not null && !IsSelfDrop(payload, pane)));
    }

    private static bool IsSelfDrop(IDockNode? payload, DockPaneControl pane)
        => payload is not null && (ReferenceEquals(payload, pane.Node) || DockTree.Contains(payload, pane.Node));

    private static double SplitExtent(Size size, DockDirection direction)
        => direction is DockDirection.Left or DockDirection.Right ? size.Width : size.Height;

    /// <summary>The region the drop will occupy, shown as a preview (§6.1).</summary>
    private Rect PreviewFor((DockDirection Direction, bool IsOuter)? hit, DockPaneControl? pane)
    {
        if (hit is not { } guide)
        {
            return default;
        }

        var bounds = guide.IsOuter || pane is null
            ? new Rect(_dock.Bounds.Size)
            : pane.TranslatePoint(default, _dock) is { } origin
                ? new Rect(origin, pane.Bounds.Size)
                : new Rect(_dock.Bounds.Size);

        return guide.Direction switch
        {
            DockDirection.Left => bounds.WithWidth(bounds.Width / 2),
            DockDirection.Right => new Rect(bounds.X + (bounds.Width / 2), bounds.Y, bounds.Width / 2, bounds.Height),
            DockDirection.Top => bounds.WithHeight(bounds.Height / 2),
            DockDirection.Bottom => new Rect(bounds.X, bounds.Y + (bounds.Height / 2), bounds.Width, bounds.Height / 2),
            _ => bounds,
        };
    }

    private void StartDrag(PixelPoint screen)
    {
        if (_gestureNode is null)
        {
            return;
        }

        _gestureTab?.SetDragging(true);
        _session = DockDragSession.Begin(_dock, _gestureNode, screen);
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

        _reordering = true;
        DockMutator.Reorder(Layout, tab.Node, tabs.IndexOf(hovered.Node));
        _dock.NotifyLayoutChanged();
    }

    private static Rect TabBounds(DockPaneControl pane, DockTab tab)
        => tab.TranslatePoint(default, pane) is { } origin ? new Rect(origin, tab.Bounds.Size) : default;

    private static bool IsInsideStrip(DockTab tab, PointerEventArgs e)
    {
        if (tab.Pane is null)
        {
            return false;
        }

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

    private PixelPoint ToScreen(PointerEventArgs e) => _dock.PointToScreen(e.GetPosition(_dock));

    private void EndGesture()
    {
        _gestureTab?.SetDragging(false);
        _gestureTab = null;
        _gestureNode = null;
        _reordering = false;
        _dock.SetDragging(false);
        HideGuides();
    }
}
