using CommunityToolkit.Mvvm.ComponentModel;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace FlowMy.ViewModels;

public sealed partial class BodyContainerNodeDialogViewModel : BaseNodeDialogViewModel
{
    private readonly BodyContainerNode _nodeRef;

    [ObservableProperty]
    private string _bodyBackgroundColorHex = "#5A6B7280";

    [ObservableProperty]
    private string _bodyBorderColorHex = "#FF6B7280";

    [ObservableProperty]
    private bool _useUnifiedColors = true;

    [ObservableProperty]
    private double _backgroundOpacityPercent = 20;

    [ObservableProperty]
    private bool _lockInnerNodes;

    [ObservableProperty]
    private string? _bodyBackgroundColorKey;

    [ObservableProperty]
    private string? _bodyBorderColorKey;

    [ObservableProperty]
    private double _borderOpacityPercent = 100;

    [ObservableProperty]
    private double _borderThickness = 2;

    [ObservableProperty]
    private double _borderDashSpacing = 3;

    [ObservableProperty]
    private BorderDashStyle _borderDashStyle = BorderDashStyle.Dash;

    [ObservableProperty]
    private double _iconOpacityPercent = 62;

    [ObservableProperty]
    private bool _lockCanvasSize;

    [ObservableProperty]
    private double _lockedZoomLevel = 1.0;

    [ObservableProperty]
    private double _lockedX;

    [ObservableProperty]
    private double _lockedY;

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

    public BodyContainerNodeDialogViewModel(BodyContainerNode node, IWorkflowEditorHost host)
        : base(node, host)
    {
        _nodeRef = node;
        _bodyBackgroundColorHex = node.BodyBackgroundColorHex;
        _bodyBorderColorHex = node.BodyBorderColorHex;
        _useUnifiedColors = node.UseUnifiedColors;
        _backgroundOpacityPercent = node.BackgroundOpacityPercent;
        _lockInnerNodes = node.LockInnerNodes;
        _bodyBackgroundColorKey = node.BodyBackgroundColorHex;
        _bodyBorderColorKey = node.BodyBorderColorHex;
        _borderOpacityPercent = node.BorderOpacityPercent;
        _borderThickness = node.BorderThickness;
        _borderDashSpacing = node.BorderDashSpacing;
        _borderDashStyle = node.BorderDashStyle;
        _iconOpacityPercent = node.IconOpacityPercent;
        _lockCanvasSize = node.LockCanvasSize;
        _lockedZoomLevel = node.LockedZoomLevel;
        _lockedX = node.LockedX;
        _lockedY = node.LockedY;
    }

    protected override string GetDefaultTitle() => "Body Container";

    protected override bool SupportsReuseRoutes => false;

    protected override void OnSaveTitle()
    {
        var bgResolved = ResolveColorToken(BodyBackgroundColorKey) ?? BodyBackgroundColorHex;
        var borderResolved = ResolveColorToken(BodyBorderColorKey) ?? BodyBorderColorHex;
        _nodeRef.BodyBackgroundColorHex = NormalizeColorValue(bgResolved, _nodeRef.BodyBackgroundColorHex);
        _nodeRef.BodyBorderColorHex = NormalizeColorValue(borderResolved, _nodeRef.BodyBorderColorHex);
        _nodeRef.UseUnifiedColors = UseUnifiedColors;
        _nodeRef.BackgroundOpacityPercent = BackgroundOpacityPercent;
        _nodeRef.LockInnerNodes = LockInnerNodes;
        _nodeRef.BorderOpacityPercent = BorderOpacityPercent;
        _nodeRef.BorderThickness = BorderThickness;
        _nodeRef.BorderDashSpacing = BorderDashSpacing;
        _nodeRef.BorderDashStyle = BorderDashStyle;
        _nodeRef.IconOpacityPercent = IconOpacityPercent;
        _nodeRef.LockCanvasSize = LockCanvasSize;
        _nodeRef.LockedZoomLevel = LockedZoomLevel;
        _nodeRef.LockedX = LockedX;
        _nodeRef.LockedY = LockedY;
        _nodeRef.NotifyTitleChanged();
        RefreshBodyVisualImmediate();
        _host.RequestSyncDataPanels(immediate: true);
    }

    partial void OnBodyBackgroundColorHexChanged(string value)
    {
        _nodeRef.BodyBackgroundColorHex = NormalizeColorValue(value, _nodeRef.BodyBackgroundColorHex);
        RefreshBodyVisualImmediate();
    }

    partial void OnBodyBorderColorHexChanged(string value)
    {
        _nodeRef.BodyBorderColorHex = NormalizeColorValue(value, _nodeRef.BodyBorderColorHex);
        RefreshBodyVisualImmediate();
    }

    partial void OnBodyBackgroundColorKeyChanged(string? value)
    {
        var resolved = ResolveColorToken(value);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            BodyBackgroundColorHex = resolved;
            _nodeRef.BodyBackgroundColorHex = NormalizeColorValue(resolved, _nodeRef.BodyBackgroundColorHex);
            RefreshBodyVisualImmediate();
        }
    }

    partial void OnBodyBorderColorKeyChanged(string? value)
    {
        var resolved = ResolveColorToken(value);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            BodyBorderColorHex = resolved;
            _nodeRef.BodyBorderColorHex = NormalizeColorValue(resolved, _nodeRef.BodyBorderColorHex);
            RefreshBodyVisualImmediate();
        }
    }

    partial void OnUseUnifiedColorsChanged(bool value)
    {
        _nodeRef.UseUnifiedColors = value;
        RefreshBodyVisualImmediate();
    }

    partial void OnBackgroundOpacityPercentChanged(double value)
    {
        _nodeRef.BackgroundOpacityPercent = value;
        RefreshBodyVisualImmediate();
    }

    partial void OnLockInnerNodesChanged(bool value)
    {
        _nodeRef.LockInnerNodes = value;
        RefreshBodyVisualImmediate();
    }

    partial void OnBorderOpacityPercentChanged(double value)
    {
        _nodeRef.BorderOpacityPercent = value;
        RefreshBodyVisualImmediate();
    }

    partial void OnBorderThicknessChanged(double value)
    {
        _nodeRef.BorderThickness = value;
        RefreshBodyVisualImmediate();
    }

    partial void OnBorderDashSpacingChanged(double value)
    {
        _nodeRef.BorderDashSpacing = value;
        RefreshBodyVisualImmediate();
    }

    partial void OnBorderDashStyleChanged(BorderDashStyle value)
    {
        _nodeRef.BorderDashStyle = value;
        RefreshBodyVisualImmediate();
    }

    partial void OnIconOpacityPercentChanged(double value)
    {
        _nodeRef.IconOpacityPercent = value;
        RefreshBodyVisualImmediate();
    }

    partial void OnLockCanvasSizeChanged(bool value)
    {
        _nodeRef.LockCanvasSize = value;
        // Hoàn tác logic LockCanvasSize vì gây lỗi
        // Tính năng này sẽ được implement lại sau
        RefreshBodyVisualImmediate();
    }

    partial void OnLockedZoomLevelChanged(double value)
    {
        _nodeRef.LockedZoomLevel = value;
        RefreshBodyVisualImmediate();
    }

    private static string? ResolveColorToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var token = value.Trim();
        if (token.StartsWith("#", StringComparison.Ordinal)) return token;
        var resource = Application.Current.TryFindResource(token);
        if (resource is SolidColorBrush b)
            return $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}";
        if (resource is Color c)
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        return token;
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

        // Allow resource key values such as PrimaryBrush, DangerBrush, etc.
        return value;
    }

    private void RefreshBodyVisualImmediate()
    {
        BodyContainerControl.RefreshVisualFromNode(_nodeRef);
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
