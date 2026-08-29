// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React, { useState, useEffect, useRef } from 'react';
import {
  Play,
  Pause,
  RotateCcw,
  Plus,
  Trash2,
  Sliders,
  Move,
  Layers,
  Repeat,
  Sparkles,
  ChevronLeft,
  ChevronRight,
  FlipHorizontal,
  Eye,
  EyeOff,
  Clock,
  Timer,
} from 'lucide-react';
import { AnimationSliceFrame } from '../../../../types/animation_slicer';

interface AnimationFrameSequencerBarProps {
  frames: AnimationSliceFrame[];
  frameOrder: number[];
  selectedFrameIndex: number | null;
  fps: number;
  loopMode: 'loop' | 'ping_pong' | 'once';
  onionSkinEnabled: boolean;
  onSelectFrameIndex: (index: number) => void;
  onUpdateFrameOrder: (order: number[]) => void;
  onUpdateFps: (fps: number) => void;
  onUpdateLoopMode: (mode: 'loop' | 'ping_pong' | 'once') => void;
  onToggleOnionSkin: () => void;
  onUpdateFrameTransform: (
    frameIndex: number,
    updates: Partial<Pick<AnimationSliceFrame, 'offsetX' | 'offsetY' | 'scale' | 'rotation' | 'flipX' | 'durationMs'>>
  ) => void;
  onApplyTransformToAllFrames: (sourceFrameIndex: number) => void;
  onSetAllFramesDuration: (durationMs: number) => void;
}

const DURATION_PRESETS = [
  { label: '0.1s (Nhanh)', ms: 100 },
  { label: '0.2s', ms: 200 },
  { label: '0.5s', ms: 500 },
  { label: '1.0s (1s/F)', ms: 1000 },
  { label: '1.5s', ms: 1500 },
  { label: '2.0s (2s/F)', ms: 2000 },
  { label: '3.0s', ms: 3000 },
];

export const AnimationFrameSequencerBar: React.FC<AnimationFrameSequencerBarProps> = ({
  frames,
  frameOrder,
  selectedFrameIndex,
  fps,
  loopMode,
  onionSkinEnabled,
  onSelectFrameIndex,
  onUpdateFrameOrder,
  onUpdateFps,
  onUpdateLoopMode,
  onToggleOnionSkin,
  onUpdateFrameTransform,
  onApplyTransformToAllFrames,
  onSetAllFramesDuration,
}) => {
  const [isPlaying, setIsPlaying] = useState<boolean>(true);
  const [playbackCursor, setPlaybackCursor] = useState<number>(0);
  const previewCanvasRef = useRef<HTMLCanvasElement>(null);
  const imageCacheRef = useRef<Map<string, HTMLImageElement>>(new Map());

  // Helper to load and cache frame images
  const getCachedImage = (url: string): HTMLImageElement | null => {
    if (!url) return null;
    if (imageCacheRef.current.has(url)) {
      return imageCacheRef.current.get(url)!;
    }
    const img = new Image();
    img.src = url;
    imageCacheRef.current.set(url, img);
    return img;
  };

  // Playback timer loop supporting individual frame durations
  useEffect(() => {
    if (!isPlaying || frameOrder.length === 0) return;

    let activeOrderIdx = playbackCursor;
    if (loopMode === 'ping_pong' && frameOrder.length > 1) {
      const cycle = frameOrder.length * 2 - 2;
      const mod = playbackCursor % cycle;
      activeOrderIdx = mod < frameOrder.length ? mod : cycle - mod;
    }
    const currentFrameIdx = frameOrder[activeOrderIdx] ?? 0;
    const curFrame = frames[currentFrameIdx] || frames[0];

    // Per-frame duration in ms (fallback to 1000/fps if not set)
    const frameDurationMs = Math.max(30, curFrame?.durationMs || Math.round(1000 / (fps || 8)) || 500);

    const timer = setTimeout(() => {
      setPlaybackCursor((prev) => {
        if (loopMode === 'ping_pong') {
          return (prev + 1) % (frameOrder.length * 2 - 2 || 1);
        }
        return (prev + 1) % frameOrder.length;
      });
    }, frameDurationMs);

    return () => clearTimeout(timer);
  }, [isPlaying, playbackCursor, frameOrder, loopMode, frames, fps]);

  // Compute active playing frame index based on loop mode
  let activePlayingOrderIdx = playbackCursor;
  if (loopMode === 'ping_pong' && frameOrder.length > 1) {
    const cycle = frameOrder.length * 2 - 2;
    const mod = playbackCursor % cycle;
    activePlayingOrderIdx = mod < frameOrder.length ? mod : cycle - mod;
  }
  const currentFrameIdx = frameOrder[activePlayingOrderIdx] ?? 0;
  const currentFrame = frames[currentFrameIdx] || frames[0];

  // Calculate total sequence duration and current elapsed time
  const totalDurationMs = frameOrder.reduce((sum, fIdx) => {
    const f = frames[fIdx];
    return sum + (f?.durationMs || Math.round(1000 / (fps || 8)) || 500);
  }, 0);

  let currentElapsedMs = 0;
  for (let i = 0; i < activePlayingOrderIdx; i++) {
    const f = frames[frameOrder[i]];
    currentElapsedMs += (f?.durationMs || Math.round(1000 / (fps || 8)) || 500);
  }

  // Previous frame for Onion Skinning
  const prevOrderIdx = (activePlayingOrderIdx - 1 + frameOrder.length) % frameOrder.length;
  const prevFrameIdx = frameOrder[prevOrderIdx] ?? 0;
  const prevFrame = frames[prevFrameIdx];

  // Render Motion Preview Canvas
  useEffect(() => {
    const canvas = previewCanvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const w = canvas.width;
    const h = canvas.height;

    ctx.clearRect(0, 0, w, h);

    // Checkerboard Background
    const sq = 8;
    for (let x = 0; x < w; x += sq) {
      for (let y = 0; y < h; y += sq) {
        ctx.fillStyle = (Math.floor(x / sq) + Math.floor(y / sq)) % 2 === 0 ? '#0b0f19' : '#172033';
        ctx.fillRect(x, y, sq, sq);
      }
    }

    // Ground line indicator
    ctx.strokeStyle = 'rgba(56, 189, 248, 0.2)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(0, h - 25);
    ctx.lineTo(w, h - 25);
    ctx.stroke();

    if (!currentFrame) return;

    // 1. Onion Skin (Previous Frame Ghost)
    if (onionSkinEnabled && prevFrame && prevFrame.id !== currentFrame.id) {
      const prevImg = getCachedImage(prevFrame.transparentDataUrl || prevFrame.originalDataUrl);
      if (prevImg && prevImg.complete) {
        ctx.save();
        ctx.globalAlpha = 0.35;
        ctx.translate(w / 2 + prevFrame.offsetX, h / 2 + prevFrame.offsetY);
        ctx.rotate((prevFrame.rotation * Math.PI) / 180);
        ctx.scale(prevFrame.flipX ? -prevFrame.scale : prevFrame.scale, prevFrame.scale);

        const pw = prevImg.width || 120;
        const ph = prevImg.height || 160;
        const drawScale = Math.min((w * 0.75) / pw, (h * 0.75) / ph);
        ctx.drawImage(prevImg, (-pw * drawScale) / 2, (-ph * drawScale) / 2, pw * drawScale, ph * drawScale);
        ctx.restore();
      }
    }

    // 2. Active Current Frame
    const curImg = getCachedImage(currentFrame.transparentDataUrl || currentFrame.originalDataUrl);
    if (curImg && curImg.complete) {
      ctx.save();
      ctx.translate(w / 2 + currentFrame.offsetX, h / 2 + currentFrame.offsetY);
      ctx.rotate((currentFrame.rotation * Math.PI) / 180);
      ctx.scale(currentFrame.flipX ? -currentFrame.scale : currentFrame.scale, currentFrame.scale);

      const pw = curImg.width || 120;
      const ph = curImg.height || 160;
      const drawScale = Math.min((w * 0.75) / pw, (h * 0.75) / ph);
      ctx.drawImage(curImg, (-pw * drawScale) / 2, (-ph * drawScale) / 2, pw * drawScale, ph * drawScale);
      ctx.restore();
    }
  }, [currentFrame, prevFrame, onionSkinEnabled, isPlaying, playbackCursor]);

  const activeSelectedFrame = frames[selectedFrameIndex ?? 0] || frames[0];
  const activeFrameDurationSec = activeSelectedFrame ? (activeSelectedFrame.durationMs ? activeSelectedFrame.durationMs / 1000 : 0.5) : 0.5;

  return (
    <div
      style={{
        display: 'grid',
        gridTemplateColumns: '230px 1fr 310px',
        gap: 10,
        background: 'rgba(9, 13, 22, 0.95)',
        border: '1px solid rgba(56, 189, 248, 0.25)',
        borderRadius: 8,
        padding: 8,
        height: '100%',
        overflow: 'hidden',
      }}
    >
      {/* ─── COLUMN 1: Live Motion Preview Viewport ───────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4, height: '100%' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ fontSize: 9.5, fontWeight: 700, color: '#38bdf8' }}>
            🎬 XEM TRƯỚC (F{currentFrameIdx + 1})
          </span>
          <span style={{ fontSize: 9, color: '#4ade80', fontWeight: 700 }}>
            ⏱️ {((currentFrame?.durationMs || 500) / 1000).toFixed(2)}s ({activePlayingOrderIdx + 1}/{frameOrder.length})
          </span>
        </div>

        {/* Viewport Canvas */}
        <div style={{ flex: 1, minHeight: 0, position: 'relative', borderRadius: 6, overflow: 'hidden', border: '1px solid rgba(255,255,255,0.08)' }}>
          <canvas
            ref={previewCanvasRef}
            width={240}
            height={160}
            style={{ width: '100%', height: '100%', objectFit: 'contain' }}
          />

          {/* Bottom Live Progress Bar */}
          <div style={{ position: 'absolute', bottom: 0, left: 0, right: 0, height: 3, background: 'rgba(255,255,255,0.1)' }}>
            <div
              style={{
                height: '100%',
                background: 'linear-gradient(90deg, #38bdf8, #4ade80)',
                width: totalDurationMs > 0 ? `${((currentElapsedMs + (currentFrame?.durationMs || 500)) / totalDurationMs) * 100}%` : '0%',
                transition: 'width 0.1s linear',
              }}
            />
          </div>
        </div>

        {/* Playback Controls */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 4 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <button
              onClick={() => setIsPlaying(!isPlaying)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '3px 7px',
                borderRadius: 4,
                background: isPlaying ? 'rgba(56, 189, 248, 0.3)' : 'rgba(255,255,255,0.08)',
                border: isPlaying ? '1px solid #38bdf8' : '1px solid transparent',
                color: isPlaying ? '#38bdf8' : '#fff',
                fontSize: 9.5,
                fontWeight: 700,
                cursor: 'pointer',
              }}
            >
              {isPlaying ? <Pause size={11} /> : <Play size={11} />}
              <span>{isPlaying ? 'Dừng' : 'Chạy'}</span>
            </button>

            <button
              onClick={onToggleOnionSkin}
              title="Bật/Tắt bóng ma frame trước (Onion Skinning)"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 2,
                padding: '3px 5px',
                borderRadius: 4,
                fontSize: 9,
                background: onionSkinEnabled ? 'rgba(168, 85, 247, 0.3)' : 'rgba(255,255,255,0.05)',
                border: onionSkinEnabled ? '1px solid #c084fc' : '1px solid transparent',
                color: onionSkinEnabled ? '#c084fc' : '#94a3b8',
                cursor: 'pointer',
              }}
            >
              {onionSkinEnabled ? <Eye size={10} /> : <EyeOff size={10} />}
              <span>Bóng Ma</span>
            </button>
          </div>

          <div style={{ fontSize: 9, color: '#facc15', fontWeight: 600 }}>
            Tổng: {(totalDurationMs / 1000).toFixed(2)}s
          </div>
        </div>
      </div>

      {/* ─── COLUMN 2: Filmstrip Timeline of Sliced Frames ────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4, height: '100%', overflow: 'hidden' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ fontSize: 9.5, fontWeight: 700, color: '#38bdf8' }}>
            🎞️ DANH SÁCH FRAME & THỨ TỰ ({frames.length} frames):
          </span>

          <div style={{ display: 'flex', gap: 3 }}>
            <button
              onClick={() => onUpdateFrameOrder(frames.map((_, i) => i))}
              title="Khôi phục thứ tự tuần tự 1 -> 2 -> 3 -> 4"
              style={{
                padding: '2px 5px',
                borderRadius: 3,
                fontSize: 8.5,
                background: 'rgba(255,255,255,0.05)',
                border: '1px solid rgba(255,255,255,0.1)',
                color: '#cbd5e1',
                cursor: 'pointer',
              }}
            >
              1..N
            </button>

            <button
              onClick={() => {
                const forward = frames.map((_, i) => i);
                const backward = [...forward].reverse().slice(1, -1);
                onUpdateFrameOrder([...forward, ...backward]);
              }}
              title="Tạo chuỗi lặp hai chiều (1->2->3->4->3->2)"
              style={{
                padding: '2px 5px',
                borderRadius: 3,
                fontSize: 8.5,
                background: 'rgba(168, 85, 247, 0.2)',
                border: '1px solid rgba(168, 85, 247, 0.4)',
                color: '#c084fc',
                cursor: 'pointer',
              }}
            >
              Ping-Pong
            </button>
          </div>
        </div>

        {/* Filmstrip Horizontal Scrollable List */}
        <div
          style={{
            flex: 1,
            display: 'flex',
            gap: 6,
            overflowX: 'auto',
            padding: '4px',
            background: 'rgba(2, 6, 23, 0.6)',
            borderRadius: 6,
            border: '1px solid rgba(255,255,255,0.05)',
            alignItems: 'center',
          }}
        >
          {frames.map((f, idx) => {
            const isSelected = selectedFrameIndex === idx;
            const isCurrentPlaying = currentFrameIdx === idx;
            const fDurSec = f.durationMs ? (f.durationMs / 1000).toFixed(2) : '0.50';

            return (
              <div
                key={f.id}
                onClick={() => onSelectFrameIndex(idx)}
                style={{
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  gap: 3,
                  padding: 4,
                  borderRadius: 6,
                  background: isSelected ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.02)',
                  border: isSelected
                    ? '2px solid #38bdf8'
                    : isCurrentPlaying
                    ? '1.5px solid #4ade80'
                    : '1px solid rgba(255,255,255,0.08)',
                  cursor: 'pointer',
                  minWidth: 68,
                  flexShrink: 0,
                  transition: 'all 0.12s ease',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', width: '100%', fontSize: 9, fontWeight: 700 }}>
                  <span style={{ color: isSelected ? '#38bdf8' : '#cbd5e1' }}>F{idx + 1}</span>
                  <span style={{ color: '#4ade80' }}>{fDurSec}s</span>
                </div>

                <div style={{ width: 56, height: 56, background: '#0f172a', borderRadius: 4, overflow: 'hidden', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <img
                    src={f.transparentDataUrl || f.originalDataUrl}
                    alt={`Frame ${idx + 1}`}
                    style={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain' }}
                  />
                </div>

                <div style={{ fontSize: 8, color: '#94a3b8' }}>
                  {f.offsetX !== 0 || f.offsetY !== 0 ? `(${f.offsetX}, ${f.offsetY})` : 'Khớp 0'}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* ─── COLUMN 3: Per-Frame Alignment & Duration Sliders ─────────── */}
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
          background: 'rgba(2, 6, 23, 0.6)',
          padding: 8,
          borderRadius: 6,
          border: '1px solid rgba(255,255,255,0.05)',
          overflowY: 'auto',
        }}
      >
        {/* Header with frame selector indicator */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontSize: 9.5, fontWeight: 700, color: '#facc15', display: 'flex', alignItems: 'center', gap: 4 }}>
            <Clock size={11} /> CÂN CHỈNH F{(selectedFrameIndex ?? 0) + 1}:
          </span>
          <button
            onClick={() => onApplyTransformToAllFrames(selectedFrameIndex ?? 0)}
            title="Sao chép thông số (Offset, Scale) của frame này cho toàn bộ frame còn lại"
            style={{
              fontSize: 8.5,
              color: '#38bdf8',
              background: 'none',
              border: 'none',
              cursor: 'pointer',
              fontWeight: 600,
            }}
          >
            Áp dụng vị trí tất cả
          </button>
        </div>

        {activeSelectedFrame && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
            {/* 1. Per-Frame Duration Tuning */}
            <div style={{ background: 'rgba(15, 23, 42, 0.8)', padding: 6, borderRadius: 5, border: '1px solid rgba(74, 222, 128, 0.2)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#94a3b8', marginBottom: 2 }}>
                <span style={{ color: '#4ade80', fontWeight: 700 }}>⏱️ Thời lượng Frame:</span>
                <span style={{ color: '#4ade80', fontWeight: 700 }}>{activeFrameDurationSec.toFixed(2)} giây ({Math.round(activeFrameDurationSec * 1000)}ms)</span>
              </div>

              <input
                type="range"
                min="0.05"
                max="4.0"
                step="0.05"
                value={activeFrameDurationSec}
                onChange={(e) => {
                  const sec = parseFloat(e.target.value);
                  onUpdateFrameTransform(selectedFrameIndex ?? 0, { durationMs: Math.round(sec * 1000) });
                }}
                style={{ width: '100%', accentColor: '#4ade80' }}
              />

              {/* Quick Duration Pills */}
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
                    onClick={() => onUpdateFrameTransform(selectedFrameIndex ?? 0, { durationMs: d.ms })}
                    style={{
                      padding: '2px 3px',
                      borderRadius: 3,
                      fontSize: 8.5,
                      fontWeight: (activeSelectedFrame.durationMs || 500) === d.ms ? 700 : 500,
                      background: (activeSelectedFrame.durationMs || 500) === d.ms ? 'rgba(74, 222, 128, 0.3)' : 'rgba(255,255,255,0.03)',
                      border: (activeSelectedFrame.durationMs || 500) === d.ms ? '1px solid #4ade80' : '1px solid rgba(255,255,255,0.06)',
                      color: (activeSelectedFrame.durationMs || 500) === d.ms ? '#4ade80' : '#cbd5e1',
                      cursor: 'pointer',
                    }}
                  >
                    {d.label}
                  </button>
                ))}
              </div>

              {/* Set this duration to ALL frames button */}
              <button
                onClick={() => onSetAllFramesDuration(activeSelectedFrame.durationMs || 500)}
                title="Đặt thời lượng của frame này cho TẤT CẢ các frame"
                style={{
                  width: '100%',
                  marginTop: 5,
                  padding: '3px 6px',
                  borderRadius: 4,
                  background: 'rgba(74, 222, 128, 0.15)',
                  border: '1px solid rgba(74, 222, 128, 0.3)',
                  color: '#4ade80',
                  fontSize: 8.5,
                  fontWeight: 700,
                  cursor: 'pointer',
                }}
              >
                ⏱️ Đặt {activeFrameDurationSec.toFixed(2)}s Cho TẤT CẢ Frame
              </button>
            </div>

            {/* 2. Offset X & Y Sliders */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5, color: '#94a3b8' }}>
                  <span>Offset X:</span>
                  <span style={{ color: '#38bdf8', fontWeight: 700 }}>{activeSelectedFrame.offsetX}px</span>
                </div>
                <input
                  type="range"
                  min="-150"
                  max="150"
                  value={activeSelectedFrame.offsetX}
                  onChange={(e) =>
                    onUpdateFrameTransform(selectedFrameIndex ?? 0, { offsetX: parseInt(e.target.value) })
                  }
                  style={{ width: '100%', accentColor: '#38bdf8' }}
                />
              </div>

              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5, color: '#94a3b8' }}>
                  <span>Offset Y:</span>
                  <span style={{ color: '#38bdf8', fontWeight: 700 }}>{activeSelectedFrame.offsetY}px</span>
                </div>
                <input
                  type="range"
                  min="-150"
                  max="150"
                  value={activeSelectedFrame.offsetY}
                  onChange={(e) =>
                    onUpdateFrameTransform(selectedFrameIndex ?? 0, { offsetY: parseInt(e.target.value) })
                  }
                  style={{ width: '100%', accentColor: '#38bdf8' }}
                />
              </div>
            </div>

            {/* 3. Scale Slider */}
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5, color: '#94a3b8' }}>
                <span>Tỉ lệ (Scale):</span>
                <span style={{ color: '#c084fc', fontWeight: 700 }}>{activeSelectedFrame.scale.toFixed(2)}x</span>
              </div>
              <input
                type="range"
                min="0.5"
                max="2.5"
                step="0.05"
                value={activeSelectedFrame.scale}
                onChange={(e) =>
                  onUpdateFrameTransform(selectedFrameIndex ?? 0, { scale: parseFloat(e.target.value) })
                }
                style={{ width: '100%', accentColor: '#c084fc' }}
              />
            </div>

            {/* 4. FlipX & Reset */}
            <div style={{ display: 'flex', gap: 4 }}>
              <button
                onClick={() =>
                  onUpdateFrameTransform(selectedFrameIndex ?? 0, { flipX: !activeSelectedFrame.flipX })
                }
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
                  background: activeSelectedFrame.flipX ? 'rgba(236, 72, 153, 0.3)' : 'rgba(255,255,255,0.05)',
                  border: activeSelectedFrame.flipX ? '1px solid #ec4899' : '1px solid rgba(255,255,255,0.08)',
                  color: activeSelectedFrame.flipX ? '#f472b6' : '#cbd5e1',
                  cursor: 'pointer',
                }}
              >
                <FlipHorizontal size={10} /> Lật Gương
              </button>

              <button
                onClick={() =>
                  onUpdateFrameTransform(selectedFrameIndex ?? 0, { offsetX: 0, offsetY: 0, scale: 1.0, rotation: 0 })
                }
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
        )}
      </div>
    </div>
  );
};
