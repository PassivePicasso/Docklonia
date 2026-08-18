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
            // A float carries a resizable frame; the drag ghost carries none.
            // The hint keeps Windows from drawing that frame itself, which
            // would put a system line above the float's own chrome.
            WindowDecorations = hitTestable
                ? WindowDecorations.BorderOnly
                : WindowDecorations.None,
            ExtendClientAreaToDecorationsHint = hitTestable,
            // A float is a window of the application, so the OS shell should
            // list it: without a taskbar button, minimizing one sends it
            // nowhere the user can reach it from (§5.2).
            ShowInTaskbar = hitTestable,
            Icon = owner.Icon,
            Background = null,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            SizeToContent = SizeToContent.Manual,
            CanResize = hitTestable,
            IsHitTestVisible = hitTestable,
            Topmost = !hitTestable,
        };

        _window.PositionChanged += (_, _) => RaiseGeometryChanged();
        _window.Resized += (_, _) => RaiseGeometryChanged();
        _window.PropertyChanged += OnWindowPropertyChanged;
        _window.Closed += (_, _) => RaiseClosed();

        _window.Show(owner);
    }

    internal override string? Title
    {
        get => _window.Title;
        set => _window.Title = value;
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

    /// <summary>
    /// Minimizing and restoring from the taskbar are the user moving the
    /// window, so they reach the model by the same route a drag does.
    /// </summary>
    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            RaiseGeometryChanged();
        }
    }
}
