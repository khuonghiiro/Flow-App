// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlowMy.Models.Nodes;

namespace FlowMy.Services.Workflow
{
    /// <summary>
    /// Computes grid layout geometry and composites batches of video frames into high-resolution parent collage sheets.
    /// </summary>
    public static class VideoFrameCollageComposer
    {
        public readonly record struct SlotLayout(Rect CellRect, Rect InnerRect, Rect ImageRect, int SlotIndex);

        /// <summary>
        /// Parses padding or margin string in CSS/WPF shorthand format:
        /// 1 number: uniform all 4 sides (e.g. "10" -> 10,10,10,10)
        /// 2 numbers: horizontal, vertical (e.g. "10, 20" -> 10,20,10,20)
        /// 4 numbers: left, top, right, bottom (e.g. "10, 20, 15, 25" -> 10,20,15,25)
        /// </summary>
        public static Thickness ParseThickness(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new Thickness(0);

            var clean = text.Trim().Replace("px", string.Empty, StringComparison.OrdinalIgnoreCase);
            var parts = clean.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var uniform))
            {
                var val = Math.Max(0, uniform);
                return new Thickness(val);
            }

            if (parts.Length == 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var h) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                return new Thickness(Math.Max(0, h), Math.Max(0, v), Math.Max(0, h), Math.Max(0, v));
            }

            if (parts.Length == 4 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var left) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var top) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var right) &&
                double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var bottom))
            {
                return new Thickness(Math.Max(0, left), Math.Max(0, top), Math.Max(0, right), Math.Max(0, bottom));
            }

            return new Thickness(0);
        }

        /// <summary>
        /// Calculates optimal column and row count for N frames in a canvas of size (canvasW x canvasH) with item aspect ratio.
        /// </summary>
        public static (int cols, int rows) CalculateGridDimensions(int count, int canvasW, int canvasH, double itemAspect)
        {
            if (count <= 1) return (1, 1);
            if (canvasW <= 0) canvasW = 1000;
            if (canvasH <= 0) canvasH = 1000;
            if (itemAspect <= 0.001) itemAspect = 1.0;

            var canvasAspect = canvasW / (double)canvasH;
            var idealCols = Math.Sqrt(count * (canvasAspect / itemAspect));
            var cols = Math.Clamp((int)Math.Round(idealCols), 1, count);
            var rows = (int)Math.Ceiling((double)count / cols);

            while (cols * rows < count)
                rows++;
            while (cols > 1 && (cols - 1) * rows >= count)
                cols--;

            return (Math.Max(1, cols), Math.Max(1, rows));
        }

        /// <summary>
        /// Computes bounding rectangles for every slot in the collage sheet.
        /// </summary>
        public static List<SlotLayout> CalculateSlotLayouts(
            int count,
            int canvasW,
            int canvasH,
            Thickness margin,
            Thickness padding,
            double itemAspect)
        {
            var result = new List<SlotLayout>(count);
            if (count <= 0) return result;
            if (canvasW <= 0) canvasW = 1000;
            if (canvasH <= 0) canvasH = 1000;
            if (itemAspect <= 0.001) itemAspect = 1.0;

            var (cols, rows) = CalculateGridDimensions(count, canvasW, canvasH, itemAspect);

            var totalMarginX = (margin.Left + margin.Right) * cols;
            var totalMarginY = (margin.Top + margin.Bottom) * rows;

            var cellW = Math.Max(1, (canvasW - totalMarginX) / (double)cols);
            var cellH = Math.Max(1, (canvasH - totalMarginY) / (double)rows);

            for (var i = 0; i < count; i++)
            {
                var row = i / cols;
                var col = i % cols;

                var cellX = col * (cellW + margin.Left + margin.Right) + margin.Left;
                var cellY = row * (cellH + margin.Top + margin.Bottom) + margin.Top;
                var cellRect = new Rect(cellX, cellY, cellW, cellH);

                var innerX = cellX + padding.Left;
                var innerY = cellY + padding.Top;
                var innerW = Math.Max(1, cellW - padding.Left - padding.Right);
                var innerH = Math.Max(1, cellH - padding.Top - padding.Bottom);
                var innerRect = new Rect(innerX, innerY, innerW, innerH);

                // Uniform fit inside innerRect
                var itemW = itemAspect * 1000.0;
                var itemH = 1000.0;
                var scale = Math.Min(innerW / itemW, innerH / itemH);
                var drawW = itemW * scale;
                var drawH = itemH * scale;
                var drawX = innerX + (innerW - drawW) / 2.0;
                var drawY = innerY + (innerH - drawH) / 2.0;
                var imageRect = new Rect(drawX, drawY, drawW, drawH);

                result.Add(new SlotLayout(cellRect, innerRect, imageRect, i));
            }

            return result;
        }

        /// <summary>
        /// Parses color string from Hex, ColorKey, or theme resources.
        /// </summary>
        public static Color ParseColor(string? colorStr, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(colorStr))
                return fallback;

            var trimmed = colorStr.Trim();

            // Check Application Resource dictionary (Theme brushes)
            if (Application.Current != null)
            {
                var brushObj = Application.Current.TryFindResource(trimmed)
                               ?? Application.Current.TryFindResource($"{trimmed}Brush");
                if (brushObj is SolidColorBrush scb)
                    return scb.Color;
            }

            // HTML / Hex color parse
            try
            {
                var drawingColor = System.Drawing.ColorTranslator.FromHtml(trimmed);
                return Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
            }
            catch
            {
                /* fallback to standard colors */
            }

            return trimmed.ToLowerInvariant() switch
            {
                "white" => Colors.White,
                "black" => Colors.Black,
                "transparent" => Colors.Transparent,
                "red" => Colors.Red,
                "green" => Colors.Green,
                "blue" => Colors.Blue,
                "yellow" => Colors.Yellow,
                "gray" or "grey" => Colors.Gray,
                _ => fallback
            };
        }

        /// <summary>
        /// Resolves item aspect ratio from node setting or probed video dimensions.
        /// </summary>
        public static double ResolveItemAspect(VideoProcessingNode node, double fallbackAspect = 16.0 / 9.0)
        {
            var mode = (node.GridCollageAspectMode ?? "auto").Trim().ToLowerInvariant();
            return mode switch
            {
                "16:9" => 16.0 / 9.0,
                "9:16" => 9.0 / 16.0,
                "1:1" => 1.0,
                "4:3" => 4.0 / 3.0,
                "3:4" => 3.0 / 4.0,
                "2:3" => 2.0 / 3.0,
                "3:2" => 3.0 / 2.0,
                _ => fallbackAspect > 0.05 ? fallbackAspect : (16.0 / 9.0)
            };
        }

        /// <summary>
        /// Composites a batch of frame image paths onto a parent canvas bitmap.
        /// </summary>
        public static BitmapSource CompositeBatchToBitmap(
            VideoProcessingNode node,
            IReadOnlyList<string> framePaths,
            int canvasW,
            int canvasH,
            double itemAspect)
        {
            canvasW = Math.Clamp(canvasW, 200, 8000);
            canvasH = Math.Clamp(canvasH, 200, 8000);

            var margin = ParseThickness(node.GridCollageMargin);
            var padding = ParseThickness(node.GridCollagePadding);
            var bgColor = ParseColor(node.GridCollageBackgroundColor, Colors.White);

            var slots = CalculateSlotLayouts(framePaths.Count, canvasW, canvasH, margin, padding, itemAspect);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // Background fill
                if (bgColor.A > 0)
                {
                    dc.DrawRectangle(new SolidColorBrush(bgColor), null, new Rect(0, 0, canvasW, canvasH));
                }

                for (var i = 0; i < framePaths.Count && i < slots.Count; i++)
                {
                    var path = framePaths[i];
                    if (!File.Exists(path)) continue;

                    var slot = slots[i];
                    BitmapSource? frameBmp = null;
                    try
                    {
                        using var stream = File.OpenRead(path);
                        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        if (decoder.Frames.Count > 0)
                            frameBmp = decoder.Frames[0];
                    }
                    catch
                    {
                        /* best-effort skip invalid image */
                    }

                    if (frameBmp != null)
                    {
                        var actualAspect = frameBmp.PixelWidth / (double)Math.Max(1, frameBmp.PixelHeight);
                        var targetRect = slot.ImageRect;

                        // Re-fit if actual bitmap aspect differs
                        if (Math.Abs(actualAspect - itemAspect) > 0.05)
                        {
                            var scale = Math.Min(slot.InnerRect.Width / (actualAspect * 1000.0), slot.InnerRect.Height / 1000.0);
                            var dw = actualAspect * 1000.0 * scale;
                            var dh = 1000.0 * scale;
                            var dx = slot.InnerRect.X + (slot.InnerRect.Width - dw) / 2.0;
                            var dy = slot.InnerRect.Y + (slot.InnerRect.Height - dh) / 2.0;
                            targetRect = new Rect(dx, dy, dw, dh);
                        }

                        dc.DrawImage(frameBmp, targetRect);
                    }
                }
            }

            var rtb = new RenderTargetBitmap(canvasW, canvasH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        /// <summary>
        /// Batches raw extracted frame images into composite parent collage sheets.
        /// </summary>
        public static async Task<List<string>> CreateCompositeGridSheetsAsync(
            VideoProcessingNode node,
            IReadOnlyList<string> rawFramePaths,
            string outputFolder,
            bool isBase64Mode,
            double sourceAspect,
            CancellationToken ct)
        {
            var result = new List<string>();
            if (rawFramePaths.Count == 0) return result;

            var batchSize = Math.Clamp(node.GridCollageFrameCount, 1, 128);
            var canvasW = Math.Clamp(node.GridCollageWidth, 200, 8000);
            var canvasH = Math.Clamp(node.GridCollageHeight, 200, 8000);
            var itemAspect = ResolveItemAspect(node, sourceAspect);

            var ext = (node.FrameOutputFormat ?? "png").Trim().ToLowerInvariant() switch
            {
                "jpg" or "jpeg" => "jpg",
                "webp" => "webp",
                _ => "png"
            };

            Directory.CreateDirectory(outputFolder);

            var totalBatches = (int)Math.Ceiling((double)rawFramePaths.Count / batchSize);

            for (var b = 0; b < totalBatches; b++)
            {
                ct.ThrowIfCancellationRequested();
                var batchFiles = rawFramePaths.Skip(b * batchSize).Take(batchSize).ToList();
                if (batchFiles.Count == 0) continue;

                var outPath = Path.Combine(outputFolder, $"collage_{b + 1:D6}.{ext}");

                await RunCompositorStaAsync(() =>
                {
                    var sheetBmp = CompositeBatchToBitmap(node, batchFiles, canvasW, canvasH, itemAspect);
                    SaveBitmapSourceToFile(outPath, sheetBmp, node.JpegQuality);
                }, ct).ConfigureAwait(false);

                if (File.Exists(outPath))
                    result.Add(outPath);
            }

            // Cleanup raw individual frame files to avoid clutter
            foreach (var rawFile in rawFramePaths)
            {
                try
                {
                    if (File.Exists(rawFile) && !result.Contains(rawFile, StringComparer.OrdinalIgnoreCase))
                        File.Delete(rawFile);
                }
                catch
                {
                    /* best-effort cleanup */
                }
            }

            return result;
        }

        private static Task RunCompositorStaAsync(Action work, CancellationToken ct)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                work();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            var thread = new Thread(() =>
            {
                try
                {
                    work();
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
            return tcs.Task;
        }

        private static void SaveBitmapSourceToFile(string path, BitmapSource bitmap, int jpegQuality)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            BitmapEncoder encoder = ext switch
            {
                ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = Math.Clamp(jpegQuality, 1, 100) },
                ".webp" => GetWebpEncoder() ?? new PngBitmapEncoder(),
                _ => new PngBitmapEncoder()
            };

            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }

        private static BitmapEncoder? GetWebpEncoder()
        {
            var t = Type.GetType("System.Windows.Media.Imaging.WebpBitmapEncoder, PresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
            return t != null ? (BitmapEncoder)Activator.CreateInstance(t)! : null;
        }
    }
}
