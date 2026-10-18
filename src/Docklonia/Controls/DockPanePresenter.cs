using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// Materializes the view for one layout node, recursively.
/// </summary>
/// <remarks>
/// The tree holds no visuals (§3.1), so this is the only place model shape
/// becomes control shape. Each <c>Dock</c> independently builds presenters for
/// whatever nodes it now owns and discards them when the tree changes — which is
/// why moving a node between trees, including across windows, needs no visual
/// reparenting. A node with nothing to show is presented hidden (§8).
/// </remarks>
public class DockPanePresenter : Decorator
{
    public static readonly Avalonia.StyledProperty<IDockNode?> PaneProperty =
        Avalonia.AvaloniaProperty.Register<DockPanePresenter, IDockNode?>(nameof(Pane));

    static DockPanePresenter()
    {
        PaneProperty.Changed.AddClassHandler<DockPanePresenter>((presenter, e) => presenter.OnPaneChanged(e));
    }

    public IDockNode? Pane
    {
        get => GetValue(PaneProperty);
        set => SetValue(PaneProperty, value);
    }

    internal Dock? Owner { get; set; }

    private void OnPaneChanged(Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is DockPane previous)
        {
            previous.PropertyChanged -= OnNodePropertyChanged;
            previous.StructureChanged -= OnStructureChanged;
        }

        if (e.NewValue is DockPane current)
        {
            current.PropertyChanged += OnNodePropertyChanged;
            current.StructureChanged += OnStructureChanged;
        }

        Rebuild();
    }

    private void OnStructureChanged(object? sender, EventArgs e) => UpdateShown();

    private void UpdateShown() => IsVisible = Pane is null || DockTree.Shows(Pane);

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A split swapping a child changes what must be presented; a tab pane's
        // own children are handled by the pane control itself.
        if (e.PropertyName is nameof(DockSplitPane.First) or nameof(DockSplitPane.Second))
        {
            Rebuild();
        }
    }

    internal void Rebuild()
    {
        Child = Pane switch
        {
            DockSplitPane split => new DockSplitPresenter { Owner = Owner, Split = split },
            null => null,
            var node => new DockPaneControl { Owner = Owner, Node = node },
        };

        UpdateShown();

        Owner?.RegisterRealizedView(this);
    }
}
