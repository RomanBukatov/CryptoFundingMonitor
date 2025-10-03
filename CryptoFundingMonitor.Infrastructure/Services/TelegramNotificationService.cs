using CryptoFundingMonitor.Core.Models;
using CryptoFundingMonitor.Core.Services;
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
        private readonly ITelegramBotClient _botClient;

        /// <summary>
        /// Инициализирует новый экземпляр TelegramNotificationService
        /// </summary>
        /// <param name="botToken">Токен Telegram бота</param>
        public TelegramNotificationService(string botToken)
        {
            _botClient = new TelegramBotClient(botToken);
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
                var message = FormatMessage(signal, tradeBotUrl);

                await _botClient.SendMessage(
                    chatId: new ChatId(chatId),
                    text: message,
                    parseMode: ParseMode.Markdown,
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }
                );
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при отправке уведомления в Telegram: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Форматирует сообщение согласно требованиям ТЗ
        /// </summary>
        private string FormatMessage(FundingRateSignal signal, string tradeBotUrl)
        {
            // Получаем ссылку на биржу
            var exchangeUrl = GetExchangeUrl(signal.ExchangeName, signal.Pair);
            
            // Получаем ссылку на CoinGlass
            var coinGlassUrl = GetCoinGlassUrl(signal.ExchangeName, signal.Pair);

            // Форматируем временную метку
            var timestamp = signal.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

            // Формируем сообщение с Markdown разметкой
            var message = $"⚫️ [{signal.ExchangeName}]({exchangeUrl}) - [{signal.Symbol}]({coinGlassUrl}) - {signal.Pair}\n";
            message += "🟢 Analyzing Buy ⬆️\n";
            message += $"🅿️ {signal.CurrentPrice:F4}\n";
            message += $"📃 {signal.FundingRate:F4}\n";

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
                "MEXC" => $"https://futures.mexc.com/exchange/{pair.Replace("USDT", "_USDT")}",
                _ => $"https://www.{exchangeName.ToLower()}.com"
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