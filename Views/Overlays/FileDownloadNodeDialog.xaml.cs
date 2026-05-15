using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class FileDownloadNodeDialog : BaseNodeDialog
    {
        private readonly FileDownloadNodeDialogViewModel _vm;

        public FileDownloadNodeDialog(FileDownloadNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _vm = new FileDownloadNodeDialogViewModel(node, host);
            InitializeBase(_vm, owner);
            _vm.RefreshAvailableNodes();
            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Multiselect = false,
                Title = "Chọn thư mục lưu file tải về"
            };
            if (dlg.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dlg.FolderName))
                _vm.DownloadFolderPath = dlg.FolderName;
        }
    }
}
