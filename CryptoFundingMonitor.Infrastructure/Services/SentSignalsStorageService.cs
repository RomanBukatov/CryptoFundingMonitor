using System.Text.Json;
using CryptoFundingMonitor.Core.Models;
using CryptoFundingMonitor.Core.Services;

namespace CryptoFundingMonitor.Infrastructure.Services
{
    /// <summary>
    /// Сервис для хранения отправленных сигналов в JSON файле
    /// </summary>
    public class SentSignalsStorageService : ISentSignalsStorageService
    {
        private const string StorageFileName = "sent_signals.json";
        private readonly string _storagePath;
        private List<SentSignalRecord> _sentSignals;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Конструктор сервиса
        /// </summary>
        public SentSignalsStorageService()
        {
            // Используем папку приложения для хранения файла
            _storagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StorageFileName);
            _sentSignals = new List<SentSignalRecord>();
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Загружаем данные при инициализации
            _ = LoadSentSignalsAsync();
        }

        /// <summary>
        /// Загрузить все отправленные сигналы из хранилища
        /// </summary>
        public async Task<List<SentSignalRecord>> LoadSentSignalsAsync()
        {
            try
            {
                if (!File.Exists(_storagePath))
                {
                    _sentSignals = new List<SentSignalRecord>();
                    return _sentSignals;
                }

                var jsonContent = await File.ReadAllTextAsync(_storagePath);
                _sentSignals = JsonSerializer.Deserialize<List<SentSignalRecord>>(jsonContent, _jsonOptions) ?? new List<SentSignalRecord>();

                return _sentSignals;
            }
            catch (Exception ex)
            {
                // В случае ошибки создаем пустой список
                _sentSignals = new List<SentSignalRecord>();
                return _sentSignals;
            }
        }

        /// <summary>
        /// Сохранить отправленный сигнал в хранилище
        /// </summary>
        public async Task SaveSentSignalAsync(SentSignalRecord record)
        {
            try
            {
                // Ищем существующую запись
                var existingRecord = _sentSignals.FirstOrDefault(r => r.GetKey() == record.GetKey());

                if (existingRecord != null)
                {
                    // Обновляем время отправки
                    existingRecord.LastSentTime = record.LastSentTime;
                }
                else
                {
                    // Добавляем новую запись
                    _sentSignals.Add(record);
                }

                // Сохраняем в файл
                var jsonContent = JsonSerializer.Serialize(_sentSignals, _jsonOptions);
                await File.WriteAllTextAsync(_storagePath, jsonContent);
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем выполнение
                Console.WriteLine($"Ошибка при сохранении сигнала: {ex.Message}");
            }
        }

        /// <summary>
        /// Проверить, можно ли отправить сигнал для указанной пары
        /// </summary>
        public async Task<bool> CanSendSignalAsync(string exchangeName, string symbol, int intervalHours = 8)
        {
            try
            {
                var key = $"{exchangeName}-{symbol}";
                var existingRecord = _sentSignals.FirstOrDefault(r => r.GetKey() == key);

                if (existingRecord == null)
                {
                    // Нет записи - можно отправить сигнал
                    return true;
                }

                // Проверяем интервал времени
                return existingRecord.CanSendSignal(intervalHours);
            }
            catch (Exception ex)
            {
                // В случае ошибки разрешаем отправку сигнала
                Console.WriteLine($"Ошибка при проверке сигнала: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Очистить все записи старше указанного количества часов
        /// </summary>
        public async Task CleanupOldRecordsAsync(int hoursToKeep = 24)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-hoursToKeep);
                var initialCount = _sentSignals.Count;

                _sentSignals.RemoveAll(r => r.LastSentTime < cutoffTime);

                if (_sentSignals.Count != initialCount)
                {
                    // Сохраняем изменения в файл
                    var jsonContent = JsonSerializer.Serialize(_sentSignals, _jsonOptions);
                    await File.WriteAllTextAsync(_storagePath, jsonContent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при очистке старых записей: {ex.Message}");
            }
        }
    }
}