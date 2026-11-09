using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Docklonia.Controls;
using Dock = Docklonia.Controls.Dock;
using Docklonia.Descriptors;
using Docklonia.Model;
using Docklonia.Model.Mutations;
using Xunit;

namespace Docklonia.Tests;

/// <summary>
/// Activating a pane by pressing in it leaves focus where the press put it
/// (§3.11).
/// </summary>
/// <remarks>
/// A press bubbles to the pane, which activates its node. Focus moves to the
/// pane only from outside it: a control inside that took focus on the press
/// keeps it, or a view that steers while it holds focus is let go mid-drag.
/// </remarks>
public class ActivationFocusTests
{
    private sealed class Doc
    {
        public Doc(string key) => Key = key;

        public string Key { get; }
    }

    /// <summary>A focusable surface that takes focus when pressed, as a 3D view does.</summary>
    private sealed class Surface : Border
    {
        public Surface()
        {
            Focusable = true;
            Background = Brushes.Black;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus();
        }
    }

    private sealed class SurfaceTemplate : IDataTemplate
    {
        public bool Match(object? data) => data is Doc;

        public Control? Build(object? data) => new Surface();
    }

    private static void Flush()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void APressInAPaneOfItsOwnKeepsFocusOnWhatItPressed()
    {
        var dock = new Dock();
        dock.ItemDescriptors.Add(new DockItemDescriptor
        {
            DataType = typeof(Doc),
            Title = new Avalonia.Data.Binding(nameof(Doc.Key)),
            ContentKey = new Avalonia.Data.Binding(nameof(Doc.Key)),
        });
        dock.DataTemplates.Add(new SurfaceTemplate());
        dock.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(new[] { new Doc("a"), new Doc("b") });

        var window = new Window { Width = 800, Height = 600, Content = dock };
        window.Show();
        Flush();

        // Dragged out of the tabs and docked beside them: a pane of its own.
        var tabs = (DockTabPane)dock.Layout!.Root!;
        var moved = tabs.Children[1];
        DockMutator.Dock(dock.Layout, moved, tabs, DockDirection.Right);
        dock.NotifyLayoutChanged();
        Flush();

        var pane = dock.PaneControls.Single(control => ReferenceEquals(control.SelectedNode, moved));
        var surface = pane.GetVisualDescendants().OfType<Surface>().Single();
        var centre = surface.TranslatePoint(new Point(surface.Bounds.Width / 2, surface.Bounds.Height / 2), window)!.Value;

        window.MouseDown(centre, MouseButton.Right);
        Flush();

        Assert.True(surface.IsFocused, "the pressed surface keeps the focus it took");
        Assert.Same(moved, dock.Layout.ActivePane);

        window.MouseUp(centre, MouseButton.Right);
    }
}
