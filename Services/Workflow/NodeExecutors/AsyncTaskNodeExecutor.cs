using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho AsyncTask Node.
    /// Manual: nhiều port out. Loop-like: một body + dispatch theo N/mảng, song song hoặc tuần tự giữa các vòng.
    /// </summary>
    internal sealed class AsyncTaskNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is AsyncTaskNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            if (node is not AsyncTaskNode asyncTaskNode)
            {
                env.OnNodeCompleted?.Invoke(node, TimeSpan.Zero);
                return;
            }

            if (asyncTaskNode.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch
                && asyncTaskNode.AsyncTaskBodyNode != null)
            {
                await ExecuteLoopLikeDispatchAsync(asyncTaskNode, env);
                return;
            }

            if (asyncTaskNode.AsyncTaskBranches == null || asyncTaskNode.AsyncTaskBranches.Count == 0)
            {
                env.OnNodeCompleted?.Invoke(node, TimeSpan.Zero);
                return;
            }

            var connections = env.Connections;
            var service = env.Service;

            if (asyncTaskNode.RunInParallel)
            {
                var allOutgoingConnections = new List<WorkflowConnection>();
                foreach (var branch in asyncTaskNode.AsyncTaskBranches)
                {
                    if (branch.Port == null) continue;
                    var nextConnections = service.GetConnectionsFromPort(branch.Port, node, connections);
                    foreach (var conn in nextConnections)
                    {
                        if (conn.ToNode != null && !WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
                            allOutgoingConnections.Add(conn);
                    }
                }

                foreach (var conn in allOutgoingConnections)
                {
                    conn.IsExecutionPinned = true;
                    conn.IsExecutionActive = true;
                }

                if (allOutgoingConnections.Count > 0)
                    env.OnEnteringNode?.Invoke(allOutgoingConnections[0]);

                var tasks = new List<Task>();
                // Mỗi nhánh song song cần ExecutionId riêng: ResolveDynamicValueForExecution chỉ khóa theo ExecutionId,
                // dùng chung env.ExecutionId sẽ ghi đè scoped store (HTTP/Code/Output/File…) giữa các thread.
                var parallelBranchRunIds = new ConcurrentBag<string>();

                foreach (var branch in asyncTaskNode.AsyncTaskBranches)
                {
                    if (branch.Port == null) continue;

                    var nextConnections = service.GetConnectionsFromPort(branch.Port, node, connections);

                    foreach (var conn in nextConnections)
                    {
                        if (conn.ToNode == null) continue;

                        if (WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
                        {
                            env.Service.SignalLoopBodyReturn(conn, env.ExecutionId, env.BranchId);
                            continue;
                        }

                        tasks.Add(Task.Run(async () =>
                        {
                            var branchExecutionId = $"{env.ExecutionId}:at-manual-{Guid.NewGuid():N}";
                            parallelBranchRunIds.Add(branchExecutionId);
                            try
                            {
                                await service.ExecuteNodeAsync(
                                    conn.ToNode,
                                    connections,
                                    env.CancellationToken,
                                    env.OnEnteringNode,
                                    env.OnNodeStarted,
                                    env.OnNodeCompleted,
                                    env.OnNodeFailed,
                                    conn,
                                    env.ReachableToEnd,
                                    false,
                                    new List<string>(env.ExecutionPath),
                                    branchExecutionId,
                                    env.FlowScopeId,
                                    $"{env.BranchId}:{branch.Id}",
                                    env.ParentFlowScopeId);
                            }
                            catch (OperationCanceledException)
                            {
                                // workflow stop/cancel
                            }
                            catch (Exception ex)
                            {
                                // Một nhánh lỗi thì báo lỗi nhánh đó, nhưng không làm tắt toàn bộ async branches.
                                env.OnNodeFailed?.Invoke(conn.ToNode, ex.Message);
                            }
                        }, env.CancellationToken));
                    }
                }

                try
                {
                    if (tasks.Count > 0)
                        await Task.WhenAll(tasks);
                }
                finally
                {
                    foreach (var rid in parallelBranchRunIds)
                    {
                        try { service.ClearScopedOutputsForRun(rid); }
                        catch { /* best-effort */ }
                    }

                    foreach (var conn in allOutgoingConnections)
                    {
                        conn.IsExecutionPinned = false;
                        conn.IsExecutionActive = false;
                    }

                    env.OnEnteringNode?.Invoke(null);
                }
            }
            else
            {
                foreach (var branch in asyncTaskNode.AsyncTaskBranches)
                {
                    if (branch.Port == null) continue;

                    try
                    {
                        var nextConnections = service.GetConnectionsFromPort(branch.Port, node, connections);

                        foreach (var conn in nextConnections)
                        {
                            if (conn.ToNode == null) continue;

                            if (WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
                            {
                                env.Service.SignalLoopBodyReturn(conn, env.ExecutionId, env.BranchId);
                                continue;
                            }

                            var sequentialEnv = env.ForkForBranch($"{env.BranchId}:{branch.Id}");
                            await sequentialEnv.ExecuteNextAsync(conn.ToNode, conn);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Không throw để vẫn chạy các branch còn lại theo đúng kỳ vọng async.
                        env.OnNodeFailed?.Invoke(node, $"Task branch error: {ex.Message}");
                        continue;
                    }
                }
            }

            env.OnNodeCompleted?.Invoke(node, TimeSpan.Zero);
        }

        private static async Task ExecuteLoopLikeDispatchAsync(AsyncTaskNode asyncTaskNode, NodeExecutionEnvironment env)
        {
            var connections = env.Connections;
            var service = env.Service;
            var cancellationToken = env.CancellationToken;
            var bodyNode = asyncTaskNode.AsyncTaskBodyNode!;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            WorkflowExecutionService.EnsureAsyncTaskBodyPortsExist(bodyNode);

            foreach (var o in connections
                         .SelectMany(c => new[] { c.FromNode, c.ToNode })
                         .OfType<OutputNode>()
                         .Distinct())
                o.ResetParallelDispatchOutputAccumulation();

            var loopBottomPort = asyncTaskNode.Ports.FirstOrDefault(p => p.Id == "LoopNodeBottom");
            var bodyTopPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyTop");
            var bodyLeftPort = bodyNode.Ports.FirstOrDefault(p => p.Id == "LoopBodyLeft");
            var loopOutPort = asyncTaskNode.Ports.FirstOrDefault(p => p.Id == "LoopNodeOut");
            var readResultsInBody = asyncTaskNode.ReadResultsInBody;

            WorkflowConnection? loopToBodyConn = null;
            if (loopBottomPort != null && bodyTopPort != null)
            {
                loopToBodyConn = connections.FirstOrDefault(c =>
                    c.FromNode == asyncTaskNode &&
                    c.ToNode == bodyNode &&
                    (ReferenceEquals(c.FromPort, loopBottomPort) ||
                     (c.FromPort != null && string.Equals(c.FromPort.Id, loopBottomPort.Id, StringComparison.OrdinalIgnoreCase))) &&
                    (ReferenceEquals(c.ToPort, bodyTopPort) ||
                     (c.ToPort != null && string.Equals(c.ToPort.Id, bodyTopPort.Id, StringComparison.OrdinalIgnoreCase))));

                if (loopToBodyConn == null)
                {
                    loopToBodyConn = new WorkflowConnection
                    {
                        FromNode = asyncTaskNode,
                        ToNode = bodyNode,
                        FromPort = loopBottomPort,
                        ToPort = bodyTopPort,
                        IsDeleteVisible = false
                    };
                    connections.Add(loopToBodyConn);
                }

                if (asyncTaskNode.DefaultConnection == null)
                    asyncTaskNode.DefaultConnection = loopToBodyConn;
            }

            var iterations = service.ResolveAsyncTaskDispatchIterations(asyncTaskNode, connections, env).ToList();

            var iterationExecutionIds = iterations
                .Select(pair => $"{env.ExecutionId}:dispatch-{pair.index}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Connections out from AsyncTask header (LoopNodeOut).
            // readResultsInBody=true: traverse them inside each dispatch iteration (so each iteration can use its own scoped values).
            // Otherwise: traverse them once after all iterations.
            var nextConnections = (loopOutPort != null && readResultsInBody)
                ? service.GetConnectionsFromPortIncludingLegacy(loopOutPort, asyncTaskNode, connections)
                    .Where(c => c.ToNode != null)
                    .ToList()
                : new List<WorkflowConnection>();

            var bodyMappings = asyncTaskNode.BodyOutputMappings ?? new List<AsyncTaskBodyOutputMapping>();
            var collectedResultsByMapping = new ConcurrentDictionary<string, ConcurrentDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

            // When RunInParallel is enabled, multiple dispatch iterations run concurrently.
            // They share the same default connection (diamond -> body), so we keep it
            // active until the last iteration finishes.
            var activeIterationCount = 0;

            async Task RunOneDispatchIterationAsync(int index, string? item)
            {
                var startedAt = DateTime.UtcNow;
                if (cancellationToken.IsCancellationRequested) return;

                // Always use per-iteration executionId so scoped outputs are isolated.
                // This is required for "B" (collect N results after loopOut) to be parallel-safe.
                var iterationExecutionId = $"{env.ExecutionId}:dispatch-{index}";

                try
                {
                    // Store runtime outputs (index/item) into scoped store.
                    service.SetScopedNodeStringOutput(iterationExecutionId, asyncTaskNode.Id, "index", index.ToString());
                    service.SetScopedNodeStringOutput(iterationExecutionId, asyncTaskNode.Id, "item", item ?? string.Empty);

                    // Also update DynamicOutputs (legacy/fallback path).
                    // Some downstream nodes may resolve via NodeDataPanelService fallback (e.g. if executionId changes
                    // across UI-driven routing nodes). Keeping "last-known" index/item avoids empty outputs.
                    WorkflowExecutionService.SetAsyncTaskDispatchRuntimeOutputs(asyncTaskNode, index, item);

                    if (loopToBodyConn == null) return;

                var newCount = Interlocked.Increment(ref activeIterationCount);
                loopToBodyConn.IsExecutionPinned = true;
                loopToBodyConn.IsExecutionActive = true;

                // Only trigger OnEnteringNode when the first iteration becomes active,
                // otherwise parallel iterations might immediately turn it off.
                if (newCount == 1)
                {
                    env.OnEnteringNode?.Invoke(loopToBodyConn);
                }

                using var iterationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var iterationBranchScope = $"{env.BranchId}:at-dispatch-{index}";
                var (returnTask, cleanup) = service.BeginAwaitLoopBodyReturn(
                    bodyNode,
                    iterationExecutionId,
                    cancellationToken,
                    hardStopCts: null,
                    branchScope: iterationBranchScope);

                try
                {
                    var entryConnections = new List<WorkflowConnection>();
                    if (bodyLeftPort != null)
                    {
                        entryConnections = service.GetConnectionsFromPortIncludingLegacy(
                            bodyLeftPort, bodyNode, connections);
                    }

                    if (entryConnections.Count == 0 && bodyTopPort != null)
                    {
                        entryConnections = service.GetConnectionsFromPortIncludingLegacy(
                            bodyTopPort, bodyNode, connections);
                    }

                    if (entryConnections.Count > 0)
                    {
                        var noPrune = new HashSet<WorkflowNode>();
                        var tasks = new List<Task>();

                        foreach (var conn in entryConnections)
                        {
                            if (conn.ToNode != null)
                            {
                                tasks.Add(service.ExecuteNodeAsync(
                                    conn.ToNode,
                                    connections,
                                    iterationCts.Token,
                                    env.OnEnteringNode,
                                    env.OnNodeStarted,
                                    env.OnNodeCompleted,
                                    env.OnNodeFailed,
                                    conn,
                                    noPrune,
                                    false,
                                    new List<string>(env.ExecutionPath),
                                    executionId: iterationExecutionId,
                                    flowScopeId: env.FlowScopeId,
                                    branchId: iterationBranchScope,
                                    parentFlowScopeId: env.ParentFlowScopeId));
                            }
                        }

                        try
                        {
                            await Task.WhenAll(tasks);
                        }
                        catch (OperationCanceledException)
                        {
                            if (cancellationToken.IsCancellationRequested) throw;
                            if (!returnTask.IsCompletedSuccessfully) throw;
                        }
                    }
                }
                finally
                {
                    cleanup.Dispose();
                    // If the body return completes immediately (e.g. body has no entry connections),
                    // keep the connection "active" briefly so the UI dash/energy effect has time to render.
                    var elapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;
                    const double minActiveMs = 200;
                    if (elapsedMs < minActiveMs && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(minActiveMs - elapsedMs), cancellationToken);
                    }

                    var remaining = Interlocked.Decrement(ref activeIterationCount);
                    if (remaining <= 0)
                    {
                        loopToBodyConn.IsExecutionPinned = false;
                        loopToBodyConn.IsExecutionActive = false;
                        env.OnEnteringNode?.Invoke(null);
                    }
                }

                if (!returnTask.IsCompletedSuccessfully && !cancellationToken.IsCancellationRequested)
                {
                    var loopRightPort = bodyNode.Ports.FirstOrDefault(p => string.Equals(p.Id, "LoopBodyRight", StringComparison.OrdinalIgnoreCase));
                    if (loopRightPort != null)
                    {
                        var dummyReturnConn = new WorkflowConnection
                        {
                            FromNode = bodyNode,
                            ToNode = bodyNode,
                            ToPort = loopRightPort
                        };
                        service.SignalLoopBodyReturn(dummyReturnConn, iterationExecutionId, iterationBranchScope);
                    }
                }

                if (!returnTask.IsCompletedSuccessfully && !cancellationToken.IsCancellationRequested)
                {
                    throw new InvalidOperationException(
                        "Async Task body chưa có đường return về port 'Port Body Right'. " +
                        "Hãy nối node kết thúc trong body vào 'Port Body Right'.");
                }

                // ── Extract body outputs configured in BodyOutputMappings ──
                if (bodyMappings.Count > 0)
                {
                    foreach (var mapping in bodyMappings)
                    {
                        if (string.IsNullOrWhiteSpace(mapping.SourceNodeId) ||
                            string.IsNullOrWhiteSpace(mapping.SourceOutputKey) ||
                            string.IsNullOrWhiteSpace(mapping.OutputKey))
                            continue;

                        var srcNodeId = mapping.SourceNodeId.Trim();
                        var srcKey = mapping.SourceOutputKey.Trim();
                        var outKey = mapping.OutputKey.Trim();

                        string? outVal = null;
                        if (service.TryGetScopedNodeStringOutput(iterationExecutionId, srcNodeId, srcKey, out var scopedVal))
                        {
                            outVal = scopedVal;
                        }
                        else
                        {
                            var srcNode = connections
                                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                                .FirstOrDefault(n => n != null && string.Equals(n.Id, srcNodeId, StringComparison.OrdinalIgnoreCase));
                            if (srcNode != null)
                            {
                                outVal = service.ResolveDynamicValueForRun(srcNode, srcKey, iterationExecutionId);
                            }
                        }

                        outVal ??= string.Empty;

                        var dict = collectedResultsByMapping.GetOrAdd(outKey, _ => new ConcurrentDictionary<int, string>());
                        dict[index] = outVal;
                        service.SetScopedNodeStringOutput(iterationExecutionId, asyncTaskNode.Id, outKey, outVal);
                    }
                }

                // ── Push async data to HtmlUi nodes after each iteration ──
                try
                {
                    PushAsyncDataToHtmlUiNodes(asyncTaskNode, connections, service, iterationExecutionId);
                }
                catch (Exception exPush)
                {
                    System.Diagnostics.Debug.WriteLine($"AsyncTask: Async data push error: {exPush.Message}");
                }

                // readResultsInBody=true: traverse LoopNodeOut connections right after this dispatch iteration returns,
                // so downstream reads iteration-specific results.
                if (readResultsInBody && nextConnections.Count > 0)
                {
                    var noPrune = new HashSet<WorkflowNode>();
                    var pending = new List<Task>();

                    foreach (var conn in nextConnections)
                    {
                        if (conn.ToNode == null) continue;

                        if (WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
                        {
                            service.SignalLoopBodyReturn(conn, iterationExecutionId, iterationBranchScope);
                            continue;
                        }

                        if (asyncTaskNode.RunInParallel)
                        {
                            pending.Add(service.ExecuteNodeAsync(
                                conn.ToNode,
                                connections,
                                iterationCts.Token,
                                env.OnEnteringNode,
                                env.OnNodeStarted,
                                env.OnNodeCompleted,
                                env.OnNodeFailed,
                                conn,
                                noPrune,
                                false,
                                new List<string>(env.ExecutionPath),
                                executionId: iterationExecutionId,
                                flowScopeId: env.FlowScopeId,
                                branchId: iterationBranchScope,
                                parentFlowScopeId: env.ParentFlowScopeId));
                        }
                        else
                        {
                            await service.ExecuteNodeAsync(
                                conn.ToNode,
                                connections,
                                iterationCts.Token,
                                env.OnEnteringNode,
                                env.OnNodeStarted,
                                env.OnNodeCompleted,
                                env.OnNodeFailed,
                                conn,
                                noPrune,
                                false,
                                new List<string>(env.ExecutionPath),
                                executionId: iterationExecutionId,
                                flowScopeId: env.FlowScopeId,
                                branchId: iterationBranchScope,
                                parentFlowScopeId: env.ParentFlowScopeId);
                        }
                    }

                    if (pending.Count > 0)
                    {
                        await Task.WhenAll(pending);
                    }
                }
            }
            finally
            {
                if (readResultsInBody)
                    service.ClearScopedOutputsForRun(iterationExecutionId);
            }
            }

            try
            {
                if (asyncTaskNode.RunInParallel)
                {
                    var wave = iterations
                        .Select(async pair =>
                        {
                            try
                            {
                                await RunOneDispatchIterationAsync(pair.index, pair.item);
                            }
                            catch (OperationCanceledException)
                            {
                                if (cancellationToken.IsCancellationRequested) return;
                                env.OnNodeFailed?.Invoke(asyncTaskNode,
                                    $"Dispatch {pair.index} cancelled unexpectedly.");
                            }
                            catch (Exception ex)
                            {
                                // Isolate one failed iteration so others keep running.
                                env.OnNodeFailed?.Invoke(asyncTaskNode,
                                    $"Dispatch {pair.index} failed: {ex.Message}");
                            }
                        })
                        .ToList();
                    if (wave.Count > 0)
                        await Task.WhenAll(wave);
                }
                else
                {
                    foreach (var (index, item) in iterations)
                    {
                        try
                        {
                            await RunOneDispatchIterationAsync(index, item);
                        }
                        catch (OperationCanceledException)
                        {
                            if (cancellationToken.IsCancellationRequested) throw;
                            env.OnNodeFailed?.Invoke(asyncTaskNode,
                                $"Dispatch {index} cancelled unexpectedly.");
                        }
                        catch (Exception ex)
                        {
                            // Sequential mode should continue to next iteration on per-item failure.
                            env.OnNodeFailed?.Invoke(asyncTaskNode,
                                $"Dispatch {index} failed: {ex.Message}");
                        }
                    }
                }
            }
            finally
            {
                // Serialize & Sync output results across all iterations for asyncTaskNode
                if (bodyMappings.Count > 0)
                {
                    foreach (var mapping in bodyMappings)
                    {
                        if (!string.IsNullOrWhiteSpace(mapping.OutputKey))
                        {
                            var targetKey = mapping.OutputKey.Trim();
                            if (collectedResultsByMapping.TryGetValue(targetKey, out var dict))
                            {
                                var rawList = iterations
                                    .Select(p => dict.TryGetValue(p.index, out var v) ? v : string.Empty)
                                    .Where(v => !string.IsNullOrWhiteSpace(v))
                                    .ToList();

                                // Flatten array outputs (such as from FlowOverwriteNode or list outputs) across iterations
                                var flattenedList = new List<string>();
                                foreach (var rawItem in rawList)
                                {
                                    var trimmed = rawItem.Trim();
                                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                                    {
                                        try
                                        {
                                            using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                                            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                                            {
                                                foreach (var elem in doc.RootElement.EnumerateArray())
                                                {
                                                    var str = elem.ValueKind == System.Text.Json.JsonValueKind.String 
                                                        ? elem.GetString() 
                                                        : elem.GetRawText();
                                                    if (str != null)
                                                    {
                                                        flattenedList.Add(str);
                                                    }
                                                }
                                                continue;
                                            }
                                        }
                                        catch
                                        {
                                            // Fallback to raw string if JSON parsing fails
                                        }
                                    }
                                    flattenedList.Add(rawItem);
                                }

                                string finalVal;
                                if (mapping.IsCollectArray || flattenedList.Count > 1)
                                {
                                    finalVal = System.Text.Json.JsonSerializer.Serialize(flattenedList);
                                }
                                else if (flattenedList.Count == 1)
                                {
                                    finalVal = flattenedList[0];
                                }
                                else
                                {
                                    finalVal = string.Empty;
                                }

                                service.SetScopedNodeStringOutput(env.ExecutionId, asyncTaskNode.Id, targetKey, finalVal);
                                foreach (var runId in iterationExecutionIds)
                                {
                                    service.SetScopedNodeStringOutput(runId, asyncTaskNode.Id, targetKey, finalVal);
                                }

                                if (asyncTaskNode.DynamicOutputs != null)
                                {
                                    var port = asyncTaskNode.DynamicOutputs.FirstOrDefault(p => string.Equals(p.Key, targetKey, StringComparison.OrdinalIgnoreCase));
                                    if (port == null)
                                    {
                                        port = new WorkflowDynamicDataPort
                                        {
                                            Key = targetKey,
                                            DisplayName = targetKey,
                                            OutputType = WorkflowDataType.String,
                                            ConvertType = WorkflowDataType.String,
                                            IsUserAdded = true
                                        };
                                        asyncTaskNode.DynamicOutputs.Add(port);
                                    }
                                    port.UserValueOverride = finalVal;
                                }
                            }
                        }
                    }
                }

                // If running in background mode (readResultsInBody), trigger UI update when background tasks finish
                if (readResultsInBody)
                {
                    sw.Stop();
                    env.OnNodeCompleted?.Invoke(asyncTaskNode, sw.Elapsed);
                }
            }

            if (!readResultsInBody && loopOutPort != null && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var afterLoopOutConnections = service.GetConnectionsFromPortIncludingLegacy(loopOutPort, asyncTaskNode, connections);
                    var pending = new List<Task>();
                    foreach (var conn in afterLoopOutConnections)
                    {
                        if (conn.ToNode == null) continue;

                        if (WorkflowExecutionService.IsLoopBodyReturnConnection(conn))
                        {
                            service.SignalLoopBodyReturn(conn, env.ExecutionId, env.BranchId);
                            continue;
                        }

                        if (asyncTaskNode.RunInParallel)
                        {
                            pending.Add(service.ExecuteNodeAsync(
                                conn.ToNode,
                                connections,
                                cancellationToken,
                                env.OnEnteringNode,
                                env.OnNodeStarted,
                                env.OnNodeCompleted,
                                env.OnNodeFailed,
                                conn,
                                env.ReachableToEnd,
                                false,
                                new List<string>(env.ExecutionPath),
                                executionId: env.ExecutionId,
                                flowScopeId: env.FlowScopeId,
                                branchId: env.BranchId,
                                parentFlowScopeId: env.ParentFlowScopeId));
                        }
                        else
                        {
                            await service.ExecuteNodeAsync(
                                conn.ToNode,
                                connections,
                                cancellationToken,
                                env.OnEnteringNode,
                                env.OnNodeStarted,
                                env.OnNodeCompleted,
                                env.OnNodeFailed,
                                conn,
                                env.ReachableToEnd,
                                false,
                                new List<string>(env.ExecutionPath),
                                executionId: env.ExecutionId,
                                flowScopeId: env.FlowScopeId,
                                branchId: env.BranchId,
                                parentFlowScopeId: env.ParentFlowScopeId);
                        }
                    }

                    if (pending.Count > 0)
                    {
                        await Task.WhenAll(pending);
                    }
                }
                finally
                {
                    // Collector (new node) reads per-dispatch scoped outputs.
                    // Clear them only after all loopOut nodes have run.
                    foreach (var id in iterationExecutionIds)
                        service.ClearScopedOutputsForRun(id);
                }
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(asyncTaskNode, sw.Elapsed);
        }

        /// <summary>
        /// Sau mỗi iteration, tìm tất cả HtmlUiNode có AsyncDataSources
        /// referencing bất kỳ node nào trong workflow, resolve value và push vào cache.
        /// </summary>
        private static void PushAsyncDataToHtmlUiNodes(
            AsyncTaskNode asyncTaskNode,
            List<WorkflowConnection> connections,
            WorkflowExecutionService service,
            string iterationExecutionId)
        {
            // Tìm tất cả UI node (HtmlUiNode, ShowInputMsgNode, DynamicUiNode, ISciterNode) trong workflow
            var targetUiNodes = connections
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .Where(n => n != null)
                .Select(n => new
                {
                    Node = n!,
                    Sources = (n as HtmlUiNode)?.AsyncDataSources ?? (n as ISciterNode)?.AsyncDataSources,
                    Queue = (n as HtmlUiNode)?.PendingAsyncPushQueue ?? (n as ISciterNode)?.PendingAsyncPushQueue,
                    ReplayBuffer = (n as HtmlUiNode)?.AsyncDataReplayBuffer ?? (n as ISciterNode)?.AsyncDataReplayBuffer,
                    Cache = (n as HtmlUiNode)?.AsyncDataCache ?? (n as ISciterNode)?.AsyncDataCache,
                    SetPendingPush = new Action(() =>
                    {
                        if (n is HtmlUiNode h) h.PendingAsyncDataPush = true;
                        else if (n is ISciterNode s) s.PendingAsyncDataPush = true;
                    })
                })
                .Where(x => x.Sources != null && x.Sources.Count > 0 && x.Queue != null && x.ReplayBuffer != null && x.Cache != null)
                .GroupBy(x => x.Node.Id)
                .Select(g => g.First())
                .ToList();

            if (targetUiNodes.Count == 0) return;

            // Tạo map tất cả node theo ID để lookup nhanh
            var nodeMap = connections
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .Where(n => n != null)
                .GroupBy(n => n!.Id)
                .ToDictionary(g => g.Key, g => g.First()!, System.StringComparer.OrdinalIgnoreCase);

            foreach (var uiItem in targetUiNodes)
            {
                var pushed = false;
                foreach (var ads in uiItem.Sources!)
                {
                    if (string.IsNullOrWhiteSpace(ads.SourceNodeId) || string.IsNullOrWhiteSpace(ads.SourceOutputKey))
                        continue;

                    if (!nodeMap.TryGetValue(ads.SourceNodeId, out var srcNode))
                        continue;

                    // Ưu tiên scoped value từ iteration hiện tại.
                    string? value = null;
                    var outputKey = ads.SourceOutputKey?.Trim();
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(outputKey) &&
                            service.TryGetScopedNodeStringOutput(iterationExecutionId, srcNode.Id, outputKey, out var scoped))
                        {
                            value = scoped;
                            System.Diagnostics.Debug.WriteLine($"[AsyncPush] Scoped '{outputKey}' for node '{srcNode.Id}' with execId '{iterationExecutionId}' → '{value}'");
                        }
                        else if (!asyncTaskNode.RunInParallel)
                        {
                            // Chỉ cho phép fallback shared-output khi chạy tuần tự.
                            value = service.ResolveDynamicValueForRun(srcNode, outputKey, iterationExecutionId);
                            System.Diagnostics.Debug.WriteLine($"[AsyncPush] Fallback resolve '{outputKey}' for node '{srcNode.Id}' with execId '{iterationExecutionId}' → '{value}'");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[AsyncPush] Scoped miss for '{outputKey}' on node '{srcNode.Id}' with execId '{iterationExecutionId}' (parallel mode: skip fallback)");
                        }
                    }
                    catch (Exception exResolve)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AsyncPush] Resolve error: {exResolve.Message}");
                    }

                    if (!string.IsNullOrEmpty(value))
                    {
                        var receiverKey = ads.EffectiveKey;
                        var sessionId = NormalizeAsyncSessionId(iterationExecutionId);
                        // Enqueue cho push handler (thread-safe, không mất khi parallel)
                        uiItem.Queue!.Enqueue((sessionId, receiverKey, value));
                        // Lưu history để replay đầy đủ sau F5/Ctrl+R (không chỉ value cuối theo key).
                        uiItem.ReplayBuffer!.Enqueue((sessionId, receiverKey, value));
                        while (uiItem.ReplayBuffer.Count > 2000)
                        {
                            uiItem.ReplayBuffer.TryDequeue(out _);
                        }
                        // Cập nhật cache cho F5 reload (last known value)
                        uiItem.Cache![receiverKey] = value;
                        pushed = true;
                        System.Diagnostics.Debug.WriteLine($"[AsyncPush] Enqueued '{receiverKey}' = '{value}' for node '{uiItem.Node.Id}'");
                    }
                }

                if (pushed)
                {
                    uiItem.SetPendingPush();
                }
            }
        }

        private static string NormalizeAsyncSessionId(string? executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
                return "session:unknown";

            const string dispatchMarker = ":dispatch-";
            var idx = executionId.IndexOf(dispatchMarker, StringComparison.Ordinal);
            return idx > 0 ? executionId[..idx] : executionId;
        }
    }
}
