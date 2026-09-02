// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Slicer Frames Canvas Stage (Robust State Resolution, Live Batch Erase & Undo/Redo)
// =========================================================================================
import React, { useRef, useEffect, useState, useCallback, useMemo } from 'react';
import { VideoSliceFrame, VideoCropBBox } from '../../../../types/video_slicer';
import { VideoSlicerStageOverlayControls } from './VideoSlicerStageOverlayControls';

export interface VideoSlicerFramesCanvasStageProps {
  frames: VideoSliceFrame[];
  setFrames: React.Dispatch<React.SetStateAction<VideoSliceFrame[]>>;
  selectedFrameIndex: number | null;
  activePlaybackIndex: number;
  isAnimationPlaying: boolean;
  demoPeeledUrl: string | null;
  onionSkinMode: 'off' | 'sequential' | 'all';
  onToggleOnionSkin: () => void;
  previewDisplayMode: 'transparent' | 'original';
  checkerTheme: 'dark' | 'light';
  setCheckerTheme: React.Dispatch<React.SetStateAction<'dark' | 'light'>>;
  isBBoxCropMode: boolean;
  activeBBox: VideoCropBBox | null;
  onUpdateActiveBBox: (bbox: VideoCropBBox) => void;
  onShowToast?: (text: string, type?: 'success' | 'error' | 'info') => void;
}

interface UndoSnapshot {
  updates: { index: number; prevTransparentUrl?: string; prevOriginalUrl?: string; nextUrl: string }[];
}

export const VideoSlicerFramesCanvasStage: React.FC<VideoSlicerFramesCanvasStageProps> = ({
  frames,
  setFrames,
  selectedFrameIndex,
  activePlaybackIndex,
  isAnimationPlaying,
  demoPeeledUrl,
  onionSkinMode,
  onToggleOnionSkin,
  previewDisplayMode,
  checkerTheme,
  setCheckerTheme,
  isBBoxCropMode,
  activeBBox,
  onShowToast,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  // Viewport transforms
  const [zoom, setZoom] = useState<number>(1.0);
  const [pan, setPan] = useState<{ x: number; y: number }>({ x: 0, y: 0 });

  // Tools & States
  const [activeTool, setActiveTool] = useState<'select' | 'eraser'>('select');
  const [brushRadius, setBrushRadius] = useState<number>(20);
  const [mouseCanvasPos, setMouseCanvasPos] = useState<{ x: number; y: number } | null>(null);

  // Accumulated Erase Mask for Batch Application Across All Frames
  const [hasEraseMask, setHasEraseMask] = useState<boolean>(false);
  const [isApplyingBatch, setIsApplyingBatch] = useState<boolean>(false);
  const [batchProgress, setBatchProgress] = useState<{ current: number; total: number; percent: number } | null>(null);
  const accumulatedMaskCanvasRef = useRef<HTMLCanvasElement | null>(null);

  // In-Memory Persistent Frame Canvases with URL matching
  const frameCanvasesRef = useRef<Map<number, { url: string; canvas: HTMLCanvasElement }>>(new Map());
  const isErasingRef = useRef<boolean>(false);
  const lastErasePointRef = useRef<{ x: number; y: number } | null>(null);

  // Dragging / Panning
  const isDraggingFrameRef = useRef<boolean>(false);
  const isPanningRef = useRef<boolean>(false);
  const lastMousePosRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 });

  // Image cache loader
  const imageCacheRef = useRef<Map<string, HTMLImageElement>>(new Map());

  // Lightweight Undo / Redo history stack
  const historyStackRef = useRef<UndoSnapshot[]>([]);
  const historyIndexRef = useRef<number>(-1);
  const [canUndo, setCanUndo] = useState<boolean>(false);
  const [canRedo, setCanRedo] = useState<boolean>(false);

  const updateHistoryFlags = useCallback(() => {
    setCanUndo(historyIndexRef.current >= 0);
    setCanRedo(historyIndexRef.current < historyStackRef.current.length - 1);
  }, []);

  const redrawStageRef = useRef<() => void>(() => {});

  const getCachedImage = useCallback((url: string): HTMLImageElement | null => {
    if (!url) return null;
    if (imageCacheRef.current.has(url)) {
      return imageCacheRef.current.get(url)!;
    }
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      redrawStageRef.current?.();
    };
    img.src = url;
    imageCacheRef.current.set(url, img);
    return img;
  }, []);

  // Current active frame index
  const currentFrameIdx = isAnimationPlaying
    ? activePlaybackIndex
    : selectedFrameIndex !== null
    ? selectedFrameIndex
    : 0;

  const currentFrame = frames[currentFrameIdx] || null;
  const prevGhostIdx = currentFrameIdx > 0 ? currentFrameIdx - 1 : frames.length - 1;
  const prevGhostFrame = frames.length > 1 ? frames[prevGhostIdx] : null;

  // Check if background has been peeled on any frame
  const hasPeeledBackground = useMemo(() => {
    return frames.some((f) => !!f.transparentDataUrl && f.transparentDataUrl !== f.originalDataUrl);
  }, [frames]);

  /**
   * Deterministic URL Resolver for Any Frame:
   * Guarantees live demo peel preview is always visible on selected frame!
   */
  const getResolvedFrameUrl = useCallback(
    (frame: VideoSliceFrame | null, isSelected: boolean): string | null => {
      if (!frame) return null;

      // 1. When actively using eraser or during animation playback: always use the frame's true rendered pixels
      if (activeTool === 'eraser' || isAnimationPlaying) {
        return frame.transparentDataUrl || frame.originalDataUrl || null;
      }

      // 2. When selecting a frame and live demo peel preview is available: ALWAYS show demo peel
      if (isSelected && demoPeeledUrl) {
        return demoPeeledUrl;
      }

      // 3. When viewing in original mode: show original raw image
      if (previewDisplayMode === 'original') {
        return frame.originalDataUrl || frame.transparentDataUrl || null;
      }

      // 4. Default: Always prioritize transparentDataUrl so peeled frames show transparent background
      return frame.transparentDataUrl || frame.originalDataUrl || null;
    },
    [activeTool, isAnimationPlaying, demoPeeledUrl, previewDisplayMode]
  );

  /**
   * Helper: Gets or creates an offscreen canvas for a frame (strictly URL-matched)
   */
  const getOrCreateFrameCanvas = useCallback(
    (idx: number): HTMLCanvasElement | null => {
      const frame = frames[idx];
      if (!frame) return null;

      const targetUrl = frame.transparentDataUrl || frame.originalDataUrl;
      if (!targetUrl) return null;

      if (frameCanvasesRef.current.has(idx)) {
        const cached = frameCanvasesRef.current.get(idx)!;
        if (cached.url === targetUrl) {
          return cached.canvas;
        }
      }

      const img = getCachedImage(targetUrl);
      let nw = 300;
      let nh = 300;
      if (img && (img.complete || img.naturalWidth > 0)) {
        nw = img.naturalWidth || img.width || 300;
        nh = img.naturalHeight || img.height || 300;
      }

      const offscreen = document.createElement('canvas');
      offscreen.width = nw;
      offscreen.height = nh;
      const ctx = offscreen.getContext('2d');
      if (!ctx) return null;

      if (img && (img.complete || img.naturalWidth > 0)) {
        ctx.drawImage(img, 0, 0, nw, nh);
      }

      frameCanvasesRef.current.set(idx, { url: targetUrl, canvas: offscreen });
      return offscreen;
    },
    [frames, getCachedImage]
  );

  /**
   * High-Performance Canvas Redraw Routine (Direct HTML5 2D Canvas Draw)
   */
  const redrawStage = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const w = canvas.width;
    const h = canvas.height;

    // 1. Draw Checkerboard Background
    ctx.clearRect(0, 0, w, h);
    const sq = 16;
    const c1 = checkerTheme === 'dark' ? '#090d16' : '#ffffff';
    const c2 = checkerTheme === 'dark' ? '#131b2e' : '#e2e8f0';
    for (let x = 0; x < w; x += sq) {
      for (let y = 0; y < h; y += sq) {
        ctx.fillStyle = (Math.floor(x / sq) + Math.floor(y / sq)) % 2 === 0 ? c1 : c2;
        ctx.fillRect(x, y, sq, sq);
      }
    }

    // 2. Reference Center Guidelines
    ctx.strokeStyle = checkerTheme === 'dark' ? 'rgba(56, 189, 248, 0.15)' : 'rgba(14, 165, 233, 0.25)';
    ctx.lineWidth = 1;
    ctx.setLineDash([4, 4]);

    ctx.beginPath();
    ctx.moveTo(w / 2 + pan.x, 0);
    ctx.lineTo(w / 2 + pan.x, h);
    ctx.moveTo(0, h / 2 + pan.y);
    ctx.lineTo(w, h / 2 + pan.y);
    ctx.stroke();
    ctx.setLineDash([]);

    if (currentFrame) {
      ctx.save();
      const centerX = w / 2 + pan.x;
      const centerY = h / 2 + pan.y;
      ctx.translate(centerX, centerY);
      ctx.scale(zoom, zoom);

      const maxDw = w * 0.85;
      const maxDh = h * 0.85;

      // ─── 3. ONION SKIN GHOSTS ───
      if (onionSkinMode === 'sequential' && prevGhostFrame) {
        const prevTargetUrl = getResolvedFrameUrl(prevGhostFrame, false);
        const prevCached = frameCanvasesRef.current.get(prevGhostIdx);
        const prevImg =
          prevCached && prevCached.url === prevTargetUrl
            ? prevCached.canvas
            : prevTargetUrl
            ? getCachedImage(prevTargetUrl)
            : null;

        if (prevImg) {
          ctx.save();
          ctx.globalAlpha = 0.35;
          ctx.translate(prevGhostFrame.offsetX || 0, prevGhostFrame.offsetY || 0);
          ctx.rotate(((prevGhostFrame.rotation || 0) * Math.PI) / 180);
          ctx.scale(
            prevGhostFrame.flipX ? -(prevGhostFrame.scale || 1) : prevGhostFrame.scale || 1,
            prevGhostFrame.scale || 1
          );

          const fw = prevImg.width || 300;
          const fh = prevImg.height || 300;
          const sFactor = Math.min(maxDw / fw, maxDh / fh);
          const dw = fw * sFactor;
          const dh = fh * sFactor;
          ctx.drawImage(prevImg, -dw / 2, -dh / 2, dw, dh);
          ctx.restore();
        }
      } else if (onionSkinMode === 'all') {
        frames.forEach((gf, gIdx) => {
          if (gIdx === currentFrameIdx) return;
          const gTargetUrl = getResolvedFrameUrl(gf, false);
          const gCached = frameCanvasesRef.current.get(gIdx);
          const gImg =
            gCached && gCached.url === gTargetUrl
              ? gCached.canvas
              : gTargetUrl
              ? getCachedImage(gTargetUrl)
              : null;

          if (gImg) {
            ctx.save();
            ctx.globalAlpha = 0.28;
            ctx.translate(gf.offsetX || 0, gf.offsetY || 0);
            ctx.rotate(((gf.rotation || 0) * Math.PI) / 180);
            ctx.scale(gf.flipX ? -(gf.scale || 1) : gf.scale || 1, gf.scale || 1);

            const fw = gImg.width || 300;
            const fh = gImg.height || 300;
            const sFactor = Math.min(maxDw / fw, maxDh / fh);
            const dw = fw * sFactor;
            const dh = fh * sFactor;
            ctx.drawImage(gImg, -dw / 2, -dh / 2, dw, dh);
            ctx.restore();
          }
        });
      }

      // ─── 4. ACTIVE CURRENT FRAME ───
      const targetUrl = getResolvedFrameUrl(currentFrame, selectedFrameIndex === currentFrameIdx);
      const cached = frameCanvasesRef.current.get(currentFrameIdx);
      const curImg =
        cached && cached.url === targetUrl
          ? cached.canvas
          : targetUrl
          ? getCachedImage(targetUrl)
          : null;

      if (curImg) {
        const isReady =
          curImg instanceof HTMLCanvasElement ||
          (curImg instanceof HTMLImageElement && curImg.complete && curImg.naturalWidth > 0);

        if (isReady) {
          ctx.save();
          ctx.translate(currentFrame.offsetX || 0, currentFrame.offsetY || 0);
          ctx.rotate(((currentFrame.rotation || 0) * Math.PI) / 180);
          ctx.scale(
            currentFrame.flipX ? -(currentFrame.scale || 1) : currentFrame.scale || 1,
            currentFrame.scale || 1
          );

          const fw = (curImg as any).naturalWidth || curImg.width || 300;
          const fh = (curImg as any).naturalHeight || curImg.height || 300;
          const sFactor = Math.min(maxDw / fw, maxDh / fh);
          const dw = fw * sFactor;
          const dh = fh * sFactor;
          ctx.drawImage(curImg, -dw / 2, -dh / 2, dw, dh);

          // Frame bounding box outline if enabled
          if (isBBoxCropMode && activeBBox) {
            ctx.strokeStyle = '#f59e0b';
            ctx.lineWidth = 1.5;
            ctx.setLineDash([4, 4]);
            const bx = -dw / 2 + (activeBBox.x / 100) * dw;
            const by = -dh / 2 + (activeBBox.y / 100) * dh;
            const bw = (activeBBox.width / 100) * dw;
            const bh = (activeBBox.height / 100) * dh;
            ctx.strokeRect(bx, by, bw, bh);
          }

          ctx.restore();
        }
      }

      ctx.restore();
    }

    // ─── 5. ERASER BRUSH CIRCULAR CURSOR PREVIEW ───
    if (activeTool === 'eraser' && mouseCanvasPos) {
      ctx.save();
      ctx.beginPath();
      ctx.arc(mouseCanvasPos.x, mouseCanvasPos.y, brushRadius, 0, Math.PI * 2);
      ctx.fillStyle = 'rgba(239, 68, 68, 0.22)';
      ctx.fill();
      ctx.strokeStyle = '#ef4444';
      ctx.lineWidth = 1.5;
      ctx.setLineDash([3, 3]);
      ctx.stroke();

      // Crosshair center dot
      ctx.setLineDash([]);
      ctx.strokeStyle = '#ffffff';
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(mouseCanvasPos.x - 4, mouseCanvasPos.y);
      ctx.lineTo(mouseCanvasPos.x + 4, mouseCanvasPos.y);
      ctx.moveTo(mouseCanvasPos.x, mouseCanvasPos.y - 4);
      ctx.lineTo(mouseCanvasPos.x, mouseCanvasPos.y + 4);
      ctx.stroke();
      ctx.restore();
    }
  }, [
    currentFrame,
    prevGhostFrame,
    prevGhostIdx,
    frames,
    onionSkinMode,
    checkerTheme,
    zoom,
    pan,
    currentFrameIdx,
    selectedFrameIndex,
    isBBoxCropMode,
    activeBBox,
    activeTool,
    mouseCanvasPos,
    brushRadius,
    getCachedImage,
    getResolvedFrameUrl,
  ]);

  // Keep latest reference for async image onload
  redrawStageRef.current = redrawStage;

  // Redraw whenever state changes
  useEffect(() => {
    redrawStage();
  }, [redrawStage]);

  /**
   * Internal Erase Segment Execution on Current Frame & Mask
   */
  const executeEraseStroke = useCallback(
    (fromX: number, fromY: number, toX: number, toY: number) => {
      const frameCanvas = getOrCreateFrameCanvas(currentFrameIdx);
      const frame = currentFrame;
      if (!frameCanvas || !frame) return;

      const fw = frameCanvas.width;
      const fh = frameCanvas.height;
      const canvas = canvasRef.current;
      if (!canvas) return;

      const stageW = canvas.width;
      const stageH = canvas.height;
      const centerX = stageW / 2 + pan.x;
      const centerY = stageH / 2 + pan.y;

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

      const p1 = transformPoint(fromX, fromY);
      const p2 = transformPoint(toX, toY);
      const frameScale = frame.scale || 1.0;
      const localRadius = Math.max(1, brushRadius / (zoom * frameScale * scaleFactor));

      // 1. Erase on Current Frame Canvas (destination-out)
      const ctx = frameCanvas.getContext('2d');
      if (ctx) {
        ctx.save();
        ctx.globalCompositeOperation = 'destination-out';
        ctx.lineWidth = localRadius * 2;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.beginPath();
        ctx.moveTo(p1.x, p1.y);
        ctx.lineTo(p2.x, p2.y);
        ctx.stroke();

        ctx.beginPath();
        ctx.arc(p2.x, p2.y, localRadius, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      }

      // 2. Accumulate stroke onto Accumulated Mask Canvas
      if (!accumulatedMaskCanvasRef.current) {
        const maskCanvas = document.createElement('canvas');
        maskCanvas.width = fw;
        maskCanvas.height = fh;
        accumulatedMaskCanvasRef.current = maskCanvas;
      }

      const maskCtx = accumulatedMaskCanvasRef.current.getContext('2d');
      if (maskCtx) {
        maskCtx.save();
        maskCtx.fillStyle = '#ffffff';
        maskCtx.strokeStyle = '#ffffff';
        maskCtx.lineWidth = localRadius * 2;
        maskCtx.lineCap = 'round';
        maskCtx.lineJoin = 'round';
        maskCtx.beginPath();
        maskCtx.moveTo(p1.x, p1.y);
        maskCtx.lineTo(p2.x, p2.y);
        maskCtx.stroke();

        maskCtx.beginPath();
        maskCtx.arc(p2.x, p2.y, localRadius, 0, Math.PI * 2);
        maskCtx.fill();
        maskCtx.restore();
      }

      setHasEraseMask(true);
    },
    [currentFrameIdx, currentFrame, getOrCreateFrameCanvas, pan.x, pan.y, zoom, brushRadius]
  );

  // Mouse Handlers
  const handleMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const x = ((e.clientX - rect.left) / rect.width) * canvas.width;
    const y = ((e.clientY - rect.top) / rect.height) * canvas.height;

    lastMousePosRef.current = { x: e.clientX, y: e.clientY };

    if (activeTool === 'eraser') {
      isErasingRef.current = true;
      lastErasePointRef.current = { x, y };

      // Initialize frame canvas and execute touch point erasure
      getOrCreateFrameCanvas(currentFrameIdx);
      executeEraseStroke(x, y, x, y);
      redrawStage();
    } else if (e.shiftKey || e.button === 1) {
      isPanningRef.current = true;
    } else {
      isDraggingFrameRef.current = true;
    }
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const x = ((e.clientX - rect.left) / rect.width) * canvas.width;
    const y = ((e.clientY - rect.top) / rect.height) * canvas.height;

    if (activeTool === 'eraser') {
      setMouseCanvasPos({ x, y });
    }

    if (isErasingRef.current && lastErasePointRef.current) {
      executeEraseStroke(lastErasePointRef.current.x, lastErasePointRef.current.y, x, y);
      lastErasePointRef.current = { x, y };
      redrawStage();
    } else if (isPanningRef.current) {
      const dx = e.clientX - lastMousePosRef.current.x;
      const dy = e.clientY - lastMousePosRef.current.y;
      lastMousePosRef.current = { x: e.clientX, y: e.clientY };
      setPan((p) => ({ x: p.x + dx, y: p.y + dy }));
    } else if (isDraggingFrameRef.current && currentFrame) {
      const dx = (e.clientX - lastMousePosRef.current.x) / zoom;
      const dy = (e.clientY - lastMousePosRef.current.y) / zoom;
      lastMousePosRef.current = { x: e.clientX, y: e.clientY };
      setFrames((prev) =>
        prev.map((f, i) =>
          i === currentFrameIdx
            ? {
                ...f,
                offsetX: Math.round((f.offsetX || 0) + dx),
                offsetY: Math.round((f.offsetY || 0) + dy),
              }
            : f
        )
      );
    }
  };

  const handleMouseUp = () => {
    if (isErasingRef.current) {
      isErasingRef.current = false;
      lastErasePointRef.current = null;

      const cached = frameCanvasesRef.current.get(currentFrameIdx);
      if (cached && currentFrame) {
        const frameCanvas = cached.canvas;
        const nextDataUrl = frameCanvas.toDataURL('image/png');

        // Immediate cache
        const img = new Image();
        img.src = nextDataUrl;
        imageCacheRef.current.set(nextDataUrl, img);

        // Update in-memory persistent reference
        frameCanvasesRef.current.set(currentFrameIdx, { url: nextDataUrl, canvas: frameCanvas });

        const prevTransparent = currentFrame.transparentDataUrl;
        const prevOriginal = currentFrame.originalDataUrl;

        // Update current frame in state (save transparentDataUrl so preview mode shows erased frame)
        setFrames((prev) =>
          prev.map((f, i) =>
            i === currentFrameIdx
              ? {
                  ...f,
                  transparentDataUrl: nextDataUrl,
                }
              : f
          )
        );

        // Record Undo History
        const nextHistory = historyStackRef.current.slice(0, historyIndexRef.current + 1);
        nextHistory.push({
          updates: [
            {
              index: currentFrameIdx,
              prevTransparentUrl: prevTransparent,
              prevOriginalUrl: prevOriginal,
              nextUrl: nextDataUrl,
            },
          ],
        });
        if (nextHistory.length > 30) nextHistory.shift();
        historyStackRef.current = nextHistory;
        historyIndexRef.current = nextHistory.length - 1;
        updateHistoryFlags();
      }

      redrawStage();
    }

    isPanningRef.current = false;
    isDraggingFrameRef.current = false;
  };

  /**
   * Action: Batch Apply the Accumulated Erase Mask Across ALL Frames
   * Runs in asynchronous non-blocking chunks (10 frames per tick) so it NEVER freezes!
   */
  const handleApplyEraseToAllFrames = async () => {
    if (!accumulatedMaskCanvasRef.current || frames.length === 0 || isApplyingBatch) return;

    setIsApplyingBatch(true);
    onShowToast?.(`Đang áp dụng vết tẩy đồng loạt trên ${frames.length} frame...`, 'info');

    const maskCanvas = accumulatedMaskCanvasRef.current;
    const total = frames.length;
    const updatedFrames = [...frames];
    const snapshotUpdates: { index: number; prevTransparentUrl?: string; prevOriginalUrl?: string; nextUrl: string }[] = [];

    const chunkSize = 10;
    for (let startIdx = 0; startIdx < total; startIdx += chunkSize) {
      const endIdx = Math.min(startIdx + chunkSize, total);

      for (let i = startIdx; i < endIdx; i++) {
        const frame = updatedFrames[i];
        if (!frame) continue;

        const offscreen = getOrCreateFrameCanvas(i);
        if (!offscreen) continue;

        const ctx = offscreen.getContext('2d');
        if (!ctx) continue;

        ctx.save();
        ctx.globalCompositeOperation = 'destination-out';
        ctx.drawImage(maskCanvas, 0, 0, offscreen.width, offscreen.height);
        ctx.restore();

        const newUrl = offscreen.toDataURL('image/png');

        // Immediate Image Cache
        const img = new Image();
        img.src = newUrl;
        imageCacheRef.current.set(newUrl, img);
        frameCanvasesRef.current.set(i, { url: newUrl, canvas: offscreen });

        snapshotUpdates.push({
          index: i,
          prevTransparentUrl: frame.transparentDataUrl,
          prevOriginalUrl: frame.originalDataUrl,
          nextUrl: newUrl,
        });

        updatedFrames[i] = {
          ...frame,
          transparentDataUrl: newUrl,
        };
      }

      setBatchProgress({
        current: endIdx,
        total,
        percent: Math.round((endIdx / total) * 100),
      });

      // Yield thread to allow React to render live progress
      await new Promise((resolve) => setTimeout(resolve, 15));
    }

    setFrames(updatedFrames);

    // Record Undo Snapshot for the entire batch
    const nextHistory = historyStackRef.current.slice(0, historyIndexRef.current + 1);
    nextHistory.push({ updates: snapshotUpdates });
    if (nextHistory.length > 30) nextHistory.shift();
    historyStackRef.current = nextHistory;
    historyIndexRef.current = nextHistory.length - 1;
    updateHistoryFlags();

    setBatchProgress(null);
    setIsApplyingBatch(false);
    onShowToast?.(`✓ Đã áp dụng tẩy đồng loạt thành công cho ${total} frame!`, 'success');
    redrawStage();
  };

  /**
   * Action: Clear Erase Mask
   */
  const handleClearEraseMask = () => {
    accumulatedMaskCanvasRef.current = null;
    setHasEraseMask(false);
    onShowToast?.('Đã xóa vùng nhớ vệt cọ', 'info');
  };

  // Undo Handler
  const handleUndo = useCallback(() => {
    if (historyIndexRef.current >= 0) {
      const snap = historyStackRef.current[historyIndexRef.current];
      if (snap) {
        setFrames((prev) => {
          const next = [...prev];
          snap.updates.forEach((u) => {
            if (next[u.index]) {
              next[u.index] = {
                ...next[u.index],
                transparentDataUrl: u.prevTransparentUrl || next[u.index].transparentDataUrl,
                originalDataUrl: u.prevOriginalUrl || next[u.index].originalDataUrl,
              };
              frameCanvasesRef.current.delete(u.index);
            }
          });
          return next;
        });
        historyIndexRef.current -= 1;
        updateHistoryFlags();
        onShowToast?.('↩ Đã hoàn tác cọ tẩy (Undo)', 'info');
        redrawStage();
      }
    }
  }, [setFrames, updateHistoryFlags, onShowToast, redrawStage]);

  // Redo Handler
  const handleRedo = useCallback(() => {
    if (historyIndexRef.current < historyStackRef.current.length - 1) {
      historyIndexRef.current += 1;
      const snap = historyStackRef.current[historyIndexRef.current];
      if (snap) {
        setFrames((prev) => {
          const next = [...prev];
          snap.updates.forEach((u) => {
            if (next[u.index]) {
              next[u.index] = {
                ...next[u.index],
                transparentDataUrl: u.nextUrl,
              };
              frameCanvasesRef.current.delete(u.index);
            }
          });
          return next;
        });
        updateHistoryFlags();
        onShowToast?.('↪ Đã làm lại cọ tẩy (Redo)', 'info');
        redrawStage();
      }
    }
  }, [setFrames, updateHistoryFlags, onShowToast, redrawStage]);

  // Keyboard Shortcuts
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (['INPUT', 'TEXTAREA'].includes((e.target as HTMLElement)?.tagName)) return;

      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') {
        e.preventDefault();
        if (e.shiftKey) {
          handleRedo();
        } else {
          handleUndo();
        }
      } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'y') {
        e.preventDefault();
        handleRedo();
      } else if (e.key === '[') {
        setBrushRadius((r) => Math.max(4, r - 4));
      } else if (e.key === ']') {
        setBrushRadius((r) => Math.min(80, r + 4));
      } else if (e.key.toLowerCase() === 'e' && !e.ctrlKey && !e.metaKey && !e.altKey) {
        if (hasPeeledBackground) {
          setActiveTool((t) => (t === 'eraser' ? 'select' : 'eraser'));
        } else {
          onShowToast?.('Vui lòng bóc nền trước khi sử dụng cọ tẩy pixel!', 'info');
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [handleUndo, handleRedo, hasPeeledBackground, onShowToast]);

  const handleWheel = (e: React.WheelEvent) => {
    e.preventDefault();
    if (e.deltaY < 0) {
      setZoom((z) => Math.min(z + 0.15, 4.0));
    } else {
      setZoom((z) => Math.max(z - 0.15, 0.25));
    }
  };

  return (
    <div
      ref={containerRef}
      style={{
        width: '100%',
        height: '100%',
        position: 'relative',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        overflow: 'hidden',
        background: '#070a13',
        borderRadius: 8,
        userSelect: 'none',
      }}
    >
      {/* ─── FLOATING TOP-RIGHT & TOOLS OVERLAY (ERASER, BATCH BUTTON, ZOOM) ─ */}
      <VideoSlicerStageOverlayControls
        viewMode="frames"
        checkerTheme={checkerTheme}
        setCheckerTheme={setCheckerTheme}
        onionSkinMode={onionSkinMode}
        onToggleOnionSkin={onToggleOnionSkin}
        zoom={zoom}
        setZoom={setZoom}
        onResetZoom={() => {
          setZoom(1.0);
          setPan({ x: 0, y: 0 });
        }}
        activeTool={activeTool}
        setActiveTool={setActiveTool}
        brushRadius={brushRadius}
        setBrushRadius={setBrushRadius}
        canUndo={canUndo}
        canRedo={canRedo}
        onUndo={handleUndo}
        onRedo={handleRedo}
        totalFramesCount={frames.length}
        hasEraseMask={hasEraseMask}
        isApplyingBatch={isApplyingBatch}
        batchProgress={batchProgress}
        hasPeeledBackground={hasPeeledBackground}
        onApplyEraseToAllFrames={handleApplyEraseToAllFrames}
        onClearEraseMask={handleClearEraseMask}
      />

      {/* ─── 120 FPS HIGH-PERFORMANCE 2D CANVAS ─── */}
      <canvas
        ref={canvasRef}
        width={960}
        height={600}
        onMouseDown={handleMouseDown}
        onMouseMove={handleMouseMove}
        onMouseUp={handleMouseUp}
        onMouseLeave={() => {
          setMouseCanvasPos(null);
          handleMouseUp();
        }}
        onWheel={handleWheel}
        style={{
          width: '100%',
          height: '100%',
          objectFit: 'contain',
          cursor: activeTool === 'eraser' ? 'crosshair' : 'default',
          display: 'block',
        }}
      />

      {/* Status Badge */}
      {currentFrame && (
        <div
          style={{
            position: 'absolute',
            bottom: 10,
            left: 10,
            background: 'rgba(15, 23, 42, 0.88)',
            backdropFilter: 'blur(8px)',
            border: '1px solid rgba(255, 255, 255, 0.1)',
            borderRadius: 6,
            padding: '4px 10px',
            fontSize: 10,
            color: '#94a3b8',
            display: 'flex',
            gap: 8,
            alignItems: 'center',
            zIndex: 10,
          }}
        >
          <span style={{ color: '#38bdf8', fontWeight: 700 }}>
            Frame {currentFrameIdx + 1}/{frames.length}
          </span>
          <span>•</span>
          <span>{currentFrame.timestamp.toFixed(2)}s</span>
          <span>•</span>
          <span style={{ color: '#fbbf24' }}>Thời lượng: {currentFrame.durationMs}ms</span>
          <span>•</span>
          <span style={{ color: '#34d399' }}>
            {previewDisplayMode === 'transparent'
              ? currentFrame.transparentDataUrl && currentFrame.transparentDataUrl !== currentFrame.originalDataUrl
                ? 'Đã Bóc Nền'
                : 'Chưa Bóc (Xem Ảnh Gốc)'
              : 'Ảnh Gốc'}
          </span>
          {activeTool === 'eraser' && (
            <>
              <span>•</span>
              <span style={{ color: '#ef4444', fontWeight: 700 }}>
                🧹 Đang bật cọ tẩy ({brushRadius}px)
              </span>
              {hasEraseMask && (
                <span style={{ color: '#c084fc', fontWeight: 700 }}>
                  • ✨ Sẵn sàng áp dụng cho {frames.length} frame
                </span>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
};
