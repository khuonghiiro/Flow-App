using System.Collections.Generic;

namespace FlowMy.Helpers
{
    /// <summary>Gợi ý method/property khi gõ đối tượng JavaScript (JSON., Math., ...).</summary>
    public static class JavaScriptCompletionData
    {
        private static readonly Dictionary<string, string[]> ObjectMembers = new Dictionary<string, string[]>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["JSON"] = new[] { "parse", "stringify" },
            ["Math"] = new[] { "abs", "acos", "acosh", "asin", "asinh", "atan", "atan2", "atanh", "cbrt", "ceil", "clz32", "cos", "cosh", "exp", "expm1", "floor", "fround", "hypot", "imul", "log", "log10", "log1p", "log2", "max", "min", "pow", "random", "round", "sign", "sin", "sinh", "sqrt", "tan", "tanh", "trunc", "E", "PI" },
            ["console"] = new[] { "log", "info", "warn", "error", "debug", "trace", "dir", "table", "clear", "count", "countReset", "group", "groupEnd", "groupCollapsed", "time", "timeEnd", "timeLog", "assert" },
            ["Array"] = new[] { "from", "isArray", "of", "concat", "copyWithin", "entries", "every", "fill", "filter", "find", "findIndex", "flat", "flatMap", "forEach", "includes", "indexOf", "join", "keys", "lastIndexOf", "map", "pop", "push", "reduce", "reduceRight", "reverse", "shift", "slice", "some", "sort", "splice", "toLocaleString", "toString", "unshift", "values", "length" },
            ["Object"] = new[] { "assign", "create", "defineProperties", "defineProperty", "entries", "freeze", "fromEntries", "getOwnPropertyDescriptor", "getOwnPropertyDescriptors", "getOwnPropertyNames", "getPrototypeOf", "hasOwn", "is", "isExtensible", "isFrozen", "isSealed", "keys", "preventExtensions", "seal", "setPrototypeOf", "values" },
            ["String"] = new[] { "fromCharCode", "fromCodePoint", "raw", "charAt", "charCodeAt", "codePointAt", "concat", "endsWith", "includes", "indexOf", "lastIndexOf", "localeCompare", "match", "matchAll", "normalize", "padEnd", "padStart", "repeat", "replace", "replaceAll", "search", "slice", "split", "startsWith", "substring", "toLowerCase", "toUpperCase", "trim", "trimEnd", "trimStart", "length" },
            ["Number"] = new[] { "isFinite", "isInteger", "isNaN", "isSafeInteger", "parseFloat", "parseInt", "toExponential", "toFixed", "toPrecision", "toString", "EPSILON", "MAX_SAFE_INTEGER", "MIN_SAFE_INTEGER", "MAX_VALUE", "MIN_VALUE", "NEGATIVE_INFINITY", "POSITIVE_INFINITY", "NaN" },
            ["Date"] = new[] { "now", "parse", "UTC", "getDate", "getDay", "getFullYear", "getHours", "getMilliseconds", "getMinutes", "getMonth", "getSeconds", "getTime", "getTimezoneOffset", "getUTCDate", "getUTCDay", "getUTCFullYear", "getUTCHours", "getUTCMilliseconds", "getUTCMinutes", "getUTCMonth", "getUTCSeconds", "setDate", "setFullYear", "setHours", "setMilliseconds", "setMinutes", "setMonth", "setSeconds", "setTime", "setUTCDate", "setUTCFullYear", "setUTCHours", "setUTCMilliseconds", "setUTCMinutes", "setUTCMonth", "setUTCSeconds", "toDateString", "toISOString", "toJSON", "toLocaleDateString", "toLocaleString", "toLocaleTimeString", "toString", "toTimeString", "toUTCString", "valueOf" },
            ["RegExp"] = new[] { "exec", "test", "source", "flags", "global", "ignoreCase", "multiline", "lastIndex" },
            ["Promise"] = new[] { "all", "allSettled", "any", "race", "reject", "resolve", "then", "catch", "finally" },
            ["Map"] = new[] { "clear", "delete", "entries", "forEach", "get", "has", "keys", "set", "size", "values" },
            ["Set"] = new[] { "add", "clear", "delete", "entries", "forEach", "has", "keys", "size", "values" },
            ["Reflect"] = new[] { "apply", "construct", "defineProperty", "deleteProperty", "get", "getOwnPropertyDescriptor", "getPrototypeOf", "has", "isExtensible", "ownKeys", "preventExtensions", "set", "setPrototypeOf" },
        };

        public static bool TryGetMembers(string objectName, out string[]? members)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                members = null;
                return false;
            }
            return ObjectMembers.TryGetValue(objectName.Trim(), out members);
        }
    }
}
