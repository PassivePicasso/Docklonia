using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Docklonia.Sample.ViewModels;

namespace Docklonia.Sample;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Both windows share one shell view model, so the same documents are
            // reachable from either — which is what makes cross-window drag and
            // cross-window duplication observable.
            var shell = new ShellViewModel();

            desktop.MainWindow = new MainWindow { DataContext = shell };

            var second = new SecondWindow { DataContext = shell };
            second.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
