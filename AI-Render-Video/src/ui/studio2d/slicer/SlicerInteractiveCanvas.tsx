import React, { useState, useRef, useEffect } from 'react';
import { Eye, Layers, ZoomIn, ZoomOut, Target, Grid, Move, Undo2, Redo2 } from 'lucide-react';
import { GridCategoryDefinition } from '../../../core/assets/GridSliceRegistry';

interface SlicerInteractiveCanvasProps {
  imageCanvasRef: React.RefObject<HTMLCanvasElement>;
  hasImage?: boolean;
  isEyedropperActive?: boolean;
  eyedropperTarget?: 'chroma' | 'fringe' | 'smooth';
  eyedropperHoverColor?: { hex: string; r: number; g: number; b: number; x: number; y: number } | null;
  previewDisplayMode: 'transparent' | 'original';
  setPreviewDisplayMode: (mode: 'transparent' | 'original') => void;
  onTogglePreviewDisplayMode?: (mode: 'transparent' | 'original') => void;
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
}

export const SlicerInteractiveCanvas: React.FC<SlicerInteractiveCanvasProps> = ({
  imageCanvasRef,
  hasImage = false,
  isEyedropperActive = false,
  eyedropperTarget = 'chroma',
  eyedropperHoverColor = null,
  previewDisplayMode,
  setPreviewDisplayMode,
  onTogglePreviewDisplayMode,
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
}) => {
  const [zoom, setZoom] = useState<number>(1);
  const [panOffset, setPanOffset] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [isPanning, setIsPanning] = useState<boolean>(false);
  const [panStart, setPanStart] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [isSpacePressed, setIsSpacePressed] = useState<boolean>(false);
  const viewportRef = useRef<HTMLDivElement>(null);

  // Spacebar pan listener
  useEffect(() => {
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
  }, []);

  const handleWheel = (e: React.WheelEvent<HTMLDivElement>) => {
    if (!hasImage || isEyedropperActive) return;
    e.preventDefault();
    if (!viewportRef.current) return;

    const cRect = viewportRef.current.getBoundingClientRect();
    const mouseX = e.clientX - (cRect.left + cRect.width / 2);
    const mouseY = e.clientY - (cRect.top + cRect.height / 2);

    const zoomFactor = e.deltaY < 0 ? 1.15 : (1 / 1.15);
    const newZoom = Math.max(0.5, Math.min(8, Math.round(zoom * zoomFactor * 100) / 100));

    if (newZoom === zoom) return;

    const scaleRatio = newZoom / zoom;
    const newPanX = mouseX - (mouseX - panOffset.x) * scaleRatio;
    const newPanY = mouseY - (mouseY - panOffset.y) * scaleRatio;

    setZoom(newZoom);
    setPanOffset({ x: newPanX, y: newPanY });
  };

  const handleContainerMouseDown = (e: React.MouseEvent<HTMLDivElement>) => {
    if (e.button === 1 || e.button === 2 || isSpacePressed) {
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
    if (isPanning) {
      setIsPanning(false);
    }
  };
  const handleModeClick = (mode: 'transparent' | 'original') => {
    if (onTogglePreviewDisplayMode) {
      onTogglePreviewDisplayMode(mode);
    } else {
      setPreviewDisplayMode(mode);
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
      {/* Top Canvas Bar */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 6 }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
          <Layers size={13} /> {currentCategory.id === 'single_full_image' ? '🖼️ Chế độ ảnh đơn (Đã tắt khung lưới)' : `Khung lưới cắt (${currentCategory.rows} hàng × ${currentCategory.cols} cột)`}
        </div>

        {/* Action Controls: Auto-Fit + Reset Uniform + Preview Mode Toggle */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
          {currentCategory.id !== 'single_full_image' && (
            <div style={{ display: 'flex', gap: 4 }}>
              <button
                onClick={onAutoFitGrid}
                disabled={!hasImage}
                style={{
                  padding: '4px 9px',
                  fontSize: 10,
                  fontWeight: 700,
                  borderRadius: 4,
                  background: 'linear-gradient(135deg, rgba(2, 132, 199, 0.3), rgba(139, 92, 246, 0.3))',
                  color: hasImage ? '#38bdf8' : '#475569',
                  border: '1px solid rgba(56, 189, 248, 0.35)',
                  cursor: hasImage ? 'pointer' : 'not-allowed',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                  boxShadow: hasImage ? '0 0 8px rgba(56, 189, 248, 0.2)' : 'none',
                  transition: 'all 0.15s ease',
                }}
                title="Tự động quét và căn chỉnh các đường lưới ôm khớp khít từng linh kiện theo ảnh AI"
              >
                <Target size={11} /> 🎯 Tự Căn Khung (Auto-Fit)
              </button>

              <button
                onClick={onResetUniformGrid}
                disabled={!hasImage}
                style={{
                  padding: '4px 8px',
                  fontSize: 10,
                  fontWeight: 600,
                  borderRadius: 4,
                  background: 'rgba(255, 255, 255, 0.05)',
                  color: hasImage ? '#94a3b8' : '#475569',
                  border: '1px solid rgba(255, 255, 255, 0.1)',
                  cursor: hasImage ? 'pointer' : 'not-allowed',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                }}
                title="Đặt lại các ô lưới chia đều nhau theo chiều ngang và dọc"
              >
                <Grid size={11} /> 📐 Lưới Đều
              </button>
            </div>
          )}

          {/* Undo / Redo Controls */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 2, background: 'rgba(0,0,0,0.4)', padding: '2px 4px', borderRadius: 5, border: '1px solid rgba(255,255,255,0.06)' }}>
            <button
              onClick={onUndo}
              disabled={!canUndo}
              style={{
                padding: '3px 7px',
                borderRadius: 4,
                background: canUndo ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255,255,255,0.04)',
                color: canUndo ? '#38bdf8' : '#475569',
                border: canUndo ? '1px solid rgba(56, 189, 248, 0.3)' : '1px solid transparent',
                cursor: canUndo ? 'pointer' : 'not-allowed',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                fontSize: 10,
                fontWeight: 600,
                transition: 'all 0.15s ease',
              }}
              title="Hoàn tác thao tác trước (Ctrl + Z)"
            >
              <Undo2 size={12} /> Hoàn tác
            </button>
            <button
              onClick={onRedo}
              disabled={!canRedo}
              style={{
                padding: '3px 7px',
                borderRadius: 4,
                background: canRedo ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255,255,255,0.04)',
                color: canRedo ? '#38bdf8' : '#475569',
                border: canRedo ? '1px solid rgba(56, 189, 248, 0.3)' : '1px solid transparent',
                cursor: canRedo ? 'pointer' : 'not-allowed',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                fontSize: 10,
                fontWeight: 600,
                transition: 'all 0.15s ease',
              }}
              title="Làm lại thao tác vừa hoàn tác (Ctrl + Y)"
            >
              <Redo2 size={12} /> Làm lại
            </button>
          </div>

          {/* Zoom Controls */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, background: 'rgba(0,0,0,0.4)', padding: '2px 6px', borderRadius: 5, border: '1px solid rgba(255,255,255,0.06)' }}>
            <button
              onClick={() => {
                const newZ = Math.max(0.5, Math.round((zoom / 1.2) * 10) / 10);
                setZoom(newZ);
              }}
              disabled={!hasImage}
              style={{
                padding: '3px 6px',
                borderRadius: 4,
                background: 'rgba(255,255,255,0.08)',
                color: hasImage ? '#fff' : '#475569',
                border: 'none',
                cursor: hasImage ? 'pointer' : 'not-allowed',
                display: 'flex',
                alignItems: 'center',
              }}
              title="Thu nhỏ (-)"
            >
              <ZoomOut size={12} />
            </button>
            <span style={{ fontSize: 10.5, fontWeight: 700, color: '#38bdf8', minWidth: 38, textAlign: 'center', fontFamily: 'monospace' }}>
              {Math.round(zoom * 100)}%
            </span>
            <button
              onClick={() => {
                const newZ = Math.min(8, Math.round((zoom * 1.2) * 10) / 10);
                setZoom(newZ);
              }}
              disabled={!hasImage}
              style={{
                padding: '3px 6px',
                borderRadius: 4,
                background: 'rgba(255,255,255,0.08)',
                color: hasImage ? '#fff' : '#475569',
                border: 'none',
                cursor: hasImage ? 'pointer' : 'not-allowed',
                display: 'flex',
                alignItems: 'center',
              }}
              title="Phóng to (+)"
            >
              <ZoomIn size={12} />
            </button>
            <button
              onClick={() => {
                setZoom(1);
                setPanOffset({ x: 0, y: 0 });
              }}
              disabled={!hasImage}
              style={{
                padding: '2px 5px',
                fontSize: 9.5,
                fontWeight: 600,
                borderRadius: 4,
                background: 'rgba(255,255,255,0.06)',
                color: hasImage ? '#94a3b8' : '#475569',
                border: '1px solid rgba(255,255,255,0.08)',
                cursor: hasImage ? 'pointer' : 'not-allowed',
              }}
              title="Khôi phục kích thước 100%"
            >
              100%
            </button>
            <button
              onClick={() => {
                setZoom(1);
                setPanOffset({ x: 0, y: 0 });
              }}
              disabled={!hasImage}
              style={{
                padding: '2px 5px',
                fontSize: 9.5,
                fontWeight: 700,
                borderRadius: 4,
                background: 'rgba(56, 189, 248, 0.15)',
                color: hasImage ? '#38bdf8' : '#475569',
                border: '1px solid rgba(56, 189, 248, 0.3)',
                cursor: hasImage ? 'pointer' : 'not-allowed',
              }}
              title="Khôi phục vị trí mặc định"
            >
              Fit
            </button>
          </div>

          {/* Preview Mode Toggle */}
          <div style={{ display: 'flex', gap: 3, background: 'rgba(0,0,0,0.4)', padding: 2, borderRadius: 5 }}>
            <button
              onClick={() => handleModeClick('transparent')}
              disabled={!hasImage}
              style={{
                padding: '4px 9px',
                fontSize: 10,
                fontWeight: 600,
                borderRadius: 4,
                background: previewDisplayMode === 'transparent' ? '#0284c7' : 'transparent',
                color: previewDisplayMode === 'transparent' ? '#ffffff' : hasImage ? '#94a3b8' : '#475569',
                border: previewDisplayMode === 'transparent' ? '1px solid #38bdf8' : '1px solid transparent',
                cursor: hasImage ? 'pointer' : 'not-allowed',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                boxShadow: previewDisplayMode === 'transparent' ? '0 0 8px rgba(56,189,248,0.3)' : 'none',
                transition: 'all 0.15s ease',
              }}
              title="Xem kết quả sau khi đã bóc tách nền (trong suốt)"
            >
              <Layers size={12} /> 🏁 Đã tách nền
            </button>

            <button
              onClick={() => handleModeClick('original')}
              disabled={!hasImage}
              style={{
                padding: '4px 9px',
                fontSize: 10,
                fontWeight: 600,
                borderRadius: 4,
                background: previewDisplayMode === 'original' && hasImage ? '#0284c7' : 'transparent',
                color: hasImage ? (previewDisplayMode === 'original' ? '#ffffff' : '#94a3b8') : '#475569',
                border: previewDisplayMode === 'original' && hasImage ? '1px solid #38bdf8' : '1px solid transparent',
                cursor: hasImage ? 'pointer' : 'not-allowed',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                boxShadow: previewDisplayMode === 'original' && hasImage ? '0 0 8px rgba(56,189,248,0.3)' : 'none',
                transition: 'all 0.15s ease',
              }}
              title="Xem bức ảnh gốc ban đầu"
            >
              <Eye size={12} /> 👁️ Ảnh gốc
            </button>
          </div>
        </div>
      </div>

      {/* Canvas Display Viewport */}
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
        {/* Top Eyedropper Guidance Banner */}
        {isEyedropperActive && hasImage && (
          <div
            style={{
              position: 'absolute',
              top: 12,
              left: '50%',
              transform: 'translateX(-50%)',
              background: 'rgba(15, 23, 42, 0.94)',
              border: eyedropperTarget === 'smooth' ? '1.5px solid #38bdf8' : eyedropperTarget === 'fringe' ? '1.5px solid #10b981' : '1.5px solid #f59e0b',
              borderRadius: 20,
              padding: '5px 14px',
              color: eyedropperTarget === 'smooth' ? '#38bdf8' : eyedropperTarget === 'fringe' ? '#a7f3d0' : '#fef08a',
              fontSize: 10.5,
              fontWeight: 600,
              boxShadow: '0 4px 20px rgba(0,0,0,0.7), 0 0 12px rgba(56,189,248,0.3)',
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              zIndex: 30,
              pointerEvents: 'none',
              fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
            }}
          >
            <span>
              {eyedropperTarget === 'smooth'
                ? '🎯 Chế độ hút màu viền làm mịn: Rê chuột và nhấp vào nét vẽ để chọn màu viền khử răng cưa'
                : eyedropperTarget === 'fringe'
                ? '🎯 Chế độ hút màu viền rác: Rê chuột và nhấp vào vùng viền sượng/sạn để chọn màu khử'
                : '🎯 Chế độ hút màu nền: Rê chuột lên ảnh và nhấp chuột để chọn mã màu nền cần tách'}
            </span>
          </div>
        )}

        {/* Photoshop CS6 Style Floating Loupe / Color Preview Swatch */}
        {isEyedropperActive && eyedropperHoverColor && (
          <div
            style={{
              position: 'fixed',
              left: eyedropperHoverColor.x + 18,
              top: eyedropperHoverColor.y - 45,
              pointerEvents: 'none',
              zIndex: 9999,
              background: 'rgba(15, 23, 42, 0.94)',
              backdropFilter: 'blur(8px)',
              border: '1.5px solid #38bdf8',
              borderRadius: 8,
              padding: '6px 10px',
              boxShadow: '0 8px 24px rgba(0,0,0,0.85), 0 0 14px rgba(56,189,248,0.4)',
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              transform: 'translate3d(0,0,0)',
              fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
            }}
          >
            <div
              style={{
                width: 30,
                height: 30,
                borderRadius: '50%',
                background: eyedropperHoverColor.hex,
                border: '2.5px solid #ffffff',
                boxShadow: '0 0 0 1.5px #000000, 0 2px 6px rgba(0,0,0,0.5)',
                flexShrink: 0,
              }}
            />
            <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
              <div style={{ fontSize: 11.5, fontWeight: 700, color: '#38bdf8', fontFamily: 'monospace', letterSpacing: 0.5 }}>
                {eyedropperHoverColor.hex}
              </div>
              <div style={{ fontSize: 9, color: '#94a3b8', fontFamily: 'monospace' }}>
                RGB({eyedropperHoverColor.r}, {eyedropperHoverColor.g}, {eyedropperHoverColor.b})
              </div>
              <div style={{ fontSize: 8.5, color: '#4ade80', fontWeight: 600 }}>
                👆 Nhấp chuột để lấy màu
              </div>
            </div>
          </div>
        )}

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

        {/* Scaled and Translated Canvas Container */}
        <div
          style={{
            position: 'relative',
            transform: `translate(${panOffset.x}px, ${panOffset.y}px) scale(${zoom})`,
            transformOrigin: 'center center',
            display: hasImage ? 'flex' : 'none',
            alignItems: 'center',
            justifyContent: 'center',
            maxWidth: '100%',
            maxHeight: '100%',
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
              maxWidth: '100%',
              maxHeight: '100%',
              objectFit: 'contain',
              boxShadow: '0 4px 20px rgba(0,0,0,0.6)',
              cursor: isEyedropperActive ? 'crosshair' : isSpacePressed ? (isPanning ? 'grabbing' : 'grab') : 'default',
            }}
          />
        </div>

        {/* Floating History Toast Notification (Undo/Redo Feedback) */}
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
              fontWeight: 600,
              color: historyToast.type === 'undo' ? '#38bdf8' : '#4ade80',
              backdropFilter: 'blur(10px)',
              pointerEvents: 'none',
              animation: 'fadeInOut 2.5s ease',
              display: 'flex',
              alignItems: 'center',
              gap: 6,
            }}
          >
            {historyToast.message}
          </div>
        )}

        {/* Floating Zoom & Pan Guide Badge */}
        {hasImage && (
          <div
            style={{
              position: 'absolute',
              bottom: 8,
              right: 8,
              background: 'rgba(15, 23, 42, 0.85)',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              borderRadius: 5,
              padding: '4px 10px',
              fontSize: 10,
              color: '#94a3b8',
              backdropFilter: 'blur(6px)',
              pointerEvents: 'none',
              display: 'flex',
              alignItems: 'center',
              gap: 6,
            }}
          >
            <span>🔍 <b>{Math.round(zoom * 100)}%</b></span>
            <span>•</span>
            <span>🖱️ Cuộn chuột để Zoom</span>
            <span>•</span>
            <span>✋ Giữ Space để kéo</span>
          </div>
        )}
      </div>
    </div>
  );
};
