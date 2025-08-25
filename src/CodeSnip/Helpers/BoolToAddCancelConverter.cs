using System.Globalization;
using System.Windows.Data;

namespace CodeSnip.Helpers
{
    public class BoolToAddCancelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "Cancel" : "Add";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
