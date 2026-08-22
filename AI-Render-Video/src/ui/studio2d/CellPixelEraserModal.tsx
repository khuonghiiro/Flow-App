import React, { useState, useRef, useEffect, useCallback } from 'react';
import {
  Eraser,
  ZoomIn,
  ZoomOut,
  Sparkles,
  Save,
  X,
  Square,
  Wand2,
  Undo2,
  Redo2,
  Hand,
  Feather,
} from 'lucide-react';

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
  const [opacity, setOpacity] = useState<number>(60); // 5% to 100% (Default 60% for smooth gradual erasing)
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

  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const imgDimensionsRef = useRef<{ w: number; h: number }>({ w: 100, h: 100 });

  // Photoshop Continuous Stroke Path Buffers
  const strokeInitialImageDataRef = useRef<ImageData | null>(null);
  const strokeMaskCanvasRef = useRef<HTMLCanvasElement | null>(null);
  const strokePointsRef = useRef<{ x: number; y: number }[]>([]);

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

  /**
   * Renders the continuous vector stroke path with Gaussian blur feathered edge onto maskCanvas,
   * then composites onto main canvas bounded strictly by opacity (Photoshop Continuous Stroke Algorithm)
   */
  const renderPhotoshopStrokePath = () => {
    const pts = strokePointsRef.current;
    if (pts.length === 0) return;

    const canvas = canvasRef.current;
    if (!canvas || !strokeInitialImageDataRef.current) return;

    if (!strokeMaskCanvasRef.current) {
      strokeMaskCanvasRef.current = document.createElement('canvas');
    }
    const maskCanvas = strokeMaskCanvasRef.current;
    maskCanvas.width = canvas.width;
    maskCanvas.height = canvas.height;
    const maskCtx = maskCanvas.getContext('2d');
    if (!maskCtx) return;

    maskCtx.clearRect(0, 0, maskCanvas.width, maskCanvas.height);

    // Hardness profile: 0% = Maximum smooth Gaussian feather, 100% = 0 blur sharp edge
    const hardRatio = Math.max(0, Math.min(1, hardness / 100));
    const blurPx = (1 - hardRatio) * (brushSize * 0.35);
    const coreWidth = Math.max(1, brushSize - blurPx * 1.6);
    const flowAlpha = Math.max(0.05, Math.min(1, flow / 100));

    maskCtx.save();
    if (blurPx > 0.4) {
      maskCtx.filter = `blur(${blurPx.toFixed(1)}px)`;
    }
    maskCtx.fillStyle = `rgba(0, 0, 0, ${flowAlpha})`;
    maskCtx.strokeStyle = `rgba(0, 0, 0, ${flowAlpha})`;
    maskCtx.lineWidth = coreWidth;
    maskCtx.lineCap = 'round';
    maskCtx.lineJoin = 'round';

    if (pts.length === 1) {
      // Single dot click
      maskCtx.beginPath();
      maskCtx.arc(pts[0].x, pts[0].y, coreWidth / 2, 0, Math.PI * 2);
      maskCtx.fill();
    } else {
      // Continuous smooth stroke path (no overlapping dab alpha blowups!)
      maskCtx.beginPath();
      maskCtx.moveTo(pts[0].x, pts[0].y);
      for (let i = 1; i < pts.length; i++) {
        const prev = pts[i - 1];
        const curr = pts[i];
        const midX = (prev.x + curr.x) / 2;
        const midY = (prev.y + curr.y) / 2;
        maskCtx.quadraticCurveTo(prev.x, prev.y, midX, midY);
      }
      maskCtx.lineTo(pts[pts.length - 1].x, pts[pts.length - 1].y);
      maskCtx.stroke();

      // Ensure rounded ends
      maskCtx.beginPath();
      maskCtx.arc(pts[0].x, pts[0].y, coreWidth / 2, 0, Math.PI * 2);
      maskCtx.arc(pts[pts.length - 1].x, pts[pts.length - 1].y, coreWidth / 2, 0, Math.PI * 2);
      maskCtx.fill();
    }
    maskCtx.restore();

    // Composite single stroke mask into original image data
    const mainCtx = canvas.getContext('2d', { willReadFrequently: true });
    if (!mainCtx) return;

    mainCtx.putImageData(strokeInitialImageDataRef.current, 0, 0);
    mainCtx.save();
    mainCtx.globalAlpha = Math.max(0.01, Math.min(1, opacity / 100));
    mainCtx.globalCompositeOperation = 'destination-out';
    mainCtx.drawImage(maskCanvas, 0, 0);
    mainCtx.restore();
  };

  /**
   * Starts a new Photoshop-Grade Stroke on MouseDown
   */
  const startPhotoshopStroke = (startX: number, startY: number) => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return;

    // Snapshot image before stroke starts
    strokeInitialImageDataRef.current = ctx.getImageData(0, 0, canvas.width, canvas.height);
    strokePointsRef.current = [{ x: startX, y: startY }];
    renderPhotoshopStrokePath();
  };

  /**
   * Continues the stroke as mouse moves
   */
  const continuePhotoshopStroke = (currX: number, currY: number) => {
    const last = strokePointsRef.current[strokePointsRef.current.length - 1];
    if (last && Math.hypot(currX - last.x, currY - last.y) < 1) {
      return; // Skip identical or micro movements
    }
    strokePointsRef.current.push({ x: currX, y: currY });
    renderPhotoshopStrokePath();
  };

  /**
   * Magic Wand Color Erase
   */
  const magicWandErase = (startX: number, startY: number) => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return;

    const w = canvas.width;
    const h = canvas.height;
    const imgData = ctx.getImageData(0, 0, w, h);
    const data = imgData.data;

    const targetIdx = (startY * w + startX) * 4;
    const targetR = data[targetIdx];
    const targetG = data[targetIdx + 1];
    const targetB = data[targetIdx + 2];
    const targetA = data[targetIdx + 3];

    if (targetA === 0) return;

    saveStateToUndo();

    const visited = new Uint8Array(w * h);
    const queue = new Int32Array(w * h);
    let head = 0;
    let tail = 0;

    const startPos = startY * w + startX;
    visited[startPos] = 1;
    queue[tail++] = startPos;

    const tolDist = (magicTolerance / 100) * 441.67;

    while (head < tail) {
      const curr = queue[head++];
      const cx = curr % w;
      const cy = Math.floor(curr / w);
      const pIdx = curr * 4;

      data[pIdx + 3] = 0;

      const neighbors = [
        cx > 0 ? curr - 1 : -1,
        cx < w - 1 ? curr + 1 : -1,
        cy > 0 ? curr - w : -1,
        cy < h - 1 ? curr + w : -1,
      ];

      for (let n = 0; n < 4; n++) {
        const nIdx = neighbors[n];
        if (nIdx >= 0 && !visited[nIdx]) {
          visited[nIdx] = 1;
          const npIdx = nIdx * 4;
          if (data[npIdx + 3] > 0) {
            const dr = data[npIdx] - targetR;
            const dg = data[npIdx + 1] - targetG;
            const db = data[npIdx + 2] - targetB;
            const dist = Math.sqrt(dr * dr + dg * dg + db * db);
            if (dist <= tolDist) {
              queue[tail++] = nIdx;
            }
          }
        }
      }
    }

    ctx.putImageData(imgData, 0, 0);
  };

  /**
   * Auto cleanup isolated speckles/islands in this specific cell
   */
  const handleAutoDespeckleThisCell = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return;

    saveStateToUndo();

    const w = canvas.width;
    const h = canvas.height;
    const imgData = ctx.getImageData(0, 0, w, h);
    const data = imgData.data;
    const totalPixels = w * h;

    const visited = new Uint8Array(totalPixels);
    const compQueue = new Int32Array(totalPixels);
    const allComponents: { pixels: number[]; isWhite: boolean }[] = [];
    let maxSize = 0;

    for (let i = 0; i < totalPixels; i++) {
      if (data[i * 4 + 3] > 10 && !visited[i]) {
        let qHead = 0;
        let qTail = 0;
        compQueue[qTail++] = i;
        visited[i] = 1;

        const currentPixels: number[] = [];
        let whiteCount = 0;

        while (qHead < qTail) {
          const curr = compQueue[qHead++];
          currentPixels.push(curr);

          const pIdx = curr * 4;
          if (data[pIdx] > 190 && data[pIdx + 1] > 190 && data[pIdx + 2] > 190) {
            whiteCount++;
          }

          const cx = curr % w;
          const cy = Math.floor(curr / w);

          const neighbors = [
            cx > 0 ? curr - 1 : -1,
            cx < w - 1 ? curr + 1 : -1,
            cy > 0 ? curr - w : -1,
            cy < h - 1 ? curr + w : -1,
            cx > 0 && cy > 0 ? curr - w - 1 : -1,
            cx < w - 1 && cy > 0 ? curr - w + 1 : -1,
            cx > 0 && cy < h - 1 ? curr + w - 1 : -1,
            cx < w - 1 && cy < h - 1 ? curr + w + 1 : -1,
          ];

          for (let n = 0; n < 8; n++) {
            const nIdx = neighbors[n];
            if (nIdx >= 0 && !visited[nIdx] && data[nIdx * 4 + 3] > 10) {
              visited[nIdx] = 1;
              compQueue[qTail++] = nIdx;
            }
          }
        }

        if (currentPixels.length > maxSize) maxSize = currentPixels.length;
        allComponents.push({
          pixels: currentPixels,
          isWhite: whiteCount / currentPixels.length > 0.5,
        });
      }
    }

    for (const comp of allComponents) {
      if (comp.pixels.length < Math.min(60, maxSize * 0.15) || (comp.isWhite && comp.pixels.length < 120)) {
        for (let p = 0; p < comp.pixels.length; p++) {
          data[comp.pixels[p] * 4 + 3] = 0;
        }
      }
    }

    ctx.putImageData(imgData, 0, 0);
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
    if (e.button === 1 || e.button === 2 || tool === 'pan' || (e as unknown as { spaceKey?: boolean }).spaceKey) {
      e.preventDefault();
      setIsPanning(true);
      setPanStart({ x: e.clientX - panOffset.x, y: e.clientY - panOffset.y });
      return;
    }

    if (e.button !== 0) return;

    const coords = getImageCoords(e);
    if (!coords) return;
    const { imgX, imgY } = coords;

    if (imgX < 0 || imgX >= imgDimensionsRef.current.w || imgY < 0 || imgY >= imgDimensionsRef.current.h) {
      return;
    }

    if (tool === 'brush') {
      saveStateToUndo();
      setIsDrawing(true);
      startPhotoshopStroke(imgX, imgY);
    } else if (tool === 'rect_erase') {
      setIsDrawing(true);
      setRectSelection({ startX: imgX, startY: imgY, currX: imgX, currY: imgY });
    } else if (tool === 'magic_wand') {
      magicWandErase(imgX, imgY);
    }
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (containerRef.current) {
      const cRect = containerRef.current.getBoundingClientRect();
      setContainerMousePos({
        x: e.clientX - cRect.left,
        y: e.clientY - cRect.top,
      });
    }

    if (isPanning) {
      setPanOffset({
        x: e.clientX - panStart.x,
        y: e.clientY - panStart.y,
      });
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
    if (isPanning) {
      setIsPanning(false);
    }

    if (isDrawing) {
      if (tool === 'rect_erase' && rectSelection) {
        const canvas = canvasRef.current;
        if (canvas) {
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
    const delta = e.deltaY < 0 ? 0.5 : -0.5;
    setZoom((prev) => Math.max(1, Math.min(16, Math.round((prev + delta) * 10) / 10)));
  };

  const handleSaveAndApply = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const finalDataUrl = canvas.toDataURL('image/png');
    onSave(finalDataUrl);
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
        {/* Modal Top Header */}
        <div
          style={{
            padding: '12px 18px',
            background: 'rgba(15, 23, 42, 0.95)',
            borderBottom: '1px solid rgba(255, 255, 255, 0.1)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div
              style={{
                width: 32,
                height: 32,
                borderRadius: 8,
                background: 'linear-gradient(135deg, #0284c7, #38bdf8)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#fff',
                boxShadow: '0 0 12px rgba(56, 189, 248, 0.4)',
              }}
            >
              <Eraser size={18} />
            </div>
            <div>
              <h2 style={{ fontSize: 14, fontWeight: 700, color: '#f8fafc', margin: 0 }}>
                HỘP THOẠI TẨY XÓA PIXEL & CỌ MỀM ADOBE PHOTOSHOP
              </h2>
              <p style={{ fontSize: 11, color: '#38bdf8', margin: 0 }}>
                {cellTitle} • Tẩy mềm mịn, giữ nguyên độ mờ Opacity dọc theo toàn bộ vết kéo chuột
              </p>
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <button
              onClick={handleSaveAndApply}
              style={{
                padding: '7px 14px',
                fontSize: 12,
                fontWeight: 700,
                borderRadius: 6,
                background: 'linear-gradient(135deg, #0284c7, #2563eb)',
                color: '#fff',
                border: '1px solid #38bdf8',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                boxShadow: '0 0 15px rgba(56, 189, 248, 0.4)',
              }}
            >
              <Save size={14} /> LƯU & CẬP NHẬT VÀO 3D
            </button>
            <button
              onClick={onClose}
              style={{
                padding: '6px',
                borderRadius: 6,
                background: 'rgba(255, 255, 255, 0.05)',
                color: '#94a3b8',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                cursor: 'pointer',
              }}
              title="Đóng hộp thoại"
            >
              <X size={16} />
            </button>
          </div>
        </div>

        {/* Modal Toolbar - Photoshop Standard Controls */}
        <div
          style={{
            padding: '8px 16px',
            background: '#080d1a',
            borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            gap: 10,
            flexWrap: 'wrap',
          }}
        >
          {/* Tool Selectors */}
          <div style={{ display: 'flex', background: 'rgba(0,0,0,0.4)', padding: 3, borderRadius: 6, border: '1px solid rgba(255,255,255,0.08)', gap: 2 }}>
            <button
              onClick={() => setTool('brush')}
              style={{
                padding: '5px 10px',
                fontSize: 11,
                fontWeight: 600,
                borderRadius: 4,
                border: 'none',
                background: tool === 'brush' ? '#0284c7' : 'transparent',
                color: tool === 'brush' ? '#fff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 5,
              }}
              title="Cọ Tẩy Photoshop (Bấm giữ và quét để xóa mờ mịn)"
            >
              <Eraser size={13} /> 🖌️ Cọ Tẩy (E)
            </button>

            <button
              onClick={() => setTool('rect_erase')}
              style={{
                padding: '5px 10px',
                fontSize: 11,
                fontWeight: 600,
                borderRadius: 4,
                border: 'none',
                background: tool === 'rect_erase' ? '#0284c7' : 'transparent',
                color: tool === 'rect_erase' ? '#fff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 5,
              }}
              title="Xóa Theo Vùng Chữ Nhật"
            >
              <Square size={13} /> 🔲 Vùng Chọn Xóa
            </button>

            <button
              onClick={() => setTool('magic_wand')}
              style={{
                padding: '5px 10px',
                fontSize: 11,
                fontWeight: 600,
                borderRadius: 4,
                border: 'none',
                background: tool === 'magic_wand' ? '#0284c7' : 'transparent',
                color: tool === 'magic_wand' ? '#fff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 5,
              }}
              title="Đũa Thần Tẩy Màu (Wand)"
            >
              <Wand2 size={13} /> 🪄 Tẩy Cụm Màu
            </button>

            <button
              onClick={() => setTool('pan')}
              style={{
                padding: '5px 10px',
                fontSize: 11,
                fontWeight: 600,
                borderRadius: 4,
                border: 'none',
                background: tool === 'pan' ? '#0284c7' : 'transparent',
                color: tool === 'pan' ? '#fff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 5,
              }}
              title="Di Chuyển Khung (Pan)"
            >
              <Hand size={13} /> ✋ Kéo Canvas
            </button>
          </div>

          {/* Quick Brush Preset Buttons */}
          {tool === 'brush' && (
            <div style={{ display: 'flex', background: 'rgba(0,0,0,0.4)', padding: 3, borderRadius: 6, border: '1px solid rgba(255,255,255,0.08)', gap: 2 }}>
              <button
                onClick={() => {
                  setHardness(0);
                  setOpacity(50);
                  setFlow(80);
                }}
                style={{
                  padding: '5px 8px',
                  fontSize: 10,
                  fontWeight: hardness === 0 ? 700 : 400,
                  borderRadius: 4,
                  border: 'none',
                  background: hardness === 0 ? '#0284c7' : 'transparent',
                  color: hardness === 0 ? '#fff' : '#94a3b8',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 3,
                }}
                title="Cọ Mềm Photoshop (Soft Round 0% Hardness): Xóa mịn màng viền tóc"
              >
                <Feather size={12} /> 🪶 Cọ Mềm (Soft 0%)
              </button>

              <button
                onClick={() => {
                  setHardness(50);
                  setOpacity(80);
                  setFlow(90);
                }}
                style={{
                  padding: '5px 8px',
                  fontSize: 10,
                  fontWeight: hardness === 50 ? 700 : 400,
                  borderRadius: 4,
                  border: 'none',
                  background: hardness === 50 ? '#0284c7' : 'transparent',
                  color: hardness === 50 ? '#fff' : '#94a3b8',
                  cursor: 'pointer',
                }}
                title="Cọ Vừa (Medium 50% Hardness)"
              >
                🌓 Cọ Vừa (50%)
              </button>

              <button
                onClick={() => {
                  setHardness(100);
                  setOpacity(100);
                  setFlow(100);
                }}
                style={{
                  padding: '5px 8px',
                  fontSize: 10,
                  fontWeight: hardness === 100 ? 700 : 400,
                  borderRadius: 4,
                  border: 'none',
                  background: hardness === 100 ? '#0284c7' : 'transparent',
                  color: hardness === 100 ? '#fff' : '#94a3b8',
                  cursor: 'pointer',
                }}
                title="Cọ Sắc Nét (Hard Round 100% Hardness)"
              >
                ⚡ Cọ Cứng (100%)
              </button>
            </div>
          )}

          {/* Adobe Photoshop Precision Sliders */}
          {tool === 'brush' && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, background: 'rgba(255,255,255,0.03)', padding: '3px 8px', borderRadius: 6, flexWrap: 'wrap' }}>
              {/* Size */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                <span style={{ fontSize: 10.5, color: '#94a3b8' }}>Size: <b>{brushSize}px</b></span>
                <input
                  type="range"
                  min="1"
                  max="100"
                  value={brushSize}
                  onChange={(e) => setBrushSize(parseInt(e.target.value))}
                  style={{ width: 60, cursor: 'pointer' }}
                />
              </div>

              {/* Hardness */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, borderLeft: '1px solid rgba(255,255,255,0.1)', paddingLeft: 6 }}>
                <span style={{ fontSize: 10.5, color: '#38bdf8' }}>Hardness: <b>{hardness}%</b></span>
                <input
                  type="range"
                  min="0"
                  max="100"
                  value={hardness}
                  onChange={(e) => setHardness(parseInt(e.target.value))}
                  style={{ width: 55, cursor: 'pointer' }}
                  title="Độ cứng cọ (0% = Siêu mềm mịn Gaussian, 100% = Sắc nét)"
                />
              </div>

              {/* Opacity */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, borderLeft: '1px solid rgba(255,255,255,0.1)', paddingLeft: 6 }}>
                <span style={{ fontSize: 10.5, color: '#4ade80' }}>Opacity: <b>{opacity}%</b></span>
                <input
                  type="range"
                  min="5"
                  max="100"
                  value={opacity}
                  onChange={(e) => setOpacity(parseInt(e.target.value))}
                  style={{ width: 55, cursor: 'pointer' }}
                  title="Độ mờ tối đa trong 1 lần quét chuột (Ví dụ 50% sẽ không bao giờ bị xóa trắng khi kéo)"
                />
              </div>

              {/* Flow */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, borderLeft: '1px solid rgba(255,255,255,0.1)', paddingLeft: 6 }}>
                <span style={{ fontSize: 10.5, color: '#94a3b8' }}>Flow: <b>{flow}%</b></span>
                <input
                  type="range"
                  min="5"
                  max="100"
                  value={flow}
                  onChange={(e) => setFlow(parseInt(e.target.value))}
                  style={{ width: 50, cursor: 'pointer' }}
                  title="Tốc độ ra lực xóa (Flow rate)"
                />
              </div>
            </div>
          )}

          {/* Magic Wand Tolerance */}
          {tool === 'magic_wand' && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, background: 'rgba(255,255,255,0.03)', padding: '3px 8px', borderRadius: 6 }}>
              <span style={{ fontSize: 11, color: '#94a3b8' }}>Độ nhạy màu: <b>{magicTolerance}%</b></span>
              <input
                type="range"
                min="5"
                max="80"
                value={magicTolerance}
                onChange={(e) => setMagicTolerance(parseInt(e.target.value))}
                style={{ width: 80, cursor: 'pointer' }}
              />
            </div>
          )}

          {/* Zoom Controls */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 5, background: 'rgba(255,255,255,0.03)', padding: '3px 8px', borderRadius: 6 }}>
            <button
              onClick={() => setZoom((prev) => Math.max(1, prev - 1))}
              style={{ padding: '3px 6px', borderRadius: 4, background: 'rgba(255,255,255,0.08)', color: '#fff', border: 'none', cursor: 'pointer' }}
              title="Thu nhỏ"
            >
              <ZoomOut size={13} />
            </button>
            <span style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', minWidth: 38, textAlign: 'center' }}>
              {Math.round(zoom * 100)}%
            </span>
            <button
              onClick={() => setZoom((prev) => Math.min(16, prev + 1))}
              style={{ padding: '3px 6px', borderRadius: 4, background: 'rgba(255,255,255,0.08)', color: '#fff', border: 'none', cursor: 'pointer' }}
              title="Phóng to"
            >
              <ZoomIn size={13} />
            </button>
            <button
              onClick={() => {
                setZoom(3);
                setPanOffset({ x: 0, y: 0 });
              }}
              style={{ padding: '2px 5px', fontSize: 10, borderRadius: 4, background: 'rgba(255,255,255,0.06)', color: '#94a3b8', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
            >
              Fit
            </button>
          </div>

          {/* Undo / Redo & One-Click Despeckle */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, marginLeft: 'auto' }}>
            <button
              onClick={handleUndo}
              disabled={undoStack.length <= 1}
              style={{
                padding: '4px 7px',
                fontSize: 10.5,
                borderRadius: 4,
                background: undoStack.length > 1 ? 'rgba(255,255,255,0.1)' : 'rgba(255,255,255,0.02)',
                color: undoStack.length > 1 ? '#f8fafc' : '#475569',
                border: 'none',
                cursor: undoStack.length > 1 ? 'pointer' : 'not-allowed',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
              }}
              title="Hoàn tác (Ctrl+Z)"
            >
              <Undo2 size={12} /> Undo
            </button>

            <button
              onClick={handleRedo}
              disabled={redoStack.length === 0}
              style={{
                padding: '4px 7px',
                fontSize: 10.5,
                borderRadius: 4,
                background: redoStack.length > 0 ? 'rgba(255,255,255,0.1)' : 'rgba(255,255,255,0.02)',
                color: redoStack.length > 0 ? '#f8fafc' : '#475569',
                border: 'none',
                cursor: redoStack.length > 0 ? 'pointer' : 'not-allowed',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
              }}
              title="Làm lại (Ctrl+Y)"
            >
              <Redo2 size={12} /> Redo
            </button>

            <button
              onClick={handleAutoDespeckleThisCell}
              style={{
                padding: '4px 9px',
                fontSize: 10.5,
                fontWeight: 600,
                borderRadius: 4,
                background: 'rgba(56, 189, 248, 0.15)',
                color: '#38bdf8',
                border: '1px solid rgba(56, 189, 248, 0.3)',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
              }}
              title="Tự động quét và xóa sạch các hạt đốm trắng li ti trong ô này"
            >
              <Sparkles size={12} /> 🧹 Quét Đốm Trắng
            </button>
          </div>
        </div>

        {/* Main Canvas Workspace with Transparency Checkerboard */}
        <div
          ref={containerRef}
          onMouseDown={handleMouseDown}
          onMouseMove={handleMouseMove}
          onMouseUp={handleMouseUp}
          onMouseLeave={() => {
            handleMouseUp();
            setContainerMousePos(null);
          }}
          onMouseEnter={(e) => {
            if (containerRef.current) {
              const cRect = containerRef.current.getBoundingClientRect();
              setContainerMousePos({
                x: e.clientX - cRect.left,
                y: e.clientY - cRect.top,
              });
            }
          }}
          onWheel={handleWheel}
          style={{
            flex: 1,
            position: 'relative',
            overflow: 'hidden',
            background: `
              linear-gradient(45deg, #1e293b 25%, transparent 25%), 
              linear-gradient(-45deg, #1e293b 25%, transparent 25%), 
              linear-gradient(45deg, transparent 75%, #1e293b 75%), 
              linear-gradient(-45deg, transparent 75%, #1e293b 75%)
            `,
            backgroundSize: '24px 24px',
            backgroundPosition: '0 0, 0 12px, 12px -12px, -12px 0px',
            backgroundColor: '#0f172a',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: tool === 'brush' ? 'none' : (tool === 'pan' ? (isPanning ? 'grabbing' : 'grab') : 'crosshair'),
          }}
        >
          {/* Zoomed & Panned Canvas Viewport */}
          <div
            style={{
              position: 'relative',
              transform: `translate(${panOffset.x}px, ${panOffset.y}px)`,
              display: 'inline-block',
              boxShadow: '0 0 30px rgba(0,0,0,0.8), 0 0 0 1px rgba(56, 189, 248, 0.3)',
            }}
          >
            <canvas
              ref={canvasRef}
              style={{
                display: 'block',
                width: `${imgDimensionsRef.current.w * zoom}px`,
                height: `${imgDimensionsRef.current.h * zoom}px`,
                imageRendering: zoom >= 2 ? 'pixelated' : 'auto',
              }}
            />

            {/* Selection Marquee Overlay */}
            {rectSelection && (
              <div
                style={{
                  position: 'absolute',
                  left: `${Math.min(rectSelection.startX, rectSelection.currX) * zoom}px`,
                  top: `${Math.min(rectSelection.startY, rectSelection.currY) * zoom}px`,
                  width: `${(Math.abs(rectSelection.currX - rectSelection.startX) + 1) * zoom}px`,
                  height: `${(Math.abs(rectSelection.currY - rectSelection.startY) + 1) * zoom}px`,
                  border: '1.5px dashed #38bdf8',
                  background: 'rgba(56, 189, 248, 0.25)',
                  pointerEvents: 'none',
                }}
              />
            )}
          </div>

          {/* Photoshop Dynamic Floating Brush Circle Cursor Indicator */}
          {tool === 'brush' && containerMousePos && (
            <div
              style={{
                position: 'absolute',
                left: containerMousePos.x,
                top: containerMousePos.y,
                width: Math.max(4, brushSize * zoom),
                height: Math.max(4, brushSize * zoom),
                borderRadius: '50%',
                border: '1px solid rgba(56, 189, 248, 0.9)',
                background:
                  hardness < 100
                    ? `radial-gradient(circle, rgba(56, 189, 248, 0.3) 0%, rgba(56, 189, 248, 0.15) ${hardness}%, rgba(56, 189, 248, 0.02) 100%)`
                    : 'rgba(56, 189, 248, 0.2)',
                transform: 'translate(-50%, -50%)',
                pointerEvents: 'none',
                zIndex: 50,
                boxShadow: '0 0 8px rgba(0, 0, 0, 0.6), inset 0 0 6px rgba(56, 189, 248, 0.4)',
              }}
            >
              {/* Inner Hardness Ring when hardness < 100% */}
              {hardness > 0 && hardness < 100 && (
                <div
                  style={{
                    position: 'absolute',
                    left: '50%',
                    top: '50%',
                    width: `${hardness}%`,
                    height: `${hardness}%`,
                    borderRadius: '50%',
                    border: '1px dashed rgba(255, 255, 255, 0.4)',
                    transform: 'translate(-50%, -50%)',
                  }}
                />
              )}

              {/* Center Dot */}
              <div
                style={{
                  position: 'absolute',
                  left: '50%',
                  top: '50%',
                  width: 2,
                  height: 2,
                  borderRadius: '50%',
                  background: '#ffffff',
                  transform: 'translate(-50%, -50%)',
                  boxShadow: '0 0 2px #000',
                }}
              />
            </div>
          )}

          {/* Bottom Info Bar */}
          <div
            style={{
              position: 'absolute',
              bottom: 12,
              left: 14,
              display: 'flex',
              alignItems: 'center',
              gap: 12,
              background: 'rgba(15, 23, 42, 0.85)',
              padding: '6px 12px',
              borderRadius: 6,
              border: '1px solid rgba(255, 255, 255, 0.1)',
              fontSize: 11,
              color: '#94a3b8',
              backdropFilter: 'blur(8px)',
              pointerEvents: 'none',
            }}
          >
            <span>Kích thước: <b>{imgDimensionsRef.current.w} × {imgDimensionsRef.current.h}px</b></span>
            {cursorPos && (
              <span>Vị trí: <b>X:{cursorPos.imgX}, Y:{cursorPos.imgY}</b></span>
            )}
            <span>💡 <i>Kéo chuột xóa mờ mượt mà theo Opacity {opacity}% • Nhả chuột quét lại để tăng độ đậm</i></span>
          </div>
        </div>
      </div>
    </div>
  );
};
