using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class GitManagerDialog : Window
    {
        private readonly GitManagerDialogViewModel _viewModel;

        public GitSourceNode? ResultNode => _viewModel.ResultNode;

        public GitManagerDialog(GitSourceNode node, IWorkflowEditorHost host, Window? owner)
        {
            InitializeComponent();
            _viewModel = new GitManagerDialogViewModel(node, host);
            DataContext = _viewModel;

            if (owner != null) Owner = owner;

            // Lắng nghe event chuyển sang tab Git khi nhấn Sửa
            _viewModel.RequestSwitchToGitTab += () =>
            {
                TabGit.IsChecked = true;
                UpdateNodeColorPreview();
                UpdateIconColorPreview();
            };

            Loaded += (s, e) =>
            {
                UpdateNodeColorPreview();
                UpdateIconColorPreview();
            };
        }

        // ═══ Chặn maximize ═══
        private void Window_StateChanged(object sender, System.EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
        }

        // ═══ Main tab switching ═══
        private void MainTab_Checked(object sender, RoutedEventArgs e)
        {
            if (PageTongHop == null || PageGit == null) return;

            if (TabTongHop?.IsChecked == true)
            {
                PageTongHop.Visibility = Visibility.Visible;
                PageGit.Visibility = Visibility.Collapsed;
            }
            else
            {
                PageTongHop.Visibility = Visibility.Collapsed;
                PageGit.Visibility = Visibility.Visible;
            }
        }

        // ═══ Sub-tab switching ═══
        private void SubTab_Checked(object sender, RoutedEventArgs e)
        {
            if (SubPageClone == null || SubPageDisplay == null || SubPageSettings == null) return;

            SubPageClone.Visibility = Visibility.Collapsed;
            SubPageDisplay.Visibility = Visibility.Collapsed;
            SubPageSettings.Visibility = Visibility.Collapsed;

            if (SubTabClone?.IsChecked == true)
                SubPageClone.Visibility = Visibility.Visible;
            else if (SubTabDisplay?.IsChecked == true)
                SubPageDisplay.Visibility = Visibility.Visible;
            else if (SubTabSettings?.IsChecked == true)
                SubPageSettings.Visibility = Visibility.Visible;
        }

        // ═══ Color pickers ═══
        private void NodeColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateNodeColorPreview();

        private void IconColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateIconColorPreview();

        private void PickNodeColor_Click(object sender, RoutedEventArgs e)
        {
            // Simple color picker — dùng system dialog
            var dlg = new System.Windows.Forms.ColorDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                _viewModel.NodeColorKey = hex;
                UpdateNodeColorPreview();
            }
        }

        private void PickIconColor_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.ColorDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                _viewModel.IconColorKey = hex;
                UpdateIconColorPreview();
            }
        }

        private void UpdateNodeColorPreview()
        {
            if (NodeColorPreview == null) return;
            NodeColorPreview.Background = ResolveBrush(_viewModel.NodeColorKey, Brushes.Indigo);
            _viewModel.PreviewNodeBrush = NodeColorPreview.Background;
        }

        private void UpdateIconColorPreview()
        {
            if (IconColorPreview == null) return;
            IconColorPreview.Background = ResolveBrush(_viewModel.IconColorKey, Brushes.White);
            _viewModel.PreviewIconBrush = IconColorPreview.Background;
        }

        private static Brush ResolveBrush(string? key, Brush fallback)
        {
            if (string.IsNullOrWhiteSpace(key)) return fallback;

            if (key.StartsWith("#"))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(key);
                    return new SolidColorBrush(color);
                }
                catch { return fallback; }
            }

            return Application.Current?.TryFindResource($"{key}Brush") as Brush
                ?? Application.Current?.TryFindResource(key) as Brush
                ?? fallback;
        }
    }
}
