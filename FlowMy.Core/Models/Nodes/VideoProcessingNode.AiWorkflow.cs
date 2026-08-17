// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models.Nodes
{
    public sealed partial class VideoProcessingNode
    {
        // ═════════════════════════════════════════════════════════════════════
        // TAB: PHỤ ĐỀ (SUBTITLE AI / STT WORKFLOW)
        // ═════════════════════════════════════════════════════════════════════
        private string _subtitleSplitMode = "Duration"; // "Duration", "EqualParts"
        private double _subtitleChunkDurationSec = 60.0;
        private int _subtitleChunkCount = 10;
        private bool _subtitleEnableSmartSilenceSplit = true;
        private double _subtitleSilenceThresholdDb = -30.0;
        private double _subtitleMinSilenceSec = 0.3;
        private double _subtitleMaxSearchWindowSec = 8.0;
        private bool _subtitleUseEditedAudio = true;
        private string _subtitleAudioExportFormat = "mp3";
        private string _subtitleAudioExportBitrate = "128k";
        private bool _subtitleOutputBase64;

        private string? _returnSubtitleNodeId;
        private string? _returnSubtitleOutputKey;
        private string _returnSubtitleCodeIdKeys = "chunkId, codeId, chunkIndex, segmentId, id";
        private string _returnSubtitleTextKeys = "text, subtitle, subtitles, transcript, result, content";
        private string _returnSubtitleStartKeys = "start, start_time, startTime, from, begin";
        private string _returnSubtitleEndKeys = "end, end_time, endTime, to";
        private string _returnSubtitleListKeys = "segments, items, lines, subtitles, chunks, words, data";

        public string SubtitleSplitMode
        {
            get => _subtitleSplitMode;
            set { if (_subtitleSplitMode != value) { _subtitleSplitMode = value ?? "Duration"; OnPropertyChanged(); } }
        }

        public double SubtitleChunkDurationSec
        {
            get => _subtitleChunkDurationSec;
            set
            {
                var clamped = Math.Clamp(value, 5.0, 600.0);
                if (Math.Abs(_subtitleChunkDurationSec - clamped) > 0.01)
                {
                    _subtitleChunkDurationSec = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public int SubtitleChunkCount
        {
            get => _subtitleChunkCount;
            set
            {
                var clamped = Math.Clamp(value, 1, 100);
                if (_subtitleChunkCount != clamped)
                {
                    _subtitleChunkCount = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public bool SubtitleEnableSmartSilenceSplit
        {
            get => _subtitleEnableSmartSilenceSplit;
            set { if (_subtitleEnableSmartSilenceSplit != value) { _subtitleEnableSmartSilenceSplit = value; OnPropertyChanged(); } }
        }

        public double SubtitleSilenceThresholdDb
        {
            get => _subtitleSilenceThresholdDb;
            set
            {
                var clamped = Math.Clamp(value, -60.0, -10.0);
                if (Math.Abs(_subtitleSilenceThresholdDb - clamped) > 0.1)
                {
                    _subtitleSilenceThresholdDb = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public double SubtitleMinSilenceSec
        {
            get => _subtitleMinSilenceSec;
            set
            {
                var clamped = Math.Clamp(value, 0.1, 3.0);
                if (Math.Abs(_subtitleMinSilenceSec - clamped) > 0.01)
                {
                    _subtitleMinSilenceSec = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public double SubtitleMaxSearchWindowSec
        {
            get => _subtitleMaxSearchWindowSec;
            set
            {
                var clamped = Math.Clamp(value, 1.0, 20.0);
                if (Math.Abs(_subtitleMaxSearchWindowSec - clamped) > 0.1)
                {
                    _subtitleMaxSearchWindowSec = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public bool SubtitleUseEditedAudio
        {
            get => _subtitleUseEditedAudio;
            set { if (_subtitleUseEditedAudio != value) { _subtitleUseEditedAudio = value; OnPropertyChanged(); } }
        }

        public string SubtitleAudioExportFormat
        {
            get => _subtitleAudioExportFormat;
            set { if (_subtitleAudioExportFormat != value) { _subtitleAudioExportFormat = value ?? "mp3"; OnPropertyChanged(); } }
        }

        public string SubtitleAudioExportBitrate
        {
            get => _subtitleAudioExportBitrate;
            set { if (_subtitleAudioExportBitrate != value) { _subtitleAudioExportBitrate = value ?? "128k"; OnPropertyChanged(); } }
        }

        public bool SubtitleOutputBase64
        {
            get => _subtitleOutputBase64;
            set { if (_subtitleOutputBase64 != value) { _subtitleOutputBase64 = value; OnPropertyChanged(); } }
        }

        public string? ReturnSubtitleNodeId
        {
            get => _returnSubtitleNodeId;
            set { if (_returnSubtitleNodeId != value) { _returnSubtitleNodeId = value; OnPropertyChanged(); } }
        }

        public string? ReturnSubtitleOutputKey
        {
            get => _returnSubtitleOutputKey;
            set { if (_returnSubtitleOutputKey != value) { _returnSubtitleOutputKey = value; OnPropertyChanged(); } }
        }

        public string ReturnSubtitleCodeIdKeys
        {
            get => _returnSubtitleCodeIdKeys;
            set { if (_returnSubtitleCodeIdKeys != value) { _returnSubtitleCodeIdKeys = value ?? "chunkId, codeId, chunkIndex, segmentId, id"; OnPropertyChanged(); } }
        }

        public string ReturnSubtitleTextKeys
        {
            get => _returnSubtitleTextKeys;
            set { if (_returnSubtitleTextKeys != value) { _returnSubtitleTextKeys = value ?? "text, subtitle, subtitles, transcript, result, content"; OnPropertyChanged(); } }
        }

        public string ReturnSubtitleStartKeys
        {
            get => _returnSubtitleStartKeys;
            set { if (_returnSubtitleStartKeys != value) { _returnSubtitleStartKeys = value ?? "start, start_time, startTime, from, begin"; OnPropertyChanged(); } }
        }

        public string ReturnSubtitleEndKeys
        {
            get => _returnSubtitleEndKeys;
            set { if (_returnSubtitleEndKeys != value) { _returnSubtitleEndKeys = value ?? "end, end_time, endTime, to"; OnPropertyChanged(); } }
        }

        public string ReturnSubtitleListKeys
        {
            get => _returnSubtitleListKeys;
            set { if (_returnSubtitleListKeys != value) { _returnSubtitleListKeys = value ?? "segments, items, lines, subtitles, chunks, words, data"; OnPropertyChanged(); } }
        }

        // ═════════════════════════════════════════════════════════════════════
        // TAB: LỒNG TIẾNG (DUBBING / TTS WORKFLOW)
        // ═════════════════════════════════════════════════════════════════════
        private string _dubbingSplitMode = "SubtitleSegments"; // "SubtitleSegments", "Duration", "Full"
        private string _dubbingTargetVoice = string.Empty;
        private string _dubbingTargetLanguage = "vi";
        private double _dubbingSpeechRate = 1.0;
        private double _dubbingPitch;
        private bool _dubbingAutoDuckOriginalAudio = true;
        private double _dubbingOriginalAudioVolumePercent = 20.0;

        private string? _returnDubbingNodeId;
        private string? _returnDubbingOutputKey;
        private string _returnDubbingCodeIdKeys = "chunkId, codeId, chunkIndex, segmentId, id";
        private string _returnDubbingAudioLinkKeys = "audioUrl, linkAudio, audioPath, url, path, src, link";
        private string _returnDubbingAudioBase64Keys = "audioBase64, base64, audioData, data";
        private string _returnDubbingStartKeys = "start, start_time, startTime, from";
        private string _returnDubbingEndKeys = "duration, end, end_time, endTime, to";

        public string DubbingSplitMode
        {
            get => _dubbingSplitMode;
            set { if (_dubbingSplitMode != value) { _dubbingSplitMode = value ?? "SubtitleSegments"; OnPropertyChanged(); } }
        }

        public string DubbingTargetVoice
        {
            get => _dubbingTargetVoice;
            set { if (_dubbingTargetVoice != value) { _dubbingTargetVoice = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string DubbingTargetLanguage
        {
            get => _dubbingTargetLanguage;
            set { if (_dubbingTargetLanguage != value) { _dubbingTargetLanguage = value ?? "vi"; OnPropertyChanged(); } }
        }

        public double DubbingSpeechRate
        {
            get => _dubbingSpeechRate;
            set
            {
                var clamped = Math.Clamp(value, 0.25, 4.0);
                if (Math.Abs(_dubbingSpeechRate - clamped) > 0.01)
                {
                    _dubbingSpeechRate = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public double DubbingPitch
        {
            get => _dubbingPitch;
            set
            {
                var clamped = Math.Clamp(value, -12.0, 12.0);
                if (Math.Abs(_dubbingPitch - clamped) > 0.01)
                {
                    _dubbingPitch = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public bool DubbingAutoDuckOriginalAudio
        {
            get => _dubbingAutoDuckOriginalAudio;
            set { if (_dubbingAutoDuckOriginalAudio != value) { _dubbingAutoDuckOriginalAudio = value; OnPropertyChanged(); } }
        }

        public double DubbingOriginalAudioVolumePercent
        {
            get => _dubbingOriginalAudioVolumePercent;
            set
            {
                var clamped = Math.Clamp(value, 0.0, 100.0);
                if (Math.Abs(_dubbingOriginalAudioVolumePercent - clamped) > 0.1)
                {
                    _dubbingOriginalAudioVolumePercent = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public string? ReturnDubbingNodeId
        {
            get => _returnDubbingNodeId;
            set { if (_returnDubbingNodeId != value) { _returnDubbingNodeId = value; OnPropertyChanged(); } }
        }

        public string? ReturnDubbingOutputKey
        {
            get => _returnDubbingOutputKey;
            set { if (_returnDubbingOutputKey != value) { _returnDubbingOutputKey = value; OnPropertyChanged(); } }
        }

        public string ReturnDubbingCodeIdKeys
        {
            get => _returnDubbingCodeIdKeys;
            set { if (_returnDubbingCodeIdKeys != value) { _returnDubbingCodeIdKeys = value ?? "chunkId, codeId, chunkIndex, segmentId, id"; OnPropertyChanged(); } }
        }

        public string ReturnDubbingAudioLinkKeys
        {
            get => _returnDubbingAudioLinkKeys;
            set { if (_returnDubbingAudioLinkKeys != value) { _returnDubbingAudioLinkKeys = value ?? "audioUrl, linkAudio, audioPath, url, path, src, link"; OnPropertyChanged(); } }
        }

        public string ReturnDubbingAudioBase64Keys
        {
            get => _returnDubbingAudioBase64Keys;
            set { if (_returnDubbingAudioBase64Keys != value) { _returnDubbingAudioBase64Keys = value ?? "audioBase64, base64, audioData, data"; OnPropertyChanged(); } }
        }

        public string ReturnDubbingStartKeys
        {
            get => _returnDubbingStartKeys;
            set { if (_returnDubbingStartKeys != value) { _returnDubbingStartKeys = value ?? "start, start_time, startTime, from"; OnPropertyChanged(); } }
        }

        public string ReturnDubbingEndKeys
        {
            get => _returnDubbingEndKeys;
            set { if (_returnDubbingEndKeys != value) { _returnDubbingEndKeys = value ?? "duration, end, end_time, endTime, to"; OnPropertyChanged(); } }
        }
    }
}
