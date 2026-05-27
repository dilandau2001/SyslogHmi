using System.Windows;
using SyslogHmi.ViewModels;

namespace SyslogHmi
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
