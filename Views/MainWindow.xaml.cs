
using System.Windows;

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
                DataContext = App.Services.GetService(typeof(ViewModels.MainViewModel));
            }
        }

    }
}
