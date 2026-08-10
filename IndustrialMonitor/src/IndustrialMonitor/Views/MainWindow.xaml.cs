using System.Windows;
using IndustrialMonitor.ViewModels;

namespace IndustrialMonitor.Views
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