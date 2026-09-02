// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Slicer Stage Overlay Controls (Pixel Eraser Gated by Peeling, Live Batch Progress)
// =========================================================================================
import React from 'react';
import {
  ZoomIn,
  ZoomOut,
  Maximize2,
  Eraser,
  Sun,
  Moon,
  Undo2,
  Redo2,
  Layers,
  Sparkles,
  RotateCcw,
  Loader2,
} from 'lucide-react';

export interface VideoSlicerStageOverlayControlsProps {
  viewMode: 'video' | 'frames';
  checkerTheme: 'dark' | 'light';
  setCheckerTheme: React.Dispatch<React.SetStateAction<'dark' | 'light'>>;
  onionSkinMode: 'off' | 'sequential' | 'all';
  onToggleOnionSkin: () => void;
  zoom: number;
  setZoom: React.Dispatch<React.SetStateAction<number>>;
  onResetZoom: () => void;
  activeTool: 'select' | 'eraser';
  setActiveTool: (tool: 'select' | 'eraser') => void;
  brushRadius: number;
  setBrushRadius: (r: number) => void;
  canUndo: boolean;
  canRedo: boolean;
  onUndo: () => void;
  onRedo: () => void;
  totalFramesCount: number;
  hasEraseMask: boolean;
  isApplyingBatch: boolean;
  batchProgress?: { current: number; total: number; percent: number } | null;
  hasPeeledBackground: boolean;
  onApplyEraseToAllFrames: () => void;
  onClearEraseMask: () => void;
}

export const VideoSlicerStageOverlayControls: React.FC<VideoSlicerStageOverlayControlsProps> = ({
  viewMode,
  checkerTheme,
  setCheckerTheme,
  onionSkinMode,
  onToggleOnionSkin,
  zoom,
  setZoom,
  onResetZoom,
  activeTool,
  setActiveTool,
  brushRadius,
  setBrushRadius,
  canUndo,
  canRedo,
  onUndo,
  onRedo,
  totalFramesCount,
  hasEraseMask,
  isApplyingBatch,
  batchProgress,
  hasPeeledBackground,
  onApplyEraseToAllFrames,
  onClearEraseMask,
}) => {
  return (
    <>
      {/* ─── Top Floating Right Toolbar (Undo/Redo, Theme, Onion Skin, Zoom) ─── */}
      <div
        style={{
          position: 'absolute',
          top: 10,
          right: 10,
          zIndex: 20,
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          background: 'rgba(9, 13, 22, 0.92)',
          backdropFilter: 'blur(14px)',
          border: '1px solid rgba(255, 255, 255, 0.1)',
          borderRadius: 6,
          padding: '4px 8px',
        }}
      >
        {/* Undo / Redo Buttons (Frames mode only) */}
        {viewMode === 'frames' && (
          <>
            <button
              onClick={onUndo}
              disabled={!canUndo}
              title="Hoàn tác (Ctrl+Z)"
              style={{
                background: 'none',
                border: 'none',
                color: canUndo ? '#f8fafc' : '#475569',
                cursor: canUndo ? 'pointer' : 'not-allowed',
                padding: 3,
                display: 'flex',
                alignItems: 'center',
                borderRadius: 4,
              }}
            >
              <Undo2 size={13} />
            </button>

            <button
              onClick={onRedo}
              disabled={!canRedo}
              title="Làm lại (Ctrl+Y / Shift+Ctrl+Z)"
              style={{
                background: 'none',
                border: 'none',
                color: canRedo ? '#f8fafc' : '#475569',
                cursor: canRedo ? 'pointer' : 'not-allowed',
                padding: 3,
                display: 'flex',
                alignItems: 'center',
                borderRadius: 4,
              }}
            >
              <Redo2 size={13} />
            </button>

            <div style={{ width: 1, height: 14, background: 'rgba(255,255,255,0.12)' }} />
          </>
        )}

        {/* Theme Toggle */}
        <button
          onClick={() => setCheckerTheme((t) => (t === 'dark' ? 'light' : 'dark'))}
          title="Đổi nền bàn cờ (Sáng / Tối)"
          style={{
            background: 'none',
            border: 'none',
            color: '#cbd5e1',
            cursor: 'pointer',
            padding: 3,
            display: 'flex',
            alignItems: 'center',
          }}
        >
          {checkerTheme === 'dark' ? <Sun size={13} /> : <Moon size={13} />}
        </button>

        {/* Onion Skin Mode Toggle (Frames mode only) */}
        {viewMode === 'frames' && (
          <button
            onClick={onToggleOnionSkin}
            title="Chế độ bóng ma (Tuần tự K-1 / Tất cả / Tắt)"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '3px 6px',
              borderRadius: 4,
              fontSize: 9.5,
              fontWeight: 600,
              background:
                onionSkinMode === 'sequential'
                  ? 'rgba(56, 189, 248, 0.25)'
                  : onionSkinMode === 'all'
                  ? 'rgba(168, 85, 247, 0.25)'
                  : 'rgba(255,255,255,0.06)',
              color:
                onionSkinMode === 'sequential'
                  ? '#38bdf8'
                  : onionSkinMode === 'all'
                  ? '#c084fc'
                  : '#94a3b8',
              border:
                onionSkinMode !== 'off'
                  ? '1px solid currentColor'
                  : '1px solid transparent',
              cursor: 'pointer',
            }}
          >
            <Layers size={11} />
            <span>
              {onionSkinMode === 'sequential'
                ? '👻 K-1'
                : onionSkinMode === 'all'
                ? '👻 Tất cả'
                : '👻 Tắt'}
            </span>
          </button>
        )}

        <div style={{ width: 1, height: 14, background: 'rgba(255,255,255,0.12)' }} />

        {/* Zoom Controls */}
        <button
          onClick={() => setZoom((z) => Math.max(0.3, z * 0.85))}
          title="Thu nhỏ"
          style={{ background: 'none', border: 'none', color: '#cbd5e1', cursor: 'pointer', padding: 2 }}
        >
          <ZoomOut size={13} />
        </button>
        <span
          onClick={onResetZoom}
          title="Bấm để đặt lại 100%"
          style={{
            padding: '2px 4px',
            fontSize: 9.5,
            fontWeight: 700,
            color: '#38bdf8',
            cursor: 'pointer',
          }}
        >
          {Math.round(zoom * 100)}%
        </span>
        <button
          onClick={() => setZoom((z) => Math.min(3.5, z * 1.15))}
          title="Phóng to"
          style={{ background: 'none', border: 'none', color: '#cbd5e1', cursor: 'pointer', padding: 2 }}
        >
          <ZoomIn size={13} />
        </button>
        <button
          onClick={onResetZoom}
          title="Vừa khung hình"
          style={{ background: 'none', border: 'none', color: '#cbd5e1', cursor: 'pointer', padding: 2 }}
        >
          <Maximize2 size={13} />
        </button>
      </div>

      {/* ─── Floating Tool: Pixel Eraser Bar (ONLY visible AFTER background is peeled) ─── */}
      {viewMode === 'frames' && hasPeeledBackground && (
        <div
          style={{
            position: 'absolute',
            top: 48,
            right: 10,
            zIndex: 20,
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            background: 'rgba(9, 13, 22, 0.94)',
            backdropFilter: 'blur(14px)',
            border: '1px solid rgba(255, 255, 255, 0.12)',
            borderRadius: 6,
            padding: '4px 8px',
            boxShadow: '0 8px 24px rgba(0, 0, 0, 0.6)',
          }}
        >
          {/* Toggle Eraser Tool */}
          <button
            onClick={() => setActiveTool(activeTool === 'eraser' ? 'select' : 'eraser')}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '3px 8px',
              borderRadius: 4,
              fontSize: 9.5,
              fontWeight: 700,
              background:
                activeTool === 'eraser'
                  ? 'linear-gradient(135deg, #ef4444, #dc2626)'
                  : 'rgba(255,255,255,0.06)',
              color: '#ffffff',
              border: 'none',
              cursor: 'pointer',
              boxShadow: activeTool === 'eraser' ? '0 2px 8px rgba(239, 68, 68, 0.4)' : 'none',
            }}
          >
            <Eraser size={11} /> {activeTool === 'eraser' ? 'Đang Bật Cọ Tẩy (E)' : 'Cọ Tẩy Pixel (E)'}
          </button>

          {activeTool === 'eraser' && (
            <>
              {/* Brush Size Slider */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, marginLeft: 2 }}>
                <span style={{ fontSize: 9, color: '#fca5a5', fontWeight: 600 }}>Cỡ: {brushRadius}px</span>
                <input
                  type="range"
                  min="4"
                  max="80"
                  value={brushRadius}
                  onChange={(e) => setBrushRadius(parseInt(e.target.value))}
                  style={{ width: 55, accentColor: '#ef4444', cursor: 'pointer' }}
                />
              </div>

              <div style={{ width: 1, height: 14, background: 'rgba(255,255,255,0.12)', margin: '0 2px' }} />

              {/* Batch Apply Button: Shows Live Count Processing */}
              <button
                onClick={onApplyEraseToAllFrames}
                disabled={!hasEraseMask || isApplyingBatch}
                title="Áp dụng vết tẩy vừa quét này đồng loạt cho TOÀN BỘ các frame trong chuỗi video"
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 5,
                  padding: '4px 9px',
                  borderRadius: 4,
                  fontSize: 9.5,
                  fontWeight: 700,
                  background: isApplyingBatch
                    ? 'linear-gradient(135deg, rgba(168, 85, 247, 0.4), rgba(56, 189, 248, 0.4))'
                    : hasEraseMask
                    ? 'linear-gradient(135deg, #a855f7, #7c3aed)'
                    : 'rgba(255,255,255,0.05)',
                  border: hasEraseMask ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.1)',
                  color: isApplyingBatch ? '#38bdf8' : hasEraseMask ? '#ffffff' : '#64748b',
                  cursor: hasEraseMask && !isApplyingBatch ? 'pointer' : 'not-allowed',
                  boxShadow: hasEraseMask ? '0 2px 10px rgba(168, 85, 247, 0.4)' : 'none',
                }}
              >
                {isApplyingBatch ? (
                  <Loader2 size={11} className="spin" color="#38bdf8" />
                ) : (
                  <Sparkles size={11} />
                )}
                <span>
                  {isApplyingBatch && batchProgress
                    ? `Đang tẩy: ${batchProgress.current}/${batchProgress.total} (${batchProgress.percent}%)`
                    : isApplyingBatch
                    ? 'Đang xử lý...'
                    : `✨ Áp Dụng Cho TẤT CẢ Frame (${totalFramesCount})`}
                </span>
              </button>

              {/* Clear Mask Button */}
              {hasEraseMask && !isApplyingBatch && (
                <button
                  onClick={onClearEraseMask}
                  title="Xóa vùng nhớ vệt cọ vừa quét"
                  style={{
                    background: 'none',
                    border: 'none',
                    color: '#94a3b8',
                    cursor: 'pointer',
                    padding: 3,
                    display: 'flex',
                    alignItems: 'center',
                  }}
                >
                  <RotateCcw size={11} />
                </button>
              )}
            </>
          )}
        </div>
      )}

      {/* ─── FLOATING LIVE BATCH PROGRESS CARD (CENTER TOP) ─── */}
      {isApplyingBatch && batchProgress && (
        <div
          style={{
            position: 'absolute',
            top: 96,
            left: '50%',
            transform: 'translateX(-50%)',
            background: 'rgba(15, 23, 42, 0.95)',
            backdropFilter: 'blur(16px)',
            border: '1px solid rgba(168, 85, 247, 0.5)',
            borderRadius: 8,
            padding: '10px 18px',
            display: 'flex',
            flexDirection: 'column',
            gap: 7,
            boxShadow: '0 12px 36px rgba(0, 0, 0, 0.8), 0 0 20px rgba(168, 85, 247, 0.35)',
            zIndex: 100,
            minWidth: 280,
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 11, fontWeight: 700, color: '#f8fafc' }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <Loader2 size={13} className="spin" color="#c084fc" />
              Đang Tẩy Pixel Hàng Loạt...
            </span>
            <span style={{ color: '#38bdf8', fontWeight: 800 }}>
              {batchProgress.current}/{batchProgress.total} ({batchProgress.percent}%)
            </span>
          </div>

          {/* Glowing Animated Progress Bar */}
          <div style={{ width: '100%', height: 6, background: 'rgba(255, 255, 255, 0.1)', borderRadius: 3, overflow: 'hidden' }}>
            <div
              style={{
                width: `${batchProgress.percent}%`,
                height: '100%',
                background: 'linear-gradient(90deg, #a855f7, #38bdf8)',
                transition: 'width 0.12s ease-out',
                borderRadius: 3,
                boxShadow: '0 0 8px rgba(56, 189, 248, 0.6)',
              }}
            />
          </div>
        </div>
      )}
    </>
  );
};
