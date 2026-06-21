using FlowMy.Models;
using System;
using System.Collections.Generic;

namespace FlowMy.Models.Nodes
{
    // ✅ KHÔNG thêm INotifyPropertyChanged — WorkflowNode đã implement
    // ✅ KHÔNG khai báo lại: PropertyChanged, OnPropertyChanged, TitleDisplayMode, TitleColorMode, TitleColorKey
    public sealed class ActionCanVasNode : WorkflowNode
    {
        // ─── Border Visual Properties (giống BodyContainerNode) ───
        private double _bodyWidth = 600;
        private double _bodyHeight = 300;
        private string _bodyBackgroundColorHex = "#1A6B7280"; // Mờ hơn
        private string _bodyBorderColorHex = "#FF3B82F6"; // Viền xanh dương (Blue)
        private bool _useUnifiedColors = false;
        private double _backgroundOpacityPercent = 10;
        private double _borderOpacityPercent = 100;
        private double _borderThickness = 2;
        private BorderDashStyle _borderDashStyle = BorderDashStyle.Dash;
        private double _borderDashSpacing = 3;

        // ─── Macro Recording Properties (giống MacroRecorderNode) ───
        private string _outputKey = "canvasData";
        private string _macroDataJson = "";
        private MacroPlaybackMode _playbackMode = MacroPlaybackMode.Once;
        private int _repeatIntervalMs = 500;
        private int _repeatCount = 1;
        private VisualPlaybackMode _visualPlaybackMode = VisualPlaybackMode.Live;
        private int _countdownSeconds = 3;

        // ─── Playback Highlight Properties (giống BorderHighlightNode) ───
        private string _playbackBorderColorHex = "#00D2FF";
        private int _playbackBorderThickness = 2;
        private int _playbackGradientSize = 15;
        private double _playbackOpacity = 0.85;
        private BorderEffectType _playbackEffectType = BorderEffectType.Pulse;

        public const string OutputKey_JsonStep = "JsonStep";

        public ActionCanVasNode()
        {
            Type = NodeType.ActionCanVas;
            Title = "Thao tác canvas";
            ColorKey = "Blue"; // Đổi màu node sang xanh cho khác biệt

            // ⚠️ Cổng sẽ được thêm ở TemplateFactory
        }

        // ─── Border Properties ───

        public double BodyWidth
        {
            get => _bodyWidth;
            set
            {
                var normalized = value < 100 ? 100 : value;
                if (_bodyWidth == normalized) return;
                _bodyWidth = normalized;
                OnPropertyChanged();
            }
        }

        public double BodyHeight
        {
            get => _bodyHeight;
            set
            {
                var normalized = value < 100 ? 100 : value;
                if (_bodyHeight == normalized) return;
                _bodyHeight = normalized;
                OnPropertyChanged();
            }
        }

        public string BodyBackgroundColorHex
        {
            get => _bodyBackgroundColorHex;
            set { var n = string.IsNullOrWhiteSpace(value) ? "#1A6B7280" : value.Trim(); if (_bodyBackgroundColorHex != n) { _bodyBackgroundColorHex = n; OnPropertyChanged(); } }
        }

        public string BodyBorderColorHex
        {
            get => _bodyBorderColorHex;
            set { var n = string.IsNullOrWhiteSpace(value) ? "#FF3B82F6" : value.Trim(); if (_bodyBorderColorHex != n) { _bodyBorderColorHex = n; OnPropertyChanged(); } }
        }

        public bool UseUnifiedColors
        {
            get => _useUnifiedColors;
            set { if (_useUnifiedColors != value) { _useUnifiedColors = value; OnPropertyChanged(); } }
        }

        public double BackgroundOpacityPercent
        {
            get => _backgroundOpacityPercent;
            set { var n = Math.Max(0, Math.Min(100, value)); if (Math.Abs(_backgroundOpacityPercent - n) > 0.01) { _backgroundOpacityPercent = n; OnPropertyChanged(); } }
        }

        public double BorderOpacityPercent
        {
            get => _borderOpacityPercent;
            set { var n = Math.Max(0, Math.Min(100, value)); if (Math.Abs(_borderOpacityPercent - n) > 0.01) { _borderOpacityPercent = n; OnPropertyChanged(); } }
        }

        public double BorderThickness
        {
            get => _borderThickness;
            set { var n = Math.Max(0.5, Math.Min(10, value)); if (Math.Abs(_borderThickness - n) > 0.01) { _borderThickness = n; OnPropertyChanged(); } }
        }

        public BorderDashStyle BorderDashStyle
        {
            get => _borderDashStyle;
            set { if (_borderDashStyle != value) { _borderDashStyle = value; OnPropertyChanged(); } }
        }

        public double BorderDashSpacing
        {
            get => _borderDashSpacing;
            set { var n = Math.Max(0, Math.Min(20, value)); if (Math.Abs(_borderDashSpacing - n) > 0.01) { _borderDashSpacing = n; OnPropertyChanged(); } }
        }

        // ─── Macro Properties ───

        public string OutputKey
        {
            get => _outputKey;
            set { var s = value ?? "canvasData"; if (_outputKey != s) { _outputKey = s; OnPropertyChanged(); } }
        }

        public string MacroDataJson
        {
            get => _macroDataJson;
            set { var s = value ?? ""; if (_macroDataJson != s) { _macroDataJson = s; OnPropertyChanged(); } }
        }

        public MacroPlaybackMode PlaybackMode
        {
            get => _playbackMode;
            set { if (_playbackMode != value) { _playbackMode = value; OnPropertyChanged(); } }
        }

        public int RepeatIntervalMs
        {
            get => _repeatIntervalMs;
            set { var v = value < 0 ? 0 : value; if (_repeatIntervalMs != v) { _repeatIntervalMs = v; OnPropertyChanged(); } }
        }

        public int RepeatCount
        {
            get => _repeatCount;
            set { var v = value < 1 ? 1 : value; if (_repeatCount != v) { _repeatCount = v; OnPropertyChanged(); } }
        }

        public VisualPlaybackMode VisualPlaybackMode
        {
            get => _visualPlaybackMode;
            set { if (_visualPlaybackMode != value) { _visualPlaybackMode = value; OnPropertyChanged(); } }
        }

        public int CountdownSeconds
        {
            get => _countdownSeconds;
            set { var v = Math.Clamp(value, 0, 10); if (_countdownSeconds != v) { _countdownSeconds = v; OnPropertyChanged(); } }
        }

        // ─── Playback Highlight Properties ───

        public string PlaybackBorderColorHex
        {
            get => _playbackBorderColorHex;
            set { var s = value ?? "#00D2FF"; if (_playbackBorderColorHex != s) { _playbackBorderColorHex = s; OnPropertyChanged(); } }
        }

        public int PlaybackBorderThickness
        {
            get => _playbackBorderThickness;
            set { var v = value < 1 ? 1 : value > 10 ? 10 : value; if (_playbackBorderThickness != v) { _playbackBorderThickness = v; OnPropertyChanged(); } }
        }

        public int PlaybackGradientSize
        {
            get => _playbackGradientSize;
            set { var v = value < 5 ? 5 : value > 50 ? 50 : value; if (_playbackGradientSize != v) { _playbackGradientSize = v; OnPropertyChanged(); } }
        }

        public double PlaybackOpacity
        {
            get => _playbackOpacity;
            set { var v = value < 0.1 ? 0.1 : value > 1.0 ? 1.0 : value; if (Math.Abs(_playbackOpacity - v) > 0.01) { _playbackOpacity = v; OnPropertyChanged(); } }
        }

        public BorderEffectType PlaybackEffectType
        {
            get => _playbackEffectType;
            set { if (_playbackEffectType != value) { _playbackEffectType = value; OnPropertyChanged(); } }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public System.Windows.Controls.Border? ContainerBorder { get; set; }
    }
}
