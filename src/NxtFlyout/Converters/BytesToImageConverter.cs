using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace NxtFlyout.Converters;

/// <summary>
/// Decodes encoded image bytes (album art) into a bitmap for display. Returns null for
/// missing or undecodable data, which leaves the placeholder showing.
/// </summary>
public sealed class BytesToImageConverter : IValueConverter
{
    /// <summary>Art is shown small, so decode it small: less memory than keeping the full-size cover.</summary>
    private const int DecodeWidth = 192;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = DecodeWidth;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            // Players occasionally publish truncated or exotic images.
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
