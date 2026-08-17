using Avalonia;
using Avalonia.Controls;

namespace Docklonia.Model;

/// <summary>
/// The model of a floating window: one arbitrary subtree plus window geometry
/// (§5.2). Owned by a <c>Dock</c> and serialized into that <c>Dock</c>'s single
/// layout document — a <see cref="FloatPane"/> is not a second <c>Dock</c>, so
/// it inherits the owner's descriptors and acceptance filter.
/// </summary>
/// <remarks>
/// Root only. This is enforced structurally: <see cref="FloatPane"/> implements
/// <see cref="IDockPane"/> but not <see cref="IDockNode"/>, and every composite
/// slot is typed <see cref="IDockNode"/>, so nesting one inside a split, a tab
/// group, or another float does not compile.
/// </remarks>
public sealed class FloatPane : DockPane
{
    private IDockNode _child;
    private PixelPoint _position;
    private Size _size = new(600, 400);
    private WindowState _windowState = WindowState.Normal;

    public FloatPane(IDockNode child, PixelPoint position, Size size)
    {
        ArgumentNullException.ThrowIfNull(child);

        _child = child;
        _position = position;
        _size = size;

        Adopt(child);
    }

    public IDockNode Child
    {
        get => _child;
        internal set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_child, value))
            {
                return;
            }

            Release(_child);
            _child = value;
            Adopt(value);
            Raise();
            Raise(nameof(Children));
        }
    }

    /// <summary>Top-left in screen coordinates. Written back on gesture completion only (§9.2).</summary>
    public PixelPoint Position
    {
        get => _position;
        set => Set(ref _position, value);
    }

    public Size Size
    {
        get => _size;
        set => Set(ref _size, value);
    }

    /// <summary>
    /// Real window state. Minimize on a float is an OS-window minimize, never
    /// auto-hide (§5.3).
    /// </summary>
    public WindowState WindowState
    {
        get => _windowState;
        set => Set(ref _windowState, value);
    }

    public override IReadOnlyList<IDockNode> Children => new[] { _child };

    public override string ToString() => $"Float({_child})";
}
