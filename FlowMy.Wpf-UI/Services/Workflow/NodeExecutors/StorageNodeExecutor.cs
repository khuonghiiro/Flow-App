using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class StorageNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is StorageNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var storageNode = (StorageNode)node;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Callback: entering + started đã được WorkflowExecutionService gọi qua env

                // ✅ Nếu IsInputMode = true (checked) và có incoming connection vào port IN 
                // → LUÔN tự động set outputs từ node nguồn (không cần cấu hình tab2)
                if (storageNode.IsInputMode && env.IncomingConnection?.FromNode != null)
                {
                    var sourceNode = env.IncomingConnection.FromNode;
                    
                    storageNode.StoredOutputs.Clear();
                    storageNode.DynamicOutputs.Clear();

                    if (sourceNode.DynamicOutputs != null)
                    {
                        // ✅ Nếu SourceOutputKey được chỉ định (không null/empty) → chỉ lấy key đó
                        if (!string.IsNullOrWhiteSpace(storageNode.SourceOutputKey))
                        {
                            var key = storageNode.SourceOutputKey.Trim();
                                var value = env.Service.ResolveDynamicValueForExecution(sourceNode, key, env);
                            if (string.Equals(value?.Trim(), "—", StringComparison.Ordinal))
                                value = string.Empty;

                            storageNode.StoredOutputs[key] = value;
                            
                            var outputPort = sourceNode.DynamicOutputs.FirstOrDefault(o => 
                                string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                            
                            storageNode.DynamicOutputs.Add(new WorkflowDynamicDataPort
                            {
                                Key = key,
                                DisplayName = outputPort?.DisplayName ?? key,
                                IsMultiple = false,
                                OutputType = outputPort?.OutputType ?? outputPort?.ConvertType ?? WorkflowDataType.String,
                                UserValueOverride = value
                            });
                        }
                        // ✅ Nếu SourceOutputKey null/empty → lấy TẤT CẢ outputs
                        else
                        {
                            foreach (var o in sourceNode.DynamicOutputs)
                            {
                                if (string.IsNullOrWhiteSpace(o.Key)) continue;
                                var key = o.Key.Trim();
                                var value = env.Service.ResolveDynamicValueForExecution(sourceNode, key, env);
                                if (string.Equals(value?.Trim(), "—", StringComparison.Ordinal))
                                    value = string.Empty;

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
                }
                // ✅ Nếu IsInputMode = false (unchecked) → port OUT visible
                // → lấy dữ liệu từ StorageNode đã chọn trong combobox (SourceNodeId)
                else if (!storageNode.IsInputMode && !string.IsNullOrWhiteSpace(storageNode.SourceNodeId))
                {
                    // Tìm node nguồn:
                    // 1) ưu tiên trong ReachableToEnd (toàn bộ graph hiện tại),
                    // 2) fallback qua connections (FromNode/ToNode).
                    WorkflowNode? sourceNode = null;

                    if (env.ReachableToEnd != null && env.ReachableToEnd.Count > 0)
                    {
                        sourceNode = env.ReachableToEnd.FirstOrDefault(n =>
                            string.Equals(n.Id, storageNode.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                    }

                    if (sourceNode == null)
                    {
                        sourceNode = env.Connections
                            .SelectMany(c => new[] { c.FromNode, c.ToNode })
                            .FirstOrDefault(n => n != null &&
                                string.Equals(n.Id, storageNode.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                    }

                    if (sourceNode != null)
                    {
                        // ✅ Nếu sourceNode là StorageNode → lấy từ StoredOutputs (dữ liệu đã lưu)
                        if (sourceNode is StorageNode sourceStorage)
                        {
                            // Nếu SourceOutputKey rỗng → copy TẤT CẢ stored outputs
                            if (string.IsNullOrWhiteSpace(storageNode.SourceOutputKey))
                            {
                                storageNode.StoredOutputs.Clear();
                                storageNode.DynamicOutputs.Clear();

                                foreach (var kv in sourceStorage.StoredOutputs)
                                {
                                    if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                                    var key = kv.Key.Trim();
                                    var value = kv.Value ?? string.Empty;
                                    if (string.Equals(value.Trim(), "—", StringComparison.Ordinal))
                                        value = string.Empty;

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
                            }
                            else
                            {
                                // Chỉ copy một key duy nhất từ StoredOutputs
                                var key = storageNode.SourceOutputKey.Trim();
                                var value = sourceStorage.StoredOutputs.TryGetValue(key, out var v) ? v : string.Empty;
                                if (string.Equals(value?.Trim(), "—", StringComparison.Ordinal))
                                    value = string.Empty;

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
                        }
                        // ✅ Nếu sourceNode là node thường → lấy từ DynamicOutputs
                        else
                        {
                            // Nếu SourceOutputKey rỗng → copy TẤT CẢ outputs
                            if (string.IsNullOrWhiteSpace(storageNode.SourceOutputKey))
                            {
                                storageNode.StoredOutputs.Clear();
                                storageNode.DynamicOutputs.Clear();

                                if (sourceNode.DynamicOutputs != null)
                                {
                                    foreach (var o in sourceNode.DynamicOutputs)
                                    {
                                        if (string.IsNullOrWhiteSpace(o.Key)) continue;
                                        var key = o.Key.Trim();
                                        var value = env.Service.ResolveDynamicValueForExecution(sourceNode, key, env);
                                        if (string.Equals(value?.Trim(), "—", StringComparison.Ordinal))
                                            value = string.Empty;

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
                                var value = env.Service.ResolveDynamicValueForExecution(sourceNode, key, env);
                                if (string.Equals(value?.Trim(), "—", StringComparison.Ordinal))
                                    value = string.Empty;

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
                        }
                    }
                }

                sw.Stop();
                env.OnNodeCompleted?.Invoke(storageNode, sw.Elapsed);

                // Phải publish trước Traverse: downstream resolve scoped trước khi finally của service mirror (tránh đọc StoredOutputs chung khi nhiều run song song).
                env.Service.PublishStorageOutputsToScoped(storageNode, env.ExecutionId);

                // Cho phép flow đi tiếp nếu node có port OUT
                await env.TraverseOutputsAsync(storageNode);
            }
            catch (Exception ex)
            {
                env.OnNodeFailed?.Invoke(storageNode, ex.Message);
                throw;
            }
        }
    }
}

