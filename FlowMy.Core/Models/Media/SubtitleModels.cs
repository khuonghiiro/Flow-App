// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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
                    OnPropertyChanged(nameof(StartTimeHms));
                    OnPropertyChanged(nameof(FormattedTimeRange));
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
                    OnPropertyChanged(nameof(EndTimeHms));
                    OnPropertyChanged(nameof(FormattedTimeRange));
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

        public string FormattedStartTime => FormatHms(_startTimeSec);
        public string FormattedEndTime => FormatHms(_endTimeSec);

        public string StartTimeHms
        {
            get => FormatHms(_startTimeSec);
            set
            {
                if (TryParseHms(value, out var sec) && Math.Abs(sec - _startTimeSec) > 0.001)
                {
                    StartTimeSec = sec;
                }
            }
        }

        public string EndTimeHms
        {
            get => FormatHms(_endTimeSec);
            set
            {
                if (TryParseHms(value, out var sec) && Math.Abs(sec - _endTimeSec) > 0.001)
                {
                    EndTimeSec = sec;
                }
            }
        }

        public string FormattedTimeRange => $"{FormatHms(_startTimeSec)} ➔ {FormatHms(_endTimeSec)} ({DurationSec:0.0}s)";

        public static string FormatHms(double sec)
        {
            if (double.IsNaN(sec) || double.IsInfinity(sec) || sec < 0) sec = 0;
            var ts = TimeSpan.FromSeconds(sec);
            int hours = (int)ts.TotalHours;
            int mins = ts.Minutes;
            int secs = ts.Seconds;
            int tenths = (ts.Milliseconds / 100);
            return $"{hours:00}:{mins:00}:{secs:00}.{tenths:0}";
        }

        public static bool TryParseHms(string? input, out double seconds)
        {
            seconds = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;
            input = input.Trim().Replace(',', '.');

            // Plain seconds like "12.5" or "12"
            if (double.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var plainSec))
            {
                seconds = Math.Max(0, plainSec);
                return true;
            }

            // Formats like "00:01:23.5" or "01:23.5" or "01:23"
            var parts = input.Split(':');
            try
            {
                if (parts.Length == 3)
                {
                    if (double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h) &&
                        double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) &&
                        double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s))
                    {
                        seconds = Math.Max(0, h * 3600 + m * 60 + s);
                        return true;
                    }
                }
                else if (parts.Length == 2)
                {
                    if (double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) &&
                        double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s))
                    {
                        seconds = Math.Max(0, m * 60 + s);
                        return true;
                    }
                }
            }
            catch { }

            return false;
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
        private bool _autoWrapLongText = true;
        private int _maxCharsPerLine = 36;
        private double _positionOffsetX;
        private double _positionOffsetY;

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
        public double PositionOffsetX { get => _positionOffsetX; set => SetField(ref _positionOffsetX, value); }
        public double PositionOffsetY { get => _positionOffsetY; set => SetField(ref _positionOffsetY, value); }
        public string AnimationEffect { get => _animationEffect; set => SetField(ref _animationEffect, value); }
        public string PresetTheme { get => _presetTheme; set => SetField(ref _presetTheme, value); }
        public bool AutoWrapLongText { get => _autoWrapLongText; set => SetField(ref _autoWrapLongText, value); }
        public int MaxCharsPerLine { get => _maxCharsPerLine; set => SetField(ref _maxCharsPerLine, value); }

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

    public static class SubtitleAssBuilder
    {
        public static string BuildAssFileContent(
            System.Collections.Generic.IEnumerable<SubtitleItem> subtitles,
            SubtitleStyleConfig? style,
            int sourceWidth,
            int sourceHeight)
        {
            var sb = new System.Text.StringBuilder();
            var font = !string.IsNullOrWhiteSpace(style?.FontFamily) ? style.FontFamily : "Segoe UI";

            // Resolution canvas directly matching actual video dimensions
            int playResX = sourceWidth > 0 ? sourceWidth : 1920;
            int playResY = sourceHeight > 0 ? sourceHeight : 1080;
            double minDim = Math.Min(playResX, playResY);

            // Responsive video-scale font size calibrated to standard video subtitle proportions
            // (userFontSize 24 -> 54px on 1080p, ~5% of video reference dimension)
            double userFontSize = style?.FontSize ?? 24;
            double videoFontSize = Math.Max(12.0, (userFontSize * 2.25) * (minDim / 1080.0));
            int assFontSize = (int)Math.Round(videoFontSize);

            int wrapStyle = (style?.AutoWrapLongText ?? true) ? 0 : 2;

            sb.AppendLine("[Script Info]");
            sb.AppendLine("ScriptType: v4.00+");
            sb.AppendLine($"PlayResX: {playResX}");
            sb.AppendLine($"PlayResY: {playResY}");
            sb.AppendLine($"WrapStyle: {wrapStyle}");
            sb.AppendLine("ScaledBorderAndShadow: yes");
            sb.AppendLine();

            sb.AppendLine("[V4+ Styles]");
            sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");

            var boldVal = (style?.IsBold ?? true) ? -1 : 0;
            var italicVal = (style?.IsItalic ?? false) ? -1 : 0;

            var alignStr = style?.Alignment ?? "BottomCenter";
            var alignVal = alignStr switch
            {
                "TopLeft" => 7,
                "TopCenter" => 8,
                "TopRight" => 9,
                "CenterLeft" => 4,
                "Center" => 5,
                "CenterRight" => 6,
                "BottomLeft" => 1,
                "BottomRight" => 3,
                _ => 2 // BottomCenter
            };

            var primaryColor = ColorToAssHex(style?.TextColor ?? "#FFFFFF", 0x00);

            // Background box styling & BorderStyle mapping
            int borderStyle = (style != null && style.BackgroundBoxEnabled) ? 3 : 1;
            string outlineColor;
            string backColor;
            int outlineThick;
            int shadowDist;

            if (borderStyle == 3)
            {
                // In ASS with BorderStyle 3 (box):
                // OutlineColour is the background box fill color
                // Outline is the box padding in video pixels
                // BackColour is the box shadow color
                outlineColor = ColorToAssHex(style?.BackgroundBoxColor ?? "#A0000000", 0x60);
                backColor = "&HFF000000"; // transparent shadow for clean box
                outlineThick = Math.Max(6, (int)Math.Round(videoFontSize * 0.38)); // proportional box padding (~20px on 1080p)
                shadowDist = 0;
            }
            else
            {
                // In ASS with BorderStyle 1 (outline + shadow):
                outlineColor = ColorToAssHex(style?.OutlineColor ?? "#000000", 0x00);
                backColor = ColorToAssHex(style?.ShadowColor ?? "#80000000", 0x80);
                outlineThick = Math.Max(0, (int)Math.Round(((style?.OutlineThickness ?? 2) * 2.0) * (minDim / 1080.0)));
                shadowDist = Math.Max(0, (int)Math.Round(((style?.ShadowDistance ?? 1) * 2.0) * (minDim / 1080.0)));
            }

            double sMargin = Math.Max(30.0, playResX * 0.05) * (minDim / 1080.0);
            double bMargin = ((style?.BottomMarginPx ?? 40) * 1.5) * (minDim / 1080.0);
            double offX = ((style?.PositionOffsetX ?? 0) * 1.5) * (minDim / 1080.0);
            double offY = ((style?.PositionOffsetY ?? 0) * 1.5) * (minDim / 1080.0);

            int marginL = (int)Math.Max(0, Math.Round(sMargin));
            int marginR = (int)Math.Max(0, Math.Round(sMargin));
            int marginV = (int)Math.Max(0, Math.Round(bMargin));

            sb.AppendLine($"Style: Default,{font},{assFontSize},{primaryColor},&H000000FF,{outlineColor},{backColor},{boldVal},{italicVal},0,0,100,100,0,0,{borderStyle},{outlineThick},{shadowDist},{alignVal},{marginL},{marginR},{marginV},1");
            sb.AppendLine();

            sb.AppendLine("[Events]");
            sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

            // Calculate precise anchor coordinates on the video canvas
            double posX;
            double posY;

            switch (alignStr)
            {
                case "TopLeft":
                    posX = sMargin + offX;
                    posY = bMargin + offY;
                    break;
                case "TopCenter":
                    posX = (playResX / 2.0) + offX;
                    posY = bMargin + offY;
                    break;
                case "TopRight":
                    posX = (playResX - sMargin) + offX;
                    posY = bMargin + offY;
                    break;
                case "CenterLeft":
                    posX = sMargin + offX;
                    posY = (playResY / 2.0) + offY;
                    break;
                case "Center":
                    posX = (playResX / 2.0) + offX;
                    posY = (playResY / 2.0) + offY;
                    break;
                case "CenterRight":
                    posX = (playResX - sMargin) + offX;
                    posY = (playResY / 2.0) + offY;
                    break;
                case "BottomLeft":
                    posX = sMargin + offX;
                    posY = playResY - bMargin + offY;
                    break;
                case "BottomRight":
                    posX = (playResX - sMargin) + offX;
                    posY = playResY - bMargin + offY;
                    break;
                default: // BottomCenter
                    posX = (playResX / 2.0) + offX;
                    posY = playResY - bMargin + offY;
                    break;
            }

            var posTag = $"\\an{alignVal}\\pos({posX.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)},{posY.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)})";

            foreach (var sub in subtitles)
            {
                var st = TimeSpan.FromSeconds(Math.Max(0, sub.StartTimeSec));
                var et = TimeSpan.FromSeconds(Math.Max(sub.StartTimeSec + 0.1, sub.EndTimeSec));
                var stStr = $"{(int)st.TotalHours:0}:{st.Minutes:00}:{st.Seconds:00}.{st.Milliseconds / 10:00}";
                var etStr = $"{(int)et.TotalHours:0}:{et.Minutes:00}:{et.Seconds:00}.{et.Milliseconds / 10:00}";
                var txt = (sub.Text ?? string.Empty).Replace("\r\n", @"\N").Replace("\n", @"\N").Replace("\r", @"\N");
                sb.AppendLine($"Dialogue: 0,{stStr},{etStr},Default,,0,0,0,,{{{posTag}}}{txt}");
            }

            return sb.ToString();
        }

        public static string ColorToAssHex(string hexColor, byte defaultAssAlpha = 0x00)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hexColor)) return "&H00FFFFFF";
                var cleaned = hexColor.Trim().TrimStart('#');
                byte a = defaultAssAlpha, r = 255, g = 255, b = 255;
                if (cleaned.Length == 6)
                {
                    r = Convert.ToByte(cleaned.Substring(0, 2), 16);
                    g = Convert.ToByte(cleaned.Substring(2, 2), 16);
                    b = Convert.ToByte(cleaned.Substring(4, 2), 16);
                    a = defaultAssAlpha;
                }
                else if (cleaned.Length == 8)
                {
                    var wpfAlpha = Convert.ToByte(cleaned.Substring(0, 2), 16);
                    a = (byte)(255 - wpfAlpha);
                    r = Convert.ToByte(cleaned.Substring(2, 2), 16);
                    g = Convert.ToByte(cleaned.Substring(4, 2), 16);
                    b = Convert.ToByte(cleaned.Substring(6, 2), 16);
                }
                return $"&H{a:X2}{b:X2}{g:X2}{r:X2}";
            }
            catch
            {
                return "&H00FFFFFF";
            }
        }
    }
}

