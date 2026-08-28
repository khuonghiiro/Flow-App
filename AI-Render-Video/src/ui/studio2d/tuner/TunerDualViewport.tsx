import React from 'react';
import { Box, Image as ImageIcon, Columns, Layers } from 'lucide-react';
import { AngleMenuItem } from './TunerAngleSidebar';

export type ViewportSyncMode = 'split_2d_3d' | '3d_only' | '2d_only';
export type Canvas2DCompositeMode = 'composite' | 'isolated';

interface TunerDualViewportProps {
  viewportMode: ViewportSyncMode;
  setViewportMode: (mode: ViewportSyncMode) => void;
  canvas2DMode: Canvas2DCompositeMode;
  setCanvas2DMode: (mode: Canvas2DCompositeMode) => void;
  currentAngleInfo?: AngleMenuItem;
  modal2DCanvasRef: React.RefObject<HTMLCanvasElement>;
  modalThreeContainerRef: React.RefObject<HTMLDivElement>;
  selectedSlotLabel: string;
}

export const TunerDualViewport: React.FC<TunerDualViewportProps> = ({
  viewportMode,
  setViewportMode,
  canvas2DMode,
  setCanvas2DMode,
  currentAngleInfo,
  modal2DCanvasRef,
  modalThreeContainerRef,
  selectedSlotLabel,
}) => {
  return (
    <div
      style={{
        padding: 14,
        background: 'rgba(5, 8, 18, 0.95)',
        borderRight: '1px solid rgba(255, 255, 255, 0.08)',
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        overflow: 'hidden',
        minHeight: 0,
      }}
    >
      {/* Viewport Top Bar */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 6 }}>
        <div style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Box size={15} /> XEM TRƯỚC: {currentAngleInfo?.label}
        </div>

        <div style={{ display: 'flex', gap: 6 }}>
          {/* 2D Mode Switcher (Composite vs Isolated) */}
          <div style={{ display: 'flex', gap: 2, background: 'rgba(0,0,0,0.6)', padding: 2, borderRadius: 5, border: '1px solid rgba(255,255,255,0.1)' }}>
            <button
              onClick={() => setCanvas2DMode('composite')}
              style={{
                padding: '3px 7px',
                fontSize: 9.5,
                fontWeight: 700,
                borderRadius: 4,
                border: 'none',
                background: canvas2DMode === 'composite' ? '#38bdf8' : 'transparent',
                color: canvas2DMode === 'composite' ? '#090d16' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 3,
              }}
              title="Ghép đầy đủ các lớp: Tóc sau + Đỉnh đầu + Tóc mai + Mái trước"
            >
              <Layers size={11} /> Ghép Toàn Bộ Tóc
            </button>
            <button
              onClick={() => setCanvas2DMode('isolated')}
              style={{
                padding: '3px 7px',
                fontSize: 9.5,
                fontWeight: 700,
                borderRadius: 4,
                border: 'none',
                background: canvas2DMode === 'isolated' ? '#38bdf8' : 'transparent',
                color: canvas2DMode === 'isolated' ? '#090d16' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 3,
              }}
              title="Chỉ hiển thị riêng một lớp tóc đang chọn"
            >
              🔍 Soi Riêng Lớp
            </button>
          </div>

          {/* Viewport Layout Mode (Split vs 2D vs 3D) */}
          <div style={{ display: 'flex', gap: 2, background: 'rgba(0,0,0,0.6)', padding: 2, borderRadius: 5, border: '1px solid rgba(255,255,255,0.1)' }}>
            <button
              onClick={() => setViewportMode('split_2d_3d')}
              style={{
                padding: '3px 7px',
                fontSize: 9.5,
                fontWeight: 700,
                borderRadius: 4,
                border: 'none',
                background: viewportMode === 'split_2d_3d' ? '#0284c7' : 'transparent',
                color: viewportMode === 'split_2d_3d' ? '#fff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 3,
              }}
              title="Hiển thị song song 2D và 3D"
            >
              <Columns size={11} /> Song Song 2D | 3D
            </button>
            <button
              onClick={() => setViewportMode('2d_only')}
              style={{
                padding: '3px 7px',
                fontSize: 9.5,
                fontWeight: 700,
                borderRadius: 4,
                border: 'none',
                background: viewportMode === '2d_only' ? '#0284c7' : 'transparent',
                color: viewportMode === '2d_only' ? '#fff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 3,
              }}
            >
              <ImageIcon size={11} /> 2D
            </button>
            <button
              onClick={() => setViewportMode('3d_only')}
              style={{
                padding: '3px 7px',
                fontSize: 9.5,
                fontWeight: 700,
                borderRadius: 4,
                border: 'none',
                background: viewportMode === '3d_only' ? '#0284c7' : 'transparent',
                color: viewportMode === '3d_only' ? '#fff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 3,
              }}
            >
              <Box size={11} /> 3D
            </button>
          </div>
        </div>
      </div>

      {/* Center Display: Dual Grid or Full Viewport */}
      <div style={{ flex: 1, display: 'flex', gap: 10, minHeight: 0 }}>
        {/* 2D Canvas View */}
        {(viewportMode === '2d_only' || viewportMode === 'split_2d_3d') && (
          <div
            style={{
              flex: 1,
              display: 'flex',
              flexDirection: 'column',
              background: '#040711',
              borderRadius: 10,
              overflow: 'hidden',
              border: '1px solid rgba(56, 189, 248, 0.25)',
              position: 'relative',
            }}
          >
            <div style={{ position: 'absolute', top: 8, left: 10, fontSize: 10, fontWeight: 700, color: '#38bdf8', background: 'rgba(0,0,0,0.75)', padding: '3px 8px', borderRadius: 4, zIndex: 10 }}>
              {canvas2DMode === 'composite' ? '🎭 2D Ghép Lớp Tóc' : `🔍 2D Riêng: ${selectedSlotLabel}`}
            </div>
            <canvas
              ref={modal2DCanvasRef}
              width={500}
              height={500}
              style={{ width: '100%', height: '100%', objectFit: 'contain' }}
            />
          </div>
        )}

        {/* 3D WebGL Billboard View */}
        {(viewportMode === '3d_only' || viewportMode === 'split_2d_3d') && (
          <div
            style={{
              flex: 1,
              display: 'flex',
              flexDirection: 'column',
              background: '#040711',
              borderRadius: 10,
              overflow: 'hidden',
              border: '1px solid rgba(56, 189, 248, 0.4)',
              position: 'relative',
            }}
          >
            <div style={{ position: 'absolute', top: 8, left: 10, fontSize: 10, fontWeight: 700, color: '#4ade80', background: 'rgba(0,0,0,0.75)', padding: '3px 8px', borderRadius: 4, zIndex: 10 }}>
              🌟 Nhân Vật 3D Billboard ({currentAngleInfo?.label})
            </div>
            <div
              ref={modalThreeContainerRef}
              style={{ width: '100%', height: '100%' }}
            />
          </div>
        )}
      </div>
    </div>
  );
};
