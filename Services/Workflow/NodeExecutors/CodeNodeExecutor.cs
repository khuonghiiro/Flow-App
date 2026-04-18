using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow;
using Jint;
using Jint.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class CodeNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is CodeNode;

        /// <summary>Nếu chuỗi là JSON thì parse thành object để script dùng input.result.data...; không thì trả null.</summary>
        //private static object? TryParseJsonToObject(string? value)
        //{
        //    if (string.IsNullOrWhiteSpace(value)) return null;
        //    var trimmed = value.Trim();
        //    if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '[')) return null;
        //    try
        //    {
        //        using var doc = JsonDocument.Parse(value);
        //        return JsonElementToObject(doc.RootElement);
        //    }
        //    catch
        //    {
        //        return null;
        //    }
        //}

        private static object? TryParseJsonToObject(string? value, Engine engine)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();
            if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '[')) return null;

            try
            {
                using var doc = JsonDocument.Parse(value);
                return JsonElementToObject(doc.RootElement);
            }
            catch
            {
                try
                {
                    // Dùng engine được truyền vào thay vì tạo mới
                    var result = engine.Evaluate($"JSON.stringify(({value}))");
                    var jsonStr = result.AsString();

                    using var doc = JsonDocument.Parse(jsonStr);
                    return JsonElementToObject(doc.RootElement);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static object? JsonElementToObject(JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object?>();
                    foreach (var p in el.EnumerateObject())
                        dict[p.Name] = JsonElementToObject(p.Value);
                    return dict;
                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in el.EnumerateArray())
                        list.Add(JsonElementToObject(item));
                    return list;
                case JsonValueKind.String:
                    return el.GetString();
                case JsonValueKind.Number:
                    if (el.TryGetInt64(out var l)) return l;
                    return el.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                default:
                    return null;
            }
        }

        /// <summary>
        /// Xây dựng tập node trong đường dẫn thực thi (từ IncomingConnection.FromNode lùi về phía trước) và khoảng cách đến Code Node.
        /// IncomingConnection.FromNode = khoảng cách 1, node nguồn của nó = 2, ...
        /// </summary>
        private static (HashSet<string> pathNodeIds, Dictionary<string, int> nodeIdToDistance) BuildExecutionPath(
            WorkflowConnection? incomingConnection,
            List<WorkflowConnection> connections,
            WorkflowNode codeNode)
        {
            var pathNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nodeIdToDistance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (incomingConnection?.FromNode == null) return (pathNodeIds, nodeIdToDistance);

            var queue = new Queue<(WorkflowNode node, int distance)>();
            queue.Enqueue((incomingConnection.FromNode, 1));
            pathNodeIds.Add(incomingConnection.FromNode.Id);
            nodeIdToDistance[incomingConnection.FromNode.Id] = 1;

            while (queue.Count > 0)
            {
                var (current, dist) = queue.Dequeue();
                var predecessors = connections
                    .Where(c => c.ToNode == current && c.FromNode != null && c.ToNode != c.FromNode)
                    .Select(c => c.FromNode!)
                    .Distinct();
                foreach (var pred in predecessors)
                {
                    if (pathNodeIds.Add(pred.Id))
                    {
                        var nextDist = dist + 1;
                        nodeIdToDistance[pred.Id] = nextDist;
                        queue.Enqueue((pred, nextDist));
                    }
                }
            }
            return (pathNodeIds, nodeIdToDistance);
        }

        /// <summary>
        /// Lọc danh sách mappings cần áp dụng: biến khác nhau → áp dụng hết; biến trùng → chỉ áp dụng mapping từ node đang trong đường dẫn thực thi.
        /// </summary>
        private static List<CodeInputMapping> FilterMappingsByExecutionPath(
            List<CodeInputMapping> mappings,
            HashSet<string> pathNodeIds,
            Dictionary<string, int> nodeIdToDistance,
            bool hasIncomingPath)
        {
            var byVarName = mappings
                .GroupBy(m => string.IsNullOrWhiteSpace(m.EffectiveInputKey) ? "input" : m.EffectiveInputKey.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new List<CodeInputMapping>();
            foreach (var group in byVarName)
            {
                var list = group.ToList();
                if (list.Count == 1)
                {
                    result.Add(list[0]);
                    continue;
                }
                // Nhiều mapping cùng biến: chỉ áp dụng mapping từ node trong đường dẫn thực thi (node gần Code Node nhất)
                if (hasIncomingPath && pathNodeIds.Count > 0)
                {
                    var inPath = list
                        .Where(m => !string.IsNullOrWhiteSpace(m.SourceNodeId) && pathNodeIds.Contains(m.SourceNodeId))
                        .OrderBy(m => nodeIdToDistance.GetValueOrDefault(m.SourceNodeId!, int.MaxValue))
                        .ToList();
                    if (inPath.Count > 0)
                    {
                        result.Add(inPath[0]);
                        continue;
                    }
                }
                // Fallback: không có path hoặc không có mapping nào trong path → dùng mapping đầu tiên
                result.Add(list[0]);
            }
            return result;
        }

        private static void InjectWorkflowKv(Engine engine, NodeExecutionEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(env.ExecutionId) || env.RefreshOnly) return;
            var execId = env.ExecutionId;
            engine.SetValue("__kvClrGet", new Func<string, JsValue>(k =>
            {
                var snap = WorkflowKeyValueStore.GetSnapshot(execId, k);
                return snap == null ? JsValue.Undefined : JsValue.FromObject(engine, snap);
            }));
            engine.SetValue("__kvClrSet", new Action<string, JsValue>((k, v) =>
            {
                object? o = v.IsUndefined() || v.IsNull() ? null : v.ToObject();
                WorkflowKeyValueStore.Append(execId, k, o);
            }));
            engine.Execute("""
                var kv = new Proxy({}, {
                  get: function(_t, p) { return __kvClrGet(String(p)); },
                  set: function(_t, p, v) { __kvClrSet(String(p), v); return true; }
                });
                """);
        }

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var codeNode = (CodeNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var batchOutputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var engine = new Engine();
                InjectWorkflowKv(engine, env);
                var mappings = codeNode.InputMappings ?? new List<CodeInputMapping>();
                if (mappings.Count == 0)
                    mappings = new List<CodeInputMapping> { new CodeInputMapping() };

                var (pathNodeIds, nodeIdToDistance) = BuildExecutionPath(env.IncomingConnection, connections, codeNode);
                var hasIncomingPath = env.IncomingConnection?.FromNode != null;
                var mappingsToApply = FilterMappingsByExecutionPath(mappings, pathNodeIds, nodeIdToDistance, hasIncomingPath);

                foreach (var m in mappingsToApply)
                {
                    WorkflowNode? sourceNode = null;
                    if (!string.IsNullOrWhiteSpace(m.SourceNodeId))
                    {
                        sourceNode = env.ReachableToEnd?.FirstOrDefault(n =>
                            string.Equals(n.Id, m.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                        if (sourceNode == null)
                        {
                            var conn = connections.FirstOrDefault(c =>
                                c.ToNode == codeNode && c.FromNode != null &&
                                string.Equals(c.FromNode.Id, m.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                            sourceNode = conn?.FromNode;
                        }
                        if (sourceNode == null)
                            sourceNode = connections.SelectMany(c => new[] { c.FromNode, c.ToNode })
                                .FirstOrDefault(n => n != null && string.Equals(n.Id, m.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                    }

                    string inputValue = string.Empty;
                    if (sourceNode != null)
                    {
                        // Chỉ chạy lại logic node nguồn nếu ShouldReExecute = true
                        if (m.ShouldReExecute)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CodeNodeExecutor] Re-executing source node {sourceNode.Id} (ShouldReExecute=true)");
                            await env.Service.ExecuteNodeLogicOnlyAsync(sourceNode, connections, env.CancellationToken,
                                allNodesForLookup: env.ReachableToEnd?.ToList());
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[CodeNodeExecutor] Skipping re-execution of source node {sourceNode.Id} (ShouldReExecute=false), using cached data");
                        }

                        var key = string.IsNullOrWhiteSpace(m.SourceOutputKey) ? null : m.SourceOutputKey.Trim();
                        // Khi SourceOutputKey trống: dùng key đầu tiên của DynamicOutputs (vd. InputNode có "Input"/"value")
                        if (string.IsNullOrWhiteSpace(key) && sourceNode.DynamicOutputs != null && sourceNode.DynamicOutputs.Count > 0)
                            key = sourceNode.DynamicOutputs[0].Key ?? "output";
                        inputValue = env.Service.ResolveDynamicValueForExecution(sourceNode, key ?? "output", env);
                        // "—" là placeholder khi không có giá trị → dùng chuỗi rỗng để biến trong JS nhận được
                        if (string.Equals(inputValue?.Trim(), "—", StringComparison.Ordinal))
                            inputValue = string.Empty;
                    }

                    var varName = m.EffectiveInputKey;
                    if (string.IsNullOrWhiteSpace(varName)) varName = "input";
                    // Nếu input là chuỗi JSON thì parse thành object để script dùng input.result.data... trực tiếp.
                    var valueToSet = TryParseJsonToObject(inputValue, engine);
                    var finalValue = valueToSet ?? (object)inputValue;
                    engine.SetValue(varName, finalValue);
                }

                var script = codeNode.ScriptCode ?? "return {};";

                var result = engine.Evaluate(script);

                // Nếu có nhiều hàm và không có biểu thức trả về, gọi main() nếu có định nghĩa.
                if ((result == null || !result.IsObject()) && script.IndexOf("function", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        result = engine.Invoke("main");
                    }
                    catch
                    {
                        // Bỏ qua nếu main không tồn tại hoặc không gọi được.
                    }
                }

                if (result != null && result.IsObject())
                {
                    engine.SetValue("__codeResult", result);
                    var jsonStr = engine.Evaluate("JSON.stringify(__codeResult)").AsString();
                    System.Diagnostics.Debug.WriteLine($"[CodeNodeExecutor] Serialized result JSON: {jsonStr}");
                    using var doc = JsonDocument.Parse(jsonStr);
                    var root = doc.RootElement;
                    var outputKeys = codeNode.OutputKeys ?? new List<string>();
                    if (outputKeys.Count == 0)
                    {
                        // Nếu chưa cấu hình Output keys thì lấy tất cả key từ object trả về.
                        foreach (var prop in root.EnumerateObject())
                        {
                            var key = prop.Name.Trim();
                            if (string.IsNullOrEmpty(key)) continue;
                            batchOutputs[key] = prop.Value.ValueKind == JsonValueKind.String
                                ? prop.Value.GetString()
                                : prop.Value.GetRawText();
                        }
                    }
                    else
                    {
                        foreach (var key in outputKeys)
                        {
                            if (string.IsNullOrWhiteSpace(key)) continue;
                            var k = key.Trim();
                            JsonElement prop;
                            if (root.TryGetProperty(k, out prop))
                            {
                                batchOutputs[k] = prop.ValueKind == JsonValueKind.String
                                    ? prop.GetString()
                                    : prop.GetRawText();
                            }
                            else
                            {
                                // Tìm property không phân biệt hoa thường (vd. toggeresult / toggleResult).
                                var found = false;
                                foreach (var p in root.EnumerateObject())
                                {
                                    if (string.Equals(p.Name, k, StringComparison.OrdinalIgnoreCase))
                                    {
                                        batchOutputs[k] = p.Value.ValueKind == JsonValueKind.String
                                            ? p.Value.GetString()
                                            : p.Value.GetRawText();
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found)
                                {
                                    // Nếu key không xuất hiện ở lần chạy lại (callback/retry),
                                    // đừng ghi đè scoped value hiện có của cùng execution thành null.
                                    // Điều này giúp downstream node (Conditional/FlowOverwrite/Output)
                                    // vẫn đọc đúng dữ liệu đã sinh trước đó trong cùng luồng chạy.
                                    string? previous = null;
                                    if (!string.IsNullOrWhiteSpace(env.ExecutionId) &&
                                        env.Service.TryGetScopedNodeStringOutput(env.ExecutionId, codeNode.Id, k, out var prevScoped) &&
                                        !string.IsNullOrWhiteSpace(prevScoped))
                                    {
                                        previous = prevScoped;
                                    }

                                    if (previous != null)
                                        batchOutputs[k] = previous;
                                    else
                                        batchOutputs[k] = null;
                                }
                            }
                        }
                    }
                }
                else if (result != null && !result.IsObject())
                {
                    // Script trả về giá trị đơn (string, number, boolean) – gán vào key đầu tiên trong OutputKeys hoặc "result".
                    var str = result.ToString();
                    if (str != null && !string.Equals(str, "undefined", StringComparison.OrdinalIgnoreCase))
                    {
                        var outputKeys = codeNode.OutputKeys ?? new List<string>();
                        var singleKey = outputKeys.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "result";
                        batchOutputs[singleKey] = str;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodeNode error: {ex.Message}");
                batchOutputs["error"] = ex.Message;
                if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
                    env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, codeNode.Id, batchOutputs);
                lock (codeNode.ResolvedOutputsSyncRoot)
                {
                    codeNode.ResolvedOutputs.Clear();
                    foreach (var kv in batchOutputs)
                        codeNode.ResolvedOutputs[kv.Key] = kv.Value;
                }
                env.OnNodeFailed?.Invoke(codeNode, ex.Message);
                throw;
            }

            if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
                env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, codeNode.Id, batchOutputs);
            lock (codeNode.ResolvedOutputsSyncRoot)
            {
                codeNode.ResolvedOutputs.Clear();
                foreach (var kv in batchOutputs)
                    codeNode.ResolvedOutputs[kv.Key] = kv.Value;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(codeNode, sw.Elapsed);

            // CodeNode luôn đi tiếp theo connections (không áp dụng IsReuseRouteTerminal) vì logic
            // script thường cần output đến các node tiếp theo để xử lý.
            var outputPort = codeNode.Ports.FirstOrDefault(p => !p.IsInput && p.IsVisible);
            if (outputPort != null)
            {
                var nextConnections = env.Service.GetConnectionsFromPort(outputPort, codeNode, connections);
                foreach (var c in nextConnections)
                {
                    if (c.ToNode != null)
                    {
                        if (WorkflowExecutionService.IsLoopBodyReturnConnection(c))
                        {
                            env.Service.SignalLoopBodyReturn(c, env.ExecutionId, env.BranchId);
                            continue;
                        }
                        await env.ExecuteNextAsync(c.ToNode, c);
                    }
                }
            }
        }
    }
}
