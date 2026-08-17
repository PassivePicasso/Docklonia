using Avalonia;
using Avalonia.Data;

namespace Docklonia.Descriptors;

/// <summary>
/// Instantiates one descriptor binding against one item and keeps it live
/// (§3.7).
/// </summary>
/// <remarks>
/// A descriptor's <c>{Binding FileName}</c> is captured unevaluated and means
/// <i>"for each item of this type, bind to that item's FileName"</i>. Realizing
/// it needs a binding target with a <c>DataContext</c>, which is why this is a
/// <see cref="StyledElement"/> rather than a bare <see cref="AvaloniaObject"/> —
/// the same shape Avalonia's own per-row column bindings use. It is never added
/// to a visual or logical tree.
/// </remarks>
internal sealed class ItemValueProjection : StyledElement, IDisposable
{
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<ItemValueProjection, object?>(nameof(Value));

    private readonly Action<object?> _onValue;
    private BindingExpressionBase? _expression;

    private ItemValueProjection(Action<object?> onValue)
    {
        _onValue = onValue;
    }

    public object? Value => GetValue(ValueProperty);

    /// <summary>
    /// Binds <paramref name="binding"/> with <paramref name="item"/> as the
    /// source, calling back on every change so the projection stays live.
    /// </summary>
    public static ItemValueProjection? Create(BindingBase? binding, object? item, Action<object?> onValue)
    {
        if (binding is null)
        {
            return null;
        }

        var projection = new ItemValueProjection(onValue) { DataContext = item };
        projection._expression = projection.Bind(ValueProperty, binding);
        onValue(projection.Value);

        return projection;
    }

    /// <summary>
    /// Reads a binding once, for values that are invoked rather than displayed
    /// — the close commands of §3.10.
    /// </summary>
    public static object? EvaluateOnce(BindingBase? binding, object? item)
    {
        using var projection = Create(binding, item, static _ => { });
        return projection?.Value;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
        {
            _onValue(change.NewValue);
        }
    }

    public void Dispose()
    {
        _expression?.Dispose();
        _expression = null;
        DataContext = null;
    }
}
