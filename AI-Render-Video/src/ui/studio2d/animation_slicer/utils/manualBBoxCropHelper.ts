// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { AnimationSliceFrame } from '../../../../types/animation_slicer';

export interface StageCropRect {
  x: number; // stage relative X from center
  y: number; // stage relative Y from ground line
  width: number;
  height: number;
}

/**
 * Crops a single frame image using a stage-relative bounding rectangle
 */
export const cropFrameWithStageRect = (
  frame: AnimationSliceFrame,
  cropRect: StageCropRect,
  loadedImg: HTMLImageElement
): Promise<AnimationSliceFrame> => {
  return new Promise((resolve) => {
    const fw = loadedImg.width || 200;
    const fh = loadedImg.height || 260;

    // Convert stage crop rect to local frame coordinates
    // Frame is drawn with origin at (-fw / 2, -fh) shifted by (frame.offsetX, frame.offsetY)
    const localCropX = (cropRect.x - frame.offsetX) / frame.scale + fw / 2;
    const localCropY = (cropRect.y - frame.offsetY) / frame.scale + fh;
    const localCropW = cropRect.width / frame.scale;
    const localCropH = cropRect.height / frame.scale;

    // Clamp crop bounds to valid image dimensions
    const sx = Math.max(0, Math.min(fw - 1, Math.round(localCropX)));
    const sy = Math.max(0, Math.min(fh - 1, Math.round(localCropY)));
    const sw = Math.max(4, Math.min(fw - sx, Math.round(localCropW)));
    const sh = Math.max(4, Math.min(fh - sy, Math.round(localCropH)));

    const canvas = document.createElement('canvas');
    canvas.width = sw;
    canvas.height = sh;
    const ctx = canvas.getContext('2d');
    if (!ctx) {
      resolve(frame);
      return;
    }

    ctx.drawImage(loadedImg, sx, sy, sw, sh, 0, 0, sw, sh);

    resolve({
      ...frame,
      transparentDataUrl: canvas.toDataURL('image/png'),
      cropRect: {
        x: sx,
        y: sy,
        width: sw,
        height: sh,
      },
    });
  });
};

/**
 * Crops ALL frames to the exact same stage bounding box
 */
export const cropAllFramesWithStageRect = async (
  frames: AnimationSliceFrame[],
  cropRect: StageCropRect,
  getImageFn: (url: string) => HTMLImageElement | null
): Promise<AnimationSliceFrame[]> => {
  const promises = frames.map((f) => {
    const img = getImageFn(f.transparentDataUrl || f.originalDataUrl);
    if (!img || !img.complete) {
      return Promise.resolve(f);
    }
    return cropFrameWithStageRect(f, cropRect, img);
  });
  return Promise.all(promises);
};
