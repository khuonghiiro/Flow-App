using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class StringSplitNodeDialog : BaseNodeDialog
    {
        private readonly StringSplitNodeDialogViewModel _viewModel;

        public StringSplitNodeDialog(StringSplitNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            // Initialize ViewModel
            _viewModel = new StringSplitNodeDialogViewModel(node, host);

            // ⚠️ CRITICAL: Kết nối với BaseNodeDialog
            InitializeBase(_viewModel, owner);

            // Initialize title color preview
            UpdateTitleColorPreview();
        }

        // ⚠️ CRITICAL: Override để BaseNodeDialog biết panels nào để populate
        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.ViewModel_PropertyChanged(sender, e);
            // Thêm xử lý riêng nếu cần (ví dụ: custom validation)
        }

        // ⚠️ CRITICAL: Phải gọi SaveTitleCommand trước khi Close
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        private void TitleColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTitleColorPreview();
        }

        private void UpdateTitleColorPreview()
        {
            if (TitleColorPreview == null || _viewModel == null) return;

            var colorKey = _viewModel.TitleColorKey;
            Brush? brush = null;

            if (string.IsNullOrEmpty(colorKey) || colorKey == "NodeColor")
            {
                // Use node brush
                if (_viewModel is StringSplitNodeDialogViewModel vm && vm.Node is StringSplitNode node)
                {
                    brush = node.NodeBrush;
                }
            }
            else if (colorKey == "LimeGreen")
            {
                brush = new SolidColorBrush(Colors.LimeGreen);
            }
            else
            {
                brush = Application.Current.TryFindResource(colorKey) as Brush;
            }

            TitleColorPreview.Background = brush ?? new SolidColorBrush(Colors.Gray);
        }
    }
}
