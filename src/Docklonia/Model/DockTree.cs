namespace Docklonia.Model;

/// <summary>Read-only traversal over a layout tree. Never mutates.</summary>
public static class DockTree
{
    public static IEnumerable<IDockPane> SelfAndDescendants(IDockPane? node)
    {
        if (node is null)
        {
            yield break;
        }

        yield return node;

        foreach (var child in node.Children)
        {
            foreach (var descendant in SelfAndDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    public static IEnumerable<IDockPane> Ancestors(IDockPane? node)
    {
        for (var current = node?.Parent; current is not null; current = current.Parent)
        {
            yield return current;
        }
    }

    public static IEnumerable<IDockPane> SelfAndAncestors(IDockPane? node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            yield return current;
        }
    }

    /// <summary>The topmost node above <paramref name="node"/> — a root or a <see cref="FloatPane"/>.</summary>
    public static IDockPane? RootOf(IDockPane? node) => SelfAndAncestors(node).LastOrDefault();

    /// <summary>The <see cref="FloatPane"/> hosting this node, or null if it lives in the main tree.</summary>
    public static FloatPane? FloatOf(IDockPane? node) => RootOf(node) as FloatPane;

    public static IEnumerable<DockContent> ContentsIn(IDockPane? node)
        => SelfAndDescendants(node).OfType<DockContent>();

    public static IEnumerable<DockTabPane> TabPanesIn(IDockPane? node)
        => SelfAndDescendants(node).OfType<DockTabPane>();

    public static bool Contains(IDockPane? ancestor, IDockPane? node)
        => ancestor is not null && SelfAndAncestors(node).Any(candidate => ReferenceEquals(candidate, ancestor));

    public static IDockPane? FindById(IDockPane? root, string id)
        => SelfAndDescendants(root).FirstOrDefault(pane => pane.Id == id);
}
