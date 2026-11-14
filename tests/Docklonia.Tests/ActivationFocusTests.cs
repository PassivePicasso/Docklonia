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
        private readonly bool handles;

        /// <param name="handles">Whether it takes the press for a gesture of its own, as a drag does.</param>
        public Surface(bool handles)
        {
            this.handles = handles;
            Focusable = true;
            Background = Brushes.Black;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus();
            e.Handled = handles;
        }
    }

    private sealed class SurfaceTemplate(bool handles) : IDataTemplate
    {
        public bool Match(object? data) => data is Doc;

        public Control? Build(object? data) => new Surface(handles);
    }

    private static void Flush()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void APressInAPaneOfItsOwnKeepsFocusOnWhatItPressed() => PressedInItsOwnPane(handles: false);

    /// <summary>
    /// A graph surface or a 3D view handles nearly every press; the pane it
    /// is in is still the one pressed, and what acts on the active content
    /// (undo, save) must reach it.
    /// </summary>
    [AvaloniaFact]
    public void APressTheContentHandledActivatesItsPane() => PressedInItsOwnPane(handles: true);

    private static void PressedInItsOwnPane(bool handles)
    {
        var dock = new Dock();
        dock.ItemDescriptors.Add(new DockItemDescriptor
        {
            DataType = typeof(Doc),
            Title = new Avalonia.Data.Binding(nameof(Doc.Key)),
            ContentKey = new Avalonia.Data.Binding(nameof(Doc.Key)),
        });
        dock.DataTemplates.Add(new SurfaceTemplate(handles));
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
        Assert.Same(((DockContent)moved).Content, dock.ActiveContent);

        window.MouseUp(centre, MouseButton.Right);
    }
}
