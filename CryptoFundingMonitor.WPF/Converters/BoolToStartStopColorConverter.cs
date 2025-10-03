using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CryptoFundingMonitor.WPF.Converters
{
    /// <summary>
    /// Конвертер для преобразования bool значения в цвет кнопки Старт/Стоп
    /// </summary>
    public class BoolToStartStopColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMonitoring)
            {
                return isMonitoring
                    ? new SolidColorBrush(Color.FromRgb(220, 53, 69)) // Красный для СТОП
                    : new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Зеленый для СТАРТ
            }
            return new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Зеленый по умолчанию
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}