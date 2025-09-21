using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CodeSnip.Helpers
{
    public class StringToFontWeightConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            if (value is string fontWeightName)
            {
                var converter = new FontWeightConverter();
                object? convertedValue = converter.ConvertFromString(fontWeightName);
                if (convertedValue is FontWeight fontWeight)
                {
                    return fontWeight;
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            if (value is FontWeight fontWeight)
            {
                var converter = new FontWeightConverter();
                // ConvertToString can return null, but we want to return a string.
                // If it's null, we don't update the binding source.
                string? result = converter.ConvertToString(fontWeight);
                if (result is not null)
                {
                    return result;
                }
            }
            return Binding.DoNothing;
        }
    }
}
