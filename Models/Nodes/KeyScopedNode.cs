using System.Linq;
namespace FlowMy.Models.Nodes;

public enum KeyScopedPollUnit
{
    Milliseconds = 0,
    Seconds = 1,
    Minutes = 2
}

/// <summary>
/// Kho key→giá trị scoped theo <c>ExecutionId</c>. Ghi: port IN+OUT; đọc: chỉ OUT, optional delay.
/// </summary>
public sealed class KeyScopedNode : WorkflowNode
{
    private bool _isWriteMode = true;
    private string _staticKey = string.Empty;
    private int _pollTimeValue;
    private KeyScopedPollUnit _pollUnit = KeyScopedPollUnit.Milliseconds;
    private string _storeJson = "{}";

    public KeyScopedNode()
    {
        Type = NodeType.KeyScopedStore;
        Title = "Key Scoped";

        DynamicInputs.Add(new WorkflowDynamicDataPort
        {
            Key = "key",
            DisplayName = "Key định danh",
            ConvertType = WorkflowDataType.String
        });
        DynamicInputs.Add(new WorkflowDynamicDataPort
        {
            Key = "value",
            DisplayName = "Giá trị",
            ConvertType = WorkflowDataType.String
        });

        DynamicOutputs.Add(new WorkflowDynamicDataPort
        {
            Key = "store",
            DisplayName = "Store JSON",
            OutputType = WorkflowDataType.String,
            IsMultiple = false
        });
        DynamicOutputs.Add(new WorkflowDynamicDataPort
        {
            Key = "lastKey",
            DisplayName = "Key ghi cuối",
            OutputType = WorkflowDataType.String,
            IsMultiple = false
        });
        DynamicOutputs.Add(new WorkflowDynamicDataPort
        {
            Key = "lastValue",
            DisplayName = "Value ghi cuối",
            OutputType = WorkflowDataType.String,
            IsMultiple = false
        });
    }

    public bool IsWriteMode
    {
        get => _isWriteMode;
        set
        {
            if (_isWriteMode == value) return;
            _isWriteMode = value;
            OnPropertyChanged();
        }
    }

    public string StaticKey
    {
        get => _staticKey;
        set
        {
            if (_staticKey == value) return;
            _staticKey = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public int PollTimeValue
    {
        get => _pollTimeValue;
        set
        {
            var v = Math.Max(0, value);
            if (_pollTimeValue == v) return;
            _pollTimeValue = v;
            OnPropertyChanged();
        }
    }

    public KeyScopedPollUnit PollUnit
    {
        get => _pollUnit;
        set
        {
            if (_pollUnit == value) return;
            _pollUnit = value;
            OnPropertyChanged();
        }
    }

    /// <summary>JSON kho sau ghi/đọc (runtime + có thể lưu workflow).</summary>
    public string StoreJson
    {
        get => _storeJson;
        set
        {
            var s = value ?? "{}";
            if (_storeJson == s) return;
            _storeJson = s;
            OnPropertyChanged();
            SyncStoreToDynamicOutput();
        }
    }

    public string? LastWrittenKey { get; set; }
    public string? LastWrittenValue { get; set; }

    public void SyncStoreToDynamicOutput()
    {
        if (DynamicOutputs == null) return;
        var port = DynamicOutputs.FirstOrDefault(o =>
            string.Equals(o.Key, "store", StringComparison.OrdinalIgnoreCase));
        if (port != null)
            port.UserValueOverride = StoreJson;
    }
}
