using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CodeSnip.Helpers
{
    public class LanguageToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string currentLanguageCode || parameter is not string targetLanguageCodes)
            {
                return Visibility.Collapsed;
            }

            var supportedLanguages = targetLanguageCodes.Split(',');

            if (supportedLanguages.Contains(currentLanguageCode, StringComparer.OrdinalIgnoreCase))
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
