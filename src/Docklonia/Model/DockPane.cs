using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Docklonia.Model;

/// <summary>
/// Base for every layout node. The constructor is internal, which closes the
/// set of <see cref="IDockPane"/> implementations to library types (§3.6).
/// </summary>
public abstract class DockPane : IDockPane
{
    private static readonly IReadOnlyList<IDockNode> NoChildren = Array.Empty<IDockNode>();

    private string _id = Guid.NewGuid().ToString("N");
    private string? _title;
    private bool _isVisible = true;
    private IDockPane? _parent;

    internal DockPane()
    {
    }

    public string Id
    {
        get => _id;
        internal set => Set(ref _id, value);
    }

    public string? Title
    {
        get => _title;
        internal set => Set(ref _title, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    public IDockPane? Parent
    {
        get => _parent;
        internal set => Set(ref _parent, value);
    }

    public virtual IReadOnlyList<IDockNode> Children => NoChildren;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised for any structural change beneath this node. The mutation engine
    /// bubbles it so a view can react without walking the tree.
    /// </summary>
    public event EventHandler? StructureChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(name);
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal void RaiseStructureChanged()
    {
        StructureChanged?.Invoke(this, EventArgs.Empty);
        (Parent as DockPane)?.RaiseStructureChanged();
    }

    internal void Adopt(IDockNode? child)
    {
        if (child is DockPane pane)
        {
            pane.Parent = this;
        }
    }

    /// <summary>
    /// Clears a child's parent link, but only while this node still owns it.
    /// Slot setters run after the child may already have been re-parented into
    /// a new composite, and an unguarded clear would sever that fresh link.
    /// </summary>
    internal void Release(IDockNode? child)
    {
        if (child is DockPane pane && ReferenceEquals(pane.Parent, this))
        {
            pane.Parent = null;
        }
    }

    internal static void Orphan(IDockNode? child)
    {
        if (child is DockPane pane)
        {
            pane.Parent = null;
        }
    }
}
