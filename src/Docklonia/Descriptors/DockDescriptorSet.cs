using Docklonia.Diagnostics;

namespace Docklonia.Descriptors;

/// <summary>
/// Resolves item metadata by type and enforces describe-and-forbid (§3.7).
/// </summary>
/// <remarks>
/// <para>Because a <c>Dock</c> refuses content it cannot describe, the
/// descriptor set <b>defines what that <c>Dock</c> accepts</b>. That gives
/// applications tool-window areas and document areas — panes that may not mix —
/// without the library containing any notion of "tool window" or "document",
/// and it is why a separate named-drag-group mechanism would be redundant.</para>
///
/// <para>Resolution mirrors <c>DataTemplate</c> matching: declaration order,
/// first match wins, and a descriptor with no <c>DataType</c> is the
/// catch-all — which is also the lenient mode, written in the mechanism that
/// already exists rather than as a parallel flag.</para>
/// </remarks>
internal sealed class DockDescriptorSet
{
    private readonly IReadOnlyList<DockItemDescriptor> _descriptors;
    private readonly object _owner;

    internal DockDescriptorSet(object owner, IEnumerable<DockItemDescriptor> descriptors)
    {
        _owner = owner;
        _descriptors = descriptors.ToArray();
    }

    internal int Count => _descriptors.Count;

    /// <summary>First descriptor whose <c>DataType</c> is assignable from the item's type.</summary>
    internal DockItemDescriptor? Resolve(object? item)
    {
        foreach (var descriptor in _descriptors)
        {
            if (descriptor.Matches(item))
            {
                return descriptor;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether this <c>Dock</c> accepts the content at all. Used silently by
    /// drag target resolution (§7) — a drag over a <c>Dock</c> that cannot
    /// describe the payload shows nothing and is not an error.
    /// </summary>
    internal bool Describes(object? item) => Resolve(item) is not null;

    /// <summary>
    /// Validates the authored configuration. Called when descriptors are
    /// applied, so a typo surfaces at startup rather than the first time a pane
    /// is dragged.
    /// </summary>
    internal void Validate()
    {
        if (_descriptors.Count == 0)
        {
            DockDiagnostics.Warning(
                _owner,
                "This Dock declares no ItemDescriptors, so it accepts no content. That is coherent but " +
                "almost always a mistake; add a descriptor, or one with no DataType to accept anything.");
            return;
        }

        for (var i = 0; i < _descriptors.Count; i++)
        {
            var descriptor = _descriptors[i];

            if (descriptor.ContentKey is null)
            {
                throw new InvalidOperationException(
                    $"DockItemDescriptor for '{descriptor.DataType?.Name ?? "<any>"}' has no ContentKey. A key is " +
                    "required unconditionally, including on a Dock that never persists its layout, because " +
                    "descriptor validity must not depend on whether Layout happens to be bound.");
            }

            if (descriptor.DataType is null && i < _descriptors.Count - 1)
            {
                DockDiagnostics.Warning(
                    _owner,
                    "A DockItemDescriptor with no DataType matches anything, so the {Count} descriptor(s) " +
                    "declared after it can never be reached. Move it last.",
                    _descriptors.Count - i - 1);
            }
        }
    }

    /// <summary>
    /// Reports keys that collide across all of a <c>Dock</c>'s items. Matching
    /// on load is per-<c>Dock</c> rather than per-type, so two descriptors for
    /// different types must not produce colliding keys either (§3.7).
    /// </summary>
    internal void ValidateKeys(IEnumerable<KeyValuePair<object, string?>> keyedItems)
    {
        var seen = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var (item, key) in keyedItems)
        {
            if (key is null)
            {
                continue;
            }

            if (seen.TryGetValue(key, out var existing) && !ReferenceEquals(existing, item))
            {
                DockDiagnostics.Error(
                    _owner,
                    "Content key '{Key}' is produced by more than one item ({First} and {Second}). Keys must be " +
                    "unique across all of a Dock's items — a constant ContentKey declares its type a singleton " +
                    "within the Dock, so a second instance is a configuration error.",
                    key,
                    existing.GetType().Name,
                    item.GetType().Name);
                continue;
            }

            seen[key] = item;
        }
    }

    /// <summary>Loud, per §3.7: an undescribed item in <c>ItemsSource</c> is never silently skipped.</summary>
    internal void ReportUndescribed(object item)
    {
        DockDiagnostics.Error(
            _owner,
            "No DockItemDescriptor matches item of type '{Type}', so it cannot be docked. Add a descriptor for " +
            "that type, check the DataType for a typo, or declare a descriptor with no DataType as a catch-all.",
            item.GetType().FullName);
    }
}
