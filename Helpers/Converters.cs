using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace WinUIShared.Helpers
{
    public class EnumToVisibilityConverter<T> : IValueConverter where T : struct, Enum //Can't be used because of XAML limitations
    {
        public T Enum { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not T enumValue) return Visibility.Collapsed;
            return enumValue.Equals(Enum) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    // Required converter properties are supplied by XAML before Convert is called.
    public class EnumToVisibilityConverter : IValueConverter
    {
        public Type EnumType { get; set; } = null!;
        public string EnumForVisibility { get; set; } = null!;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var enumValue = Enum.Parse(EnumType, EnumForVisibility);
            return value.Equals(enumValue) == !(bool.TryParse(parameter.ToString(), out var invert) && invert) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToBindableStringConverter : IValueConverter
    {
        public string FalseString { get; set; } = string.Empty;
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return boolValue ? parameter : FalseString;
            }
            return FalseString;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class DoubleToVisibilityConverter : IValueConverter
    {
        public object ZeroValue { get; set; } = Visibility.Collapsed;
        public object NotZeroValue { get; set; } = Visibility.Visible;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double doubleValue)
            {
                return doubleValue > 0 ? NotZeroValue : ZeroValue;
            }
            return ZeroValue;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class StringEqualityConverter : IValueConverter
    {
        public string CompareTo { get; set; } = null!;
        public object Equal { get; set; } = null!;
        public object NotEqual { get; set; } = null!;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string stringValue)
            {
                return (stringValue == CompareTo) == !bool.Parse(parameter?.ToString() ?? "false") ? Equal : NotEqual;
            }
            return NotEqual;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class StringEqualityVisibilityConverter : StringEqualityConverter
    {
        public StringEqualityVisibilityConverter()
        {
            Equal = Visibility.Visible;
            NotEqual = Visibility.Collapsed;
        }
    }
}
