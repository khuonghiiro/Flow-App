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

        [ObservableProperty] private string? _videoSourceNodeId;
        [ObservableProperty] private string? _videoSourceOutputKey;
        [ObservableProperty] private string? _outputFolderSourceNodeId;
        [ObservableProperty] private string? _outputFolderSourceOutputKey;
        [ObservableProperty] private bool _outputBase64 = true;
        [ObservableProperty] private bool _useDialogVideoConfig = true;
        [ObservableProperty] private string? _frameOutputFolderPath;
        [ObservableProperty] private string? _defaultOutputVideoPath;
        [ObservableProperty] private string _ffmpegPath = string.Empty;

        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();

        public VideoProcessingNodeDialogViewModel(VideoProcessingNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _videoNode = node;

            VideoSourceNodeId = node.VideoSourceNodeId;
            VideoSourceOutputKey = node.VideoSourceOutputKey;
            OutputFolderSourceNodeId = node.OutputFolderSourceNodeId;
            OutputFolderSourceOutputKey = node.OutputFolderSourceOutputKey;
            OutputBase64 = node.OutputBase64;
            UseDialogVideoConfig = node.UseDialogVideoConfig;
            FrameOutputFolderPath = node.FrameOutputFolderPath;
            DefaultOutputVideoPath = node.DefaultOutputVideoPath;
            FfmpegPath = FfmpegPathPreferencesStore.Load().FfmpegPath ?? string.Empty;

            RefreshAvailableNodes();
            if (node is INotifyPropertyChanged npc)
                npc.PropertyChanged += (_, e) => OnNodePropertyChanged(e.PropertyName ?? string.Empty);
        }

        protected override string GetDefaultTitle() => "Video Processing";

        public void RefreshAvailableNodes()
        {
            AvailableNodeOptions.Clear();
            if (_host.ViewModel?.Nodes == null) return;
            foreach (var n in _host.ViewModel.Nodes)
            {
                if (ReferenceEquals(n, _videoNode)) continue;
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                AvailableNodeOptions.Add(CreateDataSourceOption(n));
            }
        }

        public ObservableCollection<WorkflowOutputKeyOption> GetOutputKeysForNode(string? nodeId)
        {
            var list = new ObservableCollection<WorkflowOutputKeyOption>();
            if (string.IsNullOrWhiteSpace(nodeId) || _host.ViewModel?.Nodes == null) return list;

            var node = _host.ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, nodeId, System.StringComparison.OrdinalIgnoreCase));
            if (node?.DynamicOutputs == null) return list;

            foreach (var o in node.DynamicOutputs)
            {
                list.Add(new WorkflowOutputKeyOption
                {
                    Key = o.Key ?? string.Empty,
                    DisplayName = o.DisplayName ?? o.Key,
                    Type = o.OutputType ?? o.ConvertType
                });
            }

            return list;
        }

        protected override void OnSaveTitle()
        {
            _videoNode.VideoSourceNodeId = string.IsNullOrWhiteSpace(VideoSourceNodeId) ? null : VideoSourceNodeId;
            _videoNode.VideoSourceOutputKey = string.IsNullOrWhiteSpace(VideoSourceOutputKey) ? null : VideoSourceOutputKey;
            _videoNode.OutputFolderSourceNodeId = string.IsNullOrWhiteSpace(OutputFolderSourceNodeId) ? null : OutputFolderSourceNodeId;
            _videoNode.OutputFolderSourceOutputKey = string.IsNullOrWhiteSpace(OutputFolderSourceOutputKey) ? null : OutputFolderSourceOutputKey;
            _videoNode.OutputBase64 = OutputBase64;
            _videoNode.UseDialogVideoConfig = UseDialogVideoConfig;
            _videoNode.FrameOutputFolderPath = string.IsNullOrWhiteSpace(FrameOutputFolderPath) ? null : FrameOutputFolderPath;
            _videoNode.DefaultOutputVideoPath = string.IsNullOrWhiteSpace(DefaultOutputVideoPath) ? null : DefaultOutputVideoPath;
            SaveFfmpegPathPreference();
            _videoNode.NotifyTitleChanged();
            _host.RequestSyncDataPanels(immediate: true);
        }

        public void SaveFfmpegPathPreference()
        {
            var normalized = FfmpegPathPreferencesStore.NormalizeUserInput(FfmpegPath);
            FfmpegPath = normalized;
            FfmpegPathPreferencesStore.Save(new FfmpegPathPreferences
            {
                FfmpegPath = normalized
            });
        }
    }
}
