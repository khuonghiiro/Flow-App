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
        private System.Windows.Controls.TextBlock? _previewIcon;
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
                _previewIcon     = FindName("PreviewIcon")      as System.Windows.Controls.TextBlock;
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

                // Update node title
                if (_previewNodeTitle != null)
                    _previewNodeTitle.Text = string.IsNullOrWhiteSpace(_viewModel.Title)
                        ? (string.IsNullOrWhiteSpace(_viewModel.NodeName) ? "Node Name" : _viewModel.NodeName)
                        : _viewModel.Title;

                // Update preview icon (ký tự an toàn, không dùng emoji có thể render lỗi)
                if (_previewIcon != null)
                    _previewIcon.Text = GetIconForColorKey(_viewModel.ColorKey);

                // Update node mock background từ {ColorKey}Brush
                if (_nodeMockBorder != null)
                {
                    var key = _viewModel.ColorKey?.Trim();
                    if (!string.IsNullOrEmpty(key))
                    {
                        // Thử tìm resource với key chính xác
                        var brush = TryFindBrush($"{key}Brush")
                                 ?? TryFindBrush($"{key}") // fallback không có suffix
                                 ?? Application.Current.TryFindResource("InfoBrush") as Brush;

                        if (brush != null)
                            _nodeMockBorder.Background = brush;
                    }
                }
            }
            catch
            {
                // Preview không critical — bỏ qua lỗi
            }
        }

        private static Brush? TryFindBrush(string resourceKey)
        {
            try { return Application.Current.TryFindResource(resourceKey) as Brush; }
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

