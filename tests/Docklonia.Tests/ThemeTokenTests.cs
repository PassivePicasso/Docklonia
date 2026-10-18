using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Docklonia.Controls;
using Docklonia.Descriptors;
using Unleash.Theme;
using Xunit;
using Dock = Docklonia.Controls.Dock;

namespace Docklonia.Tests;

/// <summary>
/// The theme's keys read the host's theme tokens, follow a scheme or density
/// switch on a dock already shown, and still lose to an application's override.
/// </summary>
public class ThemeTokenTests
{
    private static readonly string[] BrushKeys =
    {
        "DockBackground", "DockPaneBackground", "DockPaneBorderBrush",
        "DockTitleBarBackground", "DockTitleBarActiveBackground", "DockTitleForeground",
        "DockTabBackground", "DockTabHoverBackground", "DockTabSelectedBackground",
        "DockTabActiveAccent", "DockTabForeground", "DockSplitterBrush",
        "DockSplitterHoverBrush", "DockGuideBackground", "DockGuideBorderBrush",
        "DockGuideHotBackground", "DockGuideGlyphBrush", "DockGuideHotGlyphBrush",
        "DockDropPreviewBrush", "DockDropPreviewBorderBrush", "DockGhostBackground",
    };

    private static readonly string[] MetricKeys =
    {
        "DockPaneBorderThickness", "DockTabPadding", "DockTabCornerRadius",
        "DockGuideCornerRadius", "DockSplitterThickness", "DockTitleBarHeight",
        "DockTabMaxWidth", "DockTabMinWidth", "DockAutoHideStripThickness",
    };

    // Tab width bounds are layout limits the token set has no size for.
    private static readonly string[] LiteralKeys = { "DockTabMaxWidth", "DockTabMinWidth" };

    private sealed class Doc
    {
        public Doc(string key) => Key = key;

        public string Key { get; }
    }

    [AvaloniaFact]
    public void EveryKeyReadsAToken()
    {
        var resources = (ResourceDictionary)AvaloniaXamlLoader.Load(
            new Uri("avares://Docklonia/Themes/Resources.axaml"));
        var tokens = resources.MergedDictionaries.OfType<ComponentTokens>().Single();
        var mapped = tokens.Keys.ToDictionary(entry => entry.Key, entry => entry.Token);

        var named = mapped.Keys.Concat(resources.Keys.Cast<string>()).OrderBy(key => key);
        Assert.Equal(BrushKeys.Concat(MetricKeys).OrderBy(key => key), named);
        Assert.Equal(LiteralKeys.OrderBy(key => key), resources.Keys.Cast<string>().OrderBy(key => key));

        var app = Application.Current!;

        foreach (var key in BrushKeys)
        {
            Assert.Contains(mapped[key], Token.Colours);
        }

        foreach (var key in MetricKeys.Except(LiteralKeys))
        {
            Assert.Contains(mapped[key], Token.Sizes);
        }

        foreach (var variant in new[] { ThemeVariant.Dark, ThemeVariant.Light, ThemeTokens.HighContrast })
        {
            foreach (var (key, token) in mapped)
            {
                Assert.True(app.TryFindResource(key, variant, out var value), key);
                Assert.True(app.TryFindResource(token, variant, out var expected), token);
                Assert.Same(expected, value);
            }
        }
    }

    [AvaloniaFact]
    public void ASchemeOrDensitySwitchRestylesAShownPaneAndTab()
    {
        Switching(() =>
        {
            var window = Host();
            var pane = window.GetVisualDescendants().OfType<DockPaneControl>().First();
            var tab = window.GetVisualDescendants().OfType<DockTab>().First();
            var titleBar = window.GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_TitleBar");
            var seen = new List<(Color Surface, Thickness Padding)>();

            foreach (var (scheme, density) in new[]
            {
                (Scheme.Dark, Density.Default),
                (Scheme.Light, Density.Compact),
                (Scheme.HighContrast, Density.Default),
                (Scheme.Dark, Density.Compact),
            })
            {
                ThemeTokens.Apply(Application.Current!, scheme, density);
                Flush();

                Assert.Equal(ColourOf(pane, Token.Surface), ((ISolidColorBrush)pane.Background!).Color);
                Assert.Equal(ColourOf(pane, Token.Border), ((ISolidColorBrush)pane.BorderBrush!).Color);
                Assert.Equal(ValueOf(pane, Token.Stroke), pane.BorderThickness);
                Assert.Equal(ValueOf(pane, Token.ControlHeight), titleBar.Height);
                Assert.Equal(ColourOf(tab, Token.SurfaceRaised), ((ISolidColorBrush)tab.Background!).Color);
                Assert.Equal(ColourOf(tab, Token.Text), ((ISolidColorBrush)tab.Foreground!).Color);
                Assert.Equal(ValueOf(tab, Token.ControlPadding), tab.Padding);

                seen.Add((((ISolidColorBrush)pane.Background!).Color, tab.Padding));
            }

            // Each switch changed what is drawn, so the asserts above saw it follow.
            Assert.NotEqual(seen[0].Surface, seen[1].Surface);
            Assert.NotEqual(seen[1].Surface, seen[2].Surface);
            Assert.NotEqual(seen[0].Padding, seen[1].Padding);
            Assert.NotEqual(seen[2].Padding, seen[3].Padding);
        });
    }

    [AvaloniaFact]
    public void AnApplicationsOverrideOutranksTheToken()
    {
        Switching(() =>
        {
            ThemeTokens.Apply(Application.Current!, Scheme.Dark, Density.Default);

            var window = Host();
            var pane = window.GetVisualDescendants().OfType<DockPaneControl>().First();
            var tab = window.GetVisualDescendants().OfType<DockTab>().First();
            var restyle = new Styles
            {
                Resources =
                {
                    ["DockPaneBackground"] = new SolidColorBrush(Colors.DeepPink),
                    ["DockTabPadding"] = new Thickness(14, 7),
                },
            };

            Application.Current!.Styles.Add(restyle);

            try
            {
                Flush();
                Assert.Equal(Colors.DeepPink, ((ISolidColorBrush)pane.Background!).Color);
                Assert.Equal(new Thickness(14, 7), tab.Padding);
            }
            finally
            {
                Application.Current!.Styles.Remove(restyle);
            }

            Flush();
            Assert.Equal(ColourOf(pane, Token.Surface), ((ISolidColorBrush)pane.Background!).Color);
            Assert.Equal(ValueOf(tab, Token.ControlPadding), tab.Padding);
        });
    }

    /// <summary>Runs a test that applies the tokens, then puts the shared application back.</summary>
    private static void Switching(Action test)
    {
        var app = Application.Current!;
        var requested = app.RequestedThemeVariant;
        var merged = app.Resources.MergedDictionaries.ToList();

        try
        {
            test();
        }
        finally
        {
            foreach (var added in app.Resources.MergedDictionaries.Except(merged).ToList())
            {
                app.Resources.MergedDictionaries.Remove(added);
            }

            app.RequestedThemeVariant = requested;
        }
    }

    private static Window Host()
    {
        var dock = new Dock();

        dock.ItemDescriptors.Add(new DockItemDescriptor
        {
            DataType = typeof(Doc),
            Title = new Avalonia.Data.Binding(nameof(Doc.Key)),
            ContentKey = new Avalonia.Data.Binding(nameof(Doc.Key)),
        });

        dock.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object>(
            new[] { new Doc("a"), new Doc("b") });

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

    private static Color ColourOf(Control control, string token)
        => ((ISolidColorBrush)ValueOf(control, token)).Color;

    private static object ValueOf(Control control, string token)
    {
        Assert.True(control.TryFindResource(token, control.ActualThemeVariant, out var value), token);
        return value!;
    }
}
