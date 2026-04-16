using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class OutputNodeDialog : BaseNodeDialog
    {
        private readonly OutputNodeDialogViewModel _viewModel;

        public OutputNodeDialog(OutputNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            // Initialize ViewModel
            _viewModel = new OutputNodeDialogViewModel(node, host);

            // Kết nối với BaseNodeDialog
            InitializeBase(_viewModel, owner);

            // Update title color preview
            UpdateTitleColorPreview();

            // Update empty state visibility
            UpdateEmptyState();
            _viewModel.Variables.CollectionChanged += (s, e) => UpdateEmptyState();
        }

        protected override Panel? GetInputsPanel() => null; // OutputNode không có inputs section
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.ViewModel_PropertyChanged(sender, e);
            // Thêm xử lý riêng (nếu cần)
        }

        private void UpdateEmptyState()
        {
            if (EmptyStateText != null && VariablesItemsControl != null)
            {
                EmptyStateText.Visibility = _viewModel.Variables.Count == 0 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        private void TitleColorComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
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
                // Màu theo node - lấy từ node hiện tại
                if (_viewModel?.Node != null)
                {
                    brush = _viewModel.Node.NodeBrush;
                }
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
    }
}

