using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CodeSnip.Helpers
{
    public class ResultToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? result = value as string;
            if (string.IsNullOrEmpty(result))
                return Brushes.Gray;

            if (result == "✓")
                return Brushes.Green;
            else if (result == "✗")
                return Brushes.Red;

            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
