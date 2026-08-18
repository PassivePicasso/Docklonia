using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;

namespace Docklonia.Hosting;

/// <summary>
/// The single place the library decides what a floating surface <i>is</i> on a
/// given platform (§5.2).
/// </summary>
/// <remarks>
/// <para>Avalonia provides <see cref="TopLevel"/> as an abstraction but does not
/// absorb the difference: <see cref="Window"/> exists only on desktop, while
/// mobile and browser targets run under
/// <see cref="ISingleViewApplicationLifetime"/> with one root view, where a
/// second window silently fails. The branch is the library's to write, so it is
/// written exactly once, here.</para>
///
/// <para>It must not leak into the model, the mutation engine, the drag session,
/// or serialization — a layout saved on desktop loads unchanged in the browser,
/// because the tree holds no visuals and therefore records no host choice.</para>
///
/// <para>Under a single-view lifetime every surface shares one
/// <see cref="TopLevel"/>, so cross-window drag degrades to in-application drag
/// with no separate code path — simply fewer registered surfaces (§7.3).</para>
/// </remarks>
internal abstract class DockHost : IDisposable
{
    /// <summary>What the OS shell calls this surface. Unused where a surface has no shell presence.</summary>
    internal abstract string? Title { get; set; }

    internal abstract Visual? RootVisual { get; }

    internal abstract TopLevel? TopLevel { get; }

    internal abstract Control? Content { get; set; }

    internal abstract PixelPoint Position { get; set; }

    internal abstract Size Size { get; set; }

    internal abstract WindowState WindowState { get; set; }

    /// <summary>Raised when the user moves or resizes the host — a continuous gesture (§9.2).</summary>
    internal event EventHandler? GeometryChanged;

    internal event EventHandler? Closed;

    internal abstract void Show();

    internal abstract void Close();

    /// <summary>
    /// Creates a host owned by <paramref name="owner"/>. A desktop host is a
    /// real window owned by the <c>Dock</c>'s own top level, so it stays above
    /// its owner and never outlives it; a single-view host is an overlay in the
    /// root top level's overlay layer.
    /// </summary>
    internal static DockHost Create(Visual owner, bool hitTestable)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            && TopLevel.GetTopLevel(owner) is Window ownerWindow)
        {
            return new WindowDockHost(ownerWindow, hitTestable);
        }

        return new OverlayDockHost(owner, hitTestable);
    }

    protected void RaiseGeometryChanged() => GeometryChanged?.Invoke(this, EventArgs.Empty);

    protected void RaiseClosed() => Closed?.Invoke(this, EventArgs.Empty);

    public abstract void Dispose();
}
