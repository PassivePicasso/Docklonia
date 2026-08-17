using System.Text.Json.Serialization;

namespace Docklonia.Serialization;

/// <summary>
/// Wire shape of one <c>Dock</c>'s layout: the main tree, every floating
/// window, and every auto-hidden entry in a single document (§8).
/// </summary>
internal sealed class LayoutDto
{
    public int Version { get; set; } = LayoutSchema.Version;

    public NodeDto? Root { get; set; }

    public List<FloatDto> Floats { get; set; } = new();

    public List<AutoHideDto> AutoHidden { get; set; } = new();

    public string? MaximizedPaneId { get; set; }

    public string? ActivePaneId { get; set; }
}

/// <summary>
/// A layout node. The discriminator mirrors the model's closed type set, so an
/// unknown kind is a genuine schema mismatch rather than something to guess at.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ContentNodeDto), "content")]
[JsonDerivedType(typeof(SplitNodeDto), "split")]
[JsonDerivedType(typeof(TabsNodeDto), "tabs")]
internal abstract class NodeDto
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// A leaf. Carries the content <i>key</i>, never a serialized view model —
/// identity only, matched against live items on load (§8).
/// </summary>
internal sealed class ContentNodeDto : NodeDto
{
    public string? ContentKey { get; set; }
}

internal sealed class SplitNodeDto : NodeDto
{
    public string Orientation { get; set; } = nameof(Avalonia.Layout.Orientation.Horizontal);

    public double Ratio { get; set; } = 0.5;

    public NodeDto? First { get; set; }

    public NodeDto? Second { get; set; }
}

internal sealed class TabsNodeDto : NodeDto
{
    /// <summary>Durable group identity, so later members join the pane wherever it now sits (§3.9).</summary>
    public string? Group { get; set; }

    /// <summary>Whether the pane survives being emptied (§6.1). Absent in older documents, where it was never true.</summary>
    public bool IsPersistent { get; set; }

    public string? SelectedId { get; set; }

    public List<NodeDto> Children { get; set; } = new();
}

internal sealed class FloatDto
{
    public string Id { get; set; } = string.Empty;

    public int X { get; set; }

    public int Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public string WindowState { get; set; } = nameof(Avalonia.Controls.WindowState.Normal);

    public NodeDto? Child { get; set; }
}

/// <summary>
/// An auto-hidden pane and the anchor that puts it back. The anchor is a
/// sibling id plus a direction rather than a path, because unrelated docking
/// operations invalidate paths while a pane sits hidden (§5.3).
/// </summary>
internal sealed class AutoHideDto
{
    public string Edge { get; set; } = nameof(Docklonia.Model.DockEdge.Left);

    public string? AnchorId { get; set; }

    public string AnchorDirection { get; set; } = nameof(Docklonia.Model.DockDirection.Left);

    public double Ratio { get; set; } = 0.25;

    public NodeDto? Pane { get; set; }
}
