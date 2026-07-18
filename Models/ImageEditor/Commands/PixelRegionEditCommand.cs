// ========================================================================================
// IMPORTANT FOR AI CODING ASSISTANTS:
// This file contains the region-based undo command for pixel edits.
// It only stores the dirty region (bounding box of changed pixels) instead of the
// full bitmap, reducing memory usage by 95%+ for brush/eraser operations on large images.
// For full-bitmap undo (transforms, fills), use PixelEditCommand instead.
// ========================================================================================
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>
    /// Region-based undo command for brush/eraser pixel edits.
    /// Instead of storing the entire bitmap (e.g., 36MB for 3000×3000), 
    /// this command only stores the dirty region (bounding box of changed pixels).
    /// Example: brush 50px on 3000×3000 → stores ~200×200 = 160KB instead of 36MB.
    /// </summary>
    public sealed class PixelRegionEditCommand : IEditorCommand
    {
        private readonly EditorLayer _layer;
        private readonly Int32Rect _dirtyRect;
        private readonly byte[] _oldRegionPixels;
        private readonly byte[] _newRegionPixels;
        private readonly int _regionStride;

        private readonly bool _hadOriginalTransform;
        private readonly WriteableBitmap? _oldOriginalTransformBitmap;
        private readonly double _oldScaleX, _oldScaleY, _oldAngle, _oldTranslateX, _oldTranslateY;
        private readonly Rect _oldContentBounds;
        private readonly Geometry? _oldContentGeometry;

        public string Description => "Paint/Erase (Region)";

        /// <summary>
        /// Create a region-based undo command.
        /// </summary>
        /// <param name="layer">Target layer</param>
        /// <param name="dirtyRect">Bounding box of changed pixels (clipped to layer bounds)</param>
        /// <param name="oldRegionPixels">Pixel data of the dirty region BEFORE the edit</param>
        /// <param name="newRegionPixels">Pixel data of the dirty region AFTER the edit</param>
        public PixelRegionEditCommand(
            EditorLayer layer,
            Int32Rect dirtyRect,
            byte[] oldRegionPixels,
            byte[] newRegionPixels)
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _dirtyRect = dirtyRect;
            _oldRegionPixels = oldRegionPixels ?? throw new ArgumentNullException(nameof(oldRegionPixels));
            _newRegionPixels = newRegionPixels ?? throw new ArgumentNullException(nameof(newRegionPixels));
            _regionStride = dirtyRect.Width * 4;

            _oldContentGeometry = layer.ContentGeometry;

            _hadOriginalTransform = (layer.OriginalTransformBitmap != null);
            if (_hadOriginalTransform)
            {
                _oldOriginalTransformBitmap = layer.OriginalTransformBitmap;
                _oldScaleX = layer.LayerScaleX;
                _oldScaleY = layer.LayerScaleY;
                _oldAngle = layer.LayerAngle;
                _oldTranslateX = layer.LayerTranslateX;
                _oldTranslateY = layer.LayerTranslateY;
                _oldContentBounds = layer.ContentBounds;
            }
        }

        /// <summary>
        /// Create a region-based undo command by computing dirty rect from old/new full pixels.
        /// This scans old vs new to find the minimal changed bounding box, 
        /// then extracts only that region for storage.
        /// </summary>
        public static PixelRegionEditCommand FromFullPixels(
            EditorLayer layer,
            byte[] oldFullPixels,
            byte[] newFullPixels,
            Int32Rect? hintRect = null)
        {
            int w = layer.Width;
            int h = layer.Height;
            int stride = w * 4;

            // Use hint rect if provided (from stroke bounds tracking)
            Int32Rect dirtyRect;
            if (hintRect.HasValue && hintRect.Value.Width > 0 && hintRect.Value.Height > 0)
            {
                dirtyRect = ClipRect(hintRect.Value, w, h);
            }
            else
            {
                // Scan to find actual dirty region (fallback)
                dirtyRect = FindDirtyRect(oldFullPixels, newFullPixels, w, h);
            }

            if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
            {
                // No change detected — create minimal 1x1 no-op command
                dirtyRect = new Int32Rect(0, 0, 1, 1);
                byte[] singleOld = new byte[4];
                byte[] singleNew = new byte[4];
                Array.Copy(oldFullPixels, 0, singleOld, 0, 4);
                Array.Copy(newFullPixels, 0, singleNew, 0, 4);
                return new PixelRegionEditCommand(layer, dirtyRect, singleOld, singleNew);
            }

            // Extract region pixels
            int regionStride = dirtyRect.Width * 4;
            int regionSize = regionStride * dirtyRect.Height;
            byte[] oldRegion = new byte[regionSize];
            byte[] newRegion = new byte[regionSize];

            for (int y = 0; y < dirtyRect.Height; y++)
            {
                int srcOffset = (dirtyRect.Y + y) * stride + dirtyRect.X * 4;
                int dstOffset = y * regionStride;
                Array.Copy(oldFullPixels, srcOffset, oldRegion, dstOffset, regionStride);
                Array.Copy(newFullPixels, srcOffset, newRegion, dstOffset, regionStride);
            }

            return new PixelRegionEditCommand(layer, dirtyRect, oldRegion, newRegion);
        }

        public void Execute()
        {
            if (_dirtyRect.Width <= 0 || _dirtyRect.Height <= 0) return;
            _layer.Bitmap.WritePixels(_dirtyRect, _newRegionPixels, _regionStride, 0);
            _layer.OriginalTransformBitmap = null;
            _layer.ContentGeometry = null;
            _layer.PngBytes = null;
            _layer.InvalidateThumbnail();
        }

        public void Undo()
        {
            if (_dirtyRect.Width <= 0 || _dirtyRect.Height <= 0) return;
            _layer.Bitmap.WritePixels(_dirtyRect, _oldRegionPixels, _regionStride, 0);
            _layer.PngBytes = null;
            
            if (_hadOriginalTransform)
            {
                _layer.LayerScaleX = _oldScaleX;
                _layer.LayerScaleY = _oldScaleY;
                _layer.LayerAngle = _oldAngle;
                _layer.LayerTranslateX = _oldTranslateX;
                _layer.LayerTranslateY = _oldTranslateY;
                _layer.OriginalTransformBitmap = _oldOriginalTransformBitmap;
                _layer.ContentBounds = _oldContentBounds;
            }
            else
            {
                _layer.OriginalTransformBitmap = null;
            }
            
            _layer.ContentGeometry = _oldContentGeometry;
            _layer.InvalidateThumbnail();
        }

        /// <summary>Scan two full pixel arrays to find the minimal bounding box of changes.</summary>
        private static Int32Rect FindDirtyRect(byte[] oldPx, byte[] newPx, int w, int h)
        {
            int minX = w, maxX = -1, minY = h, maxY = -1;
            int stride = w * 4;

            // Use Parallel scan for large images
            if (w * h > 1_000_000)
            {
                // Thread-safe min/max tracking
                int localMinX = w, localMaxX = -1, localMinY = h, localMaxY = -1;
                object lockObj = new object();

                System.Threading.Tasks.Parallel.For(0, h, () => (w, -1, h, -1),
                    (y, _, localState) =>
                    {
                        int rowOffset = y * stride;
                        int lMinX = localState.Item1, lMaxX = localState.Item2;
                        int lMinY = localState.Item3, lMaxY = localState.Item4;

                        for (int x = 0; x < w; x++)
                        {
                            int idx = rowOffset + x * 4;
                            if (oldPx[idx] != newPx[idx] ||
                                oldPx[idx + 1] != newPx[idx + 1] ||
                                oldPx[idx + 2] != newPx[idx + 2] ||
                                oldPx[idx + 3] != newPx[idx + 3])
                            {
                                if (x < lMinX) lMinX = x;
                                if (x > lMaxX) lMaxX = x;
                                if (y < lMinY) lMinY = y;
                                if (y > lMaxY) lMaxY = y;
                            }
                        }

                        return (lMinX, lMaxX, lMinY, lMaxY);
                    },
                    localState =>
                    {
                        lock (lockObj)
                        {
                            if (localState.Item1 < localMinX) localMinX = localState.Item1;
                            if (localState.Item2 > localMaxX) localMaxX = localState.Item2;
                            if (localState.Item3 < localMinY) localMinY = localState.Item3;
                            if (localState.Item4 > localMaxY) localMaxY = localState.Item4;
                        }
                    });

                minX = localMinX;
                maxX = localMaxX;
                minY = localMinY;
                maxY = localMaxY;
            }
            else
            {
                for (int y = 0; y < h; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int idx = rowOffset + x * 4;
                        if (oldPx[idx] != newPx[idx] ||
                            oldPx[idx + 1] != newPx[idx + 1] ||
                            oldPx[idx + 2] != newPx[idx + 2] ||
                            oldPx[idx + 3] != newPx[idx + 3])
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
            }

            if (maxX < 0 || maxY < 0)
                return new Int32Rect(0, 0, 0, 0);

            return new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static Int32Rect ClipRect(Int32Rect rect, int w, int h)
        {
            int x = Math.Max(0, rect.X);
            int y = Math.Max(0, rect.Y);
            int right = Math.Min(w, rect.X + rect.Width);
            int bottom = Math.Min(h, rect.Y + rect.Height);
            return new Int32Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
        }
    }
}
