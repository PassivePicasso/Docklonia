using Avalonia;
using Avalonia.Controls;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// Owns the four edge strips and the slide-out flyout (§5.3).
/// </summary>
/// <remarks>
/// The flyout is an overlay: it slides over the content and never resizes the
/// layout or displaces other panes, which is what distinguishes auto-hide from
/// collapsing a pane in place. It is dismissed by losing focus or by re-pinning,
/// and its size is stored on the entry as a proportion so it persists.
/// </remarks>
internal sealed class DockAutoHideSurface
{
    /// <summary>Bounds on the flyout's share of the <c>Dock</c>, so it can neither vanish nor swallow the content.</summary>
    private const double MinShare = 0.1;
    private const double MaxShare = 0.85;

    private readonly Dock _dock;
    private readonly Dictionary<DockEdge, DockAutoHideStrip> _strips = new();
    private readonly DockAutoHideFlyout _flyout = new() { IsVisible = false };

    private AutoHideEntry? _open;

    internal DockAutoHideSurface(Dock dock)
    {
        _dock = dock;

        _flyout.LostFocus += (_, _) => Dismiss();
        _flyout.ExtentRequested += OnExtentRequested;
        _flyout.ResizeCompleted += () => _dock.NotifyLayoutChanged();
    }

    internal Control Flyout => _flyout;

    internal void Register(DockEdge edge, DockAutoHideStrip? strip)
    {
        if (strip is null)
        {
            _strips.Remove(edge);
            return;
        }

        strip.Owner = _dock;
        strip.Edge = edge;
        _strips[edge] = strip;

        Realize(strip);
    }

    internal void Refresh()
    {
        foreach (var strip in _strips.Values)
        {
            Realize(strip);
            strip.Rebuild();
        }

        if (_open is not null && _dock.Layout?.AutoHidden.Contains(_open) != true)
        {
            Dismiss();
        }
    }

    /// <summary>
    /// Forces the strip's template to be applied.
    /// </summary>
    /// <remarks>
    /// A strip starts collapsed, because an edge with no entries must consume no
    /// space (§5.3). But a collapsed control is never measured, and templates are
    /// applied during measure — so without this the strip would never locate its
    /// items panel, and could never become visible to show the first entry.
    /// </remarks>
    private static void Realize(DockAutoHideStrip strip) => strip.ApplyTemplate();

    /// <summary>Slides the pane out over the content, sized against the <c>Dock</c>.</summary>
    internal void Show(AutoHideEntry entry, DockAutoHideButton button)
    {
        if (ReferenceEquals(_open, entry) && _flyout.IsVisible)
        {
            Dismiss();
            return;
        }

        _open = entry;
        _flyout.Edge = entry.Edge;
        _flyout.IsVisible = true;
        _flyout.ApplyTemplate();
        _flyout.PaneContent = new DockPanePresenter { Owner = _dock, Pane = entry.Pane };

        Arrange(entry);
        button.SetOpen(true);
        _flyout.Focus();
    }

    internal void Dismiss()
    {
        _open = null;
        _flyout.IsVisible = false;
        _flyout.PaneContent = null;

        foreach (var strip in _strips.Values)
        {
            strip.Rebuild();
        }
    }

    /// <summary>
    /// Applies a dragged extent back onto the entry as a proportion of the
    /// <c>Dock</c>, then re-arranges. Storing a proportion rather than pixels is
    /// what lets the restored size survive a window resize, exactly as a split
    /// ratio does (§3.3).
    /// </summary>
    private void OnExtentRequested(double extent)
    {
        if (_open is null)
        {
            return;
        }

        var available = _open.Edge is DockEdge.Left or DockEdge.Right
            ? _dock.Bounds.Width
            : _dock.Bounds.Height;

        if (available <= 0)
        {
            return;
        }

        _open.Ratio = Math.Clamp(extent / available, MinShare, MaxShare);
        Arrange(_open);
    }

    /// <summary>
    /// Positions the flyout against its edge at the pane's remembered proportion,
    /// so revealing it shows roughly the size it had when docked.
    /// </summary>
    private void Arrange(AutoHideEntry entry)
    {
        var bounds = _dock.Bounds.Size;
        var share = Math.Clamp(entry.Ratio <= 0 ? 0.25 : entry.Ratio, MinShare, MaxShare);

        switch (entry.Edge)
        {
            case DockEdge.Left:
                Place(0, 0, bounds.Width * share, bounds.Height);
                break;

            case DockEdge.Right:
                Place(bounds.Width * (1 - share), 0, bounds.Width * share, bounds.Height);
                break;

            case DockEdge.Top:
                Place(0, 0, bounds.Width, bounds.Height * share);
                break;

            default:
                Place(0, bounds.Height * (1 - share), bounds.Width, bounds.Height * share);
                break;
        }
    }

    private void Place(double x, double y, double width, double height)
    {
        _flyout.Width = width;
        _flyout.Height = height;
        _flyout.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        _flyout.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        _flyout.Margin = new Thickness(x, y, 0, 0);
    }
}
