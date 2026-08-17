// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;

namespace FlowMy.Views.Overlays
{
    public partial class VideoProcessingNodeDialog : BaseNodeDialog
    {
        private readonly VideoProcessingNodeDialogViewModel _viewModel;

        public VideoProcessingNodeDialog(VideoProcessingNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new VideoProcessingNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
            RefreshVideoSourceKeyOptions();
            RefreshOutputFolderKeyOptions();
            RefreshVideoOutputFolderKeyOptions();
            UpdateOutputFolderVisibility();
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        private void VideoSourceNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => RefreshVideoSourceKeyOptions();

        private void OutputFolderNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => RefreshOutputFolderKeyOptions();

        private void VideoOutputFolderNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => RefreshVideoOutputFolderKeyOptions();

        private void ReturnSubtitleNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => _viewModel.RefreshReturnSubtitleKeyOptions();

        private void ReturnDubbingNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => _viewModel.RefreshReturnDubbingKeyOptions();

        private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _ = e;
        }

        private void UpdateOutputFolderVisibility()
            => OutputFolderPanel.Visibility = Visibility.Visible;

        private void RefreshVideoSourceKeyOptions()
        {
            if (VideoSourceKeyComboBox == null) return;
            VideoSourceKeyComboBox.ItemsSource = _viewModel.GetOutputKeysForNode(_viewModel.VideoSourceNodeId);
        }

        private void RefreshOutputFolderKeyOptions()
        {
            if (OutputFolderKeyComboBox == null) return;
            OutputFolderKeyComboBox.ItemsSource = _viewModel.GetOutputKeysForNode(_viewModel.OutputFolderSourceNodeId);
        }

        private void RefreshVideoOutputFolderKeyOptions()
        {
            if (VideoOutputFolderKeyComboBox == null) return;
            VideoOutputFolderKeyComboBox.ItemsSource = _viewModel.GetOutputKeysForNode(_viewModel.VideoOutputFolderSourceNodeId);
        }
    }
}
