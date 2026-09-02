// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Frame Extraction Engine (Multi-threaded FFmpeg Engine + Hardware GPU Canvas)
// =========================================================================================
import { VideoSliceFrame, VideoMetadata, VideoExtractOptions } from '../../../../types/video_slicer';
import { getAIMattingApiUrl } from '../../../../core/config/envConfig';

/**
 * Loads video metadata from a File or Blob URL
 */
export async function loadVideoMetadata(fileOrUrl: File | string): Promise<VideoMetadata> {
  const url = typeof fileOrUrl === 'string' ? fileOrUrl : URL.createObjectURL(fileOrUrl);
  const name = typeof fileOrUrl === 'string' ? 'video.mp4' : fileOrUrl.name;
  const sizeBytes = typeof fileOrUrl === 'string' ? 0 : fileOrUrl.size;

  return new Promise((resolve, reject) => {
    const video = document.createElement('video');
    video.preload = 'auto';
    video.src = url;
    video.muted = true;
    video.playsInline = true;
    video.crossOrigin = 'anonymous';

    let timer: any;
    const onReady = () => {
      clearTimeout(timer);
      const duration = video.duration || 1;
      const width = video.videoWidth || 640;
      const height = video.videoHeight || 360;
      resolve({
        name,
        duration,
        width,
        height,
        fps: 24,
        sizeBytes,
        dataUrl: url,
      });
    };

    if (video.readyState >= 1) {
      onReady();
    } else {
      video.onloadedmetadata = onReady;
      video.onerror = () => {
        clearTimeout(timer);
        reject(new Error('Không thể đọc thông tin video. Định dạng tệp có thể không tương thích.'));
      };
      timer = setTimeout(onReady, 2000);
      video.load();
    }
  });
}

/**
 * Helper to convert a blob URL to a Base64 string for backend transmission
 */
async function blobUrlToBase64(blobUrl: string): Promise<string> {
  if (!blobUrl.startsWith('blob:')) {
    return blobUrl;
  }
  const response = await fetch(blobUrl);
  const blob = await response.blob();
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onloadend = () => resolve(reader.result as string);
    reader.onerror = reject;
    reader.readAsDataURL(blob);
  });
}

/**
 * Seeks a video element to target time with minimal latency
 */
function seekVideoForFrame(video: HTMLVideoElement, targetTime: number): Promise<void> {
  return new Promise<void>((resolve) => {
    let timeoutId: any;

    const onSeeked = () => {
      video.removeEventListener('seeked', onSeeked);
      clearTimeout(timeoutId);
      resolve();
    };

    video.addEventListener('seeked', onSeeked, { once: true });

    try {
      video.currentTime = targetTime;
    } catch {
      resolve();
      return;
    }

    timeoutId = setTimeout(() => {
      video.removeEventListener('seeked', onSeeked);
      resolve();
    }, 120);
  });
}

/**
 * Extracts frames client-side using HTML5 Video + Canvas
 * Initialises video texture pipeline to avoid blank/transparent frames
 */
export async function extractFramesClientSide(
  videoUrl: string,
  options: VideoExtractOptions,
  onProgress?: (progress: number, current: number, total: number) => void
): Promise<VideoSliceFrame[]> {
  const { fps, startTime, endTime, crop } = options;
  const video = document.createElement('video');
  video.src = videoUrl;
  video.muted = true;
  video.playsInline = true;
  video.crossOrigin = 'anonymous';
  video.preload = 'auto';

  // Wait until video metadata and initial frame are ready
  await new Promise<void>((resolve) => {
    let timer: any;
    const finish = () => {
      clearTimeout(timer);
      video.removeEventListener('loadedmetadata', finish);
      video.removeEventListener('loadeddata', finish);
      video.removeEventListener('canplay', finish);
      resolve();
    };

    if (video.readyState >= 2) {
      finish();
      return;
    }

    video.addEventListener('loadedmetadata', finish, { once: true });
    video.addEventListener('loadeddata', finish, { once: true });
    video.addEventListener('canplay', finish, { once: true });
    video.onerror = () => finish();
    timer = setTimeout(finish, 1500);
    video.load();
  });

  // Wake up Chrome GPU frame decoder pipeline
  try {
    const p = video.play();
    if (p) await p.catch(() => {});
    video.pause();
  } catch {
    // Ignore autoplay policy restriction
  }

  const actualStart = Math.max(0, startTime);
  const actualEnd = endTime > actualStart ? endTime : (video.duration || actualStart + 1);
  const duration = Math.max(0.05, actualEnd - actualStart);

  const totalFrames = Math.max(1, Math.round(duration * fps));
  const interval = totalFrames > 1 ? duration / (totalFrames - 1) : 0;

  const srcW = video.videoWidth || 640;
  const srcH = video.videoHeight || 480;

  let cropX = 0;
  let cropY = 0;
  let cropW = srcW;
  let cropH = srcH;

  if (crop && crop.width > 0 && crop.height > 0) {
    const isPct = crop.width <= 100 && crop.height <= 100;
    const rawX = isPct ? (crop.x / 100) * srcW : crop.x;
    const rawY = isPct ? (crop.y / 100) * srcH : crop.y;
    const rawW = isPct ? (crop.width / 100) * srcW : crop.width;
    const rawH = isPct ? (crop.height / 100) * srcH : crop.height;

    cropX = Math.max(0, Math.min(rawX, srcW - 10));
    cropY = Math.max(0, Math.min(rawY, srcH - 10));
    cropW = Math.max(10, Math.min(rawW, srcW - cropX));
    cropH = Math.max(10, Math.min(rawH, srcH - cropY));
  }

  const canvas = document.createElement('canvas');
  canvas.width = Math.max(10, Math.round(cropW));
  canvas.height = Math.max(10, Math.round(cropH));

  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Không thể khởi tạo 2D Context Canvas');

  const frames: VideoSliceFrame[] = [];

  for (let i = 0; i < totalFrames; i++) {
    const targetTime =
      totalFrames === 1
        ? actualStart
        : Math.min(actualStart + i * interval, actualEnd);

    await seekVideoForFrame(video, targetTime);

    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(video, cropX, cropY, cropW, cropH, 0, 0, canvas.width, canvas.height);

    const dataUrl = canvas.toDataURL('image/png');

    frames.push({
      id: `vframe_${Date.now()}_${i}`,
      index: i,
      timestamp: Number(targetTime.toFixed(3)),
      originalDataUrl: dataUrl,
      transparentDataUrl: dataUrl,
      cropRect: {
        x: Number(cropX.toFixed(1)),
        y: Number(cropY.toFixed(1)),
        width: Number(cropW.toFixed(1)),
        height: Number(cropH.toFixed(1)),
      },
      durationMs: Math.round(1000 / fps),
      offsetX: 0,
      offsetY: 0,
      scale: 1.0,
      rotation: 0,
      flipX: false,
    });

    if (onProgress) {
      const pct = Math.round(((i + 1) / totalFrames) * 100);
      onProgress(pct, i + 1, totalFrames);
    }
  }

  // Cleanup
  video.src = '';
  video.remove();

  return frames;
}

/**
 * Extracts frames via multi-threaded FFmpeg Backend API endpoint (RTX 3060 / CPU Multi-threading)
 */
export async function extractFramesFFmpegBackend(
  videoDataUrl: string,
  options: VideoExtractOptions
): Promise<VideoSliceFrame[]> {
  const apiUrl = getAIMattingApiUrl('/api/video/extract-frames');

  // Convert blob URL to Base64 MP4 before transmission
  const base64Video = await blobUrlToBase64(videoDataUrl);

  const res = await fetch(apiUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      video: base64Video,
      video_data_url: base64Video,
      fps: options.fps,
      startTime: options.startTime,
      start_time: options.startTime,
      endTime: options.endTime,
      end_time: options.endTime,
      maxFrames: options.maxFrames || 500,
      max_frames: options.maxFrames || 500,
      crop: options.crop,
    }),
  });

  if (!res.ok) {
    const errJson = await res.json().catch(() => ({}));
    throw new Error(errJson.error || errJson.detail || 'Lỗi từ máy chủ FFmpeg trích xuất video');
  }

  const data = await res.json();
  const rawFrames: any[] = data.frames || [];

  if (rawFrames.length === 0) {
    throw new Error('Máy chủ FFmpeg không trả về khung hình nào');
  }

  return rawFrames.map((f: any, idx: number) => {
    const url = typeof f === 'string' ? f : f.data_url;
    const ts =
      typeof f === 'string'
        ? options.startTime + idx * (1 / options.fps)
        : f.timestamp ?? options.startTime + idx * (1 / options.fps);

    return {
      id: `ffmpeg_${idx}_${Date.now()}`,
      index: idx,
      timestamp: Number(Number(ts).toFixed(3)),
      originalDataUrl: url,
      transparentDataUrl: url,
      cropRect: options.crop || { x: 0, y: 0, width: 0, height: 0 },
      durationMs: Math.round(1000 / options.fps),
      offsetX: 0,
      offsetY: 0,
      scale: 1.0,
      rotation: 0,
      flipX: false,
    };
  });
}
