using CryptoFundingMonitor.Core.Models;
using CryptoFundingMonitor.Core.Services;
using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CryptoFundingMonitor.Infrastructure.Services
{
    /// <summary>
    /// Сервис для отправки уведомлений в Telegram
    /// </summary>
    public class TelegramNotificationService : INotificationService
    {
        private ITelegramBotClient _botClient;
        private string _botToken;

        /// <summary>
        /// Инициализирует новый экземпляр TelegramNotificationService
        /// </summary>
        public TelegramNotificationService()
        {
            // Конструктор без параметров для DI
        }

        /// <summary>
        /// Инициализирует сервис с токеном бота
        /// </summary>
        /// <param name="botToken">Токен Telegram бота</param>
        public void Initialize(string botToken)
        {
            _botToken = botToken ?? throw new ArgumentNullException(nameof(botToken));
            _botClient = new TelegramBotClient(_botToken);
            Debug.WriteLine($"[TelegramNotificationService] Инициализирован");
        }

        /// <summary>
        /// Отправляет уведомление о сигнале ставки финансирования в Telegram
        /// </summary>
        /// <param name="signal">Сигнал ставки финансирования</param>
        /// <param name="chatId">ID чата в Telegram</param>
        /// <param name="tradeBotUrl">URL торгового бота (опционально)</param>
        public async Task SendNotificationAsync(FundingRateSignal signal, long chatId, string tradeBotUrl)
        {
            try
            {
                if (_botClient == null)
                {
                    throw new InvalidOperationException("TelegramNotificationService не инициализирован. Вызовите Initialize() перед использованием.");
                }

                var message = FormatMessage(signal, tradeBotUrl);

                Debug.WriteLine($"[TelegramNotificationService] Отправка сообщения в чат {chatId}");
                Debug.WriteLine($"[TelegramNotificationService] Текст сообщения:\n{message}");

                await _botClient.SendMessage(
                    chatId: new ChatId(chatId),
                    text: message,
                    parseMode: ParseMode.Markdown,
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }
                );

                Debug.WriteLine($"[TelegramNotificationService] Сообщение успешно отправлено в чат {chatId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TelegramNotificationService] ОШИБКА при отправке: {ex.Message}");
                Debug.WriteLine($"[TelegramNotificationService] Stack trace: {ex.StackTrace}");
                throw new Exception($"Ошибка при отправке уведомления в Telegram: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Форматирует сообщение согласно требованиям ТЗ
        /// </summary>
        private string FormatMessage(FundingRateSignal signal, string tradeBotUrl)
        {
            // Получаем эмодзи для биржи
            var exchangeEmoji = GetExchangeEmoji(signal.ExchangeName);

            // Получаем ссылку на биржу
            var exchangeUrl = GetExchangeUrl(signal.ExchangeName, signal.Pair);

            // Получаем ссылку на CoinGlass
            var coinGlassUrl = GetCoinGlassUrl(signal.ExchangeName, signal.Pair);

            // Форматируем временную метку
            var timestamp = signal.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

            // Формируем сообщение с Markdown разметкой
            // Название биржи кликабельно → ведет на биржу
            // Название монеты кликабельно → ведет на CoinGlass
            // Пара справа кликабельна → копирует название монеты
            // Убираем подчеркивание из пары для корректного отображения при копировании
            var displayPair = signal.Pair.Replace("_", "");
            var message = $"{exchangeEmoji} [{signal.ExchangeName}]({exchangeUrl}) - [{signal.Symbol}]({coinGlassUrl}) - `{displayPair}`\n";

            // Динамическое определение сигнала на основе FundingRate (инвертированная логика)
            var signalText = signal.FundingRate < 0
                ? "🟢 Analyzing Buy ⬆️"
                : "🔴 Analyzing Sell ⬇️";
            message += $"{signalText}\n";
            message += $"🅿️ {signal.CurrentPrice:F4}\n";

            // Добавляем Take Profit только для BUY сигналов
            if (signal.TakeProfitPrice.HasValue)
            {
                message += $"✅ Take Profit: {signal.TakeProfitPrice.Value:F4}\n";
            }

            // Добавляем ссылку Trade только если URL предоставлен
            if (!string.IsNullOrWhiteSpace(tradeBotUrl))
            {
                message += $"[🌐 Trade]({tradeBotUrl})\n";
            }

            message += $"Since: {timestamp}";

            return message;
        }

        /// <summary>
        /// Возвращает URL графика на сайте биржи
        /// </summary>
        private string GetExchangeUrl(string exchangeName, string pair)
        {
            return exchangeName.ToUpper() switch
            {
                "BINANCE" => $"https://www.binance.com/en/futures/{pair}",
                "BYBIT" => $"https://www.bybit.com/trade/usdt/{pair}",
                "MEXC" => $"https://futures.mexc.com/exchange/{pair}",
                _ => $"https://www.{exchangeName.ToLower()}.com"
            };
        }

        /// <summary>
        /// Возвращает эмодзи для биржи
        /// </summary>
        private string GetExchangeEmoji(string exchangeName)
        {
            return exchangeName.ToUpper() switch
            {
                "BINANCE" => "🟡", // Желтый шарик для Binance
                "BYBIT" => "⚫️",   // Черный шарик для Bybit
                "MEXC" => "🔵",     // Синий шарик для Mexc
                _ => "⚫️"         // По умолчанию черный шарик
            };
        }

        /// <summary>
        /// Возвращает URL графика на CoinGlass
        /// </summary>
        private string GetCoinGlassUrl(string exchangeName, string pair)
        {
            // CoinGlass использует формат: https://www.coinglass.com/tv/Binance_BTCUSDT
            var formattedExchange = exchangeName switch
            {
                "MEXC" => "MEXC",
                _ => char.ToUpper(exchangeName[0]) + exchangeName.Substring(1).ToLower()
            };

            return $"https://www.coinglass.com/tv/{formattedExchange}_{pair}";
        }
    }
}