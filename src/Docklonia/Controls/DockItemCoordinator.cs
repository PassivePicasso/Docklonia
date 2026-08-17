using System.Collections;
using System.Collections.Specialized;
using Docklonia.Descriptors;
using Docklonia.Model;
using Docklonia.Model.Mutations;

namespace Docklonia.Controls;

/// <summary>
/// Keeps a <c>Dock</c>'s layout tree in step with its bound collection and its
/// authored items.
/// </summary>
/// <remarks>
/// <para><see cref="DockContent"/> <i>is</i> the association layer (§3.8). It
/// exists precisely because the mapping from document to tab is one-to-many and
/// because layout position is per-tab, not per-document — which is why this
/// class tracks a list of nodes per item rather than a single node.</para>
///
/// <para>The consuming application never stores, sees, or manages a pane id. Its
/// side of the association is a key it already has, surfaced through the
/// descriptor's <c>ContentKey</c> binding.</para>
/// </remarks>
internal sealed class DockItemCoordinator : IDisposable
{
    private readonly Dock _dock;
    private readonly Dictionary<object, List<DockContent>> _nodesByItem = new(ReferenceComparer.Instance);
    private readonly Dictionary<DockContent, ItemMetadata> _metadata = new();
    private readonly Dictionary<DockItem, AuthoredContent> _authored = new();

    private IEnumerable? _itemsSource;

    internal DockItemCoordinator(Dock dock)
    {
        _dock = dock;
    }

    internal void SetItemsSource(IEnumerable? source)
    {
        if (_itemsSource is INotifyCollectionChanged previous)
        {
            previous.CollectionChanged -= OnItemsChanged;
        }

        _itemsSource = source;

        if (source is INotifyCollectionChanged current)
        {
            current.CollectionChanged += OnItemsChanged;
        }

        Resync();
    }

    /// <summary>
    /// Reconciles the tree against the current items. Placement is consulted only
    /// for items with no node — once a node exists, the layout wins (§3.9).
    /// </summary>
    internal void Resync()
    {
        var layout = _dock.EnsureLayout();
        var descriptors = _dock.Descriptors;
        var live = Items().Concat(AuthoredItems()).ToArray();

        AdoptNodesFromLoadedLayout(layout, live);
        DropUnmatchedNodes(layout);

        descriptors.ValidateKeys(live
            .Select(item => new KeyValuePair<object, string?>(item, KeyOf(item)))
            .Where(pair => pair.Value is not null));

        foreach (var item in live)
        {
            if (_nodesByItem.TryGetValue(item, out var existing) && existing.Count > 0)
            {
                continue;
            }

            Introduce(layout, item);
        }

        _dock.NotifyLayoutChanged();
    }

    /// <summary>Every node currently presenting <paramref name="item"/> (§3.5 duplication).</summary>
    internal IReadOnlyList<DockContent> NodesFor(object item)
        => _nodesByItem.TryGetValue(item, out var nodes) ? nodes : Array.Empty<DockContent>();

    internal ItemMetadata? MetadataFor(DockContent content)
        => _metadata.GetValueOrDefault(content);

    /// <summary>
    /// Stops tracking a subtree that is leaving for another <c>Dock</c>.
    /// </summary>
    /// <remarks>
    /// The bindings are torn down but no close is signalled: the item's view has
    /// moved, not closed, so <c>ClosedCommand</c> must not fire (§3.10). The item
    /// may still sit in this <c>Dock</c>'s collection, and releasing it here is
    /// what lets placement re-introduce it should it ever be needed again.
    /// </remarks>
    internal void Release(IDockNode node)
    {
        foreach (var content in DockTree.ContentsIn(node).ToArray())
        {
            if (!_metadata.Remove(content, out var metadata))
            {
                continue;
            }

            if (metadata.Item is { } item && _nodesByItem.TryGetValue(item, out var nodes))
            {
                nodes.Remove(content);

                if (nodes.Count == 0)
                {
                    _nodesByItem.Remove(item);
                }
            }

            metadata.Dispose();
        }
    }

    /// <summary>
    /// Takes over a subtree that arrived from another <c>Dock</c>, re-resolving
    /// every node against this <c>Dock</c>'s descriptors (§3.7). Describe-and-
    /// forbid already guaranteed the drop was only offered because this
    /// <c>Dock</c> can describe the content.
    /// </summary>
    internal void Adopt(IDockNode node)
    {
        foreach (var content in DockTree.ContentsIn(node).ToArray())
        {
            if (content.Content is { } item && !_metadata.ContainsKey(content))
            {
                Track(item, content);
            }
        }
    }

    /// <summary>
    /// Removes a node and, when it was the last view of its item, invokes the
    /// item's <c>ClosedCommand</c> exactly once (§3.10).
    /// </summary>
    internal void Detach(DockContent content)
    {
        if (_metadata.Remove(content, out var metadata))
        {
            var item = metadata.Item;

            if (item is not null && _nodesByItem.TryGetValue(item, out var nodes))
            {
                nodes.Remove(content);

                if (nodes.Count == 0)
                {
                    _nodesByItem.Remove(item);
                    InvokeClosed(metadata, item);
                }
            }

            metadata.Dispose();
        }
    }

    /// <summary>Creates a second node over the same item, so both views observe one state.</summary>
    internal DockContent Track(object item, DockContent node)
    {
        var descriptor = _dock.Descriptors.Resolve(item);

        if (descriptor is not null)
        {
            _metadata[node] = ItemMetadata.Bind(descriptor, item, node);
        }

        Nodes(item).Add(node);
        return node;
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            Resync();
            return;
        }

        foreach (var removed in e.OldItems?.Cast<object>() ?? Enumerable.Empty<object>())
        {
            RemoveItem(removed);
        }

        Resync();
    }

    /// <summary>
    /// Every node referencing the item is removed and normalization runs, which
    /// follows directly from duplication being N nodes to one item (§3.9).
    /// </summary>
    private void RemoveItem(object item)
    {
        var layout = _dock.EnsureLayout();

        foreach (var node in NodesFor(item).ToArray())
        {
            _dock.RemoveNode(node);
        }

        _nodesByItem.Remove(item);
    }

    /// <summary>
    /// A layout loaded from JSON carries keys but no content. Each node is
    /// matched against the live items using the same <c>ContentKey</c> binding —
    /// the collection is the set of live documents, the layout file only records
    /// where they sit (§8).
    /// </summary>
    private void AdoptNodesFromLoadedLayout(DockLayout layout, IReadOnlyList<object> items)
    {
        var byKey = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            if (KeyOf(item) is { } key)
            {
                byKey.TryAdd(key, item);
            }
        }

        foreach (var node in layout.AllPanes().OfType<DockContent>().ToArray())
        {
            if (_metadata.ContainsKey(node))
            {
                continue;
            }

            if (node.ContentKey is { } key && byKey.TryGetValue(key, out var item))
            {
                node.Content = item;
                Track(item, node);
            }
        }
    }

    /// <summary>
    /// A serialized key with no matching item yields a <b>dropped</b> node, never
    /// a fabricated one (§8). An empty tab would be a node the user cannot reason
    /// about; dropping it runs the same normalization every other removal does.
    /// </summary>
    /// <remarks>
    /// Only nodes still awaiting content are dropped — those are the ones a
    /// layout document produced and load-time matching could not satisfy. A node
    /// already holding live content that simply is not in <b>this</b> collection
    /// arrived by drag from another <c>Dock</c> (§7), and content keys are
    /// <c>Dock</c>-scoped, so its absence here says nothing about it. Dropping it
    /// would delete a pane the user just moved.
    /// </remarks>
    private void DropUnmatchedNodes(DockLayout layout)
    {
        foreach (var node in layout.AllPanes().OfType<DockContent>().ToArray())
        {
            if (node.Content is not null)
            {
                continue;
            }

            Detach(node);
            DockMutator.Remove(layout, node);
        }
    }

    /// <summary>Creates a node for an item that has none, and places it (§3.9).</summary>
    private void Introduce(DockLayout layout, object item)
    {
        var descriptor = _dock.Descriptors.Resolve(item);

        if (descriptor is null)
        {
            _dock.Descriptors.ReportUndescribed(item);
            return;
        }

        var node = new DockContent(item);
        Track(item, node);

        DockPlacement.Place(layout, _dock.Activation, node, GroupOf(item, descriptor), _dock.EffectiveGroups);
    }

    private string? KeyOf(object item)
    {
        var descriptor = _dock.Descriptors.Resolve(item);
        return descriptor is null ? null : ItemMetadata.KeyOf(descriptor, item);
    }

    private static string? GroupOf(object item, DockItemDescriptor descriptor)
        => item is AuthoredContent authored ? authored.Item.Group : descriptor.Group;

    private static void InvokeClosed(ItemMetadata metadata, object item)
    {
        var command = metadata.ClosedCommand;

        if (command?.CanExecute(item) == true)
        {
            command.Execute(item);
        }
    }

    private List<DockContent> Nodes(object item)
    {
        if (!_nodesByItem.TryGetValue(item, out var nodes))
        {
            nodes = new List<DockContent>();
            _nodesByItem[item] = nodes;
        }

        return nodes;
    }

    private IEnumerable<object> Items()
        => _itemsSource?.Cast<object>() ?? Enumerable.Empty<object>();

    /// <summary>Authored panes travel as data, one <see cref="AuthoredContent"/> per declaration.</summary>
    private IEnumerable<object> AuthoredItems()
    {
        foreach (var item in _dock.Items)
        {
            if (!_authored.TryGetValue(item, out var content))
            {
                content = new AuthoredContent(item);
                _authored[item] = content;
            }

            yield return content;
        }
    }

    public void Dispose()
    {
        if (_itemsSource is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged -= OnItemsChanged;
        }

        foreach (var metadata in _metadata.Values)
        {
            metadata.Dispose();
        }

        _metadata.Clear();
        _nodesByItem.Clear();
    }
}

/// <summary>Items are matched by identity, never by value — two equal documents are two documents.</summary>
internal sealed class ReferenceComparer : IEqualityComparer<object>
{
    internal static readonly ReferenceComparer Instance = new();

    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

    public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
