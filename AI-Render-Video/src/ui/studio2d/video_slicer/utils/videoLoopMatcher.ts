// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Loop Frame Matcher (Accurate Decoded Pixel Capture & Similarity Search)
// =========================================================================================
import { VideoCropBBox } from '../../../../types/video_slicer';

export interface LoopMatchOptions {
  startTime: number;
  videoDuration: number;
  maxSearchSeconds?: number;
  stepSeconds?: number;
  minSearchOffset?: number; // Minimum offset after startTime (default 0.8s)
  bbox?: VideoCropBBox | null;
  shouldCancel?: () => boolean;
  onProgress?: (currentSec: number, progressPct: number) => void;
}

export interface LoopMatchResult {
  bestTimestamp: number;
  bestSimilarity: number;
  scannedFramesCount: number;
}

/**
 * Extracts a normalized 64x64 pixel buffer from a video element at its current frame
 * If bbox is provided (percentage or pixel), crops strictly to that detail region
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

  ctx.clearRect(0, 0, sampleSize, sampleSize);

  if (bbox && bbox.width > 0 && bbox.height > 0) {
    const isPct = bbox.width <= 100 && bbox.height <= 100;
    const rawX = isPct ? (bbox.x / 100) * vw : bbox.x;
    const rawY = isPct ? (bbox.y / 100) * vh : bbox.y;
    const rawW = isPct ? (bbox.width / 100) * vw : bbox.width;
    const rawH = isPct ? (bbox.height / 100) * vh : bbox.height;

    const clampedX = Math.max(0, Math.min(rawX, vw - 10));
    const clampedY = Math.max(0, Math.min(rawY, vh - 10));
    const clampedW = Math.max(10, Math.min(rawW, vw - clampedX));
    const clampedH = Math.max(10, Math.min(rawH, vh - clampedY));

    ctx.drawImage(video, clampedX, clampedY, clampedW, clampedH, 0, 0, sampleSize, sampleSize);
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

  const avgDiff = totalDiff / totalPixels;
  return Math.max(0, 1 - avgDiff / 255);
}

/**
 * Seeks a video element to target time and waits until decoded frame data is ready
 */
function seekVideoPromise(video: HTMLVideoElement, targetTime: number): Promise<void> {
  return new Promise<void>((resolve) => {
    let timeoutId: any;

    const onSeeked = () => {
      video.removeEventListener('seeked', onSeeked);
      clearTimeout(timeoutId);

      // If video has decoded current frame, resolve immediately
      if (video.readyState >= 2) {
        resolve();
      } else {
        const onCanPlay = () => {
          video.removeEventListener('canplay', onCanPlay);
          video.removeEventListener('loadeddata', onCanPlay);
          resolve();
        };
        video.addEventListener('canplay', onCanPlay, { once: true });
        video.addEventListener('loadeddata', onCanPlay, { once: true });
      }
    };

    video.addEventListener('seeked', onSeeked, { once: true });

    try {
      video.currentTime = targetTime;
    } catch {
      resolve();
    }

    // Safety timeout fallback (300ms)
    timeoutId = setTimeout(() => {
      video.removeEventListener('seeked', onSeeked);
      resolve();
    }, 300);
  });
}

/**
 * Finds the candidate frame that most closely matches the Start Frame within search duration
 * Ensures Start Frame is decoded accurately and candidates start after minSearchOffset (0.8s)
 */
export async function findBestLoopEndFrame(
  videoSourceUrl: string,
  options: LoopMatchOptions
): Promise<LoopMatchResult> {
  const {
    startTime,
    videoDuration,
    maxSearchSeconds = 3.0,
    minSearchOffset = 0.80, // Minimum 0.8s offset
    stepSeconds = 0.04, // ~25 fps scan precision
    bbox,
    shouldCancel,
    onProgress,
  } = options;

  const video = document.createElement('video');
  video.muted = true;
  video.playsInline = true;
  video.preload = 'auto';
  video.src = videoSourceUrl;

  // Wait until video metadata and initial frame data are decoded
  await new Promise<void>((resolve, reject) => {
    const onReady = () => {
      if (video.readyState >= 2) {
        video.removeEventListener('loadeddata', onReady);
        video.removeEventListener('canplay', onReady);
        resolve();
      }
    };

    video.addEventListener('loadeddata', onReady);
    video.addEventListener('canplay', onReady);
    video.onerror = () => reject(new Error('Không thể nạp video để quét vòng lặp'));

    // Fallback if readyState is already satisfied
    if (video.readyState >= 2) {
      resolve();
    }
  });

  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Không thể khởi tạo Canvas 2D');

  // Step 1: Seek to Start Frame and capture real decoded pixel buffer
  await seekVideoPromise(video, startTime);
  const startFrameBuffer = captureNormalizedFrame(video, canvas, ctx, bbox);
  if (!startFrameBuffer) {
    throw new Error('Không thể đọc dữ liệu Start Frame');
  }

  // Step 2: Iterate candidate frames starting at least minSearchOffset (0.8s) after startTime
  const searchMinTime = Math.min(startTime + minSearchOffset, videoDuration);
  const searchMaxTime = Math.min(startTime + Math.max(minSearchOffset, maxSearchSeconds), videoDuration);
  const totalSearchRange = Math.max(0.1, searchMaxTime - searchMinTime);

  let bestTimestamp = searchMinTime;
  let bestSimilarity = -1;
  let scannedFramesCount = 0;

  for (let t = searchMinTime; t <= searchMaxTime; t += stepSeconds) {
    if (shouldCancel && shouldCancel()) {
      break;
    }

    await seekVideoPromise(video, t);
    const candidateBuffer = captureNormalizedFrame(video, canvas, ctx, bbox);
    if (!candidateBuffer) continue;

    scannedFramesCount++;
    const sim = calculateFrameSimilarity(startFrameBuffer, candidateBuffer);

    if (sim > bestSimilarity) {
      bestSimilarity = sim;
      bestTimestamp = Number(t.toFixed(2));
    }

    if (onProgress) {
      const prog = Math.min(100, Math.round(((t - searchMinTime) / totalSearchRange) * 100));
      onProgress(t, prog);
    }

    // Early exit if near perfect match found (> 96%)
    if (sim >= 0.96) {
      break;
    }
  }

  // Cleanup
  video.src = '';
  video.remove();

  return {
    bestTimestamp,
    bestSimilarity: Math.max(0, bestSimilarity),
    scannedFramesCount,
  };
}
