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
            if (IsBinanceEnabled && string.IsNullOrEmpty(BinanceApiKey))
            {
                throw new InvalidOperationException("Необходимо указать API ключ для Binance");
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
            try
            {
                while (IsMonitoring && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Проверяем активные биржи и отправляем уведомления
                        await CheckAndSendNotificationsAsync();

                        // Ждем 1 минуту перед следующей проверкой
                        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // Задача была отменена - выходим из цикла
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка в цикле мониторинга: {ex.Message}");
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
                    await CheckExchangeAndSendNotificationsAsync(
                        _binanceService, BinanceApiKey, string.Empty, BinanceThreshold, "Binance");
                }

                // Проверяем Bybit
                if (IsBybitEnabled && !string.IsNullOrEmpty(BybitApiKey))
                {
                    await CheckExchangeAndSendNotificationsAsync(
                        _bybitService, BybitApiKey, string.Empty, BybitThreshold, "Bybit");
                }

                // Проверяем MEXC
                if (IsMexcEnabled && !string.IsNullOrEmpty(MexcApiKey))
                {
                    await CheckExchangeAndSendNotificationsAsync(
                        _mexcService, MexcApiKey, string.Empty, MexcThreshold, "MEXC");
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
            try
            {
                // Получаем данные о ставках финансирования
                var signals = await brokerService.GetFundingRatesAsync(apiKey, apiSecret);

                foreach (var signal in signals)
                {
                    // Проверяем условие срабатывания сигнала
                    if (signal.FundingRate <= threshold)
                    {
                        await SendNotificationsToActiveChannelsAsync(signal);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при проверке биржи {exchangeName}: {ex.Message}");

                // Показываем пользователю информацию об ошибке биржи
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Здесь можно добавить более детальную обработку ошибок для каждой биржи
                    // Например, показать статус ошибки для конкретной биржи
                });
            }
        }

        /// <summary>
        /// Отправка уведомлений в активные каналы
        /// </summary>
        private async Task SendNotificationsToActiveChannelsAsync(FundingRateSignal signal)
        {
            try
            {
                string tradeBotUrl = IsTradeBotEnabled ? TradeBotUrl : string.Empty;

                // Отправляем в канал 1, если он активен
                if (IsTelegramChannel1Enabled && !string.IsNullOrEmpty(TelegramChannel1Id))
                {
                    try
                    {
                        if (long.TryParse(TelegramChannel1Id, out long channel1Id))
                        {
                            await _notificationService.SendNotificationAsync(signal, channel1Id, tradeBotUrl);
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
                            await _notificationService.SendNotificationAsync(signal, channel2Id, tradeBotUrl);
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Критическая ошибка при отправке уведомлений: {ex.Message}");
            }
        }

        #endregion
    }
}