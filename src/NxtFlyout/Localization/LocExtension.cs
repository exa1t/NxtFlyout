using System.Windows.Data;
using System.Windows.Markup;
using NxtFlyout.Core.Localization;

namespace NxtFlyout.Localization;

/// <summary>
/// XAML markup extension for localized text: <c>Text="{loc:Loc Some_Key}"</c>.
/// It produces a binding to <see cref="Localizer"/>, so the text updates in place when the
/// language changes.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    /// <summary>Name of the string in Strings.resx.</summary>
    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Localizer.Instance,
            Mode = BindingMode.OneWay,
        };

        return binding.ProvideValue(serviceProvider);
    }
}
