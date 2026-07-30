using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            // Thu thập upstream giống BaseNodeDialogViewModel.RefreshAvailableSourcesForInputs:
            // - bỏ cạnh vào LoopBodyRight (return path, không phải data flow)
            // - ListOutNode là barrier (không đi ngược qua ListOut)
            // - ghi nhận LoopNode cha khi đi qua LoopBodyLeft
            var upstream = new HashSet<WorkflowNode>();
            var listOutBarriers = new HashSet<ListOutNode>();
            var parentLoops = new HashSet<LoopNode>();
            var stack = new Stack<WorkflowNode>();
            stack.Push(toNode);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                var incoming = connections
                    .Where(c => c.ToNode == current && c.FromNode != null)
                    .Where(c => !(current is LoopBodyNode &&
                                  c.ToPort != null &&
                                  string.Equals(c.ToPort.Id, "LoopBodyRight", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                foreach (var conn in incoming)
                {
                    var src = conn.FromNode!;
                    if (src is ListOutNode listOutNode)
                    {
                        if (upstream.Add(src))
                            listOutBarriers.Add(listOutNode);
                        continue;
                    }

                    if (src is LoopBodyNode body &&
                        conn.FromPort != null &&
                        string.Equals(conn.FromPort.Id, "LoopBodyLeft", StringComparison.OrdinalIgnoreCase) &&
                        body.ParentLoopNode != null)
                    {
                        parentLoops.Add(body.ParentLoopNode);
                    }

                    if (upstream.Add(src))
                        stack.Push(src);
                }
            }

            var producerNodes = upstream
                .Where(n => n.DynamicOutputs != null && n.DynamicOutputs.Count > 0)
                .ToList();

            if (listOutBarriers.Count > 0)
            {
                producerNodes = producerNodes
                    .Where(n => n is ListOutNode)
                    .ToList();
            }

            foreach (var loop in parentLoops)
            {
                if (loop.DynamicOutputs != null && loop.DynamicOutputs.Count > 0 && !producerNodes.Contains(loop))
                {
                    if (listOutBarriers.Count == 0)
                        producerNodes.Add(loop);
                }
            }

            producerNodes = producerNodes
                .Where(n => !ReferenceEquals(n, toNode))
                .ToList();

            // InputNode có thể không có DynamicOutputs nhưng vẫn là nguồn hợp lệ (giống một số dialog refresh).
            var srcNode = producerNodes.FirstOrDefault(n =>
                              string.Equals(n.Id, input.SelectedSourceNodeId, StringComparison.OrdinalIgnoreCase))
                          ?? upstream.FirstOrDefault(n =>
                              string.Equals(n.Id, input.SelectedSourceNodeId, StringComparison.OrdinalIgnoreCase) &&
                              n is InputNode);
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
        /// Resolve giá trị từ node theo key. Dùng cho cả UI (forDisplay = true) và execution/data-passing (forDisplay = false).
        /// Khi forDisplay = true (mặc định cho UI canvas), các dữ liệu dạng base64 lớn sẽ được ngắt đoạn "..." để tránh làm đơ WPF canvas.
        /// Khi forDisplay = false (dành cho copy hoặc truyền dữ liệu sang node logic khác), giữ nguyên base64 đầy đủ.
        /// </summary>
        public static string ResolveDynamicValueByKey(WorkflowNode node, string key, bool forDisplay = true)
        {
            var rawValue = ResolveRawDynamicValueByKey(node, key);
            if (forDisplay && IsBase64Value(key, rawValue))
            {
                return TruncateBase64ForDisplay(rawValue);
            }
            return rawValue;
        }

        public static string ResolveRawDynamicValueByKey(WorkflowNode node, string key)
        {
            key = key.Trim();
            if (string.IsNullOrWhiteSpace(key)) return "—";

            // Hỗ trợ truy cập đường dẫn cú pháp ngoặc vuông: key[fieldName], key[index], key[fieldName][index], key[index][fieldName], key[0...n]
            if (key.Contains("[") && key.Contains("]"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(key, @"^([^\[]+)((?:\[[^\]]+\])+)$");
                if (match.Success)
                {
                    var rootKey = match.Groups[1].Value.Trim();
                    var bracketExpr = match.Groups[2].Value;

                    var rootRaw = ResolveRawDynamicValueByKey(node, rootKey);
                    if (!string.IsNullOrWhiteSpace(rootRaw) && rootRaw != "—")
                    {
                        var evaluated = EvaluateJsonPath(rootRaw, bracketExpr);
                        if (evaluated != null)
                        {
                            return evaluated;
                        }
                    }
                }
            }

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

            if (node is AsyncTaskNode asyncTask)
            {
                if (!string.IsNullOrWhiteSpace(asyncTask.LastExecutionId))
                {
                    var scopedVal = TryGetScopedOutput(asyncTask.LastExecutionId, asyncTask.Id, key);
                    if (!string.IsNullOrWhiteSpace(scopedVal) && scopedVal != "—")
                        return scopedVal;

                    var rootId = asyncTask.LastExecutionId;
                    var idx = rootId.IndexOf(":dispatch-", StringComparison.Ordinal);
                    if (idx > 0) rootId = rootId.Substring(0, idx);
                    scopedVal = TryGetScopedOutput(rootId, asyncTask.Id, key);
                    if (!string.IsNullOrWhiteSpace(scopedVal) && scopedVal != "—")
                        return scopedVal;
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
                // Kiểm tra SkipOutputs: key bị unchecked trong dialog → trả về "—" (không output)
                if (cap.SkipOutputs != null && cap.SkipOutputs.Contains(key))
                    return "—";

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
                            // Chỉ hiển thị 50 ký tự đầu để tránh nặng UI
                            return b64.Length > 50 ? b64.Substring(0, 50) + "…" : b64;
                        }
                    case "image":
                        // legacy key
                        return has ? $"Image {w}×{h}" : "—";
                }

                // Fallback
                if (has) return $"Image {w}×{h} @ ({cap.CaptureX},{cap.CaptureY})";
                return cap.HasCaptureRegion ? $"Region {cap.CaptureWidth}×{cap.CaptureHeight} @ ({cap.CaptureX},{cap.CaptureY})" : "—";
            }

            // TextScanNode (OCR)
            if (node is TextScanNode textScan)
            {
                if (textScan.SkipOutputs != null && textScan.SkipOutputs.Contains(key))
                    return "—";

                var hasImg = textScan.CapturedImage != null;
                var w = hasImg ? textScan.CapturedImage!.PixelWidth : textScan.CaptureWidth;
                var h = hasImg ? textScan.CapturedImage!.PixelHeight : textScan.CaptureHeight;

                switch (key.ToLowerInvariant())
                {
                    case "extractedtext":
                    case "text":
                        return string.IsNullOrWhiteSpace(textScan.ExtractedText) ? "—" : textScan.ExtractedText;
                    case "extractedtextlines":
                    case "lines":
                        return string.IsNullOrWhiteSpace(textScan.ExtractedTextLines) ? "—" : textScan.ExtractedTextLines;
                    case "capturex": return textScan.HasCaptureRegion ? textScan.CaptureX.ToString() : "—";
                    case "capturey": return textScan.HasCaptureRegion ? textScan.CaptureY.ToString() : "—";
                    case "capturewidth": return textScan.HasCaptureRegion ? textScan.CaptureWidth.ToString() : "—";
                    case "captureheight": return textScan.HasCaptureRegion ? textScan.CaptureHeight.ToString() : "—";
                    case "imagewidth": return w > 0 ? w.ToString() : "—";
                    case "imageheight": return h > 0 ? h.ToString() : "—";
                    case "ocrlanguage": return string.IsNullOrWhiteSpace(textScan.OcrLanguage) ? "—" : textScan.OcrLanguage;
                    case "wordcount": return textScan.ExtractedWords != null ? textScan.ExtractedWords.Count.ToString() : "0";
                    case "imagebase64":
                    case "base64":
                    case "image":
                        {
                            var bytes = TryEncodePngBytes(textScan.CapturedImage);
                            if (bytes != null && bytes.Length > 0)
                            {
                                return Convert.ToBase64String(bytes);
                            }
                            if (!string.IsNullOrWhiteSpace(textScan.Base64Image))
                            {
                                return textScan.Base64Image;
                            }
                            if (!string.IsNullOrWhiteSpace(textScan.ImagePath) && File.Exists(textScan.ImagePath))
                            {
                                try
                                {
                                    var fileBytes = File.ReadAllBytes(textScan.ImagePath);
                                    return Convert.ToBase64String(fileBytes);
                                }
                                catch { }
                            }
                            return "—";
                        }
                }
            }

            // ImageProcessingNode
            if (node is ImageProcessingNode imgProc)
            {
                if (imgProc.SkipOutputs != null && imgProc.SkipOutputs.Contains(key))
                    return "—";

                switch (key.ToLowerInvariant())
                {
                    case "imagebase64":
                    case "base64":
                    case "image":
                    case "processedimagebase64":
                        if (!string.IsNullOrWhiteSpace(imgProc.ImageBase64))
                            return imgProc.ImageBase64;
                        if (!string.IsNullOrWhiteSpace(imgProc.ImageUrl) && File.Exists(imgProc.ImageUrl))
                        {
                            try
                            {
                                var fileBytes = File.ReadAllBytes(imgProc.ImageUrl);
                                return Convert.ToBase64String(fileBytes);
                            }
                            catch { }
                        }
                        if (!string.IsNullOrWhiteSpace(imgProc.ImageUrl))
                            return imgProc.ImageUrl;
                        return "—";
                    case "imageurl":
                    case "imagepath":
                        return string.IsNullOrWhiteSpace(imgProc.ImageUrl) ? "—" : imgProc.ImageUrl;
                }
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
                string? directResult = null;
                switch (key.ToLowerInvariant())
                {
                    case "statuscode":
                        directResult = httpNode.LastStatusCode?.ToString();
                        break;
                    case "responsebody":
                        directResult = httpNode.LastResponseBody;
                        break;
                    case "responseheaders":
                        if (httpNode.LastResponseHeaders != null && httpNode.LastResponseHeaders.Count > 0)
                            directResult = JsonSerializer.Serialize(httpNode.LastResponseHeaders);
                        break;
                    case "issuccess":
                        directResult = httpNode.LastIsSuccess?.ToString();
                        break;
                    case "errormessage":
                        directResult = httpNode.LastErrorMessage;
                        break;
                    case "responsetimems":
                        directResult = httpNode.LastResponseTimeMs?.ToString();
                        break;
                    case "curl":
                        directResult = httpNode.LastCurlCommand;
                        break;
                }

                // Nếu Last* có giá trị, trả về ngay
                if (!string.IsNullOrWhiteSpace(directResult))
                    return directResult;

                // Fallback: đọc từ scoped output store (hỗ trợ parallel execution mode)
                var execId = httpNode.LastExecutionId;
                if (!string.IsNullOrWhiteSpace(execId))
                {
                    var scopedVal = TryGetScopedOutput(execId, httpNode.Id, key.ToLowerInvariant());
                    if (!string.IsNullOrWhiteSpace(scopedVal))
                        return scopedVal;
                }

                return "—";
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

        /// <summary>
        /// Đọc giá trị từ scoped output historical cache — fallback cho trường hợp
        /// node.Last* bị null (parallel execution mode không set shared state).
        /// </summary>
        private static string? TryGetScopedOutput(string executionId, string nodeId, string key)
        {
            if (string.IsNullOrWhiteSpace(executionId) || string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(key))
                return null;
            try
            {
                var cache = FlowMy.Services.Workflow.WorkflowExecutionService.ScopedOutputsHistoricalCache;
                if (cache.TryGetValue(executionId, out var byNode) &&
                    byNode.TryGetValue(nodeId, out var byKey) &&
                    byKey.TryGetValue(key, out var val) &&
                    !string.IsNullOrWhiteSpace(val))
                {
                    return val;
                }
            }
            catch { /* Ignore read errors from concurrent cleanup */ }
            return null;
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

        public static bool IsBase64Value(string? key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "—") return false;
            var trimmed = value.Trim();

            if (!string.IsNullOrWhiteSpace(key))
            {
                var k = key.ToLowerInvariant();
                if (k.Contains("base64") || k.Contains("crop") || k.Contains("image") || k.Contains("avatar") || k.Contains("photo") || k.Contains("thumb"))
                    return true;
            }

            if (trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("data:application/", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("data:video/", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("data:audio/", StringComparison.OrdinalIgnoreCase))
                return true;

            if (trimmed.StartsWith("[\"data:image/", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("[\"iVBORw0", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("[\"/9j/", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("[\"UklGR", StringComparison.OrdinalIgnoreCase))
                return true;

            if (trimmed.Length > 80 && (
                trimmed.StartsWith("iVBORw0", StringComparison.Ordinal) ||
                trimmed.StartsWith("/9j/", StringComparison.Ordinal) ||
                trimmed.StartsWith("UklGR", StringComparison.Ordinal) ||
                trimmed.StartsWith("R0lGOD", StringComparison.Ordinal) ||
                trimmed.StartsWith("Qk0", StringComparison.Ordinal) ||
                trimmed.StartsWith("JVBERi", StringComparison.Ordinal)))
            {
                return true;
            }

            return false;
        }

        public static string TruncateBase64ForDisplay(string? rawValue, int maxChars = 50)
        {
            if (string.IsNullOrWhiteSpace(rawValue) || rawValue == "—") return "—";
            var trimmed = rawValue.Trim();

            if ((trimmed.StartsWith("[") && trimmed.EndsWith("]")) ||
                (trimmed.StartsWith("{") && trimmed.EndsWith("}")))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    return FormatElementForDisplay(doc.RootElement, maxChars);
                }
                catch { }
            }

            return TruncateSingleBase64String(trimmed, maxChars);
        }

        private static string FormatElementForDisplay(JsonElement element, int maxChars = 50)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var str = item.GetString() ?? string.Empty;
                        list.Add(IsBase64Value(null, str) ? TruncateSingleBase64String(str, maxChars) : (str.Length > maxChars ? str.Substring(0, maxChars) + "..." : str));
                    }
                    else if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    {
                        using var doc = JsonDocument.Parse(FormatElementForDisplay(item, maxChars));
                        list.Add(doc.RootElement.Clone());
                    }
                    else
                    {
                        list.Add(item.Clone());
                    }
                }
                return JsonSerializer.Serialize(list);
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                var dict = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var str = prop.Value.GetString() ?? string.Empty;
                        dict[prop.Name] = IsBase64Value(prop.Name, str) ? TruncateSingleBase64String(str, maxChars) : (str.Length > maxChars ? str.Substring(0, maxChars) + "..." : str);
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        using var doc = JsonDocument.Parse(FormatElementForDisplay(prop.Value, maxChars));
                        dict[prop.Name] = doc.RootElement.Clone();
                    }
                    else
                    {
                        dict[prop.Name] = prop.Value.Clone();
                    }
                }
                return JsonSerializer.Serialize(dict);
            }

            return element.ToString();
        }

        private static string TruncateSingleBase64String(string? value, int maxChars = 50)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var trimmed = value.Trim();

            if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var commaIdx = trimmed.IndexOf(',');
                if (commaIdx > 0 && commaIdx < trimmed.Length - 1)
                {
                    var prefix = trimmed.Substring(0, commaIdx + 1);
                    var payload = trimmed.Substring(commaIdx + 1);
                    var payloadKb = (payload.Length * 3 / 4) / 1024.0;
                    var shortPayload = payload.Length > 16 ? payload.Substring(0, 16) + "…" : payload;
                    return $"{prefix}{shortPayload} ({payloadKb:0.0} KB)";
                }
            }

            if (trimmed.Length > maxChars)
            {
                var approxKb = (trimmed.Length * 3 / 4) / 1024.0;
                var shortPart = trimmed.Substring(0, Math.Min(25, trimmed.Length));
                return $"{shortPart}… ({approxKb:0.0} KB)";
            }

            return trimmed;
        }

        /// <summary>
        /// Evaluate cú pháp ngoặc vuông trên JSON object / JSON array tương tự OutputNode:
        /// - [fieldName] -> lấy thuộc tính của object
        /// - [index] -> lấy phần tử của array
        /// - [fieldName][index] hoặc [index][fieldName] -> lấy mảng trong object / object trong mảng
        /// - [0...n] -> hiển thị danh sách tất cả phần tử
        /// </summary>
        public static string? EvaluateJsonPath(string jsonRaw, string bracketExpression)
        {
            if (string.IsNullOrWhiteSpace(jsonRaw) || string.IsNullOrWhiteSpace(bracketExpression))
                return null;

            var matches = System.Text.RegularExpressions.Regex.Matches(bracketExpression, @"\[([^\]]+)\]");
            if (matches.Count == 0) return null;

            var segments = new List<string>();
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                var seg = m.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(seg))
                {
                    segments.Add(seg);
                }
            }

            if (segments.Count == 0) return null;

            try
            {
                using var doc = JsonDocument.Parse(jsonRaw.Trim());
                var current = doc.RootElement;

                for (int i = 0; i < segments.Count; i++)
                {
                    var seg = segments[i];

                    if (string.Equals(seg, "0...n", StringComparison.OrdinalIgnoreCase))
                    {
                        if (current.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<string>();
                            foreach (var el in current.EnumerateArray())
                            {
                                if (el.ValueKind == JsonValueKind.String)
                                    list.Add(el.GetString() ?? string.Empty);
                                else
                                    list.Add(el.ToString());
                            }
                            return $"[{string.Join(", ", list)}]";
                        }
                        else if (current.ValueKind == JsonValueKind.String)
                        {
                            return current.GetString();
                        }
                        else
                        {
                            return current.ToString();
                        }
                    }

                    if (int.TryParse(seg, out var index))
                    {
                        if (current.ValueKind == JsonValueKind.Array)
                        {
                            int len = current.GetArrayLength();
                            if (index >= 0 && index < len)
                            {
                                current = current[index];
                            }
                            else
                            {
                                return string.Empty;
                            }
                        }
                        else if (current.ValueKind == JsonValueKind.Object)
                        {
                            var indexKey = index.ToString();
                            bool found = false;
                            foreach (var prop in current.EnumerateObject())
                            {
                                if (string.Equals(prop.Name, indexKey, StringComparison.OrdinalIgnoreCase))
                                {
                                    current = prop.Value;
                                    found = true;
                                    break;
                                }
                            }
                            if (!found) return string.Empty;
                        }
                        else
                        {
                            return string.Empty;
                        }
                    }
                    else
                    {
                        if (current.ValueKind == JsonValueKind.Object)
                        {
                            bool found = false;
                            foreach (var prop in current.EnumerateObject())
                            {
                                if (string.Equals(prop.Name, seg, StringComparison.OrdinalIgnoreCase))
                                {
                                    current = prop.Value;
                                    found = true;
                                    break;
                                }
                            }
                            if (!found) return string.Empty;
                        }
                        else
                        {
                            return string.Empty;
                        }
                    }
                }

                if (current.ValueKind == JsonValueKind.String)
                {
                    return current.GetString() ?? string.Empty;
                }
                else if (current.ValueKind == JsonValueKind.Null)
                {
                    return string.Empty;
                }
                else if (current.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var el in current.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.String)
                            list.Add(el.GetString() ?? string.Empty);
                        else
                            list.Add(el.ToString());
                    }
                    return $"[{string.Join(", ", list)}]";
                }
                else
                {
                    return current.ToString();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}

