using Avalonia;
using Avalonia.Controls;
using Docklonia.Hosting;

namespace Docklonia.Dragging;

/// <summary>
/// The single visual that follows the cursor during a drag (§7.2 step 2).
/// </summary>
/// <remarks>
/// Hosted the same way a <c>FloatPane</c> is realized on the platform, so there
/// is one hosting decision rather than two. It is never a hit-test target —
/// otherwise it would sit under the cursor and mask the very surface the drag is
/// trying to resolve.
/// </remarks>
internal sealed class DragGhost : IDisposable
{
    private static readonly PixelVector CursorOffset = new(12, 12);

    private readonly DockHost _host;

    private DragGhost(DockHost host)
    {
        _host = host;
    }

    internal PixelPoint Position { get; private set; }

    internal static DragGhost Create(Visual anchor, string? title)
    {
        var host = DockHost.Create(anchor, hitTestable: false);

        host.Size = new Size(180, 32);
        host.Content = new DragGhostPresenter { Title = title };
        host.Show();

        return new DragGhost(host);
    }

    internal void MoveTo(PixelPoint screen)
    {
        Position = screen;
        _host.Position = screen + CursorOffset;
    }

    public void Dispose() => _host.Dispose();
}

/// <summary>
/// The ghost's content. Lookless like every other control here, so an
/// application can restyle the drag feedback (§12).
/// </summary>
public class DragGhostPresenter : Avalonia.Controls.Primitives.TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DragGhostPresenter, string?>(nameof(Title));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
