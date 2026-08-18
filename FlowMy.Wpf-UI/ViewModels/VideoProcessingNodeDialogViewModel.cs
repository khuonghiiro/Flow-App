// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Utilities;

namespace FlowMy.ViewModels
{
    public sealed class KeyValueDisplayOption
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public partial class VideoProcessingNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly VideoProcessingNode _videoNode;
        private bool _isRealtimeSyncing;

        // ═══════ Video Sources & Output Folders ═══════
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

        // ═══════ Subtitle AI (STT Workflow) ═══════
        [ObservableProperty] private string _subtitleSplitMode = "Duration";
        [ObservableProperty] private double _subtitleChunkDurationSec = 60.0;
        [ObservableProperty] private int _subtitleChunkCount = 10;
        [ObservableProperty] private bool _subtitleEnableSmartSilenceSplit = true;
        [ObservableProperty] private double _subtitleSilenceThresholdDb = -30.0;
        [ObservableProperty] private double _subtitleMinSilenceSec = 0.3;
        [ObservableProperty] private double _subtitleMaxSearchWindowSec = 8.0;
        [ObservableProperty] private bool _subtitleUseEditedAudio = true;
        [ObservableProperty] private string _subtitleAudioExportFormat = "mp3";
        [ObservableProperty] private string _subtitleAudioExportBitrate = "128k";
        [ObservableProperty] private bool _subtitleOutputBase64;

        [ObservableProperty] private string? _returnSubtitleNodeId;
        [ObservableProperty] private string? _returnSubtitleOutputKey;
        [ObservableProperty] private string _returnSubtitleCodeIdKeys = "chunkIndex, chunkId, codeId, segmentId, chunk_index, chunk_id, id";
        [ObservableProperty] private string _returnSubtitleTextKeys = "text, translated_text, translation, content, subtitle, transcript, result, sentence, caption, val";
        [ObservableProperty] private string _returnSubtitleOrigTextKeys = "original_text, orig_text, source_text, src_text, raw_text, origin, raw, source";
        [ObservableProperty] private string _returnSubtitleSpeakerKeys = "speaker, speaker_id, speaker_name, character, person, voice, role, actor";
        [ObservableProperty] private string _returnSubtitleTranslationsKeys = "translations, langs, localized, translated, languages, trans";
        [ObservableProperty] private string _returnSubtitleWordsKeys = "words, word_timestamps, tokens, word_list, aligned_words";
        [ObservableProperty] private string _returnSubtitleStartKeys = "start, start_time, startTime, from, begin, st, start_sec, start_ms, offset";
        [ObservableProperty] private string _returnSubtitleEndKeys = "end, end_time, endTime, to, ed, end_sec, end_ms, duration, dur, length";
        [ObservableProperty] private string _returnSubtitleListKeys = "segments, items, lines, subtitles, chunks, utterances, data, results, sentences";

        // ═══════ Dubbing (TTS / Voiceover Workflow) ═══════
        [ObservableProperty] private string _dubbingSplitMode = "SubtitleSegments";
        [ObservableProperty] private string _dubbingTargetVoice = string.Empty;
        [ObservableProperty] private string _dubbingTargetLanguage = "vi";
        [ObservableProperty] private double _dubbingSpeechRate = 1.0;
        [ObservableProperty] private double _dubbingPitch;
        [ObservableProperty] private bool _dubbingAutoDuckOriginalAudio = true;
        [ObservableProperty] private double _dubbingOriginalAudioVolumePercent = 20.0;

        [ObservableProperty] private string? _returnDubbingNodeId;
        [ObservableProperty] private string? _returnDubbingOutputKey;
        [ObservableProperty] private string _returnDubbingCodeIdKeys = "chunkId, codeId, chunkIndex, segmentId, id";
        [ObservableProperty] private string _returnDubbingAudioLinkKeys = "audioUrl, linkAudio, audioPath, url, path, src, link";
        [ObservableProperty] private string _returnDubbingAudioBase64Keys = "audioBase64, base64, audioData, data";
        [ObservableProperty] private string _returnDubbingStartKeys = "start, start_time, startTime, from";
        [ObservableProperty] private string _returnDubbingEndKeys = "duration, end, end_time, endTime, to";

        // ═══════ Collections & Options ═══════
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<WorkflowDataSourceOption> ReturnSubtitleNodeOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> ReturnSubtitleKeyOptions { get; } = new();
        public ObservableCollection<WorkflowDataSourceOption> ReturnDubbingNodeOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> ReturnDubbingKeyOptions { get; } = new();

        public List<KeyValueDisplayOption> SplitModeOptions { get; } = new()
        {
            new KeyValueDisplayOption { Key = "Duration", DisplayName = "Theo thời lượng từng đoạn (giây)" },
            new KeyValueDisplayOption { Key = "EqualParts", DisplayName = "Chia đều thành N phần bằng nhau" }
        };

        public List<string> AudioFormatOptions { get; } = new() { "mp3", "wav", "m4a", "aac", "ogg", "flac" };
        public List<string> AudioBitrateOptions { get; } = new() { "64k", "128k", "192k", "256k", "320k" };

        public List<KeyValueDisplayOption> DubbingSplitModeOptions { get; } = new()
        {
            new KeyValueDisplayOption { Key = "SubtitleSegments", DisplayName = "Theo từng phân đoạn phụ đề (Khuyên dùng)" },
            new KeyValueDisplayOption { Key = "Duration", DisplayName = "Theo từng đoạn thời gian cố định" },
            new KeyValueDisplayOption { Key = "Full", DisplayName = "Toàn bộ âm thanh video" }
        };

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

            // Subtitle AI
            SubtitleSplitMode = node.SubtitleSplitMode;
            SubtitleChunkDurationSec = node.SubtitleChunkDurationSec;
            SubtitleChunkCount = node.SubtitleChunkCount;
            SubtitleEnableSmartSilenceSplit = node.SubtitleEnableSmartSilenceSplit;
            SubtitleSilenceThresholdDb = node.SubtitleSilenceThresholdDb;
            SubtitleMinSilenceSec = node.SubtitleMinSilenceSec;
            SubtitleMaxSearchWindowSec = node.SubtitleMaxSearchWindowSec;
            SubtitleUseEditedAudio = node.SubtitleUseEditedAudio;
            SubtitleAudioExportFormat = node.SubtitleAudioExportFormat;
            SubtitleAudioExportBitrate = node.SubtitleAudioExportBitrate;
            SubtitleOutputBase64 = node.SubtitleOutputBase64;

            ReturnSubtitleNodeId = node.ReturnSubtitleNodeId;
            ReturnSubtitleOutputKey = node.ReturnSubtitleOutputKey;
            ReturnSubtitleCodeIdKeys = node.ReturnSubtitleCodeIdKeys;
            ReturnSubtitleTextKeys = node.ReturnSubtitleTextKeys;
            ReturnSubtitleOrigTextKeys = node.ReturnSubtitleOrigTextKeys;
            ReturnSubtitleSpeakerKeys = node.ReturnSubtitleSpeakerKeys;
            ReturnSubtitleTranslationsKeys = node.ReturnSubtitleTranslationsKeys;
            ReturnSubtitleWordsKeys = node.ReturnSubtitleWordsKeys;
            ReturnSubtitleStartKeys = node.ReturnSubtitleStartKeys;
            ReturnSubtitleEndKeys = node.ReturnSubtitleEndKeys;
            ReturnSubtitleListKeys = node.ReturnSubtitleListKeys;

            // Dubbing
            DubbingSplitMode = node.DubbingSplitMode;
            DubbingTargetVoice = node.DubbingTargetVoice;
            DubbingTargetLanguage = node.DubbingTargetLanguage;
            DubbingSpeechRate = node.DubbingSpeechRate;
            DubbingPitch = node.DubbingPitch;
            DubbingAutoDuckOriginalAudio = node.DubbingAutoDuckOriginalAudio;
            DubbingOriginalAudioVolumePercent = node.DubbingOriginalAudioVolumePercent;

            ReturnDubbingNodeId = node.ReturnDubbingNodeId;
            ReturnDubbingOutputKey = node.ReturnDubbingOutputKey;
            ReturnDubbingCodeIdKeys = node.ReturnDubbingCodeIdKeys;
            ReturnDubbingAudioLinkKeys = node.ReturnDubbingAudioLinkKeys;
            ReturnDubbingAudioBase64Keys = node.ReturnDubbingAudioBase64Keys;
            ReturnDubbingStartKeys = node.ReturnDubbingStartKeys;
            ReturnDubbingEndKeys = node.ReturnDubbingEndKeys;

            RefreshAvailableNodes();
            RefreshReturnSubtitleNodeOptions();
            RefreshReturnDubbingNodeOptions();

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
                    case nameof(VideoProcessingNode.SubtitleSplitMode):
                        SubtitleSplitMode = _videoNode.SubtitleSplitMode;
                        break;
                    case nameof(VideoProcessingNode.SubtitleChunkDurationSec):
                        SubtitleChunkDurationSec = _videoNode.SubtitleChunkDurationSec;
                        break;
                    case nameof(VideoProcessingNode.SubtitleChunkCount):
                        SubtitleChunkCount = _videoNode.SubtitleChunkCount;
                        break;
                    case nameof(VideoProcessingNode.SubtitleEnableSmartSilenceSplit):
                        SubtitleEnableSmartSilenceSplit = _videoNode.SubtitleEnableSmartSilenceSplit;
                        break;
                    case nameof(VideoProcessingNode.SubtitleSilenceThresholdDb):
                        SubtitleSilenceThresholdDb = _videoNode.SubtitleSilenceThresholdDb;
                        break;
                    case nameof(VideoProcessingNode.SubtitleMinSilenceSec):
                        SubtitleMinSilenceSec = _videoNode.SubtitleMinSilenceSec;
                        break;
                    case nameof(VideoProcessingNode.SubtitleMaxSearchWindowSec):
                        SubtitleMaxSearchWindowSec = _videoNode.SubtitleMaxSearchWindowSec;
                        break;
                    case nameof(VideoProcessingNode.SubtitleUseEditedAudio):
                        SubtitleUseEditedAudio = _videoNode.SubtitleUseEditedAudio;
                        break;
                    case nameof(VideoProcessingNode.SubtitleAudioExportFormat):
                        SubtitleAudioExportFormat = _videoNode.SubtitleAudioExportFormat;
                        break;
                    case nameof(VideoProcessingNode.SubtitleAudioExportBitrate):
                        SubtitleAudioExportBitrate = _videoNode.SubtitleAudioExportBitrate;
                        break;
                    case nameof(VideoProcessingNode.SubtitleOutputBase64):
                        SubtitleOutputBase64 = _videoNode.SubtitleOutputBase64;
                        break;
                    case nameof(VideoProcessingNode.ReturnSubtitleNodeId):
                        ReturnSubtitleNodeId = _videoNode.ReturnSubtitleNodeId;
                        break;
                    case nameof(VideoProcessingNode.ReturnSubtitleOutputKey):
                        ReturnSubtitleOutputKey = _videoNode.ReturnSubtitleOutputKey;
                        break;
                    case nameof(VideoProcessingNode.DubbingSplitMode):
                        DubbingSplitMode = _videoNode.DubbingSplitMode;
                        break;
                    case nameof(VideoProcessingNode.DubbingTargetVoice):
                        DubbingTargetVoice = _videoNode.DubbingTargetVoice;
                        break;
                    case nameof(VideoProcessingNode.DubbingTargetLanguage):
                        DubbingTargetLanguage = _videoNode.DubbingTargetLanguage;
                        break;
                    case nameof(VideoProcessingNode.DubbingSpeechRate):
                        DubbingSpeechRate = _videoNode.DubbingSpeechRate;
                        break;
                    case nameof(VideoProcessingNode.DubbingPitch):
                        DubbingPitch = _videoNode.DubbingPitch;
                        break;
                    case nameof(VideoProcessingNode.DubbingAutoDuckOriginalAudio):
                        DubbingAutoDuckOriginalAudio = _videoNode.DubbingAutoDuckOriginalAudio;
                        break;
                    case nameof(VideoProcessingNode.DubbingOriginalAudioVolumePercent):
                        DubbingOriginalAudioVolumePercent = _videoNode.DubbingOriginalAudioVolumePercent;
                        break;
                    case nameof(VideoProcessingNode.ReturnDubbingNodeId):
                        ReturnDubbingNodeId = _videoNode.ReturnDubbingNodeId;
                        break;
                    case nameof(VideoProcessingNode.ReturnDubbingOutputKey):
                        ReturnDubbingOutputKey = _videoNode.ReturnDubbingOutputKey;
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
                case nameof(SubtitleSplitMode):
                    _videoNode.SubtitleSplitMode = SubtitleSplitMode;
                    break;
                case nameof(SubtitleChunkDurationSec):
                    _videoNode.SubtitleChunkDurationSec = SubtitleChunkDurationSec;
                    break;
                case nameof(SubtitleChunkCount):
                    _videoNode.SubtitleChunkCount = SubtitleChunkCount;
                    break;
                case nameof(SubtitleEnableSmartSilenceSplit):
                    _videoNode.SubtitleEnableSmartSilenceSplit = SubtitleEnableSmartSilenceSplit;
                    break;
                case nameof(SubtitleSilenceThresholdDb):
                    _videoNode.SubtitleSilenceThresholdDb = SubtitleSilenceThresholdDb;
                    break;
                case nameof(SubtitleMinSilenceSec):
                    _videoNode.SubtitleMinSilenceSec = SubtitleMinSilenceSec;
                    break;
                case nameof(SubtitleMaxSearchWindowSec):
                    _videoNode.SubtitleMaxSearchWindowSec = SubtitleMaxSearchWindowSec;
                    break;
                case nameof(SubtitleUseEditedAudio):
                    _videoNode.SubtitleUseEditedAudio = SubtitleUseEditedAudio;
                    break;
                case nameof(SubtitleAudioExportFormat):
                    _videoNode.SubtitleAudioExportFormat = SubtitleAudioExportFormat;
                    break;
                case nameof(SubtitleAudioExportBitrate):
                    _videoNode.SubtitleAudioExportBitrate = SubtitleAudioExportBitrate;
                    break;
                case nameof(SubtitleOutputBase64):
                    _videoNode.SubtitleOutputBase64 = SubtitleOutputBase64;
                    break;
                case nameof(ReturnSubtitleNodeId):
                    _videoNode.ReturnSubtitleNodeId = ReturnSubtitleNodeId;
                    RefreshReturnSubtitleKeyOptions();
                    break;
                case nameof(ReturnSubtitleOutputKey):
                    _videoNode.ReturnSubtitleOutputKey = ReturnSubtitleOutputKey;
                    break;
                case nameof(ReturnSubtitleCodeIdKeys):
                    _videoNode.ReturnSubtitleCodeIdKeys = ReturnSubtitleCodeIdKeys;
                    break;
                case nameof(ReturnSubtitleTextKeys):
                    _videoNode.ReturnSubtitleTextKeys = ReturnSubtitleTextKeys;
                    break;
                case nameof(ReturnSubtitleOrigTextKeys):
                    _videoNode.ReturnSubtitleOrigTextKeys = ReturnSubtitleOrigTextKeys;
                    break;
                case nameof(ReturnSubtitleSpeakerKeys):
                    _videoNode.ReturnSubtitleSpeakerKeys = ReturnSubtitleSpeakerKeys;
                    break;
                case nameof(ReturnSubtitleTranslationsKeys):
                    _videoNode.ReturnSubtitleTranslationsKeys = ReturnSubtitleTranslationsKeys;
                    break;
                case nameof(ReturnSubtitleWordsKeys):
                    _videoNode.ReturnSubtitleWordsKeys = ReturnSubtitleWordsKeys;
                    break;
                case nameof(ReturnSubtitleStartKeys):
                    _videoNode.ReturnSubtitleStartKeys = ReturnSubtitleStartKeys;
                    break;
                case nameof(ReturnSubtitleEndKeys):
                    _videoNode.ReturnSubtitleEndKeys = ReturnSubtitleEndKeys;
                    break;
                case nameof(ReturnSubtitleListKeys):
                    _videoNode.ReturnSubtitleListKeys = ReturnSubtitleListKeys;
                    break;
                case nameof(DubbingSplitMode):
                    _videoNode.DubbingSplitMode = DubbingSplitMode;
                    break;
                case nameof(DubbingTargetVoice):
                    _videoNode.DubbingTargetVoice = DubbingTargetVoice;
                    break;
                case nameof(DubbingTargetLanguage):
                    _videoNode.DubbingTargetLanguage = DubbingTargetLanguage;
                    break;
                case nameof(DubbingSpeechRate):
                    _videoNode.DubbingSpeechRate = DubbingSpeechRate;
                    break;
                case nameof(DubbingPitch):
                    _videoNode.DubbingPitch = DubbingPitch;
                    break;
                case nameof(DubbingAutoDuckOriginalAudio):
                    _videoNode.DubbingAutoDuckOriginalAudio = DubbingAutoDuckOriginalAudio;
                    break;
                case nameof(DubbingOriginalAudioVolumePercent):
                    _videoNode.DubbingOriginalAudioVolumePercent = DubbingOriginalAudioVolumePercent;
                    break;
                case nameof(ReturnDubbingNodeId):
                    _videoNode.ReturnDubbingNodeId = ReturnDubbingNodeId;
                    RefreshReturnDubbingKeyOptions();
                    break;
                case nameof(ReturnDubbingOutputKey):
                    _videoNode.ReturnDubbingOutputKey = ReturnDubbingOutputKey;
                    break;
                case nameof(ReturnDubbingCodeIdKeys):
                    _videoNode.ReturnDubbingCodeIdKeys = ReturnDubbingCodeIdKeys;
                    break;
                case nameof(ReturnDubbingAudioLinkKeys):
                    _videoNode.ReturnDubbingAudioLinkKeys = ReturnDubbingAudioLinkKeys;
                    break;
                case nameof(ReturnDubbingAudioBase64Keys):
                    _videoNode.ReturnDubbingAudioBase64Keys = ReturnDubbingAudioBase64Keys;
                    break;
                case nameof(ReturnDubbingStartKeys):
                    _videoNode.ReturnDubbingStartKeys = ReturnDubbingStartKeys;
                    break;
                case nameof(ReturnDubbingEndKeys):
                    _videoNode.ReturnDubbingEndKeys = ReturnDubbingEndKeys;
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

        public void RefreshReturnSubtitleNodeOptions()
        {
            var list = new List<WorkflowDataSourceOption>();
            if (_host.ViewModel?.Nodes != null)
            {
                foreach (var n in _host.ViewModel.Nodes)
                {
                    if (ReferenceEquals(n, _videoNode)) continue;
                    EnsureNodeDynamicOutputsSynced(n);
                    if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                    list.Add(CreateDataSourceOption(n));
                }
            }

            var currentId = ReturnSubtitleNodeId;
            ReturnSubtitleNodeOptions.Clear();
            foreach (var opt in list) ReturnSubtitleNodeOptions.Add(opt);

            if (!string.IsNullOrWhiteSpace(currentId) && list.Any(o => string.Equals(o.NodeId, currentId, StringComparison.OrdinalIgnoreCase)))
            {
                ReturnSubtitleNodeId = currentId;
            }

            RefreshReturnSubtitleKeyOptions();
        }

        public void RefreshReturnSubtitleKeyOptions()
        {
            var currentKey = ReturnSubtitleOutputKey;
            var options = GetOutputKeysForNode(ReturnSubtitleNodeId);
            ReturnSubtitleKeyOptions.Clear();
            foreach (var opt in options) ReturnSubtitleKeyOptions.Add(opt);

            if (!string.IsNullOrWhiteSpace(currentKey) && options.Any(o => string.Equals(o.Key, currentKey, StringComparison.OrdinalIgnoreCase)))
            {
                ReturnSubtitleOutputKey = currentKey;
            }
            else if (options.Count > 0 && string.IsNullOrWhiteSpace(ReturnSubtitleOutputKey))
            {
                ReturnSubtitleOutputKey = options[0].Key;
            }
        }

        public void RefreshReturnDubbingNodeOptions()
        {
            var list = new List<WorkflowDataSourceOption>();
            if (_host.ViewModel?.Nodes != null)
            {
                foreach (var n in _host.ViewModel.Nodes)
                {
                    if (ReferenceEquals(n, _videoNode)) continue;
                    EnsureNodeDynamicOutputsSynced(n);
                    if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                    list.Add(CreateDataSourceOption(n));
                }
            }

            var currentId = ReturnDubbingNodeId;
            ReturnDubbingNodeOptions.Clear();
            foreach (var opt in list) ReturnDubbingNodeOptions.Add(opt);

            if (!string.IsNullOrWhiteSpace(currentId) && list.Any(o => string.Equals(o.NodeId, currentId, StringComparison.OrdinalIgnoreCase)))
            {
                ReturnDubbingNodeId = currentId;
            }

            RefreshReturnDubbingKeyOptions();
        }

        public void RefreshReturnDubbingKeyOptions()
        {
            var currentKey = ReturnDubbingOutputKey;
            var options = GetOutputKeysForNode(ReturnDubbingNodeId);
            ReturnDubbingKeyOptions.Clear();
            foreach (var opt in options) ReturnDubbingKeyOptions.Add(opt);

            if (!string.IsNullOrWhiteSpace(currentKey) && options.Any(o => string.Equals(o.Key, currentKey, StringComparison.OrdinalIgnoreCase)))
            {
                ReturnDubbingOutputKey = currentKey;
            }
            else if (options.Count > 0 && string.IsNullOrWhiteSpace(ReturnDubbingOutputKey))
            {
                ReturnDubbingOutputKey = options[0].Key;
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

            // Subtitle AI
            _videoNode.SubtitleSplitMode = SubtitleSplitMode;
            _videoNode.SubtitleChunkDurationSec = SubtitleChunkDurationSec;
            _videoNode.SubtitleChunkCount = SubtitleChunkCount;
            _videoNode.SubtitleEnableSmartSilenceSplit = SubtitleEnableSmartSilenceSplit;
            _videoNode.SubtitleSilenceThresholdDb = SubtitleSilenceThresholdDb;
            _videoNode.SubtitleMinSilenceSec = SubtitleMinSilenceSec;
            _videoNode.SubtitleMaxSearchWindowSec = SubtitleMaxSearchWindowSec;
            _videoNode.SubtitleUseEditedAudio = SubtitleUseEditedAudio;
            _videoNode.SubtitleAudioExportFormat = SubtitleAudioExportFormat;
            _videoNode.SubtitleAudioExportBitrate = SubtitleAudioExportBitrate;
            _videoNode.SubtitleOutputBase64 = SubtitleOutputBase64;

            _videoNode.ReturnSubtitleNodeId = string.IsNullOrWhiteSpace(ReturnSubtitleNodeId) ? null : ReturnSubtitleNodeId;
            _videoNode.ReturnSubtitleOutputKey = string.IsNullOrWhiteSpace(ReturnSubtitleOutputKey) ? null : ReturnSubtitleOutputKey;
            _videoNode.ReturnSubtitleCodeIdKeys = ReturnSubtitleCodeIdKeys;
            _videoNode.ReturnSubtitleTextKeys = ReturnSubtitleTextKeys;
            _videoNode.ReturnSubtitleOrigTextKeys = ReturnSubtitleOrigTextKeys;
            _videoNode.ReturnSubtitleSpeakerKeys = ReturnSubtitleSpeakerKeys;
            _videoNode.ReturnSubtitleTranslationsKeys = ReturnSubtitleTranslationsKeys;
            _videoNode.ReturnSubtitleWordsKeys = ReturnSubtitleWordsKeys;
            _videoNode.ReturnSubtitleStartKeys = ReturnSubtitleStartKeys;
            _videoNode.ReturnSubtitleEndKeys = ReturnSubtitleEndKeys;
            _videoNode.ReturnSubtitleListKeys = ReturnSubtitleListKeys;

            // Dubbing
            _videoNode.DubbingSplitMode = DubbingSplitMode;
            _videoNode.DubbingTargetVoice = DubbingTargetVoice;
            _videoNode.DubbingTargetLanguage = DubbingTargetLanguage;
            _videoNode.DubbingSpeechRate = DubbingSpeechRate;
            _videoNode.DubbingPitch = DubbingPitch;
            _videoNode.DubbingAutoDuckOriginalAudio = DubbingAutoDuckOriginalAudio;
            _videoNode.DubbingOriginalAudioVolumePercent = DubbingOriginalAudioVolumePercent;

            _videoNode.ReturnDubbingNodeId = string.IsNullOrWhiteSpace(ReturnDubbingNodeId) ? null : ReturnDubbingNodeId;
            _videoNode.ReturnDubbingOutputKey = string.IsNullOrWhiteSpace(ReturnDubbingOutputKey) ? null : ReturnDubbingOutputKey;
            _videoNode.ReturnDubbingCodeIdKeys = ReturnDubbingCodeIdKeys;
            _videoNode.ReturnDubbingAudioLinkKeys = ReturnDubbingAudioLinkKeys;
            _videoNode.ReturnDubbingAudioBase64Keys = ReturnDubbingAudioBase64Keys;
            _videoNode.ReturnDubbingStartKeys = ReturnDubbingStartKeys;
            _videoNode.ReturnDubbingEndKeys = ReturnDubbingEndKeys;

            _videoNode.EnsureStandardDynamicOutputs();
            _videoNode.NotifyTitleChanged();
            _host.RequestSyncDataPanels(immediate: true);
        }
    }
}
