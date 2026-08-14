using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace PasswordManager.UI.Converters;

/// <summary>
/// Converte <see cref="bool"/> em <see cref="Visibility"/>. Com
/// <see cref="Invert"/> igual a true, inverte o resultado (usado para
/// ocultar/mostrar elementos opostos).
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is bool b && b;
        if (Invert)
            flag = !flag;

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}