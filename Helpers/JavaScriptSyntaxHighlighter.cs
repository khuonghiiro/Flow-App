using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace FlowMy.Helpers
{
    public enum JsTokenType
    {
        Plain,
        CommentLine,
        CommentBlock,
        Keyword,
        BuiltIn,
        FunctionName,
        StringDouble,
        StringSingle,
        Number,
        Regex
    }

    /// <summary>Tokenize JavaScript for syntax highlighting (Monokai-like theme).</summary>
    public static class JavaScriptSyntaxHighlighter
    {
        private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
        {
            "async", "await", "break", "case", "catch", "continue", "debugger", "default", "delete", "do", "else",
            "finally", "for", "function", "if", "in", "instanceof", "new", "return", "switch",
            "this", "throw", "try", "typeof", "var", "void", "while", "with", "const", "let",
            "true", "false", "null", "undefined"
        };

        private static readonly HashSet<string> BuiltIns = new(StringComparer.Ordinal)
        {
            "JSON", "Math", "console", "Array", "Object", "String", "Number", "Boolean", "Date",
            "RegExp", "Error", "Promise", "Symbol", "Map", "Set", "WeakMap", "WeakSet", "Proxy",
            "Reflect", "Intl", "parseFloat", "parseInt", "isNaN", "isFinite", "decodeURI",
            "decodeURIComponent", "encodeURI", "encodeURIComponent", "eval", "setTimeout",
            "setInterval", "clearTimeout", "clearInterval", "NaN", "Infinity"
        };

        /// <summary>Monokai theme colors (VS Code style).</summary>
        public static Brush GetBrush(JsTokenType type)
        {
            return type switch
            {
                JsTokenType.CommentLine or JsTokenType.CommentBlock => new SolidColorBrush(Color.FromRgb(0x75, 0x71, 0x5E)), // #75715E comment
                JsTokenType.Keyword => new SolidColorBrush(Color.FromRgb(0xF9, 0x26, 0x72)),       // #F92672 keyword
                JsTokenType.BuiltIn => new SolidColorBrush(Color.FromRgb(0x66, 0xD9, 0xEF)),       // #66D9EF library/type
                JsTokenType.FunctionName => new SolidColorBrush(Color.FromRgb(0xA6, 0xE2, 0x2E)),  // #A6E22E function (xanh lá Monokai)
                JsTokenType.StringDouble or JsTokenType.StringSingle => new SolidColorBrush(Color.FromRgb(0xE6, 0xDB, 0x74)), // #E6DB74 string
                JsTokenType.Number => new SolidColorBrush(Color.FromRgb(0xAE, 0x81, 0xFF)),       // #AE81FF number
                JsTokenType.Regex => new SolidColorBrush(Color.FromRgb(0xAE, 0x81, 0xFF)),        // #AE81FF regex
                _ => new SolidColorBrush(Color.FromRgb(0xF8, 0xF8, 0xF2))                         // #F8F8F2 foreground
            };
        }

        /// <summary>Returns list of (startIndex, length, tokenType) for the given code.</summary>
        public static List<(int Start, int Length, JsTokenType Type)> Tokenize(string code)
        {
            var result = TokenizeFirstPass(code);
            if (result.Count == 0) return result;

            var declaredFunctions = new HashSet<string>(StringComparer.Ordinal);
            var tokens = new List<(int Start, int Length, JsTokenType Type)>(result);

            // Sau "function" (hoặc "async" rồi "function"), identifier tiếp theo là tên hàm.
            for (int idx = 0; idx < tokens.Count; idx++)
            {
                var (s, l, t) = tokens[idx];
                if (t != JsTokenType.Keyword || idx + 1 >= tokens.Count) continue;
                var word = code.Substring(s, l);
                if (word != "function") continue;
                var next = tokens[idx + 1];
                if (next.Type != JsTokenType.Plain || next.Start < s + l) continue;
                var name = code.Substring(next.Start, next.Length);
                declaredFunctions.Add(name);
                tokens[idx + 1] = (next.Start, next.Length, JsTokenType.FunctionName);
            }

            // Chỗ gọi hàm: identifier thuộc declaredFunctions và ngay sau nó là '('.
            for (int idx = 0; idx < tokens.Count; idx++)
            {
                var (s, l, t) = tokens[idx];
                if (t != JsTokenType.Plain) continue;
                var name = code.Substring(s, l);
                if (!declaredFunctions.Contains(name)) continue;
                var after = s + l;
                while (after < code.Length && char.IsWhiteSpace(code[after])) after++;
                if (after < code.Length && code[after] == '(')
                    tokens[idx] = (s, l, JsTokenType.FunctionName);
            }

            return tokens;
        }

        private static List<(int Start, int Length, JsTokenType Type)> TokenizeFirstPass(string code)
        {
            var result = new List<(int, int, JsTokenType)>();
            if (string.IsNullOrEmpty(code)) return result;

            var i = 0;
            var n = code.Length;

            while (i < n)
            {
                // Line comment
                if (i + 1 < n && code[i] == '/' && code[i + 1] == '/')
                {
                    var start = i;
                    while (i < n && code[i] != '\n') i++;
                    result.Add((start, i - start, JsTokenType.CommentLine));
                    continue;
                }

                // Block comment
                if (i + 1 < n && code[i] == '/' && code[i + 1] == '*')
                {
                    var start = i;
                    i += 2;
                    while (i + 1 < n && !(code[i] == '*' && code[i + 1] == '/')) i++;
                    if (i + 1 < n) i += 2;
                    result.Add((start, i - start, JsTokenType.CommentBlock));
                    continue;
                }

                // Double-quoted string
                if (code[i] == '"')
                {
                    var start = i;
                    i++;
                    while (i < n && code[i] != '"')
                    {
                        if (code[i] == '\\') i++;
                        i++;
                    }
                    if (i < n) i++;
                    result.Add((start, i - start, JsTokenType.StringDouble));
                    continue;
                }

                // Single-quoted string
                if (code[i] == '\'')
                {
                    var start = i;
                    i++;
                    while (i < n && code[i] != '\'')
                    {
                        if (code[i] == '\\') i++;
                        i++;
                    }
                    if (i < n) i++;
                    result.Add((start, i - start, JsTokenType.StringSingle));
                    continue;
                }

                // Regex (simplified: /.../ not in string) - only if looks like regex after = ( ) , ; : [ ] !
                if (code[i] == '/' && i + 1 < n && code[i + 1] != '/' && code[i + 1] != '*')
                {
                    var prev = i;
                    while (prev > 0 && char.IsWhiteSpace(code[prev - 1])) prev--;
                    var c = prev > 0 ? code[prev - 1] : ' ';
                    var regexStart = (c == '=' || c == '(' || c == ',' || c == ';' || c == ':' || c == '[' || c == '!' || c == '?' || c == '&' || c == '|' || c == '\n' || c == '\r');
                    if (regexStart)
                    {
                        var start = i;
                        i++;
                        while (i < n && code[i] != '/')
                        {
                            if (code[i] == '\\') i++;
                            i++;
                        }
                        if (i < n) i++;
                        result.Add((start, i - start, JsTokenType.Regex));
                        continue;
                    }
                }

                // Number
                if (char.IsDigit(code[i]) || (code[i] == '.' && i + 1 < n && char.IsDigit(code[i + 1])))
                {
                    var start = i;
                    if (code[i] == '.') i++;
                    while (i < n && (char.IsDigit(code[i]) || code[i] == '.')) i++;
                    if (i < n && (code[i] == 'e' || code[i] == 'E'))
                    {
                        i++;
                        if (i < n && (code[i] == '+' || code[i] == '-')) i++;
                        while (i < n && char.IsDigit(code[i])) i++;
                    }
                    result.Add((start, i - start, JsTokenType.Number));
                    continue;
                }

                // Identifier / keyword / built-in
                if (char.IsLetter(code[i]) || code[i] == '_' || code[i] == '$')
                {
                    var start = i;
                    while (i < n && (char.IsLetterOrDigit(code[i]) || code[i] == '_' || code[i] == '$')) i++;
                    var word = code.Substring(start, i - start);
                    var tokenType = Keywords.Contains(word) ? JsTokenType.Keyword
                        : BuiltIns.Contains(word) ? JsTokenType.BuiltIn
                        : JsTokenType.Plain;
                    result.Add((start, i - start, tokenType));
                    continue;
                }

                i++;
            }

            return result;
        }
    }
}
