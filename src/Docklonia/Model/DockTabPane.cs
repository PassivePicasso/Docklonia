using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Docklonia.Model;

/// <summary>
/// N children presented as a tab group, and itself the tab host (§3.4).
/// </summary>
/// <remarks>
/// <see cref="SelectedChild"/> is pure view state: changing it switches which
/// child is displayed and never moves keyboard focus (§3.11). Activation is a
/// separate layer owned by the <c>Dock</c>.
/// </remarks>
public sealed class DockTabPane : DockPane, IDockNode
{
    private readonly ObservableCollection<IDockNode> _children = new();
    private IDockNode? _selectedChild;
    private string? _group;

    public DockTabPane()
    {
        _children.CollectionChanged += OnChildrenChanged;
    }

    public DockTabPane(params IDockNode[] children) : this()
    {
        foreach (var child in children)
        {
            Insert(_children.Count, child);
        }
    }

    public override IReadOnlyList<IDockNode> Children => _children;

    /// <summary>Which child the group currently displays. One per tab pane, many concurrently.</summary>
    public IDockNode? SelectedChild
    {
        get => _selectedChild;
        set
        {
            if (value is not null && !_children.Contains(value))
            {
                throw new ArgumentException("Selected child must be a child of this tab pane.", nameof(value));
            }

            var previous = _selectedChild;

            if (!Set(ref _selectedChild, value))
            {
                return;
            }

            Unwatch(previous);
            Watch(value);
            UpdateTitle();
        }
    }

    /// <summary>
    /// Durable group identity (§3.9). Carried by the pane and persisted with
    /// it, so later members of the group join it wherever the user has moved
    /// it and the seed is never reconsulted.
    /// </summary>
    public string? Group
    {
        get => _group;
        set => Set(ref _group, value);
    }

    internal void Insert(int index, IDockNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (_children.Contains(child))
        {
            throw new ArgumentException("Node is already a child of this tab pane.", nameof(child));
        }

        _children.Insert(Math.Clamp(index, 0, _children.Count), child);
    }

    internal void Add(IDockNode child) => Insert(_children.Count, child);

    internal bool Remove(IDockNode child) => _children.Remove(child);

    /// <summary>Reorders a child within the strip (§6.1). Persisted like any other mutation.</summary>
    internal void Move(IDockNode child, int index)
    {
        var from = _children.IndexOf(child);

        if (from < 0)
        {
            throw new ArgumentException("Node is not a child of this tab pane.", nameof(child));
        }

        _children.Move(from, Math.Clamp(index, 0, _children.Count - 1));
    }

    internal int IndexOf(IDockNode child) => _children.IndexOf(child);

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var removed in Enumerate(e.OldItems))
        {
            if (ReferenceEquals(removed.Parent, this))
            {
                Orphan(removed);
            }
        }

        foreach (var added in Enumerate(e.NewItems))
        {
            Adopt(added);
        }

        if (_selectedChild is null || !_children.Contains(_selectedChild))
        {
            SelectedChild = SelectionAfterRemoval(e);
        }

        Raise(nameof(Children));
        RaiseStructureChanged();
    }

    /// <summary>
    /// Keeps selection adjacent to what was removed, so closing a tab does not
    /// throw the user back to the first tab.
    /// </summary>
    private IDockNode? SelectionAfterRemoval(NotifyCollectionChangedEventArgs e)
    {
        if (_children.Count == 0)
        {
            return null;
        }

        var index = e.Action == NotifyCollectionChangedAction.Remove ? e.OldStartingIndex : 0;
        return _children[Math.Clamp(index, 0, _children.Count - 1)];
    }

    private static IEnumerable<IDockNode> Enumerate(System.Collections.IList? items)
    {
        if (items is null)
        {
            yield break;
        }

        foreach (var item in items)
        {
            if (item is IDockNode node)
            {
                yield return node;
            }
        }
    }

    private void Watch(IDockNode? child)
    {
        if (child is INotifyPropertyChanged observable)
        {
            observable.PropertyChanged += OnSelectedChildPropertyChanged;
        }
    }

    private void Unwatch(IDockNode? child)
    {
        if (child is INotifyPropertyChanged observable)
        {
            observable.PropertyChanged -= OnSelectedChildPropertyChanged;
        }
    }

    private void OnSelectedChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Title) or null)
        {
            UpdateTitle();
        }
    }

    private void UpdateTitle() => Title = _selectedChild?.Title;

    public override string ToString() => $"Tabs({_children.Count}{(Group is null ? "" : $", {Group}")})";
}
