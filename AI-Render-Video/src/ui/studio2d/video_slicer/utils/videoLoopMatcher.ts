// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Loop Frame Matcher (Compares candidate frames within 3s to find best animation loop)
// =========================================================================================
import { VideoCropBBox } from '../../../../types/video_slicer';

export interface LoopMatchOptions {
  startTime: number;
  videoDuration: number;
  maxSearchSeconds?: number;
  stepSeconds?: number;
  bbox?: VideoCropBBox | null;
  shouldCancel?: () => boolean;
  onProgress?: (currentSec: number, progressPct: number) => void;
}

export interface LoopMatchResult {
  bestTimestamp: number;
  bestSimilarity: number; // 0 to 1 (1 = exact match)
  scannedFramesCount: number;
}

/**
 * Extracts a normalized 64x64 pixel buffer from a video element at its current frame
 */
function captureNormalizedFrame(
  video: HTMLVideoElement,
  canvas: HTMLCanvasElement,
  ctx: CanvasRenderingContext2D,
  bbox?: VideoCropBBox | null
): Uint8ClampedArray | null {
  const sampleSize = 64;
  canvas.width = sampleSize;
  canvas.height = sampleSize;

  const vw = video.videoWidth || 640;
  const vh = video.videoHeight || 480;

  if (bbox && bbox.width > 10 && bbox.height > 10) {
    const sx = Math.max(0, Math.min(bbox.x, vw - 10));
    const sy = Math.max(0, Math.min(bbox.y, vh - 10));
    const sw = Math.min(bbox.width, vw - sx);
    const sh = Math.min(bbox.height, vh - sy);
    ctx.drawImage(video, sx, sy, sw, sh, 0, 0, sampleSize, sampleSize);
  } else {
    ctx.drawImage(video, 0, 0, sampleSize, sampleSize);
  }

  const imgData = ctx.getImageData(0, 0, sampleSize, sampleSize);
  return imgData.data;
}

/**
 * Calculates similarity between two frame byte arrays (1 = identical, 0 = totally different)
 */
function calculateFrameSimilarity(data1: Uint8ClampedArray, data2: Uint8ClampedArray): number {
  let totalDiff = 0;
  const totalPixels = data1.length / 4;

  for (let i = 0; i < data1.length; i += 4) {
    const dr = Math.abs(data1[i] - data2[i]);
    const dg = Math.abs(data1[i + 1] - data2[i + 1]);
    const db = Math.abs(data1[i + 2] - data2[i + 2]);
    totalDiff += (dr + dg + db) / 3;
  }

  const avgDiff = totalDiff / totalPixels; // 0 to 255
  return Math.max(0, 1 - avgDiff / 255);
}

/**
 * Seeks a hidden/clone video element and waits for seeked event
 */
function seekVideoPromise(video: HTMLVideoElement, targetTime: number): Promise<void> {
  return new Promise<void>((resolve) => {
    let timeoutId: any;

    const onSeeked = () => {
      video.removeEventListener('seeked', onSeeked);
      clearTimeout(timeoutId);
      resolve();
    };

    video.addEventListener('seeked', onSeeked, { once: true });
    video.currentTime = targetTime;

    // Timeout safety fallback (150ms)
    timeoutId = setTimeout(() => {
      video.removeEventListener('seeked', onSeeked);
      resolve();
    }, 150);
  });
}

/**
 * Finds the candidate frame that most closely matches the Start Frame within max 3s
 */
export async function findBestLoopEndFrame(
  videoSourceUrl: string,
  options: LoopMatchOptions
): Promise<LoopMatchResult> {
  const {
    startTime,
    videoDuration,
    maxSearchSeconds = 3.0,
    stepSeconds = 0.04, // ~25 FPS scanning
    bbox,
    shouldCancel,
    onProgress,
  } = options;

  const searchStart = startTime + 0.25; // Skip first 0.25s to avoid comparing immediate identical frames
  const searchEnd = Math.min(startTime + maxSearchSeconds, videoDuration);

  if (searchStart >= videoDuration) {
    return { bestTimestamp: Math.min(startTime + 1.0, videoDuration), bestSimilarity: 1, scannedFramesCount: 0 };
  }

  // Create offscreen video & canvas for scanning
  const scannerVideo = document.createElement('video');
  scannerVideo.src = videoSourceUrl;
  scannerVideo.muted = true;
  scannerVideo.playsInline = true;
  scannerVideo.preload = 'auto';

  await new Promise<void>((res) => {
    scannerVideo.onloadedmetadata = () => res();
    scannerVideo.onerror = () => res();
  });

  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d');
  if (!ctx) {
    return { bestTimestamp: searchEnd, bestSimilarity: 0.5, scannedFramesCount: 0 };
  }

  // 1. Capture reference Start Frame
  await seekVideoPromise(scannerVideo, startTime);
  const startFrameBuffer = captureNormalizedFrame(scannerVideo, canvas, ctx, bbox);
  if (!startFrameBuffer) {
    return { bestTimestamp: searchEnd, bestSimilarity: 0.5, scannedFramesCount: 0 };
  }

  let bestTimestamp = searchEnd;
  let bestSimilarity = -1;
  let scannedCount = 0;

  const totalRange = searchEnd - searchStart;

  // 2. Scan forward frame by frame
  for (let t = searchStart; t <= searchEnd; t += stepSeconds) {
    if (shouldCancel && shouldCancel()) {
      break;
    }

    await seekVideoPromise(scannerVideo, t);
    const candidateBuffer = captureNormalizedFrame(scannerVideo, canvas, ctx, bbox);

    if (candidateBuffer) {
      scannedCount++;
      const similarity = calculateFrameSimilarity(startFrameBuffer, candidateBuffer);

      if (similarity > bestSimilarity) {
        bestSimilarity = similarity;
        bestTimestamp = Number(t.toFixed(2));
      }

      // Early stop if almost exact match (> 95% similarity)
      if (similarity >= 0.95) {
        break;
      }
    }

    if (onProgress && totalRange > 0) {
      const progress = Math.min(100, Math.round(((t - searchStart) / totalRange) * 100));
      onProgress(t, progress);
    }
  }

  // Cleanup scanner video element
  scannerVideo.src = '';
  scannerVideo.remove();

  return {
    bestTimestamp,
    bestSimilarity: Math.max(0, bestSimilarity),
    scannedFramesCount: scannedCount,
  };
}
