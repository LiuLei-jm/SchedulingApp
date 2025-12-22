using System.Globalization;
using System.Windows.Data;

namespace SchedulingApp.Converters
{
    public class ShiftReadOnlyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string shiftName)
            {
                return shiftName == "休息";
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}