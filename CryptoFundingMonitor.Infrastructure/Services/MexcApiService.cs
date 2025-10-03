using System.Net.Http.Json;
using System.Text.Json.Serialization;
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
        private readonly HttpClient _httpClient;

        public MexcApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl)
            };
        }

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
                // Получаем список всех фьючерсных контрактов
                var contractsResponse = await _httpClient.GetFromJsonAsync<MexcContractsResponse>("/api/v1/contract/detail");

                if (contractsResponse?.Data == null)
                {
                    throw new Exception("Ошибка при получении списка контрактов с MEXC");
                }

                // Фильтруем только USDT контракты
                var usdtContracts = contractsResponse.Data
                    .Where(c => c.Symbol.EndsWith("_USDT", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Получаем текущие цены для всех контрактов
                var tickersResponse = await _httpClient.GetFromJsonAsync<MexcTickersResponse>("/api/v1/contract/ticker");

                if (tickersResponse?.Data == null)
                {
                    throw new Exception("Ошибка при получении тикеров с MEXC");
                }

                // Создаем словарь цен для быстрого поиска
                var priceDict = tickersResponse.Data.ToDictionary(t => t.Symbol, t => t.LastPrice);

                // Обрабатываем каждый контракт
                foreach (var contract in usdtContracts)
                {
                    try
                    {
                        // Получаем текущую цену
                        if (!priceDict.TryGetValue(contract.Symbol, out var currentPrice) || currentPrice == 0)
                        {
                            continue;
                        }

                        // Получаем funding rate для конкретного символа
                        var fundingRateResponse = await _httpClient.GetFromJsonAsync<MexcFundingRateResponse>(
                            $"/api/v1/contract/funding_rate/{contract.Symbol}");

                        if (fundingRateResponse?.Data == null || !fundingRateResponse.Data.Any())
                        {
                            continue;
                        }

                        var fundingRateInfo = fundingRateResponse.Data.First();

                        // Извлекаем базовый символ (например, из "BTC_USDT" получаем "BTC")
                        var baseSymbol = contract.Symbol.Replace("_USDT", "", StringComparison.OrdinalIgnoreCase);
                        
                        // Форматируем пару в стандартный вид (BTCUSDT)
                        var standardPair = contract.Symbol.Replace("_", "");

                        var signal = new FundingRateSignal(
                            ExchangeName: ExchangeName,
                            Symbol: baseSymbol,
                            Pair: standardPair,
                            CurrentPrice: currentPrice,
                            FundingRate: fundingRateInfo.FundingRate,
                            Timestamp: DateTime.UtcNow
                        );

                        signals.Add(signal);
                    }
                    catch (Exception ex)
                    {
                        // Логируем ошибку для конкретного символа, но продолжаем обработку остальных
                        Console.WriteLine($"Ошибка при обработке символа {contract.Symbol}: {ex.Message}");
                    }
                }

                return signals;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при получении funding rates с MEXC: {ex.Message}", ex);
            }
        }

        // Модели для десериализации ответов MEXC API
        private class MexcContractsResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("data")]
            public List<MexcContract> Data { get; set; } = new();
        }

        private class MexcContract
        {
            [JsonPropertyName("symbol")]
            public string Symbol { get; set; } = string.Empty;

            [JsonPropertyName("displayName")]
            public string DisplayName { get; set; } = string.Empty;
        }

        private class MexcTickersResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("data")]
            public List<MexcTicker> Data { get; set; } = new();
        }

        private class MexcTicker
        {
            [JsonPropertyName("symbol")]
            public string Symbol { get; set; } = string.Empty;

            [JsonPropertyName("lastPrice")]
            public decimal LastPrice { get; set; }
        }

        private class MexcFundingRateResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("data")]
            public List<MexcFundingRate> Data { get; set; } = new();
        }

        private class MexcFundingRate
        {
            [JsonPropertyName("symbol")]
            public string Symbol { get; set; } = string.Empty;

            [JsonPropertyName("fundingRate")]
            public decimal FundingRate { get; set; }

            [JsonPropertyName("settleTime")]
            public long SettleTime { get; set; }
        }
    }
}