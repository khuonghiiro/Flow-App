using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class BorderHighlightNodeDialog : BaseNodeDialog
    {
        private readonly BorderHighlightNodeDialogViewModel _viewModel;

        public BorderHighlightNodeDialog(BorderHighlightNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new BorderHighlightNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            UpdateTitleColorPreview();
            UpdateBorderColorPreview();

            // Set initial visibility of target app panel
            TargetAppPanel.Visibility = _viewModel.HighlightMode == Models.BorderHighlightMode.TargetApp
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Hide/show target app panel based on mode
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.HighlightMode))
                {
                    TargetAppPanel.Visibility = _viewModel.HighlightMode == Models.BorderHighlightMode.TargetApp
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            };
        }

        protected override Panel? GetInputsPanel() => InputsPanel;

        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private void BorderColorPickerButton_Click(object sender, RoutedEventArgs e)
        {
            var hex = ShowColorPicker(_viewModel.BorderColorHex);
            if (!string.IsNullOrWhiteSpace(hex))
            {
                _viewModel.BorderColorHex = hex;
                UpdateBorderColorPreview();
            }
        }

        private void UpdateBorderColorPreview()
        {
            if (BorderColorPreview != null)
            {
                BorderColorPreview.Background = ResolveBrush(_viewModel.BorderColorHex, Brushes.Transparent);
            }
        }
    }
}
