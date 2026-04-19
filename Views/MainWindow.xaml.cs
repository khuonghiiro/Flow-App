
using System.Windows;
using FlowMy.ViewModels;

namespace FlowMy.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Set DataContext từ DI service nếu chưa được set trong XAML
            if (DataContext == null && App.Services != null)
            {
                DataContext = App.Services.GetService(typeof(MainViewModel));
            }

            // Mỗi lần window được Activate (quay lại từ Editor), refresh danh sách widgets
            Activated += (_, __) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.RefreshWidgetShortcuts();
                }
            };
        }
    }
}
