using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using FlowMy.Views.NodeControls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using WinForms = System.Windows.Forms;

namespace FlowMy.Views.Overlays
{
    /// <summary>
    /// Dialog cấu hình Floating Widget cho bất kỳ node nào thuộc workflow đang mở.
    /// Thay thế cho menu chuột phải "📌 Mở Widget" trên node.
    /// </summary>
    public partial class FloatingWidgetConfigDialog : Window
    {
        private readonly IReadOnlyList<WorkflowNode> _nodes;
        private readonly IWorkflowEditorHost? _host;
        private readonly System.Action? _persistChanges;
        private readonly bool _runtimeActionsEnabled;
        private readonly ObservableCollection<WorkflowDataSourceOption> _nodeOptions = new();
        private WorkflowNode? _selectedNode;
        private bool _loadingValues;
        private string? _idleBackgroundColorHex;
        private string? _idleForegroundColorHex;

        public FloatingWidgetConfigDialog(IEnumerable<WorkflowNode> nodes, IWorkflowEditorHost host)
            : this(nodes, host, persistChanges: null, runtimeActionsEnabled: true)
        {
        }

        public FloatingWidgetConfigDialog(
            IEnumerable<WorkflowNode> nodes,
            IWorkflowEditorHost? host,
            System.Action? persistChanges,
            bool runtimeActionsEnabled = true)
        {
            InitializeComponent();

            _nodes = (nodes ?? Enumerable.Empty<WorkflowNode>())
                .Where(n => n != null && n.Type != NodeType.Start && n.Type != NodeType.End)
                .OrderBy(n => string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title)
                .ToList();
            _host = host;
            _persistChanges = persistChanges;
            _runtimeActionsEnabled = runtimeActionsEnabled && host != null;

            PopulateNodeList();
            PopulateMonitorList();
            RefreshExistingWidgets();
            ApplyRuntimeActionsUiState();
        }

        private void ApplyRuntimeActionsUiState()
        {
            if (_runtimeActionsEnabled) return;

            if (OpenWidgetButton != null) OpenWidgetButton.IsEnabled = false;
            if (OpenAllWidgetsButton != null) OpenAllWidgetsButton.IsEnabled = false;
            if (CloseWidgetButton != null) CloseWidgetButton.IsEnabled = false;
            if (CloseAllWidgetsButton != null) CloseAllWidgetsButton.IsEnabled = false;
            if (RuntimeModeHintText != null) RuntimeModeHintText.Visibility = Visibility.Visible;
        }

        private void PopulateNodeList()
        {
            _nodeOptions.Clear();
            foreach (var node in _nodes)
            {
                _nodeOptions.Add(BaseNodeDialogViewModel.CreateDataSourceOption(node));
            }

            NodeComboBox.ItemsSource = _nodeOptions;
            if (_nodeOptions.Count > 0)
                NodeComboBox.SelectedValue = _nodeOptions[0].NodeId;
            else
                WidgetStatusText.Text = "Canvas chưa có node nào.";
        }

        /// <summary>
        /// Render danh sách các widget đã bật (IsEnabled=true) trong workflow hiện tại
        /// thành các chip có thể click để chọn nhanh.
        /// </summary>
        private void RefreshExistingWidgets()
        {
            if (ExistingWidgetsItems == null) return;
            ExistingWidgetsItems.Items.Clear();

            var configured = _nodes
                .Where(n => n.FloatingWidget is { IsEnabled: true })
                .ToList();

            WidgetCountBadge.Text = configured.Count == 0
                ? "Chưa có widget"
                : $"{configured.Count} widget";

            if (configured.Count == 0)
            {
                var empty = new TextBlock
                {
                    Text = "Chưa có widget nào được bật. Hãy chọn 1 node bên dưới và tích \"Bật chế độ Floating Widget\".",
                    Foreground = (Brush?)Application.Current.TryFindResource("TextBrush") ?? Brushes.Gray,
                    Opacity = 0.6,
                    FontSize = 12,
                    Margin = new Thickness(6, 4, 6, 4),
                    TextWrapping = TextWrapping.Wrap
                };
                ExistingWidgetsItems.Items.Add(empty);
                return;
            }

            foreach (var node in configured)
            {
                var chip = BuildWidgetChip(node);
                ExistingWidgetsItems.Items.Add(chip);
            }
        }

        private UIElement BuildWidgetChip(WorkflowNode node)
        {
            var cfg = node.FloatingWidget!;
            var name = string.IsNullOrWhiteSpace(cfg.WidgetName) ? (node.Title ?? "Widget") : cfg.WidgetName;
            var isOpen = FloatingWidgetManager.Instance.IsWidgetOpen(node.Id);
            var isSelected = ReferenceEquals(_selectedNode, node);
            var isEnabled = cfg.IsEnabled;

            var primary = (Brush?)Application.Current.TryFindResource("PrimaryBrush") ?? Brushes.SlateBlue;
            var secondary = (Brush?)Application.Current.TryFindResource("SecondaryBrush") ?? Brushes.Gray;
            var primaryHover = (Brush?)Application.Current.TryFindResource("PrimaryHoverBrush") ?? primary;
            var secondaryHover = (Brush?)Application.Current.TryFindResource("SecondaryHoverBrush") ?? secondary;
            var activeRoyalPurple = ParseHexBrush("#7851A9") ?? ((Brush?)Application.Current.TryFindResource("PrimaryBrush") ?? Brushes.SlateBlue);
            var coralVivid = ParseHexBrush("#FF6F61") ?? ((Brush?)Application.Current.TryFindResource("SuccessBrush") ?? Brushes.OrangeRed);
            var coralVividHover = ParseHexBrush("#FF816F") ?? coralVivid;
            var subtleSecondary = CreateSubtleBrush(secondary, 0.2);
            var subtleSecondaryHover = CreateSubtleBrush(secondaryHover, 0.3);

            var baseBg = isSelected
                ? activeRoyalPurple
                : (isEnabled ? coralVivid : subtleSecondary);
            var hoverBg = isSelected
                ? primaryHover
                : (isEnabled ? coralVividHover : subtleSecondaryHover);

            var border = new Border
            {
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(4),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(1),
                Background = baseBg,
                BorderBrush = isSelected
                    ? activeRoyalPurple
                    : (isEnabled ? coralVivid : secondary)
            };

            var panel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            var onPrimary = (Brush?)Application.Current.TryFindResource("TextOnPrimaryBrush") ?? Brushes.White;
            var textBrush = (Brush?)Application.Current.TryFindResource("TextBrush") ?? Brushes.WhiteSmoke;
            panel.Children.Add(new TextBlock
            {
                Text = isOpen ? "●" : "○",
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = isOpen
                    ? ((Brush?)Application.Current.TryFindResource("SuccessBrush") ?? Brushes.LimeGreen)
                    : (isSelected ? onPrimary : textBrush),
                FontSize = 12
            });
            panel.Children.Add(new TextBlock
            {
                Text = name,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = isSelected ? onPrimary : textBrush
            });

            border.Child = panel;
            border.MouseEnter += (_, _) => { border.Background = hoverBg; };
            border.MouseLeave += (_, _) => { border.Background = baseBg; };
            border.MouseLeftButtonUp += (_, _) => SelectNodeInCombo(node);
            border.ToolTip = isSelected
                ? "Widget đang được chọn để chỉnh cấu hình"
                : (isEnabled
                    ? "Widget đang bật"
                    : "Widget đang tắt (chưa bật floating)");
            return border;
        }

        private static Brush CreateSubtleBrush(Brush source, double alpha)
        {
            if (source is SolidColorBrush solid)
            {
                var c = solid.Color;
                return new SolidColorBrush(Color.FromArgb((byte)(Math.Max(0, Math.Min(1, alpha)) * 255), c.R, c.G, c.B));
            }
            return source;
        }

        private static Brush? ParseHexBrush(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(hex.Trim());
                return new SolidColorBrush(c);
            }
            catch
            {
                return null;
            }
        }

        private void SelectNodeInCombo(WorkflowNode node)
        {
            // Auto-apply pending changes for current node trước khi đổi.
            if (_selectedNode != null && !_loadingValues)
            {
                ApplyValuesToConfig();
                SyncOpenWidgetRuntime(_selectedNode.Id);
            }

            NodeComboBox.SelectedValue = node.Id;
        }

        private static string TrimId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "-";
            return id.Length <= 8 ? id : id[..8];
        }

        private void PopulateMonitorList()
        {
            MonitorComboBox.Items.Clear();
            MonitorComboBox.Items.Add(new ComboBoxItem { Content = "Primary (mặc định)", Tag = -1 });

            try
            {
                var screens = Screen.AllScreens;
                for (int i = 0; i < screens.Length; i++)
                {
                    var s = screens[i];
                    var label = $"Màn hình #{i + 1} — {s.Bounds.Width}×{s.Bounds.Height}{(s.Primary ? " (primary)" : "")}";
                    MonitorComboBox.Items.Add(new ComboBoxItem { Content = label, Tag = i });
                }
            }
            catch
            {
                // Ignore — fallback chỉ có primary
            }

            MonitorComboBox.SelectedIndex = 0;
        }

        private void NodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Khi đổi node trực tiếp từ ComboBox: auto-apply + sync node cũ trước.
            if (!_loadingValues && _selectedNode != null)
            {
                try
                {
                    ApplyValuesToConfig();
                    SyncOpenWidgetRuntime(_selectedNode.Id);
                }
                catch { }
            }

            if (NodeComboBox.SelectedValue is string nodeId)
            {
                var node = _nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, System.StringComparison.OrdinalIgnoreCase));
                if (node == null)
                {
                    _selectedNode = null;
                    SettingsPanel.IsEnabled = false;
                    WidgetStatusText.Text = string.Empty;
                    RefreshExistingWidgets();
                    return;
                }

                _selectedNode = node;
                node.FloatingWidget ??= new FloatingWidgetConfig();
                LoadValuesFromConfig(node.FloatingWidget);
                SettingsPanel.IsEnabled = true;
                UpdateWidgetStatus();
                RefreshExistingWidgets();
            }
            else
            {
                _selectedNode = null;
                SettingsPanel.IsEnabled = false;
                WidgetStatusText.Text = string.Empty;
                RefreshExistingWidgets();
            }
        }

        private void UpdateWidgetStatus()
        {
            if (_selectedNode == null)
            {
                WidgetStatusText.Text = string.Empty;
                return;
            }

            var isOpen = FloatingWidgetManager.Instance.IsWidgetOpen(_selectedNode.Id);
            WidgetStatusText.Text = isOpen ? "● Widget đang mở" : "○ Widget chưa mở";
            WidgetStatusText.Foreground = isOpen
                ? (System.Windows.Media.Brush?)Application.Current.TryFindResource("SuccessBrush")
                    ?? System.Windows.Media.Brushes.LimeGreen
                : (System.Windows.Media.Brush?)Application.Current.TryFindResource("TextBrush")
                    ?? System.Windows.Media.Brushes.Gray;
        }

        private void LoadValuesFromConfig(FloatingWidgetConfig cfg)
        {
            _loadingValues = true;
            try
            {
                ApplyNodeMinSizeDefaults(cfg);

                IsEnabledCheckBox.IsChecked = cfg.IsEnabled;
                WidgetNameTextBox.Text = string.IsNullOrWhiteSpace(cfg.WidgetName)
                    ? (_selectedNode?.Title ?? string.Empty)
                    : cfg.WidgetName;

                SelectComboByTag(IdleShapeComboBox, cfg.IdleShape.ToString());
                SelectComboByTag(IdleAnimationComboBox, cfg.IdleAnimation.ToString());
                IdleSizeTextBox.Text = cfg.IdleSize.ToString("0.#", CultureInfo.InvariantCulture);
                IdleOpacityTextBox.Text = cfg.IdleOpacity.ToString("0.##", CultureInfo.InvariantCulture);

                // Nếu IdleIconText là tên icon SVG hợp lệ → hiển thị trong IconSelector,
                // TextBox emoji để trống. Ngược lại: coi là emoji/ký tự.
                var raw = cfg.IdleIconText ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(raw) && FlowMy.IconResources.IconExists(raw))
                {
                    IdleIconSelector.SetSelectedIcon(raw);
                    IdleIconTextBox.Text = string.Empty;
                }
                else
                {
                    IdleIconSelector.SetSelectedIcon(string.Empty);
                    IdleIconTextBox.Text = raw;
                }

                _idleBackgroundColorHex = cfg.IdleBackgroundColor;
                _idleForegroundColorHex = cfg.IdleForegroundColor;
                UpdateIdleColorPreviews();

                UseRatioSizeCheckBox.IsChecked = cfg.UseRatioSize;
                AllowResizeCheckBox.IsChecked = cfg.AllowResize;

                if (cfg.UseRatioSize)
                {
                    ExpandedWidthTextBox.Text = cfg.WidthRatio.ToString("0.##", CultureInfo.InvariantCulture);
                    ExpandedHeightTextBox.Text = cfg.HeightRatio.ToString("0.##", CultureInfo.InvariantCulture);
                    MinWidthTextBox.Text = cfg.MinWidthRatio.ToString("0.##", CultureInfo.InvariantCulture);
                    MinHeightTextBox.Text = cfg.MinHeightRatio.ToString("0.##", CultureInfo.InvariantCulture);
                    MaxWidthTextBox.Text = cfg.MaxWidthRatio.ToString("0.##", CultureInfo.InvariantCulture);
                    MaxHeightTextBox.Text = cfg.MaxHeightRatio.ToString("0.##", CultureInfo.InvariantCulture);
                }
                else
                {
                    ExpandedWidthTextBox.Text = cfg.ExpandedWidth.ToString("0.#", CultureInfo.InvariantCulture);
                    ExpandedHeightTextBox.Text = cfg.ExpandedHeight.ToString("0.#", CultureInfo.InvariantCulture);
                    MinWidthTextBox.Text = cfg.MinExpandedWidth.ToString("0.#", CultureInfo.InvariantCulture);
                    MinHeightTextBox.Text = cfg.MinExpandedHeight.ToString("0.#", CultureInfo.InvariantCulture);
                    MaxWidthTextBox.Text = cfg.MaxExpandedWidth.ToString("0.#", CultureInfo.InvariantCulture);
                    MaxHeightTextBox.Text = cfg.MaxExpandedHeight.ToString("0.#", CultureInfo.InvariantCulture);
                }

                UpdateSizeLabels(cfg.UseRatioSize);

                AllowDragCheckBox.IsChecked = cfg.AllowDrag;
                SnapToEdgeCheckBox.IsChecked = cfg.SnapToEdge;
                LockPositionCheckBox.IsChecked = cfg.LockPosition;
                SelectComboByTag(PreferredEdgeComboBox, cfg.PreferredEdge.ToString());
                SnapMarginTextBox.Text = cfg.SnapMargin.ToString("0.#", CultureInfo.InvariantCulture);

                AutoCollapseCheckBox.IsChecked = cfg.AutoCollapseWhenIdle;
                PinnedNoAutoHideCheckBox.IsChecked = cfg.PinnedNoAutoHide;
                CollapseOutsideExpandedCheckBox.IsChecked = cfg.CollapseWhenClickOutsideExpanded;
                SlideToEdgeCheckBox.IsChecked = cfg.SlideToEdgeWhenIdle;
                EdgeDockAsSquareCheckBox.IsChecked = cfg.EdgeDockAsSquare;
                IdleTimeoutTextBox.Text = cfg.IdleTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
                SlideHidePercentTextBox.Text = (cfg.SlideHidePercent * 100).ToString("0.#", CultureInfo.InvariantCulture);
                UpdatePinnedUiState();

                AlwaysOnTopCheckBox.IsChecked = cfg.AlwaysOnTop;
                ShowInTaskbarCheckBox.IsChecked = cfg.ShowInTaskbar;
                SelectComboByTag(TaskbarIconShapeComboBox, cfg.TaskbarIconShape.ToString());
                TaskbarIconSizeTextBox.Text = cfg.TaskbarIconSize.ToString("0.#", CultureInfo.InvariantCulture);
                if (!cfg.ShowTitleBar)
                    TitleBarHiddenRadio.IsChecked = true;
                else if (cfg.AutoHideTitleBar)
                    TitleBarAutoHideRadio.IsChecked = true;
                else
                    TitleBarAlwaysVisibleRadio.IsChecked = true;
                ShowSideActionButtonCheckBox.IsChecked = cfg.ShowSideActionButton;
                TitleBarHideTimeoutTextBox.Text = cfg.TitleBarHideTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
                UpdateTitleTimeoutState();
                UpdateSideActionButtonState();

                SelectComboByTag(DisplayModeComboBox, cfg.DisplayMode.ToString());
                SelectMonitor(cfg.MonitorIndex);
                ShowOnAllMonitorsCheckBox.IsChecked = cfg.ShowOnAllMonitors;
            }
            finally
            {
                _loadingValues = false;
            }
        }

        private (double MinW, double MinH) ResolveNodeMinSizePx()
        {
            if (_selectedNode?.Border is Border b)
            {
                var minW = b.MinWidth > 0 ? b.MinWidth : 0;
                var minH = b.MinHeight > 0 ? b.MinHeight : 0;
                if (minW > 0 && minH > 0) return (minW, minH);
            }

            return _selectedNode switch
            {
                ImageProcessingNode => (ImageProcessingNodeControl.ImageNodeMinWidthPx, ImageProcessingNodeControl.ImageNodeMinHeightPx),
                VideoProcessingNode => (540, 340),
                HtmlUiNode => (600, 600),
                WebNode => (600, 600),
                MediaGalleryNode => (200, 180),
                _ => (200, 150),
            };
        }

        private static (double Width, double Height) GetPrimaryWorkAreaSize()
            => FloatingWidgetConfig.GetPrimaryWorkAreaSize();

        private void ApplyNodeMinSizeDefaults(FloatingWidgetConfig cfg)
        {
            var (nodeMinW, nodeMinH) = ResolveNodeMinSizePx();

            cfg.MinExpandedWidth = Math.Max(nodeMinW, cfg.MinExpandedWidth);
            cfg.MinExpandedHeight = Math.Max(nodeMinH, cfg.MinExpandedHeight);
            cfg.ExpandedWidth = Math.Max(cfg.MinExpandedWidth, cfg.ExpandedWidth);
            cfg.ExpandedHeight = Math.Max(cfg.MinExpandedHeight, cfg.ExpandedHeight);

            var (areaW, areaH) = GetPrimaryWorkAreaSize();
            var minWRatioFromNode = Math.Max(0.05, Math.Min(1.0, nodeMinW / areaW));
            var minHRatioFromNode = Math.Max(0.05, Math.Min(1.0, nodeMinH / areaH));

            cfg.MinWidthRatio = Math.Max(minWRatioFromNode, cfg.MinWidthRatio);
            cfg.MinHeightRatio = Math.Max(minHRatioFromNode, cfg.MinHeightRatio);
            cfg.WidthRatio = Math.Max(cfg.MinWidthRatio, cfg.WidthRatio);
            cfg.HeightRatio = Math.Max(cfg.MinHeightRatio, cfg.HeightRatio);
        }

        private static void SelectComboByTag(System.Windows.Controls.ComboBox combo, string tag)
        {
            foreach (var obj in combo.Items)
            {
                if (obj is ComboBoxItem cbi && string.Equals(cbi.Tag?.ToString(), tag, System.StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = cbi;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private void SelectMonitor(int monitorIndex)
        {
            foreach (var obj in MonitorComboBox.Items)
            {
                if (obj is ComboBoxItem cbi && cbi.Tag is int idx && idx == monitorIndex)
                {
                    MonitorComboBox.SelectedItem = cbi;
                    return;
                }
            }
            MonitorComboBox.SelectedIndex = 0;
        }

        private bool ApplyValuesToConfig()
        {
            if (_selectedNode == null) return false;
            _selectedNode.FloatingWidget ??= new FloatingWidgetConfig();
            var cfg = _selectedNode.FloatingWidget;

            cfg.IsEnabled = IsEnabledCheckBox.IsChecked == true;
            cfg.WidgetName = (WidgetNameTextBox.Text ?? string.Empty).Trim();

            cfg.IdleShape = ParseEnum(IdleShapeComboBox, WidgetIdleShape.Circle);
            cfg.IdleAnimation = ParseEnum(IdleAnimationComboBox, WidgetIdleAnimation.Heartbeat);
            cfg.IdleSize = ParseDouble(IdleSizeTextBox.Text, cfg.IdleSize);
            cfg.IdleOpacity = ParseDouble(IdleOpacityTextBox.Text, cfg.IdleOpacity);

            // Ưu tiên icon SVG từ IconSelector; nếu chưa chọn thì lấy emoji từ TextBox; fallback ⚡.
            var pickedIconKey = IdleIconSelector?.SelectedIcon;
            if (!string.IsNullOrWhiteSpace(pickedIconKey) && FlowMy.IconResources.IconExists(pickedIconKey))
            {
                cfg.IdleIconText = pickedIconKey;
            }
            else if (!string.IsNullOrWhiteSpace(IdleIconTextBox.Text))
            {
                cfg.IdleIconText = IdleIconTextBox.Text;
            }
            else
            {
                cfg.IdleIconText = "⚡";
            }

            cfg.IdleBackgroundColor = string.IsNullOrWhiteSpace(_idleBackgroundColorHex)
                ? null
                : _idleBackgroundColorHex;
            cfg.IdleForegroundColor = string.IsNullOrWhiteSpace(_idleForegroundColorHex)
                ? null
                : _idleForegroundColorHex;

            cfg.UseRatioSize = UseRatioSizeCheckBox.IsChecked == true;
            cfg.AllowResize = AllowResizeCheckBox.IsChecked == true;

            if (cfg.UseRatioSize)
            {
                cfg.WidthRatio = ParseDouble(ExpandedWidthTextBox.Text, cfg.WidthRatio);
                cfg.HeightRatio = ParseDouble(ExpandedHeightTextBox.Text, cfg.HeightRatio);
                cfg.MinWidthRatio = ParseDouble(MinWidthTextBox.Text, cfg.MinWidthRatio);
                cfg.MinHeightRatio = ParseDouble(MinHeightTextBox.Text, cfg.MinHeightRatio);
                cfg.MaxWidthRatio = ParseDouble(MaxWidthTextBox.Text, cfg.MaxWidthRatio);
                cfg.MaxHeightRatio = ParseDouble(MaxHeightTextBox.Text, cfg.MaxHeightRatio);
            }
            else
            {
                cfg.ExpandedWidth = ParseDouble(ExpandedWidthTextBox.Text, cfg.ExpandedWidth);
                cfg.ExpandedHeight = ParseDouble(ExpandedHeightTextBox.Text, cfg.ExpandedHeight);
                cfg.MinExpandedWidth = ParseDouble(MinWidthTextBox.Text, cfg.MinExpandedWidth);
                cfg.MinExpandedHeight = ParseDouble(MinHeightTextBox.Text, cfg.MinExpandedHeight);
                cfg.MaxExpandedWidth = ParseDouble(MaxWidthTextBox.Text, cfg.MaxExpandedWidth);
                cfg.MaxExpandedHeight = ParseDouble(MaxHeightTextBox.Text, cfg.MaxExpandedHeight);
            }

            cfg.AllowDrag = AllowDragCheckBox.IsChecked == true;
            cfg.SnapToEdge = SnapToEdgeCheckBox.IsChecked == true;
            cfg.LockPosition = LockPositionCheckBox.IsChecked == true;
            cfg.PreferredEdge = ParseEnum(PreferredEdgeComboBox, WidgetSnapEdge.Right);
            cfg.SnapMargin = ParseDouble(SnapMarginTextBox.Text, cfg.SnapMargin);

            cfg.AutoCollapseWhenIdle = AutoCollapseCheckBox.IsChecked == true;
            cfg.PinnedNoAutoHide = PinnedNoAutoHideCheckBox.IsChecked == true;
            cfg.CollapseWhenClickOutsideExpanded = CollapseOutsideExpandedCheckBox.IsChecked == true;
            cfg.SlideToEdgeWhenIdle = SlideToEdgeCheckBox.IsChecked == true;
            cfg.EdgeDockAsSquare = EdgeDockAsSquareCheckBox.IsChecked == true;
            cfg.IdleTimeoutSeconds = (int)ParseDouble(IdleTimeoutTextBox.Text, cfg.IdleTimeoutSeconds);
            cfg.SlideHidePercent = ParseDouble(SlideHidePercentTextBox.Text, cfg.SlideHidePercent * 100.0) / 100.0;

            // Ưu tiên chế độ ghim: tắt các cơ chế tự ẩn theo thời gian/ngoài màn.
            if (cfg.PinnedNoAutoHide)
            {
                cfg.AutoCollapseWhenIdle = false;
                cfg.SlideToEdgeWhenIdle = false;
                cfg.CollapseWhenClickOutsideExpanded = false;
            }

            cfg.AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true;
            cfg.ShowInTaskbar = ShowInTaskbarCheckBox.IsChecked == true;
            cfg.TaskbarIconShape = ParseEnum(TaskbarIconShapeComboBox, WidgetIdleShape.Circle);
            cfg.TaskbarIconSize = ParseDouble(TaskbarIconSizeTextBox.Text, cfg.TaskbarIconSize);
            cfg.ShowTitleBar = TitleBarAlwaysVisibleRadio.IsChecked == true || TitleBarAutoHideRadio.IsChecked == true;
            cfg.AutoHideTitleBar = TitleBarAutoHideRadio.IsChecked == true;
            cfg.ShowSideActionButton = ShowSideActionButtonCheckBox.IsChecked != false;
            cfg.TitleBarHideTimeoutSeconds = (int)ParseDouble(TitleBarHideTimeoutTextBox.Text, cfg.TitleBarHideTimeoutSeconds);

            cfg.DisplayMode = ParseEnum(DisplayModeComboBox, WidgetDisplayMode.Normal);
            if (MonitorComboBox.SelectedItem is ComboBoxItem mi && mi.Tag is int midx) cfg.MonitorIndex = midx;
            cfg.ShowOnAllMonitors = ShowOnAllMonitorsCheckBox.IsChecked == true;

            return true;
        }

        private static void SyncOpenWidgetRuntime(string? nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) return;
            if (FloatingWidgetManager.Instance.IsWidgetOpen(nodeId))
            {
                FloatingWidgetManager.Instance.RefreshWidget(nodeId);
            }
        }

        private void PersistWorkflowChanges()
        {
            try
            {
                if (_persistChanges != null)
                {
                    _persistChanges();
                }
                else
                {
                    _host?.ViewModel?.SaveWorkflowSilently();
                }
            }
            catch { /* best effort */ }
        }

        private static double ParseDouble(string text, double fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            text = text.Replace(',', '.').Trim();
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        private static TEnum ParseEnum<TEnum>(System.Windows.Controls.ComboBox combo, TEnum fallback) where TEnum : struct
        {
            if (combo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag
                && System.Enum.TryParse<TEnum>(tag, true, out var parsed))
            {
                return parsed;
            }
            return fallback;
        }

        // ── Button handlers ──

        private void OpenWidgetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode == null)
            {
                MessageBox.Show(this, "Hãy chọn một node trước.", "Floating Widget", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!ApplyValuesToConfig()) return;

            FloatingWidgetManager.Instance.OpenWidget(_selectedNode, _host);
            SyncOpenWidgetRuntime(_selectedNode.Id);
            UpdateWidgetStatus();
            RefreshExistingWidgets();
        }

        private void OpenAllWidgetsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode != null && !_loadingValues)
            {
                ApplyValuesToConfig();
                SyncOpenWidgetRuntime(_selectedNode.Id);
            }

            var enabled = _nodes.Where(n => n.FloatingWidget is { IsEnabled: true }).ToList();
            if (enabled.Count == 0)
            {
                MessageBox.Show(this, "Không có widget nào đang bật (checked) để mở.", "Floating Widget", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var node in enabled)
            {
                FloatingWidgetManager.Instance.OpenWidget(node, _host);
                SyncOpenWidgetRuntime(node.Id);
            }

            UpdateWidgetStatus();
            RefreshExistingWidgets();
            PersistWorkflowChanges();
        }

        private void CloseAllWidgetsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode != null && !_loadingValues)
            {
                ApplyValuesToConfig();
                SyncOpenWidgetRuntime(_selectedNode.Id);
            }

            FloatingWidgetManager.Instance.CloseAllWidgets();
            UpdateWidgetStatus();
            RefreshExistingWidgets();
            PersistWorkflowChanges();
        }

        private void CloseWidgetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode == null) return;
            FloatingWidgetManager.Instance.CloseWidget(_selectedNode.Id);
            // Không tắt IsEnabled — chỉ đóng widget instance hiện tại.
            UpdateWidgetStatus();
            RefreshExistingWidgets();
        }

        private void RemoveWidgetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode == null) return;

            var confirm = MessageBox.Show(
                this,
                $"Gỡ widget khỏi node \"{_selectedNode.Title ?? _selectedNode.Id}\"?\n\nThao tác này sẽ đóng widget (nếu đang mở) và xóa cấu hình widget của node này.",
                "Gỡ Widget",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            FloatingWidgetManager.Instance.CloseWidget(_selectedNode.Id);
            _selectedNode.FloatingWidget = null;

            // Reload form cho node đang chọn (tạo config mặc định mới, không bật)
            _selectedNode.FloatingWidget = new FloatingWidgetConfig();
            LoadValuesFromConfig(_selectedNode.FloatingWidget);

            PopulateNodeList();
            SelectNodeInCombo(_selectedNode);
            RefreshExistingWidgets();
            UpdateWidgetStatus();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode == null) return;
            ApplyValuesToConfig();
            SyncOpenWidgetRuntime(_selectedNode.Id);

            // Restart các widget đang mở để áp toàn bộ config mới (UI/runtime) nhất quán.
            var activeIds = FloatingWidgetManager.Instance.GetActiveWidgetNodeIds();
            foreach (var nodeId in activeIds)
            {
                var node = _nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, System.StringComparison.OrdinalIgnoreCase));
                if (node == null) continue;
                FloatingWidgetManager.Instance.CloseWidget(nodeId);
                FloatingWidgetManager.Instance.OpenWidget(node, _host);
            }
            UpdateWidgetStatus();
            PopulateNodeList();
            SelectNodeInCombo(_selectedNode);
            RefreshExistingWidgets();
            PersistWorkflowChanges();
        }

        private void UseRatioSizeCheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loadingValues || _selectedNode?.FloatingWidget == null) return;

            var cfg = _selectedNode.FloatingWidget;
            var switchingToRatio = UseRatioSizeCheckBox.IsChecked == true;

            // Lưu giá trị đang hiển thị vào config dựa trên mode cũ
            if (cfg.UseRatioSize)
            {
                cfg.WidthRatio = ParseDouble(ExpandedWidthTextBox.Text, cfg.WidthRatio);
                cfg.HeightRatio = ParseDouble(ExpandedHeightTextBox.Text, cfg.HeightRatio);
                cfg.MinWidthRatio = ParseDouble(MinWidthTextBox.Text, cfg.MinWidthRatio);
                cfg.MinHeightRatio = ParseDouble(MinHeightTextBox.Text, cfg.MinHeightRatio);
                cfg.MaxWidthRatio = ParseDouble(MaxWidthTextBox.Text, cfg.MaxWidthRatio);
                cfg.MaxHeightRatio = ParseDouble(MaxHeightTextBox.Text, cfg.MaxHeightRatio);
            }
            else
            {
                cfg.ExpandedWidth = ParseDouble(ExpandedWidthTextBox.Text, cfg.ExpandedWidth);
                cfg.ExpandedHeight = ParseDouble(ExpandedHeightTextBox.Text, cfg.ExpandedHeight);
                cfg.MinExpandedWidth = ParseDouble(MinWidthTextBox.Text, cfg.MinExpandedWidth);
                cfg.MinExpandedHeight = ParseDouble(MinHeightTextBox.Text, cfg.MinExpandedHeight);
                cfg.MaxExpandedWidth = ParseDouble(MaxWidthTextBox.Text, cfg.MaxExpandedWidth);
                cfg.MaxExpandedHeight = ParseDouble(MaxHeightTextBox.Text, cfg.MaxExpandedHeight);
            }

            cfg.UseRatioSize = switchingToRatio;
            ApplyNodeMinSizeDefaults(cfg);

            _loadingValues = true;
            try
            {
                if (switchingToRatio)
                {
                    ExpandedWidthTextBox.Text = cfg.WidthRatio.ToString("0.##", CultureInfo.InvariantCulture);
                    ExpandedHeightTextBox.Text = cfg.HeightRatio.ToString("0.##", CultureInfo.InvariantCulture);
                    MinWidthTextBox.Text = cfg.MinWidthRatio.ToString("0.##", CultureInfo.InvariantCulture);
                    MinHeightTextBox.Text = cfg.MinHeightRatio.ToString("0.##", CultureInfo.InvariantCulture);
                    MaxWidthTextBox.Text = cfg.MaxWidthRatio.ToString("0.##", CultureInfo.InvariantCulture);
                    MaxHeightTextBox.Text = cfg.MaxHeightRatio.ToString("0.##", CultureInfo.InvariantCulture);
                }
                else
                {
                    ExpandedWidthTextBox.Text = cfg.ExpandedWidth.ToString("0.#", CultureInfo.InvariantCulture);
                    ExpandedHeightTextBox.Text = cfg.ExpandedHeight.ToString("0.#", CultureInfo.InvariantCulture);
                    MinWidthTextBox.Text = cfg.MinExpandedWidth.ToString("0.#", CultureInfo.InvariantCulture);
                    MinHeightTextBox.Text = cfg.MinExpandedHeight.ToString("0.#", CultureInfo.InvariantCulture);
                    MaxWidthTextBox.Text = cfg.MaxExpandedWidth.ToString("0.#", CultureInfo.InvariantCulture);
                    MaxHeightTextBox.Text = cfg.MaxExpandedHeight.ToString("0.#", CultureInfo.InvariantCulture);
                }
            }
            finally
            {
                _loadingValues = false;
            }

            UpdateSizeLabels(switchingToRatio);
        }

        private void LockPositionCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_loadingValues) return;
            // Khóa vị trí thì không cho các mode tự đổi vị trí.
            _loadingValues = true;
            try
            {
                AllowDragCheckBox.IsChecked = false;
                SnapToEdgeCheckBox.IsChecked = false;
                SlideToEdgeCheckBox.IsChecked = false;
            }
            finally { _loadingValues = false; }
        }

        private void LockPositionCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Không auto bật lại gì, user tự chọn.
        }

        private void AllowDragCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_loadingValues) return;
            if (LockPositionCheckBox.IsChecked == true)
            {
                _loadingValues = true;
                try { LockPositionCheckBox.IsChecked = false; } finally { _loadingValues = false; }
            }
        }

        private void AllowDragCheckBox_Unchecked(object sender, RoutedEventArgs e) { }

        private void SnapToEdgeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_loadingValues) return;
            _loadingValues = true;
            try
            {
                if (LockPositionCheckBox.IsChecked == true) LockPositionCheckBox.IsChecked = false;
                // Bật snap thì nên có drag để user thả cạnh dễ hiểu.
                if (AllowDragCheckBox.IsChecked != true) AllowDragCheckBox.IsChecked = true;
            }
            finally { _loadingValues = false; }
        }

        private void SnapToEdgeCheckBox_Unchecked(object sender, RoutedEventArgs e) { }

        private void SlideToEdgeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_loadingValues) return;
            if (LockPositionCheckBox.IsChecked == true)
            {
                _loadingValues = true;
                try { LockPositionCheckBox.IsChecked = false; } finally { _loadingValues = false; }
            }
        }

        private void SlideToEdgeCheckBox_Unchecked(object sender, RoutedEventArgs e) { }

        private void PinnedNoAutoHideCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_loadingValues) return;
            _loadingValues = true;
            try
            {
                AutoCollapseCheckBox.IsChecked = false;
                SlideToEdgeCheckBox.IsChecked = false;
                CollapseOutsideExpandedCheckBox.IsChecked = false;
            }
            finally { _loadingValues = false; }

            UpdatePinnedUiState();
            SyncCollapseOutsideToggleToRuntime();
        }

        private void PinnedNoAutoHideCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdatePinnedUiState();
            SyncCollapseOutsideToggleToRuntime();
        }

        private void CollapseOutsideExpandedCheckBox_Checked(object sender, RoutedEventArgs e)
            => SyncCollapseOutsideToggleToRuntime();

        private void CollapseOutsideExpandedCheckBox_Unchecked(object sender, RoutedEventArgs e)
            => SyncCollapseOutsideToggleToRuntime();

        private void SyncCollapseOutsideToggleToRuntime()
        {
            if (_loadingValues || _selectedNode == null) return;
            ApplyValuesToConfig();
            SyncOpenWidgetRuntime(_selectedNode.Id);
            PersistWorkflowChanges();
            RefreshExistingWidgets();
        }

        private void UpdatePinnedUiState()
        {
            if (PinnedNoAutoHideCheckBox == null) return;
            var pinned = PinnedNoAutoHideCheckBox.IsChecked == true;
            AutoCollapseCheckBox.IsEnabled = !pinned;
            SlideToEdgeCheckBox.IsEnabled = !pinned;
            CollapseOutsideExpandedCheckBox.IsEnabled = !pinned;
            AutoCollapseCheckBox.Opacity = pinned ? 0.55 : 1.0;
            SlideToEdgeCheckBox.Opacity = pinned ? 0.55 : 1.0;
            CollapseOutsideExpandedCheckBox.Opacity = pinned ? 0.55 : 1.0;
        }

        private void TitleBarModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_loadingValues) return;
            UpdateTitleTimeoutState();
            UpdateSideActionButtonState();
        }

        private void UpdateTitleTimeoutState()
        {
            if (TitleBarHideTimeoutTextBox == null) return;
            var isAutoHide = TitleBarAutoHideRadio.IsChecked == true;
            TitleBarHideTimeoutTextBox.IsEnabled = isAutoHide;
            TitleBarHideTimeoutTextBox.Opacity = isAutoHide ? 1 : 0.55;
        }

        private void UpdateSideActionButtonState()
        {
            if (ShowSideActionButtonCheckBox == null) return;
            var alwaysVisibleTitle = TitleBarAlwaysVisibleRadio?.IsChecked == true;
            if (alwaysVisibleTitle)
            {
                ShowSideActionButtonCheckBox.IsChecked = false;
                ShowSideActionButtonCheckBox.IsEnabled = false;
                ShowSideActionButtonCheckBox.Opacity = 0.55;
                return;
            }

            ShowSideActionButtonCheckBox.IsEnabled = true;
            ShowSideActionButtonCheckBox.Opacity = 1.0;
        }

        private void PickIdleBackgroundColor_Click(object sender, RoutedEventArgs e)
        {
            var hex = ShowColorPicker(_idleBackgroundColorHex);
            if (!string.IsNullOrWhiteSpace(hex))
            {
                _idleBackgroundColorHex = hex;
                UpdateIdleColorPreviews();
            }
        }

        private void PickIdleForegroundColor_Click(object sender, RoutedEventArgs e)
        {
            var hex = ShowColorPicker(_idleForegroundColorHex);
            if (!string.IsNullOrWhiteSpace(hex))
            {
                _idleForegroundColorHex = hex;
                UpdateIdleColorPreviews();
            }
        }

        private void UpdateIdleColorPreviews()
        {
            if (IdleBackgroundColorPreview != null)
            {
                IdleBackgroundColorPreview.Background = ResolveBrush(
                    _idleBackgroundColorHex,
                    (Brush?)Application.Current.TryFindResource("PrimaryBrush") ?? Brushes.SlateBlue);
                IdleBackgroundColorPreview.ToolTip = string.IsNullOrWhiteSpace(_idleBackgroundColorHex)
                    ? "Đang dùng màu Primary của theme"
                    : _idleBackgroundColorHex;
            }

            if (IdleForegroundColorPreview != null)
            {
                IdleForegroundColorPreview.Background = ResolveBrush(
                    _idleForegroundColorHex,
                    (Brush?)Application.Current.TryFindResource("TextOnPrimaryBrush") ?? Brushes.White);
                IdleForegroundColorPreview.ToolTip = string.IsNullOrWhiteSpace(_idleForegroundColorHex)
                    ? "Đang dùng màu TextOnPrimary của theme"
                    : _idleForegroundColorHex;
            }
        }

        private static string? ShowColorPicker(string? currentHex)
        {
            try
            {
                using var dialog = new WinForms.ColorDialog
                {
                    FullOpen = true
                };

                if (!string.IsNullOrWhiteSpace(currentHex) && currentHex.StartsWith("#", System.StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        dialog.Color = System.Drawing.ColorTranslator.FromHtml(currentHex);
                    }
                    catch { }
                }

                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    var c = dialog.Color;
                    return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }
            }
            catch { }

            return null;
        }

        private static Brush ResolveBrush(string? key, Brush fallback)
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback;

            try
            {
                if (key.StartsWith("#", System.StringComparison.OrdinalIgnoreCase))
                {
                    var converter = new BrushConverter();
                    var brush = converter.ConvertFromString(key) as Brush;
                    if (brush != null) return brush;
                }

                var resource = Application.Current.TryFindResource(key);
                if (resource is Brush b) return b;
                if (resource is Color c) return new SolidColorBrush(c);
            }
            catch { }

            return fallback;
        }

        private void UpdateSizeLabels(bool useRatio)
        {
            if (useRatio)
            {
                WidthLabel.Text = "Rộng (tỉ lệ 0.0–1.0)";
                HeightLabel.Text = "Cao (tỉ lệ 0.0–1.0)";
                MinLabel.Text = "Min W × H (tỉ lệ)";
                MaxLabel.Text = "Max W × H (tỉ lệ)";
            }
            else
            {
                WidthLabel.Text = "Rộng (px)";
                HeightLabel.Text = "Cao (px)";
                MinLabel.Text = "Min W × H (px)";
                MaxLabel.Text = "Max W × H (px)";
            }
        }

        private void CloseDialogButton_Click(object sender, RoutedEventArgs e)
        {
            // Auto-apply khi đóng để không mất thay đổi
            if (_selectedNode != null && !_loadingValues)
            {
                ApplyValuesToConfig();
                SyncOpenWidgetRuntime(_selectedNode.Id);
                PersistWorkflowChanges();
            }
            Close();
        }

        public void SelectNodeById(string? nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) return;
            var node = _nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, System.StringComparison.OrdinalIgnoreCase));
            if (node != null)
                NodeComboBox.SelectedValue = node.Id;
        }
    }
}
