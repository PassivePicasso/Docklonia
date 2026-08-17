using System.Runtime.CompilerServices;
using Avalonia.Metadata;

[assembly: InternalsVisibleTo("Docklonia.Tests")]

// One XML namespace for the whole library, so a consumer writes a single `dock:`
// prefix for the control, its descriptors, and its groups — matching §9's
// integration example. Namespaces stay separate in C#.
[assembly: XmlnsDefinition(Docklonia.DocklonaXaml.Namespace, "Docklonia.Controls")]
[assembly: XmlnsDefinition(Docklonia.DocklonaXaml.Namespace, "Docklonia.Descriptors")]
[assembly: XmlnsDefinition(Docklonia.DocklonaXaml.Namespace, "Docklonia.Model")]
[assembly: XmlnsDefinition(Docklonia.DocklonaXaml.Namespace, "Docklonia.Dragging")]
[assembly: XmlnsPrefix(Docklonia.DocklonaXaml.Namespace, "dock")]

namespace Docklonia;

/// <summary>The XAML namespace every Docklonia type is reachable through.</summary>
public static class DocklonaXaml
{
    public const string Namespace = "https://github.com/docklonia";
}
