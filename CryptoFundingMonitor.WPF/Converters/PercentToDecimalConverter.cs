using System;
using System.Globalization;
using System.Windows.Data;

namespace CryptoFundingMonitor.WPF.Converters
{
    /// <summary>
    /// Конвертер для преобразования процентов в десятичные числа и обратно
    /// </summary>
    public class PercentToDecimalConverter : IValueConverter
    {
        /// <summary>
        /// Конвертирует процент в десятичное число (делит на 100)
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                return (decimalValue / 100m).ToString("F4", culture);
            }
            return "0.0000";
        }

        /// <summary>
        /// Конвертирует десятичное число в процент (умножает на 100)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && decimal.TryParse(stringValue, out decimal decimalValue))
            {
                return decimalValue * 100m;
            }
            return 0m;
        }
    }
}