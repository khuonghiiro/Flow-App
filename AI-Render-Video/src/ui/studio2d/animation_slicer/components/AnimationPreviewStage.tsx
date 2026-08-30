// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React, { useRef, useEffect, useState, useCallback } from 'react';
import {
  Play,
  Pause,
  ZoomIn,
  ZoomOut,
  Sparkles,
  Eraser,
  Sun,
  Moon,
  Square,
  Undo2,
  Redo2,
  Scissors,
  Crop,
  Check,
  X,
} from 'lucide-react';
import { AnimationSliceFrame } from '../../../../types/animation_slicer';
import {
  initWorkingCanvases,
  eraseSegmentOnWorkingCanvas,
  commitWorkingCanvases,
  WorkingCanvasItem,
} from '../utils/pixelEraserHelper';
import { StageCropRect, cropAllFramesWithStageRect } from '../utils/manualBBoxCropHelper';
import { AnimationStageOverlayControls } from './AnimationStageOverlayControls';

interface AnimationPreviewStageProps {
  frames: AnimationSliceFrame[];
  frameOrder: number[];
  selectedFrameIndex: number | null;
  fps: number;
  loopMode: 'loop' | 'ping_pong' | 'once';
  onionSkinMode: 'off' | 'sequential' | 'all';
  showBBox: boolean;
  canUndo?: boolean;
  canRedo?: boolean;
  onUndo?: () => void;
  onRedo?: () => void;
  onAutoTrimAllBBox?: () => void;
  onApplyManualCropAllFrames?: (cropRect: StageCropRect) => void;
  onSelectFrameIndex: (index: number) => void;
  onUpdateFrameTransform: (
    frameIndex: number,
    updates: Partial<Pick<AnimationSliceFrame, 'offsetX' | 'offsetY' | 'scale' | 'rotation' | 'flipX' | 'durationMs' | 'transparentDataUrl'>>
  ) => void;
  onUpdateMultipleFramesDataUrl: (updates: { index: number; dataUrl: string }[], label?: string) => void;
  onToggleOnionSkinMode?: () => void;
  onToggleShowBBox: () => void;
  onLoadDemoFrames?: () => void;
}

const GHOST_COLORS = ['#38bdf8', '#a855f7', '#4ade80', '#facc15', '#f472b6', '#34d399', '#fb923c', '#818cf8'];

export const AnimationPreviewStage: React.FC<AnimationPreviewStageProps> = ({
  frames,
  frameOrder,
  selectedFrameIndex,
  fps,
  loopMode,
  onionSkinMode,
  showBBox,
  canUndo = false,
  canRedo = false,
  onUndo,
  onRedo,
  onAutoTrimAllBBox,
  onApplyManualCropAllFrames,
  onSelectFrameIndex,
  onUpdateFrameTransform,
  onUpdateMultipleFramesDataUrl,
  onToggleOnionSkinMode,
  onToggleShowBBox,
  onLoadDemoFrames,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [isPlaying, setIsPlaying] = useState<boolean>(true);
  const [playbackCursor, setPlaybackCursor] = useState<number>(0);
  const [zoom, setZoom] = useState<number>(1.0);
  const [pan, setPan] = useState<{ x: number; y: number }>({ x: 0, y: 0 });

  // Themes & Tools
  const [checkerTheme, setCheckerTheme] = useState<'dark' | 'light'>('dark');
  const [activeTool, setActiveTool] = useState<'select' | 'eraser' | 'manual_crop'>('select');
  const [brushRadius, setBrushRadius] = useState<number>(18);
  const [eraseAllFrames, setEraseAllFrames] = useState<boolean>(true);
  const [mouseCanvasPos, setMouseCanvasPos] = useState<{ x: number; y: number } | null>(null);

  // Manual Crop BBox State (Stage-relative)
  const [manualCropRect, setManualCropRect] = useState<StageCropRect>({
    x: -80,
    y: -220,
    width: 160,
    height: 220,
  });
  const isDraggingCropRef = useRef<'move' | 'nw' | 'ne' | 'sw' | 'se' | null>(null);

  // In-Memory Fast Working Canvases for Lag-Free Erasing
  const workingCanvasesRef = useRef<Map<number, WorkingCanvasItem> | null>(null);
  const isErasingRef = useRef<boolean>(false);
  const lastErasePointRef = useRef<{ x: number; y: number } | null>(null);

  // Dragging / Panning
  const isDraggingFrameRef = useRef<boolean>(false);
  const isPanningRef = useRef<boolean>(false);
  const lastMousePosRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 });

  const imageCacheRef = useRef<Map<string, HTMLImageElement>>(new Map());

  // Image cache loader with auto-reload trigger
  const getCachedImage = useCallback((url: string): HTMLImageElement | null => {
    if (!url) return null;
    if (imageCacheRef.current.has(url)) {
      const cached = imageCacheRef.current.get(url)!;
      if (cached.complete || cached.naturalWidth > 0) {
        return cached;
      }
    }
    const img = new Image();
    img.onload = () => {
      redrawStage();
    };
    img.src = url;
    imageCacheRef.current.set(url, img);
    return img;
  }, []);

  // Preload all frames images into memory
  useEffect(() => {
    frames.forEach((f) => {
      const url = f.transparentDataUrl || f.originalDataUrl;
      if (url && !imageCacheRef.current.has(url)) {
        const img = new Image();
        img.onload = () => redrawStage();
        img.src = url;
        imageCacheRef.current.set(url, img);
      }
    });
  }, [frames]);

  // Playback timer loop
  useEffect(() => {
    if (!isPlaying || frameOrder.length === 0) return;

    let activeOrderIdx = playbackCursor;
    if (loopMode === 'ping_pong' && frameOrder.length > 1) {
      const cycle = frameOrder.length * 2 - 2;
      const mod = playbackCursor % cycle;
      activeOrderIdx = mod < frameOrder.length ? mod : cycle - mod;
    }
    const curFrameIdx = frameOrder[activeOrderIdx] ?? 0;
    const curFrame = frames[curFrameIdx] || frames[0];
    const frameDur = Math.max(30, curFrame?.durationMs || Math.round(1000 / (fps || 8)) || 500);

    const timer = setTimeout(() => {
      setPlaybackCursor((prev) => {
        if (loopMode === 'ping_pong') {
          return (prev + 1) % (frameOrder.length * 2 - 2 || 1);
        }
        return (prev + 1) % frameOrder.length;
      });
    }, frameDur);

    return () => clearTimeout(timer);
  }, [isPlaying, playbackCursor, frameOrder, loopMode, frames, fps]);

  // Current active frame calculation
  let activeOrderIdx = playbackCursor;
  if (loopMode === 'ping_pong' && frameOrder.length > 1) {
    const cycle = frameOrder.length * 2 - 2;
    const mod = playbackCursor % cycle;
    activeOrderIdx = mod < frameOrder.length ? mod : cycle - mod;
  }
  const currentFrameIdx = isPlaying ? (frameOrder[activeOrderIdx] ?? 0) : (selectedFrameIndex ?? 0);
  const currentFrame = frames[currentFrameIdx] || frames[0];

  // STRICT SEQUENTIAL GHOST (K-1):
  const prevGhostFrame = currentFrameIdx > 0 ? frames[currentFrameIdx - 1] : null;

  // Total duration
  const totalDurationMs = frameOrder.reduce((sum, fIdx) => sum + (frames[fIdx]?.durationMs || 500), 0);

  // Render Canvas
  const redrawStage = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const w = canvas.width;
    const h = canvas.height;
    ctx.clearRect(0, 0, w, h);

    // 1. Checkerboard Background
    const sq = 14;
    const c1 = checkerTheme === 'dark' ? '#090d16' : '#ffffff';
    const c2 = checkerTheme === 'dark' ? '#131b2e' : '#e2e8f0';
    for (let x = 0; x < w; x += sq) {
      for (let y = 0; y < h; y += sq) {
        ctx.fillStyle = (Math.floor(x / sq) + Math.floor(y / sq)) % 2 === 0 ? c1 : c2;
        ctx.fillRect(x, y, sq, sq);
      }
    }

    // 2. Reference Crosshair & Ground Line
    ctx.strokeStyle = checkerTheme === 'dark' ? 'rgba(56, 189, 248, 0.2)' : 'rgba(14, 165, 233, 0.35)';
    ctx.lineWidth = 1;
    ctx.setLineDash([4, 4]);

    ctx.beginPath();
    ctx.moveTo(w / 2 + pan.x, 0);
    ctx.lineTo(w / 2 + pan.x, h);
    ctx.stroke();

    ctx.strokeStyle = checkerTheme === 'dark' ? 'rgba(56, 189, 248, 0.45)' : 'rgba(2, 132, 199, 0.6)';
    ctx.setLineDash([]);
    ctx.beginPath();
    ctx.moveTo(0, h * 0.75 + pan.y);
    ctx.lineTo(w, h * 0.75 + pan.y);
    ctx.stroke();

    if (currentFrame) {
      ctx.save();
      ctx.translate(w / 2 + pan.x, h * 0.75 + pan.y);
      ctx.scale(zoom, zoom);

      // ─── 3. ONION SKIN GHOSTS ───
      if (onionSkinMode === 'sequential' && prevGhostFrame) {
        const prevWorkingItem = workingCanvasesRef.current?.get(currentFrameIdx - 1);
        const prevImg = prevWorkingItem ? prevWorkingItem.canvas : getCachedImage(prevGhostFrame.transparentDataUrl || prevGhostFrame.originalDataUrl);
        if (prevImg) {
          ctx.save();
          ctx.globalAlpha = 0.38;
          ctx.translate(prevGhostFrame.offsetX, prevGhostFrame.offsetY);
          ctx.rotate((prevGhostFrame.rotation * Math.PI) / 180);
          ctx.scale(prevGhostFrame.flipX ? -prevGhostFrame.scale : prevGhostFrame.scale, prevGhostFrame.scale);

          const pw = prevImg.width || 200;
          const ph = prevImg.height || 260;
          ctx.drawImage(prevImg, -pw / 2, -ph, pw, ph);

          if (showBBox) {
            ctx.strokeStyle = '#a855f7';
            ctx.lineWidth = 1.5;
            ctx.setLineDash([3, 3]);
            ctx.strokeRect(-pw / 2, -ph, pw, ph);

            ctx.fillStyle = '#a855f7';
            ctx.font = 'bold 10px sans-serif';
            ctx.fillText(`👻 Bóng ma [F${currentFrameIdx}]`, -pw / 2 + 4, -ph + 14);
          }
          ctx.restore();
        }
      } else if (onionSkinMode === 'all') {
        frames.forEach((gf, gIdx) => {
          if (gf.id === currentFrame.id) return;
          const workingItem = workingCanvasesRef.current?.get(gIdx);
          const gImg = workingItem ? workingItem.canvas : getCachedImage(gf.transparentDataUrl || gf.originalDataUrl);
          if (gImg) {
            ctx.save();
            ctx.globalAlpha = 0.32;
            ctx.translate(gf.offsetX, gf.offsetY);
            ctx.rotate((gf.rotation * Math.PI) / 180);
            ctx.scale(gf.flipX ? -gf.scale : gf.scale, gf.scale);

            const pw = gImg.width || 200;
            const ph = gImg.height || 260;
            ctx.drawImage(gImg, -pw / 2, -ph, pw, ph);

            if (showBBox) {
              const bColor = GHOST_COLORS[gIdx % GHOST_COLORS.length];
              ctx.strokeStyle = bColor;
              ctx.lineWidth = 1.5;
              ctx.setLineDash([3, 3]);
              ctx.strokeRect(-pw / 2, -ph, pw, ph);
              ctx.fillStyle = bColor;
              ctx.font = 'bold 10px sans-serif';
              ctx.fillText(`[F${gIdx + 1}]`, -pw / 2 + 4, -ph + 14);
            }
            ctx.restore();
          }
        });
      }

      // ─── 4. ACTIVE CURRENT FRAME ───
      const workingItem = workingCanvasesRef.current?.get(currentFrameIdx);
      const curImg = workingItem ? workingItem.canvas : getCachedImage(currentFrame.transparentDataUrl || currentFrame.originalDataUrl);
      if (curImg) {
        ctx.save();
        ctx.translate(currentFrame.offsetX, currentFrame.offsetY);
        ctx.rotate((currentFrame.rotation * Math.PI) / 180);
        ctx.scale(currentFrame.flipX ? -currentFrame.scale : currentFrame.scale, currentFrame.scale);

        const pw = curImg.width || 200;
        const ph = curImg.height || 260;
        ctx.drawImage(curImg, -pw / 2, -ph, pw, ph);

        if (showBBox) {
          ctx.strokeStyle = '#38bdf8';
          ctx.lineWidth = 2;
          ctx.setLineDash([]);
          ctx.strokeRect(-pw / 2, -ph, pw, ph);

          const handleSize = 6;
          ctx.fillStyle = '#facc15';
          [
            [-pw / 2, -ph],
            [pw / 2, -ph],
            [-pw / 2, 0],
            [pw / 2, 0],
          ].forEach(([hx, hy]) => {
            ctx.fillRect(hx - handleSize / 2, hy - handleSize / 2, handleSize, handleSize);
          });

          ctx.fillStyle = 'rgba(2, 132, 199, 0.95)';
          ctx.fillRect(-pw / 2, -ph - 18, 140, 16);
          ctx.fillStyle = '#ffffff';
          ctx.font = 'bold 9.5px sans-serif';
          ctx.fillText(`F${currentFrameIdx + 1} (${currentFrame.offsetX}px, ${currentFrame.offsetY}px)`, -pw / 2 + 4, -ph - 6);
        }
        ctx.restore();
      }

      // ─── 5. MANUAL CROP BOUNDING BOX ───
      if (activeTool === 'manual_crop') {
        ctx.strokeStyle = '#10b981';
        ctx.lineWidth = 2;
        ctx.setLineDash([4, 4]);
        ctx.strokeRect(manualCropRect.x, manualCropRect.y, manualCropRect.width, manualCropRect.height);

        ctx.fillStyle = '#34d399';
        const chSize = 8;
        [
          [manualCropRect.x, manualCropRect.y],
          [manualCropRect.x + manualCropRect.width, manualCropRect.y],
          [manualCropRect.x, manualCropRect.y + manualCropRect.height],
          [manualCropRect.x + manualCropRect.width, manualCropRect.y + manualCropRect.height],
        ].forEach(([cx, cy]) => {
          ctx.fillRect(cx - chSize / 2, cy - chSize / 2, chSize, chSize);
        });

        ctx.fillStyle = 'rgba(5, 150, 105, 0.95)';
        ctx.fillRect(manualCropRect.x, manualCropRect.y - 20, 140, 18);
        ctx.fillStyle = '#ffffff';
        ctx.font = 'bold 10px sans-serif';
        ctx.fillText(`Khung Cắt: ${Math.round(manualCropRect.width)} × ${Math.round(manualCropRect.height)}px`, manualCropRect.x + 4, manualCropRect.y - 7);
      }

      ctx.restore();
    }

    // ─── 6. ERASER BRUSH CIRCULAR CURSOR PREVIEW ───
    if (activeTool === 'eraser' && mouseCanvasPos) {
      ctx.save();
      ctx.beginPath();
      ctx.arc(mouseCanvasPos.x, mouseCanvasPos.y, brushRadius, 0, Math.PI * 2);
      ctx.fillStyle = 'rgba(239, 68, 68, 0.25)';
      ctx.fill();
      ctx.strokeStyle = '#ef4444';
      ctx.lineWidth = 1.5;
      ctx.stroke();

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
  }, [currentFrame, prevGhostFrame, frames, onionSkinMode, showBBox, checkerTheme, zoom, pan, isPlaying, playbackCursor, getCachedImage, currentFrameIdx, activeTool, mouseCanvasPos, brushRadius, manualCropRect]);

  useEffect(() => {
    redrawStage();
  }, [redrawStage]);

  // Execute Manual Crop All Frames
  const handleExecuteManualCrop = async () => {
    if (onApplyManualCropAllFrames) {
      onApplyManualCropAllFrames(manualCropRect);
      setActiveTool('select');
    } else {
      const cropped = await cropAllFramesWithStageRect(frames, manualCropRect, getCachedImage);
      const updates = cropped.map((f, idx) => ({ index: idx, dataUrl: f.transparentDataUrl }));
      onUpdateMultipleFramesDataUrl(updates, 'Cắt BBox thủ công');
      setActiveTool('select');
    }
  };

  // Mouse Handlers
  const handleMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const x = ((e.clientX - rect.left) / rect.width) * 960;
    const y = ((e.clientY - rect.top) / rect.height) * 600;

    lastMousePosRef.current = { x: e.clientX, y: e.clientY };

    if (activeTool === 'eraser') {
      isErasingRef.current = true;
      lastErasePointRef.current = { x, y };

      // Guaranteed to include ALL frame indices when eraseAllFrames is checked
      const targetIndices = eraseAllFrames ? frames.map((_, i) => i) : [currentFrameIdx];
      workingCanvasesRef.current = initWorkingCanvases(frames, targetIndices, getCachedImage);

      const canvas = canvasRef.current;
      if (canvas && workingCanvasesRef.current) {
        workingCanvasesRef.current.forEach((item) => {
          eraseSegmentOnWorkingCanvas(item, frames[item.index], x, y, x, y, brushRadius, canvas.width, canvas.height, pan.x, pan.y, zoom);
        });
        redrawStage();
      }
    } else if (activeTool === 'manual_crop') {
      isDraggingCropRef.current = 'move';
    } else if (e.shiftKey || e.button === 1) {
      isPanningRef.current = true;
    } else {
      isDraggingFrameRef.current = true;
    }
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const x = ((e.clientX - rect.left) / rect.width) * 960;
    const y = ((e.clientY - rect.top) / rect.height) * 600;

    if (activeTool === 'eraser') {
      setMouseCanvasPos({ x, y });
    }

    if (isErasingRef.current && lastErasePointRef.current && workingCanvasesRef.current) {
      const canvas = canvasRef.current;
      if (canvas) {
        workingCanvasesRef.current.forEach((item) => {
          eraseSegmentOnWorkingCanvas(
            item,
            frames[item.index],
            lastErasePointRef.current!.x,
            lastErasePointRef.current!.y,
            x,
            y,
            brushRadius,
            canvas.width,
            canvas.height,
            pan.x,
            pan.y,
            zoom
          );
        });
        lastErasePointRef.current = { x, y };
        redrawStage();
      }
    } else if (activeTool === 'manual_crop' && isDraggingCropRef.current) {
      const dx = (e.clientX - lastMousePosRef.current.x) / zoom;
      const dy = (e.clientY - lastMousePosRef.current.y) / zoom;
      lastMousePosRef.current = { x: e.clientX, y: e.clientY };

      setManualCropRect((prev) => ({
        ...prev,
        x: Math.round(prev.x + dx),
        y: Math.round(prev.y + dy),
      }));
    } else if (isPanningRef.current) {
      const dx = e.clientX - lastMousePosRef.current.x;
      const dy = e.clientY - lastMousePosRef.current.y;
      lastMousePosRef.current = { x: e.clientX, y: e.clientY };
      setPan((p) => ({ x: p.x + dx, y: p.y + dy }));
    } else if (isDraggingFrameRef.current && currentFrame) {
      const dx = (e.clientX - lastMousePosRef.current.x) / zoom;
      const dy = (e.clientY - lastMousePosRef.current.y) / zoom;
      lastMousePosRef.current = { x: e.clientX, y: e.clientY };
      onUpdateFrameTransform(currentFrameIdx, {
        offsetX: Math.round(currentFrame.offsetX + dx),
        offsetY: Math.round(currentFrame.offsetY + dy),
      });
    }
  };

  const handleMouseUp = () => {
    if (isErasingRef.current && workingCanvasesRef.current) {
      isErasingRef.current = false;
      lastErasePointRef.current = null;
      const updates = commitWorkingCanvases(workingCanvasesRef.current);

      // Preload updated dataUrls immediately into cache so switching frames shows erased pixels instantly
      updates.forEach((u) => {
        const img = new Image();
        img.src = u.dataUrl;
        imageCacheRef.current.set(u.dataUrl, img);
      });

      workingCanvasesRef.current = null;
      onUpdateMultipleFramesDataUrl(updates, 'Tẩy pixel tất cả frame');
    }

    isDraggingCropRef.current = null;
    isPanningRef.current = false;
    isDraggingFrameRef.current = false;
  };

  return (
    <div
      style={{
        position: 'relative',
        width: '100%',
        height: '100%',
        background: '#040711',
        borderRadius: 8,
        overflow: 'hidden',
        border: '1px solid rgba(56, 189, 248, 0.25)',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      {/* Main Canvas */}
      <canvas
        ref={canvasRef}
        width={960}
        height={600}
        onMouseDown={handleMouseDown}
        onMouseMove={handleMouseMove}
        onMouseUp={handleMouseUp}
        onMouseLeave={() => {
          handleMouseUp();
          setMouseCanvasPos(null);
        }}
        onWheel={(e) => {
          e.preventDefault();
          const factor = e.deltaY < 0 ? 1.1 : 0.9;
          setZoom((z) => Math.max(0.3, Math.min(3.5, z * factor)));
        }}
        style={{
          width: '100%',
          height: '100%',
          objectFit: 'contain',
          cursor: activeTool === 'eraser' ? 'none' : activeTool === 'manual_crop' ? 'move' : isPanningRef.current ? 'grabbing' : 'grab',
        }}
      />

      {/* Empty State Overlay */}
      {frames.length === 0 && (
        <div
          style={{
            position: 'absolute',
            inset: 0,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 12,
            background: 'rgba(4, 7, 17, 0.85)',
            backdropFilter: 'blur(8px)',
            color: '#94a3b8',
          }}
        >
          <div style={{ width: 44, height: 44, borderRadius: '50%', background: 'rgba(56, 189, 248, 0.1)', border: '1px solid rgba(56, 189, 248, 0.3)', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#38bdf8' }}>
            <Play size={22} />
          </div>
          <div style={{ fontSize: 13, fontWeight: 700, color: '#f8fafc' }}>
            Chưa Có Khung Hình Hoạt Ảnh Nào
          </div>
          {onLoadDemoFrames && (
            <button
              onClick={onLoadDemoFrames}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '7px 16px',
                borderRadius: 6,
                background: 'linear-gradient(135deg, #0284c7, #a855f7)',
                border: 'none',
                color: '#ffffff',
                fontSize: 11,
                fontWeight: 700,
                cursor: 'pointer',
                boxShadow: '0 2px 12px rgba(56, 189, 248, 0.4)',
              }}
            >
              <Sparkles size={13} /> 🎲 Nạp Mẫu Hoạt Ảnh Demo
            </button>
          )}
        </div>
      )}

      {/* Top Left Floating Info Bar & Undo/Redo & Auto-Trim */}
      <div
        style={{
          position: 'absolute',
          top: 10,
          left: 10,
          display: 'flex',
          alignItems: 'center',
          gap: 5,
        }}
      >
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 5,
            background: 'rgba(9, 13, 22, 0.92)',
            backdropFilter: 'blur(12px)',
            border: '1px solid rgba(255, 255, 255, 0.1)',
            borderRadius: 6,
            padding: '4px 8px',
            fontSize: 10,
            color: '#cbd5e1',
          }}
        >
          <span style={{ color: '#38bdf8', fontWeight: 700 }}>
            🎬 FRAME F{currentFrameIdx + 1}
          </span>
          <span>•</span>
          <span style={{ color: '#4ade80', fontWeight: 700 }}>
            ⏱️ {((currentFrame?.durationMs || 500) / 1000).toFixed(2)}s
          </span>
          <span>•</span>
          <span style={{ color: '#facc15', fontWeight: 600 }}>
            Tổng: {(totalDurationMs / 1000).toFixed(2)}s
          </span>
          {onionSkinMode === 'sequential' && (
            <span style={{ color: '#c084fc', fontWeight: 700, fontSize: 9.5 }}>
              {currentFrameIdx === 0 ? '• F1 (Không bóng ma)' : `• Bóng ma: F${currentFrameIdx}`}
            </span>
          )}
        </div>
      </div>

      {/* Floating Overlays & Controls Subcomponent */}
      <AnimationStageOverlayControls
        checkerTheme={checkerTheme}
        setCheckerTheme={setCheckerTheme}
        canUndo={canUndo}
        canRedo={canRedo}
        onUndo={onUndo}
        onRedo={onRedo}
        onAutoTrimAllBBox={onAutoTrimAllBBox}
        onionSkinMode={onionSkinMode}
        onToggleOnionSkin={onToggleOnionSkinMode || (() => {})}
        showBBox={showBBox}
        onToggleShowBBox={onToggleShowBBox}
        zoom={zoom}
        setZoom={setZoom}
        setPan={setPan}
        activeTool={activeTool}
        setActiveTool={setActiveTool}
        brushRadius={brushRadius}
        setBrushRadius={setBrushRadius}
        eraseAllFrames={eraseAllFrames}
        setEraseAllFrames={setEraseAllFrames}
        handleExecuteManualCrop={handleExecuteManualCrop}
        isPlaying={isPlaying}
        setIsPlaying={setIsPlaying}
      />
    </div>
  );
};
