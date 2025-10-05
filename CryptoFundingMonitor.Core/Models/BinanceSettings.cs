namespace CryptoFundingMonitor.Core.Models
{
    /// <summary>
    /// Настройки для биржи Binance
    /// </summary>
    public class BinanceSettings
    {
        /// <summary>
        /// API ключ биржи Binance
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// API секрет биржи Binance
        /// </summary>
        public string ApiSecret { get; set; } = string.Empty;

        /// <summary>
        /// Включена ли биржа Binance
        /// </summary>
        public bool IsEnabled { get; set; } = false;

        /// <summary>
        /// Пороговое значение Funding Rate для Binance
        /// </summary>
        public decimal Threshold { get; set; } = -0.1m;
    }
}