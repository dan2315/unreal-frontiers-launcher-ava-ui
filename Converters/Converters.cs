using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Platform;

namespace UnrealFrontiersLauncher.Converters
{
    public class UniversalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Parameter can be a comma-separated list of conditions or expected values
            string[] parameters = (parameter as string)?.Split(',') ?? Array.Empty<string>();

            if (parameters.Length > 1)
            {
                string expectedValue = parameters[0];
                string trueValue = parameters[1];
                string falseValue = parameters.Length > 2 ? parameters[2] : null;

                if (value?.ToString() == expectedValue)
                {
                    return GetConvertedValue(trueValue, targetType);
                }
                else
                {
                    return GetConvertedValue(falseValue, targetType);
                }
            }

            return value; // Return value if no conditions are met
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private object GetConvertedValue(string value, Type targetType)
        {
            if (targetType == typeof(IBrush) && !string.IsNullOrEmpty(value))
            {
                return new SolidColorBrush(Color.Parse(value));
            }
            if (targetType == typeof(Color) && !string.IsNullOrEmpty(value))
            {
                return Color.Parse(value);
            }
            if (targetType == typeof(bool) && bool.TryParse(value, out bool boolResult))
            {
                return boolResult;
            }
            if (targetType == typeof(double) && double.TryParse(value, out double doubleResult))
            {
                return doubleResult;
            }
            return value;
        }
    }

    public class MultiConditionColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Tuple<bool, bool> conditions)
            {
                bool isSelected = conditions.Item1;
                bool isHovered = conditions.Item2;

                if (isSelected)
                    return new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff)); // White for selected

                if (isHovered)
                    return new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff)); // White for hovered
            }
            return new SolidColorBrush(Color.FromArgb(0xff, 0x6f, 0x6f, 0x6f)); // Default gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class TextToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
        {
            return !string.IsNullOrWhiteSpace(value as string);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
        {
            throw new NotImplementedException();
        }
    }
    
}
