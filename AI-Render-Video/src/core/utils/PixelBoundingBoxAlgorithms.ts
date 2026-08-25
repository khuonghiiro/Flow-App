/**
 * Pixel Bounding Box & Smart Content Auto-Trim Algorithms for Studio 2D
 * Detects non-transparent pixel content bounds and applies dynamic padding.
 */

export interface PixelBoundingBox {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
  width: number;
  height: number;
  hasContent: boolean;
}

export interface PaddedCropRect {
  left: number;
  top: number;
  right: number;
  bottom: number;
  width: number;
  height: number;
}

/**
 * Scans an HTML5 Canvas or ImageData to find the exact bounding box of non-transparent pixels
 * @param ctx 2D Canvas rendering context
 * @param width Canvas width
 * @param height Canvas height
 * @param alphaThreshold Minimum alpha value (0-255) to consider as visible content
 */
export function detectPixelContentBoundingBox(
  ctx: CanvasRenderingContext2D,
  width: number,
  height: number,
  alphaThreshold = 10
): PixelBoundingBox {
  const imgData = ctx.getImageData(0, 0, width, height);
  const data = imgData.data;
  let minX = width;
  let minY = height;
  let maxX = -1;
  let maxY = -1;
  let hasContent = false;

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = (y * width + x) * 4;
      const alpha = data[idx + 3];
      if (alpha > alphaThreshold) {
        hasContent = true;
        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
      }
    }
  }

  if (!hasContent) {
    return {
      minX: 0,
      minY: 0,
      maxX: width - 1,
      maxY: height - 1,
      width,
      height,
      hasContent: false,
    };
  }

  return {
    minX,
    minY,
    maxX,
    maxY,
    width: Math.max(1, maxX - minX + 1),
    height: Math.max(1, maxY - minY + 1),
    hasContent: true,
  };
}

/**
 * Computes a padded bounding box expanding outwards from the detected content
 * Clamped within image boundaries [0, imageWidth] and [0, imageHeight]
 */
export function computePaddedBoundingBox(
  bbox: PixelBoundingBox,
  imageWidth: number,
  imageHeight: number,
  paddingPx: number
): PaddedCropRect {
  const p = Math.max(0, Math.round(paddingPx));
  const left = Math.max(0, bbox.minX - p);
  const top = Math.max(0, bbox.minY - p);
  const right = Math.min(imageWidth - 1, bbox.maxX + p);
  const bottom = Math.min(imageHeight - 1, bbox.maxY + p);
  const width = Math.max(1, right - left + 1);
  const height = Math.max(1, bottom - top + 1);

  return { left, top, right, bottom, width, height };
}

/**
 * Extracts and returns a new canvas and DataURL cropped to the padded bounding box
 */
export function cropCanvasWithPadding(
  sourceCanvas: HTMLCanvasElement,
  bbox: PixelBoundingBox,
  paddingPx: number
): { croppedCanvas: HTMLCanvasElement; dataUrl: string; rect: PaddedCropRect } {
  const rect = computePaddedBoundingBox(bbox, sourceCanvas.width, sourceCanvas.height, paddingPx);
  const croppedCanvas = document.createElement('canvas');
  croppedCanvas.width = rect.width;
  croppedCanvas.height = rect.height;
  const ctx = croppedCanvas.getContext('2d', { willReadFrequently: true });

  if (ctx) {
    ctx.drawImage(
      sourceCanvas,
      rect.left,
      rect.top,
      rect.width,
      rect.height,
      0,
      0,
      rect.width,
      rect.height
    );
  }

  return {
    croppedCanvas,
    dataUrl: croppedCanvas.toDataURL('image/png'),
    rect,
  };
}
