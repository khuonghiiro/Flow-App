using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Core.Models.Media
{
    public class SubtitleItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private double _startTimeSec;
        private double _endTimeSec = 2.0;
        private string _text = string.Empty;
        private bool _isSelected;

        public string Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        public double StartTimeSec
        {
            get => _startTimeSec;
            set
            {
                if (SetField(ref _startTimeSec, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(FormattedStartTime));
                    OnPropertyChanged(nameof(DurationSec));
                }
            }
        }

        public double EndTimeSec
        {
            get => _endTimeSec;
            set
            {
                if (SetField(ref _endTimeSec, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(FormattedEndTime));
                    OnPropertyChanged(nameof(DurationSec));
                }
            }
        }

        public double DurationSec => Math.Max(0, _endTimeSec - _startTimeSec);

        public string Text
        {
            get => _text;
            set => SetField(ref _text, value ?? string.Empty);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public string FormattedStartTime => FormatSeconds(_startTimeSec);
        public string FormattedEndTime => FormatSeconds(_endTimeSec);

        private static string FormatSeconds(double sec)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, sec));
            return $"{(int)ts.TotalMinutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
        }

        public SubtitleItem Clone()
        {
            return new SubtitleItem
            {
                Id = Guid.NewGuid().ToString("N"),
                StartTimeSec = StartTimeSec,
                EndTimeSec = EndTimeSec,
                Text = Text
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

    public class SubtitleStyleConfig : INotifyPropertyChanged
    {
        private bool _enabled = true;
        private bool _hardcodeBurnIn = true;
        private string _fontFamily = "Segoe UI";
        private double _fontSize = 24;
        private bool _isBold = true;
        private bool _isItalic;
        private string _textColor = "#FFFFFF";
        private string _highlightColor = "#FFD700";
        private double _outlineThickness = 2.0;
        private string _outlineColor = "#000000";
        private double _shadowDistance = 1.5;
        private string _shadowColor = "#80000000";
        private bool _backgroundBoxEnabled;
        private string _backgroundBoxColor = "#A0000000";
        private double _backgroundBoxPadding = 6;
        private double _backgroundBoxCornerRadius = 4;
        private string _alignment = "BottomCenter"; // BottomCenter, Center, TopCenter, BottomLeft, BottomRight
        private double _bottomMarginPx = 40;
        private double _sideMarginPx = 20;
        private string _animationEffect = "None"; // None, Fade, Pop, Karaoke
        private string _presetTheme = "Default"; // Default, TikTokViral, Netflix, MinimalClean, GamingRgb, NewsBanner

        public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
        public bool HardcodeBurnIn { get => _hardcodeBurnIn; set => SetField(ref _hardcodeBurnIn, value); }
        public string FontFamily { get => _fontFamily; set => SetField(ref _fontFamily, value); }
        public double FontSize { get => _fontSize; set => SetField(ref _fontSize, value); }
        public bool IsBold { get => _isBold; set => SetField(ref _isBold, value); }
        public bool IsItalic { get => _isItalic; set => SetField(ref _isItalic, value); }
        public string TextColor { get => _textColor; set => SetField(ref _textColor, value); }
        public string HighlightColor { get => _highlightColor; set => SetField(ref _highlightColor, value); }
        public double OutlineThickness { get => _outlineThickness; set => SetField(ref _outlineThickness, value); }
        public string OutlineColor { get => _outlineColor; set => SetField(ref _outlineColor, value); }
        public double ShadowDistance { get => _shadowDistance; set => SetField(ref _shadowDistance, value); }
        public string ShadowColor { get => _shadowColor; set => SetField(ref _shadowColor, value); }
        public bool BackgroundBoxEnabled { get => _backgroundBoxEnabled; set => SetField(ref _backgroundBoxEnabled, value); }
        public string BackgroundBoxColor { get => _backgroundBoxColor; set => SetField(ref _backgroundBoxColor, value); }
        public double BackgroundBoxPadding { get => _backgroundBoxPadding; set => SetField(ref _backgroundBoxPadding, value); }
        public double BackgroundBoxCornerRadius { get => _backgroundBoxCornerRadius; set => SetField(ref _backgroundBoxCornerRadius, value); }
        public string Alignment { get => _alignment; set => SetField(ref _alignment, value); }
        public double BottomMarginPx { get => _bottomMarginPx; set => SetField(ref _bottomMarginPx, value); }
        public double SideMarginPx { get => _sideMarginPx; set => SetField(ref _sideMarginPx, value); }
        public string AnimationEffect { get => _animationEffect; set => SetField(ref _animationEffect, value); }
        public string PresetTheme { get => _presetTheme; set => SetField(ref _presetTheme, value); }

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
