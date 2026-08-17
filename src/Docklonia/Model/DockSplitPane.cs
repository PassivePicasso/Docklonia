using Avalonia.Layout;

namespace Docklonia.Model;

/// <summary>
/// Exactly two children separated by a splitter (§3.3). The two-child invariant
/// is carried by the type: there are two typed, non-null slots and no
/// collection, so a third child is unrepresentable rather than merely refused.
/// </summary>
public sealed class DockSplitPane : DockPane, IDockNode
{
    /// <summary>Smallest ratio the model itself permits, before <c>Dock.MinPaneSize</c> narrows it further.</summary>
    public const double MinimumRatio = 0.01;

    private IDockNode _first;
    private IDockNode _second;
    private Orientation _orientation;
    private double _ratio = 0.5;

    public DockSplitPane(Orientation orientation, IDockNode first, IDockNode second, double ratio = 0.5)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (ReferenceEquals(first, second))
        {
            throw new ArgumentException("A split's two children must be distinct nodes.", nameof(second));
        }

        _orientation = orientation;
        _first = first;
        _second = second;
        _ratio = ClampRatio(ratio);

        Adopt(first);
        Adopt(second);
    }

    /// <summary>
    /// <see cref="Orientation.Horizontal"/> places the children side by side;
    /// <see cref="Orientation.Vertical"/> stacks them.
    /// </summary>
    public Orientation Orientation
    {
        get => _orientation;
        set => Set(ref _orientation, value);
    }

    /// <summary>Leading child — left when horizontal, top when vertical.</summary>
    public IDockNode First
    {
        get => _first;
        internal set => Replace(ref _first, value);
    }

    /// <summary>Trailing child — right when horizontal, bottom when vertical.</summary>
    public IDockNode Second
    {
        get => _second;
        internal set => Replace(ref _second, value);
    }

    /// <summary>
    /// Proportion of the available extent given to <see cref="First"/>.
    /// Proportional rather than absolute so layouts survive window resizing and
    /// restore (§3.3). Never reaches zero, so a pane cannot be dragged out of
    /// existence.
    /// </summary>
    public double Ratio
    {
        get => _ratio;
        set => Set(ref _ratio, ClampRatio(value));
    }

    public override IReadOnlyList<IDockNode> Children => new[] { _first, _second };

    internal IDockNode Other(IDockNode child)
    {
        if (ReferenceEquals(child, _first))
        {
            return _second;
        }

        if (ReferenceEquals(child, _second))
        {
            return _first;
        }

        throw new ArgumentException("Node is not a child of this split.", nameof(child));
    }

    internal void ReplaceChild(IDockNode existing, IDockNode replacement)
    {
        if (ReferenceEquals(existing, _first))
        {
            First = replacement;
        }
        else if (ReferenceEquals(existing, _second))
        {
            Second = replacement;
        }
        else
        {
            throw new ArgumentException("Node is not a child of this split.", nameof(existing));
        }
    }

    private void Replace(ref IDockNode slot, IDockNode value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (ReferenceEquals(slot, value))
        {
            return;
        }

        Release(slot);
        slot = value;
        Adopt(value);
        Raise(name);
        Raise(nameof(Children));
    }

    private static double ClampRatio(double value)
    {
        if (double.IsNaN(value))
        {
            return 0.5;
        }

        return Math.Clamp(value, MinimumRatio, 1d - MinimumRatio);
    }

    public override string ToString() => $"Split({Orientation}, {Ratio:0.###})";
}
