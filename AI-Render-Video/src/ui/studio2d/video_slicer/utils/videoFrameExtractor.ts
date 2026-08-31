// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Frame Extraction Engine (Client-side HTML5 + FFmpeg Backend API)
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
    video.preload = 'metadata';
    video.src = url;
    video.muted = true;
    video.crossOrigin = 'anonymous';

    video.onloadedmetadata = () => {
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

    video.onerror = () => {
      reject(new Error('Không thể đọc thông tin video. Định dạng tệp có thể không tương thích.'));
    };
  });
}

/**
 * Extracts frames client-side using HTML5 Video + Canvas
 * Calculates exact total frames = Math.round(duration * fps) without artificial 30-frame capping
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
  video.crossOrigin = 'anonymous';

  await new Promise<void>((resolve, reject) => {
    video.onloadedmetadata = () => resolve();
    video.onerror = () => reject(new Error('Lỗi nạp video vào Canvas'));
  });

  const duration = Math.max(0.1, (endTime || video.duration) - Math.max(startTime, 0));
  // Exact calculation based on duration * fps (e.g. 8s * 10 FPS = 80 frames)
  const totalFrames = Math.max(1, Math.round(duration * fps));
  const interval = totalFrames > 1 ? duration / (totalFrames - 1) : 0;

  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Không thể khởi tạo 2D Context Canvas');

  const srcW = video.videoWidth || 640;
  const srcH = video.videoHeight || 480;
  const cropX = crop ? Math.max(0, crop.x) : 0;
  const cropY = crop ? Math.max(0, crop.y) : 0;
  const cropW = crop ? Math.min(srcW - cropX, crop.width) : srcW;
  const cropH = crop ? Math.min(srcH - cropY, crop.height) : srcH;

  canvas.width = cropW;
  canvas.height = cropH;

  const frames: VideoSliceFrame[] = [];

  for (let i = 0; i < totalFrames; i++) {
    const targetTime = Math.min(
      Math.max(startTime, 0) + i * interval,
      endTime || video.duration
    );
    video.currentTime = targetTime;

    await new Promise<void>((resolve) => {
      let timeoutId: any;
      const onSeeked = () => {
        video.removeEventListener('seeked', onSeeked);
        clearTimeout(timeoutId);
        resolve();
      };
      video.addEventListener('seeked', onSeeked, { once: true });
      timeoutId = setTimeout(() => {
        video.removeEventListener('seeked', onSeeked);
        resolve();
      }, 150);
    });

    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(video, cropX, cropY, cropW, cropH, 0, 0, canvas.width, canvas.height);

    const dataUrl = canvas.toDataURL('image/png');

    frames.push({
      id: `vframe_${Date.now()}_${i}`,
      index: i,
      timestamp: Number(targetTime.toFixed(3)),
      originalDataUrl: dataUrl,
      transparentDataUrl: dataUrl,
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
 * Extracts frames via FFmpeg 8.0.1 Backend API endpoint
 */
export async function extractFramesFFmpegBackend(
  videoDataUrl: string,
  options: VideoExtractOptions
): Promise<VideoSliceFrame[]> {
  const apiUrl = getAIMattingApiUrl();
  const res = await fetch(`${apiUrl}/api/video/extract-frames`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      video_data_url: videoDataUrl,
      fps: options.fps,
      start_time: options.startTime,
      end_time: options.endTime,
      crop: options.crop,
    }),
  });

  if (!res.ok) {
    const errJson = await res.json().catch(() => ({}));
    throw new Error(errJson.detail || 'Lỗi từ máy chủ FFmpeg trích xuất video');
  }

  const data = await res.json();
  return (data.frames || []).map((f: any, idx: number) => ({
    id: `ffmpeg_${idx}_${Date.now()}`,
    index: idx,
    timestamp: f.timestamp || idx * (1 / options.fps),
    originalDataUrl: f.data_url,
    transparentDataUrl: f.data_url,
    durationMs: Math.round(1000 / options.fps),
    offsetX: 0,
    offsetY: 0,
    scale: 1.0,
    rotation: 0,
    flipX: false,
  }));
}
