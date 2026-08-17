# Docklonia

A docking-layout library for **Avalonia 12** / **.NET 10**: split panes, tabbed
groups, floating windows, drag-and-drop re-docking with directional guides, and a
JSON-serializable layout.

Built to [`Documentation/DOCKLONIA_PROMPT.md`](Documentation/DOCKLONIA_PROMPT.md),
which is the authoritative specification. Section references throughout the code
point back to it.

## Integration

Two steps. One include, one control.

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://Docklonia/Themes/Docklonia.axaml" />
</Application.Styles>
```

```xml
<dock:Dock ItemsSource="{Binding Panels}" Layout="{Binding Layout, Mode=TwoWay}">

  <dock:Dock.Groups>
    <dock:DockGroup Name="Tools" Seed="Right" SeedSize="0.25" />
  </dock:Dock.Groups>

  <dock:Dock.ItemDescriptors>
    <dock:DockItemDescriptor DataType="vm:CodeDocument"
                             Title="{Binding FileName}"
                             ContentKey="{Binding FullPath}"
                             CanClose="{Binding IsClosable}" />
    <dock:DockItemDescriptor DataType="vm:InspectorViewModel"
                             Title="Inspector" ContentKey="Inspector"
                             CanClose="False" Group="Tools" />
  </dock:Dock.ItemDescriptors>

</dock:Dock>
```

`xmlns:dock="https://github.com/docklonia"`. No code-behind, no bootstrapper, no
service registration.

**Why the one include cannot be avoided.** Avalonia has no equivalent of WPF's
`Themes/Generic.xaml` auto-discovery, so a library's default `ControlTheme`s are
never located automatically. Merging into `Application.Current.Resources` from a
static constructor would mutate global state as a side effect of constructing a
control; merging into each control's own `Resources` would shadow the
application's overrides, since Avalonia resolves resources from the control
outward — defeating the styling contract. One `StyleInclude` keeps resolution
order correct.

## Zero consumer entanglement

Content view models are POCOs. No interface to implement, no base class, no
attribute, no library type referenced:

```csharp
public sealed class CodeDocument      // knows nothing about Docklonia
{
    public string FileName { get; set; }
    public string FullPath { get; }
}
```

The library needs three things about your content — a title, a stable key, and
whether it may close. None is obtained by requiring an interface; each is a
binding on a per-type descriptor, instantiated once per item **with that item as
the source**. `{Binding FileName}` inside a descriptor means *"for each
`CodeDocument`, bind to that document's `FileName`"*, and it stays live, so
renaming a document renames its tab.

## Sharing a descriptor set

`ItemDescriptors` and `Groups` are styled properties over named collection
types, so a set is authored once and given to as many `Dock`s as you like. A
descriptor holds unevaluated bindings and no per-`Dock` state; each `Dock`
realizes them independently per item.

```xml
<Application.Resources>
  <dock:DockItemDescriptors x:Key="WorkspaceDescriptors">
    <dock:DockItemDescriptor DataType="vm:CodeDocument"
                             Title="{Binding FileName}"
                             ContentKey="{Binding FullPath}" />
  </dock:DockItemDescriptors>
</Application.Resources>
```

```xml
<dock:Dock ItemDescriptors="{StaticResource WorkspaceDescriptors}" />
```

Or confer the set with a class, which is how a `Dock` is declared a tool area —
one that declares no document type refuses documents (§3.7):

```xml
<Style Selector="dock|Dock.tools">
  <Setter Property="ItemDescriptors" Value="{StaticResource ToolDescriptors}" />
</Style>
```

Descriptors authored inline on the element outrank a style's, per the ordinary
XAML precedence rule.

The single library type a consumer holds is the opaque `DockLayout` on the shell
view model. It is stored and handed back, never inspected.

## Design in one paragraph

**The layout tree is view models, not controls.** No `IDockPane` implementation
derives from `Control`; the `Dock` is a *view* over the tree. That one decision
carries the rest: a drag payload is a live object reference, so moving a node
between trees — including across windows — is an object-graph operation with no
visual reparenting, nothing to serialize mid-drag, and no control-parentage
problem for the docking engine to reason about.

## Repository layout

| Path | Contents |
|---|---|
| `src/Docklonia/Model` | The tree, and the traversal over it |
| `src/Docklonia/Model/Mutations` | The single mutation engine, activation, placement, auto-hide |
| `src/Docklonia/Descriptors` | Per-item-type metadata and the live per-item binding mechanism |
| `src/Docklonia/Controls` | The `Dock` and its lookless controls |
| `src/Docklonia/Dragging` | The library-owned pointer drag and the process-wide registry |
| `src/Docklonia/Hosting` | The one place the desktop/single-view platform branch lives |
| `src/Docklonia/Serialization` | JSON, schema version, compatibility policy |
| `src/Docklonia/Themes` | The default theme, as one merged dictionary |
| `samples/Docklonia.Sample` | The sample application |
| `tests/Docklonia.Tests` | Mutation-engine and round-trip tests |

## Running the sample

```
dotnet run --project samples/Docklonia.Sample
```

It opens two windows over one shared set of documents, exercising: split, tab,
reorder, float, raft, close, maximize, auto-hide and restore, multi-line tab
wrapping, drag between two `Dock`s in separate windows, outer-edge docking, a
tool-only `Dock` that refuses document content, seeded tool groups, a
declaratively-authored `DockItem` beside bound content, duplicated tabs sharing
one view model, a vetoed close, and save/load of a JSON layout covering docked,
floating, and auto-hidden state together.

The **Restyle** button swaps in
[`Themes/Restyle.axaml`](samples/Docklonia.Sample/Themes/Restyle.axaml), which
restyles every control using only documented resource keys and pseudo-classes —
no template replaced, nothing subclassed. That is an acceptance test for the
styling contract, not a decoration.

[`Documentation/KEYBOARD_WALKTHROUGH.md`](Documentation/KEYBOARD_WALKTHROUGH.md)
drives every docking operation without a pointer.

## Tests

```
dotnet test
```

## License

MIT — see [LICENSE](LICENSE). The same license Avalonia itself uses, so adding
Docklonia does not change a consuming application's licensing story.
