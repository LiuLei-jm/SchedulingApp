using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SchedulingApp.Converters
{
    public class IsRestShiftConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string shiftName)
            {
                return shiftName == "休息" ? new SolidColorBrush(Colors.LightGray) : System.Windows.Media.Brushes.Transparent;
            }
            return System.Windows.Media.Brushes.Transparent; // Default to transparent if value is not a string
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}