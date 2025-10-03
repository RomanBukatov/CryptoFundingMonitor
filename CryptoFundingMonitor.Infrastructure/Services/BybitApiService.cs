using Bybit.Net.Clients;
using Bybit.Net.Enums;
using CryptoFundingMonitor.Core.Models;
using CryptoFundingMonitor.Core.Services;
using CryptoExchange.Net.Authentication;

namespace CryptoFundingMonitor.Infrastructure.Services
{
    /// <summary>
    /// Сервис для работы с API биржи Bybit
    /// </summary>
    public class BybitApiService : IBrokerApiService
    {
        private const string ExchangeName = "Bybit";

        /// <summary>
        /// Получает ставки финансирования для всех USDT-фьючерсов с Bybit
        /// </summary>
        /// <param name="apiKey">API ключ биржи</param>
        /// <param name="apiSecret">API секрет биржи</param>
        /// <returns>Коллекция сигналов ставок финансирования</returns>
        public async Task<IEnumerable<FundingRateSignal>> GetFundingRatesAsync(string apiKey, string apiSecret)
        {
            var signals = new List<FundingRateSignal>();

            try
            {
                // Создаем клиента Bybit с учетными данными
                using var client = new BybitRestClient(options =>
                {
                    if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret))
                    {
                        options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
                    }
                });

                // Получаем информацию о всех торговых парах для линейных фьючерсов (USDT)
                var symbolsResult = await client.V5Api.ExchangeData.GetLinearInverseTickersAsync(Category.Linear);

                if (!symbolsResult.Success)
                {
                    throw new Exception($"Ошибка при получении списка символов с Bybit: {symbolsResult.Error?.Message}");
                }

                var usdtSymbols = symbolsResult.Data.List
                    .Where(t => t.Symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Получаем funding rate для каждого символа
                foreach (var ticker in usdtSymbols)
                {
                    try
                    {
                        // Получаем текущую цену из тикера
                        var currentPrice = ticker.LastPrice;
                        if (currentPrice == 0)
                        {
                            continue;
                        }

                        // Получаем funding rate из тикера
                        var fundingRate = ticker.FundingRate ?? 0;

                        // Извлекаем базовый символ (например, из "BTCUSDT" получаем "BTC")
                        var baseSymbol = ticker.Symbol.Replace("USDT", "", StringComparison.OrdinalIgnoreCase);

                        var signal = new FundingRateSignal(
                            ExchangeName: ExchangeName,
                            Symbol: baseSymbol,
                            Pair: ticker.Symbol,
                            CurrentPrice: currentPrice,
                            FundingRate: fundingRate,
                            Timestamp: DateTime.UtcNow
                        );

                        signals.Add(signal);
                    }
                    catch (Exception ex)
                    {
                        // Логируем ошибку для конкретного символа, но продолжаем обработку остальных
                        Console.WriteLine($"Ошибка при обработке символа {ticker.Symbol}: {ex.Message}");
                    }
                }

                return signals;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при получении funding rates с Bybit: {ex.Message}", ex);
            }
        }
    }
}