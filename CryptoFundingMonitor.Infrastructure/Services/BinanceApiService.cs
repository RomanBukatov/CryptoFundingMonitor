using Binance.Net.Clients;
using Binance.Net.Enums;
using CryptoFundingMonitor.Core.Models;
using CryptoFundingMonitor.Core.Services;
using CryptoExchange.Net.Authentication;
using System.Globalization;

namespace CryptoFundingMonitor.Infrastructure.Services
{
    /// <summary>
    /// Модель ответа от эндпоинта /fapi/v1/premiumIndex
    /// </summary>
    public class BinancePremiumIndex
    {
        public string Symbol { get; set; } = string.Empty;
        public string MarkPrice { get; set; } = string.Empty;
        public string IndexPrice { get; set; } = string.Empty;
        public string EstimatedSettlePrice { get; set; } = string.Empty;
        public string LastFundingRate { get; set; } = string.Empty;
        public long NextFundingTime { get; set; }
        public string InterestRate { get; set; } = string.Empty;
        public long Time { get; set; }

        /// <summary>
        /// Получает funding rate как decimal
        /// </summary>
        public decimal GetLastFundingRateDecimal()
        {
            if (decimal.TryParse(LastFundingRate, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate))
            {
                return rate;
            }
            return 0;
        }

        /// <summary>
        /// Получает mark price как decimal
        /// </summary>
        public decimal GetMarkPriceDecimal()
        {
            if (decimal.TryParse(MarkPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            {
                return price;
            }
            return 0;
        }
    }
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
            var startTime = DateTime.UtcNow;
            Console.WriteLine($"[BINANCE API] === НАЧАЛО ПОЛУЧЕНИЯ ДАННЫХ С BINANCE ===");
            Console.WriteLine($"[BINANCE API] apiKey length: {apiKey.Length}");
            Console.WriteLine($"[BINANCE API] apiSecret length: {apiSecret.Length}");
            Console.WriteLine($"[BINANCE API] apiKey пустой: {string.IsNullOrEmpty(apiKey)}");
            Console.WriteLine($"[BINANCE API] apiSecret пустой: {string.IsNullOrEmpty(apiSecret)}");

            var signals = new List<FundingRateSignal>();

            try
            {
                Console.WriteLine($"[BINANCE API] Создаем клиента Binance");
                Console.WriteLine($"[BINANCE API] API Key: {apiKey.Substring(0, Math.Min(8, apiKey.Length))}...");
                Console.WriteLine($"[BINANCE API] API Secret: {apiSecret.Substring(0, Math.Min(8, apiSecret.Length))}...");

                // Создаем клиента Binance с учетными данными
                using var client = new BinanceRestClient(options =>
                {
                    if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret))
                    {
                        Console.WriteLine($"[BINANCE API] Устанавливаем API credentials");
                        options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
                        Console.WriteLine($"[BINANCE API] Credentials установлены успешно");
                    }
                    else
                    {
                        Console.WriteLine($"[BINANCE API] Пропускаем установку API credentials - ключи пустые");
                    }

                    // Устанавливаем таймаут ожидания ответа в 30 секунд
                    options.RequestTimeout = TimeSpan.FromSeconds(30);
                    Console.WriteLine($"[BINANCE API] Установлен таймаут запроса: 30 секунд");
                });

                Console.WriteLine($"[BINANCE API] Клиент Binance создан успешно");

                // Предварительный запрос для "разогрева" соединения
                Console.WriteLine($"[BINANCE API] Выполняем предварительный запрос для инициализации соединения...");
                var warmupStart = DateTime.UtcNow;
                try
                {
                    var warmupResult = await client.UsdFuturesApi.ExchangeData.GetTickersAsync();
                    if (warmupResult.Success)
                    {
                        var warmupTime = DateTime.UtcNow - warmupStart;
                        Console.WriteLine($"[BINANCE API] Предварительный запрос выполнен успешно за {warmupTime.TotalSeconds:F2} сек");
                    }
                    else
                    {
                        Console.WriteLine($"[BINANCE API] Предварительный запрос вернул ошибку: {warmupResult.Error?.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BINANCE API] Предварительный запрос не удался: {ex.Message}");
                }

                Console.WriteLine($"[BINANCE API] Получаем информацию о всех фьючерсных парах USDT");
                Console.WriteLine($"[BINANCE API] Вызываем client.UsdFuturesApi.ExchangeData.GetTickersAsync()");

                var tickersStart = DateTime.UtcNow;

                // Получаем информацию о всех фьючерсных парах USDT с повторными попытками
                var symbolsResult = await client.UsdFuturesApi.ExchangeData.GetTickersAsync();
                var tickersTime = DateTime.UtcNow - tickersStart;

                // Если первый запрос не удался, пробуем еще раз (может помочь с задержками API)
                if (!symbolsResult.Success && tickersTime.TotalSeconds < 25) // Если прошло меньше 25 сек и есть ошибка
                {
                    Console.WriteLine($"[BINANCE API] Первый запрос не удался, пробуем еще раз...");
                    await Task.Delay(1000); // Ждем 1 секунду перед повторной попыткой

                    var retryStart = DateTime.UtcNow;
                    symbolsResult = await client.UsdFuturesApi.ExchangeData.GetTickersAsync();
                    var retryTime = DateTime.UtcNow - retryStart;

                    Console.WriteLine($"[BINANCE API] Повторный запрос выполнен за {retryTime.TotalSeconds:F2} сек");
                    tickersTime = DateTime.UtcNow - tickersStart; // Обновляем общее время
                }

                Console.WriteLine($"[BINANCE API] GetTickersAsync() выполнен за {tickersTime.TotalSeconds:F2} сек");

                Console.WriteLine($"[BINANCE API] Результат Success: {symbolsResult.Success}");
                Console.WriteLine($"[BINANCE API] Error: {symbolsResult.Error?.Message}");

                if (!symbolsResult.Success)
                {
                    Console.WriteLine($"[BINANCE API] ОШИБКА при получении списка символов: {symbolsResult.Error?.Message}");
                    Console.WriteLine($"[BINANCE API] Error Code: {symbolsResult.Error?.Code}");
                    throw new Exception($"Ошибка при получении списка символов с Binance: {symbolsResult.Error?.Message}");
                }

                Console.WriteLine($"[BINANCE API] Получено {symbolsResult.Data.Length} символов");
                Console.WriteLine($"[BINANCE API] Первые 5 символов для проверки:");
                for (int i = 0; i < Math.Min(5, symbolsResult.Data.Length); i++)
                {
                    Console.WriteLine($"[BINANCE API]   {i+1}. {symbolsResult.Data[i].Symbol} - {symbolsResult.Data[i].LastPrice}");
                }

                var usdtSymbols = symbolsResult.Data
                    .Where(t => t.Symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Console.WriteLine($"[BINANCE API] Найдено {usdtSymbols.Count} USDT пар");

                var processingStart = DateTime.UtcNow;
                Console.WriteLine($"[BINANCE API] Начинаем обработку funding rates для {usdtSymbols.Count} пар");

                // Обрабатываем каждый символ
                foreach (var ticker in usdtSymbols)
                {
                    Console.WriteLine($"[BINANCE API] Обрабатываем символ: {ticker.Symbol}, LastPrice: {ticker.LastPrice}");

                    try
                    {
                        // Получаем текущую цену
                        var currentPrice = ticker.LastPrice;
                        if (currentPrice == 0)
                        {
                            Console.WriteLine($"[BINANCE API] Пропускаем {ticker.Symbol} - цена равна 0");
                            continue;
                        }

                        Console.WriteLine($"[BINANCE API] Получаем funding rate для {ticker.Symbol}");
                        // Получаем funding rate для конкретного символа
                        var fundingRateResult = await client.UsdFuturesApi.ExchangeData.GetFundingRatesAsync(ticker.Symbol);

                        if (!fundingRateResult.Success || !fundingRateResult.Data.Any())
                        {
                            Console.WriteLine($"[BINANCE API] Не удалось получить funding rate для {ticker.Symbol}: {fundingRateResult.Error?.Message}");
                            continue;
                        }

                        var fundingRateInfo = fundingRateResult.Data.First();

                        // Извлекаем базовый символ (например, из "BTCUSDT" получаем "BTC")
                        var baseSymbol = ticker.Symbol.Replace("USDT", "", StringComparison.OrdinalIgnoreCase);

                        // Логируем funding rate для диагностики
                        Console.WriteLine($"DEBUG: {ticker.Symbol} - FundingRate: {fundingRateInfo.FundingRate}, Type: {fundingRateInfo.FundingRate.GetType()}");

                        signals.Add(new FundingRateSignal(
                            ExchangeName: ExchangeName,
                            Symbol: baseSymbol,
                            Pair: ticker.Symbol,
                            CurrentPrice: currentPrice,
                            FundingRate: fundingRateInfo.FundingRate,
                            TakeProfitPrice: null,
                            Timestamp: DateTime.UtcNow
                        ));

                        var signal = new FundingRateSignal(
                            ExchangeName: ExchangeName,
                            Symbol: baseSymbol,
                            Pair: ticker.Symbol,
                            CurrentPrice: currentPrice,
                            FundingRate: fundingRateInfo.FundingRate,
                            TakeProfitPrice: null,
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

                var processingTime = DateTime.UtcNow - processingStart;
                var totalTime = DateTime.UtcNow - startTime;

                Console.WriteLine($"[BINANCE API] Создано {signals.Count} сигналов funding rate");
                Console.WriteLine($"[BINANCE API] Время обработки funding rates: {processingTime.TotalSeconds:F2} сек");
                Console.WriteLine($"[BINANCE API] Общее время выполнения: {totalTime.TotalSeconds:F2} сек");

                // Логируем количество полученных пар и примеры ставок для отладки
                Console.WriteLine($"[BINANCE API] Получено {signals.Count} пар от Binance.");
                if (signals.Any())
                {
                    Console.WriteLine($"[BINANCE API] Данные будут отсортированы по алфавиту в MainViewModel перед отправкой");
                    Console.WriteLine($"[BINANCE API] Пример первых 5 пар (до сортировки): {string.Join(", ", signals.Take(5).Select(s => $"{s.Pair} - Price: {s.CurrentPrice:F4}"))}");
                }

                Console.WriteLine($"[BINANCE API] === УСПЕШНОЕ ЗАВЕРШЕНИЕ ПОЛУЧЕНИЯ ДАННЫХ С BINANCE ===");
                Console.WriteLine($"[BINANCE API] Производительность: {signals.Count} пар за {totalTime.TotalSeconds:F2} сек ({signals.Count / totalTime.TotalSeconds:F1} пар/сек)");
                return signals;
            }
            catch (Exception ex)
            {
                var totalTime = DateTime.UtcNow - startTime;
                Console.WriteLine($"[ERROR] Произошла ошибка при получении данных от Binance: {ex.Message}");
                Console.WriteLine($"[ERROR] Время выполнения до ошибки: {totalTime.TotalSeconds:F2} сек");
                Console.WriteLine($"[BINANCE API] Stack trace: {ex.StackTrace}");
                throw new Exception($"Ошибка при получении funding rates с Binance: {ex.Message}", ex);
            }
        }
    }
}