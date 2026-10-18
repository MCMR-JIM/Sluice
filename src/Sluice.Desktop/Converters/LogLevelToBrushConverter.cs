using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Sluice.Desktop.ViewModels;

namespace Sluice.Desktop.Converters;

/// <summary>Colours a log line by severity, using the theme brushes.</summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            LogLevel.Success => "SuccessBrush",
            LogLevel.Warning => "WarningBrush",
            LogLevel.Error => "DangerBrush",
            LogLevel.Command => "AccentBrush",
            _ => "TextSecondaryBrush",
        };

        return Application.Current?.TryFindResource(key) as Brush
            ?? new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
