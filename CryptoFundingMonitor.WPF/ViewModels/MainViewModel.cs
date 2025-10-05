using System.Windows.Media;
 using CommunityToolkit.Mvvm.ComponentModel;
 using CommunityToolkit.Mvvm.Input;
 using CryptoFundingMonitor.Core.Services;
 using CryptoFundingMonitor.Core.Models;
 using CryptoFundingMonitor.Infrastructure.Services;
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

        #region Сервисы

        private readonly IServiceProvider _serviceProvider;
        private IBrokerApiService _binanceService;
        private IBrokerApiService _bybitService;
        private IBrokerApiService _mexcService;
        private INotificationService _notificationService;
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

                // Запускаем фоновую задачу мониторинга
                _ = Task.Run(async () => await MonitoringLoopAsync(_cancellationTokenSource.Token));

                IsMonitoring = true;
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

                // Очищаем HashSet отправленных сигналов для нового цикла мониторинга
                signaledPairs.Clear();

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
                        // Ждем 1 минуту перед следующей проверкой
                        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
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
                        // Продолжаем цикл даже при ошибке
                        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
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
                // Проверяем Binance
                if (IsBinanceEnabled && !string.IsNullOrEmpty(BinanceApiKey))
                {
                    Debug.WriteLine($"[BINANCE] === НАЧАЛО ЦИКЛА ПРОВЕРКИ BINANCE ===");
                    Debug.WriteLine($"[BINANCE] IsBinanceEnabled: {IsBinanceEnabled}");
                    Debug.WriteLine($"[BINANCE] BinanceApiKey length: {BinanceApiKey.Length}");
                    Debug.WriteLine($"[BINANCE] BinanceApiSecret length: {BinanceApiSecret.Length}");
                    Debug.WriteLine($"[BINANCE] BinanceThreshold: {BinanceThreshold}");
                    Debug.WriteLine($"[BINANCE] _binanceService is null: {_binanceService == null}");

                    try
                    {
                        Debug.WriteLine($"[BINANCE] Вызываем CheckExchangeAndSendNotificationsAsync для Binance");
                        await CheckExchangeAndSendNotificationsAsync(
                            _binanceService, BinanceApiKey, BinanceApiSecret, BinanceThreshold, "Binance");
                        Debug.WriteLine($"[BINANCE] CheckExchangeAndSendNotificationsAsync выполнен успешно");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[BINANCE] ОШИБКА при проверке: {ex.Message}");
                        Debug.WriteLine($"[BINANCE] Stack trace: {ex.StackTrace}");
                        // Показываем пользователю информацию об ошибке Binance
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
                    Debug.WriteLine($"[BINANCE] === КОНЕЦ ЦИКЛА ПРОВЕРКИ BINANCE ===");
                }
                else if (IsBinanceEnabled)
                {
                    Debug.WriteLine($"[BINANCE] Binance включена, но API ключи не настроены. ApiKey пустой: {string.IsNullOrEmpty(BinanceApiKey)}, ApiSecret пустой: {string.IsNullOrEmpty(BinanceApiSecret)}");
                }

                // Проверяем Bybit
                if (IsBybitEnabled && !string.IsNullOrEmpty(BybitApiKey))
                {
                    await CheckExchangeAndSendNotificationsAsync(
                        _bybitService, BybitApiKey, string.Empty, BybitThreshold, "Bybit");
                }
                else if (IsBybitEnabled)
                {
                    Debug.WriteLine("Bybit включена, но API ключ не настроен");
                }

                // Проверяем MEXC
                if (IsMexcEnabled && !string.IsNullOrEmpty(MexcApiKey))
                {
                    try
                    {
                        await CheckExchangeAndSendNotificationsAsync(
                            _mexcService, MexcApiKey, string.Empty, MexcThreshold, "MEXC");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Критическая ошибка MEXC API: {ex.Message}");
                        // Показываем пользователю информацию об ошибке MEXC
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при проверке бирж: {ex.Message}");
            }
        }

        /// <summary>
        /// Проверка конкретной биржи и отправка уведомлений
        /// </summary>
        private async Task CheckExchangeAndSendNotificationsAsync(
            IBrokerApiService brokerService, string apiKey, string apiSecret, decimal threshold, string exchangeName)
        {
            Debug.WriteLine($"[{exchangeName}] === НАЧАЛО ПРОВЕРКИ БИРЖИ {exchangeName} ===");
            Debug.WriteLine($"[{exchangeName}] brokerService is null: {brokerService == null}");
            Debug.WriteLine($"[{exchangeName}] apiKey length: {apiKey.Length}");
            Debug.WriteLine($"[{exchangeName}] apiSecret length: {apiSecret.Length}");
            Debug.WriteLine($"[{exchangeName}] threshold: {threshold}");

            try
            {
                Debug.WriteLine($"[{exchangeName}] Получаем данные о ставках финансирования");
                // Получаем данные о ставках финансирования
                var signals = await brokerService.GetFundingRatesAsync(apiKey, apiSecret);

                // Сортируем сигналы по алфавиту для корректного порядка отправки в Telegram
                signals = signals.OrderBy(s => s.Pair).ToList();

                Debug.WriteLine($"[{exchangeName}] Получено {signals.Count()} сигналов от {exchangeName}");
                Debug.WriteLine($"[{exchangeName}] Сигналы отсортированы по алфавиту для корректного порядка отправки");
                Debug.WriteLine($"[{exchangeName}] Первые 5 пар после сортировки:");
                for (int i = 0; i < Math.Min(5, signals.Count()); i++)
                {
                    Debug.WriteLine($"[{exchangeName}]   {i+1}. {signals.ElementAt(i).Pair} - {signals.ElementAt(i).FundingRate:F6}");
                }

                int signalIndex = 0;
                Debug.WriteLine($"[{exchangeName}] Начинаем последовательную обработку {signals.Count()} отсортированных сигналов:");
                foreach (var signal in signals)
                {
                    signalIndex++;
                    Debug.WriteLine($"[{exchangeName}] [{signalIndex:000}/{signals.Count():000}] Обрабатываем пару: {signal.Pair} (Symbol: {signal.Symbol}, FundingRate: {signal.FundingRate:F6}, Price: {signal.CurrentPrice:F4})");

                    // Формируем уникальный ключ для пары в формате "ExchangeName-Symbol"
                    string pairKey = $"{signal.ExchangeName}-{signal.Symbol}";

                    // Логируем funding rate для диагностики
                    bool shouldTrigger = threshold < 0 ? signal.FundingRate <= threshold : signal.FundingRate >= threshold;
                    Debug.WriteLine($"[{exchangeName}] Проверка пары {pairKey}, Funding Rate: {signal.FundingRate:F6}, Threshold: {threshold}, ShouldTrigger: {shouldTrigger}");

                    // Проверяем условие срабатывания сигнала
                    if (shouldTrigger)
                    {
                        // Условие выполняется - проверяем, нужно ли отправить сигнал
                        if (!signaledPairs.Contains(pairKey))
                        {
                            // Пара еще не в signaledPairs - отправляем сигнал
                            signaledPairs.Add(pairKey);
                            Debug.WriteLine($"[{exchangeName}] ОТПРАВКА СИГНАЛА [{signalIndex:000}]: {pairKey}, Funding Rate: {signal.FundingRate:F6}");
                            await SendNotificationsToActiveChannelsAsync(signal);

                            Debug.WriteLine($"[{exchangeName}] ✅ Сигнал отправлен для пары: {pairKey}, Funding Rate: {signal.FundingRate:F6}");
                        }
                        else
                        {
                            Debug.WriteLine($"[{exchangeName}] Сигнал для пары {pairKey} уже был отправлен ранее, пропускаем");
                        }
                    }
                    else
                    {
                        // Условие НЕ выполняется - проверяем, нужно ли сбросить сигнал
                        bool shouldReset = threshold < 0 ? signal.FundingRate > threshold : signal.FundingRate < threshold;
                        if (signaledPairs.Contains(pairKey) && shouldReset)
                        {
                            // Пара есть в signaledPairs, но условие больше не выполняется - удаляем для будущего сигнала
                            signaledPairs.Remove(pairKey);

                            Debug.WriteLine($"[{exchangeName}] Сброс сигнала для пары: {pairKey}, Funding Rate: {signal.FundingRate:F6}, Threshold: {threshold}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при проверке биржи {exchangeName}: {ex.Message}");
            }
            finally
            {
                Debug.WriteLine($"[{exchangeName}] === КОНЕЦ ПРОВЕРКИ БИРЖИ {exchangeName} ===");
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
                await Task.Delay(200); // 200ms задержка между уведомлениями
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Критическая ошибка при отправке уведомлений: {ex.Message}");
            }
        }

        #endregion
    }
}