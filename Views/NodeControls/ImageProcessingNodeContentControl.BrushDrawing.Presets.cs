// ========================================================================================
// IMPORTANT FOR AI CODING ASSISTANTS & DEVELOPERS:
// DO NOT ALLOW ANY FILE IN THIS COMPONENT TO EXCEED ~1500 LINES OF CODE!
// To maintain readability, ease of testing, and modularity:
// - If a file grows larger than ~1500 lines, you MUST split/separate the logic into a new
//   partial class file (e.g., ImageProcessingNodeContentControl.<FeatureName>.cs).
// - Always place distinct features, tools, or event groupings in their respective files.
// - Ensure comments and documentation remain clean and structured.
// ========================================================================================
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;
using FlowMy.Services.Rendering;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl : UserControl
    {
        private void DrawBrushCircle(float[] alphaMask, int width, int height, double cx, double cy, double radius, double hardness, double flow,
            BrushPreset preset = BrushPreset.RoundHard)
        {
            switch (preset)
            {
                case BrushPreset.RoundSoft:
                    DrawBrush_RoundSoft(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.Flat:
                    DrawBrush_Flat(alphaMask, width, height, cx, cy, radius, hardness, flow);
                    break;
                case BrushPreset.Chalk:
                    DrawBrush_Chalk(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.Spray:
                    DrawBrush_Spray(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.Scatter:
                    DrawBrush_Scatter(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.Pencil:
                    DrawBrush_Pencil(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.Airbrush:
                    DrawBrush_Airbrush(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.Splatter:
                    DrawBrush_Splatter(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.Charcoal:
                    DrawBrush_Charcoal(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                case BrushPreset.OilBrush:
                    DrawBrush_OilBrush(alphaMask, width, height, cx, cy, radius, flow);
                    break;
                default: // RoundHard
                    DrawBrush_RoundHard(alphaMask, width, height, cx, cy, radius, hardness, flow);
                    break;
            }
        }


        /// <summary>Round Hard — cọ tròn cứng (mặc định gốc).</summary>
        private void DrawBrush_RoundHard(float[] alphaMask, int width, int height, double cx, double cy, double radius, double hardness, double flow)
        {
            double r = radius * 0.5;
            if (r <= 0.5)
            {
                int px = (int)Math.Floor(cx + 0.5);
                int py = (int)Math.Floor(cy + 0.5);
                if (px >= 0 && px < width && py >= 0 && py < height)
                {
                    if (IsInsideSelection(px, py))
                    {
                        float stampOpacity = (float)(flow / 100.0);
                        int maskOffset = py * width + px;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
                return;
            }

            double outerRadius = r + 0.5;
            int startX = Math.Max(0, (int)Math.Floor(cx - outerRadius));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + outerRadius));
            int startY = Math.Max(0, (int)Math.Floor(cy - outerRadius));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + outerRadius));

            double innerRadius = r * (hardness / 100.0);
            double range = outerRadius - innerRadius;
            double flowMul = flow / 100.0;
            double r2 = outerRadius * outerRadius;

            // Precompute 1D LUT based on squared distance ratio
            const int lutSize = 1024;
            float[] lut = new float[lutSize];
            for (int i = 0; i < lutSize; i++)
            {
                double sqRatio = (double)i / (lutSize - 1);
                double d = Math.Sqrt(sqRatio) * outerRadius;
                
                double edgeOpacity;
                if (d <= innerRadius)
                {
                    edgeOpacity = 1.0;
                }
                else
                {
                    double t = (d - innerRadius) / (range > 0 ? range : 1.0);
                    t = Math.Clamp(t, 0.0, 1.0);
                    edgeOpacity = 1.0 - (t * t * (3.0 - 2.0 * t));
                }
                lut[i] = (float)(edgeOpacity * flowMul);
            }

            bool hasSelection = (_activeSelectionGeometry != null && _hasCachedSelectionMask && _cachedSelectionMask != null);

            if (hasSelection)
            {
                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - cy;
                    double dy2 = dy * dy;
                    int maskY = y - _cachedSelectionStartY;
                    bool checkY = (y >= _cachedSelectionStartY && y <= _cachedSelectionEndY);

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - cx;
                        double dist2 = dx * dx + dy2;

                        if (dist2 <= r2)
                        {
                            if (checkY && x >= _cachedSelectionStartX && x <= _cachedSelectionEndX)
                            {
                                if (!_cachedSelectionMask[x - _cachedSelectionStartX, maskY]) continue;
                            }
                            else
                            {
                                continue;
                            }

                            int lutIdx = (int)(dist2 / r2 * 1023.0);
                            float stampOpacity = lut[lutIdx];
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
            else
            {
                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - cy;
                    double dy2 = dy * dy;

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - cx;
                        double dist2 = dx * dx + dy2;

                        if (dist2 <= r2)
                        {
                            int lutIdx = (int)(dist2 / r2 * 1023.0);
                            float stampOpacity = lut[lutIdx];
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }

        /// <summary>Round Soft — gaussian-like smooth falloff.</summary>
        private void DrawBrush_RoundSoft(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            if (r <= 0.5)
            {
                int px = (int)Math.Floor(cx + 0.5);
                int py = (int)Math.Floor(cy + 0.5);
                if (px >= 0 && px < width && py >= 0 && py < height)
                {
                    if (IsInsideSelection(px, py))
                    {
                        float stampOpacity = (float)(flow / 100.0);
                        int maskOffset = py * width + px;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
                return;
            }

            double outerRadius = r + 0.5;
            int startX = Math.Max(0, (int)Math.Floor(cx - outerRadius));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + outerRadius));
            int startY = Math.Max(0, (int)Math.Floor(cy - outerRadius));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + outerRadius));

            double flowMul = flow / 100.0;
            double sigma = r / 2.5;
            double sigma2x2 = 2.0 * sigma * sigma;
            double r2 = outerRadius * outerRadius;

            // Precompute 1D LUT based on squared distance ratio
            const int lutSize = 1024;
            float[] lut = new float[lutSize];
            for (int i = 0; i < lutSize; i++)
            {
                double sqRatio = (double)i / (lutSize - 1);
                double d = Math.Sqrt(sqRatio) * outerRadius;
                double d2 = d * d;
                double gaussianOpacity = Math.Exp(-d2 / sigma2x2);
                double edgeOpacity = Math.Clamp((outerRadius - d), 0.0, 1.0);
                lut[i] = (float)(gaussianOpacity * edgeOpacity * flowMul);
            }

            bool hasSelection = (_activeSelectionGeometry != null && _hasCachedSelectionMask && _cachedSelectionMask != null);

            if (hasSelection)
            {
                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - cy;
                    double dy2 = dy * dy;
                    int maskY = y - _cachedSelectionStartY;
                    bool checkY = (y >= _cachedSelectionStartY && y <= _cachedSelectionEndY);

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - cx;
                        double dist2 = dx * dx + dy2;

                        if (dist2 <= r2)
                        {
                            if (checkY && x >= _cachedSelectionStartX && x <= _cachedSelectionEndX)
                            {
                                if (!_cachedSelectionMask[x - _cachedSelectionStartX, maskY]) continue;
                            }
                            else
                            {
                                continue;
                            }

                            int lutIdx = (int)(dist2 / r2 * 1023.0);
                            float stampOpacity = lut[lutIdx];
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
            else
            {
                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - cy;
                    double dy2 = dy * dy;

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - cx;
                        double dist2 = dx * dx + dy2;

                        if (dist2 <= r2)
                        {
                            int lutIdx = (int)(dist2 / r2 * 1023.0);
                            float stampOpacity = lut[lutIdx];
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }

        /// <summary>Flat — cọ dẹp hình chữ nhật ngang, ratio ~3:1.</summary>
        private void DrawBrush_Flat(float[] alphaMask, int width, int height, double cx, double cy, double radius, double hardness, double flow)
        {
            double halfW = radius;          // chiều rộng = diameter
            double halfH = radius / 3.0;    // chiều cao = 1/3 diameter
            double flowMul = flow / 100.0;
            double hardnessMul = hardness / 100.0;
            double innerEdge = Math.Min(hardnessMul, Math.Max(0.0, 1.0 - 1.0 / Math.Max(1.0, radius)));

            int startX = Math.Max(0, (int)Math.Floor(cx - halfW));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + halfW));
            int startY = Math.Max(0, (int)Math.Floor(cy - halfH));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + halfH));

            for (int y = startY; y <= endY; y++)
            {
                int rowOffset = y * width;
                double dy = Math.Abs(y - cy) / halfH; // 0..1

                for (int x = startX; x <= endX; x++)
                {
                    double dx = Math.Abs(x - cx) / halfW; // 0..1
                    if (dx > 1.0 || dy > 1.0) continue;
                    if (!IsInsideSelection(x, y)) continue;

                    // Edge softness
                    double edgeDist = Math.Max(dx, dy);
                    double pixelOpacity = 1.0;
                    if (edgeDist > innerEdge && innerEdge < 1.0)
                        pixelOpacity = 1.0 - (edgeDist - innerEdge) / (1.0 - innerEdge);

                    float stampOpacity = (float)(pixelOpacity * flowMul);
                    if (stampOpacity <= 0f) continue;

                    int maskOffset = rowOffset + x;
                    alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                }
            }
        }

        private void DrawBrush_Chalk(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            double flowMul = flow / 100.0;
            double offsetMul = r * 1.0;

            if (r <= 0.5)
            {
                foreach (var offset in ChalkPresetOffsets)
                {
                    double spotCx = cx + offset.x * offsetMul;
                    double spotCy = cy + offset.y * offsetMul;
                    int px = (int)Math.Floor(spotCx + 0.5);
                    int py = (int)Math.Floor(spotCy + 0.5);
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        if (IsInsideSelection(px, py))
                        {
                            float stampOpacity = (float)(flowMul * 180.0 / 255.0);
                            if (stampOpacity <= 0f) continue;
                            int maskOffset = py * width + px;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
                return;
            }

            foreach (var offset in ChalkPresetOffsets)
            {
                double spotCx = cx + offset.x * offsetMul;
                double spotCy = cy + offset.y * offsetMul;
                double spotRadius = 0.5 + (r - 0.5) * (offset.size * 0.06);

                int startX = Math.Max(0, (int)Math.Floor(spotCx - spotRadius - 0.5));
                int endX = Math.Min(width - 1, (int)Math.Ceiling(spotCx + spotRadius + 0.5));
                int startY = Math.Max(0, (int)Math.Floor(spotCy - spotRadius - 0.5));
                int endY = Math.Min(height - 1, (int)Math.Ceiling(spotCy + spotRadius + 0.5));
                double sr2 = (spotRadius + 0.5) * (spotRadius + 0.5);

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - spotCy;
                    double dy2 = dy * dy;

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - spotCx;
                        double dist2 = dx * dx + dy2;
                        if (dist2 <= sr2)
                        {
                            if (!IsInsideSelection(x, y)) continue;

                            double dist = Math.Sqrt(dist2);
                            double edgeOpacity = Math.Clamp((spotRadius + 0.5 - dist), 0.0, 1.0);
                            float stampOpacity = (float)(edgeOpacity * flowMul * 180.0 / 255.0);
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }

        /// <summary>Spray — bình xịt, sử dụng phân bổ điểm cố định để đồng bộ hoàn toàn với preview.</summary>
        private void DrawBrush_Spray(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            double flowMul = flow / 100.0;
            double offsetMul = r * 1.0;

            if (r <= 0.5)
            {
                foreach (var offset in SprayPresetOffsets)
                {
                    double spotCx = cx + offset.x * offsetMul;
                    double spotCy = cy + offset.y * offsetMul;
                    int px = (int)Math.Floor(spotCx + 0.5);
                    int py = (int)Math.Floor(spotCy + 0.5);
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        if (IsInsideSelection(px, py))
                        {
                            double distRatio = Math.Sqrt(offset.x * offset.x + offset.y * offset.y);
                            double opacityScale = 1.0 - distRatio * 0.5;
                            float stampOpacity = (float)(opacityScale * flowMul);
                            if (stampOpacity <= 0f) continue;
                            int maskOffset = py * width + px;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
                return;
            }

            foreach (var offset in SprayPresetOffsets)
            {
                double spotCx = cx + offset.x * offsetMul;
                double spotCy = cy + offset.y * offsetMul;
                double spotRadius = 0.5 + (r - 0.5) * 0.08;

                int startX = Math.Max(0, (int)Math.Floor(spotCx - spotRadius - 0.5));
                int endX = Math.Min(width - 1, (int)Math.Ceiling(spotCx + spotRadius + 0.5));
                int startY = Math.Max(0, (int)Math.Floor(spotCy - spotRadius - 0.5));
                int endY = Math.Min(height - 1, (int)Math.Ceiling(spotCy + spotRadius + 0.5));
                double sr2 = (spotRadius + 0.5) * (spotRadius + 0.5);

                double distRatio = Math.Sqrt(offset.x * offset.x + offset.y * offset.y);
                double opacityScale = 1.0 - distRatio * 0.5;

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - spotCy;
                    double dy2 = dy * dy;

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - spotCx;
                        double dist2 = dx * dx + dy2;

                        if (dist2 <= sr2)
                        {
                            if (!IsInsideSelection(x, y)) continue;

                            double dist = Math.Sqrt(dist2);
                            double edgeOpacity = Math.Clamp((spotRadius + 0.5 - dist), 0.0, 1.0);
                            float stampOpacity = (float)(edgeOpacity * opacityScale * flowMul);
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }

        /// <summary>Scatter — điểm rải rác, sử dụng phân bổ cố định để đồng bộ hoàn toàn với preview.</summary>
        private void DrawBrush_Scatter(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            double flowMul = flow / 100.0;
            double offsetMul = r * 1.0;

            if (r <= 0.5)
            {
                foreach (var offset in ScatterPresetOffsets)
                {
                    double blobCx = cx + offset.x * offsetMul;
                    double blobCy = cy + offset.y * offsetMul;
                    int px = (int)Math.Floor(blobCx + 0.5);
                    int py = (int)Math.Floor(blobCy + 0.5);
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        if (IsInsideSelection(px, py))
                        {
                            float stampOpacity = (float)flowMul;
                            if (stampOpacity <= 0f) continue;
                            int maskOffset = py * width + px;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
                return;
            }

            foreach (var offset in ScatterPresetOffsets)
            {
                double blobCx = cx + offset.x * offsetMul;
                double blobCy = cy + offset.y * offsetMul;
                double blobRadius = 0.5 + (r - 0.5) * offset.scale * 0.4;

                int startX = Math.Max(0, (int)Math.Floor(blobCx - blobRadius - 0.5));
                int endX = Math.Min(width - 1, (int)Math.Ceiling(blobCx + blobRadius + 0.5));
                int startY = Math.Max(0, (int)Math.Floor(blobCy - blobRadius - 0.5));
                int endY = Math.Min(height - 1, (int)Math.Ceiling(blobCy + blobRadius + 0.5));
                double outerRadius = blobRadius + 0.5;

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - blobCy;
                    double dy2 = dy * dy;
                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - blobCx;
                        double dist2 = dx * dx + dy2;
                        if (dist2 <= outerRadius * outerRadius)
                        {
                            if (!IsInsideSelection(x, y)) continue;
                            double dist = Math.Sqrt(dist2);
                            double falloff = 1.0 - (dist / outerRadius);
                            double edgeOpacity = Math.Clamp((outerRadius - dist), 0.0, 1.0);
                            double pixelOpacity = falloff * edgeOpacity;
                            float stampOpacity = (float)Math.Clamp(pixelOpacity * flowMul, 0.0, 1.0);
                            if (stampOpacity <= 0f) continue;
                            
                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }

        /// <summary>Pencil — bút chì, nét nhỏ cứng với slight jitter.</summary>
        private void DrawBrush_Pencil(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            if (r <= 0.5)
            {
                int px = (int)Math.Floor(cx + 0.5);
                int py = (int)Math.Floor(cy + 0.5);
                if (px >= 0 && px < width && py >= 0 && py < height)
                {
                    if (IsInsideSelection(px, py))
                    {
                        float stampOpacity = (float)(flow / 100.0);
                        int maskOffset = py * width + px;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
                return;
            }

            // Pencil: cap radius nhỏ hơn, hardness = 100%
            double pencilRadius = Math.Min(r, Math.Max(1.5, r * 0.5));
            double flowMul = flow / 100.0;

            // Slight position jitter
            cx += (_brushRng.NextDouble() - 0.5) * 0.8;
            cy += (_brushRng.NextDouble() - 0.5) * 0.8;

            int startX = Math.Max(0, (int)Math.Floor(cx - pencilRadius));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + pencilRadius));
            int startY = Math.Max(0, (int)Math.Floor(cy - pencilRadius));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + pencilRadius));
            double r2 = pencilRadius * pencilRadius;

            for (int y = startY; y <= endY; y++)
            {
                int rowOffset = y * width;
                double dy = y - cy;
                double dy2 = dy * dy;

                for (int x = startX; x <= endX; x++)
                {
                    double dx = x - cx;
                    double dist2 = dx * dx + dy2;

                    if (dist2 <= r2)
                    {
                        if (!IsInsideSelection(x, y)) continue;

                        // Hard edge — full opacity within circle
                        float stampOpacity = (float)flowMul;
                        if (stampOpacity <= 0f) continue;

                        int maskOffset = rowOffset + x;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
            }
        }

        private void DrawBrush_Airbrush(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            if (r <= 0.5)
            {
                int px = (int)Math.Floor(cx + 0.5);
                int py = (int)Math.Floor(cy + 0.5);
                if (px >= 0 && px < width && py >= 0 && py < height)
                {
                    if (IsInsideSelection(px, py))
                    {
                        float stampOpacity = (float)(flow / 100.0);
                        int maskOffset = py * width + px;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
                return;
            }

            double outerRadius = r + 0.5;
            int startX = Math.Max(0, (int)Math.Floor(cx - outerRadius));
            int endX = Math.Min(width - 1, (int)Math.Ceiling(cx + outerRadius));
            int startY = Math.Max(0, (int)Math.Floor(cy - outerRadius));
            int endY = Math.Min(height - 1, (int)Math.Ceiling(cy + outerRadius));

            double flowMul = flow / 100.0;
            double divisor = Math.Max(0.1, r * 0.8);

            for (int y = startY; y <= endY; y++)
            {
                int rowOffset = y * width;
                double dy = y - cy;
                double dy2 = dy * dy;

                for (int x = startX; x <= endX; x++)
                {
                    double dx = x - cx;
                    double dist2 = dx * dx + dy2;

                    if (dist2 <= outerRadius * outerRadius)
                    {
                        if (!IsInsideSelection(x, y)) continue;

                        double dist = Math.Sqrt(dist2);
                        double exponent = -dist / divisor;
                        double falloff = Math.Exp(exponent);
                        double edgeOpacity = Math.Clamp(outerRadius - dist, 0.0, 1.0);
                        double pixelOpacity = falloff * edgeOpacity;

                        float stampOpacity = (float)(pixelOpacity * flowMul);
                        if (stampOpacity <= 0f) continue;

                        int maskOffset = rowOffset + x;
                        alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                    }
                }
            }
        }

        private void DrawBrush_Splatter(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            double flowMul = flow / 100.0;
            double offsetMul = r * 1.0;

            if (r <= 0.5)
            {
                foreach (var offset in SplatterPresetOffsets)
                {
                    double spotCx = cx + offset.x * offsetMul;
                    double spotCy = cy + offset.y * offsetMul;
                    int px = (int)Math.Floor(spotCx + 0.5);
                    int py = (int)Math.Floor(spotCy + 0.5);
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        if (IsInsideSelection(px, py))
                        {
                            float stampOpacity = (float)(flowMul * offset.opacity);
                            if (stampOpacity <= 0f) continue;
                            int maskOffset = py * width + px;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
                return;
            }

            foreach (var offset in SplatterPresetOffsets)
            {
                double spotCx = cx + offset.x * offsetMul;
                double spotCy = cy + offset.y * offsetMul;
                double spotRadius = 0.5 + (r - 0.5) * offset.size * 0.15;

                int startX = Math.Max(0, (int)Math.Floor(spotCx - spotRadius - 0.5));
                int endX = Math.Min(width - 1, (int)Math.Ceiling(spotCx + spotRadius + 0.5));
                int startY = Math.Max(0, (int)Math.Floor(spotCy - spotRadius - 0.5));
                int endY = Math.Min(height - 1, (int)Math.Ceiling(spotCy + spotRadius + 0.5));
                double sr2 = (spotRadius + 0.5) * (spotRadius + 0.5);

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - spotCy;
                    double dy2 = dy * dy;

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - spotCx;
                        double dist2 = dx * dx + dy2;
                        if (dist2 <= sr2)
                        {
                            if (!IsInsideSelection(x, y)) continue;

                            double dist = Math.Sqrt(dist2);
                            double edgeOpacity = Math.Clamp((spotRadius + 0.5 - dist), 0.0, 1.0);
                            float stampOpacity = (float)(edgeOpacity * flowMul * offset.opacity);
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }

        private void DrawBrush_Charcoal(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            double flowMul = flow / 100.0;
            double offsetMul = r * 1.0;

            if (r <= 0.5)
            {
                foreach (var offset in CharcoalPresetOffsets)
                {
                    double spotCx = cx + offset.x * offsetMul;
                    double spotCy = cy + offset.y * offsetMul;
                    int px = (int)Math.Floor(spotCx + 0.5);
                    int py = (int)Math.Floor(spotCy + 0.5);
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        if (IsInsideSelection(px, py))
                        {
                            double noise = Math.Abs(Math.Sin(px * 12.9898 + py * 78.233) * 43758.5453) % 1.0;
                            if (noise < 0.3) continue;
                            float stampOpacity = (float)(flowMul * offset.opacity * 200.0 / 255.0);
                            if (stampOpacity <= 0f) continue;
                            int maskOffset = py * width + px;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
                return;
            }

            foreach (var offset in CharcoalPresetOffsets)
            {
                double spotCx = cx + offset.x * offsetMul;
                double spotCy = cy + offset.y * offsetMul;
                double spotRadius = 0.5 + (r - 0.5) * offset.size * 0.18;

                int startX = Math.Max(0, (int)Math.Floor(spotCx - spotRadius - 0.5));
                int endX = Math.Min(width - 1, (int)Math.Ceiling(spotCx + spotRadius + 0.5));
                int startY = Math.Max(0, (int)Math.Floor(spotCy - spotRadius - 0.5));
                int endY = Math.Min(height - 1, (int)Math.Ceiling(spotCy + spotRadius + 0.5));
                double sr2 = (spotRadius + 0.5) * (spotRadius + 0.5);

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - spotCy;
                    double dy2 = dy * dy;

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - spotCx;
                        double dist2 = dx * dx + dy2;
                        if (dist2 <= sr2)
                        {
                            if (!IsInsideSelection(x, y)) continue;

                            double noise = Math.Abs(Math.Sin(x * 12.9898 + y * 78.233) * 43758.5453) % 1.0;
                            if (noise < 0.35) continue;

                            double dist = Math.Sqrt(dist2);
                            double edgeOpacity = Math.Clamp((spotRadius + 0.5 - dist), 0.0, 1.0);
                            float stampOpacity = (float)(edgeOpacity * flowMul * offset.opacity * 200.0 / 255.0);
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }

        private void DrawBrush_OilBrush(float[] alphaMask, int width, int height, double cx, double cy, double radius, double flow)
        {
            double r = radius * 0.5;
            double flowMul = flow / 100.0;
            double offsetMul = r * 1.0;

            if (r <= 0.5)
            {
                foreach (var offset in OilBrushPresetOffsets)
                {
                    double spotCx = cx + offset.x * offsetMul;
                    double spotCy = cy + offset.y * offsetMul;
                    int px = (int)Math.Floor(spotCx + 0.5);
                    int py = (int)Math.Floor(spotCy + 0.5);
                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        if (IsInsideSelection(px, py))
                        {
                            float stampOpacity = (float)(flowMul * 180.0 / 255.0);
                            if (stampOpacity <= 0f) continue;
                            int maskOffset = py * width + px;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
                return;
            }

            foreach (var offset in OilBrushPresetOffsets)
            {
                double spotCx = cx + offset.x * offsetMul;
                double spotCy = cy + offset.y * offsetMul;
                double spotRadius = 0.5 + (r - 0.5) * offset.size * 0.10;

                double bristleFlow = 0.6 + 0.4 * (Math.Abs(Math.Sin(offset.x * 37.13 + offset.y * 53.45) * 1000.0) % 1.0);

                int startX = Math.Max(0, (int)Math.Floor(spotCx - spotRadius - 0.5));
                int endX = Math.Min(width - 1, (int)Math.Ceiling(spotCx + spotRadius + 0.5));
                int startY = Math.Max(0, (int)Math.Floor(spotCy - spotRadius - 0.5));
                int endY = Math.Min(height - 1, (int)Math.Ceiling(spotCy + spotRadius + 0.5));
                double sr2 = (spotRadius + 0.5) * (spotRadius + 0.5);

                for (int y = startY; y <= endY; y++)
                {
                    int rowOffset = y * width;
                    double dy = y - spotCy;
                    double dy2 = dy * dy;

                    for (int x = startX; x <= endX; x++)
                    {
                        double dx = x - spotCx;
                        double dist2 = dx * dx + dy2;
                        if (dist2 <= sr2)
                        {
                            if (!IsInsideSelection(x, y)) continue;

                            double dist = Math.Sqrt(dist2);
                            double edgeOpacity = Math.Clamp((spotRadius + 0.5 - dist), 0.0, 1.0);
                            float stampOpacity = (float)(edgeOpacity * flowMul * bristleFlow);
                            if (stampOpacity <= 0f) continue;

                            int maskOffset = rowOffset + x;
                            alphaMask[maskOffset] = MathF.Max(alphaMask[maskOffset], stampOpacity);
                        }
                    }
                }
            }
        }


        private double GetBrushStep(BrushPreset preset, double radius)
        {
            if (preset == BrushPreset.RoundSoft || preset == BrushPreset.Airbrush || preset == BrushPreset.Charcoal || preset == BrushPreset.OilBrush)
            {
                return Math.Max(0.1, radius * 0.1);
            }
            else if (preset == BrushPreset.Spray || preset == BrushPreset.Scatter || preset == BrushPreset.Chalk || preset == BrushPreset.Splatter)
            {
                return Math.Max(0.1, radius * 0.15);
            }
            else if (preset == BrushPreset.Pencil)
            {
                double pencilRadius = Math.Min(radius, Math.Max(1.5, radius * 0.5));
                return Math.Max(0.1, pencilRadius * 0.1);
            }
            else
            {
                return Math.Max(0.1, radius * 0.1);
            }
        }

    }
}
