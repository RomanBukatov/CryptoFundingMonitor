using Binance.Net.Clients;
using Binance.Net.Enums;
using CryptoFundingMonitor.Core.Models;
using CryptoFundingMonitor.Core.Services;
using CryptoExchange.Net.Authentication;

namespace CryptoFundingMonitor.Infrastructure.Services
{
    /// <summary>
    /// Сервис для работы с API биржи Binance
    /// </summary>
    public class BinanceApiService : IBrokerApiService
    {
        private const string ExchangeName = "Binance";

        /// <summary>
        /// Получает ставки финансирования для всех USDT-фьючерсов с Binance
        /// </summary>
        /// <param name="apiKey">API ключ биржи</param>
        /// <param name="apiSecret">API секрет биржи</param>
        /// <returns>Коллекция сигналов ставок финансирования</returns>
        public async Task<IEnumerable<FundingRateSignal>> GetFundingRatesAsync(string apiKey, string apiSecret)
        {
            var signals = new List<FundingRateSignal>();

            try
            {
                // Создаем клиента Binance с учетными данными
                using var client = new BinanceRestClient(options =>
                {
                    if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret))
                    {
                        options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
                    }
                });

                // Получаем информацию о всех фьючерсных парах USDT
                var symbolsResult = await client.UsdFuturesApi.ExchangeData.GetTickersAsync();

                if (!symbolsResult.Success)
                {
                    throw new Exception($"Ошибка при получении списка символов с Binance: {symbolsResult.Error?.Message}");
                }

                var usdtSymbols = symbolsResult.Data
                    .Where(t => t.Symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Обрабатываем каждый символ
                foreach (var ticker in usdtSymbols)
                {
                    try
                    {
                        // Получаем текущую цену
                        var currentPrice = ticker.LastPrice;
                        if (currentPrice == 0)
                        {
                            continue;
                        }

                        // Получаем funding rate для конкретного символа
                        var fundingRateResult = await client.UsdFuturesApi.ExchangeData.GetFundingRatesAsync(ticker.Symbol, limit: 1);
                        
                        if (!fundingRateResult.Success || !fundingRateResult.Data.Any())
                        {
                            continue;
                        }

                        var fundingRateInfo = fundingRateResult.Data.First();

                        // Извлекаем базовый символ (например, из "BTCUSDT" получаем "BTC")
                        var baseSymbol = ticker.Symbol.Replace("USDT", "", StringComparison.OrdinalIgnoreCase);

                        var signal = new FundingRateSignal(
                            ExchangeName: ExchangeName,
                            Symbol: baseSymbol,
                            Pair: ticker.Symbol,
                            CurrentPrice: currentPrice,
                            FundingRate: fundingRateInfo.FundingRate,
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
                throw new Exception($"Ошибка при получении funding rates с Binance: {ex.Message}", ex);
            }
        }
    }
}