using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Docklonia.Controls;

/// <summary>
/// The bespoke tab-strip layout of §4: a line packs as many tabs as fit at their
/// own required widths, and wraps onto as many lines as it takes for every tab
/// to show its content in full.
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
/// every line's tabs fit side by side at their required widths — a line of tabs
/// fits when the sum of those widths is no greater than the strip width.</para>
///
/// <para><b>Distribution and tie-breaking.</b> Tabs spread across the chosen
/// lines as evenly as possible; when the count does not divide evenly the
/// remainder goes to the <i>earlier</i> lines — 7 tabs on 3 lines gives 3/2/2,
/// never 5/1/1.</para>
///
/// <para><b>Leftover width.</b> Tabs keep differing widths: whatever the line
/// does not consume is divided into equal shares and added to every tab on it,
/// so the line fills the strip without flattening the tabs to one width. The
/// same equal share absorbs a deficit when a line cannot fit, floored by
/// <see cref="MinTabWidth"/>.</para>
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

        var unbounded = double.IsInfinity(availableSize.Width);
        var available = unbounded ? tabs.Sum(tab => tab.DesiredSize.Width) : availableSize.Width;

        BuildLines(tabs, ChooseLineCount(tabs, available));

        return new Size(
            unbounded ? _lines.Max(line => line.RequiredWidth) : availableSize.Width,
            _lines.Sum(line => line.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0d;

        var scale = UseLayoutRounding ? LayoutHelper.GetLayoutScale(this) : 0d;

        foreach (var line in _lines)
        {
            var share = (finalSize.Width - line.RequiredWidth) / line.Count;
            var edge = 0d;
            var x = 0d;

            for (var i = 0; i < line.Count; i++)
            {
                var tab = line.Tabs[i];

                edge += Math.Max(MinTabWidth, tab.DesiredSize.Width + share);

                // Tabs meet on snapped edges, so rounding never accumulates
                // into an overhang past the strip (§4).
                var right = scale > 0 ? LayoutHelper.RoundLayoutValue(edge, scale) : edge;

                tab.Arrange(new Rect(x, y, right - x, line.Height));

                (tab as DockTab)?.SetLinePosition(i == 0, i == line.Count - 1);

                x = right;
            }

            y += line.Height;
        }

        return new Size(finalSize.Width, Math.Max(y, finalSize.Height));
    }

    /// <summary>
    /// The minimum line count at which every line's tabs fit side by side at
    /// their required widths, capped at one tab per line.
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
            var required = 0d;

            for (var i = 0; i < count; i++, index++)
            {
                required += tabs[index].DesiredSize.Width;
            }

            if (required > available)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Even distribution; the remainder goes to the earlier lines.</summary>
    private static int CountOnLine(int total, int lineCount, int line)
        => total / lineCount + (line < total % lineCount ? 1 : 0);

    private void BuildLines(IReadOnlyList<Control> tabs, int lineCount)
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
            var required = 0d;

            for (var i = 0; i < count; i++, index++)
            {
                members[i] = tabs[index];
                height = Math.Max(height, tabs[index].DesiredSize.Height);
                required += tabs[index].DesiredSize.Width;
            }

            _lines.Add(new LineMetrics(members, height, required));
        }
    }

    private readonly record struct LineMetrics(Control[] Tabs, double Height, double RequiredWidth)
    {
        public int Count => Tabs.Length;
    }
}
