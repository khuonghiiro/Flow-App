using System.Windows;
using System;
using System.Windows.Controls;
using FlowMy.ViewModels;
using FlowMy.Services.Utilities;
using FlowMy.Views.Overlays;

namespace FlowMy.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ColorThemeService? _colorThemeService;

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

            _colorThemeService = App.Services?.GetService(typeof(ColorThemeService)) as ColorThemeService;
            _colorThemeService?.LoadThemePreference();
            InitializeThemeSelector();

            if (_colorThemeService != null)
            {
                _colorThemeService.ThemeChanged += (_, __) =>
                {
                    Dispatcher.BeginInvoke(new Action(InitializeThemeSelector));
                };
            }

            // 🧱 Node Generator button — chỉ hiện trong Debug build
#if DEBUG
            if (OpenNodeGeneratorButton != null)
                OpenNodeGeneratorButton.Visibility = Visibility.Visible;
#else
            if (OpenNodeGeneratorButton != null)
                OpenNodeGeneratorButton.Visibility = Visibility.Collapsed;
#endif
        }

        private void InitializeThemeSelector()
        {
            if (ThemeSelector == null) return;

            var currentTheme = _colorThemeService?.CurrentTheme ?? "Light";

            ThemeSelector.SelectionChanged -= ThemeSelector_SelectionChanged;
            foreach (ComboBoxItem item in ThemeSelector.Items)
            {
                if (string.Equals(item.Tag?.ToString(), currentTheme, StringComparison.OrdinalIgnoreCase))
                {
                    ThemeSelector.SelectedItem = item;
                    break;
                }
            }
            ThemeSelector.SelectionChanged += ThemeSelector_SelectionChanged;
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox cb || cb.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            var themeName = item.Tag?.ToString();
            if (!string.IsNullOrWhiteSpace(themeName) && _colorThemeService != null)
            {
                _colorThemeService.LoadTheme(themeName);
            }
        }
        private NodeGeneratorWindow? _nodeGeneratorWindow;

        private void OpenNodeGeneratorButton_Click(object sender, RoutedEventArgs e)
        {
            // Nếu window đã mở, bring it to front
            if (_nodeGeneratorWindow != null && _nodeGeneratorWindow.IsVisible)
            {
                _nodeGeneratorWindow.Activate();
                _nodeGeneratorWindow.WindowState = WindowState.Normal;
                return;
            }

            // Detect project root: từ bin\Debug\net*\FlowMy.exe đi lên 3-4 cấp
            var projectRoot = DetectProjectRoot();

            _nodeGeneratorWindow = new NodeGeneratorWindow(projectRoot);
            _nodeGeneratorWindow.Owner = this;
            _nodeGeneratorWindow.Closed += (_, __) => _nodeGeneratorWindow = null;
            _nodeGeneratorWindow.Show();
        }

        /// <summary>
        /// Tìm thư mục gốc project chứa *.csproj bằng cách đi ngược từ BaseDirectory.
        /// Debug: BaseDirectory = bin\Debug\net9.0-windows\ → lên 3 cấp = project root.
        /// </summary>
        private static string DetectProjectRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            for (int i = 0; i < 6; i++)
            {
                if (System.IO.Directory.GetFiles(dir, "*.csproj").Length > 0)
                    return dir;
                var parent = System.IO.Directory.GetParent(dir)?.FullName;
                if (parent == null) break;
                dir = parent;
            }
            // Fallback: thư mục exe
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
