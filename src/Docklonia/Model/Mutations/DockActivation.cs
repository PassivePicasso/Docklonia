namespace Docklonia.Model.Mutations;

/// <summary>
/// Tracks logical focus as an activation-ordered list of panes (§3.11).
/// </summary>
/// <remarks>
/// <para>A single current value is not sufficient. Active placement needs
/// <i>the last active pane holding ungrouped content</i>, not simply the
/// currently active pane — focus the Inspector, open a file, and a lone
/// <c>ActivePane</c> gives the wrong answer.</para>
///
/// <para>Activating implies selecting: the pane is selected in every ancestor
/// tab group, so an active pane is always visible. Selecting does <b>not</b>
/// imply activating — a programmatic selection change leaves activation
/// untouched, because a view model that selects a tab has not asked to steal
/// the caret.</para>
///
/// <para>Only the current pane is persisted; the rest of the ordering is
/// runtime state seeded from it on load.</para>
/// </remarks>
internal sealed class DockActivation
{
    private readonly List<IDockPane> _order = new();

    internal IDockPane? Current => _order.Count > 0 ? _order[0] : null;

    /// <summary>
    /// Records a pane as most-recently-active and selects it through every
    /// ancestor tab group. Moving keyboard focus is the caller's job, and is
    /// gated on the <c>Dock</c> actually holding focus.
    /// </summary>
    internal void Activate(IDockPane? pane)
    {
        if (pane is null)
        {
            return;
        }

        _order.Remove(pane);
        _order.Insert(0, pane);

        SelectThroughAncestors(pane);
    }

    /// <summary>Most recently active pane satisfying a predicate — the query Active placement needs.</summary>
    internal IDockPane? LastMatching(Func<IDockPane, bool> predicate)
        => _order.FirstOrDefault(predicate);

    /// <summary>
    /// Drops panes that are no longer reachable, so a closed pane cannot be
    /// returned as an activation target.
    /// </summary>
    internal void Prune(DockLayout layout)
    {
        var live = new HashSet<IDockPane>(layout.AllPanes(), ReferenceEqualityComparer.Instance);
        _order.RemoveAll(pane => !live.Contains(pane));
    }

    /// <summary>
    /// Focus must not simply drop when a pane closes: without a pointer it
    /// would be unrecoverable (§11). Returns the pane to move to.
    /// </summary>
    internal IDockPane? NextAfterClosing(DockLayout layout, IDockPane closing)
    {
        Prune(layout);
        return _order.FirstOrDefault(pane => !ReferenceEquals(pane, closing) && !DockTree.Contains(closing, pane));
    }

    internal void SeedFrom(DockLayout layout)
    {
        _order.Clear();

        if (layout.ActivePane is { } persisted)
        {
            _order.Add(persisted);
        }
    }

    private static void SelectThroughAncestors(IDockPane pane)
    {
        var child = pane;

        foreach (var ancestor in DockTree.Ancestors(pane))
        {
            if (ancestor is DockTabPane tabs && child is IDockNode node)
            {
                tabs.SelectedChild = node;
            }

            child = ancestor;
        }
    }
}

file sealed class ReferenceEqualityComparer : IEqualityComparer<IDockPane>
{
    internal static readonly ReferenceEqualityComparer Instance = new();

    public bool Equals(IDockPane? x, IDockPane? y) => ReferenceEquals(x, y);

    public int GetHashCode(IDockPane obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
