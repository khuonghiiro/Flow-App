using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class MediaGalleryNodeDialog : BaseNodeDialog
    {
        private readonly MediaGalleryNodeDialogViewModel _viewModel;

        public MediaGalleryNodeDialog(MediaGalleryNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new MediaGalleryNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);
            _viewModel.RefreshAvailableNodes();
            UpdateTitleColorPreview();
            LoadFolderKeyCombo();
            LoadFolderVideoKeyCombo();
            LoadJsonKeyCombo();
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private void LoadFolderKeyCombo()
        {
            if (FolderKeyComboBox != null && !string.IsNullOrEmpty(_viewModel.FolderSourceNodeId))
                FolderKeyComboBox.ItemsSource = _viewModel.GetOutputKeysForNode(_viewModel.FolderSourceNodeId);
        }

        private void FolderNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FolderKeyComboBox == null || _viewModel?.FolderSourceNodeId == null) return;
            FolderKeyComboBox.ItemsSource = _viewModel.GetOutputKeysForNode(_viewModel.FolderSourceNodeId);
        }

        private void LoadFolderVideoKeyCombo()
        {
            if (FolderVideoKeyComboBox != null && !string.IsNullOrEmpty(_viewModel.FolderSourceNodeIdVideo))
                FolderVideoKeyComboBox.ItemsSource = _viewModel.GetOutputKeysForNode(_viewModel.FolderSourceNodeIdVideo);
        }

        private void FolderVideoNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FolderVideoKeyComboBox == null || _viewModel?.FolderSourceNodeIdVideo == null) return;
            FolderVideoKeyComboBox.ItemsSource = _viewModel.GetOutputKeysForNode(_viewModel.FolderSourceNodeIdVideo);
        }

        private void LoadJsonKeyCombo()
        {
            if (JsonKeyComboBox != null && !string.IsNullOrEmpty(_viewModel?.JsonSourceNodeId))
                JsonKeyComboBox.ItemsSource = _viewModel.GetOutputKeysForNode(_viewModel.JsonSourceNodeId);
        }

        private void JsonNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (JsonKeyComboBox == null || _viewModel?.JsonSourceNodeId == null) return;
            JsonKeyComboBox.ItemsSource = _viewModel.GetOutputKeysForNode(_viewModel.JsonSourceNodeId);
        }

        private void CopyGridJsonExample_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(MediaGalleryJsonExamples.GridExample);
            }
            catch
            {
                // ignore clipboard errors
            }
        }

        private void CopyGroupedJsonExample_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(MediaGalleryJsonExamples.GroupedExample);
            }
            catch
            {
                // ignore clipboard errors
            }
        }
    }
}
