using FlowMy.Models;
using System;
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
        private double _width = 540;
        private double _height = 340;

        private string? _videoSourceNodeId;
        private string? _videoSourceOutputKey;
        private string _videoPath = string.Empty;
        private string? _outputFolderSourceNodeId;
        private string? _outputFolderSourceOutputKey;
        private bool _outputBase64 = true;

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
        private int? _fixedResolutionHeight;
        private bool _trimEnabled;
        private double _trimStartSec;
        private double _trimEndSec;
        private string? _outputPathOverride;
        private bool _sourceAudioEnabled = true;
        private double _previewVolume = 0.7;

        public VideoProcessingNode()
        {
            Type = NodeType.VideoProcessing;
            Title = "Video Processing";
            AudioTracks = new System.Collections.ObjectModel.ObservableCollection<VideoAudioTrackConfig>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public TextBlock? TitleTextBlockUI { get; set; }

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

        public bool OutputBase64
        {
            get => _outputBase64;
            set { if (_outputBase64 != value) { _outputBase64 = value; OnPropertyChanged(); } }
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
                var max = SourceFps < 1 ? 1 : SourceFps;
                var next = value < 1 ? 1 : (value > max ? max : value);
                if (Math.Abs(_extractFps - next) > 0.01) { _extractFps = next; OnPropertyChanged(); }
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

        public System.Collections.ObjectModel.ObservableCollection<VideoAudioTrackConfig> AudioTracks { get; }

        public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));
        public void RaisePropertyChanged(string propertyName) => OnPropertyChanged(propertyName);

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
