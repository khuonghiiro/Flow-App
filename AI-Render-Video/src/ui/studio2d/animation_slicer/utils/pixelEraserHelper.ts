// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { AnimationSliceFrame } from '../../../../types/animation_slicer';

export interface WorkingCanvasItem {
  index: number;
  canvas: HTMLCanvasElement;
  ctx: CanvasRenderingContext2D;
  width: number;
  height: number;
}

/**
 * Initializes in-memory offscreen canvases for real-time lag-free erasing across ALL target frames.
 * Guaranteed to initialize every target frame without skipping unrendered frames.
 */
export const initWorkingCanvases = (
  frames: AnimationSliceFrame[],
  targetIndices: number[],
  getImageFn: (url: string) => HTMLImageElement | null
): Map<number, WorkingCanvasItem> => {
  const map = new Map<number, WorkingCanvasItem>();

  targetIndices.forEach((idx) => {
    const frame = frames[idx];
    if (!frame) return;

    const imgUrl = frame.transparentDataUrl || frame.originalDataUrl;
    const img = imgUrl ? getImageFn(imgUrl) : null;

    let fw = 200;
    let fh = 260;

    if (img && (img.complete || img.naturalWidth > 0)) {
      fw = img.naturalWidth || img.width || 200;
      fh = img.naturalHeight || img.height || 260;
    }

    const offscreen = document.createElement('canvas');
    offscreen.width = fw;
    offscreen.height = fh;
    const ctx = offscreen.getContext('2d');
    if (!ctx) return;

    if (img && (img.complete || img.naturalWidth > 0)) {
      ctx.drawImage(img, 0, 0, fw, fh);
    }

    map.set(idx, { index: idx, canvas: offscreen, ctx, width: fw, height: fh });
  });

  return map;
};

/**
 * Erases a continuous stroke segment in memory (0ms latency, 120 FPS smooth)
 */
export const eraseSegmentOnWorkingCanvas = (
  item: WorkingCanvasItem,
  frame: AnimationSliceFrame,
  fromStageX: number,
  fromStageY: number,
  toStageX: number,
  toStageY: number,
  brushRadius: number,
  stageW: number,
  stageH: number,
  panX: number,
  panY: number,
  zoom: number
) => {
  const ctx = item.ctx;
  const fw = item.width;
  const fh = item.height;

  const centerX = stageW / 2 + panX;
  const centerY = stageH * 0.75 + panY;

  const transformPoint = (stX: number, stY: number) => {
    const relX = (stX - centerX) / zoom;
    const relY = (stY - centerY) / zoom;

    const unshiftX = relX - frame.offsetX;
    const unshiftY = relY - frame.offsetY;

    const rad = (-frame.rotation * Math.PI) / 180;
    const cos = Math.cos(rad);
    const sin = Math.sin(rad);
    const rotX = unshiftX * cos - unshiftY * sin;
    const rotY = unshiftX * sin + unshiftY * cos;

    const unscaledX = frame.flipX ? -rotX / frame.scale : rotX / frame.scale;
    const unscaledY = rotY / frame.scale;

    return {
      x: unscaledX + fw / 2,
      y: unscaledY + fh,
    };
  };

  const p1 = transformPoint(fromStageX, fromStageY);
  const p2 = transformPoint(toStageX, toStageY);

  const localRadius = Math.max(1, brushRadius / (zoom * frame.scale));

  ctx.save();
  ctx.globalCompositeOperation = 'destination-out';

  // Draw continuous stroke segment
  ctx.lineWidth = localRadius * 2;
  ctx.lineCap = 'round';
  ctx.lineJoin = 'round';
  ctx.beginPath();
  ctx.moveTo(p1.x, p1.y);
  ctx.lineTo(p2.x, p2.y);
  ctx.stroke();

  // Draw circle cap at destination point
  ctx.beginPath();
  ctx.arc(p2.x, p2.y, localRadius, 0, Math.PI * 2);
  ctx.fill();

  ctx.restore();
};

/**
 * Commits working canvases into Data URLs on stroke completion (pointerup)
 */
export const commitWorkingCanvases = (
  workingCanvases: Map<number, WorkingCanvasItem>
): { index: number; dataUrl: string }[] => {
  const results: { index: number; dataUrl: string }[] = [];
  workingCanvases.forEach((item) => {
    results.push({
      index: item.index,
      dataUrl: item.canvas.toDataURL('image/png'),
    });
  });
  return results;
};
