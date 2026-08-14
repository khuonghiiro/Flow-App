// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
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

    internal static bool TryGetLabelPresetFractions(int frameW, int frameH, out double wFrac, out double hFrac)
    {
        wFrac = 0;
        hFrac = 0;
        return false;
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
            ft.MaxTextWidth = Math.Max(1, Math.Max(innerW, (int)Math.Ceiling(ft.Width) + 10));
            ft.MaxTextHeight = Math.Max(1000, (int)Math.Ceiling(ft.Height) + 10);
            ft.TextAlignment = TextAlignment.Left;
            ft.Trimming = TextTrimming.None;

            // Trim font top leading gap (distance above cap height) so padTop = 0 sits flush against the top edge.
            var capsH = FrameLabelTypeface.CapsHeight * fontSizePx;
            var fontTopLeading = Math.Max(0, ft.Baseline - capsH);
            var textY = padTop - fontTopLeading;
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

        var sourceScale = VideoProcessingNodeExecutor.ComputeFrameLabelSourceScale(probeSrcHForFontScale > 0 ? probeSrcHForFontScale : (int?)null);
        var padVidLeft = Math.Max(0, (int)Math.Round(node.FrameLabelPaddingLeft * sourceScale));
        var padVidTop = Math.Max(0, (int)Math.Round(node.FrameLabelPaddingTop * sourceScale));
        var padVidRight = Math.Max(0, (int)Math.Round(node.FrameLabelPaddingRight * sourceScale));
        var padVidBottom = Math.Max(0, (int)Math.Round(node.FrameLabelPaddingBottom * sourceScale));

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

        var isPortrait = hf > wf;
        var defaultWFrac = isPortrait ? (2.0 / 3.0) : 0.20;
        var usePreset = TryGetLabelPresetFractions(wf, hf, out var labelWFrac, out var labelHFrac);
        if (!usePreset)
        {
            labelWFrac = (node.FrameLabelW <= 0.05 || Math.Abs(node.FrameLabelW - 0.18) < 0.001 || Math.Abs(node.FrameLabelW - 0.20) < 0.001)
                ? defaultWFrac
                : node.FrameLabelW;
            labelHFrac = node.FrameLabelH;
        }
        var labelBoxSrcH = Math.Max(4, (int)Math.Round(estH * labelHFrac));

        var padLeft = (int)Math.Round(padVidLeft * (wf / (double)Math.Max(1, estW)));
        var padTop = (int)Math.Round(padVidTop * (hf / (double)Math.Max(1, estH)));
        var padRight = (int)Math.Round(padVidRight * (wf / (double)Math.Max(1, estW)));
        var padBottom = (int)Math.Round(padVidBottom * (hf / (double)Math.Max(1, estH)));

        var fontPx = VideoProcessingNodeExecutor.ComputeFrameLabelDrawtextFontPixelSize(node, probeSrcHForFontScale > 0 ? probeSrcHForFontScale : (int?)null)
            * (labelBoxSrcH / (double)Math.Max(1, labelBoxSrcH));
        fontPx = Math.Max(6, fontPx);

        var ft = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            FrameLabelTypeface,
            fontPx,
            Brushes.Black,
            pixelsPerDip: 1);

        var capsH = FrameLabelTypeface.CapsHeight * fontPx;
        var visibleTextH = Math.Max(4, capsH + 0.5);
        var minBoxW = (int)Math.Ceiling(ft.Width) + padLeft + padRight;
        var minBoxH = Math.Max(1, (int)Math.Ceiling(visibleTextH) + padTop + padBottom);

        var boxW = minBoxW;
        var boxH = minBoxH;
        var boxX = (int)Math.Round(wf * node.FrameLabelX);
        var boxY = (int)Math.Round(hf * node.FrameLabelY);

        RenderLabelStrip(node, text, boxW, boxH, padLeft, padTop, padRight, padBottom, fontPx, out var labelBmp);

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
        // Frozen BitmapSource is thread-safe (Dispatcher is null).
        // BitmapEncoder still requires an STA thread.
        // If already on STA, encode directly. Otherwise spin up a temp STA thread.
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            WriteBitmapToFileCore(path, bitmap);
            return;
        }

        // Use a dedicated STA thread instead of Dispatcher.Invoke to avoid
        // blocking the main UI thread (which could cause a freeze).
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        var thread = new Thread(() =>
        {
            try
            {
                WriteBitmapToFileCore(path, bitmap);
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        tcs.Task.GetAwaiter().GetResult();
    }

    private static void WriteBitmapToFileCore(string path, BitmapSource bitmap)
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
        var isPortrait = estH > estW;
        var defaultWFrac = isPortrait ? (2.0 / 3.0) : 0.20;
        var usePreset = TryGetLabelPresetFractions(estW, estH, out var labelWFrac, out var labelHFrac);
        if (!usePreset)
        {
            labelWFrac = (node.FrameLabelW <= 0.05 || Math.Abs(node.FrameLabelW - 0.18) < 0.001 || Math.Abs(node.FrameLabelW - 0.20) < 0.001)
                ? defaultWFrac
                : node.FrameLabelW;
            labelHFrac = node.FrameLabelH;
        }
        var labelBoxSrcW = Math.Max(4, (int)Math.Round(estW * labelWFrac));
        var labelBoxSrcH = Math.Max(4, (int)Math.Round(estH * labelHFrac));

        var sourceScale = VideoProcessingNodeExecutor.ComputeFrameLabelSourceScale(probeSrcHForFontScale > 0 ? probeSrcHForFontScale : (int?)null);
        var padVidLeft = Math.Max(0, (int)Math.Round(node.FrameLabelPaddingLeft * sourceScale));
        var padVidTop = Math.Max(0, (int)Math.Round(node.FrameLabelPaddingTop * sourceScale));
        var padVidRight = Math.Max(0, (int)Math.Round(node.FrameLabelPaddingRight * sourceScale));
        var padVidBottom = Math.Max(0, (int)Math.Round(node.FrameLabelPaddingBottom * sourceScale));

        var fontPx = VideoProcessingNodeExecutor.ComputeFrameLabelDrawtextFontPixelSize(node, probeSrcHForFontScale > 0 ? probeSrcHForFontScale : (int?)null);
        fontPx = Math.Max(4, fontPx);

        for (var i = 1; i <= count; i++)
        {
            var tSec = timelineStartSec + (i - 1) / extractFps;
            var mediaTime = TimeSpan.FromSeconds(Math.Max(0, tSec));
            var sourceFrameApprox = Math.Max(0, (int)Math.Round(tSec * sourceFps));
            var text = FormatResolvedLabelText(node, i, sourceFrameApprox, mediaTime);

            var ftSeq = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                FrameLabelTypeface,
                fontPx,
                Brushes.Black,
                pixelsPerDip: 1);
            var seqCapsH = FrameLabelTypeface.CapsHeight * fontPx;
            var seqVisibleH = Math.Max(4, seqCapsH + 0.5);
            var seqMinW = (int)Math.Ceiling(ftSeq.Width) + padVidLeft + padVidRight;
            var seqMinH = Math.Max(1, (int)Math.Ceiling(seqVisibleH) + padVidTop + padVidBottom);

            var finalStripW = seqMinW;
            var finalStripH = seqMinH;

            RenderLabelStrip(node, text, finalStripW, finalStripH, padVidLeft, padVidTop, padVidRight, padVidBottom, fontPx, out var bmp);

            var path = Path.Combine(outputDirectory, $"label_{i:D6}.png");
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var fs = File.Create(path);
            enc.Save(fs);
        }
    }

    private static Color ParseOverlayItemColor(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        try
        {
            var conv = new BrushConverter();
            if (conv.ConvertFromString(value.Trim()) is SolidColorBrush scb)
                return scb.Color;
        }
        catch { /* ignore */ }
        return fallback;
    }

    /// <summary>
    /// Renders canvas overlay text to a PBGRA strip (transparent background), same fit logic as FFmpeg overlay path preview.
    /// </summary>
    internal static BitmapSource RenderOverlayTextStripBitmap(
        OverlayItem item,
        int stripW,
        int stripH,
        double parentSurfaceHeightPx)
    {
        stripW = Math.Max(4, stripW);
        stripH = Math.Max(4, stripH);
        parentSurfaceHeightPx = Math.Max(1.0, parentSurfaceHeightPx);

        var fg = ParseOverlayItemColor(item.FontColor, Colors.White);
        var text = item.Source ?? string.Empty;
        var family = string.IsNullOrWhiteSpace(item.FontFamily) ? "Arial" : item.FontFamily.Trim();
        var typeface = new Typeface(new FontFamily(family), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        // Match OverlayItemControl.AutoFitTextContent():
        // - Available client area = ActualWidth/Height minus 8px
        // - Base font size scales with parent surface height ( /1080 )
        // - Fit requires both ft.Height and ft.Width within available area
        // - TextAlignment comes from OverlayItem.TextAlignment
        var availableW = Math.Max(1, stripW - 8);
        var availableH = Math.Max(1, stripH - 8);
        var baseSize = Math.Max(8.0, item.FontSize * (parentSurfaceHeightPx / 1080.0));
        var fitSize = baseSize;
        var align = (item.TextAlignment ?? "Left").Trim().ToLowerInvariant() switch
        {
            "center" => TextAlignment.Center,
            "right" => TextAlignment.Right,
            _ => TextAlignment.Left
        };

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, stripW, stripH));

            for (var s = baseSize; s >= 6; s -= 0.5)
            {
                var ftTest = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    s,
                    new SolidColorBrush(fg),
                    pixelsPerDip: 1.0)
                {
                    MaxTextWidth = availableW,
                    MaxTextHeight = availableH,
                    Trimming = TextTrimming.None,
                    TextAlignment = align
                };

                if (ftTest.Height <= availableH && ftTest.Width <= availableW)
                {
                    fitSize = s;
                    break;
                }
            }

            var ft = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fitSize,
                new SolidColorBrush(fg),
                pixelsPerDip: 1.0)
            {
                MaxTextWidth = availableW,
                MaxTextHeight = availableH,
                Trimming = TextTrimming.None,
                TextAlignment = align
            };
            dc.DrawText(ft, new Point(4, 4));
        }

        var rtb = new RenderTargetBitmap(stripW, stripH, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    /// <summary>
    /// WPF composites visible canvas text layers onto an encoded still (like <see cref="CompositeLabelOntoStillFile"/>), pixel-aligned to decoded frame size.
    /// Layers are painted in overlay list order (earlier entries below later ones).
    /// </summary>
    internal static void CompositeCanvasTextOverlaysOntoStillFile(
        VideoProcessingNode node,
        string imagePath,
        int probeSrcW,
        int probeSrcH,
        int probeSrcHForFont)
    {
        var textLayers = node.Overlays.Where(o =>
            o.IsVisible &&
            string.Equals((o.Type ?? string.Empty).Trim(), "text", StringComparison.OrdinalIgnoreCase)).ToList();
        if (textLayers.Count == 0) return;

        if (string.Equals(Path.GetExtension(imagePath), ".webp", StringComparison.OrdinalIgnoreCase) && GetWebpEncoder() is null)
            return;

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

        var (estW, estH) = GetEstimatedSourceFrameSize(probeSrcW, probeSrcH, node);
        var hFontProbe = probeSrcHForFont > 0 ? probeSrcHForFont : (probeSrcH > 0 ? probeSrcH : estH);
        var sourceScale = VideoProcessingNodeExecutor.ComputeFrameLabelSourceScale(hFontProbe > 0 ? hFontProbe : (int?)null);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(baseFrame, new Rect(0, 0, wf, hf));

            foreach (var item in textLayers)
            {
                var boxW = Math.Max(4, (int)Math.Round(wf * Math.Clamp(item.Width, 0.01, 1)));
                var boxH = Math.Max(4, (int)Math.Round(hf * Math.Clamp(item.Height, 0.01, 1)));
                var boxX = (int)Math.Round(wf * Math.Clamp(item.X, 0, 1));
                var boxY = (int)Math.Round(hf * Math.Clamp(item.Y, 0, 1));

                // Match OverlayItemControl.AutoFitTextContent(): base font size scales by parent surface height.
                // Here parent surface = decoded/exported frame height (hf) to ensure exported still matches UI at this resolution.
                var strip = RenderOverlayTextStripBitmap(item, boxW, boxH, parentSurfaceHeightPx: hf);
                var opacity = Math.Clamp(item.Opacity, 0, 1);
                var angle = item.Rotation;
                var hasRotation = Math.Abs(angle) > 0.0001;
                if (hasRotation)
                    dc.PushTransform(new RotateTransform(angle, boxX + (boxW / 2.0), boxY + (boxH / 2.0)));
                if (opacity < 0.999)
                {
                    dc.PushOpacity(opacity);
                    dc.DrawImage(strip, new Rect(boxX, boxY, boxW, boxH));
                    dc.Pop();
                }
                else
                {
                    dc.DrawImage(strip, new Rect(boxX, boxY, boxW, boxH));
                }
                if (hasRotation)
                    dc.Pop();
            }
        }

        var composed = new RenderTargetBitmap(wf, hf, 96, 96, PixelFormats.Pbgra32);
        composed.Render(visual);
        composed.Freeze();

        WriteBitmapToFile(imagePath, composed);
    }
}
