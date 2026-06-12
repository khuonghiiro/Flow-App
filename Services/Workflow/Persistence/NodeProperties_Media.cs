using FlowMy.Models;
using FlowMy.Models.Nodes;
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

            if (properties.TryGetValue("OutputBase64", out var obObj) && obObj != null && bool.TryParse(obObj.ToString(), out var ob))
                videoNode.OutputBase64 = ob;
            if (properties.TryGetValue("UseDialogVideoConfig", out var udvcObj) && udvcObj != null && bool.TryParse(udvcObj.ToString(), out var udvc))
                videoNode.UseDialogVideoConfig = udvc;
            if (properties.TryGetValue("FrameOutputFolderPath", out var fofpObj))
                videoNode.FrameOutputFolderPath = fofpObj?.ToString();
            if (properties.TryGetValue("DefaultOutputVideoPath", out var dovpObj))
                videoNode.DefaultOutputVideoPath = dovpObj?.ToString();
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
            if (properties.TryGetValue("OutputPathOverride", out var opoObj))
                videoNode.OutputPathOverride = opoObj?.ToString();
            if (properties.TryGetValue("SourceAudioEnabled", out var saeObj) && saeObj != null && bool.TryParse(saeObj.ToString(), out var sae))
                videoNode.SourceAudioEnabled = sae;
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
                    CropName = r.CropName ?? string.Empty
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

            // SkipOutputs
            if (imageNode.SkipOutputs != null && imageNode.SkipOutputs.Count > 0)
                dict["SkipOutputs"] = JsonSerializer.Serialize(imageNode.SkipOutputs.ToList());

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

            dict["OutputBase64"] = videoNode.OutputBase64;
            dict["UseDialogVideoConfig"] = videoNode.UseDialogVideoConfig;
            if (!string.IsNullOrWhiteSpace(videoNode.FrameOutputFolderPath))
                dict["FrameOutputFolderPath"] = videoNode.FrameOutputFolderPath;
            if (!string.IsNullOrWhiteSpace(videoNode.DefaultOutputVideoPath))
                dict["DefaultOutputVideoPath"] = videoNode.DefaultOutputVideoPath;
            dict["SecondsPerFrame"] = videoNode.SecondsPerFrame;
            dict["ExtractFrameCount"] = videoNode.ExtractFrameCount;
            dict["PreferGpu"] = videoNode.PreferGpu;
            if (!string.IsNullOrWhiteSpace(videoNode.PreferredHwAccel))
                dict["PreferredHwAccel"] = videoNode.PreferredHwAccel;
            dict["SourceFps"] = videoNode.SourceFps;
            dict["ExtractFps"] = videoNode.ExtractFps;
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
            if (!string.IsNullOrWhiteSpace(videoNode.OutputPathOverride))
                dict["OutputPathOverride"] = videoNode.OutputPathOverride;
            dict["SourceAudioEnabled"] = videoNode.SourceAudioEnabled;
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
            if (videoNode.AudioTracks.Count > 0)
                dict["AudioTracks"] = JsonSerializer.Serialize(videoNode.AudioTracks.ToList());
            if (videoNode.Overlays.Count > 0)
                dict["Overlays"] = JsonSerializer.Serialize(videoNode.Overlays.ToList());

    }

}
