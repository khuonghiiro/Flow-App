// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React from 'react';
import {
  Clock,
  Sliders,
  Move,
  RotateCcw,
  FlipHorizontal,
  Save,
  Check,
  Sparkles,
  Layers,
  FolderOpen,
} from 'lucide-react';
import { AnimationSliceFrame } from '../../../../types/animation_slicer';

interface AnimationPropertiesPanelProps {
  selectedFrame: AnimationSliceFrame | null;
  selectedFrameIndex: number | null;
  totalFramesCount: number;
  onUpdateFrameTransform: (
    frameIndex: number,
    updates: Partial<Pick<AnimationSliceFrame, 'offsetX' | 'offsetY' | 'scale' | 'rotation' | 'flipX' | 'durationMs'>>
  ) => void;
  onApplyTransformToAllFrames: (sourceFrameIndex: number) => void;
  onSetAllFramesDuration: (durationMs: number) => void;
  onAutoTrimAllBBox?: () => void;
  onOpenSaveModal: () => void;
  onOpenLoadModal?: () => void;
}

export const AnimationPropertiesPanel: React.FC<AnimationPropertiesPanelProps> = ({
  selectedFrame,
  selectedFrameIndex,
  totalFramesCount,
  onUpdateFrameTransform,
  onApplyTransformToAllFrames,
  onSetAllFramesDuration,
  onAutoTrimAllBBox,
  onOpenSaveModal,
  onOpenLoadModal,
}) => {
  const activeIdx = selectedFrameIndex ?? 0;
  const durationSec = selectedFrame ? (selectedFrame.durationMs ? selectedFrame.durationMs / 1000 : 0.5) : 0.5;

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        background: 'rgba(15, 23, 42, 0.88)',
        border: '1px solid rgba(56, 189, 248, 0.25)',
        borderRadius: 8,
        padding: 10,
        height: '100%',
        overflowY: 'auto',
      }}
    >
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderBottom: '1px solid rgba(255, 255, 255, 0.08)', paddingBottom: 6 }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#facc15', display: 'flex', alignItems: 'center', gap: 5 }}>
          <Sliders size={13} /> CÂN CHỈNH KHUNG HÌNH F{activeIdx + 1}
        </div>
        <span style={{ fontSize: 9.5, color: '#94a3b8' }}>
          ({activeIdx + 1}/{totalFramesCount})
        </span>
      </div>

      {selectedFrame ? (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {/* ─── 1. THỜI LƯỢNG FRAME (TIMING) ────────────────────────── */}
          <div style={{ background: 'rgba(2, 6, 23, 0.6)', padding: 8, borderRadius: 6, border: '1px solid rgba(74, 222, 128, 0.25)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#94a3b8', marginBottom: 3 }}>
              <span style={{ color: '#4ade80', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 4 }}>
                <Clock size={11} /> Thời lượng Frame F{activeIdx + 1}:
              </span>
              <span style={{ color: '#4ade80', fontWeight: 800 }}>
                {durationSec.toFixed(2)}s ({Math.round(durationSec * 1000)}ms)
              </span>
            </div>

            <input
              type="range"
              min="0.05"
              max="4.0"
              step="0.05"
              value={durationSec}
              onChange={(e) => {
                const sec = parseFloat(e.target.value);
                onUpdateFrameTransform(activeIdx, { durationMs: Math.round(sec * 1000) });
              }}
              style={{ width: '100%', accentColor: '#4ade80' }}
            />

            {/* Quick Timing Pills */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 3, marginTop: 4 }}>
              {[
                { label: '0.1s', ms: 100 },
                { label: '0.2s', ms: 200 },
                { label: '0.5s', ms: 500 },
                { label: '1.0s', ms: 1000 },
                { label: '1.5s', ms: 1500 },
                { label: '2.0s', ms: 2000 },
                { label: '3.0s', ms: 3000 },
                { label: '4.0s', ms: 4000 },
              ].map((d) => (
                <button
                  key={d.ms}
                  onClick={() => onUpdateFrameTransform(activeIdx, { durationMs: d.ms })}
                  style={{
                    padding: '2px 3px',
                    borderRadius: 3,
                    fontSize: 8.5,
                    fontWeight: (selectedFrame.durationMs || 500) === d.ms ? 700 : 500,
                    background: (selectedFrame.durationMs || 500) === d.ms ? 'rgba(74, 222, 128, 0.3)' : 'rgba(255,255,255,0.03)',
                    border: (selectedFrame.durationMs || 500) === d.ms ? '1px solid #4ade80' : '1px solid rgba(255,255,255,0.06)',
                    color: (selectedFrame.durationMs || 500) === d.ms ? '#4ade80' : '#cbd5e1',
                    cursor: 'pointer',
                  }}
                >
                  {d.label}
                </button>
              ))}
            </div>

            {/* Set All Frames Button */}
            <button
              onClick={() => onSetAllFramesDuration(selectedFrame.durationMs || 500)}
              style={{
                width: '100%',
                marginTop: 6,
                padding: '4px 6px',
                borderRadius: 4,
                background: 'rgba(74, 222, 128, 0.15)',
                border: '1px solid rgba(74, 222, 128, 0.3)',
                color: '#4ade80',
                fontSize: 9,
                fontWeight: 700,
                cursor: 'pointer',
              }}
            >
              ⏱️ Đặt {durationSec.toFixed(2)}s Cho TẤT CẢ Frame
            </button>
          </div>

          {/* ─── 2. VỊ TRÍ & TỈ LỆ (TRANSFORM) ────────────────────────── */}
          <div style={{ background: 'rgba(2, 6, 23, 0.6)', padding: 8, borderRadius: 6, border: '1px solid rgba(56, 189, 248, 0.2)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 }}>
              <span style={{ fontSize: 9.5, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
                <Move size={11} /> Cân Chỉnh Khung Hình:
              </span>
              <button
                onClick={() => onApplyTransformToAllFrames(activeIdx)}
                style={{ fontSize: 8.5, color: '#38bdf8', background: 'none', border: 'none', cursor: 'pointer', fontWeight: 600 }}
              >
                Áp dụng vị trí tất cả
              </button>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
              {/* Offset X & Y */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                <div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5, color: '#94a3b8' }}>
                    <span>Offset X:</span>
                    <span style={{ color: '#38bdf8', fontWeight: 700 }}>{selectedFrame.offsetX}px</span>
                  </div>
                  <input
                    type="range"
                    min="-150"
                    max="150"
                    value={selectedFrame.offsetX}
                    onChange={(e) => onUpdateFrameTransform(activeIdx, { offsetX: parseInt(e.target.value) })}
                    style={{ width: '100%', accentColor: '#38bdf8' }}
                  />
                </div>

                <div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5, color: '#94a3b8' }}>
                    <span>Offset Y:</span>
                    <span style={{ color: '#38bdf8', fontWeight: 700 }}>{selectedFrame.offsetY}px</span>
                  </div>
                  <input
                    type="range"
                    min="-150"
                    max="150"
                    value={selectedFrame.offsetY}
                    onChange={(e) => onUpdateFrameTransform(activeIdx, { offsetY: parseInt(e.target.value) })}
                    style={{ width: '100%', accentColor: '#38bdf8' }}
                  />
                </div>
              </div>

              {/* Scale */}
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5, color: '#94a3b8' }}>
                  <span>Tỉ lệ (Scale):</span>
                  <span style={{ color: '#c084fc', fontWeight: 700 }}>{selectedFrame.scale.toFixed(2)}x</span>
                </div>
                <input
                  type="range"
                  min="0.5"
                  max="2.5"
                  step="0.05"
                  value={selectedFrame.scale}
                  onChange={(e) => onUpdateFrameTransform(activeIdx, { scale: parseFloat(e.target.value) })}
                  style={{ width: '100%', accentColor: '#c084fc' }}
                />
              </div>

              {/* FlipX & Reset */}
              <div style={{ display: 'flex', gap: 4, marginTop: 2 }}>
                <button
                  onClick={() => onUpdateFrameTransform(activeIdx, { flipX: !selectedFrame.flipX })}
                  style={{
                    flex: 1,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 3,
                    padding: '3px',
                    borderRadius: 4,
                    fontSize: 8.5,
                    fontWeight: 600,
                    background: selectedFrame.flipX ? 'rgba(236, 72, 153, 0.3)' : 'rgba(255,255,255,0.05)',
                    border: selectedFrame.flipX ? '1px solid #ec4899' : '1px solid rgba(255,255,255,0.08)',
                    color: selectedFrame.flipX ? '#f472b6' : '#cbd5e1',
                    cursor: 'pointer',
                  }}
                >
                  <FlipHorizontal size={10} /> Lật Gương
                </button>

                <button
                  onClick={() => onUpdateFrameTransform(activeIdx, { offsetX: 0, offsetY: 0, scale: 1.0, rotation: 0 })}
                  style={{
                    flex: 1,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 3,
                    padding: '3px',
                    borderRadius: 4,
                    fontSize: 8.5,
                    background: 'none',
                    border: '1px solid rgba(255,255,255,0.08)',
                    color: '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  <RotateCcw size={10} /> Đặt lại 0
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : (
        <div style={{ fontSize: 10, color: '#64748b', textAlign: 'center', padding: 20 }}>
          Chưa chọn frame nào
        </div>
      )}

      {/* ─── 3. AUTO-TRIM & SAVE ACTION BUTTONS ─────────────────────── */}
      <div style={{ marginTop: 'auto', display: 'flex', flexDirection: 'column', gap: 5, paddingTop: 6 }}>
        {onAutoTrimAllBBox && totalFramesCount > 0 && (
          <button
            onClick={onAutoTrimAllBBox}
            style={{
              width: '100%',
              height: 32,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 5,
              borderRadius: 6,
              background: 'linear-gradient(135deg, #059669, #10b981)',
              border: 'none',
              color: '#ffffff',
              fontSize: 10.5,
              fontWeight: 700,
              cursor: 'pointer',
              boxShadow: '0 2px 8px rgba(16, 185, 129, 0.3)',
            }}
          >
            ✂️ Cắt BBox Sát Viền TẤT CẢ Frame
          </button>
        )}

        <div style={{ display: 'grid', gridTemplateColumns: onOpenLoadModal ? '1fr 1fr' : '1fr', gap: 6 }}>
          {onOpenLoadModal && (
            <button
              onClick={onOpenLoadModal}
              style={{
                height: 36,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 5,
                borderRadius: 6,
                background: 'rgba(56, 189, 248, 0.15)',
                border: '1px solid rgba(56, 189, 248, 0.35)',
                color: '#38bdf8',
                fontSize: 10.5,
                fontWeight: 700,
                cursor: 'pointer',
                boxShadow: '0 2px 8px rgba(56, 189, 248, 0.2)',
              }}
            >
              <FolderOpen size={13} /> Mở Động Tác
            </button>
          )}

          <button
            onClick={onOpenSaveModal}
            disabled={totalFramesCount === 0}
            style={{
              height: 36,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 5,
              borderRadius: 6,
              background: totalFramesCount > 0 ? 'linear-gradient(135deg, #0284c7, #a855f7)' : 'rgba(255,255,255,0.05)',
              border: 'none',
              color: totalFramesCount > 0 ? '#ffffff' : '#64748b',
              fontSize: 10.5,
              fontWeight: 700,
              cursor: totalFramesCount > 0 ? 'pointer' : 'not-allowed',
              boxShadow: totalFramesCount > 0 ? '0 2px 12px rgba(56, 189, 248, 0.4)' : 'none',
            }}
          >
            <Save size={13} /> Lưu Động Tác
          </button>
        </div>
      </div>
    </div>
  );
};
