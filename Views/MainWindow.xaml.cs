using System.Windows;
using System;
using System.Windows.Controls;
using FlowMy.ViewModels;
using FlowMy.Services.Utilities;

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
    }
}
