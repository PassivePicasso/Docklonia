using Avalonia;
using Avalonia.Controls;

namespace Docklonia.Controls;

/// <summary>
/// The bespoke tab-strip layout of §4: box tabs that grow to fill the strip,
/// wrapped onto as many lines as it takes for every tab to show its content in
/// full.
/// </summary>
/// <remarks>
/// <para><b>Required width.</b> A tab's required width is the width at which all
/// of its content fits — label, close button, and icon together. None of those
/// is overlaid on another, and the close button and icon are never the first
/// thing sacrificed. Children are measured against <see cref="MaxTabWidth"/>, so
/// a child's own <c>MaxWidth</c> narrows the bound further but nothing can raise
/// it.</para>
///
/// <para><b>Why the cap exists.</b> <see cref="MaxTabWidth"/> is what makes the
/// algorithm terminate: beyond it a label truncates rather than causing another
/// line to be added, so one long title cannot demand unbounded lines and eat the
/// content area. Line count is additionally bounded by the tab count — one tab
/// per line is the most useful subdivision, after which tabs simply truncate.</para>
///
/// <para><b>Line-count selection rule.</b> The <i>minimum</i> line count at which
/// every tab reaches its bounded required width. Tabs on a line divide the strip
/// equally, so a line of <c>n</c> tabs fits when
/// <c>available / n &gt;= max(requiredWidth)</c> over that line.</para>
///
/// <para><b>Distribution and tie-breaking.</b> Tabs spread across the chosen
/// lines as evenly as possible; when the count does not divide evenly the
/// remainder goes to the <i>earlier</i> lines — 7 tabs on 3 lines gives 3/2/2,
/// never 5/1/1.</para>
///
/// <para><b>No occlusion.</b> The panel reports the full height of every line as
/// its desired size, so the strip displaces the content area instead of drawing
/// over it.</para>
/// </remarks>
public class DockTabStripPanel : Panel
{
    /// <summary>Upper bound on a tab's required width. Beyond it the label truncates.</summary>
    public static readonly StyledProperty<double> MaxTabWidthProperty =
        AvaloniaProperty.Register<DockTabStripPanel, double>(nameof(MaxTabWidth), 240d);

    /// <summary>Floor on a tab's arranged width, so a crowded strip stays a usable pointer target.</summary>
    public static readonly StyledProperty<double> MinTabWidthProperty =
        AvaloniaProperty.Register<DockTabStripPanel, double>(nameof(MinTabWidth), 48d);

    private const string FirstInLine = ":first-in-line";
    private const string LastInLine = ":last-in-line";

    private readonly List<LineMetrics> _lines = new();

    static DockTabStripPanel()
    {
        AffectsMeasure<DockTabStripPanel>(MaxTabWidthProperty, MinTabWidthProperty);
    }

    public double MaxTabWidth
    {
        get => GetValue(MaxTabWidthProperty);
        set => SetValue(MaxTabWidthProperty, value);
    }

    public double MinTabWidth
    {
        get => GetValue(MinTabWidthProperty);
        set => SetValue(MinTabWidthProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _lines.Clear();

        var tabs = Children.Where(child => child.IsVisible).ToArray();

        if (tabs.Length == 0)
        {
            return default;
        }

        var bound = Math.Max(MinTabWidth, MaxTabWidth);

        foreach (var tab in tabs)
        {
            tab.Measure(new Size(bound, availableSize.Height));
        }

        var available = double.IsInfinity(availableSize.Width) ? bound * tabs.Length : availableSize.Width;
        var lineCount = ChooseLineCount(tabs, available);

        BuildLines(tabs, lineCount, available);

        return new Size(
            double.IsInfinity(availableSize.Width) ? _lines.Max(line => line.Count) * bound : availableSize.Width,
            _lines.Sum(line => line.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0d;

        foreach (var line in _lines)
        {
            var width = Math.Max(MinTabWidth, finalSize.Width / line.Count);
            var x = 0d;

            for (var i = 0; i < line.Count; i++)
            {
                var tab = line.Tabs[i];
                tab.Arrange(new Rect(x, y, width, line.Height));

                if (tab is Control control)
                {
                    control.Classes.Set(FirstInLine, i == 0);
                    control.Classes.Set(LastInLine, i == line.Count - 1);
                }

                x += width;
            }

            y += line.Height;
        }

        return new Size(finalSize.Width, Math.Max(y, finalSize.Height));
    }

    /// <summary>
    /// The minimum line count at which every line's widest tab still reaches its
    /// bounded required width, capped at one tab per line.
    /// </summary>
    private static int ChooseLineCount(IReadOnlyList<Control> tabs, double available)
    {
        for (var lines = 1; lines < tabs.Count; lines++)
        {
            if (Fits(tabs, lines, available))
            {
                return lines;
            }
        }

        return tabs.Count;
    }

    private static bool Fits(IReadOnlyList<Control> tabs, int lineCount, double available)
    {
        var index = 0;

        for (var line = 0; line < lineCount; line++)
        {
            var count = CountOnLine(tabs.Count, lineCount, line);
            var widest = 0d;

            for (var i = 0; i < count; i++, index++)
            {
                widest = Math.Max(widest, tabs[index].DesiredSize.Width);
            }

            if (count > 0 && available / count < widest)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Even distribution; the remainder goes to the earlier lines.</summary>
    private static int CountOnLine(int total, int lineCount, int line)
        => total / lineCount + (line < total % lineCount ? 1 : 0);

    private void BuildLines(IReadOnlyList<Control> tabs, int lineCount, double available)
    {
        var index = 0;

        for (var line = 0; line < lineCount; line++)
        {
            var count = CountOnLine(tabs.Count, lineCount, line);

            if (count == 0)
            {
                continue;
            }

            var members = new Control[count];
            var height = 0d;

            for (var i = 0; i < count; i++, index++)
            {
                members[i] = tabs[index];
                height = Math.Max(height, tabs[index].DesiredSize.Height);
            }

            _lines.Add(new LineMetrics(members, height));
        }
    }

    private readonly record struct LineMetrics(Control[] Tabs, double Height)
    {
        public int Count => Tabs.Length;
    }
}
