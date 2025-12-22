using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace SchedulingApp.Converters
{
    public class RestShiftFontStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string shiftName)
            {
                return shiftName == "休息" ? FontStyles.Italic : FontStyles.Normal;
            }
            return FontStyles.Normal; // Default to normal style if value is not a string
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}