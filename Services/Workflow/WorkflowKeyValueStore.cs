using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;

namespace FlowMy.Services.Workflow;

/// <summary>
/// Kho key→giá trị cho <see cref="KeyValueBridgeNode"/> và JS <c>kv</c>, scoped theo <c>ExecutionId</c>.
/// </summary>
public static class WorkflowKeyValueStore
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<object?>>> Stores =
        new(StringComparer.Ordinal);

    // Keep KV buckets for a bounded time even after workflow run ends.
    // This is needed because KeyValueBridge "Get" mode can poll via timers that run after the workflow traversal completes.
    private const int MaxKvRunsRetained = 64;
    private static readonly object _lruLock = new();
    private static readonly LinkedList<string> _runLru = new();
    private static readonly Dictionary<string, LinkedListNode<string>> _runLruNodes =
        new(StringComparer.Ordinal);

    /// <summary>
    /// AsyncTask dispatch dùng <c>{parent}:dispatch-{i}</c>; mọi nhánh dùng chung một kho KV với lần chạy gốc
    /// để <c>kv</c> / KeyValueBridge append cùng một key thành một mảng.
    /// </summary>
    public static string NormalizeToKeyValueRunRootId(string? executionId)
    {
        if (string.IsNullOrWhiteSpace(executionId)) return string.Empty;
        var id = executionId.Trim();
        const string marker = ":dispatch-";
        while (true)
        {
            var i = id.LastIndexOf(marker, StringComparison.Ordinal);
            if (i < 0) return id;
            id = id[..i];
        }
    }

    private const string AsyncManualBranchMarker = ":at-manual-";

    /// <summary>
    /// Duyệt <paramref name="executionId"/> từ cụ thể đến tổ tiên (bỏ hậu tố <c>:at-manual-…</c> rồi <c>:dispatch-…</c>)
    /// để đọc scoped output của node chạy trên luồng cha (trước AsyncTask body / nhánh song song).
    /// </summary>
    public static IEnumerable<string> EnumerateScopedLookupExecutionIds(string? executionId)
    {
        if (string.IsNullOrWhiteSpace(executionId)) yield break;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = executionId.Trim();
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (!seen.Add(current)) yield break;

            yield return current;

            var im = current.LastIndexOf(AsyncManualBranchMarker, StringComparison.Ordinal);
            if (im >= 0)
            {
                current = current[..im];
                continue;
            }

            const string dispatchMarker = ":dispatch-";
            var di = current.LastIndexOf(dispatchMarker, StringComparison.Ordinal);
            if (di >= 0)
            {
                current = current[..di];
                continue;
            }

            break;
        }
    }

    private static ConcurrentDictionary<string, List<object?>> GetOrCreateRun(string executionId) =>
        Stores.GetOrAdd(executionId, _ => new ConcurrentDictionary<string, List<object?>>(StringComparer.Ordinal));

    private static void TouchRun(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId)) return;
        lock (_lruLock)
        {
            if (_runLruNodes.TryGetValue(runId, out var node))
            {
                _runLru.Remove(node);
                _runLru.AddLast(node);
                return;
            }

            var newNode = _runLru.AddLast(runId);
            _runLruNodes[runId] = newNode;
        }
    }

    private static void EvictIfNeeded()
    {
        lock (_lruLock)
        {
            while (_runLru.Count > MaxKvRunsRetained)
            {
                var oldest = _runLru.First;
                if (oldest == null) break;

                var runId = oldest.Value;
                _runLru.RemoveFirst();
                _runLruNodes.Remove(runId);

                Stores.TryRemove(runId, out _);
            }
        }
    }

    /// <summary>Ghi theo quy tắc: lần đầu giữ scalar; lần sau thành danh sách, append thread-safe.</summary>
    public static void Append(string? executionId, string? userKey, object? value)
    {
        var runId = NormalizeToKeyValueRunRootId(executionId);
        if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(userKey)) return;
        var key = userKey.Trim();
        var existed = Stores.TryGetValue(runId, out _);
        var run = GetOrCreateRun(runId);
        TouchRun(runId);
        if (!existed)
            EvictIfNeeded();
        var list = run.GetOrAdd(key, _ => new List<object?>());
        lock (list)
        {
            if (list.Count == 0)
            {
                list.Add(value);
                return;
            }

            if (list.Count == 1)
            {
                list.Add(value);
                return;
            }

            list.Add(value);
        }
    }

    public static object? GetSnapshot(string? executionId, string? userKey)
    {
        var runId = NormalizeToKeyValueRunRootId(executionId);
        if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(userKey)) return null;
        var key = userKey.Trim();
        if (!Stores.TryGetValue(runId, out var run) || !run.TryGetValue(key, out var list))
            return null;

        TouchRun(runId);
        lock (list)
        {
            if (list.Count == 0) return null;
            if (list.Count == 1) return list[0];
            return list.ToList();
        }
    }

    /// <summary>
    /// Lấy toàn bộ snapshot key→value(s) cho một lần chạy.
    /// Mỗi key sẽ trả về scalar nếu chỉ có 1 giá trị, hoặc List nếu có nhiều giá trị.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> GetAllSnapshots(string? executionId)
    {
        var runId = NormalizeToKeyValueRunRootId(executionId);
        if (string.IsNullOrWhiteSpace(runId)) return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (!Stores.TryGetValue(runId, out var run) || run.Count == 0)
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        TouchRun(runId);
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in run)
        {
            if (string.IsNullOrWhiteSpace(kv.Key)) continue;
            var list = kv.Value;
            if (list == null) continue;

            lock (list)
            {
                if (list.Count == 0)
                {
                    result[kv.Key] = null;
                }
                else if (list.Count == 1)
                {
                    result[kv.Key] = list[0];
                }
                else
                {
                    result[kv.Key] = list.ToList();
                }
            }
        }

        return result;
    }

    /// <summary>
    /// WorkflowEditorViewModel luôn gọi <see cref="WorkflowExecutionService.ClearScopedOutputsForRun"/>
    /// khi run kết thúc để dọn RAM.
    ///
    /// Với KeyValueBridge, "Get" mode có thể được timer bên ngoài chạy sau khi traversal kết thúc,
    /// nên không thể xóa ngay KV bucket.
    /// </summary>
    public static void ClearForExecution(string? executionId)
    {
        if (string.IsNullOrWhiteSpace(executionId)) return;
        var root = NormalizeToKeyValueRunRootId(executionId);
        if (string.IsNullOrWhiteSpace(root)) return;

        // Best-effort: just touch it so it doesn't get evicted immediately.
        TouchRun(root);
    }

    /// <summary>Xóa toàn bộ key trong một KV run, trả về số key đã xóa.</summary>
    public static int ClearRunKeys(string? executionId)
    {
        var runId = NormalizeToKeyValueRunRootId(executionId);
        if (string.IsNullOrWhiteSpace(runId)) return 0;
        if (!Stores.TryGetValue(runId, out var run) || run.Count == 0) return 0;

        var count = run.Count;
        run.Clear();
        TouchRun(runId);
        return count;
    }

    /// <summary>Xóa toàn bộ dữ liệu của một key.</summary>
    public static bool RemoveKey(string? executionId, string? userKey)
    {
        var runId = NormalizeToKeyValueRunRootId(executionId);
        if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(userKey)) return false;
        if (!Stores.TryGetValue(runId, out var run)) return false;

        TouchRun(runId);
        return run.TryRemove(userKey.Trim(), out _);
    }

    /// <summary>
    /// Xóa phần tử trong mảng value của key theo điều kiện JSON field=value.
    /// Return:
    /// - &gt;0: số phần tử bị xóa
    /// - 0: key tồn tại nhưng không có item nào khớp
    /// - -1: key không ở dạng mảng (list.Count &lt;= 1)
    /// - -2: key không tồn tại
    /// </summary>
    public static int RemoveArrayItemsByJsonField(
        string? executionId,
        string? userKey,
        string? fieldName,
        string? expectedValue,
        bool removeAllMatches)
    {
        var runId = NormalizeToKeyValueRunRootId(executionId);
        if (string.IsNullOrWhiteSpace(runId) ||
            string.IsNullOrWhiteSpace(userKey) ||
            string.IsNullOrWhiteSpace(fieldName))
            return -2;

        if (!Stores.TryGetValue(runId, out var run)) return -2;
        if (!run.TryGetValue(userKey.Trim(), out var list) || list == null) return -2;

        var removed = 0;
        var field = fieldName.Trim();
        var expected = expectedValue?.Trim() ?? string.Empty;

        lock (list)
        {
            if (list.Count <= 1) return -1;

            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (!TryMatchJsonFieldValue(list[i], field, expected))
                    continue;

                list.RemoveAt(i);
                removed++;
                if (!removeAllMatches) break;
            }
        }

        if (removed > 0)
        {
            if (list.Count == 0)
                run.TryRemove(userKey.Trim(), out _);
            TouchRun(runId);
        }

        return removed;
    }

    public static string? SnapshotToDisplayString(object? v) => v switch
    {
        null => null,
        string s => s,
        JsonElement je => je.GetRawText(),
        var o => JsonSerializer.Serialize(o)
    };

    private static bool TryMatchJsonFieldValue(object? item, string fieldName, string expectedValue)
    {
        if (item == null) return false;

        if (item is JsonElement je && je.ValueKind == JsonValueKind.Object)
            return TryMatchJsonElement(je, fieldName, expectedValue);

        if (item is string s)
        {
            var trimmed = s.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                return doc.RootElement.ValueKind == JsonValueKind.Object &&
                       TryMatchJsonElement(doc.RootElement, fieldName, expectedValue);
            }
            catch
            {
                return false;
            }
        }

        try
        {
            var json = JsonSerializer.Serialize(item);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object &&
                   TryMatchJsonElement(doc.RootElement, fieldName, expectedValue);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryMatchJsonElement(JsonElement obj, string fieldName, string expectedValue)
    {
        if (!obj.TryGetProperty(fieldName, out var valueElement)) return false;

        var actual = valueElement.ValueKind switch
        {
            JsonValueKind.String => valueElement.GetString() ?? string.Empty,
            JsonValueKind.Number => valueElement.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => valueElement.GetRawText()
        };

        return string.Equals(actual.Trim(), expectedValue.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
