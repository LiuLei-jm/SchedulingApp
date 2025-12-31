using System.Globalization;
using System.Windows.Data;
using SchedulingApp.Helpers;

namespace SchedulingApp.Converters
{
    public class ShiftReadOnlyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string shiftName)
            {
                return shiftName == RulesHelper.GetRestShiftName();
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}