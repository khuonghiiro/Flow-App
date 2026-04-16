using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class CodeNodeDialog : BaseNodeDialog
    {
        private readonly CodeNodeDialogViewModel _viewModel;

        public CodeNodeDialog(CodeNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new CodeNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);
            _viewModel.RefreshAvailableNodes();
            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private void TitleColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTitleColorPreview();
        }

        private void UpdateTitleColorPreview()
        {
            if (TitleColorPreview == null || TitleColorComboBox?.SelectedValue == null) return;
            var colorKey = TitleColorComboBox.SelectedValue.ToString();
            System.Windows.Media.Brush? brush = null;
            if (string.IsNullOrEmpty(colorKey) || colorKey == "NodeColor")
            {
                if (_viewModel?.Node != null) brush = _viewModel.Node.NodeBrush;
            }
            else if (colorKey == "LimeGreen")
            {
                brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
            }
            else
            {
                brush = Application.Current.TryFindResource(colorKey) as System.Windows.Media.Brush;
            }
            TitleColorPreview.Background = brush ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        private void ExpandCodeButton_Click(object sender, RoutedEventArgs e)
        {
            var popup = new CodeEditorPopupWindow(_viewModel, this);
            popup.ShowDialog();
        }
    }
}
