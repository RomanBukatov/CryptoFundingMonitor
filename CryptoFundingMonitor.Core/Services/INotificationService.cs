using CryptoFundingMonitor.Core.Models;

namespace CryptoFundingMonitor.Core.Services
{
    /// <summary>
    /// Интерфейс для отправки уведомлений
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Отправляет уведомление о сигнале ставки финансирования
        /// </summary>
        /// <param name="signal">Сигнал ставки финансирования</param>
        /// <param name="chatId">ID чата в Telegram</param>
        /// <param name="tradeBotUrl">URL торгового бота</param>
        /// <returns>Задача отправки уведомления</returns>
        Task SendNotificationAsync(FundingRateSignal signal, long chatId, string tradeBotUrl);
    }
}