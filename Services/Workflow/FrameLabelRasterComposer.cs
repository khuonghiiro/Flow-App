using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlowMy.Models.Nodes;
using FlowMy.Services.Workflow.NodeExecutors;

namespace FlowMy.Services.Workflow;

/// <summary>
/// Rasterizes frame label (background + text) with WPF, matching in-app preview, then composites as pixels (no FFmpeg drawtext).
/// </summary>
internal static class FrameLabelRasterComposer
{
    private static readonly Typeface FrameLabelTypeface = CreateTypeface();

    private static Typeface CreateTypeface()
    {
        try
        {
            var ff = new FontFamily("Segoe UI Semibold");
            return new Typeface(ff, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        }
        catch
        {
            return new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        }
    }

    internal static (int estW, int estH) GetEstimatedSourceFrameSize(int probeW, int probeH, VideoProcessingNode node)
    {
        if (probeW <= 0 || probeH <= 0) return (1280, 720);
        var rot = ((int)(node.RotationDegrees / 90) % 4 + 4) % 4;
        return rot is 1 or 3 ? (probeH, probeW) : (probeW, probeH);
    }

    internal static string FormatResolvedLabelText(
        VideoProcessingNode node,
        int outputIndexOneBased,
        int sourceFrameApprox,
        TimeSpan mediaTime)
    {
        var template = string.IsNullOrWhiteSpace(node.FrameLabelTemplate)
            ? "Frame {index} - {time}"
            : node.FrameLabelTemplate;
        var timeStr = string.Equals(node.FrameLabelTimeFormat, "HHMMSS", StringComparison.OrdinalIgnoreCase)
            ? mediaTime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : mediaTime.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        return template
            .Replace("{index}", outputIndexOneBased.ToString(CultureInfo.InvariantCulture))
            .Replace("{frame}", sourceFrameApprox.ToString(CultureInfo.InvariantCulture))
            .Replace("{time}", timeStr);
    }

    internal static void RenderLabelStrip(
        VideoProcessingNode node,
        string resolvedText,
        int stripW,
        int stripH,
        int padLeft,
        int padTop,
        int padRight,
        int padBottom,
        double fontSizePx,
        out BitmapSource bitmap)
    {
        stripW = Math.Max(4, stripW);
        stripH = Math.Max(4, stripH);
        fontSizePx = Math.Max(4, fontSizePx);

        var bg = ParseColor(node.FrameLabelBackgroundColor, Colors.White);
        var fg = ParseColor(node.FrameLabelTextColor, Colors.Black);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, stripW, stripH));

            var innerW = Math.Max(1, stripW - padLeft - padRight);
            var innerH = Math.Max(1, stripH - padTop - padBottom);
            var ft = new FormattedText(
                resolvedText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                FrameLabelTypeface,
                fontSizePx,
                new SolidColorBrush(fg),
                pixelsPerDip: 1);
            ft.MaxTextWidth = innerW;
            ft.MaxTextHeight = innerH;
            ft.TextAlignment = TextAlignment.Left;
            ft.Trimming = TextTrimming.CharacterEllipsis;

            // Match WPF preview: TextBlock in Border is top-aligned in the padded client area (not vertically centered).
            var textY = padTop;
            dc.DrawText(ft, new Point(padLeft, textY));
        }

        var rtb = new RenderTargetBitmap(stripW, stripH, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        bitmap = rtb;
    }

    private static Color ParseColor(string? s, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        try
        {
            var conv = new BrushConverter();
            if (conv.ConvertFromString(s.Trim()) is SolidColorBrush scb)
                return scb.Color;
        }
        catch { }
        return fallback;
    }

    /// <summary>Composites WPF-rendered label onto an existing still frame (replacing file in place).</summary>
    internal static void CompositeLabelOntoStillFile(
        VideoProcessingNode node,
        string imagePath,
        int sequentialIndexZeroBased,
        double timelineStartSec,
        double extractFps,
        double sourceFps,
        int probeSrcW,
        int probeSrcH,
        int probeSrcHForFontScale)
    {
        if (extractFps <= 0) extractFps = 30;
        if (sourceFps <= 0) sourceFps = 30;

        var tSec = timelineStartSec + sequentialIndexZeroBased / extractFps;
        var mediaTime = TimeSpan.FromSeconds(Math.Max(0, tSec));
        var outputIndex = sequentialIndexZeroBased + 1;
        var sourceFrameApprox = Math.Max(0, (int)Math.Round(tSec * sourceFps));
        var text = FormatResolvedLabelText(node, outputIndex, sourceFrameApprox, mediaTime);

        if (string.Equals(Path.GetExtension(imagePath), ".webp", StringComparison.OrdinalIgnoreCase) && GetWebpEncoder() is null)
            return;

        var (estW, estH) = GetEstimatedSourceFrameSize(probeSrcW, probeSrcH, node);
        var labelBoxSrcH = Math.Max(4, (int)Math.Round(estH * node.FrameLabelH));

        var sourceScale = VideoProcessingNodeExecutor.ComputeFrameLabelSourceScale(probeSrcHForFontScale > 0 ? probeSrcHForFontScale : (int?)null);
        var padVidX = Math.Max(0, (int)Math.Round(node.FrameLabelHorizontalPadding * sourceScale));
        var padVidY = Math.Max(0, (int)Math.Round(node.FrameLabelVerticalPadding * sourceScale));

        BitmapSource baseFrame;
        using (var streamIn = File.OpenRead(imagePath))
        {
            var decoder = BitmapDecoder.Create(streamIn, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            baseFrame = BitmapFrame.Create(decoder.Frames[0]);
        }

        baseFrame.Freeze();
        var wf = baseFrame.PixelWidth;
        var hf = baseFrame.PixelHeight;
        if (wf <= 0 || hf <= 0) return;

        var boxW = Math.Max(4, (int)Math.Round(wf * node.FrameLabelW));
        var boxH = Math.Max(4, (int)Math.Round(hf * node.FrameLabelH));
        var boxX = (int)Math.Round(wf * node.FrameLabelX);
        var boxY = (int)Math.Round(hf * node.FrameLabelY);

        var fontPx = VideoProcessingNodeExecutor.ComputeFrameLabelDrawtextFontPixelSize(node, probeSrcHForFontScale > 0 ? probeSrcHForFontScale : (int?)null)
            * (boxH / (double)Math.Max(1, labelBoxSrcH));
        fontPx = Math.Max(4, fontPx);

        var padX = (int)Math.Round(padVidX * (wf / (double)Math.Max(1, estW)));
        var padY = (int)Math.Round(padVidY * (hf / (double)Math.Max(1, estH)));

        RenderLabelStrip(node, text, boxW, boxH, padX, padY, padX, padY, fontPx, out var labelBmp);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(baseFrame, new Rect(0, 0, wf, hf));
            dc.DrawImage(labelBmp, new Rect(boxX, boxY, boxW, boxH));
        }

        var composed = new RenderTargetBitmap(wf, hf, 96, 96, PixelFormats.Pbgra32);
        composed.Render(visual);
        composed.Freeze();

        WriteBitmapToFile(imagePath, composed);
    }

    private static void WriteBitmapToFile(string path, BitmapSource bitmap)
    {
        var ext = Path.GetExtension(path);
        BitmapEncoder encoder;
        switch (ext.ToLowerInvariant())
        {
            case ".jpg":
            case ".jpeg":
                encoder = new JpegBitmapEncoder { QualityLevel = 92 };
                break;
            case ".png":
                encoder = new PngBitmapEncoder();
                break;
            case ".webp":
                if (GetWebpEncoder() is { } we)
                    encoder = we;
                else
                    return;
                break;
            default:
                encoder = new PngBitmapEncoder();
                break;
        }

        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var outStream = File.Create(path);
        encoder.Save(outStream);
    }

    private static BitmapEncoder? GetWebpEncoder()
    {
        var t = Type.GetType("System.Windows.Media.Imaging.WebpBitmapEncoder, PresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
        return t != null ? (BitmapEncoder)Activator.CreateInstance(t)! : null;
    }

    /// <summary>Writes label_%06d.png (1-based) for FFmpeg image2 overlay.</summary>
    internal static void WriteLabelSequencePngs(
        VideoProcessingNode node,
        string outputDirectory,
        int count,
        double timelineStartSec,
        double extractFps,
        double sourceFps,
        int probeSrcW,
        int probeSrcH,
        int probeSrcHForFontScale)
    {
        Directory.CreateDirectory(outputDirectory);
        if (count <= 0) return;
        if (extractFps <= 0) extractFps = 30;
        if (sourceFps <= 0) sourceFps = 30;

        var (estW, estH) = GetEstimatedSourceFrameSize(probeSrcW, probeSrcH, node);
        var labelBoxSrcW = Math.Max(4, (int)Math.Round(estW * node.FrameLabelW));
        var labelBoxSrcH = Math.Max(4, (int)Math.Round(estH * node.FrameLabelH));

        var sourceScale = VideoProcessingNodeExecutor.ComputeFrameLabelSourceScale(probeSrcHForFontScale > 0 ? probeSrcHForFontScale : (int?)null);
        var padVidX = Math.Max(0, (int)Math.Round(node.FrameLabelHorizontalPadding * sourceScale));
        var padVidY = Math.Max(0, (int)Math.Round(node.FrameLabelVerticalPadding * sourceScale));

        var fontPx = VideoProcessingNodeExecutor.ComputeFrameLabelDrawtextFontPixelSize(node, probeSrcHForFontScale > 0 ? probeSrcHForFontScale : (int?)null);
        fontPx = Math.Max(4, fontPx);

        var padX = padVidX;
        var padY = padVidY;

        for (var i = 1; i <= count; i++)
        {
            var tSec = timelineStartSec + (i - 1) / extractFps;
            var mediaTime = TimeSpan.FromSeconds(Math.Max(0, tSec));
            var sourceFrameApprox = Math.Max(0, (int)Math.Round(tSec * sourceFps));
            var text = FormatResolvedLabelText(node, i, sourceFrameApprox, mediaTime);

            RenderLabelStrip(node, text, labelBoxSrcW, labelBoxSrcH, padX, padY, padX, padY, fontPx, out var bmp);

            var path = Path.Combine(outputDirectory, $"label_{i:D6}.png");
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var fs = File.Create(path);
            enc.Save(fs);
        }
    }
}
