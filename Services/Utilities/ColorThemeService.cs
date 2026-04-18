using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Utilities
{
    public sealed class ColorThemeService
    {
        private readonly ConcurrentDictionary<string, Brush?> _brushCache = new();
        private readonly ConcurrentDictionary<string, Color?> _colorCache = new();

        private string _currentTheme = "Light";

        /// <summary>
        /// Theme names supported (display name → file suffix)
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> AvailableThemes = new Dictionary<string, string>
        {
            { "Light",     "LightTheme"     },
            { "Dark",      "DarkTheme"      },
            { "SoftLight", "SoftLightTheme" },
            { "SoftDark",  "SoftDarkTheme"  },
            { "Dracula",   "DraculaTheme"   },
            { "Monokai",   "MonokaiTheme"   },
            { "Night",     "NightTheme"     },
            { "Modern",    "ModernTheme"    },
        };

        /// <summary>
        /// Current theme display name ("Light", "Dark", "Dracula", "Monokai", "Night", "Modern")
        /// </summary>
        public string CurrentTheme
        {
            get => _currentTheme;
            private set => _currentTheme = value;
        }

        /// <summary>
        /// Event fired when theme changes
        /// </summary>
        public event EventHandler? ThemeChanged;

        public Color? GetColor(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return _colorCache.GetOrAdd(key, k =>
            {
                var brush = GetBrush(k);
                return brush == null ? null : GetColorFromBrush(brush);
            });
        }

        public Brush? GetBrush(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return _brushCache.GetOrAdd(key, k =>
            {
                try
                {
                    return Application.Current.TryFindResource(k) as Brush;
                }
                catch
                {
                    return null;
                }
            });
        }

        public Color GetColorFromBrush(Brush brush)
        {
            if (brush is SolidColorBrush solid) return solid.Color;

            if (brush is LinearGradientBrush linear && linear.GradientStops.Count > 0)
                return linear.GradientStops[0].Color;

            if (brush is RadialGradientBrush radial && radial.GradientStops.Count > 0)
                return radial.GradientStops[0].Color;

            if (brush is DrawingBrush drawingBrush &&
                drawingBrush.Drawing is GeometryDrawing geometryDrawing &&
                geometryDrawing.Brush is SolidColorBrush drawingSolid)
                return drawingSolid.Color;

            // Fallback: render brush to 1 pixel
            try
            {
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawRectangle(brush, null, new Rect(0, 0, 1, 1));
                }

                var rtb = new RenderTargetBitmap(1, 1, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);
                var pixels = new byte[4];
                rtb.CopyPixels(pixels, 4, 0);
                return Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
            }
            catch
            {
                return Colors.Gray;
            }
        }

        public Brush GetTextColorForBackground(Brush background)
        {
            Color color = GetColorFromBrush(background);
            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
            return luminance > 0.5 ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.White);
        }

        /// <summary>
        /// Toggle between Light and Dark themes only (không ảnh hưởng custom themes)
        /// </summary>
        public void ToggleTheme()
        {
            var newTheme = _currentTheme is "Dark" or "SoftDark" or "Dracula" or "Monokai" or "Night"
                ? "Light"
                : "Dark";
            LoadTheme(newTheme);
        }

        /// <summary>
        /// Load a specific theme by display name ("Light", "Dark", "Dracula", "Monokai", "Night", "Modern")
        /// </summary>
        public void LoadTheme(string themeName)
        {
            if (!AvailableThemes.TryGetValue(themeName, out var themeFile))
            {
                System.Diagnostics.Debug.WriteLine($"Invalid theme name: {themeName}. Falling back to Light.");
                themeName = "Light";
                themeFile = "LightTheme";
            }

            try
            {
                var themeUri = new Uri($"Themes/{themeFile}.xaml", UriKind.Relative);
                var newTheme = new ResourceDictionary { Source = themeUri };

                // Replace the first merged dictionary (which should be the theme)
                if (Application.Current.Resources.MergedDictionaries.Count > 0)
                    Application.Current.Resources.MergedDictionaries[0] = newTheme;
                else
                    Application.Current.Resources.MergedDictionaries.Add(newTheme);

                _currentTheme = themeName;

                // Clear caches to force refresh
                _brushCache.Clear();
                _colorCache.Clear();

                // Save preference
                SaveThemePreference(themeName);

                // Notify listeners
                ThemeChanged?.Invoke(this, EventArgs.Empty);

                System.Diagnostics.Debug.WriteLine($"Theme changed to: {themeName} ({themeFile})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading theme {themeName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Load theme preference from user settings
        /// </summary>
        public void LoadThemePreference()
        {
            try
            {
                var savedTheme = Properties.Settings.Default.ThemePreference;
                if (!string.IsNullOrEmpty(savedTheme) && AvailableThemes.ContainsKey(savedTheme))
                    LoadTheme(savedTheme);
                else
                    LoadTheme("Light");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading theme preference: {ex.Message}");
                LoadTheme("Light");
            }
        }

        /// <summary>
        /// Save theme preference to user settings
        /// </summary>
        private void SaveThemePreference(string themeName)
        {
            try
            {
                Properties.Settings.Default.ThemePreference = themeName;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving theme preference: {ex.Message}");
            }
        }

        /// <summary>
        /// Get list of available theme names for UI display
        /// </summary>
        public static IEnumerable<string> GetThemeNames() => AvailableThemes.Keys;
    }
}
