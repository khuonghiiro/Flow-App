// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Chroma Key Background Peeling Hook (Tab 1 Standard Engine with Single-Frame Tuning)
// =========================================================================================
import { useState, useCallback, useEffect } from 'react';
import { VideoSliceFrame } from '../../../../types/video_slicer';
import {
  processCellChromaAndDespeckle,
  ChromaProcessOptions,
} from '../../../../core/utils/ChromaDespeckleProcessor';

export interface UseVideoChromaPeelingProps {
  frames: VideoSliceFrame[];
  setFrames: React.Dispatch<React.SetStateAction<VideoSliceFrame[]>>;
  selectedFrameIndex: number | null;
}

export function useVideoChromaPeeling({
  frames,
  setFrames,
  selectedFrameIndex,
}: UseVideoChromaPeelingProps) {
  // Chroma settings (Aligned with Tab 1 Standard Engine)
  const [keyColorType, setKeyColorType] = useState<'chroma_green' | 'pure_white' | 'custom'>('chroma_green');
  const [keyColorHex, setKeyColorHex] = useState<string>('#00ff00');
  const [isolationMode, setIsolationMode] = useState<'all' | 'outer_only'>('all');
  const [tolerance, setTolerance] = useState<number>(38);
  const [feather, setFeather] = useState<number>(12);
  const [shadowRetention, setShadowRetention] = useState<number>(0);
  const [despeckleSize, setDespeckleSize] = useState<number>(0);
  const [defringeStrength, setDefringeStrength] = useState<number>(30);

  // Live single-frame preview demo
  const [demoPeeledUrl, setDemoPeeledUrl] = useState<string | null>(null);
  const [isApplyingAll, setIsApplyingAll] = useState<boolean>(false);
  const [isApplyingSingle, setIsApplyingSingle] = useState<boolean>(false);
  const [peelStatusText, setPeelStatusText] = useState<string>('');

  // Eyedropper sampling state
  const [isEyedropperActive, setIsEyedropperActive] = useState<boolean>(false);

  /**
   * Helper to process a single dataUrl with current chroma settings
   */
  const processSingleImageDataUrl = useCallback(
    async (originalDataUrl: string): Promise<string> => {
      const img = new Image();
      img.crossOrigin = 'anonymous';
      await new Promise<void>((resolve, reject) => {
        img.onload = () => resolve();
        img.onerror = () => reject(new Error('Lỗi nạp ảnh cho bộ lọc Chroma'));
        img.src = originalDataUrl;
      });

      const canvas = document.createElement('canvas');
      canvas.width = img.naturalWidth || img.width;
      canvas.height = img.naturalHeight || img.height;
      const ctx = canvas.getContext('2d');
      if (!ctx) return originalDataUrl;

      ctx.drawImage(img, 0, 0);

      const options: ChromaProcessOptions = {
        keyColorType,
        keyColorHex,
        isolationMode,
        tolerance,
        feather,
        shadowRetention,
        despeckleSize,
        whiteSpeckleSensitivity: 0,
        keepLargestIslandOnly: false,
        defringeStrength,
      };

      processCellChromaAndDespeckle(ctx, canvas.width, canvas.height, options);
      return canvas.toDataURL('image/png');
    },
    [
      keyColorType,
      keyColorHex,
      isolationMode,
      tolerance,
      feather,
      shadowRetention,
      despeckleSize,
      defringeStrength,
    ]
  );

  /**
   * Automatically compute Live Demo Peeling for the CURRENTLY SELECTED FRAME ONLY
   * Leaves all other frames untouched until user explicitly clicks apply
   */
  useEffect(() => {
    if (selectedFrameIndex === null || !frames[selectedFrameIndex]) {
      setDemoPeeledUrl(null);
      return;
    }

    let isCancelled = false;
    const targetFrame = frames[selectedFrameIndex];

    const timer = setTimeout(async () => {
      try {
        const peeled = await processSingleImageDataUrl(targetFrame.originalDataUrl);
        if (!isCancelled) {
          setDemoPeeledUrl(peeled);
        }
      } catch (err) {
        console.warn('Live demo peel error:', err);
      }
    }, 60);

    return () => {
      isCancelled = true;
      clearTimeout(timer);
    };
  }, [
    selectedFrameIndex,
    frames,
    keyColorType,
    keyColorHex,
    isolationMode,
    tolerance,
    feather,
    shadowRetention,
    despeckleSize,
    defringeStrength,
    processSingleImageDataUrl,
  ]);

  /**
   * Action 1: Apply Chroma Peeling to the CURRENT SELECTED FRAME ONLY
   */
  const handleApplyChromaToSingleFrame = useCallback(async () => {
    if (selectedFrameIndex === null || !frames[selectedFrameIndex]) return;

    setIsApplyingSingle(true);
    setPeelStatusText(`Đang bóc nền Frame ${selectedFrameIndex + 1}...`);

    try {
      const targetFrame = frames[selectedFrameIndex];
      const peeled = demoPeeledUrl || (await processSingleImageDataUrl(targetFrame.originalDataUrl));

      setFrames((prev) =>
        prev.map((f, idx) =>
          idx === selectedFrameIndex ? { ...f, transparentDataUrl: peeled } : f
        )
      );
      setPeelStatusText(`✓ Đã bóc nền xong Frame ${selectedFrameIndex + 1}!`);
      setTimeout(() => setPeelStatusText(''), 2500);
    } catch (err: any) {
      setPeelStatusText(`Lỗi: ${err.message}`);
    } finally {
      setIsApplyingSingle(false);
    }
  }, [selectedFrameIndex, frames, demoPeeledUrl, processSingleImageDataUrl, setFrames]);

  /**
   * Action 2: Apply Chroma Peeling to ALL FRAMES
   */
  const handleApplyChromaToAllFrames = useCallback(async () => {
    if (frames.length === 0) return;

    setIsApplyingAll(true);
    setPeelStatusText(`Đang bóc nền toàn bộ ${frames.length} frame...`);

    try {
      const updatedFrames: VideoSliceFrame[] = [];

      for (let i = 0; i < frames.length; i++) {
        const frame = frames[i];
        setPeelStatusText(`Bóc nền: Frame ${i + 1}/${frames.length}...`);

        const peeledUrl = await processSingleImageDataUrl(frame.originalDataUrl);
        updatedFrames.push({
          ...frame,
          transparentDataUrl: peeledUrl,
        });
      }

      setFrames(updatedFrames);
      setPeelStatusText(`✓ Đã hoàn tất bóc nền toàn bộ ${frames.length} khung hình!`);
      setTimeout(() => setPeelStatusText(''), 3000);
    } catch (err: any) {
      setPeelStatusText(`Lỗi bóc nền: ${err.message}`);
    } finally {
      setIsApplyingAll(false);
    }
  }, [frames, processSingleImageDataUrl, setFrames]);

  /**
   * Eyedropper color picker activation (supports native EyeDropper API)
   */
  const handleTriggerEyedropper = useCallback(async () => {
    if (typeof window !== 'undefined' && (window as any).EyeDropper) {
      try {
        const eyeDropper = new (window as any).EyeDropper();
        const result = await eyeDropper.open();
        if (result?.sRGBHex) {
          setKeyColorHex(result.sRGBHex);
          setKeyColorType('custom');
        }
      } catch {
        // User canceled eyedropper or unsupported
      }
    } else {
      setIsEyedropperActive((prev) => !prev);
    }
  }, []);

  return {
    keyColorType,
    setKeyColorType,
    keyColorHex,
    setKeyColorHex,
    isolationMode,
    setIsolationMode,
    tolerance,
    setTolerance,
    feather,
    setFeather,
    shadowRetention,
    setShadowRetention,
    despeckleSize,
    setDespeckleSize,
    defringeStrength,
    setDefringeStrength,
    demoPeeledUrl,
    isApplyingAll,
    isApplyingSingle,
    peelStatusText,
    handleApplyChromaToSingleFrame,
    handleApplyChromaToAllFrames,
    isEyedropperActive,
    setIsEyedropperActive,
    handleTriggerEyedropper,
  };
}
