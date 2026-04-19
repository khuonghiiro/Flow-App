using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services;
using FlowMy.Services.Interaction;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace FlowMy.Views.Overlays
{
    /// <summary>
    /// Dialog cấu hình Floating Widget cho bất kỳ node nào thuộc workflow đang mở.
    /// Thay thế cho menu chuột phải "📌 Mở Widget" trên node.
    /// </summary>
    public partial class FloatingWidgetConfigDialog : Window
    {
        private readonly IReadOnlyList<WorkflowNode> _nodes;
        private readonly IWorkflowEditorHost _host;
        private WorkflowNode? _selectedNode;
        private bool _loadingValues;

        public FloatingWidgetConfigDialog(IEnumerable<WorkflowNode> nodes, IWorkflowEditorHost host)
        {
            InitializeComponent();

            _nodes = (nodes ?? Enumerable.Empty<WorkflowNode>())
                .Where(n => n != null && n.Type != NodeType.Start && n.Type != NodeType.End)
                .OrderBy(n => string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title)
                .ToList();
            _host = host;

            PopulateNodeList();
            PopulateMonitorList();
            RefreshExistingWidgets();
        }

        private void PopulateNodeList()
        {
            NodeComboBox.Items.Clear();
            foreach (var node in _nodes)
            {
                var title = string.IsNullOrWhiteSpace(node.Title) ? "(chưa đặt tên)" : node.Title;
                var hasWidget = node.FloatingWidget is { IsEnabled: true };
                var prefix = hasWidget ? "📌 " : "    ";
                var display = $"{prefix}{title}  —  {node.Type}  [{TrimId(node.Id)}]";
                NodeComboBox.Items.Add(new ComboBoxItem { Content = display, Tag = node });
            }

            if (NodeComboBox.Items.Count > 0)
            {
                NodeComboBox.SelectedIndex = 0;
            }
            else
            {
                WidgetStatusText.Text = "Canvas chưa có node nào.";
            }
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

            var border = new Border
            {
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(4),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(1),
                Background = isSelected
                    ? ((Brush?)Application.Current.TryFindResource("PrimaryBrush") ?? Brushes.SlateBlue)
                    : ((Brush?)Application.Current.TryFindResource("CardColor") ?? Brushes.Transparent),
                BorderBrush = isSelected
                    ? ((Brush?)Application.Current.TryFindResource("PrimaryBrush") ?? Brushes.SlateBlue)
                    : ((Brush?)Application.Current.TryFindResource("BorderColor") ?? Brushes.DimGray)
            };

            var panel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            panel.Children.Add(new TextBlock
            {
                Text = isOpen ? "●" : "○",
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = isOpen
                    ? ((Brush?)Application.Current.TryFindResource("SuccessBrush") ?? Brushes.LimeGreen)
                    : (isSelected
                        ? ((Brush?)Application.Current.TryFindResource("TextOnPrimaryBrush") ?? Brushes.White)
                        : ((Brush?)Application.Current.TryFindResource("TextBrush") ?? Brushes.Gray)),
                FontSize = 12
            });
            panel.Children.Add(new TextBlock
            {
                Text = name,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = isSelected
                    ? ((Brush?)Application.Current.TryFindResource("TextOnPrimaryBrush") ?? Brushes.White)
                    : ((Brush?)Application.Current.TryFindResource("TextBrush") ?? Brushes.Gray)
            });

            border.Child = panel;
            border.MouseLeftButtonUp += (_, _) => SelectNodeInCombo(node);
            return border;
        }

        private void SelectNodeInCombo(WorkflowNode node)
        {
            // Auto-apply pending changes for current node trước khi đổi.
            if (_selectedNode != null && !_loadingValues)
            {
                ApplyValuesToConfig();
                SyncOpenWidgetRuntime(_selectedNode.Id);
            }

            foreach (var obj in NodeComboBox.Items)
            {
                if (obj is ComboBoxItem cbi && ReferenceEquals(cbi.Tag, node))
                {
                    NodeComboBox.SelectedItem = cbi;
                    return;
                }
            }
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

            if (NodeComboBox.SelectedItem is ComboBoxItem item && item.Tag is WorkflowNode node)
            {
                _selectedNode = node;
                node.FloatingWidget ??= new FloatingWidgetConfig();
                LoadValuesFromConfig(node.FloatingWidget);
                SettingsPanel.IsEnabled = true;
                UpdateWidgetStatus();
            }
            else
            {
                _selectedNode = null;
                SettingsPanel.IsEnabled = false;
                WidgetStatusText.Text = string.Empty;
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
                SlideToEdgeCheckBox.IsChecked = cfg.SlideToEdgeWhenIdle;
                EdgeDockAsSquareCheckBox.IsChecked = cfg.EdgeDockAsSquare;
                IdleTimeoutTextBox.Text = cfg.IdleTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
                SlideHidePercentTextBox.Text = (cfg.SlideHidePercent * 100).ToString("0.#", CultureInfo.InvariantCulture);

                AlwaysOnTopCheckBox.IsChecked = cfg.AlwaysOnTop;
                ShowInTaskbarCheckBox.IsChecked = cfg.ShowInTaskbar;
                ShowTitleBarCheckBox.IsChecked = cfg.ShowTitleBar;
                AutoHideTitleBarCheckBox.IsChecked = cfg.AutoHideTitleBar;
                TitleBarHideTimeoutTextBox.Text = cfg.TitleBarHideTimeoutSeconds.ToString(CultureInfo.InvariantCulture);

                SelectComboByTag(DisplayModeComboBox, cfg.DisplayMode.ToString());
                SelectMonitor(cfg.MonitorIndex);
                ShowOnAllMonitorsCheckBox.IsChecked = cfg.ShowOnAllMonitors;
            }
            finally
            {
                _loadingValues = false;
            }
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
            cfg.SlideToEdgeWhenIdle = SlideToEdgeCheckBox.IsChecked == true;
            cfg.EdgeDockAsSquare = EdgeDockAsSquareCheckBox.IsChecked == true;
            cfg.IdleTimeoutSeconds = (int)ParseDouble(IdleTimeoutTextBox.Text, cfg.IdleTimeoutSeconds);
            cfg.SlideHidePercent = ParseDouble(SlideHidePercentTextBox.Text, cfg.SlideHidePercent * 100.0) / 100.0;

            cfg.AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true;
            cfg.ShowInTaskbar = ShowInTaskbarCheckBox.IsChecked == true;
            cfg.ShowTitleBar = ShowTitleBarCheckBox.IsChecked == true;
            cfg.AutoHideTitleBar = AutoHideTitleBarCheckBox.IsChecked == true;
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

            // Nếu widget đang mở, refresh để áp dụng tức thì
            if (FloatingWidgetManager.Instance.IsWidgetOpen(_selectedNode.Id))
            {
                FloatingWidgetManager.Instance.RefreshWidget(_selectedNode.Id);
            }
            UpdateWidgetStatus();
            PopulateNodeList();
            SelectNodeInCombo(_selectedNode);
            RefreshExistingWidgets();
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
            }
            Close();
        }

        public void SelectNodeById(string? nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) return;
            foreach (var obj in NodeComboBox.Items)
            {
                if (obj is ComboBoxItem cbi && cbi.Tag is WorkflowNode node &&
                    string.Equals(node.Id, nodeId, System.StringComparison.OrdinalIgnoreCase))
                {
                    NodeComboBox.SelectedItem = cbi;
                    return;
                }
            }
        }
    }
}
