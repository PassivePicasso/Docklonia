using System.ComponentModel;

namespace Docklonia.Model;

/// <summary>
/// A node in a dock layout tree. Implemented only by library types (§3.6);
/// <see cref="DockPane"/>'s constructor is internal, so the set of
/// implementations is closed.
/// </summary>
public interface IDockPane : INotifyPropertyChanged
{
    /// <summary>Identity of the node itself, distinct from any content key (§8).</summary>
    string Id { get; }

    /// <summary>Display title. A projection of content metadata, never authored state.</summary>
    string? Title { get; }

    bool IsVisible { get; set; }

    IDockPane? Parent { get; }

    IReadOnlyList<IDockNode> Children { get; }
}

/// <summary>
/// A pane that may be a child of a composite pane. <see cref="FloatPane"/>
/// deliberately does not implement this, which is what makes a nested float
/// unrepresentable rather than merely discouraged (§5.2).
/// </summary>
public interface IDockNode : IDockPane
{
}
