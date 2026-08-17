using System.Collections.ObjectModel;
using System.Windows.Input;
using Docklonia.Model;

namespace Docklonia.Sample.ViewModels;

/// <summary>
/// The shell. It holds exactly one library type — the opaque
/// <see cref="DockLayout"/> handle bound to <c>Dock.Layout</c> — and never
/// inspects it, only stores it and hands it back (§3.6, §9.2).
/// </summary>
public sealed class ShellViewModel : Observable
{
    private static readonly string LayoutPath =
        Path.Combine(Path.GetTempPath(), "docklonia-sample-layout.json");

    private DockLayout? _layout;
    private DockLayout? _toolLayout;
    private object? _activeContent;
    private string _status = "Ready.";
    private int _nextDocument = 1;

    public ShellViewModel()
    {
        Inspector = new InspectorViewModel();
        Outline = new OutlineViewModel();

        Outline.Symbols.Add("Program.Main");
        Outline.Symbols.Add("ShellViewModel");

        AddDocument = new RelayCommand(() => OpenDocument($"Untitled{_nextDocument}.cs"));
        AddTerminal = new RelayCommand(AddTerminalSession);
        SaveLayout = new RelayCommand(Save);
        LoadLayout = new RelayCommand(Load);
        ResetLayout = new RelayCommand(Reset);
        ToggleDirty = new RelayCommand(() =>
        {
            if (ActiveContent is CodeDocument document)
            {
                document.IsDirty = !document.IsDirty;
                Status = $"{document.FileName} is now {(document.IsDirty ? "dirty" : "clean")}.";
            }
        });

        Panels.Add(Inspector);
        Panels.Add(Outline);

        OpenDocument("Program.cs");
        OpenDocument("ShellViewModel.cs");
        AddTerminalSession();
    }

    /// <summary>Heterogeneous by design: these types share no base, no interface, and no member names.</summary>
    public ObservableCollection<object> Panels { get; } = new();

    /// <summary>A second, tool-only surface. Its descriptor set is what makes it refuse documents.</summary>
    public ObservableCollection<object> ToolPanels { get; } = new();

    public InspectorViewModel Inspector { get; }

    public OutlineViewModel Outline { get; }

    /// <summary>The opaque layout handle. Two-way bound; never inspected here.</summary>
    public DockLayout? Layout
    {
        get => _layout;
        set => Set(ref _layout, value);
    }

    public DockLayout? ToolLayout
    {
        get => _toolLayout;
        set => Set(ref _toolLayout, value);
    }

    /// <summary>The active pane's content, as this application's own object — never an <c>IDockPane</c>.</summary>
    public object? ActiveContent
    {
        get => _activeContent;
        set
        {
            if (Set(ref _activeContent, value))
            {
                Inspector.Selection = value switch
                {
                    CodeDocument document => $"Document: {document.FileName}",
                    TerminalPane terminal => $"Terminal: {terminal.SessionName}",
                    null => "Nothing selected",
                    _ => value.GetType().Name,
                };
            }
        }
    }

    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    public ICommand AddDocument { get; }

    public ICommand AddTerminal { get; }

    public ICommand SaveLayout { get; }

    public ICommand LoadLayout { get; }

    public ICommand ResetLayout { get; }

    public ICommand ToggleDirty { get; }

    private void OpenDocument(string name)
    {
        var document = new CodeDocument(name, $"/src/{name}", $"// {name}\n");

        // A dirty document intercepts its own close: the Dock invokes this
        // instead of closing, and the document decides (§3.10).
        document.CloseCommand = new RelayCommand(_ =>
        {
            if (document.IsDirty)
            {
                Status = $"{document.FileName} has unsaved changes — close vetoed. Use 'Toggle dirty' then close again.";
                return;
            }

            Panels.Remove(document);
        });

        // Fired once, when the last tab showing this document is gone.
        document.ClosedCommand = new RelayCommand(_ => Status = $"{document.FileName} released its last view.");

        document.ContextActions.Add(new ContextAction("Mark clean", () =>
        {
            document.IsDirty = false;
            Status = $"{document.FileName} marked clean.";
        }));

        Panels.Add(document);
        _nextDocument++;
        Status = $"Opened {name}.";
    }

    private void AddTerminalSession()
    {
        var index = Panels.OfType<TerminalPane>().Count() + 1;
        var terminal = new TerminalPane($"Session {index}", $"term-{index}");

        terminal.Lines.Add($"$ session {index} started");
        Panels.Add(terminal);
        Status = $"Started {terminal.SessionName}.";
    }

    /// <summary>
    /// Serialization is reachable from the layout object itself, so this view
    /// model persists what is bound to it without holding a reference to the
    /// control (§9.2).
    /// </summary>
    private void Save()
    {
        if (Layout is null)
        {
            Status = "Nothing to save yet.";
            return;
        }

        File.WriteAllText(LayoutPath, Layout.ToJson(new Serialization.LayoutJsonOptions { WriteIndented = true }));
        Status = $"Saved layout to {LayoutPath}";
    }

    /// <summary>
    /// The inverse is symmetric: a layout object is constructed from JSON and
    /// assigned, requiring no <c>Dock</c> reference either. Content is matched
    /// back to the live items in <see cref="Panels"/> by key.
    /// </summary>
    private void Load()
    {
        if (!File.Exists(LayoutPath))
        {
            Status = "No saved layout found. Save one first.";
            return;
        }

        try
        {
            Layout = DockLayout.FromJson(File.ReadAllText(LayoutPath));
            Status = $"Loaded layout from {LayoutPath}";
        }
        catch (Serialization.LayoutFormatException error)
        {
            Status = $"Could not read layout: {error.Message}";
        }
    }

    private void Reset()
    {
        Layout = new DockLayout();
        Status = "Layout reset. Panes re-seeded from their groups.";
    }
}
