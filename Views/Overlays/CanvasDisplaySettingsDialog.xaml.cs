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
        // Lưu mode animation line trước khi bật Cache node, để khi tắt cache thì khôi phục năng lượng/animation đúng.
        private string? _animationModeBeforeCacheTag;

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
            CacheNodeCheckBox.IsChecked = preferences.CacheNodeEnabled;
            UiAnimationsEnabledCheckBox.IsChecked = preferences.UiAnimationsEnabled;
            StrictFinalSyncEnabledCheckBox.IsChecked = preferences.StrictFinalSyncEnabled;
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
            NodeSpinnerArcModeCheckBox.IsChecked = preferences.NodeSpinnerArcMode;
            NodeSpinnerMultiColorCheckBox.IsChecked = preferences.NodeSpinnerMultiColor;
            NodeSpinnerSizeSlider.Value = preferences.NodeSpinnerSize > 8 ? preferences.NodeSpinnerSize : 26.0;
            NodeSpinnerScaleWithNodeCheckBox.IsChecked = preferences.NodeSpinnerScaleWithNode;
            NodeSpinnerSizeRatioSlider.Value = preferences.NodeSpinnerSizeRatio > 0 ? preferences.NodeSpinnerSizeRatio : 0.32;
            SelectByTag(NodeSpinnerShapeComboBox, preferences.NodeSpinnerShape);
            SelectByTag(NodeSpinnerPositionComboBox, preferences.NodeSpinnerPosition);
            NodeSpinnerStrokeThicknessSlider.Value = preferences.NodeSpinnerStrokeThickness > 0 ? preferences.NodeSpinnerStrokeThickness : 3.2;
            NodeSpinnerSpinSecondsSlider.Value = preferences.NodeSpinnerSpinSeconds > 0 ? preferences.NodeSpinnerSpinSeconds : 1.1;
            NodeSpinnerBlinkBackgroundCheckBox.IsChecked = preferences.NodeSpinnerBlinkBackground;
            SelectByTag(NodeSpinnerBlinkBackgroundColorComboBox, string.IsNullOrWhiteSpace(preferences.NodeSpinnerBlinkBackgroundColorKey) ? "WarningBrush" : preferences.NodeSpinnerBlinkBackgroundColorKey);
            SelectByTag(NodeSpinnerBlinkModeComboBox, string.IsNullOrWhiteSpace(preferences.NodeSpinnerBlinkMode) ? "Soft" : preferences.NodeSpinnerBlinkMode);
            NodeSpinnerBlinkIntensitySlider.Value = preferences.NodeSpinnerBlinkIntensity > 0 ? preferences.NodeSpinnerBlinkIntensity : 0.65;
            NodeSpinnerBlinkBaseOpacitySlider.Value = preferences.NodeSpinnerBlinkBaseOpacity >= 0 ? preferences.NodeSpinnerBlinkBaseOpacity : 0.16;
            NodeSpinnerBlinkPeakOpacitySlider.Value = preferences.NodeSpinnerBlinkPeakOpacity > 0 ? preferences.NodeSpinnerBlinkPeakOpacity : 0.60;
            SetRadioSelection(GetDebounceFastRadio(), GetDebounceBalancedRadio(), GetDebounceSmoothRadio(), NormalizeDebounceTag(preferences.ApplyDebounceMs));
            ApplyDebounceIntervalFromSelection();
            UpdateSliderTexts();
            UpdateCacheNodeAnimationUiState();
            UpdateConstraintDependentUiState();
            _isApplying = false;
            RecomputePresetSelectionFromFields();
        }

        private void UpdateCacheNodeAnimationUiState()
        {
            var cacheEnabled = CacheNodeCheckBox.IsChecked == true;
            if (cacheEnabled)
            {
                // Cache mode: không chạy "Animated" (đổi sang Off hoặc Dashed).
                AnimationModeAnimatedRadio.IsChecked = false;
                AnimationModeAnimatedRadio.IsEnabled = false;

                if (AnimationModeOffRadio.IsChecked != true && AnimationModeDashedRadio.IsChecked != true)
                {
                    AnimationModeOffRadio.IsChecked = true;
                }
            }
            else
            {
                AnimationModeAnimatedRadio.IsEnabled = true;
            }

            // Hiện banner giải thích khi radio bị disable, ẩn khi cache off.
            if (AnimationDisabledNotice != null)
            {
                AnimationDisabledNotice.Visibility = cacheEnabled
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Đồng bộ enable/disable các field phụ thuộc lẫn nhau để tránh cấu hình mâu thuẫn:
        /// - GPU=OFF  => disable GpuQualityComboBox (effective quality sẽ = Low khi apply).
        /// - Cache=ON => disable các hiệu ứng động phá cache (meteor/text-rotate/multi-color).
        /// - Quality=Low/Medium => disable các hiệu ứng nặng (blink background, multi-color, meteor).
        /// - Culling=Low => coerce CanvasDisplayMode về ViewportOnly (tránh render toàn bộ trên máy yếu).
        /// </summary>
        private void UpdateConstraintDependentUiState()
        {
            var gpuEnabled = GpuEnabledCheckBox.IsChecked == true;
            var cacheEnabled = CacheNodeCheckBox.IsChecked == true;
            var qualityTag = SelectedTag(GpuQualityComboBox);
            var isHeavyQuality = qualityTag is "High" or "Best";
            var cullingLow = CullingProfileLowRadio.IsChecked == true;

            GpuQualityComboBox.IsEnabled = gpuEnabled;

            // Các effect phá cache khi Cache=ON, hoặc không phù hợp với quality thấp.
            var allowHeavyEffects = gpuEnabled && isHeavyQuality && !cacheEnabled;

            EnergyMeteorModeCheckBox.IsEnabled = allowHeavyEffects;
            NodeSpinnerMultiColorCheckBox.IsEnabled = allowHeavyEffects;
            NodeSpinnerBlinkBackgroundCheckBox.IsEnabled = gpuEnabled && !cacheEnabled;

            // TextRotate không bị cache làm hỏng nặng nhưng là animation phụ,
            // ép tắt khi cache ON để đồng nhất trải nghiệm với Animated đã bị disable.
            EnergyDotRotateCheckBox.IsEnabled = !cacheEnabled;

            if (!allowHeavyEffects)
            {
                if (EnergyMeteorModeCheckBox.IsChecked == true) EnergyMeteorModeCheckBox.IsChecked = false;
                if (NodeSpinnerMultiColorCheckBox.IsChecked == true) NodeSpinnerMultiColorCheckBox.IsChecked = false;
            }
            if (cacheEnabled && EnergyDotRotateCheckBox.IsChecked == true)
            {
                EnergyDotRotateCheckBox.IsChecked = false;
            }
            if (!(gpuEnabled && !cacheEnabled) && NodeSpinnerBlinkBackgroundCheckBox.IsChecked == true)
            {
                NodeSpinnerBlinkBackgroundCheckBox.IsChecked = false;
            }

            // Culling=Low ⇒ ViewportOnly (tránh ShowAll gây render toàn cục trên máy yếu).
            if (cullingLow && CanvasDisplayModeAllRadio.IsChecked == true)
            {
                SetRadioSelection(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio, "ViewportOnly");
            }
            // Chỉ disable "ShowAll" khi culling=Low để nhấn mạnh ràng buộc, bật lại ở các mức cao hơn.
            CanvasDisplayModeAllRadio.IsEnabled = !cullingLow;
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
            var blinkBase = NodeSpinnerBlinkBaseOpacitySlider.Value >= 0 ? NodeSpinnerBlinkBaseOpacitySlider.Value : 0.16;
            var blinkPeak = NodeSpinnerBlinkPeakOpacitySlider.Value > 0 ? NodeSpinnerBlinkPeakOpacitySlider.Value : 0.60;
            if (blinkPeak <= blinkBase)
                blinkPeak = System.Math.Min(1.0, blinkBase + 0.02);

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
                CacheNodeEnabled = CacheNodeCheckBox.IsChecked == true,
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
                NodeSpinnerArcMode = NodeSpinnerArcModeCheckBox.IsChecked == true,
                NodeSpinnerMultiColor = NodeSpinnerMultiColorCheckBox.IsChecked == true,
                NodeSpinnerSize = NodeSpinnerSizeSlider.Value > 8 ? NodeSpinnerSizeSlider.Value : 26.0,
                NodeSpinnerScaleWithNode = NodeSpinnerScaleWithNodeCheckBox.IsChecked == true,
                NodeSpinnerSizeRatio = NodeSpinnerSizeRatioSlider.Value > 0 ? NodeSpinnerSizeRatioSlider.Value : 0.32,
                NodeSpinnerShape = SelectedTag(NodeSpinnerShapeComboBox),
                NodeSpinnerPosition = SelectedTag(NodeSpinnerPositionComboBox),
                NodeSpinnerStrokeThickness = NodeSpinnerStrokeThicknessSlider.Value > 0 ? NodeSpinnerStrokeThicknessSlider.Value : 3.2,
                NodeSpinnerSpinSeconds = NodeSpinnerSpinSecondsSlider.Value > 0 ? NodeSpinnerSpinSecondsSlider.Value : 1.1,
                NodeSpinnerBlinkBackground = NodeSpinnerBlinkBackgroundCheckBox.IsChecked == true,
                NodeSpinnerBlinkBackgroundColorKey = SelectedTag(NodeSpinnerBlinkBackgroundColorComboBox),
                NodeSpinnerBlinkMode = SelectedTag(NodeSpinnerBlinkModeComboBox),
                NodeSpinnerBlinkIntensity = NodeSpinnerBlinkIntensitySlider.Value > 0 ? NodeSpinnerBlinkIntensitySlider.Value : 0.65,
                NodeSpinnerBlinkBaseOpacity = blinkBase,
                NodeSpinnerBlinkPeakOpacity = blinkPeak,
                ApplyDebounceMs = SelectedDebounceMs(),
                UiAnimationsEnabled = UiAnimationsEnabledCheckBox.IsChecked == true,
                StrictFinalSyncEnabled = StrictFinalSyncEnabledCheckBox.IsChecked == true
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
            UiAnimationsEnabledCheckBox.Checked += AnyControlChanged;
            UiAnimationsEnabledCheckBox.Unchecked += AnyControlChanged;
            StrictFinalSyncEnabledCheckBox.Checked += AnyControlChanged;
            StrictFinalSyncEnabledCheckBox.Unchecked += AnyControlChanged;
            BulkTitleColorKeyComboBox.SelectionChanged += AnyControlChanged;
            EnergyCustomColorKeyComboBox.SelectionChanged += AnyControlChanged;
            CacheNodeCheckBox.Checked += CacheNodeCheckBox_Checked;
            CacheNodeCheckBox.Unchecked += CacheNodeCheckBox_Unchecked;
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
            NodeSpinnerArcModeCheckBox.Checked += AnyControlChanged;
            NodeSpinnerArcModeCheckBox.Unchecked += AnyControlChanged;
            NodeSpinnerMultiColorCheckBox.Checked += AnyControlChanged;
            NodeSpinnerMultiColorCheckBox.Unchecked += AnyControlChanged;
            NodeSpinnerScaleWithNodeCheckBox.Checked += AnyControlChanged;
            NodeSpinnerScaleWithNodeCheckBox.Unchecked += AnyControlChanged;
            NodeSpinnerShapeComboBox.SelectionChanged += AnyControlChanged;
            NodeSpinnerPositionComboBox.SelectionChanged += AnyControlChanged;
            NodeSpinnerBlinkBackgroundCheckBox.Checked += AnyControlChanged;
            NodeSpinnerBlinkBackgroundCheckBox.Unchecked += AnyControlChanged;
            NodeSpinnerBlinkBackgroundColorComboBox.SelectionChanged += AnyControlChanged;
            NodeSpinnerBlinkModeComboBox.SelectionChanged += AnyControlChanged;
            GetDebounceFastRadio().Checked += AnyControlChanged;
            GetDebounceBalancedRadio().Checked += AnyControlChanged;
            GetDebounceSmoothRadio().Checked += AnyControlChanged;
            EnergyDotTextBoxEvents();
        }

        private void CacheNodeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isApplying) return;

            // Remember current animation mode the first time user enables cache.
            if (_animationModeBeforeCacheTag == null)
                _animationModeBeforeCacheTag = SelectedRadioTag(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio);

            _isApplying = true;
            UpdateCacheNodeAnimationUiState();
            UpdateConstraintDependentUiState();
            _isApplying = false;
            UpdateSliderTexts();
            RecomputePresetSelectionFromFields();
            QueuePreferencesApply();
        }

        private void CacheNodeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isApplying) return;

            if (!string.IsNullOrWhiteSpace(_animationModeBeforeCacheTag))
            {
                _isApplying = true;
                // Re-enable and restore previous selection.
                SetRadioSelection(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio, _animationModeBeforeCacheTag);
                UpdateCacheNodeAnimationUiState();
                UpdateConstraintDependentUiState();
                _isApplying = false;

                _animationModeBeforeCacheTag = null;
            }
            else
            {
                _isApplying = true;
                UpdateCacheNodeAnimationUiState();
                UpdateConstraintDependentUiState();
                _isApplying = false;
            }
            UpdateSliderTexts();
            RecomputePresetSelectionFromFields();
            QueuePreferencesApply();
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
            NodeSpinnerSizeSlider.ValueChanged += AnyControlChanged;
            NodeSpinnerSizeRatioSlider.ValueChanged += AnyControlChanged;
            NodeSpinnerStrokeThicknessSlider.ValueChanged += AnyControlChanged;
            NodeSpinnerSpinSecondsSlider.ValueChanged += AnyControlChanged;
            NodeSpinnerBlinkIntensitySlider.ValueChanged += AnyControlChanged;
            NodeSpinnerBlinkBaseOpacitySlider.ValueChanged += AnyControlChanged;
            NodeSpinnerBlinkPeakOpacitySlider.ValueChanged += AnyControlChanged;
        }

        private void AnyControlChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplying) return;
            UpdateSliderTexts();
            UpdateConstraintDependentUiState();
            RecomputePresetSelectionFromFields();
            QueuePreferencesApply();
        }

        private void AnyControlChanged(object sender, RoutedEventArgs e)
        {
            if (_isApplying) return;
            UpdateSliderTexts();
            UpdateConstraintDependentUiState();
            RecomputePresetSelectionFromFields();
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
            if (NodeSpinnerSizeValueText != null) NodeSpinnerSizeValueText.Text = NodeSpinnerSizeSlider.Value.ToString("0.0");
            if (NodeSpinnerSizeRatioValueText != null) NodeSpinnerSizeRatioValueText.Text = NodeSpinnerSizeRatioSlider.Value.ToString("0.00");
            if (NodeSpinnerStrokeThicknessValueText != null) NodeSpinnerStrokeThicknessValueText.Text = NodeSpinnerStrokeThicknessSlider.Value.ToString("0.0");
            if (NodeSpinnerSpinSecondsValueText != null) NodeSpinnerSpinSecondsValueText.Text = NodeSpinnerSpinSecondsSlider.Value.ToString("0.0");
            if (NodeSpinnerBlinkIntensityValueText != null) NodeSpinnerBlinkIntensityValueText.Text = NodeSpinnerBlinkIntensitySlider.Value.ToString("0.00");
            if (NodeSpinnerBlinkBaseOpacityValueText != null) NodeSpinnerBlinkBaseOpacityValueText.Text = NodeSpinnerBlinkBaseOpacitySlider.Value.ToString("0.00");
            if (NodeSpinnerBlinkPeakOpacityValueText != null) NodeSpinnerBlinkPeakOpacityValueText.Text = NodeSpinnerBlinkPeakOpacitySlider.Value.ToString("0.00");
        }

        private void BlinkPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isApplying) return;
            if (sender is not Button btn || btn.Tag is not string presetTag) return;

            _isApplying = true;
            NodeSpinnerBlinkBackgroundCheckBox.IsChecked = true;
            switch (presetTag)
            {
                case "Light":
                    SelectByTag(NodeSpinnerBlinkModeComboBox, "Soft");
                    NodeSpinnerBlinkIntensitySlider.Value = 0.35;
                    NodeSpinnerBlinkBaseOpacitySlider.Value = 0.10;
                    NodeSpinnerBlinkPeakOpacitySlider.Value = 0.38;
                    break;
                case "Strong":
                    SelectByTag(NodeSpinnerBlinkModeComboBox, "Hard");
                    NodeSpinnerBlinkIntensitySlider.Value = 0.95;
                    NodeSpinnerBlinkBaseOpacitySlider.Value = 0.28;
                    NodeSpinnerBlinkPeakOpacitySlider.Value = 0.92;
                    break;
                case "VeryStrong":
                    SelectByTag(NodeSpinnerBlinkModeComboBox, "Hard");
                    NodeSpinnerBlinkIntensitySlider.Value = 1.00;
                    NodeSpinnerBlinkBaseOpacitySlider.Value = 0.34;
                    NodeSpinnerBlinkPeakOpacitySlider.Value = 1.00;
                    break;
                default:
                    SelectByTag(NodeSpinnerBlinkModeComboBox, "Soft");
                    NodeSpinnerBlinkIntensitySlider.Value = 0.65;
                    NodeSpinnerBlinkBaseOpacitySlider.Value = 0.16;
                    NodeSpinnerBlinkPeakOpacitySlider.Value = 0.60;
                    break;
            }
            _isApplying = false;
            UpdateSliderTexts();
            QueuePreferencesApply();
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
            // "Custom" là trạng thái hiển thị "user đang tự chỉnh", không áp preset value nào cả —
            // chỉ check button và giữ nguyên cấu hình hiện tại của user.
            if (presetTag == "Custom")
            {
                _isApplying = true;
                UncheckAllPresetButtonsExcept("Custom");
                _isApplying = false;
                return;
            }

            _isApplying = true;
            UncheckAllPresetButtonsExcept(presetTag);

            switch (presetTag)
            {
                case "Low":
                    // Máy yếu: ưu tiên FPS, tắt mọi hiệu ứng nặng.
                    SetRadioSelection(CullingProfileLowRadio, CullingProfileNormalRadio, CullingProfileHighRadio, "Low");
                    SetRadioSelection(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio, "ViewportOnly");
                    SetRadioSelection(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio, "Off");
                    SelectByTag(GpuQualityComboBox, "Low");
                    GpuEnabledCheckBox.IsChecked = true;
                    CacheNodeCheckBox.IsChecked = true;
                    UiAnimationsEnabledCheckBox.IsChecked = false;
                    EnergyDotGapSlider.Value = 12;
                    EnergyDotThicknessSlider.Value = 1.2;
                    EnergyRunSpeedSlider.Value = 1.0;
                    EnergySpinSpeedSlider.Value = 1.0;
                    EnergyMeteorModeCheckBox.IsChecked = false;
                    EnergyDotRotateCheckBox.IsChecked = false;
                    NodeSpinnerArcModeCheckBox.IsChecked = true;
                    NodeSpinnerMultiColorCheckBox.IsChecked = false;
                    NodeSpinnerBlinkBackgroundCheckBox.IsChecked = false;
                    NodeSpinnerSpinSecondsSlider.Value = 1.6;
                    break;
                case "Normal":
                    // Máy trung bình: animation cơ bản, cân bằng.
                    SetRadioSelection(CullingProfileLowRadio, CullingProfileNormalRadio, CullingProfileHighRadio, "Normal");
                    SetRadioSelection(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio, "ViewportOnly");
                    SetRadioSelection(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio, "Animated");
                    SelectByTag(GpuQualityComboBox, "Medium");
                    GpuEnabledCheckBox.IsChecked = true;
                    CacheNodeCheckBox.IsChecked = false;
                    UiAnimationsEnabledCheckBox.IsChecked = true;
                    EnergyDotGapSlider.Value = 8;
                    EnergyDotThicknessSlider.Value = 0.8;
                    EnergyRunSpeedSlider.Value = 1.4;
                    EnergySpinSpeedSlider.Value = 0.8;
                    EnergyMeteorModeCheckBox.IsChecked = false;
                    EnergyDotRotateCheckBox.IsChecked = false;
                    NodeSpinnerArcModeCheckBox.IsChecked = true;
                    NodeSpinnerMultiColorCheckBox.IsChecked = false;
                    NodeSpinnerBlinkBackgroundCheckBox.IsChecked = true;
                    SelectByTag(NodeSpinnerBlinkModeComboBox, "Soft");
                    NodeSpinnerBlinkIntensitySlider.Value = 0.45;
                    NodeSpinnerBlinkBaseOpacitySlider.Value = 0.12;
                    NodeSpinnerBlinkPeakOpacitySlider.Value = 0.45;
                    NodeSpinnerSpinSecondsSlider.Value = 1.1;
                    break;
                case "High":
                    // Máy mạnh: bật đầy đủ hiệu ứng chất lượng cao.
                    SetRadioSelection(CullingProfileLowRadio, CullingProfileNormalRadio, CullingProfileHighRadio, "High");
                    SetRadioSelection(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio, "ViewportOnly");
                    SetRadioSelection(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio, "Animated");
                    SelectByTag(GpuQualityComboBox, "High");
                    GpuEnabledCheckBox.IsChecked = true;
                    CacheNodeCheckBox.IsChecked = false;
                    UiAnimationsEnabledCheckBox.IsChecked = true;
                    EnergyDotGapSlider.Value = 6;
                    EnergyDotThicknessSlider.Value = 1.6;
                    EnergyRunSpeedSlider.Value = 2.0;
                    EnergySpinSpeedSlider.Value = 0.6;
                    EnergyMeteorModeCheckBox.IsChecked = true;
                    EnergyDotRotateCheckBox.IsChecked = false;
                    NodeSpinnerArcModeCheckBox.IsChecked = true;
                    NodeSpinnerMultiColorCheckBox.IsChecked = false;
                    NodeSpinnerBlinkBackgroundCheckBox.IsChecked = true;
                    SelectByTag(NodeSpinnerBlinkModeComboBox, "Soft");
                    NodeSpinnerBlinkIntensitySlider.Value = 0.65;
                    NodeSpinnerBlinkBaseOpacitySlider.Value = 0.16;
                    NodeSpinnerBlinkPeakOpacitySlider.Value = 0.60;
                    NodeSpinnerSpinSecondsSlider.Value = 1.0;
                    break;
                case "Debug":
                    // Debug: GPU off, không cache, hiển thị mọi node.
                    SetRadioSelection(CullingProfileLowRadio, CullingProfileNormalRadio, CullingProfileHighRadio, "Low");
                    SetRadioSelection(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio, "ShowAll");
                    SetRadioSelection(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio, "Off");
                    SelectByTag(GpuQualityComboBox, "Low");
                    GpuEnabledCheckBox.IsChecked = false;
                    CacheNodeCheckBox.IsChecked = false;
                    UiAnimationsEnabledCheckBox.IsChecked = false;
                    EnergyDotGapSlider.Value = 14;
                    EnergyDotThicknessSlider.Value = 0.4;
                    EnergyRunSpeedSlider.Value = 0.8;
                    EnergySpinSpeedSlider.Value = 1.2;
                    EnergyMeteorModeCheckBox.IsChecked = false;
                    EnergyDotRotateCheckBox.IsChecked = false;
                    NodeSpinnerArcModeCheckBox.IsChecked = true;
                    NodeSpinnerMultiColorCheckBox.IsChecked = false;
                    NodeSpinnerBlinkBackgroundCheckBox.IsChecked = false;
                    NodeSpinnerSpinSecondsSlider.Value = 2.0;
                    break;
                case "Ultra":
                    // Ultra: full effect, full culling range.
                    SetRadioSelection(CullingProfileLowRadio, CullingProfileNormalRadio, CullingProfileHighRadio, "High");
                    SetRadioSelection(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio, "ShowAll");
                    SetRadioSelection(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio, "Animated");
                    SelectByTag(GpuQualityComboBox, "Best");
                    GpuEnabledCheckBox.IsChecked = true;
                    CacheNodeCheckBox.IsChecked = false;
                    UiAnimationsEnabledCheckBox.IsChecked = true;
                    EnergyDotGapSlider.Value = 5;
                    EnergyDotThicknessSlider.Value = 2.2;
                    EnergyRunSpeedSlider.Value = 2.6;
                    EnergySpinSpeedSlider.Value = 0.4;
                    EnergyMeteorModeCheckBox.IsChecked = true;
                    EnergyDotRotateCheckBox.IsChecked = true;
                    NodeSpinnerArcModeCheckBox.IsChecked = false;
                    NodeSpinnerMultiColorCheckBox.IsChecked = true;
                    NodeSpinnerBlinkBackgroundCheckBox.IsChecked = true;
                    SelectByTag(NodeSpinnerBlinkModeComboBox, "Hard");
                    NodeSpinnerBlinkIntensitySlider.Value = 0.95;
                    NodeSpinnerBlinkBaseOpacitySlider.Value = 0.28;
                    NodeSpinnerBlinkPeakOpacitySlider.Value = 0.92;
                    NodeSpinnerSpinSecondsSlider.Value = 0.8;
                    break;
            }

            UpdateSliderTexts();
            UpdateCacheNodeAnimationUiState();
            UpdateConstraintDependentUiState();
            _isApplying = false;
            QueuePreferencesApply();
        }

        private void UncheckAllPresetButtonsExcept(string selectedTag)
        {
            var presets = new[] { PresetLowButton, PresetNormalButton, PresetHighButton, PresetDebugButton, PresetUltraButton, PresetCustomButton };
            foreach (var button in presets.Where(b => b != null))
            {
                var tag = button.Tag?.ToString() ?? string.Empty;
                button.IsChecked = tag == selectedTag;
            }
        }

        /// <summary>
        /// So sánh cấu hình hiện tại với định nghĩa từng preset để chọn preset đúng,
        /// nếu không khớp preset nào (tức user đang ở trạng thái "Tuỳ chỉnh") thì bỏ check tất cả.
        /// Chỉ so sánh các field định tính (radio/combobox/checkbox) — bỏ qua slider chính xác.
        /// </summary>
        private void RecomputePresetSelectionFromFields()
        {
            if (_isApplying) return;

            var snapshot = new PresetSignature(
                Culling: SelectedRadioTag(CullingProfileLowRadio, CullingProfileNormalRadio, CullingProfileHighRadio),
                Display: SelectedRadioTag(CanvasDisplayModeAllRadio, CanvasDisplayModeViewportRadio),
                Animation: SelectedRadioTag(AnimationModeAnimatedRadio, AnimationModeOffRadio, AnimationModeDashedRadio),
                Quality: SelectedTag(GpuQualityComboBox),
                GpuEnabled: GpuEnabledCheckBox.IsChecked == true,
                CacheEnabled: CacheNodeCheckBox.IsChecked == true,
                UiAnimations: UiAnimationsEnabledCheckBox.IsChecked == true,
                MeteorMode: EnergyMeteorModeCheckBox.IsChecked == true,
                MultiColor: NodeSpinnerMultiColorCheckBox.IsChecked == true,
                BlinkBackground: NodeSpinnerBlinkBackgroundCheckBox.IsChecked == true
            );

            var matchedTag = PresetSignatures.FirstOrDefault(kv => kv.Value.Equals(snapshot)).Key;

            // Không khớp preset nào ⇒ user đang ở trạng thái tự cấu hình ⇒ check "Custom".
            var tagToSelect = matchedTag ?? "Custom";

            _isApplying = true;
            UncheckAllPresetButtonsExcept(tagToSelect);
            _isApplying = false;
        }

        private readonly record struct PresetSignature(
            string Culling,
            string Display,
            string Animation,
            string Quality,
            bool GpuEnabled,
            bool CacheEnabled,
            bool UiAnimations,
            bool MeteorMode,
            bool MultiColor,
            bool BlinkBackground);

        private static readonly Dictionary<string, PresetSignature> PresetSignatures = new()
        {
            ["Low"] = new PresetSignature("Low", "ViewportOnly", "Off", "Low",
                GpuEnabled: true, CacheEnabled: true, UiAnimations: false,
                MeteorMode: false, MultiColor: false, BlinkBackground: false),

            ["Normal"] = new PresetSignature("Normal", "ViewportOnly", "Animated", "Medium",
                GpuEnabled: true, CacheEnabled: false, UiAnimations: true,
                MeteorMode: false, MultiColor: false, BlinkBackground: true),

            ["High"] = new PresetSignature("High", "ViewportOnly", "Animated", "High",
                GpuEnabled: true, CacheEnabled: false, UiAnimations: true,
                MeteorMode: true, MultiColor: false, BlinkBackground: true),

            ["Debug"] = new PresetSignature("Low", "ShowAll", "Off", "Low",
                GpuEnabled: false, CacheEnabled: false, UiAnimations: false,
                MeteorMode: false, MultiColor: false, BlinkBackground: false),

            ["Ultra"] = new PresetSignature("High", "ShowAll", "Animated", "Best",
                GpuEnabled: true, CacheEnabled: false, UiAnimations: true,
                MeteorMode: true, MultiColor: true, BlinkBackground: true),
        };
    }
}
