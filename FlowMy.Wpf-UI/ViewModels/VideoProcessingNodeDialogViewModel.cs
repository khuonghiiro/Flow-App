// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using CommunityToolkit.Mvvm.ComponentModel;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class VideoProcessingNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly VideoProcessingNode _videoNode;
        private bool _isRealtimeSyncing;

        [ObservableProperty] private string? _videoSourceNodeId;
        [ObservableProperty] private string? _videoSourceOutputKey;
        [ObservableProperty] private string? _outputFolderSourceNodeId;
        [ObservableProperty] private string? _outputFolderSourceOutputKey;
        [ObservableProperty] private string? _videoOutputFolderSourceNodeId;
        [ObservableProperty] private string? _videoOutputFolderSourceOutputKey;
        [ObservableProperty] private string? _audioOutputFolderSourceNodeId;
        [ObservableProperty] private string? _audioOutputFolderSourceOutputKey;
        [ObservableProperty] private bool _outputBase64 = true;
        [ObservableProperty] private bool _useDialogVideoConfig = true;
        [ObservableProperty] private bool _extractFramesEnabled = true;
        [ObservableProperty] private bool _exportVideoEnabled = true;
        [ObservableProperty] private bool _extractAudioEnabled;
        [ObservableProperty] private string? _frameOutputFolderPath;
        [ObservableProperty] private string? _defaultOutputVideoPath;
        [ObservableProperty] private string? _audioOutputFolderPath;

        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();

        public VideoProcessingNodeDialogViewModel(VideoProcessingNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _videoNode = node;
            _videoNode.EnsureStandardDynamicOutputs();
            RefreshOutputs();

            VideoSourceNodeId = node.VideoSourceNodeId;
            VideoSourceOutputKey = node.VideoSourceOutputKey;
            OutputFolderSourceNodeId = node.OutputFolderSourceNodeId;
            OutputFolderSourceOutputKey = node.OutputFolderSourceOutputKey;
            VideoOutputFolderSourceNodeId = node.VideoOutputFolderSourceNodeId;
            VideoOutputFolderSourceOutputKey = node.VideoOutputFolderSourceOutputKey;
            AudioOutputFolderSourceNodeId = node.AudioOutputFolderSourceNodeId;
            AudioOutputFolderSourceOutputKey = node.AudioOutputFolderSourceOutputKey;
            OutputBase64 = node.OutputBase64;
            UseDialogVideoConfig = node.UseDialogVideoConfig;
            ExtractFramesEnabled = node.ExtractFramesEnabled;
            ExportVideoEnabled = node.ExportVideoEnabled;
            ExtractAudioEnabled = node.ExtractAudioEnabled;
            FrameOutputFolderPath = node.FrameOutputFolderPath;
            DefaultOutputVideoPath = node.DefaultOutputVideoPath;
            AudioOutputFolderPath = node.AudioOutputFolderPath;

            RefreshAvailableNodes();

            PropertyChanged += VideoProcessingNodeDialogViewModel_PropertyChanged;

            if (node is INotifyPropertyChanged npc)
                npc.PropertyChanged += (_, e) => OnNodePropertyChanged(e.PropertyName ?? string.Empty);
        }

        protected override void OnNodePropertyChanged(string propertyName)
        {
            if (_isRealtimeSyncing) return;

            try
            {
                _isRealtimeSyncing = true;
                switch (propertyName)
                {
                    case nameof(VideoSourceNodeId):
                        VideoSourceNodeId = _videoNode.VideoSourceNodeId;
                        break;
                    case nameof(VideoSourceOutputKey):
                        VideoSourceOutputKey = _videoNode.VideoSourceOutputKey;
                        break;
                    case nameof(OutputFolderSourceNodeId):
                        OutputFolderSourceNodeId = _videoNode.OutputFolderSourceNodeId;
                        break;
                    case nameof(OutputFolderSourceOutputKey):
                        OutputFolderSourceOutputKey = _videoNode.OutputFolderSourceOutputKey;
                        break;
                    case nameof(VideoProcessingNode.UseDialogVideoConfig):
                        UseDialogVideoConfig = _videoNode.UseDialogVideoConfig;
                        break;
                    case nameof(VideoProcessingNode.FrameOutputFolderPath):
                        FrameOutputFolderPath = _videoNode.FrameOutputFolderPath;
                        break;
                    case nameof(VideoProcessingNode.DefaultOutputVideoPath):
                        DefaultOutputVideoPath = _videoNode.DefaultOutputVideoPath;
                        break;
                    case nameof(VideoProcessingNode.OutputBase64):
                        OutputBase64 = _videoNode.OutputBase64;
                        break;
                    case nameof(VideoProcessingNode.VideoOutputFolderSourceNodeId):
                        VideoOutputFolderSourceNodeId = _videoNode.VideoOutputFolderSourceNodeId;
                        break;
                    case nameof(VideoProcessingNode.VideoOutputFolderSourceOutputKey):
                        VideoOutputFolderSourceOutputKey = _videoNode.VideoOutputFolderSourceOutputKey;
                        break;
                    case nameof(VideoProcessingNode.AudioOutputFolderSourceNodeId):
                        AudioOutputFolderSourceNodeId = _videoNode.AudioOutputFolderSourceNodeId;
                        break;
                    case nameof(VideoProcessingNode.AudioOutputFolderSourceOutputKey):
                        AudioOutputFolderSourceOutputKey = _videoNode.AudioOutputFolderSourceOutputKey;
                        break;
                    case nameof(VideoProcessingNode.ExtractFramesEnabled):
                        ExtractFramesEnabled = _videoNode.ExtractFramesEnabled;
                        break;
                    case nameof(VideoProcessingNode.ExportVideoEnabled):
                        ExportVideoEnabled = _videoNode.ExportVideoEnabled;
                        break;
                    case nameof(VideoProcessingNode.ExtractAudioEnabled):
                        ExtractAudioEnabled = _videoNode.ExtractAudioEnabled;
                        break;
                    case nameof(VideoProcessingNode.AudioOutputFolderPath):
                        AudioOutputFolderPath = _videoNode.AudioOutputFolderPath;
                        break;
                }
            }
            finally
            {
                _isRealtimeSyncing = false;
            }
        }

        private void VideoProcessingNodeDialogViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isRealtimeSyncing) return;
            if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

            switch (e.PropertyName)
            {
                case nameof(VideoSourceNodeId):
                    _videoNode.VideoSourceNodeId = VideoSourceNodeId;
                    break;
                case nameof(VideoSourceOutputKey):
                    _videoNode.VideoSourceOutputKey = VideoSourceOutputKey;
                    break;
                case nameof(OutputFolderSourceNodeId):
                    _videoNode.OutputFolderSourceNodeId = OutputFolderSourceNodeId;
                    break;
                case nameof(OutputFolderSourceOutputKey):
                    _videoNode.OutputFolderSourceOutputKey = OutputFolderSourceOutputKey;
                    break;
                case nameof(UseDialogVideoConfig):
                    _videoNode.UseDialogVideoConfig = UseDialogVideoConfig;
                    break;
                case nameof(FrameOutputFolderPath):
                    _videoNode.FrameOutputFolderPath = FrameOutputFolderPath;
                    break;
                case nameof(DefaultOutputVideoPath):
                    _videoNode.DefaultOutputVideoPath = DefaultOutputVideoPath;
                    break;
                case nameof(OutputBase64):
                    _videoNode.OutputBase64 = OutputBase64;
                    break;
                case nameof(VideoOutputFolderSourceNodeId):
                    _videoNode.VideoOutputFolderSourceNodeId = VideoOutputFolderSourceNodeId;
                    break;
                case nameof(VideoOutputFolderSourceOutputKey):
                    _videoNode.VideoOutputFolderSourceOutputKey = VideoOutputFolderSourceOutputKey;
                    break;
                case nameof(AudioOutputFolderSourceNodeId):
                    _videoNode.AudioOutputFolderSourceNodeId = AudioOutputFolderSourceNodeId;
                    break;
                case nameof(AudioOutputFolderSourceOutputKey):
                    _videoNode.AudioOutputFolderSourceOutputKey = AudioOutputFolderSourceOutputKey;
                    break;
                case nameof(ExtractFramesEnabled):
                    _videoNode.ExtractFramesEnabled = ExtractFramesEnabled;
                    break;
                case nameof(ExportVideoEnabled):
                    _videoNode.ExportVideoEnabled = ExportVideoEnabled;
                    break;
                case nameof(ExtractAudioEnabled):
                    _videoNode.ExtractAudioEnabled = ExtractAudioEnabled;
                    break;
                case nameof(AudioOutputFolderPath):
                    _videoNode.AudioOutputFolderPath = AudioOutputFolderPath;
                    break;
            }
        }

        protected override string GetDefaultTitle() => "Video Processing";

        public void RefreshAvailableNodes()
        {
            var vm = _host.ViewModel;
            if (vm?.Nodes == null) return;

            var producerNodes = GetUpstreamProducerNodes(_videoNode);
            var newOptions = producerNodes.Select(n => CreateDataSourceOption(n)).ToList();

            var mappedNodeIds = new List<string?>
            {
                VideoSourceNodeId, OutputFolderSourceNodeId, VideoOutputFolderSourceNodeId, AudioOutputFolderSourceNodeId
            }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var nodeId in mappedNodeIds)
            {
                if (newOptions.Any(o => string.Equals(o.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var n = vm.Nodes.FirstOrDefault(x => string.Equals(x.Id, nodeId, StringComparison.OrdinalIgnoreCase));
                if (n != null)
                    newOptions.Add(CreateDataSourceOption(n));
            }

            AvailableNodeOptions.Clear();
            foreach (var option in newOptions)
            {
                AvailableNodeOptions.Add(option);
            }
        }

        protected override void OnSaveTitle()
        {
            _videoNode.VideoSourceNodeId = string.IsNullOrWhiteSpace(VideoSourceNodeId) ? null : VideoSourceNodeId;
            _videoNode.VideoSourceOutputKey = string.IsNullOrWhiteSpace(VideoSourceOutputKey) ? null : VideoSourceOutputKey;
            _videoNode.OutputFolderSourceNodeId = string.IsNullOrWhiteSpace(OutputFolderSourceNodeId) ? null : OutputFolderSourceNodeId;
            _videoNode.OutputFolderSourceOutputKey = string.IsNullOrWhiteSpace(OutputFolderSourceOutputKey) ? null : OutputFolderSourceOutputKey;
            _videoNode.VideoOutputFolderSourceNodeId = string.IsNullOrWhiteSpace(VideoOutputFolderSourceNodeId) ? null : VideoOutputFolderSourceNodeId;
            _videoNode.VideoOutputFolderSourceOutputKey = string.IsNullOrWhiteSpace(VideoOutputFolderSourceOutputKey) ? null : VideoOutputFolderSourceOutputKey;
            _videoNode.AudioOutputFolderSourceNodeId = string.IsNullOrWhiteSpace(AudioOutputFolderSourceNodeId) ? null : AudioOutputFolderSourceNodeId;
            _videoNode.AudioOutputFolderSourceOutputKey = string.IsNullOrWhiteSpace(AudioOutputFolderSourceOutputKey) ? null : AudioOutputFolderSourceOutputKey;
            _videoNode.OutputBase64 = OutputBase64;
            _videoNode.UseDialogVideoConfig = UseDialogVideoConfig;
            _videoNode.ExtractFramesEnabled = ExtractFramesEnabled;
            _videoNode.ExportVideoEnabled = ExportVideoEnabled;
            _videoNode.ExtractAudioEnabled = ExtractAudioEnabled;
            _videoNode.FrameOutputFolderPath = string.IsNullOrWhiteSpace(FrameOutputFolderPath) ? null : FrameOutputFolderPath;
            _videoNode.DefaultOutputVideoPath = string.IsNullOrWhiteSpace(DefaultOutputVideoPath) ? null : DefaultOutputVideoPath;
            _videoNode.AudioOutputFolderPath = string.IsNullOrWhiteSpace(AudioOutputFolderPath) ? null : AudioOutputFolderPath;
            _videoNode.EnsureStandardDynamicOutputs();
            _videoNode.NotifyTitleChanged();
            _host.RequestSyncDataPanels(immediate: true);
        }

    }
}
