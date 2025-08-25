using ControlzEx.Theming;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CodeSnip.Helpers
{
    public class AccentToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var accentName = value as string;
            if (string.IsNullOrWhiteSpace(accentName))
                return Brushes.Transparent;

            var theme = ThemeManager.Current.Themes
                .FirstOrDefault(t => t.ColorScheme == accentName);

            return theme?.ShowcaseBrush ?? Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }

}
