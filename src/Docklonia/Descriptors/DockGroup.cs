using Docklonia.Model;

namespace Docklonia.Descriptors;

/// <summary>
/// A named layout region, declared once on the <c>Dock</c> (§3.9).
/// </summary>
/// <remarks>
/// <para>The seed lives here rather than on each descriptor so that
/// contradictory seeds for one group are unrepresentable rather than merely
/// discouraged, and so a descriptor stays about the <i>item</i> while the group
/// is about the <i>region</i>.</para>
///
/// <para>Placement is a seed, not a rule: it is consulted only when an item has
/// no node. Once the group's pane exists, later members join it wherever the
/// user has since moved it.</para>
/// </remarks>
public sealed class DockGroup
{
    /// <summary>Name referenced by <see cref="DockItemDescriptor.Group"/> and by <see cref="DockItem.Group"/>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Where the group's pane is created the first time it is needed, applied
    /// against the <c>Dock</c> <b>root</b> rather than the active pane — which
    /// is what makes it predictable, since a bottom-seeded group spans the full
    /// width regardless of what happened to be focused.
    /// </summary>
    /// <remarks>
    /// <see cref="DockDirection.Center"/> tabs into the root's existing pane, so
    /// the group is born sharing the document area rather than owning a region.
    /// </remarks>
    public DockDirection Seed { get; set; } = DockDirection.Right;

    /// <summary>Proportion of the root given to the group's pane when it is seeded.</summary>
    public double SeedSize { get; set; } = 0.25;
}
