using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Core.Models.Media
{
    public class DubbingClipItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private string _clipName = "Voice Clip";
        private string _audioFilePath = string.Empty;
        private double _startAtSec;
        private double _durationSec;
        private double _volumePercent = 100.0;
        private double _pitchSemitones;
        private double _speedFactor = 1.0;
        private double _pan; // -1.0 (Left) to +1.0 (Right)
        private bool _isMuted;
        private bool _isSolo;
        private string _scriptText = string.Empty;
        private string _voiceModel = "Vietnamese_Natural";

        public string Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        public string ClipName
        {
            get => _clipName;
            set => SetField(ref _clipName, value ?? "Voice Clip");
        }

        public string AudioFilePath
        {
            get => _audioFilePath;
            set => SetField(ref _audioFilePath, value ?? string.Empty);
        }

        public double StartAtSec
        {
            get => _startAtSec;
            set
            {
                if (SetField(ref _startAtSec, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(FormattedStartAt));
                    OnPropertyChanged(nameof(EndAtSec));
                    OnPropertyChanged(nameof(FormattedEndAt));
                }
            }
        }

        public double DurationSec
        {
            get => _durationSec;
            set
            {
                if (SetField(ref _durationSec, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(FormattedDuration));
                    OnPropertyChanged(nameof(EndAtSec));
                    OnPropertyChanged(nameof(FormattedEndAt));
                }
            }
        }

        public double EndAtSec => _startAtSec + _durationSec;

        public double VolumePercent
        {
            get => _volumePercent;
            set => SetField(ref _volumePercent, Math.Clamp(value, 0, 300));
        }

        public double PitchSemitones
        {
            get => _pitchSemitones;
            set => SetField(ref _pitchSemitones, Math.Clamp(value, -12, 12));
        }

        public double SpeedFactor
        {
            get => _speedFactor;
            set => SetField(ref _speedFactor, Math.Clamp(value, 0.5, 2.0));
        }

        public double Pan
        {
            get => _pan;
            set => SetField(ref _pan, Math.Clamp(value, -1.0, 1.0));
        }

        public bool IsMuted
        {
            get => _isMuted;
            set => SetField(ref _isMuted, value);
        }

        public bool IsSolo
        {
            get => _isSolo;
            set => SetField(ref _isSolo, value);
        }

        public string ScriptText
        {
            get => _scriptText;
            set => SetField(ref _scriptText, value ?? string.Empty);
        }

        public string VoiceModel
        {
            get => _voiceModel;
            set => SetField(ref _voiceModel, value ?? string.Empty);
        }

        public string FormattedStartAt => FormatSeconds(_startAtSec);
        public string FormattedEndAt => FormatSeconds(EndAtSec);
        public string FormattedDuration => $"{_durationSec:F1}s";

        private static string FormatSeconds(double sec)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, sec));
            return $"{(int)ts.TotalMinutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
        }

        public DubbingClipItem Clone()
        {
            return new DubbingClipItem
            {
                Id = Guid.NewGuid().ToString("N"),
                ClipName = ClipName,
                AudioFilePath = AudioFilePath,
                StartAtSec = StartAtSec,
                DurationSec = DurationSec,
                VolumePercent = VolumePercent,
                PitchSemitones = PitchSemitones,
                SpeedFactor = SpeedFactor,
                Pan = Pan,
                IsMuted = IsMuted,
                ScriptText = ScriptText,
                VoiceModel = VoiceModel
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class AutoDuckingConfig : INotifyPropertyChanged
    {
        private bool _enabled = true;
        private double _duckingAmountDb = -12.0; // -6dB to -24dB reduction in background music
        private double _attackMs = 150.0;
        private double _releaseMs = 350.0;
        private double _thresholdSensitivity = 0.05;

        public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
        public double DuckingAmountDb { get => _duckingAmountDb; set => SetField(ref _duckingAmountDb, Math.Clamp(value, -30.0, -1.0)); }
        public double AttackMs { get => _attackMs; set => SetField(ref _attackMs, Math.Clamp(value, 10.0, 1000.0)); }
        public double ReleaseMs { get => _releaseMs; set => SetField(ref _releaseMs, Math.Clamp(value, 50.0, 2000.0)); }
        public double ThresholdSensitivity { get => _thresholdSensitivity; set => SetField(ref _thresholdSensitivity, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
