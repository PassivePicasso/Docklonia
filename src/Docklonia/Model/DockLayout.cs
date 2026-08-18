using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Docklonia.Serialization;

namespace Docklonia.Model;

/// <summary>
/// The opaque layout handle bound two-way to <c>Dock.Layout</c> (§9.2) — the
/// dock tree itself, not a JSON string.
/// </summary>
/// <remarks>
/// One document per <c>Dock</c> covering every surface: the main root, every
/// <see cref="FloatPane"/>, and every <see cref="AutoHideEntry"/> (§8). JSON is
/// produced only when <see cref="ToJson"/> is called, never implicitly on
/// mutation, and both directions are reachable from this object alone so a
/// shell view model can persist its layout without holding a reference to the
/// control.
/// </remarks>
public sealed class DockLayout : INotifyPropertyChanged
{
    private IDockNode? _root;
    private IDockPane? _maximizedPane;
    private IDockPane? _activePane;

    public DockLayout()
    {
        Floats.CollectionChanged += OnCollectionChanged;
        AutoHidden.CollectionChanged += OnCollectionChanged;
    }

    /// <summary>The main docked tree. Null when the <c>Dock</c> is empty.</summary>
    public IDockNode? Root
    {
        get => _root;
        internal set
        {
            if (ReferenceEquals(_root, value))
            {
                return;
            }

            _root = value;
            Raise();
            MarkChanged();
        }
    }

    /// <summary>Floating windows owned by this layout. Root-only subtrees (§5.2).</summary>
    public ObservableCollection<FloatPane> Floats { get; } = new();

    /// <summary>Panes parked on an edge strip, with their restore anchors (§5.3).</summary>
    public ObservableCollection<AutoHideEntry> AutoHidden { get; } = new();

    /// <summary>
    /// The pane temporarily covering the whole <c>Dock</c> (§5.3). A property of
    /// the layout rather than a tree mutation, so nothing normalizes and
    /// restoring reveals the siblings exactly as they were.
    /// </summary>
    public IDockPane? MaximizedPane
    {
        get => _maximizedPane;
        set
        {
            if (Set(ref _maximizedPane, value))
            {
                MarkChanged();
            }
        }
    }

    /// <summary>
    /// Logical focus (§3.11). Persisted, and used to seed the runtime
    /// activation order on load.
    /// </summary>
    public IDockPane? ActivePane
    {
        get => _activePane;
        internal set
        {
            if (Set(ref _activePane, value))
            {
                MarkChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised after any change to persisted state. A shell view model can use
    /// this to mark itself dirty; nothing is serialized until it asks.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>Serializes this layout, including floating and auto-hidden state, to JSON (§8).</summary>
    public string ToJson(LayoutJsonOptions? options = null) => LayoutSerializer.Serialize(this, options);

    /// <summary>
    /// Rebuilds a layout from JSON. Content is not resolved here — nodes carry
    /// their content keys, and the owning <c>Dock</c> matches those against the
    /// items in its bound collection (§8). Nothing is ever fabricated.
    /// </summary>
    public static DockLayout FromJson(string json, LayoutJsonOptions? options = null)
        => LayoutSerializer.Deserialize(json, options);

    /// <summary>Every node reachable from this layout, across every surface.</summary>
    public IEnumerable<IDockPane> AllPanes()
    {
        foreach (var pane in DockTree.SelfAndDescendants(Root))
        {
            yield return pane;
        }

        foreach (var pane in Floats.SelectMany(DockTree.SelfAndDescendants))
        {
            yield return pane;
        }

        foreach (var pane in AutoHidden.SelectMany(entry => DockTree.SelfAndDescendants(entry.Pane)))
        {
            yield return pane;
        }
    }

    /// <summary>
    /// The entry parking <paramref name="node"/>, or the ancestor of it that a
    /// strip holds. Null when the node is docked or floating.
    /// </summary>
    /// <remarks>
    /// A parked node is detached, so it has no parent link back to its entry.
    /// The entry is found by lookup rather than recorded on the node, because a
    /// node that knew which strip held it would be a second place the answer
    /// lives and could disagree with this collection.
    /// </remarks>
    internal AutoHideEntry? AutoHideOf(IDockPane? node)
        => node is null ? null : AutoHidden.FirstOrDefault(entry => DockTree.Contains(entry.Pane, node));

    internal void MarkChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => MarkChanged();

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(name);
        return true;
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
