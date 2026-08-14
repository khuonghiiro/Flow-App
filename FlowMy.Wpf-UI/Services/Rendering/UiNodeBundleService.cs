// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace FlowMy.Services.Rendering
{
    public class UiNodeBundleResult
    {
        public string Title { get; set; } = string.Empty;
        public string HtmlCode { get; set; } = string.Empty;
        public string CssCode { get; set; } = string.Empty;
        public string JsCode { get; set; } = string.Empty;
        public string ParamsCode { get; set; } = string.Empty;
    }

    public static class UiNodeBundleService
    {
        public static void ExportToZip(
            string destinationZipPath,
            string title,
            string htmlCode,
            string cssCode,
            string jsCode,
            string paramsCode)
        {
            if (File.Exists(destinationZipPath))
                File.Delete(destinationZipPath);

            using var archive = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create);

            // 1. index.html
            var htmlEntry = archive.CreateEntry("index.html");
            using (var writer = new StreamWriter(htmlEntry.Open()))
                writer.Write(htmlCode ?? string.Empty);

            // 2. style.css
            var cssEntry = archive.CreateEntry("style.css");
            using (var writer = new StreamWriter(cssEntry.Open()))
                writer.Write(cssCode ?? string.Empty);

            // 3. script.js
            var jsEntry = archive.CreateEntry("script.js");
            using (var writer = new StreamWriter(jsEntry.Open()))
                writer.Write(jsCode ?? string.Empty);

            // 4. params.txt
            var paramsEntry = archive.CreateEntry("params.txt");
            using (var writer = new StreamWriter(paramsEntry.Open()))
                writer.Write(paramsCode ?? string.Empty);

            // 5. manifest.json
            var manifestObj = new
            {
                title = title ?? string.Empty,
                version = "1.0",
                exportedAt = DateTime.Now.ToString("o")
            };
            var manifestJson = JsonSerializer.Serialize(manifestObj, new JsonSerializerOptions { WriteIndented = true });
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
                writer.Write(manifestJson);
        }

        public static UiNodeBundleResult ImportFromZip(string zipPath)
        {
            var result = new UiNodeBundleResult();
            using var archive = ZipFile.OpenRead(zipPath);

            foreach (var entry in archive.Entries)
            {
                var name = entry.Name.ToLowerInvariant();
                using var reader = new StreamReader(entry.Open());
                var content = reader.ReadToEnd();

                if (name == "index.html" || name == "index.htm")
                    result.HtmlCode = content;
                else if (name == "style.css")
                    result.CssCode = content;
                else if (name == "script.js")
                    result.JsCode = content;
                else if (name == "params.txt" || name == "param.txt")
                    result.ParamsCode = content;
                else if (name == "manifest.json")
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("title", out var titleProp))
                            result.Title = titleProp.GetString() ?? string.Empty;
                    }
                    catch { }
                }
            }

            return result;
        }
    }
}
