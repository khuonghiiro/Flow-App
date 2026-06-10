using FlowMy.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace FlowMy.Views.Overlays
{
    /// <summary>
    /// Code-behind cho NodeGeneratorWindow — Tool tạo base node files tự động.
    /// </summary>
    public partial class NodeGeneratorWindow : Window
    {
        private readonly NodeGeneratorViewModel _viewModel;

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
            _viewModel.InputPorts.CollectionChanged += (_, __) => UpdatePreview();
            _viewModel.OutputPorts.CollectionChanged += (_, __) => UpdatePreview();
            _viewModel.CustomTextBoxes.CollectionChanged += (_, __) => UpdatePreview();
            _viewModel.CustomComboBoxes.CollectionChanged += (_, __) => UpdatePreview();
            _viewModel.CustomCheckBoxes.CollectionChanged += (_, __) => UpdatePreview();
            _viewModel.RadioGroups.CollectionChanged += (_, __) => UpdatePreview();

            // Initial preview
            Loaded += (_, __) => UpdatePreview();
        }

        // ─── Drag window by title bar ─────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        // ─── Close ───────────────────────────────────────────────────────────

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ─── Live preview update ──────────────────────────────────────────────

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Update preview on any property change (throttle-free is fine for this tool)
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (_viewModel == null || PreviewTextBox == null) return;

            try
            {
                // Update preview text
                PreviewTextBox.Text = _viewModel.GetPreviewText();

                // Update node mock title
                PreviewNodeTitle.Text = string.IsNullOrWhiteSpace(_viewModel.Title)
                    ? (string.IsNullOrWhiteSpace(_viewModel.NodeName) ? "Node Name" : _viewModel.NodeName)
                    : _viewModel.Title;

                // Update icon emoji based on ColorKey
                PreviewIcon.Text = GetEmojiForColorKey(_viewModel.ColorKey);

                // Update node border color
                if (Application.Current.TryFindResource($"{_viewModel.ColorKey}Brush") is System.Windows.Media.Brush brush)
                {
                    var nodeMockBorder = FindNodeMockBorder();
                    if (nodeMockBorder != null)
                        nodeMockBorder.Background = brush;
                }
            }
            catch
            {
                // Bỏ qua lỗi preview - không critical
            }
        }

        private System.Windows.Controls.Border? FindNodeMockBorder()
        {
            // Tìm border mock node trong visual tree
            try
            {
                return FindName("NodeMockBorder") as System.Windows.Controls.Border;
            }
            catch { return null; }
        }

        private static string GetEmojiForColorKey(string colorKey) => colorKey?.ToLower() switch
        {
            "info" => "ℹ️",
            "primary" => "⚡",
            "success" => "✅",
            "warning" => "⚠️",
            "danger" => "🔴",
            "sunsetorange" => "🌅",
            "limegreen" => "🌿",
            "forestpine" => "🌲",
            "navydeep" => "🌊",
            "chocolatebrown" => "🍫",
            "gold" => "⭐",
            "violet" => "💜",
            "teal" => "🩵",
            "coral" => "🪸",
            _ => "⬡"
        };
    }
}
