using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService
{
    // -- RESTORE (Deserialize) --

    private static void RestoreScreenCaptureNodeProperties(ScreenCaptureNode cap, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("CaptureX", out var cx))
                cap.CaptureX = int.Parse(cx.ToString()!);
            if (properties.TryGetValue("CaptureY", out var cy))
                cap.CaptureY = int.Parse(cy.ToString()!);
            if (properties.TryGetValue("CaptureWidth", out var cw))
                cap.CaptureWidth = int.Parse(cw.ToString()!);
            if (properties.TryGetValue("CaptureHeight", out var ch))
                cap.CaptureHeight = int.Parse(ch.ToString()!);

            if (properties.TryGetValue("CapturedImageBase64", out var b64Obj))
            {
                var b64 = b64Obj?.ToString();
                var restored = TryDecodePngBase64ToBitmapImage(b64);
                if (restored != null)
                {
                    cap.CapturedImage = restored;
                }
            }

            // Chế độ hoạt động
            if (properties.TryGetValue("CaptureMode", out var cmObj) &&
                Enum.TryParse<ScreenCaptureMode>(cmObj?.ToString(), out var cm))
                cap.CaptureMode = cm;

            // Input node — toạ độ
            if (properties.TryGetValue("CoordSourceNodeId", out var csnId))
                cap.CoordSourceNodeId = csnId?.ToString();
            if (properties.TryGetValue("CoordSourceOutputKey", out var csnKey))
                cap.CoordSourceOutputKey = csnKey?.ToString();

            // Input node — Path/URL
            if (properties.TryGetValue("PathSourceNodeId", out var psnId))
                cap.PathSourceNodeId = psnId?.ToString();
            if (properties.TryGetValue("PathSourceOutputKey", out var psnKey))
                cap.PathSourceOutputKey = psnKey?.ToString();

            // Path/URL nhập tay
            if (properties.TryGetValue("ImagePath", out var ipObj))
                cap.ImagePath = ipObj?.ToString() ?? string.Empty;

            // Kích thước node
            if (properties.TryGetValue("UseNativeWidth", out var unwObj) &&
                bool.TryParse(unwObj?.ToString(), out var unw))
                cap.UseNativeWidth = unw;
            if (properties.TryGetValue("MaxNodeWidth", out var mnwObj) &&
                double.TryParse(mnwObj?.ToString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var mnw))
                cap.MaxNodeWidth = mnw;

            // SkipOutputs
            if (properties.TryGetValue("SkipOutputs", out var soObj) && soObj != null)
            {
                try
                {
                    string? json = soObj is string s ? s
                        : soObj is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.String ? je.GetString()
                        : soObj is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.Array ? je2.GetRawText()
                        : null;
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(json);
                        if (list != null)
                            cap.SkipOutputs = new System.Collections.Generic.HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch { /* best-effort */ }
            }

            // Target app
            if (properties.TryGetValue("TargetProcessName", out var tpnObj))
                cap.TargetProcessName = tpnObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("TargetWindowTitle", out var twtObj))
                cap.TargetWindowTitle = twtObj?.ToString() ?? string.Empty;

            // Background Mode
            if (properties.TryGetValue("UseBackgroundMode", out var ubmObj) &&
                bool.TryParse(ubmObj?.ToString(), out var ubm))
                cap.UseBackgroundMode = ubm;
            if (properties.TryGetValue("BackgroundInputMode", out var bimObj) &&
                Enum.TryParse<FlowMy.Helpers.BackgroundInputHelper.InputMode>(bimObj?.ToString(), out var bim))
                cap.BackgroundInputMode = bim;
    }

    private static void RestoreTextScanNodeProperties(TextScanNode textScan, Dictionary<string, object> properties)
    {
            // OCR Engine Mode
            if (properties.TryGetValue("OcrEngineMode", out var oemObj) &&
                Enum.TryParse<OcrEngineMode>(oemObj?.ToString(), out var oem))
                textScan.OcrEngineMode = oem;

            // Tesseract settings
            if (properties.TryGetValue("TessdataPath", out var tdpObj))
                textScan.TessdataPath = tdpObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("TesseractPageSegMode", out var tpsmObj) &&
                Enum.TryParse<TesseractPageSegMode>(tpsmObj?.ToString(), out var tpsm))
                textScan.TesseractPageSegMode = tpsm;
            if (properties.TryGetValue("TesseractEngineMode", out var temObj) &&
                Enum.TryParse<TesseractEngineMode>(temObj?.ToString(), out var tem))
                textScan.TesseractEngineMode = tem;

            // SelectedLanguages (list)
            if (properties.TryGetValue("SelectedLanguages", out var slObj) && slObj != null)
            {
                try
                {
                    string? json = slObj is string s ? s
                        : slObj is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.String ? je.GetString()
                        : slObj is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.Array ? je2.GetRawText()
                        : null;
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(json);
                        if (list != null)
                            textScan.SelectedLanguages = list;
                    }
                }
                catch { /* best-effort */ }
            }

            // Image Source Mode
            if (properties.TryGetValue("ImageSourceMode", out var ismObj) &&
                Enum.TryParse<ImageSourceMode>(ismObj?.ToString(), out var ism))
                textScan.ImageSourceMode = ism;

            // Capture region
            if (properties.TryGetValue("CaptureX", out var cxObj))
                textScan.CaptureX = int.Parse(cxObj.ToString()!);
            if (properties.TryGetValue("CaptureY", out var cyObj))
                textScan.CaptureY = int.Parse(cyObj.ToString()!);
            if (properties.TryGetValue("CaptureWidth", out var cwObj))
                textScan.CaptureWidth = int.Parse(cwObj.ToString()!);
            if (properties.TryGetValue("CaptureHeight", out var chObj))
                textScan.CaptureHeight = int.Parse(chObj.ToString()!);

            // Captured image
            if (properties.TryGetValue("CapturedImageBase64", out var b64Obj))
            {
                var b64 = b64Obj?.ToString();
                var restored = TryDecodePngBase64ToBitmapImage(b64);
                if (restored != null)
                    textScan.CapturedImage = restored;
            }

            // Input node — coordinates
            if (properties.TryGetValue("CoordSourceNodeId", out var csnIdObj))
                textScan.CoordSourceNodeId = csnIdObj?.ToString();
            if (properties.TryGetValue("CoordSourceOutputKey", out var csnKeyObj))
                textScan.CoordSourceOutputKey = csnKeyObj?.ToString();

            // Input node — image
            if (properties.TryGetValue("ImageSourceNodeId", out var isnIdObj))
                textScan.ImageSourceNodeId = isnIdObj?.ToString();
            if (properties.TryGetValue("ImageSourceOutputKey", out var isnKeyObj))
                textScan.ImageSourceOutputKey = isnKeyObj?.ToString();

            // Path / URL
            if (properties.TryGetValue("ImagePath", out var ipObj))
                textScan.ImagePath = ipObj?.ToString() ?? string.Empty;

            // Base64 image
            if (properties.TryGetValue("Base64Image", out var biObj))
                textScan.Base64Image = biObj?.ToString() ?? string.Empty;

            // OCR Language
            if (properties.TryGetValue("OcrLanguage", out var olObj))
                textScan.OcrLanguage = olObj?.ToString() ?? "eng";
            if (properties.TryGetValue("AutoDetectLanguage", out var adlObj) &&
                bool.TryParse(adlObj?.ToString(), out var adl))
                textScan.AutoDetectLanguage = adl;

            // Target app
            if (properties.TryGetValue("TargetProcessName", out var tpnObj2))
                textScan.TargetProcessName = tpnObj2?.ToString() ?? string.Empty;
            if (properties.TryGetValue("TargetWindowTitle", out var twtObj2))
                textScan.TargetWindowTitle = twtObj2?.ToString() ?? string.Empty;

            // Background Mode
            if (properties.TryGetValue("UseBackgroundMode", out var ubmObj2) &&
                bool.TryParse(ubmObj2?.ToString(), out var ubm2))
                textScan.UseBackgroundMode = ubm2;
            if (properties.TryGetValue("BackgroundInputMode", out var bimObj2) &&
                Enum.TryParse<FlowMy.Helpers.BackgroundInputHelper.InputMode>(bimObj2?.ToString(), out var bim2))
                textScan.BackgroundInputMode = bim2;

            // SkipOutputs
            if (properties.TryGetValue("SkipOutputs", out var soObj) && soObj != null)
            {
                try
                {
                    string? json = soObj is string s ? s
                        : soObj is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.String ? je.GetString()
                        : soObj is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.Array ? je2.GetRawText()
                        : null;
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(json);
                        if (list != null)
                            textScan.SkipOutputs = new System.Collections.Generic.HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch { /* best-effort */ }
            }
    }

    // -- GET (Serialize) --

    private static void GetScreenCaptureNodeProperties(ScreenCaptureNode cap, Dictionary<string, object> dict)
    {
            dict["CaptureX"] = cap.CaptureX;
            dict["CaptureY"] = cap.CaptureY;
            dict["CaptureWidth"] = cap.CaptureWidth;
            dict["CaptureHeight"] = cap.CaptureHeight;

            var b64 = TryEncodeBitmapSourceToPngBase64(cap.CapturedImage);
            if (!string.IsNullOrWhiteSpace(b64))
                dict["CapturedImageBase64"] = b64;

            // Chế độ hoạt động
            dict["CaptureMode"] = cap.CaptureMode.ToString();

            // Input node — toạ độ
            if (!string.IsNullOrWhiteSpace(cap.CoordSourceNodeId))
                dict["CoordSourceNodeId"] = cap.CoordSourceNodeId;
            if (!string.IsNullOrWhiteSpace(cap.CoordSourceOutputKey))
                dict["CoordSourceOutputKey"] = cap.CoordSourceOutputKey;

            // Input node — Path/URL
            if (!string.IsNullOrWhiteSpace(cap.PathSourceNodeId))
                dict["PathSourceNodeId"] = cap.PathSourceNodeId;
            if (!string.IsNullOrWhiteSpace(cap.PathSourceOutputKey))
                dict["PathSourceOutputKey"] = cap.PathSourceOutputKey;

            // Path/URL nhập tay
            if (!string.IsNullOrWhiteSpace(cap.ImagePath))
                dict["ImagePath"] = cap.ImagePath;

            // Kích thước node
            dict["UseNativeWidth"] = cap.UseNativeWidth;
            dict["MaxNodeWidth"]   = cap.MaxNodeWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // SkipOutputs
            if (cap.SkipOutputs != null && cap.SkipOutputs.Count > 0)
                dict["SkipOutputs"] = System.Text.Json.JsonSerializer.Serialize(cap.SkipOutputs.ToList());

            // Target app
            if (!string.IsNullOrWhiteSpace(cap.TargetProcessName))
                dict["TargetProcessName"] = cap.TargetProcessName;
            if (!string.IsNullOrWhiteSpace(cap.TargetWindowTitle))
                dict["TargetWindowTitle"] = cap.TargetWindowTitle;

            // Background Mode
            dict["UseBackgroundMode"] = cap.UseBackgroundMode;
            dict["BackgroundInputMode"] = cap.BackgroundInputMode.ToString();
    }

    private static void GetTextScanNodeProperties(TextScanNode textScan, Dictionary<string, object> dict)
    {
            // OCR Engine Mode
            dict["OcrEngineMode"] = textScan.OcrEngineMode.ToString();

            // Tesseract settings
            if (!string.IsNullOrWhiteSpace(textScan.TessdataPath))
                dict["TessdataPath"] = textScan.TessdataPath;
            dict["TesseractPageSegMode"] = textScan.TesseractPageSegMode.ToString();
            dict["TesseractEngineMode"] = textScan.TesseractEngineMode.ToString();

            // SelectedLanguages (list)
            if (textScan.SelectedLanguages != null && textScan.SelectedLanguages.Count > 0)
                dict["SelectedLanguages"] = System.Text.Json.JsonSerializer.Serialize(textScan.SelectedLanguages);

            // Image Source Mode
            dict["ImageSourceMode"] = textScan.ImageSourceMode.ToString();

            // Capture region
            dict["CaptureX"] = textScan.CaptureX;
            dict["CaptureY"] = textScan.CaptureY;
            dict["CaptureWidth"] = textScan.CaptureWidth;
            dict["CaptureHeight"] = textScan.CaptureHeight;

            // Captured image
            var b64 = TryEncodeBitmapSourceToPngBase64(textScan.CapturedImage);
            if (!string.IsNullOrWhiteSpace(b64))
                dict["CapturedImageBase64"] = b64;

            // Input node — coordinates
            if (!string.IsNullOrWhiteSpace(textScan.CoordSourceNodeId))
                dict["CoordSourceNodeId"] = textScan.CoordSourceNodeId;
            if (!string.IsNullOrWhiteSpace(textScan.CoordSourceOutputKey))
                dict["CoordSourceOutputKey"] = textScan.CoordSourceOutputKey;

            // Input node — image
            if (!string.IsNullOrWhiteSpace(textScan.ImageSourceNodeId))
                dict["ImageSourceNodeId"] = textScan.ImageSourceNodeId;
            if (!string.IsNullOrWhiteSpace(textScan.ImageSourceOutputKey))
                dict["ImageSourceOutputKey"] = textScan.ImageSourceOutputKey;

            // Path / URL
            if (!string.IsNullOrWhiteSpace(textScan.ImagePath))
                dict["ImagePath"] = textScan.ImagePath;

            // Base64 image
            if (!string.IsNullOrWhiteSpace(textScan.Base64Image))
                dict["Base64Image"] = textScan.Base64Image;

            // OCR Language
            if (!string.IsNullOrWhiteSpace(textScan.OcrLanguage))
                dict["OcrLanguage"] = textScan.OcrLanguage;
            dict["AutoDetectLanguage"] = textScan.AutoDetectLanguage.ToString();

            // Target app
            if (!string.IsNullOrWhiteSpace(textScan.TargetProcessName))
                dict["TargetProcessName"] = textScan.TargetProcessName;
            if (!string.IsNullOrWhiteSpace(textScan.TargetWindowTitle))
                dict["TargetWindowTitle"] = textScan.TargetWindowTitle;

            // Background Mode
            dict["UseBackgroundMode"] = textScan.UseBackgroundMode;
            dict["BackgroundInputMode"] = textScan.BackgroundInputMode.ToString();

            // SkipOutputs
            if (textScan.SkipOutputs != null && textScan.SkipOutputs.Count > 0)
                dict["SkipOutputs"] = System.Text.Json.JsonSerializer.Serialize(textScan.SkipOutputs.ToList());
    }

}
