using System.ComponentModel;
using Avalonia.Data;
using Avalonia.Metadata;

namespace Docklonia.Descriptors;

/// <summary>
/// Per-item-type metadata: how to title an item, how to key it, whether it may
/// close, what it contributes to a menu, and where it docks (§3.7).
/// </summary>
/// <remarks>
/// <para>Keyed <b>by item type</b> rather than as flat properties on the
/// <c>Dock</c>, because a docking application is the normal case for
/// heterogeneity — a single <c>TitleBinding</c> would force one member name
/// onto unrelated types.</para>
///
/// <para>Every binding property is instantiated once per item <b>with that item
/// as the source</b>. Inside a descriptor, <c>{Binding FileName}</c> means
/// <i>"for each item of this type, bind to that item's FileName"</i>; it does
/// not resolve against the <c>Dock</c>'s own <c>DataContext</c>. Each is live,
/// so renaming a document renames its tab.</para>
/// </remarks>
public sealed class DockItemDescriptor
{
    /// <summary>
    /// The item type this descriptor describes. A descriptor with no
    /// <see cref="DataType"/> matches anything — it is the fallback, and a
    /// single such descriptor is the whole configuration for the homogeneous
    /// case.
    /// </summary>
    public Type? DataType { get; set; }

    /// <summary>Tab title. Optional; degrades to <c>ToString()</c>.</summary>
    [AssignBinding]
    [TypeConverter(typeof(BindingLiteralConverter))]
    public BindingBase? Title { get; set; }

    /// <summary>
    /// Stable identity used for save and load (§8). <b>Required</b>, including
    /// on a <c>Dock</c> that never persists its layout: descriptor validity must
    /// not depend on whether <c>Layout</c> happens to be bound, and a missing key
    /// is what would reintroduce unpersistable nodes.
    /// </summary>
    /// <remarks>
    /// A <i>constant</i> key declares the type a singleton within the
    /// <c>Dock</c> — the normal case for tool panes. It does not prevent the
    /// item appearing in more than one tab (§3.5).
    /// </remarks>
    [AssignBinding]
    [TypeConverter(typeof(BindingLiteralConverter))]
    public BindingBase? ContentKey { get; set; }

    /// <summary>Whether a tab may close at all. Optional; degrades to <c>true</c>.</summary>
    [AssignBinding]
    [TypeConverter(typeof(BindingLiteralConverter))]
    public BindingBase? CanClose { get; set; }

    /// <summary>
    /// Consumer-contributed tab-context-menu entries (§5.4), projected from the
    /// item's own view model and rendered through ordinary
    /// <c>DataTemplate</c> resolution — command objects, not <c>MenuItem</c>
    /// controls.
    /// </summary>
    [AssignBinding]
    [TypeConverter(typeof(BindingLiteralConverter))]
    public BindingBase? MenuItems { get; set; }

    /// <summary>
    /// Intercepts a close (§3.10). When supplied the <c>Dock</c> invokes it
    /// <i>instead of</i> closing and does nothing further; the consumer closes by
    /// removing the item from the bound collection, or declines. The veto lives
    /// entirely on the consumer's side — the library never waits on a
    /// cancellable event.
    /// </summary>
    [AssignBinding]
    [TypeConverter(typeof(BindingLiteralConverter))]
    public BindingBase? CloseCommand { get; set; }

    /// <summary>
    /// Invoked once, with the item as parameter, when the <b>last</b>
    /// <c>DockContent</c> referencing it is removed (§3.10). A notification, not
    /// a veto. Does not fire when a duplicate closes, nor when a pane is
    /// auto-hidden or floated.
    /// </summary>
    [AssignBinding]
    [TypeConverter(typeof(BindingLiteralConverter))]
    public BindingBase? ClosedCommand { get; set; }

    /// <summary>
    /// Name of the <see cref="DockGroup"/> new items of this type join (§3.9).
    /// Omit for Active placement — the document case, which needs no
    /// configuration. <c>Seed</c> deliberately lives on the group, not here, so
    /// contradictory seeds for one group are unrepresentable.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Mirrors <c>DataTemplate</c> matching, which the consumer already
    /// understands: a null <see cref="DataType"/> matches anything, otherwise
    /// the type must be assignable from the item's.
    /// </summary>
    public bool Matches(object? item) => DataType is null || DataType.IsInstanceOfType(item);
}
