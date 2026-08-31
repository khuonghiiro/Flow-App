// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Extractor & Live Continuous Video Player Hook (Percentage BBox Support)
// =========================================================================================
import { useState, useCallback, useRef, useMemo } from 'react';
import { VideoSliceFrame, VideoMetadata, VideoCropBBox } from '../../../../types/video_slicer';
import {
  loadVideoMetadata,
  extractFramesClientSide,
  extractFramesFFmpegBackend,
} from '../utils/videoFrameExtractor';

export function useVideoExtractor() {
  const [videoMetadata, setVideoMetadata] = useState<VideoMetadata | null>(null);
  const [frames, setFrames] = useState<VideoSliceFrame[]>([]);
  const [frameOrder, setFrameOrder] = useState<number[]>([]);
  const [selectedFrameIndex, setSelectedFrameIndex] = useState<number | null>(0);

  // View Mode: 'video' (watch live video with continuous playback) vs 'frames' (view extracted animation)
  const [viewMode, setViewMode] = useState<'video' | 'frames'>('video');

  // Video Live Player state
  const [videoCurrentTime, setVideoCurrentTime] = useState<number>(0);
  const [isVideoPlaying, setIsVideoPlaying] = useState<boolean>(false);
  const [isLooping, setIsLooping] = useState<boolean>(true);
  const videoElementRef = useRef<HTMLVideoElement | null>(null);

  // Extraction Options
  const [targetFps, setTargetFps] = useState<number>(8);
  const [startTime, setStartTime] = useState<number>(0);
  const [endTime, setEndTime] = useState<number>(0);
  const [maxFrames, setMaxFrames] = useState<number>(300);
  // Default percentage BBox (10%, 10%, 80%, 80%)
  const [videoCropBBox, setVideoCropBBox] = useState<VideoCropBBox | null>({
    x: 10,
    y: 10,
    width: 80,
    height: 80,
  });

  // Processing state
  const [isExtracting, setIsExtracting] = useState<boolean>(false);
  const [extractProgress, setExtractProgress] = useState<number>(0);
  const [extractStatusText, setExtractStatusText] = useState<string>('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const videoFileInputRef = useRef<HTMLInputElement | null>(null);

  // Exact estimated total frames calculation = (duration * fps)
  const estimatedTotalFrames = useMemo(() => {
    if (!videoMetadata) return 0;
    const duration = Math.max(0.1, (endTime || videoMetadata.duration) - startTime);
    return Math.max(1, Math.round(duration * targetFps));
  }, [videoMetadata, startTime, endTime, targetFps]);

  /**
   * Loads a video file from input
   */
  const handleLoadVideoFile = useCallback(async (file: File) => {
    try {
      setErrorMessage(null);
      const meta = await loadVideoMetadata(file);
      setVideoMetadata(meta);
      setStartTime(0);
      setEndTime(Number(meta.duration.toFixed(2)));
      setVideoCurrentTime(0);
      setIsVideoPlaying(false);
      setViewMode('video');

      // Default percentage BBox (10%, 10%, 80%, 80%)
      setVideoCropBBox({
        x: 10,
        y: 10,
        width: 80,
        height: 80,
      });

      // Reset frames
      setFrames([]);
      setFrameOrder([]);
      setSelectedFrameIndex(null);
    } catch (err: any) {
      setErrorMessage(err.message || 'Không thể tải video');
    }
  }, []);

  /**
   * Time update listener strictly looping in [startTime, endTime]
   */
  const handleVideoTimeUpdate = useCallback(
    (currentTime: number) => {
      setVideoCurrentTime(currentTime);

      const clipEnd = endTime || videoMetadata?.duration || 1;
      const clipStart = startTime || 0;

      if (currentTime >= clipEnd - 0.05) {
        if (videoElementRef.current) {
          if (isLooping) {
            videoElementRef.current.currentTime = clipStart;
          } else {
            videoElementRef.current.pause();
            setIsVideoPlaying(false);
          }
        }
      }
    },
    [startTime, endTime, videoMetadata?.duration, isLooping]
  );

  /**
   * User seeks video
   */
  const handleUserSeekTime = useCallback((time: number) => {
    setVideoCurrentTime(time);
    if (videoElementRef.current) {
      videoElementRef.current.currentTime = time;
    }
  }, []);

  /**
   * Toggles video Play / Pause
   */
  const handleTogglePlayVideo = useCallback(() => {
    const video = videoElementRef.current;
    if (!video) return;

    if (video.paused) {
      const clipEnd = endTime || videoMetadata?.duration || 1;
      if (video.currentTime >= clipEnd - 0.05 || video.currentTime < startTime) {
        video.currentTime = startTime;
      }
      video
        .play()
        .then(() => setIsVideoPlaying(true))
        .catch((err) => console.warn('Video play error:', err));
    } else {
      video.pause();
      setIsVideoPlaying(false);
    }
  }, [startTime, endTime, videoMetadata?.duration]);

  /**
   * Executes frame extraction (converts % BBox to pixel crop)
   */
  const handleExtractFrames = useCallback(
    async (cropBoxOverride?: VideoCropBBox | null) => {
      if (!videoMetadata) return [];

      setIsExtracting(true);
      setExtractProgress(0);
      setExtractStatusText('Đang trích xuất chuỗi frame hoạt ảnh...');
      setErrorMessage(null);

      const rawCrop = cropBoxOverride === undefined ? videoCropBBox : cropBoxOverride;
      let activeCrop: VideoCropBBox | undefined = undefined;

      if (rawCrop && rawCrop.width > 0 && rawCrop.height > 0) {
        const isPct = rawCrop.width <= 100 && rawCrop.height <= 100;
        activeCrop = isPct
          ? {
              x: Math.round((rawCrop.x / 100) * videoMetadata.width),
              y: Math.round((rawCrop.y / 100) * videoMetadata.height),
              width: Math.round((rawCrop.width / 100) * videoMetadata.width),
              height: Math.round((rawCrop.height / 100) * videoMetadata.height),
            }
          : rawCrop;
      }

      try {
        let extracted: VideoSliceFrame[] = [];
        try {
          setExtractStatusText('Đang xử lý qua FFmpeg 8.0.1 Engine...');
          extracted = await extractFramesFFmpegBackend(videoMetadata.dataUrl, {
            fps: targetFps,
            startTime,
            endTime: endTime || videoMetadata.duration,
            maxFrames,
            crop: activeCrop,
          });
        } catch {
          setExtractStatusText('Đang trích xuất trực tiếp qua Canvas...');
          extracted = await extractFramesClientSide(
            videoMetadata.dataUrl,
            {
              fps: targetFps,
              startTime,
              endTime: endTime || videoMetadata.duration,
              maxFrames,
              crop: activeCrop,
            },
            (prog) => setExtractProgress(prog)
          );
        }

        setFrames(extracted);
        setFrameOrder(extracted.map((_, i) => i));
        setSelectedFrameIndex(extracted.length > 0 ? 0 : null);
        setViewMode('frames');
        setExtractStatusText(`✓ Đã trích xuất ${extracted.length} khung hình!`);
        return extracted;
      } catch (err: any) {
        setErrorMessage(`Lỗi trích xuất video: ${err.message}`);
        return [];
      } finally {
        setIsExtracting(false);
      }
    },
    [videoMetadata, targetFps, startTime, endTime, maxFrames, videoCropBBox]
  );

  return {
    videoMetadata,
    frames,
    setFrames,
    frameOrder,
    setFrameOrder,
    selectedFrameIndex,
    setSelectedFrameIndex,
    viewMode,
    setViewMode,
    videoCurrentTime,
    setVideoCurrentTime,
    isVideoPlaying,
    setIsVideoPlaying,
    isLooping,
    setIsLooping,
    videoElementRef,
    handleVideoTimeUpdate,
    handleUserSeekTime,
    handleTogglePlayVideo,
    targetFps,
    setTargetFps,
    startTime,
    setStartTime,
    endTime,
    setEndTime,
    maxFrames,
    setMaxFrames,
    estimatedTotalFrames,
    videoCropBBox,
    setVideoCropBBox,
    isExtracting,
    extractProgress,
    extractStatusText,
    errorMessage,
    videoFileInputRef,
    handleLoadVideoFile,
    handleExtractFrames,
  };
}
