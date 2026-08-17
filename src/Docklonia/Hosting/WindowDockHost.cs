using Avalonia;
using Avalonia.Controls;

namespace Docklonia.Hosting;

/// <summary>
/// Desktop realization: a real <see cref="Window"/> owned by the <c>Dock</c>'s
/// own window, which is what gives §5.4's ownership semantics for free — the
/// float stays above its owner, and closing the owner closes it.
/// </summary>
internal sealed class WindowDockHost : DockHost
{
    private readonly Window _window;
    private bool _closing;

    internal WindowDockHost(Window owner, bool hitTestable)
    {
        _window = new Window
        {
            SystemDecorations = WindowDecorations.None,
            ShowInTaskbar = false,
            Background = null,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            SizeToContent = SizeToContent.Manual,
            CanResize = hitTestable,
            IsHitTestVisible = hitTestable,
            Topmost = !hitTestable,
        };

        _window.PositionChanged += (_, _) => RaiseGeometryChanged();
        _window.Resized += (_, _) => RaiseGeometryChanged();
        _window.Closed += (_, _) => RaiseClosed();

        _window.Show(owner);
    }

    internal override Visual? RootVisual => _window;

    internal override TopLevel? TopLevel => _window;

    internal override Control? Content
    {
        get => _window.Content as Control;
        set => _window.Content = value;
    }

    internal override PixelPoint Position
    {
        get => _window.Position;
        set => _window.Position = value;
    }

    internal override Size Size
    {
        get => new(_window.Width, _window.Height);
        set
        {
            _window.Width = value.Width;
            _window.Height = value.Height;
        }
    }

    internal override WindowState WindowState
    {
        get => _window.WindowState;
        set => _window.WindowState = value;
    }

    internal override void Show() => _window.Show();

    internal override void Close()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        _window.Close();
    }

    public override void Dispose() => Close();
}
