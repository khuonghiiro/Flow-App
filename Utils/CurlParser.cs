using FlowMy.Models.Nodes;
using NodeHttpMethod = FlowMy.Models.Nodes.HttpMethod;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace FlowMy.Utils
{
    /// <summary>
    /// Result of parsing a cURL command.
    /// </summary>
    public class CurlParseResult
    {
        public bool IsValid { get; set; }
        public string Url { get; set; } = string.Empty;
        public NodeHttpMethod Method { get; set; } = NodeHttpMethod.GET;
        public List<HttpKeyValuePair> Headers { get; } = new();
        public List<HttpKeyValuePair> QueryParams { get; } = new();
        public HttpAuthType AuthType { get; set; } = HttpAuthType.None;
        public string AuthUsername { get; set; } = string.Empty;
        public string AuthPassword { get; set; } = string.Empty;
        public string AuthToken { get; set; } = string.Empty;
        public HttpBodyType BodyType { get; set; } = HttpBodyType.None;
        public string RawBody { get; set; } = string.Empty;
        public List<HttpKeyValuePair> FormData { get; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Parser for cURL commands (like "Copy as cURL" from browser DevTools or Postman).
    /// Supports both cmd and bash formats.
    /// </summary>
    public static class CurlParser
    {
        /// <summary>
        /// Check if the input text looks like a cURL command.
        /// </summary>
        public static bool IsCurlCommand(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var trimmed = text.Trim();

            return trimmed.StartsWith("curl ", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("curl.exe ", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("curl\t", StringComparison.OrdinalIgnoreCase) ||
                   Regex.IsMatch(trimmed, @"^curl(\s|\.exe\s|\^)", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Convert Windows CMD cURL format to bash format for easier parsing.
        /// </summary>
        private static string ConvertCmdToBashFormat(string cmdCurl)
        {
            var bash = cmdCurl;

            bash = Regex.Replace(bash, @"\^\s*\r?\n\s*", " \\\n", RegexOptions.Multiline);
            bash = Regex.Replace(bash, @"\^\^", "\x00CARET\x00");
            bash = bash.Replace("^\"", "\"");
            bash = bash.Replace("\x00CARET\x00", "^");

            for (int i = 0; i < 5; i++)
            {
                bash = Regex.Replace(bash, @"\^%\^([0-9A-Fa-f]{2})", "%$1");
                bash = Regex.Replace(bash, @"%\^([0-9A-Fa-f]{2})", "%$1");
                bash = Regex.Replace(bash, @"\^%([0-9A-Fa-f]{2})", "%$1");
            }

            bash = Regex.Replace(bash, @"\^([&|<>()@^""'%])", "$1");
            bash = Regex.Replace(bash, @"\^(?=[a-zA-Z0-9;,=\$\.\-_])", "");

            // Windows CMD batch: %% → % (double-percent is CMD escape for literal %)
            bash = bash.Replace("%%", "%");

            return bash;
        }

        public static CurlParseResult Parse(string curlCommand)
        {
            var result = new CurlParseResult();

            if (string.IsNullOrWhiteSpace(curlCommand))
            {
                result.ErrorMessage = "Empty cURL command";
                return result;
            }

            try
            {
                bool isCmdFormat = curlCommand.Contains("^\"") ||
                                   Regex.IsMatch(curlCommand, @"\^%\^[0-9A-Fa-f]{2}") ||
                                   Regex.IsMatch(curlCommand, @"\^\s*\r?\n") ||
                                   curlCommand.Contains("%%");  // CMD batch double-percent

                if (isCmdFormat)
                {
                    curlCommand = ConvertCmdToBashFormat(curlCommand);
                }

                var normalized = NormalizeCommand(curlCommand);
                var tokens = Tokenize(normalized);

                if (tokens.Count == 0 || !tokens[0].Equals("curl", StringComparison.OrdinalIgnoreCase))
                {
                    result.ErrorMessage = "Not a valid cURL command";
                    return result;
                }

                for (int i = 1; i < tokens.Count; i++)
                {
                    var token = tokens[i];

                    if (token == "-X" || token == "--request")
                    {
                        if (i + 1 < tokens.Count)
                        {
                            i++;
                            result.Method = ParseHttpMethod(tokens[i]);
                        }
                    }
                    else if (token == "-H" || token == "--header")
                    {
                        if (i + 1 < tokens.Count)
                        {
                            i++;
                            ParseHeader(tokens[i], result);
                        }
                    }
                    else if (token == "-d" || token == "--data" || token == "--data-raw" ||
                             token == "--data-binary" || token == "--data-urlencode")
                    {
                        if (i + 1 < tokens.Count)
                        {
                            i++;
                            ParseData(tokens[i], token, result);
                        }
                    }
                    else if (token == "-F" || token == "--form")
                    {
                        if (i + 1 < tokens.Count)
                        {
                            i++;
                            ParseFormField(tokens[i], result);
                            result.BodyType = HttpBodyType.FormData;
                        }
                    }
                    else if (token == "-u" || token == "--user")
                    {
                        if (i + 1 < tokens.Count)
                        {
                            i++;
                            ParseBasicAuth(tokens[i], result);
                        }
                    }
                    else if (!token.StartsWith("-"))
                    {
                        var cleanedToken = token.Trim('\'', '"');
                        if (cleanedToken.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            cleanedToken.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            ParseUrl(cleanedToken, result);
                        }
                        else if (string.IsNullOrEmpty(result.Url) &&
                                 (cleanedToken.Contains(".") || cleanedToken.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)))
                        {
                            ParseUrl("https://" + cleanedToken, result);
                        }
                    }
                    else if (token == "-b" || token == "--cookie")
                    {
                        if (i + 1 < tokens.Count)
                        {
                            i++;
                            var cookieValue = tokens[i];

                            // ===== CRITICAL FIX: Proper Cookie processing =====

                            // Step 1: Clean CMD escape sequences
                            for (int pass = 0; pass < 3; pass++)
                            {
                                cookieValue = Regex.Replace(cookieValue, @"\^%\^([0-9A-Fa-f]{2})", "%$1");
                                cookieValue = Regex.Replace(cookieValue, @"%\^([0-9A-Fa-f]{2})", "%$1");
                            }
                            cookieValue = Regex.Replace(cookieValue, @"\^([^\s%])", "$1");
                            cookieValue = cookieValue.Trim('"', '\'');

                            // CRITICAL FIX: DO NOT decode URL-encoded cookie values!
                            // Cookie values are sent AS-IS (URL-encoded) in HTTP headers
                            // Only CMD escapes need to be cleaned (done above)
                            // 
                            // Postman exports cookies as: %22%257B%2522isOpen...
                            // Chrome browser sends: %22%257B%2522isOpen...
                            // Our code should match Postman/Browser behavior!

                            var existingCookieHeader = result.Headers.FirstOrDefault(h =>
                                h.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase));

                            if (existingCookieHeader != null)
                            {
                                if (!string.IsNullOrWhiteSpace(cookieValue))
                                {
                                    existingCookieHeader.Value = existingCookieHeader.Value.TrimEnd(';') +
                                        (string.IsNullOrWhiteSpace(existingCookieHeader.Value) ? "" : "; ") +
                                        cookieValue;
                                }
                            }
                            else
                            {
                                result.Headers.Add(new HttpKeyValuePair
                                {
                                    Key = "Cookie",
                                    Value = cookieValue ?? string.Empty,
                                    IsEnabled = true
                                });
                            }
                        }
                    }
                    else if (token == "-c" || token == "--cookie-jar" ||
                             token == "-o" || token == "--output")
                    {
                        if (i + 1 < tokens.Count)
                        {
                            i++;
                        }
                    }
                    else if (token == "--compressed" || token == "-k" || token == "--insecure" ||
                             token == "-L" || token == "--location" || token == "-s" || token == "--silent" ||
                             token == "-v" || token == "--verbose" || token == "-i" || token == "--include" ||
                             token == "-I" || token == "--head" || token == "-G" || token == "--get" ||
                             token == "-f" || token == "--fail" || token == "-S" || token == "--show-error" ||
                             token == "-#" || token == "--progress-bar" || token == "-N" || token == "--no-buffer")
                    {
                        continue;
                    }
                    else if (token.StartsWith("-") && i + 1 < tokens.Count && !tokens[i + 1].StartsWith("-"))
                    {
                        i++;
                    }
                }

                if (result.Method == NodeHttpMethod.GET && !string.IsNullOrEmpty(result.RawBody))
                {
                    result.Method = NodeHttpMethod.POST;
                }

                if (!string.IsNullOrEmpty(result.RawBody))
                {
                    var trimmedBody = result.RawBody.Trim();
                    if ((trimmedBody.StartsWith("{") && trimmedBody.EndsWith("}")) ||
                        (trimmedBody.StartsWith("[") && trimmedBody.EndsWith("]")))
                    {
                        result.BodyType = HttpBodyType.Json;
                    }
                    else if (result.BodyType == HttpBodyType.None)
                    {
                        result.BodyType = HttpBodyType.Raw;
                    }
                }

                result.IsValid = !string.IsNullOrEmpty(result.Url);
                if (!result.IsValid)
                {
                    result.ErrorMessage = $"No URL found in cURL command. Tokens found: {tokens.Count}";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Parse error: {ex.Message}";
            }

            return result;
        }

        public static string GetParseDebugInfo(string curlCommand)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== cURL Parse Debug Info ===");
            sb.AppendLine($"Original length: {curlCommand.Length}");

            var normalized = NormalizeCommand(curlCommand);
            sb.AppendLine($"Normalized length: {normalized.Length}");
            sb.AppendLine($"Normalized (first 500 chars): {(normalized.Length > 500 ? normalized.Substring(0, 500) + "..." : normalized)}");

            var tokens = Tokenize(normalized);
            sb.AppendLine($"Token count: {tokens.Count}");
            for (int i = 0; i < Math.Min(20, tokens.Count); i++)
            {
                var token = tokens[i];
                sb.AppendLine($"  [{i}]: {(token.Length > 100 ? token.Substring(0, 100) + "..." : token)}");
            }
            if (tokens.Count > 20)
            {
                sb.AppendLine($"  ... and {tokens.Count - 20} more tokens");
            }

            return sb.ToString();
        }

        private static string NormalizeCommand(string command)
        {
            var normalized = command;

            normalized = Regex.Replace(normalized, @"^curl\.exe\s+", "curl ", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"\^\s*\r?\n\s*", " ");
            normalized = Regex.Replace(normalized, @"\\\s*\r?\n\s*", " ");
            normalized = normalized.Replace("\r\n", " ").Replace("\n", " ");

            normalized = Regex.Replace(normalized, @"\^\^", "\x00CARET\x00");
            normalized = normalized.Replace("^\"", "\"");
            normalized = normalized.Replace("\x00CARET\x00", "^");

            for (int pass = 0; pass < 5; pass++)
            {
                string prev = normalized;
                normalized = Regex.Replace(normalized, @"\^%\^([0-9A-Fa-f]{2})", "%$1");
                normalized = Regex.Replace(normalized, @"%\^([0-9A-Fa-f]{2})", "%$1");
                normalized = Regex.Replace(normalized, @"\^%([0-9A-Fa-f]{2})", "%$1");
                if (normalized == prev) break;
            }

            normalized = Regex.Replace(normalized, @"\^%(?!\^?[0-9A-Fa-f]{2})", "%");
            normalized = normalized.Replace("\\^\"", "\\\"");
            normalized = Regex.Replace(normalized, @"\\\^([^\s])", "\\$1");
            normalized = Regex.Replace(normalized, @"\^([&|<>()@^])", "$1");
            normalized = Regex.Replace(normalized, @"\^(?=[a-zA-Z0-9;,=\$\.\-_])", "");
            normalized = Regex.Replace(normalized, @"(%[0-9A-Fa-f]?)\^([0-9A-Fa-f])", "$1$2");
            normalized = Regex.Replace(normalized, @"\^([^\s\^""'%])", "$1");
            normalized = Regex.Replace(normalized, @"\s+", " ");

            return normalized.Trim();
        }

        private static List<string> Tokenize(string command)
        {
            var tokens = new List<string>();
            var sb = new StringBuilder();
            char? quoteChar = null;
            bool escaped = false;

            for (int i = 0; i < command.Length; i++)
            {
                char c = command[i];

                if (escaped)
                {
                    switch (c)
                    {
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case '"':
                        case '\'':
                        case '\\':
                            sb.Append(c);
                            break;
                        default:
                            sb.Append('\\');
                            sb.Append(c);
                            break;
                    }
                    escaped = false;
                    continue;
                }

                if (c == '\\' && quoteChar != '\'')
                {
                    if (i + 1 < command.Length)
                    {
                        char next = command[i + 1];
                        if (next == '"' || next == '\'' || next == '\\' || next == 'n' || next == 'r' || next == 't')
                        {
                            escaped = true;
                            continue;
                        }
                    }
                    sb.Append(c);
                    continue;
                }

                if (quoteChar.HasValue)
                {
                    if (c == quoteChar.Value)
                    {
                        quoteChar = null;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else if (c == '"' || c == '\'')
                {
                    quoteChar = c;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (sb.Length > 0)
            {
                tokens.Add(sb.ToString());
            }

            return tokens;
        }

        private static NodeHttpMethod ParseHttpMethod(string method)
        {
            return method.ToUpperInvariant() switch
            {
                "GET" => NodeHttpMethod.GET,
                "POST" => NodeHttpMethod.POST,
                "PUT" => NodeHttpMethod.PUT,
                "DELETE" => NodeHttpMethod.DELETE,
                "PATCH" => NodeHttpMethod.PATCH,
                "HEAD" => NodeHttpMethod.HEAD,
                "OPTIONS" => NodeHttpMethod.OPTIONS,
                _ => NodeHttpMethod.GET
            };
        }

        private static void ParseHeader(string headerValue, CurlParseResult result)
        {
            // Clean CMD escapes in header value
            for (int pass = 0; pass < 3; pass++)
            {
                headerValue = Regex.Replace(headerValue, @"\^%\^([0-9A-Fa-f]{2})", "%$1");
                headerValue = Regex.Replace(headerValue, @"%\^([0-9A-Fa-f]{2})", "%$1");
            }
            headerValue = Regex.Replace(headerValue, @"\^\^", "^");
            headerValue = Regex.Replace(headerValue, @"\^([^\s%])", "$1");

            var colonIndex = headerValue.IndexOf(':');
            if (colonIndex <= 0) return;

            var key = headerValue.Substring(0, colonIndex).Trim();
            var value = headerValue.Substring(colonIndex + 1).Trim();
            value = value.Trim('"', '\'');

            // Authorization header handling
            if (key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    result.AuthType = HttpAuthType.Bearer;
                    result.AuthToken = value.Substring(7).Trim();
                    return;
                }
                else if (value.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var base64 = value.Substring(6).Trim();
                        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                        var parts = decoded.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            result.AuthType = HttpAuthType.Basic;
                            result.AuthUsername = parts[0];
                            result.AuthPassword = parts[1];
                            return;
                        }
                    }
                    catch { }
                }
            }

            // Content-Type detection
            if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Contains("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    result.BodyType = HttpBodyType.Json;
                }
                else if (value.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                {
                    result.BodyType = HttpBodyType.FormUrlEncoded;
                }
                else if (value.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                {
                    result.BodyType = HttpBodyType.FormData;
                }
            }

            result.Headers.Add(new HttpKeyValuePair { Key = key ?? string.Empty, Value = value ?? string.Empty, IsEnabled = true });
        }

        private static void ParseData(string data, string flag, CurlParseResult result)
        {
            if (flag == "--data-urlencode")
            {
                var eqIndex = data.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = data.Substring(0, eqIndex);
                    var value = HttpUtility.UrlDecode(data.Substring(eqIndex + 1));
                    if (result.BodyType != HttpBodyType.FormData)
                    {
                        result.BodyType = HttpBodyType.FormUrlEncoded;
                    }
                    result.FormData.Add(new HttpKeyValuePair { Key = key ?? string.Empty, Value = value ?? string.Empty, IsEnabled = true });
                }
            }
            else
            {
                if (string.IsNullOrEmpty(result.RawBody))
                {
                    result.RawBody = data;
                }
                else
                {
                    result.RawBody += "&" + data;
                }

                if (result.BodyType == HttpBodyType.None || result.BodyType == HttpBodyType.FormUrlEncoded)
                {
                    if (Regex.IsMatch(data, @"^[^=&]+=[^&]*(&[^=&]+=[^&]*)*$"))
                    {
                        result.BodyType = HttpBodyType.FormUrlEncoded;
                        var pairs = data.Split('&');
                        foreach (var pair in pairs)
                        {
                            var eqIndex = pair.IndexOf('=');
                            if (eqIndex > 0)
                            {
                                var key = HttpUtility.UrlDecode(pair.Substring(0, eqIndex));
                                var value = HttpUtility.UrlDecode(pair.Substring(eqIndex + 1));
                                result.FormData.Add(new HttpKeyValuePair { Key = key ?? string.Empty, Value = value ?? string.Empty, IsEnabled = true });
                            }
                        }
                    }
                }
            }
        }

        private static void ParseFormField(string formField, CurlParseResult result)
        {
            var eqIndex = formField.IndexOf('=');
            if (eqIndex <= 0) return;

            var key = formField.Substring(0, eqIndex);
            var value = formField.Substring(eqIndex + 1);

            if (value.StartsWith("@"))
            {
                value = "[FILE] " + value.Substring(1);
            }

            result.FormData.Add(new HttpKeyValuePair { Key = key, Value = value, IsEnabled = true });
        }

        private static void ParseBasicAuth(string auth, CurlParseResult result)
        {
            var parts = auth.Split(':', 2);
            result.AuthType = HttpAuthType.Basic;
            result.AuthUsername = parts[0];
            result.AuthPassword = parts.Length > 1 ? parts[1] : string.Empty;
        }

        private static void ParseUrl(string url, CurlParseResult result)
        {
            string prevUrl;
            int maxIterations = 5;
            int iteration = 0;
            do
            {
                prevUrl = url;
                iteration++;

                url = Regex.Replace(url, @"\^%\^([0-9A-Fa-f]{2})", "%$1");
                url = Regex.Replace(url, @"%\^([0-9A-Fa-f]{2})", "%$1");
                url = Regex.Replace(url, @"(%[0-9A-Fa-f]?)\^([0-9A-Fa-f])", "$1$2");
                url = Regex.Replace(url, @"\^(?=[0-9A-Fa-f%&=?])", "");

            } while (url != prevUrl && iteration < maxIterations);

            // Giữ nguyên URL đầy đủ (kể cả query) cho HttpClient / tải file — nhiều URL ký hạn / CDN cần query.
            // Vẫn tách query vào QueryParams để UI HttpRequestNode chỉnh sửa/ghép lại như cũ.
            result.Url = url;

            var questionIndex = url.IndexOf('?');
            if (questionIndex > 0)
            {
                var queryString = url.Substring(questionIndex + 1);

                var pairs = queryString.Split('&');
                foreach (var pair in pairs)
                {
                    var eqIndex = pair.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        var key = HttpUtility.UrlDecode(pair.Substring(0, eqIndex));
                        var value = HttpUtility.UrlDecode(pair.Substring(eqIndex + 1));
                        result.QueryParams.Add(new HttpKeyValuePair { Key = key ?? string.Empty, Value = value ?? string.Empty, IsEnabled = true });
                    }
                    else if (!string.IsNullOrEmpty(pair))
                    {
                        result.QueryParams.Add(new HttpKeyValuePair { Key = HttpUtility.UrlDecode(pair), Value = "", IsEnabled = true });
                    }
                }
            }
        }
    }
}