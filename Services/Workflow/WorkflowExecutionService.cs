using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Windows;

namespace FlowMy.Services.Workflow
{
    /// <summary>
    /// Service xử lý logic thực thi workflow từ Start node đến End node.
    /// Hỗ trợ sequential và parallel execution cho các output ports.
    /// </summary>
    public class WorkflowExecutionService
    {
        private readonly GlobalKeyboardHookService _keyboardHook;
        private readonly FlowMy.Services.Interaction.KeyboardInputService _keyboardInput;
        private readonly MouseInputService _mouseInput;

        private readonly List<NodeExecutors.INodeExecutor> _nodeExecutors;

        // ✅ LoopBody / AsyncTaskBody "return-to-right" synchronization — key gồm executionId + bodyId + branchScope (để nhiều vòng song song).
        private readonly Dictionary<string, LoopBodyReturnWaiter> _loopBodyReturnWaiters = new();

        private static string LoopBodyWaiterCompositeKey(string executionId, string loopBodyNodeId, string? branchScope = null)
        {
            var scope = branchScope ?? string.Empty;
            return (executionId ?? string.Empty) + '\u001f' + loopBodyNodeId + '\u001f' + scope;
        }

        /// <summary>
        /// Output chuỗi theo từng lần chạy (ExecutionId → NodeId → key → value).
        /// Tránh hai workflow chạy đồng thời trên cùng graph ghi đè <see cref="CodeNode.ResolvedOutputs"/> lẫn nhau khi đọc downstream trong cùng luồng.
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, string?>>> _scopedStringOutputsByRun =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Bản sao "bền" theo gốc root-run: RootRunId → ExecutionId → NodeId → key → value.
        /// Giữ lại giá trị ngay cả khi <see cref="_scopedStringOutputsByRun"/> bị clear/evict trước khi downstream đọc
        /// (ví dụ: AsyncTask nhiều dispatch + HTTP chạy lâu → lookup chain primary miss, sticky cover).
        /// Chỉ bị xóa khi root run thực sự kết thúc (xem <see cref="ClearScopedOutputsForRun"/>).
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, string?>>>> _stickyScopedStringOutputsByRoot =
            new(StringComparer.Ordinal);

        /// <summary>Giới hạn số snapshot run còn giữ trong RAM (tránh rò nếu quên Clear); run cũ nhất bị evict.</summary>
        private const int MaxScopedRunsRetained = 64;

        private readonly object _scopedRunRegistryLock = new();
        private readonly LinkedList<string> _scopedRunLru = new();
        private readonly Dictionary<string, LinkedListNode<string>> _scopedRunLruNodes = new(StringComparer.Ordinal);

        internal MouseInputService MouseInput => _mouseInput;
        internal FlowMy.Services.Interaction.KeyboardInputService KeyboardInput => _keyboardInput;
        internal GlobalKeyboardHookService GlobalKeyboardHook => _keyboardHook;

        internal void SetScopedNodeStringOutput(string executionId, string nodeId, string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(executionId) || string.IsNullOrWhiteSpace(nodeId)) return;
            var k = (key ?? string.Empty).Trim();
            if (k.Length == 0) return;
            RegisterScopedRunForEviction(executionId);
            var byNode = _scopedStringOutputsByRun.GetOrAdd(executionId,
                static _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, string?>>(StringComparer.OrdinalIgnoreCase));
            var byKey = byNode.GetOrAdd(nodeId,
                static _ => new ConcurrentDictionary<string, string?>(StringComparer.OrdinalIgnoreCase));
            byKey[k] = value;

            // Mirror vào sticky theo root run để downstream vẫn đọc được dù primary scoped bị evict/clear sớm.
            var rootId = NormalizeToRootRunId(executionId);
            if (!string.IsNullOrWhiteSpace(rootId))
            {
                var stickyByExec = _stickyScopedStringOutputsByRoot.GetOrAdd(rootId,
                    static _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, string?>>>(StringComparer.Ordinal));
                var stickyByNode = stickyByExec.GetOrAdd(executionId,
                    static _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, string?>>(StringComparer.OrdinalIgnoreCase));
                var stickyByKey = stickyByNode.GetOrAdd(nodeId,
                    static _ => new ConcurrentDictionary<string, string?>(StringComparer.OrdinalIgnoreCase));
                stickyByKey[k] = value;
            }
        }

        internal bool TryGetScopedNodeStringOutput(string executionId, string nodeId, string key, out string? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(executionId) || string.IsNullOrWhiteSpace(nodeId)) return false;
            var k = (key ?? string.Empty).Trim();
            if (k.Length == 0) return false;

            // 1) Primary scoped store.
            if (_scopedStringOutputsByRun.TryGetValue(executionId, out var byNode) &&
                byNode.TryGetValue(nodeId, out var byKey))
            {
                if (byKey.TryGetValue(k, out value)) return true;
                foreach (var kv in byKey)
                {
                    if (string.Equals(kv.Key, k, StringComparison.OrdinalIgnoreCase))
                    {
                        value = kv.Value;
                        return true;
                    }
                }
            }

            // 2) Sticky fallback (theo root run): tồn tại kể cả sau khi primary bị clear/evict sớm.
            var rootId = NormalizeToRootRunId(executionId);
            if (!string.IsNullOrWhiteSpace(rootId) &&
                _stickyScopedStringOutputsByRoot.TryGetValue(rootId, out var stickyByExec) &&
                stickyByExec.TryGetValue(executionId, out var stickyByNode) &&
                stickyByNode.TryGetValue(nodeId, out var stickyByKey))
            {
                if (stickyByKey.TryGetValue(k, out value)) return true;
                foreach (var kv in stickyByKey)
                {
                    if (string.Equals(kv.Key, k, StringComparison.OrdinalIgnoreCase))
                    {
                        value = kv.Value;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Đọc output scoped theo chuỗi <see cref="WorkflowKeyValueStore.EnumerateScopedLookupExecutionIds"/>:
        /// cùng lần chạy hiện tại rồi tổ tiên (bỏ <c>:at-manual-</c>, <c>:dispatch-</c>).
        /// Dùng khi node downstream (FlowOverwrite, v.v.) chạy với id khác id lúc producer ghi snapshot.
        /// Không đi ngang sang nhánh song song khác — chỉ đi lên cha.
        /// </summary>
        internal bool TryGetScopedNodeStringOutputForLookupChain(string? executionId, string nodeId, string key, out string? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(nodeId)) return false;
            var k = (key ?? string.Empty).Trim();
            if (k.Length == 0) return false;
            foreach (var runId in WorkflowKeyValueStore.EnumerateScopedLookupExecutionIds(executionId))
            {
                if (TryGetScopedNodeStringOutput(runId, nodeId, k, out var scoped) && scoped != null)
                {
                    value = scoped;
                    return true;
                }
            }
            return false;
        }

        private void RegisterScopedRunForEviction(string executionId)
        {
            lock (_scopedRunRegistryLock)
            {
                if (_scopedRunLruNodes.TryGetValue(executionId, out var existing))
                {
                    _scopedRunLru.Remove(existing);
                    _scopedRunLruNodes[executionId] = _scopedRunLru.AddLast(executionId);
                    return;
                }

                var node = _scopedRunLru.AddLast(executionId);
                _scopedRunLruNodes[executionId] = node;

                while (_scopedRunLru.Count > MaxScopedRunsRetained)
                {
                    var first = _scopedRunLru.First;
                    if (first == null) break;
                    var evictId = first.Value;
                    _scopedRunLru.RemoveFirst();
                    _scopedRunLruNodes.Remove(evictId);
                    _scopedStringOutputsByRun.TryRemove(evictId, out _);
                }
            }
        }

        /// <summary>
        /// Đẩy toàn bộ <see cref="StorageNode.StoredOutputs"/> vào snapshot scoped của <paramref name="executionId"/>.
        /// Gọi sau AssignData / Loop ghi Storage để downstream trong cùng lần chạy đọc đúng (không trộn với run khác).
        /// </summary>
        internal void PublishStorageOutputsToScoped(StorageNode storage, string executionId)
        {
            if (storage == null || string.IsNullOrWhiteSpace(executionId)) return;
            foreach (var kv in storage.StoredOutputs)
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                SetScopedNodeStringOutput(executionId, storage.Id, kv.Key.Trim(), kv.Value);
            }
        }

        /// <summary>
        /// Xóa snapshot output của một lần chạy (gọi sau khi workflow kết thúc để giảm RAM).
        /// Chỉ xóa sticky store khi <paramref name="executionId"/> là ROOT run (không phải dispatch/at-manual branch) —
        /// downstream trong cùng root run vẫn đọc được giá trị của các dispatch đã hoàn tất.
        /// </summary>
        public void ClearScopedOutputsForRun(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId)) return;
            WorkflowKeyValueStore.ClearForExecution(executionId);
            NodeExecutors.KeyScopedNodeExecutor.ClearStoreForExecution(executionId);
            _scopedStringOutputsByRun.TryRemove(executionId, out _);
            lock (_scopedRunRegistryLock)
            {
                if (_scopedRunLruNodes.TryGetValue(executionId, out var node))
                {
                    _scopedRunLru.Remove(node);
                    _scopedRunLruNodes.Remove(executionId);
                }
            }

            // Sticky store: chỉ xóa khi root run (không phải dispatch/at-manual branch).
            // Lý do: nhiều dispatch có thể clear primary trong quá trình loop nhưng downstream vẫn cần đọc giá trị.
            if (!IsParallelScopedRun(executionId))
            {
                _stickyScopedStringOutputsByRoot.TryRemove(executionId, out _);

                // Dọn luôn mọi entry primary thuộc root này (dispatch-X / at-manual-…) để tránh leak.
                var prefix = executionId + ":";
                var orphanKeys = _scopedStringOutputsByRun.Keys
                    .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                    .ToList();
                foreach (var orphan in orphanKeys)
                {
                    _scopedStringOutputsByRun.TryRemove(orphan, out _);
                    lock (_scopedRunRegistryLock)
                    {
                        if (_scopedRunLruNodes.TryGetValue(orphan, out var n))
                        {
                            _scopedRunLru.Remove(n);
                            _scopedRunLruNodes.Remove(orphan);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// True nếu <paramref name="executionId"/> nằm trong nhánh chạy song song (AsyncTask dispatch hoặc at-manual branch).
        /// Trong các nhánh này KHÔNG được fallback về state dùng chung của <see cref="WorkflowNode.ResolvedOutputs"/>
        /// vì nó bị ghi đè chéo giữa các dispatch (dispatch hoàn thành sau cùng "thắng").
        /// </summary>
        internal static bool IsParallelScopedRun(string? executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId)) return false;
            return executionId.Contains(":dispatch-", StringComparison.Ordinal) ||
                   executionId.Contains(":at-manual-", StringComparison.Ordinal);
        }

        /// <summary>
        /// Bóc toàn bộ hậu tố <c>:dispatch-…</c> và <c>:at-manual-…</c> để lấy id gốc của root run
        /// (dùng làm khóa cho <see cref="_stickyScopedStringOutputsByRoot"/>).
        /// </summary>
        private static string NormalizeToRootRunId(string? executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId)) return string.Empty;
            var id = executionId.Trim();
            while (true)
            {
                var iA = id.LastIndexOf(":at-manual-", StringComparison.Ordinal);
                var iD = id.LastIndexOf(":dispatch-", StringComparison.Ordinal);
                var i = Math.Max(iA, iD);
                if (i < 0) return id;
                id = id[..i];
            }
        }

        /// <summary>
        /// Resolve theo snapshot scoped của <paramref name="executionId"/> (nếu có), fallback UI/node phẳng.
        /// Dùng khi không có full <see cref="NodeExecutors.NodeExecutionEnvironment"/> (mirror Storage, v.v.).
        /// Lookup chain đã được gia cố bằng sticky-per-root-run (xem <see cref="SetScopedNodeStringOutput"/>)
        /// nên primary miss hầu như chỉ xảy ra với node publish qua sự kiện ngoài (WebNode browser events, v.v.).
        /// Với các node đó shared <see cref="WorkflowNode.DynamicOutputs"/> là nguồn hợp lệ duy nhất.
        /// </summary>
        internal string ResolveDynamicValueForRun(WorkflowNode? node, string? key, string? executionId)
        {
            if (node == null) return string.Empty;
            var k = (key ?? string.Empty).Trim();
            if (k.Length == 0) return string.Empty;
            string resolved;
            if (!string.IsNullOrWhiteSpace(executionId) &&
                TryGetScopedNodeStringOutputForLookupChain(executionId, node.Id, k, out var scoped) &&
                scoped != null)
            {
                resolved = scoped;
            }
            else
            {
                resolved = NodeDataPanelService.ResolveDynamicValueByKey(node, k) ?? string.Empty;
            }
            return string.Equals(resolved, "—", StringComparison.Ordinal) ? string.Empty : resolved;
        }

        /// <summary>
        /// Resolve giá trị khi thực thi: ưu tiên output đã lưu theo <see cref="NodeExecutors.NodeExecutionEnvironment.ExecutionId"/>,
        /// rồi mới fallback <see cref="NodeDataPanelService.ResolveDynamicValueByKey"/> (UI / node mặt phẳng).
        /// </summary>
        internal string ResolveDynamicValueForExecution(
            WorkflowNode node,
            string key,
            NodeExecutors.NodeExecutionEnvironment env)
        {
            if (node == null) return string.Empty;
            var k = (key ?? string.Empty).Trim();
            if (k.Length == 0) return string.Empty;
            if (env.RefreshOnly)
            {
                var r = NodeDataPanelService.ResolveDynamicValueByKey(node, k) ?? string.Empty;
                return string.Equals(r, "—", StringComparison.Ordinal) ? string.Empty : r;
            }

            return ResolveDynamicValueForRun(node, k, env.ExecutionId);
        }

        /// <summary>
        /// Giống <see cref="ResolveValueByNodeIdAndKey"/> nhưng ưu tiên snapshot scoped của lần chạy hiện tại (<paramref name="env"/>).
        /// </summary>
        internal string ResolveValueByNodeIdAndKeyForExecution(
            IEnumerable<WorkflowConnection> connections,
            string? nodeId,
            string? key,
            NodeExecutors.NodeExecutionEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(key)) return string.Empty;
            var node = connections
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .FirstOrDefault(n => n != null && string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            return node == null ? string.Empty : ResolveDynamicValueForExecution(node, key.Trim(), env);
        }

        /// <summary>
        /// Legacy path upstream vào conditional: dùng scoped khi có <paramref name="env"/>.
        /// </summary>
        internal string ResolveConditionFromUpstreamForExecution(
            WorkflowNode conditionalNode,
            string? key,
            List<WorkflowConnection> connections,
            NodeExecutors.NodeExecutionEnvironment env)
        {
            if (conditionalNode?.Ports == null || string.IsNullOrWhiteSpace(key)) return string.Empty;

            var inputPort = conditionalNode.Ports.FirstOrDefault(p => p.IsInput && p.IsVisible);
            if (inputPort == null) return string.Empty;

            var conn = connections.FirstOrDefault(c =>
                c.ToNode == conditionalNode &&
                (ReferenceEquals(c.ToPort, inputPort) ||
                 (c.ToPort != null && c.ToPort.IsInput && (string.IsNullOrEmpty(inputPort.Id) || string.Equals(c.ToPort.Id, inputPort.Id, StringComparison.OrdinalIgnoreCase)))));
            var fromNode = conn?.FromNode;
            if (fromNode == null) return string.Empty;

            return ResolveDynamicValueForExecution(fromNode, key.Trim(), env);
        }

        private static string? ObjectToScopedSnapshotString(object? v) =>
            v switch
            {
                null => null,
                string s => s,
                var o => o.ToString()
            };

        /// <summary>
        /// Đẩy dictionary output (vd. Code vừa tính xong) vào scoped store trước khi gọi node downstream —
        /// cần khi nhiều dispatch song song cùng graph (mirror trong finally của ExecuteNodeAsync chạy quá muộn).
        /// </summary>
        internal void PublishDictionaryOutputsToScopedStore(string executionId, string nodeId, Dictionary<string, object?> outputs)
        {
            if (string.IsNullOrWhiteSpace(executionId) || string.IsNullOrWhiteSpace(nodeId) || outputs == null) return;
            foreach (var kv in outputs)
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                SetScopedNodeStringOutput(executionId, nodeId, kv.Key.Trim(), ObjectToScopedSnapshotString(kv.Value));
            }
        }

        /// <summary>
        /// Public wrapper: mirror node outputs vào scoped store TRƯỚC khi traverse downstream.
        /// Giải quyết race condition trong parallel mode (AsyncTask) khi nhiều iteration dùng chung node object.
        /// </summary>
        internal void PreTraverseMirrorToScopedStore(WorkflowNode node, string executionId)
            => MirrorRuntimeOutputsToScopedStore(node, executionId);

        /// <summary>
        /// Copy runtime outputs vào kho theo <paramref name="executionId"/> để <see cref="ResolveDynamicValueForExecution"/> đọc đúng khi nhiều luồng chạy cùng graph.
        /// </summary>
        private void MirrorRuntimeOutputsToScopedStore(WorkflowNode node, string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId) || node == null) return;

            // CodeNode/OutputNode đã tự publish vào scoped store ngay trong executor
            // trước khi ghi vào shared runtime fields.
            // Nếu mirror lại từ object shared ở đây, parallel dispatch có thể ghi đè chéo
            // executionId (race), làm nhiều dispatch đọc cùng một giá trị.
            if (node is CodeNode
                or OutputNode
                or ListOutNode
                or DataFetcherNode
                or FolderNode
                or StringSplitNode
                or HttpRequestNode
                or WebNode
                or FileDownloadNode
                or FolderFilePathsNode
                or KeyValueBridgeNode
                or FlowOverwriteNode
                or AsyncTaskDispatchCollectNode
                or StorageNode
                or HtmlUiNode)
                return;

            void CopyDict(Dictionary<string, object?> d)
            {
                foreach (var kv in d)
                {
                    if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                    SetScopedNodeStringOutput(executionId, node.Id, kv.Key.Trim(), ObjectToScopedSnapshotString(kv.Value));
                }
            }

            switch (node)
            {
                case CodeNode c:
                    CopyDict(c.ResolvedOutputs);
                    break;
                case ListOutNode l:
                    CopyDict(l.ResolvedOutputs);
                    break;
                case HtmlUiNode h:
                    CopyDict(h.ResolvedOutputs);
                    break;
                case FolderNode f:
                    CopyDict(f.ResolvedOutputs);
                    break;
                case FileDownloadNode fd:
                    CopyDict(fd.ResolvedOutputs);
                    break;
                case FolderFilePathsNode ffp:
                    CopyDict(ffp.ResolvedOutputs);
                    break;
                case StringSplitNode ss:
                    if (ss.SplitResult != null && ss.SplitResult.Count > 0)
                    {
                        var ok = string.IsNullOrWhiteSpace(ss.OutputKey) ? "ListItems" : ss.OutputKey.Trim();
                        SetScopedNodeStringOutput(executionId, node.Id, ok, JsonSerializer.Serialize(ss.SplitResult));
                    }
                    break;
                case OutputNode o:
                    if (!string.IsNullOrWhiteSpace(o.OutputKey))
                        SetScopedNodeStringOutput(executionId, node.Id, o.OutputKey.Trim(), o.OutputText);
                    break;
                case InputNode input:
                    if (input.DynamicOutputs != null && input.DynamicOutputs.Count > 0)
                    {
                        var outKey = input.DynamicOutputs[0].Key;
                        if (!string.IsNullOrWhiteSpace(outKey))
                        {
                            var val = input.IsArrayType
                                ? JsonSerializer.Serialize(input.ArrayValues ?? new List<string>())
                                : input.Value;
                            SetScopedNodeStringOutput(executionId, node.Id, outKey.Trim(), val);
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(input.Key))
                        SetScopedNodeStringOutput(executionId, node.Id, input.Key.Trim(), input.Value);
                    break;
                case KeyPressEventNode kp:
                    if (!string.IsNullOrWhiteSpace(kp.Key))
                    {
                        SetScopedNodeStringOutput(executionId, node.Id, "key", kp.Key);
                        SetScopedNodeStringOutput(executionId, node.Id, "triggerkey", kp.Key);
                    }
                    break;
                case HotkeyPressEventNode hk:
                    if (!string.IsNullOrWhiteSpace(hk.Key))
                    {
                        SetScopedNodeStringOutput(executionId, node.Id, "key", hk.Key);
                        SetScopedNodeStringOutput(executionId, node.Id, "triggerhotkey", hk.Key);
                    }
                    break;
                case HttpRequestNode http:
                    if (http.LastStatusCode.HasValue)
                        SetScopedNodeStringOutput(executionId, node.Id, "statuscode", http.LastStatusCode.Value.ToString());
                    if (!string.IsNullOrWhiteSpace(http.LastResponseBody))
                        SetScopedNodeStringOutput(executionId, node.Id, "responsebody", http.LastResponseBody);
                    if (http.LastResponseHeaders != null && http.LastResponseHeaders.Count > 0)
                        SetScopedNodeStringOutput(executionId, node.Id, "responseheaders", JsonSerializer.Serialize(http.LastResponseHeaders));
                    if (http.LastIsSuccess.HasValue)
                        SetScopedNodeStringOutput(executionId, node.Id, "issuccess", http.LastIsSuccess.Value.ToString());
                    if (!string.IsNullOrWhiteSpace(http.LastErrorMessage))
                        SetScopedNodeStringOutput(executionId, node.Id, "errormessage", http.LastErrorMessage);
                    if (http.LastResponseTimeMs.HasValue)
                        SetScopedNodeStringOutput(executionId, node.Id, "responsetimems", http.LastResponseTimeMs.Value.ToString());
                    if (!string.IsNullOrWhiteSpace(http.LastCurlCommand))
                        SetScopedNodeStringOutput(executionId, node.Id, "curl", http.LastCurlCommand);
                    break;
                case WebNode web:
                    if (!string.IsNullOrWhiteSpace(web.LastCookie))
                    {
                        SetScopedNodeStringOutput(executionId, node.Id, "cookie", web.LastCookie);
                    }
                    if (!string.IsNullOrWhiteSpace(web.LastBearer))
                        SetScopedNodeStringOutput(executionId, node.Id, "bearer", web.LastBearer);
                    if (!string.IsNullOrWhiteSpace(web.LastAccessToken))
                        SetScopedNodeStringOutput(executionId, node.Id, "access_token", web.LastAccessToken);
                    if (web.ResponseOutputValues != null)
                    {
                        foreach (var kv in web.ResponseOutputValues)
                        {
                            if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                            SetScopedNodeStringOutput(executionId, node.Id, kv.Key.Trim(), kv.Value);
                        }
                    }
                    break;
                case StorageNode st:
                    PublishStorageOutputsToScoped(st, executionId);
                    break;
                case KeyValueBridgeNode kvb:
                    if (kvb.ResolvedOutputs != null && kvb.ResolvedOutputs.Count > 0)
                        CopyDict(kvb.ResolvedOutputs);
                    break;
                default:
                    break;
            }

            if (node is IWorkflowScopedOutputContributor scopedContributor)
            {
                scopedContributor.AppendScopedStringOutputs((key, value) =>
                {
                    if (string.IsNullOrWhiteSpace(key)) return;
                    SetScopedNodeStringOutput(executionId, node.Id, key.Trim(), value);
                });
            }
        }

        /// <summary>
        /// Sau <see cref="ExecuteNodeLogicOnlyAsync"/> giữa một lần chạy workflow, đồng bộ output runtime của node vào snapshot
        /// để <see cref="ResolveDynamicValueForRun"/> không còn đọc giá trị scoped cũ.
        /// </summary>
        internal void RefreshScopedOutputsFromNodeRuntime(WorkflowNode node, string executionId)
            => MirrorRuntimeOutputsToScopedStore(node, executionId);

        public WorkflowExecutionService(
            GlobalKeyboardHookService keyboardHook,
            FlowMy.Services.Interaction.KeyboardInputService keyboardInput,
             MouseInputService mouseInput)
        {
            _keyboardHook = keyboardHook ?? throw new ArgumentNullException(nameof(keyboardHook));
            _keyboardInput = keyboardInput ?? throw new ArgumentNullException(nameof(keyboardInput));
            _mouseInput = mouseInput ?? throw new ArgumentNullException(nameof(mouseInput));

            // Đăng ký các executor cho từng loại node.
            _nodeExecutors = new List<NodeExecutors.INodeExecutor>
            {
                new NodeExecutors.LoopNodeExecutor(),
                new NodeExecutors.ConditionalNodeExecutor(),
                new NodeExecutors.AsyncTaskNodeExecutor(),
                new NodeExecutors.DelayNodeExecutor(),
                new NodeExecutors.CallbackNodeExecutor(),
                new NodeExecutors.StringSplitNodeExecutor(),
                new NodeExecutors.ListOutNodeExecutor(),
                new NodeExecutors.AsyncTaskDispatchCollectNodeExecutor(),
                new NodeExecutors.AssignDataNodeExecutor(),
                new NodeExecutors.StorageNodeExecutor(),
                new NodeExecutors.OutputNodeExecutor(),
                new NodeExecutors.NotificationNodeExecutor(),
                new NodeExecutors.MouseEventNodeExecutor(),
                new NodeExecutors.KeyPressEventNodeExecutor(),
                new NodeExecutors.HotkeyPressEventNodeExecutor(),
                new NodeExecutors.HttpRequestNodeExecutor(),
                new NodeExecutors.MediaGalleryNodeExecutor(),
                new NodeExecutors.ImageProcessingNodeExecutor(),
                new NodeExecutors.VideoProcessingNodeExecutor(),
                new NodeExecutors.DataFetcherNodeExecutor(),
                new NodeExecutors.KeyValueBridgeNodeExecutor(),
                new NodeExecutors.FlowOverwriteNodeExecutor(),
                new NodeExecutors.KeyScopedNodeExecutor(),
                new NodeExecutors.CodeNodeExecutor(),
                new NodeExecutors.FolderNodeExecutor(),
                new NodeExecutors.WebNodeExecutor(),
                new NodeExecutors.HtmlUiNodeExecutor(),
                new NodeExecutors.FileDownloadNodeExecutor(),
                new NodeExecutors.FolderFilePathsNodeExecutor(),
                new NodeExecutors.GitSourceNodeExecutor(),
                new NodeExecutors.MacroRecorderNodeExecutor(),
                new NodeExecutors.BorderHighlightNodeExecutor(),
                new NodeExecutors.ScreenPositionPickerNodeExecutor(),
                new NodeExecutors.ScreenCaptureNodeExecutor(),
                new NodeExecutors.TextScanNodeExecutor(),
                new NodeExecutors.EmbedApplicationNodeExecutor(),
                new NodeExecutors.DefaultNodeExecutor()
            };
        }

        /// <summary>
        /// Begin waiting for a LoopBodyRight "return" connection to be encountered for the given body node.
        /// The returned IDisposable MUST be disposed to avoid leaks (finally-block).
        /// </summary>
        internal (Task<WorkflowConnection> task, IDisposable cleanup) BeginAwaitLoopBodyReturn(
            WorkflowNode bodyNode,
            string executionId,
            CancellationToken cancellationToken,
            CancellationTokenSource? hardStopCts = null,
            string? branchScope = null)
        {
            if (bodyNode == null) throw new ArgumentNullException(nameof(bodyNode));
            if (bodyNode is not LoopBodyNode and not AsyncTaskBodyNode)
                throw new ArgumentException("Body node must be LoopBodyNode or AsyncTaskBodyNode.", nameof(bodyNode));

            var mapKey = LoopBodyWaiterCompositeKey(executionId, bodyNode.Id, branchScope);
            var waiter = new LoopBodyReturnWaiter(
                new TaskCompletionSource<WorkflowConnection>(TaskCreationOptions.RunContinuationsAsynchronously),
                hardStopCts);
            lock (_loopBodyReturnWaiters)
            {
                _loopBodyReturnWaiters[mapKey] = waiter;
            }

            CancellationTokenRegistration ctr = default;
            if (cancellationToken.CanBeCanceled)
            {
                ctr = cancellationToken.Register(() =>
                {
                    lock (_loopBodyReturnWaiters)
                    {
                        if (_loopBodyReturnWaiters.TryGetValue(mapKey, out var current) && ReferenceEquals(current, waiter))
                        {
                            _loopBodyReturnWaiters.Remove(mapKey);
                        }
                    }
                    waiter.Tcs.TrySetCanceled(cancellationToken);
                });
            }

            IDisposable cleanup = new DelegateDisposable(() =>
            {
                ctr.Dispose();
                lock (_loopBodyReturnWaiters)
                {
                    if (_loopBodyReturnWaiters.TryGetValue(mapKey, out var current) && ReferenceEquals(current, waiter))
                    {
                        _loopBodyReturnWaiters.Remove(mapKey);
                    }
                }
            });

            return (waiter.Tcs.Task, cleanup);
        }

        /// <summary>
        /// Signal that a LoopBodyRight return connection has been hit.
        /// Used by executors that see a return-connection and should not traverse into LoopBodyNode.
        /// </summary>
        internal void SignalLoopBodyReturn(WorkflowConnection conn, string executionId, string? signalingBranchId = null)
        {
            if (conn?.ToNode is not LoopBodyNode and not AsyncTaskBodyNode) return;
            var bodyNode = conn.ToNode!;
            if (conn.ToPort == null || !string.Equals(conn.ToPort.Id, "LoopBodyRight", StringComparison.OrdinalIgnoreCase)) return;

            var keyPrefix = (executionId ?? string.Empty) + '\u001f' + bodyNode.Id + '\u001f';
            var legacyTwoPartKey = (executionId ?? string.Empty) + '\u001f' + bodyNode.Id;

            LoopBodyReturnWaiter? waiter = null;
            lock (_loopBodyReturnWaiters)
            {
                if (string.IsNullOrEmpty(signalingBranchId))
                {
                    var triple = LoopBodyWaiterCompositeKey(executionId, bodyNode.Id, string.Empty);
                    if (!_loopBodyReturnWaiters.TryGetValue(triple, out waiter))
                        _loopBodyReturnWaiters.TryGetValue(legacyTwoPartKey, out waiter);
                }
                else
                {
                    var bestLen = -1;
                    foreach (var kv in _loopBodyReturnWaiters)
                    {
                        if (!kv.Key.StartsWith(keyPrefix, StringComparison.Ordinal)) continue;
                        var scope = kv.Key.Length > keyPrefix.Length ? kv.Key.Substring(keyPrefix.Length) : string.Empty;
                        if (string.Equals(signalingBranchId, scope, StringComparison.Ordinal)
                            || signalingBranchId.StartsWith(scope + ":", StringComparison.Ordinal))
                        {
                            if (scope.Length > bestLen)
                            {
                                bestLen = scope.Length;
                                waiter = kv.Value;
                            }
                        }
                    }
                }
            }

            if (waiter == null) return;

            // ✅ 1) mark return reached
            waiter.Tcs.TrySetResult(conn);
            // ✅ 2) hard stop: cancel the iteration CTS so any other pending branches stop ASAP
            if (waiter.HardStopCts != null && !waiter.HardStopCts.IsCancellationRequested)
            {
                try { waiter.HardStopCts.Cancel(); } catch { /* ignore */ }
            }
        }

        /// <summary>
        /// Tìm tất cả Start nodes trong workflow
        /// </summary>
        public List<WorkflowNode> FindStartNodes(IEnumerable<WorkflowNode> nodes)
        {
            return nodes
                .Where(n => n.Type == NodeType.Start && n.RunMode != FlowRunMode.AutoScheduled)
                .ToList();
        }

        /// <summary>
        /// Tìm tất cả End nodes trong workflow
        /// </summary>
        public List<WorkflowNode> FindEndNodes(IEnumerable<WorkflowNode> nodes)
        {
            return nodes.Where(n => n.Type == NodeType.End).ToList();
        }

        /// <summary>
        /// Lấy tất cả connections từ một output port cụ thể
        /// </summary>
        public List<WorkflowConnection> GetConnectionsFromPort(
            NodePort port,
            WorkflowNode fromNode,
            IEnumerable<WorkflowConnection> allConnections)
        {
            if (port == null) return new List<WorkflowConnection>();

            // NOTE:
            // - Some workflows (import/legacy) may have connections without FromPort/ToPort persisted,
            //   or the NodePort instance may differ after ports are re-created.
            // - Therefore we match primarily by FromNode + PortId, and optionally include legacy (FromPort == null).
            return allConnections
                .Where(c =>
                    c.FromNode == fromNode &&
                    (
                        // normal: same object reference
                        ReferenceEquals(c.FromPort, port) ||
                        // robust: match by semantic PortId
                        (c.FromPort != null && !string.IsNullOrWhiteSpace(c.FromPort.Id) &&
                         string.Equals(c.FromPort.Id, port.Id, StringComparison.OrdinalIgnoreCase))
                    ))
                .ToList();
        }

        /// <summary>
        /// Like GetConnectionsFromPort, but also includes legacy connections that have FromPort == null.
        /// This is mainly used for Loop nodes during execution because older JSONs may not store port ids.
        /// </summary>
        internal List<WorkflowConnection> GetConnectionsFromPortIncludingLegacy(
            NodePort port,
            WorkflowNode fromNode,
            IEnumerable<WorkflowConnection> allConnections)
        {
            var strict = GetConnectionsFromPort(port, fromNode, allConnections);
            var legacy = allConnections
                .Where(c => c.FromNode == fromNode && c.FromPort == null)
                .ToList();

            // Avoid duplicates if graph keeps both normal + legacy copies for the same semantic edge.
            // This can otherwise trigger duplicate execution (notably in AsyncTask dispatch mode).
            static string BuildSemanticKey(WorkflowConnection c)
            {
                var fromId = c.FromNode?.Id ?? string.Empty;
                var toId = c.ToNode?.Id ?? string.Empty;
                var toPortId = c.ToPort?.Id ?? string.Empty;
                // Intentionally ignore FromPort here:
                // strict list already scoped by requested source port; legacy duplicates often have FromPort=null.
                return $"{fromId}|{toId}|{toPortId}";
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in strict)
                seen.Add(BuildSemanticKey(c));

            foreach (var c in legacy)
            {
                var k = BuildSemanticKey(c);
                if (seen.Add(k))
                    strict.Add(c);
            }
            return strict;
        }

        /// <summary>
        /// Lấy tất cả output ports của một node, sắp xếp theo ExecutionOrder
        /// </summary>
        public List<NodePort> GetOrderedOutputPorts(WorkflowNode node)
        {
            return node.Ports
                .Where(p => !p.IsInput && p.IsVisible)
                .OrderBy(p => p.ExecutionOrder)
                .ThenBy(p => p.Position) // Nếu cùng ExecutionOrder, sắp xếp theo Position
                .ToList();
        }

        /// <summary>
        /// Xác định luồng thực thi từ Start node đến End node.
        /// Trả về danh sách các node theo thứ tự thực thi.
        /// </summary>
        public List<WorkflowNode> DetermineExecutionFlow(
            IEnumerable<WorkflowNode> nodes,
            IEnumerable<WorkflowConnection> connections)
        {
            var nodeList = nodes.ToList();
            var connectionList = connections.ToList();
            var executionOrder = new List<WorkflowNode>();
            var visited = new HashSet<WorkflowNode>();

            // Tìm Start nodes
            var startNodes = FindStartNodes(nodeList);
            if (startNodes.Count == 0)
            {
                return executionOrder; // Không có Start node
            }

            // Bắt đầu từ mỗi Start node
            foreach (var startNode in startNodes)
            {
                TraverseFromNode(startNode, connectionList, executionOrder, visited);
            }

            return executionOrder;
        }

        /// <summary>
        /// Duyệt đồ thị từ một node, theo thứ tự execution của các output ports
        /// </summary>
        private void TraverseFromNode(
            WorkflowNode currentNode,
            List<WorkflowConnection> connections,
            List<WorkflowNode> executionOrder,
            HashSet<WorkflowNode> visited)
        {
            // Nếu đã thăm rồi, bỏ qua (tránh vòng lặp)
            if (visited.Contains(currentNode))
            {
                return;
            }

            // Thêm node vào danh sách thực thi
            if (!executionOrder.Contains(currentNode))
            {
                executionOrder.Add(currentNode);
            }
            visited.Add(currentNode);

            // Nếu là End node, dừng lại
            if (currentNode.Type == NodeType.End)
            {
                return;
            }

            // Lấy tất cả output ports đã sắp xếp
            var outputPorts = GetOrderedOutputPorts(currentNode);
            if (outputPorts.Count == 0)
            {
                return; // Không có output ports
            }

            // Nhóm ports theo ExecutionMode
            var sequentialPorts = outputPorts.Where(p => p.ExecutionMode == PortExecutionMode.Sequential).ToList();
            var parallelPorts = outputPorts.Where(p => p.ExecutionMode == PortExecutionMode.Parallel).ToList();

            // Xử lý Sequential ports trước (theo thứ tự)
            foreach (var port in sequentialPorts)
            {
                var portConnections = connections.Where(c => c.FromNode == currentNode && c.FromPort == port).ToList();
                foreach (var conn in portConnections)
                {
                    if (conn.ToNode != null && !visited.Contains(conn.ToNode))
                    {
                        TraverseFromNode(conn.ToNode, connections, executionOrder, visited);
                    }
                }
            }

            // Xử lý Parallel ports (có thể chạy song song)
            // Trong mô hình này, chúng ta vẫn duyệt tuần tự nhưng đánh dấu là parallel
            // Runtime execution engine sẽ quyết định cách thực thi song song
            foreach (var port in parallelPorts.OrderBy(p => p.ExecutionOrder))
            {
                var portConnections = connections.Where(c => c.FromNode == currentNode && c.FromPort == port).ToList();
                foreach (var conn in portConnections)
                {
                    if (conn.ToNode != null && !visited.Contains(conn.ToNode))
                    {
                        TraverseFromNode(conn.ToNode, connections, executionOrder, visited);
                    }
                }
            }
        }

        /// <summary>
        /// Kiểm tra xem workflow có hợp lệ không (có Start và End nodes, không có vòng lặp flow)
        /// </summary>
        public WorkflowValidationResult ValidateWorkflow(
            IEnumerable<WorkflowNode> nodes,
            IEnumerable<WorkflowConnection> connections)
        {
            var nodeList = nodes.ToList();
            var connectionList = connections.ToList();
            var result = new WorkflowValidationResult();

            // Kiểm tra Start nodes
            var startNodes = FindStartNodes(nodeList);
            if (startNodes.Count == 0)
            {
                result.Errors.Add("Workflow phải có ít nhất một Start node");
            }

            // Kiểm tra End nodes
            var endNodes = FindEndNodes(nodeList);
            if (endNodes.Count == 0)
            {
                result.Errors.Add("Workflow phải có ít nhất một End node");
            }

            var scopedStarts = startNodes
                .Where(s => !string.IsNullOrWhiteSpace(s.FlowScopeKey))
                .ToList();
            var duplicatedScopeKeys = scopedStarts
                .GroupBy(s => s.FlowScopeKey!, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicatedScopeKeys.Count > 0)
            {
                result.Warnings.Add($"FlowScopeKey bị trùng: {string.Join(", ", duplicatedScopeKeys)}. Điều này có thể gây khó truy vết nhánh async.");
            }

            var hasSubFlowStartWithoutEnd = startNodes.Any(s =>
                s.RunMode != FlowRunMode.MainFlow &&
                !HasReachableEndNode(s, connectionList, endNodes));
            if (hasSubFlowStartWithoutEnd)
            {
                result.Warnings.Add("Có SubFlow Start không đi tới End node nào. Hãy kiểm tra để tránh luồng con chạy dở dang.");
            }

            // Kiểm tra vòng lặp flow.
            // Trước đây flow loops bị chặn hoàn toàn, nhưng để hỗ trợ các kịch bản tái sử dụng node
            // (ví dụ Start -> A -> B -> M -> A và M -> C) chúng ta chỉ cảnh báo,
            // cho phép user tự kiểm soát tránh lặp vô hạn trong logic.
            var hasFlowLoop = DetectFlowLoop(connectionList);
            if (hasFlowLoop)
            {
                result.Warnings.Add("Workflow đang có vòng lặp flow (flow loop). Hãy đảm bảo không tạo lặp vô hạn (ví dụ M -> A -> B -> M ...).");
            }

            // Kiểm tra kết nối hợp lệ (chỉ cho phép outPort -> inPort)
            foreach (var conn in connectionList)
            {
                if (conn.FromPort != null && conn.FromPort.IsInput)
                {
                    result.Errors.Add($"Connection không hợp lệ: không thể kết nối từ input port của node '{conn.FromNode?.Title}'");
                }
                if (conn.ToPort != null && !conn.ToPort.IsInput)
                {
                    result.Errors.Add($"Connection không hợp lệ: không thể kết nối đến output port của node '{conn.ToNode?.Title}'");
                }
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        private static bool HasReachableEndNode(
            WorkflowNode startNode,
            List<WorkflowConnection> connections,
            List<WorkflowNode> endNodes)
        {
            if (endNodes.Count == 0) return false;
            var visited = new HashSet<WorkflowNode>();
            var queue = new Queue<WorkflowNode>();
            visited.Add(startNode);
            queue.Enqueue(startNode);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Type == NodeType.End) return true;

                foreach (var next in connections.Where(c => c.FromNode == current && c.ToNode != null).Select(c => c.ToNode!))
                {
                    if (visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            return false;
        }

        /// <summary>
        /// Phát hiện vòng lặp flow trong workflow
        /// </summary>
        private bool DetectFlowLoop(List<WorkflowConnection> connections)
        {
            // ✅ Allow cycles inside LoopBody cluster (LoopBody logic is meant to be "code-like")
            // but still forbid flow cycles outside loop containers.
            var loopBodyClusters = BuildLoopBodyClusters(connections);

            // Tạo đồ thị hướng từ connections
            var graph = new Dictionary<WorkflowNode, List<WorkflowNode>>();
            foreach (var conn in connections)
            {
                if (conn.FromNode != null && conn.ToNode != null)
                {
                    // Skip edges fully inside the same LoopBody cluster (allowed to have cycles there)
                    if (IsEdgeInsideAnyLoopBodyCluster(conn.FromNode, conn.ToNode, loopBodyClusters))
                    {
                        continue;
                    }

                    if (!graph.ContainsKey(conn.FromNode))
                    {
                        graph[conn.FromNode] = new List<WorkflowNode>();
                    }
                    graph[conn.FromNode].Add(conn.ToNode);
                }
            }

            // DFS để phát hiện cycle
            var visited = new HashSet<WorkflowNode>();
            var recStack = new HashSet<WorkflowNode>();

            foreach (var node in graph.Keys)
            {
                if (HasCycle(node, graph, visited, recStack))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEdgeInsideAnyLoopBodyCluster(
            WorkflowNode from,
            WorkflowNode to,
            List<HashSet<WorkflowNode>> clusters)
        {
            foreach (var cluster in clusters)
            {
                if (cluster.Contains(from) && cluster.Contains(to))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Build undirected connectivity clusters for each LoopBodyNode, excluding the parent LoopNode header
        /// so traversal doesn't leak outside the container via default connection.
        /// </summary>
        private static List<HashSet<WorkflowNode>> BuildLoopBodyClusters(List<WorkflowConnection> connections)
        {
            var nodes = new HashSet<WorkflowNode>();
            foreach (var c in connections)
            {
                if (c.FromNode != null) nodes.Add(c.FromNode);
                if (c.ToNode != null) nodes.Add(c.ToNode);
            }

            var loops = nodes.OfType<LoopNode>().ToList();
            var clusters = new List<HashSet<WorkflowNode>>();

            foreach (var loop in loops)
            {
                var body = loop.LoopBodyNode;
                if (body == null) continue;

                var visited = new HashSet<WorkflowNode> { body };
                var queue = new Queue<WorkflowNode>();
                queue.Enqueue(body);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();

                    var neighbors = connections
                        .Where(c => c.FromNode == current || c.ToNode == current)
                        .Select(c => c.FromNode == current ? c.ToNode : c.FromNode)
                        .Where(n => n != null)!;

                    foreach (var neighbor in neighbors)
                    {
                        // Bỏ qua LoopNode cha để không lan ra ngoài qua default connection
                        if (ReferenceEquals(neighbor, loop)) continue;

                        if (visited.Add(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                // Include body itself in cluster (helps skipping edges that touch the body node)
                clusters.Add(visited);
            }

            return clusters;
        }

        private bool HasCycle(
            WorkflowNode node,
            Dictionary<WorkflowNode, List<WorkflowNode>> graph,
            HashSet<WorkflowNode> visited,
            HashSet<WorkflowNode> recStack)
        {
            if (recStack.Contains(node))
            {
                return true; // Phát hiện cycle
            }

            if (visited.Contains(node))
            {
                return false;
            }

            visited.Add(node);
            recStack.Add(node);

            if (graph.ContainsKey(node))
            {
                foreach (var neighbor in graph[node])
                {
                    if (HasCycle(neighbor, graph, visited, recStack))
                    {
                        return true;
                    }
                }
            }

            recStack.Remove(node);
            return false;
        }

        /// <summary>
        /// Lấy thông tin về các node tiếp theo từ một node cụ thể
        /// </summary>
        public List<WorkflowNode> GetNextNodes(
            WorkflowNode currentNode,
            IEnumerable<WorkflowConnection> allConnections)
        {
            var outputPorts = GetOrderedOutputPorts(currentNode);
            var nextNodes = new List<WorkflowNode>();

            foreach (var port in outputPorts)
            {
                var connections = allConnections
                    .Where(c => c.FromNode == currentNode && c.FromPort == port)
                    .ToList();

                foreach (var conn in connections)
                {
                    if (conn.ToNode != null && !nextNodes.Contains(conn.ToNode))
                    {
                        nextNodes.Add(conn.ToNode);
                    }
                }
            }

            return nextNodes;
        }

        /// <summary>
        /// Lấy thông tin về các node trước đó từ một node cụ thể
        /// </summary>
        public List<WorkflowNode> GetPreviousNodes(
            WorkflowNode currentNode,
            IEnumerable<WorkflowConnection> allConnections)
        {
            var inputPorts = currentNode.Ports.Where(p => p.IsInput && p.IsVisible).ToList();
            var previousNodes = new List<WorkflowNode>();

            foreach (var port in inputPorts)
            {
                var connections = allConnections
                    .Where(c => c.ToNode == currentNode && c.ToPort == port)
                    .ToList();

                foreach (var conn in connections)
                {
                    if (conn.FromNode != null && !previousNodes.Contains(conn.FromNode))
                    {
                        previousNodes.Add(conn.FromNode);
                    }
                }
            }

            return previousNodes;
        }

        /// <summary>
        /// Tính tập các node có thể dẫn tới bất kỳ End node nào (theo flow).
        /// Dùng để loại bỏ các nhánh không nối được tới End khi thực thi.
        /// </summary>
        private HashSet<WorkflowNode> ComputeNodesReachingEnd(IEnumerable<WorkflowConnection> connections)
        {
            var connectionList = connections.ToList();

            // Xây đồ thị ngược: ToNode -> list FromNode
            var reverseGraph = new Dictionary<WorkflowNode, List<WorkflowNode>>();

            foreach (var conn in connectionList)
            {
                if (conn.FromNode == null || conn.ToNode == null) continue;

                if (!reverseGraph.TryGetValue(conn.ToNode, out var fromList))
                {
                    fromList = new List<WorkflowNode>();
                    reverseGraph[conn.ToNode] = fromList;
                }
                if (!fromList.Contains(conn.FromNode))
                {
                    fromList.Add(conn.FromNode);
                }
            }

            // Tìm tất cả End nodes
            var endNodes = connectionList
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .Where(n => n != null && n.Type == NodeType.End)
                .Distinct()
                .ToList();
            var reachable = new HashSet<WorkflowNode>();

            if (endNodes.Count == 0)
            {
                // Không có End node -> không filter gì, cho phép chạy toàn bộ graph hiện tại
                return reachable;
            }

            // BFS/DFS ngược từ các End node
            var queue = new Queue<WorkflowNode>();
            foreach (var end in endNodes)
            {
                if (reachable.Add(end))
                {
                    queue.Enqueue(end);
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!reverseGraph.TryGetValue(current, out var preds)) continue;

                foreach (var pred in preds)
                {
                    if (reachable.Add(pred))
                    {
                        queue.Enqueue(pred);
                    }
                }
            }

            // MỞ RỘNG: giữ lại toàn bộ các node nằm trong cùng "cụm kết nối" (undirected)
            // với bất kỳ node nào có thể reach End.
            //
            // Điều này đảm bảo:
            // - Các node chỉ nối "bên hông" vào flow chính (ví dụ Storage chỉ có 1 connection ra từ ScreenCapture)
            //   nhưng vẫn nằm trong cụm Start-End sẽ KHÔNG bị coi là nhánh rác.
            // - Chỉ các node hoàn toàn tách biệt khỏi cụm Start-End mới bị bỏ qua.
            if (reachable.Count > 0)
            {
                var undirected = new Dictionary<WorkflowNode, List<WorkflowNode>>();

                foreach (var conn in connectionList)
                {
                    if (conn.FromNode == null || conn.ToNode == null) continue;

                    if (!undirected.TryGetValue(conn.FromNode, out var fromNeighbors))
                    {
                        fromNeighbors = new List<WorkflowNode>();
                        undirected[conn.FromNode] = fromNeighbors;
                    }
                    if (!fromNeighbors.Contains(conn.ToNode))
                    {
                        fromNeighbors.Add(conn.ToNode);
                    }

                    if (!undirected.TryGetValue(conn.ToNode, out var toNeighbors))
                    {
                        toNeighbors = new List<WorkflowNode>();
                        undirected[conn.ToNode] = toNeighbors;
                    }
                    if (!toNeighbors.Contains(conn.FromNode))
                    {
                        toNeighbors.Add(conn.FromNode);
                    }
                }

                var queue2 = new Queue<WorkflowNode>(reachable);
                while (queue2.Count > 0)
                {
                    var current = queue2.Dequeue();
                    if (!undirected.TryGetValue(current, out var neighbors)) continue;

                    foreach (var neighbor in neighbors)
                    {
                        if (reachable.Add(neighbor))
                        {
                            queue2.Enqueue(neighbor);
                        }
                    }
                }
            }

            return reachable;
        }

        /// <summary>
        /// Thực thi node theo luồng connections.
        /// - Chỉ đi theo các nhánh có thể dẫn tới End node (loại bỏ các nhánh "cụt" không nối được tới End).
        /// - Hỗ trợ các node có action riêng (Delay, MouseEvent, KeyPress, HotkeyPress, v.v.).
        /// 
        /// Để dễ bảo trì, hàm này chỉ điều phối control-flow chính và ủy quyền
        /// cho các handler nhỏ hơn theo từng loại node.
        /// </summary>
        public async Task ExecuteNodeAsync(
            WorkflowNode node,
            IEnumerable<WorkflowConnection> connections,
            CancellationToken cancellationToken,
            Action<WorkflowConnection?>? onEnteringNode = null,
            Action<WorkflowNode, WorkflowConnection?>? onNodeStarted = null,
            Action<WorkflowNode, TimeSpan>? onNodeCompleted = null,
            Action<WorkflowNode, string>? onNodeFailed = null,
            WorkflowConnection? incomingConnection = null,
            HashSet<WorkflowNode>? reachableToEnd = null,
            bool isReuseRouteTerminal = false,
            List<string>? executionPath = null,
            string? executionId = null,
            string? flowScopeId = null,
            string? branchId = null,
            string? parentFlowScopeId = null)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            if (string.IsNullOrWhiteSpace(executionId))
                executionId = Guid.NewGuid().ToString("N");

            var connectionList = connections?.ToList() ?? new List<WorkflowConnection>();

            // Tính trước tập các node có thể đi tới End (dùng chung cho toàn bộ đệ quy)
            if (reachableToEnd == null)
            {
                reachableToEnd = ComputeNodesReachingEnd(connectionList);
            }

            // Nếu workflow có End node và node hiện tại KHÔNG nằm trên bất kỳ đường nào tới End
            // => bỏ qua, không thực thi logic và cũng không đi tiếp (nhánh "rác").
            // ⚠️ NGOẠI LỆ: StorageNode LUÔN được chạy khi có incoming connection (để set dữ liệu)
            if (reachableToEnd.Count > 0 &&
                node.Type != NodeType.End &&
                node is not FlowMy.Models.Nodes.StorageNode &&
                !reachableToEnd.Contains(node))
            {
                return;
            }

            var resolvedFlowScopeId = ResolveFlowScopeId(node, flowScopeId, incomingConnection, parentFlowScopeId, out var resolvedParentScopeId);
            var resolvedBranchId = string.IsNullOrWhiteSpace(branchId) ? "main" : branchId!;

            node.LastExecutionId = executionId;
            node.LastFlowScopeId = resolvedFlowScopeId;
            node.LastBranchId = resolvedBranchId;
            node.LastParentFlowScopeId = resolvedParentScopeId;

            // AsyncLocal context: concurrent AsyncTask dispatches share the same WorkflowNode
            // instance, so the field above can be raced. AsyncLocal isolates each dispatch per async flow.
            WorkflowExecutionContext.CurrentExecutionId = executionId;

            // Báo cho UI biết connection dẫn tới node đang xử lý (nếu có)
            onEnteringNode?.Invoke(incomingConnection);
            onNodeStarted?.Invoke(node, incomingConnection);

            // ── AutoTrigger: Kiểm tra và kích hoạt chức năng tự động từ ReuseRoutes ──
            // Khi workflow chạy đến node này, kiểm tra xem có node nào cấu hình FunctionType trong ReuseRoutes không
            // Nếu có FunctionType = "Capture", kích hoạt cơ chế chụp ảnh trước khi chạy logic
            await ProcessReuseRouteFunctionTypeAsync(node, incomingConnection, connectionList, cancellationToken).ConfigureAwait(false);

            // Flow-control trong LoopBody (Break / Continue)
            if (node is BreakNode)
            {
                onNodeCompleted?.Invoke(node, TimeSpan.Zero);
                throw new LoopBreakException();
            }
            if (node is ContinueNode)
            {
                onNodeCompleted?.Invoke(node, TimeSpan.Zero);
                throw new LoopContinueException();
            }

            // Điều phối theo INodeExecutor
            var env = new NodeExecutors.NodeExecutionEnvironment(
                service: this,
                connections: connectionList,
                cancellationToken: cancellationToken,
                onEnteringNode: onEnteringNode,
                onNodeStarted: onNodeStarted,
                onNodeCompleted: onNodeCompleted,
                onNodeFailed: onNodeFailed,
                incomingConnection: incomingConnection,
                reachableToEnd: reachableToEnd,
                refreshOnly: false,
                isReuseRouteTerminal: isReuseRouteTerminal,
                executionPath: executionPath,
                executionId: executionId,
                flowScopeId: resolvedFlowScopeId,
                branchId: resolvedBranchId,
                parentFlowScopeId: resolvedParentScopeId);


            var executor = _nodeExecutors.FirstOrDefault(e => e.CanExecute(node))
                           ?? _nodeExecutors.Last(); // DefaultNodeExecutor

            try
            {
                try
                {
                    // ConfigureAwait(false): tránh marshal ngược về WPF SynchronizationContext sau mỗi await
                    // bên trong executor (HTTP call, File IO, v.v.) — giữ execution trên ThreadPool thread.
                    await executor.ExecuteAsync(node, env).ConfigureAwait(false);

                    // Sau khi node chạy xong, tự động mirror outputs sang các StorageNode trỏ tới node này
                    MirrorOutputsToStorageNodes(node, connectionList, reachableToEnd, executionId);
                }
                finally
                {
                    if (!env.RefreshOnly)
                        MirrorRuntimeOutputsToScopedStore(node, executionId);
                }
            }
            catch (Exception ex) when (ex is not LoopBreakException and not LoopContinueException)
            {
                // Không gọi onNodeFailed ở đây: khi node con throw, exception nổi lên và catch này chạy với node = node CHA.
                // Chỉ executor của node thực sự lỗi đã gọi env.OnNodeFailed(nodeLỗi) trước khi throw → chỉ node đó hiện toggle lỗi.
                throw;
            }
        }

        /// <summary>
        /// Chỉ chạy logic của node (cập nhật output), không chạy các node tiếp theo.
        /// Dùng khi AssignData cần lấy giá trị mới nhất từ node nguồn (RefreshSourceBeforeUse),
        /// hoặc khi nhấn nút Play trong dialog (single node run).
        /// </summary>
        /// <param name="allNodesForLookup">Khi chạy single node từ dialog: truyền toàn bộ nodes của workflow để executor có thể resolve node nguồn theo Id (ví dụ MediaGalleryNode.JsonSourceNodeId). Null = dùng set rỗng.</param>
        internal async Task ExecuteNodeLogicOnlyAsync(
            WorkflowNode node,
            List<WorkflowConnection> connections,
            CancellationToken cancellationToken,
            IReadOnlyList<WorkflowNode>? allNodesForLookup = null)
        {
            if (node == null) return;
            var reachableToEnd = allNodesForLookup != null && allNodesForLookup.Count > 0
                ? new HashSet<WorkflowNode>(allNodesForLookup)
                : new HashSet<WorkflowNode>();
            var env = new NodeExecutors.NodeExecutionEnvironment(
                service: this,
                connections: connections,
                cancellationToken: cancellationToken,
                onEnteringNode: null,
                onNodeStarted: null,
                onNodeCompleted: null,
                onNodeFailed: null, // onNodeFailed
                incomingConnection: null,
                reachableToEnd: reachableToEnd,
                refreshOnly: true,
                isReuseRouteTerminal: false,
                executionPath: null,
                executionId: null,
                flowScopeId: "single-node",
                branchId: "main",
                parentFlowScopeId: null);
            var executor = _nodeExecutors.FirstOrDefault(e => e.CanExecute(node))
                           ?? _nodeExecutors.Last();
            await executor.ExecuteAsync(node, env);
        }

        /// <summary>
        /// Xử lý FunctionType từ ReuseRoutes: Khi workflow chạy đến node này, kiểm tra xem có node nào cấu hình FunctionType trong ReuseRoutes không
        /// Nếu có FunctionType = "Capture", kích hoạt cơ chế chụp ảnh trước khi chạy logic
        /// </summary>
        private async Task ProcessReuseRouteFunctionTypeAsync(
            WorkflowNode node,
            WorkflowConnection? incomingConnection,
            List<WorkflowConnection> connections,
            CancellationToken cancellationToken)
        {
            if (node == null || connections == null) return;
            if (node.ReuseRoutes == null || node.ReuseRoutes.Count == 0) return;

            // Debug log: hiển thị tất cả ReuseRoute của node
            System.Diagnostics.Debug.WriteLine($"ProcessReuseRouteFunctionType: Node {node.Title} ({node.Id}) has {node.ReuseRoutes.Count} ReuseRoutes, IncomingConnection: {incomingConnection?.FromNode?.Title ?? "null"}");

            // Tìm ReuseRoute tương ứng với incoming connection hiện tại
            var incomingNodeId = incomingConnection?.FromNode?.Id;
            var matchingRoute = node.ReuseRoutes.FirstOrDefault(r => 
                string.Equals(r.IncomingNodeId, incomingNodeId, StringComparison.OrdinalIgnoreCase));

            if (matchingRoute != null)
            {
                System.Diagnostics.Debug.WriteLine($"  - Found matching route: Incoming: {matchingRoute.IncomingNodeId}, Outgoing: {matchingRoute.OutgoingNodeId}, FunctionType: '{matchingRoute.FunctionType}'");

                // Chỉ trigger capture nếu ReuseRoute tương ứng có FunctionType = "Capture"
                if (string.Equals(matchingRoute.FunctionType, "Capture", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"ProcessReuseRouteFunctionType: Triggering capture for route from {matchingRoute.IncomingNodeId} to {matchingRoute.OutgoingNodeId}");
                    await ExecuteScreenCaptureAsync(node, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ProcessReuseRouteFunctionType: FunctionType is '{matchingRoute.FunctionType}', not triggering capture");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"  - No matching route found for incoming node {incomingNodeId}");
            }
        }

        /// <summary>
        /// Thực thi chụp màn hình cho node
        /// </summary>
        private async Task ExecuteScreenCaptureAsync(WorkflowNode node, CancellationToken cancellationToken)
        {
            // Khi FunctionType = "Capture", luôn trigger capture mỗi lần workflow chạy vào node
            // Không check xem node đã có ảnh hay chưa
            System.Diagnostics.Debug.WriteLine($"AutoTrigger: Triggering screen capture for {node.Type} node {node.Id}");

            // Gọi ScreenCaptureHelper trực tiếp trên UI thread
            bool captureResult = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // Lấy main window
                    var mainWindow = System.Windows.Application.Current.MainWindow;
                    if (mainWindow == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"AutoTrigger: Main window not found");
                        return false;
                    }

                    // Sử dụng ScreenCaptureHelper để thực hiện capture
                    if (node is TextScanNode textScanNode)
                    {
                        return FlowMy.Helpers.ScreenCaptureHelper.CaptureForTextScanNode(textScanNode, mainWindow);
                    }
                    else if (node is ScreenCaptureNode screenCaptureNode)
                    {
                        return FlowMy.Helpers.ScreenCaptureHelper.CaptureForScreenCaptureNode(screenCaptureNode, mainWindow);
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AutoTrigger: Error during screen capture: {ex.Message}");
                    return false;
                }
            });

            if (captureResult)
            {
                System.Diagnostics.Debug.WriteLine($"AutoTrigger: Screen capture completed for {node.Type} node {node.Id}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"AutoTrigger: Screen capture cancelled for {node.Type} node {node.Id}");
            }
        }

        private static string ResolveFlowScopeId(
            WorkflowNode node,
            string? inheritedScopeId,
            WorkflowConnection? incomingConnection,
            string? inheritedParentScopeId,
            out string? parentScopeId)
        {
            var inherited = string.IsNullOrWhiteSpace(inheritedScopeId) ? "main" : inheritedScopeId!;
            parentScopeId = inheritedParentScopeId;

            if (node.Type != NodeType.Start)
                return inherited;

            var configuredKey = node.FlowScopeKey?.Trim();
            switch (node.RunMode)
            {
                case FlowRunMode.MainFlow:
                    parentScopeId = null;
                    return string.IsNullOrWhiteSpace(configuredKey) ? "main" : configuredKey!;
                case FlowRunMode.SubFlowIndependent:
                    parentScopeId = inherited;
                    return string.IsNullOrWhiteSpace(configuredKey)
                        ? $"sub-{node.Id}"
                        : configuredKey!;
                case FlowRunMode.AutoScheduled:
                    parentScopeId = null;
                    return string.IsNullOrWhiteSpace(configuredKey)
                        ? $"auto-{node.Id}"
                        : configuredKey!;
                case FlowRunMode.SubFlowAttached:
                default:
                    parentScopeId = incomingConnection == null ? null : inherited;
                    if (!string.IsNullOrWhiteSpace(configuredKey))
                        return configuredKey!;
                    return incomingConnection == null
                        ? inherited
                        : $"{inherited}/sub-{node.Id}";
            }
        }

        /// <summary>
        /// Helper dùng chung cho các node có đúng một output port chính + hỗ trợ legacy connections.
        /// Dùng cho KeyPressEventNode, HotkeyPressEventNode, v.v.
        /// </summary>
        internal async Task TraverseSingleOutputAndLegacyAsync(
            WorkflowNode node,
            List<WorkflowConnection> connectionList,
            CancellationToken cancellationToken,
            Action<WorkflowConnection?>? onEnteringNode,
            Action<WorkflowNode, WorkflowConnection?>? onNodeStarted,
            Action<WorkflowNode, TimeSpan>? onNodeCompleted,
            Action<WorkflowNode, string>? onNodeFailed,
            HashSet<WorkflowNode> reachableToEnd,
            string? executionId = null,
            string? flowScopeId = null,
            string? branchId = null,
            string? parentFlowScopeId = null,
            List<string>? executionPath = null)
        {
            var outputPort = node.Ports.FirstOrDefault(p => !p.IsInput && p.IsVisible);
            if (outputPort != null)
            {
                var nextConnections = GetConnectionsFromPort(outputPort, node, connectionList);
                foreach (var conn in nextConnections)
                {
                    if (conn.ToNode != null)
                    {
                        if (IsLoopBodyReturnConnection(conn))
                        {
                            SignalLoopBodyReturn(conn, executionId ?? string.Empty, branchId);
                            continue;
                        }
                        await ExecuteNodeAsync(conn.ToNode, connectionList, cancellationToken,
                            onEnteringNode, onNodeStarted, onNodeCompleted, onNodeFailed, conn, reachableToEnd,
                            false, executionPath, executionId, flowScopeId, branchId, parentFlowScopeId).ConfigureAwait(false);
                    }
                }
            }

            var legacyNext = connectionList
                .Where(c => c.FromNode == node && c.FromPort == null)
                .ToList();
            foreach (var conn in legacyNext)
            {
                if (conn.ToNode != null)
                {
                    if (IsLoopBodyReturnConnection(conn))
                    {
                        SignalLoopBodyReturn(conn, executionId ?? string.Empty, branchId);
                        continue;
                    }
                    await ExecuteNodeAsync(conn.ToNode, connectionList, cancellationToken,
                        onEnteringNode, onNodeStarted, onNodeCompleted, onNodeFailed, conn, reachableToEnd,
                        false, executionPath, executionId, flowScopeId, branchId, parentFlowScopeId).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Resolve giá trị điều kiện từ node nguồn nối vào input port của conditional node.
        /// Dùng cho ConditionalNodeExecutor khi đánh giá if/else if.
        /// </summary>
        internal string ResolveConditionFromUpstream(
            WorkflowNode conditionalNode,
            string? key,
            List<WorkflowConnection> connections)
        {
            if (conditionalNode?.Ports == null || string.IsNullOrWhiteSpace(key)) return string.Empty;

            var inputPort = conditionalNode.Ports.FirstOrDefault(p => p.IsInput && p.IsVisible);
            if (inputPort == null) return string.Empty;

            var conn = connections.FirstOrDefault(c =>
                c.ToNode == conditionalNode &&
                (ReferenceEquals(c.ToPort, inputPort) ||
                 (c.ToPort != null && c.ToPort.IsInput && (string.IsNullOrEmpty(inputPort.Id) || string.Equals(c.ToPort.Id, inputPort.Id, StringComparison.OrdinalIgnoreCase)))));
            var fromNode = conn?.FromNode;
            if (fromNode == null) return string.Empty;

            return ResolveDynamicValueByKey(fromNode, key.Trim());
        }

        /// <summary>
        /// Resolve giá trị từ node theo Id (trong graph) và key. Dùng cho điều kiện Left/Right trong dialog.
        /// </summary>
        internal string ResolveValueByNodeIdAndKey(
            IEnumerable<WorkflowConnection> connections,
            string? nodeId,
            string? key)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(key)) return string.Empty;
            var node = connections
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .FirstOrDefault(n => n != null && string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            return node == null ? string.Empty : ResolveDynamicValueByKey(node, key.Trim());
        }

        /// <summary>
        /// So sánh hai giá trị theo toán tử. Trả về true nếu điều kiện thỏa.
        /// </summary>
        internal static bool EvaluateCondition(string? left, string? right, ConditionOperator op)
        {
            left = left?.Trim() ?? string.Empty;
            right = right?.Trim() ?? string.Empty;

            switch (op)
            {
                case ConditionOperator.Equal: return string.Equals(left, right, StringComparison.Ordinal);
                case ConditionOperator.NotEqual: return !string.Equals(left, right, StringComparison.Ordinal);
                case ConditionOperator.GreaterThan: return TryCompareNumeric(left, right, out var gt) && gt > 0;
                case ConditionOperator.GreaterThanOrEqual: return TryCompareNumeric(left, right, out var ge) && ge >= 0;
                case ConditionOperator.LessThan: return TryCompareNumeric(left, right, out var lt) && lt < 0;
                case ConditionOperator.LessThanOrEqual: return TryCompareNumeric(left, right, out var le) && le <= 0;
                case ConditionOperator.Contains: return left.IndexOf(right, StringComparison.OrdinalIgnoreCase) >= 0;
                case ConditionOperator.NotContains: return left.IndexOf(right, StringComparison.OrdinalIgnoreCase) < 0;
                case ConditionOperator.TextEquals: return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
                case ConditionOperator.TextNotEquals: return !string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
                case ConditionOperator.Empty: return string.IsNullOrEmpty(left);
                case ConditionOperator.NotEmpty: return !string.IsNullOrEmpty(left);
                case ConditionOperator.True: return ConditionValueToBool(left);
                case ConditionOperator.False: return !ConditionValueToBool(left);
                default: return false;
            }
        }

        private static bool TryCompareNumeric(string left, string right, out int result)
        {
            result = 0;
            if (double.TryParse(left, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var l) &&
                double.TryParse(right, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var r))
            {
                result = l.CompareTo(r);
                return true;
            }
            result = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        /// <summary>
        /// Chuyển giá trị string thành bool cho điều kiện if/else if.
        /// true: "true", "1", "yes", số khác 0; false: "false", "0", "no", empty.
        /// </summary>
        internal static bool ConditionValueToBool(string? value)
        {
            if (value == null) return false;
            var v = value.Trim();
            if (string.IsNullOrEmpty(v)) return false;
            if (string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(v, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(v, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(v, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(v, "no", StringComparison.OrdinalIgnoreCase))
                return false;
            if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
                return num != 0;
            return true; // non-empty, non-numeric => truthy
        }

        private sealed class DelegateDisposable : IDisposable
        {
            private readonly Action _dispose;
            private bool _disposed;
            public DelegateDisposable(Action dispose) { _dispose = dispose; }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _dispose();
            }
        }

        private sealed class LoopBodyReturnWaiter
        {
            public TaskCompletionSource<WorkflowConnection> Tcs { get; }
            public CancellationTokenSource? HardStopCts { get; }
            public LoopBodyReturnWaiter(TaskCompletionSource<WorkflowConnection> tcs, CancellationTokenSource? hardStopCts)
            {
                Tcs = tcs;
                HardStopCts = hardStopCts;
            }
        }

        internal sealed class LoopBreakException : Exception { }
        internal sealed class LoopContinueException : Exception { }

        internal static bool IsLoopBodyReturnConnection(WorkflowConnection conn)
        {
            return conn.ToNode is LoopBodyNode or AsyncTaskBodyNode
                   && conn.ToPort != null
                   && string.Equals(conn.ToPort.Id, "LoopBodyRight", StringComparison.OrdinalIgnoreCase);
        }

        internal static void EnsureLoopBodyPortsExist(LoopBodyNode bodyNode)
            => EnsureLoopLikeBodyPorts(bodyNode);

        internal static void EnsureAsyncTaskBodyPortsExist(AsyncTaskBodyNode bodyNode)
            => EnsureLoopLikeBodyPorts(bodyNode);

        private static void EnsureLoopLikeBodyPorts(WorkflowNode bodyNode)
        {
            // Must match the semantic roles used in LoopNodeRenderer / ConnectionHandler
            if (bodyNode.Ports.All(p => p.Id != "LoopBodyTop"))
            {
                bodyNode.Ports.Add(new NodePort
                {
                    Id = "LoopBodyTop",
                    IsInput = true,
                    Position = PortPosition.Top,
                    IsVisible = true,
                    CanDeleteConnection = false
                });
            }
            if (bodyNode.Ports.All(p => p.Id != "LoopBodyLeft"))
            {
                bodyNode.Ports.Add(new NodePort
                {
                    Id = "LoopBodyLeft",
                    IsInput = false,
                    Position = PortPosition.Right, // inward-facing
                    IsVisible = true
                });
            }
            if (bodyNode.Ports.All(p => p.Id != "LoopBodyRight"))
            {
                bodyNode.Ports.Add(new NodePort
                {
                    Id = "LoopBodyRight",
                    IsInput = true,
                    Position = PortPosition.Left, // inward-facing
                    IsVisible = true
                });
            }
        }

        internal IEnumerable<(int index, string? item)> ResolveAsyncTaskDispatchIterations(
            AsyncTaskNode asyncTaskNode,
            List<WorkflowConnection> connections,
            NodeExecutors.NodeExecutionEnvironment env)
        {
            switch (asyncTaskNode.DispatchLoopType)
            {
                case LoopType.ForEachArray:
                    {
                        var items = ResolveLoopArray(asyncTaskNode, connections, env);
                        for (int i = 0; i < items.Count; i++)
                            yield return (i, items[i]);
                        yield break;
                    }
                case LoopType.ForLoop:
                    {
                        var start = asyncTaskNode.StartIndex;
                        var end = asyncTaskNode.EndIndex;
                        var step = start <= end ? 1 : -1;
                        for (int i = start; step > 0 ? i <= end : i >= end; i += step)
                            yield return (i, null);
                        yield break;
                    }
                case LoopType.RepeatN:
                default:
                    {
                        var count = ResolveLoopCount(asyncTaskNode, connections, env) ?? asyncTaskNode.RepeatCount;
                        if (count < 1) count = 1;
                        for (int i = 0; i < count; i++)
                            yield return (i, null);
                        yield break;
                    }
            }
        }

        internal IEnumerable<(int index, string? item)> ResolveLoopIterations(
            LoopNode loopNode,
            List<WorkflowConnection> connections,
            NodeExecutors.NodeExecutionEnvironment env)
        {
            switch (loopNode.LoopType)
            {
                case LoopType.ForEachArray:
                    {
                        var items = ResolveLoopArray(loopNode, connections, env);
                        for (int i = 0; i < items.Count; i++)
                        {
                            yield return (i, items[i]);
                        }
                        yield break;
                    }
                case LoopType.ForLoop:
                    {
                        var start = loopNode.StartIndex;
                        var end = loopNode.EndIndex;
                        var step = start <= end ? 1 : -1;
                        for (int i = start; step > 0 ? i <= end : i >= end; i += step)
                        {
                            yield return (i, null);
                        }
                        yield break;
                    }
                case LoopType.RepeatN:
                default:
                    {
                        var count = ResolveLoopCount(loopNode, connections, env) ?? loopNode.RepeatCount;
                        if (count < 1) count = 1;
                        for (int i = 0; i < count; i++)
                        {
                            yield return (i, null);
                        }
                        yield break;
                    }
            }
        }

        private int? ResolveLoopCount(
            WorkflowNode anchorNode,
            List<WorkflowConnection> connections,
            NodeExecutors.NodeExecutionEnvironment env)
        {
            var input = anchorNode.DynamicInputs?.FirstOrDefault(i =>
                string.Equals(i.Key, "loopCount", StringComparison.OrdinalIgnoreCase));
            if (input == null) return null;

            var valueText = input.UserValueOverride?.Trim();
            if (!string.IsNullOrWhiteSpace(valueText) && int.TryParse(valueText, out var v1))
                return v1 < 1 ? 1 : v1;

            if (!string.IsNullOrWhiteSpace(input.SelectedSourceNodeId))
            {
                var resolved = ResolveInputValueFromConnections(anchorNode, input, connections, env);
                if (int.TryParse(resolved?.Trim(), out var v2))
                    return v2 < 1 ? 1 : v2;
            }

            return null;
        }

        /// <summary>
        /// Tự động đồng bộ outputs của node nguồn sang tất cả StorageNode có SourceNodeId trỏ tới node đó.
        /// </summary>
        private void MirrorOutputsToStorageNodes(
            WorkflowNode sourceNode,
            List<WorkflowConnection> connections,
            HashSet<WorkflowNode> reachableToEnd,
            string? executionId)
        {
            // Không phụ thuộc vào reachableToEnd (đặc biệt trong LoopBody, reachableToEnd có thể rỗng/noPrune).
            // Thay vào đó, build tập node từ toàn bộ connections hiện tại.
            var allNodes = new HashSet<WorkflowNode>();
            foreach (var c in connections)
            {
                if (c.FromNode != null) allNodes.Add(c.FromNode);
                if (c.ToNode != null) allNodes.Add(c.ToNode);
            }

            var storageNodes = allNodes
                .OfType<StorageNode>()
                .Where(sn =>
                    !string.IsNullOrWhiteSpace(sn.SourceNodeId) &&
                    string.Equals(sn.SourceNodeId, sourceNode.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (storageNodes.Count == 0) return;

            foreach (var storageNode in storageNodes)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(storageNode.SourceOutputKey))
                    {
                        // Copy tất cả outputs
                        storageNode.StoredOutputs.Clear();
                        storageNode.DynamicOutputs.Clear();

                        if (sourceNode.DynamicOutputs != null)
                        {
                            foreach (var o in sourceNode.DynamicOutputs)
                            {
                                if (string.IsNullOrWhiteSpace(o.Key)) continue;
                                var key = o.Key.Trim();
                                var value = ResolveDynamicValueForRun(sourceNode, key, executionId);

                                storageNode.StoredOutputs[key] = value;
                                storageNode.DynamicOutputs.Add(new WorkflowDynamicDataPort
                                {
                                    Key = key,
                                    DisplayName = o.DisplayName ?? key,
                                    IsMultiple = false,
                                    OutputType = o.OutputType ?? o.ConvertType,
                                    UserValueOverride = value
                                });
                            }
                        }
                    }
                    else
                    {
                        // Chỉ copy một key duy nhất
                        var key = storageNode.SourceOutputKey.Trim();
                        var value = ResolveDynamicValueForRun(sourceNode, key, executionId);

                        storageNode.StoredOutputs.Clear();
                        storageNode.DynamicOutputs.Clear();

                        storageNode.StoredOutputs[key] = value;
                        storageNode.DynamicOutputs.Add(new WorkflowDynamicDataPort
                        {
                            Key = key,
                            DisplayName = key,
                            IsMultiple = false,
                            OutputType = WorkflowDataType.String,
                            UserValueOverride = value
                        });
                    }

                    PublishStorageOutputsToScoped(storageNode, executionId);
                }
                catch
                {
                    // best-effort, không để lỗi mirror làm hỏng workflow
                }
            }
        }

        private List<string> ResolveLoopArray(
            WorkflowNode anchorNode,
            List<WorkflowConnection> connections,
            NodeExecutors.NodeExecutionEnvironment env)
        {
            var input = anchorNode.DynamicInputs?.FirstOrDefault(i =>
                string.Equals(i.Key, "loopArray", StringComparison.OrdinalIgnoreCase));
            if (input == null) return new List<string>();

            var valueText = input.UserValueOverride?.Trim();
            if (!string.IsNullOrWhiteSpace(valueText))
            {
                return valueText
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }

            if (string.IsNullOrWhiteSpace(input.SelectedSourceNodeId)) return new List<string>();

            // 1) Tìm đúng node nguồn theo SelectedSourceNodeId (node combobox đã chọn),
            //    không chỉ node ngay trước Loop trong graph.
            var srcNode = connections
                .Where(c => c.FromNode != null && c.FromNode.Id == input.SelectedSourceNodeId)
                .Select(c => c.FromNode)
                .FirstOrDefault();

            // Nếu không tìm được qua connections (edge case), fallback: node ngay upstream
            if (srcNode == null)
            {
                var upstreamConnections = connections
                    .Where(c => c.ToNode == anchorNode && c.FromNode != null)
                    .ToList();
                if (upstreamConnections.Count == 0) return new List<string>();

                srcNode = upstreamConnections.First().FromNode;
                if (srcNode == null) return new List<string>();
            }

            // 2) Lấy key đã chọn từ combobox key (SelectedSourceOutputKey / UserKeyOverride / Key)
            var key = (input.SelectedSourceOutputKey ?? input.UserKeyOverride ?? input.Key ?? string.Empty).Trim();

            // 3) Các đặc biệt: StringSplitNode và InputNode array dùng state sẵn có trên node
            if (srcNode is FlowMy.Models.Nodes.StringSplitNode splitNode)
                return splitNode.SplitResult?.ToList() ?? new List<string>();

            if (srcNode is InputNode inputNode && inputNode.IsArrayType)
                return inputNode.ArrayValues?.ToList() ?? new List<string>();

            // 4) Generic: scoped + fallback data panel
            if (!string.IsNullOrWhiteSpace(key))
            {
                var resolved = ResolveDynamicValueForExecution(srcNode, key, env);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    // ✅ Ưu tiên parse JSON array trước (khi source là CodeNode, KeyValueBridgeNode, etc.
                    //    trả về dạng ["a","b","c"]).
                    //    Nếu split newline thì cả cục JSON thành 1 item duy nhất → bug!
                    var trimmed = resolved.TrimStart();
                    if (trimmed.StartsWith("["))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(trimmed);
                            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                var elements = new List<string>();
                                foreach (var el in doc.RootElement.EnumerateArray())
                                {
                                    var s = el.ValueKind == JsonValueKind.String
                                        ? el.GetString() ?? string.Empty
                                        : el.GetRawText();
                                    if (!string.IsNullOrWhiteSpace(s))
                                        elements.Add(s.Trim());
                                }
                                if (elements.Count > 0)
                                    return elements;
                            }
                        }
                        catch { /* không parse được JSON, fallback newline */ }
                    }

                    // Fallback: split theo newline (plain text, mỗi dòng 1 item)
                    return resolved
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
                }
            }

            return new List<string>();
        }

        internal static void SetLoopRuntimeOutputs(LoopNode loopNode, int index, string? item)
            => SetDispatchIndexItemOutputs(loopNode, index, item);

        internal static void SetAsyncTaskDispatchRuntimeOutputs(AsyncTaskNode asyncTaskNode, int index, string? item)
            => SetDispatchIndexItemOutputs(asyncTaskNode, index, item);

        private static void SetDispatchIndexItemOutputs(WorkflowNode node, int index, string? item)
        {
            var indexOut = node.DynamicOutputs?.FirstOrDefault(o =>
                string.Equals(o.Key, "index", StringComparison.OrdinalIgnoreCase));
            if (indexOut != null) indexOut.UserValueOverride = index.ToString();

            var itemOut = node.DynamicOutputs?.FirstOrDefault(o =>
                string.Equals(o.Key, "item", StringComparison.OrdinalIgnoreCase));
            if (itemOut != null) itemOut.UserValueOverride = item ?? string.Empty;
        }

        /// <summary>
        /// Gán giá trị cho key của node (output hoặc input UserValueOverride). Dùng cho DataAssignments trong loop.
        /// </summary>
        internal static void SetDynamicValueByKey(WorkflowNode node, string key, string value)
        {
            if (node?.DynamicOutputs != null)
            {
                var output = node.DynamicOutputs.FirstOrDefault(o =>
                    string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                if (output != null) { output.UserValueOverride = value ?? string.Empty; return; }
            }
            if (node?.DynamicInputs != null)
            {
                var input = node.DynamicInputs.FirstOrDefault(i =>
                    string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
                if (input != null) input.UserValueOverride = value ?? string.Empty;
            }
        }

        private int? GetIntValueFromDynamicInput(WorkflowNode node, string key)
        {
            if (node.DynamicInputs == null || node.DynamicInputs.Count == 0) return null;

            var input = node.DynamicInputs.FirstOrDefault(i =>
                string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
            if (input == null) return null;

            var valueText = input.UserValueOverride?.Trim();
            if (string.IsNullOrWhiteSpace(valueText)) return null;

            if (int.TryParse(valueText, out var value))
            {
                return value < 1 ? 1 : value;
            }

            return null;
        }

        private double? GetDoubleValueFromDynamicInput(WorkflowNode node, string key)
        {
            if (node.DynamicInputs == null || node.DynamicInputs.Count == 0) return null;

            var input = node.DynamicInputs.FirstOrDefault(i =>
                string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
            if (input == null) return null;

            var valueText = input.UserValueOverride?.Trim();
            if (string.IsNullOrWhiteSpace(valueText)) return null;

            if (double.TryParse(valueText, out var value))
            {
                return value < 0 ? 0 : value;
            }

            return null;
        }

        /// <summary>
        /// Lấy repeat count từ DynamicInputs. 
        /// Ưu tiên UserValueOverride (textbox), nếu không có thì resolve từ output key của source node.
        /// </summary>
        internal int? GetRepeatCountFromDynamicInputs(
            WorkflowNode node,
            IEnumerable<WorkflowConnection>? connections = null,
            NodeExecutors.NodeExecutionEnvironment? env = null)
        {
            if (node.DynamicInputs == null || node.DynamicInputs.Count == 0) return null;

            var input = node.DynamicInputs.FirstOrDefault(i =>
                string.Equals(i.Key, "repeatCount", StringComparison.OrdinalIgnoreCase));
            if (input == null) return null;

            // Ưu tiên UserValueOverride (value từ textbox)
            var valueText = input.UserValueOverride?.Trim();

            // Nếu không có UserValueOverride, resolve từ output key của source node
            if (string.IsNullOrWhiteSpace(valueText) && connections != null && !string.IsNullOrWhiteSpace(input.SelectedSourceNodeId))
            {
                valueText = ResolveInputValueFromConnections(node, input, connections, env);
            }

            if (string.IsNullOrWhiteSpace(valueText)) return null;

            // Try parse as integer
            if (int.TryParse(valueText, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var count) ||
                int.TryParse(valueText, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.CurrentCulture, out count))
            {
                return count < 1 ? 1 : count;
            }

            // Try parse as double and convert to int
            if (double.TryParse(valueText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ||
                double.TryParse(valueText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture, out d))
            {
                var intVal = (int)Math.Round(d);
                return intVal < 1 ? 1 : intVal;
            }

            return null;
        }

        /// <summary>
        /// Resolve giá trị input từ output key của source node thông qua connections.
        /// </summary>
        private string ResolveInputValueFromConnections(
            WorkflowNode toNode,
            WorkflowDynamicDataPort input,
            IEnumerable<WorkflowConnection> connections,
            NodeExecutors.NodeExecutionEnvironment? env = null)
        {
            if (string.IsNullOrWhiteSpace(input.SelectedSourceNodeId)) return string.Empty;

            var connectionList = connections.ToList();

            // Tìm source node từ upstream connections
            var upstreamConnections = connectionList
                .Where(c => c.ToNode == toNode && c.FromNode != null)
                .ToList();

            if (upstreamConnections.Count == 0) return string.Empty;

            // Tìm source node từ SelectedSourceNodeId
            WorkflowNode? srcNode = null;
            var matchingConnection = upstreamConnections.FirstOrDefault(c =>
                c.FromNode != null && c.FromNode.Id == input.SelectedSourceNodeId);

            if (matchingConnection?.FromNode != null)
            {
                srcNode = matchingConnection.FromNode;
            }
            // Không fallback về connection đầu tiên — nếu SelectedSourceNodeId không match thì không resolve

            if (srcNode == null) return string.Empty;

            // Lấy key để resolve (ưu tiên SelectedSourceOutputKey)
            var key = (input.SelectedSourceOutputKey ?? input.UserKeyOverride ?? input.Key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;

            if (env != null && !env.RefreshOnly)
                return ResolveDynamicValueForExecution(srcNode, key, env);

            return ResolveDynamicValueByKey(srcNode, key);
        }

        /// <summary>
        /// Resolve giá trị từ node theo key (dùng trong execution).
        /// Ủy quyền cho NodeDataPanelService để lấy đúng value từ StringSplitNode (SplitResult),
        /// ListOutNode (ResolvedOutputs), InputNode (Value), HttpRequestNode, LoopNode, v.v.
        /// </summary>
        private string ResolveDynamicValueByKey(WorkflowNode node, string key)
        {
            key = key.Trim();
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;

            var resolved = NodeDataPanelService.ResolveDynamicValueByKey(node, key);
            // Chuẩn hóa "—" (placeholder trống trong UI) thành empty cho so sánh điều kiện
            return string.Equals(resolved, "—", StringComparison.Ordinal) ? string.Empty : (resolved ?? string.Empty);
        }
    }

    /// <summary>
    /// Kết quả validation của workflow
    /// </summary>
    public class WorkflowValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }
}

