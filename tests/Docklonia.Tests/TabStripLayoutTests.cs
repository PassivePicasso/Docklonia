using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Docklonia.Controls;
using Xunit;

namespace Docklonia.Tests;

/// <summary>
/// Layout tests for the tab strip panel (§4): packing at required widths,
/// wrapping by content fit, and even distribution across the lines.
/// </summary>
public class TabStripLayoutTests
{
    /// <summary>A tab-shaped child: fixed required size, arranged size honoured.</summary>
    private sealed class Stub : Control
    {
        private readonly Size _required;

        public Stub(double width) => _required = new Size(width, 24);

        protected override Size MeasureOverride(Size availableSize)
            => new(Math.Min(_required.Width, availableSize.Width), _required.Height);
    }

    private static DockTabStripPanel Strip(params double[] requiredWidths)
    {
        var panel = new DockTabStripPanel { MaxTabWidth = 240, MinTabWidth = 20 };

        foreach (var width in requiredWidths)
        {
            panel.Children.Add(new Stub(width));
        }

        return panel;
    }

    private static Rect[] Layout(DockTabStripPanel panel, double width)
    {
        panel.Measure(new Size(width, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));

        return panel.Children.Select(child => child.Bounds).ToArray();
    }

    [AvaloniaFact]
    public void TabsOnALineKeepTheirDifferingWidths()
    {
        var bounds = Layout(Strip(60, 120, 90), 400);

        Assert.All(bounds, rect => Assert.Equal(0, rect.Y));
        // Width differences survive the equal share, up to pixel snapping.
        Assert.InRange(bounds[1].Width - bounds[0].Width, 59, 61);
        Assert.InRange(bounds[1].Width - bounds[2].Width, 29, 31);
    }

    [AvaloniaFact]
    public void LeftoverWidthIsSharedEquallyAndFillsTheStrip()
    {
        var bounds = Layout(Strip(60, 120, 90), 400);

        Assert.Equal(0, bounds[0].X, 3);
        Assert.Equal(400, bounds[^1].Right, 3);
        Assert.InRange(bounds[0].Width - 60, bounds[1].Width - 121, bounds[1].Width - 119);
    }

    [AvaloniaFact]
    public void TabsWrapWhenTheirRequiredWidthsDoNotFit()
    {
        // 5 tabs of 100 need 500; a 300-wide strip fits 3 on a line, so two
        // lines with the remainder on the earlier one: 3/2.
        var bounds = Layout(Strip(100, 100, 100, 100, 100), 300);

        Assert.Equal(3, bounds.Count(rect => rect.Y == 0));
        Assert.Equal(2, bounds.Count(rect => rect.Y == 24));
        Assert.Equal(300, bounds[2].Right, 3);
        Assert.Equal(300, bounds[4].Right, 3);
    }

    [AvaloniaFact]
    public void StripHeightGrowsByTheLineCountSoContentIsNotOccluded()
    {
        var panel = Strip(100, 100, 100, 100, 100);

        panel.Measure(new Size(300, double.PositiveInfinity));

        Assert.Equal(48, panel.DesiredSize.Height, 3);
    }

    [AvaloniaFact]
    public void OneTabPerLineIsTheDeepestSubdivision()
    {
        var bounds = Layout(Strip(200, 200, 200), 120);

        Assert.Equal(new[] { 0d, 24d, 48d }, bounds.Select(rect => rect.Y));
        Assert.All(bounds, rect => Assert.Equal(120, rect.Width, 3));
    }
}
