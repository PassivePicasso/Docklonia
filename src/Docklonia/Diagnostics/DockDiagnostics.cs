using Avalonia.Logging;

namespace Docklonia.Diagnostics;

/// <summary>
/// Configuration problems surfaced through Avalonia's own logging channel, so
/// nothing extra has to be wired up to see them (§1's zero-setup requirement).
/// </summary>
/// <remarks>
/// Unmatched content means two different things (§3.7). An item in
/// <c>ItemsSource</c> with no descriptor is almost certainly a forgotten
/// descriptor or a <c>DataType</c> typo, so it is reported <b>loudly</b>. A drop
/// target with no descriptor is almost certainly deliberate — the tool-area /
/// document-area separation — so it is <b>silent</b>: no guides, no drop, no
/// diagnostic.
/// </remarks>
public static class DockDiagnostics
{
    /// <summary>Log area used for every message this library emits.</summary>
    public const string LogArea = "Docklonia";

    internal static void Error(object source, string template, params object?[] values)
        => Logger.TryGet(LogEventLevel.Error, LogArea)?.Log(source, template, values);

    internal static void Warning(object source, string template, params object?[] values)
        => Logger.TryGet(LogEventLevel.Warning, LogArea)?.Log(source, template, values);
}
