using System.Text.Json;
using System.Text.Json.Serialization;

namespace Docklonia.Serialization;

/// <summary>Formatting choices for <see cref="Docklonia.Model.DockLayout.ToJson"/>.</summary>
public sealed class LayoutJsonOptions
{
    internal static readonly LayoutJsonOptions Default = new();

    /// <summary>Indent the output. Useful when a layout file is checked in or diffed.</summary>
    public bool WriteIndented { get; set; }

    internal JsonSerializerOptions ToSerializerOptions() => new()
    {
        WriteIndented = WriteIndented,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
