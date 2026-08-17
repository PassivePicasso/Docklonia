using System.Collections.ObjectModel;

namespace Docklonia.Descriptors;

/// <summary>
/// A <c>Dock</c>'s descriptor set as one named type, so a set can be declared
/// once in a <c>ResourceDictionary</c> and shared by several <c>Dock</c>s
/// (§3.7).
/// </summary>
/// <remarks>
/// <para>Named and non-generic because a XAML resource and a style
/// <c>Setter</c> both need a type they can name and construct.</para>
///
/// <para>Sharing one instance across <c>Dock</c>s is safe: a
/// <see cref="DockItemDescriptor"/> holds unevaluated bindings and no owner
/// state, and each <c>Dock</c> realizes them per item.</para>
/// </remarks>
public sealed class DockItemDescriptors : ObservableCollection<DockItemDescriptor>
{
    public DockItemDescriptors()
    {
    }

    public DockItemDescriptors(IEnumerable<DockItemDescriptor> descriptors) : base(descriptors)
    {
    }
}
