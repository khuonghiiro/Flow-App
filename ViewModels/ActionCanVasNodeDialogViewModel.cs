using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace FlowMy.ViewModels
{
    public partial class ActionCanVasNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly ActionCanVasNode _actionCanVasNode;

        // ─── Border Visual Properties ───
        [ObservableProperty] private double _bodyWidth;
        [ObservableProperty] private double _bodyHeight;
        [ObservableProperty] private string _bodyBackgroundColorHex = string.Empty;
        [ObservableProperty] private string _bodyBorderColorHex = string.Empty;
        [ObservableProperty] private bool _useUnifiedColors;
        [ObservableProperty] private double _backgroundOpacityPercent;
        [ObservableProperty] private double _borderOpacityPercent;
        [ObservableProperty] private double _borderThickness;
        [ObservableProperty] private double _borderDashSpacing;
        [ObservableProperty] private BorderDashStyle _borderDashStyle = BorderDashStyle.Dash;
        [ObservableProperty] private string? _bodyBackgroundColorKey;
        [ObservableProperty] private string? _bodyBorderColorKey;

        public ObservableCollection<BodyColorOption> BodyColorOptions { get; } = new()
        {
            new BodyColorOption("SlateBrush", "Slate"),
            new BodyColorOption("PrimaryBrush", "Primary Blue"),
            new BodyColorOption("SuccessBrush", "Success Green"),
            new BodyColorOption("DangerBrush", "Danger Red"),
            new BodyColorOption("WarningBrush", "Warning Orange"),
            new BodyColorOption("InfoBrush", "Info Cyan"),
            new BodyColorOption("IndigoBrush", "Indigo"),
            new BodyColorOption("CoralBrush", "Coral"),
            new BodyColorOption("OceanBrush", "Ocean"),
            new BodyColorOption("LavenderBrush", "Lavender")
        };

        public ObservableCollection<BorderDashStyleOption> BorderDashStyleOptions { get; } = new()
        {
            new BorderDashStyleOption(BorderDashStyle.Solid, "Solid (Liền)"),
            new BorderDashStyleOption(BorderDashStyle.Dash, "Dash (Nét đứt)"),
            new BorderDashStyleOption(BorderDashStyle.Dot, "Dot (Chấm)"),
            new BorderDashStyleOption(BorderDashStyle.DashDot, "Dash-Dot"),
            new BorderDashStyleOption(BorderDashStyle.DashDotDot, "Dash-Dot-Dot")
        };

        // ─── Macro Recording Properties ───
        [ObservableProperty] private string _outputKey = string.Empty;
        [ObservableProperty] private string _macroDataJson = string.Empty;
        [ObservableProperty] private string _selectedPlaybackMode = string.Empty;
        [ObservableProperty] private int _repeatIntervalMs;
        [ObservableProperty] private int _repeatCount;
        [ObservableProperty] private string _selectedVisualPlaybackMode = string.Empty;
        [ObservableProperty] private int _countdownSeconds;

        public bool IsRepeatVisible => SelectedPlaybackMode == "Lặp lại";
        public bool CanExportJson => !string.IsNullOrWhiteSpace(MacroDataJson);

        public ObservableCollection<string> PlaybackModeOptions { get; } = new()
        {
            "Chạy 1 lần", "Lặp lại"
        };

        public ObservableCollection<string> VisualPlaybackModeOptions { get; } = new()
        {
            "Không hiển thị", "Hiển thị trực tiếp", "Hiển thị luồng sẵn"
        };

        public ActionCanVasNodeDialogViewModel(ActionCanVasNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _actionCanVasNode = node ?? throw new ArgumentNullException(nameof(node));

            _bodyWidth = node.BodyWidth;
            _bodyHeight = node.BodyHeight;
            _bodyBackgroundColorHex = node.BodyBackgroundColorHex;
            _bodyBorderColorHex = node.BodyBorderColorHex;
            _backgroundOpacityPercent = node.BackgroundOpacityPercent;
            _borderOpacityPercent = node.BorderOpacityPercent;
            _borderThickness = node.BorderThickness;
            _borderDashStyle = node.BorderDashStyle;
            _borderDashSpacing = node.BorderDashSpacing;
            _useUnifiedColors = node.UseUnifiedColors;
            _bodyBackgroundColorKey = node.BodyBackgroundColorHex;
            _bodyBorderColorKey = node.BodyBorderColorHex;

            _outputKey = node.OutputKey ?? "canvasData";
            _macroDataJson = node.MacroDataJson ?? "";
            _selectedPlaybackMode = node.PlaybackMode == MacroPlaybackMode.Repeat ? "Lặp lại" : "Chạy 1 lần";
            _repeatIntervalMs = node.RepeatIntervalMs;
            _repeatCount = node.RepeatCount;
            _selectedVisualPlaybackMode = VisualModeToString(node.VisualPlaybackMode);
            _countdownSeconds = node.CountdownSeconds;

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ActionCanVasNode.BodyWidth)) BodyWidth = node.BodyWidth;
                    else if (e.PropertyName == nameof(ActionCanVasNode.BodyHeight)) BodyHeight = node.BodyHeight;
                    else if (e.PropertyName == nameof(ActionCanVasNode.BodyBackgroundColorHex)) BodyBackgroundColorHex = node.BodyBackgroundColorHex;
                    else if (e.PropertyName == nameof(ActionCanVasNode.BodyBorderColorHex)) BodyBorderColorHex = node.BodyBorderColorHex;
                    else if (e.PropertyName == nameof(ActionCanVasNode.UseUnifiedColors)) UseUnifiedColors = node.UseUnifiedColors;
                    else if (e.PropertyName == nameof(ActionCanVasNode.BackgroundOpacityPercent)) BackgroundOpacityPercent = node.BackgroundOpacityPercent;
                    else if (e.PropertyName == nameof(ActionCanVasNode.BorderOpacityPercent)) BorderOpacityPercent = node.BorderOpacityPercent;
                    else if (e.PropertyName == nameof(ActionCanVasNode.BorderThickness)) BorderThickness = node.BorderThickness;
                    else if (e.PropertyName == nameof(ActionCanVasNode.BorderDashStyle)) BorderDashStyle = node.BorderDashStyle;
                    else if (e.PropertyName == nameof(ActionCanVasNode.BorderDashSpacing)) BorderDashSpacing = node.BorderDashSpacing;

                    else if (e.PropertyName == nameof(ActionCanVasNode.OutputKey)) OutputKey = node.OutputKey ?? "canvasData";
                    else if (e.PropertyName == nameof(ActionCanVasNode.MacroDataJson))
                    {
                        MacroDataJson = node.MacroDataJson ?? "";
                        OnPropertyChanged(nameof(CanExportJson));
                    }
                    else if (e.PropertyName == nameof(ActionCanVasNode.PlaybackMode))
                        SelectedPlaybackMode = node.PlaybackMode == MacroPlaybackMode.Repeat ? "Lặp lại" : "Chạy 1 lần";
                    else if (e.PropertyName == nameof(ActionCanVasNode.RepeatIntervalMs)) RepeatIntervalMs = node.RepeatIntervalMs;
                    else if (e.PropertyName == nameof(ActionCanVasNode.RepeatCount)) RepeatCount = node.RepeatCount;
                    else if (e.PropertyName == nameof(ActionCanVasNode.VisualPlaybackMode)) SelectedVisualPlaybackMode = VisualModeToString(node.VisualPlaybackMode);
                    else if (e.PropertyName == nameof(ActionCanVasNode.CountdownSeconds)) CountdownSeconds = node.CountdownSeconds;

                    OnNodePropertyChanged(e.PropertyName);
                };
            }
        }

        protected override string GetDefaultTitle() => "Thao tác canvas";

        partial void OnSelectedPlaybackModeChanged(string value) => OnPropertyChanged(nameof(IsRepeatVisible));
        partial void OnMacroDataJsonChanged(string value) => OnPropertyChanged(nameof(CanExportJson));

        partial void OnBodyBackgroundColorHexChanged(string value)
        {
            _actionCanVasNode.BodyBackgroundColorHex = NormalizeColorValue(value, _actionCanVasNode.BodyBackgroundColorHex);
        }

        partial void OnBodyBorderColorHexChanged(string value)
        {
            _actionCanVasNode.BodyBorderColorHex = NormalizeColorValue(value, _actionCanVasNode.BodyBorderColorHex);
        }

        partial void OnBodyBackgroundColorKeyChanged(string? value)
        {
            var resolved = ResolveColorToken(value);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                BodyBackgroundColorHex = resolved;
                _actionCanVasNode.BodyBackgroundColorHex = NormalizeColorValue(resolved, _actionCanVasNode.BodyBackgroundColorHex);
            }
        }

        partial void OnBodyBorderColorKeyChanged(string? value)
        {
            var resolved = ResolveColorToken(value);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                BodyBorderColorHex = resolved;
                _actionCanVasNode.BodyBorderColorHex = NormalizeColorValue(resolved, _actionCanVasNode.BodyBorderColorHex);
            }
        }

        partial void OnBackgroundOpacityPercentChanged(double value)
        {
            _actionCanVasNode.BackgroundOpacityPercent = value;
        }

        partial void OnUseUnifiedColorsChanged(bool value)
        {
            _actionCanVasNode.UseUnifiedColors = value;
        }

        partial void OnBorderOpacityPercentChanged(double value)
        {
            _actionCanVasNode.BorderOpacityPercent = value;
        }

        partial void OnBorderThicknessChanged(double value)
        {
            _actionCanVasNode.BorderThickness = value;
        }

        partial void OnBorderDashSpacingChanged(double value)
        {
            _actionCanVasNode.BorderDashSpacing = value;
        }

        partial void OnBorderDashStyleChanged(BorderDashStyle value)
        {
            _actionCanVasNode.BorderDashStyle = value;
        }

        protected override void OnSaveTitle()
        {
            bool needSync = false;

            if (_actionCanVasNode.Title != NodeTitle)
            { _actionCanVasNode.Title = NodeTitle; needSync = true; }

            if (Math.Abs(_actionCanVasNode.BodyWidth - BodyWidth) > 0.01)
            { _actionCanVasNode.BodyWidth = BodyWidth; needSync = true; }
            if (Math.Abs(_actionCanVasNode.BodyHeight - BodyHeight) > 0.01)
            { _actionCanVasNode.BodyHeight = BodyHeight; needSync = true; }

            var bgResolved = ResolveColorToken(BodyBackgroundColorKey) ?? BodyBackgroundColorHex;
            var borderResolved = ResolveColorToken(BodyBorderColorKey) ?? BodyBorderColorHex;
            var finalBg = NormalizeColorValue(bgResolved, _actionCanVasNode.BodyBackgroundColorHex);
            var finalBorder = NormalizeColorValue(borderResolved, _actionCanVasNode.BodyBorderColorHex);

            if (_actionCanVasNode.BodyBackgroundColorHex != finalBg)
            { _actionCanVasNode.BodyBackgroundColorHex = finalBg; needSync = true; }
            if (_actionCanVasNode.BodyBorderColorHex != finalBorder)
            { _actionCanVasNode.BodyBorderColorHex = finalBorder; needSync = true; }

            if (_actionCanVasNode.UseUnifiedColors != UseUnifiedColors)
            { _actionCanVasNode.UseUnifiedColors = UseUnifiedColors; needSync = true; }
            if (Math.Abs(_actionCanVasNode.BackgroundOpacityPercent - BackgroundOpacityPercent) > 0.01)
            { _actionCanVasNode.BackgroundOpacityPercent = BackgroundOpacityPercent; needSync = true; }
            if (Math.Abs(_actionCanVasNode.BorderOpacityPercent - BorderOpacityPercent) > 0.01)
            { _actionCanVasNode.BorderOpacityPercent = BorderOpacityPercent; needSync = true; }
            if (Math.Abs(_actionCanVasNode.BorderThickness - BorderThickness) > 0.01)
            { _actionCanVasNode.BorderThickness = BorderThickness; needSync = true; }

            if (_actionCanVasNode.BorderDashStyle != BorderDashStyle)
            { _actionCanVasNode.BorderDashStyle = BorderDashStyle; needSync = true; }
            if (Math.Abs(_actionCanVasNode.BorderDashSpacing - BorderDashSpacing) > 0.01)
            { _actionCanVasNode.BorderDashSpacing = BorderDashSpacing; needSync = true; }

            if (_actionCanVasNode.OutputKey != OutputKey)
            { _actionCanVasNode.OutputKey = OutputKey; needSync = true; }
            if (_actionCanVasNode.MacroDataJson != MacroDataJson)
            { _actionCanVasNode.MacroDataJson = MacroDataJson; needSync = true; }
            var newPlaybackMode = SelectedPlaybackMode == "Lặp lại" ? MacroPlaybackMode.Repeat : MacroPlaybackMode.Once;
            if (_actionCanVasNode.PlaybackMode != newPlaybackMode)
            { _actionCanVasNode.PlaybackMode = newPlaybackMode; needSync = true; }
            if (_actionCanVasNode.RepeatIntervalMs != RepeatIntervalMs)
            { _actionCanVasNode.RepeatIntervalMs = RepeatIntervalMs; needSync = true; }
            if (_actionCanVasNode.RepeatCount != RepeatCount)
            { _actionCanVasNode.RepeatCount = RepeatCount; needSync = true; }
            var newVisual = StringToVisualMode(SelectedVisualPlaybackMode);
            if (_actionCanVasNode.VisualPlaybackMode != newVisual)
            { _actionCanVasNode.VisualPlaybackMode = newVisual; needSync = true; }
            if (_actionCanVasNode.CountdownSeconds != CountdownSeconds)
            { _actionCanVasNode.CountdownSeconds = CountdownSeconds; needSync = true; }

            if (needSync)
                _host.RequestSyncDataPanels(immediate: true);
        }

        // ─── Helpers ───

        private static string VisualModeToString(VisualPlaybackMode m) => m switch
        {
            VisualPlaybackMode.Silent => "Không hiển thị",
            VisualPlaybackMode.Ghost => "Hiển thị luồng sẵn",
            _ => "Hiển thị trực tiếp"
        };

        private static VisualPlaybackMode StringToVisualMode(string s) => s switch
        {
            "Không hiển thị" => VisualPlaybackMode.Silent,
            "Hiển thị luồng sẵn" => VisualPlaybackMode.Ghost,
            _ => VisualPlaybackMode.Live
        };

        private static string? ResolveColorToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var token = value.Trim();
            if (token.StartsWith("#", StringComparison.Ordinal)) return token;
            var resource = Application.Current?.TryFindResource(token);
            if (resource is SolidColorBrush b)
                return $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}";
            if (resource is Color c)
                return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            return token;
        }

        private static string NormalizeColorValue(string? input, string fallback)
        {
            if (string.IsNullOrWhiteSpace(input)) return fallback;
            var value = input.Trim();

            if (value.StartsWith('#'))
            {
                if (value.Length is not (7 or 9)) return fallback;
                for (var i = 1; i < value.Length; i++)
                {
                    if (!Uri.IsHexDigit(value[i])) return fallback;
                }
                return value.ToUpperInvariant();
            }

            return value;
        }

        public sealed class BodyColorOption
        {
            public BodyColorOption(string key, string displayName)
            {
                Key = key;
                DisplayName = displayName;
            }

            public string Key { get; }
            public string DisplayName { get; }
        }

        public sealed class BorderDashStyleOption
        {
            public BorderDashStyleOption(BorderDashStyle style, string displayName)
            {
                Style = style;
                DisplayName = displayName;
            }

            public BorderDashStyle Style { get; }
            public string DisplayName { get; }
        }
    }
}
