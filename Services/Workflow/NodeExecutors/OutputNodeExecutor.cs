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

                // Copy to clipboard if SaveToClipboard is enabled
                if (outputNode.SaveToClipboard && !string.IsNullOrWhiteSpace(outputText))
                {
                    try
                    {
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            Clipboard.SetText(outputText);
                        });
                        System.Diagnostics.Debug.WriteLine($"OutputNode: Copied to clipboard");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"OutputNode: Failed to copy to clipboard: {ex.Message}");
                    }
                }
                // Copy images to clipboard if CopyImagesToClipboard is enabled
                if (outputNode.CopyImagesToClipboard)
                {
                    try
                    {
                        var base64List = ExtractBase64Images(outputNode, variableValues, variableArrays);
                        if (base64List.Count > 0)
                        {
                            CopyBase64ImagesToClipboard(base64List);
                            System.Diagnostics.Debug.WriteLine($"OutputNode: Copied {base64List.Count} image(s) to clipboard");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"OutputNode: CopyImagesToClipboard enabled but no base64 images found");
                        }
                    }
                    catch (Exception imgEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"OutputNode: Failed to copy images to clipboard: {imgEx.Message}");
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
        /// If ImageParamKeys is specified, only look at those keys; otherwise use all variables.
        /// Supports: plain base64 string, JSON array of base64 strings, JSON object with specified field keys.
        /// </summary>
        private List<string> ExtractBase64Images(
            OutputNode outputNode,
            Dictionary<string, string> variableValues,
            Dictionary<string, List<string>> variableArrays)
        {
            var result = new List<string>();
            var paramKeysRaw = outputNode.ImageParamKeys?.Trim() ?? string.Empty;

            System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: ImageParamKeys='{paramKeysRaw}'");
            System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: variableValues keys=[{string.Join(", ", variableValues.Keys)}]");
            System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: variableArrays keys=[{string.Join(", ", variableArrays.Keys)}]");

            // Determine which variable keys to scan
            IEnumerable<string> keysToScan;
            if (!string.IsNullOrWhiteSpace(paramKeysRaw))
            {
                // User specified explicit keys, split by , or ;
                keysToScan = paramKeysRaw
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .Where(k => k.Length > 0)
                    .ToList();
                System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: explicit keysToScan=[{string.Join(", ", keysToScan)}]");
            }
            else
            {
                // Use all variable keys
                keysToScan = variableValues.Keys.ToList();
                System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: using ALL variable keys");
            }

            foreach (var key in keysToScan)
            {
                System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: scanning key='{key}'");

                // Check array values first
                if (variableArrays.TryGetValue(key, out var arr) && arr != null)
                {
                    System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: key='{key}' found in variableArrays ({arr.Count} items)");
                    foreach (var item in arr)
                    {
                        var b64 = CleanBase64(item);
                        if (IsLikelyBase64Image(b64))
                        {
                            result.Add(b64);
                            System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: added base64 from array item ({b64.Length} chars)");
                        }
                    }
                    continue;
                }

                // Check plain string value
                if (variableValues.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
                {
                    var trimmed = val.Trim();
                    System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: key='{key}' value length={trimmed.Length}, starts='{(trimmed.Length > 30 ? trimmed.Substring(0, 30) : trimmed)}...'");

                    // If it's a JSON array string, parse it
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        try
                        {
                            var parsed = ParseArrayToList(trimmed);
                            if (parsed != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: parsed JSON array with {parsed.Count} items");
                                foreach (var item in parsed)
                                {
                                    var b64 = CleanBase64(item);
                                    if (IsLikelyBase64Image(b64))
                                    {
                                        result.Add(b64);
                                        System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: added base64 from JSON array ({b64.Length} chars)");
                                    }
                                }
                                continue;
                            }
                        }
                        catch { /* not a valid JSON array, try as plain base64 */ }
                    }

                    // If it's a JSON object, try extracting fields matching paramKeys
                    if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(trimmed);
                            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                            {
                                // If paramKeysRaw has specific sub-keys, try them as JSON field names
                                var subKeys = paramKeysRaw
                                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(k => k.Trim())
                                    .Where(k => k.Length > 0);

                                foreach (var subKey in subKeys)
                                {
                                    if (doc.RootElement.TryGetProperty(subKey, out var prop))
                                    {
                                        var propVal = prop.ValueKind == JsonValueKind.String
                                            ? prop.GetString() ?? string.Empty
                                            : prop.GetRawText();
                                        var b64 = CleanBase64(propVal);
                                        if (IsLikelyBase64Image(b64))
                                        {
                                            result.Add(b64);
                                            System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: added base64 from JSON object field '{subKey}' ({b64.Length} chars)");
                                        }
                                    }
                                }
                                continue;
                            }
                        }
                        catch { /* not valid JSON object */ }
                    }

                    // Plain base64 string
                    var cleaned = CleanBase64(trimmed);
                    if (IsLikelyBase64Image(cleaned))
                    {
                        result.Add(cleaned);
                        System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: added plain base64 ({cleaned.Length} chars)");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: key='{key}' value is NOT a valid base64 image");
                    }
                }
                else if (variableValues.TryGetValue(key, out var emptyVal))
                {
                    System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: key='{key}' found but value is EMPTY (source node may not have run yet)");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: key='{key}' NOT found in variableValues (check Variable Key name)");
                }
            }

            System.Diagnostics.Debug.WriteLine($"OutputNode.ExtractBase64Images: total extracted={result.Count}");
            return result;
        }

        /// <summary>
        /// Decode base64 images, save as temp PNG files, and copy to clipboard.
        /// Supports both folder paste (FileDrop) and web paste (BitmapImage).
        /// </summary>
        private void CopyBase64ImagesToClipboard(List<string> base64List)
        {
            if (base64List.Count == 0) return;

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

            for (int i = 0; i < base64List.Count; i++)
            {
                try
                {
                    var bytes = Convert.FromBase64String(base64List[i]);
                    var fileName = $"image_{i + 1}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    var filePath = Path.Combine(tempDir, fileName);
                    File.WriteAllBytes(filePath, bytes);
                    tempFiles.Add(filePath);

                    // Keep first image as BitmapImage for web paste support
                    if (firstBitmap == null)
                    {
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

            if (tempFiles.Count == 0) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                var dataObject = new DataObject();

                // FileDrop: cho phép Ctrl+V vào folder
                dataObject.SetFileDropList(tempFiles);

                // Bitmap: cho phép Ctrl+V vào web/app hỗ trợ paste ảnh
                if (firstBitmap != null)
                {
                    dataObject.SetImage(firstBitmap);
                }

                Clipboard.SetDataObject(dataObject, true);
            });
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

