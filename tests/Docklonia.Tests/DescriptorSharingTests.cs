using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Docklonia.Descriptors;
using Docklonia.Model;
using Xunit;
using Dock = Docklonia.Controls.Dock;

namespace Docklonia.Tests;

/// <summary>
/// Covers descriptor sets authored once and given to more than one
/// <c>Dock</c> — by assignment, by resource, or by a style (§3.7).
/// </summary>
public class DescriptorSharingTests
{
    private sealed class Tool
    {
        public Tool(string name) => Name = name;

        public string Name { get; }
    }

    private static DockItemDescriptors ToolSet() => new()
    {
        new DockItemDescriptor
        {
            DataType = typeof(Tool),
            Title = new Binding(nameof(Tool.Name)),
            ContentKey = new Binding(nameof(Tool.Name)),
        },
    };

    private static Window Show(params Control[] content)
    {
        var panel = new StackPanel();

        foreach (var child in content)
        {
            panel.Children.Add(child);
        }

        var window = new Window { Width = 800, Height = 600, Content = panel };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static IEnumerable<string?> TitlesOf(Dock dock)
        => dock.Layout!.AllPanes().OfType<DockContent>().Select(content => content.Title);

    /// <summary>
    /// A styled property's default value is one instance shared by the whole
    /// type, which is exactly what must not happen to a collection.
    /// </summary>
    [AvaloniaFact]
    public void EachDockGetsItsOwnCollectionWhenNoneIsAssigned()
    {
        var first = new Dock();
        var second = new Dock();

        first.ItemDescriptors.Add(new DockItemDescriptor { DataType = typeof(Tool) });

        Assert.NotSame(first.ItemDescriptors, second.ItemDescriptors);
        Assert.Empty(second.ItemDescriptors);
    }

    /// <summary>
    /// One authored set, two Docks, two independent realizations — the point of
    /// making the set shareable at all.
    /// </summary>
    [AvaloniaFact]
    public void OneAssignedSetServesTwoDocksIndependently()
    {
        var shared = ToolSet();

        var first = new Dock
        {
            ItemDescriptors = shared,
            ItemsSource = new ObservableCollection<object> { new Tool("Outline") },
        };

        var second = new Dock
        {
            ItemDescriptors = shared,
            ItemsSource = new ObservableCollection<object> { new Tool("Errors") },
        };

        Show(first, second);

        Assert.Same(shared, first.ItemDescriptors);
        Assert.Same(shared, second.ItemDescriptors);
        Assert.Equal(new[] { "Outline" }, TitlesOf(first));
        Assert.Equal(new[] { "Errors" }, TitlesOf(second));
    }

    /// <summary>A class confers the set, so a Dock is declared a tool area by styling.</summary>
    [AvaloniaFact]
    public void AStyleCanConferTheDescriptorSet()
    {
        var dock = new Dock
        {
            Classes = { "tools" },
            ItemsSource = new ObservableCollection<object> { new Tool("Outline") },
        };

        var window = new Window { Width = 800, Height = 600, Content = dock };

        window.Styles.Add(new Style(selector => selector.OfType<Dock>().Class("tools"))
        {
            Setters = { new Setter(Dock.ItemDescriptorsProperty, ToolSet()) },
        });

        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(dock.DescribesContent(new Tool("Errors")));
        Assert.Equal(new[] { "Outline" }, TitlesOf(dock));
    }

    /// <summary>
    /// Descriptors authored on the element win over a style's, because inline
    /// authoring is a local value — the ordinary XAML precedence rule.
    /// </summary>
    [AvaloniaFact]
    public void InlineDescriptorsOutrankAStyle()
    {
        var inline = ToolSet();
        var dock = new Dock { Classes = { "tools" } };

        dock.ItemDescriptors.Add(inline[0]);

        var window = new Window { Width = 800, Height = 600, Content = dock };

        window.Styles.Add(new Style(selector => selector.OfType<Dock>().Class("tools"))
        {
            Setters = { new Setter(Dock.ItemDescriptorsProperty, ToolSet()) },
        });

        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Same(inline[0], dock.ItemDescriptors[0]);
    }

    /// <summary>
    /// A set assigned after the Dock is in the tree redefines what it accepts,
    /// so the resolved set and the placed items follow it.
    /// </summary>
    [AvaloniaFact]
    public void ReplacingTheSetOnALiveDockTakesEffect()
    {
        var dock = new Dock { ItemsSource = new ObservableCollection<object> { new Tool("Outline") } };

        Show(dock);

        Assert.False(dock.DescribesContent(new Tool("Outline")));

        dock.ItemDescriptors = ToolSet();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(dock.DescribesContent(new Tool("Outline")));
        Assert.Equal(new[] { "Outline" }, TitlesOf(dock));
    }
}
