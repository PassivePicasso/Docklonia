using System.ComponentModel;
using System.Globalization;
using Avalonia.Data;

namespace Docklonia.Descriptors;

/// <summary>
/// Lets a descriptor property accept a constant as well as a binding (§3.7):
/// <c>Title="Inspector"</c> alongside <c>Title="{Binding FileName}"</c>.
/// </summary>
/// <remarks>
/// The properties stay typed <see cref="BindingBase"/> and the literal is
/// wrapped in a binding whose source <i>is</i> the literal. Typing them
/// <c>object</c> instead would make XAML evaluate <c>{Binding …}</c> against the
/// <c>Dock</c>'s own <c>DataContext</c> and assign the result — precisely the
/// per-item/ordinary-binding confusion the descriptor grouping exists to
/// prevent.
/// </remarks>
public sealed class BindingLiteralConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || typeof(BindingBase).IsAssignableFrom(sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object? value) => value switch
    {
        BindingBase binding => binding,
        null => null,
        _ => Constant(value),
    };

    /// <summary>
    /// A binding whose source is the value itself and whose path is empty, so
    /// it resolves to that value for every item and never consults a
    /// <c>DataContext</c>.
    /// </summary>
    internal static BindingBase Constant(object value) => new Binding
    {
        Source = value,
        Path = string.Empty,
        Mode = BindingMode.OneTime,
    };
}
