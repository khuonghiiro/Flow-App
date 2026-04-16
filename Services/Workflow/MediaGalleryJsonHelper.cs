using FlowMy.Models.Nodes;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FlowMy.Services.Workflow
{
    /// <summary>
    /// Parse JSON và điền GalleryItems theo key template (vd: {projectId}, {fifeUri}, {servingBaseUri}).
    /// Tìm đệ quy các mảng object trong JSON, với mỗi phần tử trích giá trị theo tên key.
    /// </summary>
    public static class MediaGalleryJsonHelper
    {
        /// <summary>Trích tên key từ template dạng "{keyName}".</summary>
        public static string KeyFromTemplate(string? template)
        {
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;
            var t = template.Trim();
            if (t.Length >= 2 && t[0] == '{' && t[t.Length - 1] == '}')
                return t.Substring(1, t.Length - 2).Trim();
            return t;
        }

        /// <summary>
        /// Cố gắng normalize các JSON string không chuẩn (thiếu dấu ngoặc kép quanh key) để JsonDocument.Parse chấp nhận được.
        /// Không đảm bảo xử lý được mọi trường hợp, chỉ hỗ trợ format phổ biến kiểu {media: [...], workflows: [...]}.
        /// </summary>
        private static string TryNormalizeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;
            var trimmed = json.Trim();

            // Nếu đã parse được thì trả về luôn.
            try
            {
                using var _ = JsonDocument.Parse(trimmed);
                return trimmed;
            }
            catch
            {
                // ignore, sẽ thử normalize bên dưới.
            }

            // Thêm dấu ngoặc kép cho key không có dấu ngoặc ở cấp object đơn giản.
            // Pattern: {media: [...] } -> {"media": [...]}
            try
            {
                var normalized = Regex.Replace(
                    trimmed,
                    "([{,]\\s*)([A-Za-z_\\$][A-Za-z0-9_\\$]*)\\s*:",
                    "$1\"$2\":");
                return normalized;
            }
            catch
            {
                // Nếu regex lỗi thì trả về bản gốc.
                return json;
            }
        }

        /// <summary>Parse JSON string và điền node.GalleryItems hoặc node.GalleryGroups (gọi từ UI thread hoặc Dispatcher).</summary>
        public static void ParseAndFill(string? json, MediaGalleryNode node)
        {
            if (node == null) return;
            node.GalleryItems.Clear();
            node.GalleryGroups.Clear();
            if (string.IsNullOrWhiteSpace(json)) return;

            var titleKey = KeyFromTemplate(node.TitleKeyTemplate);
            var imageKey = KeyFromTemplate(node.ImageUrlKeyTemplate);
            var videoKey = KeyFromTemplate(node.VideoUrlKeyTemplate);
            var keys = new HashSet<string> { titleKey, imageKey, videoKey, "title", "imageUrl", "videoUrl" };

            try
            {
                var normalizedJson = TryNormalizeJson(json);
                using var doc = JsonDocument.Parse(normalizedJson);
                var root = doc.RootElement;

                if (node.DisplayMode == GalleryDisplayMode.Grouped && root.ValueKind == JsonValueKind.Object)
                {
                    var groupArrayKey = KeyFromTemplate(node.GroupArrayKey);
                    var groupTitleKey = KeyFromTemplate(node.GroupTitleKey);
                    var groupItemsKey = KeyFromTemplate(node.GroupItemsKey);
                    if (string.IsNullOrEmpty(groupArrayKey)) groupArrayKey = "workflows";
                    if (string.IsNullOrEmpty(groupItemsKey)) groupItemsKey = "videos";
                    if (root.TryGetProperty(groupArrayKey, out var groupsArr) && groupsArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var groupEl in groupsArr.EnumerateArray())
                        {
                            if (groupEl.ValueKind != JsonValueKind.Object) continue;
                            var groupTitle = !string.IsNullOrEmpty(groupTitleKey) && groupEl.TryGetProperty(groupTitleKey, out var gt) ? (gt.GetString() ?? gt.GetRawText()) : "";
                            var group = new MediaGalleryGroup { Title = groupTitle ?? "" };
                            if (groupEl.TryGetProperty(groupItemsKey, out var itemsArr) && itemsArr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var itemEl in itemsArr.EnumerateArray())
                                {
                                    if (itemEl.ValueKind != JsonValueKind.Object) continue;
                                    var dict = ExtractKeysFromObject(itemEl, keys);
                                    var t = dict.TryGetValue(titleKey, out var tv) ? tv : (dict.TryGetValue("title", out var t2) ? t2 : null);
                                    var i = dict.TryGetValue(imageKey, out var iv) ? iv : (dict.TryGetValue("imageUrl", out var i2) ? i2 : null);
                                    var v = dict.TryGetValue(videoKey, out var vv) ? vv : (dict.TryGetValue("videoUrl", out var v2) ? v2 : null);
                                    group.Items.Add(new MediaGalleryItem
                                    {
                                        Title = t ?? "",
                                        ImageUrl = i,
                                        VideoUrl = v,
                                        IsSelected = false
                                    });
                                }
                            }
                            if (group.Items.Count > 0)
                                node.GalleryGroups.Add(group);
                        }
                        return;
                    }
                }

                var items = CollectItemsFromElement(root, keys);
                foreach (var dict in items)
                {
                    var t = dict.TryGetValue(titleKey, out var tv) ? tv : (dict.TryGetValue("title", out var t2) ? t2 : null);
                    var i = dict.TryGetValue(imageKey, out var iv) ? iv : (dict.TryGetValue("imageUrl", out var i2) ? i2 : null);
                    var v = dict.TryGetValue(videoKey, out var vv) ? vv : (dict.TryGetValue("videoUrl", out var v2) ? v2 : null);
                    node.GalleryItems.Add(new MediaGalleryItem
                    {
                        Title = t ?? "",
                        ImageUrl = i,
                        VideoUrl = v,
                        IsSelected = false
                    });
                }
            }
            catch
            {
                // ignore parse errors
            }
        }

        /// <summary>Duyệt đệ quy: tìm mảng, với mỗi phần tử object trích các key cần lấy thành một item.</summary>
        private static List<Dictionary<string, string?>> CollectItemsFromElement(JsonElement el, HashSet<string> keys)
        {
            var list = new List<Dictionary<string, string?>>();
            CollectFromElement(el, keys, list);
            return list;
        }

        private static void CollectFromElement(JsonElement el, HashSet<string> keys, List<Dictionary<string, string?>> result)
        {
            if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        var dict = ExtractKeysFromObject(item, keys);
                        if (dict.Count > 0)
                            result.Add(dict);
                        CollectFromElement(item, keys, result);
                    }
                    else
                        CollectFromElement(item, keys, result);
                }
                return;
            }
            if (el.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in el.EnumerateObject())
                    CollectFromElement(prop.Value, keys, result);
            }
        }

        private static Dictionary<string, string?> ExtractKeysFromObject(JsonElement obj, HashSet<string> keys)
        {
            var dict = new Dictionary<string, string?>();
            foreach (var prop in obj.EnumerateObject())
            {
                if (keys.Contains(prop.Name))
                    dict[prop.Name] = GetStringValue(prop.Value);
                else if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                {
                    var nested = ExtractKeysFromElementRecursive(prop.Value, keys);
                    foreach (var kv in nested)
                        if (!dict.ContainsKey(kv.Key)) dict[kv.Key] = kv.Value;
                }
            }
            return dict;
        }

        private static Dictionary<string, string?> ExtractKeysFromElementRecursive(JsonElement el, HashSet<string> keys)
        {
            var dict = new Dictionary<string, string?>();
            if (el.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in el.EnumerateObject())
                {
                    if (keys.Contains(prop.Name))
                        dict[prop.Name] = GetStringValue(prop.Value);
                    else
                    {
                        var nested = ExtractKeysFromElementRecursive(prop.Value, keys);
                        foreach (var kv in nested)
                            if (!dict.ContainsKey(kv.Key)) dict[kv.Key] = kv.Value;
                    }
                }
            }
            else if (el.ValueKind == JsonValueKind.Array && el.GetArrayLength() > 0)
            {
                var first = el[0];
                return ExtractKeysFromElementRecursive(first, keys);
            }
            return dict;
        }

        private static string? GetStringValue(JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.String: return el.GetString();
                case JsonValueKind.Number: return el.GetRawText();
                case JsonValueKind.True: return "true";
                case JsonValueKind.False: return "false";
                case JsonValueKind.Null: return null;
                default: return el.GetRawText();
            }
        }
    }
}
