using FlowMy.Models;
using FlowMy.Models.Nodes;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho HtmlUiNode: trigger đọc DOM từ WebView2 theo Params khi chạy workflow.
    /// Sau khi đọc xong, outputs sẽ có trong ResolvedOutputs và có thể dùng cho node tiếp theo.
    /// </summary>
    internal sealed class HtmlUiNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is HtmlUiNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var htmlNode = (HtmlUiNode)node;
            var connections = env.Connections;

            // Clear outputs cũ
            htmlNode.ResolvedOutputs.Clear();
            htmlNode.RequestWake();

            try
            {
                // Trigger UI thread đọc DOM từ WebView2 theo Params
                // HtmlUiNodeControl sẽ listen PropertyChanged và đọc DOM khi thấy PendingReadDom = true
                htmlNode.PendingReadDom = true;

                // Đợi một chút để UI thread có thời gian đọc DOM (WebView2.ExecuteScriptAsync là async)
                // Thường thì 100-200ms là đủ, nhưng để an toàn đợi tối đa 2 giây
                var maxWaitMs = 2000;
                var checkIntervalMs = 50;
                var waited = 0;

                while (waited < maxWaitMs)
                {
                    // Dùng try/catch để TaskCanceledException không crash toàn bộ flow
                    try
                    {
                        await Task.Delay(checkIntervalMs, env.CancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Workflow bị cancel — thoát sớm, không throw
                        htmlNode.PendingReadDom = false;
                        return;
                    }
                    waited += checkIntervalMs;

                    // Nếu PendingReadDom đã được reset về false nghĩa là đã đọc xong
                    if (!htmlNode.PendingReadDom)
                    {
                        break;
                    }

                    // Nếu đã có outputs thì cũng coi như xong (có thể đã có từ trước hoặc đã đọc xong)
                    if (htmlNode.ResolvedOutputs.Count > 0)
                    {
                        break;
                    }
                }

                // Reset flag sau khi đợi xong
                htmlNode.PendingReadDom = false;

                // Nếu sau khi đợi vẫn không có outputs, có thể Params chưa cấu hình hoặc DOM chưa sẵn sàng
                if (htmlNode.ResolvedOutputs.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("HtmlUiNodeExecutor: No outputs found after reading DOM. Check Params configuration.");
                }

                // ─── Async Data Sources: resolve + cache + trigger push ───
                try
                {
                    var asyncSources = htmlNode.AsyncDataSources;
                    if (asyncSources != null && asyncSources.Count > 0)
                    {
                        // Tìm tất cả node trong workflow
                        var allNodes = env.Connections?
                            .SelectMany(c => new[] { c.FromNode, c.ToNode })
                            .Where(n => n != null)
                            .Select(n => n!)
                            .GroupBy(n => n.Id)
                            .Select(g => g.First())
                            .ToList() ?? new System.Collections.Generic.List<WorkflowNode>();

                        foreach (var ads in asyncSources)
                        {
                            if (string.IsNullOrWhiteSpace(ads.SourceNodeId) || string.IsNullOrWhiteSpace(ads.SourceOutputKey))
                                continue;

                            try
                            {
                                var srcNode = allNodes.FirstOrDefault(n =>
                                    string.Equals(n.Id, ads.SourceNodeId, StringComparison.OrdinalIgnoreCase));

                                if (srcNode == null) continue;

                                // Ưu tiên scoped value (env-aware), fallback sang NodeDataPanelService
                                string? value = null;
                                try
                                {
                                    value = env.Service.ResolveDynamicValueForExecution(srcNode, ads.SourceOutputKey, env);
                                }
                                catch
                                {
                                    // Fallback: node có thể chưa run → output chưa có → bỏ qua
                                }

                                if (string.IsNullOrEmpty(value))
                                {
                                    // Fallback: thử direct output
                                    value = Services.Rendering.NodeDataPanelService.ResolveDynamicValueByKey(srcNode, ads.SourceOutputKey);
                                }

                                if (!string.IsNullOrEmpty(value))
                                {
                                    var receiverKey = ads.EffectiveKey;
                                    htmlNode.AsyncDataReplayBuffer.Enqueue((receiverKey, value));
                                    while (htmlNode.AsyncDataReplayBuffer.Count > 2000)
                                    {
                                        htmlNode.AsyncDataReplayBuffer.TryDequeue(out _);
                                    }
                                    htmlNode.AsyncDataCache[receiverKey] = value;
                                }
                            }
                            catch (Exception adsEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"HtmlUiNodeExecutor: AsyncDataSource resolve error for '{ads.SourceOutputKey}': {adsEx.Message}");
                            }
                        }

                        // Trigger push vào WebView2
                        if (htmlNode.AsyncDataCache.Count > 0)
                        {
                            htmlNode.PendingAsyncDataPush = true;
                        }
                    }
                }
                catch (Exception asyncEx)
                {
                    System.Diagnostics.Debug.WriteLine($"HtmlUiNodeExecutor: AsyncDataSources error: {asyncEx.Message}");
                    // Non-fatal — continue workflow
                }

                if (!string.IsNullOrWhiteSpace(env.ExecutionId) && htmlNode.ResolvedOutputs.Count > 0)
                {
                    var snapshot = new Dictionary<string, object?>(htmlNode.ResolvedOutputs, StringComparer.OrdinalIgnoreCase);
                    env.Service.PublishDictionaryOutputsToScopedStore(env.ExecutionId, htmlNode.Id, snapshot);
                }

                env.OnNodeCompleted?.Invoke(htmlNode, default);
            }
            catch (OperationCanceledException)
            {
                // Workflow bị cancel — reset flag và thoát sạch
                htmlNode.PendingReadDom = false;
                return;
            }
            catch (Exception ex)
            {
                htmlNode.PendingReadDom = false;
                System.Diagnostics.Debug.WriteLine($"HtmlUiNodeExecutor error: {ex.Message}");
                htmlNode.ResolvedOutputs["error"] = ex.Message;
                env.OnNodeFailed?.Invoke(htmlNode, ex.Message);
                throw;
            }

            await env.TraverseOutputsAsync(htmlNode).ConfigureAwait(false);
        }
    }
}
