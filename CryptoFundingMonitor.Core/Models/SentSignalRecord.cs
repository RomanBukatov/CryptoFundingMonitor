using System.Text.Json.Serialization;

namespace CryptoFundingMonitor.Core.Models
{
    /// <summary>
    /// Модель записи об отправленном сигнале с временной меткой
    /// </summary>
    public class SentSignalRecord
    {
        /// <summary>
        /// Название биржи
        /// </summary>
        [JsonPropertyName("exchangeName")]
        public string ExchangeName { get; set; } = string.Empty;

        /// <summary>
        /// Символ торговой пары
        /// </summary>
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Время последнего отправленного сигнала
        /// </summary>
        [JsonPropertyName("lastSentTime")]
        public DateTime LastSentTime { get; set; }

        /// <summary>
        /// Конструктор для создания записи
        /// </summary>
        public SentSignalRecord(string exchangeName, string symbol, DateTime lastSentTime)
        {
            ExchangeName = exchangeName;
            Symbol = symbol;
            LastSentTime = lastSentTime;
        }

        /// <summary>
        /// Получить уникальный ключ для пары
        /// </summary>
        public string GetKey() => $"{ExchangeName}-{Symbol}";

        /// <summary>
        /// Проверить, прошло ли достаточно времени с момента последнего отправления сигнала
        /// </summary>
        /// <param name="intervalHours">Интервал в часах (по умолчанию 8 часов)</param>
        /// <returns>true если можно отправить сигнал повторно</returns>
        public bool CanSendSignal(int intervalHours = 8)
        {
            return (DateTime.UtcNow - LastSentTime).TotalHours >= intervalHours;
        }
    }
}