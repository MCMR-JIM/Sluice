using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Sluice.Desktop.Converters;

/// <summary>Maps a chip tone token to a brush.</summary>
public sealed class ToneToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = (value as string) switch
        {
            "ok" => "SuccessBrush",
            "warn" => "WarningBrush",
            "bad" => "DangerBrush",
            _ => "TextSecondaryBrush",
        };

        return Application.Current?.TryFindResource(key) as Brush ?? new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
