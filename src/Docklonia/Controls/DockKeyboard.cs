using Avalonia.Input;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// Which key performs which navigation operation (§11).
/// </summary>
/// <remarks>
/// <para>Drag-and-drop is inherently pointer-driven, so the <b>pane menu is what
/// makes docking keyboard-accessible</b>: it exposes every operation the drag
/// engine does, and this class is what opens it without a pointer.</para>
///
/// <para>Every arm calls the <see cref="Dock"/>'s public operation, which is the
/// same one a host binding its own keys calls. The default key map is a policy
/// over those operations, and <c>HandlesKeys</c> is what turns it off.</para>
/// </remarks>
internal sealed class DockKeyboard
{
    private readonly Dock _dock;

    internal DockKeyboard(Dock dock)
    {
        _dock = dock;
    }

    internal bool Handle(KeyEventArgs e)
    {
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        return (e.Key, control, shift, alt) switch
        {
            // Most-recently-used order across panes, from the activation list.
            (Key.Tab, true, _, false) => _dock.CycleMostRecentlyUsed(shift),

            // Next / previous tab within the active pane.
            (Key.PageDown, true, false, false) => _dock.StepTab(1),
            (Key.PageUp, true, false, false) => _dock.StepTab(-1),

            // Cycle panes.
            (Key.F6, false, _, false) => _dock.CyclePanes(shift),

            // Directional traversal between panes.
            (Key.Left, false, false, true) => _dock.Traverse(DockDirection.Left),
            (Key.Right, false, false, true) => _dock.Traverse(DockDirection.Right),
            (Key.Up, false, false, true) => _dock.Traverse(DockDirection.Top),
            (Key.Down, false, false, true) => _dock.Traverse(DockDirection.Bottom),

            // The pane menu, which is the keyboard route to every docking operation.
            (Key.F10, false, true, false) => _dock.OpenPaneMenu(),
            (Key.Apps, false, false, false) => _dock.OpenPaneMenu(),

            _ => false,
        };
    }
}
