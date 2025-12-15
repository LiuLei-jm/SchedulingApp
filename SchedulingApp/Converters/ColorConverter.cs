using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SchedulingApp.Converters
{
    public class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorString)
            {
                if (string.IsNullOrEmpty(colorString))
                {
                    return Brushes.Transparent;
                }
                try
                {
                    // Use the ColorConverter from System.Windows.Media to parse the color string
                    var color = (Color)ColorConverterHelper.ConvertFromString(colorString);
                    return new SolidColorBrush(color);
                }
                catch
                {
                    return Brushes.Transparent;
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color.ToString();
            }
            return null;
        }
    }

    // Helper class to resolve the issue with ColorConverter
    public static class ColorConverterHelper
    {
        private static readonly System.Windows.Media.ColorConverter _colorConverter = new System.Windows.Media.ColorConverter();

        public static object ConvertFromString(string colorString)
        {
            // Fix: Use the static method ConvertFromInvariantString instead of the instance method ConvertFromString
            return _colorConverter.ConvertFromInvariantString(colorString);
        }
    }
}