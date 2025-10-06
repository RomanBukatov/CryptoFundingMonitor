using System.Net.Http;
using System.Text.Json;
using CryptoFundingMonitor.Core.Models;
using CryptoFundingMonitor.Core.Services;

namespace CryptoFundingMonitor.Infrastructure.Services
{
    /// <summary>
    /// Сервис для работы с API биржи MEXC
    /// </summary>
    public class MexcApiService : IBrokerApiService
    {
        private const string ExchangeName = "MEXC";
        private const string BaseUrl = "https://contract.mexc.com";

        /// <summary>
        /// Получает ставки финансирования для всех USDT-фьючерсов с MEXC
        /// </summary>
        /// <param name="apiKey">API ключ биржи</param>
        /// <param name="apiSecret">API секрет биржи</param>
        /// <returns>Коллекция сигналов ставок финансирования</returns>
        public async Task<IEnumerable<FundingRateSignal>> GetFundingRatesAsync(string apiKey, string apiSecret)
        {
            var signals = new List<FundingRateSignal>();

            try
            {
                using var httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri(BaseUrl);
                httpClient.Timeout = TimeSpan.FromSeconds(10); // Таймаут для надежности

                // Дополнительные параметры для надежности в режиме публикации
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoFundingMonitor/1.0");
                httpClient.DefaultRequestHeaders.ConnectionClose = false; // Keep-Alive для производительности

                // Получаем информацию по всем фьючерсным контрактам
                var tickerResponse = await httpClient.GetAsync("/api/v1/contract/ticker");
                if (!tickerResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[MEXC API] Ошибка HTTP запроса: {tickerResponse.StatusCode}");
                    throw new Exception($"Ошибка при получении данных с MEXC: {tickerResponse.StatusCode}");
                }

                var tickerContent = await tickerResponse.Content.ReadAsStringAsync();
                var tickerData = JsonDocument.Parse(tickerContent);

                var contracts = tickerData.RootElement
                    .GetProperty("data")
                    .EnumerateArray()
                    .Where(c => c.GetProperty("symbol").GetString().EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var contract in contracts)
                {
                    try
                    {
                        var symbol = contract.GetProperty("symbol").GetString();
                        var fundingRate = contract.GetProperty("fundingRate").GetDecimal();
                        var currentPrice = contract.GetProperty("lastPrice").GetDecimal();

                        if (currentPrice == 0)
                        {
                            continue;
                        }

                        // Извлекаем базовый символ (например, из "BTCUSDT" или "BTC_USDT" получаем "BTC")
                        var baseSymbol = symbol.Replace("USDT", "", StringComparison.OrdinalIgnoreCase).Replace("_", "");

                        var signal = new FundingRateSignal(
                            ExchangeName: ExchangeName,
                            Symbol: baseSymbol,
                            Pair: symbol,
                            CurrentPrice: currentPrice,
                            FundingRate: fundingRate,
                            TakeProfitPrice: null,
                            Timestamp: DateTime.UtcNow
                        );

                        signals.Add(signal);
                    }
                    catch (Exception ex)
                    {
                        // Логируем ошибку для конкретного контракта, но продолжаем обработку остальных
                        Console.WriteLine($"Ошибка при обработке контракта {contract.GetProperty("symbol").GetString()}: {ex.Message}");
                    }
                }

                return signals;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при получении funding rates с MEXC: {ex.Message}", ex);
            }
        }
    }
}