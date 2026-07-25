using System;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using System.Collections.Specialized;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho OutputNode.
    /// Format string với các biến input và tạo output text.
    /// </summary>
    internal sealed class OutputNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is OutputNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var outputNode = (OutputNode)node;
            var connections = env.Connections;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                outputNode.OutputText = string.Empty;

                // Resolve all input variables
                // Use case-insensitive dictionary for variable lookup
                var variableValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                // Store raw array values for index access
                var variableArrays = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                // Store parsed JSON objects for field access
                var variableObjects = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var variable in outputNode.InputVariables)
                {
                    var variableKey = variable.VariableKey?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(variableKey)) continue;

                    string value = string.Empty;

                    if (variable.UseClipboard)
                    {
                        // Đợi 150ms để hệ điều hành và ứng dụng đích kịp xử lý thao tác Ctrl+C và ghi dữ liệu vào Clipboard
                        await Task.Delay(150, env.CancellationToken);
                        try
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                if (Clipboard.ContainsText())
                                {
                                    value = Clipboard.GetText() ?? string.Empty;
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"OutputNode: Failed to read from clipboard for variable {variableKey}: {ex.Message}");
                        }
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(variable.SourceNodeId) ||
                            string.IsNullOrWhiteSpace(variable.SourceOutputKey))
                        {
                            variableValues[variableKey] = string.Empty;
                            continue;
                        }

                        // Find source node
                        WorkflowNode? sourceNode = null;

                        // Try direct connection first
                        var upstreamConnection = connections
                            .FirstOrDefault(c =>
                                c.ToNode == outputNode &&
                                c.FromNode != null &&
                                c.FromNode.Id == variable.SourceNodeId);

                        sourceNode = upstreamConnection?.FromNode;

                        // Fallback: find node by ID in graph (for LoopBody scenarios)
                        if (sourceNode == null)
                        {
                            sourceNode = connections
                                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                                .FirstOrDefault(n => n != null && n.Id == variable.SourceNodeId);
                        }

                        // Resolve value from source node
                        if (sourceNode != null)
                        {
                            string? scopedValue = null;
                            var scopedFound = !string.IsNullOrWhiteSpace(env.ExecutionId)
                                && env.Service.TryGetScopedNodeStringOutputForLookupChain(env.ExecutionId, sourceNode.Id, variable.SourceOutputKey, out scopedValue);

                            // Runtime nodes in async/parallel branches must read scoped value of current execution only.
                            // If scoped value is missing, falling back to shared UI state can leak stale data from other iterations.
                            if (scopedFound)
                            {
                                value = scopedValue ?? string.Empty;
                            }
                            else
                            {
                                // Fallback: đọc từ DynamicOutputs / UserValueOverride (vd: cropBase64 set từ LayerAiDialog)
                                // InputNode luôn cần fallback; các node khác cũng cần nếu output nằm trong DynamicOutputs.
                                value = env.Service.ResolveDynamicValueForExecution(sourceNode, variable.SourceOutputKey, env);
                            }
                        }
                    }

                    // Check if value is JSON object
                    if (IsObjectValue(value))
                    {
                        // Parse JSON object and store both formatted string and parsed dictionary
                        var parsedObject = ParseObjectToDictionary(value);
                        if (parsedObject != null && parsedObject.Count > 0)
                        {
                            variableObjects[variableKey] = parsedObject;
                            variableValues[variableKey] = FormatObjectValue(value);
                        }
                        else
                        {
                            variableValues[variableKey] = "{}";
                        }
                    }
                    // Check if value is array
                    else if (IsArrayValue(value))
                    {
                        // Parse array and store both formatted string and parsed list
                        var parsedArray = ParseArrayToList(value);
                        if (parsedArray != null && parsedArray.Count > 0)
                        {
                            variableArrays[variableKey] = parsedArray;
                            variableValues[variableKey] = FormatArrayValue(value);
                        }
                        else
                        {
                            variableValues[variableKey] = "[]";
                        }
                    }
                    else
                    {
                        // Handle "—" as empty
                        if (value == "—" || string.IsNullOrWhiteSpace(value))
                        {
                            variableValues[variableKey] = string.Empty;
                        }
                        else
                        {
                            variableValues[variableKey] = value;
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"OutputNode: Resolved {variableKey} = '{variableValues[variableKey]}'");
                }
                
                System.Diagnostics.Debug.WriteLine($"OutputNode: Total variables resolved: {variableValues.Count}");
                foreach (var kvp in variableValues)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {kvp.Key} = '{kvp.Value}'");
                }

                // Format string với các biến
                var formatString = outputNode.FormatString ?? string.Empty;
                var outputText = FormatString(formatString, variableValues, variableArrays, variableObjects);

                // ── QUAN TRỌNG: Lưu vào scoped store TRƯỚC khi set shared OutputText ──
                // Parallel iterations dùng chung OutputNode object → OutputText bị overwrite.
                // Lưu scoped store ngay tại đây với executionId riêng → đảm bảo mỗi iteration có giá trị đúng.
                if (!string.IsNullOrWhiteSpace(outputNode.OutputKey) && !string.IsNullOrWhiteSpace(env.ExecutionId))
                {
                    env.Service.SetScopedNodeStringOutput(env.ExecutionId, outputNode.Id, outputNode.OutputKey.Trim(), outputText);
                }

                outputNode.OutputText = outputText;

                bool hasImageClipboard = false;

                // ── Tự động xử lý Copy ảnh / dữ liệu vào Clipboard với đa định dạng (CF_DIB, HTML, FileDrop, Bitmap, Text) ──
                if (outputNode.CopyImagesToClipboard || outputNode.SaveToClipboard)
                {
                    try
                    {
                        var base64List = ExtractBase64Images(outputNode, variableValues, variableArrays, variableObjects);
                        var cleanedOutput = CleanBase64(outputText);
                        if (IsLikelyBase64Image(cleanedOutput) && !base64List.Contains(cleanedOutput))
                        {
                            base64List.Insert(0, cleanedOutput);
                        }

                        if (base64List.Count > 0)
                        {
                            string? textToSave = outputNode.SaveToClipboard ? outputText : null;
                            CopyBase64ImagesToClipboard(base64List, textToSave);
                            hasImageClipboard = true;
                            System.Diagnostics.Debug.WriteLine($"OutputNode: Copied {base64List.Count} image(s) and multi-format data to clipboard");
                        }
                    }
                    catch (Exception imgEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"OutputNode: Failed to copy images to clipboard: {imgEx.Message}");
                    }
                }

                // Fallback: Copy to clipboard as plain text if SaveToClipboard is enabled and images were not processed
                if (outputNode.SaveToClipboard && !hasImageClipboard && !string.IsNullOrWhiteSpace(outputText))
                {
                    try
                    {
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            Clipboard.SetText(outputText);
                        });
                        System.Diagnostics.Debug.WriteLine($"OutputNode: Copied plain text to clipboard");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"OutputNode: Failed to copy text to clipboard: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"OutputNode: Formatted output = {outputText}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OutputNode error: {ex.Message}");
                outputNode.OutputText = string.Empty;
                env.OnNodeFailed?.Invoke(outputNode, ex.Message);
                throw;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(outputNode, sw.Elapsed);

            await env.TraverseOutputsAsync(outputNode);
        }


        /// <summary>
        /// Format string với các biến. Hỗ trợ:
        /// - {variableKey} - giá trị thường hoặc toàn bộ array/object
        /// - {variableKey[0...n]} - toàn bộ array
        /// - {variableKey[index]} - phần tử tại index cụ thể (ví dụ: {Input1[0]}, {Input1[1]})
        /// - {variableKey[fieldName]} - trường của JSON object (ví dụ: {Input1[name]}, {Input1[age]})
        /// </summary>
        private string FormatString(string formatString, Dictionary<string, string> variableValues, Dictionary<string, List<string>> variableArrays, Dictionary<string, Dictionary<string, string>> variableObjects)
        {
            if (string.IsNullOrWhiteSpace(formatString))
                return string.Empty;

            System.Diagnostics.Debug.WriteLine($"OutputNode: Formatting string: '{formatString}'");
            System.Diagnostics.Debug.WriteLine($"OutputNode: Available variables: {string.Join(", ", variableValues.Keys)}");

            // Pattern để match {variableKey}, {variableKey[0...n]}, {variableKey[index]}, hoặc {variableKey[fieldName]}
            // Dùng [^{}]+ thay vì [^}]+ để tránh match nhầm khi format string chứa JSON với nested braces
            // (vd: {"json":{"projectTitle":"{input1}","toolName":"PINHOLE"}} - phải match {input1} chứ không phải {"json":{"projectTitle":"{input1})
            var pattern = @"\{([^{}]+)\}";
            var result = Regex.Replace(formatString, pattern, match =>
            {
                var variableKey = match.Groups[1].Value.Trim();
                System.Diagnostics.Debug.WriteLine($"OutputNode: Found placeholder: '{variableKey}'");
                
                // Check if it's bracket format: {variableKey[keyOrIndex]}
                var bracketMatch = Regex.Match(variableKey, @"^(.+?)\[(.+?)\]$");
                if (bracketMatch.Success)
                {
                    var actualKey = bracketMatch.Groups[1].Value.Trim();
                    var keyOrIndex = bracketMatch.Groups[2].Value.Trim();
                    
                    // Check if it's numeric index (array access)
                    if (int.TryParse(keyOrIndex, out var index))
                    {
                        System.Diagnostics.Debug.WriteLine($"OutputNode: Index format detected, key: '{actualKey}', index: {index}");
                        
                        // Try to get from parsed array first
                        if (variableArrays.TryGetValue(actualKey, out var array) && array != null)
                        {
                            if (index >= 0 && index < array.Count)
                            {
                                var item = array[index];
                                System.Diagnostics.Debug.WriteLine($"OutputNode: Replaced '{match.Value}' with '{item}' (from array index {index})");
                                return item;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"OutputNode: Index {index} out of range for array with {array.Count} items");
                                return string.Empty;
                            }
                        }

                        // Fallback: if actualKey is a JSON object (e.g. {"0":"...","1":"..."}),
                        // allow {results[0]} style access by checking variableObjects.
                        if (variableObjects.TryGetValue(actualKey, out var objDict) && objDict != null)
                        {
                            var indexKey = index.ToString();
                            if (objDict.TryGetValue(indexKey, out var objValue))
                            {
                                System.Diagnostics.Debug.WriteLine($"OutputNode: Replaced '{match.Value}' with '{objValue}' (from object key '{indexKey}')");
                                return objValue ?? string.Empty;
                            }

                            // Case-insensitive scan as a last resort
                            var kv = objDict.FirstOrDefault(p => string.Equals(p.Key, indexKey, StringComparison.OrdinalIgnoreCase));
                            if (!string.IsNullOrEmpty(kv.Value))
                            {
                                System.Diagnostics.Debug.WriteLine($"OutputNode: Replaced '{match.Value}' with '{kv.Value}' (from object key '{indexKey}')");
                                return kv.Value;
                            }
                        }
                        
                        // Fallback: try to parse from formatted value
                        if (variableValues.TryGetValue(actualKey, out var formattedValue) && IsArrayValue(formattedValue))
                        {
                            var parsedArray = ParseArrayToList(formattedValue);
                            if (parsedArray != null && index >= 0 && index < parsedArray.Count)
                            {
                                var item = parsedArray[index];
                                System.Diagnostics.Debug.WriteLine($"OutputNode: Replaced '{match.Value}' with '{item}' (parsed from formatted value)");
                                return item;
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"OutputNode: Variable '{actualKey}' not found or not an array");
                        return string.Empty;
                    }
                    // It's a field name (object access) - check for nested array access: {variableKey[fieldName][index]}
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"OutputNode: Object field format detected, key: '{actualKey}', field: '{keyOrIndex}'");
                        
                        // Check if there's nested array access: {variableKey[fieldName][index]}
                        var nestedArrayMatch = Regex.Match(variableKey, @"^(.+?)\[(.+?)\]\[(\d+)\]$");
                        if (nestedArrayMatch.Success)
                        {
                            var rootKey = nestedArrayMatch.Groups[1].Value.Trim();
                            var fieldName = nestedArrayMatch.Groups[2].Value.Trim();
                            var nestedIndexStr = nestedArrayMatch.Groups[3].Value;
                            
                            if (int.TryParse(nestedIndexStr, out var nestedIndex))
                            {
                                System.Diagnostics.Debug.WriteLine($"OutputNode: Nested array access detected, root: '{rootKey}', field: '{fieldName}', index: {nestedIndex}");
                                
                                // Get field value from object
                                string? fieldValueStr = null;
                                
                                if (variableObjects.TryGetValue(rootKey, out var obj) && obj != null)
                                {
                                    var fieldValue = obj.FirstOrDefault(kvp => 
                                        string.Equals(kvp.Key, fieldName, StringComparison.OrdinalIgnoreCase));
                                    if (!string.IsNullOrEmpty(fieldValue.Key))
                                    {
                                        fieldValueStr = fieldValue.Value;
                                    }
                                }
                                
                                // Fallback: parse from formatted value
                                if (fieldValueStr == null && variableValues.TryGetValue(rootKey, out var formattedValue) && IsObjectValue(formattedValue))
                                {
                                    var parsedObject = ParseObjectToDictionary(formattedValue);
                                    if (parsedObject != null)
                                    {
                                        var fieldValue = parsedObject.FirstOrDefault(kvp => 
                                            string.Equals(kvp.Key, fieldName, StringComparison.OrdinalIgnoreCase));
                                        if (!string.IsNullOrEmpty(fieldValue.Key))
                                        {
                                            fieldValueStr = fieldValue.Value;
                                        }
                                    }
                                }
                                
                                // If field value is an array, parse and get index
                                if (!string.IsNullOrWhiteSpace(fieldValueStr) && IsArrayValue(fieldValueStr))
                                {
                                    var nestedArray = ParseArrayToList(fieldValueStr);
                                    if (nestedArray != null && nestedIndex >= 0 && nestedIndex < nestedArray.Count)
                                    {
                                        var item = nestedArray[nestedIndex];
                                        System.Diagnostics.Debug.WriteLine($"OutputNode: Replaced '{match.Value}' with '{item}' (from nested array '{fieldName}[{nestedIndex}]')");
                                        return item;
                                    }
                                }
                                
                                System.Diagnostics.Debug.WriteLine($"OutputNode: Nested array access failed for '{rootKey}[{fieldName}][{nestedIndex}]'");
                                return string.Empty;
                            }
                        }
                        
                        // Regular object field access: {variableKey[fieldName]}
                        // Try to get from parsed object first
                        if (variableObjects.TryGetValue(actualKey, out var obj2) && obj2 != null)
                        {
                            // Case-insensitive lookup
                            var fieldValue = obj2.FirstOrDefault(kvp => 
                                string.Equals(kvp.Key, keyOrIndex, StringComparison.OrdinalIgnoreCase));
                            
                            if (!string.IsNullOrEmpty(fieldValue.Key))
                            {
                                System.Diagnostics.Debug.WriteLine($"OutputNode: Replaced '{match.Value}' with '{fieldValue.Value}' (from object field '{keyOrIndex}')");
                                return fieldValue.Value ?? string.Empty;
                            }
                        }
                        
                        // Fallback: try to parse from formatted value
                        if (variableValues.TryGetValue(actualKey, out var formattedValue2) && IsObjectValue(formattedValue2))
                        {
                            var parsedObject = ParseObjectToDictionary(formattedValue2);
                            if (parsedObject != null)
                            {
                                var fieldValue = parsedObject.FirstOrDefault(kvp => 
                                    string.Equals(kvp.Key, keyOrIndex, StringComparison.OrdinalIgnoreCase));
                                
                                if (!string.IsNullOrEmpty(fieldValue.Key))
                                {
                                    System.Diagnostics.Debug.WriteLine($"OutputNode: Replaced '{match.Value}' with '{fieldValue.Value}' (parsed from formatted value)");
                                    return fieldValue.Value ?? string.Empty;
                                }
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"OutputNode: Variable '{actualKey}' not found or field '{keyOrIndex}' not found in object");
                        return string.Empty;
                    }
                }
                
                // Check if it's array format {variableKey[0...n]}
                var arrayMatch = Regex.Match(variableKey, @"^(.+?)\[0\.\.\.n\]$");
                if (arrayMatch.Success)
                {
                    var actualKey = arrayMatch.Groups[1].Value.Trim();
                    System.Diagnostics.Debug.WriteLine($"OutputNode: Array format detected, actual key: '{actualKey}'");
                    if (variableValues.TryGetValue(actualKey, out var value))
                    {
                        System.Diagnostics.Debug.WriteLine($"OutputNode: Replaced '{match.Value}' with '{value}'");
                        return value; // Value đã được format sẵn trong FormatArrayValue
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"OutputNode: Variable '{actualKey}' not found in dictionary");
                    }
                }
                
                // Regular variable replacement (case-insensitive)
                if (variableValues.TryGetValue(variableKey, out var varValue))
                {
                    System.Diagnostics.Debug.WriteLine($"OutputNode: Replaced '{match.Value}' with '{varValue}'");
                    return varValue;
                }
                
                // Variable not found, return placeholder
                System.Diagnostics.Debug.WriteLine($"OutputNode: Variable '{variableKey}' not found, keeping placeholder");
                return match.Value;
            });

            System.Diagnostics.Debug.WriteLine($"OutputNode: Final formatted string: '{result}'");
            return result;
        }
        
        /// <summary>
        /// Parse JSON array string to List of strings.
        /// </summary>
        private List<string>? ParseArrayToList(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            
            // Try to parse as JSON array
            try
            {
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var items = new List<string>();
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            if (el.ValueKind == JsonValueKind.String)
                            {
                                items.Add(el.GetString() ?? string.Empty);
                            }
                            else
                            {
                                items.Add(el.ToString());
                            }
                        }
                        return items;
                    }
                }
            }
            catch
            {
                // Not a valid JSON array, try simple parsing
            }

            // Fallback: Simple parsing - split by comma
            try
            {
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    var content = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    if (string.IsNullOrWhiteSpace(content))
                        return new List<string>();

                    var items = content.Split(',')
                        .Select(item => item.Trim().Trim('"').Trim('\'')) // Remove quotes if present
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .ToList();

                    return items;
                }
            }
            catch
            {
                // Return null if parsing fails
            }

            return null;
        }
        
        /// <summary>
        /// Parse JSON object string to Dictionary of string keys and string values.
        /// </summary>
        private Dictionary<string, string>? ParseObjectToDictionary(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            
            // Try to parse as JSON object
            try
            {
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            string propValue;
                            if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                propValue = prop.Value.GetString() ?? string.Empty;
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Number)
                            {
                                propValue = prop.Value.GetRawText();
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False)
                            {
                                propValue = prop.Value.GetBoolean().ToString();
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Null)
                            {
                                propValue = string.Empty;
                            }
                            else
                            {
                                propValue = prop.Value.ToString();
                            }
                            dict[prop.Name] = propValue;
                        }
                        return dict;
                    }
                }
            }
            catch
            {
                // Not a valid JSON object
            }

            return null;
        }
        
        /// <summary>
        /// Check if value is JSON object.
        /// </summary>
        private bool IsObjectValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            
            // Try to parse as JSON object
            try
            {
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Not a valid JSON object
            }

            return false;
        }
        
        /// <summary>
        /// Format JSON object value as readable string.
        /// </summary>
        private string FormatObjectValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "{}";

            var trimmed = value.Trim();
            
            // Try to parse and format as JSON object
            try
            {
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        var props = new List<string>();
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            string propValue;
                            if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                propValue = $"\"{prop.Value.GetString()}\"";
                            }
                            else
                            {
                                propValue = prop.Value.ToString();
                            }
                            props.Add($"{prop.Name}: {propValue}");
                        }
                        return $"{{{string.Join(", ", props)}}}";
                    }
                }
            }
            catch
            {
                // Return as-is if parsing fails
            }

            return value ?? "{}";
        }

        /// <summary>
        /// Check if value is JSON array. First try to parse as JSON array, if fails, use simple heuristic.
        /// </summary>
        private bool IsArrayValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            
            // First: Try to parse as JSON array
            try
            {
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Not a valid JSON array, continue to simple heuristic
            }

            // Fallback: Simple heuristic - check if it looks like array representation
            return trimmed.StartsWith("[") && trimmed.Contains(",") && trimmed.EndsWith("]");
        }

        /// <summary>
        /// Format array value as a JSON array string.
        /// - If value is a valid JSON array, keep it (normalized) as JSON (e.g. ["a","b"]).
        /// - Otherwise, try to split and rebuild a JSON array string from items.
        /// </summary>
        private string FormatArrayValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "[]";

            var trimmed = value.Trim();

            // First: Try to parse as JSON array and return its raw JSON text
            try
            {
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        // Keep as proper JSON array string (includes quotes for strings)
                        return doc.RootElement.GetRawText();
                    }
                }
            }
            catch
            {
                // Not a valid JSON array, continue to simple logic
            }

            // Fallback: Simple logic - split by comma and rebuild JSON array
            try
            {
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    var content = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    if (string.IsNullOrWhiteSpace(content))
                        return "[]";

                    var items = content.Split(',')
                        .Select(item => item.Trim().Trim('"').Trim('\'')) // Remove outer quotes if present
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .ToList();

                    if (items.Count == 0)
                        return "[]";

                    // Rebuild as proper JSON array of strings: ["a","b",...]
                    var jsonItems = items
                        .Select(i => JsonSerializer.Serialize(i))
                        .ToArray();

                    return $"[{string.Join(", ", jsonItems)}]";
                }
            }
            catch
            {
                // Fallback: return as-is
            }

            return value ?? "[]";
        }

        // ─── Base64 Image Clipboard Helpers ───

        /// <summary>
        /// Extract base64 image strings from resolved variable values.
        /// Supports same syntax as FormatString:
        ///   - input1                    → plain variable key
        ///   - input1[0]                 → array index
        ///   - input1[name]              → object field
        ///   - input1[0][name]           → nested: array index + field
        ///   - input1[{index}][name]     → dynamic variable as index
        ///   - input1[0...n]             → all array items
        ///   - {input1[name]}            → braces syntax (same as without braces)
        /// </summary>
        private List<string> ExtractBase64Images(
            OutputNode outputNode,
            Dictionary<string, string> variableValues,
            Dictionary<string, List<string>> variableArrays,
            Dictionary<string, Dictionary<string, string>> variableObjects)
        {
            var result = new List<string>();
            var paramKeysRaw = outputNode.ImageParamKeys?.Trim() ?? string.Empty;

            System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: ImageParamKeys='{paramKeysRaw}'");

            if (string.IsNullOrWhiteSpace(paramKeysRaw))
            {
                // No explicit keys → scan ALL variable values for base64
                System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: scanning ALL variables");
                foreach (var kvp in variableArrays)
                {
                    foreach (var item in kvp.Value)
                        CollectBase64FromValue(item, result);
                }
                foreach (var kvp in variableValues)
                {
                    // Skip keys already handled as arrays
                    if (variableArrays.ContainsKey(kvp.Key)) continue;
                    CollectBase64FromValue(kvp.Value, result);
                }
                System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: total extracted={result.Count}");
                return result;
            }

            // Split by , or ;
            var expressions = paramKeysRaw
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => e.Length > 0)
                .ToList();

            foreach (var rawExpr in expressions)
            {
                var expr = rawExpr;
                System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: processing expression='{expr}'");

                // Strip outer braces: {input1[0]} → input1[0]
                if (expr.StartsWith("{") && expr.EndsWith("}") && !expr.Contains(","))
                    expr = expr.Substring(1, expr.Length - 2).Trim();

                // Pre-resolve {var} references within the expression
                // e.g. input1[{index}][name] → input1[2][name] (if index variable = "2")
                expr = Regex.Replace(expr, @"\{([^{}]+)\}", m =>
                {
                    var innerKey = m.Groups[1].Value.Trim();
                    if (variableValues.TryGetValue(innerKey, out var innerVal) && !string.IsNullOrWhiteSpace(innerVal))
                    {
                        System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: resolved {{'{innerKey}'}} → '{innerVal}'");
                        return innerVal;
                    }
                    return m.Value; // keep as-is if not found
                });

                // Check [0...n] pattern → expand ALL array items
                var arrayAllMatch = Regex.Match(expr, @"^(.+?)\[0\.\.\.n\]$");
                if (arrayAllMatch.Success)
                {
                    var baseKey = arrayAllMatch.Groups[1].Value.Trim();
                    System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: [0...n] pattern, baseKey='{baseKey}'");

                    // Try variableArrays first
                    if (variableArrays.TryGetValue(baseKey, out var arr) && arr != null)
                    {
                        foreach (var item in arr)
                            CollectBase64FromValue(item, result);
                        continue;
                    }

                    // Try parsing from variableValues
                    if (variableValues.TryGetValue(baseKey, out var val) && IsArrayValue(val))
                    {
                        var parsed = ParseArrayToList(val);
                        if (parsed != null)
                        {
                            foreach (var item in parsed)
                                CollectBase64FromValue(item, result);
                        }
                        continue;
                    }

                    // Try object field that is an array: e.g. input1[fieldName][0...n]
                    // Already handled by pre-resolving the expression above
                    System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: [0...n] no array found for '{baseKey}'");
                    continue;
                }

                // Check if it's a simple key (no brackets) → direct lookup
                if (!expr.Contains("["))
                {
                    // Simple key: input1
                    if (variableArrays.TryGetValue(expr, out var arr) && arr != null)
                    {
                        foreach (var item in arr)
                            CollectBase64FromValue(item, result);
                    }
                    else if (variableValues.TryGetValue(expr, out var val) && !string.IsNullOrWhiteSpace(val))
                    {
                        CollectBase64FromValue(val, result);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: key='{expr}' not found or empty");
                    }
                    continue;
                }

                // Expression with brackets → use FormatString engine to resolve
                // e.g. input1[0], input1[name], input1[0][name]
                var formatted = FormatString("{" + expr + "}", variableValues, variableArrays, variableObjects);

                // If FormatString returned the placeholder unchanged, it wasn't resolved
                if (formatted == "{" + expr + "}")
                {
                    System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: FormatString could not resolve '{expr}'");
                    continue;
                }

                System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: FormatString resolved '{expr}' → length={formatted.Length}");
                CollectBase64FromValue(formatted, result);
            }

            System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: total extracted={result.Count}");
            return result;
        }

        /// <summary>
        /// Collect base64 images from a resolved value (plain string or JSON array).
        /// </summary>
        private void CollectBase64FromValue(string? value, List<string> result)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var trimmed = value.Trim();

            // If it's a JSON array, parse each item
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                try
                {
                    var parsed = ParseArrayToList(trimmed);
                    if (parsed != null && parsed.Count > 0)
                    {
                        foreach (var item in parsed)
                        {
                            var b64 = CleanBase64(item);
                            if (IsLikelyBase64Image(b64))
                                result.Add(b64);
                        }
                        return;
                    }
                }
                catch { /* not a valid array, try as plain */ }
            }

            // Plain base64 string
            var cleaned = CleanBase64(trimmed);
            if (IsLikelyBase64Image(cleaned))
                result.Add(cleaned);
        }

        /// <summary>
        /// Decode base64 images, save as temp PNG files, and copy to clipboard with multi-format support
        /// (FileDrop, Bitmap, CF_DIB for Chromium/CefSharp, HTML format, and Text).
        /// </summary>
        private void CopyBase64ImagesToClipboard(List<string> base64List, string? textToInclude = null)
        {
            if (base64List.Count == 0 && string.IsNullOrWhiteSpace(textToInclude)) return;

            var tempDir = Path.Combine(Path.GetTempPath(), "FlowMy_ClipboardImages");
            Directory.CreateDirectory(tempDir);

            // Clean old temp files
            try
            {
                foreach (var old in Directory.GetFiles(tempDir, "*.png"))
                {
                    try { File.Delete(old); } catch { }
                }
            }
            catch { }

            var tempFiles = new StringCollection();
            BitmapImage? firstBitmap = null;
            byte[]? firstImageBytes = null;
            string? firstBase64 = null;

            for (int i = 0; i < base64List.Count; i++)
            {
                try
                {
                    var rawBase64 = CleanBase64(base64List[i]);
                    if (string.IsNullOrWhiteSpace(rawBase64)) continue;

                    var bytes = Convert.FromBase64String(rawBase64);
                    var fileName = $"image_{i + 1}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    var filePath = Path.Combine(tempDir, fileName);
                    File.WriteAllBytes(filePath, bytes);
                    tempFiles.Add(filePath);

                    if (firstBitmap == null)
                    {
                        firstBase64 = rawBase64;
                        firstImageBytes = bytes;
                        firstBitmap = new BitmapImage();
                        firstBitmap.BeginInit();
                        firstBitmap.StreamSource = new MemoryStream(bytes);
                        firstBitmap.CacheOption = BitmapCacheOption.OnLoad;
                        firstBitmap.EndInit();
                        firstBitmap.Freeze();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OutputNode: Failed to decode base64 image {i}: {ex.Message}");
                }
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                var dataObject = new DataObject();

                // 1. FileDrop: cho phép Ctrl+V vào folder / desktop / chat app
                if (tempFiles.Count > 0)
                {
                    dataObject.SetFileDropList(tempFiles);
                }

                // 2. Bitmap & CF_DIB & HTML Format: cho phép Ctrl+V vào web CefSharp / Chromium / Web UI
                if (firstBitmap != null && firstImageBytes != null)
                {
                    dataObject.SetImage(firstBitmap);

                    // CF_DIB (Device Independent Bitmap) - lõi Chromium/CefSharp đọc định dạng này cho e.clipboardData.files
                    var dibStream = CreateDibStreamFromPngBytes(firstImageBytes);
                    if (dibStream != null)
                    {
                        dataObject.SetData("DeviceIndependentBitmap", dibStream);
                    }

                    // HTML format - cho phép Chromium web paste nhận diện tag <img src="...">
                    if (!string.IsNullOrWhiteSpace(firstBase64))
                    {
                        try
                        {
                            string htmlData = BuildHtmlClipboardFormat(firstBase64);
                            dataObject.SetData(DataFormats.Html, htmlData);
                        }
                        catch { }
                    }
                }

                // 3. Text format: nếu có text cần lưu cùng lúc
                if (!string.IsNullOrWhiteSpace(textToInclude))
                {
                    dataObject.SetText(textToInclude);
                }

                Clipboard.SetDataObject(dataObject, true);
            });
        }

        private static MemoryStream? CreateDibStreamFromPngBytes(byte[] imageBytes)
        {
            try
            {
                using var ms = new MemoryStream(imageBytes);
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count > 0)
                {
                    var frame = decoder.Frames[0];
                    var encoder = new BmpBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(frame));
                    using var bmpMs = new MemoryStream();
                    encoder.Save(bmpMs);
                    var bmpBytes = bmpMs.ToArray();

                    if (bmpBytes.Length > 14 && bmpBytes[0] == 'B' && bmpBytes[1] == 'M')
                    {
                        byte[] dibBytes = new byte[bmpBytes.Length - 14];
                        Buffer.BlockCopy(bmpBytes, 14, dibBytes, 0, dibBytes.Length);
                        return new MemoryStream(dibBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateDibStreamFromPngBytes error: {ex.Message}");
            }
            return null;
        }

        private static string BuildHtmlClipboardFormat(string base64Data)
        {
            string src = base64Data.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? base64Data
                : $"data:image/png;base64,{base64Data}";

            string fragment = $"<!--StartFragment--><img src=\"{src}\" alt=\"image\"/><!--EndFragment-->";
            string html = $"<!DOCTYPE html><html><body>{fragment}</body></html>";

            string headerTemplate =
                "Version:0.9\r\n" +
                "StartHTML:{0:D10}\r\n" +
                "EndHTML:{1:D10}\r\n" +
                "StartFragment:{2:D10}\r\n" +
                "EndFragment:{3:D10}\r\n";

            string dummyHeader = string.Format(headerTemplate, 0, 0, 0, 0);
            int startHtml = System.Text.Encoding.UTF8.GetByteCount(dummyHeader);
            int startFragment = startHtml + System.Text.Encoding.UTF8.GetByteCount("<!DOCTYPE html><html><body>");
            int endFragment = startFragment + System.Text.Encoding.UTF8.GetByteCount(fragment);
            int endHtml = startHtml + System.Text.Encoding.UTF8.GetByteCount(html);

            string header = string.Format(headerTemplate, startHtml, endHtml, startFragment, endFragment);
            return header + html;
        }

        /// <summary>
        /// Remove data URL prefix (e.g. "data:image/png;base64,") if present.
        /// </summary>
        private static string CleanBase64(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var s = input.Trim();
            // Remove data URL prefix
            var commaIdx = s.IndexOf(',');
            if (commaIdx >= 0 && commaIdx < 100 && s.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(commaIdx + 1);
            }
            return s.Trim();
        }

        /// <summary>
        /// Quick check if a string is likely a base64-encoded image (minimum length + valid chars).
        /// </summary>
        private static bool IsLikelyBase64Image(string? s)
        {
            if (string.IsNullOrWhiteSpace(s) || s.Length < 100) return false;
            // Must be reasonable length and contain only base64 chars
            foreach (var c in s)
            {
                if (!char.IsLetterOrDigit(c) && c != '+' && c != '/' && c != '=' && !char.IsWhiteSpace(c))
                    return false;
            }
            // Try decode a small portion to verify
            try
            {
                Convert.FromBase64String(s.Length > 256 ? s.Substring(0, 256).PadRight(256 + (4 - 256 % 4) % 4, '=') : s);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

