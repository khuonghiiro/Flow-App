using FlowMy.Helpers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace FlowMy.Extensions
{
    /// <summary>
    /// Extension methods để hỗ trợ theme switching
    /// </summary>
    public static class ThemeExtensions
    {
        #region Theme Application

        /// <summary>
        /// Áp dụng theme mới và cập nhật tất cả components
        /// </summary>
        public static void ApplyTheme(string themeName)
        {
            try
            {
                string themePath = $"Themes/{themeName}.xaml";
                var dict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };

                // Xóa các ResourceDictionary cũ (theme resources)
                Application.Current.Resources.MergedDictionaries.Clear();

                // Thêm theme mới
                Application.Current.Resources.MergedDictionaries.Add(dict);

                // Trigger theme change event để các component cập nhật
                OnThemeChanged?.Invoke(themeName);

                // Force refresh UI sau khi đổi theme
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    RefreshAllWindows();
                }));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi áp dụng theme: {ex.Message}", "Lỗi Theme",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Event được trigger khi theme thay đổi
        /// </summary>
        public static event Action<string> OnThemeChanged;

        /// <summary>
        /// Refresh tất cả windows để áp dụng theme mới
        /// </summary>
        private static void RefreshAllWindows()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window != null)
                {
                    window.InvalidateVisual();

                }
            }
        }

        #endregion

        #region Theme Detection

        /// <summary>
        /// Lấy tên theme hiện tại
        /// </summary>
        public static string GetCurrentThemeName()
        {
            try
            {
                string stored = Properties.Settings.Default.AppTheme;
                return string.IsNullOrEmpty(stored) ? "LightTheme" : stored;
            }
            catch
            {
                return DynamicColorHelper.IsDarkTheme() ? "DarkTheme" : "LightTheme";
            }
        }

        /// <summary>
        /// Toggle giữa dark và light theme
        /// </summary>
        public static void ToggleTheme()
        {
            string newTheme = DynamicColorHelper.IsDarkTheme() ? "LightTheme" : "DarkTheme";
            ApplyTheme(newTheme);
        }

        #endregion

        #region Color Utilities

        /// <summary>
        /// Lấy màu từ current theme resources
        /// </summary>
        public static SolidColorBrush GetThemeColor(string resourceKey)
        {
            try
            {
                return Application.Current.Resources[resourceKey] as SolidColorBrush;
            }
            catch
            {
                return new SolidColorBrush(Colors.Gray); // Fallback color
            }
        }

        /// <summary>
        /// Kiểm tra xem resource có tồn tại không
        /// </summary>
        public static bool HasThemeResource(string resourceKey)
        {
            try
            {
                return Application.Current.Resources.Contains(resourceKey);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lấy màu text phù hợp với background
        /// </summary>
        public static SolidColorBrush GetContrastTextColor(SolidColorBrush backgroundBrush)
        {
            if (backgroundBrush?.Color != null)
            {
                return OxyColorHelper.GetContrastColor(backgroundBrush.Color);
            }
            return GetThemeColor("TextBrush") ?? new SolidColorBrush(Colors.Black);
        }

        #endregion

        #region Animation Support

        /// <summary>
        /// Tạo animation cho theme transition
        /// </summary>
        public static void AnimateThemeTransition(FrameworkElement element, TimeSpan duration)
        {
            if (element == null) return;

            try
            {
                // Tạo fade effect khi chuyển theme
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.7,
                    TimeSpan.FromMilliseconds(duration.TotalMilliseconds / 2));
                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.7, 1.0,
                    TimeSpan.FromMilliseconds(duration.TotalMilliseconds / 2));

                fadeOut.Completed += (s, e) =>
                {
                    element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                };

                element.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Animation error: {ex.Message}");
            }
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Lưu theme preference vào registry/settings
        /// </summary>
        public static void SaveThemePreference(string themeName)
        {
            try
            {
                Properties.Settings.Default.AppTheme = themeName;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save theme preference: {ex.Message}");
            }
        }

        /// <summary>
        /// Load theme preference từ registry/settings
        /// </summary>
        public static string LoadThemePreference()
        {
            try
            {
                return Properties.Settings.Default.AppTheme ?? "LightTheme";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load theme preference: {ex.Message}");
                return "LightTheme"; // Default fallback
            }
        }

        /// <summary>
        /// Áp dụng theme đã lưu khi khởi động ứng dụng
        /// </summary>
        public static void ApplyStoredTheme()
        {
            string storedTheme = LoadThemePreference();
            ApplyTheme(storedTheme);
        }

        #endregion

        #region System Theme Detection

        /// <summary>
        /// Detect system theme (Windows 10/11)
        /// </summary>
        public static bool IsSystemDarkTheme()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key?.GetValue("AppsUseLightTheme");
                    return value is int intValue && intValue == 0;
                }
            }
            catch
            {
                return false; // Fallback to light theme
            }
        }

        /// <summary>
        /// Áp dụng theme theo system setting
        /// </summary>
        public static void ApplySystemTheme()
        {
            string systemTheme = IsSystemDarkTheme() ? "DarkTheme" : "LightTheme";
            ApplyTheme(systemTheme);
        }

        /// <summary>
        /// Enable automatic theme switching theo system
        /// </summary>
        public static void EnableAutoThemeSwitch()
        {
            // Monitor system theme changes (simplified version)
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5) // Check every 5 seconds
            };

            bool lastSystemTheme = IsSystemDarkTheme();

            timer.Tick += (s, e) =>
            {
                bool currentSystemTheme = IsSystemDarkTheme();
                if (currentSystemTheme != lastSystemTheme)
                {
                    lastSystemTheme = currentSystemTheme;
                    ApplySystemTheme();
                }
            };

            timer.Start();
        }

        #endregion


        #region Theme Validation

        /// <summary>
        /// Kiểm tra theme file có tồn tại không
        /// </summary>
        public static bool IsThemeValid(string themeName)
        {
            try
            {
                string themePath = $"Themes/{themeName}.xaml";
                var uri = new Uri(themePath, UriKind.Relative);

                // Try to create ResourceDictionary to validate
                var dict = new ResourceDictionary { Source = uri };
                return dict.MergedDictionaries.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lấy danh sách themes available
        /// </summary>
        public static string[] GetAvailableThemes()
        {
            var themes = new List<string>();

            string[] possibleThemes =
            {
                "LightTheme",
                "DarkTheme",
                "DraculaTheme",
                "MonokaiTheme",
                "NightTheme",
                "ModernTheme"
            };

            foreach (string theme in possibleThemes)
            {
                if (IsThemeValid(theme))
                {
                    themes.Add(theme);
                }
            }

            return themes.ToArray();
        }

        #endregion

        #region Error Handling

        /// <summary>
        /// Fallback theme khi có lỗi
        /// </summary>
        public static void ApplyFallbackTheme()
        {
            try
            {
                // Try light theme first
                if (IsThemeValid("LightTheme"))
                {
                    ApplyTheme("LightTheme");
                }
                else
                {
                    // Create minimal theme in code
                    CreateMinimalTheme();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fallback theme failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Tạo theme tối thiểu khi không load được theme files
        /// </summary>
        private static void CreateMinimalTheme()
        {
            var dict = new ResourceDictionary();

            // Basic colors
            dict["WindowBackgroundBrush"] = new SolidColorBrush(Colors.White);
            dict["TextBrush"] = new SolidColorBrush(Colors.Black);
            dict["PrimaryBrush"] = new SolidColorBrush(Colors.DodgerBlue);
            dict["SecondaryBrush"] = new SolidColorBrush(Colors.Gray);
            dict["ControlBorderBrush"] = new SolidColorBrush(Colors.LightGray);

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        #endregion

        #region Performance Optimization

        /// <summary>
        /// Batch update multiple elements với theme mới
        /// </summary>
        public static void BatchUpdateTheme(IEnumerable<FrameworkElement> elements)
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                foreach (var element in elements)
                {
                    element?.InvalidateVisual();
                }
            }));
        }

        /// <summary>
        /// Debounce theme changes để tránh multiple updates
        /// </summary>
        private static DispatcherTimer _themeChangeTimer;
        private static string _pendingTheme;

        public static void DebouncedApplyTheme(string themeName, TimeSpan delay)
        {
            _pendingTheme = themeName;

            if (_themeChangeTimer == null)
            {
                _themeChangeTimer = new DispatcherTimer();
                _themeChangeTimer.Tick += (s, e) =>
                {
                    _themeChangeTimer.Stop();
                    if (!string.IsNullOrEmpty(_pendingTheme))
                    {
                        ApplyTheme(_pendingTheme);
                        _pendingTheme = null;
                    }
                };
            }

            _themeChangeTimer.Interval = delay;
            _themeChangeTimer.Stop();
            _themeChangeTimer.Start();
        }

        #endregion
    }

    #region Theme Event Args

    /// <summary>
    /// Event args cho theme change events
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        public string OldTheme { get; set; }
        public string NewTheme { get; set; }
        public DateTime ChangedAt { get; set; }

        public ThemeChangedEventArgs(string oldTheme, string newTheme)
        {
            OldTheme = oldTheme;
            NewTheme = newTheme;
            ChangedAt = DateTime.Now;
        }
    }

    #endregion

    #region Theme Manager

    /// <summary>
    /// Central theme manager class
    /// </summary>
    public static class ThemeManager
    {
        public static event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        private static string _currentTheme = FlowMy.Properties.Settings.Default.AppTheme;

        public static string CurrentTheme
        {
            get => _currentTheme;
            private set
            {
                if (_currentTheme != value)
                {
                    string oldTheme = _currentTheme;
                    _currentTheme = value;
                    ThemeChanged?.Invoke(null, new ThemeChangedEventArgs(oldTheme, value));
                }
            }
        }

        public static void Initialize()
        {
            // Load stored theme or apply system theme
            string storedTheme = ThemeExtensions.LoadThemePreference();
            if (ThemeExtensions.IsThemeValid(storedTheme))
            {
                ApplyTheme(storedTheme);
            }
            else
            {
                ThemeExtensions.ApplySystemTheme();
            }
        }

        public static void ApplyTheme(string themeName)
        {
            if (ThemeExtensions.IsThemeValid(themeName))
            {
                ThemeExtensions.ApplyTheme(themeName);
                CurrentTheme = themeName;
                ThemeExtensions.SaveThemePreference(themeName);
            }
        }

        public static void ToggleTheme()
        {
            // Chỉ toggle giữa Dark và Light. Nếu đang dùng custom theme (Dracula, Monokai, Night)
            // thì toggle về LightTheme.
            string current = CurrentTheme;
            if (current == "DarkTheme")
                ApplyTheme("LightTheme");
            else if (current == "LightTheme")
                ApplyTheme("DarkTheme");
            else
                ApplyTheme("LightTheme");
        }
    }

    #endregion
}
