using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class GitSourceNodeDialog : BaseNodeDialog
    {
        private readonly GitSourceNodeDialogViewModel _viewModel;

        public GitSourceNodeDialog(GitSourceNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new GitSourceNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);
            UpdateTitleColorPreview();
            UpdateIconColorPreview();
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private void PickNodeColor_Click(object sender, RoutedEventArgs e)
        {
            var hex = ShowColorPicker(_viewModel.TitleColorKey);
            if (!string.IsNullOrWhiteSpace(hex))
            {
                _viewModel.TitleColorKey = hex;
                UpdateTitleColorPreview();
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

        private void IconColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateIconColorPreview();
        }

        private void UpdateIconColorPreview()
        {
            if (IconColorPreview == null) return;
            var key = _viewModel.IconColorKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                IconColorPreview.Background = Brushes.White;
                return;
            }
            IconColorPreview.Background = ResolveBrush(key, Brushes.White);
        }
    }
}
