// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Bottom Filmstrip Frame Sequence Bar (Visibility Eye Toggle & Drag Reordering)
// =========================================================================================
import React, { useRef } from 'react';
import {
  Trash2,
  Eye,
  EyeOff,
} from 'lucide-react';
import { VideoSliceFrame } from '../../../../types/video_slicer';

export interface VideoSlicerFilmstripBarProps {
  frames: VideoSliceFrame[];
  frameOrder: number[];
  selectedFrameIndex: number | null;
  activePlaybackIndex: number;
  isPlaying: boolean;
  previewDisplayMode: 'transparent' | 'original';
  onSelectFrameIndex: (index: number) => void;
  onMoveFrame: (fromIdx: number, toIdx: number) => void;
  onToggleHideFrame: (index: number) => void;
  onDeleteFrame: (index: number) => void;
}

export const VideoSlicerFilmstripBar: React.FC<VideoSlicerFilmstripBarProps> = ({
  frames,
  frameOrder,
  selectedFrameIndex,
  activePlaybackIndex,
  isPlaying,
  previewDisplayMode,
  onSelectFrameIndex,
  onMoveFrame,
  onToggleHideFrame,
  onDeleteFrame,
}) => {
  const dragItemRef = useRef<number | null>(null);
  const dragOverItemRef = useRef<number | null>(null);

  if (frames.length === 0) {
    return null;
  }

  const handleDragStart = (orderIndex: number) => {
    dragItemRef.current = orderIndex;
  };

  const handleDragEnter = (orderIndex: number) => {
    dragOverItemRef.current = orderIndex;
  };

  const handleDragEnd = () => {
    if (dragItemRef.current !== null && dragOverItemRef.current !== null) {
      onMoveFrame(dragItemRef.current, dragOverItemRef.current);
    }
    dragItemRef.current = null;
    dragOverItemRef.current = null;
  };

  return (
    <div
      style={{
        height: 110,
        background: 'rgba(11, 15, 25, 0.98)',
        borderTop: '1px solid rgba(255, 255, 255, 0.1)',
        padding: '6px 12px',
        boxSizing: 'border-box',
        display: 'flex',
        flexDirection: 'column',
        gap: 4,
        flexShrink: 0,
        fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
      }}
    >
      {/* Top Header: Total count & Status */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div style={{ fontSize: 10.5, fontWeight: 700, color: '#f8fafc', display: 'flex', alignItems: 'center', gap: 6 }}>
          <span>🎞️ Chuỗi Khung Hình Hoạt Ảnh:</span>
          <span style={{ color: '#38bdf8' }}>{frames.length} frames</span>
          <span style={{ fontSize: 9.5, color: '#94a3b8', fontWeight: 400 }}>
            (Kéo thả để đổi thứ tự • Bấm icon mắt để Ẩn/Bật frame khi phát và xuất)
          </span>
        </div>
      </div>

      {/* Filmstrip Horizontal Scrollable Reel */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          overflowX: 'auto',
          overflowY: 'hidden',
          paddingBottom: 4,
          height: '100%',
        }}
      >
        {frameOrder.map((frameIdx, orderIdx) => {
          const frame = frames[frameIdx];
          if (!frame) return null;

          const isSelected = selectedFrameIndex === frameIdx;
          const isPlaybackActive = isPlaying && activePlaybackIndex === orderIdx;
          const isHidden = !!frame.hidden;

          return (
            <div
              key={frame.id || `frame_${frameIdx}`}
              draggable
              onDragStart={() => handleDragStart(orderIdx)}
              onDragEnter={() => handleDragEnter(orderIdx)}
              onDragEnd={handleDragEnd}
              onClick={() => onSelectFrameIndex(frameIdx)}
              style={{
                width: 72,
                height: 76,
                flexShrink: 0,
                background: '#070a13',
                border: `2px solid ${
                  isPlaybackActive
                    ? '#10b981'
                    : isSelected
                    ? '#38bdf8'
                    : isHidden
                    ? 'rgba(239, 68, 68, 0.4)'
                    : 'rgba(255, 255, 255, 0.1)'
                }`,
                borderRadius: 6,
                cursor: 'grab',
                position: 'relative',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                overflow: 'hidden',
                boxShadow: isPlaybackActive
                  ? '0 0 10px rgba(16, 185, 129, 0.5)'
                  : isSelected
                  ? '0 0 10px rgba(56, 189, 248, 0.4)'
                  : 'none',
                opacity: isHidden ? 0.4 : 1,
                filter: isHidden ? 'grayscale(0.7)' : 'none',
                transition: 'all 0.1s ease',
              }}
            >
              {/* Frame Image Thumbnail */}
              <img
                src={
                  previewDisplayMode === 'transparent'
                    ? frame.transparentDataUrl
                    : frame.originalDataUrl
                }
                alt={`Frame ${orderIdx + 1}`}
                style={{
                  width: '100%',
                  height: '100%',
                  objectFit: 'contain',
                  transform: `translate(${frame.offsetX * 0.1}px, ${frame.offsetY * 0.1}px) scale(${frame.scale}) rotate(${frame.rotation}deg) ${frame.flipX ? 'scaleX(-1)' : ''}`,
                }}
              />

              {/* Order Index Badge */}
              <div
                style={{
                  position: 'absolute',
                  top: 2,
                  left: 2,
                  background: isPlaybackActive ? '#10b981' : isSelected ? '#0284c7' : 'rgba(0, 0, 0, 0.7)',
                  color: '#fff',
                  fontSize: 8.5,
                  fontWeight: 800,
                  padding: '1px 4px',
                  borderRadius: 3,
                }}
              >
                #{orderIdx + 1}
              </div>

              {/* Hidden Status Badge */}
              {isHidden && (
                <div
                  style={{
                    position: 'absolute',
                    top: 2,
                    right: 2,
                    background: '#ef4444',
                    color: '#fff',
                    fontSize: 7.5,
                    fontWeight: 900,
                    padding: '1px 3px',
                    borderRadius: 2,
                  }}
                >
                  ẨN
                </div>
              )}

              {/* Bottom Quick Action Overlay on Hover */}
              <div
                style={{
                  position: 'absolute',
                  bottom: 0,
                  left: 0,
                  right: 0,
                  background: 'rgba(0, 0, 0, 0.85)',
                  display: 'flex',
                  justifyContent: 'space-between',
                  padding: '2px 4px',
                  alignItems: 'center',
                }}
              >
                {/* Eye Toggle Visibility Button (Replaced Duplicate) */}
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    onToggleHideFrame(frameIdx);
                  }}
                  title={isHidden ? 'Bật lại frame này vào hoạt ảnh' : 'Ẩn frame này khỏi hoạt ảnh'}
                  style={{
                    background: 'transparent',
                    border: 'none',
                    color: isHidden ? '#ef4444' : '#34d399',
                    cursor: 'pointer',
                    padding: 1,
                    display: 'flex',
                  }}
                >
                  {isHidden ? <EyeOff size={11} /> : <Eye size={11} />}
                </button>

                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    onDeleteFrame(frameIdx);
                  }}
                  title="Xóa frame"
                  style={{
                    background: 'transparent',
                    border: 'none',
                    color: '#ef4444',
                    cursor: 'pointer',
                    padding: 1,
                    display: 'flex',
                  }}
                >
                  <Trash2 size={11} />
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
