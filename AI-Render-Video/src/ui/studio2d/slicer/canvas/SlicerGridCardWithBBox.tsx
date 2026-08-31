// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Check } from 'lucide-react';
import { SlicerUploadedImageItem } from '../hooks/useSlicerMultiImageGallery';
import { PaddedCropRect, detectImageBBoxRect } from '../../../../core/utils/PixelBoundingBoxAlgorithms';
import { ChromaProcessOptions } from '../../../../core/utils/ChromaDespeckleProcessor';
import { GridCategoryDefinition } from '../../../../core/assets/GridSliceRegistry';
import { eraseBrushStrokeOnCanvas, eraseBoxSelectionOnCanvas } from '../utils/slicerPixelEraserHelper';
import { loadSafeImage } from '../utils/slicerImageLoaderHelper';

interface SlicerGridCardWithBBoxProps {
  item: SlicerUploadedImageItem;
  isActive: boolean;
  isSingleCard?: boolean;
  isEyedropperActive: boolean;
  isDirectBBoxCropActive: boolean;
  directBBoxPadding: number;
  chromaOptions?: ChromaProcessOptions;
  checkerTheme: 'dark' | 'light';
  previewDisplayMode: 'transparent' | 'original';
  currentCategory?: GridCategoryDefinition;
  colDividers?: number[];
  rowDividers?: number[];
  setColDividers?: React.Dispatch<React.SetStateAction<number[]>>;
  setRowDividers?: React.Dispatch<React.SetStateAction<number[]>>;
  onUpdateItemDividers?: (itemId: string, newColDividers?: number[], newRowDividers?: number[]) => void;
  paddingInset?: number;
  eraserMode?: 'off' | 'brush' | 'box';
  eraserBrushSize?: number;
  onUpdateItemImage?: (id: string, newDataUrl: string) => void;
  onClick: (e: React.MouseEvent<HTMLDivElement>, item: SlicerUploadedImageItem) => void;
  onMouseMove: (e: React.MouseEvent<HTMLDivElement>) => void;
  onToggleCheckedItem?: (id: string) => void;
  onPickColor?: (hex: string) => void;
  onHoverColor?: (color: { hex: string; r: number; g: number; b: number; x: number; y: number } | null) => void;
}

export const SlicerGridCardWithBBox: React.FC<SlicerGridCardWithBBoxProps> = ({
  item,
  isActive,
  isSingleCard = false,
  isEyedropperActive,
  isDirectBBoxCropActive,
  directBBoxPadding,
  chromaOptions,
  checkerTheme,
  previewDisplayMode,
  currentCategory,
  colDividers,
  rowDividers,
  setColDividers,
  setRowDividers,
  onUpdateItemDividers,
  paddingInset = 0,
  eraserMode = 'off',
  eraserBrushSize = 20,
  onUpdateItemImage,
  onClick,
  onMouseMove,
  onToggleCheckedItem,
  onPickColor,
  onHoverColor,
}) => {
  const cardContainerRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [naturalSize, setNaturalSize] = useState<{ width: number; height: number }>({
    width: item.width || 800,
    height: item.height || 600,
  });
  const [bboxRect, setBboxRect] = useState<PaddedCropRect | null>(null);

  interface EraserBoxPoint {
    screenX: number;
    screenY: number;
    canvasX: number;
    canvasY: number;
  }

  // Interactive Grid Divider states on each card
  const [hoveredDivider, setHoveredDivider] = useState<{ type: 'col' | 'row'; index: number } | null>(null);
  const draggingDividerRef = useRef<{ type: 'col' | 'row'; index: number } | null>(null);

  // Local card Zoom & Pan states (Ctrl + Wheel để zoom, Space + Drag để pan khi đã zoom)
  const [cardZoom, setCardZoom] = useState<number>(1.0);
  const [cardPanOffset, setCardPanOffset] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [isCardPanning, setIsCardPanning] = useState<boolean>(false);
  const [cardPanStart, setCardPanStart] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [isSpacePressed, setIsSpacePressed] = useState<boolean>(false);

  // Space key listener for panning
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.code === 'Space' && !e.repeat && document.activeElement?.tagName !== 'INPUT' && document.activeElement?.tagName !== 'TEXTAREA') {
        setIsSpacePressed(true);
      }
    };
    const handleKeyUp = (e: KeyboardEvent) => {
      if (e.code === 'Space') {
        setIsSpacePressed(false);
        setIsCardPanning(false);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
    };
  }, []);

  // Native Non-Passive Wheel listener to completely PREVENT browser page zoom when scrolling / zooming cards
  useEffect(() => {
    const el = cardContainerRef.current;
    if (!el) return;

    const onNativeWheel = (e: WheelEvent) => {
      // Khi chỉ có 1 ảnh trong khung (isSingleCard) hoặc khi nhấn giữ Ctrl/Cmd:
      if (isSingleCard || e.ctrlKey || e.metaKey) {
        e.preventDefault();
        e.stopPropagation();
        const zoomFactor = 1.15;
        if (e.deltaY < 0) {
          setCardZoom((prev) => Math.min(8.0, Math.round(prev * zoomFactor * 100) / 100));
        } else {
          setCardZoom((prev) => {
            const next = Math.max(1.0, Math.round((prev / zoomFactor) * 100) / 100);
            if (next <= 1.0) {
              setCardPanOffset({ x: 0, y: 0 });
            }
            return next;
          });
        }
      }
    };

    el.addEventListener('wheel', onNativeWheel, { passive: false });
    return () => {
      el.removeEventListener('wheel', onNativeWheel);
    };
  }, [isSingleCard]);

  const handleCardPointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
    if (cardZoom > 1.02 && (isSpacePressed || e.button === 1 || e.altKey)) {
      e.preventDefault();
      e.stopPropagation();
      setIsCardPanning(true);
      setCardPanStart({ x: e.clientX - cardPanOffset.x, y: e.clientY - cardPanOffset.y });
      try {
        (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
      } catch {}
    }
  };

  const handleCardPointerMove = (e: React.PointerEvent<HTMLDivElement>) => {
    if (isCardPanning) {
      e.preventDefault();
      e.stopPropagation();
      setCardPanOffset({ x: e.clientX - cardPanStart.x, y: e.clientY - cardPanStart.y });
    }
  };

  const handleCardPointerUp = (e: React.PointerEvent<HTMLDivElement>) => {
    if (isCardPanning) {
      setIsCardPanning(false);
      try {
        (e.currentTarget as HTMLElement).releasePointerCapture(e.pointerId);
      } catch {}
    }
  };

  // Pixel Eraser interactive states on each card
  const [isErasing, setIsErasing] = useState<boolean>(false);
  const [lastPoint, setLastPoint] = useState<{ x: number; y: number } | null>(null);
  const [boxStart, setBoxStart] = useState<EraserBoxPoint | null>(null);
  const [boxCurrent, setBoxCurrent] = useState<EraserBoxPoint | null>(null);
  const [hoverCursor, setHoverCursor] = useState<{ x: number; y: number; size: number; visible: boolean }>({
    x: 0,
    y: 0,
    size: 20,
    visible: false,
  });

  const isSep = Boolean(item.isTransparentSeparated);
  const sourceUrl =
    previewDisplayMode === 'transparent' && item.transparentUrl
      ? item.transparentUrl
      : item.originalUrl || item.url;

  const cardAspectRatio = naturalSize.height > 0 ? naturalSize.width / naturalSize.height : 1;

  // Use per-item custom dividers if present and matching current matrix dimensions, else fallback to global dividers
  const effectiveColDividers =
    item.customColDividers && item.customColDividers.length === (currentCategory?.cols || 1) + 1
      ? item.customColDividers
      : colDividers && colDividers.length === (currentCategory?.cols || 1) + 1
        ? colDividers
        : null;

  const effectiveRowDividers =
    item.customRowDividers && item.customRowDividers.length === (currentCategory?.rows || 1) + 1
      ? item.customRowDividers
      : rowDividers && rowDividers.length === (currentCategory?.rows || 1) + 1
        ? rowDividers
        : null;

  const getColX = useCallback((c: number, w: number, numCols: number, baseW: number) => {
    if (effectiveColDividers && effectiveColDividers.length > c && baseW > 0) {
      return Math.round((effectiveColDividers[c] / baseW) * w);
    }
    return Math.round((c * w) / numCols);
  }, [effectiveColDividers]);

  const getRowY = useCallback((r: number, h: number, numRows: number, baseH: number) => {
    if (effectiveRowDividers && effectiveRowDividers.length > r && baseH > 0) {
      return Math.round((effectiveRowDividers[r] / baseH) * h);
    }
    return Math.round((r * h) / numRows);
  }, [effectiveRowDividers]);

  // Redraw canvas on prop changes
  useEffect(() => {
    let isMounted = true;
    const canvas = canvasRef.current;
    if (!canvas || !sourceUrl) return;

    loadSafeImage(sourceUrl)
      .then((img) => {
        if (!isMounted) return;
        const w = img.naturalWidth || img.width;
        const h = img.naturalHeight || img.height;
        setNaturalSize({ width: w, height: h });

        canvas.width = w;
        canvas.height = h;

        const ctx = canvas.getContext('2d', { willReadFrequently: true });
        if (!ctx) return;

        // Draw the image
        ctx.clearRect(0, 0, w, h);
        ctx.drawImage(img, 0, 0);

        // Draw custom/proportional grid dividers and padding inset guides ONLY on un-sliced sprite sheets
        if (!item.isFrameItem && !isSep && currentCategory && currentCategory.id !== 'single_full_image' && (currentCategory.cols > 1 || currentCategory.rows > 1)) {
        const numCols = Math.max(1, currentCategory.cols || 4);
        const numRows = Math.max(1, currentCategory.rows || 1);
        const pad = Math.max(0, paddingInset);
        const cardLw = Math.max(3.5, Math.round(w / 200));
        const dashLen = Math.max(10, Math.round(w / 50));

        const baseW = effectiveColDividers && effectiveColDividers.length > numCols ? effectiveColDividers[effectiveColDividers.length - 1] : w;
        const baseH = effectiveRowDividers && effectiveRowDividers.length > numRows ? effectiveRowDividers[effectiveRowDividers.length - 1] : h;

        // Draw contrast background stroke
        ctx.lineWidth = cardLw + 2;
        ctx.strokeStyle = '#000000';
        ctx.setLineDash([]);
        for (let c = 1; c < numCols; c++) {
          const x = getColX(c, w, numCols, baseW);
          ctx.beginPath();
          ctx.moveTo(x, 0);
          ctx.lineTo(x, h);
          ctx.stroke();
        }
        for (let r = 1; r < numRows; r++) {
          const y = getRowY(r, h, numRows, baseH);
          ctx.beginPath();
          ctx.moveTo(0, y);
          ctx.lineTo(w, y);
          ctx.stroke();
        }

        // Draw foreground bright cyan dash (or highlight if hovered)
        ctx.lineWidth = cardLw;
        ctx.strokeStyle = '#38bdf8';
        ctx.setLineDash([dashLen, dashLen]);

        for (let c = 1; c < numCols; c++) {
          const x = getColX(c, w, numCols, baseW);
          const isHovered = hoveredDivider?.type === 'col' && hoveredDivider?.index === c;
          if (isHovered) {
            ctx.save();
            ctx.lineWidth = cardLw + 3;
            ctx.strokeStyle = '#fbbf24';
            ctx.setLineDash([]);
            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, h);
            ctx.stroke();
            ctx.restore();
          } else {
            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, h);
            ctx.stroke();
          }
        }
        for (let r = 1; r < numRows; r++) {
          const y = getRowY(r, h, numRows, baseH);
          const isHovered = hoveredDivider?.type === 'row' && hoveredDivider?.index === r;
          if (isHovered) {
            ctx.save();
            ctx.lineWidth = cardLw + 3;
            ctx.strokeStyle = '#fbbf24';
            ctx.setLineDash([]);
            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(w, y);
            ctx.stroke();
            ctx.restore();
          } else {
            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(w, y);
            ctx.stroke();
          }
        }

        // Draw padding inset green guides inside each cell
        if (pad > 0) {
          const padLw = Math.max(2.5, Math.round(w / 250));
          ctx.strokeStyle = '#22c55e';
          ctx.lineWidth = padLw;
          ctx.setLineDash([Math.max(6, Math.round(dashLen / 2)), Math.max(6, Math.round(dashLen / 2))]);
          for (let r = 0; r < numRows; r++) {
            for (let c = 0; c < numCols; c++) {
              const rawX0 = getColX(c, w, numCols, baseW);
              const rawX1 = getColX(c + 1, w, numCols, baseW);
              const rawY0 = getRowY(r, h, numRows, baseH);
              const rawY1 = getRowY(r + 1, h, numRows, baseH);
              const rawW = rawX1 - rawX0;
              const rawH = rawY1 - rawY0;
              const safePadX = Math.min(pad, Math.floor(rawW / 2.5));
              const safePadY = Math.min(pad, Math.floor(rawH / 2.5));
              ctx.strokeRect(rawX0 + safePadX, rawY0 + safePadY, Math.max(4, rawW - safePadX * 2), Math.max(4, rawH - safePadY * 2));
            }
          }
        }
        ctx.setLineDash([]);
      }

      // If BBox is active, calculate and draw BBox overlay with alternating black/white dashed border
      if (isDirectBBoxCropActive) {
        const rect = detectImageBBoxRect(img, chromaOptions, directBBoxPadding, 20);
        if (isMounted) setBboxRect(rect);

        if (rect) {
          ctx.fillStyle = 'rgba(0, 0, 0, 0.45)';
          ctx.fillRect(0, 0, w, rect.top);
          ctx.fillRect(0, rect.bottom + 1, w, h - (rect.bottom + 1));
          ctx.fillRect(0, rect.top, rect.left, rect.height);
          ctx.fillRect(rect.right + 1, rect.top, w - (rect.right + 1), rect.height);

          const lw = Math.max(3.5, Math.round(w / 180));
          const bDash = Math.max(10, Math.round(w / 50));
          ctx.lineWidth = lw;
          ctx.strokeStyle = '#000000';
          ctx.setLineDash([bDash, bDash]);
          ctx.strokeRect(rect.left, rect.top, rect.width, rect.height);

          ctx.strokeStyle = '#ffffff';
          ctx.setLineDash([bDash, bDash]);
          ctx.lineDashOffset = bDash;
          ctx.strokeRect(rect.left, rect.top, rect.width, rect.height);
          ctx.setLineDash([]);
        }
        } else {
          if (isMounted) setBboxRect(null);
        }
      })
      .catch((err) => {
        console.warn('Failed to load card image:', err);
      });

    return () => {
      isMounted = false;
    };
  }, [
    sourceUrl,
    isDirectBBoxCropActive,
    directBBoxPadding,
    chromaOptions,
    isSep,
    currentCategory,
    effectiveColDividers,
    effectiveRowDividers,
    paddingInset,
    hoveredDivider,
    getColX,
    getRowY,
  ]);

  const getCanvasCoords = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas) return { x: 0, y: 0 };
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / (rect.width || 1);
    const scaleY = canvas.height / (rect.height || 1);
    return {
      x: (e.clientX - rect.left) * scaleX,
      y: (e.clientY - rect.top) * scaleY,
    };
  };

  const handlePointerDown = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (cardZoom > 1.02 && (isSpacePressed || e.button === 1 || e.altKey)) {
      handleCardPointerDown(e as unknown as React.PointerEvent<HTMLDivElement>);
      return;
    }

    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / (rect.width || 1);
    const scaleY = canvas.height / (rect.height || 1);
    const screenX = e.clientX - rect.left;
    const screenY = e.clientY - rect.top;
    const pt = {
      x: screenX * scaleX,
      y: screenY * scaleY,
    };

    // 0. Eyedropper Sampling Handler
    if (isEyedropperActive) {
      e.stopPropagation();
      if (onPickColor) {
        const normX = Math.max(0, Math.min(1, screenX / (rect.width || 1)));
        const normY = Math.max(0, Math.min(1, screenY / (rect.height || 1)));
        const ctx = canvas.getContext('2d', { willReadFrequently: true });
        if (ctx) {
          const pxX = Math.min(canvas.width - 1, Math.max(0, Math.floor(normX * canvas.width)));
          const pxY = Math.min(canvas.height - 1, Math.max(0, Math.floor(normY * canvas.height)));
          const pixel = ctx.getImageData(pxX, pxY, 1, 1).data;
          const hex = `#${((1 << 24) + (pixel[0] << 16) + (pixel[1] << 8) + pixel[2]).toString(16).slice(1).toUpperCase()}`;
          onPickColor(hex);
        }
      }
      return;
    }

    // 1. Divider Dragging Handler
    if (eraserMode === 'off' && hoveredDivider && !isDirectBBoxCropActive) {
      e.stopPropagation();
      try {
        (e.target as HTMLElement).setPointerCapture(e.pointerId);
      } catch { }
      draggingDividerRef.current = hoveredDivider;
      return;
    }

    if (eraserMode === 'off') return;
    e.stopPropagation();
    try {
      (e.target as HTMLElement).setPointerCapture(e.pointerId);
    } catch { }

    setIsErasing(true);
    setLastPoint(pt);

    if (eraserMode === 'brush') {
      const ctx = canvas.getContext('2d');
      if (ctx) {
        const canvasRadius = (eraserBrushSize / 2) * scaleX;
        eraseBrushStrokeOnCanvas(ctx, pt.x, pt.y, pt.x, pt.y, canvasRadius);
      }
    } else if (eraserMode === 'box') {
      const boxPt: EraserBoxPoint = {
        screenX,
        screenY,
        canvasX: pt.x,
        canvasY: pt.y,
      };
      setBoxStart(boxPt);
      setBoxCurrent(boxPt);
    }
  };

  const handlePointerMove = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (isCardPanning) {
      handleCardPointerMove(e as unknown as React.PointerEvent<HTMLDivElement>);
      return;
    }
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / (rect.width || 1);
    const scaleY = canvas.height / (rect.height || 1);
    const screenX = e.clientX - rect.left;
    const screenY = e.clientY - rect.top;
    const pt = {
      x: screenX * scaleX,
      y: screenY * scaleY,
    };

    // 1. Handle Divider Dragging & Hover
    if (eraserMode === 'off' && !isDirectBBoxCropActive && !isSep && currentCategory && currentCategory.cols > 1) {
      const w = canvas.width;
      const h = canvas.height;
      const numCols = Math.max(1, currentCategory.cols || 4);
      const numRows = Math.max(1, currentCategory.rows || 1);

      const getInitialCols = (nCols: number, bW: number) => {
        if (effectiveColDividers && effectiveColDividers.length === nCols + 1 && !effectiveColDividers.some(isNaN)) {
          return [...effectiveColDividers];
        }
        const res: number[] = [];
        for (let i = 0; i <= nCols; i++) {
          res.push(Math.round((i * bW) / nCols));
        }
        return res;
      };

      const getInitialRows = (nRows: number, bH: number) => {
        if (effectiveRowDividers && effectiveRowDividers.length === nRows + 1 && !effectiveRowDividers.some(isNaN)) {
          return [...effectiveRowDividers];
        }
        const res: number[] = [];
        for (let i = 0; i <= nRows; i++) {
          res.push(Math.round((i * bH) / nRows));
        }
        return res;
      };

      const currentCols = getInitialCols(numCols, w);
      const currentRows = getInitialRows(numRows, h);
      const baseW = currentCols[currentCols.length - 1] || w;
      const baseH = currentRows[currentRows.length - 1] || h;

      if (draggingDividerRef.current) {
        if (draggingDividerRef.current.type === 'col') {
          const idx = draggingDividerRef.current.index;
          const newNormalized = Math.max(0, Math.min(1, pt.x / w));
          const newColVal = Math.round(newNormalized * baseW);
          const minVal = (currentCols[idx - 1] ?? Math.round(((idx - 1) * baseW) / numCols)) + 10;
          const maxVal = (currentCols[idx + 1] ?? Math.round(((idx + 1) * baseW) / numCols)) - 10;
          const clamped = Math.max(minVal, Math.min(maxVal, newColVal));
          const nextCols = [...currentCols];
          nextCols[idx] = clamped;

          if (onUpdateItemDividers) {
            onUpdateItemDividers(item.id, nextCols, currentRows);
          } else if (setColDividers) {
            setColDividers(nextCols);
          }
        } else if (draggingDividerRef.current.type === 'row') {
          const idx = draggingDividerRef.current.index;
          const newNormalized = Math.max(0, Math.min(1, pt.y / h));
          const newRowVal = Math.round(newNormalized * baseH);
          const minVal = (currentRows[idx - 1] ?? Math.round(((idx - 1) * baseH) / numRows)) + 10;
          const maxVal = (currentRows[idx + 1] ?? Math.round(((idx + 1) * baseH) / numRows)) - 10;
          const clamped = Math.max(minVal, Math.min(maxVal, newRowVal));
          const nextRows = [...currentRows];
          nextRows[idx] = clamped;

          if (onUpdateItemDividers) {
            onUpdateItemDividers(item.id, currentCols, nextRows);
          } else if (setRowDividers) {
            setRowDividers(nextRows);
          }
        }
        return;
      }

      // Hit-test dividers for hover cursor
      const hitThreshold = Math.max(8, Math.round(w / 45));
      let found: { type: 'col' | 'row'; index: number } | null = null;

      for (let c = 1; c < numCols; c++) {
        const x = getColX(c, w, numCols, baseW);
        if (Math.abs(x - pt.x) <= hitThreshold) {
          found = { type: 'col', index: c };
          break;
        }
      }

      if (!found) {
        for (let r = 1; r < numRows; r++) {
          const y = getRowY(r, h, numRows, baseH);
          if (Math.abs(y - pt.y) <= hitThreshold) {
            found = { type: 'row', index: r };
            break;
          }
        }
      }

      setHoveredDivider(found);
      if (found) return;
    } else {
      if (hoveredDivider) setHoveredDivider(null);
    }

    // 2. Handle Eyedropper Live Loupe Hover Sampling
    if (isEyedropperActive && onHoverColor) {
      const normX = Math.max(0, Math.min(1, screenX / (rect.width || 1)));
      const normY = Math.max(0, Math.min(1, screenY / (rect.height || 1)));
      const ctx = canvas.getContext('2d', { willReadFrequently: true });
      if (ctx) {
        const pxX = Math.min(canvas.width - 1, Math.max(0, Math.floor(normX * canvas.width)));
        const pxY = Math.min(canvas.height - 1, Math.max(0, Math.floor(normY * canvas.height)));
        const pixel = ctx.getImageData(pxX, pxY, 1, 1).data;
        const hex = `#${((1 << 24) + (pixel[0] << 16) + (pixel[1] << 8) + pixel[2]).toString(16).slice(1).toUpperCase()}`;
        onHoverColor({
          hex,
          r: pixel[0],
          g: pixel[1],
          b: pixel[2],
          x: e.clientX,
          y: e.clientY,
        });
      }
      return;
    }

    if (eraserMode === 'off') return;

    if (eraserMode === 'brush') {
      setHoverCursor({
        x: screenX,
        y: screenY,
        size: eraserBrushSize,
        visible: true,
      });

      if (isErasing && lastPoint) {
        const ctx = canvas.getContext('2d');
        if (ctx) {
          const canvasRadius = (eraserBrushSize / 2) * scaleX;
          eraseBrushStrokeOnCanvas(ctx, lastPoint.x, lastPoint.y, pt.x, pt.y, canvasRadius);
          setLastPoint(pt);
        }
      }
    } else if (eraserMode === 'box') {
      if (isErasing) {
        setBoxCurrent({
          screenX,
          screenY,
          canvasX: pt.x,
          canvasY: pt.y,
        });
      }
    }
  };

  const handlePointerUp = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (isCardPanning) {
      handleCardPointerUp(e as unknown as React.PointerEvent<HTMLDivElement>);
    }
    const canvas = canvasRef.current;
    if (draggingDividerRef.current) {
      try {
        (e.target as HTMLElement).releasePointerCapture(e.pointerId);
      } catch { }
      draggingDividerRef.current = null;
      setHoveredDivider(null);
      return;
    }

    if (eraserMode === 'box' && isErasing && boxStart && boxCurrent && canvas) {
      const ctx = canvas.getContext('2d');
      if (ctx) {
        const x = Math.min(boxStart.canvasX, boxCurrent.canvasX);
        const y = Math.min(boxStart.canvasY, boxCurrent.canvasY);
        const w = Math.abs(boxCurrent.canvasX - boxStart.canvasX);
        const h = Math.abs(boxCurrent.canvasY - boxStart.canvasY);
        if (w > 2 && h > 2) {
          eraseBoxSelectionOnCanvas(ctx, x, y, w, h);
        }
      }
    }

    if (isErasing && canvas && onUpdateItemImage) {
      const newDataUrl = canvas.toDataURL('image/png');
      onUpdateItemImage(item.id, newDataUrl);
    }

    if (isErasing) {
      setIsErasing(false);
      setLastPoint(null);
      setBoxStart(null);
      setBoxCurrent(null);
    }
  };

  const handlePointerLeave = () => {
    setHoverCursor((prev) => ({ ...prev, visible: false }));
    if (isEyedropperActive && onHoverColor) {
      onHoverColor(null);
    }
    if (isErasing) {
      setIsErasing(false);
      setLastPoint(null);
      setBoxStart(null);
      setBoxCurrent(null);
    }
  };

  const cardCursor =
    isEyedropperActive
      ? 'crosshair'
      : eraserMode !== 'off'
        ? 'crosshair'
        : isDirectBBoxCropActive
          ? 'crosshair'
          : cardZoom > 1.02 && isSpacePressed
            ? isCardPanning
              ? 'grabbing'
              : 'grab'
            : hoveredDivider
              ? hoveredDivider.type === 'col'
                ? 'col-resize'
                : 'row-resize'
              : 'pointer';

  return (
    <div
      ref={cardContainerRef}
      onClick={(e) => {
        if (eraserMode === 'off' && !hoveredDivider && !isCardPanning) {
          onClick && onClick(e, item);
        }
      }}
      onMouseMove={onMouseMove}
      onPointerDown={handleCardPointerDown}
      onPointerMove={handleCardPointerMove}
      onPointerUp={handleCardPointerUp}
      style={{
        borderRadius: 8,
        border: isActive
          ? '2px solid #38bdf8'
          : isSep
            ? '1.5px solid rgba(74,222,128,0.4)'
            : '1.5px solid rgba(255,255,255,0.12)',
        background:
          isSep || previewDisplayMode === 'transparent'
            ? checkerTheme === 'light'
              ? 'repeating-conic-gradient(#e2e8f0 0% 25%, #ffffff 0% 50%) 50% / 12px 12px'
              : 'repeating-conic-gradient(#1e293b 0% 25%, #0f172a 0% 50%) 50% / 12px 12px'
            : checkerTheme === 'light'
              ? 'rgba(245, 245, 250, 0.9)'
              : 'rgba(15, 23, 42, 0.7)',
        padding: 6,
        cursor: cardCursor,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 4,
        boxShadow: isActive ? '0 0 12px rgba(56,189,248,0.4)' : '0 2px 8px rgba(0,0,0,0.3)',
        transition: 'all 0.15s ease',
        position: 'relative',
        userSelect: 'none',
        width: '100%',
        height: '100%',
        minHeight: 0,
        boxSizing: 'border-box',
      }}
    >
      {/* Checkbox button */}
      {onToggleCheckedItem && (
        <button
          onClick={(e) => {
            e.stopPropagation();
            onToggleCheckedItem(item.id);
          }}
          style={{
            position: 'absolute',
            top: 10,
            left: 10,
            zIndex: 10,
            width: 22,
            height: 22,
            borderRadius: 5,
            border: '1.5px solid #38bdf8',
            background: '#0284c7',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#fff',
            cursor: 'pointer',
            boxShadow: '0 2px 6px rgba(0,0,0,0.5)',
          }}
        >
          <Check size={14} strokeWidth={3} />
        </button>
      )}

      {/* Status badge */}
      <div
        style={{
          position: 'absolute',
          top: 10,
          right: 10,
          zIndex: 10,
          padding: '2px 6px',
          borderRadius: 4,
          fontSize: 9.5,
          fontWeight: 700,
          background: isSep ? 'rgba(16,185,129,0.85)' : (item.customColDividers || item.customRowDividers) ? 'rgba(99,102,241,0.9)' : 'rgba(2,132,199,0.85)',
          color: '#ffffff',
          boxShadow: '0 2px 6px rgba(0,0,0,0.4)',
        }}
      >
        {isSep ? '✨ Đã tách' : (item.customColDividers || item.customRowDividers) ? '🎯 Lưới riêng' : '🖼️ Sprite Sheet'}
      </div>

      {/* Zoom Reset Badge */}
      {cardZoom > 1.02 && (
        <button
          onClick={(e) => {
            e.stopPropagation();
            setCardZoom(1.0);
            setCardPanOffset({ x: 0, y: 0 });
          }}
          style={{
            position: 'absolute',
            bottom: 26,
            right: 8,
            zIndex: 15,
            padding: '2px 7px',
            fontSize: 9,
            fontWeight: 700,
            borderRadius: 4,
            background: 'rgba(2, 132, 199, 0.95)',
            color: '#ffffff',
            border: '1px solid rgba(255,255,255,0.4)',
            cursor: 'pointer',
            boxShadow: '0 2px 8px rgba(0,0,0,0.6)',
            display: 'flex',
            alignItems: 'center',
            gap: 3,
          }}
          title="Nhấp để reset về cỡ Fit 100%"
        >
          🔍 {Math.round(cardZoom * 100)}% (Fit)
        </button>
      )}

      {/* Canvas Wrapper with natural responsive aspect ratio fitting the card */}
      <div
        style={{
          width: '100%',
          flex: 1,
          minHeight: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          overflow: 'hidden',
          borderRadius: 4,
          position: 'relative',
        }}
      >
        <div
          style={{
            position: 'relative',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            maxWidth: '100%',
            maxHeight: '100%',
            width: '100%',
            height: '100%',
            transform:
              cardZoom > 1.0
                ? `translate(${cardPanOffset.x}px, ${cardPanOffset.y}px) scale(${cardZoom})`
                : 'none',
            transformOrigin: 'center center',
            transition: isCardPanning ? 'none' : 'transform 0.08s ease-out',
            cursor: cardCursor,
          }}
        >
          <canvas
            ref={canvasRef}
            onPointerDown={handlePointerDown}
            onPointerMove={handlePointerMove}
            onPointerUp={handlePointerUp}
            onPointerLeave={handlePointerLeave}
            style={{
              maxWidth: '100%',
              maxHeight: '100%',
              width: 'auto',
              height: 'auto',
              objectFit: 'contain',
              aspectRatio:
                naturalSize.width > 0 && naturalSize.height > 0
                  ? `${naturalSize.width} / ${naturalSize.height}`
                  : 'auto',
              display: 'block',
              borderRadius: 4,
              touchAction: 'none',
              cursor: cardCursor,
            }}
          />

          {/* Brush Preview Ring Overlay */}
          {eraserMode === 'brush' && hoverCursor.visible && (
            <div
              style={{
                position: 'absolute',
                left: hoverCursor.x,
                top: hoverCursor.y,
                width: hoverCursor.size,
                height: hoverCursor.size,
                borderRadius: '50%',
                border: '1.5px solid #f59e0b',
                boxShadow: '0 0 8px rgba(245, 158, 11, 0.6), inset 0 0 4px rgba(245, 158, 11, 0.3)',
                transform: 'translate(-50%, -50%)',
                pointerEvents: 'none',
                zIndex: 20,
              }}
            />
          )}

          {/* Box Selection Eraser Overlay */}
          {eraserMode === 'box' && isErasing && boxStart && boxCurrent && (
            <div
              style={{
                position: 'absolute',
                left: Math.min(boxStart.screenX, boxCurrent.screenX),
                top: Math.min(boxStart.screenY, boxCurrent.screenY),
                width: Math.abs(boxCurrent.screenX - boxStart.screenX),
                height: Math.abs(boxCurrent.screenY - boxStart.screenY),
                border: '2px dashed #ffffff',
                outline: '2px dashed #000000',
                background: 'rgba(239, 68, 68, 0.25)',
                boxShadow: '0 0 10px rgba(239, 68, 68, 0.4)',
                pointerEvents: 'none',
                zIndex: 30,
              }}
            />
          )}
        </div>
      </div>

      {/* Item metadata label */}
      <div
        style={{
          width: '100%',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          fontSize: 9.5,
          color: '#94a3b8',
          padding: '0 2px',
        }}
      >
        <span
          title={item.name}
          style={{
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
            maxWidth: '70%',
            fontWeight: 600,
            color: isActive ? '#38bdf8' : '#e2e8f0',
          }}
        >
          {item.name}
        </span>
        <span style={{ fontSize: 9, opacity: 0.8 }}>
          {naturalSize.width}×{naturalSize.height}
        </span>
      </div>
    </div>
  );
};
