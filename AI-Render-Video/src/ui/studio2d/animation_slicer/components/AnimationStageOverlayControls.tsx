// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React from 'react';
import {
  Play,
  Pause,
  ZoomIn,
  ZoomOut,
  Eraser,
  Sun,
  Moon,
  Square,
  Undo2,
  Redo2,
  Scissors,
  Crop,
  Check,
  X,
} from 'lucide-react';

export interface AnimationStageOverlayControlsProps {
  checkerTheme: 'dark' | 'light';
  setCheckerTheme: React.Dispatch<React.SetStateAction<'dark' | 'light'>>;
  canUndo: boolean;
  canRedo: boolean;
  onUndo?: () => void;
  onRedo?: () => void;
  onAutoTrimAllBBox?: () => void;
  onionSkinMode: 'off' | 'sequential' | 'all';
  onToggleOnionSkin: () => void;
  showBBox: boolean;
  onToggleShowBBox: () => void;
  zoom: number;
  setZoom: React.Dispatch<React.SetStateAction<number>>;
  setPan: React.Dispatch<React.SetStateAction<{ x: number; y: number }>>;
  activeTool: 'select' | 'eraser' | 'manual_crop';
  setActiveTool: (tool: 'select' | 'eraser' | 'manual_crop') => void;
  brushRadius: number;
  setBrushRadius: (r: number) => void;
  eraseAllFrames: boolean;
  setEraseAllFrames: (all: boolean) => void;
  handleExecuteManualCrop: () => void;
  isPlaying: boolean;
  setIsPlaying: (playing: boolean) => void;
}

export const AnimationStageOverlayControls: React.FC<AnimationStageOverlayControlsProps> = ({
  checkerTheme,
  setCheckerTheme,
  canUndo,
  canRedo,
  onUndo,
  onRedo,
  onAutoTrimAllBBox,
  onionSkinMode,
  onToggleOnionSkin,
  showBBox,
  onToggleShowBBox,
  zoom,
  setZoom,
  setPan,
  activeTool,
  setActiveTool,
  brushRadius,
  setBrushRadius,
  eraseAllFrames,
  setEraseAllFrames,
  handleExecuteManualCrop,
  isPlaying,
  setIsPlaying,
}) => {
  return (
    <>
      {/* Top Floating Action Bar */}
      <div
        style={{
          position: 'absolute',
          top: 10,
          right: 10,
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          background: 'rgba(9, 13, 22, 0.92)',
          backdropFilter: 'blur(14px)',
          border: '1px solid rgba(255, 255, 255, 0.1)',
          borderRadius: 6,
          padding: '4px 8px',
          zIndex: 10,
        }}
      >
        {/* Undo / Redo */}
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
          }}
        >
          <Undo2 size={12} />
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
          }}
        >
          <Redo2 size={12} />
        </button>

        <div style={{ width: 1, height: 12, background: 'rgba(255,255,255,0.1)' }} />

        {/* Auto Trim BBox */}
        {onAutoTrimAllBBox && (
          <button
            onClick={onAutoTrimAllBBox}
            title="Tự động cắt sát viền trong suốt BBox cho toàn bộ frame"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 3,
              padding: '3px 6px',
              borderRadius: 4,
              fontSize: 9.5,
              fontWeight: 700,
              background: 'linear-gradient(135deg, rgba(168, 85, 247, 0.25), rgba(139, 92, 246, 0.15))',
              color: '#c084fc',
              border: '1px solid rgba(168, 85, 247, 0.35)',
              cursor: 'pointer',
            }}
          >
            <Scissors size={10} />
            <span>Tự Cắt BBox</span>
          </button>
        )}

        {/* Theme Toggle */}
        <button
          onClick={() => setCheckerTheme((t) => (t === 'dark' ? 'light' : 'dark'))}
          title="Đổi nền bàn cờ"
          style={{ background: 'none', border: 'none', color: '#cbd5e1', cursor: 'pointer', padding: 2 }}
        >
          {checkerTheme === 'dark' ? <Sun size={12} /> : <Moon size={12} />}
        </button>

        {/* Onion Skin Mode */}
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
          <span>
            {onionSkinMode === 'sequential'
              ? '👻 Tuần tự (K-1)'
              : onionSkinMode === 'all'
              ? '👻 Tất cả'
              : '👻 Tắt'}
          </span>
        </button>

        {/* BBox Toggle */}
        <button
          onClick={onToggleShowBBox}
          title="Bật/tắt khung viền BBox bao quanh nhân vật"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 3,
            padding: '3px 6px',
            borderRadius: 4,
            fontSize: 9.5,
            fontWeight: 600,
            background: showBBox ? 'rgba(56, 189, 248, 0.3)' : 'rgba(255,255,255,0.06)',
            color: showBBox ? '#38bdf8' : '#94a3b8',
            border: showBBox ? '1px solid #38bdf8' : '1px solid transparent',
            cursor: 'pointer',
          }}
        >
          <Square size={11} />
          <span>BBox</span>
        </button>

        {/* Zoom Controls */}
        <button
          onClick={() => setZoom((z) => Math.max(0.3, z * 0.85))}
          title="Thu nhỏ"
          style={{ background: 'none', border: 'none', color: '#cbd5e1', cursor: 'pointer', padding: 2 }}
        >
          <ZoomOut size={12} />
        </button>
        <button
          onClick={() => {
            setZoom(1.0);
            setPan({ x: 0, y: 0 });
          }}
          style={{
            padding: '2px 4px',
            fontSize: 9,
            fontWeight: 700,
            color: '#38bdf8',
            background: 'none',
            border: 'none',
            cursor: 'pointer',
          }}
        >
          {Math.round(zoom * 100)}%
        </button>
        <button
          onClick={() => setZoom((z) => Math.min(3.5, z * 1.15))}
          title="Phóng to"
          style={{ background: 'none', border: 'none', color: '#cbd5e1', cursor: 'pointer', padding: 2 }}
        >
          <ZoomIn size={12} />
        </button>
      </div>

      {/* Floating Tools: Eraser & Manual BBox Crop Bar */}
      <div
        style={{
          position: 'absolute',
          top: 48,
          right: 10,
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          background: 'rgba(9, 13, 22, 0.94)',
          backdropFilter: 'blur(14px)',
          border: '1px solid rgba(255, 255, 255, 0.12)',
          borderRadius: 6,
          padding: '4px 8px',
          zIndex: 10,
        }}
      >
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
          <Eraser size={11} /> {activeTool === 'eraser' ? 'Đang Bật Cọ Tẩy' : 'Cọ Tẩy Pixel'}
        </button>

        <button
          onClick={() => setActiveTool(activeTool === 'manual_crop' ? 'select' : 'manual_crop')}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '3px 8px',
            borderRadius: 4,
            fontSize: 9.5,
            fontWeight: 700,
            background:
              activeTool === 'manual_crop'
                ? 'linear-gradient(135deg, #10b981, #059669)'
                : 'rgba(255,255,255,0.06)',
            color: '#ffffff',
            border: 'none',
            cursor: 'pointer',
            boxShadow: activeTool === 'manual_crop' ? '0 2px 8px rgba(16, 185, 129, 0.4)' : 'none',
          }}
        >
          <Crop size={11} /> {activeTool === 'manual_crop' ? 'Đang Kéo Khung Cắt' : 'Tạo Khung Cắt BBox'}
        </button>

        {activeTool === 'eraser' && (
          <>
            <div style={{ display: 'flex', alignItems: 'center', gap: 3 }}>
              <span style={{ fontSize: 9, color: '#fca5a5' }}>Cỡ: {brushRadius}px</span>
              <input
                type="range"
                min="4"
                max="60"
                value={brushRadius}
                onChange={(e) => setBrushRadius(parseInt(e.target.value))}
                style={{ width: 60, accentColor: '#ef4444' }}
              />
            </div>

            <button
              onClick={() => setEraseAllFrames(!eraseAllFrames)}
              title="Khi bật: xóa 1 điểm sẽ xóa xuyên suốt TẤT CẢ các frame chồng lên nhau!"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 3,
                padding: '2px 6px',
                borderRadius: 4,
                fontSize: 9,
                fontWeight: 700,
                background: eraseAllFrames ? 'rgba(239, 68, 68, 0.3)' : 'rgba(255,255,255,0.05)',
                border: eraseAllFrames ? '1px solid #ef4444' : '1px solid rgba(255,255,255,0.1)',
                color: eraseAllFrames ? '#fca5a5' : '#94a3b8',
                cursor: 'pointer',
              }}
            >
              <Square size={10} />
              <span>{eraseAllFrames ? 'Xóa TẤT CẢ Frame' : 'Chỉ Frame Này'}</span>
            </button>
          </>
        )}

        {/* Manual Crop Action Controls */}
        {activeTool === 'manual_crop' && (
          <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <button
              onClick={handleExecuteManualCrop}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '3px 8px',
                borderRadius: 4,
                fontSize: 9.5,
                fontWeight: 700,
                background: 'linear-gradient(135deg, #10b981, #059669)',
                color: '#ffffff',
                border: 'none',
                cursor: 'pointer',
                boxShadow: '0 2px 8px rgba(16, 185, 129, 0.4)',
              }}
            >
              <Check size={11} /> Cắt BBox Này Cho TẤT CẢ Frame
            </button>

            <button
              onClick={() => setActiveTool('select')}
              style={{
                display: 'flex',
                alignItems: 'center',
                padding: '3px',
                borderRadius: 4,
                background: 'rgba(255,255,255,0.05)',
                border: '1px solid rgba(255,255,255,0.1)',
                color: '#cbd5e1',
                cursor: 'pointer',
              }}
            >
              <X size={11} />
            </button>
          </div>
        )}
      </div>

      {/* Bottom Center Floating Playback Bar */}
      <div
        style={{
          position: 'absolute',
          bottom: 12,
          left: '50%',
          transform: 'translateX(-50%)',
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          background: 'rgba(9, 13, 22, 0.94)',
          backdropFilter: 'blur(14px)',
          border: '1px solid rgba(56, 189, 248, 0.3)',
          borderRadius: 20,
          padding: '4px 10px',
          boxShadow: '0 8px 24px rgba(0, 0, 0, 0.6)',
          zIndex: 10,
        }}
      >
        <button
          onClick={() => setIsPlaying(!isPlaying)}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '4px 10px',
            borderRadius: 14,
            background: isPlaying ? 'rgba(56, 189, 248, 0.3)' : 'rgba(74, 222, 128, 0.25)',
            border: isPlaying ? '1px solid #38bdf8' : '1px solid #4ade80',
            color: isPlaying ? '#38bdf8' : '#4ade80',
            fontSize: 10,
            fontWeight: 700,
            cursor: 'pointer',
          }}
        >
          {isPlaying ? <Pause size={11} /> : <Play size={11} />}
          <span>{isPlaying ? 'Tạm Dừng' : 'Phát'}</span>
        </button>
      </div>
    </>
  );
};
