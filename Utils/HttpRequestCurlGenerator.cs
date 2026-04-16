using FlowMy.Models;
using FlowMy.Models.Nodes;
using NodeHttpMethod = FlowMy.Models.Nodes.HttpMethod;
using FlowMy.Services.Rendering;
using System.Text;

namespace FlowMy.Utils
{
    /// <summary>
    /// Helper class to generate cURL command from HttpRequestNode configuration.
    /// Supports dynamic binding resolution from connections.
    /// </summary>
    public static class HttpRequestCurlGenerator
    {
        /// <summary>
        /// Generate cURL command from HttpRequestNode with resolved dynamic values.
        /// </summary>
        /// <param name="node">The HttpRequestNode to generate cURL from</param>
        /// <param name="connections">List of workflow connections for resolving dynamic values</param>
        /// <returns>cURL command string</returns>
        public static string GenerateCurlCommand(HttpRequestNode node, List<WorkflowConnection>? connections = null)
        {
            var parts = new List<string> { "curl" };

            // URL - handle dynamic binding
            string url = ResolveStringValue(
                node.Url,
                node.UrlSourceNodeId,
                node.UrlSourceOutputKey,
                connections,
                node);

            // Build query parameters
            var queryParams = new List<string>();
            foreach (var param in node.QueryParams.Where(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.Key)))
            {
                string value = ResolveKeyValuePairValue(param, connections, node);
                queryParams.Add($"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(value)}");
            }

            // Add API Key as query param if configured
            if (node.AuthType == HttpAuthType.ApiKey && !node.ApiKeyInHeader && !string.IsNullOrWhiteSpace(node.ApiKeyName))
            {
                string apiKeyValue = ResolveStringValue(
                    node.ApiKeyValue ?? string.Empty,
                    node.ApiKeyValueSourceNodeId,
                    node.ApiKeyValueSourceOutputKey,
                    connections,
                    node);
                queryParams.Add($"{Uri.EscapeDataString(node.ApiKeyName)}={Uri.EscapeDataString(apiKeyValue)}");
            }

            // Append query params to URL
            if (queryParams.Count > 0)
            {
                var separator = url.Contains('?') ? "&" : "?";
                url = $"{url}{separator}{string.Join("&", queryParams)}";
            }

            // Add URL (escape if needed)
            parts.Add($"\"{url}\"");

            // Method (if not GET)
            if (node.HttpMethod != HttpMethod.GET)
            {
                parts.Add($"-X {node.HttpMethod}");
            }

            // Headers
            foreach (var header in node.Headers.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key)))
            {
                string value = ResolveKeyValuePairValue(header, connections, node);
                // Escape quotes in header value
                value = value.Replace("\"", "\\\"");
                parts.Add($"-H \"{header.Key}: {value}\"");
            }

            // Authentication
            switch (node.AuthType)
            {
                case HttpAuthType.Basic:
                    if (!string.IsNullOrWhiteSpace(node.AuthUsername))
                    {
                        var credentials = Convert.ToBase64String(
                            Encoding.UTF8.GetBytes($"{node.AuthUsername}:{node.AuthPassword ?? string.Empty}"));
                        parts.Add($"-H \"Authorization: Basic {credentials}\"");
                    }
                    break;

                case HttpAuthType.Bearer:
                    string token = ResolveStringValue(
                        node.AuthToken ?? string.Empty,
                        node.TokenSourceNodeId,
                        node.TokenSourceOutputKey,
                        connections,
                        node);
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        token = token.Replace("\"", "\\\"");
                        parts.Add($"-H \"Authorization: Bearer {token}\"");
                    }
                    break;

                case HttpAuthType.ApiKey:
                    if (node.ApiKeyInHeader && !string.IsNullOrWhiteSpace(node.ApiKeyName))
                    {
                        string apiKeyValue = ResolveStringValue(
                            node.ApiKeyValue ?? string.Empty,
                            node.ApiKeyValueSourceNodeId,
                            node.ApiKeyValueSourceOutputKey,
                            connections,
                            node);
                        apiKeyValue = apiKeyValue.Replace("\"", "\\\"");
                        parts.Add($"-H \"{node.ApiKeyName}: {apiKeyValue}\"");
                    }
                    break;
            }

            // Body
            if (node.HttpMethod != NodeHttpMethod.GET && node.HttpMethod != NodeHttpMethod.HEAD)
            {
                switch (node.BodyType)
                {
                    case HttpBodyType.Raw:
                        if (!string.IsNullOrWhiteSpace(node.RawBody))
                        {
                            string body = ResolveStringValue(
                                node.RawBody,
                                node.BodySourceNodeId,
                                node.BodySourceOutputKey,
                                connections,
                                node);
                            // Escape quotes and newlines
                            body = body.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
                            parts.Add($"-d \"{body}\"");
                        }
                        break;

                    case HttpBodyType.Json:
                        if (!string.IsNullOrWhiteSpace(node.RawBody))
                        {
                            string body = ResolveStringValue(
                                node.RawBody,
                                node.BodySourceNodeId,
                                node.BodySourceOutputKey,
                                connections,
                                node);
                            // Escape quotes and newlines
                            body = body.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
                            parts.Add($"-H \"Content-Type: application/json\"");
                            parts.Add($"-d \"{body}\"");
                        }
                        break;

                    case HttpBodyType.FormUrlEncoded:
                        var formDataPairs = new List<string>();
                        foreach (var item in node.FormData.Where(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.Key)))
                        {
                            string value = ResolveKeyValuePairValue(item, connections, node);
                            formDataPairs.Add($"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(value)}");
                        }
                        if (formDataPairs.Count > 0)
                        {
                            parts.Add($"-H \"Content-Type: application/x-www-form-urlencoded\"");
                            parts.Add($"-d \"{string.Join("&", formDataPairs)}\"");
                        }
                        break;

                    case HttpBodyType.FormData:
                        // For multipart/form-data, we'll use --form for each field
                        foreach (var item in node.FormData.Where(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.Key)))
                        {
                            string value = ResolveKeyValuePairValue(item, connections, node);
                            value = value.Replace("\"", "\\\"");
                            parts.Add($"-F \"{item.Key}={value}\"");
                        }
                        break;
                }
            }

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Resolve a string value that may have dynamic binding from another node.
        /// </summary>
        private static string ResolveStringValue(
            string staticValue,
            string? sourceNodeId,
            string? sourceOutputKey,
            List<WorkflowConnection>? connections,
            HttpRequestNode currentNode)
        {
            // If no dynamic binding, return static value
            if (string.IsNullOrWhiteSpace(sourceNodeId) || string.IsNullOrWhiteSpace(sourceOutputKey))
            {
                return staticValue ?? string.Empty;
            }

            // Find source node
            var sourceNode = FindSourceNode(sourceNodeId, connections, currentNode);
            if (sourceNode == null)
            {
                return staticValue ?? string.Empty;
            }

            // Resolve value from source node
            var value = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, sourceOutputKey);
            if (value == "—" || string.IsNullOrWhiteSpace(value))
            {
                return staticValue ?? string.Empty;
            }

            return value;
        }

        /// <summary>
        /// Resolve the value of a key-value pair that may have dynamic binding.
        /// </summary>
        private static string ResolveKeyValuePairValue(
            HttpKeyValuePair kvp,
            List<WorkflowConnection>? connections,
            HttpRequestNode currentNode)
        {
            // If no dynamic binding, return static value
            if (string.IsNullOrWhiteSpace(kvp.SourceNodeId) || string.IsNullOrWhiteSpace(kvp.SourceOutputKey))
            {
                return kvp.Value ?? string.Empty;
            }

            // Find source node
            var sourceNode = FindSourceNode(kvp.SourceNodeId, connections, currentNode);
            if (sourceNode == null)
            {
                return kvp.Value ?? string.Empty;
            }

            // Resolve value from source node
            var value = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, kvp.SourceOutputKey);
            if (value == "—" || string.IsNullOrWhiteSpace(value))
            {
                return kvp.Value ?? string.Empty;
            }

            return value;
        }

        /// <summary>
        /// Find a source node by ID from connections or graph.
        /// </summary>
        private static WorkflowNode? FindSourceNode(
            string sourceNodeId,
            List<WorkflowConnection>? connections,
            HttpRequestNode currentNode)
        {
            if (connections == null || connections.Count == 0)
                return null;

            // Try direct connection first
            var upstreamConnection = connections
                .FirstOrDefault(c =>
                    c.ToNode == currentNode &&
                    c.FromNode != null &&
                    c.FromNode.Id == sourceNodeId);

            if (upstreamConnection?.FromNode != null)
            {
                return upstreamConnection.FromNode;
            }

            // Fallback: find node by ID in graph (for LoopBody scenarios)
            return connections
                .SelectMany(c => new[] { c.FromNode, c.ToNode })
                .FirstOrDefault(n => n != null && n.Id == sourceNodeId);
        }
    }
}

