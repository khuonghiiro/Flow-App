using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class FolderFilePathsNodeDialog : BaseNodeDialog
    {
        private readonly FolderFilePathsNodeDialogViewModel _vm;

        public FolderFilePathsNodeDialog(FolderFilePathsNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _vm = new FolderFilePathsNodeDialogViewModel(node, host);
            InitializeBase(_vm, owner);
            _vm.RefreshAvailableNodes();
            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private new void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Multiselect = false,
                Title = "Chọn thư mục cần liệt kê file"
            };
            if (dlg.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dlg.FolderName))
                _vm.FolderPath = dlg.FolderName;
        }

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
                brush = _vm?.Node?.NodeBrush;
            else if (colorKey == "LimeGreen")
                brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
            else
                brush = Application.Current.TryFindResource(colorKey) as System.Windows.Media.Brush
                     ?? Application.Current.TryFindResource($"{colorKey}Brush") as System.Windows.Media.Brush;

            TitleColorPreview.Background = brush ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }
    }
}
