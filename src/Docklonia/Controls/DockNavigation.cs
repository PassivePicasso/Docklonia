using Avalonia;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// Moving between panes and tabs, and opening the pane menu (§11).
/// </summary>
/// <remarks>
/// <para>These are the operations a key press performs. They are separate from
/// <see cref="DockKeyboard"/>, which only decides which key means which one, so
/// a host that binds its own keys reaches the same operations through
/// <see cref="Dock"/>.</para>
///
/// <para>Traversal between panes is geometric rather than tree-order, because
/// the user is reasoning about what they can see. Cycling uses the activation
/// list, so it follows the order panes were actually used.</para>
/// </remarks>
internal sealed class DockNavigation
{
    private readonly Dock _dock;

    internal DockNavigation(Dock dock)
    {
        _dock = dock;
    }

    internal bool CycleMostRecentlyUsed(bool backwards)
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

    internal bool StepTab(int delta)
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
    internal bool Traverse(DockDirection direction)
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

    internal bool OpenPaneMenu()
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

    /// <summary>
    /// A pane's centre in this <c>Dock</c>'s space. Its own bounds are local to
    /// the presenter that arranged it, where every pane shares one origin and
    /// no direction can be told from another.
    /// </summary>
    private Point CentreOf(DockPaneControl pane)
        => pane.TranslatePoint(new Point(pane.Bounds.Width / 2, pane.Bounds.Height / 2), _dock)
           ?? pane.Bounds.Center;

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
