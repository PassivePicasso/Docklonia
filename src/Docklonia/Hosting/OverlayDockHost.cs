using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Docklonia.Hosting;

/// <summary>
/// Single-view realization: an overlay in the root top level's
/// <see cref="OverlayLayer"/>, which is the documented approach where a second
/// window is unavailable (§5.2).
/// </summary>
/// <remarks>
/// Geometry is kept in the same screen coordinates the desktop host uses, so the
/// model and the serialized layout cannot tell the two apart. Position is
/// translated into the overlay layer's own space on the way in.
/// </remarks>
internal sealed class OverlayDockHost : DockHost
{
    private readonly OverlayLayer? _layer;
    private readonly Border _root;
    private PixelPoint _position;
    private Size _size = new(600, 400);
    private bool _closed;

    internal OverlayDockHost(Visual owner, bool hitTestable)
    {
        _layer = OverlayLayer.GetOverlayLayer(owner);

        _root = new Border
        {
            IsHitTestVisible = hitTestable,
            Width = _size.Width,
            Height = _size.Height,
        };

        _layer?.Children.Add(_root);
        ApplyPosition();
    }

    /// <summary>An overlay has no shell presence, so the title is carried and not shown.</summary>
    internal override string? Title { get; set; }

    internal override Visual? RootVisual => _root;

    internal override TopLevel? TopLevel => TopLevel.GetTopLevel(_root);

    internal override Control? Content
    {
        get => _root.Child;
        set => _root.Child = value;
    }

    internal override PixelPoint Position
    {
        get => _position;
        set
        {
            _position = value;
            ApplyPosition();
            RaiseGeometryChanged();
        }
    }

    internal override Size Size
    {
        get => _size;
        set
        {
            _size = value;
            _root.Width = value.Width;
            _root.Height = value.Height;
            RaiseGeometryChanged();
        }
    }

    /// <summary>
    /// A single-view host has no OS window state. Maximizing fills the overlay
    /// layer, which is the closest honest equivalent.
    /// </summary>
    internal override WindowState WindowState { get; set; } = WindowState.Normal;

    internal override void Show() => _root.IsVisible = true;

    internal override void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _layer?.Children.Remove(_root);
        RaiseClosed();
    }

    private void ApplyPosition()
    {
        if (_layer is null)
        {
            return;
        }

        var local = _layer.PointToClient(_position);
        Canvas.SetLeft(_root, local.X);
        Canvas.SetTop(_root, local.Y);
    }

    public override void Dispose() => Close();
}
