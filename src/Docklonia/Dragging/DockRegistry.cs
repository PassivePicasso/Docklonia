using Docklonia.Controls;

namespace Docklonia.Dragging;

/// <summary>
/// Every live <c>Dock</c> in the process, so a pane dragged out of one can be
/// dropped into any other (§7).
/// </summary>
/// <remarks>
/// This is the only global state the library owns. It is in-process — cross-
/// process drag is explicitly out of scope — and its lifetime is tied to control
/// attach and detach, so a closed window leaves no entry behind.
/// </remarks>
public static class DockRegistry
{
    private static readonly List<Dock> Registered = new();
    private static readonly object Gate = new();

    /// <summary>Snapshot of the currently attached <c>Dock</c>s.</summary>
    public static IReadOnlyList<Dock> Docks
    {
        get
        {
            lock (Gate)
            {
                return Registered.ToArray();
            }
        }
    }

    internal static void Register(Dock dock)
    {
        lock (Gate)
        {
            if (!Registered.Contains(dock))
            {
                Registered.Add(dock);
            }
        }
    }

    internal static void Unregister(Dock dock)
    {
        lock (Gate)
        {
            Registered.Remove(dock);
        }
    }
}
