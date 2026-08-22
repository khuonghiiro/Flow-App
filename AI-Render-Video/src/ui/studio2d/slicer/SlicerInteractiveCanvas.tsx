import React from 'react';
import { Eye, Layers, ZoomIn } from 'lucide-react';
import { GridCategoryDefinition } from '../../../core/assets/GridSliceRegistry';

interface SlicerInteractiveCanvasProps {
  imageCanvasRef: React.RefObject<HTMLCanvasElement>;
  previewDisplayMode: 'transparent' | 'original';
  setPreviewDisplayMode: (mode: 'transparent' | 'original') => void;
  hasExplicitlySliced: boolean;
  currentCategory: GridCategoryDefinition;
  onMouseDown: (e: React.MouseEvent<HTMLCanvasElement>) => void;
  onDoubleClick: (e: React.MouseEvent<HTMLCanvasElement>) => void;
  onMouseMove: (e: React.MouseEvent<HTMLCanvasElement>) => void;
  onMouseUp: () => void;
}

export const SlicerInteractiveCanvas: React.FC<SlicerInteractiveCanvasProps> = ({
  imageCanvasRef,
  previewDisplayMode,
  setPreviewDisplayMode,
  hasExplicitlySliced,
  currentCategory,
  onMouseDown,
  onDoubleClick,
  onMouseMove,
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
            disabled={!hasExplicitlySliced}
            style={{
              padding: '3px 7px',
              fontSize: 9.5,
              fontWeight: 600,
              borderRadius: 4,
              background: previewDisplayMode === 'transparent' ? '#0284c7' : 'transparent',
              color: previewDisplayMode === 'transparent' ? '#ffffff' : hasExplicitlySliced ? '#94a3b8' : '#475569',
              border: 'none',
              cursor: hasExplicitlySliced ? 'pointer' : 'not-allowed',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            <Layers size={11} /> 🏁 Đã Tách Nền
          </button>

          <button
            onClick={() => setPreviewDisplayMode('original')}
            style={{
              padding: '3px 7px',
              fontSize: 9.5,
              fontWeight: 600,
              borderRadius: 4,
              background: previewDisplayMode === 'original' ? '#0284c7' : 'transparent',
              color: previewDisplayMode === 'original' ? '#ffffff' : '#94a3b8',
              border: 'none',
              cursor: 'pointer',
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
          border: '1px solid rgba(255,255,255,0.1)',
          padding: 6,
        }}
      >
        <canvas
          ref={imageCanvasRef}
          onMouseDown={onMouseDown}
          onDoubleClick={onDoubleClick}
          onMouseMove={onMouseMove}
          onMouseUp={onMouseUp}
          onMouseLeave={onMouseUp}
          style={{
            maxWidth: '100%',
            maxHeight: '100%',
            objectFit: 'contain',
            boxShadow: '0 4px 20px rgba(0,0,0,0.6)',
          }}
        />
      </div>
    </div>
  );
};
