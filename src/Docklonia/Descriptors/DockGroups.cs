using System.Collections.ObjectModel;

namespace Docklonia.Descriptors;

/// <summary>
/// A <c>Dock</c>'s group definitions as one named type, shareable from a
/// <c>ResourceDictionary</c> alongside the descriptor set that names them
/// (§3.9).
/// </summary>
/// <remarks>
/// A shared descriptor whose <c>Group</c> names a region is only reusable if
/// the region travels with it, which is why this mirrors
/// <see cref="DockItemDescriptors"/>. A <see cref="DockGroup"/> is a seed
/// declaration with no per-<c>Dock</c> state, so one instance may serve
/// several.
/// </remarks>
public sealed class DockGroups : ObservableCollection<DockGroup>
{
    public DockGroups()
    {
    }

    public DockGroups(IEnumerable<DockGroup> groups) : base(groups)
    {
    }
}
