// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// High-Performance Pixel Eraser Helper for Video Slicer (120 FPS 0ms-Lag Offscreen Engine)
// =========================================================================================
import { VideoSliceFrame } from '../../../../types/video_slicer';

export interface VideoWorkingCanvasItem {
  index: number;
  canvas: HTMLCanvasElement;
  ctx: CanvasRenderingContext2D;
  naturalWidth: number;
  naturalHeight: number;
  isModified: boolean;
}

/**
 * Initializes in-memory offscreen canvases for real-time lag-free erasing across target frames.
 */
export const initVideoWorkingCanvases = (
  frames: VideoSliceFrame[],
  targetIndices: number[],
  getImageFn: (url: string) => HTMLImageElement | null
): Map<number, VideoWorkingCanvasItem> => {
  const map = new Map<number, VideoWorkingCanvasItem>();

  targetIndices.forEach((idx) => {
    const frame = frames[idx];
    if (!frame) return;

    const imgUrl = frame.transparentDataUrl || frame.originalDataUrl;
    const img = imgUrl ? getImageFn(imgUrl) : null;

    let nw = 300;
    let nh = 300;

    if (img && (img.complete || img.naturalWidth > 0)) {
      nw = img.naturalWidth || img.width || 300;
      nh = img.naturalHeight || img.height || 300;
    }

    const offscreen = document.createElement('canvas');
    offscreen.width = nw;
    offscreen.height = nh;
    const ctx = offscreen.getContext('2d', { willReadFrequently: false });
    if (!ctx) return;

    if (img && (img.complete || img.naturalWidth > 0)) {
      ctx.drawImage(img, 0, 0, nw, nh);
    }

    map.set(idx, {
      index: idx,
      canvas: offscreen,
      ctx,
      naturalWidth: nw,
      naturalHeight: nh,
      isModified: false,
    });
  });

  return map;
};

/**
 * Erases a continuous stroke segment in memory with 0ms latency
 */
export const eraseSegmentOnVideoWorkingCanvas = (
  item: VideoWorkingCanvasItem,
  frame: VideoSliceFrame,
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
  const fw = item.naturalWidth;
  const fh = item.naturalHeight;

  const centerX = stageW / 2 + panX;
  const centerY = stageH / 2 + panY;

  // Fit scale factor used when drawing image inside stage
  const maxDw = stageW * 0.85;
  const maxDh = stageH * 0.85;
  const scaleFactor = Math.min(maxDw / fw, maxDh / fh);

  const transformPoint = (stX: number, stY: number) => {
    const relX = (stX - centerX) / zoom;
    const relY = (stY - centerY) / zoom;

    const unshiftX = relX - (frame.offsetX || 0);
    const unshiftY = relY - (frame.offsetY || 0);

    const rad = (-(frame.rotation || 0) * Math.PI) / 180;
    const cos = Math.cos(rad);
    const sin = Math.sin(rad);
    const rotX = unshiftX * cos - unshiftY * sin;
    const rotY = unshiftX * sin + unshiftY * cos;

    const frameScale = frame.scale || 1.0;
    const unscaledX = (frame.flipX ? -rotX : rotX) / (frameScale * scaleFactor);
    const unscaledY = rotY / (frameScale * scaleFactor);

    return {
      x: unscaledX + fw / 2,
      y: unscaledY + fh / 2,
    };
  };

  const p1 = transformPoint(fromStageX, fromStageY);
  const p2 = transformPoint(toStageX, toStageY);

  const frameScale = frame.scale || 1.0;
  const localRadius = Math.max(1, brushRadius / (zoom * frameScale * scaleFactor));

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
  item.isModified = true;
};

/**
 * Commits ONLY modified working canvases to PNG Data URLs
 */
export const commitVideoWorkingCanvases = (
  workingCanvases: Map<number, VideoWorkingCanvasItem>
): { index: number; dataUrl: string }[] => {
  const results: { index: number; dataUrl: string }[] = [];
  workingCanvases.forEach((item) => {
    if (item.isModified) {
      results.push({
        index: item.index,
        dataUrl: item.canvas.toDataURL('image/png'),
      });
    }
  });
  return results;
};
