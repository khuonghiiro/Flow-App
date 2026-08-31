// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Slicer Right Properties Panel (Added Visibility Toggle & Removed Duplicate)
// =========================================================================================
import React from 'react';
import {
  Clock,
  RotateCw,
  Maximize,
  Move,
  FlipHorizontal,
  Trash2,
  Sliders,
  Eye,
  EyeOff,
} from 'lucide-react';
import { VideoSliceFrame } from '../../../../types/video_slicer';

export interface VideoSlicerPropertiesPanelProps {
  selectedFrame: VideoSliceFrame | null;
  selectedFrameIndex: number | null;
  totalFramesCount: number;
  onUpdateFrameTransform: (
    index: number,
    updates: Partial<Pick<VideoSliceFrame, 'offsetX' | 'offsetY' | 'scale' | 'rotation' | 'flipX' | 'durationMs'>>
  ) => void;
  onApplyTransformToAll: (sourceIndex: number) => void;
  onSetAllDuration: (durationMs: number) => void;
  onToggleHideFrame: (index: number) => void;
  onDeleteFrame: (index: number) => void;
}

export const VideoSlicerPropertiesPanel: React.FC<VideoSlicerPropertiesPanelProps> = ({
  selectedFrame,
  selectedFrameIndex,
  totalFramesCount,
  onUpdateFrameTransform,
  onApplyTransformToAll,
  onSetAllDuration,
  onToggleHideFrame,
  onDeleteFrame,
}) => {
  if (!selectedFrame || selectedFrameIndex === null) {
    return (
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          height: '100%',
          color: '#64748b',
          padding: 16,
          textAlign: 'center',
          fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
        }}
      >
        <Sliders size={32} style={{ marginBottom: 8, opacity: 0.5 }} />
        <div style={{ fontSize: 11, fontWeight: 600 }}>Chưa chọn frame</div>
        <div style={{ fontSize: 9.5, marginTop: 4 }}>Bấm chọn một khung hình ở dải phim bên dưới để tinh chỉnh</div>
      </div>
    );
  }

  const isHidden = !!selectedFrame.hidden;

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        height: '100%',
        minHeight: 0,
        overflowY: 'auto',
        padding: '0 10px 12px 6px',
        boxSizing: 'border-box',
        fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
      }}
    >
      {/* ─── FRAME INFO CARD ─────────────────────────────────── */}
      <div
        style={{
          background: 'rgba(15, 23, 42, 0.7)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          borderRadius: 8,
          padding: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8' }}>
            Frame #{selectedFrameIndex + 1} / {totalFramesCount}
          </div>
          <div
            style={{
              fontSize: 9,
              padding: '1px 6px',
              borderRadius: 4,
              background: isHidden ? 'rgba(239, 68, 68, 0.2)' : 'rgba(16, 185, 129, 0.15)',
              color: isHidden ? '#f87171' : '#34d399',
              border: `1px solid ${isHidden ? 'rgba(239, 68, 68, 0.4)' : 'rgba(16, 185, 129, 0.3)'}`,
            }}
          >
            {isHidden ? 'Đã Ẩn' : 'Đang Bật'}
          </div>
        </div>

        <div style={{ fontSize: 9.5, color: '#94a3b8' }}>
          Mốc thời gian: <span style={{ color: '#fff', fontFamily: 'monospace' }}>{selectedFrame.timestamp.toFixed(3)}s</span>
        </div>
      </div>

      {/* ─── DURATION CARD ───────────────────────────────────── */}
      <div
        style={{
          background: 'rgba(15, 23, 42, 0.7)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          borderRadius: 8,
          padding: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
        }}
      >
        <div style={{ fontSize: 11, fontWeight: 700, color: '#fbbf24', display: 'flex', alignItems: 'center', gap: 5 }}>
          <Clock size={13} />
          Thời Lượng Frame (FPS)
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <input
            type="number"
            min={16}
            max={2000}
            step={10}
            value={selectedFrame.durationMs}
            onChange={(e) =>
              onUpdateFrameTransform(selectedFrameIndex, {
                durationMs: Math.max(16, Number(e.target.value)),
              })
            }
            style={{
              flex: 1,
              background: '#090e1a',
              border: '1px solid rgba(255, 255, 255, 0.15)',
              borderRadius: 4,
              color: '#fbbf24',
              fontSize: 10.5,
              fontWeight: 700,
              padding: '4px 6px',
            }}
          />
          <span style={{ fontSize: 10, color: '#94a3b8' }}>ms</span>
        </div>

        <button
          onClick={() => onSetAllDuration(selectedFrame.durationMs)}
          title="Áp dụng thời lượng ms này cho toàn bộ frame trong chuỗi"
          style={{
            background: 'rgba(251, 191, 36, 0.15)',
            border: '1px solid rgba(251, 191, 36, 0.3)',
            borderRadius: 5,
            color: '#fbbf24',
            padding: '5px 8px',
            fontSize: 9.5,
            fontWeight: 700,
            cursor: 'pointer',
          }}
        >
          ⏱️ Áp Dụng Cho Tất Cả Frame
        </button>
      </div>

      {/* ─── TRANSFORM CARD (OFFSET, SCALE, ROTATION, FLIP) ───── */}
      <div
        style={{
          background: 'rgba(15, 23, 42, 0.7)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          borderRadius: 8,
          padding: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
        }}
      >
        <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
          <Move size={13} />
          Biến Đổi & Căn Chỉnh
        </div>

        {/* Offset X & Y */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>Dịch X: {selectedFrame.offsetX}px</div>
            <input
              type="range"
              min={-150}
              max={150}
              value={selectedFrame.offsetX}
              onChange={(e) =>
                onUpdateFrameTransform(selectedFrameIndex, {
                  offsetX: Number(e.target.value),
                })
              }
              style={{ accentColor: '#38bdf8', width: '100%', height: 4 }}
            />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>Dịch Y: {selectedFrame.offsetY}px</div>
            <input
              type="range"
              min={-150}
              max={150}
              value={selectedFrame.offsetY}
              onChange={(e) =>
                onUpdateFrameTransform(selectedFrameIndex, {
                  offsetY: Number(e.target.value),
                })
              }
              style={{ accentColor: '#38bdf8', width: '100%', height: 4 }}
            />
          </div>
        </div>

        {/* Scale & Rotation */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            <div style={{ fontSize: 9.5, color: '#94a3b8', display: 'flex', alignItems: 'center', gap: 2 }}>
              <Maximize size={10} /> Tỷ lệ: {selectedFrame.scale.toFixed(2)}x
            </div>
            <input
              type="range"
              min={0.2}
              max={3.0}
              step={0.05}
              value={selectedFrame.scale}
              onChange={(e) =>
                onUpdateFrameTransform(selectedFrameIndex, {
                  scale: Number(e.target.value),
                })
              }
              style={{ accentColor: '#38bdf8', width: '100%', height: 4 }}
            />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            <div style={{ fontSize: 9.5, color: '#94a3b8', display: 'flex', alignItems: 'center', gap: 2 }}>
              <RotateCw size={10} /> Xoay: {selectedFrame.rotation}°
            </div>
            <input
              type="range"
              min={-180}
              max={180}
              value={selectedFrame.rotation}
              onChange={(e) =>
                onUpdateFrameTransform(selectedFrameIndex, {
                  rotation: Number(e.target.value),
                })
              }
              style={{ accentColor: '#38bdf8', width: '100%', height: 4 }}
            />
          </div>
        </div>

        {/* Flip Horizontal Toggle */}
        <button
          onClick={() =>
            onUpdateFrameTransform(selectedFrameIndex, {
              flipX: !selectedFrame.flipX,
            })
          }
          style={{
            background: selectedFrame.flipX ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255, 255, 255, 0.05)',
            border: `1px solid ${selectedFrame.flipX ? '#38bdf8' : 'rgba(255, 255, 255, 0.1)'}`,
            borderRadius: 5,
            color: selectedFrame.flipX ? '#38bdf8' : '#94a3b8',
            padding: '5px 8px',
            fontSize: 9.5,
            fontWeight: 700,
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 4,
          }}
        >
          <FlipHorizontal size={11} /> Lật Ngang: {selectedFrame.flipX ? 'Đang Lật' : 'Bình Thường'}
        </button>

        {/* Apply Transform to All Frames Button */}
        <button
          onClick={() => onApplyTransformToAll(selectedFrameIndex)}
          title="Áp dụng toàn bộ góc xoay, kích thước và lật cho tất cả frame"
          style={{
            background: 'rgba(56, 189, 248, 0.15)',
            border: '1px solid rgba(56, 189, 248, 0.3)',
            borderRadius: 5,
            color: '#38bdf8',
            padding: '5px 8px',
            fontSize: 9.5,
            fontWeight: 700,
            cursor: 'pointer',
            marginTop: 2,
          }}
        >
          📐 Áp Dụng Vị Trí Cho Tất Cả Frame
        </button>
      </div>

      {/* ─── FRAME ACTIONS: HIDE/SHOW (EYE) & DELETE ────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 'auto' }}>
        {/* Toggle Visibility Eye Button (Replaced Duplicate) */}
        <button
          onClick={() => onToggleHideFrame(selectedFrameIndex)}
          style={{
            background: isHidden ? 'rgba(16, 185, 129, 0.2)' : 'rgba(239, 68, 68, 0.15)',
            border: `1px solid ${isHidden ? '#10b981' : 'rgba(239, 68, 68, 0.3)'}`,
            borderRadius: 6,
            color: isHidden ? '#34d399' : '#f87171',
            padding: '6px 10px',
            fontSize: 10,
            fontWeight: 700,
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 4,
          }}
        >
          {isHidden ? <Eye size={12} /> : <EyeOff size={12} />}
          {isHidden ? '👁️ Bật Lại Frame Này' : '🚫 Ẩn Khỏi Hoạt Ảnh'}
        </button>

        {/* Delete Frame Button */}
        <button
          onClick={() => onDeleteFrame(selectedFrameIndex)}
          style={{
            background: 'rgba(220, 38, 38, 0.2)',
            border: '1px solid rgba(220, 38, 38, 0.3)',
            borderRadius: 6,
            color: '#ef4444',
            padding: '6px 10px',
            fontSize: 10,
            fontWeight: 700,
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 4,
          }}
        >
          <Trash2 size={12} /> Xóa Frame Này
        </button>
      </div>
    </div>
  );
};
