using System.Globalization;
using System.Windows.Data;

namespace SchedulingApp.Converters
{
    [ValueConversion(typeof(TimeSpan?), typeof(DateTime?))]
    public class TimeSpanToDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var timeSpan = value as TimeSpan?;
            if (timeSpan == null)
            {
                return null;
            }
            return DateTime.Today + timeSpan.Value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var dateTime = value as DateTime?;
            if (dateTime == null)
            {
                return null;
            }
            return dateTime.Value.TimeOfDay;
        }
    }
}
