using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Properties;
using FlowMy.Services.Rendering;
using FlowMy.Services.Utilities;
using FlowMy.Views.Overlays;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        #region Minimap

        /// <summary>
        /// Khởi tạo minimap
        /// </summary>
        private void InitializeMinimap()
        {
            UpdateMinimap();
        }

        /// <summary>
        /// Cập nhật minimap với tất cả nodes và connections
        /// </summary>
        private void UpdateMinimap()
        {
            _minimapService?.Update();
        }

        private void MinimapCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Có thể implement click vào minimap để pan đến vị trí đó
            // Tạm thời để trống
        }

        /// <summary>
        /// Fit to View: Zoom và pan để hiển thị tất cả nodes trong viewport
        /// </summary>
        private void FitToViewButton_Click(object sender, RoutedEventArgs e)
        {
            _minimapService?.FitToView();
        }

        /// <summary>
        /// Auto Layout: Tự động sắp xếp nodes theo hierarchical layout đẹp mắt
        /// </summary>
        private void AutoLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null || ViewModel.Nodes.Count == 0) return;

            // Xác nhận với người dùng
            //var result = MessageBox.Show(
            //    "Tự động sắp xếp lại tất cả nodes?\n(Thao tác này không thể hoàn tác)",
            //    "Auto Layout",
            //    MessageBoxButton.YesNo,
            //    MessageBoxImage.Question
            //);

            //if (result != MessageBoxResult.Yes) return;

            // Lấy viewport center để làm điểm neo
            var viewportCenter = GetViewportCenter();

            // ✅ Phase 5: AutoLayoutService sẽ tự:
            // - apply layout (connected/unconnected)
            // - redraw connections
            // - update minimap
            // - fit to view
            _layoutService?.AutoArrange(ViewModel.Nodes, ViewModel.Connections, viewportCenter);

            // Canvas size vẫn cần update để kéo/zoom mượt
            UpdateCanvasSize();
        }
        // Layout algorithms đã được chuyển sang Services/Layout (AutoLayoutService, HierarchicalLayout, GridLayout).

        /// <summary>
        /// Xử lý click button chọn style line
        /// </summary>
        private void ConnectionLineStyleButton_Click(object sender, RoutedEventArgs e)
        {
            // Context menu sẽ tự động hiển thị
        }

        private void CanvasSettingsPopupButton_Click(object sender, RoutedEventArgs e)
        {
            var current = BuildCurrentCanvasToolbarPreferences();
            var dialog = new CanvasDisplaySettingsDialog(current)
            {
                Owner = this
            };
            dialog.PreferencesChanged += preferences => ApplyCanvasToolbarPreferences(preferences, saveToDisk: true);
            dialog.ShowDialog();
        }

        /// <summary>
        /// Mở dialog cấu hình Floating Widget — cho phép chọn node bất kỳ trên canvas
        /// hiện tại và thiết lập widget nổi.
        /// </summary>
        private void OpenFloatingWidgetButton_Click(object sender, RoutedEventArgs e)
        {
            var nodes = ViewModel?.Nodes?.ToList() ?? new System.Collections.Generic.List<Models.WorkflowNode>();
            if (nodes.Count == 0)
            {
                System.Windows.MessageBox.Show(this,
                    "Canvas hiện tại chưa có node nào để cấu hình widget.",
                    "Floating Widget",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var dialog = new FloatingWidgetConfigDialog(nodes, this)
            {
                Owner = this
            };
            dialog.ShowDialog();
        }

        /// <summary>
        /// Chọn style Bezier
        /// </summary>
        private void LineStyle_Bezier_Click(object sender, RoutedEventArgs e)
        {
            _connectionLineStyle = ConnectionLineStyle.Bezier;
            if (ViewModel != null)
            {
                ViewModel.ConnectionLineStyle = ConnectionLineStyle.Bezier;
            }
            UpdateAllConnectionPaths();
            RefreshConditionalDiamondLineStyles();
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        /// <summary>
        /// Chọn style Orthogonal (vuông góc)
        /// </summary>
        private void LineStyle_Orthogonal_Click(object sender, RoutedEventArgs e)
        {
            _connectionLineStyle = ConnectionLineStyle.Orthogonal;
            if (ViewModel != null)
            {
                ViewModel.ConnectionLineStyle = ConnectionLineStyle.Orthogonal;
            }
            UpdateAllConnectionPaths();
            RefreshConditionalDiamondLineStyles();
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        /// <summary>
        /// Chọn style Straight (thẳng)
        /// </summary>
        private void LineStyle_Straight_Click(object sender, RoutedEventArgs e)
        {
            _connectionLineStyle = ConnectionLineStyle.Straight;
            if (ViewModel != null)
            {
                ViewModel.ConnectionLineStyle = ConnectionLineStyle.Straight;
            }
            UpdateAllConnectionPaths();
            RefreshConditionalDiamondLineStyles();
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        /// <summary>
        /// Chọn style Smooth-Orthogonal (vuông góc bo tròn mạnh)
        /// </summary>
        private void LineStyle_SmoothOrthogonal_Click(object sender, RoutedEventArgs e)
        {
            _connectionLineStyle = ConnectionLineStyle.SmoothOrthogonal;
            if (ViewModel != null)
            {
                ViewModel.ConnectionLineStyle = ConnectionLineStyle.SmoothOrthogonal;
            }
            UpdateAllConnectionPaths();
            RefreshConditionalDiamondLineStyles();
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        /// <summary>
        /// Chọn style Arc (cung tròn)
        /// </summary>
        private void LineStyle_Arc_Click(object sender, RoutedEventArgs e)
        {
            _connectionLineStyle = ConnectionLineStyle.Arc;
            if (ViewModel != null)
            {
                ViewModel.ConnectionLineStyle = ConnectionLineStyle.Arc;
            }
            UpdateAllConnectionPaths();
            RefreshConditionalDiamondLineStyles();
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        /// <summary>
        /// Chọn style Radial / Fan-out (tỏa quạt)
        /// </summary>
        private void LineStyle_RadialFanout_Click(object sender, RoutedEventArgs e)
        {
            _connectionLineStyle = ConnectionLineStyle.RadialFanout;
            if (ViewModel != null)
            {
                ViewModel.ConnectionLineStyle = ConnectionLineStyle.RadialFanout;
            }
            UpdateAllConnectionPaths();
            RefreshConditionalDiamondLineStyles();
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        /// <summary>
        /// Chọn style Windy (gió thổi)
        /// </summary>
        private void LineStyle_Windy_Click(object sender, RoutedEventArgs e)
        {
            _connectionLineStyle = ConnectionLineStyle.Windy;
            if (ViewModel != null)
            {
                ViewModel.ConnectionLineStyle = ConnectionLineStyle.Windy;
            }
            UpdateAllConnectionPaths();
            RefreshConditionalDiamondLineStyles();
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        /// <summary>
        /// Chọn style Orthogonal V2 (vuông góc thông minh, tránh node)
        /// </summary>
        private void LineStyle_OrthogonalV2_Click(object sender, RoutedEventArgs e)
        {
            _connectionLineStyle = ConnectionLineStyle.OrthogonalV2;
            if (ViewModel != null)
            {
                ViewModel.ConnectionLineStyle = ConnectionLineStyle.OrthogonalV2;
            }
            UpdateAllConnectionPaths();
            RefreshConditionalDiamondLineStyles();
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        // NOTE: UpdateAllConnectionPaths đã được tách sang Services/Rendering/ConnectionRenderer

        /// <summary>
        /// Xử lý click button chọn màu connection
        /// </summary>
        private void ConnectionColorButton_Click(object sender, RoutedEventArgs e)
        {
            // Context menu sẽ tự động hiển thị
        }
        
        private void CanvasDisplayModeButton_Click(object sender, RoutedEventArgs e)
        {
            // Context menu sẽ tự động hiển thị.
        }

        private void CanvasDisplayMode_All_Click(object sender, RoutedEventArgs e)
        {
            ApplyCanvasDisplayMode(CanvasDisplayMode.ShowAll);
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        private void CanvasDisplayMode_Viewport_Click(object sender, RoutedEventArgs e)
        {
            ApplyCanvasDisplayMode(CanvasDisplayMode.ViewportOnly);
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        private void ApplyCanvasDisplayMode(CanvasDisplayMode mode, bool forceRefresh = true)
        {
            _canvasDisplayMode = mode;

            var showAll = mode == CanvasDisplayMode.ShowAll;
            if (CanvasDisplayAllMenuItem != null) CanvasDisplayAllMenuItem.IsChecked = showAll;
            if (CanvasDisplayViewportMenuItem != null) CanvasDisplayViewportMenuItem.IsChecked = !showAll;

            if (_viewportCullingService != null)
            {
                _viewportCullingService.IsEnabled = !showAll;
                _viewportCullingService.ForceShowTitleForVisibleNodes = !showAll;
                // Luôn culling theo viewport kể cả lúc chạy workflow, không ẩn theo "running nodes only".
                _viewportCullingService.FocusRunningNodesWhenExecuting = false;
                _viewportCullingService.UseStrictViewportCulling = !showAll;
            }

            RefreshCanvasViewportStatsText();

            if (forceRefresh)
            {
                if (showAll)
                {
                    _viewportCullingService?.ForceShowEverything();
                }
                else
                {
                    _viewportCullingService?.ForceUpdate();
                }
            }
        }

        private void CanvasPerformanceProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewportCullingService == null) return;
            if (sender is not ComboBox cb || cb.SelectedItem is not ComboBoxItem item) return;
            if (item.Tag is not string profileTag) return;

            var profile = profileTag switch
            {
                "Low" => ViewportCullingService.CullingPerformanceProfile.Low,
                "High" => ViewportCullingService.CullingPerformanceProfile.High,
                _ => ViewportCullingService.CullingPerformanceProfile.Normal
            };

            _viewportCullingService.PerformanceProfile = profile;
            RefreshCanvasViewportStatsText();
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        private void CanvasPerformanceProfileComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ComboBox cb) return;
            if (cb.SelectedItem != null) return;
            if (cb.Items.Count > 1)
            {
                cb.SelectedIndex = 1; // Máy bình thường
            }

            if (_viewportCullingService != null)
            {
                _viewportCullingService.VisibilityStatsChanged -= ViewportCullingService_VisibilityStatsChanged;
                _viewportCullingService.VisibilityStatsChanged += ViewportCullingService_VisibilityStatsChanged;
            }
            RefreshCanvasViewportStatsText();
        }

        private void AnimationToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // Context menu tự hiển thị.
        }

        private void AnimationMode_Animated_Click(object sender, RoutedEventArgs e)
        {
            SetConnectionAnimationDisplayMode(ConnectionAnimationDisplayMode.Animated);
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        private void AnimationMode_Off_Click(object sender, RoutedEventArgs e)
        {
            SetConnectionAnimationDisplayMode(ConnectionAnimationDisplayMode.Off);
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        private void AnimationMode_Dashed_Click(object sender, RoutedEventArgs e)
        {
            SetConnectionAnimationDisplayMode(ConnectionAnimationDisplayMode.Dashed);
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        private void SetConnectionAnimationDisplayMode(ConnectionAnimationDisplayMode mode)
        {
            _connectionAnimationDisplayMode = mode;
            _isAnimationEnabled = mode == ConnectionAnimationDisplayMode.Animated;
            UpdateAnimationModeMenuState();
            _eventService.SetConnectionAnimationDisplayMode(mode);
        }

        private ConnectionAnimationDisplayMode GetCurrentConnectionAnimationDisplayMode()
        {
            return _connectionAnimationDisplayMode;
        }

        private void UpdateAnimationModeMenuState()
        {
            if (AnimationModeAnimatedMenuItem != null)
                AnimationModeAnimatedMenuItem.IsChecked = _connectionAnimationDisplayMode == ConnectionAnimationDisplayMode.Animated;
            if (AnimationModeOffMenuItem != null)
                AnimationModeOffMenuItem.IsChecked = _connectionAnimationDisplayMode == ConnectionAnimationDisplayMode.Off;
            if (AnimationModeDashedMenuItem != null)
                AnimationModeDashedMenuItem.IsChecked = _connectionAnimationDisplayMode == ConnectionAnimationDisplayMode.Dashed;
        }

        private void ViewportCullingService_VisibilityStatsChanged(int visibleNodes, int totalNodes)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (CanvasViewportStatsText != null)
                {
                    CanvasViewportStatsText.Text = $"Visible: {visibleNodes} / {totalNodes}";
                }
            }));
        }

        private void RefreshCanvasViewportStatsText()
        {
            if (CanvasViewportStatsText == null) return;
            if (ViewModel?.Nodes == null)
            {
                CanvasViewportStatsText.Text = "Visible: 0 / 0";
                return;
            }

            var total = ViewModel.Nodes.Count;
            var visible = ViewModel.Nodes.Count(n => n.Border?.Visibility == Visibility.Visible);
            CanvasViewportStatsText.Text = $"Visible: {visible} / {total}";
        }

        /// <summary>
        /// Xử lý click button chọn màu năng lượng (connection đang chạy)
        /// </summary>
        private void EnergyColorButton_Click(object sender, RoutedEventArgs e)
        {
            // Context menu sẽ tự động hiển thị
        }

        /// <summary>
        /// Chọn mode màu theo node
        /// </summary>
        private void ConnectionColorMode_NodeColor_Click(object sender, RoutedEventArgs e)
        {
            _connectionColorMode = ConnectionColorMode.NodeColor;
            UpdateAllConnectionColors();
            CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
        }

        /// <summary>
        /// Chọn mode màu tùy chọn
        /// </summary>
        private void ConnectionColorMode_CustomColor_Click(object sender, RoutedEventArgs e)
        {
            // Context menu sẽ hiển thị các màu
        }

        /// <summary>
        /// Chọn màu tùy chọn
        /// </summary>
        private void CustomColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string colorKey)
            {
                _connectionColorMode = ConnectionColorMode.CustomColor;
                _customConnectionColorKey = colorKey;

                // Lấy màu từ theme
                var brush = Application.Current.TryFindResource(colorKey) as SolidColorBrush;
                if (brush != null)
                {
                    _customConnectionColor = brush.Color;
                }
                else if (colorKey == "LimeGreen")
                {
                    _customConnectionColor = Colors.LimeGreen;
                }

                UpdateAllConnectionColors();
                CanvasToolbarPreferencesStore.Save(BuildCurrentCanvasToolbarPreferences());
            }
        }

        private CanvasToolbarPreferences BuildCurrentCanvasToolbarPreferences()
        {
            var persisted = CanvasToolbarPreferencesStore.Load() ?? new CanvasToolbarPreferences();
            var profile = _viewportCullingService?.PerformanceProfile switch
            {
                ViewportCullingService.CullingPerformanceProfile.Low => "Low",
                ViewportCullingService.CullingPerformanceProfile.High => "High",
                _ => "Normal"
            };

            return new CanvasToolbarPreferences
            {
                GridType = _currentGridType,
                CanvasDisplayMode = _canvasDisplayMode == CanvasDisplayMode.ViewportOnly ? "ViewportOnly" : "ShowAll",
                CullingPerformanceProfile = profile,
                ConnectionLineStyle = _connectionLineStyle.ToString(),
                ConnectionAnimationMode = _connectionAnimationDisplayMode.ToString(),
                ConnectionColorMode = _connectionColorMode.ToString(),
                CustomConnectionColorKey = _customConnectionColorKey,
                GpuEnabled = _gpuEnabled,
                GpuRenderQuality = _gpuRenderQuality.ToString(),
                CacheNodeEnabled = _cacheNodeEnabled,
                BulkTitleColorMode = _bulkTitleColorMode.ToString(),
                BulkTitleColorKey = _bulkTitleColorKey,
                EnergyColorMode = _connectionEnergyColorMode.ToString(),
                CustomEnergyColorKey = _customEnergyColorKey,
                EnergyDotGap = _energyDotGap,
                EnergyDotThicknessExtra = _energyDotThicknessExtra,
                EnergyDotText = _energyDotText,
                EnergyDotTextRotate = _energyDotTextRotate,
                EnergyRunSpeed = _energyRunSpeed,
                EnergyTextSpinSeconds = _energyTextSpinSeconds,
                EnergyMeteorMode = _energyMeteorMode,
                NodeSpinnerArcMode = _nodeSpinnerArcMode,
                NodeSpinnerMultiColor = _nodeSpinnerMultiColor,
                NodeSpinnerSize = _nodeSpinnerSize,
                NodeSpinnerScaleWithNode = _nodeSpinnerScaleWithNode,
                NodeSpinnerSizeRatio = _nodeSpinnerSizeRatio,
                NodeSpinnerShape = _nodeSpinnerShape,
                NodeSpinnerPosition = _nodeSpinnerPosition,
                NodeSpinnerStrokeThickness = _nodeSpinnerStrokeThickness,
                NodeSpinnerSpinSeconds = _nodeSpinnerSpinSeconds,
                NodeSpinnerBlinkBackground = _nodeSpinnerBlinkBackground,
                NodeSpinnerBlinkBackgroundColorKey = _nodeSpinnerBlinkBackgroundColorKey,
                NodeSpinnerBlinkMode = _nodeSpinnerBlinkMode,
                NodeSpinnerBlinkIntensity = _nodeSpinnerBlinkIntensity,
                NodeSpinnerBlinkBaseOpacity = _nodeSpinnerBlinkBaseOpacity,
                NodeSpinnerBlinkPeakOpacity = _nodeSpinnerBlinkPeakOpacity,
                UiAnimationsEnabled = persisted.UiAnimationsEnabled
            };
        }

        internal void ApplyCanvasToolbarPreferences()
        {
            ApplyCanvasToolbarPreferences(CanvasToolbarPreferencesStore.Load(), saveToDisk: false);
        }

        private void ApplyCanvasToolbarPreferences(CanvasToolbarPreferences preferences, bool saveToDisk)
        {
            if (preferences == null) return;

            SetGridType(preferences.GridType);
            ApplyCanvasDisplayMode(preferences.CanvasDisplayMode == "ViewportOnly" ? CanvasDisplayMode.ViewportOnly : CanvasDisplayMode.ShowAll);

            if (_viewportCullingService != null)
            {
                _viewportCullingService.PerformanceProfile = preferences.CullingPerformanceProfile switch
                {
                    "Low" => ViewportCullingService.CullingPerformanceProfile.Low,
                    "High" => ViewportCullingService.CullingPerformanceProfile.High,
                    _ => ViewportCullingService.CullingPerformanceProfile.Normal
                };
            }

            if (Enum.TryParse(preferences.ConnectionLineStyle, out ConnectionLineStyle lineStyle))
            {
                _connectionLineStyle = lineStyle;
                if (ViewModel != null)
                {
                    ViewModel.ConnectionLineStyle = lineStyle;
                }
                UpdateAllConnectionPaths();
                RefreshConditionalDiamondLineStyles();
            }

            // Cache node mode: bật/tắt BitmapCache cho node border và thay animation energy bằng spinner.
            _cacheNodeEnabled = preferences.CacheNodeEnabled;

            if (Enum.TryParse(preferences.ConnectionAnimationMode, out ConnectionAnimationDisplayMode animationMode))
            {
                // Cache node mode: không cho dùng Animated để tránh energy animation.
                if (_cacheNodeEnabled && animationMode == ConnectionAnimationDisplayMode.Animated)
                {
                    animationMode = ConnectionAnimationDisplayMode.Off;
                }
                SetConnectionAnimationDisplayMode(animationMode);
            }

            // Update spinner visibility immediately (node đang chạy có thể đang visible badge).
            if (ViewModel?.Nodes != null)
            {
                var useNodeExecutionIndicator =
                    _cacheNodeEnabled ||
                    _connectionAnimationDisplayMode != ConnectionAnimationDisplayMode.Animated;

                foreach (var node in ViewModel.Nodes)
                {
                    if (node.ExecutionBusySpinnerUI == null) continue;

                    node.ExecutionBusySpinnerUI.Tag = useNodeExecutionIndicator;
                    var isExecutingNow = node.ExecutionStatusTextUI?.Text?.StartsWith("⏳", System.StringComparison.Ordinal) == true;
                    node.ExecutionBusySpinnerUI.Visibility = (useNodeExecutionIndicator && isExecutingNow)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }

            if (preferences.ConnectionColorMode == nameof(ConnectionColorMode.CustomColor))
            {
                _connectionColorMode = ConnectionColorMode.CustomColor;
                _customConnectionColorKey = string.IsNullOrWhiteSpace(preferences.CustomConnectionColorKey) ? "LimeGreen" : preferences.CustomConnectionColorKey;
                var brush = Application.Current.TryFindResource(_customConnectionColorKey) as SolidColorBrush;
                _customConnectionColor = brush?.Color ?? Colors.LimeGreen;
            }
            else
            {
                _connectionColorMode = ConnectionColorMode.NodeColor;
            }
            UpdateAllConnectionColors();

            if (Enum.TryParse(preferences.GpuRenderQuality, out GpuRenderQuality gpuQuality))
            {
                GpuRenderQuality = gpuQuality;
            }
            GpuEnabled = preferences.GpuEnabled;

            // Cache node toggle không kích hoạt ApplyGpuSettings qua setter, nên gọi lại để áp BitmapCache.
            ApplyGpuSettings();

            if (preferences.BulkTitleColorMode == nameof(TitleColorMode.CustomColor))
            {
                var titleColorKey = string.IsNullOrWhiteSpace(preferences.BulkTitleColorKey) ? "PrimaryBrush" : preferences.BulkTitleColorKey;
                ApplyBulkTitleColor(titleColorKey, TitleColorMode.CustomColor);
            }
            else
            {
                ApplyBulkTitleColor(null, TitleColorMode.NodeColor);
            }

            if (preferences.EnergyColorMode == nameof(ConnectionEnergyColorMode.CustomColor))
            {
                _connectionEnergyColorMode = ConnectionEnergyColorMode.CustomColor;
                _customEnergyColorKey = string.IsNullOrWhiteSpace(preferences.CustomEnergyColorKey) ? "Gold" : preferences.CustomEnergyColorKey;
                if (_customEnergyColorKey == "Gold")
                {
                    _customEnergyColor = Color.FromRgb(255, 215, 0);
                }
                else
                {
                    var energyBrush = Application.Current.TryFindResource(_customEnergyColorKey) as SolidColorBrush;
                    _customEnergyColor = energyBrush?.Color ?? Colors.LimeGreen;
                }
            }
            else
            {
                _connectionEnergyColorMode = ConnectionEnergyColorMode.FollowLineColor;
            }
            _energyDotGap = preferences.EnergyDotGap > 0 ? preferences.EnergyDotGap : _energyDotGap;
            _energyDotThicknessExtra = preferences.EnergyDotThicknessExtra;
            _energyDotText = preferences.EnergyDotText ?? string.Empty;
            _energyDotTextRotate = preferences.EnergyDotTextRotate;
            _energyRunSpeed = preferences.EnergyRunSpeed > 0 ? preferences.EnergyRunSpeed : _energyRunSpeed;
            _energyTextSpinSeconds = preferences.EnergyTextSpinSeconds > 0 ? preferences.EnergyTextSpinSeconds : _energyTextSpinSeconds;
            _energyMeteorMode = preferences.EnergyMeteorMode;
            _nodeSpinnerArcMode = preferences.NodeSpinnerArcMode;
            _nodeSpinnerMultiColor = preferences.NodeSpinnerMultiColor;
            _nodeSpinnerSize = preferences.NodeSpinnerSize > 8 ? preferences.NodeSpinnerSize : 26.0;
            _nodeSpinnerScaleWithNode = preferences.NodeSpinnerScaleWithNode;
            _nodeSpinnerSizeRatio = preferences.NodeSpinnerSizeRatio > 0 ? preferences.NodeSpinnerSizeRatio : 0.32;
            _nodeSpinnerShape = string.IsNullOrWhiteSpace(preferences.NodeSpinnerShape) ? "Circle" : preferences.NodeSpinnerShape;
            _nodeSpinnerPosition = string.IsNullOrWhiteSpace(preferences.NodeSpinnerPosition) ? "TopRight" : preferences.NodeSpinnerPosition;
            _nodeSpinnerStrokeThickness = preferences.NodeSpinnerStrokeThickness > 0 ? preferences.NodeSpinnerStrokeThickness : 3.2;
            _nodeSpinnerSpinSeconds = preferences.NodeSpinnerSpinSeconds > 0 ? preferences.NodeSpinnerSpinSeconds : 1.1;
            _nodeSpinnerBlinkBackground = preferences.NodeSpinnerBlinkBackground;
            _nodeSpinnerBlinkBackgroundColorKey = string.IsNullOrWhiteSpace(preferences.NodeSpinnerBlinkBackgroundColorKey) ? "WarningBrush" : preferences.NodeSpinnerBlinkBackgroundColorKey;
            _nodeSpinnerBlinkMode = string.IsNullOrWhiteSpace(preferences.NodeSpinnerBlinkMode) ? "Soft" : preferences.NodeSpinnerBlinkMode;
            _nodeSpinnerBlinkIntensity = preferences.NodeSpinnerBlinkIntensity > 0
                ? System.Math.Max(0.10, System.Math.Min(1.0, preferences.NodeSpinnerBlinkIntensity))
                : 0.65;
            _nodeSpinnerBlinkBaseOpacity = preferences.NodeSpinnerBlinkBaseOpacity >= 0
                ? System.Math.Max(0.0, System.Math.Min(1.0, preferences.NodeSpinnerBlinkBaseOpacity))
                : 0.16;
            _nodeSpinnerBlinkPeakOpacity = preferences.NodeSpinnerBlinkPeakOpacity > 0
                ? System.Math.Max(0.0, System.Math.Min(1.0, preferences.NodeSpinnerBlinkPeakOpacity))
                : 0.60;
            if (_nodeSpinnerBlinkPeakOpacity <= _nodeSpinnerBlinkBaseOpacity)
                _nodeSpinnerBlinkPeakOpacity = System.Math.Min(1.0, _nodeSpinnerBlinkBaseOpacity + 0.02);
            Settings.Default.EnergyDotGap = _energyDotGap;
            Settings.Default.EnergyDotThicknessExtra = _energyDotThicknessExtra;
            Settings.Default.EnergyDotText = _energyDotText;
            Settings.Default.EnergyDotTextRotate = _energyDotTextRotate;
            Settings.Default.EnergyRunSpeed = _energyRunSpeed;
            Settings.Default.EnergyTextSpinSeconds = _energyTextSpinSeconds;
            Settings.Default.Save();
            RefreshExecutionEnergyVisual();
            if (ViewModel?.Nodes != null)
                NodeChrome.RefreshExecutionIndicators(ViewModel.Nodes, this);

            if (CanvasPerformanceProfileComboBox != null)
            {
                foreach (var item in CanvasPerformanceProfileComboBox.Items.OfType<ComboBoxItem>())
                {
                    if ((item.Tag?.ToString() ?? string.Empty) == preferences.CullingPerformanceProfile)
                    {
                        CanvasPerformanceProfileComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            if (saveToDisk)
            {
                CanvasToolbarPreferencesStore.Save(preferences);
            }
        }

        /// <summary>
        /// Xử lý click button chọn màu title hàng loạt
        /// </summary>
        private void BulkTitleColorButton_Click(object sender, RoutedEventArgs e)
        {
            // Context menu sẽ tự động hiển thị
        }

        /// <summary>
        /// Đổi màu title tất cả nodes về màu theo node
        /// </summary>
        private void BulkTitleColor_NodeColor_Click(object sender, RoutedEventArgs e)
        {
            ApplyBulkTitleColor(null, TitleColorMode.NodeColor);
        }

        /// <summary>
        /// Đổi màu title tất cả nodes theo màu tùy chọn
        /// </summary>
        private void BulkTitleColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string colorKey)
            {
                ApplyBulkTitleColor(colorKey, TitleColorMode.CustomColor);
            }
        }

        /// <summary>
        /// Áp dụng màu title cho tất cả nodes có hỗ trợ TitleColorMode
        /// </summary>
        private void ApplyBulkTitleColor(string? colorKey, TitleColorMode colorMode)
        {
            if (ViewModel?.Nodes == null) return;

            foreach (var node in ViewModel.Nodes)
            {
                // Cập nhật TitleColorMode và TitleColorKey cho từng loại node
                switch (node)
                {
                    case StringSplitNode n:
                        n.TitleColorMode = colorMode;
                        n.TitleColorKey = colorMode == TitleColorMode.NodeColor ? null : colorKey;
                        break;
                    case HttpRequestNode n:
                        n.TitleColorMode = colorMode;
                        n.TitleColorKey = colorMode == TitleColorMode.NodeColor ? null : colorKey;
                        break;
                    case MouseEventNode n:
                        n.TitleColorMode = colorMode;
                        n.TitleColorKey = colorMode == TitleColorMode.NodeColor ? null : colorKey;
                        break;
                    case LoopNode n:
                        n.TitleColorMode = colorMode;
                        n.TitleColorKey = colorMode == TitleColorMode.NodeColor ? null : colorKey;
                        break;
                    case InputNode n:
                        n.TitleColorMode = colorMode;
                        n.TitleColorKey = colorMode == TitleColorMode.NodeColor ? null : colorKey;
                        break;
                    case OutputNode n:
                        n.TitleColorMode = colorMode;
                        n.TitleColorKey = colorMode == TitleColorMode.NodeColor ? null : colorKey;
                        break;
                    case ListOutNode n:
                        n.TitleColorMode = colorMode;
                        n.TitleColorKey = colorMode == TitleColorMode.NodeColor ? null : colorKey;
                        break;
                    case HotkeyPressEventNode n:
                        n.TitleColorMode = colorMode;
                        n.TitleColorKey = colorMode == TitleColorMode.NodeColor ? null : colorKey;
                        break;
                    case KeyPressEventNode n:
                        n.TitleColorMode = colorMode;
                        n.TitleColorKey = colorMode == TitleColorMode.NodeColor ? null : colorKey;
                        break;
                    case CodeNode n:
                        n.TitleColorMode = colorMode;
                        n.TitleColorKey = colorMode == TitleColorMode.NodeColor ? null : colorKey;
                        break;
                    case MediaGalleryNode n:
                        n.TitleColorMode = colorMode;
                        n.TitleColorKey = colorMode == TitleColorMode.NodeColor ? null : colorKey;
                        break;
                    case ImageProcessingNode n:
                        n.TitleColorMode = colorMode;
                        n.TitleColorKey = colorMode == TitleColorMode.NodeColor ? null : colorKey;
                        break;
                }

                // Cập nhật màu TitleTextBlockUI ngay lập tức nếu có
                UpdateNodeTitleColor(node, colorMode, colorKey);
            }

            // Lưu lại màu hiện tại để áp dụng cho node mới
            _bulkTitleColorMode = colorMode;
            _bulkTitleColorKey = colorMode == TitleColorMode.NodeColor ? null : colorKey;
        }

        // Lưu màu title hàng loạt hiện tại để áp dụng cho node mới
        private TitleColorMode _bulkTitleColorMode = TitleColorMode.NodeColor;
        private string? _bulkTitleColorKey = null;

        /// <summary>
        /// Cập nhật màu TitleTextBlockUI cho một node
        /// </summary>
        private void UpdateNodeTitleColor(WorkflowNode node, TitleColorMode colorMode, string? colorKey)
        {
            if (node.TitleTextBlockUI == null) return;

            Brush? brush = null;

            if (colorMode == TitleColorMode.NodeColor)
            {
                brush = node.NodeBrush;
            }
            else if (colorKey == "LimeGreen")
            {
                brush = new SolidColorBrush(Colors.LimeGreen);
            }
            else if (!string.IsNullOrEmpty(colorKey))
            {
                brush = Application.Current.TryFindResource(colorKey) as Brush;
            }

            if (brush != null)
            {
                node.TitleTextBlockUI.Foreground = brush;
            }
        }

        /// <summary>
        /// Lấy màu title hàng loạt hiện tại (được áp dụng cho node mới)
        /// </summary>
        public (TitleColorMode colorMode, string? colorKey) GetBulkTitleColor()
        {
            return (_bulkTitleColorMode, _bulkTitleColorKey);
        }

        private void EnergyColorMode_FollowLineColor_Click(object sender, RoutedEventArgs e)
        {
            _connectionEnergyColorMode = ConnectionEnergyColorMode.FollowLineColor;
            RefreshExecutionEnergyVisual();
        }

        private void EnergyColorMode_CustomColor_Click(object sender, RoutedEventArgs e)
        {
            // Context menu sẽ hiển thị các màu
        }

        private void CustomEnergyColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string colorKey)
            {
                _connectionEnergyColorMode = ConnectionEnergyColorMode.CustomColor;
                _customEnergyColorKey = colorKey;

                if (colorKey == "Gold")
                {
                    _customEnergyColor = Color.FromRgb(255, 215, 0);
                }
                else
                {
                    // Lấy màu từ theme
                    var brush = Application.Current.TryFindResource(colorKey) as SolidColorBrush;
                    if (brush != null)
                    {
                        _customEnergyColor = brush.Color;
                    }
                    else if (colorKey == "LimeGreen")
                    {
                        _customEnergyColor = Colors.LimeGreen;
                    }
                }

                RefreshExecutionEnergyVisual();
            }
        }

        private void RefreshExecutionEnergyVisual()
        {
            // Chỉ cần refresh connection đang active (nếu có)
            RefreshExecutionConnectionHighlight();
        }

        private void EnergyDotGapTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplyingEnergyMenuState) return;
            if (sender is not TextBox tb) return;
            if (double.TryParse(tb.Text, out var v) && v > 0)
            {
                _energyDotGap = v;
                Settings.Default.EnergyDotGap = v;
                Settings.Default.Save();
                RefreshExecutionEnergyVisual();
            }
        }

        private void EnergyDotThicknessTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplyingEnergyMenuState) return;
            if (sender is not TextBox tb) return;
            if (double.TryParse(tb.Text, out var v))
            {
                _energyDotThicknessExtra = v;
                Settings.Default.EnergyDotThicknessExtra = v;
                Settings.Default.Save();
                RefreshExecutionEnergyVisual();
            }
        }

        private void EnergyDotTextTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplyingEnergyMenuState) return;
            if (sender is not TextBox tb) return;
            _energyDotText = tb.Text ?? string.Empty;
            Settings.Default.EnergyDotText = _energyDotText;
            Settings.Default.Save();
            RefreshExecutionEnergyVisual();
        }

        private void EnergyDotRotateCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingEnergyMenuState) return;
            if (sender is not CheckBox cb) return;
            _energyDotTextRotate = cb.IsChecked == true;
            Settings.Default.EnergyDotTextRotate = _energyDotTextRotate;
            Settings.Default.Save();
            RefreshExecutionEnergyVisual();
        }

        private void EnergyRunSpeedTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplyingEnergyMenuState) return;
            if (sender is not TextBox tb) return;
            if (double.TryParse(tb.Text, out var v) && v > 0)
            {
                _energyRunSpeed = v;
                Settings.Default.EnergyRunSpeed = v;
                Settings.Default.Save();
                RefreshExecutionEnergyVisual();
            }
        }

        private void EnergySpinSpeedTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplyingEnergyMenuState) return;
            if (sender is not TextBox tb) return;
            if (double.TryParse(tb.Text, out var v) && v > 0)
            {
                _energyTextSpinSeconds = v;
                Settings.Default.EnergyTextSpinSeconds = v;
                Settings.Default.Save();
                RefreshExecutionEnergyVisual();
            }
        }

        private void EnergyMeteorModeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingEnergyMenuState) return;
            if (sender is not CheckBox cb) return;
            _energyMeteorMode = cb.IsChecked == true;
            RefreshExecutionEnergyVisual();
        }

        private void EnergyDotGapTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            _isApplyingEnergyMenuState = true;
            tb.Text = _energyDotGap.ToString("0.###");
            _isApplyingEnergyMenuState = false;
        }

        private void EnergyDotThicknessTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            _isApplyingEnergyMenuState = true;
            tb.Text = _energyDotThicknessExtra.ToString("0.###");
            _isApplyingEnergyMenuState = false;
        }

        private void EnergyDotTextTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            _isApplyingEnergyMenuState = true;
            tb.Text = _energyDotText ?? string.Empty;
            _isApplyingEnergyMenuState = false;
        }

        private void EnergyDotRotateCheckBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb) return;
            _isApplyingEnergyMenuState = true;
            cb.IsChecked = _energyDotTextRotate;
            _isApplyingEnergyMenuState = false;
        }

        private void EnergyRunSpeedTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            _isApplyingEnergyMenuState = true;
            tb.Text = _energyRunSpeed.ToString("0.###");
            _isApplyingEnergyMenuState = false;
        }

        private void EnergySpinSpeedTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            _isApplyingEnergyMenuState = true;
            tb.Text = _energyTextSpinSeconds.ToString("0.###");
            _isApplyingEnergyMenuState = false;
        }

        private void EnergyMeteorModeCheckBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb) return;
            _isApplyingEnergyMenuState = true;
            cb.IsChecked = _energyMeteorMode;
            _isApplyingEnergyMenuState = false;
        }

        /// <summary>
        /// Xóa tất cả node trừ Start và End (khôi phục về trạng thái ban đầu)
        /// </summary>
        private void ClearAllNodesButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            // Xác nhận với người dùng
            //var result = MessageBox.Show(
            //    "Bạn có chắc chắn muốn xóa tất cả node (giữ lại Start và End)?",
            //    "Xác nhận",
            //    MessageBoxButton.YesNo,
            //    MessageBoxImage.Question
            //);

            //if (result != MessageBoxResult.Yes) return;

            // Lấy danh sách node cần xóa (tất cả trừ Start và End)
            var nodesToDelete = ViewModel.Nodes
                .Where(n => n.Id != "Node_Start" && n.Id != "Node_End")
                .ToList();

            // Xóa tất cả connections liên quan đến các node này
            var connectionsToRemove = ViewModel.Connections
                .Where(c => nodesToDelete.Contains(c.FromNode) || nodesToDelete.Contains(c.ToNode))
                .ToList();

            foreach (var connection in connectionsToRemove)
            {
                // Xóa UI của connection
                if (connection.LineUI != null && WorkflowCanvas.Children.Contains(connection.LineUI))
                {
                    WorkflowCanvas.Children.Remove(connection.LineUI);
                }
                ViewModel.Connections.Remove(connection);
            }

            // Xóa UI và node
            foreach (var node in nodesToDelete)
            {
                // Sử dụng NodeRendererService để cleanup đúng cách (bao gồm titleTextBlock)
                NodeRendererService.RemoveNode(node, WorkflowCanvas);

                // Xóa node khỏi collection
                ViewModel.Nodes.Remove(node);
            }

            // Reset selected node
            ViewModel.SelectedNode = null;

            // Cập nhật minimap và canvas size
            UpdateMinimap();
            UpdateCanvasSize();
        }

        /// <summary>
        /// Dọn sạch toàn bộ visuals (node/port/connection) trước khi load workflow khác.
        /// Không đụng tới dữ liệu ViewModel, chỉ xóa UI trên canvas.
        /// </summary>
        private void ClearVisualsForReload()
        {
            // Xóa node/port UI
            NodeRendererService.RemoveAllNodeVisuals(WorkflowCanvas);

            // Xóa connection UI
            ConnectionRendererService.RenderAllConnections(
                Array.Empty<WorkflowConnection>(),
                setSelectedConnection: c => _selectedConnection = c,
                focusWindow: () => Focus(),
                requestDeleteConnection: DeleteConnection);
            _selectedConnection = null;

            UpdateMinimap();
            UpdateCanvasSize();
        }

        // NOTE: UpdateAllConnectionColors đã được tách sang Services/Rendering/ConnectionRenderer

        #endregion
    }
}

