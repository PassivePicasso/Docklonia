using Docklonia.Descriptors;

namespace Docklonia.Model.Mutations;

/// <summary>
/// Decides where content with no node yet is docked (§3.9).
/// </summary>
/// <remarks>
/// <para><b>Placement is a seed, not a rule.</b> It is consulted only in the
/// three cases where an item has no node: a fresh layout, an item added at
/// runtime, and an item present in <c>ItemsSource</c> but absent from a loaded
/// layout. Once a node exists the layout wins — otherwise a saved layout would
/// fight the descriptors on every load.</para>
///
/// <para>Seeding is not a separate mechanism. It calls the same
/// <see cref="DockMutator"/> the drag session uses, with the direction supplied
/// by configuration instead of by a cursor.</para>
/// </remarks>
internal static class DockPlacement
{
    /// <summary>
    /// Docks a node that has no place yet. <paramref name="group"/> names the
    /// region it joins, or null for Active placement.
    /// </summary>
    internal static void Place(
        DockLayout layout,
        DockActivation activation,
        IDockNode node,
        string? group,
        IReadOnlyList<DockGroup> groups)
    {
        if (group is not null)
        {
            PlaceGrouped(layout, node, group, groups);
            return;
        }

        PlaceActive(layout, activation, node, groups);
    }

    /// <summary>
    /// Joins the group's pane, creating it from the group's seed only if it does
    /// not exist. Because the group's identity lives on the pane, later members
    /// join it wherever the user has since moved it.
    /// </summary>
    private static void PlaceGrouped(DockLayout layout, IDockNode node, string group, IReadOnlyList<DockGroup> groups)
    {
        if (FindGroupPane(layout, group) is { } existing)
        {
            DockMutator.Dock(layout, node, existing, DockDirection.Center);
            return;
        }

        var definition = groups.FirstOrDefault(candidate => candidate.Name == group);

        var pane = new DockTabPane
        {
            Group = group,
            IsPersistent = definition?.IsPersistent ?? false,
        };

        pane.Add(node);

        DockMutator.DockToRoot(layout, pane, definition?.Seed ?? DockDirection.Right, definition?.SeedSize ?? 0.25);
    }

    /// <summary>
    /// Opens in the last active pane holding ungrouped content. Naive "active
    /// pane" is wrong — focus the Inspector, open a file, and the document lands
    /// in the tool pane.
    /// </summary>
    private static void PlaceActive(DockLayout layout, DockActivation activation, IDockNode node, IReadOnlyList<DockGroup> groups)
    {
        activation.Prune(layout);

        if (activation.LastMatching(IsUngroupedTabPane) is DockTabPane active)
        {
            DockMutator.Dock(layout, node, active, DockDirection.Center);
            return;
        }

        if (FirstUngroupedPane(layout) is { } anywhere)
        {
            DockMutator.Dock(layout, node, anywhere, DockDirection.Center);
            return;
        }

        CreateUngroupedRootPane(layout, node, groups);
    }

    /// <summary>
    /// Creates the pane ungrouped content lives in. When the root is already
    /// occupied by a seeded group, the new pane takes the side that group seeded
    /// <i>away from</i>, and the remaining proportion — so a Right-seeded tool
    /// group at 0.25 leaves a document area of 0.75 on the left, which is what
    /// the seed already declared the intent to be.
    /// </summary>
    private static void CreateUngroupedRootPane(DockLayout layout, IDockNode node, IReadOnlyList<DockGroup> groups)
    {
        var pane = new DockTabPane();
        pane.Add(node);

        if (layout.Root is null)
        {
            DockMutator.DockToRoot(layout, pane, DockDirection.Center);
            return;
        }

        var occupying = DockTree.TabPanesIn(layout.Root).FirstOrDefault(tabs => tabs.Group is not null);
        var definition = groups.FirstOrDefault(candidate => candidate.Name == occupying?.Group);
        var seed = definition?.Seed ?? DockDirection.Right;
        var size = definition?.SeedSize ?? 0.25;

        if (seed == DockDirection.Center)
        {
            DockMutator.DockToRoot(layout, pane, DockDirection.Center);
            return;
        }

        DockMutator.DockToRoot(layout, pane, Opposite(seed), 1d - size);
    }

    /// <summary>
    /// A group's pane, searched across every surface — the main tree and every
    /// float — because floating a tool pane does not leave the group behind.
    /// </summary>
    private static DockTabPane? FindGroupPane(DockLayout layout, string group)
        => layout.AllPanes().OfType<DockTabPane>().FirstOrDefault(tabs => tabs.Group == group);

    private static DockTabPane? FirstUngroupedPane(DockLayout layout)
        => layout.AllPanes().OfType<DockTabPane>().FirstOrDefault(tabs => tabs.Group is null);

    private static bool IsUngroupedTabPane(IDockPane pane) => pane is DockTabPane { Group: null };

    private static DockDirection Opposite(DockDirection direction) => direction switch
    {
        DockDirection.Left => DockDirection.Right,
        DockDirection.Right => DockDirection.Left,
        DockDirection.Top => DockDirection.Bottom,
        DockDirection.Bottom => DockDirection.Top,
        _ => DockDirection.Center,
    };
}
