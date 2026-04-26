using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

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

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                _viewModel.SaveFfmpegPathPreference();
            }
            catch
            {
                // best-effort
            }
            base.OnClosing(e);
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

        private void BrowseFfmpegPathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new OpenFileDialog
                {
                    Title = "Chon ffmpeg.exe",
                    Filter = "ffmpeg.exe|ffmpeg.exe|Executable files|*.exe|All files|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (picker.ShowDialog(this) == true)
                {
                    _viewModel.FfmpegPath = picker.FileName;
                    _viewModel.SaveFfmpegPathPreference();
                }
            }
            catch
            {
                // best-effort
            }
        }

        private void BrowseFfmpegFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new OpenFolderDialog
                {
                    Title = "Chon thu muc chua ffmpeg.exe",
                    Multiselect = false
                };
                if (picker.ShowDialog(this) == true)
                {
                    _viewModel.FfmpegPath = picker.FolderName;
                    _viewModel.SaveFfmpegPathPreference();
                }
            }
            catch
            {
                // best-effort
            }
        }

        private void OpenFfmpegGuideLink_Click(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch
            {
                // ignore
            }
        }
    }
}
