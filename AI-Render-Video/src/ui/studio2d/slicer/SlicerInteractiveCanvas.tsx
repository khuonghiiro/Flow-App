import React from 'react';
import { Eye, Layers, ZoomIn, Target, Grid } from 'lucide-react';
import { GridCategoryDefinition } from '../../../core/assets/GridSliceRegistry';

interface SlicerInteractiveCanvasProps {
  imageCanvasRef: React.RefObject<HTMLCanvasElement>;
  hasImage?: boolean;
  isEyedropperActive?: boolean;
  eyedropperTarget?: 'chroma' | 'fringe';
  eyedropperHoverColor?: { hex: string; r: number; g: number; b: number; x: number; y: number } | null;
  previewDisplayMode: 'transparent' | 'original';
  setPreviewDisplayMode: (mode: 'transparent' | 'original') => void;
  onTogglePreviewDisplayMode?: (mode: 'transparent' | 'original') => void;
  hasExplicitlySliced: boolean;
  currentCategory: GridCategoryDefinition;
  onAutoFitGrid?: () => void;
  onResetUniformGrid?: () => void;
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
  onMouseDown,
  onDoubleClick,
  onMouseMove,
  onMouseLeave,
  onMouseUp,
}) => {
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

      {/* Canvas Display */}
      <div
        style={{
          flex: 1,
          position: 'relative',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          overflow: 'auto',
          background: '#090d16',
          borderRadius: 6,
          border: isEyedropperActive ? '1.5px solid #f59e0b' : '1px dashed rgba(255,255,255,0.1)',
          padding: 6,
          boxShadow: isEyedropperActive ? 'inset 0 0 20px rgba(245,158,11,0.15)' : 'none',
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
              border: eyedropperTarget === 'fringe' ? '1.5px solid #10b981' : '1.5px solid #f59e0b',
              borderRadius: 20,
              padding: '5px 14px',
              color: eyedropperTarget === 'fringe' ? '#a7f3d0' : '#fef08a',
              fontSize: 10.5,
              fontWeight: 600,
              boxShadow: '0 4px 20px rgba(0,0,0,0.7), 0 0 12px rgba(245,158,11,0.3)',
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              zIndex: 30,
              pointerEvents: 'none',
              fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
            }}
          >
            <span>
              {eyedropperTarget === 'fringe'
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
            {/* Photoshop Dual Ring Swatch */}
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

        <canvas
          ref={imageCanvasRef}
          onMouseDown={onMouseDown}
          onDoubleClick={onDoubleClick}
          onMouseMove={onMouseMove}
          onMouseUp={onMouseUp}
          onMouseLeave={onMouseLeave || onMouseUp}
          style={{
            display: hasImage ? 'block' : 'none',
            maxWidth: '100%',
            maxHeight: '100%',
            objectFit: 'contain',
            boxShadow: '0 4px 20px rgba(0,0,0,0.6)',
            cursor: isEyedropperActive ? 'crosshair' : 'default',
          }}
        />
      </div>
    </div>
  );
};
