using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Docklonia.Sample.ViewModels;

/// <summary>
/// Minimal notification plumbing. Deliberately not a library type — nothing in
/// this file references Docklonia at all.
/// </summary>
public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(name);
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// A document. Note what is absent: no interface from the library, no base
/// class, no attribute, no pane id. The <c>Dock</c> learns everything it needs
/// from the descriptor bindings in MainWindow.axaml.
/// </summary>
public sealed class CodeDocument : Observable
{
    private string _fileName;
    private string _text;
    private bool _isClosable = true;
    private bool _isDirty;

    public CodeDocument(string fileName, string fullPath, string text)
    {
        _fileName = fileName;
        _text = text;
        FullPath = fullPath;
    }

    /// <summary>Bound as the tab title. Renaming updates the tab automatically.</summary>
    public string FileName
    {
        get => _fileName;
        set => Set(ref _fileName, value);
    }

    /// <summary>The consumer's own key — a path it already had. Used as the content key.</summary>
    public string FullPath { get; }

    public string Text
    {
        get => _text;
        set
        {
            if (Set(ref _text, value))
            {
                IsDirty = true;
            }
        }
    }

    public bool IsClosable
    {
        get => _isClosable;
        set => Set(ref _isClosable, value);
    }

    /// <summary>A dirty document vetoes its own close, to exercise §3.10.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (Set(ref _isDirty, value))
            {
                Raise(nameof(FileName));
            }
        }
    }

    public ObservableCollection<object> ContextActions { get; } = new();

    public ICommand? CloseCommand { get; set; }

    public ICommand? ClosedCommand { get; set; }

    public override string ToString() => FileName;
}

public sealed class TerminalPane : Observable
{
    private string _sessionName;

    public TerminalPane(string sessionName, string sessionId)
    {
        _sessionName = sessionName;
        SessionId = sessionId;
    }

    public string SessionName
    {
        get => _sessionName;
        set => Set(ref _sessionName, value);
    }

    public string SessionId { get; }

    public ObservableCollection<string> Lines { get; } = new();
}

/// <summary>
/// A genuinely singular tool pane. Its descriptor uses a constant
/// <c>ContentKey</c>, which declares it a singleton within the <c>Dock</c> —
/// though it can still be duplicated into two tabs, both observing this one
/// instance.
/// </summary>
public sealed class InspectorViewModel : Observable
{
    private string _selection = "Nothing selected";

    public string Selection
    {
        get => _selection;
        set => Set(ref _selection, value);
    }
}

public sealed class OutlineViewModel : Observable
{
    public ObservableCollection<string> Symbols { get; } = new();
}

/// <summary>An action contributed to a tab's context menu. A plain command object, not a MenuItem.</summary>
public sealed class ContextAction : ICommand
{
    private readonly Action _execute;

    public ContextAction(string title, Action execute)
    {
        Title = title;
        _execute = execute;
    }

    public string Title { get; }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();

    public override string ToString() => Title;
}

/// <summary>A command that needs no state beyond a delegate.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute) : this(_ => execute())
    {
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
