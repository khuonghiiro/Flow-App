// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Services.Utilities;
using FlowMy.ViewModels;
using FlowMy.Views.Overlays;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlowMy.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ColorThemeService? _colorThemeService;
        private bool _isHiddenToTray = false;
        private bool _enableHideToTray = Properties.Settings.Default.EnableHideToTray;

        public MainWindow()
        {
            InitializeComponent();

            // Set DataContext từ DI service nếu chưa được set trong XAML
            if (DataContext == null && App.Services != null)
            {
                DataContext = App.Services.GetService(typeof(MainViewModel));
            }

            // Khôi phục trạng thái "Ẩn tray" từ lần chạy trước
            if (EnableHideToTrayCheckBox != null)
            {
                EnableHideToTrayCheckBox.IsChecked = _enableHideToTray;
            }
            if (HideToTrayButton != null)
            {
                HideToTrayButton.IsEnabled = _enableHideToTray;
            }

            // Mỗi lần window được Activate (quay lại từ Editor), refresh danh sách widgets (async để không block UI)
            Activated += async (_, __) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    await vm.RefreshWidgetShortcutsAsync();
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

            // Handle window state changes
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;

            // 🧱 Node Generator button — chỉ hiện trong Debug build
#if DEBUG
            if (OpenNodeGeneratorButton != null)
                OpenNodeGeneratorButton.Visibility = Visibility.Visible;
#else
            if (OpenNodeGeneratorButton != null)
                OpenNodeGeneratorButton.Visibility = Visibility.Collapsed;
#endif
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _enableHideToTray)
            {
                Hide();
                _isHiddenToTray = true;
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isHiddenToTray)
            {
                e.Cancel = true;
                Hide();
                _isHiddenToTray = false;
            }
        }

        private void HideToTrayButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
            Hide();
            _isHiddenToTray = true;
        }

        private void EnableHideToTrayCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _enableHideToTray = true;
            if (HideToTrayButton != null)
                HideToTrayButton.IsEnabled = true;
            Properties.Settings.Default.EnableHideToTray = true;
            try { Properties.Settings.Default.Save(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Settings save error: {ex.Message}"); }
        }

        private void EnableHideToTrayCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _enableHideToTray = false;
            if (HideToTrayButton != null)
                HideToTrayButton.IsEnabled = false;
            Properties.Settings.Default.EnableHideToTray = false;
            try { Properties.Settings.Default.Save(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Settings save error: {ex.Message}"); }
        }

        private void ToggleBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (EnableHideToTrayCheckBox != null)
            {
                EnableHideToTrayCheckBox.IsChecked = !EnableHideToTrayCheckBox.IsChecked;
            }
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            _isHiddenToTray = false;
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

        private void OpenIconEditor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new IconEditorDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
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
