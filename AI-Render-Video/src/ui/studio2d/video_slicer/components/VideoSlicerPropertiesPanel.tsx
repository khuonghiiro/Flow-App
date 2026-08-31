// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Slicer Right Properties Panel
// =========================================================================================
import React from 'react';
import {
  Sliders,
  Move,
  Maximize,
  RotateCw,
  Clock,
  FlipHorizontal,
  Copy,
  Trash2,
  Check,
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
  onDuplicateFrame: (index: number) => void;
  onDeleteFrame: (index: number) => void;
}

export const VideoSlicerPropertiesPanel: React.FC<VideoSlicerPropertiesPanelProps> = ({
  selectedFrame,
  selectedFrameIndex,
  totalFramesCount,
  onUpdateFrameTransform,
  onApplyTransformToAll,
  onSetAllDuration,
  onDuplicateFrame,
  onDeleteFrame,
}) => {
  if (!selectedFrame || selectedFrameIndex === null) {
    return (
      <div
        style={{
          width: 280,
          height: '100%',
          background: '#0f172a',
          borderLeft: '1px solid rgba(255, 255, 255, 0.08)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          padding: 16,
          color: '#64748b',
          fontSize: 11,
          textAlign: 'center',
          boxSizing: 'border-box',
          flexShrink: 0,
        }}
      >
        Chọn 1 frame ở thanh đáy để tinh chỉnh vị trí & thời lượng
      </div>
    );
  }

  return (
    <div
      style={{
        width: 280,
        height: '100%',
        background: '#0f172a',
        borderLeft: '1px solid rgba(255, 255, 255, 0.08)',
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        padding: 10,
        overflowY: 'auto',
        boxSizing: 'border-box',
        flexShrink: 0,
      }}
    >
      {/* Frame Header Badge */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          background: 'rgba(30, 41, 59, 0.6)',
          padding: '6px 10px',
          borderRadius: 6,
          border: '1px solid rgba(255, 255, 255, 0.06)',
        }}
      >
        <div style={{ fontSize: 11, fontWeight: 700, color: '#f8fafc', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Sliders size={13} color="#38bdf8" /> Khung Hình F{selectedFrameIndex + 1}
        </div>
        <span style={{ fontSize: 9, color: '#94a3b8' }}>
          {selectedFrame.timestamp.toFixed(2)}s
        </span>
      </div>

      {/* ─── 1. TRANSFORM (OFFSET, SCALE, ROTATION) ─────────────── */}
      <div
        style={{
          background: 'rgba(30, 41, 59, 0.4)',
          border: '1px solid rgba(255, 255, 255, 0.05)',
          borderRadius: 6,
          padding: 8,
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
        }}
      >
        <div style={{ fontSize: 10, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
          <Move size={11} /> Vị Trí (Offset X / Y)
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#94a3b8' }}>
              <span>X:</span>
              <span style={{ color: '#fff' }}>{selectedFrame.offsetX}px</span>
            </div>
            <input
              type="range"
              min={-150}
              max={150}
              value={selectedFrame.offsetX}
              onChange={(e) =>
                onUpdateFrameTransform(selectedFrameIndex, { offsetX: Number(e.target.value) })
              }
              style={{ width: '100%', cursor: 'pointer' }}
            />
          </div>

          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#94a3b8' }}>
              <span>Y:</span>
              <span style={{ color: '#fff' }}>{selectedFrame.offsetY}px</span>
            </div>
            <input
              type="range"
              min={-150}
              max={150}
              value={selectedFrame.offsetY}
              onChange={(e) =>
                onUpdateFrameTransform(selectedFrameIndex, { offsetY: Number(e.target.value) })
              }
              style={{ width: '100%', cursor: 'pointer' }}
            />
          </div>
        </div>

        {/* Scale & Rotation */}
        <div style={{ marginTop: 2 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#94a3b8' }}>
            <span>Tỉ lệ (Scale):</span>
            <span style={{ color: '#38bdf8', fontWeight: 700 }}>{selectedFrame.scale.toFixed(2)}x</span>
          </div>
          <input
            type="range"
            min={0.4}
            max={2.5}
            step={0.05}
            value={selectedFrame.scale}
            onChange={(e) =>
              onUpdateFrameTransform(selectedFrameIndex, { scale: Number(e.target.value) })
            }
            style={{ width: '100%', cursor: 'pointer' }}
          />
        </div>

        <div style={{ marginTop: 2 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#94a3b8' }}>
            <span>Góc Xoay:</span>
            <span style={{ color: '#fff' }}>{selectedFrame.rotation}°</span>
          </div>
          <input
            type="range"
            min={-180}
            max={180}
            step={1}
            value={selectedFrame.rotation}
            onChange={(e) =>
              onUpdateFrameTransform(selectedFrameIndex, { rotation: Number(e.target.value) })
            }
            style={{ width: '100%', cursor: 'pointer' }}
          />
        </div>

        {/* Flip Horizontal */}
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 10, color: '#cbd5e1', cursor: 'pointer', marginTop: 4 }}>
          <input
            type="checkbox"
            checked={selectedFrame.flipX}
            onChange={(e) =>
              onUpdateFrameTransform(selectedFrameIndex, { flipX: e.target.checked })
            }
          />
          <FlipHorizontal size={12} /> Lật gương ngang (Flip X)
        </label>
      </div>

      {/* ─── 2. FRAME TIMING ───────────────────────────────────── */}
      <div
        style={{
          background: 'rgba(30, 41, 59, 0.4)',
          border: '1px solid rgba(255, 255, 255, 0.05)',
          borderRadius: 6,
          padding: 8,
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
        }}
      >
        <div style={{ fontSize: 10, fontWeight: 700, color: '#10b981', display: 'flex', alignItems: 'center', gap: 4 }}>
          <Clock size={11} /> Thời Lượng Frame (ms)
        </div>

        <div style={{ display: 'flex', gap: 6 }}>
          <input
            type="number"
            min={20}
            max={3000}
            step={25}
            value={selectedFrame.durationMs}
            onChange={(e) =>
              onUpdateFrameTransform(selectedFrameIndex, { durationMs: Number(e.target.value) })
            }
            style={{
              flex: 1,
              background: 'rgba(0, 0, 0, 0.4)',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              borderRadius: 4,
              padding: '4px 6px',
              color: '#fff',
              fontSize: 11,
            }}
          />
          <button
            onClick={() => onSetAllDuration(selectedFrame.durationMs)}
            title="Đặt thời lượng này cho tất cả frame"
            style={{
              padding: '4px 8px',
              borderRadius: 4,
              fontSize: 10,
              fontWeight: 600,
              border: '1px solid rgba(16, 185, 129, 0.3)',
              background: 'rgba(16, 185, 129, 0.15)',
              color: '#34d399',
              cursor: 'pointer',
            }}
          >
            Tất Cả
          </button>
        </div>
      </div>

      {/* ─── 3. BATCH TRANSFORM & ACTIONS ──────────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 4 }}>
        <button
          onClick={() => onApplyTransformToAll(selectedFrameIndex)}
          style={{
            padding: '6px 10px',
            borderRadius: 6,
            fontSize: 10,
            fontWeight: 600,
            border: '1px solid rgba(56, 189, 248, 0.3)',
            background: 'rgba(56, 189, 248, 0.12)',
            color: '#38bdf8',
            cursor: 'pointer',
          }}
        >
          Áp Dụng Vị Trí Cho Tất Cả Frame
        </button>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          <button
            onClick={() => onDuplicateFrame(selectedFrameIndex)}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              padding: '6px 8px',
              borderRadius: 6,
              fontSize: 10,
              fontWeight: 600,
              border: '1px solid rgba(255, 255, 255, 0.1)',
              background: 'rgba(255, 255, 255, 0.05)',
              color: '#f8fafc',
              cursor: 'pointer',
            }}
          >
            <Copy size={11} /> Nhân Bản
          </button>

          <button
            onClick={() => onDeleteFrame(selectedFrameIndex)}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              padding: '6px 8px',
              borderRadius: 6,
              fontSize: 10,
              fontWeight: 600,
              border: '1px solid rgba(239, 68, 68, 0.3)',
              background: 'rgba(239, 68, 68, 0.15)',
              color: '#f87171',
              cursor: 'pointer',
            }}
          >
            <Trash2 size={11} /> Xóa Frame
          </button>
        </div>
      </div>
    </div>
  );
};
