using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Docklonia.Descriptors;

namespace Docklonia.Controls;

/// <summary>
/// Renders a <see cref="AuthoredContent"/> by building a fresh instance of its
/// declared content (§9.1).
/// </summary>
/// <remarks>
/// Registered on every <c>Dock</c> so authored panes need no consumer-supplied
/// template, which keeps the authored path as zero-setup as the bound one.
/// Building per presentation is what gives a duplicated <c>DockItem</c> its own
/// independent control instance.
/// </remarks>
internal sealed class AuthoredContentTemplate : IDataTemplate
{
    internal static readonly AuthoredContentTemplate Instance = new();

    public bool Match(object? data) => data is AuthoredContent;

    public Control? Build(object? data) => (data as AuthoredContent)?.Build();
}
