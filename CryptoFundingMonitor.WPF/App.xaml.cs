using System.Configuration;
 using System.Data;
 using System.Windows;
 using Microsoft.Extensions.DependencyInjection;
 using CryptoFundingMonitor.Core.Services;
 using CryptoFundingMonitor.Infrastructure.Services;
 using CryptoFundingMonitor.WPF.ViewModels;

 namespace CryptoFundingMonitor.WPF
 {
     /// <summary>
     /// Interaction logic for App.xaml
     /// </summary>
     public partial class App : Application
     {
         /// <summary>
         /// Контейнер внедрения зависимостей
         /// </summary>
         public static IServiceProvider ServiceProvider { get; private set; }

         /// <summary>
         /// Конфигурация сервисов при запуске приложения
         /// </summary>
         protected override void OnStartup(StartupEventArgs e)
         {
             base.OnStartup(e);

             // Настраиваем DI контейнер
             ConfigureServices();

             // Создаем главное окно с использованием DI
             var mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();
             var mainWindow = new MainWindow(mainViewModel);
             mainWindow.Show();
         }

         /// <summary>
         /// Настройка контейнера внедрения зависимостей
         /// </summary>
         private static void ConfigureServices()
         {
             var services = new ServiceCollection();

             // Регистрируем сервисы бирж как реализации IBrokerApiService
             services.AddTransient<IBrokerApiService, BybitApiService>();
             services.AddTransient<IBrokerApiService, BinanceApiService>();
             services.AddTransient<IBrokerApiService, MexcApiService>();

             // Регистрируем сервис уведомлений
             services.AddTransient<INotificationService, TelegramNotificationService>();

             // Регистрируем ViewModel с внедрением зависимостей
             services.AddTransient<MainViewModel>();

             ServiceProvider = services.BuildServiceProvider();
         }
     }
 }
