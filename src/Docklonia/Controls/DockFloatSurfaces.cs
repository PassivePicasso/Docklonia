using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Docklonia.Hosting;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// Realizes a <c>Dock</c>'s <see cref="FloatPane"/> collection as platform hosts
/// and keeps geometry flowing back into the model (§5.2).
/// </summary>
/// <remarks>
/// A float is <b>not a second <c>Dock</c></b>: it renders a subtree of its
/// owner's model and owns no model of its own. Descriptors, the acceptance
/// filter, and the single layout document therefore all extend to floats for
/// free — floating can never strand content in a surface that cannot describe
/// it.
/// </remarks>
internal sealed class DockFloatSurfaces : IDisposable
{
    private readonly Dock _dock;
    private readonly Dictionary<FloatPane, DockHost> _hosts = new();
    private DockLayout? _layout;

    internal DockFloatSurfaces(Dock dock)
    {
        _dock = dock;
    }

    internal IEnumerable<DockHost> Hosts => _hosts.Values;

    internal IReadOnlyCollection<DockHost> Surfaces => _hosts.Values;

    internal void Attach(DockLayout? layout)
    {
        if (_layout is not null)
        {
            ((INotifyCollectionChanged)_layout.Floats).CollectionChanged -= OnFloatsChanged;
        }

        _layout = layout;

        if (layout is not null)
        {
            ((INotifyCollectionChanged)layout.Floats).CollectionChanged += OnFloatsChanged;
        }

        Sync();
    }

    internal void Sync()
    {
        var wanted = _layout?.Floats.ToArray() ?? Array.Empty<FloatPane>();

        foreach (var stale in _hosts.Keys.Except(wanted).ToArray())
        {
            Release(stale);
        }

        foreach (var pane in wanted)
        {
            if (!_hosts.ContainsKey(pane))
            {
                Realize(pane);
            }
        }
    }

    private void OnFloatsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    private void Realize(FloatPane pane)
    {
        var host = DockHost.Create(_dock, hitTestable: true);

        host.Content = new DockPanePresenter { Owner = _dock, Pane = pane.Child };
        host.Position = pane.Position;
        host.Size = pane.Size;
        host.WindowState = pane.WindowState;

        // Geometry is a continuous gesture, so the model is updated as it moves
        // but the layout write-back happens once, on completion (§9.2).
        host.GeometryChanged += (_, _) => PullGeometry(pane, host);
        host.Closed += (_, _) => OnHostClosed(pane);

        pane.PropertyChanged += OnPanePropertyChanged;

        _hosts[pane] = host;
        host.Show();
    }

    private void Release(FloatPane pane)
    {
        if (_hosts.Remove(pane, out var host))
        {
            pane.PropertyChanged -= OnPanePropertyChanged;
            host.Dispose();
        }
    }

    private void OnHostClosed(FloatPane pane)
    {
        _hosts.Remove(pane);
        pane.PropertyChanged -= OnPanePropertyChanged;
        _layout?.Floats.Remove(pane);
    }

    private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not FloatPane pane || !_hosts.TryGetValue(pane, out var host))
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(FloatPane.Child):
                host.Content = new DockPanePresenter { Owner = _dock, Pane = pane.Child };
                break;

            case nameof(FloatPane.WindowState):
                host.WindowState = pane.WindowState;
                break;

            case nameof(FloatPane.Position):
                host.Position = pane.Position;
                break;

            case nameof(FloatPane.Size):
                host.Size = pane.Size;
                break;
        }
    }

    private void PullGeometry(FloatPane pane, DockHost host)
    {
        pane.Position = host.Position;
        pane.Size = host.Size;
        pane.WindowState = host.WindowState;

        // Moving or resizing is continuous; the write-back happens once, when
        // the gesture completes (§9.2).
        if (!ReferenceEquals(Dragging.DockDragSession.Current?.MovingFloat, pane))
        {
            _dock.NotifyLayoutChanged();
        }
    }

    /// <summary>A float never outlives its owner (§5.4).</summary>
    public void Dispose()
    {
        foreach (var pane in _hosts.Keys.ToArray())
        {
            Release(pane);
        }

        Attach(null);
    }
}
