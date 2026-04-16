using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Rendering
{
    /// <summary>
    /// Service xử lý Data Panel và resolution dữ liệu theo key từ upstream nodes (chỉ nhận từ trái sang phải).
    /// </summary>
    public static class NodeDataPanelService
    {
        /// <summary>
        /// Resolve giá trị input CHỈ từ upstream connections (FromNode -> ToNode), không nhận từ downstream.
        /// Chỉ lấy dữ liệu từ node kết nối đến input port của node hiện tại.
        /// </summary>
        public static string ResolveInputValueUpstream(IWorkflowEditorHost host, WorkflowNode toNode, WorkflowDynamicDataPort input)
        {
            if (host?.ViewModel == null) return "—";
            if (string.IsNullOrWhiteSpace(input.SelectedSourceNodeId)) return "—";

            var vm = host.ViewModel;
            var connections = vm.Connections;
            if (connections == null || connections.Count == 0) return "—";

            // Thu thập toàn bộ upstream nodes thực sự kết nối đến toNode (D): A, E, G...
            var upstream = new HashSet<WorkflowNode>();
            var stack = new Stack<WorkflowNode>();
            stack.Push(toNode);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                var incoming = connections
                    .Where(c => c.ToNode == current && c.FromNode != null)
                    .Select(c => c.FromNode)
                    .Where(n => n != null)
                    .ToList();

                foreach (var src in incoming)
                {
                    if (upstream.Add(src))
                    {
                        stack.Push(src);
                    }
                }
            }

            // Tìm source node đúng với SelectedSourceNodeId trong tập upstream hợp lệ
            var srcNode = upstream.FirstOrDefault(n => n.Id == input.SelectedSourceNodeId);
            if (srcNode == null) return "—";

            // Ưu tiên output key đã chọn; fallback sang input.Key
            var key = (input.SelectedSourceOutputKey ?? input.UserKeyOverride ?? input.Key ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                var resolved = ResolveDynamicValueByKey(srcNode, key);
                return resolved;
            }

            // Fallback summary
            return SummarizeNodeDynamicOutputs(srcNode);
        }

        /// <summary>
        /// Resolve giá trị từ node theo key. Dùng cho cả UI và execution.
        /// </summary>
        public static string ResolveDynamicValueByKey(WorkflowNode node, string key)
        {
            key = key.Trim();

            // WebNode: cookie, bearer, access_token luôn lấy từ LastCookie/LastBearer/LastAccessToken (runtime từ response),
            // KHÔNG dùng UserValueOverride vì có thể bị sync nhầm từ node input (dữ liệu cookie gửi vào, không phải cookie nhận được).
            if (node is WebNode webNodeEarly)
            {
                switch (key.ToLowerInvariant())
                {
                    case "cookie":
                        return string.IsNullOrWhiteSpace(webNodeEarly.LastCookie) ? "—" : webNodeEarly.LastCookie;
                    case "bearer":
                        return string.IsNullOrWhiteSpace(webNodeEarly.LastBearer) ? "—" : webNodeEarly.LastBearer;
                    case "access_token":
                        return string.IsNullOrWhiteSpace(webNodeEarly.LastAccessToken) ? "—" : webNodeEarly.LastAccessToken;
                }
            }

            // Kiểm tra xem có output với key này và có UserValueOverride không
            if (node.DynamicOutputs != null)
            {
                var output = node.DynamicOutputs.FirstOrDefault(o =>
                    string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));

                if (output != null && !string.IsNullOrWhiteSpace(output.UserValueOverride))
                {
                    // Ưu tiên UserValueOverride nếu có
                    return output.UserValueOverride;
                }
            }

            // StringSplitNode - return array items as JSON array string
            // để WorkflowEditorViewModel.UpdateNodeExecutionResults có thể parse và hiển thị dạng toggle "Có X kết quả"
            if (node is StringSplitNode stringSplitNode)
            {
                if (stringSplitNode.SplitResult == null || stringSplitNode.SplitResult.Count == 0)
                {
                    return "—";
                }

                // Với mọi key output (đặc biệt "ListItems"), luôn trả về JSON array
                // Ví dụ: ["item1","item2","item3"]
                return JsonSerializer.Serialize(stringSplitNode.SplitResult);
            }

            // Screen Position
            if (node is ScreenPositionPickerNode pos)
            {
                var x = (int)pos.SelectedPosition.X;
                var y = (int)pos.SelectedPosition.Y;
                return key.ToLowerInvariant() switch
                {
                    "x" => pos.HasPosition ? x.ToString() : "—",
                    "y" => pos.HasPosition ? y.ToString() : "—",
                    "position" => pos.HasPosition ? $"({x}, {y})" : "—",
                    "positiontext" => pos.PositionText,
                    _ => pos.PositionText
                };
            }

            // Screen Capture
            if (node is ScreenCaptureNode cap)
            {
                var has = cap.CapturedImage != null;
                var w = has ? cap.CapturedImage!.PixelWidth : cap.CaptureWidth;
                var h = has ? cap.CapturedImage!.PixelHeight : cap.CaptureHeight;

                switch (key.ToLowerInvariant())
                {
                    case "capturex": return cap.HasCaptureRegion ? cap.CaptureX.ToString() : "—";
                    case "capturey": return cap.HasCaptureRegion ? cap.CaptureY.ToString() : "—";
                    case "capturewidth": return cap.HasCaptureRegion ? cap.CaptureWidth.ToString() : "—";
                    case "captureheight": return cap.HasCaptureRegion ? cap.CaptureHeight.ToString() : "—";
                    case "imagesize":
                    case "dimensions":
                    case "imagewh":
                    case "imagewidthheight":
                        return (w > 0 && h > 0) ? $"{w}×{h}" : "—";
                    case "imagewidth":
                        return w > 0 ? w.ToString() : "—";
                    case "imageheight":
                        return h > 0 ? h.ToString() : "—";
                    case "captureposition":
                    case "capturexy":
                        return cap.HasCaptureRegion ? $"({cap.CaptureX}, {cap.CaptureY})" : "—";
                    case "capturerect":
                        return cap.HasCaptureRegion ? $"({cap.CaptureX}, {cap.CaptureY}, {cap.CaptureWidth}, {cap.CaptureHeight})" : "—";
                    case "imagesizebytes":
                    case "imagesizekb":
                    case "sizebytes":
                    case "filesize":
                        {
                            var bytes = TryEncodePngBytes(cap.CapturedImage);
                            if (bytes == null) return "—";
                            return $"{bytes.Length} bytes ({bytes.Length / 1024.0:0.0} KB)";
                        }
                    case "imagebase64":
                    case "base64":
                        {
                            var bytes = TryEncodePngBytes(cap.CapturedImage);
                            if (bytes == null) return "—";
                            var b64 = Convert.ToBase64String(bytes);
                            return b64.Length > 80 ? b64.Substring(0, 80) + "…" : b64;
                        }
                    case "image":
                        // legacy key
                        return has ? $"Image {w}×{h}" : "—";
                }

                // Fallback
                if (has) return $"Image {w}×{h} @ ({cap.CaptureX},{cap.CaptureY})";
                return cap.HasCaptureRegion ? $"Region {cap.CaptureWidth}×{cap.CaptureHeight} @ ({cap.CaptureX},{cap.CaptureY})" : "—";
            }

            // Input Node
            if (node is InputNode input)
            {
                // Kiểm tra xem key có match với key trong DynamicOutputs không
                // (để hỗ trợ key tùy chỉnh từ InputNode.Key)
                if (node.DynamicOutputs != null)
                {
                    var matchingOutput = node.DynamicOutputs.FirstOrDefault(o =>
                        string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                    
                    if (matchingOutput != null)
                    {
                        // Key match với output key -> trả về Value
                        if (input.IsArrayType)
                        {
                            var arr = input.ArrayValues ?? new List<string>();
                            return JsonSerializer.Serialize(arr);
                        }
                        return string.IsNullOrWhiteSpace(input.Value) ? "—" : input.Value;
                    }
                }
                
                // Backward compatible: check "Input" hoặc "value" nếu không tìm thấy trong DynamicOutputs
                if (string.Equals(key, "Input", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "value", StringComparison.OrdinalIgnoreCase))
                {
                    if (input.IsArrayType)
                    {
                        var arr = input.ArrayValues ?? new List<string>();
                        return JsonSerializer.Serialize(arr);
                    }
                    return string.IsNullOrWhiteSpace(input.Value) ? "—" : input.Value;
                }
                
                // Nếu key match với InputNode.Key property, trả về Value (không phải chính key).
                if (string.Equals(key, input.Key, StringComparison.OrdinalIgnoreCase))
                {
                    if (input.IsArrayType)
                    {
                        var arr = input.ArrayValues ?? new List<string>();
                        return JsonSerializer.Serialize(arr);
                    }
                    return string.IsNullOrWhiteSpace(input.Value) ? "—" : input.Value;
                }
                return "—";
            }

            // StorageNode - đọc giá trị đã lưu trữ toàn cục
            if (node is StorageNode storageNode)
            {
                var stored = storageNode.GetStoredOutput(key);
                if (string.IsNullOrWhiteSpace(stored)) return "—";
                return stored;
            }

            // Key Press Event Node
            if (node is KeyPressEventNode kp)
            {
                if (string.Equals(key, "key", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "triggerkey", StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrWhiteSpace(kp.Key) ? "—" : kp.Key!;
                }
                return string.IsNullOrWhiteSpace(kp.Key) ? "—" : kp.Key!;
            }

            // Hotkey Press Event Node
            if (node is HotkeyPressEventNode hk)
            {
                if (string.Equals(key, "key", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "triggerhotkey", StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrWhiteSpace(hk.Key) ? "—" : hk.Key!;
                }
                return string.IsNullOrWhiteSpace(hk.Key) ? "—" : hk.Key!;
            }

            // ListOutNode - return resolved outputs from mappings
            if (node is ListOutNode listOut)
            {
                if (listOut.ResolvedOutputs.TryGetValue(key, out var value))
                {
                    if (value == null) return "—";
                    if (value is string strVal) return string.IsNullOrWhiteSpace(strVal) ? "—" : strVal;
                    return value.ToString() ?? "—";
                }
                return "—";
            }

            // CodeNode - return resolved outputs from script return object (tra cứu không phân biệt hoa thường).
            if (node is CodeNode codeNode)
            {
                if (codeNode.ResolvedOutputs.TryGetValue(key, out var value))
                {
                    if (value == null) return "—";
                    if (value is string strVal) return string.IsNullOrWhiteSpace(strVal) ? "—" : strVal;
                    return value.ToString() ?? "—";
                }
                var match = codeNode.ResolvedOutputs.FirstOrDefault(kv =>
                    string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
                if (match.Key != null)
                {
                    value = match.Value;
                    if (value == null) return "—";
                    if (value is string strVal) return string.IsNullOrWhiteSpace(strVal) ? "—" : strVal;
                    return value.ToString() ?? "—";
                }
                return "—";
            }

            // HtmlUiNode - return resolved outputs từ DOM đã đọc theo Params hoặc từ JS postMessage
            if (node is HtmlUiNode htmlUiNode)
            {
                if (htmlUiNode.ResolvedOutputs.TryGetValue(key, out var value))
                {
                    if (value == null) return "—";
                    if (value is string strVal) return string.IsNullOrWhiteSpace(strVal) ? "—" : strVal;
                    return value.ToString() ?? "—";
                }
                var match = htmlUiNode.ResolvedOutputs.FirstOrDefault(kv =>
                    string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
                if (match.Key != null)
                {
                    value = match.Value;
                    if (value == null) return "—";
                    if (value is string strVal) return string.IsNullOrWhiteSpace(strVal) ? "—" : strVal;
                    return value.ToString() ?? "—";
                }
                return "—";
            }

            // FolderNode - return resolved outputs (folder, fullPath)
            if (node is FolderNode folderNode)
            {
                if (folderNode.ResolvedOutputs.TryGetValue(key, out var value))
                {
                    if (value == null) return "—";
                    if (value is string strVal) return string.IsNullOrWhiteSpace(strVal) ? "—" : strVal;
                    return value.ToString() ?? "—";
                }
                var fnMatch = folderNode.ResolvedOutputs.FirstOrDefault(kv =>
                    string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
                if (fnMatch.Key != null)
                {
                    value = fnMatch.Value;
                    if (value == null) return "—";
                    if (value is string strVal) return string.IsNullOrWhiteSpace(strVal) ? "—" : strVal;
                    return value.ToString() ?? "—";
                }
                return "—";
            }

            if (node is FileDownloadNode fileDl)
            {
                lock (fileDl.ResolvedOutputsSyncRoot)
                {
                    if (fileDl.ResolvedOutputs.TryGetValue(key, out var value))
                    {
                        if (value == null) return "—";
                        if (value is string strVal) return string.IsNullOrWhiteSpace(strVal) ? "—" : strVal;
                        return value.ToString() ?? "—";
                    }
                    var dlMatch = fileDl.ResolvedOutputs.FirstOrDefault(kv =>
                        string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
                    if (dlMatch.Key != null)
                    {
                        value = dlMatch.Value;
                        if (value == null) return "—";
                        if (value is string strVal) return string.IsNullOrWhiteSpace(strVal) ? "—" : strVal;
                        return value.ToString() ?? "—";
                    }
                }
                return "—";
            }

            if (node is FolderFilePathsNode ffp)
            {
                lock (ffp.ResolvedOutputsSyncRoot)
                {
                    if (ffp.ResolvedOutputs.TryGetValue(key, out var valueF))
                    {
                        if (valueF == null) return "—";
                        if (valueF is string strVal) return string.IsNullOrWhiteSpace(strVal) ? "—" : strVal;
                        return valueF.ToString() ?? "—";
                    }
                    var match = ffp.ResolvedOutputs.FirstOrDefault(kv =>
                        string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
                    if (match.Key != null)
                    {
                        valueF = match.Value;
                        if (valueF == null) return "—";
                        if (valueF is string strVal) return string.IsNullOrWhiteSpace(strVal) ? "—" : strVal;
                        return valueF.ToString() ?? "—";
                    }
                }
                return "—";
            }

            // OutputNode - return formatted output text
            if (node is OutputNode outputNode)
            {
                // Check if key matches OutputKey
                if (string.Equals(key, outputNode.OutputKey, StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrWhiteSpace(outputNode.OutputText) ? "—" : outputNode.OutputText;
                }
                return "—";
            }

            // LoopNode - resolve value từ ListOutNodes trong LoopBody
            // Outputs từ ListOutNode được copy sang LoopNode, nhưng value cần resolve từ ListOutNode gốc
            if (node is LoopNode loopNode)
            {
                // Tìm ListOutNode trong LoopBody có output key match
                var listOutNodeInBody = FindListOutNodeWithOutputKey(loopNode, key);
                if (listOutNodeInBody != null)
                {
                    if (listOutNodeInBody.ResolvedOutputs.TryGetValue(key, out var value))
                    {
                        if (value == null) return "—";
                        if (value is string strVal) return string.IsNullOrWhiteSpace(strVal) ? "—" : strVal;
                        return value.ToString() ?? "—";
                    }
                }
                return "—";
            }

            // HttpRequestNode - return response data from last request
            if (node is HttpRequestNode httpNode)
            {
                switch (key.ToLowerInvariant())
                {
                    case "statuscode":
                        return httpNode.LastStatusCode?.ToString() ?? "—";
                    case "responsebody":
                        return string.IsNullOrWhiteSpace(httpNode.LastResponseBody) ? "—" : httpNode.LastResponseBody;
                    case "responseheaders":
                        if (httpNode.LastResponseHeaders == null || httpNode.LastResponseHeaders.Count == 0)
                            return "—";
                        return JsonSerializer.Serialize(httpNode.LastResponseHeaders);
                    case "issuccess":
                        return httpNode.LastIsSuccess?.ToString() ?? "—";
                    case "errormessage":
                        return string.IsNullOrWhiteSpace(httpNode.LastErrorMessage) ? "—" : httpNode.LastErrorMessage;
                    case "responsetimems":
                        return httpNode.LastResponseTimeMs?.ToString() ?? "—";
                    case "curl":
                        return string.IsNullOrWhiteSpace(httpNode.LastCurlCommand) ? "—" : httpNode.LastCurlCommand;
                    default:
                        return "—";
                }
            }

            // WebNode - cookie, bearer, access_token từ response, và các response outputs đã cấu hình
            if (node is WebNode webNode)
            {
                switch (key.ToLowerInvariant())
                {
                    case "cookie":
                        return string.IsNullOrWhiteSpace(webNode.LastCookie) ? "—" : webNode.LastCookie;
                    case "bearer":
                        return string.IsNullOrWhiteSpace(webNode.LastBearer) ? "—" : webNode.LastBearer;
                    case "access_token":
                        return string.IsNullOrWhiteSpace(webNode.LastAccessToken) ? "—" : webNode.LastAccessToken;
                    default:
                        // Kiểm tra ResponseOutputValues cho các output đã cấu hình
                        if (webNode.ResponseOutputValues != null && webNode.ResponseOutputValues.TryGetValue(key, out var responseValue))
                        {
                            return string.IsNullOrWhiteSpace(responseValue) ? "—" : responseValue;
                        }
                        return "—";
                }
            }

            return "—";
        }

        private static string SummarizeNodeDynamicOutputs(WorkflowNode node)
        {
            if (node.DynamicOutputs == null || node.DynamicOutputs.Count == 0) return "—";
            if (node.DynamicOutputs.Count == 1) return ResolveDynamicValueByKey(node, node.DynamicOutputs[0].Key);
            return $"[{node.DynamicOutputs.Count} outputs]";
        }

        /// <summary>
        /// Tìm ListOutNode trong LoopBody có output key match.
        /// Dùng để resolve value từ ListOutNode cho LoopNode.
        /// </summary>
        private static ListOutNode? FindListOutNodeWithOutputKey(LoopNode loopNode, string key)
        {
            var body = loopNode.LoopBodyNode;
            if (body == null) return null;

            // Tìm trong CachedListOutNodes nếu có
            if (loopNode.CachedListOutNodes != null)
            {
                foreach (var listOutNode in loopNode.CachedListOutNodes)
                {
                    if (listOutNode.DynamicOutputs.Any(o => 
                        string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase)))
                    {
                        return listOutNode;
                    }
                }
            }

            return null;
        }

        private static byte[]? TryEncodePngBytes(BitmapSource? source)
        {
            if (source == null) return null;

            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }
    }
}

