import React from 'react';
import { Eye, Layers, ZoomIn } from 'lucide-react';
import { GridCategoryDefinition } from '../../../core/assets/GridSliceRegistry';

interface SlicerInteractiveCanvasProps {
  imageCanvasRef: React.RefObject<HTMLCanvasElement>;
  hasImage?: boolean;
  isEyedropperActive?: boolean;
  eyedropperHoverColor?: { hex: string; r: number; g: number; b: number; x: number; y: number } | null;
  previewDisplayMode: 'transparent' | 'original';
  setPreviewDisplayMode: (mode: 'transparent' | 'original') => void;
  hasExplicitlySliced: boolean;
  currentCategory: GridCategoryDefinition;
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
  eyedropperHoverColor = null,
  previewDisplayMode,
  setPreviewDisplayMode,
  hasExplicitlySliced,
  currentCategory,
  onMouseDown,
  onDoubleClick,
  onMouseMove,
  onMouseLeave,
  onMouseUp,
}) => {
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
      }}
    >
      {/* Top Canvas Bar */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 6 }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
          <Layers size={13} /> Khung Lưới Cắt ({currentCategory.rows} Hàng × {currentCategory.cols} Cột)
        </div>

        {/* Preview Mode Toggle */}
        <div style={{ display: 'flex', gap: 3, background: 'rgba(0,0,0,0.4)', padding: 2, borderRadius: 5 }}>
          <button
            onClick={() => setPreviewDisplayMode('transparent')}
            disabled={!hasExplicitlySliced || !hasImage}
            style={{
              padding: '3px 7px',
              fontSize: 9.5,
              fontWeight: 600,
              borderRadius: 4,
              background: previewDisplayMode === 'transparent' ? '#0284c7' : 'transparent',
              color: previewDisplayMode === 'transparent' ? '#ffffff' : hasExplicitlySliced && hasImage ? '#94a3b8' : '#475569',
              border: 'none',
              cursor: hasExplicitlySliced && hasImage ? 'pointer' : 'not-allowed',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            <Layers size={11} /> 🏁 Đã Tách Nền
          </button>

          <button
            onClick={() => setPreviewDisplayMode('original')}
            disabled={!hasImage}
            style={{
              padding: '3px 7px',
              fontSize: 9.5,
              fontWeight: 600,
              borderRadius: 4,
              background: previewDisplayMode === 'original' && hasImage ? '#0284c7' : 'transparent',
              color: hasImage ? (previewDisplayMode === 'original' ? '#ffffff' : '#94a3b8') : '#475569',
              border: 'none',
              cursor: hasImage ? 'pointer' : 'not-allowed',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            <Eye size={11} /> 👁️ Ảnh Gốc
          </button>
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
              border: '1.5px solid #f59e0b',
              borderRadius: 20,
              padding: '5px 14px',
              color: '#fef08a',
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
            <span>🎯 Chế độ hút màu: Rê chuột lên ảnh và nhấp chuột để chọn mã màu nền</span>
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
