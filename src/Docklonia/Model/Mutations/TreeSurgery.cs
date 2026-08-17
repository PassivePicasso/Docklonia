namespace Docklonia.Model.Mutations;

/// <summary>
/// Identifies the slot a node occupies, captured before that node is moved so
/// the slot can be refilled afterwards.
/// </summary>
internal readonly record struct DockSlot(IDockPane? Parent, int TabIndex, bool IsRoot);

/// <summary>
/// The primitive structural edits every layout mutation is built from:
/// refilling the slot a node occupies, and lifting a node out of the tree while
/// collapsing what it leaves behind.
/// </summary>
/// <remarks>
/// Detach collapses as it goes rather than leaving a hole for a later sweep to
/// find. <see cref="DockSplitPane"/> has two non-null slots, so a hole is not
/// representable — collapsing at the moment of removal is what keeps the
/// invariant true at every instant rather than only between operations.
/// </remarks>
internal static class TreeSurgery
{
    internal static DockSlot Capture(DockLayout layout, IDockNode node) => new(
        node.Parent,
        node.Parent is DockTabPane tabs ? tabs.IndexOf(node) : -1,
        node.Parent is null && ReferenceEquals(layout.Root, node));

    /// <summary>
    /// Puts <paramref name="replacement"/> into a captured slot, evicting
    /// whatever stale reference the slot still holds.
    /// </summary>
    internal static void Fill(DockLayout layout, DockSlot slot, IDockNode stale, IDockNode replacement)
    {
        switch (slot.Parent)
        {
            case DockSplitPane split:
                split.ReplaceChild(stale, replacement);
                break;

            case DockTabPane tabs:
                tabs.Remove(stale);
                tabs.Insert(slot.TabIndex < 0 ? tabs.Children.Count : slot.TabIndex, replacement);
                break;

            case FloatPane host:
                host.Child = replacement;
                break;

            case null when slot.IsRoot || layout.Root is null:
                layout.Root = replacement;
                break;

            default:
                throw new InvalidOperationException("Node is not attached to this layout.");
        }
    }

    /// <summary>Swaps <paramref name="existing"/> for <paramref name="replacement"/> in its parent slot.</summary>
    internal static void Replace(DockLayout layout, IDockNode existing, IDockNode replacement)
    {
        if (ReferenceEquals(existing, replacement))
        {
            return;
        }

        Fill(layout, Capture(layout, existing), existing, replacement);
    }

    /// <summary>
    /// Lifts a node out of the tree, collapsing what it leaves behind: a split
    /// gives way to its surviving child, an emptied tab pane detaches in turn,
    /// and a float whose child leaves is closed (§6.1).
    /// </summary>
    internal static void Detach(DockLayout layout, IDockNode node)
    {
        switch (node.Parent)
        {
            case DockSplitPane split:
            {
                var survivor = split.Other(node);
                var slot = Capture(layout, split);

                DockPane.Orphan(node);
                DockPane.Orphan(survivor);
                Fill(layout, slot, split, survivor);
                break;
            }

            case DockTabPane tabs:
            {
                tabs.Remove(node);

                // A persistent pane is a region the user arranged, not a
                // container for whatever happened to be in it, so emptying it
                // is not a reason to take it away.
                if (tabs.Children.Count == 0 && !tabs.IsPersistent)
                {
                    Detach(layout, tabs);
                }

                break;
            }

            case FloatPane host:
                DockPane.Orphan(node);
                layout.Floats.Remove(host);
                break;

            case null when ReferenceEquals(layout.Root, node):
                layout.Root = null;
                break;

            case null:
                break;

            default:
                throw new InvalidOperationException("Node is not attached to this layout.");
        }
    }
}
