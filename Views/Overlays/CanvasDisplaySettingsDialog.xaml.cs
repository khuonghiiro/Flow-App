using FlowMy.Services.Utilities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using FlowMy.Services.Rendering;

namespace FlowMy.Views.Overlays
{
    public partial class CanvasDisplaySettingsDialog : Window
    {
        public CanvasToolbarPreferences Result { get; private set; }
        public event System.Action<CanvasToolbarPreferences>? PreferencesChanged;
        private bool _isApplying;
        private readonly DispatcherTimer _applyDebounceTimer;
        private CanvasToolbarPreferences? _pendingPreferences;

        public CanvasDisplaySettingsDialog(CanvasToolbarPreferences current)
        {
            InitializeComponent();
            Result = current ?? new CanvasToolbarPreferences();
            _applyDebounceTimer = new DispatcherTimer
            {
                Interval = System.TimeSpan.FromMilliseconds(70)
            };
            _applyDebounceTimer.Tick += ApplyDebounceTimer_Tick;
            PopulateConnectionLineStyles();
            PopulateCustomColors();
            PopulateGpuQualities();
            HookRealtimeEvents();
            ApplyInitialSelection(Result);
        }

        private void FlushPendingPreferences()
        {
            // If user closes dialog quickly, debounce tick might not happen yet.
            // Flush the latest preferences so host can persist them.
            if (_pendingPreferences == null) return;

            _applyDebounceTimer.Stop();
            Result = _pendingPreferences;
            _pendingPreferences = null;
            PreferencesChanged?.Invoke(Result);
        }

        private void PopulateConnectionLineStyles()
        {
            var styles = new Dictionary<string, string>
            {
                ["Bezier"] = "Bezier (Cong mượt)",
                ["Orthogonal"] = "Vuông góc (Orthogonal)",
                ["Straight"] = "Thẳng (Straight)",
                ["SmoothOrthogonal"] = "Vuông góc bo tròn",
                ["Arc"] = "Cung tròn (Arc)",
                ["RadialFanout"] = "Tỏa quạt (Radial / Fan-out)",
                ["Windy"] = "Gió thổi (Windy)",
                ["OrthogonalV2"] = "Vuông góc thông minh (V2)"
            };

            foreach (var item in styles)
            {
                LineStyleComboBox.Items.Add(new ComboBoxItem { Content = item.Value, Tag = item.Key });
            }
        }

        private void PopulateCustomColors()
        {
            var colors = new Dictionary<string, string>
            {
                ["LimeGreen"] = "Lime Green",
                ["PrimaryBrush"] = "Primary Blue",
                ["SuccessBrush"] = "Success Green",
                ["DangerBrush"] = "Danger Red",
                ["WarningBrush"] = "Warning Orange",
                ["InfoBrush"] = "Info Cyan",
                ["IndigoBrush"] = "Indigo",
                ["CoralBrush"] = "Coral",
                ["OceanBrush"] = "Ocean",
                ["LavenderBrush"] = "Lavender"
            };

            foreach (var item in colors)
            {
                CustomColorKeyComboBox.Items.Add(new ComboBoxItem { Content = item.Value, Tag = item.Key });
                BulkTitleColorKeyComboBox.Items.Add(new ComboBoxItem { Content = item.Value, Tag = item.Key });
                EnergyCustomColorKeyComboBox.Items.Add(new ComboBoxItem { Content = item.Value, Tag = item.Key });
            }

            EnergyCustomColorKeyComboBox.Items.Insert(0, new ComboBoxItem { Content = "Gold", Tag = "Gold" });
        }

        private void PopulateGpuQualities()
        {
            foreach (GpuRenderQuality quality in System.Enum.GetValues(typeof(GpuRenderQuality)))
            {
                GpuQualityComboBox.Items.Add(new ComboBoxItem
                {
                    Content = GpuRenderQualityHelper.GetDisplayName(quality),
                    Tag = quality.ToString()
                });
            }
        }

        private void ApplyInitialSelection(CanvasToolbarPreferences preferences)
        {
            _isApplying = true;
            SelectByTag(GridTypeComboBox, preferences.GridType);
            SetRadioSelection(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio, preferences.CanvasDisplayMode);
            SetRadioSelection(CullingProfileLowRadio, CullingProfileNormalRadio, CullingProfileHighRadio, preferences.CullingPerformanceProfile);
            SelectByTag(LineStyleComboBox, preferences.ConnectionLineStyle);
            SetRadioSelection(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio, preferences.ConnectionAnimationMode);
            SetRadioSelection(ConnectionColorModeNodeRadio, ConnectionColorModeCustomRadio, preferences.ConnectionColorMode);
            SelectByTag(CustomColorKeyComboBox, preferences.CustomConnectionColorKey);
            SelectByTag(GpuQualityComboBox, preferences.GpuRenderQuality);
            GpuEnabledCheckBox.IsChecked = preferences.GpuEnabled;
            SetRadioSelection(BulkTitleModeNodeRadio, BulkTitleModeCustomRadio, preferences.BulkTitleColorMode);
            SelectByTag(BulkTitleColorKeyComboBox, string.IsNullOrWhiteSpace(preferences.BulkTitleColorKey) ? "PrimaryBrush" : preferences.BulkTitleColorKey);
            SetRadioSelection(EnergyColorModeFollowLineRadio, EnergyColorModeCustomRadio, preferences.EnergyColorMode);
            SelectByTag(EnergyCustomColorKeyComboBox, preferences.CustomEnergyColorKey);
            EnergyDotGapSlider.Value = preferences.EnergyDotGap;
            EnergyDotThicknessSlider.Value = preferences.EnergyDotThicknessExtra;
            EnergyDotTextTextBox.Text = preferences.EnergyDotText ?? string.Empty;
            EnergyDotRotateCheckBox.IsChecked = preferences.EnergyDotTextRotate;
            EnergyRunSpeedSlider.Value = preferences.EnergyRunSpeed;
            EnergySpinSpeedSlider.Value = preferences.EnergyTextSpinSeconds;
            EnergyMeteorModeCheckBox.IsChecked = preferences.EnergyMeteorMode;
            SetRadioSelection(GetDebounceFastRadio(), GetDebounceBalancedRadio(), GetDebounceSmoothRadio(), NormalizeDebounceTag(preferences.ApplyDebounceMs));
            ApplyDebounceIntervalFromSelection();
            UpdateSliderTexts();
            _isApplying = false;
            UpdatePresetButtonStateFromProfile(preferences.CullingPerformanceProfile);
        }

        private static void SelectByTag(ComboBox comboBox, string tag)
        {
            foreach (var item in comboBox.Items)
            {
                if (item is ComboBoxItem cb && (cb.Tag?.ToString() ?? string.Empty) == tag)
                {
                    comboBox.SelectedItem = cb;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private static string SelectedTag(ComboBox comboBox)
        {
            return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
        }

        private CanvasToolbarPreferences BuildPreferences()
        {
            var gap = EnergyDotGapSlider.Value;
            var thickness = EnergyDotThicknessSlider.Value;
            var speed = EnergyRunSpeedSlider.Value;
            var spin = EnergySpinSpeedSlider.Value;

            return new CanvasToolbarPreferences
            {
                GridType = SelectedTag(GridTypeComboBox),
                CanvasDisplayMode = SelectedRadioTag(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio),
                CullingPerformanceProfile = SelectedRadioTag(CullingProfileLowRadio, CullingProfileNormalRadio, CullingProfileHighRadio),
                ConnectionLineStyle = SelectedTag(LineStyleComboBox),
                ConnectionAnimationMode = SelectedRadioTag(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio),
                ConnectionColorMode = SelectedRadioTag(ConnectionColorModeNodeRadio, ConnectionColorModeCustomRadio),
                CustomConnectionColorKey = SelectedTag(CustomColorKeyComboBox),
                GpuEnabled = GpuEnabledCheckBox.IsChecked == true,
                GpuRenderQuality = SelectedTag(GpuQualityComboBox),
                BulkTitleColorMode = SelectedRadioTag(BulkTitleModeNodeRadio, BulkTitleModeCustomRadio),
                BulkTitleColorKey = SelectedTag(BulkTitleColorKeyComboBox),
                EnergyColorMode = SelectedRadioTag(EnergyColorModeFollowLineRadio, EnergyColorModeCustomRadio),
                CustomEnergyColorKey = SelectedTag(EnergyCustomColorKeyComboBox),
                EnergyDotGap = gap > 0 ? gap : 8.0,
                EnergyDotThicknessExtra = thickness,
                EnergyDotText = EnergyDotTextTextBox.Text ?? string.Empty,
                EnergyDotTextRotate = EnergyDotRotateCheckBox.IsChecked == true,
                EnergyRunSpeed = speed > 0 ? speed : 1.0,
                EnergyTextSpinSeconds = spin > 0 ? spin : 0.7,
                EnergyMeteorMode = EnergyMeteorModeCheckBox.IsChecked == true,
                ApplyDebounceMs = SelectedDebounceMs()
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Treat "Đóng" as: apply latest settings and persist (best effort).
            FlushPendingPreferences();
            DialogResult = true;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Also flush when user clicks the window "X" close button
            // or closes via other means, so settings are not lost.
            FlushPendingPreferences();
            base.OnClosing(e);
        }

        private void HookRealtimeEvents()
        {
            GridTypeComboBox.SelectionChanged += AnyControlChanged;
            LineStyleComboBox.SelectionChanged += AnyControlChanged;
            CustomColorKeyComboBox.SelectionChanged += AnyControlChanged;
            GpuEnabledCheckBox.Checked += AnyControlChanged;
            GpuEnabledCheckBox.Unchecked += AnyControlChanged;
            GpuQualityComboBox.SelectionChanged += AnyControlChanged;
            BulkTitleColorKeyComboBox.SelectionChanged += AnyControlChanged;
            EnergyCustomColorKeyComboBox.SelectionChanged += AnyControlChanged;
            CanvasDisplayModeAllRadio.Checked += AnyControlChanged;
            CanvasDisplayModeViewportRadio.Checked += AnyControlChanged;
            CullingProfileLowRadio.Checked += AnyControlChanged;
            CullingProfileNormalRadio.Checked += AnyControlChanged;
            CullingProfileHighRadio.Checked += AnyControlChanged;
            AnimationModeAnimatedRadio.Checked += AnyControlChanged;
            AnimationModeOffRadio.Checked += AnyControlChanged;
            AnimationModeDashedRadio.Checked += AnyControlChanged;
            ConnectionColorModeNodeRadio.Checked += AnyControlChanged;
            ConnectionColorModeCustomRadio.Checked += AnyControlChanged;
            BulkTitleModeNodeRadio.Checked += AnyControlChanged;
            BulkTitleModeCustomRadio.Checked += AnyControlChanged;
            EnergyColorModeFollowLineRadio.Checked += AnyControlChanged;
            EnergyColorModeCustomRadio.Checked += AnyControlChanged;
            GetDebounceFastRadio().Checked += AnyControlChanged;
            GetDebounceBalancedRadio().Checked += AnyControlChanged;
            GetDebounceSmoothRadio().Checked += AnyControlChanged;
            EnergyDotTextBoxEvents();
        }

        private void EnergyDotTextBoxEvents()
        {
            EnergyDotGapSlider.ValueChanged += AnyControlChanged;
            EnergyDotThicknessSlider.ValueChanged += AnyControlChanged;
            EnergyDotTextTextBox.TextChanged += AnyControlChanged;
            EnergyDotRotateCheckBox.Checked += AnyControlChanged;
            EnergyDotRotateCheckBox.Unchecked += AnyControlChanged;
            EnergyRunSpeedSlider.ValueChanged += AnyControlChanged;
            EnergySpinSpeedSlider.ValueChanged += AnyControlChanged;
            EnergyMeteorModeCheckBox.Checked += AnyControlChanged;
            EnergyMeteorModeCheckBox.Unchecked += AnyControlChanged;
        }

        private void AnyControlChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplying) return;
            UpdateSliderTexts();
            QueuePreferencesApply();
        }

        private void AnyControlChanged(object sender, RoutedEventArgs e)
        {
            if (_isApplying) return;
            UpdateSliderTexts();
            QueuePreferencesApply();
        }

        private void QueuePreferencesApply()
        {
            ApplyDebounceIntervalFromSelection();
            _pendingPreferences = BuildPreferences();
            _applyDebounceTimer.Stop();
            _applyDebounceTimer.Start();
        }

        private void ApplyDebounceTimer_Tick(object? sender, System.EventArgs e)
        {
            _applyDebounceTimer.Stop();
            if (_pendingPreferences == null) return;
            Result = _pendingPreferences;
            _pendingPreferences = null;
            PreferencesChanged?.Invoke(Result);
        }

        private void UpdateSliderTexts()
        {
            if (EnergyDotGapValueText != null) EnergyDotGapValueText.Text = EnergyDotGapSlider.Value.ToString("0.0");
            if (EnergyDotThicknessValueText != null) EnergyDotThicknessValueText.Text = EnergyDotThicknessSlider.Value.ToString("0.0");
            if (EnergyRunSpeedValueText != null) EnergyRunSpeedValueText.Text = EnergyRunSpeedSlider.Value.ToString("0.0");
            if (EnergySpinSpeedValueText != null) EnergySpinSpeedValueText.Text = EnergySpinSpeedSlider.Value.ToString("0.0");
        }

        private int SelectedDebounceMs()
        {
            if (GetDebounceFastRadio().IsChecked == true) return 30;
            if (GetDebounceSmoothRadio().IsChecked == true) return 120;
            return 70;
        }

        private static string NormalizeDebounceTag(int ms)
        {
            if (ms <= 40) return "30";
            if (ms >= 100) return "120";
            return "70";
        }

        private void ApplyDebounceIntervalFromSelection()
        {
            var ms = SelectedDebounceMs();
            if (_applyDebounceTimer.Interval.TotalMilliseconds != ms)
            {
                _applyDebounceTimer.Interval = System.TimeSpan.FromMilliseconds(ms);
            }
            UpdateDebounceStatusText(ms);
        }

        private void UpdateDebounceStatusText(int ms)
        {
            if (DebounceStatusText == null) return;
            var label = ms switch
            {
                <= 40 => "Nhanh",
                >= 100 => "Mượt",
                _ => "Cân bằng"
            };
            DebounceStatusText.Text = $"Apply debounce: {ms}ms ({label})";
        }

        private RadioButton GetDebounceFastRadio() => (RadioButton)FindName("DebounceFastRadio");
        private RadioButton GetDebounceBalancedRadio() => (RadioButton)FindName("DebounceBalancedRadio");
        private RadioButton GetDebounceSmoothRadio() => (RadioButton)FindName("DebounceSmoothRadio");

        private static void SetRadioSelection(RadioButton option1, RadioButton option2, string tag)
        {
            option1.IsChecked = (option1.Tag?.ToString() ?? string.Empty) == tag;
            option2.IsChecked = (option2.Tag?.ToString() ?? string.Empty) == tag;
            if (option1.IsChecked != true && option2.IsChecked != true)
            {
                option1.IsChecked = true;
            }
        }

        private static void SetRadioSelection(RadioButton option1, RadioButton option2, RadioButton option3, string tag)
        {
            option1.IsChecked = (option1.Tag?.ToString() ?? string.Empty) == tag;
            option2.IsChecked = (option2.Tag?.ToString() ?? string.Empty) == tag;
            option3.IsChecked = (option3.Tag?.ToString() ?? string.Empty) == tag;
            if (option1.IsChecked != true && option2.IsChecked != true && option3.IsChecked != true)
            {
                option1.IsChecked = true;
            }
        }

        private static string SelectedRadioTag(RadioButton option1, RadioButton option2)
        {
            if (option1.IsChecked == true) return option1.Tag?.ToString() ?? string.Empty;
            if (option2.IsChecked == true) return option2.Tag?.ToString() ?? string.Empty;
            return option1.Tag?.ToString() ?? string.Empty;
        }

        private static string SelectedRadioTag(RadioButton option1, RadioButton option2, RadioButton option3)
        {
            if (option1.IsChecked == true) return option1.Tag?.ToString() ?? string.Empty;
            if (option2.IsChecked == true) return option2.Tag?.ToString() ?? string.Empty;
            if (option3.IsChecked == true) return option3.Tag?.ToString() ?? string.Empty;
            return option1.Tag?.ToString() ?? string.Empty;
        }

        private void PresetButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_isApplying) return;
            if (sender is not ToggleButton tb || tb.Tag is not string presetTag) return;
            ApplyPreset(presetTag);
        }

        private void ApplyPreset(string presetTag)
        {
            _isApplying = true;
            UncheckAllPresetButtonsExcept(presetTag);

            switch (presetTag)
            {
                case "Low":
                    SetRadioSelection(CullingProfileLowRadio, CullingProfileNormalRadio, CullingProfileHighRadio, "Low");
                    SetRadioSelection(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio, "ViewportOnly");
                    SetRadioSelection(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio, "Off");
                    SelectByTag(GpuQualityComboBox, "Low");
                    GpuEnabledCheckBox.IsChecked = true;
                    EnergyDotGapSlider.Value = 12;
                    EnergyDotThicknessSlider.Value = 1.2;
                    EnergyRunSpeedSlider.Value = 1.0;
                    EnergySpinSpeedSlider.Value = 1.0;
                    break;
                case "Normal":
                    SetRadioSelection(CullingProfileLowRadio, CullingProfileNormalRadio, CullingProfileHighRadio, "Normal");
                    SetRadioSelection(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio, "ViewportOnly");
                    SetRadioSelection(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio, "Animated");
                    SelectByTag(GpuQualityComboBox, "Medium");
                    GpuEnabledCheckBox.IsChecked = true;
                    EnergyDotGapSlider.Value = 8;
                    EnergyDotThicknessSlider.Value = 0.8;
                    EnergyRunSpeedSlider.Value = 1.4;
                    EnergySpinSpeedSlider.Value = 0.8;
                    break;
                case "High":
                    SetRadioSelection(CullingProfileLowRadio, CullingProfileNormalRadio, CullingProfileHighRadio, "High");
                    SetRadioSelection(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio, "ViewportOnly");
                    SetRadioSelection(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio, "Animated");
                    SelectByTag(GpuQualityComboBox, "High");
                    GpuEnabledCheckBox.IsChecked = true;
                    EnergyDotGapSlider.Value = 6;
                    EnergyDotThicknessSlider.Value = 1.6;
                    EnergyRunSpeedSlider.Value = 2.0;
                    EnergySpinSpeedSlider.Value = 0.6;
                    break;
                case "Debug":
                    SetRadioSelection(CullingProfileLowRadio, CullingProfileNormalRadio, CullingProfileHighRadio, "Low");
                    SetRadioSelection(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio, "ShowAll");
                    SetRadioSelection(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio, "Off");
                    SelectByTag(GpuQualityComboBox, "Low");
                    GpuEnabledCheckBox.IsChecked = false;
                    EnergyDotGapSlider.Value = 14;
                    EnergyDotThicknessSlider.Value = 0.4;
                    EnergyRunSpeedSlider.Value = 0.8;
                    EnergySpinSpeedSlider.Value = 1.2;
                    break;
                case "Ultra":
                    SetRadioSelection(CullingProfileLowRadio, CullingProfileNormalRadio, CullingProfileHighRadio, "High");
                    SetRadioSelection(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio, "ShowAll");
                    SetRadioSelection(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio, "Animated");
                    SelectByTag(GpuQualityComboBox, "Best");
                    GpuEnabledCheckBox.IsChecked = true;
                    EnergyDotGapSlider.Value = 5;
                    EnergyDotThicknessSlider.Value = 2.2;
                    EnergyRunSpeedSlider.Value = 2.6;
                    EnergySpinSpeedSlider.Value = 0.4;
                    break;
            }

            UpdateSliderTexts();
            _isApplying = false;
            QueuePreferencesApply();
        }

        private void UncheckAllPresetButtonsExcept(string selectedTag)
        {
            var presets = new[] { PresetLowButton, PresetNormalButton, PresetHighButton, PresetDebugButton, PresetUltraButton };
            foreach (var button in presets.Where(b => b != null))
            {
                var tag = button.Tag?.ToString() ?? string.Empty;
                button.IsChecked = tag == selectedTag;
            }
        }

        private void UpdatePresetButtonStateFromProfile(string cullingProfileTag)
        {
            if (_isApplying) return;
            _isApplying = true;
            var selected = cullingProfileTag switch
            {
                "Low" => "Low",
                "High" => "High",
                _ => "Normal"
            };
            UncheckAllPresetButtonsExcept(selected);
            _isApplying = false;
        }
    }
}
