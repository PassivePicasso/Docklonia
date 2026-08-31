using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Docklonia.Descriptors;
using Docklonia.Model;
using Xunit;
using Dock = Docklonia.Controls.Dock;

namespace Docklonia.Tests;

/// <summary>
/// Handing a <c>Dock</c> a different layout, when it is already showing one.
/// </summary>
/// <remarks>
/// The nodes it was tracking belong to the layout it had, and every one of
/// them is in no tree the moment a new layout arrives. What it does about
/// those decides whether the items come back.
/// </remarks>
public class LayoutReplacementTests
{
    private sealed class Doc
    {
        public Doc(string key) => Key = key;

        public string Key { get; }

        public ICommand Closed { get; } = new Counter();
    }

    /// <summary>A command that only records having been asked.</summary>
    private sealed class Counter : ICommand
    {
        public int Count { get; private set; }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Count++;
    }

    /// <summary>
    /// An empty layout is the arrangement a dock builds for itself, so the
    /// items are placed into it again.
    /// </summary>
    /// <remarks>
    /// This is what "reset the layout" is: forget what was arranged and open
    /// as a first launch does. Without it the dock keeps saying each item is
    /// already placed, in panes belonging to the layout just discarded, and
    /// the window resets to nothing at all.
    /// </remarks>
    [AvaloniaFact]
    public void AnEmptyLayoutIsFilledWithTheItemsAgain()
    {
        var dock = Showing(new Doc("a"), new Doc("b"));

        Assert.Equal(2, Contents(dock.Layout!));

        dock.Layout = new DockLayout();
        Flush();

        Assert.Equal(2, Contents(dock.Layout!));
    }

    /// <summary>
    /// Replacing the layout is not closing anything.
    /// </summary>
    /// <remarks>
    /// The items never left the collection; their views did. A close signalled
    /// here would have the consumer drop the documents it is being asked to
    /// show again.
    /// </remarks>
    [AvaloniaFact]
    public void ReplacingTheLayoutClosesNothing()
    {
        var first = new Doc("a");
        var dock = Showing(first, new Doc("b"));

        dock.Layout = new DockLayout();
        Flush();

        Assert.Equal(0, ((Counter)first.Closed).Count);
    }

    /// <summary>How many items the layout is presenting.</summary>
    private static int Contents(DockLayout layout)
        => layout.AllPanes().OfType<DockContent>().Count(node => node.Content is not null);

    /// <summary>A dock in a shown window, bound to these documents.</summary>
    private static Dock Showing(params Doc[] documents)
    {
        var dock = new Dock();

        dock.ItemDescriptors.Add(new DockItemDescriptor
        {
            DataType = typeof(Doc),
            Title = new Avalonia.Data.Binding(nameof(Doc.Key)),
            ContentKey = new Avalonia.Data.Binding(nameof(Doc.Key)),
            ClosedCommand = new Avalonia.Data.Binding(nameof(Doc.Closed)),
        });

        new Window { Width = 800, Height = 600, Content = dock }.Show();
        Flush();

        dock.ItemsSource = new ObservableCollection<object>(documents);
        Flush();

        return dock;
    }

    /// <summary>Headless has no render loop, so layout is driven here.</summary>
    private static void Flush()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }
}
