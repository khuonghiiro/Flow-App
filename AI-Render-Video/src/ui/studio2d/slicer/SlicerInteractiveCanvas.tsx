import React, { useState, useRef, useEffect, useCallback } from 'react';
import { GridCategoryDefinition } from '../../../core/assets/GridSliceRegistry';
import { SlicerUploadedImageItem } from './hooks/useSlicerMultiImageGallery';
import { ChromaProcessOptions } from '../../../core/utils/ChromaDespeckleProcessor';

// Subcomponents
import { SlicerCanvasTopBar } from './canvas/SlicerCanvasTopBar';
import { SlicerGridCardWithBBox } from './canvas/SlicerGridCardWithBBox';
import { SlicerBBoxControlCard } from './canvas/SlicerBBoxControlCard';
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
  chromaOptions?: ChromaProcessOptions;

  // Checked images grid mode
  checkedImageItems?: SlicerUploadedImageItem[];
  activeImageId?: string;
  onSelectCheckedImage?: (id: string) => void;
  onToggleCheckedItem?: (id: string) => void;
  onClearCheckedImages?: () => void;

  // Eyedropper callbacks
  onPickColor?: (hex: string) => void;
  onHoverColor?: (c: { hex: string; r: number; g: number; b: number; x: number; y: number } | null) => void;
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
  chromaOptions,
  checkedImageItems = [],
  activeImageId,
  onSelectCheckedImage,
  onToggleCheckedItem,
  onClearCheckedImages,
  onPickColor,
  onHoverColor,
}) => {
  const [zoom, setZoom] = useState<number>(1);
  const [panOffset, setPanOffset] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [isPanning, setIsPanning] = useState<boolean>(false);
  const [panStart, setPanStart] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [isSpacePressed, setIsSpacePressed] = useState<boolean>(false);
  const viewportRef = useRef<HTMLDivElement>(null);

  const handleAutoFitToViewport = useCallback(() => {
    const canvas = imageCanvasRef.current;
    const viewport = viewportRef.current;
    if (!canvas || !viewport) return;
    const vpW = viewport.clientWidth - 40;
    const vpH = viewport.clientHeight - 40;
    if (vpW <= 0 || vpH <= 0 || canvas.width <= 0 || canvas.height <= 0) return;
    const scale = Math.min(vpW / canvas.width, vpH / canvas.height, 1.0);
    setZoom(Math.max(0.1, Math.round(scale * 100) / 100));
    setPanOffset({ x: 0, y: 0 });
  }, [imageCanvasRef]);

  useEffect(() => {
    if (hasImage && checkedImageItems.length <= 1) {
      handleAutoFitToViewport();
    }
  }, [hasImage, checkedImageItems.length, handleAutoFitToViewport]);

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
    if (checkedImageItems.length > 1) return;
    if (e.ctrlKey || e.metaKey || isSpacePressed) {
      e.preventDefault();
      const zoomFactor = e.deltaY < 0 ? 1.15 : 0.85;
      setZoom((prev) => Math.min(8.0, Math.max(0.05, Math.round(prev * zoomFactor * 100) / 100)));
    } else {
      setPanOffset((prev) => ({
        x: prev.x - e.deltaX * 0.8,
        y: prev.y - e.deltaY * 0.8,
      }));
    }
  };

  const handleContainerMouseDown = (e: React.MouseEvent<HTMLDivElement>) => {
    if (e.button === 1 || (e.button === 0 && isSpacePressed)) {
      e.preventDefault();
      setIsPanning(true);
      setPanStart({ x: e.clientX - panOffset.x, y: e.clientY - panOffset.y });
    }
  };

  const handleContainerMouseMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (isPanning) {
      setPanOffset({
        x: e.clientX - panStart.x,
        y: e.clientY - panStart.y,
      });
    }
  };

  const handleContainerMouseUp = () => {
    setIsPanning(false);
  };

  const handleModeClick = (mode: 'transparent' | 'original') => {
    if (onTogglePreviewDisplayMode) {
      onTogglePreviewDisplayMode(mode);
    } else {
      setPreviewDisplayMode(mode);
    }
  };

  // Grid card click and mouse move for eyedropper sampling
  const handleGridImageClick = (e: React.MouseEvent<HTMLDivElement>, item: SlicerUploadedImageItem) => {
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
            : 'default',
        }}
      >
        {/* Eyedropper Guidance & Loupe Overlay */}
        <SlicerEyedropperOverlay
          isEyedropperActive={isEyedropperActive}
          eyedropperTarget={eyedropperTarget}
          eyedropperHoverColor={eyedropperHoverColor}
        />

        {/* Multi-Image Checked Grid Mode OR Single Canvas Mode */}
        {checkedImageItems.length > 1 ? (
          <div
            style={{
              width: '100%',
              height: '100%',
              display: 'flex',
              flexDirection: 'column',
              gap: 6,
              padding: 8,
              overflow: 'hidden',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexShrink: 0 }}>
              <div style={{ fontSize: 11, fontWeight: 800, color: '#c084fc', display: 'flex', alignItems: 'center', gap: 5 }}>
                🎯 Đang chọn: <span style={{ color: '#38bdf8', fontWeight: 900 }}>{checkedImageItems.length}</span> ảnh
              </div>
              {onClearCheckedImages && (
                <button
                  onClick={onClearCheckedImages}
                  style={{
                    padding: '2px 7px',
                    fontSize: 9,
                    fontWeight: 600,
                    borderRadius: 4,
                    background: 'rgba(239, 68, 68, 0.15)',
                    color: '#f87171',
                    border: '1px solid rgba(239, 68, 68, 0.3)',
                    cursor: 'pointer',
                  }}
                >
                  ✕ Bỏ chọn
                </button>
              )}
            </div>

            <div
              style={{
                flex: 1,
                display: 'grid',
                gridTemplateColumns: `repeat(${Math.min(Math.ceil(Math.sqrt(checkedImageItems.length)), 4)}, minmax(0, 1fr))`,
                gap: 8,
                overflowY: 'auto',
                minHeight: 0,
                padding: 4,
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
                  onClick={handleGridImageClick}
                  onMouseMove={handleGridImageMouseMove}
                  onToggleCheckedItem={onToggleCheckedItem}
                />
              ))}
            </div>
          </div>
        ) : (
          <>
            {!hasImage && (
              <div
                style={{
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: '#64748b',
                  fontSize: 12,
                  gap: 6,
                  padding: 24,
                  textAlign: 'center',
                  userSelect: 'none',
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
                display: hasImage ? 'flex' : 'none',
                alignItems: 'center',
                justifyContent: 'center',
                boxShadow: '0 8px 32px rgba(0,0,0,0.8), 0 0 0 1px rgba(56, 189, 248, 0.25)',
                borderRadius: 4,
                transition: isPanning ? 'none' : 'transform 0.05s ease-out',
                flexShrink: 0,
              }}
            >
              <canvas
                ref={imageCanvasRef}
                onMouseDown={onMouseDown}
                onDoubleClick={onDoubleClick}
                onMouseMove={onMouseMove}
                onMouseUp={onMouseUp}
                onMouseLeave={onMouseLeave || onMouseUp}
                style={{
                  display: 'block',
                  borderRadius: 4,
                  cursor: isEyedropperActive ? 'crosshair' : isSpacePressed ? (isPanning ? 'grabbing' : 'grab') : 'default',
                }}
              />
            </div>
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

        {/* Floating BBox Control Card */}
        <SlicerBBoxControlCard
          isDirectBBoxCropActive={isDirectBBoxCropActive}
          hasImage={hasImage}
          checkedCount={checkedImageItems.length}
          directBBoxPadding={directBBoxPadding}
          setDirectBBoxPadding={setDirectBBoxPadding}
          onToggleDirectBBoxCrop={onToggleDirectBBoxCrop}
          onApplyDirectBBoxCrop={onApplyDirectBBoxCrop}
        />
      </div>
    </div>
  );
};
