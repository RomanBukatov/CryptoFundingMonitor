using CryptoFundingMonitor.Core.Models;

namespace CryptoFundingMonitor.Core.Services
{
    /// <summary>
    /// Интерфейс сервиса для хранения отправленных сигналов
    /// </summary>
    public interface ISentSignalsStorageService
    {
        /// <summary>
        /// Загрузить все отправленные сигналы из хранилища
        /// </summary>
        /// <returns>Список записей об отправленных сигналах</returns>
        Task<List<SentSignalRecord>> LoadSentSignalsAsync();

        /// <summary>
        /// Сохранить отправленный сигнал в хранилище
        /// </summary>
        /// <param name="record">Запись об отправленном сигнале</param>
        Task SaveSentSignalAsync(SentSignalRecord record);

        /// <summary>
        /// Проверить, можно ли отправить сигнал для указанной пары
        /// </summary>
        /// <param name="exchangeName">Название биржи</param>
        /// <param name="symbol">Символ пары</param>
        /// <param name="intervalHours">Интервал в часах (по умолчанию 8)</param>
        /// <returns>true если можно отправить сигнал</returns>
        Task<bool> CanSendSignalAsync(string exchangeName, string symbol, int intervalHours = 8);

        /// <summary>
        /// Очистить все записи старше указанного количества часов
        /// </summary>
        /// <param name="hoursToKeep">Количество часов для хранения записей</param>
        Task CleanupOldRecordsAsync(int hoursToKeep = 24);
    }
}