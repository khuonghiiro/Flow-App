// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video BBox Cropping & Auto-Trim Custom Hook (Percentage & Pixel Aware)
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

  /**
   * Automatically detect and crop transparent margins for all frames
   */
  const handleAutoTrimAllFramesBBox = useCallback(async () => {
    if (frames.length === 0) return;

    const trimmed: VideoSliceFrame[] = await Promise.all(
      frames.map(async (frame) => {
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
        if (!ctx) return frame;

        ctx.drawImage(img, 0, 0);
        const imgData = ctx.getImageData(0, 0, tempCanvas.width, tempCanvas.height);
        const rect = detectImageBBoxRect(imgData, 10, 2);

        if (!rect || rect.width <= 0 || rect.height <= 0) return frame;

        const croppedCanvas = document.createElement('canvas');
        croppedCanvas.width = rect.width;
        croppedCanvas.height = rect.height;
        const cropCtx = croppedCanvas.getContext('2d');
        if (!cropCtx) return frame;

        cropCtx.drawImage(
          tempCanvas,
          rect.x,
          rect.y,
          rect.width,
          rect.height,
          0,
          0,
          rect.width,
          rect.height
        );

        const newUrl = croppedCanvas.toDataURL('image/png');
        return {
          ...frame,
          originalDataUrl: frame.originalDataUrl,
          transparentDataUrl: newUrl,
          cropRect: {
            x: rect.x,
            y: rect.y,
            width: rect.width,
            height: rect.height,
          },
        };
      })
    );

    setFrames(trimmed);
  }, [frames, setFrames]);

  /**
   * Applies manual stage crop box across all frames (supports % and pixels)
   */
  const handleApplyCropBoxToAllFrames = useCallback(
    async (cropBox: VideoCropBBox) => {
      if (frames.length === 0 || cropBox.width <= 0 || cropBox.height <= 0) return;

      const cropped: VideoSliceFrame[] = await Promise.all(
        frames.map(async (frame) => {
          const img = new Image();
          img.crossOrigin = 'anonymous';
          await new Promise<void>((res) => {
            img.onload = () => res();
            img.onerror = () => res();
            img.src = frame.transparentDataUrl || frame.originalDataUrl;
          });

          const nw = img.naturalWidth || img.width;
          const nh = img.naturalHeight || img.height;

          // Convert percentage to pixel coordinates if box <= 100
          const isPct = cropBox.width <= 100 && cropBox.height <= 100;
          const pxX = isPct ? Math.round((cropBox.x / 100) * nw) : cropBox.x;
          const pxY = isPct ? Math.round((cropBox.y / 100) * nh) : cropBox.y;
          const pxW = isPct ? Math.round((cropBox.width / 100) * nw) : cropBox.width;
          const pxH = isPct ? Math.round((cropBox.height / 100) * nh) : cropBox.height;

          const canvas = document.createElement('canvas');
          canvas.width = Math.max(10, pxW);
          canvas.height = Math.max(10, pxH);
          const ctx = canvas.getContext('2d');
          if (!ctx) return frame;

          ctx.drawImage(img, pxX, pxY, pxW, pxH, 0, 0, canvas.width, canvas.height);

          const newUrl = canvas.toDataURL('image/png');
          return {
            ...frame,
            transparentDataUrl: newUrl,
            cropRect: { x: pxX, y: pxY, width: pxW, height: pxH },
          };
        })
      );

      setFrames(cropped);
    },
    [frames, setFrames]
  );

  return {
    activeBBox,
    setActiveBBox,
    isBBoxCropMode,
    setIsBBoxCropMode,
    handleAutoTrimAllFramesBBox,
    handleApplyCropBoxToAllFrames,
  };
}
