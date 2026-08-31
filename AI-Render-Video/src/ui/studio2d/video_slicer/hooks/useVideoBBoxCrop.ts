// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video BBox Cropping & Auto-Trim Custom Hook (Crops Both URLs with Live Progress)
// =========================================================================================
import { useState, useCallback } from 'react';
import { VideoSliceFrame, VideoCropBBox } from '../../../../types/video_slicer';
import { detectImageBBoxRect } from '../../../../core/utils/PixelBoundingBoxAlgorithms';

export interface UseVideoBBoxCropProps {
  frames: VideoSliceFrame[];
  setFrames: React.Dispatch<React.SetStateAction<VideoSliceFrame[]>>;
}

export function useVideoBBoxCrop({ frames, setFrames }: UseVideoBBoxCropProps) {
  const [activeBBox, setActiveBBox] = useState<VideoCropBBox | null>(null);
  const [isBBoxCropMode, setIsBBoxCropMode] = useState<boolean>(false);
  const [isCroppingBBox, setIsCroppingBBox] = useState<boolean>(false);
  const [cropStatusText, setCropStatusText] = useState<string>('');

  /**
   * Helper to crop an image URL with given rectangle
   */
  const cropImageUrl = async (
    url: string,
    cropBox: { x: number; y: number; width: number; height: number }
  ): Promise<string> => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    await new Promise<void>((res) => {
      img.onload = () => res();
      img.onerror = () => res();
      img.src = url;
    });

    const nw = img.naturalWidth || img.width || 200;
    const nh = img.naturalHeight || img.height || 200;

    const isPct = cropBox.width <= 100 && cropBox.height <= 100;
    const pxX = isPct ? Math.round((cropBox.x / 100) * nw) : cropBox.x;
    const pxY = isPct ? Math.round((cropBox.y / 100) * nh) : cropBox.y;
    const pxW = isPct ? Math.round((cropBox.width / 100) * nw) : cropBox.width;
    const pxH = isPct ? Math.round((cropBox.height / 100) * nh) : cropBox.height;

    const canvas = document.createElement('canvas');
    canvas.width = Math.max(10, pxW);
    canvas.height = Math.max(10, pxH);
    const ctx = canvas.getContext('2d');
    if (!ctx) return url;

    ctx.drawImage(img, pxX, pxY, pxW, pxH, 0, 0, canvas.width, canvas.height);
    return canvas.toDataURL('image/png');
  };

  /**
   * Automatically detect and crop transparent margins for all frames
   */
  const handleAutoTrimAllFramesBBox = useCallback(async () => {
    if (frames.length === 0) return;
    setIsCroppingBBox(true);
    setCropStatusText('Đang tự động phát hiện và cắt viền trong suốt...');

    const trimmed: VideoSliceFrame[] = [];

    for (let i = 0; i < frames.length; i++) {
      const frame = frames[i];
      setCropStatusText(`Auto Trim: ${i + 1}/${frames.length} frames...`);

      const img = new Image();
      img.crossOrigin = 'anonymous';
      await new Promise<void>((res) => {
        img.onload = () => res();
        img.onerror = () => res();
        img.src = frame.transparentDataUrl || frame.originalDataUrl;
      });

      const tempCanvas = document.createElement('canvas');
      tempCanvas.width = img.naturalWidth || img.width || 200;
      tempCanvas.height = img.naturalHeight || img.height || 200;
      const ctx = tempCanvas.getContext('2d');

      if (!ctx) {
        trimmed.push(frame);
        continue;
      }

      ctx.drawImage(img, 0, 0);
      const imgData = ctx.getImageData(0, 0, tempCanvas.width, tempCanvas.height);
      const rect = detectImageBBoxRect(imgData, 10, 2);

      if (!rect || rect.width <= 0 || rect.height <= 0) {
        trimmed.push(frame);
        continue;
      }

      const [newTransUrl, newOrigUrl] = await Promise.all([
        cropImageUrl(frame.transparentDataUrl, rect),
        cropImageUrl(frame.originalDataUrl, rect),
      ]);

      trimmed.push({
        ...frame,
        originalDataUrl: newOrigUrl,
        transparentDataUrl: newTransUrl,
        cropRect: {
          x: rect.x,
          y: rect.y,
          width: rect.width,
          height: rect.height,
        },
      });
    }

    setFrames(trimmed);
    setIsCroppingBBox(false);
    setCropStatusText(`✓ Đã tự động cắt gọn ${trimmed.length} frames!`);
    setTimeout(() => setCropStatusText(''), 3000);
  }, [frames, setFrames]);

  /**
   * Applies manual stage crop box across all frames (Crops both transparent & original URLs)
   */
  const handleApplyCropBoxToAllFrames = useCallback(
    async (cropBox: VideoCropBBox) => {
      if (frames.length === 0 || cropBox.width <= 0 || cropBox.height <= 0) return;

      setIsCroppingBBox(true);
      setCropStatusText(`Đang cắt khung BBox: 0/${frames.length} frames (0%)...`);

      const cropped: VideoSliceFrame[] = [];

      for (let i = 0; i < frames.length; i++) {
        const frame = frames[i];
        const pct = Math.round(((i + 1) / frames.length) * 100);
        setCropStatusText(`Đang cắt khung BBox: ${i + 1}/${frames.length} frames (${pct}%)...`);

        const [newTransUrl, newOrigUrl] = await Promise.all([
          cropImageUrl(frame.transparentDataUrl, cropBox),
          cropImageUrl(frame.originalDataUrl, cropBox),
        ]);

        cropped.push({
          ...frame,
          originalDataUrl: newOrigUrl,
          transparentDataUrl: newTransUrl,
          cropRect: cropBox,
        });
      }

      setFrames(cropped);
      setIsCroppingBBox(false);
      setCropStatusText(`✓ Đã cắt khung BBox thành công cho ${cropped.length} frames!`);
      setTimeout(() => setCropStatusText(''), 3000);
    },
    [frames, setFrames]
  );

  return {
    activeBBox,
    setActiveBBox,
    isBBoxCropMode,
    setIsBBoxCropMode,
    isCroppingBBox,
    cropStatusText,
    handleAutoTrimAllFramesBBox,
    handleApplyCropBoxToAllFrames,
  };
}
