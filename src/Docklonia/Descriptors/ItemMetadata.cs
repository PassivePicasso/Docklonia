using System.Collections;
using System.Globalization;
using System.Windows.Input;
using Docklonia.Model;

namespace Docklonia.Descriptors;

/// <summary>
/// Holds one node's live descriptor projections and pushes them into the node
/// (§3.7).
/// </summary>
/// <remarks>
/// Metadata is resolved by whichever <c>Dock</c> currently owns the node, never
/// captured when the node is created — that live resolution is what lets two
/// <c>Dock</c>s present the same type differently, since a node moved between
/// them re-resolves against the destination's descriptors.
/// </remarks>
internal sealed class ItemMetadata : IDisposable
{
    private readonly List<ItemValueProjection> _projections = new();

    private ItemMetadata(DockItemDescriptor descriptor, object? item)
    {
        Descriptor = descriptor;
        Item = item;
    }

    public DockItemDescriptor Descriptor { get; }

    public object? Item { get; }

    public static ItemMetadata Bind(DockItemDescriptor descriptor, object? item, DockContent target)
    {
        var metadata = new ItemMetadata(descriptor, item);

        metadata.Project(descriptor.Title, value => target.Title = AsString(value) ?? item?.ToString());
        metadata.Project(descriptor.ContentKey, value => target.ContentKey = AsString(value));
        metadata.Project(descriptor.CanClose, value => target.CanClose = AsBoolean(value, fallback: true));
        metadata.Project(descriptor.MenuItems, value => target.MenuItems = value as IEnumerable);

        if (descriptor.Title is null)
        {
            target.Title = item?.ToString();
        }

        return metadata;
    }

    /// <summary>Read on demand — these are invoked, not displayed (§3.10).</summary>
    public ICommand? CloseCommand => ItemValueProjection.EvaluateOnce(Descriptor.CloseCommand, Item) as ICommand;

    public ICommand? ClosedCommand => ItemValueProjection.EvaluateOnce(Descriptor.ClosedCommand, Item) as ICommand;

    /// <summary>Evaluates a descriptor's key for an item without creating a node — used for load-time matching (§8).</summary>
    public static string? KeyOf(DockItemDescriptor descriptor, object? item)
        => AsString(ItemValueProjection.EvaluateOnce(descriptor.ContentKey, item));

    private void Project(Avalonia.Data.BindingBase? binding, Action<object?> apply)
    {
        var projection = ItemValueProjection.Create(binding, Item, apply);

        if (projection is not null)
        {
            _projections.Add(projection);
        }
    }

    private static string? AsString(object? value) => value switch
    {
        null => null,
        string text => text,
        _ => Convert.ToString(value, CultureInfo.InvariantCulture),
    };

    private static bool AsBoolean(object? value, bool fallback) => value switch
    {
        null => fallback,
        bool flag => flag,
        string text when bool.TryParse(text, out var parsed) => parsed,
        IConvertible convertible => convertible.ToBoolean(CultureInfo.InvariantCulture),
        _ => fallback,
    };

    public void Dispose()
    {
        foreach (var projection in _projections)
        {
            projection.Dispose();
        }

        _projections.Clear();
    }
}
