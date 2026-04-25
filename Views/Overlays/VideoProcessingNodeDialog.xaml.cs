using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

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

        private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VideoProcessingNodeDialogViewModel.OutputBase64))
                UpdateOutputFolderVisibility();
        }

        private void UpdateOutputFolderVisibility()
            => OutputFolderPanel.Visibility = _viewModel.OutputBase64 ? Visibility.Collapsed : Visibility.Visible;

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
    }
}
