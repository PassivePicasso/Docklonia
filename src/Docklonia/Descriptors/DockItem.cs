using Avalonia.Metadata;

namespace Docklonia.Descriptors;

/// <summary>
/// A statically-authored pane holding ordinary controls (§9.1). Authored panes
/// and bound documents occupy the same layout tree and serialize into the same
/// document.
/// </summary>
/// <remarks>
/// <para><b>Content is captured as a template, not as an instance.</b> Storing a
/// live control would put a visual in the layout tree and would make
/// cross-<c>Dock</c> and cross-window moves require visual reparenting — the
/// precise coupling the view-model tree exists to eliminate. The consumer's XAML
/// is unchanged either way; only the semantics differ.</para>
///
/// <para><b>Consequence for duplication.</b> A duplicated <see cref="DockItem"/>
/// builds a <i>second, independent</i> instance of its content, because there is
/// no shared view model behind it — two copies of the authored controls, with
/// independent scroll positions and control state. This differs from
/// <c>ItemsSource</c> content, where duplicated tabs share one view model and
/// observe the same state (§3.5).</para>
/// </remarks>
public sealed class DockItem
{
    /// <summary>The authored content, held as a deferred template.</summary>
    [Content]
    [TemplateContent]
    public object? Content { get; set; }

    /// <summary>Tab title. Required — describe-and-forbid is not relaxed for authored content.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Stable identity within the owning <c>Dock</c>. Required for the same
    /// reason a descriptor's key is: an item that cannot be persisted would
    /// reintroduce exactly the unpersistable nodes describe-and-forbid prevents.
    /// </summary>
    public string ContentKey { get; set; } = string.Empty;

    /// <summary>
    /// Name of the <see cref="DockGroup"/> this pane joins, inheriting that
    /// group's seed. A seed per item is deliberately not expressible here, for
    /// the same reason it is not expressible on a descriptor (§3.9). An
    /// ungrouped item uses Active placement.
    /// </summary>
    public string? Group { get; set; }

    public bool CanClose { get; set; } = true;
}
