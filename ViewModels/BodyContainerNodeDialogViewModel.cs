using CommunityToolkit.Mvvm.ComponentModel;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

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
    private double _backgroundOpacityPercent = 35;

    [ObservableProperty]
    private bool _lockInnerNodes;

    [ObservableProperty]
    private string? _bodyBackgroundColorKey;

    [ObservableProperty]
    private string? _bodyBorderColorKey;

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
    }

    protected override string GetDefaultTitle() => "Body Container";

    protected override bool SupportsReuseRoutes => false;

    protected override void OnSaveTitle()
    {
        _nodeRef.BodyBackgroundColorHex = NormalizeHex(BodyBackgroundColorHex, _nodeRef.BodyBackgroundColorHex);
        _nodeRef.BodyBorderColorHex = NormalizeHex(BodyBorderColorHex, _nodeRef.BodyBorderColorHex);
        _nodeRef.UseUnifiedColors = UseUnifiedColors;
        _nodeRef.BackgroundOpacityPercent = BackgroundOpacityPercent;
        _nodeRef.LockInnerNodes = LockInnerNodes;
        _nodeRef.BodyBackgroundColorHex = NormalizeHex(BodyBackgroundColorKey, _nodeRef.BodyBackgroundColorHex);
        _nodeRef.BodyBorderColorHex = NormalizeHex(BodyBorderColorKey, _nodeRef.BodyBorderColorHex);
        _nodeRef.NotifyTitleChanged();
        _host.RequestSyncDataPanels(immediate: true);
    }

    partial void OnBodyBackgroundColorHexChanged(string value)
    {
        BodyBackgroundColorKey = value;
        _nodeRef.BodyBackgroundColorHex = NormalizeHex(value, _nodeRef.BodyBackgroundColorHex);
    }

    partial void OnBodyBorderColorHexChanged(string value)
    {
        BodyBorderColorKey = value;
        _nodeRef.BodyBorderColorHex = NormalizeHex(value, _nodeRef.BodyBorderColorHex);
    }

    partial void OnBodyBackgroundColorKeyChanged(string? value)
    {
        var resolved = ResolveColorToken(value);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            BodyBackgroundColorHex = resolved;
            _nodeRef.BodyBackgroundColorHex = NormalizeHex(resolved, _nodeRef.BodyBackgroundColorHex);
        }
    }

    partial void OnBodyBorderColorKeyChanged(string? value)
    {
        var resolved = ResolveColorToken(value);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            BodyBorderColorHex = resolved;
            _nodeRef.BodyBorderColorHex = NormalizeHex(resolved, _nodeRef.BodyBorderColorHex);
        }
    }

    partial void OnUseUnifiedColorsChanged(bool value)
    {
        _nodeRef.UseUnifiedColors = value;
    }

    partial void OnBackgroundOpacityPercentChanged(double value)
    {
        _nodeRef.BackgroundOpacityPercent = value;
    }

    partial void OnLockInnerNodesChanged(bool value)
    {
        _nodeRef.LockInnerNodes = value;
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

    private static string NormalizeHex(string? input, string fallback)
    {
        if (string.IsNullOrWhiteSpace(input)) return fallback;
        var value = input.Trim();
        if (!value.StartsWith('#')) value = $"#{value}";
        if (value.Length is not (7 or 9)) return fallback;
        for (var i = 1; i < value.Length; i++)
        {
            if (!Uri.IsHexDigit(value[i])) return fallback;
        }
        return value.ToUpperInvariant();
    }
}
