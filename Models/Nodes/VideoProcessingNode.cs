using FlowMy.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

    public sealed class VideoAudioTrackConfig : INotifyPropertyChanged
    {
        private string? _sourceNodeId;
        private string? _sourceOutputKey;
        private double _volumePercent = 100;
        private AudioSyncMode _shorterMode = AudioSyncMode.Loop;
        private AudioSyncMode _longerMode = AudioSyncMode.Trim;

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

    public sealed class VideoProcessingNode : WorkflowNode, INotifyPropertyChanged
    {
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;
        private double _width = 1360;
        private double _height = 768;

        private string? _videoSourceNodeId;
        private string? _videoSourceOutputKey;
        private string _videoPath = string.Empty;
        private string? _outputFolderSourceNodeId;
        private string? _outputFolderSourceOutputKey;
        private string? _videoOutputFolderSourceNodeId;
        private string? _videoOutputFolderSourceOutputKey;
        private bool _outputBase64 = true;
        private bool _useDialogVideoConfig = true;
        private string? _frameOutputFolderPath;
        private string? _defaultOutputVideoPath;
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
        private double _trimStartSec;
        private double _trimEndSec;
        private string? _outputPathOverride;
        private bool _sourceAudioEnabled = true;
        private double _previewVolume = 0.7;
        private string _previewQualityMode = "normal";
        private string _previewVisualStrengthMode = "balanced";
        private bool _watermarkEnabled;
        private string? _watermarkImagePath;
        private string _watermarkPosition = "BR";
        private double _watermarkOpacity = 1.0;
        private int _watermarkPaddingPx = 10;
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
        private int _frameLabelFontSize = 12;
        private double _frameLabelX = 0.765223;
        private double _frameLabelY = 0.005624;
        private double _frameLabelW = 0.233889;
        private double _frameLabelH = 0.090679;
        private int _frameLabelHorizontalPadding = 2;
        private int _frameLabelVerticalPadding = 4;
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

        public VideoProcessingNode()
        {
            Type = NodeType.VideoProcessing;
            Title = "Video Processing";
            AudioTracks = new ObservableCollection<VideoAudioTrackConfig>();
            Overlays = new ObservableCollection<OverlayItem>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

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

        public TitleDisplayMode TitleDisplayMode
        {
            get => _titleDisplayMode;
            set { if (_titleDisplayMode != value) { _titleDisplayMode = value; OnPropertyChanged(); } }
        }

        public TitleColorMode TitleColorMode
        {
            get => _titleColorMode;
            set { if (_titleColorMode != value) { _titleColorMode = value; OnPropertyChanged(); } }
        }

        public string? TitleColorKey
        {
            get => _titleColorKey;
            set { if (_titleColorKey != value) { _titleColorKey = value; OnPropertyChanged(); } }
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
            get => _frameLabelHorizontalPadding;
            set
            {
                var next = value < 0 ? 0 : (value > 120 ? 120 : value);
                if (_frameLabelHorizontalPadding != next) { _frameLabelHorizontalPadding = next; OnPropertyChanged(); }
            }
        }

        public int FrameLabelVerticalPadding
        {
            get => _frameLabelVerticalPadding;
            set
            {
                var next = value < 0 ? 0 : (value > 80 ? 80 : value);
                if (_frameLabelVerticalPadding != next) { _frameLabelVerticalPadding = next; OnPropertyChanged(); }
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

        public ObservableCollection<VideoAudioTrackConfig> AudioTracks { get; }
        public ObservableCollection<OverlayItem> Overlays { get; }

        public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));
        public void RaisePropertyChanged(string propertyName) => OnPropertyChanged(propertyName);

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
