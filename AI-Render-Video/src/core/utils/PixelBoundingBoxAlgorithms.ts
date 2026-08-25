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
 * Scans an HTML5 Canvas or ImageData to find the exact bounding box of non-transparent pixels.
 * Scans minX, maxX, minY, maxY based on first non-transparent pixel (alpha > alphaThreshold).
 * @param ctx 2D Canvas rendering context
 * @param width Canvas width
 * @param height Canvas height
 * @param alphaThreshold Minimum alpha value (0-255) to consider as visible content (default 0)
 */
export function detectPixelContentBoundingBox(
  ctx: CanvasRenderingContext2D,
  width: number,
  height: number,
  alphaThreshold = 0
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
 * Computes a padded bounding box expanding outwards from the detected content.
 * Clamped within image boundaries [0, imageWidth - 1] and [0, imageHeight - 1].
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

import { ChromaProcessOptions, processCellChromaAndDespeckle } from './ChromaDespeckleProcessor';

/**
 * Detects the bounding box rectangle of the non-transparent/foreground subject in an image.
 * Uses chroma key processing if specified to find the subject even on colored backgrounds.
 */
export function detectImageBBoxRect(
  source: HTMLImageElement | HTMLCanvasElement,
  chromaOpts?: ChromaProcessOptions,
  paddingPx: number = 0,
  alphaThreshold = 20
): PaddedCropRect | null {
  const w = source instanceof HTMLImageElement ? (source.naturalWidth || source.width) : source.width;
  const h = source instanceof HTMLImageElement ? (source.naturalHeight || source.height) : source.height;
  if (!w || !h) return null;

  const tempCanvas = document.createElement('canvas');
  tempCanvas.width = w;
  tempCanvas.height = h;
  const ctx = tempCanvas.getContext('2d', { willReadFrequently: true });
  if (!ctx) return null;

  ctx.drawImage(source, 0, 0);
  if (chromaOpts && chromaOpts.keyColorType) {
    processCellChromaAndDespeckle(ctx, w, h, {
      ...chromaOpts,
      despeckleSize: Math.max(chromaOpts.despeckleSize || 0, 4),
      whiteSpeckleSensitivity: Math.max(chromaOpts.whiteSpeckleSensitivity || 0, 30),
    });
  }

  const bbox = detectPixelContentBoundingBox(ctx, w, h, alphaThreshold);
  if (!bbox.hasContent) return null;

  return computePaddedBoundingBox(bbox, w, h, paddingPx);
}

