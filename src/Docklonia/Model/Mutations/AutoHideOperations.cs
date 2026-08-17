using Docklonia.Descriptors;

namespace Docklonia.Model.Mutations;

/// <summary>
/// Minimize-as-auto-hide, and the restore that makes it worth having (§5.3).
/// </summary>
/// <remarks>
/// <para>Minimizing does not collapse a pane in place: the pane leaves the tree
/// and is parked on an edge strip. Restoring to the original location is the
/// entire point, so — unlike group position, where amnesia is accepted — the
/// restore target is persisted.</para>
///
/// <para>The target cannot be a path, because unrelated docking operations
/// invalidate paths while a pane sits hidden. It is a <b>relative anchor</b>: a
/// surviving sibling's id plus the direction the pane sat in relative to it,
/// falling back to the placement seed when that sibling is gone too.</para>
/// </remarks>
internal static class AutoHideOperations
{
    /// <summary>Removes the pane from the tree and parks it, recording how to put it back.</summary>
    internal static AutoHideEntry Hide(DockLayout layout, IDockNode pane, DockEdge edge)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(pane);

        var (anchorId, direction, ratio) = CaptureAnchor(pane);

        if (ReferenceEquals(layout.MaximizedPane, pane) || DockTree.Contains(pane, layout.MaximizedPane))
        {
            layout.MaximizedPane = null;
        }

        TreeSurgery.Detach(layout, pane);

        var entry = new AutoHideEntry(pane, edge, anchorId, direction, ratio);
        layout.AutoHidden.Add(entry);
        layout.MarkChanged();

        return entry;
    }

    /// <summary>
    /// Re-pins the pane. The anchor is resolved against the live tree; when it
    /// no longer exists the pane falls back to its group's seed, and failing
    /// that to the root.
    /// </summary>
    internal static void Restore(DockLayout layout, AutoHideEntry entry, IReadOnlyList<DockGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(entry);

        layout.AutoHidden.Remove(entry);

        var anchor = entry.AnchorId is null
            ? null
            : layout.AllPanes().OfType<IDockNode>().FirstOrDefault(pane => pane.Id == entry.AnchorId);

        if (anchor is not null)
        {
            DockMutator.Dock(layout, entry.Pane, anchor, entry.AnchorDirection, entry.Ratio);
            return;
        }

        var group = (entry.Pane as DockTabPane)?.Group;
        var definition = groups.FirstOrDefault(candidate => candidate.Name == group);

        DockMutator.DockToRoot(
            layout,
            entry.Pane,
            definition?.Seed ?? ToDirection(entry.Edge),
            definition?.SeedSize ?? entry.Ratio);
    }

    /// <summary>
    /// Records the surviving sibling and the side the pane occupied. A pane that
    /// was the whole root has no sibling, so it restores through the seed path.
    /// </summary>
    private static (string? AnchorId, DockDirection Direction, double Ratio) CaptureAnchor(IDockNode pane)
    {
        if (pane.Parent is DockSplitPane split)
        {
            var sibling = split.Other(pane);
            var leading = ReferenceEquals(split.First, pane);

            var direction = split.Orientation == Avalonia.Layout.Orientation.Horizontal
                ? leading ? DockDirection.Left : DockDirection.Right
                : leading ? DockDirection.Top : DockDirection.Bottom;

            return (sibling.Id, direction, split.Ratio);
        }

        if (pane.Parent is DockTabPane tabs)
        {
            return (tabs.Id, DockDirection.Center, 0.5);
        }

        return (null, DockDirection.Right, 0.25);
    }

    private static DockDirection ToDirection(DockEdge edge) => edge switch
    {
        DockEdge.Left => DockDirection.Left,
        DockEdge.Top => DockDirection.Top,
        DockEdge.Right => DockDirection.Right,
        _ => DockDirection.Bottom,
    };
}
