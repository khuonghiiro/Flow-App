// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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
        private void DrawBrushLine(float[] alphaMask, int width, int height, Point p1, Point p2, double radius, double hardness, double flow,
            BrushPreset preset, ref double distanceAccumulator)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);

            if (len == 0)
            {
                DrawBrushCircle(alphaMask, width, height, p1.X, p1.Y, radius, hardness, flow, preset);
                return;
            }

            double step = GetBrushStep(preset, radius);

            double d = 0;
            while (d <= len)
            {
                double remainingToStep = step - distanceAccumulator;
                if (d + remainingToStep <= len)
                {
                    d += remainingToStep;
                    double cx = p1.X + (dx * d / len);
                    double cy = p1.Y + (dy * d / len);
                    DrawBrushCircle(alphaMask, width, height, cx, cy, radius, hardness, flow, preset);
                    distanceAccumulator = 0;
                }
                else
                {
                    distanceAccumulator += (len - d);
                    break;
                }
            }
        }

        private Point CatmullRom(Point p0, Point p1, Point p2, Point p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            float x = 0.5f * (float)((2 * p1.X) + (-p0.X + p2.X) * t
                + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2
                + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
            float y = 0.5f * (float)((2 * p1.Y) + (-p0.Y + p2.Y) * t
                + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2
                + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);
            return new Point(x, y);
        }

        private void DrawBrushSplineSegment(float[] alphaMask, int width, int height, Point p0, Point p1, Point p2, Point p3, double radius, double hardness, double flow, BrushPreset preset, ref double distanceAccumulator)
        {
            double step = GetBrushStep(preset, radius);
            double estLength = Point.Subtract(p2, p1).Length;
            int subdivisions = Math.Max(20, (int)(estLength * 2.0));
            subdivisions = Math.Min(subdivisions, 200);

            Point pPrev = p1;
            for (int i = 1; i <= subdivisions; i++)
            {
                float t = (float)i / subdivisions;
                Point pCurr = CatmullRom(p0, p1, p2, p3, t);
                double dist = Point.Subtract(pCurr, pPrev).Length;

                if (dist == 0) continue;

                double dx = pCurr.X - pPrev.X;
                double dy = pCurr.Y - pPrev.Y;

                double d = 0;
                while (d <= dist)
                {
                    double remainingToStep = step - distanceAccumulator;
                    if (d + remainingToStep <= dist)
                    {
                        d += remainingToStep;
                        double cx = pPrev.X + (dx * d / dist);
                        double cy = pPrev.Y + (dy * d / dist);
                        DrawBrushCircle(alphaMask, width, height, cx, cy, radius, hardness, flow, preset);
                        distanceAccumulator = 0;
                    }
                    else
                    {
                        distanceAccumulator += (dist - d);
                        break;
                    }
                }
                pPrev = pCurr;
            }
        }

        private void ApplyStrokeToPixels(byte[] destPixels, byte[] srcPixels, float[] alphaMask, int width, int height, Color color, bool isEraser, int minX, int minY, int maxX, int maxY)
        {
            minX = Math.Clamp(minX, 0, width - 1);
            maxX = Math.Clamp(maxX, 0, width - 1);
            minY = Math.Clamp(minY, 0, height - 1);
            maxY = Math.Clamp(maxY, 0, height - 1);

            if (_selectionRect.HasValue)
            {
                minX = Math.Max(minX, (int)_selectionRect.Value.Left);
                maxX = Math.Min(maxX, (int)_selectionRect.Value.Right);
                minY = Math.Max(minY, (int)_selectionRect.Value.Top);
                maxY = Math.Min(maxY, (int)_selectionRect.Value.Bottom);
            }

            if (minX > maxX || minY > maxY) return;

            int colorA = color.A;
            int colorR = color.R;
            int colorG = color.G;
            int colorB = color.B;

            unsafe
            {
                fixed (byte* pDest = destPixels, pSrc = srcPixels)
                {
                    fixed (float* pAlpha = alphaMask)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            int rowOffset = y * width;
                            int pixelRowOffset = rowOffset * 4;
                            for (int x = minX; x <= maxX; x++)
                            {
                                int maskOffset = rowOffset + x;
                                float maskAlpha = pAlpha[maskOffset];
                                int pOffset = pixelRowOffset + x * 4;

                                if (maskAlpha <= 0.0001f)
                                {
                                    *(int*)(pDest + pOffset) = *(int*)(pSrc + pOffset);
                                    continue;
                                }

                                if (isEraser)
                                {
                                    byte srcA = pSrc[pOffset + 3];
                                    pDest[pOffset] = pSrc[pOffset];
                                    pDest[pOffset + 1] = pSrc[pOffset + 1];
                                    pDest[pOffset + 2] = pSrc[pOffset + 2];
                                    pDest[pOffset + 3] = (byte)Math.Clamp(srcA * (1.0f - maskAlpha), 0f, 255f);
                                }
                                else
                                {
                                    int bB = pSrc[pOffset];
                                    int bG = pSrc[pOffset + 1];
                                    int bR = pSrc[pOffset + 2];
                                    int bA = pSrc[pOffset + 3];

                                    float srcAlphaF = (colorA / 255.0f) * maskAlpha;
                                    if (srcAlphaF <= 0.0001f)
                                    {
                                        *(int*)(pDest + pOffset) = *(int*)(pSrc + pOffset);
                                        continue;
                                    }
                                    if (srcAlphaF >= 0.999f)
                                    {
                                        pDest[pOffset] = (byte)colorB;
                                        pDest[pOffset + 1] = (byte)colorG;
                                        pDest[pOffset + 2] = (byte)colorR;
                                        pDest[pOffset + 3] = 255;
                                        continue;
                                    }

                                    float dstAlphaF = bA / 255.0f;
                                    float outAlphaF = srcAlphaF + dstAlphaF * (1.0f - srcAlphaF);

                                    if (outAlphaF > 0f)
                                    {
                                        float invOutAlphaF = 1.0f / outAlphaF;
                                        float srcFactor = srcAlphaF * invOutAlphaF;
                                        float dstFactor = dstAlphaF * (1.0f - srcAlphaF) * invOutAlphaF;

                                        pDest[pOffset] = (byte)Math.Clamp(colorB * srcFactor + bB * dstFactor, 0f, 255f);
                                        pDest[pOffset + 1] = (byte)Math.Clamp(colorG * srcFactor + bG * dstFactor, 0f, 255f);
                                        pDest[pOffset + 2] = (byte)Math.Clamp(colorR * srcFactor + bR * dstFactor, 0f, 255f);
                                        pDest[pOffset + 3] = (byte)(outAlphaF * 255.0f + 0.5f);
                                    }
                                    else
                                    {
                                        pDest[pOffset] = 0;
                                        pDest[pOffset + 1] = 0;
                                        pDest[pOffset + 2] = 0;
                                        pDest[pOffset + 3] = 0;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void FloodFill(byte[] pixels, int width, int height, int startX, int startY, Color fillColor)
        {
            if (pixels == null || width <= 0 || height <= 0) return;
            if (startX < 0 || startX >= width || startY < 0 || startY >= height) return;

            int stride = width * 4;
            int offset = startY * stride + startX * 4;
            if (offset < 0 || offset + 3 >= pixels.Length) return;

            byte targetB = pixels[offset];
            byte targetG = pixels[offset + 1];
            byte targetR = pixels[offset + 2];
            byte targetA = pixels[offset + 3];

            byte fillB = fillColor.B;
            byte fillG = fillColor.G;
            byte fillR = fillColor.R;
            byte fillA = fillColor.A;

            // If selection exists and start point is outside the selection, do nothing.
            if (_activeSelectionGeometry != null && !IsInsideSelection(startX, startY))
                return;

            if (targetB == fillB && targetG == fillG && targetR == fillR && targetA == fillA)
                return;

            var queue = new System.Collections.Generic.Queue<Point>();
            queue.Enqueue(new Point(startX, startY));

            while (queue.Count > 0)
            {
                Point p = queue.Dequeue();
                int x = (int)p.X;
                int y = (int)p.Y;

                if (x < 0 || x >= width || y < 0 || y >= height) continue;

                if (!IsInsideSelection(x, y)) continue;

                if (_selectionRect.HasValue)
                {
                    if (x < _selectionRect.Value.Left || x > _selectionRect.Value.Right ||
                        y < _selectionRect.Value.Top || y > _selectionRect.Value.Bottom)
                    {
                        continue;
                    }
                }

                int currentOffset = y * stride + x * 4;
                if (pixels[currentOffset] == targetB &&
                    pixels[currentOffset + 1] == targetG &&
                    pixels[currentOffset + 2] == targetR &&
                    pixels[currentOffset + 3] == targetA)
                {
                    pixels[currentOffset] = fillB;
                    pixels[currentOffset + 1] = fillG;
                    pixels[currentOffset + 2] = fillR;
                    pixels[currentOffset + 3] = fillA;

                    queue.Enqueue(new Point(x + 1, y));
                    queue.Enqueue(new Point(x - 1, y));
                    queue.Enqueue(new Point(x, y + 1));
                    queue.Enqueue(new Point(x, y - 1));
                }
            }
        }

        private void PickColorWithEyedropper(int px, int py)
        {
            if (_node.EditorDoc == null) return;
            var activeLayer = _node.EditorDoc.ActiveLayer;
            if (activeLayer == null) return;

            int localX = px - activeLayer.OffsetX;
            int localY = py - activeLayer.OffsetY;

            if (localX >= 0 && localX < activeLayer.Width && localY >= 0 && localY < activeLayer.Height)
            {
                var stride = 4;
                var singlePixel = new byte[4];
                activeLayer.Bitmap.CopyPixels(new Int32Rect(localX, localY, 1, 1), singlePixel, stride, 0);

                Color picked = Color.FromArgb(singlePixel[3], singlePixel[2], singlePixel[1], singlePixel[0]);
                _node.EditorDoc.ForegroundColor = picked;
            }
        }


    }
}
