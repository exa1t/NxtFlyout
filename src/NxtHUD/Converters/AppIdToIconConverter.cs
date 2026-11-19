using System.Globalization;
using System.Windows.Data;
using NxtHUD.Services;

namespace NxtHUD.Converters;

/// <summary>Turns a media session's app id into that app's icon (or null).</summary>
public sealed class AppIdToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return AppIconProvider.GetIcon(value as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
