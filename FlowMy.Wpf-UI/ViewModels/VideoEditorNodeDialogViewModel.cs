using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class VideoEditorInputMappingItemViewModel : ObservableObject
    {
        [ObservableProperty] private string? _sourceNodeId;
        [ObservableProperty] private string? _sourceOutputKey;
        [ObservableProperty] private string _inputKeyOverride = string.Empty;
        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();
    }

    public partial class VideoEditorNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly VideoEditorNode _videoEditorNode;
        private bool _isSyncingFromNode;

        [ObservableProperty] private VideoEditorDisplayMode _displayMode;
        [ObservableProperty] private string _sourceNodeId = string.Empty;
        [ObservableProperty] private string _sourceOutputKey = string.Empty;
        [ObservableProperty] private string _customKey = string.Empty;
        [ObservableProperty] private string _inputVideoUrl = string.Empty;

        // Trim
        [ObservableProperty] private bool _trimEnabled;
        [ObservableProperty] private string _trimStartTime = "00:00:00.000";
        [ObservableProperty] private string _trimEndTime = "00:00:10.000";

        // Filter / Color
        [ObservableProperty] private double _brightness;
        [ObservableProperty] private double _contrast = 1.0;
        [ObservableProperty] private double _saturation = 1.0;
        [ObservableProperty] private double _gamma = 1.0;
        [ObservableProperty] private double _hue;
        [ObservableProperty] private string _filterPreset = "None";

        // Scale / Speed
        [ObservableProperty] private bool _scaleEnabled;
        [ObservableProperty] private int _targetWidth = 1280;
        [ObservableProperty] private int _targetHeight = 720;
        [ObservableProperty] private double _speed = 1.0;
        [ObservableProperty] private string _rotateFlip = "None";

        // Watermark
        [ObservableProperty] private bool _watermarkEnabled;
        [ObservableProperty] private string _watermarkText = string.Empty;
        [ObservableProperty] private string _watermarkImagePath = string.Empty;
        [ObservableProperty] private string _watermarkPosition = "BottomRight";

        // Audio
        [ObservableProperty] private string _audioMode = "Keep";
        [ObservableProperty] private double _audioVolume = 1.0;

        // Export
        [ObservableProperty] private string _exportMode = "Video";
        [ObservableProperty] private double _exportFps = 30.0;
        [ObservableProperty] private string _exportFormat = "mp4";
        [ObservableProperty] private string _outputFolderPath = string.Empty;

        [ObservableProperty] private string _inputMappingsHeader = "📥 Danh sách Inputs (0 items)";
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<VideoEditorInputMappingItemViewModel> InputMappingsList { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> SourceKeyOptions { get; } = new();

        public List<KeyValuePair<VideoEditorDisplayMode, string>> DisplayModeOptions { get; } = new()
        {
            new(VideoEditorDisplayMode.InteractiveEditor, "🎞️ Interactive Editor (Chỉnh sửa trực tiếp)"),
            new(VideoEditorDisplayMode.AutomatedPipeline, "⚙️ Automated Pipeline (Cấu hình tự động)")
        };

        [ObservableProperty] private double _gifFps = 15.0;
        [ObservableProperty] private int _gifScaleWidth = 480;
        [ObservableProperty] private double _frameExtractFps = 1.0;

        public List<string> FilterPresetOptions { get; } = new() { "None", "Vivid", "Warm", "Cool", "Grayscale", "Sepia", "Vintage", "Invert" };
        public List<string> RotateFlipOptions { get; } = new() { "None", "Rotate90", "Rotate180", "Rotate270", "FlipHorizontal", "FlipVertical" };
        public List<string> WatermarkPositionOptions { get; } = new() { "BottomRight", "BottomLeft", "TopRight", "TopLeft", "Center" };
        public List<string> AudioModeOptions { get; } = new() { "Keep", "Mute", "ExtractAudio", "VolumeAdjust" };
        public List<string> ExportModeOptions { get; } = new() { "Video", "FrameSequence", "SingleFrame", "AudioOnly", "Gif" };
        public List<string> ExportFormatOptions { get; } = new() { "mp4", "gif", "webm", "png", "jpg", "mp3" };

        partial void OnSourceNodeIdChanged(string value)
        {
            _videoEditorNode.SourceNodeId = value;
            FillOutputKeys(value, SourceKeyOptions);
        }

        partial void OnSourceOutputKeyChanged(string value)
        {
            _videoEditorNode.SourceOutputKey = value;
        }

        partial void OnCustomKeyChanged(string value)
        {
            _videoEditorNode.CustomKey = value;
        }

        private void UpdateInputMappingsHeader()
        {
            InputMappingsHeader = $"📥 Danh sách Inputs ({InputMappingsList.Count} items)";
        }

        [RelayCommand]
        private void AddInputMapping()
        {
            var item = new VideoEditorInputMappingItemViewModel();
            item.PropertyChanged += InputMappingItem_PropertyChanged;
            InputMappingsList.Add(item);
            UpdateInputMappingsHeader();
        }

        [RelayCommand]
        private void RemoveInputMapping(VideoEditorInputMappingItemViewModel? item)
        {
            if (item != null && InputMappingsList.Contains(item) && InputMappingsList.Count > 1)
            {
                item.PropertyChanged -= InputMappingItem_PropertyChanged;
                InputMappingsList.Remove(item);
                UpdateInputMappingsHeader();
            }
        }

        private void InputMappingItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingFromNode) return;
            if (sender is not VideoEditorInputMappingItemViewModel item) return;
            if (e.PropertyName == nameof(VideoEditorInputMappingItemViewModel.SourceNodeId))
            {
                RefreshOutputKeyOptionsFor(item);
            }
        }

        public void RefreshOutputKeyOptionsFor(VideoEditorInputMappingItemViewModel item)
        {
            item.AvailableOutputKeyOptions.Clear();
            if (string.IsNullOrWhiteSpace(item.SourceNodeId) || _host.ViewModel?.Nodes == null) return;
            var node = _host.ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, item.SourceNodeId, StringComparison.OrdinalIgnoreCase));
            if (node == null) return;
            foreach (var o in GetActiveOutputs(node))
            {
                item.AvailableOutputKeyOptions.Add(new WorkflowOutputKeyOption
                {
                    Key = o.Key ?? string.Empty,
                    Type = o.OutputType ?? o.ConvertType,
                    DisplayName = o.DisplayName ?? o.Key
                });
            }
            if (item.AvailableOutputKeyOptions.Count > 0 &&
                !item.AvailableOutputKeyOptions.Any(k =>
                    string.Equals(k.Key, item.SourceOutputKey, StringComparison.OrdinalIgnoreCase)))
            {
                item.SourceOutputKey = item.AvailableOutputKeyOptions[0].Key;
            }
        }

        public void RefreshAvailableNodes()
        {
            var vm = _host.ViewModel;
            if (vm?.Nodes == null || vm.Connections == null) return;
            var connections = vm.Connections;
            var upstream = new HashSet<WorkflowNode>();
            var stack = new Stack<WorkflowNode>();
            stack.Push(_videoEditorNode);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                foreach (var conn in connections.Where(c => c.ToNode == current && c.FromNode != null))
                {
                    var src = conn.FromNode!;
                    if (upstream.Add(src)) stack.Push(src);
                }
            }
            var newOptions = upstream
                .Where(n => n.DynamicOutputs != null && n.DynamicOutputs.Count > 0 && !ReferenceEquals(n, _videoEditorNode))
                .Select(n => CreateDataSourceOption(n))
                .ToList();
            AvailableNodeOptions.Clear();
            foreach (var opt in newOptions) AvailableNodeOptions.Add(opt);
        }

        public VideoEditorNodeDialogViewModel(VideoEditorNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _videoEditorNode = node ?? throw new ArgumentNullException(nameof(node));

            RefreshAllNodesWithOutputs(AvailableNodeOptions);

            // Load properties from node
            DisplayMode = _videoEditorNode.DisplayMode;
            SourceNodeId = _videoEditorNode.SourceNodeId;
            FillOutputKeys(SourceNodeId, SourceKeyOptions);
            SourceOutputKey = _videoEditorNode.SourceOutputKey;
            CustomKey = _videoEditorNode.CustomKey;
            InputVideoUrl = _videoEditorNode.InputVideoUrl;

            TrimEnabled = _videoEditorNode.TrimEnabled;
            TrimStartTime = _videoEditorNode.TrimStartTime;
            TrimEndTime = _videoEditorNode.TrimEndTime;

            Brightness = _videoEditorNode.Brightness;
            Contrast = _videoEditorNode.Contrast;
            Saturation = _videoEditorNode.Saturation;
            Gamma = _videoEditorNode.Gamma;
            Hue = _videoEditorNode.Hue;
            FilterPreset = _videoEditorNode.FilterPreset;

            ScaleEnabled = _videoEditorNode.ScaleEnabled;
            TargetWidth = _videoEditorNode.TargetWidth;
            TargetHeight = _videoEditorNode.TargetHeight;
            Speed = _videoEditorNode.Speed;
            RotateFlip = _videoEditorNode.RotateFlip;

            WatermarkEnabled = _videoEditorNode.WatermarkEnabled;
            WatermarkText = _videoEditorNode.WatermarkText;
            WatermarkImagePath = _videoEditorNode.WatermarkImagePath;
            WatermarkPosition = _videoEditorNode.WatermarkPosition;

            AudioMode = _videoEditorNode.AudioMode;
            AudioVolume = _videoEditorNode.AudioVolume;

            ExportMode = _videoEditorNode.ExportMode;
            ExportFps = _videoEditorNode.ExportFps;
            ExportFormat = _videoEditorNode.ExportFormat;
            OutputFolderPath = _videoEditorNode.OutputFolderPath;
            GifFps = _videoEditorNode.GifFps;
            GifScaleWidth = _videoEditorNode.GifScaleWidth;
            FrameExtractFps = _videoEditorNode.FrameExtractFps;

            RefreshAvailableNodes();
            if (InputMappingsList.Count == 0)
            {
                var empty = new VideoEditorInputMappingItemViewModel();
                empty.PropertyChanged += InputMappingItem_PropertyChanged;
                InputMappingsList.Add(empty);
            }
        }

        protected override string GetDefaultTitle() => "Chỉnh sửa video";

        protected override void OnSaveTitle()
        {
            base.OnSaveTitle();

            _videoEditorNode.DisplayMode = DisplayMode;
            _videoEditorNode.SourceNodeId = SourceNodeId;
            _videoEditorNode.SourceOutputKey = SourceOutputKey;
            _videoEditorNode.CustomKey = CustomKey;
            _videoEditorNode.InputVideoUrl = InputVideoUrl;

            _videoEditorNode.TrimEnabled = TrimEnabled;
            _videoEditorNode.TrimStartTime = TrimStartTime;
            _videoEditorNode.TrimEndTime = TrimEndTime;

            _videoEditorNode.Brightness = Brightness;
            _videoEditorNode.Contrast = Contrast;
            _videoEditorNode.Saturation = Saturation;
            _videoEditorNode.Gamma = Gamma;
            _videoEditorNode.Hue = Hue;
            _videoEditorNode.FilterPreset = FilterPreset;

            _videoEditorNode.ScaleEnabled = ScaleEnabled;
            _videoEditorNode.TargetWidth = TargetWidth;
            _videoEditorNode.TargetHeight = TargetHeight;
            _videoEditorNode.Speed = Speed;
            _videoEditorNode.RotateFlip = RotateFlip;

            _videoEditorNode.WatermarkEnabled = WatermarkEnabled;
            _videoEditorNode.WatermarkText = WatermarkText;
            _videoEditorNode.WatermarkImagePath = WatermarkImagePath;
            _videoEditorNode.WatermarkPosition = WatermarkPosition;

            _videoEditorNode.AudioMode = AudioMode;
            _videoEditorNode.AudioVolume = AudioVolume;

            _videoEditorNode.ExportMode = ExportMode;
            _videoEditorNode.ExportFps = ExportFps;
            _videoEditorNode.ExportFormat = ExportFormat;
            _videoEditorNode.OutputFolderPath = OutputFolderPath;
            _videoEditorNode.GifFps = GifFps;
            _videoEditorNode.GifScaleWidth = GifScaleWidth;
            _videoEditorNode.FrameExtractFps = FrameExtractFps;

            _videoEditorNode.NotifyTitleChanged();
            _host.RequestSyncDataPanels(immediate: true);
        }
    }
}
