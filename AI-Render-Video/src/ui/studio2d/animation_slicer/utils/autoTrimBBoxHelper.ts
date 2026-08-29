// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { AnimationSliceFrame } from '../../../../types/animation_slicer';
import { detectPixelContentBoundingBox, cropCanvasWithPadding } from '../../../../core/utils/PixelBoundingBoxAlgorithms';

/**
 * Trims transparent empty space for a single frame
 */
export const autoTrimSingleFrameBBox = (
  frame: AnimationSliceFrame,
  padding: number = 4
): Promise<AnimationSliceFrame> => {
  return new Promise((resolve) => {
    const srcUrl = frame.transparentDataUrl || frame.originalDataUrl;
    if (!srcUrl) {
      resolve(frame);
      return;
    }

    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      const canvas = document.createElement('canvas');
      canvas.width = img.width;
      canvas.height = img.height;
      const ctx = canvas.getContext('2d');
      if (!ctx) {
        resolve(frame);
        return;
      }

      ctx.drawImage(img, 0, 0);
      const bbox = detectPixelContentBoundingBox(ctx, img.width, img.height, 10);

      if (!bbox.hasContent) {
        resolve(frame);
        return;
      }

      const cropped = cropCanvasWithPadding(canvas, bbox, padding);
      resolve({
        ...frame,
        transparentDataUrl: cropped.dataUrl,
        cropRect: {
          x: bbox.minX,
          y: bbox.minY,
          width: cropped.width,
          height: cropped.height,
        },
      });
    };
    img.onerror = () => resolve(frame);
    img.src = srcUrl;
  });
};

/**
 * Trims transparent empty bounding boxes for ALL frames in parallel
 */
export const autoTrimAllFramesBBox = async (
  frames: AnimationSliceFrame[],
  padding: number = 4
): Promise<AnimationSliceFrame[]> => {
  const promises = frames.map((f) => autoTrimSingleFrameBBox(f, padding));
  return Promise.all(promises);
};
