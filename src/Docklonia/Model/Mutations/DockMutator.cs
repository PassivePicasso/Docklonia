using Avalonia;
using Avalonia.Layout;

namespace Docklonia.Model.Mutations;

/// <summary>
/// The single place layout mutation happens (§13). Same-<c>Dock</c> drag,
/// cross-window drag, placement seeding, menu commands, and deserialization all
/// call these methods — cross-window docking is a different <i>source of
/// coordinates</i> and seeding a different <i>source of direction</i>, never a
/// different docking implementation.
/// </summary>
public static class DockMutator
{
    /// <summary>
    /// Docks <paramref name="node"/> relative to <paramref name="target"/> in
    /// the direction a guide indicates (§6). Detachment from the origin and
    /// insertion at the destination are one operation.
    /// </summary>
    public static void Dock(DockLayout layout, IDockNode node, IDockNode target, DockDirection direction, double ratio = 0.5)
        => Move(layout, layout, node, target, direction, ratio);

    /// <summary>
    /// Moves a node out of <paramref name="from"/> and into
    /// <paramref name="to"/>. Cross-window docking is exactly this with the two
    /// layouts differing (§7.2 step 6) — there is no separate code path.
    /// </summary>
    /// <remarks>
    /// The source layout must be the one that actually <b>owns</b> the node.
    /// Detaching against the destination would follow the node's parent links
    /// correctly but leave the origin's own root pointing at a subtree that is no
    /// longer there, so the origin would keep presenting a node it no longer has.
    /// </remarks>
    public static void Move(DockLayout from, DockLayout to, IDockNode node, IDockNode target, DockDirection direction, double ratio = 0.5)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(target);

        if (ReferenceEquals(node, target))
        {
            return;
        }

        if (DockTree.Contains(node, target))
        {
            throw new InvalidOperationException("Cannot dock a node into its own subtree.");
        }

        TreeSurgery.Detach(from, node);
        var layout = to;

        if (direction == DockDirection.Center)
        {
            Tabify(layout, node, target);
        }
        else
        {
            Split(layout, node, target, direction, ratio);
        }

        from.MarkChanged();
        layout.MarkChanged();
    }

    /// <summary>
    /// Docks against the <c>Dock</c> root, so the node spans the full extent of
    /// that edge rather than subdividing a hovered pane. Outer guides (§6) and
    /// placement seeding (§3.9) are both this method — the only difference is
    /// where the direction came from.
    /// </summary>
    public static void DockToRoot(DockLayout layout, IDockNode node, DockDirection direction, double size = 0.25)
        => MoveToRoot(layout, layout, node, direction, size);

    /// <summary>Cross-layout form of <see cref="DockToRoot"/>; see <see cref="Move"/> for why the source matters.</summary>
    public static void MoveToRoot(DockLayout from, DockLayout to, IDockNode node, DockDirection direction, double size = 0.25)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(node);

        TreeSurgery.Detach(from, node);

        var layout = to;
        var root = layout.Root;

        if (root is null || ReferenceEquals(root, node))
        {
            layout.Root = node;
            layout.MarkChanged();
            return;
        }

        var ratio = direction is DockDirection.Left or DockDirection.Top ? size : 1d - size;

        if (direction == DockDirection.Center)
        {
            Tabify(layout, node, root);
        }
        else
        {
            Split(layout, node, root, direction, ratio);
        }

        layout.MarkChanged();
    }

    /// <summary>Removes a node and everything beneath it, collapsing the structure it leaves (§6.1).</summary>
    public static void Remove(DockLayout layout, IDockNode node)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(node);

        if (ReferenceEquals(layout.MaximizedPane, node) || DockTree.Contains(node, layout.MaximizedPane))
        {
            layout.MaximizedPane = null;
        }

        TreeSurgery.Detach(layout, node);
        layout.MarkChanged();
    }

    /// <summary>
    /// Detaches a subtree into a new <see cref="FloatPane"/> on the same layout.
    /// Floating out of a float produces a sibling float, never a nested one
    /// (§5.4).
    /// </summary>
    public static FloatPane Float(DockLayout layout, IDockNode node, PixelPoint position, Size size)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(node);

        TreeSurgery.Detach(layout, node);

        var host = new FloatPane(node, position, size);
        layout.Floats.Add(host);
        layout.MarkChanged();

        return host;
    }

    /// <summary>
    /// Re-docks a floated subtree into the main tree (§5.1 raft). Closing the
    /// float falls out of <see cref="TreeSurgery.Detach"/>.
    /// </summary>
    public static void Raft(DockLayout layout, FloatPane host, IDockNode target, DockDirection direction)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(host);

        Dock(layout, host.Child, target, direction);
    }

    /// <summary>Reorders a tab within its strip (§6.1). The same engine as every other mutation.</summary>
    public static void Reorder(DockLayout layout, IDockNode node, int index)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(node);

        if (node.Parent is not DockTabPane tabs)
        {
            throw new InvalidOperationException("Only a child of a tab pane can be reordered.");
        }

        tabs.Move(node, index);
        layout.MarkChanged();
    }

    /// <summary>
    /// Creates a second node over the same consumer object (§3.5). Duplication
    /// does not consult placement — the caller supplies an explicit target.
    /// </summary>
    public static DockContent Duplicate(DockLayout layout, DockContent source, IDockNode target, DockDirection direction)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(source);

        var copy = new DockContent(source.Content)
        {
            Title = source.Title,
            ContentKey = source.ContentKey,
            CanClose = source.CanClose,
            MenuItems = source.MenuItems,
        };

        if (direction == DockDirection.Center)
        {
            Tabify(layout, copy, target);
        }
        else
        {
            Split(layout, copy, target, direction);
        }

        layout.MarkChanged();
        return copy;
    }

    /// <summary>
    /// Merges into the target as a tab, producing or extending a
    /// <see cref="DockTabPane"/>. A dragged tab group merges its children in
    /// rather than nesting, which is the inverse of §6.1's promotion of a leaf
    /// into a composite.
    /// </summary>
    private static void Tabify(DockLayout layout, IDockNode node, IDockNode target)
    {
        if (target is DockTabPane existing)
        {
            AddToTabs(existing, node, existing.Children.Count);
            return;
        }

        var group = new DockTabPane();
        var slot = TreeSurgery.Capture(layout, target);

        DockPane.Orphan(target);
        group.Add(target);
        AddToTabs(group, node, group.Children.Count);

        TreeSurgery.Fill(layout, slot, target, group);
        group.SelectedChild = LastAdded(group, node);
    }

    private static void AddToTabs(DockTabPane tabs, IDockNode node, int index)
    {
        if (node is DockTabPane incoming)
        {
            foreach (var child in incoming.Children.ToArray())
            {
                incoming.Remove(child);
                tabs.Insert(index++, child);
            }

            tabs.Group ??= incoming.Group;
            tabs.SelectedChild = tabs.Children.Count > 0 ? tabs.Children[Math.Min(index - 1, tabs.Children.Count - 1)] : null;
            return;
        }

        tabs.Insert(index, node);
        tabs.SelectedChild = node;
    }

    private static IDockNode? LastAdded(DockTabPane tabs, IDockNode node)
        => tabs.Children.Contains(node) ? node : tabs.Children.LastOrDefault();

    private static void Split(DockLayout layout, IDockNode node, IDockNode target, DockDirection direction, double ratio = 0.5)
    {
        var orientation = direction is DockDirection.Left or DockDirection.Right
            ? Orientation.Horizontal
            : Orientation.Vertical;

        var leading = direction is DockDirection.Left or DockDirection.Top;
        var slot = TreeSurgery.Capture(layout, target);

        DockPane.Orphan(target);

        var split = leading
            ? new DockSplitPane(orientation, node, target, ratio)
            : new DockSplitPane(orientation, target, node, ratio);

        TreeSurgery.Fill(layout, slot, target, split);
    }
}
