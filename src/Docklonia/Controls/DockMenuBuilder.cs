using System.Windows.Input;
using Avalonia.Controls;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// Builds the two menus of §5.4. The library supplies the standard docking
/// operations itself, so a consumer that configures nothing still gets a working
/// menu.
/// </summary>
/// <remarks>
/// <para>The <b>pane menu</b> is pane-scoped and takes built-ins only. The
/// <b>tab context menu</b> is the per-item scope and is where consumer
/// contributions appear.</para>
///
/// <para>Ordering is fixed so behaviour stays predictable: consumer items first,
/// a separator, then built-ins. Items that cannot apply in context are
/// <b>hidden</b> rather than shown disabled.</para>
///
/// <para>Because the pane menu exposes every operation the drag engine does, it
/// is what makes docking keyboard-accessible (§11) — drag-and-drop itself is
/// inherently pointer-driven.</para>
/// </remarks>
internal static class DockMenuBuilder
{
    internal static ContextMenu BuildPaneMenu(Dock dock, DockPaneControl pane)
    {
        var node = pane.Node;
        var menu = new ContextMenu();
        var items = new List<object>();

        if (node is not null)
        {
            var isFloating = DockTree.FloatOf(node) is not null;
            var isMaximized = ReferenceEquals(dock.Layout?.MaximizedPane, node);

            if (!isFloating)
            {
                items.Add(Item("Float", () => dock.Commands.Float(node, default)));
                items.Add(Item("Auto-hide", () => dock.Commands.Minimize(pane)));
            }
            else
            {
                items.Add(Item("Dock", () => dock.Commands.Raft(node)));
            }

            items.Add(Item(isMaximized ? "Restore" : "Maximize", () => dock.Commands.ToggleMaximize(node)));
            items.Add(Item("Close pane", () => dock.Commands.RequestClose(node)));
        }

        menu.ItemsSource = items;
        return menu;
    }

    internal static ContextMenu BuildTabMenu(Dock dock, DockTab tab)
    {
        var node = tab.Node;
        var menu = new ContextMenu();
        var items = new List<object>();

        if (node is DockContent content)
        {
            AddContributions(items, content);
        }

        if (node is not null)
        {
            if (tab.CanClose)
            {
                items.Add(Item("Close", () => dock.Commands.RequestClose(node)));
            }

            items.Add(Item("Close others", () => dock.Commands.CloseOthers(node)));
            items.Add(Item("Close all", () => dock.Commands.CloseAll(node)));
            items.Add(Item("Float", () => dock.Commands.Float(node, default)));
            items.Add(Item("Duplicate", () => dock.Commands.Duplicate(node)));
        }

        menu.ItemsSource = items;
        return menu;
    }

    /// <summary>
    /// Consumer items first, then a separator. They are the consumer's own
    /// objects, rendered through ordinary <c>DataTemplate</c> resolution; one
    /// that implements <see cref="ICommand"/> is invoked on click, and otherwise
    /// its template supplies its own interactivity.
    /// </summary>
    private static void AddContributions(List<object> items, DockContent content)
    {
        if (content.MenuItems is null)
        {
            return;
        }

        var added = false;

        foreach (var contributed in content.MenuItems)
        {
            if (contributed is null)
            {
                continue;
            }

            items.Add(new MenuItem
            {
                Header = contributed,
                Command = contributed as ICommand,
                CommandParameter = content.Content,
            });

            added = true;
        }

        if (added)
        {
            items.Add(new Separator());
        }
    }

    private static MenuItem Item(string header, Action execute)
        => new() { Header = header, Command = new RelayCommand(execute) };

    private sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;

        internal RelayCommand(Action execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}
