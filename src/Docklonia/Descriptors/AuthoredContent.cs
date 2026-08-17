using Avalonia.Controls;
using Avalonia.Markup.Xaml.Templates;

namespace Docklonia.Descriptors;

/// <summary>
/// The data stand-in for a <see cref="DockItem"/>'s authored content, so an
/// authored pane travels through the layout tree as data like everything else
/// (§9.1).
/// </summary>
/// <remarks>
/// One instance per <see cref="DockItem"/>, which is what makes the item's
/// cardinality match a bound view model's: N <c>DockContent</c> nodes may
/// reference it, and each presentation calls <see cref="Build"/> for its own
/// control instance.
/// </remarks>
public sealed class AuthoredContent
{
    private readonly DockItem _item;

    internal AuthoredContent(DockItem item)
    {
        _item = item;
    }

    /// <summary>The originating declaration, so the <c>Dock</c> can read its metadata.</summary>
    internal DockItem Item => _item;

    public string Title => _item.Title;

    public string ContentKey => _item.ContentKey;

    /// <summary>Materializes a fresh, independent instance of the authored content.</summary>
    public Control? Build() => _item.Content is null ? null : TemplateContent.Load(_item.Content)?.Result;

    public override string ToString() => _item.Title;
}
