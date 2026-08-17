using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Docklonia.Sample;

/// <summary>
/// The only code-behind in the sample, and it does nothing but load the XAML.
/// No docking operation requires an event handler here (§1).
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
