using ICSharpCode.AvalonEdit.Highlighting;
using System.Windows;
using System.Windows.Media;

namespace FlowMy.Helpers
{
    /// <summary>
    /// Helper để apply Monokai theme cho AvalonEdit highlighting definitions.
    /// Sử dụng tên highlighting colors CHÍNH XÁC từ AvalonEdit.
    /// </summary>
    public static class AvalonEditMonokaiHelper
    {
        // Monokai color palette - màu chuẩn
        private static readonly Color MonokaiBackground = Color.FromRgb(0x27, 0x28, 0x22);    // #272822
        private static readonly Color MonokaiForeground = Color.FromRgb(0xF8, 0xF8, 0xF2);    // #F8F8F2
        private static readonly Color MonokaiComment = Color.FromRgb(0x75, 0x71, 0x5E);       // #75715E - xám
        private static readonly Color MonokaiKeyword = Color.FromRgb(0xF9, 0x26, 0x72);       // #F92672 - hồng
        private static readonly Color MonokaiString = Color.FromRgb(0xE6, 0xDB, 0x74);        // #E6DB74 - vàng
        private static readonly Color MonokaiNumber = Color.FromRgb(0xAE, 0x81, 0xFF);        // #AE81FF - tím
        private static readonly Color MonokaiFunction = Color.FromRgb(0xA6, 0xE2, 0x2E);      // #A6E22E - xanh lá
        private static readonly Color MonokaiType = Color.FromRgb(0x66, 0xD9, 0xEF);          // #66D9EF - xanh dương nhạt
        private static readonly Color MonokaiProperty = Color.FromRgb(0x66, 0xD9, 0xEF);      // #66D9EF - xanh dương nhạt

        /// <summary>
        /// Apply Monokai colors vào highlighting definition
        /// </summary>
        public static void ApplyMonokaiTheme(IHighlightingDefinition definition)
        {
            if (definition == null) return;

            var defName = definition.Name?.ToLowerInvariant() ?? "";

            // Apply màu cho tất cả named colors
            foreach (var namedColor in definition.NamedHighlightingColors)
            {
                var name = namedColor.Name?.ToLowerInvariant() ?? "";
                ApplyColorByName(namedColor, name, defName);
            }

            // Apply màu cho MainRuleSet
            if (definition.MainRuleSet != null)
            {
                ModifyRuleSetColors(definition.MainRuleSet, defName);
            }
        }

        private static void ApplyColorByName(HighlightingColor color, string name, string language)
        {
            if (color == null || string.IsNullOrEmpty(name)) return;

            // === COMMENT - Xám italic ===
            if (name == "comment")
            {
                color.Foreground = new SimpleHighlightingBrush(MonokaiComment);
                color.FontStyle = FontStyles.Italic;
                return;
            }

            // === STRING - Vàng ===
            if (name == "string" || name == "character")
            {
                color.Foreground = new SimpleHighlightingBrush(MonokaiString);
                return;
            }

            // === NUMBER - Tím ===
            if (name == "digits")
            {
                color.Foreground = new SimpleHighlightingBrush(MonokaiNumber);
                return;
            }

            // === HTML SPECIFIC ===
            if (language.Contains("html"))
            {
                switch (name)
                {
                    case "htmltag":
                    case "tags":
                    case "scripttag":
                    case "javascripttag":
                    case "jscripttag":
                    case "vbscripttag":
                    case "unknownscripttag":
                        color.Foreground = new SimpleHighlightingBrush(MonokaiKeyword); // Tags màu hồng
                        return;

                    case "attributes":
                    case "unknownattribute":
                        color.Foreground = new SimpleHighlightingBrush(MonokaiFunction); // Attributes màu xanh lá
                        return;

                    case "assignment":
                    case "slash":
                        color.Foreground = new SimpleHighlightingBrush(MonokaiForeground); // Màu trắng
                        return;

                    case "entityreference":
                    case "entities":
                        color.Foreground = new SimpleHighlightingBrush(MonokaiNumber); // Entities màu tím
                        return;
                }
            }

            // === CSS SPECIFIC ===
            if (language.Contains("css"))
            {
                switch (name)
                {
                    case "selector":
                    case "class":
                        color.Foreground = new SimpleHighlightingBrush(MonokaiFunction); // Selectors màu xanh lá
                        return;

                    case "property":
                        color.Foreground = new SimpleHighlightingBrush(MonokaiProperty); // Properties màu xanh dương
                        return;

                    case "value":
                        color.Foreground = new SimpleHighlightingBrush(MonokaiNumber); // Values màu tím
                        return;

                    case "curlybraces":
                    case "colon":
                        color.Foreground = new SimpleHighlightingBrush(MonokaiForeground); // Màu trắng
                        return;
                }
            }

            // === JAVASCRIPT SPECIFIC ===
            if (language.Contains("javascript") || language.Contains("js"))
            {
                switch (name)
                {
                    case "javascriptkeywords":
                        color.Foreground = new SimpleHighlightingBrush(MonokaiKeyword); // Keywords màu hồng
                        return;

                    case "javascriptintrinsics":
                    case "javascriptglobalfunctions":
                        color.Foreground = new SimpleHighlightingBrush(MonokaiFunction); // Functions màu xanh lá
                        return;

                    case "javascriptliterals":
                        color.Foreground = new SimpleHighlightingBrush(MonokaiNumber); // Literals (true, false, null) màu tím
                        return;

                    case "regex":
                        color.Foreground = new SimpleHighlightingBrush(MonokaiString); // Regex màu vàng (giống string)
                        return;
                }
            }

            // === DEFAULT - Trắng nhạt ===
            if (color.Foreground == null)
            {
                color.Foreground = new SimpleHighlightingBrush(MonokaiForeground);
            }
        }

        private static void ModifyRuleSetColors(HighlightingRuleSet ruleSet, string language)
        {
            if (ruleSet == null) return;

            // Apply cho tất cả rules
            foreach (var rule in ruleSet.Rules)
            {
                if (rule.Color != null)
                {
                    var name = rule.Color.Name?.ToLowerInvariant() ?? "";
                    ApplyColorByName(rule.Color, name, language);
                }
            }

            // Apply cho tất cả spans
            foreach (var span in ruleSet.Spans)
            {
                if (span.SpanColor != null)
                {
                    var name = span.SpanColor.Name?.ToLowerInvariant() ?? "";
                    ApplyColorByName(span.SpanColor, name, language);
                }
                if (span.StartColor != null)
                {
                    var name = span.StartColor.Name?.ToLowerInvariant() ?? "";
                    ApplyColorByName(span.StartColor, name, language);
                }
                if (span.EndColor != null)
                {
                    var name = span.EndColor.Name?.ToLowerInvariant() ?? "";
                    ApplyColorByName(span.EndColor, name, language);
                }

                // Đệ quy cho nested rule sets
                if (span.RuleSet != null)
                    ModifyRuleSetColors(span.RuleSet, language);
            }
        }

        #region Public Methods

        /// <summary>
        /// Lấy highlighting definition với Monokai theme cho C#
        /// </summary>
        public static IHighlightingDefinition GetCSharpMonokai()
        {
            try
            {
                var baseDef = HighlightingManager.Instance.GetDefinition("C#");
                if (baseDef != null)
                {
                    ApplyMonokaiTheme(baseDef);
                    return baseDef;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Lấy highlighting definition với Monokai theme cho JavaScript
        /// </summary>
        public static IHighlightingDefinition GetJavaScriptMonokai()
        {
            try
            {
                var baseDef = HighlightingManager.Instance.GetDefinition("JavaScript");
                if (baseDef != null)
                {
                    ApplyMonokaiTheme(baseDef);
                    return baseDef;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Lấy highlighting definition với Monokai theme cho HTML
        /// </summary>
        public static IHighlightingDefinition GetHtmlMonokai()
        {
            try
            {
                var baseDef = HighlightingManager.Instance.GetDefinition("HTML");
                if (baseDef != null)
                {
                    ApplyMonokaiTheme(baseDef);
                    return baseDef;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Lấy highlighting definition với Monokai theme cho CSS
        /// </summary>
        public static IHighlightingDefinition GetCssMonokai()
        {
            try
            {
                var baseDef = HighlightingManager.Instance.GetDefinition("CSS");
                if (baseDef != null)
                {
                    ApplyMonokaiTheme(baseDef);
                    return baseDef;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Lấy màu background của Monokai theme
        /// </summary>
        public static Color GetMonokaiBackground() => MonokaiBackground;

        /// <summary>
        /// Lấy màu foreground của Monokai theme
        /// </summary>
        public static Color GetMonokaiForeground() => MonokaiForeground;

        #endregion
    }
}