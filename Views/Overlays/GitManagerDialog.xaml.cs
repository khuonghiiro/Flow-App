using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class GitManagerDialog : BaseNodeDialog
    {
        private readonly GitManagerDialogViewModel _viewModel;

        public GitSourceNode? ResultNode => _viewModel.ResultNode;

        public GitManagerDialog(GitSourceNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new GitManagerDialogViewModel(node, host);
            DataContext = _viewModel;

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (owner != null) Owner = owner;

            UpdateNodeColorPreview();
            UpdateIconColorPreview();
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => null;

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void NodeColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateNodeColorPreview();

        private void IconColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateIconColorPreview();

        private void PickNodeColor_Click(object sender, RoutedEventArgs e)
        {
            var hex = ShowColorPicker(_viewModel.NodeColorKey);
            if (!string.IsNullOrWhiteSpace(hex))
            {
                _viewModel.NodeColorKey = hex;
                UpdateNodeColorPreview();
            }
        }

        private void PickIconColor_Click(object sender, RoutedEventArgs e)
        {
            var hex = ShowColorPicker(_viewModel.IconColorKey);
            if (!string.IsNullOrWhiteSpace(hex))
            {
                _viewModel.IconColorKey = hex;
                UpdateIconColorPreview();
            }
        }

        private void UpdateNodeColorPreview()
        {
            if (NodeColorPreview == null) return;
            var brush = ResolveBrush(_viewModel.NodeColorKey, Brushes.Indigo);
            NodeColorPreview.Background = brush;
        }

        private void UpdateIconColorPreview()
        {
            if (IconColorPreview == null) return;
            var brush = ResolveBrush(_viewModel.IconColorKey, Brushes.White);
            IconColorPreview.Background = brush;
        }

        protected new void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CancelCurrentOperation();
            DialogResult = false;
            Close();
        }
    }
}
