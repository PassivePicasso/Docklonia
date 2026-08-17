using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Docklonia.Model;

namespace Docklonia.Serialization;

/// <summary>
/// Converts a <see cref="DockLayout"/> to and from JSON (§8). Structure only:
/// content is identified by key and matched against live items by the owning
/// <c>Dock</c>, so nothing is ever fabricated on load.
/// </summary>
internal static class LayoutSerializer
{
    internal static string Serialize(DockLayout layout, LayoutJsonOptions? options)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var document = new LayoutDto
        {
            Version = LayoutSchema.Version,
            Root = WriteNode(layout.Root),
            MaximizedPaneId = layout.MaximizedPane?.Id,
            ActivePaneId = layout.ActivePane?.Id,
        };

        foreach (var host in layout.Floats)
        {
            document.Floats.Add(new FloatDto
            {
                Id = host.Id,
                X = host.Position.X,
                Y = host.Position.Y,
                Width = host.Size.Width,
                Height = host.Size.Height,
                WindowState = host.WindowState.ToString(),
                Child = WriteNode(host.Child),
            });
        }

        foreach (var entry in layout.AutoHidden)
        {
            document.AutoHidden.Add(new AutoHideDto
            {
                Edge = entry.Edge.ToString(),
                AnchorId = entry.AnchorId,
                AnchorDirection = entry.AnchorDirection.ToString(),
                Ratio = entry.Ratio,
                Pane = WriteNode(entry.Pane),
            });
        }

        return JsonSerializer.Serialize(document, (options ?? LayoutJsonOptions.Default).ToSerializerOptions());
    }

    internal static DockLayout Deserialize(string json, LayoutJsonOptions? options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        LayoutDto? document;

        try
        {
            document = JsonSerializer.Deserialize<LayoutDto>(json, (options ?? LayoutJsonOptions.Default).ToSerializerOptions());
        }
        catch (JsonException exception)
        {
            throw new LayoutFormatException("The layout document is not valid JSON for this schema.", exception);
        }

        if (document is null)
        {
            throw new LayoutFormatException("The layout document was empty.");
        }

        if (document.Version < LayoutSchema.MinimumSupportedVersion || document.Version > LayoutSchema.Version)
        {
            throw new LayoutFormatException(
                $"Layout schema version {document.Version} is outside the supported range " +
                $"{LayoutSchema.MinimumSupportedVersion}-{LayoutSchema.Version}. See LayoutSchema for the " +
                "compatibility policy.");
        }

        var layout = new DockLayout { Root = ReadNode(document.Root) };

        foreach (var dto in document.Floats)
        {
            var child = ReadNode(dto.Child);

            if (child is null)
            {
                continue;
            }

            var host = new FloatPane(child, new PixelPoint(dto.X, dto.Y), new Size(dto.Width, dto.Height))
            {
                WindowState = Parse(dto.WindowState, WindowState.Normal),
            };

            host.Id = dto.Id;
            layout.Floats.Add(host);
        }

        foreach (var dto in document.AutoHidden)
        {
            var pane = ReadNode(dto.Pane);

            if (pane is null)
            {
                continue;
            }

            layout.AutoHidden.Add(new AutoHideEntry(
                pane,
                Parse(dto.Edge, DockEdge.Left),
                dto.AnchorId,
                Parse(dto.AnchorDirection, DockDirection.Left),
                dto.Ratio));
        }

        layout.MaximizedPane = FindPane(layout, document.MaximizedPaneId);
        layout.ActivePane = FindPane(layout, document.ActivePaneId);

        return layout;
    }

    private static NodeDto? WriteNode(IDockNode? node) => node switch
    {
        null => null,

        DockContent content => new ContentNodeDto
        {
            Id = content.Id,
            ContentKey = content.ContentKey,
        },

        DockSplitPane split => new SplitNodeDto
        {
            Id = split.Id,
            Orientation = split.Orientation.ToString(),
            Ratio = split.Ratio,
            First = WriteNode(split.First),
            Second = WriteNode(split.Second),
        },

        DockTabPane tabs => new TabsNodeDto
        {
            Id = tabs.Id,
            Group = tabs.Group,
            IsPersistent = tabs.IsPersistent,
            SelectedId = tabs.SelectedChild?.Id,
            Children = tabs.Children.Select(WriteNode).OfType<NodeDto>().ToList(),
        },

        _ => throw new LayoutFormatException($"Cannot serialize node type '{node.GetType().Name}'."),
    };

    private static IDockNode? ReadNode(NodeDto? dto)
    {
        switch (dto)
        {
            case null:
                return null;

            case ContentNodeDto content:
                return new DockContent { Id = content.Id, ContentKey = content.ContentKey };

            case SplitNodeDto splitDto:
            {
                var first = ReadNode(splitDto.First);
                var second = ReadNode(splitDto.Second);

                if (first is null || second is null)
                {
                    throw new LayoutFormatException("A split node must have both children.");
                }

                var split = new DockSplitPane(
                    Parse(splitDto.Orientation, Orientation.Horizontal),
                    first,
                    second,
                    splitDto.Ratio);

                split.Id = splitDto.Id;
                return split;
            }

            case TabsNodeDto tabsDto:
            {
                var tabs = new DockTabPane
                {
                    Id = tabsDto.Id,
                    Group = tabsDto.Group,
                    IsPersistent = tabsDto.IsPersistent,
                };

                foreach (var child in tabsDto.Children.Select(ReadNode).OfType<IDockNode>())
                {
                    tabs.Add(child);
                }

                tabs.SelectedChild = tabs.Children.FirstOrDefault(child => child.Id == tabsDto.SelectedId)
                    ?? tabs.Children.FirstOrDefault();

                return tabs;
            }

            default:
                throw new LayoutFormatException($"Unknown layout node kind '{dto.GetType().Name}'.");
        }
    }

    private static IDockPane? FindPane(DockLayout layout, string? id)
        => id is null ? null : layout.AllPanes().FirstOrDefault(pane => pane.Id == id);

    private static T Parse<T>(string? value, T fallback) where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}
