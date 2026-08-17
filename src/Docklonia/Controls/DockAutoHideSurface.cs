using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// Owns the four edge strips and the slide-out flyout (§5.3).
/// </summary>
/// <remarks>
/// The flyout is an overlay: it slides over the content and never resizes the
/// layout or displaces other panes, which is what distinguishes auto-hide from
/// collapsing a pane in place. It is dismissed by losing focus or by re-pinning.
/// </remarks>
internal sealed class DockAutoHideSurface
{
    private readonly Dock _dock;
    private readonly Dictionary<DockEdge, DockAutoHideStrip> _strips = new();
    private readonly Border _flyout = new() { IsVisible = false };

    private AutoHideEntry? _open;

    internal DockAutoHideSurface(Dock dock)
    {
        _dock = dock;
        _flyout.LostFocus += (_, _) => Dismiss();
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
    }

    internal void Refresh()
    {
        foreach (var strip in _strips.Values)
        {
            strip.Rebuild();
        }

        if (_open is not null && _dock.Layout?.AutoHidden.Contains(_open) != true)
        {
            Dismiss();
        }
    }

    /// <summary>Slides the pane out over the content, sized against the <c>Dock</c>.</summary>
    internal void Show(AutoHideEntry entry, DockAutoHideButton button)
    {
        if (ReferenceEquals(_open, entry) && _flyout.IsVisible)
        {
            Dismiss();
            return;
        }

        _open = entry;
        _flyout.Child = new DockPanePresenter { Owner = _dock, Pane = entry.Pane };
        _flyout.IsVisible = true;

        Arrange(entry);
        button.SetOpen(true);
        _flyout.Focus();
    }

    internal void Dismiss()
    {
        _open = null;
        _flyout.IsVisible = false;
        _flyout.Child = null;

        foreach (var strip in _strips.Values)
        {
            strip.Rebuild();
        }
    }

    /// <summary>
    /// Positions the flyout against its edge at the pane's remembered
    /// proportion, so revealing it shows roughly the size it had when docked.
    /// </summary>
    private void Arrange(AutoHideEntry entry)
    {
        var bounds = _dock.Bounds.Size;
        var share = Math.Clamp(entry.Ratio <= 0 ? 0.25 : entry.Ratio, 0.15, 0.6);

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
        Canvas.SetLeft(_flyout, x);
        Canvas.SetTop(_flyout, y);
        _flyout.Width = width;
        _flyout.Height = height;
        _flyout.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        _flyout.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        _flyout.Margin = new Thickness(x, y, 0, 0);
    }
}
