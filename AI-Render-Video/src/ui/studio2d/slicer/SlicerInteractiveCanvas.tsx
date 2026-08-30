import React, { useState, useRef, useEffect, useCallback } from 'react';
import { GridCategoryDefinition } from '../../../core/assets/GridSliceRegistry';
import { SlicerUploadedImageItem } from './hooks/useSlicerMultiImageGallery';
import { ChromaProcessOptions } from '../../../core/utils/ChromaDespeckleProcessor';

// Subcomponents
import { SlicerCanvasTopBar } from './canvas/SlicerCanvasTopBar';
import { SlicerGridCardWithBBox } from './canvas/SlicerGridCardWithBBox';
import { SlicerEyedropperOverlay } from './canvas/SlicerEyedropperOverlay';

export interface SlicerInteractiveCanvasProps {
  imageCanvasRef: React.RefObject<HTMLCanvasElement>;
  hasImage?: boolean;
  loadedImage?: HTMLImageElement | null;
  isEyedropperActive?: boolean;
  eyedropperTarget?: 'chroma' | 'fringe' | 'smooth';
  eyedropperHoverColor?: { hex: string; r: number; g: number; b: number; x: number; y: number } | null;
  previewDisplayMode: 'transparent' | 'original';
  setPreviewDisplayMode: (mode: 'transparent' | 'original') => void;
  onTogglePreviewDisplayMode?: (mode: 'transparent' | 'original') => void;
  checkerTheme?: 'dark' | 'light';
  onToggleCheckerTheme?: () => void;
  onOpenGridTablePicker?: () => void;
  isSingleImageMode?: boolean;
  onToggleSingleImageMode?: () => void;
  hasExplicitlySliced: boolean;
  currentCategory: GridCategoryDefinition;
  onAutoFitGrid?: () => void;
  onResetUniformGrid?: () => void;
  onUndo?: () => void;
  onRedo?: () => void;
  canUndo?: boolean;
  canRedo?: boolean;
  historyToast?: { message: string; type: 'undo' | 'redo' } | null;
  onMouseDown: (e: React.MouseEvent<HTMLCanvasElement>) => void;
  onDoubleClick: (e: React.MouseEvent<HTMLCanvasElement>) => void;
  onMouseMove: (e: React.MouseEvent<HTMLCanvasElement>) => void;
  onMouseLeave?: () => void;
  onMouseUp: () => void;

  // Direct Bounding Box Crop
  isDirectBBoxCropActive?: boolean;
  onToggleDirectBBoxCrop?: () => void;
  directBBoxPadding?: number;
  setDirectBBoxPadding?: (pad: number) => void;
  onApplyDirectBBoxCrop?: () => void;
  paddingInset?: number;
  chromaOptions?: ChromaProcessOptions;

  // Checked images grid mode
  checkedImageItems?: SlicerUploadedImageItem[];
  activeImageId?: string;
  onSelectCheckedImage?: (id: string) => void;
  onToggleCheckedItem?: (id: string) => void;
  onClearCheckedImages?: () => void;

  // Eraser Tools
  eraserMode?: 'off' | 'brush' | 'box';
  setEraserMode?: (m: 'off' | 'brush' | 'box') => void;
  eraserBrushSize?: number;
  setEraserBrushSize?: (s: number) => void;
  onUpdateItemImage?: (id: string, newDataUrl: string) => void;

  // Eyedropper callbacks
  onPickColor?: (hex: string) => void;
  onHoverColor?: (c: { hex: string; r: number; g: number; b: number; x: number; y: number } | null) => void;

  // Grid Divider Shift & Sync Controls
  colDividers?: number[];
  rowDividers?: number[];
  setColDividers?: React.Dispatch<React.SetStateAction<number[]>>;
  setRowDividers?: React.Dispatch<React.SetStateAction<number[]>>;
  dividerSyncMode?: 'all' | 'single';
  onToggleDividerSyncMode?: () => void;
  onUpdateItemDividers?: (itemId: string, newColDividers?: number[], newRowDividers?: number[]) => void;
}

export const SlicerInteractiveCanvas: React.FC<SlicerInteractiveCanvasProps> = ({
  imageCanvasRef,
  hasImage = false,
  loadedImage = null,
  isEyedropperActive = false,
  eyedropperTarget = 'chroma',
  eyedropperHoverColor = null,
  previewDisplayMode,
  setPreviewDisplayMode,
  onTogglePreviewDisplayMode,
  checkerTheme = 'dark',
  onToggleCheckerTheme,
  onOpenGridTablePicker,
  isSingleImageMode = false,
  onToggleSingleImageMode,
  hasExplicitlySliced,
  currentCategory,
  onAutoFitGrid,
  onResetUniformGrid,
  onUndo,
  onRedo,
  canUndo = false,
  canRedo = false,
  historyToast = null,
  onMouseDown,
  onDoubleClick,
  onMouseMove,
  onMouseLeave,
  onMouseUp,
  isDirectBBoxCropActive = false,
  onToggleDirectBBoxCrop,
  directBBoxPadding = 0,
  setDirectBBoxPadding,
  onApplyDirectBBoxCrop,
  paddingInset = 0,
  chromaOptions,
  checkedImageItems = [],
  activeImageId,
  onSelectCheckedImage,
  onToggleCheckedItem,
  onClearCheckedImages,
  eraserMode = 'off',
  setEraserMode,
  eraserBrushSize = 20,
  setEraserBrushSize,
  onUpdateItemImage,
  onPickColor,
  onHoverColor,
  colDividers,
  rowDividers,
  setColDividers,
  setRowDividers,
  dividerSyncMode = 'single',
  onToggleDividerSyncMode,
  onUpdateItemDividers,
}) => {
  const [zoom, setZoom] = useState<number>(1);
  const [panOffset, setPanOffset] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [isPanning, setIsPanning] = useState<boolean>(false);
  const [panStart, setPanStart] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [isSpacePressed, setIsSpacePressed] = useState<boolean>(false);
  const viewportRef = useRef<HTMLDivElement>(null);

interface EraserBoxPoint {
  screenX: number;
  screenY: number;
  canvasX: number;
  canvasY: number;
}

  // Single Canvas Eraser States
  const [isSingleErasing, setIsSingleErasing] = useState<boolean>(false);
  const [singleLastPt, setSingleLastPt] = useState<{ x: number; y: number } | null>(null);
  const [singleBoxStart, setSingleBoxStart] = useState<EraserBoxPoint | null>(null);
  const [singleBoxCurrent, setSingleBoxCurrent] = useState<EraserBoxPoint | null>(null);
  const [singleCursorPos, setSingleCursorPos] = useState<{ x: number; y: number; visible: boolean }>({ x: 0, y: 0, visible: false });

  const handleAutoFitToViewport = useCallback(() => {
    const canvas = imageCanvasRef.current;
    const viewport = viewportRef.current;
    if (!canvas || !viewport) return;
    const vpW = viewport.clientWidth - 32;
    const vpH = viewport.clientHeight - 32;
    if (vpW <= 0 || vpH <= 0 || canvas.width <= 0 || canvas.height <= 0) return;
    const scale = Math.min(vpW / canvas.width, vpH / canvas.height);
    setZoom(Math.max(0.1, Math.round(scale * 100) / 100));
    setPanOffset({ x: 0, y: 0 });
  }, [imageCanvasRef]);

  useEffect(() => {
    if (hasImage && checkedImageItems.length <= 1) {
      const timer = setTimeout(() => {
        handleAutoFitToViewport();
      }, 50);
      return () => clearTimeout(timer);
    }
  }, [hasImage, loadedImage, activeImageId, checkedImageItems.length, handleAutoFitToViewport]);

  // Spacebar pan navigation
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.code === 'Space' && !e.repeat && document.activeElement?.tagName !== 'INPUT' && document.activeElement?.tagName !== 'TEXTAREA') {
        setIsSpacePressed(true);
      }
    };
    const handleKeyUp = (e: KeyboardEvent) => {
      if (e.code === 'Space') {
        setIsSpacePressed(false);
        setIsPanning(false);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
    };
  }, []);

  const handleWheel = (e: React.WheelEvent<HTMLDivElement>) => {
    // Khi đang xem danh sách card ảnh (checkedImageItems > 0), cho phép cuộn chuột dọc tự nhiên trừ khi nhấn Ctrl/Cmd để zoom
    if (checkedImageItems.length > 0 && !e.ctrlKey && !e.metaKey) {
      return;
    }
    e.preventDefault();
    const zoomFactor = 1.1;
    if (e.deltaY < 0) {
      setZoom((prev) => Math.min(8.0, Math.round(prev * zoomFactor * 100) / 100));
    } else {
      setZoom((prev) => Math.max(0.1, Math.round((prev / zoomFactor) * 100) / 100));
    }
  };

  const handleContainerMouseDown = (e: React.MouseEvent<HTMLDivElement>) => {
    if (eraserMode !== 'off') return;
    if (isSpacePressed || e.button === 1 || e.altKey) {
      e.preventDefault();
      setIsPanning(true);
      setPanStart({ x: e.clientX - panOffset.x, y: e.clientY - panOffset.y });
    }
  };

  const handleContainerMouseMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (isPanning) {
      setPanOffset({ x: e.clientX - panStart.x, y: e.clientY - panStart.y });
    }
  };

  const handleContainerMouseUp = () => {
    if (isPanning) setIsPanning(false);
  };

  const handleModeClick = (mode: 'transparent' | 'original') => {
    if (onTogglePreviewDisplayMode) {
      onTogglePreviewDisplayMode(mode);
    } else {
      setPreviewDisplayMode(mode);
    }
  };

  // Helper to convert mouse event to single canvas image pixel coords
  const getSingleCanvasCoords = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = imageCanvasRef.current;
    if (!canvas) return { x: 0, y: 0 };
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / (rect.width || 1);
    const scaleY = canvas.height / (rect.height || 1);
    return {
      x: (e.clientX - rect.left) * scaleX,
      y: (e.clientY - rect.top) * scaleY,
    };
  };

  const handleSingleCanvasMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (eraserMode === 'off') {
      onMouseDown(e);
      return;
    }
    const canvas = imageCanvasRef.current;
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
    setIsSingleErasing(true);
    setSingleLastPt(pt);

    const ctx = canvas.getContext('2d');
    if (ctx && eraserMode === 'brush') {
      const canvasRadius = (eraserBrushSize / 2) * scaleX;
      ctx.save();
      ctx.globalCompositeOperation = 'destination-out';
      ctx.beginPath();
      ctx.arc(pt.x, pt.y, canvasRadius, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    } else if (eraserMode === 'box') {
      const boxPt: EraserBoxPoint = {
        screenX,
        screenY,
        canvasX: pt.x,
        canvasY: pt.y,
      };
      setSingleBoxStart(boxPt);
      setSingleBoxCurrent(boxPt);
    }
  };

  const handleSingleCanvasMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (eraserMode !== 'off') {
      const viewport = viewportRef.current;
      if (viewport) {
        const vpRect = viewport.getBoundingClientRect();
        setSingleCursorPos({
          x: e.clientX - vpRect.left,
          y: e.clientY - vpRect.top,
          size: eraserBrushSize,
          visible: true,
        });
      }
    }

    if (eraserMode === 'off') {
      onMouseMove(e);
      return;
    }

    if (!isSingleErasing) return;
    const canvas = imageCanvasRef.current;
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
    const ctx = canvas.getContext('2d');

    if (eraserMode === 'brush' && singleLastPt && ctx) {
      const canvasRadius = (eraserBrushSize / 2) * scaleX;
      ctx.save();
      ctx.globalCompositeOperation = 'destination-out';
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';
      ctx.lineWidth = canvasRadius * 2;
      ctx.beginPath();
      ctx.moveTo(singleLastPt.x, singleLastPt.y);
      ctx.lineTo(pt.x, pt.y);
      ctx.stroke();
      ctx.beginPath();
      ctx.arc(pt.x, pt.y, canvasRadius, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
      setSingleLastPt(pt);
    } else if (eraserMode === 'box') {
      setSingleBoxCurrent({
        screenX,
        screenY,
        canvasX: pt.x,
        canvasY: pt.y,
      });
    }
  };

  const handleSingleCanvasMouseUp = () => {
    if (eraserMode === 'off') {
      onMouseUp();
      return;
    }
    if (!isSingleErasing) return;
    setIsSingleErasing(false);

    const canvas = imageCanvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');

    if (eraserMode === 'box' && singleBoxStart && singleBoxCurrent && ctx) {
      const x = Math.min(singleBoxStart.canvasX, singleBoxCurrent.canvasX);
      const y = Math.min(singleBoxStart.canvasY, singleBoxCurrent.canvasY);
      const w = Math.abs(singleBoxCurrent.canvasX - singleBoxStart.canvasX);
      const h = Math.abs(singleBoxCurrent.canvasY - singleBoxStart.canvasY);
      if (w > 2 && h > 2) {
        ctx.save();
        ctx.globalCompositeOperation = 'destination-out';
        ctx.fillRect(x, y, w, h);
        ctx.restore();
      }
      setSingleBoxStart(null);
      setSingleBoxCurrent(null);
    }

    setSingleLastPt(null);

    // If active image item exists, sync updated dataUrl
    if (activeImageId && onUpdateItemImage) {
      const newDataUrl = canvas.toDataURL('image/png');
      onUpdateItemImage(activeImageId, newDataUrl);
    }
  };

  const handleSingleCanvasMouseLeave = () => {
    setSingleCursorPos((prev) => ({ ...prev, visible: false }));
    if (isSingleErasing) {
      handleSingleCanvasMouseUp();
    }
    if (onMouseLeave) onMouseLeave();
  };

  // Grid card click and mouse move for eyedropper sampling
  const handleGridImageClick = (e: React.MouseEvent<HTMLDivElement>, item: SlicerUploadedImageItem) => {
    if (eraserMode !== 'off') return;
    if (isEyedropperActive) {
      const card = e.currentTarget;
      const canvas = card.querySelector('canvas');
      if (canvas && onPickColor) {
        const rect = canvas.getBoundingClientRect();
        const normX = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
        const normY = Math.max(0, Math.min(1, (e.clientY - rect.top) / rect.height));

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

    if (e.ctrlKey || e.metaKey) {
      onToggleCheckedItem?.(item.id);
    } else {
      onSelectCheckedImage?.(item.id);
    }
  };

  const handleGridImageMouseMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (isEyedropperActive && onHoverColor) {
      const card = e.currentTarget;
      const canvas = card.querySelector('canvas');
      if (canvas) {
        const rect = canvas.getBoundingClientRect();
        const normX = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
        const normY = Math.max(0, Math.min(1, (e.clientY - rect.top) / rect.height));

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
      }
    }
  };

  return (
    <div
      style={{
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        background: 'rgba(15, 23, 42, 0.7)',
        padding: 10,
        borderRadius: 8,
        border: '1px solid rgba(255,255,255,0.08)',
        minHeight: 0,
        overflow: 'hidden',
        fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
      }}
    >
      {/* Top Control Bar */}
      <SlicerCanvasTopBar
        currentCategory={currentCategory}
        hasImage={hasImage}
        loadedImage={loadedImage}
        checkedCount={checkedImageItems.length}
        zoom={zoom}
        setZoom={setZoom}
        setPanOffset={setPanOffset}
        onAutoFitToViewport={handleAutoFitToViewport}
        onOpenGridTablePicker={onOpenGridTablePicker}
        isSingleImageMode={isSingleImageMode}
        onToggleSingleImageMode={onToggleSingleImageMode}
        isDirectBBoxCropActive={isDirectBBoxCropActive}
        onToggleDirectBBoxCrop={onToggleDirectBBoxCrop}
        onAutoFitGrid={onAutoFitGrid}
        onResetUniformGrid={onResetUniformGrid}
        canUndo={canUndo}
        canRedo={canRedo}
        onUndo={onUndo}
        onRedo={onRedo}
        checkerTheme={checkerTheme}
        onToggleCheckerTheme={onToggleCheckerTheme}
        previewDisplayMode={previewDisplayMode}
        onModeClick={handleModeClick}
        eraserMode={eraserMode}
        setEraserMode={setEraserMode}
        eraserBrushSize={eraserBrushSize}
        setEraserBrushSize={setEraserBrushSize}
        dividerSyncMode={dividerSyncMode}
        onToggleDividerSyncMode={onToggleDividerSyncMode}
      />

      {/* Main Viewport */}
      <div
        ref={viewportRef}
        onWheel={handleWheel}
        onMouseDown={handleContainerMouseDown}
        onMouseMove={handleContainerMouseMove}
        onMouseUp={handleContainerMouseUp}
        style={{
          flex: 1,
          position: 'relative',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          overflow: 'hidden',
          background: '#090d16',
          borderRadius: 6,
          border: isEyedropperActive ? '1.5px solid #f59e0b' : '1px dashed rgba(255,255,255,0.1)',
          padding: 6,
          boxShadow: isEyedropperActive ? 'inset 0 0 20px rgba(245,158,11,0.15)' : 'none',
          cursor: isSpacePressed
            ? isPanning
              ? 'grabbing'
              : 'grab'
            : isEyedropperActive
            ? 'crosshair'
            : eraserMode !== 'off'
            ? 'crosshair'
            : 'default',
        }}
      >
        {/* Eyedropper Guidance & Loupe Overlay */}
        <SlicerEyedropperOverlay
          isEyedropperActive={isEyedropperActive}
          eyedropperTarget={eyedropperTarget}
          eyedropperHoverColor={eyedropperHoverColor}
        />

        {/* Multi-image checked grid view (hoặc 1 ảnh checked đơn lẻ) */}
        {checkedImageItems.length > 0 ? (
          <div
            style={{
              width: '100%',
              height: '100%',
              maxHeight: '100%',
              minHeight: 0,
              overflowY: 'auto',
              display: 'grid',
              gridTemplateColumns:
                checkedImageItems.length === 1
                  ? '1fr'
                  : 'repeat(auto-fill, minmax(140px, 180px))',
              gridAutoRows: checkedImageItems.length === 1 ? '1fr' : 'minmax(200px, 320px)',
              gap: 10,
              padding: 10,
              alignContent: 'start',
              justifyContent: 'center',
              boxSizing: 'border-box',
            }}
          >
            {checkedImageItems.map((item) => (
              <SlicerGridCardWithBBox
                key={item.id}
                item={item}
                isActive={item.id === activeImageId}
                isEyedropperActive={isEyedropperActive}
                isDirectBBoxCropActive={isDirectBBoxCropActive}
                directBBoxPadding={directBBoxPadding}
                chromaOptions={chromaOptions}
                checkerTheme={checkerTheme}
                previewDisplayMode={previewDisplayMode}
                currentCategory={currentCategory}
                colDividers={colDividers}
                rowDividers={rowDividers}
                setColDividers={setColDividers}
                setRowDividers={setRowDividers}
                onUpdateItemDividers={onUpdateItemDividers}
                paddingInset={paddingInset || directBBoxPadding}
                eraserMode={eraserMode}
                eraserBrushSize={eraserBrushSize}
                onUpdateItemImage={onUpdateItemImage}
                onClick={handleGridImageClick}
                onMouseMove={handleGridImageMouseMove}
                onToggleCheckedItem={onToggleCheckedItem}
                onPickColor={onPickColor}
                onHoverColor={onHoverColor}
              />
            ))}
          </div>
        ) : (
          /* Single image main canvas view */
          <>
            {!hasImage && (
              <div
                style={{
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  gap: 10,
                  color: '#64748b',
                  textAlign: 'center',
                }}
              >
                <div style={{ fontSize: 32, opacity: 0.5 }}>🖼️</div>
                <div style={{ fontWeight: 600, color: '#94a3b8' }}>Khung ảnh đang trống</div>
                <div style={{ fontSize: 11, color: '#475569', maxWidth: 280 }}>
                  Vui lòng tải ảnh sprite sheet lên hoặc chọn ảnh mẫu bên trái để hiển thị
                </div>
              </div>
            )}

            <div
              style={{
                position: 'relative',
                transform: `translate(${panOffset.x}px, ${panOffset.y}px) scale(${zoom})`,
                transformOrigin: 'center center',
                display: hasImage ? 'inline-block' : 'none',
                lineHeight: 0,
                boxShadow: '0 8px 32px rgba(0,0,0,0.8), 0 0 0 1px rgba(56, 189, 248, 0.25)',
                borderRadius: 4,
                transition: isPanning ? 'none' : 'transform 0.05s ease-out',
                flexShrink: 0,
              }}
            >
              <canvas
                ref={imageCanvasRef}
                onMouseDown={handleSingleCanvasMouseDown}
                onDoubleClick={onDoubleClick}
                onMouseMove={handleSingleCanvasMouseMove}
                onMouseUp={handleSingleCanvasMouseUp}
                onMouseLeave={handleSingleCanvasMouseLeave}
                style={{
                  display: 'block',
                  borderRadius: 4,
                  cursor: isEyedropperActive ? 'crosshair' : isSpacePressed ? (isPanning ? 'grabbing' : 'grab') : eraserMode !== 'off' ? 'crosshair' : undefined,
                }}
              />

              {/* Single Canvas Box Selection Overlay */}
              {eraserMode === 'box' && isSingleErasing && singleBoxStart && singleBoxCurrent && (
                <div
                  style={{
                    position: 'absolute',
                    left: Math.min(singleBoxStart.screenX, singleBoxCurrent.screenX),
                    top: Math.min(singleBoxStart.screenY, singleBoxCurrent.screenY),
                    width: Math.abs(singleBoxCurrent.screenX - singleBoxStart.screenX),
                    height: Math.abs(singleBoxCurrent.screenY - singleBoxStart.screenY),
                    border: '2px dashed #ffffff',
                    outline: '2px dashed #000000',
                    background: 'rgba(239, 68, 68, 0.25)',
                    boxShadow: '0 0 10px rgba(239, 68, 68, 0.4)',
                    pointerEvents: 'none',
                    zIndex: 40,
                  }}
                />
              )}
            </div>

            {/* Single Canvas Brush Cursor Preview Ring at Viewport Level */}
            {eraserMode === 'brush' && singleCursorPos.visible && (
              <div
                style={{
                  position: 'absolute',
                  left: singleCursorPos.x,
                  top: singleCursorPos.y,
                  width: singleCursorPos.size,
                  height: singleCursorPos.size,
                  borderRadius: '50%',
                  border: '2px solid #f59e0b',
                  boxShadow: '0 0 10px rgba(245, 158, 11, 0.7), inset 0 0 4px rgba(245, 158, 11, 0.4)',
                  transform: 'translate(-50%, -50%)',
                  pointerEvents: 'none',
                  zIndex: 50,
                }}
              />
            )}
          </>
        )}

        {historyToast && (
          <div
            style={{
              position: 'absolute',
              top: 52,
              left: '50%',
              transform: 'translateX(-50%)',
              zIndex: 40,
              background: 'rgba(15, 23, 42, 0.92)',
              border: historyToast.type === 'undo' ? '1px solid rgba(56, 189, 248, 0.4)' : '1px solid rgba(74, 222, 128, 0.4)',
              boxShadow: '0 8px 24px rgba(0,0,0,0.6)',
              borderRadius: 6,
              padding: '6px 14px',
              fontSize: 11,
              fontWeight: 700,
              color: '#ffffff',
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              animation: 'fadeIn 0.2s ease-out',
            }}
          >
            <span>{historyToast.type === 'undo' ? '↩️' : '↪️'}</span>
            <span>{historyToast.message}</span>
          </div>
        )}
      </div>
    </div>
  );
};
