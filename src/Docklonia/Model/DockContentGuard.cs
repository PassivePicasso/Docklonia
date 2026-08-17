using Avalonia.Controls;

namespace Docklonia.Model;

/// <summary>
/// Enforces §3.5's hard constraint that <see cref="DockContent.Content"/> holds
/// data rather than a control. Stated at the public API surface and thrown
/// eagerly, rather than failing silently the first time a tab is duplicated.
/// </summary>
internal static class DockContentGuard
{
    internal static void Reject(object? value)
    {
        if (value is not Control control)
        {
            return;
        }

        throw new ArgumentException(
            $"DockContent.Content must hold data, not a control, but a '{control.GetType().FullName}' " +
            "was assigned. A control has exactly one visual parent, so it cannot be presented by two " +
            "tabs at once and duplication (§3.5) would be impossible. Assign a view model and supply a " +
            "DataTemplate for it, or use Dock.Items with a DockItem to author content as a template.",
            nameof(DockContent.Content));
    }
}
