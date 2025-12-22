using System.Globalization;
using System.Windows.Data;

namespace SchedulingApp.Converters
{
    public class NotRestShiftConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string shiftName)
            {
                return shiftName != "休息";
            }
            return true; // Default to enabled if value is not a string
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}