using System.Collections;

namespace Docklonia.Model;

/// <summary>
/// A leaf node wrapping one piece of consumer content (§3.2).
/// </summary>
/// <remarks>
/// <see cref="Content"/> holds <b>data, not a control</b> (§3.5). N
/// <see cref="DockContent"/> nodes may reference the same consumer object; each
/// carries its own <see cref="DockPane.Id"/> and its own position in the tree,
/// which is what makes a duplicated tab fall out for free. Assigning an
/// Avalonia <c>Control</c> here is rejected, because a second presenter would
/// have to reparent the same visual.
/// </remarks>
public sealed class DockContent : DockPane, IDockNode
{
    private object? _content;
    private string? _contentKey;
    private bool _canClose = true;
    private IEnumerable? _menuItems;

    public DockContent()
    {
    }

    public DockContent(object? content)
    {
        Content = content;
    }

    /// <summary>
    /// The consumer's own object, rendered through ordinary
    /// <c>DataTemplate</c> resolution (§3.8).
    /// </summary>
    public object? Content
    {
        get => _content;
        set
        {
            DockContentGuard.Reject(value);
            Set(ref _content, value);
        }
    }

    /// <summary>
    /// Stable string identity of the wrapped content within its owning
    /// <c>Dock</c>, projected from the descriptor's <c>ContentKey</c> binding
    /// (§3.7). Many nodes may share one key (§3.5).
    /// </summary>
    public string? ContentKey
    {
        get => _contentKey;
        internal set => Set(ref _contentKey, value);
    }

    /// <summary>Projection of the descriptor's <c>CanClose</c>; defaults to true.</summary>
    public bool CanClose
    {
        get => _canClose;
        internal set => Set(ref _canClose, value);
    }

    /// <summary>
    /// Consumer-contributed context-menu items, projected from the
    /// descriptor's <c>MenuItems</c> binding (§5.4). The consumer's own command
    /// objects, rendered through <c>DataTemplate</c>s — never library types and
    /// never <c>MenuItem</c> controls.
    /// </summary>
    public IEnumerable? MenuItems
    {
        get => _menuItems;
        internal set => Set(ref _menuItems, value);
    }

    public override string ToString() => $"Content({Title ?? ContentKey ?? Id})";
}
