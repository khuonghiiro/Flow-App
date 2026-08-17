// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Models;
using FlowMy.Core.Models.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace FlowMy.Models.Nodes
{
    public enum AudioSyncMode
    {
        Loop = 0,
        PadSilence = 1,
        Stretch = 2,
        Trim = 3,
        Compress = 4
    }

    public sealed class VideoConcatItemConfig : INotifyPropertyChanged
    {
        private string _sourcePath = string.Empty;
        public string SourcePath
        {
            get => _sourcePath;
            set { if (_sourcePath != value) { _sourcePath = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public sealed class VideoAudioTrackConfig : INotifyPropertyChanged
    {
        private string _trackName = "Track";
        private bool _isMuted;
        private double _fadeInSec;
        private double _fadeOutSec;
        private string? _sourceNodeId;
        private string? _sourceOutputKey;
        private double _volumePercent = 100;
        private double _startAtSec;
        private double _trimStartSec;
        private double _trimEndSec;
        private AudioSyncMode _shorterMode = AudioSyncMode.Loop;
        private AudioSyncMode _longerMode = AudioSyncMode.Trim;

        public string TrackName
        {
            get => _trackName;
            set { if (_trackName != value) { _trackName = value ?? "Track"; OnPropertyChanged(); } }
        }

        public bool IsMuted
        {
            get => _isMuted;
            set { if (_isMuted != value) { _isMuted = value; OnPropertyChanged(); } }
        }

        public double FadeInSec
        {
            get => _fadeInSec;
            set { if (Math.Abs(_fadeInSec - value) > 0.01) { _fadeInSec = Math.Clamp(value, 0, 10); OnPropertyChanged(); } }
        }

        public double FadeOutSec
        {
            get => _fadeOutSec;
            set { if (Math.Abs(_fadeOutSec - value) > 0.01) { _fadeOutSec = Math.Clamp(value, 0, 10); OnPropertyChanged(); } }
        }

        public string? SourceNodeId
        {
            get => _sourceNodeId;
            set { if (_sourceNodeId != value) { _sourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? SourceOutputKey
        {
            get => _sourceOutputKey;
            set { if (_sourceOutputKey != value) { _sourceOutputKey = value; OnPropertyChanged(); } }
        }

        public double VolumePercent
        {
            get => _volumePercent;
            set
            {
                var clamped = value < 0 ? 0 : (value > 300 ? 300 : value);
                if (Math.Abs(_volumePercent - clamped) > 0.01)
                {
                    _volumePercent = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public double StartAtSec
        {
            get => _startAtSec;
            set
            {
                var clamped = value < 0 ? 0 : value;
                if (Math.Abs(_startAtSec - clamped) > 0.001)
                {
                    _startAtSec = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public double TrimStartSec
        {
            get => _trimStartSec;
            set
            {
                var clamped = value < 0 ? 0 : value;
                if (Math.Abs(_trimStartSec - clamped) > 0.001)
                {
                    _trimStartSec = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public double TrimEndSec
        {
            get => _trimEndSec;
            set
            {
                var clamped = value < 0 ? 0 : value;
                if (Math.Abs(_trimEndSec - clamped) > 0.001)
                {
                    _trimEndSec = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public AudioSyncMode ShorterMode
        {
            get => _shorterMode;
            set { if (_shorterMode != value) { _shorterMode = value; OnPropertyChanged(); } }
        }

        public AudioSyncMode LongerMode
        {
            get => _longerMode;
            set { if (_longerMode != value) { _longerMode = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed partial class VideoProcessingNode : WorkflowNode
    {
        private double _width = 1360;
        private double _height = 768;

        private string? _videoSourceNodeId;
        private string? _videoSourceOutputKey;
        private string _videoPath = string.Empty;
        private string? _outputFolderSourceNodeId;
        private string? _outputFolderSourceOutputKey;
        private string? _videoOutputFolderSourceNodeId;
        private string? _videoOutputFolderSourceOutputKey;
        private string? _audioOutputFolderSourceNodeId;
        private string? _audioOutputFolderSourceOutputKey;
        private bool _outputBase64 = true;
        private bool _useDialogVideoConfig = true;
        private bool _extractFramesEnabled = true;
        private bool _exportVideoEnabled = true;
        private bool _extractAudioEnabled;
        private string? _frameOutputFolderPath;
        private string? _defaultOutputVideoPath;
        private string? _audioOutputFolderPath;
        private double _secondsPerFrame = 1;
        private int _extractFrameCount = 10;

        private bool _preferGpu = true;
        private string _preferredHwAccel = "cuda";
        private double _sourceFps = 30;
        private double _extractFps = 1;
        private double _brightness;
        private double _contrast = 1;
        private double _saturation = 1;
        private double _hue;
        private double _gamma = 1;

        private bool _sharpenEnabled;
        private double _sharpenStrength = 1;
        private bool _denoiseEnabled;
        private double _denoiseStrength = 3;
        private bool _blurEnabled;
        private double _blurRadius = 3;
        private bool _stabilizeEnabled;
        private double _speedFactor = 1;
        private double _rotationDegrees;
        private bool _flipH;
        private bool _flipV;

        private string _outputFormat = "mp4_h264";
        private string _encoderPreset = "medium";
        private double _crf = 23;
        private double _resolutionScale = 1;
        private double _frameResizeScale = 1.0;
        private int? _fixedResolutionHeight;
        private bool _trimEnabled;
        private bool _concatEnabled;
        private double _trimStartSec;
        private double _trimEndSec;
        private string? _outputPathOverride;
        private bool _sourceAudioEnabled = true;
        private double _sourceAudioVolumePercent = 100.0;
        private double _audioFadeInSec;
        private double _audioFadeOutSec;
        private bool _audioNormalizeEnabled;
        private bool _audioDenoiseEnabled;
        private double _previewVolume = 0.7;
        private string _previewQualityMode = "normal";
        private string _previewVisualStrengthMode = "balanced";
        private bool _watermarkEnabled;
        private string? _watermarkImagePath;
        private string _watermarkPosition = "BR";
        private double _watermarkOpacity = 1.0;
        private int _watermarkPaddingPx = 10;
        private double _watermarkWidthFraction = 0.20;
        private double _watermarkInsetFraction = 0.05;
        private bool _textOverlayEnabled;
        private string _overlayText = string.Empty;
        private string _overlayFont = "Arial";
        private int _overlayFontSize = 32;
        private string _overlayFontColor = "white";
        private string _textPosition = "BC";
        private bool _frameLabelEnabled;
        private bool _frameLabelDebugSamplesEnabled;
        private string _frameLabelTemplate = "Frame {index} - {time}";
        private string _frameLabelPosition = "TL";
        private string _frameLabelTextColor = "black";
        private string _frameLabelBackgroundColor = "white";
        private int _frameLabelFontSize = 18;
        private double _frameLabelX;
        private double _frameLabelY;
        private double _frameLabelW = 0.20;
        private double _frameLabelH = 0.05;
        private int _frameLabelHorizontalPadding = 10;
        private int _frameLabelVerticalPadding = 6;
        private int _frameLabelPaddingLeft = 10;
        private int _frameLabelPaddingTop = 6;
        private int _frameLabelPaddingRight = 10;
        private int _frameLabelPaddingBottom = 6;
        private string _frameLabelTimeFormat = "HHMMSS";
        private int _extractParallelJobs = 1;
        private string _frameOutputFormat = "png";
        private int _jpegQuality = 90;
        private bool _extractAllFrames;
        private bool _twoPassEnabled;
        private string _audioCodec = "aac";
        private string _audioBitrate = "192k";
        private string? _subtitlePath;
        private bool _burnSubtitleEnabled;
        private bool _gridCollageEnabled;
        private int _gridCollageWidth = 1000;
        private int _gridCollageHeight = 1000;
        private int _gridCollageFrameCount = 4;
        private string _gridCollageBackgroundColor = "white";
        private string _gridCollageColorKey = "white";
        private string _gridCollagePadding = "10";
        private string _gridCollageMargin = "0";
        private string _gridCollageAspectMode = "auto";
        private bool _gridCollageShowFrameIndex;
        private bool _extractByFpsEnabled = true;
        private List<double> _excludedFrameTimestamps = new();
        private double _audioSpeedFactor = 1.0;
        private string _audioEqPreset = "neutral";
        private double _audioBassGain;
        private double _audioLowMidGain;
        private double _audioMidGain;
        private double _audioHighMidGain;
        private double _audioTrebleGain;
        private double _audioToneClarity;
        private bool _audioHighpassFilter;
        private double _audioHighpassCutoffHz = 80.0;
        private bool _audioLowpassFilter;
        private double _audioLowpassCutoffHz = 12000.0;
        private double _audioStereoWidthPercent = 100.0;
        private double _audioWarmthPercent;
        private double _audioReverbPercent;
        private double _audioVocalBalance;
        private double _audioPitchSemitones;
        private bool _audioEchoEnabled;
        private double _audioEchoDelayMs = 250.0;
        private double _audioEchoFeedbackPercent = 40.0;
        private double _audioEchoMixPercent = 30.0;
        private bool _audio8DEnabled;
        private double _audio8DSpeedHz = 0.125;
        private bool _audioRobotVoiceEnabled;
        private bool _audioRadioVoiceEnabled;
        private bool _audioChorusEnabled;
        private double _audioChorusMixPercent = 40.0;
        private double _audioCompressorPercent;
        private double _audioDeEsserPercent;
        private double _audioNoiseGatePercent;
        private double _audioTargetLufs = -14.0;
        private bool _audioWaveShaperEnabled;
        private string _audioWaveShaperCurve = "clean";
        private double _audioWaveShaperDrivePercent;
        private double _audioTransientPunchPercent;
        private double _audioSubHarmonicsPercent;
        private double _audioHarmonicExciterPercent;
        private bool _audioPhaseInvertLeft;
        private bool _audioPhaseInvertRight;
        private bool _audioTrimEnabled;
        private double _audioTrimStartSec;
        private double _audioTrimEndSec;
        private string _audioExportFormat = "mp3";
        private string _audioExportBitrate = "320k";
        private string _audioExportSampleRate = "48000";
        private string _audioExportChannels = "stereo";

        public ObservableCollection<SubtitleItem> Subtitles { get; set; }
        public SubtitleStyleConfig SubtitleStyle { get; set; }
        public ObservableCollection<DubbingClipItem> DubbingClips { get; set; }
        public AutoDuckingConfig AutoDucking { get; set; }

        public VideoProcessingNode()
        {
            Type = NodeType.VideoProcessing;
            IconSize = 32;
            Title = "Video Processing";
            AudioTracks = new ObservableCollection<VideoAudioTrackConfig>();
            ConcatVideos = new ObservableCollection<VideoConcatItemConfig>();
            Overlays = new ObservableCollection<OverlayItem>();
            Subtitles = new ObservableCollection<SubtitleItem>();
            SubtitleStyle = new SubtitleStyleConfig();
            DubbingClips = new ObservableCollection<DubbingClipItem>();
            AutoDucking = new AutoDuckingConfig();
            EnsureStandardDynamicOutputs();
        }

        public new TextBlock? TitleTextBlockUI { get; set; }

        public double Width
        {
            get => _width;
            set
            {
                var clamped = value < 540 ? 540 : value;
                if (Math.Abs(_width - clamped) > 0.01) { _width = clamped; OnPropertyChanged(); }
            }
        }

        public double Height
        {
            get => _height;
            set
            {
                var clamped = value < 340 ? 340 : value;
                if (Math.Abs(_height - clamped) > 0.01) { _height = clamped; OnPropertyChanged(); }
            }
        }

        public string? VideoSourceNodeId
        {
            get => _videoSourceNodeId;
            set { if (_videoSourceNodeId != value) { _videoSourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? VideoSourceOutputKey
        {
            get => _videoSourceOutputKey;
            set { if (_videoSourceOutputKey != value) { _videoSourceOutputKey = value; OnPropertyChanged(); } }
        }

        public string VideoPath
        {
            get => _videoPath;
            set
            {
                var next = value ?? string.Empty;
                if (_videoPath != next) { _videoPath = next; OnPropertyChanged(); }
            }
        }

        public string? OutputFolderSourceNodeId
        {
            get => _outputFolderSourceNodeId;
            set { if (_outputFolderSourceNodeId != value) { _outputFolderSourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? OutputFolderSourceOutputKey
        {
            get => _outputFolderSourceOutputKey;
            set { if (_outputFolderSourceOutputKey != value) { _outputFolderSourceOutputKey = value; OnPropertyChanged(); } }
        }

        public string? VideoOutputFolderSourceNodeId
        {
            get => _videoOutputFolderSourceNodeId;
            set
            {
                if (_videoOutputFolderSourceNodeId != value)
                {
                    _videoOutputFolderSourceNodeId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? VideoOutputFolderSourceOutputKey
        {
            get => _videoOutputFolderSourceOutputKey;
            set
            {
                if (_videoOutputFolderSourceOutputKey != value)
                {
                    _videoOutputFolderSourceOutputKey = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? AudioOutputFolderSourceNodeId
        {
            get => _audioOutputFolderSourceNodeId;
            set
            {
                if (_audioOutputFolderSourceNodeId != value)
                {
                    _audioOutputFolderSourceNodeId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? AudioOutputFolderSourceOutputKey
        {
            get => _audioOutputFolderSourceOutputKey;
            set
            {
                if (_audioOutputFolderSourceOutputKey != value)
                {
                    _audioOutputFolderSourceOutputKey = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool OutputBase64
        {
            get => _outputBase64;
            set { if (_outputBase64 != value) { _outputBase64 = value; OnPropertyChanged(); } }
        }

        public bool UseDialogVideoConfig
        {
            get => _useDialogVideoConfig;
            set { if (_useDialogVideoConfig != value) { _useDialogVideoConfig = value; OnPropertyChanged(); } }
        }

        public bool ExtractFramesEnabled
        {
            get => _extractFramesEnabled;
            set { if (_extractFramesEnabled != value) { _extractFramesEnabled = value; OnPropertyChanged(); } }
        }

        public bool ExportVideoEnabled
        {
            get => _exportVideoEnabled;
            set { if (_exportVideoEnabled != value) { _exportVideoEnabled = value; OnPropertyChanged(); } }
        }

        public bool ExtractAudioEnabled
        {
            get => _extractAudioEnabled;
            set { if (_extractAudioEnabled != value) { _extractAudioEnabled = value; OnPropertyChanged(); } }
        }

        public string? FrameOutputFolderPath
        {
            get => _frameOutputFolderPath;
            set { if (_frameOutputFolderPath != value) { _frameOutputFolderPath = value; OnPropertyChanged(); } }
        }

        public string? DefaultOutputVideoPath
        {
            get => _defaultOutputVideoPath;
            set { if (_defaultOutputVideoPath != value) { _defaultOutputVideoPath = value; OnPropertyChanged(); } }
        }

        public string? AudioOutputFolderPath
        {
            get => _audioOutputFolderPath;
            set { if (_audioOutputFolderPath != value) { _audioOutputFolderPath = value; OnPropertyChanged(); } }
        }

        public double SecondsPerFrame
        {
            get => _secondsPerFrame;
            set
            {
                var next = value < 0.1 ? 0.1 : (value > 60 ? 60 : value);
                if (Math.Abs(_secondsPerFrame - next) > 0.001) { _secondsPerFrame = next; OnPropertyChanged(); }
            }
        }

        public int ExtractFrameCount
        {
            get => _extractFrameCount;
            set
            {
                var next = value < 1 ? 1 : value;
                if (_extractFrameCount != next) { _extractFrameCount = next; OnPropertyChanged(); }
            }
        }

        public bool PreferGpu
        {
            get => _preferGpu;
            set { if (_preferGpu != value) { _preferGpu = value; OnPropertyChanged(); } }
        }

        public string PreferredHwAccel
        {
            get => _preferredHwAccel;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "cuda" : value.Trim();
                if (_preferredHwAccel != next) { _preferredHwAccel = next; OnPropertyChanged(); }
            }
        }

        public double SourceFps
        {
            get => _sourceFps;
            set
            {
                var next = value < 1 ? 1 : value;
                if (Math.Abs(_sourceFps - next) > 0.01) { _sourceFps = next; OnPropertyChanged(); }
                if (_extractFps > next) ExtractFps = next;
            }
        }

        public double ExtractFps
        {
            get => _extractFps;
            set
            {
                // Allows fractional FPS (e.g. 1 frame / 3 seconds => ~0.333 fps).
                // Minimum is clamped to a tiny positive number to keep ffmpeg filter arguments valid.
                var next = value <= 0 ? 0.001 : value;

                var max = SourceFps > 0 ? SourceFps : double.PositiveInfinity;
                if (!double.IsInfinity(max) && next > max) next = max;

                if (Math.Abs(_extractFps - next) > 0.001)
                {
                    _extractFps = next;
                    OnPropertyChanged();
                }
            }
        }

        public double Brightness
        {
            get => _brightness;
            set
            {
                var next = value < -1 ? -1 : (value > 1 ? 1 : value);
                if (Math.Abs(_brightness - next) > 0.001) { _brightness = next; OnPropertyChanged(); }
            }
        }

        public double Contrast
        {
            get => _contrast;
            set
            {
                var next = value < 0.1 ? 0.1 : (value > 3 ? 3 : value);
                if (Math.Abs(_contrast - next) > 0.001) { _contrast = next; OnPropertyChanged(); }
            }
        }

        public double Saturation
        {
            get => _saturation;
            set
            {
                var next = value < 0 ? 0 : (value > 3 ? 3 : value);
                if (Math.Abs(_saturation - next) > 0.001) { _saturation = next; OnPropertyChanged(); }
            }
        }

        public double Hue
        {
            get => _hue;
            set
            {
                var next = value < -180 ? -180 : (value > 180 ? 180 : value);
                if (Math.Abs(_hue - next) > 0.001) { _hue = next; OnPropertyChanged(); }
            }
        }

        public double Gamma
        {
            get => _gamma;
            set
            {
                var next = value < 0.1 ? 0.1 : (value > 3 ? 3 : value);
                if (Math.Abs(_gamma - next) > 0.001) { _gamma = next; OnPropertyChanged(); }
            }
        }

        public bool SharpenEnabled
        {
            get => _sharpenEnabled;
            set { if (_sharpenEnabled != value) { _sharpenEnabled = value; OnPropertyChanged(); } }
        }

        public double SharpenStrength
        {
            get => _sharpenStrength;
            set
            {
                var next = value < 0 ? 0 : (value > 5 ? 5 : value);
                if (Math.Abs(_sharpenStrength - next) > 0.001) { _sharpenStrength = next; OnPropertyChanged(); }
            }
        }

        public bool DenoiseEnabled
        {
            get => _denoiseEnabled;
            set { if (_denoiseEnabled != value) { _denoiseEnabled = value; OnPropertyChanged(); } }
        }

        public double DenoiseStrength
        {
            get => _denoiseStrength;
            set
            {
                var next = value < 0 ? 0 : (value > 10 ? 10 : value);
                if (Math.Abs(_denoiseStrength - next) > 0.001) { _denoiseStrength = next; OnPropertyChanged(); }
            }
        }

        public bool BlurEnabled
        {
            get => _blurEnabled;
            set { if (_blurEnabled != value) { _blurEnabled = value; OnPropertyChanged(); } }
        }

        public double BlurRadius
        {
            get => _blurRadius;
            set
            {
                var next = value < 0 ? 0 : (value > 15 ? 15 : value);
                if (Math.Abs(_blurRadius - next) > 0.001) { _blurRadius = next; OnPropertyChanged(); }
            }
        }

        public bool StabilizeEnabled
        {
            get => _stabilizeEnabled;
            set { if (_stabilizeEnabled != value) { _stabilizeEnabled = value; OnPropertyChanged(); } }
        }

        public double SpeedFactor
        {
            get => _speedFactor;
            set
            {
                var next = value < 0.25 ? 0.25 : (value > 4 ? 4 : value);
                if (Math.Abs(_speedFactor - next) > 0.001) { _speedFactor = next; OnPropertyChanged(); }
            }
        }

        public double RotationDegrees
        {
            get => _rotationDegrees;
            set
            {
                var next = value < 0 ? 0 : (value > 270 ? 270 : value);
                if (Math.Abs(_rotationDegrees - next) > 0.001) { _rotationDegrees = next; OnPropertyChanged(); }
            }
        }

        public bool FlipH
        {
            get => _flipH;
            set { if (_flipH != value) { _flipH = value; OnPropertyChanged(); } }
        }

        public bool FlipV
        {
            get => _flipV;
            set { if (_flipV != value) { _flipV = value; OnPropertyChanged(); } }
        }

        public string OutputFormat
        {
            get => _outputFormat;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "mp4_h264" : value.Trim();
                if (_outputFormat != next) { _outputFormat = next; OnPropertyChanged(); }
            }
        }

        public string EncoderPreset
        {
            get => _encoderPreset;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "medium" : value.Trim();
                if (_encoderPreset != next) { _encoderPreset = next; OnPropertyChanged(); }
            }
        }

        public double Crf
        {
            get => _crf;
            set
            {
                var next = value < 0 ? 0 : (value > 51 ? 51 : value);
                if (Math.Abs(_crf - next) > 0.001) { _crf = next; OnPropertyChanged(); }
            }
        }

        public double ResolutionScale
        {
            get => _resolutionScale;
            set
            {
                var next = value < 0.1 ? 0.1 : (value > 1 ? 1 : value);
                if (Math.Abs(_resolutionScale - next) > 0.001) { _resolutionScale = next; OnPropertyChanged(); }
            }
        }

        public double FrameResizeScale
        {
            get => _frameResizeScale;
            set
            {
                // UI slider: 0.4 - 1.0. Keep a narrow range to avoid unexpected scaling.
                var next = value < 0.4 ? 0.4 : (value > 1.0 ? 1.0 : value);
                if (Math.Abs(_frameResizeScale - next) > 0.001)
                {
                    _frameResizeScale = next;
                    OnPropertyChanged();
                }
            }
        }

        public int? FixedResolutionHeight
        {
            get => _fixedResolutionHeight;
            set
            {
                int? next = value is null ? (int?)null : Math.Max(144, value.Value);
                if (_fixedResolutionHeight != next) { _fixedResolutionHeight = next; OnPropertyChanged(); }
            }
        }

        public bool TrimEnabled
        {
            get => _trimEnabled;
            set { if (_trimEnabled != value) { _trimEnabled = value; OnPropertyChanged(); } }
        }

        public double TrimStartSec
        {
            get => _trimStartSec;
            set
            {
                var next = value < 0 ? 0 : value;
                if (Math.Abs(_trimStartSec - next) > 0.001) { _trimStartSec = next; OnPropertyChanged(); }
            }
        }

        public double TrimEndSec
        {
            get => _trimEndSec;
            set
            {
                var next = value < 0 ? 0 : value;
                if (Math.Abs(_trimEndSec - next) > 0.001) { _trimEndSec = next; OnPropertyChanged(); }
            }
        }

        public string? OutputPathOverride
        {
            get => _outputPathOverride;
            set { if (_outputPathOverride != value) { _outputPathOverride = value; OnPropertyChanged(); } }
        }

        public bool SourceAudioEnabled
        {
            get => _sourceAudioEnabled;
            set { if (_sourceAudioEnabled != value) { _sourceAudioEnabled = value; OnPropertyChanged(); } }
        }

        public double SourceAudioVolumePercent
        {
            get => _sourceAudioVolumePercent;
            set { if (Math.Abs(_sourceAudioVolumePercent - value) > 0.01) { _sourceAudioVolumePercent = Math.Clamp(value, 0, 300); OnPropertyChanged(); } }
        }

        public double AudioFadeInSec
        {
            get => _audioFadeInSec;
            set { if (Math.Abs(_audioFadeInSec - value) > 0.01) { _audioFadeInSec = Math.Clamp(value, 0, 10); OnPropertyChanged(); } }
        }

        public double AudioFadeOutSec
        {
            get => _audioFadeOutSec;
            set { if (Math.Abs(_audioFadeOutSec - value) > 0.01) { _audioFadeOutSec = Math.Clamp(value, 0, 10); OnPropertyChanged(); } }
        }

        public bool AudioNormalizeEnabled
        {
            get => _audioNormalizeEnabled;
            set { if (_audioNormalizeEnabled != value) { _audioNormalizeEnabled = value; OnPropertyChanged(); } }
        }

        public bool AudioDenoiseEnabled
        {
            get => _audioDenoiseEnabled;
            set { if (_audioDenoiseEnabled != value) { _audioDenoiseEnabled = value; OnPropertyChanged(); } }
        }

        public double AudioSpeedFactor
        {
            get => _audioSpeedFactor;
            set { if (Math.Abs(_audioSpeedFactor - value) > 0.01) { _audioSpeedFactor = Math.Clamp(value, 0.25, 4.0); OnPropertyChanged(); } }
        }

        public string AudioEqPreset
        {
            get => _audioEqPreset;
            set { if (_audioEqPreset != value) { _audioEqPreset = value ?? "neutral"; OnPropertyChanged(); } }
        }

        public double AudioBassGain
        {
            get => _audioBassGain;
            set { if (Math.Abs(_audioBassGain - value) > 0.01) { _audioBassGain = Math.Clamp(value, -20.0, 20.0); OnPropertyChanged(); } }
        }

        public double AudioMidGain
        {
            get => _audioMidGain;
            set { if (Math.Abs(_audioMidGain - value) > 0.01) { _audioMidGain = Math.Clamp(value, -20.0, 20.0); OnPropertyChanged(); } }
        }

        public double AudioLowMidGain
        {
            get => _audioLowMidGain;
            set { if (Math.Abs(_audioLowMidGain - value) > 0.01) { _audioLowMidGain = Math.Clamp(value, -20.0, 20.0); OnPropertyChanged(); } }
        }

        public double AudioHighMidGain
        {
            get => _audioHighMidGain;
            set { if (Math.Abs(_audioHighMidGain - value) > 0.01) { _audioHighMidGain = Math.Clamp(value, -20.0, 20.0); OnPropertyChanged(); } }
        }

        public double AudioTrebleGain
        {
            get => _audioTrebleGain;
            set { if (Math.Abs(_audioTrebleGain - value) > 0.01) { _audioTrebleGain = Math.Clamp(value, -20.0, 20.0); OnPropertyChanged(); } }
        }

        public double AudioToneClarity
        {
            get => _audioToneClarity;
            set { if (Math.Abs(_audioToneClarity - value) > 0.1) { _audioToneClarity = Math.Clamp(value, -100.0, 100.0); OnPropertyChanged(); } }
        }

        public bool AudioHighpassFilter
        {
            get => _audioHighpassFilter;
            set { if (_audioHighpassFilter != value) { _audioHighpassFilter = value; OnPropertyChanged(); } }
        }

        public double AudioHighpassCutoffHz
        {
            get => _audioHighpassCutoffHz;
            set { if (Math.Abs(_audioHighpassCutoffHz - value) > 0.1) { _audioHighpassCutoffHz = Math.Clamp(value, 20.0, 500.0); OnPropertyChanged(); } }
        }

        public bool AudioLowpassFilter
        {
            get => _audioLowpassFilter;
            set { if (_audioLowpassFilter != value) { _audioLowpassFilter = value; OnPropertyChanged(); } }
        }

        public double AudioLowpassCutoffHz
        {
            get => _audioLowpassCutoffHz;
            set { if (Math.Abs(_audioLowpassCutoffHz - value) > 1.0) { _audioLowpassCutoffHz = Math.Clamp(value, 2000.0, 20000.0); OnPropertyChanged(); } }
        }

        public double AudioStereoWidthPercent
        {
            get => _audioStereoWidthPercent;
            set { if (Math.Abs(_audioStereoWidthPercent - value) > 0.1) { _audioStereoWidthPercent = Math.Clamp(value, 0.0, 200.0); OnPropertyChanged(); } }
        }

        public double AudioWarmthPercent
        {
            get => _audioWarmthPercent;
            set { if (Math.Abs(_audioWarmthPercent - value) > 0.1) { _audioWarmthPercent = Math.Clamp(value, 0.0, 100.0); OnPropertyChanged(); } }
        }

        public double AudioReverbPercent
        {
            get => _audioReverbPercent;
            set { if (Math.Abs(_audioReverbPercent - value) > 0.1) { _audioReverbPercent = Math.Clamp(value, 0.0, 100.0); OnPropertyChanged(); } }
        }

        public double AudioVocalBalance
        {
            get => _audioVocalBalance;
            set { if (Math.Abs(_audioVocalBalance - value) > 0.1) { _audioVocalBalance = Math.Clamp(value, -100.0, 100.0); OnPropertyChanged(); } }
        }

        public double AudioPitchSemitones
        {
            get => _audioPitchSemitones;
            set { if (Math.Abs(_audioPitchSemitones - value) > 0.1) { _audioPitchSemitones = Math.Clamp(value, -12.0, 12.0); OnPropertyChanged(); } }
        }

        public bool AudioEchoEnabled
        {
            get => _audioEchoEnabled;
            set { if (_audioEchoEnabled != value) { _audioEchoEnabled = value; OnPropertyChanged(); } }
        }

        public double AudioEchoDelayMs
        {
            get => _audioEchoDelayMs;
            set { if (Math.Abs(_audioEchoDelayMs - value) > 1.0) { _audioEchoDelayMs = Math.Clamp(value, 50.0, 1000.0); OnPropertyChanged(); } }
        }

        public double AudioEchoFeedbackPercent
        {
            get => _audioEchoFeedbackPercent;
            set { if (Math.Abs(_audioEchoFeedbackPercent - value) > 0.1) { _audioEchoFeedbackPercent = Math.Clamp(value, 0.0, 90.0); OnPropertyChanged(); } }
        }

        public double AudioEchoMixPercent
        {
            get => _audioEchoMixPercent;
            set { if (Math.Abs(_audioEchoMixPercent - value) > 0.1) { _audioEchoMixPercent = Math.Clamp(value, 0.0, 100.0); OnPropertyChanged(); } }
        }

        public bool Audio8DEnabled
        {
            get => _audio8DEnabled;
            set { if (_audio8DEnabled != value) { _audio8DEnabled = value; OnPropertyChanged(); } }
        }

        public double Audio8DSpeedHz
        {
            get => _audio8DSpeedHz;
            set { if (Math.Abs(_audio8DSpeedHz - value) > 0.001) { _audio8DSpeedHz = Math.Clamp(value, 0.05, 0.5); OnPropertyChanged(); } }
        }

        public bool AudioRobotVoiceEnabled
        {
            get => _audioRobotVoiceEnabled;
            set { if (_audioRobotVoiceEnabled != value) { _audioRobotVoiceEnabled = value; OnPropertyChanged(); } }
        }

        public bool AudioRadioVoiceEnabled
        {
            get => _audioRadioVoiceEnabled;
            set { if (_audioRadioVoiceEnabled != value) { _audioRadioVoiceEnabled = value; OnPropertyChanged(); } }
        }

        public bool AudioChorusEnabled
        {
            get => _audioChorusEnabled;
            set { if (_audioChorusEnabled != value) { _audioChorusEnabled = value; OnPropertyChanged(); } }
        }

        public double AudioChorusMixPercent
        {
            get => _audioChorusMixPercent;
            set { if (Math.Abs(_audioChorusMixPercent - value) > 0.1) { _audioChorusMixPercent = Math.Clamp(value, 0.0, 100.0); OnPropertyChanged(); } }
        }

        public double AudioCompressorPercent
        {
            get => _audioCompressorPercent;
            set { if (Math.Abs(_audioCompressorPercent - value) > 0.1) { _audioCompressorPercent = Math.Clamp(value, 0.0, 100.0); OnPropertyChanged(); } }
        }

        public double AudioDeEsserPercent
        {
            get => _audioDeEsserPercent;
            set { if (Math.Abs(_audioDeEsserPercent - value) > 0.1) { _audioDeEsserPercent = Math.Clamp(value, 0.0, 100.0); OnPropertyChanged(); } }
        }

        public double AudioNoiseGatePercent
        {
            get => _audioNoiseGatePercent;
            set { if (Math.Abs(_audioNoiseGatePercent - value) > 0.1) { _audioNoiseGatePercent = Math.Clamp(value, 0.0, 100.0); OnPropertyChanged(); } }
        }

        public double AudioTargetLufs
        {
            get => _audioTargetLufs;
            set { if (Math.Abs(_audioTargetLufs - value) > 0.01) { _audioTargetLufs = Math.Clamp(value, -30.0, -6.0); OnPropertyChanged(); } }
        }

        public bool AudioWaveShaperEnabled
        {
            get => _audioWaveShaperEnabled;
            set { if (_audioWaveShaperEnabled != value) { _audioWaveShaperEnabled = value; OnPropertyChanged(); } }
        }

        public string AudioWaveShaperCurve
        {
            get => _audioWaveShaperCurve;
            set { if (_audioWaveShaperCurve != value) { _audioWaveShaperCurve = value ?? "clean"; OnPropertyChanged(); } }
        }

        public double AudioWaveShaperDrivePercent
        {
            get => _audioWaveShaperDrivePercent;
            set { if (Math.Abs(_audioWaveShaperDrivePercent - value) > 0.1) { _audioWaveShaperDrivePercent = Math.Clamp(value, 0.0, 200.0); OnPropertyChanged(); } }
        }

        public double AudioTransientPunchPercent
        {
            get => _audioTransientPunchPercent;
            set { if (Math.Abs(_audioTransientPunchPercent - value) > 0.1) { _audioTransientPunchPercent = Math.Clamp(value, -100.0, 100.0); OnPropertyChanged(); } }
        }

        public double AudioSubHarmonicsPercent
        {
            get => _audioSubHarmonicsPercent;
            set { if (Math.Abs(_audioSubHarmonicsPercent - value) > 0.1) { _audioSubHarmonicsPercent = Math.Clamp(value, 0.0, 100.0); OnPropertyChanged(); } }
        }

        public double AudioHarmonicExciterPercent
        {
            get => _audioHarmonicExciterPercent;
            set { if (Math.Abs(_audioHarmonicExciterPercent - value) > 0.1) { _audioHarmonicExciterPercent = Math.Clamp(value, 0.0, 100.0); OnPropertyChanged(); } }
        }

        public bool AudioPhaseInvertLeft
        {
            get => _audioPhaseInvertLeft;
            set { if (_audioPhaseInvertLeft != value) { _audioPhaseInvertLeft = value; OnPropertyChanged(); } }
        }

        public bool AudioPhaseInvertRight
        {
            get => _audioPhaseInvertRight;
            set { if (_audioPhaseInvertRight != value) { _audioPhaseInvertRight = value; OnPropertyChanged(); } }
        }

        public bool AudioTrimEnabled
        {
            get => _audioTrimEnabled;
            set { if (_audioTrimEnabled != value) { _audioTrimEnabled = value; OnPropertyChanged(); } }
        }

        public double AudioTrimStartSec
        {
            get => _audioTrimStartSec;
            set { if (Math.Abs(_audioTrimStartSec - value) > 0.001) { _audioTrimStartSec = Math.Max(0, value); OnPropertyChanged(); } }
        }

        public double AudioTrimEndSec
        {
            get => _audioTrimEndSec;
            set { if (Math.Abs(_audioTrimEndSec - value) > 0.001) { _audioTrimEndSec = Math.Max(0, value); OnPropertyChanged(); } }
        }

        public string AudioExportFormat
        {
            get => _audioExportFormat;
            set { if (_audioExportFormat != value) { _audioExportFormat = value ?? "mp3"; OnPropertyChanged(); } }
        }

        public string AudioExportBitrate
        {
            get => _audioExportBitrate;
            set { if (_audioExportBitrate != value) { _audioExportBitrate = value ?? "320k"; OnPropertyChanged(); } }
        }

        public string AudioExportSampleRate
        {
            get => _audioExportSampleRate;
            set { if (_audioExportSampleRate != value) { _audioExportSampleRate = value ?? "48000"; OnPropertyChanged(); } }
        }

        public string AudioExportChannels
        {
            get => _audioExportChannels;
            set { if (_audioExportChannels != value) { _audioExportChannels = value ?? "stereo"; OnPropertyChanged(); } }
        }

        public double PreviewVolume
        {
            get => _previewVolume;
            set
            {
                var next = value < 0 ? 0 : (value > 1 ? 1 : value);
                if (Math.Abs(_previewVolume - next) > 0.001) { _previewVolume = next; OnPropertyChanged(); }
            }
        }

        public string PreviewQualityMode
        {
            get => _previewQualityMode;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? "normal" : value.Trim().ToLowerInvariant();
                if (normalized != "low" && normalized != "normal" && normalized != "high" && normalized != "auto")
                {
                    normalized = "normal";
                }

                if (_previewQualityMode != normalized)
                {
                    _previewQualityMode = normalized;
                    OnPropertyChanged();
                }
            }
        }

        public string PreviewVisualStrengthMode
        {
            get => _previewVisualStrengthMode;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? "balanced" : value.Trim().ToLowerInvariant();
                if (normalized != "fast" && normalized != "balanced" && normalized != "strong")
                {
                    normalized = "balanced";
                }

                if (_previewVisualStrengthMode != normalized)
                {
                    _previewVisualStrengthMode = normalized;
                    OnPropertyChanged();
                }
            }
        }

        public bool WatermarkEnabled
        {
            get => _watermarkEnabled;
            set { if (_watermarkEnabled != value) { _watermarkEnabled = value; OnPropertyChanged(); } }
        }

        public string? WatermarkImagePath
        {
            get => _watermarkImagePath;
            set { if (_watermarkImagePath != value) { _watermarkImagePath = value; OnPropertyChanged(); } }
        }

        public string WatermarkPosition
        {
            get => _watermarkPosition;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "BR" : value.Trim().ToUpperInvariant();
                if (_watermarkPosition != next) { _watermarkPosition = next; OnPropertyChanged(); }
            }
        }

        public double WatermarkOpacity
        {
            get => _watermarkOpacity;
            set
            {
                var next = value < 0 ? 0 : (value > 1 ? 1 : value);
                if (Math.Abs(_watermarkOpacity - next) > 0.001) { _watermarkOpacity = next; OnPropertyChanged(); }
            }
        }

        public int WatermarkPaddingPx
        {
            get => _watermarkPaddingPx;
            set
            {
                var next = value < 0 ? 0 : value;
                if (_watermarkPaddingPx != next) { _watermarkPaddingPx = next; OnPropertyChanged(); }
            }
        }

        /// <summary>Watermark draw width as a fraction of the source frame width (e.g. 0.2 = 20%).</summary>
        public double WatermarkWidthFraction
        {
            get => _watermarkWidthFraction;
            set
            {
                var next = value < 0.05 ? 0.05 : (value > 0.90 ? 0.90 : value);
                if (Math.Abs(_watermarkWidthFraction - next) > 1e-6) { _watermarkWidthFraction = next; OnPropertyChanged(); }
            }
        }

        /// <summary>Padding from edges as a fraction of frame width/height (e.g. 0.05 = 5%).</summary>
        public double WatermarkInsetFraction
        {
            get => _watermarkInsetFraction;
            set
            {
                var next = value < 0 ? 0 : (value > 0.25 ? 0.25 : value);
                if (Math.Abs(_watermarkInsetFraction - next) > 1e-6) { _watermarkInsetFraction = next; OnPropertyChanged(); }
            }
        }

        public bool TextOverlayEnabled
        {
            get => _textOverlayEnabled;
            set { if (_textOverlayEnabled != value) { _textOverlayEnabled = value; OnPropertyChanged(); } }
        }

        public string OverlayText
        {
            get => _overlayText;
            set
            {
                var next = value ?? string.Empty;
                if (_overlayText != next) { _overlayText = next; OnPropertyChanged(); }
            }
        }

        public string OverlayFont
        {
            get => _overlayFont;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "Arial" : value.Trim();
                if (_overlayFont != next) { _overlayFont = next; OnPropertyChanged(); }
            }
        }

        public int OverlayFontSize
        {
            get => _overlayFontSize;
            set
            {
                var next = value < 10 ? 10 : (value > 120 ? 120 : value);
                if (_overlayFontSize != next) { _overlayFontSize = next; OnPropertyChanged(); }
            }
        }

        public string OverlayFontColor
        {
            get => _overlayFontColor;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "white" : value.Trim();
                if (_overlayFontColor != next) { _overlayFontColor = next; OnPropertyChanged(); }
            }
        }

        public string TextPosition
        {
            get => _textPosition;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "BC" : value.Trim().ToUpperInvariant();
                if (_textPosition != next) { _textPosition = next; OnPropertyChanged(); }
            }
        }

        public bool FrameLabelEnabled
        {
            get => _frameLabelEnabled;
            set { if (_frameLabelEnabled != value) { _frameLabelEnabled = value; OnPropertyChanged(); } }
        }

        /// <summary>Khi true: ghi thư mục ảnh nhãn render (debug) khi xuất frame/encode.</summary>
        public bool FrameLabelDebugSamplesEnabled
        {
            get => _frameLabelDebugSamplesEnabled;
            set { if (_frameLabelDebugSamplesEnabled != value) { _frameLabelDebugSamplesEnabled = value; OnPropertyChanged(); } }
        }

        public string FrameLabelTemplate
        {
            get => _frameLabelTemplate;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "Frame {index} - {time}" : value;
                if (_frameLabelTemplate != next) { _frameLabelTemplate = next; OnPropertyChanged(); }
            }
        }

        public string FrameLabelPosition
        {
            get => _frameLabelPosition;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "TL" : value.Trim().ToUpperInvariant();
                if (_frameLabelPosition != next) { _frameLabelPosition = next; OnPropertyChanged(); }
            }
        }

        public string FrameLabelTextColor
        {
            get => _frameLabelTextColor;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "black" : value.Trim();
                if (_frameLabelTextColor != next) { _frameLabelTextColor = next; OnPropertyChanged(); }
            }
        }

        public string FrameLabelBackgroundColor
        {
            get => _frameLabelBackgroundColor;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "white" : value.Trim();
                if (_frameLabelBackgroundColor != next) { _frameLabelBackgroundColor = next; OnPropertyChanged(); }
            }
        }

        public int FrameLabelFontSize
        {
            get => _frameLabelFontSize;
            set
            {
                var next = value < 8 ? 8 : (value > 120 ? 120 : value);
                if (_frameLabelFontSize != next) { _frameLabelFontSize = next; OnPropertyChanged(); }
            }
        }

        public double FrameLabelX
        {
            get => _frameLabelX;
            set
            {
                var next = Math.Clamp(value, 0, 1);
                if (Math.Abs(_frameLabelX - next) > 0.0001) { _frameLabelX = next; OnPropertyChanged(); }
            }
        }

        public double FrameLabelY
        {
            get => _frameLabelY;
            set
            {
                var next = Math.Clamp(value, 0, 1);
                if (Math.Abs(_frameLabelY - next) > 0.0001) { _frameLabelY = next; OnPropertyChanged(); }
            }
        }

        public double FrameLabelW
        {
            get => _frameLabelW;
            set
            {
                var next = Math.Clamp(value, 0.05, 1);
                if (Math.Abs(_frameLabelW - next) > 0.0001) { _frameLabelW = next; OnPropertyChanged(); }
            }
        }

        public double FrameLabelH
        {
            get => _frameLabelH;
            set
            {
                var next = Math.Clamp(value, 0.03, 1);
                if (Math.Abs(_frameLabelH - next) > 0.0001) { _frameLabelH = next; OnPropertyChanged(); }
            }
        }

        public int FrameLabelHorizontalPadding
        {
            get => _frameLabelPaddingLeft;
            set
            {
                var next = value < 0 ? 0 : (value > 120 ? 120 : value);
                if (_frameLabelHorizontalPadding != next)
                {
                    _frameLabelHorizontalPadding = next;
                    _frameLabelPaddingLeft = next;
                    _frameLabelPaddingRight = next;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FrameLabelPaddingLeft));
                    OnPropertyChanged(nameof(FrameLabelPaddingRight));
                }
            }
        }

        public int FrameLabelVerticalPadding
        {
            get => _frameLabelPaddingTop;
            set
            {
                var next = value < 0 ? 0 : (value > 80 ? 80 : value);
                if (_frameLabelVerticalPadding != next)
                {
                    _frameLabelVerticalPadding = next;
                    _frameLabelPaddingTop = next;
                    _frameLabelPaddingBottom = next;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FrameLabelPaddingTop));
                    OnPropertyChanged(nameof(FrameLabelPaddingBottom));
                }
            }
        }

        public int FrameLabelPaddingLeft
        {
            get => _frameLabelPaddingLeft;
            set
            {
                var next = value < 0 ? 0 : (value > 120 ? 120 : value);
                if (_frameLabelPaddingLeft != next)
                {
                    _frameLabelPaddingLeft = next;
                    _frameLabelHorizontalPadding = next;
                    OnPropertyChanged();
                }
            }
        }

        public int FrameLabelPaddingTop
        {
            get => _frameLabelPaddingTop;
            set
            {
                var next = value < 0 ? 0 : (value > 80 ? 80 : value);
                if (_frameLabelPaddingTop != next)
                {
                    _frameLabelPaddingTop = next;
                    _frameLabelVerticalPadding = next;
                    OnPropertyChanged();
                }
            }
        }

        public int FrameLabelPaddingRight
        {
            get => _frameLabelPaddingRight;
            set
            {
                var next = value < 0 ? 0 : (value > 120 ? 120 : value);
                if (_frameLabelPaddingRight != next)
                {
                    _frameLabelPaddingRight = next;
                    _frameLabelHorizontalPadding = next;
                    OnPropertyChanged();
                }
            }
        }

        public int FrameLabelPaddingBottom
        {
            get => _frameLabelPaddingBottom;
            set
            {
                var next = value < 0 ? 0 : (value > 80 ? 80 : value);
                if (_frameLabelPaddingBottom != next)
                {
                    _frameLabelPaddingBottom = next;
                    _frameLabelVerticalPadding = next;
                    OnPropertyChanged();
                }
            }
        }

        public string FrameLabelTimeFormat
        {
            get => _frameLabelTimeFormat;
            set
            {
                var next = string.Equals(value, "HHMMSS", StringComparison.OrdinalIgnoreCase) ? "HHMMSS" : "MMSS";
                if (_frameLabelTimeFormat != next) { _frameLabelTimeFormat = next; OnPropertyChanged(); }
            }
        }

        public int ExtractParallelJobs
        {
            get => _extractParallelJobs;
            set
            {
                var next = value < 1 ? 1 : (value > 8 ? 8 : value);
                if (_extractParallelJobs != next) { _extractParallelJobs = next; OnPropertyChanged(); }
            }
        }

        public string FrameOutputFormat
        {
            get => _frameOutputFormat;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "png" : value.Trim().ToLowerInvariant();
                if (_frameOutputFormat != next) { _frameOutputFormat = next; OnPropertyChanged(); }
            }
        }

        public int JpegQuality
        {
            get => _jpegQuality;
            set
            {
                var next = value < 0 ? 0 : (value > 100 ? 100 : value);
                if (_jpegQuality != next) { _jpegQuality = next; OnPropertyChanged(); }
            }
        }

        public bool ExtractAllFrames
        {
            get => _extractAllFrames;
            set { if (_extractAllFrames != value) { _extractAllFrames = value; OnPropertyChanged(); } }
        }

        public bool TwoPassEnabled
        {
            get => _twoPassEnabled;
            set { if (_twoPassEnabled != value) { _twoPassEnabled = value; OnPropertyChanged(); } }
        }

        public string AudioCodec
        {
            get => _audioCodec;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "aac" : value.Trim().ToLowerInvariant();
                if (_audioCodec != next) { _audioCodec = next; OnPropertyChanged(); }
            }
        }

        public string AudioBitrate
        {
            get => _audioBitrate;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "192k" : value.Trim().ToLowerInvariant();
                if (_audioBitrate != next) { _audioBitrate = next; OnPropertyChanged(); }
            }
        }

        public string? SubtitlePath
        {
            get => _subtitlePath;
            set { if (_subtitlePath != value) { _subtitlePath = value; OnPropertyChanged(); } }
        }

        public bool BurnSubtitleEnabled
        {
            get => _burnSubtitleEnabled;
            set { if (_burnSubtitleEnabled != value) { _burnSubtitleEnabled = value; OnPropertyChanged(); } }
        }

        public bool ConcatEnabled
        {
            get => _concatEnabled;
            set { if (_concatEnabled != value) { _concatEnabled = value; OnPropertyChanged(); } }
        }

        public bool GridCollageEnabled
        {
            get => _gridCollageEnabled;
            set { if (_gridCollageEnabled != value) { _gridCollageEnabled = value; OnPropertyChanged(); } }
        }

        public int GridCollageWidth
        {
            get => _gridCollageWidth;
            set
            {
                var clamped = Math.Clamp(value, 200, 8000);
                if (_gridCollageWidth != clamped) { _gridCollageWidth = clamped; OnPropertyChanged(); }
            }
        }

        public int GridCollageHeight
        {
            get => _gridCollageHeight;
            set
            {
                var clamped = Math.Clamp(value, 200, 8000);
                if (_gridCollageHeight != clamped) { _gridCollageHeight = clamped; OnPropertyChanged(); }
            }
        }

        public int GridCollageFrameCount
        {
            get => _gridCollageFrameCount;
            set
            {
                var clamped = Math.Clamp(value, 1, 128);
                if (_gridCollageFrameCount != clamped) { _gridCollageFrameCount = clamped; OnPropertyChanged(); }
            }
        }

        public string GridCollageBackgroundColor
        {
            get => _gridCollageBackgroundColor;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "white" : value.Trim();
                if (_gridCollageBackgroundColor != next) { _gridCollageBackgroundColor = next; OnPropertyChanged(); }
            }
        }

        public string GridCollageColorKey
        {
            get => _gridCollageColorKey;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "white" : value.Trim();
                if (_gridCollageColorKey != next) { _gridCollageColorKey = next; OnPropertyChanged(); }
            }
        }

        public string GridCollagePadding
        {
            get => _gridCollagePadding;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "0" : value.Trim();
                if (_gridCollagePadding != next) { _gridCollagePadding = next; OnPropertyChanged(); }
            }
        }

        public string GridCollageMargin
        {
            get => _gridCollageMargin;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "0" : value.Trim();
                if (_gridCollageMargin != next) { _gridCollageMargin = next; OnPropertyChanged(); }
            }
        }

        public string GridCollageAspectMode
        {
            get => _gridCollageAspectMode;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "auto" : value.Trim().ToLowerInvariant();
                if (_gridCollageAspectMode != next) { _gridCollageAspectMode = next; OnPropertyChanged(); }
            }
        }

        public bool GridCollageShowFrameIndex
        {
            get => _gridCollageShowFrameIndex;
            set { if (_gridCollageShowFrameIndex != value) { _gridCollageShowFrameIndex = value; OnPropertyChanged(); } }
        }

        public bool ExtractByFpsEnabled
        {
            get => _extractByFpsEnabled;
            set { if (_extractByFpsEnabled != value) { _extractByFpsEnabled = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<VideoAudioTrackConfig> AudioTracks { get; }
        public ObservableCollection<VideoConcatItemConfig> ConcatVideos { get; }
        public ObservableCollection<OverlayItem> Overlays { get; }

        public void EnsureStandardDynamicOutputs()
        {
            AddOrUpdateOutput("frames_output", "Frames Output (base64 or file paths)", WorkflowDataType.ArrayString, true);
            AddOrUpdateOutput("frames_paths", "Frame File Paths", WorkflowDataType.ArrayString, true);
            AddOrUpdateOutput("frames_base64", "Frame Base64", WorkflowDataType.ArrayString, true);
            AddOrUpdateOutput("frame_folder", "Frame Output Folder", WorkflowDataType.String, false);
            AddOrUpdateOutput("video_output", "Video Output", WorkflowDataType.String, false);
            AddOrUpdateOutput("video_path", "Video File Path", WorkflowDataType.String, false);
            AddOrUpdateOutput("audio_output", "Extracted Audio Path", WorkflowDataType.String, false);
            AddOrUpdateOutput("linkAudio", "Extracted Audio Path (linkAudio)", WorkflowDataType.String, false);
            AddOrUpdateOutput("base_audio_path", "Base Audio Path (Edited/Filtered)", WorkflowDataType.String, false);
            AddOrUpdateOutput("output_manifest", "Output Manifest JSON", WorkflowDataType.String, false);
            AddOrUpdateOutput("audio_chunks", "Audio Chunks (Paths / Base64)", WorkflowDataType.ArrayString, true);
            AddOrUpdateOutput("audio_chunks_json", "Audio Chunks Manifest JSON", WorkflowDataType.String, false);
            AddOrUpdateOutput("audio_chunk_count", "Audio Chunk Count", WorkflowDataType.Number, false);
            AddOrUpdateOutput("dubbing_audio_output", "Dubbed Audio Output Path", WorkflowDataType.String, false);

            DynamicOutputs.RemoveAll(o =>
                string.Equals(o.Key, "current_chunk_audio", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(o.Key, "current_chunk_base64", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(o.Key, "current_chunk_start", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(o.Key, "current_chunk_end", StringComparison.OrdinalIgnoreCase));
        }

        private void AddOrUpdateOutput(string key, string displayName, WorkflowDataType type, bool isMultiple)
        {
            var existing = DynamicOutputs.FirstOrDefault(o =>
                string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                DynamicOutputs.Add(new WorkflowDynamicDataPort
                {
                    Key = key,
                    DisplayName = displayName,
                    OutputType = type,
                    IsMultiple = isMultiple
                });
                return;
            }

            existing.DisplayName = displayName;
            existing.OutputType = type;
            existing.IsMultiple = isMultiple;
        }

        /// <summary>
        /// Danh sách timestamp (giây) của các frame bị exclude khỏi playback/extract/export.
        /// </summary>
        public List<double> ExcludedFrameTimestamps
        {
            get => _excludedFrameTimestamps;
            set { _excludedFrameTimestamps = value ?? new(); OnPropertyChanged(); }
        }

        /// <summary>
        /// Kiểm tra timestamp có bị exclude không (tolerance ±0.5/FPS).
        /// </summary>
        public bool IsFrameExcluded(double timestampSec)
        {
            if (_excludedFrameTimestamps.Count == 0) return false;
            var effectiveFps = ExtractFps > 0 ? ExtractFps : (SourceFps > 0 ? SourceFps : 30.0);
            var tolerance = Math.Min(0.02, 0.4 / effectiveFps);
            return _excludedFrameTimestamps.Any(t => Math.Abs(t - timestampSec) < tolerance);
        }

        public void ToggleFrameExclusion(double timestampSec)
        {
            var effectiveFps = ExtractFps > 0 ? ExtractFps : (SourceFps > 0 ? SourceFps : 30.0);
            var tolerance = Math.Min(0.02, 0.4 / effectiveFps);
            var existing = _excludedFrameTimestamps.FirstOrDefault(t => Math.Abs(t - timestampSec) < tolerance);
            if (_excludedFrameTimestamps.Any(t => Math.Abs(t - timestampSec) < tolerance))
                _excludedFrameTimestamps.RemoveAll(t => Math.Abs(t - timestampSec) < tolerance);
            else
                _excludedFrameTimestamps.Add(timestampSec);
            OnPropertyChanged(nameof(ExcludedFrameTimestamps));
        }

        /// <summary>Explicitly exclude or include a frame.</summary>
        public void ToggleFrameExclusion(double timestampSec, bool exclude)
        {
            var effectiveFps = ExtractFps > 0 ? ExtractFps : (SourceFps > 0 ? SourceFps : 30.0);
            var tolerance = Math.Min(0.02, 0.4 / effectiveFps);
            var isCurrentlyExcluded = _excludedFrameTimestamps.Any(t => Math.Abs(t - timestampSec) < tolerance);
            if (exclude && !isCurrentlyExcluded)
                _excludedFrameTimestamps.Add(timestampSec);
            else if (!exclude && isCurrentlyExcluded)
                _excludedFrameTimestamps.RemoveAll(t => Math.Abs(t - timestampSec) < tolerance);
            OnPropertyChanged(nameof(ExcludedFrameTimestamps));
        }

        public void ClearExcludedFrames()
        {
            _excludedFrameTimestamps.Clear();
            OnPropertyChanged(nameof(ExcludedFrameTimestamps));
        }

        public void RaisePropertyChanged(string propertyName) => OnPropertyChanged(propertyName);
    }
}
