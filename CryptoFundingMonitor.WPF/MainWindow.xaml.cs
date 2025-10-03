using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CryptoFundingMonitor.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // DataContext будет установлен в App.xaml.cs через DI
        }

        /// <summary>
        /// Конструктор с внедрением зависимостей
        /// </summary>
        /// <param name="mainViewModel">MainViewModel с внедренными зависимостями</param>
        public MainWindow(ViewModels.MainViewModel mainViewModel) : this()
        {
            DataContext = mainViewModel;
        }
    }
}