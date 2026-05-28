using FlowMy.Models;
using System;

namespace FlowMy.Models.Nodes;

public enum BorderDashStyle
{
    Solid,
    Dash,
    Dot,
    DashDot,
    DashDotDot
}

public sealed class BodyContainerNode : WorkflowNode
{
    private double _bodyWidth = 800;
    private double _bodyHeight = 400;
    private string _bodyBackgroundColorHex = "#5A6B7280";
    private string _bodyBorderColorHex = "#FF6B7280";
    private bool _useUnifiedColors = true;
    private double _backgroundOpacityPercent = 10;
    private bool _lockInnerNodes;
    private double _borderOpacityPercent = 100;
    private double _borderThickness = 2;
    private double _borderDashSpacing = 3;
    private BorderDashStyle _borderDashStyle = BorderDashStyle.Dash;
    private double _iconOpacityPercent = 62;
    private bool _lockCanvasSize;
    private double _lockedZoomLevel = 1.0;

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

    public double BorderOpacityPercent
    {
        get => _borderOpacityPercent;
        set
        {
            var normalized = Math.Max(0, Math.Min(100, value));
            if (Math.Abs(_borderOpacityPercent - normalized) < 0.01) return;
            _borderOpacityPercent = normalized;
            OnPropertyChanged();
        }
    }

    public double BorderThickness
    {
        get => _borderThickness;
        set
        {
            var normalized = Math.Max(0.5, Math.Min(10, value));
            if (Math.Abs(_borderThickness - normalized) < 0.01) return;
            _borderThickness = normalized;
            OnPropertyChanged();
        }
    }

    public double BorderDashSpacing
    {
        get => _borderDashSpacing;
        set
        {
            var normalized = Math.Max(0, Math.Min(20, value));
            if (Math.Abs(_borderDashSpacing - normalized) < 0.01) return;
            _borderDashSpacing = normalized;
            OnPropertyChanged();
        }
    }

    public BorderDashStyle BorderDashStyle
    {
        get => _borderDashStyle;
        set
        {
            if (_borderDashStyle == value) return;
            _borderDashStyle = value;
            OnPropertyChanged();
        }
    }

    public double IconOpacityPercent
    {
        get => _iconOpacityPercent;
        set
        {
            var normalized = Math.Max(0, Math.Min(100, value));
            if (Math.Abs(_iconOpacityPercent - normalized) < 0.01) return;
            _iconOpacityPercent = normalized;
            OnPropertyChanged();
        }
    }

    public bool LockCanvasSize
    {
        get => _lockCanvasSize;
        set
        {
            if (_lockCanvasSize == value) return;
            _lockCanvasSize = value;
            OnPropertyChanged();
        }
    }

    public double LockedZoomLevel
    {
        get => _lockedZoomLevel;
        set
        {
            if (Math.Abs(_lockedZoomLevel - value) < 0.001) return;
            _lockedZoomLevel = value;
            OnPropertyChanged();
        }
    }
}
