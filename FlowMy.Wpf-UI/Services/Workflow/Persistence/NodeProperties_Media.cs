// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Core.Models.Media;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService
{
    // -- RESTORE (Deserialize) --

    private static void RestoreMediaGalleryNodeProperties(MediaGalleryNode mediaGalleryNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("Width", out var wObj) && wObj != null && double.TryParse(wObj.ToString(), out var w) && w >= 200)
                mediaGalleryNode.Width = w;
            if (properties.TryGetValue("Height", out var hObj) && hObj != null && double.TryParse(hObj.ToString(), out var h) && h >= 180)
                mediaGalleryNode.Height = h;
            if (properties.TryGetValue("FrameDisplayWidth", out var fdwObj) && fdwObj != null && double.TryParse(fdwObj.ToString(), out var fdw) && fdw >= 60)
                mediaGalleryNode.FrameDisplayWidth = fdw;
            if (properties.TryGetValue("FrameDisplayHeight", out var fdhObj) && fdhObj != null && double.TryParse(fdhObj.ToString(), out var fdh) && fdh >= 40)
                mediaGalleryNode.FrameDisplayHeight = fdh;
            if (properties.TryGetValue("TitleKeyTemplate", out var tktObj))
                mediaGalleryNode.TitleKeyTemplate = tktObj?.ToString() ?? "";
            if (properties.TryGetValue("ImageUrlKeyTemplate", out var iukObj))
                mediaGalleryNode.ImageUrlKeyTemplate = iukObj?.ToString() ?? "";
            if (properties.TryGetValue("VideoUrlKeyTemplate", out var vukObj))
                mediaGalleryNode.VideoUrlKeyTemplate = vukObj?.ToString() ?? "";
            if (properties.TryGetValue("GroupArrayKey", out var gakObj))
                mediaGalleryNode.GroupArrayKey = gakObj?.ToString() ?? "";
            if (properties.TryGetValue("GroupTitleKey", out var gtkObj))
                mediaGalleryNode.GroupTitleKey = gtkObj?.ToString() ?? "";
            if (properties.TryGetValue("GroupItemsKey", out var gikObj))
                mediaGalleryNode.GroupItemsKey = gikObj?.ToString() ?? "";
            if (properties.TryGetValue("FolderSaveImages", out var fsiObj))
                mediaGalleryNode.FolderSaveImages = fsiObj?.ToString() ?? "";
            if (properties.TryGetValue("FolderSourceNodeId", out var fsidObj))
                mediaGalleryNode.FolderSourceNodeId = fsidObj?.ToString();
            if (properties.TryGetValue("FolderSourceOutputKey", out var fsokObj))
                mediaGalleryNode.FolderSourceOutputKey = fsokObj?.ToString();
            if (properties.TryGetValue("FolderSaveVideos", out var fsvObj))
                mediaGalleryNode.FolderSaveVideos = fsvObj?.ToString() ?? "";
            if (properties.TryGetValue("FolderSourceNodeIdVideo", out var fsvidObj))
                mediaGalleryNode.FolderSourceNodeIdVideo = fsvidObj?.ToString();
            if (properties.TryGetValue("FolderSourceOutputKeyVideo", out var fsvokObj))
                mediaGalleryNode.FolderSourceOutputKeyVideo = fsvokObj?.ToString();
            if (properties.TryGetValue("JsonSourceNodeId", out var jsidObj))
                mediaGalleryNode.JsonSourceNodeId = jsidObj?.ToString();
            if (properties.TryGetValue("JsonSourceOutputKey", out var jsokObj))
                mediaGalleryNode.JsonSourceOutputKey = jsokObj?.ToString();
            if (properties.TryGetValue("ItemClickPreviewMode", out var icpmObj) && icpmObj != null && Enum.TryParse<ItemClickPreviewMode>(icpmObj.ToString(), out var icpm))
                mediaGalleryNode.ItemClickPreviewMode = icpm;
            if (properties.TryGetValue("DisplayMode", out var dmObj) && dmObj != null && Enum.TryParse<GalleryDisplayMode>(dmObj.ToString(), out var dm))
                mediaGalleryNode.DisplayMode = dm;
            if (properties.TryGetValue("CanReexecuteSourceNode", out var crsnObj) && crsnObj != null &&
                bool.TryParse(crsnObj.ToString(), out var crsn))
                mediaGalleryNode.CanReexecuteSourceNode = crsn;
    }

    private static void RestoreImageProcessingNodeProperties(ImageProcessingNode imageNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("Width", out var wObj) && wObj != null && double.TryParse(wObj.ToString(), out var w) && w >= 260)
                imageNode.Width = w;
            if (properties.TryGetValue("Height", out var hObj) && hObj != null && double.TryParse(hObj.ToString(), out var h) && h >= 200)
                imageNode.Height = h;

            if (properties.TryGetValue("InputMode", out var imObj) && imObj != null &&
                Enum.TryParse<ImageInputMode>(imObj.ToString(), out var im))
                imageNode.InputMode = im;

            if (properties.TryGetValue("CropMode", out var cmObj) && cmObj != null &&
                Enum.TryParse<ImageCropMode>(cmObj.ToString(), out var cropM))
                imageNode.CropMode = cropM;

            if (properties.TryGetValue("ProcessingMode", out var pmObj) && pmObj != null &&
                Enum.TryParse<ImageProcessingMode>(pmObj.ToString(), out var pm))
                imageNode.ProcessingMode = pm;

            if (properties.TryGetValue("ImageUrl", out var urlObj))
                imageNode.ImageUrl = urlObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("ImageUrlSourceNodeId", out var usnObj))
                imageNode.ImageUrlSourceNodeId = usnObj?.ToString();
            if (properties.TryGetValue("ImageUrlSourceOutputKey", out var uskObj))
                imageNode.ImageUrlSourceOutputKey = uskObj?.ToString();

            if (properties.TryGetValue("ImageBase64", out var b64Obj))
                imageNode.ImageBase64 = b64Obj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("ImageBase64SourceNodeId", out var bsnObj))
                imageNode.ImageBase64SourceNodeId = bsnObj?.ToString();
            if (properties.TryGetValue("ImageBase64SourceOutputKey", out var bskObj))
                imageNode.ImageBase64SourceOutputKey = bskObj?.ToString();

            if (properties.TryGetValue("PreferGpu", out var pgObj) && pgObj != null &&
                bool.TryParse(pgObj.ToString(), out var pg))
                imageNode.PreferGpu = pg;
            if (properties.TryGetValue("FfmpegFilter", out var ffObj))
                imageNode.FfmpegFilter = ffObj?.ToString() ?? string.Empty;

            if (properties.TryGetValue("CroppedFolderPath", out var cfpObj))
                imageNode.CroppedFolderPath = cfpObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("CroppedFolderSourceNodeId", out var cfsnObj))
                imageNode.CroppedFolderSourceNodeId = cfsnObj?.ToString();
            if (properties.TryGetValue("CroppedFolderSourceOutputKey", out var cfskObj))
                imageNode.CroppedFolderSourceOutputKey = cfskObj?.ToString();

            // Image Processor settings
            if (properties.TryGetValue("PromptSize", out var psObj) && psObj != null &&
                int.TryParse(psObj.ToString(), out var ps) && ps >= 1 && ps <= 4)
                imageNode.PromptSize = ps;
            if (properties.TryGetValue("ProcessorPrompt", out var ppObj))
                imageNode.ProcessorPrompt = ppObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("IsVerticalMode", out var ivmObj) && ivmObj != null &&
                bool.TryParse(ivmObj.ToString(), out var ivm))
                imageNode.IsVerticalMode = ivm;

            // Render node config
            if (properties.TryGetValue("RenderNodeId", out var rnObj))
                imageNode.RenderNodeId = rnObj?.ToString();
            if (properties.TryGetValue("RenderNodeOutputKey", out var rnkObj))
                imageNode.RenderNodeOutputKey = rnkObj?.ToString();
            if (properties.TryGetValue("RenderCodeIdKeys", out var rckObj))
                imageNode.RenderCodeIdKeys = rckObj?.ToString() ?? "codeId, CodeId, code_id";
            if (properties.TryGetValue("RenderImageIdKeys", out var rikObj))
                imageNode.RenderImageIdKeys = rikObj?.ToString() ?? "id, Id, ID, mediaId, imageId, assetId";
            if (properties.TryGetValue("RenderImageLinkKeys", out var rlkObj))
                imageNode.RenderImageLinkKeys = rlkObj?.ToString() ?? "linkImage, linkImg, link_image, imageUrl, url, src, link, path";

            // Return ID node config (Node nhận lại ID ảnh)
            if (properties.TryGetValue("ReturnIdNodeId", out var retnObj))
                imageNode.ReturnIdNodeId = retnObj?.ToString();
            if (properties.TryGetValue("ReturnIdOutputKey", out var retnkObj))
                imageNode.ReturnIdOutputKey = retnkObj?.ToString();
            if (properties.TryGetValue("ReturnCodeIdKeys", out var retckObj))
                imageNode.ReturnCodeIdKeys = retckObj?.ToString() ?? "codeId, CodeId, code_id";
            if (properties.TryGetValue("ReturnImageIdKeys", out var retikObj))
                imageNode.ReturnImageIdKeys = retikObj?.ToString() ?? "id, Id, ID, mediaId, imageId, assetId";
            if (properties.TryGetValue("ReturnImageLinkKeys", out var retlkObj))
                imageNode.ReturnImageLinkKeys = retlkObj?.ToString() ?? "linkImage, linkImg, link_image, imageUrl, url, src, link, path";

            // SkipOutputs
            if (properties.TryGetValue("SkipOutputs", out var soObj) && soObj != null)
            {
                try
                {
                    string? soJson = null;
                    if (soObj is string s) soJson = s;
                    else if (soObj is JsonElement je)
                        soJson = je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText();
                    if (!string.IsNullOrWhiteSpace(soJson))
                    {
                        var list = JsonSerializer.Deserialize<List<string>>(soJson);
                        if (list != null)
                        {
                            imageNode.SkipOutputs = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }
                catch { }
            }

            // Layer AI settings
            if (properties.TryGetValue("LayerAiHtmlCode", out var htmlObj))
                imageNode.LayerAiHtmlCode = htmlObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("LayerAiCssCode", out var cssObj))
                imageNode.LayerAiCssCode = cssObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("LayerAiJsCode", out var jsObj))
                imageNode.LayerAiJsCode = jsObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("LayerAiParamsCode", out var paramsObj))
                imageNode.LayerAiParamsCode = paramsObj?.ToString() ?? string.Empty;

            if (properties.TryGetValue("LayerAiInputMappings", out var laimObj) && laimObj != null)
            {
                try
                {
                    string? laimJson = laimObj is string s ? s : laimObj is JsonElement je
                        ? (je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText())
                        : null;
                    if (!string.IsNullOrWhiteSpace(laimJson))
                    {
                        var mappings = JsonSerializer.Deserialize<List<CodeInputMapping>>(laimJson);
                        if (mappings != null)
                            imageNode.LayerAiInputMappings = mappings;
                    }
                }
                catch { }
            }

            if (properties.TryGetValue("LayerAiWebUrl", out var webUrlObj))
                imageNode.LayerAiWebUrl = webUrlObj?.ToString() ?? "https://google.com";
            if (properties.TryGetValue("LayerAiCacheProfileName", out var profileObj))
                imageNode.LayerAiCacheProfileName = profileObj?.ToString() ?? "Shared";
            if (properties.TryGetValue("LayerAiWebTabsJson", out var tabsJsonObj))
                imageNode.LayerAiWebTabsJson = tabsJsonObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("LayerAiWebSplitMode", out var splitModeObj))
                imageNode.LayerAiWebSplitMode = splitModeObj?.ToString() ?? "Single";
            if (properties.TryGetValue("LayerAiActiveTab", out var activeTabObj))
                imageNode.LayerAiActiveTab = activeTabObj?.ToString() ?? "Prompt";

            if (properties.TryGetValue("LayerAiPromptHidden", out var promptHiddenObj) && promptHiddenObj != null &&
                bool.TryParse(promptHiddenObj.ToString(), out var ph))
                imageNode.LayerAiPromptHidden = ph;

            if (properties.TryGetValue("LayerAiSendModeOn", out var sendModeOnObj) && sendModeOnObj != null &&
                bool.TryParse(sendModeOnObj.ToString(), out var smo))
                imageNode.LayerAiSendModeOn = smo;

            if (properties.TryGetValue("LayerAiIsCombinedMode", out var isCombObj) && isCombObj != null &&
                bool.TryParse(isCombObj.ToString(), out var icm))
                imageNode.LayerAiIsCombinedMode = icm;

            // Deserialize danh sách vùng crop
            if (properties.TryGetValue("Crops", out var cropsObj) && cropsObj != null)
            {
                try
                {
                    string? cropsJson = null;
                    if (cropsObj is string s)
                        cropsJson = s;
                    else if (cropsObj is JsonElement je)
                    {
                        cropsJson = je.ValueKind == JsonValueKind.String
                            ? je.GetString()
                            : je.GetRawText();
                    }

                    if (!string.IsNullOrWhiteSpace(cropsJson))
                    {
                        var cropsList = JsonSerializer.Deserialize<List<JsonElement>>(cropsJson);
                        if (cropsList != null)
                        {
                            imageNode.Crops.Clear();
                            foreach (var cropEl in cropsList)
                            {
                                var region = new Models.Nodes.ImageCropRegion();

                                if (cropEl.TryGetProperty("Id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                                    region.Id = idEl.GetString() ?? region.Id;

                                if (cropEl.TryGetProperty("ColorHex", out var chEl) && chEl.ValueKind == JsonValueKind.String)
                                {
                                    var hex = chEl.GetString();
                                    if (!string.IsNullOrWhiteSpace(hex))
                                        region.ColorHex = hex;
                                }

                                if (cropEl.TryGetProperty("IsVisible", out var ivEl) && ivEl.ValueKind == JsonValueKind.True || (cropEl.TryGetProperty("IsVisible", out ivEl) && ivEl.ValueKind == JsonValueKind.False))
                                    region.IsVisible = ivEl.GetBoolean();

                                if (cropEl.TryGetProperty("IsOutlineOnly", out var ioEl) && (ioEl.ValueKind == JsonValueKind.True || ioEl.ValueKind == JsonValueKind.False))
                                    region.IsOutlineOnly = ioEl.GetBoolean();

                                if (cropEl.TryGetProperty("SavedPath", out var spEl) && spEl.ValueKind == JsonValueKind.String)
                                    region.SavedPath = spEl.GetString();

                                if (cropEl.TryGetProperty("CropName", out var cnEl) && cnEl.ValueKind == JsonValueKind.String)
                                    region.CropName = cnEl.GetString() ?? string.Empty;

                                if (cropEl.TryGetProperty("LastExecutionId", out var leiEl) && leiEl.ValueKind == JsonValueKind.String)
                                    region.LastExecutionId = leiEl.GetString();

                                // Khôi phục Order
                                if (cropEl.TryGetProperty("Order", out var orderEl) && orderEl.TryGetInt32(out var orderVal))
                                    region.Order = orderVal;

                                // Khôi phục điểm polygon
                                if (cropEl.TryGetProperty("Points", out var ptEl) && ptEl.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var ptItem in ptEl.EnumerateArray())
                                    {
                                        if (ptItem.ValueKind == JsonValueKind.Array)
                                        {
                                            var arr = ptItem.EnumerateArray().ToList();
                                            if (arr.Count >= 2 &&
                                                arr[0].TryGetDouble(out var px) &&
                                                arr[1].TryGetDouble(out var py))
                                            {
                                                region.Points.Add(new System.Windows.Point(px, py));
                                            }
                                        }
                                    }
                                }

                                // Cập nhật BoundingBox từ Points
                                if (region.Points.Count > 0)
                                {
                                    var minX = region.Points.Min(p => p.X);
                                    var maxX = region.Points.Max(p => p.X);
                                    var minY = region.Points.Min(p => p.Y);
                                    var maxY = region.Points.Max(p => p.Y);
                                    region.BoundingBox = new System.Windows.Rect(minX, minY,
                                        Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
                                }

                                imageNode.Crops.Add(region);
                            }
                        }
                    }
                }
                catch { /* Không crash khi đọc crops - bỏ qua nếu lỗi format */ }
            }

    }

    private static void RestoreVideoProcessingNodeProperties(VideoProcessingNode videoNode, Dictionary<string, object> properties)
    {
            if (properties.TryGetValue("Width", out var wObj) && wObj != null && double.TryParse(wObj.ToString(), out var w) && w >= 540)
                videoNode.Width = w;
            if (properties.TryGetValue("Height", out var hObj) && hObj != null && double.TryParse(hObj.ToString(), out var h) && h >= 340)
                videoNode.Height = h;

            if (properties.TryGetValue("VideoSourceNodeId", out var vsnObj))
                videoNode.VideoSourceNodeId = vsnObj?.ToString();
            if (properties.TryGetValue("VideoSourceOutputKey", out var vskObj))
                videoNode.VideoSourceOutputKey = vskObj?.ToString();
            if (properties.TryGetValue("VideoPath", out var vpObj))
                videoNode.VideoPath = vpObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("OutputFolderSourceNodeId", out var fsnObj))
                videoNode.OutputFolderSourceNodeId = fsnObj?.ToString();
            if (properties.TryGetValue("OutputFolderSourceOutputKey", out var fskObj))
                videoNode.OutputFolderSourceOutputKey = fskObj?.ToString();
            if (properties.TryGetValue("VideoOutputFolderSourceNodeId", out var vosnObj))
                videoNode.VideoOutputFolderSourceNodeId = vosnObj?.ToString();
            if (properties.TryGetValue("VideoOutputFolderSourceOutputKey", out var voskObj))
                videoNode.VideoOutputFolderSourceOutputKey = voskObj?.ToString();
            if (properties.TryGetValue("AudioOutputFolderSourceNodeId", out var aosnObj))
                videoNode.AudioOutputFolderSourceNodeId = aosnObj?.ToString();
            if (properties.TryGetValue("AudioOutputFolderSourceOutputKey", out var aoskObj))
                videoNode.AudioOutputFolderSourceOutputKey = aoskObj?.ToString();

            if (properties.TryGetValue("OutputBase64", out var obObj) && obObj != null && bool.TryParse(obObj.ToString(), out var ob))
                videoNode.OutputBase64 = ob;
            if (properties.TryGetValue("UseDialogVideoConfig", out var udvcObj) && udvcObj != null && bool.TryParse(udvcObj.ToString(), out var udvc))
                videoNode.UseDialogVideoConfig = udvc;
            if (properties.TryGetValue("ExtractFramesEnabled", out var efeObj) && efeObj != null && bool.TryParse(efeObj.ToString(), out var efe))
                videoNode.ExtractFramesEnabled = efe;
            if (properties.TryGetValue("ExportVideoEnabled", out var eveObj) && eveObj != null && bool.TryParse(eveObj.ToString(), out var eve))
                videoNode.ExportVideoEnabled = eve;
            if (properties.TryGetValue("ExtractAudioEnabled", out var eaeObj) && eaeObj != null && bool.TryParse(eaeObj.ToString(), out var eae))
                videoNode.ExtractAudioEnabled = eae;
            if (properties.TryGetValue("FrameOutputFolderPath", out var fofpObj))
                videoNode.FrameOutputFolderPath = fofpObj?.ToString();
            if (properties.TryGetValue("DefaultOutputVideoPath", out var dovpObj))
                videoNode.DefaultOutputVideoPath = dovpObj?.ToString();
            if (properties.TryGetValue("AudioOutputFolderPath", out var aofpObj))
                videoNode.AudioOutputFolderPath = aofpObj?.ToString();
            if (properties.TryGetValue("SecondsPerFrame", out var spfObj) && spfObj != null && double.TryParse(spfObj.ToString(), out var spf))
                videoNode.SecondsPerFrame = spf;
            if (properties.TryGetValue("ExtractFrameCount", out var efcObj) && efcObj != null && int.TryParse(efcObj.ToString(), out var efc))
                videoNode.ExtractFrameCount = efc;
            if (properties.TryGetValue("PreferGpu", out var pgObj) && pgObj != null && bool.TryParse(pgObj.ToString(), out var pg))
                videoNode.PreferGpu = pg;
            if (properties.TryGetValue("PreferredHwAccel", out var phaObj))
                videoNode.PreferredHwAccel = phaObj?.ToString() ?? "none";

            if (properties.TryGetValue("SourceFps", out var sfObj) && sfObj != null && double.TryParse(sfObj.ToString(), out var sf))
                videoNode.SourceFps = sf;
            if (properties.TryGetValue("ExtractFps", out var efObj) && efObj != null && double.TryParse(efObj.ToString(), out var ef))
                videoNode.ExtractFps = ef;

            if (properties.TryGetValue("ExtractByFpsEnabled", out var ebfeObj) && ebfeObj != null && bool.TryParse(ebfeObj.ToString(), out var ebfe))

                videoNode.ExtractByFpsEnabled = ebfe;
            if (properties.TryGetValue("ExcludedFrameTimestamps", out var exftObj) && exftObj != null)
            {
                try
                {
                    var json = exftObj.ToString();
                    if (!string.IsNullOrWhiteSpace(json))
                        videoNode.ExcludedFrameTimestamps = JsonSerializer.Deserialize<System.Collections.Generic.List<double>>(json!) ?? new();
                }
                catch { /* ignore invalid JSON */ }
            }
            if (properties.TryGetValue("Brightness", out var brObj) && brObj != null && double.TryParse(brObj.ToString(), out var br))
                videoNode.Brightness = br;
            if (properties.TryGetValue("Contrast", out var ctObj) && ctObj != null && double.TryParse(ctObj.ToString(), out var ct))
                videoNode.Contrast = ct;
            if (properties.TryGetValue("Saturation", out var stObj) && stObj != null && double.TryParse(stObj.ToString(), out var st))
                videoNode.Saturation = st;
            if (properties.TryGetValue("Hue", out var huObj) && huObj != null && double.TryParse(huObj.ToString(), out var hu))
                videoNode.Hue = hu;
            if (properties.TryGetValue("Gamma", out var gmObj) && gmObj != null && double.TryParse(gmObj.ToString(), out var gm))
                videoNode.Gamma = gm;
            if (properties.TryGetValue("SharpenEnabled", out var seObj) && seObj != null && bool.TryParse(seObj.ToString(), out var se))
                videoNode.SharpenEnabled = se;
            if (properties.TryGetValue("SharpenStrength", out var ssObj) && ssObj != null && double.TryParse(ssObj.ToString(), out var ss))
                videoNode.SharpenStrength = ss;
            if (properties.TryGetValue("DenoiseEnabled", out var deObj) && deObj != null && bool.TryParse(deObj.ToString(), out var de))
                videoNode.DenoiseEnabled = de;
            if (properties.TryGetValue("DenoiseStrength", out var dsObj) && dsObj != null && double.TryParse(dsObj.ToString(), out var ds))
                videoNode.DenoiseStrength = ds;
            if (properties.TryGetValue("BlurEnabled", out var beObj) && beObj != null && bool.TryParse(beObj.ToString(), out var be))
                videoNode.BlurEnabled = be;
            if (properties.TryGetValue("BlurRadius", out var brdObj) && brdObj != null && double.TryParse(brdObj.ToString(), out var brd))
                videoNode.BlurRadius = brd;
            if (properties.TryGetValue("StabilizeEnabled", out var stabEnabledObj) && stabEnabledObj != null && bool.TryParse(stabEnabledObj.ToString(), out var stabEnabledVal))
                videoNode.StabilizeEnabled = stabEnabledVal;
            if (properties.TryGetValue("SpeedFactor", out var spdObj) && spdObj != null && double.TryParse(spdObj.ToString(), out var spd))
                videoNode.SpeedFactor = spd;
            if (properties.TryGetValue("RotationDegrees", out var rotObj) && rotObj != null && double.TryParse(rotObj.ToString(), out var rot))
                videoNode.RotationDegrees = rot;
            if (properties.TryGetValue("FlipH", out var flipHObj) && flipHObj != null && bool.TryParse(flipHObj.ToString(), out var flipHVal))
                videoNode.FlipH = flipHVal;
            if (properties.TryGetValue("FlipV", out var flipVObj) && flipVObj != null && bool.TryParse(flipVObj.ToString(), out var flipVVal))
                videoNode.FlipV = flipVVal;
            if (properties.TryGetValue("OutputFormat", out var ofObj))
                videoNode.OutputFormat = ofObj?.ToString() ?? "mp4_h264";
            if (properties.TryGetValue("EncoderPreset", out var epObj))
                videoNode.EncoderPreset = epObj?.ToString() ?? "medium";
            if (properties.TryGetValue("Crf", out var crfObj) && crfObj != null && double.TryParse(crfObj.ToString(), out var crf))
                videoNode.Crf = crf;
            if (properties.TryGetValue("ResolutionScale", out var rsObj) && rsObj != null && double.TryParse(rsObj.ToString(), out var rs))
                videoNode.ResolutionScale = rs;
            if (properties.TryGetValue("FrameResizeScale", out var frsObj) && frsObj != null && double.TryParse(frsObj.ToString(), out var frs))
                videoNode.FrameResizeScale = frs;
            if (properties.TryGetValue("TrimEnabled", out var teObj) && teObj != null && bool.TryParse(teObj.ToString(), out var te))
                videoNode.TrimEnabled = te;
            if (properties.TryGetValue("TrimStartSec", out var tssObj) && tssObj != null && double.TryParse(tssObj.ToString(), out var tss))
                videoNode.TrimStartSec = tss;
            if (properties.TryGetValue("TrimEndSec", out var tesObj) && tesObj != null && double.TryParse(tesObj.ToString(), out var tes))
                videoNode.TrimEndSec = tes;
            if (properties.TryGetValue("ConcatEnabled", out var ceObj) && ceObj != null && bool.TryParse(ceObj.ToString(), out var ce))
                videoNode.ConcatEnabled = ce;
            if (properties.TryGetValue("ConcatVideos", out var cvObj) && cvObj != null)
            {
                videoNode.ConcatVideos.Clear();
                if (cvObj is JsonElement cvElem && cvElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in cvElem.EnumerateArray())
                    {
                        var path = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("SourcePath", out var pProp) ? pProp.GetString() : item.GetString();
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            videoNode.ConcatVideos.Add(new VideoConcatItemConfig { SourcePath = path });
                        }
                    }
                }
            }
            if (properties.TryGetValue("OutputPathOverride", out var opoObj))
                videoNode.OutputPathOverride = opoObj?.ToString();
            if (properties.TryGetValue("SourceAudioEnabled", out var saeObj) && saeObj != null && bool.TryParse(saeObj.ToString(), out var sae))
                videoNode.SourceAudioEnabled = sae;
            if (properties.TryGetValue("SourceAudioVolumePercent", out var savpObj) && savpObj != null && double.TryParse(savpObj.ToString(), out var savp))
                videoNode.SourceAudioVolumePercent = savp;
            if (properties.TryGetValue("AudioFadeInSec", out var afiObj) && afiObj != null && double.TryParse(afiObj.ToString(), out var afi))
                videoNode.AudioFadeInSec = afi;
            if (properties.TryGetValue("AudioFadeOutSec", out var afoObj) && afoObj != null && double.TryParse(afoObj.ToString(), out var afo))
                videoNode.AudioFadeOutSec = afo;
            if (properties.TryGetValue("AudioNormalizeEnabled", out var aneObj) && aneObj != null && bool.TryParse(aneObj.ToString(), out var ane))
                videoNode.AudioNormalizeEnabled = ane;
            if (properties.TryGetValue("AudioTargetLufs", out var atlObj) && atlObj != null && double.TryParse(atlObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var atl))
                videoNode.AudioTargetLufs = atl;
            if (properties.TryGetValue("AudioDenoiseEnabled", out var adeObj) && adeObj != null && bool.TryParse(adeObj.ToString(), out var ade))
                videoNode.AudioDenoiseEnabled = ade;
            if (properties.TryGetValue("AudioSpeedFactor", out var asfObj) && asfObj != null && double.TryParse(asfObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var asf))
                videoNode.AudioSpeedFactor = asf;
            if (properties.TryGetValue("AudioEqPreset", out var aepObj))
                videoNode.AudioEqPreset = aepObj?.ToString() ?? "neutral";
            if (properties.TryGetValue("AudioBassGain", out var abgObj) && abgObj != null && double.TryParse(abgObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var abg))
                videoNode.AudioBassGain = abg;
            if (properties.TryGetValue("AudioLowMidGain", out var almgObj) && almgObj != null && double.TryParse(almgObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var almg))
                videoNode.AudioLowMidGain = almg;
            if (properties.TryGetValue("AudioMidGain", out var amgObj) && amgObj != null && double.TryParse(amgObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var amg))
                videoNode.AudioMidGain = amg;
            if (properties.TryGetValue("AudioHighMidGain", out var ahmgObj) && ahmgObj != null && double.TryParse(ahmgObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ahmg))
                videoNode.AudioHighMidGain = ahmg;
            if (properties.TryGetValue("AudioTrebleGain", out var atgObj) && atgObj != null && double.TryParse(atgObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var atg))
                videoNode.AudioTrebleGain = atg;
            if (properties.TryGetValue("AudioToneClarity", out var atcObj) && atcObj != null && double.TryParse(atcObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var atc))
                videoNode.AudioToneClarity = atc;
            if (properties.TryGetValue("AudioHighpassFilter", out var ahfObj) && ahfObj != null && bool.TryParse(ahfObj.ToString(), out var ahf))
                videoNode.AudioHighpassFilter = ahf;
            if (properties.TryGetValue("AudioHighpassCutoffHz", out var ahcObj) && ahcObj != null && double.TryParse(ahcObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ahc))
                videoNode.AudioHighpassCutoffHz = ahc;
            if (properties.TryGetValue("AudioLowpassFilter", out var alfObj) && alfObj != null && bool.TryParse(alfObj.ToString(), out var alf))
                videoNode.AudioLowpassFilter = alf;
            if (properties.TryGetValue("AudioLowpassCutoffHz", out var alcObj) && alcObj != null && double.TryParse(alcObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var alc))
                videoNode.AudioLowpassCutoffHz = alc;
            if (properties.TryGetValue("AudioStereoWidthPercent", out var aswObj) && aswObj != null && double.TryParse(aswObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var asw))
                videoNode.AudioStereoWidthPercent = asw;
            if (properties.TryGetValue("AudioWarmthPercent", out var awpObj) && awpObj != null && double.TryParse(awpObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var awp))
                videoNode.AudioWarmthPercent = awp;
            if (properties.TryGetValue("AudioReverbPercent", out var arpObj) && arpObj != null && double.TryParse(arpObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var arp))
                videoNode.AudioReverbPercent = arp;
            if (properties.TryGetValue("AudioVocalBalance", out var avbObj) && avbObj != null && double.TryParse(avbObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var avb))
                videoNode.AudioVocalBalance = avb;
            if (properties.TryGetValue("AudioPitchSemitones", out var apsObj) && apsObj != null && double.TryParse(apsObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var aps))
                videoNode.AudioPitchSemitones = aps;
            if (properties.TryGetValue("AudioEchoEnabled", out var aeeObj) && aeeObj != null && bool.TryParse(aeeObj.ToString(), out var aee))
                videoNode.AudioEchoEnabled = aee;
            if (properties.TryGetValue("AudioEchoDelayMs", out var aedObj) && aedObj != null && double.TryParse(aedObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var aed))
                videoNode.AudioEchoDelayMs = aed;
            if (properties.TryGetValue("AudioEchoFeedbackPercent", out var aefbObj) && aefbObj != null && double.TryParse(aefbObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var aefb))
                videoNode.AudioEchoFeedbackPercent = aefb;
            if (properties.TryGetValue("AudioEchoMixPercent", out var aemObj) && aemObj != null && double.TryParse(aemObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var aem))
                videoNode.AudioEchoMixPercent = aem;
            if (properties.TryGetValue("Audio8DEnabled", out var a8eObj) && a8eObj != null && bool.TryParse(a8eObj.ToString(), out var a8e))
                videoNode.Audio8DEnabled = a8e;
            if (properties.TryGetValue("Audio8DSpeedHz", out var a8sObj) && a8sObj != null && double.TryParse(a8sObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var a8s))
                videoNode.Audio8DSpeedHz = a8s;
            if (properties.TryGetValue("AudioRobotVoiceEnabled", out var arvObj) && arvObj != null && bool.TryParse(arvObj.ToString(), out var arv))
                videoNode.AudioRobotVoiceEnabled = arv;
            if (properties.TryGetValue("AudioRadioVoiceEnabled", out var aradObj) && aradObj != null && bool.TryParse(aradObj.ToString(), out var arad))
                videoNode.AudioRadioVoiceEnabled = arad;
            if (properties.TryGetValue("AudioChorusEnabled", out var achObj) && achObj != null && bool.TryParse(achObj.ToString(), out var ach))
                videoNode.AudioChorusEnabled = ach;
            if (properties.TryGetValue("AudioChorusMixPercent", out var achmObj) && achmObj != null && double.TryParse(achmObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var achm))
                videoNode.AudioChorusMixPercent = achm;
            if (properties.TryGetValue("AudioCompressorPercent", out var acpObj) && acpObj != null && double.TryParse(acpObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var acp))
                videoNode.AudioCompressorPercent = acp;
            if (properties.TryGetValue("AudioDeEsserPercent", out var adpObj) && adpObj != null && double.TryParse(adpObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var adp))
                videoNode.AudioDeEsserPercent = adp;
            if (properties.TryGetValue("AudioNoiseGatePercent", out var angObj) && angObj != null && double.TryParse(angObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ang))
                videoNode.AudioNoiseGatePercent = ang;
            if (properties.TryGetValue("AudioWaveShaperEnabled", out var awseObj) && awseObj != null && bool.TryParse(awseObj.ToString(), out var awse))
                videoNode.AudioWaveShaperEnabled = awse;
            if (properties.TryGetValue("AudioWaveShaperCurve", out var awscObj))
                videoNode.AudioWaveShaperCurve = awscObj?.ToString() ?? "clean";
            if (properties.TryGetValue("AudioWaveShaperDrivePercent", out var awsdObj) && awsdObj != null && double.TryParse(awsdObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var awsd))
                videoNode.AudioWaveShaperDrivePercent = awsd;
            if (properties.TryGetValue("AudioTransientPunchPercent", out var atpObj) && atpObj != null && double.TryParse(atpObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var atp))
                videoNode.AudioTransientPunchPercent = atp;
            if (properties.TryGetValue("AudioSubHarmonicsPercent", out var ashObj) && ashObj != null && double.TryParse(ashObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ash))
                videoNode.AudioSubHarmonicsPercent = ash;
            if (properties.TryGetValue("AudioHarmonicExciterPercent", out var aheObj) && aheObj != null && double.TryParse(aheObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ahe))
                videoNode.AudioHarmonicExciterPercent = ahe;
            if (properties.TryGetValue("AudioPhaseInvertLeft", out var apilObj) && apilObj != null && bool.TryParse(apilObj.ToString(), out var apil))
                videoNode.AudioPhaseInvertLeft = apil;
            if (properties.TryGetValue("AudioPhaseInvertRight", out var apirObj) && apirObj != null && bool.TryParse(apirObj.ToString(), out var apir))
                videoNode.AudioPhaseInvertRight = apir;
            if (properties.TryGetValue("VoiceChangerEnabled", out var vceObj) && vceObj != null && bool.TryParse(vceObj.ToString(), out var vce))
                videoNode.VoiceChangerEnabled = vce;
            if (properties.TryGetValue("EqualizerFxEnabled", out var eqFeObj) && eqFeObj != null && bool.TryParse(eqFeObj.ToString(), out var eqFeVal))
                videoNode.EqualizerFxEnabled = eqFeVal;
            if (properties.TryGetValue("DynamicsMasteringEnabled", out var dmeObj) && dmeObj != null && bool.TryParse(dmeObj.ToString(), out var dme))
                videoNode.DynamicsMasteringEnabled = dme;
            if (properties.TryGetValue("MultiTrackBgmEnabled", out var mtbeObj) && mtbeObj != null && bool.TryParse(mtbeObj.ToString(), out var mtbe))
                videoNode.MultiTrackBgmEnabled = mtbe;
            if (properties.TryGetValue("AudioExportConfigEnabled", out var aeceObj) && aeceObj != null && bool.TryParse(aeceObj.ToString(), out var aece))
                videoNode.AudioExportConfigEnabled = aece;
            if (properties.TryGetValue("AudioTrimEnabled", out var ateObj) && ateObj != null && bool.TryParse(ateObj.ToString(), out var ate))
                videoNode.AudioTrimEnabled = ate;
            if (properties.TryGetValue("AudioTrimStartSec", out var atssObj) && atssObj != null && double.TryParse(atssObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var atss))
                videoNode.AudioTrimStartSec = atss;
            if (properties.TryGetValue("AudioTrimEndSec", out var atseObj) && atseObj != null && double.TryParse(atseObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var atse))
                videoNode.AudioTrimEndSec = atse;
            if (properties.TryGetValue("AudioExportFormat", out var aefObj))
                videoNode.AudioExportFormat = aefObj?.ToString() ?? "mp3";
            if (properties.TryGetValue("AudioExportBitrate", out var aebObj))
                videoNode.AudioExportBitrate = aebObj?.ToString() ?? "320k";
            if (properties.TryGetValue("AudioExportSampleRate", out var aesrObj))
                videoNode.AudioExportSampleRate = aesrObj?.ToString() ?? "48000";
            if (properties.TryGetValue("AudioExportChannels", out var aecObj))
                videoNode.AudioExportChannels = aecObj?.ToString() ?? "stereo";
            if (properties.TryGetValue("PreviewVolume", out var pvObj) && pvObj != null && double.TryParse(pvObj.ToString(), out var pv))
                videoNode.PreviewVolume = pv;
            if (properties.TryGetValue("PreviewQualityMode", out var pqmObj))
                videoNode.PreviewQualityMode = pqmObj?.ToString() ?? "normal";
            if (properties.TryGetValue("PreviewVisualStrengthMode", out var pvsmObj))
                videoNode.PreviewVisualStrengthMode = pvsmObj?.ToString() ?? "balanced";
            if (properties.TryGetValue("WatermarkEnabled", out var wmeObj) && wmeObj != null && bool.TryParse(wmeObj.ToString(), out var wme))
                videoNode.WatermarkEnabled = wme;
            if (properties.TryGetValue("WatermarkImagePath", out var wmipObj))
                videoNode.WatermarkImagePath = wmipObj?.ToString();
            if (properties.TryGetValue("WatermarkPosition", out var wmpObj))
                videoNode.WatermarkPosition = wmpObj?.ToString() ?? "BR";
            if (properties.TryGetValue("WatermarkOpacity", out var wmoObj) && wmoObj != null && double.TryParse(wmoObj.ToString(), out var wmo))
                videoNode.WatermarkOpacity = wmo;
            if (properties.TryGetValue("WatermarkPaddingPx", out var wmpxObj) && wmpxObj != null && int.TryParse(wmpxObj.ToString(), out var wmpx))
                videoNode.WatermarkPaddingPx = wmpx;
            if (properties.TryGetValue("WatermarkWidthFraction", out var wwObj) && wwObj != null && double.TryParse(wwObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ww))
                videoNode.WatermarkWidthFraction = ww;
            if (properties.TryGetValue("WatermarkInsetFraction", out var wiObj) && wiObj != null && double.TryParse(wiObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var wi))
                videoNode.WatermarkInsetFraction = wi;
            if (properties.TryGetValue("TextOverlayEnabled", out var toeObj) && toeObj != null && bool.TryParse(toeObj.ToString(), out var toe))
                videoNode.TextOverlayEnabled = toe;
            if (properties.TryGetValue("OverlayText", out var otObj))
                videoNode.OverlayText = otObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("OverlayFont", out var ofnObj))
                videoNode.OverlayFont = ofnObj?.ToString() ?? "Arial";
            if (properties.TryGetValue("OverlayFontSize", out var ofsObj) && ofsObj != null && int.TryParse(ofsObj.ToString(), out var ofs))
                videoNode.OverlayFontSize = ofs;
            if (properties.TryGetValue("OverlayFontColor", out var ofcObj))
                videoNode.OverlayFontColor = ofcObj?.ToString() ?? "white";
            if (properties.TryGetValue("TextPosition", out var tpObj))
                videoNode.TextPosition = tpObj?.ToString() ?? "BC";
            if (properties.TryGetValue("FrameLabelEnabled", out var fleObj) && fleObj != null && bool.TryParse(fleObj.ToString(), out var fle))
                videoNode.FrameLabelEnabled = fle;
            if (properties.TryGetValue("FrameLabelDebugSamplesEnabled", out var fldbgObj) && fldbgObj != null && bool.TryParse(fldbgObj.ToString(), out var fldbg))
                videoNode.FrameLabelDebugSamplesEnabled = fldbg;
            if (properties.TryGetValue("FrameLabelTemplate", out var fltObj))
                videoNode.FrameLabelTemplate = fltObj?.ToString() ?? "Frame {index} - {time}";
            if (properties.TryGetValue("FrameLabelTextColor", out var fltcObj))
                videoNode.FrameLabelTextColor = fltcObj?.ToString() ?? "black";
            if (properties.TryGetValue("FrameLabelBackgroundColor", out var flbcObj))
                videoNode.FrameLabelBackgroundColor = flbcObj?.ToString() ?? "white";
            if (properties.TryGetValue("FrameLabelFontSize", out var flfsObj) && flfsObj != null && int.TryParse(flfsObj.ToString(), out var flfs))
                videoNode.FrameLabelFontSize = flfs;
            if (properties.TryGetValue("FrameLabelX", out var flxObj) && flxObj != null && double.TryParse(flxObj.ToString(), out var flx))
                videoNode.FrameLabelX = flx;
            if (properties.TryGetValue("FrameLabelY", out var flyObj) && flyObj != null && double.TryParse(flyObj.ToString(), out var fly))
                videoNode.FrameLabelY = fly;
            if (properties.TryGetValue("FrameLabelW", out var flwObj) && flwObj != null && double.TryParse(flwObj.ToString(), out var flw))
                videoNode.FrameLabelW = flw;
            if (properties.TryGetValue("FrameLabelH", out var flhObj) && flhObj != null && double.TryParse(flhObj.ToString(), out var flh))
                videoNode.FrameLabelH = flh;
            if (properties.TryGetValue("FrameLabelHorizontalPadding", out var flhpObj) && flhpObj != null && int.TryParse(flhpObj.ToString(), out var flhp))
                videoNode.FrameLabelHorizontalPadding = flhp;
            if (properties.TryGetValue("FrameLabelVerticalPadding", out var flvpObj) && flvpObj != null && int.TryParse(flvpObj.ToString(), out var flvp))
                videoNode.FrameLabelVerticalPadding = flvp;
            if (properties.TryGetValue("FrameLabelTimeFormat", out var fltfObj))
                videoNode.FrameLabelTimeFormat = fltfObj?.ToString() ?? "MMSS";
            if (properties.TryGetValue("ExtractParallelJobs", out var epjObj) && epjObj != null && int.TryParse(epjObj.ToString(), out var epj))
                videoNode.ExtractParallelJobs = epj;
            if (properties.TryGetValue("FrameOutputFormat", out var fofObj))
                videoNode.FrameOutputFormat = fofObj?.ToString() ?? "png";
            if (properties.TryGetValue("JpegQuality", out var jqObj) && jqObj != null && int.TryParse(jqObj.ToString(), out var jq))
                videoNode.JpegQuality = jq;
            if (properties.TryGetValue("ExtractAllFrames", out var eafObj) && eafObj != null && bool.TryParse(eafObj.ToString(), out var eaf))
                videoNode.ExtractAllFrames = eaf;
            if (properties.TryGetValue("TwoPassEnabled", out var tpeObj) && tpeObj != null && bool.TryParse(tpeObj.ToString(), out var tpe))
                videoNode.TwoPassEnabled = tpe;
            if (properties.TryGetValue("AudioCodec", out var acObj))
                videoNode.AudioCodec = acObj?.ToString() ?? "aac";
            if (properties.TryGetValue("AudioBitrate", out var abrObj))
                videoNode.AudioBitrate = abrObj?.ToString() ?? "192k";
            if (properties.TryGetValue("SubtitlePath", out var subObj))
                videoNode.SubtitlePath = subObj?.ToString();
            if (properties.TryGetValue("BurnSubtitleEnabled", out var bseObj) && bseObj != null && bool.TryParse(bseObj.ToString(), out var bse))
                videoNode.BurnSubtitleEnabled = bse;

            if (properties.TryGetValue("GridCollageEnabled", out var gceObj) && gceObj != null && bool.TryParse(gceObj.ToString(), out var gce))
                videoNode.GridCollageEnabled = gce;
            if (properties.TryGetValue("GridCollageWidth", out var gcwObj) && gcwObj != null && int.TryParse(gcwObj.ToString(), out var gcw))
                videoNode.GridCollageWidth = gcw;
            if (properties.TryGetValue("GridCollageHeight", out var gchObj) && gchObj != null && int.TryParse(gchObj.ToString(), out var gch))
                videoNode.GridCollageHeight = gch;
            if (properties.TryGetValue("GridCollageFrameCount", out var gcfcObj) && gcfcObj != null && int.TryParse(gcfcObj.ToString(), out var gcfc))
                videoNode.GridCollageFrameCount = gcfc;
            if (properties.TryGetValue("GridCollageBackgroundColor", out var gcbcObj))
                videoNode.GridCollageBackgroundColor = gcbcObj?.ToString() ?? "white";
            if (properties.TryGetValue("GridCollageColorKey", out var gcckObj))
                videoNode.GridCollageColorKey = gcckObj?.ToString() ?? "white";
            if (properties.TryGetValue("GridCollagePadding", out var gcpObj))
                videoNode.GridCollagePadding = gcpObj?.ToString() ?? "10";
            if (properties.TryGetValue("GridCollageMargin", out var gcmObj))
                videoNode.GridCollageMargin = gcmObj?.ToString() ?? "0";
            if (properties.TryGetValue("GridCollageAspectMode", out var gcamObj))
                videoNode.GridCollageAspectMode = gcamObj?.ToString() ?? "auto";
            if (properties.TryGetValue("GridCollageShowFrameIndex", out var gcsfiObj) && gcsfiObj != null && bool.TryParse(gcsfiObj.ToString(), out var gcsfi))
                videoNode.GridCollageShowFrameIndex = gcsfi;

            if (properties.TryGetValue("AudioTracks", out var atObj) && atObj != null)
            {
                try
                {
                    string? atJson = atObj is string s ? s : atObj is JsonElement je
                        ? (je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText())
                        : null;
                    if (!string.IsNullOrWhiteSpace(atJson))
                    {
                        var tracks = JsonSerializer.Deserialize<List<VideoAudioTrackConfig>>(atJson);
                        if (tracks != null)
                        {
                            videoNode.AudioTracks.Clear();
                            foreach (var t in tracks) videoNode.AudioTracks.Add(t);
                        }
                    }
                }
                catch { }
            }

            if (properties.TryGetValue("Overlays", out var ovObj) && ovObj != null)
            {
                try
                {
                    string? ovJson = ovObj is string s ? s : ovObj is JsonElement je
                        ? (je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText())
                        : null;
                    if (!string.IsNullOrWhiteSpace(ovJson))
                    {
                        var overlays = JsonSerializer.Deserialize<List<OverlayItem>>(ovJson);
                        if (overlays != null)
                        {
                            videoNode.Overlays.Clear();
                            foreach (var o in overlays) videoNode.Overlays.Add(o);
                        }
                    }
                }
                catch { }
            }

            if (properties.TryGetValue("Subtitles", out var subtitlesObj) && subtitlesObj != null)
            {
                try
                {
                    string? subJson = subtitlesObj is string s ? s : subtitlesObj is JsonElement je
                        ? (je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText())
                        : null;
                    if (!string.IsNullOrWhiteSpace(subJson))
                    {
                        var subs = JsonSerializer.Deserialize<List<SubtitleItem>>(subJson);
                        if (subs != null)
                        {
                            videoNode.Subtitles.Clear();
                            foreach (var item in subs) videoNode.Subtitles.Add(item);
                        }
                    }
                }
                catch { }
            }

            if (properties.TryGetValue("SubtitleStyle", out var subStyleObj) && subStyleObj != null)
            {
                try
                {
                    string? styleJson = subStyleObj is string s ? s : subStyleObj is JsonElement je
                        ? (je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText())
                        : null;
                    if (!string.IsNullOrWhiteSpace(styleJson))
                    {
                        var style = JsonSerializer.Deserialize<SubtitleStyleConfig>(styleJson);
                        if (style != null) videoNode.SubtitleStyle = style;
                    }
                }
                catch { }
            }

            if (properties.TryGetValue("DubbingClips", out var dubObj) && dubObj != null)
            {
                try
                {
                    string? dubJson = dubObj is string s ? s : dubObj is JsonElement je
                        ? (je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText())
                        : null;
                    if (!string.IsNullOrWhiteSpace(dubJson))
                    {
                        var clips = JsonSerializer.Deserialize<List<DubbingClipItem>>(dubJson);
                        if (clips != null)
                        {
                            videoNode.DubbingClips.Clear();
                            foreach (var c in clips) videoNode.DubbingClips.Add(c);
                        }
                    }
                }
                catch { }
            }

            if (properties.TryGetValue("AutoDucking", out var duckObj) && duckObj != null)
            {
                try
                {
                    string? duckJson = duckObj is string s ? s : duckObj is JsonElement je
                        ? (je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText())
                        : null;
                    if (!string.IsNullOrWhiteSpace(duckJson))
                    {
                        var duck = JsonSerializer.Deserialize<AutoDuckingConfig>(duckJson);
                        if (duck != null) videoNode.AutoDucking = duck;
                    }
                }
                catch { }
            }

            // Subtitle AI & Dubbing Workflow Properties
            if (properties.TryGetValue("SubtitleSplitMode", out var ssmObj))
                videoNode.SubtitleSplitMode = ssmObj?.ToString() ?? "Duration";
            if (properties.TryGetValue("SubtitleChunkDurationSec", out var scdObj) && scdObj != null && double.TryParse(scdObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scd))
                videoNode.SubtitleChunkDurationSec = scd;
            if (properties.TryGetValue("SubtitleChunkCount", out var sccObj) && sccObj != null && int.TryParse(sccObj.ToString(), out var scc))
                videoNode.SubtitleChunkCount = scc;
            if (properties.TryGetValue("SubtitleEnableSmartSilenceSplit", out var sessObj) && sessObj != null && bool.TryParse(sessObj.ToString(), out var sess))
                videoNode.SubtitleEnableSmartSilenceSplit = sess;
            if (properties.TryGetValue("SubtitleSilenceThresholdDb", out var sstObj) && sstObj != null && double.TryParse(sstObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sst))
                videoNode.SubtitleSilenceThresholdDb = sst;
            if (properties.TryGetValue("SubtitleMinSilenceSec", out var smsObj) && smsObj != null && double.TryParse(smsObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sms))
                videoNode.SubtitleMinSilenceSec = sms;
            if (properties.TryGetValue("SubtitleMaxSearchWindowSec", out var smswObj) && smswObj != null && double.TryParse(smswObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var smsw))
                videoNode.SubtitleMaxSearchWindowSec = smsw;
            if (properties.TryGetValue("SubtitleUseEditedAudio", out var sueaObj) && sueaObj != null && bool.TryParse(sueaObj.ToString(), out var suea))
                videoNode.SubtitleUseEditedAudio = suea;
            if (properties.TryGetValue("SubtitleAudioExportFormat", out var saefObj))
                videoNode.SubtitleAudioExportFormat = saefObj?.ToString() ?? "mp3";
            if (properties.TryGetValue("SubtitleAudioExportBitrate", out var saebObj))
                videoNode.SubtitleAudioExportBitrate = saebObj?.ToString() ?? "128k";
            if (properties.TryGetValue("SubtitleOutputBase64", out var sob64Obj) && sob64Obj != null && bool.TryParse(sob64Obj.ToString(), out var sob64))
                videoNode.SubtitleOutputBase64 = sob64;

            if (properties.TryGetValue("ReturnSubtitleNodeId", out var rsnObj))
                videoNode.ReturnSubtitleNodeId = rsnObj?.ToString();
            if (properties.TryGetValue("ReturnSubtitleOutputKey", out var rskObj))
                videoNode.ReturnSubtitleOutputKey = rskObj?.ToString();
            if (properties.TryGetValue("ReturnSubtitleCodeIdKeys", out var rsckObj))
                videoNode.ReturnSubtitleCodeIdKeys = rsckObj?.ToString() ?? "chunkIndex, chunkId, codeId, segmentId, chunk_index, chunk_id, id";
            if (properties.TryGetValue("ReturnSubtitleTextKeys", out var rstkObj))
                videoNode.ReturnSubtitleTextKeys = rstkObj?.ToString() ?? "text, translated_text, translation, content, subtitle, transcript, result, sentence, caption, val";
            if (properties.TryGetValue("ReturnSubtitleOrigTextKeys", out var rsotkObj))
                videoNode.ReturnSubtitleOrigTextKeys = rsotkObj?.ToString() ?? "original_text, orig_text, source_text, src_text, raw_text, origin, raw, source";
            if (properties.TryGetValue("ReturnSubtitleSpeakerKeys", out var rsspeakerkObj))
                videoNode.ReturnSubtitleSpeakerKeys = rsspeakerkObj?.ToString() ?? "speaker, speaker_id, speaker_name, character, person, voice, role, actor";
            if (properties.TryGetValue("ReturnSubtitleTranslationsKeys", out var rstranskObj))
                videoNode.ReturnSubtitleTranslationsKeys = rstranskObj?.ToString() ?? "translations, langs, localized, translated, languages, trans";
            if (properties.TryGetValue("ReturnSubtitleWordsKeys", out var rswordkObj))
                videoNode.ReturnSubtitleWordsKeys = rswordkObj?.ToString() ?? "words, word_timestamps, tokens, word_list, aligned_words";
            if (properties.TryGetValue("ReturnSubtitleStartKeys", out var rsskObj))
                videoNode.ReturnSubtitleStartKeys = rsskObj?.ToString() ?? "start, start_time, startTime, from, begin, st, start_sec, start_ms, offset";
            if (properties.TryGetValue("ReturnSubtitleEndKeys", out var rsekObj))
                videoNode.ReturnSubtitleEndKeys = rsekObj?.ToString() ?? "end, end_time, endTime, to, ed, end_sec, end_ms, duration, dur, length";
            if (properties.TryGetValue("ReturnSubtitleListKeys", out var rslkObj))
                videoNode.ReturnSubtitleListKeys = rslkObj?.ToString() ?? "segments, items, lines, subtitles, chunks, utterances, data, results, sentences";

            if (properties.TryGetValue("DubbingSplitMode", out var dsmObj))
                videoNode.DubbingSplitMode = dsmObj?.ToString() ?? "SubtitleSegments";
            if (properties.TryGetValue("DubbingTargetVoice", out var dtvObj))
                videoNode.DubbingTargetVoice = dtvObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("DubbingTargetLanguage", out var dtlObj))
                videoNode.DubbingTargetLanguage = dtlObj?.ToString() ?? "vi";
            if (properties.TryGetValue("DubbingSpeechRate", out var dsrObj) && dsrObj != null && double.TryParse(dsrObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dsr))
                videoNode.DubbingSpeechRate = dsr;
            if (properties.TryGetValue("DubbingPitch", out var dpObj) && dpObj != null && double.TryParse(dpObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dp))
                videoNode.DubbingPitch = dp;
            if (properties.TryGetValue("DubbingAutoDuckOriginalAudio", out var dadObj) && dadObj != null && bool.TryParse(dadObj.ToString(), out var dad))
                videoNode.DubbingAutoDuckOriginalAudio = dad;
            if (properties.TryGetValue("DubbingOriginalAudioVolumePercent", out var doavObj) && doavObj != null && double.TryParse(doavObj.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var doav))
                videoNode.DubbingOriginalAudioVolumePercent = doav;

            if (properties.TryGetValue("ReturnDubbingNodeId", out var rdnObj))
                videoNode.ReturnDubbingNodeId = rdnObj?.ToString();
            if (properties.TryGetValue("ReturnDubbingOutputKey", out var rdkObj))
                videoNode.ReturnDubbingOutputKey = rdkObj?.ToString();
            if (properties.TryGetValue("ReturnDubbingCodeIdKeys", out var rdckObj))
                videoNode.ReturnDubbingCodeIdKeys = rdckObj?.ToString() ?? "chunkId, codeId, chunkIndex, segmentId, id";
            if (properties.TryGetValue("ReturnDubbingAudioLinkKeys", out var rdlkObj))
                videoNode.ReturnDubbingAudioLinkKeys = rdlkObj?.ToString() ?? "audioUrl, linkAudio, audioPath, url, path, src, link";
            if (properties.TryGetValue("ReturnDubbingAudioBase64Keys", out var rdb64kObj))
                videoNode.ReturnDubbingAudioBase64Keys = rdb64kObj?.ToString() ?? "audioBase64, base64, audioData, data";
            if (properties.TryGetValue("ReturnDubbingStartKeys", out var rdskObj))
                videoNode.ReturnDubbingStartKeys = rdskObj?.ToString() ?? "start, start_time, startTime, from";
            if (properties.TryGetValue("ReturnDubbingEndKeys", out var rdekObj))
                videoNode.ReturnDubbingEndKeys = rdekObj?.ToString() ?? "duration, end, end_time, endTime, to";

            videoNode.EnsureStandardDynamicOutputs();

    }

    // -- GET (Serialize) --

    private static void GetMediaGalleryNodeProperties(MediaGalleryNode mediaGalleryNode, Dictionary<string, object> dict)
    {
            dict["Width"] = mediaGalleryNode.Width;
            dict["Height"] = mediaGalleryNode.Height;
            dict["FrameDisplayWidth"] = mediaGalleryNode.FrameDisplayWidth;
            dict["FrameDisplayHeight"] = mediaGalleryNode.FrameDisplayHeight;
            if (!string.IsNullOrEmpty(mediaGalleryNode.TitleKeyTemplate))
                dict["TitleKeyTemplate"] = mediaGalleryNode.TitleKeyTemplate;
            if (!string.IsNullOrEmpty(mediaGalleryNode.ImageUrlKeyTemplate))
                dict["ImageUrlKeyTemplate"] = mediaGalleryNode.ImageUrlKeyTemplate;
            if (!string.IsNullOrEmpty(mediaGalleryNode.VideoUrlKeyTemplate))
                dict["VideoUrlKeyTemplate"] = mediaGalleryNode.VideoUrlKeyTemplate;
            if (!string.IsNullOrEmpty(mediaGalleryNode.GroupArrayKey))
                dict["GroupArrayKey"] = mediaGalleryNode.GroupArrayKey;
            if (!string.IsNullOrEmpty(mediaGalleryNode.GroupTitleKey))
                dict["GroupTitleKey"] = mediaGalleryNode.GroupTitleKey;
            if (!string.IsNullOrEmpty(mediaGalleryNode.GroupItemsKey))
                dict["GroupItemsKey"] = mediaGalleryNode.GroupItemsKey;
            if (!string.IsNullOrEmpty(mediaGalleryNode.FolderSaveImages))
                dict["FolderSaveImages"] = mediaGalleryNode.FolderSaveImages;
            if (!string.IsNullOrEmpty(mediaGalleryNode.FolderSourceNodeId))
                dict["FolderSourceNodeId"] = mediaGalleryNode.FolderSourceNodeId;
            if (!string.IsNullOrEmpty(mediaGalleryNode.FolderSourceOutputKey))
                dict["FolderSourceOutputKey"] = mediaGalleryNode.FolderSourceOutputKey;
            if (!string.IsNullOrEmpty(mediaGalleryNode.FolderSaveVideos))
                dict["FolderSaveVideos"] = mediaGalleryNode.FolderSaveVideos;
            if (!string.IsNullOrEmpty(mediaGalleryNode.FolderSourceNodeIdVideo))
                dict["FolderSourceNodeIdVideo"] = mediaGalleryNode.FolderSourceNodeIdVideo;
            if (!string.IsNullOrEmpty(mediaGalleryNode.FolderSourceOutputKeyVideo))
                dict["FolderSourceOutputKeyVideo"] = mediaGalleryNode.FolderSourceOutputKeyVideo;
            if (!string.IsNullOrEmpty(mediaGalleryNode.JsonSourceNodeId))
                dict["JsonSourceNodeId"] = mediaGalleryNode.JsonSourceNodeId;
            if (!string.IsNullOrEmpty(mediaGalleryNode.JsonSourceOutputKey))
                dict["JsonSourceOutputKey"] = mediaGalleryNode.JsonSourceOutputKey;
            dict["ItemClickPreviewMode"] = mediaGalleryNode.ItemClickPreviewMode.ToString();
            dict["DisplayMode"] = mediaGalleryNode.DisplayMode.ToString();

            dict["CanReexecuteSourceNode"] = mediaGalleryNode.CanReexecuteSourceNode;
    }

    private static void GetImageProcessingNodeProperties(ImageProcessingNode imageNode, Dictionary<string, object> dict)
    {
            dict["Width"] = imageNode.Width;
            dict["Height"] = imageNode.Height;
            dict["InputMode"] = imageNode.InputMode.ToString();
            dict["CropMode"] = imageNode.CropMode.ToString();
            dict["ProcessingMode"] = imageNode.ProcessingMode.ToString();

            if (!string.IsNullOrWhiteSpace(imageNode.ImageUrl))
                dict["ImageUrl"] = imageNode.ImageUrl;
            if (!string.IsNullOrWhiteSpace(imageNode.ImageUrlSourceNodeId))
                dict["ImageUrlSourceNodeId"] = imageNode.ImageUrlSourceNodeId;
            if (!string.IsNullOrWhiteSpace(imageNode.ImageUrlSourceOutputKey))
                dict["ImageUrlSourceOutputKey"] = imageNode.ImageUrlSourceOutputKey;

            if (!string.IsNullOrWhiteSpace(imageNode.ImageBase64))
                dict["ImageBase64"] = imageNode.ImageBase64;
            if (!string.IsNullOrWhiteSpace(imageNode.ImageBase64SourceNodeId))
                dict["ImageBase64SourceNodeId"] = imageNode.ImageBase64SourceNodeId;
            if (!string.IsNullOrWhiteSpace(imageNode.ImageBase64SourceOutputKey))
                dict["ImageBase64SourceOutputKey"] = imageNode.ImageBase64SourceOutputKey;

            dict["PreferGpu"] = imageNode.PreferGpu;
            if (!string.IsNullOrWhiteSpace(imageNode.FfmpegFilter))
                dict["FfmpegFilter"] = imageNode.FfmpegFilter;

            if (!string.IsNullOrWhiteSpace(imageNode.CroppedFolderPath))
                dict["CroppedFolderPath"] = imageNode.CroppedFolderPath;
            if (!string.IsNullOrWhiteSpace(imageNode.CroppedFolderSourceNodeId))
                dict["CroppedFolderSourceNodeId"] = imageNode.CroppedFolderSourceNodeId;
            if (!string.IsNullOrWhiteSpace(imageNode.CroppedFolderSourceOutputKey))
                dict["CroppedFolderSourceOutputKey"] = imageNode.CroppedFolderSourceOutputKey;

            // Serialize danh sách vùng crop (polygon points + state)
            if (imageNode.Crops != null && imageNode.Crops.Count > 0)
            {
                var cropsData = imageNode.Crops.Select(r => new
                {
                    Id = r.Id,
                    Order = r.Order,
                    ColorHex = r.ColorHex,
                    Points = r.Points.Select(p => new[] { p.X, p.Y }).ToList(),
                    IsVisible = r.IsVisible,
                    IsOutlineOnly = r.IsOutlineOnly,
                    SavedPath = r.SavedPath ?? string.Empty,
                    CropName = r.CropName ?? string.Empty,
                    LastExecutionId = r.LastExecutionId ?? string.Empty
                }).ToList();
                dict["Crops"] = JsonSerializer.Serialize(cropsData);
            }

            // Image Processor settings
            dict["PromptSize"] = imageNode.PromptSize;
            if (!string.IsNullOrWhiteSpace(imageNode.ProcessorPrompt))
                dict["ProcessorPrompt"] = imageNode.ProcessorPrompt;
            dict["IsVerticalMode"] = imageNode.IsVerticalMode;

            // Render node config
            if (!string.IsNullOrWhiteSpace(imageNode.RenderNodeId))
                dict["RenderNodeId"] = imageNode.RenderNodeId;
            if (!string.IsNullOrWhiteSpace(imageNode.RenderNodeOutputKey))
                dict["RenderNodeOutputKey"] = imageNode.RenderNodeOutputKey;
            if (!string.IsNullOrWhiteSpace(imageNode.RenderCodeIdKeys))
                dict["RenderCodeIdKeys"] = imageNode.RenderCodeIdKeys;
            if (!string.IsNullOrWhiteSpace(imageNode.RenderImageIdKeys))
                dict["RenderImageIdKeys"] = imageNode.RenderImageIdKeys;
            if (!string.IsNullOrWhiteSpace(imageNode.RenderImageLinkKeys))
                dict["RenderImageLinkKeys"] = imageNode.RenderImageLinkKeys;

            // Return ID node config (Node nhận lại ID ảnh)
            if (!string.IsNullOrWhiteSpace(imageNode.ReturnIdNodeId))
                dict["ReturnIdNodeId"] = imageNode.ReturnIdNodeId;
            if (!string.IsNullOrWhiteSpace(imageNode.ReturnIdOutputKey))
                dict["ReturnIdOutputKey"] = imageNode.ReturnIdOutputKey;
            if (!string.IsNullOrWhiteSpace(imageNode.ReturnCodeIdKeys))
                dict["ReturnCodeIdKeys"] = imageNode.ReturnCodeIdKeys;
            if (!string.IsNullOrWhiteSpace(imageNode.ReturnImageIdKeys))
                dict["ReturnImageIdKeys"] = imageNode.ReturnImageIdKeys;
            if (!string.IsNullOrWhiteSpace(imageNode.ReturnImageLinkKeys))
                dict["ReturnImageLinkKeys"] = imageNode.ReturnImageLinkKeys;

            // SkipOutputs
            if (imageNode.SkipOutputs != null && imageNode.SkipOutputs.Count > 0)
                dict["SkipOutputs"] = JsonSerializer.Serialize(imageNode.SkipOutputs.ToList());

            // Layer AI settings
            if (!string.IsNullOrWhiteSpace(imageNode.LayerAiHtmlCode))
                dict["LayerAiHtmlCode"] = imageNode.LayerAiHtmlCode;
            if (!string.IsNullOrWhiteSpace(imageNode.LayerAiCssCode))
                dict["LayerAiCssCode"] = imageNode.LayerAiCssCode;
            if (!string.IsNullOrWhiteSpace(imageNode.LayerAiJsCode))
                dict["LayerAiJsCode"] = imageNode.LayerAiJsCode;
            if (!string.IsNullOrWhiteSpace(imageNode.LayerAiParamsCode))
                dict["LayerAiParamsCode"] = imageNode.LayerAiParamsCode;

            if (imageNode.LayerAiInputMappings != null && imageNode.LayerAiInputMappings.Count > 0)
                dict["LayerAiInputMappings"] = JsonSerializer.Serialize(imageNode.LayerAiInputMappings);

            if (!string.IsNullOrWhiteSpace(imageNode.LayerAiWebUrl))
                dict["LayerAiWebUrl"] = imageNode.LayerAiWebUrl;
            if (!string.IsNullOrWhiteSpace(imageNode.LayerAiCacheProfileName))
                dict["LayerAiCacheProfileName"] = imageNode.LayerAiCacheProfileName;
            if (!string.IsNullOrWhiteSpace(imageNode.LayerAiWebTabsJson))
                dict["LayerAiWebTabsJson"] = imageNode.LayerAiWebTabsJson;
            if (!string.IsNullOrWhiteSpace(imageNode.LayerAiWebSplitMode))
                dict["LayerAiWebSplitMode"] = imageNode.LayerAiWebSplitMode;
            if (!string.IsNullOrWhiteSpace(imageNode.LayerAiActiveTab))
                dict["LayerAiActiveTab"] = imageNode.LayerAiActiveTab;

            dict["LayerAiPromptHidden"] = imageNode.LayerAiPromptHidden;
            dict["LayerAiSendModeOn"] = imageNode.LayerAiSendModeOn;
            dict["LayerAiIsCombinedMode"] = imageNode.LayerAiIsCombinedMode;

    }

    private static void GetVideoProcessingNodeProperties(VideoProcessingNode videoNode, Dictionary<string, object> dict)
    {
            dict["Width"] = videoNode.Width;
            dict["Height"] = videoNode.Height;
            if (!string.IsNullOrWhiteSpace(videoNode.VideoSourceNodeId))
                dict["VideoSourceNodeId"] = videoNode.VideoSourceNodeId;
            if (!string.IsNullOrWhiteSpace(videoNode.VideoSourceOutputKey))
                dict["VideoSourceOutputKey"] = videoNode.VideoSourceOutputKey;
            if (!string.IsNullOrWhiteSpace(videoNode.VideoPath))
                dict["VideoPath"] = videoNode.VideoPath;
            if (!string.IsNullOrWhiteSpace(videoNode.OutputFolderSourceNodeId))
                dict["OutputFolderSourceNodeId"] = videoNode.OutputFolderSourceNodeId;
            if (!string.IsNullOrWhiteSpace(videoNode.OutputFolderSourceOutputKey))
                dict["OutputFolderSourceOutputKey"] = videoNode.OutputFolderSourceOutputKey;
            if (!string.IsNullOrWhiteSpace(videoNode.VideoOutputFolderSourceNodeId))
                dict["VideoOutputFolderSourceNodeId"] = videoNode.VideoOutputFolderSourceNodeId;
            if (!string.IsNullOrWhiteSpace(videoNode.VideoOutputFolderSourceOutputKey))
                dict["VideoOutputFolderSourceOutputKey"] = videoNode.VideoOutputFolderSourceOutputKey;
            if (!string.IsNullOrWhiteSpace(videoNode.AudioOutputFolderSourceNodeId))
                dict["AudioOutputFolderSourceNodeId"] = videoNode.AudioOutputFolderSourceNodeId;
            if (!string.IsNullOrWhiteSpace(videoNode.AudioOutputFolderSourceOutputKey))
                dict["AudioOutputFolderSourceOutputKey"] = videoNode.AudioOutputFolderSourceOutputKey;

            dict["OutputBase64"] = videoNode.OutputBase64;
            dict["UseDialogVideoConfig"] = videoNode.UseDialogVideoConfig;
            dict["ExtractFramesEnabled"] = videoNode.ExtractFramesEnabled;
            dict["ExportVideoEnabled"] = videoNode.ExportVideoEnabled;
            dict["ExtractAudioEnabled"] = videoNode.ExtractAudioEnabled;
            if (!string.IsNullOrWhiteSpace(videoNode.FrameOutputFolderPath))
                dict["FrameOutputFolderPath"] = videoNode.FrameOutputFolderPath;
            if (!string.IsNullOrWhiteSpace(videoNode.DefaultOutputVideoPath))
                dict["DefaultOutputVideoPath"] = videoNode.DefaultOutputVideoPath;
            if (!string.IsNullOrWhiteSpace(videoNode.AudioOutputFolderPath))
                dict["AudioOutputFolderPath"] = videoNode.AudioOutputFolderPath;
            dict["SecondsPerFrame"] = videoNode.SecondsPerFrame;
            dict["ExtractFrameCount"] = videoNode.ExtractFrameCount;
            dict["PreferGpu"] = videoNode.PreferGpu;
            if (!string.IsNullOrWhiteSpace(videoNode.PreferredHwAccel))
                dict["PreferredHwAccel"] = videoNode.PreferredHwAccel;
            dict["SourceFps"] = videoNode.SourceFps;
            dict["ExtractFps"] = videoNode.ExtractFps;
            if (videoNode.ExcludedFrameTimestamps.Count > 0)
                dict["ExcludedFrameTimestamps"] = JsonSerializer.Serialize(videoNode.ExcludedFrameTimestamps);
            dict["Brightness"] = videoNode.Brightness;
            dict["Contrast"] = videoNode.Contrast;
            dict["Saturation"] = videoNode.Saturation;
            dict["Hue"] = videoNode.Hue;
            dict["Gamma"] = videoNode.Gamma;
            dict["SharpenEnabled"] = videoNode.SharpenEnabled;
            dict["SharpenStrength"] = videoNode.SharpenStrength;
            dict["DenoiseEnabled"] = videoNode.DenoiseEnabled;
            dict["DenoiseStrength"] = videoNode.DenoiseStrength;
            dict["BlurEnabled"] = videoNode.BlurEnabled;
            dict["BlurRadius"] = videoNode.BlurRadius;
            dict["StabilizeEnabled"] = videoNode.StabilizeEnabled;
            dict["SpeedFactor"] = videoNode.SpeedFactor;
            dict["RotationDegrees"] = videoNode.RotationDegrees;
            dict["FlipH"] = videoNode.FlipH;
            dict["FlipV"] = videoNode.FlipV;
            dict["OutputFormat"] = videoNode.OutputFormat;
            dict["EncoderPreset"] = videoNode.EncoderPreset;
            dict["Crf"] = videoNode.Crf;
            dict["ResolutionScale"] = videoNode.ResolutionScale;
            dict["FrameResizeScale"] = videoNode.FrameResizeScale;
            dict["TrimEnabled"] = videoNode.TrimEnabled;
            dict["TrimStartSec"] = videoNode.TrimStartSec;
            dict["TrimEndSec"] = videoNode.TrimEndSec;
            dict["ConcatEnabled"] = videoNode.ConcatEnabled;
            dict["ConcatVideos"] = videoNode.ConcatVideos.Select(c => new { c.SourcePath }).ToList();
            if (!string.IsNullOrWhiteSpace(videoNode.OutputPathOverride))
                dict["OutputPathOverride"] = videoNode.OutputPathOverride;
            dict["SourceAudioEnabled"] = videoNode.SourceAudioEnabled;
            dict["SourceAudioVolumePercent"] = videoNode.SourceAudioVolumePercent;
            dict["AudioFadeInSec"] = videoNode.AudioFadeInSec;
            dict["AudioFadeOutSec"] = videoNode.AudioFadeOutSec;
            dict["AudioNormalizeEnabled"] = videoNode.AudioNormalizeEnabled;
            dict["AudioTargetLufs"] = videoNode.AudioTargetLufs;
            dict["AudioDenoiseEnabled"] = videoNode.AudioDenoiseEnabled;
            dict["AudioSpeedFactor"] = videoNode.AudioSpeedFactor;
            dict["AudioEqPreset"] = videoNode.AudioEqPreset;
            dict["AudioBassGain"] = videoNode.AudioBassGain;
            dict["AudioLowMidGain"] = videoNode.AudioLowMidGain;
            dict["AudioMidGain"] = videoNode.AudioMidGain;
            dict["AudioHighMidGain"] = videoNode.AudioHighMidGain;
            dict["AudioTrebleGain"] = videoNode.AudioTrebleGain;
            dict["AudioToneClarity"] = videoNode.AudioToneClarity;
            dict["AudioHighpassFilter"] = videoNode.AudioHighpassFilter;
            dict["AudioHighpassCutoffHz"] = videoNode.AudioHighpassCutoffHz;
            dict["AudioLowpassFilter"] = videoNode.AudioLowpassFilter;
            dict["AudioLowpassCutoffHz"] = videoNode.AudioLowpassCutoffHz;
            dict["AudioStereoWidthPercent"] = videoNode.AudioStereoWidthPercent;
            dict["AudioWarmthPercent"] = videoNode.AudioWarmthPercent;
            dict["AudioReverbPercent"] = videoNode.AudioReverbPercent;
            dict["AudioVocalBalance"] = videoNode.AudioVocalBalance;
            dict["AudioPitchSemitones"] = videoNode.AudioPitchSemitones;
            dict["AudioEchoEnabled"] = videoNode.AudioEchoEnabled;
            dict["AudioEchoDelayMs"] = videoNode.AudioEchoDelayMs;
            dict["AudioEchoFeedbackPercent"] = videoNode.AudioEchoFeedbackPercent;
            dict["AudioEchoMixPercent"] = videoNode.AudioEchoMixPercent;
            dict["Audio8DEnabled"] = videoNode.Audio8DEnabled;
            dict["Audio8DSpeedHz"] = videoNode.Audio8DSpeedHz;
            dict["AudioRobotVoiceEnabled"] = videoNode.AudioRobotVoiceEnabled;
            dict["AudioRadioVoiceEnabled"] = videoNode.AudioRadioVoiceEnabled;
            dict["AudioChorusEnabled"] = videoNode.AudioChorusEnabled;
            dict["AudioChorusMixPercent"] = videoNode.AudioChorusMixPercent;
            dict["AudioCompressorPercent"] = videoNode.AudioCompressorPercent;
            dict["AudioDeEsserPercent"] = videoNode.AudioDeEsserPercent;
            dict["AudioNoiseGatePercent"] = videoNode.AudioNoiseGatePercent;
            dict["AudioWaveShaperEnabled"] = videoNode.AudioWaveShaperEnabled;
            dict["AudioWaveShaperCurve"] = videoNode.AudioWaveShaperCurve;
            dict["AudioWaveShaperDrivePercent"] = videoNode.AudioWaveShaperDrivePercent;
            dict["AudioTransientPunchPercent"] = videoNode.AudioTransientPunchPercent;
            dict["AudioSubHarmonicsPercent"] = videoNode.AudioSubHarmonicsPercent;
            dict["AudioHarmonicExciterPercent"] = videoNode.AudioHarmonicExciterPercent;
            dict["AudioPhaseInvertLeft"] = videoNode.AudioPhaseInvertLeft;
            dict["AudioPhaseInvertRight"] = videoNode.AudioPhaseInvertRight;
            dict["VoiceChangerEnabled"] = videoNode.VoiceChangerEnabled;
            dict["EqualizerFxEnabled"] = videoNode.EqualizerFxEnabled;
            dict["DynamicsMasteringEnabled"] = videoNode.DynamicsMasteringEnabled;
            dict["MultiTrackBgmEnabled"] = videoNode.MultiTrackBgmEnabled;
            dict["AudioExportConfigEnabled"] = videoNode.AudioExportConfigEnabled;
            dict["AudioTrimEnabled"] = videoNode.AudioTrimEnabled;
            dict["AudioTrimStartSec"] = videoNode.AudioTrimStartSec;
            dict["AudioTrimEndSec"] = videoNode.AudioTrimEndSec;
            dict["AudioExportFormat"] = videoNode.AudioExportFormat;
            dict["AudioExportBitrate"] = videoNode.AudioExportBitrate;
            dict["AudioExportSampleRate"] = videoNode.AudioExportSampleRate;
            dict["AudioExportChannels"] = videoNode.AudioExportChannels;
            dict["PreviewVolume"] = videoNode.PreviewVolume;
            dict["PreviewQualityMode"] = videoNode.PreviewQualityMode;
            dict["PreviewVisualStrengthMode"] = videoNode.PreviewVisualStrengthMode;
            dict["WatermarkEnabled"] = videoNode.WatermarkEnabled;
            if (!string.IsNullOrWhiteSpace(videoNode.WatermarkImagePath))
                dict["WatermarkImagePath"] = videoNode.WatermarkImagePath;
            dict["WatermarkPosition"] = videoNode.WatermarkPosition;
            dict["WatermarkOpacity"] = videoNode.WatermarkOpacity;
            dict["WatermarkPaddingPx"] = videoNode.WatermarkPaddingPx;
            dict["WatermarkWidthFraction"] = videoNode.WatermarkWidthFraction;
            dict["WatermarkInsetFraction"] = videoNode.WatermarkInsetFraction;
            dict["TextOverlayEnabled"] = videoNode.TextOverlayEnabled;
            if (!string.IsNullOrWhiteSpace(videoNode.OverlayText))
                dict["OverlayText"] = videoNode.OverlayText;
            dict["OverlayFont"] = videoNode.OverlayFont;
            dict["OverlayFontSize"] = videoNode.OverlayFontSize;
            dict["OverlayFontColor"] = videoNode.OverlayFontColor;
            dict["TextPosition"] = videoNode.TextPosition;
            dict["FrameLabelEnabled"] = videoNode.FrameLabelEnabled;
            dict["FrameLabelDebugSamplesEnabled"] = videoNode.FrameLabelDebugSamplesEnabled;
            dict["FrameLabelTemplate"] = videoNode.FrameLabelTemplate;
            dict["FrameLabelTextColor"] = videoNode.FrameLabelTextColor;
            dict["FrameLabelBackgroundColor"] = videoNode.FrameLabelBackgroundColor;
            dict["FrameLabelFontSize"] = videoNode.FrameLabelFontSize;
            dict["FrameLabelX"] = videoNode.FrameLabelX;
            dict["FrameLabelY"] = videoNode.FrameLabelY;
            dict["FrameLabelW"] = videoNode.FrameLabelW;
            dict["FrameLabelH"] = videoNode.FrameLabelH;
            dict["FrameLabelHorizontalPadding"] = videoNode.FrameLabelHorizontalPadding;
            dict["FrameLabelVerticalPadding"] = videoNode.FrameLabelVerticalPadding;
            dict["FrameLabelTimeFormat"] = videoNode.FrameLabelTimeFormat;
            dict["ExtractParallelJobs"] = videoNode.ExtractParallelJobs;
            dict["FrameOutputFormat"] = videoNode.FrameOutputFormat;
            dict["JpegQuality"] = videoNode.JpegQuality;
            dict["ExtractAllFrames"] = videoNode.ExtractAllFrames;
            dict["TwoPassEnabled"] = videoNode.TwoPassEnabled;
            dict["AudioCodec"] = videoNode.AudioCodec;
            dict["AudioBitrate"] = videoNode.AudioBitrate;
            if (!string.IsNullOrWhiteSpace(videoNode.SubtitlePath))
                dict["SubtitlePath"] = videoNode.SubtitlePath;
            dict["BurnSubtitleEnabled"] = videoNode.BurnSubtitleEnabled;
            dict["GridCollageEnabled"] = videoNode.GridCollageEnabled;
            dict["GridCollageWidth"] = videoNode.GridCollageWidth;
            dict["GridCollageHeight"] = videoNode.GridCollageHeight;
            dict["GridCollageFrameCount"] = videoNode.GridCollageFrameCount;
            dict["GridCollageBackgroundColor"] = videoNode.GridCollageBackgroundColor;
            dict["GridCollageColorKey"] = videoNode.GridCollageColorKey;
            dict["GridCollagePadding"] = videoNode.GridCollagePadding;
            dict["GridCollageMargin"] = videoNode.GridCollageMargin;
            dict["GridCollageAspectMode"] = videoNode.GridCollageAspectMode;
            dict["GridCollageShowFrameIndex"] = videoNode.GridCollageShowFrameIndex;

            dict["ExtractByFpsEnabled"] = videoNode.ExtractByFpsEnabled;

            // Subtitle AI & Dubbing Workflow Properties
            dict["SubtitleSplitMode"] = videoNode.SubtitleSplitMode;
            dict["SubtitleChunkDurationSec"] = videoNode.SubtitleChunkDurationSec;
            dict["SubtitleChunkCount"] = videoNode.SubtitleChunkCount;
            dict["SubtitleEnableSmartSilenceSplit"] = videoNode.SubtitleEnableSmartSilenceSplit;
            dict["SubtitleSilenceThresholdDb"] = videoNode.SubtitleSilenceThresholdDb;
            dict["SubtitleMinSilenceSec"] = videoNode.SubtitleMinSilenceSec;
            dict["SubtitleMaxSearchWindowSec"] = videoNode.SubtitleMaxSearchWindowSec;
            dict["SubtitleUseEditedAudio"] = videoNode.SubtitleUseEditedAudio;
            dict["SubtitleAudioExportFormat"] = videoNode.SubtitleAudioExportFormat;
            dict["SubtitleAudioExportBitrate"] = videoNode.SubtitleAudioExportBitrate;
            dict["SubtitleOutputBase64"] = videoNode.SubtitleOutputBase64;

            if (!string.IsNullOrWhiteSpace(videoNode.ReturnSubtitleNodeId))
                dict["ReturnSubtitleNodeId"] = videoNode.ReturnSubtitleNodeId;
            if (!string.IsNullOrWhiteSpace(videoNode.ReturnSubtitleOutputKey))
                dict["ReturnSubtitleOutputKey"] = videoNode.ReturnSubtitleOutputKey;
            dict["ReturnSubtitleCodeIdKeys"] = videoNode.ReturnSubtitleCodeIdKeys;
            dict["ReturnSubtitleTextKeys"] = videoNode.ReturnSubtitleTextKeys;
            dict["ReturnSubtitleOrigTextKeys"] = videoNode.ReturnSubtitleOrigTextKeys;
            dict["ReturnSubtitleSpeakerKeys"] = videoNode.ReturnSubtitleSpeakerKeys;
            dict["ReturnSubtitleTranslationsKeys"] = videoNode.ReturnSubtitleTranslationsKeys;
            dict["ReturnSubtitleWordsKeys"] = videoNode.ReturnSubtitleWordsKeys;
            dict["ReturnSubtitleStartKeys"] = videoNode.ReturnSubtitleStartKeys;
            dict["ReturnSubtitleEndKeys"] = videoNode.ReturnSubtitleEndKeys;
            dict["ReturnSubtitleListKeys"] = videoNode.ReturnSubtitleListKeys;

            dict["DubbingSplitMode"] = videoNode.DubbingSplitMode;
            dict["DubbingTargetVoice"] = videoNode.DubbingTargetVoice;
            dict["DubbingTargetLanguage"] = videoNode.DubbingTargetLanguage;
            dict["DubbingSpeechRate"] = videoNode.DubbingSpeechRate;
            dict["DubbingPitch"] = videoNode.DubbingPitch;
            dict["DubbingAutoDuckOriginalAudio"] = videoNode.DubbingAutoDuckOriginalAudio;
            dict["DubbingOriginalAudioVolumePercent"] = videoNode.DubbingOriginalAudioVolumePercent;

            if (!string.IsNullOrWhiteSpace(videoNode.ReturnDubbingNodeId))
                dict["ReturnDubbingNodeId"] = videoNode.ReturnDubbingNodeId;
            if (!string.IsNullOrWhiteSpace(videoNode.ReturnDubbingOutputKey))
                dict["ReturnDubbingOutputKey"] = videoNode.ReturnDubbingOutputKey;
            dict["ReturnDubbingCodeIdKeys"] = videoNode.ReturnDubbingCodeIdKeys;
            dict["ReturnDubbingAudioLinkKeys"] = videoNode.ReturnDubbingAudioLinkKeys;
            dict["ReturnDubbingAudioBase64Keys"] = videoNode.ReturnDubbingAudioBase64Keys;
            dict["ReturnDubbingStartKeys"] = videoNode.ReturnDubbingStartKeys;
            dict["ReturnDubbingEndKeys"] = videoNode.ReturnDubbingEndKeys;

            if (videoNode.AudioTracks.Count > 0)
                dict["AudioTracks"] = JsonSerializer.Serialize(videoNode.AudioTracks.ToList());
            if (videoNode.Overlays.Count > 0)
                dict["Overlays"] = JsonSerializer.Serialize(videoNode.Overlays.ToList());
            if (videoNode.Subtitles.Count > 0)
                dict["Subtitles"] = JsonSerializer.Serialize(videoNode.Subtitles.ToList());
            if (videoNode.SubtitleStyle != null)
                dict["SubtitleStyle"] = JsonSerializer.Serialize(videoNode.SubtitleStyle);
            if (videoNode.DubbingClips.Count > 0)
                dict["DubbingClips"] = JsonSerializer.Serialize(videoNode.DubbingClips.ToList());
            if (videoNode.AutoDucking != null)
                dict["AutoDucking"] = JsonSerializer.Serialize(videoNode.AutoDucking);
        }
    }
