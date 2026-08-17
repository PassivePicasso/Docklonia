using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(Docklonia.Tests.TestAppBuilder))]

namespace Docklonia.Tests;

/// <summary>
/// A headless application carrying the library's real theme, so control-level
/// tests exercise the shipped templates rather than a stand-in.
/// </summary>
public sealed class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://Docklonia.Tests/"))
        {
            Source = new Uri("avares://Docklonia/Themes/Docklonia.axaml"),
        });
    }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<TestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
