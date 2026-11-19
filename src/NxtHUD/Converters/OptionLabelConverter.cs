using System.Globalization;
using System.Windows.Data;
using NxtHUD.Core.Localization;

namespace NxtHUD.Converters;

/// <summary>
/// Turns a picker item into its display text: an enum value becomes its localized label and
/// a language code becomes that language's name. Used as a multi-value converter whose
/// second input is the current culture, purely so the text is re-evaluated when the
/// language changes.
/// </summary>
public sealed class OptionLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var value = values.Length > 0 ? values[0] : null;

        return value switch
        {
            Enum enumValue => Localizer.Instance.GetEnumLabel(enumValue),
            string language => Localizer.Instance.GetLanguageDisplayName(language),
            _ => string.Empty,
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
