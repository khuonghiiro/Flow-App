using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
namespace FlowMy.Models.Nodes;

public enum KeyValueBridgePollUnit
{
    Milliseconds = 0,
    Seconds = 1,
    Minutes = 2
}

public sealed class KeyValueBridgeAppendSource
{
    public string SourceNodeId { get; set; } = string.Empty;
    public string? SourceOutputKey { get; set; }
}

/// <summary>
/// Pass: truyền key trong flow. Get: đọc giá trị từ kho runtime theo key (có poll interval).
/// </summary>
public sealed class KeyValueBridgeNode : WorkflowNode, INotifyPropertyChanged
{
    private bool _isPassKeyModeH = true;
    private string _kvChannelKey = string.Empty;
    private string? _selectedSourceBridgeNodeId;
    private int _pollIntervalValue;
    private KeyValueBridgePollUnit _pollIntervalUnit = KeyValueBridgePollUnit.Milliseconds;
    private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
    private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
    private string? _titleColorKey;
    private bool _enableDataCleanup;
    private string? _cleanupTargetBridgeNodeId;
    private string _cleanupTargetKey = string.Empty;
    private bool _cleanupClearAllNodeData;
    private string _cleanupArrayFilterField = string.Empty;
    private string _cleanupArrayFilterValue = string.Empty;
    private bool _cleanupRemoveAllMatchedArrayItems;
    private string? _cleanupFilterFieldSourceNodeId;
    private string? _cleanupFilterFieldSourceOutputKey;
    private string? _cleanupKeySourceNodeId;
    private string? _cleanupKeySourceOutputKey;
    private string? _cleanupFilterValueSourceNodeId;
    private string? _cleanupFilterValueSourceOutputKey;
    private string? _cleanupTriggerSourceNodeId;
    private string? _cleanupTriggerSourceOutputKey;
    private string _cleanupTriggerExpectedValue = "true";

    public event PropertyChangedEventHandler? PropertyChanged;

    public KeyValueBridgeNode()
    {
        Type = NodeType.KeyValueBridge;
        Title = "KeyValue Bridge";
        ColorKey = "SeaFoam";

        Ports.Add(new NodePort
        {
            IsInput = true,
            Position = PortPosition.Left,
            IsVisible = true,
            ColorKey = "Info"
        });
        Ports.Add(new NodePort
        {
            IsInput = false,
            Position = PortPosition.Right,
            IsVisible = true,
            ColorKey = "SunsetOrange"
        });

        RebuildDataPorts();
    }

    /// <summary>Bật = Pass (ghi KV + cổng flow); tắt = Get (đọc KV theo interval, không cần nối cổng IN).</summary>
    public bool IsPassKeyMode
    {
        get => _isPassKeyModeH;
        set
        {
            if (_isPassKeyModeH == value) return;
            _isPassKeyModeH = value;
            OnPropertyChanged();
            RebuildDataPorts();
            RefreshFlowPortsVisibility();
        }
    }

    /// <summary>Key trong runtime store (dialog + JSON <c>key</c>).</summary>
    public string KvChannelKey
    {
        get => _kvChannelKey;
        set
        {
            var s = value ?? string.Empty;
            if (_kvChannelKey == s) return;
            _kvChannelKey = s;
            OnPropertyChanged();
        }
    }

    public string? SelectedSourceBridgeNodeId
    {
        get => _selectedSourceBridgeNodeId;
        set
        {
            if (_selectedSourceBridgeNodeId == value) return;
            _selectedSourceBridgeNodeId = value;
            OnPropertyChanged();
        }
    }

    public int PollIntervalValue
    {
        get => _pollIntervalValue;
        set
        {
            var v = Math.Max(0, value);
            if (_pollIntervalValue == v) return;
            _pollIntervalValue = v;
            OnPropertyChanged();
        }
    }

    public KeyValueBridgePollUnit PollIntervalUnit
    {
        get => _pollIntervalUnit;
        set
        {
            if (_pollIntervalUnit == value) return;
            _pollIntervalUnit = value;
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

    /// <summary>
    /// Khi bật, node hoạt động ở chế độ "cleanup" KV data (không chạy Pass/Get).
    /// </summary>
    public bool EnableDataCleanup
    {
        get => _enableDataCleanup;
        set
        {
            if (_enableDataCleanup == value) return;
            _enableDataCleanup = value;
            OnPropertyChanged();
            RefreshFlowPortsVisibility();
        }
    }

    /// <summary>Node KeyValueBridge (Pass mode) đích cần xóa data.</summary>
    public string? CleanupTargetBridgeNodeId
    {
        get => _cleanupTargetBridgeNodeId;
        set
        {
            if (_cleanupTargetBridgeNodeId == value) return;
            _cleanupTargetBridgeNodeId = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Key cần xóa. Để trống + bật CleanupClearAllNodeData = xóa toàn bộ node.</summary>
    public string CleanupTargetKey
    {
        get => _cleanupTargetKey;
        set
        {
            var s = value ?? string.Empty;
            if (_cleanupTargetKey == s) return;
            _cleanupTargetKey = s;
            OnPropertyChanged();
        }
    }

    /// <summary>Khi cleanup chạy (trigger khớp): xóa toàn bộ key runtime của node KeyValueBridge đích; bỏ qua key/filter chi tiết.</summary>
    public bool CleanupClearAllNodeData
    {
        get => _cleanupClearAllNodeData;
        set
        {
            if (_cleanupClearAllNodeData == value) return;
            _cleanupClearAllNodeData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Field dùng để lọc item trong mảng value của key.</summary>
    public string CleanupArrayFilterField
    {
        get => _cleanupArrayFilterField;
        set
        {
            var s = value ?? string.Empty;
            if (_cleanupArrayFilterField == s) return;
            _cleanupArrayFilterField = s;
            OnPropertyChanged();
        }
    }

    /// <summary>Giá trị field cần match khi lọc item mảng.</summary>
    public string CleanupArrayFilterValue
    {
        get => _cleanupArrayFilterValue;
        set
        {
            var s = value ?? string.Empty;
            if (_cleanupArrayFilterValue == s) return;
            _cleanupArrayFilterValue = s;
            OnPropertyChanged();
        }
    }

    /// <summary>True = xóa tất cả item khớp; false = chỉ xóa 1 item.</summary>
    public bool CleanupRemoveAllMatchedArrayItems
    {
        get => _cleanupRemoveAllMatchedArrayItems;
        set
        {
            if (_cleanupRemoveAllMatchedArrayItems == value) return;
            _cleanupRemoveAllMatchedArrayItems = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Node nguồn cấp động filter field (tên trường JSON).</summary>
    public string? CleanupFilterFieldSourceNodeId
    {
        get => _cleanupFilterFieldSourceNodeId;
        set
        {
            if (_cleanupFilterFieldSourceNodeId == value) return;
            _cleanupFilterFieldSourceNodeId = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Output key nguồn cấp động filter field.</summary>
    public string? CleanupFilterFieldSourceOutputKey
    {
        get => _cleanupFilterFieldSourceOutputKey;
        set
        {
            if (_cleanupFilterFieldSourceOutputKey == value) return;
            _cleanupFilterFieldSourceOutputKey = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Node nguồn cấp động key cần xóa.</summary>
    public string? CleanupKeySourceNodeId
    {
        get => _cleanupKeySourceNodeId;
        set
        {
            if (_cleanupKeySourceNodeId == value) return;
            _cleanupKeySourceNodeId = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Output key nguồn cấp động key cần xóa.</summary>
    public string? CleanupKeySourceOutputKey
    {
        get => _cleanupKeySourceOutputKey;
        set
        {
            if (_cleanupKeySourceOutputKey == value) return;
            _cleanupKeySourceOutputKey = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Node nguồn cấp động filter value (xóa item trong mảng).</summary>
    public string? CleanupFilterValueSourceNodeId
    {
        get => _cleanupFilterValueSourceNodeId;
        set
        {
            if (_cleanupFilterValueSourceNodeId == value) return;
            _cleanupFilterValueSourceNodeId = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Output key nguồn cấp động filter value.</summary>
    public string? CleanupFilterValueSourceOutputKey
    {
        get => _cleanupFilterValueSourceOutputKey;
        set
        {
            if (_cleanupFilterValueSourceOutputKey == value) return;
            _cleanupFilterValueSourceOutputKey = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Node nguồn trigger cleanup.</summary>
    public string? CleanupTriggerSourceNodeId
    {
        get => _cleanupTriggerSourceNodeId;
        set
        {
            if (_cleanupTriggerSourceNodeId == value) return;
            _cleanupTriggerSourceNodeId = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Output key của node trigger.</summary>
    public string? CleanupTriggerSourceOutputKey
    {
        get => _cleanupTriggerSourceOutputKey;
        set
        {
            if (_cleanupTriggerSourceOutputKey == value) return;
            _cleanupTriggerSourceOutputKey = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Giá trị kích hoạt cleanup: true/false/1/0.</summary>
    public string CleanupTriggerExpectedValue
    {
        get => _cleanupTriggerExpectedValue;
        set
        {
            var s = string.IsNullOrWhiteSpace(value) ? "true" : value.Trim();
            if (_cleanupTriggerExpectedValue == s) return;
            _cleanupTriggerExpectedValue = s;
            OnPropertyChanged();
        }
    }

    /// <summary>Runtime outputs cho mirror scoped + data panel.</summary>
    public Dictionary<string, object?> ResolvedOutputs { get; } = new(StringComparer.OrdinalIgnoreCase);

    public object ResolvedOutputsSyncRoot { get; } = new();

    /// <summary>
    /// Danh sách nguồn append bổ sung cho input "Value (append)".
    /// Mỗi nguồn chỉ append khi node thật sự được execute trong flow.
    /// </summary>
    public List<KeyValueBridgeAppendSource> AdditionalAppendSources { get; set; } = new();

    /// <summary>Cổng flow IN hiện khi Pass hoặc khi bật xóa KV (để nối luồng kích hoạt cleanup).</summary>
    public bool ShouldShowFlowInputPort => IsPassKeyMode || EnableDataCleanup;

    public void RefreshFlowPortsVisibility()
    {
        var inputPort = Ports?.FirstOrDefault(p => p.IsInput);
        if (inputPort != null)
            inputPort.IsVisible = ShouldShowFlowInputPort;
    }

    public void RebuildDataPorts()
    {
        DynamicInputs.Clear();
        DynamicOutputs.Clear();

        if (IsPassKeyMode)
        {
            // Key Identifier dùng để gom nhiều nhánh song song vào cùng một mảng trong KV store.
            // Nếu người dùng không chọn mapping này thì executor sẽ fallback sang `KvChannelKey` (textbox ở dialog).
            DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "kvChannelKeyIn",
                DisplayName = "Key Identifier",
                ConvertType = WorkflowDataType.String
            });

            // Value sẽ được append vào mảng theo Key Identifier.
            DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "keyIn",
                DisplayName = "Value (append)",
                ConvertType = WorkflowDataType.String
            });
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "key",
                DisplayName = "Key",
                OutputType = WorkflowDataType.String,
                IsMultiple = false
            });
        }
        else
        {
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "value",
                DisplayName = "Value",
                OutputType = WorkflowDataType.String,
                IsMultiple = false
            });
        }
    }

    public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
