using FlowMy.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace FlowMy.Models.Nodes;

public sealed class FlowOverwriteMapping
{
    public string SourceNodeId { get; set; } = string.Empty;
    public string? SourceOutputKey { get; set; }
}

public sealed class FlowOverwriteNode : WorkflowNode, INotifyPropertyChanged
{
    private string _outputKey = "outputKey";
    private bool _appendMode;
    private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
    private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
    private string? _titleColorKey;

    public event PropertyChangedEventHandler? PropertyChanged;

    public FlowOverwriteNode()
    {
        Type = NodeType.FlowOverwrite;
        Title = "Flow Overwrite";
        ColorKey = "KiwiGreen";

        Ports.Add(new NodePort
        {
            Id = Guid.NewGuid().ToString(),
            IsInput = true,
            Position = PortPosition.Left,
            IsVisible = true,
            ColorKey = "Info"
        });
        Ports.Add(new NodePort
        {
            Id = Guid.NewGuid().ToString(),
            IsInput = false,
            Position = PortPosition.Right,
            IsVisible = true,
            ColorKey = "SunsetOrange"
        });

        RebuildDynamicOutputs();
    }

    public string OutputKey
    {
        get => _outputKey;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "outputKey" : value.Trim();
            if (_outputKey == normalized) return;
            _outputKey = normalized;
            RebuildDynamicOutputs();
            OnPropertyChanged();
        }
    }

    public bool AppendMode
    {
        get => _appendMode;
        set
        {
            if (_appendMode == value) return;
            _appendMode = value;
            OnPropertyChanged();
        }
    }

    public List<FlowOverwriteMapping> Mappings { get; set; } = new();

    public Dictionary<string, object?> ResolvedOutputs { get; } = new(StringComparer.OrdinalIgnoreCase);
    public object ResolvedOutputsSyncRoot { get; } = new();

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

    public TextBlock? TitleTextBlockUI { get; set; }

    public void RebuildDynamicOutputs()
    {
        DynamicOutputs.Clear();
        DynamicOutputs.Add(new WorkflowDynamicDataPort
        {
            Key = OutputKey,
            DisplayName = OutputKey,
            IsMultiple = AppendMode,
            OutputType = WorkflowDataType.String
        });
    }

    public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
