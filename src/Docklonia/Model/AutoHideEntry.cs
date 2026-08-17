namespace Docklonia.Model;

/// <summary>
/// A pane parked on a <c>Dock</c> edge strip, plus enough information to put it
/// back where it came from (§5.3).
/// </summary>
/// <remarks>
/// The restore target is stored as a <b>relative anchor</b> — a sibling node's
/// <see cref="DockPane.Id"/> plus the direction the hidden pane sat in relative
/// to it — never as a path. Unrelated docking operations invalidate paths while
/// a pane sits hidden, but a sibling id stays meaningful for as long as that
/// sibling exists. When the anchor is gone the pane falls back to its placement
/// seed (§3.9).
/// </remarks>
public sealed class AutoHideEntry
{
    public AutoHideEntry(IDockNode pane, DockEdge edge, string? anchorId, DockDirection anchorDirection, double ratio)
    {
        ArgumentNullException.ThrowIfNull(pane);

        Pane = pane;
        Edge = edge;
        AnchorId = anchorId;
        AnchorDirection = anchorDirection;
        Ratio = ratio;
    }

    /// <summary>The pane itself, removed from the tree but still owned by the layout.</summary>
    public IDockNode Pane { get; }

    /// <summary>Which strip the button sits on — the nearest edge at the moment of minimizing.</summary>
    public DockEdge Edge { get; }

    /// <summary>Id of the node the pane was beside, or null if it was the whole root.</summary>
    public string? AnchorId { get; }

    /// <summary>Where the pane sat relative to <see cref="AnchorId"/>.</summary>
    public DockDirection AnchorDirection { get; }

    /// <summary>
    /// The pane's share of the <c>Dock</c>: the size its flyout opens at, and the
    /// split ratio it restores to. Resizing the flyout updates it, and it
    /// serializes with the entry, so a resized flyout survives save and load.
    /// </summary>
    public double Ratio { get; internal set; }
}
