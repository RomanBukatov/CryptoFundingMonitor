using System.Windows.Media;
 using CommunityToolkit.Mvvm.ComponentModel;
 using CommunityToolkit.Mvvm.Input;
 using CryptoFundingMonitor.Core.Services;
 using CryptoFundingMonitor.Core.Models;
 using CryptoFundingMonitor.Infrastructure.Services;
 using System.Text.Json;
 using System.Diagnostics;
 using System.Windows;
 using Microsoft.Extensions.DependencyInjection;

namespace CryptoFundingMonitor.WPF.ViewModels
{
    /// <summary>
    /// MainViewModel для управления мониторингом Funding Rates
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        /// <summary>
        /// Конструктор с внедрением зависимостей
        /// </summary>
        /// <param name="serviceProvider">Провайдер сервисов</param>
        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }
        #region Пороговые значения Funding Rate для бирж

        [ObservableProperty]
        private decimal _binanceThreshold = -0.1m;

        [ObservableProperty]
        private decimal _bybitThreshold = -0.1m;

        [ObservableProperty]
        private decimal _mexcThreshold = -0.1m;

        #endregion

        #region API ключи для бирж

        [ObservableProperty]
        private string _binanceApiKey = string.Empty;

        [ObservableProperty]
        private string _binanceApiSecret = string.Empty;

        [ObservableProperty]
        private string _bybitApiKey = string.Empty;

        [ObservableProperty]
        private string _mexcApiKey = string.Empty;

        #endregion

        #region Чекбоксы включения бирж

        [ObservableProperty]
        private bool _isBinanceEnabled = true;

        [ObservableProperty]
        private bool _isBybitEnabled = true;

        [ObservableProperty]
        private bool _isMexcEnabled = true;

        #endregion

        #region Telegram каналы

        [ObservableProperty]
        private string _telegramBotToken = string.Empty;

        [ObservableProperty]
        private string _telegramChannel1Id = string.Empty;

        [ObservableProperty]
        private bool _isTelegramChannel1Enabled = false;

        [ObservableProperty]
        private string _telegramChannel2Id = string.Empty;

        [ObservableProperty]
        private bool _isTelegramChannel2Enabled = false;

        #endregion

        #region Trade Bot

        [ObservableProperty]
        private string _tradeBotUrl = string.Empty;

        [ObservableProperty]
        private bool _isTradeBotEnabled = false;

        #endregion

        #region Таймер времени работы программы

        private DateTime _programStartTime;
        private System.Timers.Timer _runtimeTimer;
        private bool _isRuntimeTimerRunning = false;

        [ObservableProperty]
        private string _runtimeDisplay = "00:00:00";

        [ObservableProperty]
        private Brush _runtimeIndicatorColor = Brushes.Gray;

        /// <summary>
        /// Признак активности таймера времени работы
        /// </summary>
        public bool IsRuntimeTimerRunning
        {
            get => _isRuntimeTimerRunning;
            private set
            {
                if (SetProperty(ref _isRuntimeTimerRunning, value))
                {
                    UpdateRuntimeIndicator();
                }
            }
        }

        #endregion

        #region Сервисы

        private readonly IServiceProvider _serviceProvider;
        private IBrokerApiService _binanceService;
        private IBrokerApiService _bybitService;
        private IBrokerApiService _mexcService;
        private INotificationService _notificationService;
        private ISentSignalsStorageService _sentSignalsStorageService;
        private CancellationTokenSource _cancellationTokenSource;

        #endregion

        #region Управление отправленными сигналами

        /// <summary>
        /// Хранилище уже отправленных сигналов для предотвращения спама
        /// Ключ формируется в формате "ExchangeName-Symbol"
        /// </summary>
        private HashSet<string> signaledPairs = new HashSet<string>();

        #endregion

        #region Индикатор статуса

        [ObservableProperty]
        private Brush _statusIndicatorColor = Brushes.Red;

        #endregion

        #region Состояние мониторинга

        private bool _isMonitoring = false;

        /// <summary>
        /// Признак активности мониторинга
        /// </summary>
        public bool IsMonitoring
        {
            get => _isMonitoring;
            private set
            {
                if (SetProperty(ref _isMonitoring, value))
                {
                    UpdateStatusIndicator();
                }
            }
        }

        #endregion

        #region Команды

        /// <summary>
        /// Команда для запуска/остановки мониторинга
        /// </summary>
        [RelayCommand]
        private async Task ToggleMonitoringAsync()
        {
            if (IsMonitoring)
            {
                await StopMonitoringAsync();
            }
            else
            {
                await StartMonitoringAsync();
            }
        }

        #endregion

        #region Методы таймера времени работы

        /// <summary>
        /// Запуск таймера времени работы программы
        /// </summary>
        private void StartRuntimeTimer()
        {
            try
            {
                _programStartTime = DateTime.Now;
                _runtimeTimer = new System.Timers.Timer(1000); // Обновление каждую секунду
                _runtimeTimer.Elapsed += (sender, e) => UpdateRuntimeDisplay();
                _runtimeTimer.Start();

                IsRuntimeTimerRunning = true;
                RuntimeDisplay = "00:00:00";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при запуске таймера времени работы: {ex.Message}");
                ShowValidationError($"Произошла ошибка при запуске таймера: {ex.Message}");
            }
        }

        /// <summary>
        /// Остановка таймера времени работы программы
        /// </summary>
        private void StopRuntimeTimer()
        {
            try
            {
                if (_runtimeTimer != null)
                {
                    _runtimeTimer.Stop();
                    _runtimeTimer.Dispose();
                    _runtimeTimer = null;
                }

                IsRuntimeTimerRunning = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при остановке таймера времени работы: {ex.Message}");
            }
        }

        /// <summary>
        /// Обновление отображения времени работы
        /// </summary>
        private void UpdateRuntimeDisplay()
        {
            try
            {
                if (IsRuntimeTimerRunning)
                {
                    var elapsed = DateTime.Now - _programStartTime;
                    RuntimeDisplay = elapsed.ToString(@"hh\:mm\:ss");

                    // Обновляем цвет индикатора в UI потоке
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RuntimeIndicatorColor = Brushes.Green;
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при обновлении отображения времени работы: {ex.Message}");
            }
        }

        /// <summary>
        /// Обновление цвета индикатора таймера времени работы
        /// </summary>
        private void UpdateRuntimeIndicator()
        {
            RuntimeIndicatorColor = IsRuntimeTimerRunning ? Brushes.Green : Brushes.Gray;
        }

        #endregion

        #region Методы

        /// <summary>
        /// Запуск мониторинга
        /// </summary>
        private async Task StartMonitoringAsync()
        {
            try
            {
                // Валидация входных данных перед запуском
                ValidateMonitoringSettings();

                // Создаем CancellationTokenSource для управления жизненным циклом задачи
                _cancellationTokenSource = new CancellationTokenSource();

                // Получаем экземпляры сервисов бирж из DI контейнера
                _binanceService = _serviceProvider.GetRequiredService<BinanceApiService>();
                _bybitService = _serviceProvider.GetRequiredService<BybitApiService>();
                _mexcService = _serviceProvider.GetRequiredService<MexcApiService>();

                // Получаем сервис уведомлений из DI контейнера
                _notificationService = _serviceProvider.GetRequiredService<TelegramNotificationService>();

                // Получаем сервис хранения отправленных сигналов из DI контейнера
                _sentSignalsStorageService = _serviceProvider.GetRequiredService<ISentSignalsStorageService>();

                // Инициализируем Telegram сервис с токеном бота
                if (_notificationService is TelegramNotificationService telegramService)
                {
                    telegramService.Initialize(TelegramBotToken);
                }

                // Логируем статус всех бирж перед запуском
                Debug.WriteLine($"[STARTUP] Статус бирж перед запуском:");
                Debug.WriteLine($"[STARTUP] IsBinanceEnabled: {IsBinanceEnabled}");
                Debug.WriteLine($"[STARTUP] IsBybitEnabled: {IsBybitEnabled}");
                Debug.WriteLine($"[STARTUP] IsMexcEnabled: {IsMexcEnabled}");

                // Проверяем, что хотя бы одна биржа активна
                if (!IsBinanceEnabled && !IsBybitEnabled && !IsMexcEnabled)
                {
                    throw new InvalidOperationException("Необходимо включить хотя бы одну биржу для мониторинга");
                }

                // Проверяем, что хотя бы один канал Telegram активен
                if (!IsTelegramChannel1Enabled && !IsTelegramChannel2Enabled)
                {
                    throw new InvalidOperationException("Необходимо включить хотя бы один Telegram канал для отправки уведомлений");
                }

                // Проверяем настройки активных каналов
                if (IsTelegramChannel1Enabled && string.IsNullOrEmpty(TelegramChannel1Id))
                {
                    throw new InvalidOperationException("Необходимо указать ID для Канала 1");
                }

                if (IsTelegramChannel2Enabled && string.IsNullOrEmpty(TelegramChannel2Id))
                {
                    throw new InvalidOperationException("Необходимо указать ID для Канала 2");
                }

                // Устанавливаем флаг мониторинга ПЕРЕД запуском цикла
                IsMonitoring = true;

                // Запускаем фоновую задачу мониторинга (WPF-friendly подход)
                _ = MonitoringLoopAsync(_cancellationTokenSource.Token);

                // Запускаем таймер времени работы программы
                StartRuntimeTimer();
            }
            catch (InvalidOperationException ex)
            {
                // Показываем пользователю понятное сообщение об ошибке валидации
                ShowValidationError(ex.Message);
                IsMonitoring = false;
            }
            catch (Exception ex)
            {
                // Логируем неожиданные ошибки
                Debug.WriteLine($"Неожиданная ошибка при запуске мониторинга: {ex.Message}");
                ShowValidationError($"Произошла ошибка при запуске мониторинга: {ex.Message}");
                IsMonitoring = false;
            }
        }

        /// <summary>
        /// Валидация настроек мониторинга
        /// </summary>
        private void ValidateMonitoringSettings()
        {
            // Проверяем токен бота
            if (string.IsNullOrEmpty(TelegramBotToken))
            {
                throw new InvalidOperationException("Необходимо указать токен Telegram бота");
            }

            // Проверяем настройки активных бирж
            Debug.WriteLine($"[VALIDATION] Проверяем настройки бирж:");
            Debug.WriteLine($"[VALIDATION] Binance - Enabled: {IsBinanceEnabled}, ApiKey пустой: {string.IsNullOrEmpty(BinanceApiKey)}, ApiSecret пустой: {string.IsNullOrEmpty(BinanceApiSecret)}");

            if (IsBinanceEnabled && (string.IsNullOrEmpty(BinanceApiKey) || string.IsNullOrEmpty(BinanceApiSecret)))
            {
                Debug.WriteLine($"[VALIDATION] ОШИБКА: Binance включена, но ключи не настроены");
                throw new InvalidOperationException("Необходимо указать API ключ и Secret ключ для Binance");
            }
            else if (IsBinanceEnabled)
            {
                Debug.WriteLine($"[VALIDATION] Binance настройки корректны");
            }

            if (IsBybitEnabled && string.IsNullOrEmpty(BybitApiKey))
            {
                throw new InvalidOperationException("Необходимо указать API ключ для Bybit");
            }

            if (IsMexcEnabled && string.IsNullOrEmpty(MexcApiKey))
            {
                throw new InvalidOperationException("Необходимо указать API ключ для MEXC");
            }
        }

        /// <summary>
        /// Показать ошибку валидации пользователю
        /// </summary>
        private void ShowValidationError(string message)
        {
            Debug.WriteLine($"Ошибка валидации: {message}");

            // Показываем сообщение пользователю через MessageBox
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    message,
                    "Ошибка настройки мониторинга",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            });
        }

        /// <summary>
        /// Остановка мониторинга
        /// </summary>
        private async Task StopMonitoringAsync()
        {
            try
            {
                // Отменяем задачу мониторинга
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }

                // Очищаем сервисы (они будут созданы заново при следующем запуске через DI)
                _binanceService = null;
                _bybitService = null;
                _mexcService = null;
                _notificationService = null;
                _sentSignalsStorageService = null;

                // Очищаем HashSet отправленных сигналов для нового цикла мониторинга
                signaledPairs.Clear();

                // Останавливаем таймер времени работы программы
                StopRuntimeTimer();

                IsMonitoring = false;
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем остановку
                Debug.WriteLine($"Ошибка при остановке мониторинга: {ex.Message}");
                IsMonitoring = false;
            }
        }

        /// <summary>
        /// Обновление цвета индикатора статуса
        /// </summary>
        private void UpdateStatusIndicator()
        {
            StatusIndicatorColor = IsMonitoring ? Brushes.Green : Brushes.Red;
        }

        /// <summary>
        /// Основной цикл мониторинга
        /// </summary>
        private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine($"[MONITORING] === ЗАПУСК ОСНОВНОГО ЦИКЛА МОНИТОРИНГА ===");
            Debug.WriteLine($"[MONITORING] IsMonitoring: {IsMonitoring}");
            Debug.WriteLine($"[MONITORING] CancellationToken.IsCancellationRequested: {cancellationToken.IsCancellationRequested}");

            try
            {
                while (IsMonitoring && !cancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine($"[MONITORING] Начинаем итерацию цикла мониторинга");
                    try
                    {
                        // Проверяем активные биржи и отправляем уведомления
                        await CheckAndSendNotificationsAsync();

                        Debug.WriteLine($"[MONITORING] Итерация цикла мониторинга завершена успешно");
                        // Ждем 1 минуту перед следующей проверкой (не захватываем контекст для избежания deadlock)
                        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
                        Debug.WriteLine($"[MONITORING] Ждем следующую итерацию...");
                    }
                    catch (TaskCanceledException)
                    {
                        Debug.WriteLine($"[MONITORING] Задача была отменена - выходим из цикла");
                        // Задача была отменена - выходим из цикла
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MONITORING] Ошибка в цикле мониторинга: {ex.Message}");
                        Debug.WriteLine($"[MONITORING] Stack trace: {ex.StackTrace}");
                        // Продолжаем цикл даже при ошибке (не захватываем контекст)
                        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Критическая ошибка в цикле мониторинга: {ex.Message}");
                IsMonitoring = false;
            }
        }

        /// <summary>
        /// Проверка активных бирж и отправка уведомлений
        /// </summary>
        private async Task CheckAndSendNotificationsAsync()
        {
            try
            {
                var allSignals = new List<FundingRateSignal>();

                // Собираем сигналы от всех активных бирж
                Debug.WriteLine($"[COLLECTION] === НАЧАЛО СБОРА СИГНАЛОВ ОТ ВСЕХ БИРЖ ===");

                // Binance
                if (IsBinanceEnabled && !string.IsNullOrEmpty(BinanceApiKey))
                {
                    try
                    {
                        Debug.WriteLine($"[COLLECTION] НАЧИНАЕМ ПОЛУЧЕНИЕ ДАННЫХ ОТ BINANCE");
                        Debug.WriteLine($"[COLLECTION] Binance API Key length: {BinanceApiKey.Length}");
                        Debug.WriteLine($"[COLLECTION] Binance API Secret length: {BinanceApiSecret.Length}");

                        var binanceSignals = await _binanceService.GetFundingRatesAsync(BinanceApiKey, BinanceApiSecret).ConfigureAwait(false);

                        Debug.WriteLine($"[COLLECTION] Binance вернул {binanceSignals.Count()} сигналов");
                        Debug.WriteLine($"[COLLECTION] Первые 3 сигнала от Binance:");
                        foreach (var signal in binanceSignals.Take(3))
                        {
                            Debug.WriteLine($"[COLLECTION]   {signal.Pair} - {signal.FundingRate:F6}");
                        }

                        var filteredBinanceSignals = binanceSignals.Where(s => BinanceThreshold < 0 ? s.FundingRate <= BinanceThreshold : s.FundingRate >= BinanceThreshold).ToList();
                        allSignals.AddRange(filteredBinanceSignals);

                        Debug.WriteLine($"[COLLECTION] Получено {binanceSignals.Count()} сигналов от Binance, из них {filteredBinanceSignals.Count} подходят под условия (Threshold: {BinanceThreshold})");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[COLLECTION] ОШИБКА при получении данных от Binance: {ex.Message}");
                        Debug.WriteLine($"[COLLECTION] Stack trace: {ex.StackTrace}");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                $"Ошибка Binance API: {ex.Message}\n\nПроверьте настройки API ключей или отключите Binance в настройках.",
                                "Ошибка Binance API",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning
                            );
                        });
                    }
                }
                else
                {
                    Debug.WriteLine($"[COLLECTION] Binance пропущена - IsBinanceEnabled: {IsBinanceEnabled}, ApiKey пустой: {string.IsNullOrEmpty(BinanceApiKey)}");
                }

                // Bybit
                if (IsBybitEnabled && !string.IsNullOrEmpty(BybitApiKey))
                {
                    try
                    {
                        Debug.WriteLine($"[COLLECTION] НАЧИНАЕМ ПОЛУЧЕНИЕ ДАННЫХ ОТ BYBIT");
                        Debug.WriteLine($"[COLLECTION] Bybit API Key length: {BybitApiKey.Length}");

                        var bybitSignals = await _bybitService.GetFundingRatesAsync(BybitApiKey, string.Empty).ConfigureAwait(false);

                        Debug.WriteLine($"[COLLECTION] Bybit вернул {bybitSignals.Count()} сигналов");
                        Debug.WriteLine($"[COLLECTION] Первые 3 сигнала от Bybit:");
                        foreach (var signal in bybitSignals.Take(3))
                        {
                            Debug.WriteLine($"[COLLECTION]   {signal.Pair} - {signal.FundingRate:F6}");
                        }

                        var filteredBybitSignals = bybitSignals.Where(s => BybitThreshold < 0 ? s.FundingRate <= BybitThreshold : s.FundingRate >= BybitThreshold).ToList();
                        allSignals.AddRange(filteredBybitSignals);

                        Debug.WriteLine($"[COLLECTION] Получено {bybitSignals.Count()} сигналов от Bybit, из них {filteredBybitSignals.Count} подходят под условия (Threshold: {BybitThreshold})");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[COLLECTION] ОШИБКА при получении данных от Bybit: {ex.Message}");
                        Debug.WriteLine($"[COLLECTION] Stack trace: {ex.StackTrace}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[COLLECTION] Bybit пропущена - IsBybitEnabled: {IsBybitEnabled}, ApiKey пустой: {string.IsNullOrEmpty(BybitApiKey)}");
                }

                // MEXC
                if (IsMexcEnabled && !string.IsNullOrEmpty(MexcApiKey))
                {
                    try
                    {
                        Debug.WriteLine($"[COLLECTION] НАЧИНАЕМ ПОЛУЧЕНИЕ ДАННЫХ ОТ MEXC");
                        Debug.WriteLine($"[COLLECTION] MEXC API Key length: {MexcApiKey.Length}");

                        var mexcSignals = await _mexcService.GetFundingRatesAsync(MexcApiKey, string.Empty).ConfigureAwait(false);

                        Debug.WriteLine($"[COLLECTION] MEXC вернул {mexcSignals.Count()} сигналов");
                        Debug.WriteLine($"[COLLECTION] Первые 3 сигнала от MEXC:");
                        foreach (var signal in mexcSignals.Take(3))
                        {
                            Debug.WriteLine($"[COLLECTION]   {signal.Pair} - {signal.FundingRate:F6}");
                        }

                        var filteredMexcSignals = mexcSignals.Where(s => MexcThreshold < 0 ? s.FundingRate <= MexcThreshold : s.FundingRate >= MexcThreshold).ToList();
                        allSignals.AddRange(filteredMexcSignals);

                        Debug.WriteLine($"[COLLECTION] Получено {mexcSignals.Count()} сигналов от MEXC, из них {filteredMexcSignals.Count} подходят под условия (Threshold: {MexcThreshold})");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[COLLECTION] ОШИБКА при получении данных от MEXC: {ex.Message}");
                        Debug.WriteLine($"[COLLECTION] Stack trace: {ex.StackTrace}");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                $"MEXC API недоступен или возвращает некорректные данные: {ex.Message}\n\nРекомендуется отключить MEXC в настройках для стабильной работы.",
                                "Ошибка MEXC API",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning
                            );
                        });
                    }
                }
                else
                {
                    Debug.WriteLine($"[COLLECTION] MEXC пропущена - IsMexcEnabled: {IsMexcEnabled}, ApiKey пустой: {string.IsNullOrEmpty(MexcApiKey)}");
                }

                Debug.WriteLine($"[COLLECTION] Всего собрано {allSignals.Count} сигналов от всех бирж");

                // Сортируем все сигналы сначала по бирже, затем по алфавиту для корректного порядка отправки в Telegram
                var sortedSignals = allSignals.OrderBy(s => s.ExchangeName).ThenBy(s => s.Pair).ToList();

                Debug.WriteLine($"[COLLECTION] Сигналы отсортированы по биржам и алфавиту для корректного порядка отправки");
                Debug.WriteLine($"[COLLECTION] Первые 10 пар после сортировки:");
                for (int i = 0; i < Math.Min(10, sortedSignals.Count); i++)
                {
                    Debug.WriteLine($"[COLLECTION]   {i+1}. {sortedSignals[i].Pair} ({sortedSignals[i].ExchangeName}) - {sortedSignals[i].FundingRate:F6}");
                }

                // Отправляем сигналы последовательно в отсортированном порядке
                int signalIndex = 0;
                Debug.WriteLine($"[SENDING] Начинаем последовательную отправку {sortedSignals.Count} отсортированных сигналов:");

                foreach (var signal in sortedSignals)
                {
                    signalIndex++;
                    Debug.WriteLine($"[SENDING] [{signalIndex:000}/{sortedSignals.Count():000}] Обрабатываем пару: {signal.Pair} ({signal.ExchangeName}) - Funding Rate: {signal.FundingRate:F6}");

                    // Проверяем, нужно ли отправить сигнал (не чаще чем раз в 8 часов)
                    if (await _sentSignalsStorageService.CanSendSignalAsync(signal.ExchangeName, signal.Symbol, 8))
                    {
                        // Можно отправить сигнал - создаем запись с текущим временем
                        var sentSignalRecord = new SentSignalRecord(signal.ExchangeName, signal.Symbol, DateTime.UtcNow);
                        await _sentSignalsStorageService.SaveSentSignalAsync(sentSignalRecord);

                        Debug.WriteLine($"[SENDING] ОТПРАВКА СИГНАЛА [{signalIndex:000}]: {signal.Pair} ({signal.ExchangeName}), Funding Rate: {signal.FundingRate:F6}");
                        await SendNotificationsToActiveChannelsAsync(signal).ConfigureAwait(false);

                        Debug.WriteLine($"[SENDING] ✅ Сигнал отправлен для пары: {signal.Pair} ({signal.ExchangeName}), Funding Rate: {signal.FundingRate:F6}");
                    }
                    else
                    {
                        Debug.WriteLine($"[SENDING] Сигнал для пары {signal.Pair} ({signal.ExchangeName}) уже был отправлен недавно (в последние 8 часов), пропускаем");
                    }
                }

                Debug.WriteLine($"[COLLECTION] === КОНЕЦ СБОРА И ОТПРАВКИ СИГНАЛОВ ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при проверке бирж: {ex.Message}");
            }
        }


        /// <summary>
        /// Отправка уведомлений в активные каналы
        /// </summary>
        private async Task SendNotificationsToActiveChannelsAsync(FundingRateSignal signal)
        {
            try
            {
                // Создаем копию сигнала с рассчитанным TakeProfitPrice
                var signalWithTakeProfit = new FundingRateSignal(
                    ExchangeName: signal.ExchangeName,
                    Symbol: signal.Symbol,
                    Pair: signal.Pair,
                    CurrentPrice: signal.CurrentPrice,
                    FundingRate: signal.FundingRate,
                    TakeProfitPrice: signal.FundingRate < 0 ? signal.CurrentPrice * 1.40m : null, // BUY сигнал: +40%, SELL сигнал: null
                    Timestamp: signal.Timestamp
                );

                string tradeBotUrl = IsTradeBotEnabled ? TradeBotUrl : string.Empty;

                // Отправляем в канал 1, если он активен
                if (IsTelegramChannel1Enabled && !string.IsNullOrEmpty(TelegramChannel1Id))
                {
                    try
                    {
                        if (long.TryParse(TelegramChannel1Id, out long channel1Id))
                        {
                            await _notificationService.SendNotificationAsync(signalWithTakeProfit, channel1Id, tradeBotUrl);
                            Debug.WriteLine($"[NOTIFICATION] Отправлено уведомление в канал 1 для {signal.Pair}");
                        }
                        else
                        {
                            Debug.WriteLine($"Неверный формат ID канала 1: {TelegramChannel1Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка при отправке в канал 1: {ex.Message}");
                        // Продолжаем отправку в другие каналы даже при ошибке
                    }
                }

                // Отправляем в канал 2, если он активен
                if (IsTelegramChannel2Enabled && !string.IsNullOrEmpty(TelegramChannel2Id))
                {
                    try
                    {
                        if (long.TryParse(TelegramChannel2Id, out long channel2Id))
                        {
                            await _notificationService.SendNotificationAsync(signalWithTakeProfit, channel2Id, tradeBotUrl);
                            Debug.WriteLine($"[NOTIFICATION] Отправлено уведомление в канал 2 для {signal.Pair}");
                        }
                        else
                        {
                            Debug.WriteLine($"Неверный формат ID канала 2: {TelegramChannel2Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка при отправке в канал 2: {ex.Message}");
                        // Продолжаем работу даже при ошибке
                    }
                }

                // Добавляем небольшую задержку между отправками для сохранения порядка
                await Task.Delay(200).ConfigureAwait(false); // 200ms задержка между уведомлениями
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Критическая ошибка при отправке уведомлений: {ex.Message}");
            }
        }

        #endregion
    }
}