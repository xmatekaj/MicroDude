using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace MicroDude.UI
{
    public class EnumDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            var enumType = value.GetType();
            if (!enumType.IsEnum) return value.ToString();

            var field = enumType.GetField(value.ToString());
            if (field == null) return value.ToString();

            var attr = (DescriptionAttribute)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
            return attr?.Description ?? value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || !targetType.IsEnum) return null;

            foreach (var field in targetType.GetFields())
            {
                var attr = (DescriptionAttribute)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
                if (attr != null && attr.Description == value.ToString())
                {
                    return field.GetValue(null);
                }
            }

            return Enum.Parse(targetType, value.ToString());
        }
    }
}