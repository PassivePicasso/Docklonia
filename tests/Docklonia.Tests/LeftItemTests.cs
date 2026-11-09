using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Docklonia.Descriptors;
using Docklonia.Model;
using Xunit;
using Dock = Docklonia.Controls.Dock;

namespace Docklonia.Tests;

/// <summary>
/// An item that leaves a <c>Dock</c>'s collection leaves nothing of itself in
/// the <c>Dock</c>, which stays open.
/// </summary>
/// <remarks>
/// A shell keeps one dock for its whole life while what it docks comes and
/// goes; an item it still held would keep whatever the item's type keeps,
/// a plugin's whole load context among them.
/// </remarks>
public class LeftItemTests
{
    private sealed class Item
    {
        public Item(string key) => Key = key;

        public string Key { get; }

        public override string ToString() => Key;
    }

    private sealed class Document
    {
        public Document(string key) => Key = key;

        public string Key { get; }

        public override string ToString() => Key;
    }

    private static void Flush()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static (Dock Dock, ObservableCollection<object> Items) Build()
    {
        var dock = new Dock();

        dock.ItemDescriptors.Add(new DockItemDescriptor
        {
            DataType = typeof(Document),
            Title = new Avalonia.Data.Binding(nameof(Document.Key)),
            ContentKey = new Avalonia.Data.Binding(nameof(Document.Key)),
            Group = "Stage",
        });

        dock.ItemDescriptors.Add(new DockItemDescriptor
        {
            DataType = typeof(Item),
            Title = new Avalonia.Data.Binding(nameof(Item.Key)),
            ContentKey = new Avalonia.Data.Binding(nameof(Item.Key)),
            Group = "Explorers",
        });

        dock.Groups.Add(new DockGroup { Name = "Explorers", Seed = DockDirection.Left, SeedSize = 0.2 });
        dock.Groups.Add(new DockGroup { Name = "Stage", Seed = DockDirection.Right, SeedSize = 0.8, IsPersistent = true });

        var items = new ObservableCollection<object>();
        dock.ItemsSource = items;

        new Window { Width = 1200, Height = 800, Content = dock }.Show();
        Flush();

        return (dock, items);
    }

    /// <summary>
    /// A tool docked, a document opened beside it, and both gone again, as a
    /// shell's workspace closing takes them.
    /// </summary>
    [AvaloniaFact]
    public void ADockKeepsNothingOfItemsThatLeftIt()
    {
        var (dock, items) = Build();

        items.Add(new Item("kept"));
        Flush();

        var left = DockedThenGone(items);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.All(left, item => Assert.False(item.IsAlive, $"{item.Target} is still held"));

        GC.KeepAlive(dock);
    }

    /// <summary>Docks a tool, opens a document, then takes both away.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] DockedThenGone(ObservableCollection<object> items)
    {
        var tool = new Item("tool");
        var document = new Document("document");

        items.Add(tool);
        Flush();

        items.Add(document);
        Flush();

        items.Remove(document);
        items.Remove(tool);
        Flush();

        return new[] { new WeakReference(tool), new WeakReference(document) };
    }
}
