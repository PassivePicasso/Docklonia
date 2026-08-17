namespace Docklonia.Model;

/// <summary>
/// The guide vocabulary (§6). One enum serves drag guides, outer guides,
/// placement seeding (§3.9), and auto-hide restore anchors (§5.3), because all
/// four are the same mutation differing only in where the direction comes from.
/// </summary>
public enum DockDirection
{
    /// <summary>Split, placing the node on the leading side horizontally.</summary>
    Left,

    /// <summary>Split, placing the node on the leading side vertically.</summary>
    Top,

    /// <summary>Split, placing the node on the trailing side horizontally.</summary>
    Right,

    /// <summary>Split, placing the node on the trailing side vertically.</summary>
    Bottom,

    /// <summary>Merge into the target as a tab. Has no outer-guide counterpart.</summary>
    Center,
}

/// <summary>An edge of a <c>Dock</c>. The auto-hide strips live here (§5.3).</summary>
public enum DockEdge
{
    Left,
    Top,
    Right,
    Bottom,
}
