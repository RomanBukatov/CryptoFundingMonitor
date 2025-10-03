using System;
using System.Globalization;
using System.Windows.Data;

namespace CryptoFundingMonitor.WPF.Converters
{
    /// <summary>
    /// Конвертер для преобразования bool значения в текст кнопки Старт/Стоп
    /// </summary>
    public class BoolToStartStopConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMonitoring)
            {
                return isMonitoring ? "СТОП" : "СТАРТ";
            }
            return "СТАРТ";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}