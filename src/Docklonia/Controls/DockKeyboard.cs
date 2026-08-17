using Avalonia;
using Avalonia.Input;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// Full keyboard operation (§11) — a requirement, not an enhancement.
/// </summary>
/// <remarks>
/// <para>Drag-and-drop is inherently pointer-driven, so the <b>pane menu is what
/// makes docking keyboard-accessible</b>: it exposes every operation the drag
/// engine does, and this class is what opens it without a pointer.</para>
///
/// <para>Traversal between panes is geometric rather than tree-order, because
/// the user is reasoning about what they can see. Cycling uses the activation
/// list, so it follows the order panes were actually used.</para>
/// </remarks>
internal sealed class DockKeyboard
{
    private readonly Dock _dock;

    internal DockKeyboard(Dock dock)
    {
        _dock = dock;
    }

    internal bool Handle(KeyEventArgs e)
    {
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        return (e.Key, control, shift, alt) switch
        {
            // Most-recently-used order across panes, from the activation list.
            (Key.Tab, true, _, false) => CycleMostRecentlyUsed(shift),

            // Next / previous tab within the active pane.
            (Key.PageDown, true, false, false) => StepTab(1),
            (Key.PageUp, true, false, false) => StepTab(-1),

            // Cycle panes.
            (Key.F6, false, _, false) => CyclePanes(shift),

            // Directional traversal between panes.
            (Key.Left, false, false, true) => Traverse(DockDirection.Left),
            (Key.Right, false, false, true) => Traverse(DockDirection.Right),
            (Key.Up, false, false, true) => Traverse(DockDirection.Top),
            (Key.Down, false, false, true) => Traverse(DockDirection.Bottom),

            // The pane menu, which is the keyboard route to every docking operation.
            (Key.F10, false, true, false) => OpenPaneMenu(),
            (Key.Apps, false, false, false) => OpenPaneMenu(),

            _ => false,
        };
    }

    private bool CycleMostRecentlyUsed(bool backwards)
    {
        var panes = ActivePanes();

        if (panes.Count < 2)
        {
            return false;
        }

        var current = _dock.Layout?.ActivePane;
        var index = panes.FindIndex(pane => ReferenceEquals(pane.Node, current)
            || DockTree.Contains(pane.Node, current));

        var next = panes[Wrap(index + (backwards ? -1 : 1), panes.Count)];
        Activate(next);
        return true;
    }

    private bool CyclePanes(bool backwards) => CycleMostRecentlyUsed(backwards);

    private bool StepTab(int delta)
    {
        if (ActivePane()?.TabPane is not { } tabs || tabs.Children.Count < 2)
        {
            return false;
        }

        var index = tabs.SelectedChild is null ? 0 : tabs.IndexOf(tabs.SelectedChild);
        var target = tabs.Children[Wrap(index + delta, tabs.Children.Count)];

        _dock.ActivateNode(target);
        return true;
    }

    /// <summary>
    /// Nearest pane whose centre lies in the given direction, measured from the
    /// active pane's centre.
    /// </summary>
    private bool Traverse(DockDirection direction)
    {
        if (ActivePane() is not { } from)
        {
            return false;
        }

        var origin = CentreOf(from);

        var best = _dock.PaneControls
            .Where(pane => !ReferenceEquals(pane, from))
            .Select(pane => (Pane: pane, Centre: CentreOf(pane)))
            .Where(candidate => IsToward(origin, candidate.Centre, direction))
            .OrderBy(candidate => Distance(origin, candidate.Centre, direction))
            .Select(candidate => candidate.Pane)
            .FirstOrDefault();

        if (best is null)
        {
            return false;
        }

        Activate(best);
        return true;
    }

    private bool OpenPaneMenu()
    {
        if (ActivePane() is not { } pane)
        {
            return false;
        }

        var menu = DockMenuBuilder.BuildPaneMenu(_dock, pane);
        menu.PlacementTarget = pane;
        menu.Open(pane);
        return true;
    }

    private void Activate(DockPaneControl pane)
    {
        _dock.ActivateNode(pane.SelectedNode ?? pane.Node);
        pane.Focus();
    }

    private DockPaneControl? ActivePane()
    {
        var current = _dock.Layout?.ActivePane;

        return _dock.PaneControls.FirstOrDefault(pane => ReferenceEquals(pane.Node, current)
                   || DockTree.Contains(pane.Node, current))
               ?? _dock.PaneControls.FirstOrDefault();
    }

    private List<DockPaneControl> ActivePanes() => _dock.PaneControls.ToList();

    private static Point CentreOf(DockPaneControl pane) => pane.Bounds.Center;

    private static bool IsToward(Point from, Point to, DockDirection direction) => direction switch
    {
        DockDirection.Left => to.X < from.X,
        DockDirection.Right => to.X > from.X,
        DockDirection.Top => to.Y < from.Y,
        DockDirection.Bottom => to.Y > from.Y,
        _ => false,
    };

    /// <summary>
    /// Weights travel along the traversal axis over drift across it, so
    /// Alt+Right prefers the pane beside rather than the one diagonally away.
    /// </summary>
    private static double Distance(Point from, Point to, DockDirection direction)
    {
        var along = direction is DockDirection.Left or DockDirection.Right
            ? Math.Abs(to.X - from.X)
            : Math.Abs(to.Y - from.Y);

        var across = direction is DockDirection.Left or DockDirection.Right
            ? Math.Abs(to.Y - from.Y)
            : Math.Abs(to.X - from.X);

        return along + (across * 2);
    }

    private static int Wrap(int index, int count) => ((index % count) + count) % count;
}
