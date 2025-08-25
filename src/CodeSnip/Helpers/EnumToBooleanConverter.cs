using System.Globalization;
using System.Windows.Data;

namespace CodeSnip.Helpers
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? parameterString = parameter as string;
            if (parameterString == null || value == null)
                return false;

            return value.ToString() == parameterString;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? parameterString = parameter as string;
            if (parameterString == null)
                return Binding.DoNothing;

            return Enum.Parse(targetType, parameterString);
        }
    }
}
