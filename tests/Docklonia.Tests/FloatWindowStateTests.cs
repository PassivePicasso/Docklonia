using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Docklonia.Controls;
using Dock = Docklonia.Controls.Dock;
using Docklonia.Descriptors;
using Docklonia.Model;
using Xunit;

namespace Docklonia.Tests;

/// <summary>
/// What a float's own titlebar buttons mean.
/// </summary>
/// <remarks>
/// A floated pane is in a window, so minimize and maximize are the window's
/// rather than the layout's. Maximize was the layout's: it set
/// <see cref="DockLayout.MaximizedPane"/>, which the owning dock presents --
/// so maximizing a floated pane drew it in the main window instead of filling
/// the one it was floating in. It appeared to fly home rather than to grow.
/// </remarks>
public class FloatWindowStateTests
{
    private sealed class Doc
    {
        public Doc(string key) => Key = key;

        public string Key { get; }
    }

    private static Dock BuildDock(params Doc[] documents)
    {
        var dock = new Dock();

        dock.ItemDescriptors.Add(new DockItemDescriptor
        {
            DataType = typeof(Doc),
            Title = new Avalonia.Data.Binding(nameof(Doc.Key)),
            ContentKey = new Avalonia.Data.Binding(nameof(Doc.Key)),
        });

        dock.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(documents);
        return dock;
    }

    private static Window Host(Dock dock)
    {
        var window = new Window { Width = 800, Height = 600, Content = dock };
        window.Show();
        Flush();
        return window;
    }

    private static void Flush()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void MaximizingAFloatMaximizesItsWindowRatherThanTheLayout()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        Host(dock);

        var node = dock.PaneControls.First().Node!;
        var floating = dock.Commands.Float(node, new PixelPoint(100, 100));
        Flush();

        dock.Commands.ToggleMaximize(floating.Child);
        Flush();

        Assert.Equal(WindowState.Maximized, floating.WindowState);

        // The owning dock presents the maximized pane, so a float named here
        // is a float drawn in the wrong window.
        Assert.Null(dock.Layout!.MaximizedPane);
    }

    [AvaloniaFact]
    public void MaximizingAFloatAgainPutsItBack()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        Host(dock);

        var node = dock.PaneControls.First().Node!;
        var floating = dock.Commands.Float(node, new PixelPoint(100, 100));
        Flush();

        dock.Commands.ToggleMaximize(floating.Child);
        dock.Commands.ToggleMaximize(floating.Child);
        Flush();

        Assert.Equal(WindowState.Normal, floating.WindowState);
    }

    /// <summary>
    /// A docked pane still maximizes into the dock, which is what maximize has
    /// always meant there: the siblings are hidden and the tree is unchanged.
    /// </summary>
    [AvaloniaFact]
    public void MaximizingADockedPaneStillCoversTheDock()
    {
        var dock = BuildDock(new Doc("a"), new Doc("b"));
        Host(dock);

        var node = dock.PaneControls.First().Node!;

        dock.Commands.ToggleMaximize(node);
        Flush();

        Assert.Same(node, dock.Layout!.MaximizedPane);
    }
}
