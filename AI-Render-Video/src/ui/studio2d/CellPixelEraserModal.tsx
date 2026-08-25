import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Eraser, Save, X } from 'lucide-react';
import {
  renderPhotoshopStrokePath,
  executeMagicWandErase,
  executeSmartDespeckle,
  executeDespillGreen,
} from './eraser/eraserCanvasAlgorithms';
import { EraserToolbar } from './eraser/EraserToolbar';

export interface CellPixelEraserModalProps {
  isOpen: boolean;
  onClose: () => void;
  cellTitle: string;
  initialImageDataUrl: string;
  onSave: (newDataUrl: string) => void;
}

export const CellPixelEraserModal: React.FC<CellPixelEraserModalProps> = ({
  isOpen,
  onClose,
  cellTitle,
  initialImageDataUrl,
  onSave,
}) => {
  const [tool, setTool] = useState<'brush' | 'rect_erase' | 'magic_wand' | 'pan'>('brush');

  // Adobe Photoshop Soft Brush Controls
  const [brushSize, setBrushSize] = useState<number>(18);
  const [hardness, setHardness] = useState<number>(0); // 0% (Soft Gaussian) to 100% (Hard Solid)
  const [opacity, setOpacity] = useState<number>(60); // 5% to 100%
  const [flow, setFlow] = useState<number>(85); // 5% to 100%
  const [zoom, setZoom] = useState<number>(3);
  const [panOffset, setPanOffset] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [magicTolerance, setMagicTolerance] = useState<number>(25);

  const [undoStack, setUndoStack] = useState<ImageData[]>([]);
  const [redoStack, setRedoStack] = useState<ImageData[]>([]);
  const [isDrawing, setIsDrawing] = useState<boolean>(false);
  const [isPanning, setIsPanning] = useState<boolean>(false);
  const [panStart, setPanStart] = useState<{ x: number; y: number }>({ x: 0, y: 0 });

  const [rectSelection, setRectSelection] = useState<{ startX: number; startY: number; currX: number; currY: number } | null>(null);
  const [cursorPos, setCursorPos] = useState<{ imgX: number; imgY: number } | null>(null);
  const [containerMousePos, setContainerMousePos] = useState<{ x: number; y: number } | null>(null);
  const [isSpacePressed, setIsSpacePressed] = useState<boolean>(false);

  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const imgDimensionsRef = useRef<{ w: number; h: number }>({ w: 100, h: 100 });

  // Photoshop Continuous Stroke Path Buffers
  const strokeInitialImageDataRef = useRef<ImageData | null>(null);
  const strokeMaskCanvasRef = useRef<HTMLCanvasElement | null>(null);
  const strokePointsRef = useRef<{ x: number; y: number }[]>([]);

  // Spacebar pan listener
  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.code === 'Space' && !e.repeat && !(e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement)) {
        e.preventDefault();
        setIsSpacePressed(true);
      }
    };
    const handleKeyUp = (e: KeyboardEvent) => {
      if (e.code === 'Space') {
        setIsSpacePressed(false);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
    };
  }, [isOpen]);

  // Initialize canvas with source image
  useEffect(() => {
    if (!isOpen || !initialImageDataUrl) return;

    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      const canvas = canvasRef.current;
      if (!canvas) return;

      canvas.width = img.naturalWidth || img.width;
      canvas.height = img.naturalHeight || img.height;
      imgDimensionsRef.current = { w: canvas.width, h: canvas.height };

      const ctx = canvas.getContext('2d', { willReadFrequently: true });
      if (!ctx) return;

      ctx.clearRect(0, 0, canvas.width, canvas.height);
      ctx.drawImage(img, 0, 0);

      const initialData = ctx.getImageData(0, 0, canvas.width, canvas.height);
      setUndoStack([initialData]);
      setRedoStack([]);
      setPanOffset({ x: 0, y: 0 });

      if (containerRef.current) {
        const containerW = containerRef.current.clientWidth;
        const containerH = containerRef.current.clientHeight;
        const fitZoom = Math.min(8, Math.max(1.5, Math.floor(Math.min(containerW / canvas.width, containerH / canvas.height) * 0.75)));
        setZoom(fitZoom);
      }
    };
    img.src = initialImageDataUrl;
  }, [isOpen, initialImageDataUrl]);

  /**
   * Pushes current canvas state to Undo history
   */
  const saveStateToUndo = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return;

    const currentState = ctx.getImageData(0, 0, canvas.width, canvas.height);
    setUndoStack((prev) => {
      const next = [...prev, currentState];
      if (next.length > 25) next.shift();
      return next;
    });
    setRedoStack([]);
  }, []);

  const handleUndo = useCallback(() => {
    if (undoStack.length <= 1) return;
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return;

    const current = undoStack[undoStack.length - 1];
    const prev = undoStack[undoStack.length - 2];

    setRedoStack((r) => [...r, current]);
    setUndoStack((u) => u.slice(0, u.length - 1));
    ctx.putImageData(prev, 0, 0);
  }, [undoStack]);

  const handleRedo = useCallback(() => {
    if (redoStack.length === 0) return;
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return;

    const next = redoStack[redoStack.length - 1];
    setRedoStack((r) => r.slice(0, r.length - 1));
    setUndoStack((u) => [...u, next]);
    ctx.putImageData(next, 0, 0);
  }, [redoStack]);

  // Keyboard shortcut listener
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (!isOpen) return;
      if ((e.ctrlKey || e.metaKey) && e.key === 'z') {
        e.preventDefault();
        if (e.shiftKey) handleRedo();
        else handleUndo();
      } else if ((e.ctrlKey || e.metaKey) && e.key === 'y') {
        e.preventDefault();
        handleRedo();
      } else if (e.key === 'Escape') {
        onClose();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, handleUndo, handleRedo, onClose]);

  const startPhotoshopStroke = (startX: number, startY: number) => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return;

    if (!strokeMaskCanvasRef.current) {
      strokeMaskCanvasRef.current = document.createElement('canvas');
    }

    strokeInitialImageDataRef.current = ctx.getImageData(0, 0, canvas.width, canvas.height);
    strokePointsRef.current = [{ x: startX, y: startY }];
    renderPhotoshopStrokePath(canvas, strokeInitialImageDataRef.current, strokeMaskCanvasRef.current, strokePointsRef.current, {
      hardness,
      brushSize,
      flow,
      opacity,
    });
  };

  const continuePhotoshopStroke = (currX: number, currY: number) => {
    const canvas = canvasRef.current;
    if (!canvas || !strokeInitialImageDataRef.current || !strokeMaskCanvasRef.current) return;
    const last = strokePointsRef.current[strokePointsRef.current.length - 1];
    if (last && Math.hypot(currX - last.x, currY - last.y) < 1) return;

    strokePointsRef.current.push({ x: currX, y: currY });
    renderPhotoshopStrokePath(canvas, strokeInitialImageDataRef.current, strokeMaskCanvasRef.current, strokePointsRef.current, {
      hardness,
      brushSize,
      flow,
      opacity,
    });
  };

  const getImageCoords = (e: React.MouseEvent<HTMLDivElement>) => {
    const canvas = canvasRef.current;
    if (!canvas) return null;
    const rect = canvas.getBoundingClientRect();
    const screenX = e.clientX - rect.left;
    const screenY = e.clientY - rect.top;
    const imgX = Math.floor(screenX / zoom);
    const imgY = Math.floor(screenY / zoom);
    return { imgX, imgY, screenX, screenY };
  };

  const handleMouseDown = (e: React.MouseEvent<HTMLDivElement>) => {
    if (e.button === 1 || e.button === 2 || tool === 'pan' || isSpacePressed) {
      e.preventDefault();
      setIsPanning(true);
      setPanStart({ x: e.clientX - panOffset.x, y: e.clientY - panOffset.y });
      return;
    }
    if (e.button !== 0) return;

    const coords = getImageCoords(e);
    if (!coords) return;
    const { imgX, imgY } = coords;
    if (imgX < 0 || imgX >= imgDimensionsRef.current.w || imgY < 0 || imgY >= imgDimensionsRef.current.h) return;

    if (tool === 'brush') {
      saveStateToUndo();
      setIsDrawing(true);
      startPhotoshopStroke(imgX, imgY);
    } else if (tool === 'rect_erase') {
      setIsDrawing(true);
      setRectSelection({ startX: imgX, startY: imgY, currX: imgX, currY: imgY });
    } else if (tool === 'magic_wand' && canvasRef.current) {
      saveStateToUndo();
      executeMagicWandErase(canvasRef.current, imgX, imgY, magicTolerance);
    }
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (containerRef.current) {
      const cRect = containerRef.current.getBoundingClientRect();
      setContainerMousePos({ x: e.clientX - cRect.left, y: e.clientY - cRect.top });
    }

    if (isPanning) {
      setPanOffset({ x: e.clientX - panStart.x, y: e.clientY - panStart.y });
      return;
    }

    const coords = getImageCoords(e);
    if (!coords) return;
    const { imgX, imgY } = coords;
    setCursorPos({ imgX, imgY });

    if (!isDrawing) return;

    if (tool === 'brush') {
      continuePhotoshopStroke(imgX, imgY);
    } else if (tool === 'rect_erase' && rectSelection) {
      setRectSelection((prev) => (prev ? { ...prev, currX: imgX, currY: imgY } : null));
    }
  };

  const handleMouseUp = () => {
    if (isPanning) setIsPanning(false);

    if (isDrawing) {
      if (tool === 'rect_erase' && rectSelection && canvasRef.current) {
        const canvas = canvasRef.current;
        const ctx = canvas.getContext('2d', { willReadFrequently: true });
        if (ctx) {
          saveStateToUndo();
          const minX = Math.max(0, Math.min(rectSelection.startX, rectSelection.currX));
          const minY = Math.max(0, Math.min(rectSelection.startY, rectSelection.currY));
          const maxX = Math.min(canvas.width, Math.max(rectSelection.startX, rectSelection.currX));
          const maxY = Math.min(canvas.height, Math.max(rectSelection.startY, rectSelection.currY));

          ctx.save();
          ctx.globalCompositeOperation = 'destination-out';
          ctx.fillRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
          ctx.restore();
        }
        setRectSelection(null);
      }
      setIsDrawing(false);
      strokePointsRef.current = [];
      strokeInitialImageDataRef.current = null;
    }
  };

  const handleWheel = (e: React.WheelEvent<HTMLDivElement>) => {
    e.preventDefault();
    if (!containerRef.current) return;
    const cRect = containerRef.current.getBoundingClientRect();
    const mouseX = e.clientX - (cRect.left + cRect.width / 2);
    const mouseY = e.clientY - (cRect.top + cRect.height / 2);

    const zoomFactor = e.deltaY < 0 ? 1.18 : 1 / 1.18;
    const newZoom = Math.max(0.5, Math.min(32, Math.round(zoom * zoomFactor * 100) / 100));
    if (newZoom === zoom) return;

    const scaleRatio = newZoom / zoom;
    setZoom(newZoom);
    setPanOffset({
      x: mouseX - (mouseX - panOffset.x) * scaleRatio,
      y: mouseY - (mouseY - panOffset.y) * scaleRatio,
    });
  };

  const handleSaveAndApply = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    onSave(canvas.toDataURL('image/png'));
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 10000,
        background: 'rgba(3, 7, 18, 0.92)',
        backdropFilter: 'blur(20px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 12,
      }}
      onContextMenu={(e) => e.preventDefault()}
    >
      <div
        style={{
          width: '96vw',
          maxWidth: '1420px',
          height: '93vh',
          background: '#0b1120',
          border: '1px solid rgba(56, 189, 248, 0.3)',
          borderRadius: 12,
          boxShadow: '0 25px 60px rgba(0,0,0,0.85), 0 0 35px rgba(56, 189, 248, 0.2)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}
      >
        {/* Header */}
        <div style={{ padding: '12px 18px', background: 'rgba(15, 23, 42, 0.95)', borderBottom: '1px solid rgba(255, 255, 255, 0.1)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div style={{ width: 32, height: 32, borderRadius: 8, background: 'linear-gradient(135deg, #0284c7, #38bdf8)', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#fff', boxShadow: '0 0 12px rgba(56, 189, 248, 0.4)' }}>
              <Eraser size={18} />
            </div>
            <div>
              <h2 style={{ fontSize: 14, fontWeight: 700, color: '#f8fafc', margin: 0 }}>HỘP THOẠI TẨY XÓA PIXEL & CỌ MỀM PHOTOSHOP</h2>
              <p style={{ fontSize: 11, color: '#38bdf8', margin: 0 }}>{cellTitle} • Tẩy mềm mịn, giữ nguyên độ mờ Opacity dọc theo toàn bộ vết kéo chuột</p>
            </div>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <button onClick={handleSaveAndApply} style={{ padding: '7px 14px', fontSize: 12, fontWeight: 700, borderRadius: 6, background: 'linear-gradient(135deg, #0284c7, #2563eb)', color: '#fff', border: '1px solid #38bdf8', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 6, boxShadow: '0 0 15px rgba(56, 189, 248, 0.4)' }}>
              <Save size={14} /> LƯU & CẬP NHẬT VÀO 3D
            </button>
            <button onClick={onClose} style={{ padding: '6px', borderRadius: 6, background: 'rgba(255, 255, 255, 0.05)', color: '#94a3b8', border: '1px solid rgba(255, 255, 255, 0.1)', cursor: 'pointer' }} title="Đóng hộp thoại">
              <X size={16} />
            </button>
          </div>
        </div>

        {/* Modularized Toolbar */}
        <EraserToolbar
          tool={tool}
          setTool={setTool}
          brushSize={brushSize}
          setBrushSize={setBrushSize}
          hardness={hardness}
          setHardness={setHardness}
          opacity={opacity}
          setOpacity={setOpacity}
          flow={flow}
          setFlow={setFlow}
          magicTolerance={magicTolerance}
          setMagicTolerance={setMagicTolerance}
          zoom={zoom}
          setZoom={setZoom}
          onResetZoom100={() => { setZoom(1); setPanOffset({ x: 0, y: 0 }); }}
          onFitZoom={() => {
            if (containerRef.current && imgDimensionsRef.current.w > 0 && imgDimensionsRef.current.h > 0) {
              const cRect = containerRef.current.getBoundingClientRect();
              const fit = Math.min((cRect.width - 60) / imgDimensionsRef.current.w, (cRect.height - 60) / imgDimensionsRef.current.h);
              setZoom(Math.max(0.5, Math.min(10, Math.round(fit * 100) / 100)));
              setPanOffset({ x: 0, y: 0 });
            }
          }}
          undoCount={undoStack.length}
          redoCount={redoStack.length}
          onUndo={handleUndo}
          onRedo={handleRedo}
          onAutoDespeckle={() => { if (canvasRef.current) { saveStateToUndo(); executeSmartDespeckle(canvasRef.current); } }}
          onDespillGreen={() => { if (canvasRef.current) { saveStateToUndo(); executeDespillGreen(canvasRef.current); } }}
        />

        {/* Main Canvas Workspace */}
        <div
          ref={containerRef}
          onMouseDown={handleMouseDown}
          onMouseMove={handleMouseMove}
          onMouseUp={handleMouseUp}
          onMouseLeave={() => { handleMouseUp(); setContainerMousePos(null); }}
          onMouseEnter={(e) => {
            if (containerRef.current) {
              const cRect = containerRef.current.getBoundingClientRect();
              setContainerMousePos({ x: e.clientX - cRect.left, y: e.clientY - cRect.top });
            }
          }}
          onWheel={handleWheel}
          style={{
            flex: 1,
            position: 'relative',
            overflow: 'hidden',
            backgroundColor: '#0f172a',
            backgroundImage: 'linear-gradient(45deg, #1e293b 25%, transparent 25%), linear-gradient(-45deg, #1e293b 25%, transparent 25%), linear-gradient(45deg, transparent 75%, #1e293b 75%), linear-gradient(-45deg, transparent 75%, #1e293b 75%)',
            backgroundSize: '24px 24px',
            backgroundPosition: '0 0, 0 12px, 12px -12px, -12px 0px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: isSpacePressed ? (isPanning ? 'grabbing' : 'grab') : tool === 'brush' ? 'none' : tool === 'pan' ? (isPanning ? 'grabbing' : 'grab') : 'crosshair',
          }}
        >
          <div style={{ position: 'relative', transform: `translate(${panOffset.x}px, ${panOffset.y}px)`, display: 'inline-block', boxShadow: '0 0 30px rgba(0,0,0,0.8), 0 0 0 1px rgba(56, 189, 248, 0.3)' }}>
            <canvas ref={canvasRef} style={{ display: 'block', width: `${imgDimensionsRef.current.w * zoom}px`, height: `${imgDimensionsRef.current.h * zoom}px`, imageRendering: zoom >= 2 ? 'pixelated' : 'auto' }} />
            {rectSelection && (
              <div style={{ position: 'absolute', left: `${Math.min(rectSelection.startX, rectSelection.currX) * zoom}px`, top: `${Math.min(rectSelection.startY, rectSelection.currY) * zoom}px`, width: `${(Math.abs(rectSelection.currX - rectSelection.startX) + 1) * zoom}px`, height: `${(Math.abs(rectSelection.currY - rectSelection.startY) + 1) * zoom}px`, border: '1.5px dashed #38bdf8', background: 'rgba(56, 189, 248, 0.25)', pointerEvents: 'none' }} />
            )}
          </div>

          {/* Brush Indicator */}
          {tool === 'brush' && containerMousePos && (
            <div style={{ position: 'absolute', left: containerMousePos.x, top: containerMousePos.y, width: Math.max(4, brushSize * zoom), height: Math.max(4, brushSize * zoom), borderRadius: '50%', border: '1px solid rgba(56, 189, 248, 0.9)', background: hardness < 100 ? `radial-gradient(circle, rgba(56, 189, 248, 0.3) 0%, rgba(56, 189, 248, 0.15) ${hardness}%, rgba(56, 189, 248, 0.02) 100%)` : 'rgba(56, 189, 248, 0.2)', transform: 'translate(-50%, -50%)', pointerEvents: 'none', zIndex: 50, boxShadow: '0 0 8px rgba(0, 0, 0, 0.6), inset 0 0 6px rgba(56, 189, 248, 0.4)' }}>
              {hardness > 0 && hardness < 100 && (
                <div style={{ position: 'absolute', left: '50%', top: '50%', width: `${hardness}%`, height: `${hardness}%`, borderRadius: '50%', border: '1px dashed rgba(255, 255, 255, 0.4)', transform: 'translate(-50%, -50%)' }} />
              )}
              <div style={{ position: 'absolute', left: '50%', top: '50%', width: 2, height: 2, borderRadius: '50%', background: '#ffffff', transform: 'translate(-50%, -50%)', boxShadow: '0 0 2px #000' }} />
            </div>
          )}

          {/* Bottom Info Bar */}
          <div style={{ position: 'absolute', bottom: 12, left: 14, right: 14, display: 'flex', alignItems: 'center', justifyContent: 'space-between', background: 'rgba(15, 23, 42, 0.88)', padding: '6px 14px', borderRadius: 6, border: '1px solid rgba(255, 255, 255, 0.1)', fontSize: 11, color: '#94a3b8', backdropFilter: 'blur(8px)', pointerEvents: 'none' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <span>Kích thước: <b style={{ color: '#f8fafc' }}>{imgDimensionsRef.current.w} × {imgDimensionsRef.current.h}px</b></span>
              {cursorPos && <span>Vị trí: <b style={{ color: '#38bdf8' }}>X:{cursorPos.imgX}, Y:{cursorPos.imgY}</b></span>}
              <span>Thu phóng: <b style={{ color: '#4ade80' }}>{Math.round(zoom * 100)}%</b></span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, color: '#cbd5e1' }}>
              <span>🖱️ <b>Lăn chuột</b> để Zoom theo vị trí con trỏ</span>
              <span>•</span>
              <span>✋ Giữ <b>Space</b> hoặc <b>Chuột giữa</b> để kéo ảnh</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
