using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Docklonia.Model;

namespace Docklonia.Controls;

/// <summary>
/// One docking guide. Lookless — the arrow, chevron, or whatever the theme draws
/// is entirely template-supplied (§12).
/// </summary>
/// <remarks>
/// <b>Pseudo-classes.</b> <c>:left</c>, <c>:top</c>, <c>:right</c>,
/// <c>:bottom</c>, <c>:center</c>, <c>:outer</c>, <c>:hot</c>.
/// </remarks>
[PseudoClasses(":left", ":top", ":right", ":bottom", ":center", ":outer", ":hot")]
public class DockGuideButton : TemplatedControl
{
    public static readonly StyledProperty<DockDirection> DirectionProperty =
        AvaloniaProperty.Register<DockGuideButton, DockDirection>(nameof(Direction));

    public static readonly StyledProperty<bool> IsOuterProperty =
        AvaloniaProperty.Register<DockGuideButton, bool>(nameof(IsOuter));

    public static readonly StyledProperty<bool> IsHotProperty =
        AvaloniaProperty.Register<DockGuideButton, bool>(nameof(IsHot));

    static DockGuideButton()
    {
        DirectionProperty.Changed.AddClassHandler<DockGuideButton>((guide, _) => guide.UpdatePseudoClasses());
        IsOuterProperty.Changed.AddClassHandler<DockGuideButton>((guide, _) => guide.UpdatePseudoClasses());
        IsHotProperty.Changed.AddClassHandler<DockGuideButton>((guide, _) => guide.UpdatePseudoClasses());
    }

    public DockGuideButton()
    {
        UpdatePseudoClasses();
    }

    public DockDirection Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <summary>True for a guide that docks against the <c>Dock</c> root rather than the hovered pane.</summary>
    public bool IsOuter
    {
        get => GetValue(IsOuterProperty);
        set => SetValue(IsOuterProperty, value);
    }

    /// <summary>True while the cursor is over this guide.</summary>
    public bool IsHot
    {
        get => GetValue(IsHotProperty);
        set => SetValue(IsHotProperty, value);
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":left", Direction == DockDirection.Left);
        PseudoClasses.Set(":top", Direction == DockDirection.Top);
        PseudoClasses.Set(":right", Direction == DockDirection.Right);
        PseudoClasses.Set(":bottom", Direction == DockDirection.Bottom);
        PseudoClasses.Set(":center", Direction == DockDirection.Center);
        PseudoClasses.Set(":outer", IsOuter);
        PseudoClasses.Set(":hot", IsHot);
    }
}
