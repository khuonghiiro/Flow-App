// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React from 'react';
import {
  Plus,
  Trash2,
  ChevronLeft,
  ChevronRight,
  Repeat,
  Layers,
} from 'lucide-react';
import { AnimationSliceFrame } from '../../../../types/animation_slicer';

interface AnimationFrameFilmstripBarProps {
  frames: AnimationSliceFrame[];
  frameOrder: number[];
  selectedFrameIndex: number | null;
  loopMode: 'loop' | 'ping_pong' | 'once';
  onionSkinMode: 'off' | 'sequential' | 'all';
  onSelectFrameIndex: (index: number) => void;
  onUpdateFrameOrder: (order: number[]) => void;
  onUpdateLoopMode: (mode: 'loop' | 'ping_pong' | 'once') => void;
  onToggleOnionSkinMode: () => void;
  onDuplicateFrame: (index: number) => void;
  onDeleteFrame: (index: number) => void;
  onMoveFrame: (index: number, direction: 'left' | 'right') => void;
}

export const AnimationFrameFilmstripBar: React.FC<AnimationFrameFilmstripBarProps> = ({
  frames,
  frameOrder,
  selectedFrameIndex,
  loopMode,
  onionSkinMode,
  onSelectFrameIndex,
  onUpdateFrameOrder,
  onUpdateLoopMode,
  onToggleOnionSkinMode,
  onDuplicateFrame,
  onDeleteFrame,
  onMoveFrame,
}) => {
  const totalDurationMs = frameOrder.reduce((sum, fIdx) => {
    const f = frames[fIdx];
    return sum + (f?.durationMs || 500);
  }, 0);

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 5,
        background: 'rgba(9, 13, 22, 0.95)',
        border: '1px solid rgba(56, 189, 248, 0.25)',
        borderRadius: 8,
        padding: '6px 8px',
        height: '100%',
        overflow: 'hidden',
      }}
    >
      {/* Top Filmstrip Header & Sequence Order Presets */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexShrink: 0, height: 26 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: 10.5, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
            <Layers size={12} /> CHUỖI FRAME HOẠT ẢNH ({frames.length} frames):
          </span>
          <span style={{ fontSize: 9.5, color: '#facc15', fontWeight: 600 }}>
            ⏱️ Tổng: {(totalDurationMs / 1000).toFixed(2)}s
          </span>
        </div>

        {/* Quick Sequence & Onion Skin Buttons */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
          {/* Onion Skin Mode Switcher */}
          <button
            onClick={onToggleOnionSkinMode}
            title="Chuyển chế độ Bóng ma: Bóng Ma Tuần Tự (F-1) -> Chồng Tất Cả -> Tắt Bóng Ma"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '2px 7px',
              borderRadius: 4,
              fontSize: 9.5,
              fontWeight: 700,
              background: onionSkinMode === 'sequential'
                ? 'linear-gradient(135deg, #0284c7, #38bdf8)'
                : onionSkinMode === 'all'
                ? 'rgba(168, 85, 247, 0.35)'
                : 'rgba(255,255,255,0.06)',
              color: onionSkinMode === 'sequential' ? '#ffffff' : onionSkinMode === 'all' ? '#c084fc' : '#94a3b8',
              border: onionSkinMode !== 'off' ? '1px solid currentColor' : '1px solid rgba(255,255,255,0.1)',
              cursor: 'pointer',
              boxShadow: onionSkinMode === 'sequential' ? '0 2px 8px rgba(56, 189, 248, 0.4)' : 'none',
            }}
          >
            <Layers size={10} />
            <span>
              {onionSkinMode === 'sequential'
                ? '👻 Bóng Ma F-1'
                : onionSkinMode === 'all'
                ? '👻 Chồng Tất Cả'
                : 'Tắt Bóng Ma'}
            </span>
          </button>

          {/* Loop Mode Switcher */}
          <button
            onClick={() => onUpdateLoopMode(loopMode === 'loop' ? 'ping_pong' : 'loop')}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 3,
              padding: '2px 7px',
              borderRadius: 4,
              fontSize: 9.5,
              fontWeight: 600,
              background: loopMode === 'ping_pong' ? 'rgba(168, 85, 247, 0.3)' : 'rgba(56, 189, 248, 0.2)',
              border: loopMode === 'ping_pong' ? '1px solid #c084fc' : '1px solid #38bdf8',
              color: loopMode === 'ping_pong' ? '#c084fc' : '#38bdf8',
              cursor: 'pointer',
            }}
          >
            <Repeat size={10} /> {loopMode === 'ping_pong' ? 'Ping-Pong' : 'Lặp Tuần Hoàn'}
          </button>

          <button
            onClick={() => onUpdateFrameOrder(frames.map((_, i) => i))}
            title="Khôi phục thứ tự 1 -> 2 -> 3 -> 4"
            style={{
              padding: '2px 6px',
              borderRadius: 4,
              fontSize: 9,
              background: 'rgba(255,255,255,0.05)',
              border: '1px solid rgba(255,255,255,0.1)',
              color: '#cbd5e1',
              cursor: 'pointer',
            }}
          >
            Thứ tự 1..N
          </button>

          <button
            onClick={() => {
              const forward = frames.map((_, i) => i);
              const backward = [...forward].reverse().slice(1, -1);
              onUpdateFrameOrder([...forward, ...backward]);
            }}
            title="Chuỗi Ping-Pong (1->2->3->4->3->2)"
            style={{
              padding: '2px 6px',
              borderRadius: 4,
              fontSize: 9,
              background: 'rgba(168, 85, 247, 0.15)',
              border: '1px solid rgba(168, 85, 247, 0.3)',
              color: '#c084fc',
              cursor: 'pointer',
            }}
          >
            Ping-Pong
          </button>
        </div>
      </div>

      {/* Horizontal Filmstrip Cards List */}
      <div
        style={{
          flex: 1,
          display: 'flex',
          gap: 6,
          overflowX: 'auto',
          overflowY: 'hidden',
          padding: '2px 4px',
          background: 'rgba(2, 6, 23, 0.6)',
          borderRadius: 6,
          border: '1px solid rgba(255,255,255,0.05)',
          alignItems: 'stretch',
        }}
      >
        {frames.map((f, idx) => {
          const isSelected = selectedFrameIndex === idx;
          const fDurSec = f.durationMs ? (f.durationMs / 1000).toFixed(2) : '0.50';

          return (
            <div
              key={f.id || idx}
              onClick={() => onSelectFrameIndex(idx)}
              style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: 2,
                padding: '3px 4px',
                borderRadius: 6,
                background: isSelected ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.02)',
                border: isSelected
                  ? '2px solid #38bdf8'
                  : '1px solid rgba(255,255,255,0.08)',
                cursor: 'pointer',
                minWidth: 84,
                width: 84,
                flexShrink: 0,
                transition: 'all 0.12s ease',
                position: 'relative',
              }}
            >
              {/* Top Badge: Frame Number & Duration */}
              <div style={{ display: 'flex', justifyContent: 'space-between', width: '100%', fontSize: 9, fontWeight: 700, padding: '0 2px' }}>
                <span style={{ color: isSelected ? '#38bdf8' : '#cbd5e1' }}>F{idx + 1}</span>
                <span style={{ color: '#4ade80' }}>⏱️ {fDurSec}s</span>
              </div>

              {/* Thumbnail with checkerboard background */}
              <div
                style={{
                  width: 74,
                  height: 56,
                  background:
                    'repeating-conic-gradient(#1e293b 0% 25%, #0f172a 0% 50%) 50% / 10px 10px',
                  borderRadius: 4,
                  overflow: 'hidden',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  border: '1px solid rgba(255,255,255,0.06)',
                }}
              >
                <img
                  src={f.transparentDataUrl || f.originalDataUrl}
                  alt={`Frame ${idx + 1}`}
                  style={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain' }}
                />
              </div>

              {/* Quick Actions (Move Left, Clone, Delete, Move Right) */}
              <div style={{ display: 'flex', gap: 2, width: '100%', justifyContent: 'space-between', marginTop: 1 }}>
                <button
                  onClick={(e) => { e.stopPropagation(); onMoveFrame(idx, 'left'); }}
                  disabled={idx === 0}
                  title="Di chuyển sang trái"
                  style={{
                    flex: 1,
                    height: 18,
                    background: 'rgba(255,255,255,0.06)',
                    border: 'none',
                    color: idx === 0 ? '#475569' : '#94a3b8',
                    borderRadius: 3,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    cursor: idx === 0 ? 'not-allowed' : 'pointer',
                  }}
                >
                  <ChevronLeft size={10} />
                </button>

                <button
                  onClick={(e) => { e.stopPropagation(); onDuplicateFrame(idx); }}
                  title="Nhân bản frame này"
                  style={{
                    flex: 1,
                    height: 18,
                    background: 'rgba(56, 189, 248, 0.15)',
                    border: 'none',
                    color: '#38bdf8',
                    borderRadius: 3,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    cursor: 'pointer',
                  }}
                >
                  <Plus size={10} />
                </button>

                <button
                  onClick={(e) => { e.stopPropagation(); onDeleteFrame(idx); }}
                  disabled={frames.length <= 1}
                  title="Xóa frame"
                  style={{
                    flex: 1,
                    height: 18,
                    background: 'rgba(239, 68, 68, 0.15)',
                    border: 'none',
                    color: frames.length <= 1 ? '#475569' : '#f87171',
                    borderRadius: 3,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    cursor: frames.length <= 1 ? 'not-allowed' : 'pointer',
                  }}
                >
                  <Trash2 size={10} />
                </button>

                <button
                  onClick={(e) => { e.stopPropagation(); onMoveFrame(idx, 'right'); }}
                  disabled={idx === frames.length - 1}
                  title="Di chuyển sang phải"
                  style={{
                    flex: 1,
                    height: 18,
                    background: 'rgba(255,255,255,0.06)',
                    border: 'none',
                    color: idx === frames.length - 1 ? '#475569' : '#94a3b8',
                    borderRadius: 3,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    cursor: idx === frames.length - 1 ? 'not-allowed' : 'pointer',
                  }}
                >
                  <ChevronRight size={10} />
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
