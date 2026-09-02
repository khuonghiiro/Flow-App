// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Chroma Key Background Peeling Hook (Tab 1 Standard Engine - Clean State Synchronization)
// =========================================================================================
import { useState, useCallback, useEffect, useRef } from 'react';
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

  // Live single-frame preview demo (active ONLY when user is adjusting sliders)
  const [demoPeeledUrl, setDemoPeeledUrl] = useState<string | null>(null);
  const [isApplyingAll, setIsApplyingAll] = useState<boolean>(false);
  const [isApplyingSingle, setIsApplyingSingle] = useState<boolean>(false);
  const [peelStatusText, setPeelStatusText] = useState<string>('');

  // Eyedropper sampling state
  const [isEyedropperActive, setIsEyedropperActive] = useState<boolean>(false);

  // In-memory image element cache for instant 0ms chroma processing
  const imageElementCacheRef = useRef<Map<string, HTMLImageElement>>(new Map());

  const getImageElement = useCallback((url: string): HTMLImageElement | null => {
    if (!url) return null;
    if (imageElementCacheRef.current.has(url)) {
      return imageElementCacheRef.current.get(url)!;
    }
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.src = url;
    imageElementCacheRef.current.set(url, img);
    return img;
  }, []);

  /**
   * Synchronous 0ms Chroma Processor (Direct Canvas Execution in < 2ms)
   */
  const processChromaOnImage = useCallback(
    (img: HTMLImageElement): string => {
      const nw = img.naturalWidth || img.width || 300;
      const nh = img.naturalHeight || img.height || 300;
      const canvas = document.createElement('canvas');
      canvas.width = nw;
      canvas.height = nh;
      const ctx = canvas.getContext('2d');
      if (!ctx) return img.src;

      ctx.drawImage(img, 0, 0, nw, nh);

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

      processCellChromaAndDespeckle(ctx, nw, nh, options);
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
   * Helper for batch processing
   */
  const processSingleImageDataUrl = useCallback(
    async (sourceDataUrl: string): Promise<string> => {
      if (!sourceDataUrl) return '';
      const img = getImageElement(sourceDataUrl);
      if (img && (img.complete || img.naturalWidth > 0)) {
        return processChromaOnImage(img);
      }

      await new Promise<void>((resolve) => {
        const newImg = new Image();
        newImg.crossOrigin = 'anonymous';
        newImg.onload = () => resolve();
        newImg.onerror = () => resolve();
        newImg.src = sourceDataUrl;
        imageElementCacheRef.current.set(sourceDataUrl, newImg);
      });

      const loadedImg = getImageElement(sourceDataUrl);
      return loadedImg ? processChromaOnImage(loadedImg) : sourceDataUrl;
    },
    [getImageElement, processChromaOnImage]
  );

  // Debounce timer for silky smooth 60fps slider dragging
  const debounceTimerRef = useRef<any>(null);

  const computeDemoNow = useCallback(() => {
    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
      debounceTimerRef.current = null;
    }

    if (selectedFrameIndex === null || !frames[selectedFrameIndex]) {
      return;
    }

    const currentFrame = frames[selectedFrameIndex];
    const rawUrl = currentFrame.originalDataUrl || currentFrame.transparentDataUrl;
    if (!rawUrl) return;

    const img = getImageElement(rawUrl);
    if (img && (img.complete || img.naturalWidth > 0)) {
      const peeled = processChromaOnImage(img);
      setDemoPeeledUrl(peeled);
    } else if (img) {
      img.onload = () => {
        const peeled = processChromaOnImage(img);
        setDemoPeeledUrl(peeled);
      };
    }
  }, [selectedFrameIndex, frames, getImageElement, processChromaOnImage]);

  // Clear demo preview when switching selected frame
  useEffect(() => {
    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
      debounceTimerRef.current = null;
    }
    setDemoPeeledUrl(null);
  }, [selectedFrameIndex]);

  // Compute Live Demo Peeling whenever Chroma Sliders Change (debounced 75ms for silky-smooth dragging)
  useEffect(() => {
    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
    }
    debounceTimerRef.current = setTimeout(() => {
      computeDemoNow();
    }, 75);

    return () => {
      if (debounceTimerRef.current) {
        clearTimeout(debounceTimerRef.current);
      }
    };
  }, [
    keyColorType,
    keyColorHex,
    isolationMode,
    tolerance,
    feather,
    shadowRetention,
    despeckleSize,
    defringeStrength,
    computeDemoNow,
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
      const sourceUrl = targetFrame.originalDataUrl || targetFrame.transparentDataUrl;
      const peeled = demoPeeledUrl || (await processSingleImageDataUrl(sourceUrl));

      if (peeled) {
        const img = new Image();
        img.src = peeled;
        imageElementCacheRef.current.set(peeled, img);
      }

      setFrames((prev) =>
        prev.map((f, idx) =>
          idx === selectedFrameIndex
            ? {
                ...f,
                transparentDataUrl: peeled,
              }
            : f
        )
      );

      // Clear demo preview so stage immediately renders the saved transparentDataUrl
      setDemoPeeledUrl(null);

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

        const sourceUrl = frame.originalDataUrl || frame.transparentDataUrl;
        const peeledUrl = await processSingleImageDataUrl(sourceUrl);
        if (peeledUrl) {
          const img = new Image();
          img.src = peeledUrl;
          imageElementCacheRef.current.set(peeledUrl, img);
        }
        updatedFrames.push({
          ...frame,
          transparentDataUrl: peeledUrl,
        });
      }

      setFrames(updatedFrames);

      // Clear demo preview so all frames immediately render their saved transparentDataUrl
      setDemoPeeledUrl(null);

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
        if (result && result.sRGBHex) {
          setKeyColorType('custom');
          setKeyColorHex(result.sRGBHex);
        }
      } catch (err) {
        console.info('Eyedropper cancelled or not supported');
      }
    } else {
      setIsEyedropperActive(true);
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
    isEyedropperActive,
    setIsEyedropperActive,
    handleApplyChromaToSingleFrame,
    handleApplyChromaToAllFrames,
    handleTriggerEyedropper,
    triggerCommitPreview: computeDemoNow,
  };
}
