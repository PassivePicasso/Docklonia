using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Docklonia.Controls;
using Docklonia.Model;

namespace Docklonia.Automation;

/// <summary>
/// Assistive-technology view of a <c>Dock</c> (§11).
/// </summary>
/// <remarks>
/// Names come from the same values shown visually — the descriptor's
/// <c>Title</c> — so no parallel accessibility metadata is introduced and the
/// two can never drift apart.
/// </remarks>
public class DockAutomationPeer : ControlAutomationPeer
{
    public DockAutomationPeer(Dock owner) : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

    protected override string GetClassNameCore() => nameof(Dock);
}

/// <summary>
/// A pane reports as a <b>tab list</b>, even though its strip is a bespoke
/// panel rather than a <c>TabControl</c>.
/// </summary>
/// <remarks>
/// Multi-line wrapping (§4) is a visual arrangement only. The children reported
/// here are the flat sequence of tabs, so a strip wrapped onto three lines is
/// still one group with one selection — never three reported groups.
/// </remarks>
public class DockPaneAutomationPeer : ControlAutomationPeer, ISelectionProvider
{
    private readonly DockPaneControl _owner;

    public DockPaneAutomationPeer(DockPaneControl owner) : base(owner)
    {
        _owner = owner;
    }

    public bool IsSelectionRequired => true;

    public bool CanSelectMultiple => false;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;

    protected override string GetClassNameCore() => nameof(DockPaneControl);

    protected override string? GetNameCore() => _owner.Node?.Title;

    public IReadOnlyList<AutomationPeer> GetSelection()
    {
        var selected = _owner.SelectedNode;

        return GetChildren()
            .Where(peer => peer is DockTabAutomationPeer tab && ReferenceEquals(tab.Node, selected))
            .ToArray();
    }
}

/// <summary>
/// One tab. Reports selection without moving focus, matching the model's own
/// distinction between the two (§3.11).
/// </summary>
public class DockTabAutomationPeer : ControlAutomationPeer, ISelectionItemProvider
{
    private readonly DockTab _owner;

    public DockTabAutomationPeer(DockTab owner) : base(owner)
    {
        _owner = owner;
    }

    internal IDockNode? Node => _owner.Node;

    public bool IsSelected => _owner.IsSelected;

    public ISelectionProvider? SelectionContainer =>
        _owner.Pane is { } pane ? GetOrCreatePeer(pane) as ISelectionProvider : null;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

    protected override string GetClassNameCore() => nameof(DockTab);

    protected override string? GetNameCore() => _owner.Node?.Title;

    public void Select()
    {
        if (_owner.Node is { } node)
        {
            _owner.Owner?.ActivateNode(node);
        }
    }

    public void AddToSelection() => Select();

    public void RemoveFromSelection()
    {
        // A tab group always has a selection, so deselection is not meaningful.
    }

    private static AutomationPeer? GetOrCreatePeer(DockPaneControl pane)
        => ControlAutomationPeer.CreatePeerForElement(pane);
}

/// <summary>A splitter. Focusable and arrow-resizable, so it is a real control to assistive technology.</summary>
public class DockSplitterAutomationPeer : ControlAutomationPeer
{
    public DockSplitterAutomationPeer(DockSplitter owner) : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Thumb;

    protected override string GetClassNameCore() => nameof(DockSplitter);
}

/// <summary>An auto-hide button, so a parked pane is reachable without a pointer.</summary>
public class DockAutoHideButtonAutomationPeer : ControlAutomationPeer
{
    private readonly DockAutoHideButton _owner;

    public DockAutoHideButtonAutomationPeer(DockAutoHideButton owner) : base(owner)
    {
        _owner = owner;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string GetClassNameCore() => nameof(DockAutoHideButton);

    protected override string? GetNameCore() => _owner.Title;
}
