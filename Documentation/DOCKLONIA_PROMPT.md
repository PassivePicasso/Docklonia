# Docklonia — Project Prompt

A docking-layout library for Avalonia. This document is the authoritative
statement of what is to be built. It supersedes prior informal descriptions.

---

## 1. Goal

Build a self-contained docking library for Avalonia 12 (C#) that provides
Visual-Studio-class docking: split panes, tabbed groups, floating windows,
drag-and-drop re-docking with directional guides, and a JSON-serializable
layout.

### Non-negotiable constraints

| Constraint | Meaning |
|---|---|
| **No code-behind** | Consumers write XAML and view models only. The library exposes no API that requires an event handler in the consumer's window. |
| **Zero setup** | Placing a `<Dock>` in a view is the complete integration step. No bootstrapper, no service registration, no `App.axaml` edits beyond the single theme include if one is required — and if a theme include *is* required, justify why it cannot be avoided. |
| **Custom controls** | Behaviour lives in `Control`/`TemplatedControl` subclasses with default styles in generic theme resources, not in composed `UserControl`s. |
| **Simple, direct, no excess** | Prefer the smallest design that satisfies the requirement. Do not add extension points, abstraction layers, or configuration surface that no stated requirement needs. |
| **MVVM-native** | Every user-driven layout mutation must be expressible as a change to bindable state, and must round-trip through serialization. |
| **Zero consumer entanglement** | *Content* view models are POCOs that know nothing about this library. No interface to implement, no base class to inherit, no attribute, no library type referenced. The single exception is the shell view model's layout handle (§9.2), which is opaque. See §3.6. |

---

## 2. WPF → Avalonia translation notes

**Target: Avalonia 12, .NET 10+.** Backwards compatibility with earlier
Avalonia or .NET versions is explicitly not a goal. The mappings below are
written against Avalonia 11 idioms, which are stable in outline but must be
**verified against the Avalonia 12 API** before being relied on — treat any
specific type or method name here as a pointer, not as confirmed API.

The requirements below are written in WPF vocabulary. Translate as follows.

- `DependencyProperty` → `StyledProperty<T>` / `DirectProperty<T>` (use
  `DirectProperty` for values the control itself computes and owns).
- `FrameworkPropertyMetadataOptions.AffectsMeasure` → `AffectsMeasure<T>()` /
  `AffectsArrange<T>()` static registration.
- `ControlTemplate` in `Themes/Generic.xaml` → a `ControlTheme` in a resource
  dictionary merged from the library's theme entry point.
- `Control.OnApplyTemplate` + `GetTemplateChild` → `OnApplyTemplate(
  TemplateAppliedEventArgs e)` + `e.NameScope.Find<T>`.
- Attached-property-driven layout (`Grid.Row`) → identical concept, Avalonia
  `AttachedProperty<T>`.
- `Window` for floating hosts → a `FloatPane` (§5.2) realized as an Avalonia
  `TopLevel`; account for the fact that Avalonia targets platforms where a
  real OS window may be unavailable (see §5.2).
- Adorner layer for drag guides/overlays → Avalonia has **no adorner layer**.
  Use `AdornerLayer` where sufficient, or an overlay `Panel` in the
  `OverlayLayer` of the `TopLevel`. Choose one and use it consistently.
- Routed commands → `ICommand` properties on the control, or direct
  interaction; do not introduce a WPF-style routed-command infrastructure.

---

## 3. Object model

The layout is a **tree of view models**. Every node is an `IDockPane`.

```
IDockPane                 (Id, Title, IsVisible, Parent)
├── DockContent            leaf. Wraps one piece of consumer content.
├── DockSplitPane          exactly 2 children + Orientation + split ratio.
├── DockTabPane            N children. Is itself the tab host.
└── FloatPane              1 child + window geometry. Root only — see §5.2.
```

### 3.1 The tree is view models, not controls

**No `IDockPane` implementation derives from `Control`.** They are plain
objects implementing `INotifyPropertyChanged`. The `Dock` control is a *view*
over the tree: it materializes presenters for the nodes it currently owns and
discards them when the tree changes.

This is the load-bearing decision of the whole design, and everything below
depends on it:

- **Drag payload is a live object reference.** Moving a node between trees —
  including across windows — is an object-graph operation. There is no visual
  reparenting, no template or `DataContext` lifetime problem, and nothing to
  serialize mid-drag. Each `Dock` independently builds views for whatever
  nodes it now owns.
- **Control parentage is never a factor in docking.** A visual can have only
  one parent; a view model can be presented any number of times. Because the
  tree holds no visuals, the docking engine never has to reason about where a
  control currently lives.

### 3.2 `DockContent`

A leaf node. Holds one piece of consumer content in a `Content` property,
rendered through ordinary `DataTemplate` resolution. Carries the `Title` used
by its owning tab.

`DockContent` **is** the identity of a tab. It is not identified by the
content it wraps — see §3.5.

### 3.3 `DockSplitPane`

- `Orientation`: `Horizontal` | `Vertical`.
- **Exactly two** `IDockPane` children. This is an invariant, not a
  convention — the type must make three children unrepresentable.
- A resizable splitter between them, persisted as a proportional ratio (not
  an absolute pixel size) so layouts survive window resizing and restore.
- **A floor on pane size**, set by `Dock.MinPaneSize` and applying to every
  split in that `Dock`. The splitter clamps at the limit rather than
  continuing to move; a ratio can never reach zero, so a pane can never be
  dragged out of existence and left unrecoverable. Clamping applies equally
  to window resizing — if the `Dock` becomes too small to honour every
  minimum, panes overflow rather than collapse.

### 3.4 `DockTabPane`

An `IDockPane` containing N `IDockPane` children. Composed of:

- a **content display area** — a `Grid` showing the selected child;
- a **tab strip** — a custom `Panel` (see §4).

Tabs are individually closable via an `X` on the tab **and** via a tab
context menu.

### 3.5 Duplicated tabs

The same consumer content must be presentable in **two or more tabs at once**,
in the same `DockTabPane` or in different ones, in the same window or
different windows. Duplicating a tab is a supported operation, not an edge
case, and it must work without any special handling in the docking engine.

The model that makes this fall out for free:

- `DockContent` → content is **many-to-one**. N `DockContent` nodes may
  reference the same consumer view model instance.
- Each `DockContent` has its own `Id` and its own per-tab state (title,
  selection, position in the tree).
- The consumer view model is shared, so both tabs observe the same state and
  update together. This is the intended behaviour of a duplicated tab.

**Hard constraint that follows:** `DockContent.Content` must hold **data, not
a `Control`**. Assigning a control instance makes duplication impossible — the
second presenter would have to reparent the same visual — and it reintroduces
exactly the control-parentage coupling §3.1 exists to eliminate. The library
must therefore render content through `DataTemplate`s, and this restriction
must be stated in the public API, not left to fail silently at runtime.

Duplication is also what forces node `Id` and content key to be **separate
concepts** in serialization (§8): two nodes with distinct `Id`s can carry the
same content key.

### 3.6 Zero entanglement with the consuming application

`IDockPane` is implemented **only by library types**. A consuming
application never implements it, never inherits from a library base class,
and never references a library type from its **content** view models.

There is exactly one library type a consumer holds: the opaque layout object
bound to `Dock.Layout` (§9.2). It lives on the shell view model, never on a
document, and is never inspected — only stored and handed back. Layout is
inherently library state, so *something* must hold it; the requirement is that
it be one opaque handle rather than a contract imposed on content.

The boundary is exactly one property: `DockContent.Content` holds an `object`.
Everything on the consumer's side of that property is opaque to the library.

```
consumer VM  ──held by──▶  DockContent  ──implements──▶  IDockPane
(knows nothing)            (library type)                (library type)
```

`DockContent`, `DockSplitPane`, and `DockTabPane` are *layout* view models
owned by the library. In the collection-bound path (§9) the consumer does not
construct them at all — the `Dock` creates them around the consumer's items.

**Where entanglement would otherwise creep in, and the rule for each.** The
library needs three pieces of information about consumer content. None of
them may be obtained by requiring an interface:

| Need | Wrong (entangling) | Required approach |
|---|---|---|
| Tab title | `IDockTitle.Title` | `Title` binding on a per-type descriptor |
| Stable content key for save/load | `IDockSerializable.Key` | `ContentKey` binding on a per-type descriptor |
| Whether a tab may close | `IDockCloseable.CanClose` | `CanClose` binding on a per-type descriptor |
| Intercepting a close | `IClosing.OnClosing(cancel)` | `CloseCommand` on a per-type descriptor (§3.10) |
| Last view of an item closed | `IDisposable` on the item | `ClosedCommand` on a per-type descriptor (§3.10) |
| Context-menu contributions | `IHasMenu.MenuItems` | `MenuItems` binding on a per-type descriptor (§5.4) |

Do not additionally provide optional opt-in interfaces as a "convenience."
Two mechanisms for one need is the excess §1 forbids.

### 3.7 Item descriptors

Metadata is supplied by a collection of **`DockItemDescriptor`s** on the
`Dock`, resolved **by item type**.

```xml
<dock:Dock ItemsSource="{Binding Panels}" Layout="{Binding Layout, Mode=TwoWay}">
  <dock:Dock.ItemDescriptors>
    <dock:DockItemDescriptor DataType="vm:CodeDocument"
                             Title="{Binding FileName}"
                             ContentKey="{Binding FullPath}"
                             CanClose="{Binding IsClosable}" />
    <dock:DockItemDescriptor DataType="vm:TerminalPane"
                             Title="{Binding SessionName}"
                             ContentKey="{Binding SessionId}" />
  </dock:Dock.ItemDescriptors>
</dock:Dock>
```

#### Why per-type, not flat properties on the `Dock`

A single `TitleBinding` on the `Dock` would apply one member name to every
item, forcing the consumer to choose between a shared naming convention
across unrelated types (implicit, fails silently), a common interface
(entanglement, forbidden by §3.6), or a homogeneous collection only. A
docking application is the normal case for heterogeneity — tool panes and
documents are rarely the same type — so that limitation is not acceptable.

Keying by type removes the choice entirely: each type declares its own member
names, and nothing is shared.

#### Resolution rule

Mirror `DataTemplate` resolution, which the consumer already understands:

- Descriptors are matched in **declaration order; first match wins**.
- A descriptor matches when its `DataType` is assignable from the item's type.
- A descriptor with **no `DataType` matches anything** — it is the fallback,
  and a single such descriptor is the whole configuration for the homogeneous
  case.
- If Avalonia's existing template-matching machinery can be reused for this
  rather than reimplemented, do that. Do not hand-roll type matching that
  duplicates behaviour the framework already provides.

#### The properties are `IBinding`, not values

`Title`, `ContentKey`, and `CanClose` are typed **`IBinding`**. The XAML
`{Binding FileName}` is captured as an unevaluated binding *description*; the
`Dock` instantiates it once per item with **that item as the source**. It
means *"for each `CodeDocument`, bind to that document's `FileName`"* — it
does **not** resolve against the `Dock`'s own `DataContext`.

This is an established mechanism, not an invention. Precedents to follow:

- Avalonia `DataGridTextColumn.Binding` — an `IBinding` applied per row.
- WPF `GridViewColumn.DisplayMemberBinding` — identical concept.

Because each binding is live per item, changing a document's `FileName`
updates its tab title automatically. `DockContent.Title` is a **projection**
of the wrapped content, never independently authored state.

Grouping the bindings inside the descriptor also resolves an ambiguity that
flat properties would have created: on the `Dock` element itself, every
`{Binding …}` is an ordinary `DataContext`-relative binding, while every
`{Binding …}` inside a descriptor is per-item. The distinction is structural
rather than a naming convention.

#### Descriptors are mandatory: describe-and-forbid

**Content with no matching descriptor is never docked.** There is no lenient
mode and no toggle.

The lenient alternative — dock it anyway, without a content key — was
rejected because a keyless node cannot be persisted, so it must be pruned
either at save or at load, and pruning triggers normalization (§6) which
collapses its parent split. One undescribed item would silently rearrange
**fully-described panes around it**. That introduces a "temporary node"
concept the user has no way to reason about. Forbidding is what removes the
concept entirely.

The payoff is that **every node in every tree is guaranteed persistable**, so
§8's round-trip guarantee is unconditional rather than holding only over some
subset.

No separate lenient toggle is needed, because a **catch-all descriptor already
is one**: a descriptor with no `DataType` matches anything. An application that
wants to accept everything writes one line in the mechanism that already
exists, rather than flipping a parallel flag.

#### Unmatched content means two different things

The response differs by *where* the mismatch occurs, because the intent
differs:

| Situation | Almost certainly | Response |
|---|---|---|
| Item in `ItemsSource` with no descriptor | A forgotten descriptor or a `DataType` typo | **Loud.** Configuration error, surfaced as a diagnostic — never silently skipped |
| Drop target `Dock` with no descriptor | Deliberate (§3.8) | **Silent.** No guides shown, drop not offered, no diagnostic |

#### Descriptor sets as an acceptance filter

Because a `Dock` refuses content it cannot describe, **the descriptor set
defines what a `Dock` accepts**. This gives applications tool-window areas and
document areas — panes that may not mix — without the library containing any
notion of "tool window" or "document".

That distinction is a higher-level application concept, correctly downstream
of this substrate. The library provides only the mechanism.

Two consequences worth stating explicitly:

- A `Dock` declaring no descriptors accepts nothing. That is coherent but
  almost always a mistake; emit a diagnostic.
- Two `Dock`s may declare **different descriptors for the same type**, to
  present the same content differently in different regions. This is a
  supported application choice, not a misconfiguration.

**Rejected alternative: named drag groups.** An explicit grouping mechanism —
`Dock`s tagged with a group name, interchanging only within that group — was
considered for isolating docking surfaces from one another. It is redundant:
descriptor sets already determine what each `Dock` accepts, and adding a
second, parallel isolation mechanism would mean two places to configure one
behaviour. Do not reintroduce it.

#### Metadata resolves live, per owning `Dock`

Descriptors are resolved by whichever `Dock` currently owns the node — **not**
captured when the node is created.

Live resolution is what makes differing descriptors for the same type work: a
node moved from one `Dock` to another re-resolves against the destination's
descriptors and presents accordingly. Capturing at creation would freeze the
originating `Dock`'s presentation and defeat that.

Live resolution is safe because **content keys are `Dock`-scoped**. Each
`Dock` serializes its own layout, so a node leaving `Dock` A leaves A's layout
entirely and appears in B's under B's key. There is no shared key namespace
and nothing to erase. A key is only meaningful within the `Dock` holding it.

Nor can metadata be lost by moving: describe-and-forbid guarantees a node can
only ever land in a `Dock` that has a descriptor for it.

#### `ContentKey` is always required

A descriptor without a `ContentKey` binding is invalid, unconditionally —
including on a `Dock` that never persists its layout.

This costs one attribute in the non-persisting case and buys three things:
descriptor validity never depends on whether `Layout` happens to be bound;
persistence can be switched on later with no changes to existing descriptors;
and §8's round-trip guarantee stays unconditional. `Title` and `CanClose` may
be omitted — they degrade to `ToString()` and `true` respectively, which are
cosmetic — but a missing key is what would reintroduce unpersistable nodes,
and describe-and-forbid exists to make those impossible.

#### Descriptor values may be literals, not only bindings

All three properties accept a **constant** as well as a binding:

```xml
<dock:DockItemDescriptor DataType="vm:InspectorViewModel"
                         Title="Inspector"
                         ContentKey="Inspector"
                         CanClose="False" />
```

Implement this by keeping the properties typed `IBinding` and supplying a type
converter that wraps a literal in a constant binding. Do **not** type them
`object` and branch at runtime: an `object`-typed styled property causes XAML
to *evaluate* `{Binding …}` against the `Dock`'s own `DataContext` and assign
the result, which is precisely the per-item/ordinary-binding confusion §3.7
exists to prevent.

**What a constant `ContentKey` means.** Every item of that type resolves to
the same key, and keys must be unique per `Dock`. So a constant key declares
the type a **singleton within that `Dock`** — there is one Inspector, and a
second `InspectorViewModel` instance in the same `ItemsSource` is a duplicate
key and must be reported as a configuration error.

It does **not** prevent the Inspector appearing in more than one tab.
Duplication (§3.5) is N `DockContent` nodes → one item, and §8 already
requires nodes sharing a key to rehydrate to the same instance. A singleton
tool pane can still be duplicated into two panels; both views observe the one
instance, which is the intended behaviour.

This is the natural form for genuinely singular tool panes, and it is
straightforward for tests, which rarely have meaningful document identity.

**Uniqueness constraint.** Because load-time matching (§8) is per-`Dock` and
not per-type, content keys must be unique across **all** of a `Dock`'s items,
not merely within a single type. Two descriptors for different types must not
produce colliding keys, and a constant key must not collide with any value a
sibling descriptor's binding can produce.

### 3.8 Associating consumer view models with layout nodes

**The consuming application never stores, sees, or manages a pane `Id`.**

Node `Id`s are internal to the library and appear only inside the serialized
layout. The consumer's side of the association is a key *it already has* —
a file path, a document GUID, a record id — surfaced through the descriptor's
`ContentKey` binding. The library owns the mapping in both directions:

```
consumer's own key ──descriptor.ContentKey──▶ DockContent.ContentKey ──▶ layout JSON
                   ◀──── matched on load against items in ItemsSource
```

There are exactly three layers, and no fourth is needed:

| Layer | Owner | Cardinality |
|---|---|---|
| Document view model | Consumer | 1 per document |
| `DockContent` | Library | **N per document** (§3.5 duplication) |
| Layout tree | Library | 1 per `Dock` |

`DockContent` *is* the association layer. It exists precisely because the
mapping from document to tab is one-to-many and because layout position is
per-tab, not per-document.

**Consumers do not write a docking-specific `DataTemplate`.** They write
ordinary templates for their own view model types, exactly as they would
without this library:

```xml
<DataTemplate DataType="vm:CodeDocument">
    <TextEditor Text="{Binding Text}" />
</DataTemplate>
```

The `Dock` sets a presenter's content to the consumer's view model and lets
normal template resolution find that template. Titles and close-availability
are supplied by the per-item bindings, **not** pulled up out of a template by
the consumer. If the design ever requires the consumer to author a template
containing a library type in order to surface metadata, that is a design
failure — revisit it.

### 3.9 Placement: where new content docks

#### Placement is a seed, not a rule

Placement is consulted **only when an item has no node**. That happens in
exactly three situations:

- a fresh layout with nothing saved;
- an item added to the bound collection at runtime;
- an item present in `ItemsSource` but absent from a loaded layout — typically
  a pane type added in a newer version of the application.

Once a node exists, **the layout wins**. Without this rule a saved layout
would fight the descriptors on every load, and the third case above would be
unable to place genuinely-new content without disturbing everything else.

#### Groups are declared once, on the `Dock`

```xml
<!-- Layout regions: declared once. -->
<dock:Dock.Groups>
  <dock:DockGroup Name="Tools"  Seed="Right"  SeedSize="0.25" />
  <dock:DockGroup Name="Output" Seed="Bottom" SeedSize="0.3" />

  <!-- A region that outlives its contents (§6.1). -->
  <dock:DockGroup Name="Documents" Seed="Center" IsPersistent="True" />
</dock:Dock.Groups>

<!-- Items: reference a group by name, or omit for Active. -->
<dock:Dock.ItemDescriptors>
  <dock:DockItemDescriptor DataType="vm:CodeDocument"
                           Title="{Binding FileName}"
                           ContentKey="{Binding FullPath}" />
  <dock:DockItemDescriptor DataType="vm:InspectorViewModel"
                           Title="Inspector" ContentKey="Inspector"
                           Group="Tools" />
  <dock:DockItemDescriptor DataType="vm:OutlineViewModel"
                           Title="Outline" ContentKey="Outline"
                           Group="Tools" />
</dock:Dock.ItemDescriptors>
```

`Seed` must **not** be declared on descriptors. Declaring it once per group
keeps the descriptor about the *item* and the group about the *region*, and
makes contradictory seeds for one group unrepresentable rather than merely
discouraged.

#### The two placement modes

| Descriptor | Mode | Behaviour |
|---|---|---|
| No `Group` | **Active** | Opens in the active pane. The document case; needs no configuration. |
| `Group="X"` | **Grouped** | Joins group X's pane. If that pane does not exist, create it using X's seed. |

`Group` is a **durable identity**, carried by the `DockTabPane` and persisted
with it. Once the user drags a group's pane elsewhere, later members join it
at its new location — the seed is never reconsulted.

`IsPersistent` is carried the same way and answers what happens when the
group's pane is emptied rather than moved: see §6.1.

#### Seeding is a docking operation, not a separate mechanism

`Seed` takes the §6 guide vocabulary and applies it **against the `Dock`
root**:

- `Left` / `Top` / `Right` / `Bottom` — split the root, placing the new group's
  pane on that side at `SeedSize` (a proportion).
- `Center` — tab into the root's existing pane, so the group is born sharing
  the document area rather than in a region of its own. It is still a group;
  later members join that pane wherever it ends up.

Seeding relative to the **root**, not to the active pane, is what makes it
predictable: a `Bottom`-seeded group spans the full width regardless of what
happened to be focused.

Implement seeding by invoking the same mutation engine the drag session uses
(§13), with a direction supplied by configuration instead of by a cursor. It
must not be a second placement implementation.

A pane may hold both grouped and ungrouped content. The group label states
where members of that group go; it is not a claim of exclusivity.

#### `Active` must be guarded

Naive "active pane" is wrong: focus the Inspector, open a file, and the
document lands in the tool pane.

The rule is therefore that an ungrouped item opens in **the last active pane
holding ungrouped content**. Grouped items ignore active entirely. If no such
pane exists, create the root pane.

This is also why `Active` cannot simply collapse into an implicit "documents"
group: it must follow focus so that a user who has split the document area in
two gets new files in the half they are looking at.

#### Known limitation: group position amnesia

A group's identity lives on its pane. Close every tab in the group and
normalization (§6) removes that pane, taking the group's position with it —
so reopening a tool returns it to its seed rather than to where the user had
moved it.

Accept this. Fixing it requires persisting a group→last-position map in the
layout, which is meaningful extra state for a modest gain. Record it as a
known behaviour rather than discovering it as a bug.

#### Removal and duplication

- **Item removed from the collection** — every `DockContent` referencing it is
  removed and normalization runs. This follows directly from duplication being
  N nodes → one item (§3.5).
- **Duplication does not consult placement.** Duplicating a tab is an explicit
  user action with an explicit target; the new node is created there. Placement
  applies only to items with no node at all.

### 3.10 Signalling that an item's last view closed

Closing one of N tabs sharing an item removes that `DockContent` only (§3.5).
The consumer therefore cannot distinguish *"a view closed"* from *"the
document closed"* — which it must, in order to dispose or persist.

The descriptor supplies a **`ClosedCommand`**, invoked once, with the item as
parameter, when the **last** `DockContent` referencing it is removed:

```xml
<dock:DockItemDescriptor DataType="vm:CodeDocument"
                         Title="{Binding FileName}"
                         ContentKey="{Binding FullPath}"
                         ClosedCommand="{Binding CloseCommand}" />
```

- It is per-item, resolved like every other descriptor value (§3.7), so the
  command comes from the item's own view model.
- It fires on genuine last-reference removal — not on close of a duplicate,
  and not when a pane is auto-hidden (§5.3) or floated, neither of which
  removes the node.
- It is a notification, **not** a veto — see below.
- It is optional. Omitting it means the library does nothing on close, which
  is the correct default when items outlive their views.

This keeps §3.6 intact: no `IDisposable` or `IClosable` contract is imposed
on the item, and an item that wants no notification declares nothing.

#### Vetoing a close

`CanClose` (§3.7) is a static predicate and cannot express "prompt to save,
possibly cancel." The descriptor therefore also supplies an optional
**`CloseCommand`**:

- If supplied, the `Dock` **invokes it instead of closing** and does nothing
  further. The tab remains open.
- The consumer decides — prompting, saving, whatever — and closes by removing
  the item from the bound collection, or simply declines and leaves it.
- If omitted, the close button closes the tab directly.

The veto therefore lives entirely on the consumer's side; the library never
waits on a cancellable event and needs no callback interface, keeping §3.6
intact.

`CloseCommand` and `ClosedCommand` are distinct and may both be present:
`CloseCommand` intercepts *before* anything happens, `ClosedCommand` notifies
*after* the last view of an item is gone.

#### Closing a pane is closing its contents

Closing a **pane** — its close button, its menu entry, the keyboard — is
defined as each of its contents closing, one at a time, through everything
above: `CanClose`, then `CloseCommand`, then removal. Closing a pane and
closing its tabs one by one are therefore the same act with the same result.

Taking the subtree out wholesale is not an alternative implementation of it.
That leaves every item still in the bound collection with no node, and an
item in that state is one the application believes is open and the user cannot
reach: opening it again finds it already open, so nothing new is created, and
it has no view to bring forward. Nothing recovers it short of the consumer
noticing on its own.

A content that declines survives, and so does the pane holding it. A pane left
with nothing is removed — including a persistent one (§6.1), because this
close was asked for explicitly.

### 3.11 Activation and selection

These are **three distinct layers**, frequently conflated. Keeping them
separate is what makes focus behave predictably.

| Layer | Lives on | Cardinality | Serialized |
|---|---|---|---|
| **Selection** — which child a tab group displays | `DockTabPane` | One per tab pane, many concurrently | Yes |
| **Activation** — which pane is logically focused | `Dock` | Exactly one per `Dock` | Yes |
| **Keyboard focus** — the platform's real focus | `TopLevel` | One per top-level | No — owned by the platform |

#### Selection does not drive focus; activation does

`DockTabPane.SelectedChild` is pure view state. Changing it programmatically
switches which child is displayed and **must not move keyboard focus** — a
view model that selects a tab has not asked to steal the caret.

The relationship is deliberately asymmetric:

- **Activating implies selecting.** Activating a node selects it in every
  ancestor `DockTabPane`, so an active pane is always visible.
- **Selecting does not imply activating.** A programmatic selection change
  leaves activation untouched.

A user *clicking* a tab does both, because clicking is a focus gesture. That
is the gesture doing two things, not selection driving activation.

#### Activation is logical focus

`Dock.ActivePane` is **logical focus** in the WPF sense: it survives the
`Dock` losing keyboard focus, and is restored when the `Dock` regains it.

Setting activation is therefore **gated**: if the `Dock` currently holds
keyboard focus, activation moves it; if not, activation is recorded and
applied when focus returns. This gating is the whole point — without it,
activating a pane in a background window would yank focus across windows.

Activation spans every surface of a `Dock`: the main tree, and every
`FloatPane` (§5.2). Focusing a floating pane makes it the active pane, so
Active placement (§3.9) then targets the float.

#### Do not delegate this to Avalonia's focus scopes

Avalonia does have focus scopes, and does restore a scope's remembered element
when focus returns. But the public surface is thin and has moved between
versions — there is no WPF-style `FocusManager.FocusedElement` attached
property, and scope access has regressed at points.

So **the library owns activation state itself**, and uses the platform focus
API only to move keyboard focus. Do not depend on framework focus-scope
restoration to store which pane was active; verify what Avalonia 12 exposes
(§2) and treat whatever it offers as an optimisation, not the source of truth.

#### The public property is `ActiveContent`, not `ActivePane`

Exposing `ActivePane` for binding would put an `IDockPane` — a library type —
into the consumer's view model, breaking §3.6.

The bindable, consumer-facing property is therefore **`Dock.ActiveContent`**,
holding the consumer's own object. `ActivePane` remains an internal model
concept.

**These are ordinary two-way styled properties, not the per-item `IBinding`
mechanism of §3.7.** That mechanism exists to project a value out of *each
item*; activation and selection are single values on a control. Typing them
`IBinding` would be a category error.

#### A single current value is not sufficient

§3.9's Active placement needs *the last active pane holding ungrouped
content* — not simply the currently active pane. Focus the Inspector, open a
file, and a lone `ActivePane` gives the wrong answer.

Activation must therefore be tracked as an **activation-ordered list of
panes**, so placement can query for the most recent pane satisfying a
predicate. Persist `ActivePane` with the layout and seed the list from it on
load; the rest of the ordering is runtime-only.

---

## 4. The tab strip panel — custom layout

This is a bespoke measure/arrange `Panel`, not a wrapped `TabControl` header.

Requirements:

1. **Box tabs, not minimal tabs.** Tabs are rectangles that *grow to fill*
   the available strip width. They are not sized to their content.
2. **Multi-line wrapping driven by content fit.** When the tabs on a line
   cannot display their full content without truncation, add another line.
   Lines are added until every tab can render its content in full.
3. **Even distribution.** Tabs are divided across the available lines as
   evenly as possible (e.g. 7 tabs on 3 lines → 3/2/2, not 5/1/1).
4. **No occlusion of content.** Growing the strip reduces the content area;
   the content area must never be overdrawn by the strip.

A tab's **required width** is the width at which all of its content fits: the
label, the close button, and any icon. The close button and icon are not
overlaid on the label and are never the first thing to be sacrificed.

**`MaxWidth` bounds the required width, and is what makes the algorithm
terminate.** A tab needs `min(requiredWidth, MaxWidth)`; beyond `MaxWidth` the
label truncates rather than causing another line to be added. Without this
cap a single long title would demand unbounded lines and consume the content
area. Line count is additionally bounded by the tab count — one tab per line
is the maximum useful subdivision, after which tabs truncate at whatever width
the strip allows.

Specify and implement the measure/arrange algorithm explicitly. State the
line-count selection rule (the minimum line count at which every tab reaches
its bounded required width) and the tie-breaking rule for uneven distribution.

---

## 5. Panes

### 5.1 The pane titlebar

A **Pane** is a dockable unit that owns a titlebar. `DockTabPane` is a Pane.

The titlebar contains, left to right:

- a **menu button** (pane-level actions);
- a **text element** (the active child's title);
- **window controls**: minimize, maximize, close — acting on the *Pane*, not
  the OS window when docked. When the Pane is the child of a `FloatPane`
  (§5.2) these map to real window operations. Define both behaviours.

Panes support:

- **Drag** by the titlebar.
- **Float** — detach into a `FloatPane`, preserving the pane's internal tree.
- **Raft** — re-dock a floated subtree back into the main layout tree.

Maximize and minimize on a *docked* pane are defined in §5.3 — neither is an
OS-window operation in that state.

### 5.2 `FloatPane`

A `FloatPane` is an `IDockPane` that hosts a `TopLevel` **owned by a `Dock`**.
It is the model of a floating window.

- **One child**, an arbitrary `IDockPane` subtree — a single `DockContent`,
  or a whole split/tab arrangement preserved from where it was torn off.
- **Window geometry** — position, size, and window state, persisted (§8).
- **Root only.** A `FloatPane` never appears as a child of a `DockSplitPane`,
  a `DockTabPane`, or another `FloatPane`. Like `DockSplitPane`'s exactly-two
  invariant, the type must make the illegal arrangement unrepresentable.

A `Dock` therefore owns a **main root** `IDockPane` plus a collection of
`FloatPane`s. Both are part of the same `Dock`, and one layout document
covers them.

#### A `FloatPane` is not a second `Dock`

This is the load-bearing distinction, and it removes a hazard the earlier
design carried. A `FloatPane` renders a subtree of its owner's model; it does
not own a model of its own. Consequently:

- **Descriptors are inherited automatically.** A floated pane is still inside
  the same `Dock`, so it resolves against the same descriptor set. Floating
  can never strand content in a surface that cannot describe it.
- **The acceptance filter (§3.7) extends to floats for free.** A tool-only
  `Dock` stays tool-only when its panes are floated.
- **One layout document.** Docked and floating state serialize together,
  rather than needing to be correlated across separate `Dock`s.

#### Platform realization

The model says `FloatPane`; the *view* decides what a `TopLevel` is on a given
platform. Per §3.1 the tree holds no visuals, so this choice is confined to the
view layer and never reaches the model or the serialized layout. That is what
keeps floating platform-agnostic.

**Avalonia does not absorb this difference for you.** It provides the
abstraction — `TopLevel`, and `TopLevel.GetTopLevel(control)` returning either
a `Window` or a root view — but `Window` exists only on desktop. Mobile and
browser targets have no `Window` concept and run under
`ISingleViewApplicationLifetime` with a single root view; a second window
silently fails under WASM, where the documented guidance is to use overlays
instead. The branch is the library's to write.

Realize `FloatPane` by application lifetime:

| Lifetime | Realization |
|---|---|
| `IClassicDesktopStyleApplicationLifetime` | A real `Window`, owned by the `Dock`'s own `TopLevel` |
| `ISingleViewApplicationLifetime` | An overlay in the root `TopLevel`'s `OverlayLayer` |

Both must satisfy §5.2's ownership semantics — lifetime, z-order, emptiness,
drop-target behaviour. Only the host differs.

Confine the branch to **one** place in the view layer. It must not leak into
the model, the mutation engine, the drag session, or serialization; a layout
saved on desktop must load unchanged in the browser.

Consequence for §7: cross-`TopLevel` drag is a desktop capability. Under a
single-view lifetime every surface shares one `TopLevel`, so drag degrades to
in-application behaviour exactly as §7.3 requires — no separate code path,
simply fewer registered surfaces.

### 5.3 Maximize and minimize on a docked pane

#### Maximize

A maximized pane **temporarily covers the entire `Dock`**. Its siblings are
hidden, not removed: the tree is unchanged and restoring reveals it exactly as
it was.

- At most one pane per `Dock` is maximized at a time.
- Maximize state is a property of the `Dock`, not a tree mutation. Nothing in
  the layout structure changes, so no normalization runs.
- A `FloatPane`'s contents maximize within that float, not over the owner.

#### Minimize is auto-hide

Minimizing does **not** collapse a pane in place. The pane is removed from the
layout tree and parked as a button on a strip along the nearest `Dock` edge.
Activating the button slides the pane out over the content temporarily;
re-pinning it returns it to the tree.

This adds one model concept: a `Dock` owns, alongside its root and its
`FloatPane`s, a set of **auto-hidden entries**. Each records the pane and
enough information to put it back.

| Aspect | Requirement |
|---|---|
| Edge selection | Nearest `Dock` edge to the pane's position at the moment it was minimized |
| Restore target | **Must be persisted.** Unlike group position (§3.9), restoring to the original location is the entire point of auto-hide, so amnesia is not acceptable here |
| Flyout | Slides over content as an overlay; does not resize the layout or displace other panes |
| Dismissal | Loses focus, or is re-pinned back into the tree |
| Serialization | Auto-hidden entries and their restore targets persist with the layout (§8) |
| Floating panes | Minimize on a `FloatPane` is a real window minimize (§5.1), never auto-hide |

**The restore-target problem is the hard part.** A tree position cannot be
stored as a path, because unrelated docking operations invalidate paths while
a pane sits auto-hidden. Store it as a **relative anchor** — a sibling node's
`Id` plus a direction — and fall back to the pane's placement seed (§3.9) if
that anchor no longer exists. Specify this before implementing; it is the
component most likely to be got subtly wrong.

Auto-hide strips are chrome belonging to the `Dock`. An edge with no entries
shows no strip and consumes no space.

Two behaviours are `Dock`-level properties rather than fixed choices, since
both are legitimate product preferences:

- **`FlyoutTrigger`** — `Hover` or `Click`.
- **`AutoHideButtons`** — `PerPane` (one button per auto-hidden pane) or
  `PerTab` (one button per tab within it).

### 5.4 Menus

Two menus exist, at different scopes:

- The **pane menu**, opened from the titlebar menu button (§5.1), acting on
  the pane.
- The **tab context menu** (§3.4), acting on one tab.

#### Built-in items

The library supplies the standard docking operations itself, so a consumer
that configures nothing still gets a working menu:

| Menu | Built-in items |
|---|---|
| Pane | Float, Auto-hide, Maximize / Restore, Close pane |
| Tab | Close, Close others, Close all, Float, Duplicate |

Items that cannot apply in context — Close on a tab whose `CanClose` is false,
Auto-hide on a `FloatPane` — are hidden rather than shown disabled.

#### Consumer contributions

The descriptor supplies a **`MenuItems`** binding (§3.7), projecting a
collection from the item's own view model. Those items appear on the **tab
context menu**, which is the per-item scope; the pane menu is pane-scoped and
takes built-ins only.

Ordering is fixed so behaviour stays predictable: **consumer items first, a
separator, then built-ins.**

Contributed items are rendered through ordinary `DataTemplate` resolution,
exactly as content is (§3.8) — so they are the consumer's own command objects,
not library types and not `MenuItem` controls. Passing controls would
reintroduce the parentage coupling §3.1 exists to eliminate, and would break
when the same item appears in duplicated tabs (§3.5).

#### Ownership semantics

- **Lifetime** — closing the `Dock`'s own `TopLevel` closes its `FloatPane`s.
  A `FloatPane` never outlives its owner.
- **Z-order** — a `FloatPane` stays above its owner.
- **Emptiness** — a `FloatPane` whose child is removed is closed and dropped
  from the collection, by the same normalization rules as §6.
- **Drop target** — a `FloatPane`'s subtree is a normal drop target with full
  guides. Content can be docked into a floating window and torn back out.
- **No nesting** — floating a pane out of a `FloatPane` produces a *sibling*
  `FloatPane`, never a nested one.

---

## 6. Drag-and-drop docking

While a drag is in progress, display **docking guides** at two scopes
simultaneously:

**Pane guides**, over the pane under the cursor:

- **Left / Top / Right / Bottom** — split that pane, producing a
  `DockSplitPane` whose orientation is implied by the direction, with the
  dragged node placed on the indicated side.
- **Center** — merge into that pane as a tab, producing or extending a
  `DockTabPane`.

**Outer guides**, at the edges of the `Dock` itself:

- **Left / Top / Right / Bottom** — split the `Dock` **root**, placing the
  dragged node along the full extent of that edge, spanning every existing
  pane rather than subdividing the hovered one.
- There is no outer Center; tabbing is inherently a pane operation.

Outer guides are the same mutation applied to the root instead of to a leaf —
identical to how placement seeding works (§3.9). Do not implement them
separately.

Both scopes are offered at once.

**Guides must never overlap.** Rather than resolving ambiguity with a
hit-priority rule, resolve it geometrically so ambiguity cannot arise:

- **Pane guides** are a compact cluster at the **centre** of the hovered pane.
- **Outer guides** sit against the **extreme edges** of the `Dock`.

These occupy disjoint regions by construction, so every point belongs to at
most one guide and no precedence rule is needed.

Three rules keep them disjoint without ever hiding a usable option.

**1. Guide clusters are not clipped to their pane.** A pane cluster is
*centred* on the hovered pane but may overflow its bounds. A narrow tool pane
in a normal-sized window therefore still gets a full-size cluster, which
removes the common cramped case outright — the constraint is the `Dock`'s
size, not the pane's.

**2. Guides scale with available space.** Where the `Dock` genuinely is small,
both sets shrink together rather than colliding. Scaling has a floor: a guide
must stay a reliable pointer target, so it never shrinks below a documented
minimum hit size. That floor is an accessibility constraint (§11), not a
cosmetic one.

**3. A guide is shown only when its operation is permitted.** This is the
general rule, and it dissolves the degenerate case rather than special-casing
it. A split guide is offered only if both resulting panes would satisfy
`MinPaneSize` (§3.3).

So in a `Dock` too small to hold both guide sets at minimum size, the split
guides are already illegal — the split they advertise would be refused — and
the centre guide remains, since tabbing has no size implication. Nothing
usable is ever hidden, and no guide is ever offered for an operation that
would then be rejected.

Rule 3 applies everywhere, not merely when space is tight: never draw a guide
for a drop that will not be accepted.

### 6.1 Tab reordering

Dragging a tab **reorders it within its strip**. This is always available and
is not configurable; preventing reorder is a user-experience antipattern.

Reorder and tear-out begin with the same gesture, so they are disambiguated by
position:

- Pointer **inside the tab strip** — reorder. Tabs shift to show the insertion
  position; no docking guides are shown.
- Pointer **leaves the strip** — the gesture becomes a normal drag (§7), with
  guides and all docking behaviour.
- Returning to the strip reverts to reordering.

Reordering is a mutation of `DockTabPane` child order and persists (§8). It
runs through the same mutation engine as every other layout change (§13).

Additional requirement: it must be possible to **break a `DockContent` into a
`DockSplitPane` or a `DockTabPane`** — i.e. a leaf is promoted to a composite
node in place, with the original leaf becoming a child of the new node.

Also specify:

- a **drop preview** (highlight of the region the drop will occupy);
- **tree normalization** after every mutation: a `DockSplitPane` that loses a
  child collapses into its surviving child; a `DockTabPane` reduced to zero
  children is removed; a `FloatPane` that loses its child is closed and
  dropped from its owner's collection (§5.2); empty ancestors are pruned
  recursively.

#### Persistent panes

A group may declare `IsPersistent`, which its pane carries and persists (§8)
the way `Group` itself does. Such a pane is **not** removed when its last
child leaves: it stays where the user put it, empty, and later members of the
group return to it.

This exists because normalization answers the wrong question for a region the
user arranged. Collapsing an emptied pane is right when the pane was only ever
a container for what happened to be in it. It is wrong for a document area:
closing every document there is not a request to give up the layout, and
re-opening one would otherwise re-seed a pane somewhere the user did not
choose.

Persistence is about *emptying*, not permanence. Removing the pane itself —
an explicit close (§3.10), a drag, a float — behaves exactly as it does for
any other pane. A persistent pane merged into another carries the flag with
its tabs, as `Group` already does: the region is where its tabs went.

A pane with no selection presents no content. That is what an empty persistent
pane is, and a pane must never fall back to presenting itself.

---

## 7. Cross-window and cross-`Dock` drag

A pane dragged out of one `Dock` must be droppable into **any other `Dock`
instance in the application**, whether that instance lives in the same window,
in another top-level window, or in a `FloatPane` surface (§5.2). All of §6 —
guides, preview, split/merge, normalization — applies unchanged at the
destination.

**Describe-and-forbid gates every drop.** A `Dock` offers guides only for
content it has a descriptor for (§3.7). A drag over a `Dock` that cannot
describe the payload shows nothing and cannot be dropped — silently, since
that is the intended tool-area/document-area separation rather than an error.
Note that a `FloatPane` surface is part of its owning `Dock`, so it applies
that `Dock`'s filter, not one of its own.

The drag payload is a **live `IDockPane` reference** — the node itself, with
its subtree and its content intact. Nothing is serialized, cloned, or
reconstructed during a drag. Per §3.1 the node is a view model, so this is
just passing an object reference between two trees.

### 7.1 Do not build this on OS drag-and-drop

The drag mechanism must be a **library-owned pointer drag**, not the
platform's native drag-and-drop.

The reason is *not* that native DnD cannot carry an object reference — an
in-process `DataObject` can hold an arbitrary .NET object and return the same
instance to the drop handler. The reason is that native DnD's semantics,
drag-feedback rendering, and availability differ per backend and are absent or
degraded on some targets. Depending on it directly contradicts the
platform-agnostic requirement.

Instead: capture the pointer at drag start, track it in **screen
coordinates**, and drive everything else from that single coordinate stream.
This is the only part of the design that must work identically everywhere, so
it is the part that must depend on the least platform surface.

### 7.2 Mechanism

1. **Drag start.** A titlebar or tab press past a threshold begins a drag
   session. **The node is not detached.** It stays in its tree, untouched,
   for the whole drag; detachment and re-insertion happen together at drop.

   This is deliberate. Deferring detachment means cancellation is free — there
   is no original position to restore, because nothing moved — and the tree is
   never in a transient state that could be serialized (§10.1). Detaching at
   drag start would require both an undo path and a write-suppression rule.
2. **Drag visual.** A single visual follows the cursor, hosted the same way a
   `FloatPane` is realized on the platform (§5.2). It must not itself be a
   hit-test target.
3. **Target resolution.** Every live `Dock` registers itself in a
   process-wide registry on attach and deregisters on detach. On each pointer
   move, convert the screen point into each registered surface's coordinate
   space — a `Dock`'s own `TopLevel` and each of its `FloatPane`s — and
   hit-test for the innermost Pane under the cursor. Z-order across windows
   resolves ties: the topmost window wins.
4. **Acceptance check.** Resolve the target `Dock`'s descriptors against the
   dragged content. No match, no target — continue as if the cursor were over
   nothing.
5. **Guides and preview.** Rendered by the resolved target `Dock`, in its own
   overlay. Exactly one `Dock` shows guides at a time.
6. **Drop.** The node is detached from its origin and inserted into the target
   tree as one operation, by the same mutation engine used for same-`Dock`
   docking (§13). Normalization runs on the origin afterwards. No separate
   cross-window code path.
7. **Drop on nothing.** Releasing over no accepting target moves the node into
   a new `FloatPane` on its **origin `Dock`** at the cursor position — never
   an arbitrary `Dock`, since only the origin is known to describe it.
8. **Cancel.** Escape, or loss of the pointer capture, ends the session with no
   mutation at all. Because nothing was detached (step 1), there is nothing to
   undo.

### 7.3 Constraints

- The registry is the only global state the library owns. It is
  in-process, and its lifetime is tied to control attach/detach — a closed
  window must leave no entry behind.
- Screen-coordinate conversion is the sole platform dependency. Isolate it
  behind one narrow abstraction so a backend that lacks multi-window support
  degrades to single-`TopLevel` behaviour rather than failing.
- Cross-**process** drag is explicitly out of scope. It would require native
  drag-and-drop, which §7.1 rejects, and would force content identity through
  the serializer on every drop.

### 7.4 Source-less drag start

Expose an API that begins a drag session from **content plus a screen point**,
with no originating pane and no originating view.

This is what lets an application implement external drops itself — accept a
platform drop, project the payload into one of its own view models, and hand
that to the `Dock` — without the library taking on native drag-and-drop or
cross-process concerns. The library supplies the docking half; the
application supplies whatever produced the data.

Rules that differ from a normal drag:

- The content must still satisfy describe-and-forbid (§3.7). A `Dock` with no
  matching descriptor refuses it exactly as it refuses a dragged pane.
- There is no origin to return to, so **drop-on-nothing cancels** — the
  content simply never docks, rather than floating as §7.2 step 7 specifies.
- Nothing is detached at drag start, since no node existed.

This is an opt-in API for applications that want it. It is not part of the
zero-setup path (§1) and nothing in the library requires it.

---

## 8. Serialization

- The in-memory form is a **layout object** — the dock tree — bound two-way to
  `Dock.Layout` (§9.2). JSON is a serialization of that object, produced only
  when the consumer asks for it, never implicitly on mutation.
- Format: **JSON**, via `System.Text.Json`.
- **One document per `Dock`, covering every surface.** The main root tree,
  every `FloatPane` (§5.2), and every auto-hidden entry (§5.3) serialize into
  the same layout.
- The full layout tree round-trips: node types, hierarchy, split ratios, tab
  order, per-tab-pane selection and the active pane (§3.11), group names and
  persistence (§3.9, §6.1), floating geometry and window state, maximized
  pane, and auto-hide entries **with their restore anchors**.
- An **empty persistent pane** round-trips as itself. It holds no content key
  to be found by, so the flag on the pane is the only thing that says the
  region should still be there after a restart.
- Content identity is serialized as a **stable string key**, never as a
  serialized view or view model. The key comes from the resolved descriptor's
  `ContentKey` binding (§3.7), so the consumer's own identifier is used as-is
  and heterogeneous collections key per type.
- **Rehydration requires no consumer-supplied resolver.** On load, the `Dock`
  matches each serialized content key against the items already in its bound
  collection, using the same `ContentKey` binding. The collection is the set of
  live documents; the layout file only records where they sit. A key with no
  matching item yields an empty or dropped node — define which, and never
  fabricate content.
- **Every node has a key by construction.** Describe-and-forbid (§3.7) makes
  this structural: content with no descriptor never docks. A type with no
  natural per-instance identifier is still dockable via a **constant**
  `ContentKey` (§3.7), which declares it a singleton within the `Dock` — the
  normal case for tool panes. Only types needing *multiple simultaneous
  instances* require a per-instance identifier; a type with neither cannot be
  docked, and the API must say so plainly. Do **not** invent a surrogate id
  and push storage of it back onto the consumer; that is exactly the pane-id
  entanglement §3.8 forbids.
- **Node `Id` and content key are distinct.** Because tabs can be duplicated
  (§3.5), the mapping from node to content key is many-to-one. Two nodes
  carrying the same key must rehydrate to the *same* content instance, so
  duplicated tabs stay genuinely duplicated across a save/load cycle rather
  than becoming two independent copies.
- **Round-trip fidelity is a hard, unconditional requirement**:
  deserialize(serialize(x)) must produce a structurally identical tree. There
  is no "persistable subset" — describe-and-forbid guarantees every node in
  every tree has a key, so nothing is ever pruned at save or at load and the
  restored layout never differs from the saved one.
- Versioning: include a schema version field and state the forward/backward
  compatibility policy.

---

## 9. Consumer API

The `Dock` control is the single entry point.

- It is bindable to a collection (`ItemsSource`-style) with an
  `ItemTemplate`, so documents can be driven from a view model. This is the
  primary path: it satisfies §3.5 fully and is the only path that keeps the
  consumer entirely free of library types (§3.6).
- Alongside `ItemsSource` it exposes an `ItemDescriptors` collection (§3.7)
  supplying title, content key, and close-availability per item type.
- It accepts children declaratively in XAML (§9.1).
- Layout save/load is reachable without code-behind.

**An empty `Dock` renders `EmptyContent` if supplied, otherwise nothing.** The
property is optional and defaults to unset, so the zero-configuration case is
a blank region and an application that wants a "drag a document here" prompt
sets one. It is ordinary content, resolved through `DataTemplate`s like
everything else, and it is not a drop target in its own right — outer guides
(§6) already cover docking into an empty `Dock`.

**A `Dock` may not be nested inside another `Dock`'s content.** Forbid it
explicitly and diagnose it, rather than leaving it to misbehave: the drag
registry (§7.2) resolves targets by hit-testing registered surfaces, so
overlapping `Dock`s would produce two valid targets for one point with no
principled winner. Nesting also has no use case the tree already lacks —
splits and tabs compose arbitrarily within one `Dock`.

A complete integration should look approximately like this — no code-behind,
no library types in the view model:

```xml
<!-- On Dock itself: ordinary bindings against this view's DataContext. -->
<dock:Dock ItemsSource="{Binding Panels}" Layout="{Binding Layout, Mode=TwoWay}">

  <!-- Layout regions, declared once (§3.9). -->
  <dock:Dock.Groups>
    <dock:DockGroup Name="Tools" Seed="Right" SeedSize="0.25" />
  </dock:Dock.Groups>

  <!-- Inside a descriptor: per-item bindings against each item (§3.7). -->
  <dock:Dock.ItemDescriptors>
    <dock:DockItemDescriptor DataType="vm:CodeDocument"
                             Title="{Binding FileName}"
                             ContentKey="{Binding FullPath}"
                             CanClose="{Binding IsClosable}" />
    <dock:DockItemDescriptor DataType="vm:TerminalPane"
                             Title="{Binding SessionName}"
                             ContentKey="{Binding SessionId}" />
    <dock:DockItemDescriptor DataType="vm:InspectorViewModel"
                             Title="Inspector"
                             ContentKey="Inspector"
                             CanClose="False"
                             Group="Tools" />
  </dock:Dock.ItemDescriptors>
</dock:Dock>
```

`Panels` is heterogeneous — `CodeDocument`, `TerminalPane`, and
`InspectorViewModel` share no base type, no interface, and no member names.
None references the library. `Inspector` uses constant values (§3.7): one
Inspector per `Dock`, never closable, always docked with the tools.

The consumer additionally supplies ordinary `DataTemplate`s for their own
types (§3.8). They write no docking-specific template and store no pane ids.

### 9.1 Declarative items

For statically-authored panes, `Dock.Items` accepts `DockItem` elements
holding ordinary controls:

```xml
<dock:Dock ItemsSource="{Binding Panels}" Layout="{Binding Layout, Mode=TwoWay}">
  <dock:Dock.Groups>
    <dock:DockGroup Name="Tools" Seed="Right" SeedSize="0.25" />
  </dock:Dock.Groups>
  <dock:Dock.Items>
    <dock:DockItem Title="Palette" ContentKey="Palette" Group="Tools">
      <Grid> <!-- any standard content --> </Grid>
    </dock:DockItem>
  </dock:Dock.Items>
</dock:Dock>
```

`Dock.Items` and `ItemsSource` coexist: authored panes and bound documents
occupy the same layout tree and serialize into the same document.

**`ContentKey` is required here too**, and `Title` alongside it. Describe-and-
forbid (§3.7) is not relaxed for authored content — an item that cannot be
persisted would reintroduce exactly the unpersistable nodes §3.7 exists to
prevent. Both are naturally literals in this path.

**`Seed` does not belong on `DockItem`.** A `DockItem` references a group by
name and inherits that group's seed, for the same reason descriptors do
(§3.9): declaring a seed per item makes contradictory seeds for one group
expressible. An ungrouped `DockItem` uses Active placement.

#### Authored content is captured as a template, not as an instance

The child of a `DockItem` must be captured as **deferred template content**
and instantiated per presentation — not stored as a single live control.

This is not a detail. Storing the instance would put a visual in the layout
tree, contradicting §3.1, and would make cross-`Dock` and cross-window moves
require visual reparenting — the precise coupling §3.1 exists to eliminate.
Capturing a template keeps the model visual-free and leaves every mutation an
object-graph operation.

The consumer's XAML is unchanged either way; only the semantics differ.

**Consequence for duplication.** A duplicated `DockItem` builds a *second,
independent* instance of its content, because there is no shared view model
behind it — two copies of the authored `Grid`, with independent scroll
positions and control state. This differs from `ItemsSource` content, where
duplicated tabs share one view model and observe the same state (§3.5).

State that difference plainly at the API surface. It is inherent to authoring
content rather than binding it, not a defect, but it will surprise anyone who
assumes the two paths behave identically.

### 9.2 The `Layout` property

`Dock.Layout` is **two-way** and carries a **layout object** — the dock tree
itself — not a JSON string. JSON is produced only on demand.

```
Dock.Layout  ⟷  shell view model        two-way, live object
     │
     └── serialized to JSON on demand   explicit, never automatic
```

#### The write rule

The `Dock` writes back on every change to persisted state. The axis is
*discrete vs continuous*:

| Kind | Mutations | Rule |
|---|---|---|
| Discrete | Drop, close, reorder, item added/removed, float, raft, maximize, auto-hide, re-pin, selection, activation | Write immediately |
| Continuous | Splitter drag (ratio), `FloatPane` move/resize (geometry) | Write on gesture completion only |

Continuous gestures change state per frame; writing per frame would churn the
binding dozens of times a second for no benefit. Pane dragging is **not**
continuous — §7.2 defers detachment to the drop, so nothing is committed
mid-drag and the tree is never transient.

#### Serialization is explicit

Because `Layout` carries an object, serializing is a separate, deliberate act —
the consumer decides when to pay for it and when to write to disk. Automatic
JSON on every mutation is exactly the churn the object form avoids.

**Do not put this only on the `Dock` control.** A method reachable solely as
`Dock.SerializeLayout()` requires a view model to hold a reference to a
control, which is the MVVM violation §1 forbids and cannot be done without
code-behind. Serialization must therefore be reachable **from the layout
object the consumer already holds** — so a shell view model can persist what
is bound to it, with no reference to the `Dock`. A convenience method on
`Dock` may exist in addition, for code that legitimately has the control.

The inverse is symmetric: a layout object is constructible from JSON and
assigned to `Layout`, requiring no `Dock` reference either.

Everything in §8 — schema version, key matching, round-trip fidelity —
applies to that conversion.

---

## 10. Open questions

**None outstanding.** Every question raised during specification has been
answered and folded into the sections above.

Retain this section as the place to record future unknowns rather than
resolving them inline, so that open work stays visible instead of dissolving
into prose.

---

## 11. Keyboard and accessibility

Full keyboard operation and assistive-technology support are **requirements,
not enhancements**. They must be designed in: retrofitting them into a bespoke
measure/arrange `Panel` (§4) and a custom drag session (§7) is substantially
harder than building them in.

#### Keyboard operation

| Scope | Requirement |
|---|---|
| Between panes | Directional traversal between panes; a cycle gesture across panes |
| Within a tab strip | Arrow keys move between tabs, including across wrapped lines (§4); Home/End to ends |
| Tab switching | A next/previous gesture within the active pane, and a most-recently-used order that uses the activation list (§3.11) |
| Closing | Keyboard-accessible close for the focused tab, honouring `CanClose` and `CloseCommand` (§3.10) |
| Menus | Both menus (§5.4) openable and navigable from the keyboard |
| Layout changes | Float, auto-hide, maximize, and re-dock reachable via the pane menu, so docking is operable without a pointer |
| Splitters | Focusable; arrow keys resize in steps, honouring `MinPaneSize` (§3.3) |

Drag-and-drop itself is pointer-driven, so the **menu path is what makes
docking keyboard-accessible**. It must therefore expose every operation the
drag engine does, not a subset.

#### Assistive technology

- Automation peers for every custom control — the `Dock`, panes, the tab
  strip, individual tabs, splitters, and auto-hide buttons.
- The tab strip reports as a tab list with correct selection state, despite
  being a bespoke `Panel` rather than a `TabControl`. Multi-line wrapping is a
  visual arrangement and must not fragment it into several reported groups.
- Names come from the same values shown visually — `Title` (§3.7) — so no
  parallel accessibility metadata is introduced.
- Live changes to the layout are announced; a pane opening, closing, floating,
  or being auto-hidden is not a silent event.
- Selection (§3.11) is reported without moving focus, matching the model's own
  distinction between the two.

#### Focus behaviour

Keyboard focus follows the activation rules of §3.11. Closing a pane must move
focus deterministically to the next pane in activation order rather than
dropping it, since lost focus is unrecoverable without a pointer.

---

## 12. Styling contract

The controls are **lookless**. Every visual is supplied by a `ControlTheme`
and is replaceable; none is hard-coded in a control's own rendering.

Consequences the implementation must honour:

- **No drawing in code** where a template can express it, and no measurement
  that assumes a particular template's structure. §4's tab strip is the sole
  place where custom measure/arrange is inherent to the requirement — and even
  there, the tab itself is templated; only the *arrangement* is code.
- **Named template parts are public API.** Document each control's expected
  parts and which are required. A control must degrade predictably — not
  crash — when an optional part is absent from a replacement template.
- **State is expressed as pseudo-classes**, not as visuals set from code:
  selected, active, dragging, drop-target, floating, auto-hidden, maximized,
  first/last-in-line. A restyler can then target every state without
  subclassing.
- **Theme resource keys are public API.** Name and document the brushes,
  thicknesses, and metrics the default theme consumes, so an application can
  restyle by overriding resources without replacing whole templates.
- **The default theme is one merged dictionary**, matching §1's zero-setup
  requirement.

Treat template part names, pseudo-class names, and resource keys with the same
stability commitment as method signatures: they are what consumers build
against, and changing them after release breaks applications silently.

---

## 13. Architectural expectations

- Single source of truth for layout state: the view-model tree. The visual
  tree is a projection of it and is never independently mutated. Docking
  operations mutate the model; the views follow.
- Layout mutation (dock, float, close, split, merge) lives in **one** place
  operating on `IDockPane`, and is used identically by same-`Dock` drag,
  cross-window drag, placement seeding (§3.9), commands, and deserialization.
  No parallel implementations — cross-window docking is a different *source of
  coordinates*, and seeding a different *source of direction*, never a
  different docking implementation.
- Target ~400 lines per file; ~1000 lines is a defect. Decompose along real
  responsibility seams (tree model, mutation operations, drag session and
  target resolution, hit-testing/guides, tab-strip layout, serialization,
  controls/themes) — never mechanically, and never via partial classes.
- No editor-only, preview-only, or approximate second implementation of any
  behaviour.

---

## 14. Deliverables

1. Library project — the controls, model, mutation engine, drag session, and
   serializer.
2. Default theme resources.
3. A sample app exercising: split, tab, reorder, float into a `FloatPane`,
   raft, close, maximize, **auto-hide to an edge and restore to the original
   position** (§5.3), multi-line tab wrapping past `MaxWidth`, drag between two
   `Dock`s in separate windows, outer-edge docking, a **tool-only `Dock` that
   refuses document content** (descriptor filtering), **a seeded tool group
   that later members join after the user moves it** (§3.9), a
   declaratively-authored `DockItem` alongside bound content (§9.1), **the same
   content duplicated into two tabs in different windows**, a vetoed close
   (§3.10), and save/load of a JSON layout covering docked, floating, and
   auto-hidden state together.
4. **A keyboard-only walkthrough** of the sample app covering every docking
   operation (§11), and a restyled theme demonstrating that the controls are
   genuinely lookless (§12). Both are acceptance tests for requirements that
   are otherwise easy to claim and never verify.
