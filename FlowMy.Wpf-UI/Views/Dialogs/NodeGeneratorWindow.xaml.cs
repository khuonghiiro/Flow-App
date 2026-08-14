// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    /// <summary>
    /// Code-behind cho NodeGeneratorWindow — Tool tạo base node files tự động.
    /// </summary>
    public partial class NodeGeneratorWindow : Window
    {
        private readonly NodeGeneratorViewModel _viewModel;

        // Cache references sau Loaded
        private System.Windows.Controls.Border? _nodeMockBorder;
        private System.Windows.Controls.Border? _previewInputPortBorder;
        private System.Windows.Controls.Border? _previewOutputPortBorder;
        private FlowMy.Controls.SvgViewboxEx? _previewIcon;
        private System.Windows.Controls.TextBlock? _previewNodeTitle;
        private System.Windows.Controls.TextBox? _previewTextBox;

        public NodeGeneratorWindow() : this(string.Empty) { }

        public NodeGeneratorWindow(string projectRoot)
        {
            InitializeComponent();

            _viewModel = new NodeGeneratorViewModel();

            // Override project root nếu được truyền vào
            if (!string.IsNullOrWhiteSpace(projectRoot) && System.IO.Directory.Exists(projectRoot))
                _viewModel.ProjectRoot = projectRoot;

            DataContext = _viewModel;

            // Subscribe để cập nhật preview khi form thay đổi
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.InputPorts.CollectionChanged  += (_, __) => UpdatePreview();
            _viewModel.OutputPorts.CollectionChanged += (_, __) => UpdatePreview();
            _viewModel.CustomTextBoxes.CollectionChanged  += (_, __) => UpdatePreview();
            _viewModel.CustomComboBoxes.CollectionChanged += (_, __) => UpdatePreview();
            _viewModel.CustomCheckBoxes.CollectionChanged += (_, __) => UpdatePreview();
            _viewModel.RadioGroups.CollectionChanged      += (_, __) => UpdatePreview();

            // Cache và gọi preview sau khi XAML render xong
            Loaded += (_, __) =>
            {
                _nodeMockBorder  = FindName("NodeMockBorder")  as System.Windows.Controls.Border;
                _previewInputPortBorder = FindName("PreviewInputPortBorder") as System.Windows.Controls.Border;
                _previewOutputPortBorder = FindName("PreviewOutputPortBorder") as System.Windows.Controls.Border;
                _previewIcon     = FindName("PreviewIcon")      as FlowMy.Controls.SvgViewboxEx;
                _previewNodeTitle= FindName("PreviewNodeTitle") as System.Windows.Controls.TextBlock;
                _previewTextBox  = FindName("PreviewTextBox")   as System.Windows.Controls.TextBox;
                UpdatePreview();
            };
        }

        // ─── Drag window by title bar ─────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        // ─── Close ───────────────────────────────────────────────────────────

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        // ─── Live preview update ──────────────────────────────────────────────

        private void MainTabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.Source is System.Windows.Controls.TabControl)
            {
                UpdatePreview();

                // Ẩn footer khi ở tab 2 (Chỉnh Sửa) — tab 2 đã có nút "CẬP NHẬT" riêng
                if (FooterBorder != null)
                {
                    FooterBorder.Visibility = MainTabControl?.SelectedIndex == 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            => UpdatePreview();

        private void UpdatePreview()
        {
            if (_viewModel == null) return;

            try
            {
                // Update text preview
                if (_previewTextBox != null)
                    _previewTextBox.Text = _viewModel.GetPreviewText();

                bool isEditing = MainTabControl?.SelectedIndex == 1;
                
                string nodeTitleText = "Node Name";
                if (isEditing)
                {
                    nodeTitleText = string.IsNullOrWhiteSpace(_viewModel.SelectedExistingNode) ? "Node Name" : _viewModel.SelectedExistingNode;
                }
                else
                {
                    nodeTitleText = string.IsNullOrWhiteSpace(_viewModel.Title)
                        ? (string.IsNullOrWhiteSpace(_viewModel.NodeName) ? "Node Name" : _viewModel.NodeName)
                        : _viewModel.Title;
                }

                // Update node title
                if (_previewNodeTitle != null)
                    _previewNodeTitle.Text = nodeTitleText;

                string colorKeyToUse = isEditing ? _viewModel.EditColorKey : _viewModel.ColorKey;
                string iconKeyToUse = isEditing ? _viewModel.EditIconKey : _viewModel.IconKey;

                // Update preview icon
                if (_previewIcon != null)
                {
                    var converter = new FlowMy.Converters.IconKeyToPathConverter();
                    var uri = converter.Convert(iconKeyToUse, typeof(Uri), null, System.Globalization.CultureInfo.InvariantCulture) as Uri;
                    _previewIcon.Source = uri;
                    
                    if (!string.IsNullOrWhiteSpace(colorKeyToUse))
                        _previewIcon.SetResourceReference(FlowMy.Controls.SvgViewboxEx.FillProperty, $"TextOn{colorKeyToUse}Brush");

                    double size = isEditing ? _viewModel.EditIconSize : _viewModel.IconSize;
                    if (size >= 16 && size <= 60)
                    {
                        _previewIcon.Width = size;
                        _previewIcon.Height = size;
                    }
                }

                // Update node mock background từ {ColorKey}Brush
                if (_nodeMockBorder != null)
                {
                    var key = colorKeyToUse?.Trim();
                    if (!string.IsNullOrEmpty(key))
                    {
                        // Thử tìm resource với key chính xác
                        var brush = TryFindBrush($"{key}Brush")
                                 ?? TryFindBrush($"{key}") // fallback không có suffix
                                 ?? this.TryFindResource("InfoBrush") as Brush;

                        if (brush != null)
                            _nodeMockBorder.Background = brush;
                    }
                }

                // Update port mock background
                bool hasIn = isEditing ? _viewModel.HasExistingInputPort : _viewModel.HasInputSection;
                bool hasOut = isEditing ? _viewModel.HasExistingOutputPort : _viewModel.HasOutputsPanel;

                if (_previewInputPortBorder != null)
                {
                    _previewInputPortBorder.Visibility = hasIn ? Visibility.Visible : Visibility.Collapsed;
                    if (hasIn)
                    {
                        string inColor = isEditing ? _viewModel.EditInputPortColorKey : (_viewModel.InputPorts.Count > 0 ? _viewModel.InputPorts[0].ColorKey : "Info");
                        var brush = TryFindBrush($"{inColor?.Trim()}Brush") ?? TryFindBrush($"{inColor?.Trim()}") ?? TryFindBrush("InfoBrush");
                        if (brush != null) _previewInputPortBorder.Background = brush;
                    }
                }

                if (_previewOutputPortBorder != null)
                {
                    _previewOutputPortBorder.Visibility = hasOut ? Visibility.Visible : Visibility.Collapsed;
                    if (hasOut)
                    {
                        string outColor = isEditing ? _viewModel.EditOutputPortColorKey : (_viewModel.OutputPorts.Count > 0 ? _viewModel.OutputPorts[0].ColorKey : "SunsetOrange");
                        var brush = TryFindBrush($"{outColor?.Trim()}Brush") ?? TryFindBrush($"{outColor?.Trim()}") ?? TryFindBrush("SunsetOrangeBrush");
                        if (brush != null) _previewOutputPortBorder.Background = brush;
                    }
                }
            }
            catch
            {
                // Preview không critical — bỏ qua lỗi
            }
        }

        private Brush? TryFindBrush(string resourceKey)
        {
            try { return this.TryFindResource(resourceKey) as Brush; }
            catch { return null; }
        }

        /// <summary>
        /// Trả về ký tự đại diện (ASCII/Unicode cơ bản) cho từng nhóm màu.
        /// Tránh dùng emoji phức tạp vì có thể render lỗi trên một số hệ thống.
        /// </summary>
        private static string GetIconForColorKey(string colorKey)
        {
            if (string.IsNullOrWhiteSpace(colorKey)) return "◆";

            return colorKey.ToLowerInvariant() switch
            {
                // Semantic
                "info"    => "i",
                "success" => "✓",
                "warning" => "!",
                "danger"  => "✕",
                "dark"    => "■",
                "light"   => "□",

                // Blue tones
                var k when k.Contains("blue") || k.Contains("navy") || k.Contains("cobalt") || k.Contains("steel")
                         || k.Contains("azure") || k.Contains("indigo") || k.Contains("prussian")
                         || k.Contains("sapphire") || k.Contains("cerulean") || k.Contains("peacock")
                         || k.Contains("fluidity") || k.Contains("atlassian")
                    => "◈",

                // Green tones
                var k when k.Contains("green") || k.Contains("forest") || k.Contains("jade") || k.Contains("bamboo")
                         || k.Contains("emerald") || k.Contains("lime") || k.Contains("sage") || k.Contains("pistachio")
                         || k.Contains("moss") || k.Contains("seafoam") || k.Contains("mint") || k.Contains("kiwi")
                         || k.Contains("cucumber") || k.Contains("ocean") || k.Contains("teal") || k.Contains("arctic")
                    => "◉",

                // Red/Orange/Coral
                var k when k.Contains("red") || k.Contains("coral") || k.Contains("orange") || k.Contains("sunset")
                         || k.Contains("ruby") || k.Contains("crimson") || k.Contains("raspberry") || k.Contains("brick")
                         || k.Contains("terracotta") || k.Contains("burgundy") || k.Contains("wine") || k.Contains("mango")
                         || k.Contains("tangerine") || k.Contains("pumpkin") || k.Contains("cantaloupe") || k.Contains("salmon")
                    => "◇",

                // Yellow/Gold
                var k when k.Contains("yellow") || k.Contains("gold") || k.Contains("amber") || k.Contains("lemon")
                         || k.Contains("marigold") || k.Contains("honey") || k.Contains("peach") || k.Contains("apricot")
                         || k.Contains("champagne") || k.Contains("buttercup") || k.Contains("sunflower") || k.Contains("eggyolk")
                    => "★",

                // Purple/Violet/Magenta
                var k when k.Contains("purple") || k.Contains("violet") || k.Contains("lavender") || k.Contains("amethyst")
                         || k.Contains("plum") || k.Contains("wisteria") || k.Contains("slate") || k.Contains("iris")
                         || k.Contains("magenta") || k.Contains("fuchsia") || k.Contains("lilac") || k.Contains("orchid")
                         || k.Contains("cherry") || k.Contains("blush") || k.Contains("rose") || k.Contains("royal")
                    => "◆",

                // Brown/Gray
                var k when k.Contains("brown") || k.Contains("chocolate") || k.Contains("espresso") || k.Contains("caramel")
                         || k.Contains("bronze") || k.Contains("gray") || k.Contains("charcoal") || k.Contains("graphite")
                         || k.Contains("aubergine")
                    => "▣",

                _ => "◆"
            };
        }
    }
}

