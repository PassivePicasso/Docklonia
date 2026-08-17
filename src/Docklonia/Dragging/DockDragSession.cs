using Avalonia;
using Dock = Docklonia.Controls.Dock;
using Docklonia.Controls;
using Docklonia.Model;

namespace Docklonia.Dragging;

/// <summary>
/// A library-owned pointer drag (§7.1), driven entirely by a stream of screen
/// coordinates.
/// </summary>
/// <remarks>
/// <para><b>Not built on native drag-and-drop.</b> Not because native DnD cannot
/// carry an object reference — an in-process <c>DataObject</c> can — but because
/// its semantics, drag-feedback rendering, and availability differ per backend
/// and are absent or degraded on some targets. Screen coordinates are the one
/// thing every backend agrees on, so they are the only platform surface this
/// depends on.</para>
///
/// <para><b>Nothing is detached at drag start.</b> The node stays in its tree,
/// untouched, for the whole gesture; detachment and re-insertion happen together
/// at drop. Cancellation is therefore free — there is no original position to
/// restore, because nothing moved — and the tree is never in a transient state
/// that could be serialized.</para>
///
/// <para><b>A floating window drags as itself.</b> When the payload is the whole
/// contents of a <see cref="FloatPane"/>, that window <i>is</i> the drag visual:
/// it follows the cursor like an ordinary window, and no ghost is shown. Guides
/// still resolve against every surface underneath it, so a float re-docks by the
/// same gesture that moves it — which is what makes a float behave like any
/// other pane rather than a special case.</para>
/// </remarks>
internal sealed class DockDragSession : IDisposable
{
    private readonly Dock? _origin;
    private readonly IDockNode? _node;
    private readonly object?[] _payloadContent;
    private readonly DragGhost? _ghost;
    private readonly FloatPane? _movingFloat;
    private readonly PixelVector _grabOffset;

    private Dock? _target;
    private DockPaneControl? _targetPane;
    private DockDirection? _direction;
    private bool _isOuter;
    private PixelPoint _screen;

    private DockDragSession(
        Dock? origin,
        IDockNode? node,
        object?[] payloadContent,
        DragGhost? ghost,
        FloatPane? movingFloat,
        PixelVector grabOffset)
    {
        _origin = origin;
        _node = node;
        _payloadContent = payloadContent;
        _ghost = ghost;
        _movingFloat = movingFloat;
        _grabOffset = grabOffset;
    }

    internal static DockDragSession? Current { get; private set; }

    /// <summary>The floating window acting as its own drag visual, if any.</summary>
    internal FloatPane? MovingFloat => _movingFloat;

    /// <summary>Begins a drag of an existing node.</summary>
    internal static DockDragSession Begin(Dock origin, IDockNode node, PixelPoint screen, FloatPane? movingFloat)
    {
        var content = DockTree.ContentsIn(node).Select(item => item.Content).ToArray();

        // The float is the drag visual, so a ghost would be a second one.
        var ghost = movingFloat is null ? DragGhost.Create(origin, node.Title) : null;
        var grab = movingFloat is null ? default : screen - movingFloat.Position;

        return Start(new DockDragSession(origin, node, content, ghost, movingFloat, grab), screen);
    }

    /// <summary>
    /// Begins a drag from content plus a screen point, with no originating pane
    /// (§7.4). Lets an application implement external drops itself without the
    /// library taking on native drag-and-drop.
    /// </summary>
    internal static DockDragSession BeginSourceless(Dock anchor, object content, string? title, PixelPoint screen)
        => Start(
            new DockDragSession(null, null, new[] { (object?)content }, DragGhost.Create(anchor, title), null, default),
            screen);

    private static DockDragSession Start(DockDragSession session, PixelPoint screen)
    {
        Current?.Cancel();
        Current = session;
        session.Update(screen);
        return session;
    }

    /// <summary>True when this gesture had no originating node, so there is nothing to return to.</summary>
    internal bool IsSourceless => _node is null;

    /// <summary>
    /// Resolves the surface under the cursor, checks acceptance, and asks exactly
    /// one <c>Dock</c> to show guides.
    /// </summary>
    internal void Update(PixelPoint screen)
    {
        _screen = screen;

        _ghost?.MoveTo(screen);
        _movingFloat?.MoveTo(screen - _grabOffset);

        var resolved = Resolve(screen);

        if (!ReferenceEquals(resolved.Dock, _target))
        {
            _target?.Drag.HideGuides();
            _target = resolved.Dock;
        }

        _targetPane = resolved.Pane;
        var guide = _target?.Drag.UpdateGuides(_node, _targetPane, screen);

        _direction = guide?.Direction;
        _isOuter = guide?.IsOuter ?? false;
    }

    /// <summary>
    /// Detaches from the origin and inserts at the target as one operation, using
    /// the same mutation engine as same-<c>Dock</c> docking. There is no separate
    /// cross-window code path.
    /// </summary>
    internal void Complete()
    {
        try
        {
            if (_target is not null && _direction is { } direction)
            {
                _target.Drag.CompleteDrop(_node, _payloadContent, _targetPane, direction, _isOuter);
                return;
            }

            DropOnNothing();
        }
        finally
        {
            Dispose();
        }
    }

    /// <summary>
    /// Releasing over no accepting target floats the node on its <b>origin</b>
    /// <c>Dock</c> — never an arbitrary one, since only the origin is known to
    /// describe it. A float that was already floating simply stays where the user
    /// dropped it, and a source-less drag cancels, because neither has an origin
    /// to return to (§7.4).
    /// </summary>
    private void DropOnNothing()
    {
        if (_movingFloat is not null)
        {
            // Moving a window is a continuous gesture, so its geometry is written
            // back once, here, rather than per frame (§9.2).
            _origin?.NotifyLayoutChanged();
            return;
        }

        if (_origin is null || _node is null)
        {
            return;
        }

        _origin.Drag.FloatAt(_node, _screen);
    }

    /// <summary>Escape, or loss of pointer capture. Nothing was detached, so there is nothing to undo.</summary>
    internal void Cancel() => Dispose();

    /// <summary>
    /// Converts the screen point into each registered surface's own coordinate
    /// space and hit-tests for the innermost pane. A <c>Dock</c> under the cursor
    /// with no pane there is still a target, so an empty <c>Dock</c> can receive
    /// its first pane through the outer guides.
    /// </summary>
    private (Dock? Dock, DockPaneControl? Pane) Resolve(PixelPoint screen)
    {
        foreach (var dock in DockRegistry.Docks.Reverse())
        {
            if (!Accepts(dock) || !dock.Drag.ContainsScreenPoint(screen))
            {
                continue;
            }

            return (dock, dock.Drag.HitTest(screen, _node));
        }

        return (null, null);
    }

    /// <summary>
    /// Describe-and-forbid gates every drop (§7). A <c>Dock</c> that cannot
    /// describe the payload shows nothing and cannot be dropped on — silently,
    /// because that is the intended tool-area / document-area separation rather
    /// than an error.
    /// </summary>
    private bool Accepts(Dock dock)
        => _payloadContent.Length > 0 && _payloadContent.All(dock.DescribesContent);

    internal object?[] PayloadContent => _payloadContent;

    internal Dock? Origin => _origin;

    public void Dispose()
    {
        _target?.Drag.HideGuides();
        _target = null;
        _ghost?.Dispose();

        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }
}
