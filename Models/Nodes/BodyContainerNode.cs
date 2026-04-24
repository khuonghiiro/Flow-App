using FlowMy.Models;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models.Nodes;

public sealed class BodyContainerNode : WorkflowNode, INotifyPropertyChanged
{
    private double _bodyWidth = 800;
    private double _bodyHeight = 400;
    private string _bodyBackgroundColorHex = "#5A6B7280";
    private string _bodyBorderColorHex = "#FF6B7280";
    private bool _useUnifiedColors = true;
    private double _backgroundOpacityPercent = 35;
    private bool _lockInnerNodes;
    private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
    private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
    private string? _titleColorKey;

    public BodyContainerNode()
    {
        Type = NodeType.BodyContainer;
        Title = "Body Container";
        ColorKey = "CharcoalMist";
    }

    public double BodyWidth
    {
        get => _bodyWidth;
        set
        {
            var normalized = value < 200 ? 200 : value;
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
            var normalized = value < 200 ? 200 : value;
            if (_bodyHeight == normalized) return;
            _bodyHeight = normalized;
            OnPropertyChanged();
        }
    }

    public string BodyBackgroundColorHex
    {
        get => _bodyBackgroundColorHex;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "#5A6B7280" : value.Trim();
            if (_bodyBackgroundColorHex == normalized) return;
            _bodyBackgroundColorHex = normalized;
            OnPropertyChanged();
        }
    }

    public string BodyBorderColorHex
    {
        get => _bodyBorderColorHex;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "#FF6B7280" : value.Trim();
            if (_bodyBorderColorHex == normalized) return;
            _bodyBorderColorHex = normalized;
            OnPropertyChanged();
        }
    }

    public bool UseUnifiedColors
    {
        get => _useUnifiedColors;
        set
        {
            if (_useUnifiedColors == value) return;
            _useUnifiedColors = value;
            OnPropertyChanged();
        }
    }

    public double BackgroundOpacityPercent
    {
        get => _backgroundOpacityPercent;
        set
        {
            var normalized = Math.Max(0, Math.Min(100, value));
            if (Math.Abs(_backgroundOpacityPercent - normalized) < 0.01) return;
            _backgroundOpacityPercent = normalized;
            OnPropertyChanged();
        }
    }

    public bool LockInnerNodes
    {
        get => _lockInnerNodes;
        set
        {
            if (_lockInnerNodes == value) return;
            _lockInnerNodes = value;
            OnPropertyChanged();
        }
    }

    public TitleDisplayMode TitleDisplayMode
    {
        get => _titleDisplayMode;
        set
        {
            if (_titleDisplayMode == value) return;
            _titleDisplayMode = value;
            OnPropertyChanged();
        }
    }

    public TitleColorMode TitleColorMode
    {
        get => _titleColorMode;
        set
        {
            if (_titleColorMode == value) return;
            _titleColorMode = value;
            OnPropertyChanged();
        }
    }

    public string? TitleColorKey
    {
        get => _titleColorKey;
        set
        {
            if (_titleColorKey == value) return;
            _titleColorKey = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));
}
