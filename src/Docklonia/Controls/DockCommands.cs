using Avalonia;
using Avalonia.Controls;
using Docklonia.Model;
using Docklonia.Model.Mutations;

namespace Docklonia.Controls;

/// <summary>
/// The pane and tab operations, each expressed as a mutation through the one
/// engine (§13) so that a menu command and a drag produce identical results.
/// </summary>
internal sealed class DockCommands
{
    private readonly Dock _dock;

    internal DockCommands(Dock dock)
    {
        _dock = dock;
    }

    private DockLayout Layout => _dock.EnsureLayout();

    /// <summary>
    /// Closes a node, honouring the consumer's veto. When a
    /// <c>CloseCommand</c> is supplied the <c>Dock</c> invokes it <b>instead of</b>
    /// closing and does nothing further; the consumer then closes by removing the
    /// item from the bound collection, or declines (§3.10).
    /// </summary>
    internal void RequestClose(IDockNode node)
    {
        if (node is DockContent content)
        {
            if (!content.CanClose)
            {
                return;
            }

            if (_dock.Coordinator.MetadataFor(content)?.CloseCommand is { } veto)
            {
                if (veto.CanExecute(content.Content))
                {
                    veto.Execute(content.Content);
                }

                return;
            }
        }

        Close(node);
    }

    /// <summary>Closes without consulting the veto — used once the consumer has already decided.</summary>
    internal void Close(IDockNode node)
    {
        var successor = _dock.Activation.NextAfterClosing(Layout, node);

        foreach (var content in DockTree.ContentsIn(node).ToArray())
        {
            _dock.Coordinator.Detach(content);
        }

        DockMutator.Remove(Layout, node);
        _dock.Activation.Prune(Layout);

        // Focus must move deterministically rather than dropping: without a
        // pointer, lost focus is unrecoverable (§11).
        _dock.ActivateNode(successor);
        _dock.NotifyLayoutChanged();
    }

    internal void CloseOthers(IDockNode node)
    {
        if (node.Parent is not DockTabPane tabs)
        {
            return;
        }

        foreach (var sibling in tabs.Children.Where(child => !ReferenceEquals(child, node)).ToArray())
        {
            RequestClose(sibling);
        }
    }

    internal void CloseAll(IDockNode node)
    {
        if (node.Parent is not DockTabPane tabs)
        {
            RequestClose(node);
            return;
        }

        foreach (var child in tabs.Children.ToArray())
        {
            RequestClose(child);
        }
    }

    /// <summary>Detaches a subtree into a floating window, preserving its internal tree (§5.1).</summary>
    internal FloatPane Float(IDockNode node, PixelPoint position)
    {
        var host = DockMutator.Float(Layout, node, position, new Size(560, 380));
        _dock.NotifyLayoutChanged();
        return host;
    }

    internal void FloatAtPointer(IDockNode node, PixelPoint screen) => Float(node, screen);

    /// <summary>Re-docks a floated subtree back into the main tree (§5.1 raft).</summary>
    internal void Raft(IDockNode node)
    {
        if (DockTree.FloatOf(node) is not { } host)
        {
            return;
        }

        var target = Layout.Root;

        if (target is null)
        {
            DockMutator.DockToRoot(Layout, host.Child, DockDirection.Center);
        }
        else
        {
            DockMutator.Raft(Layout, host, target, DockDirection.Center);
        }

        _dock.NotifyLayoutChanged();
    }

    /// <summary>
    /// A maximized pane temporarily covers the whole <c>Dock</c>. Its siblings
    /// are hidden, not removed — the tree is unchanged, so nothing normalizes and
    /// restoring reveals it exactly as it was (§5.3).
    /// </summary>
    internal void ToggleMaximize(IDockNode node)
    {
        Layout.MaximizedPane = ReferenceEquals(Layout.MaximizedPane, node) ? null : node;
        _dock.NotifyLayoutChanged();
    }

    /// <summary>Minimize is auto-hide: the pane leaves the tree and parks on the nearest edge (§5.3).</summary>
    internal void Minimize(DockPaneControl pane)
    {
        if (pane.Node is not { } node)
        {
            return;
        }

        if (DockTree.FloatOf(node) is not null)
        {
            // Minimize on a float is a real window minimize, never auto-hide.
            DockTree.FloatOf(node)!.WindowState = WindowState.Minimized;
            _dock.NotifyLayoutChanged();
            return;
        }

        AutoHideOperations.Hide(Layout, node, NearestEdge(pane));
        _dock.NotifyLayoutChanged();
    }

    internal void Restore(AutoHideEntry entry)
    {
        AutoHideOperations.Restore(Layout, entry, _dock.Groups);
        _dock.NotifyLayoutChanged();
    }

    /// <summary>
    /// Duplicating is an explicit user action with an explicit target, so it does
    /// not consult placement (§3.9).
    /// </summary>
    internal void Duplicate(IDockNode node)
    {
        if (node is not DockContent source || source.Content is null)
        {
            return;
        }

        var copy = DockMutator.Duplicate(Layout, source, node, DockDirection.Center);
        _dock.Coordinator.Track(source.Content, copy);
        _dock.NotifyLayoutChanged();
    }

    /// <summary>The nearest <c>Dock</c> edge to the pane's position at the moment it was minimized.</summary>
    private DockEdge NearestEdge(DockPaneControl pane)
    {
        var bounds = pane.Bounds;

        if (pane.TranslatePoint(default, _dock) is { } topLeft)
        {
            bounds = new Rect(topLeft, pane.Bounds.Size);
        }

        var centre = bounds.Center;
        var size = _dock.Bounds.Size;

        var distances = new (DockEdge Edge, double Distance)[]
        {
            (DockEdge.Left, centre.X),
            (DockEdge.Top, centre.Y),
            (DockEdge.Right, Math.Max(0, size.Width - centre.X)),
            (DockEdge.Bottom, Math.Max(0, size.Height - centre.Y)),
        };

        return distances.MinBy(candidate => candidate.Distance).Edge;
    }
}
