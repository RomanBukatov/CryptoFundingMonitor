using CryptoFundingMonitor.Core.Models;

namespace CryptoFundingMonitor.Core.Services
{
    /// <summary>
    /// Интерфейс для работы с API криптовалютных бирж
    /// </summary>
    public interface IBrokerApiService
    {
        /// <summary>
        /// Получает ставки финансирования с биржи
        /// </summary>
        /// <param name="apiKey">API ключ биржи</param>
        /// <param name="apiSecret">API секрет биржи</param>
        /// <returns>Коллекция сигналов ставок финансирования</returns>
        Task<IEnumerable<FundingRateSignal>> GetFundingRatesAsync(string apiKey, string apiSecret);
    }
}