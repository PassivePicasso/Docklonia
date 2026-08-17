# Keyboard-only walkthrough

An acceptance test for §11. Drag-and-drop is inherently pointer-driven, so the
**pane menu is what makes docking keyboard-accessible** — it exposes every
operation the drag engine does, not a subset. This walkthrough exercises each
one without touching a pointer.

Run the sample (`dotnet run --project samples/Docklonia.Sample`) and put the
pointer aside.

## Key map

| Gesture | Scope | Effect |
|---|---|---|
| `Tab` / `Shift+Tab` | Application | Standard focus traversal into and out of the `Dock` |
| `F6` / `Shift+F6` | `Dock` | Cycle panes in activation order |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | `Dock` | Cycle panes in most-recently-used order |
| `Alt+←/→/↑/↓` | `Dock` | Directional traversal to the nearest pane that way |
| `←` `→` `↑` `↓` | Tab strip | Move between tabs, including across wrapped lines |
| `Home` / `End` | Tab strip | First / last tab in the strip |
| `Ctrl+PageUp` / `Ctrl+PageDown` | Active pane | Previous / next tab |
| `Enter` / `Space` | Tab | Activate (selects **and** takes focus) |
| `Delete` | Tab | Close, honouring `CanClose` and `CloseCommand` |
| `Shift+F10` or `Menu` | Active pane | Open the pane menu |
| `←` `→` `↑` `↓` | Splitter | Resize in steps, clamped at `MinPaneSize` |
| `Enter` / `Space` | Auto-hide button | Slide the pane out |
| `Ctrl+P` | Auto-hide button | Re-pin into the tree |
| `←` `→` `↑` `↓` | Flyout grip | Resize the flyout, clamped to its bounds |
| `Escape` | During a drag | Cancel with no mutation |

## Walkthrough

### 1. Reach the dock and move between panes

1. Press `Tab` until focus enters the document pane.
2. Press `F6` repeatedly. Focus cycles through the document pane, the tool pane,
   and the output pane. Each pane's titlebar takes the active accent as it gains
   activation — the `:active` pseudo-class.
3. Press `Alt+→`. Focus moves to the tool pane on the right. `Alt+↓` reaches the
   output pane. Traversal is geometric, so it matches what you can see rather
   than the tree's shape.

**Verifies:** directional traversal, pane cycling, activation as logical focus.

### 2. Move between tabs, including across a wrapped line

1. `F6` to the tool pane. It holds *Inspector*, *Outline*, and *Palette*, which
   the strip wraps onto more than one line.
2. Press `→` three times. Selection walks all three tabs in visual order —
   crossing the line break is invisible to keyboard navigation, because §4's
   wrapping is a visual arrangement and §11 forbids it fragmenting the group.
3. Press `Home`, then `End`.
4. `Ctrl+PageDown` / `Ctrl+PageUp` step through the same tabs from anywhere in
   the pane.

**Verifies:** arrow navigation across wrapped lines, `Home`/`End`, next/previous.

### 3. Split, float, and re-dock — the operations drag would do

1. `F6` to the document pane, then `Shift+F10` to open the pane menu.
2. Choose **Float**. The pane detaches into a floating window, preserving its
   internal tree.
3. In the floated pane press `Shift+F10` again and choose **Dock**. It rafts back
   into the main tree.

**Verifies:** float and raft without a pointer; the menu path covers the drag
engine's operations because both call the same mutation engine (§13).

### 4. Maximize and restore

1. `Shift+F10` on any pane → **Maximize**. The pane covers the whole `Dock`; its
   siblings are hidden, not removed.
2. `Shift+F10` → **Restore**. The layout returns exactly as it was, because
   maximize is a property of the layout rather than a tree mutation, so nothing
   normalized.

**Verifies:** §5.3 maximize semantics.

### 5. Auto-hide to an edge and restore to the original position

1. `F6` to the tool pane. Note where it sits.
2. `Shift+F10` → **Auto-hide**. The pane leaves the tree and parks as a button on
   the nearest `Dock` edge. The remaining panes reflow to fill the space.
3. `Tab` to the auto-hide button and press `Enter`. The pane slides out over the
   content as an overlay — it does not resize the layout or displace anything.
4. `Tab` to the grip on the flyout's inner edge and use the arrow keys. The
   flyout resizes, and the new size is stored on the entry as a proportion, so it
   persists across re-opening and across save/load. The grip is the same
   `DockSplitter` that resizes a real split, so it behaves identically.
5. Press `Ctrl+P` to re-pin.

The pane returns **to where it was**, not to its seed. That works because the
restore target is stored as a relative anchor — a surviving sibling's id plus a
direction — rather than a path, which unrelated docking operations would have
invalidated while the pane sat hidden (§5.3).

**Verifies:** auto-hide, flyout, and anchored restore, all from the keyboard.

### 6. Resize a split

1. `Tab` until a splitter takes focus (it is focusable and shows its focus
   state).
2. Hold `←` or `→`. The split resizes in steps and **stops** at `MinPaneSize`
   rather than continuing — a pane cannot be driven out of existence.

**Verifies:** §3.3's floor, honoured identically by keyboard and pointer.

### 7. Close, and the veto

1. `F6` to the document pane, `→` to a tab, press `Delete`.
2. For a document marked dirty, the tab **stays open** and the status bar reports
   the veto: the descriptor's `CloseCommand` was invoked instead of closing, and
   the application declined. The library never waited on a cancellable event.
3. Use the toolbar's *Toggle dirty*, then `Delete` again. The tab closes, and the
   status bar reports that the document's last view was released — the
   `ClosedCommand` firing exactly once.
4. Try `Delete` on the *Inspector* tab. Nothing happens: its descriptor sets a
   constant `CanClose="False"`, and the close affordance is hidden rather than
   shown disabled.

**Verifies:** §3.10 veto and last-view notification, §3.7 constant descriptor
values.

### 8. Focus never drops

After closing a pane, focus moves deterministically to the next pane in
activation order rather than being lost. Without a pointer, dropped focus is
unrecoverable, so this is a requirement rather than a nicety (§11).

## Assistive technology

With a screen reader attached:

- Each pane reports as a **tab list** with correct selection state, despite the
  strip being a bespoke `Panel` rather than a `TabControl`. A strip wrapped onto
  three lines is still reported as **one** group, not three.
- Tabs report as tab items and expose selection **without** moving focus,
  matching the model's own separation of selection from activation (§3.11).
- Names come from the same `Title` shown visually, so no parallel accessibility
  metadata exists to drift out of step.
- Splitters and auto-hide buttons are reported as real controls.
