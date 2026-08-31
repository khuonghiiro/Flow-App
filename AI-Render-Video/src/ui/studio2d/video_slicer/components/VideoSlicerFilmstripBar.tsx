// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Slicer Bottom Filmstrip Frame Sequence Bar
// =========================================================================================
import React, { useRef } from 'react';
import {
  ChevronLeft,
  ChevronRight,
  Plus,
  Trash2,
  Copy,
  Film,
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
  onDuplicateFrame: (index: number) => void;
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
  onDuplicateFrame,
  onDeleteFrame,
}) => {
  const scrollContainerRef = useRef<HTMLDivElement>(null);

  const scrollLeft = () => {
    if (scrollContainerRef.current) {
      scrollContainerRef.current.scrollBy({ left: -240, behavior: 'smooth' });
    }
  };

  const scrollRight = () => {
    if (scrollContainerRef.current) {
      scrollContainerRef.current.scrollBy({ left: 240, behavior: 'smooth' });
    }
  };

  return (
    <div
      style={{
        height: 125,
        background: 'rgba(15, 23, 42, 0.95)',
        borderTop: '1px solid rgba(255, 255, 255, 0.08)',
        borderRadius: '0 0 8px 8px',
        display: 'flex',
        alignItems: 'center',
        padding: '6px 10px',
        gap: 8,
        boxSizing: 'border-box',
        flexShrink: 0,
        position: 'relative',
      }}
    >
      {/* Filmstrip Header & Badge */}
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          width: 90,
          flexShrink: 0,
          borderRight: '1px solid rgba(255, 255, 255, 0.08)',
          paddingRight: 8,
        }}
      >
        <Film size={18} color="#38bdf8" />
        <div style={{ fontSize: 10, fontWeight: 700, color: '#f8fafc', marginTop: 2 }}>
          Chuỗi Frame
        </div>
        <div style={{ fontSize: 9, color: '#94a3b8' }}>
          {frames.length} khung hình
        </div>
      </div>

      {/* Scroll Left Button */}
      <button
        onClick={scrollLeft}
        title="Cuộn sang trái"
        style={{
          width: 24,
          height: '100%',
          background: 'rgba(255, 255, 255, 0.03)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          borderRadius: 4,
          color: '#94a3b8',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          cursor: 'pointer',
          flexShrink: 0,
        }}
      >
        <ChevronLeft size={16} />
      </button>

      {/* Frame Thumbnails Carousel Container */}
      <div
        ref={scrollContainerRef}
        style={{
          flex: 1,
          height: '100%',
          display: 'flex',
          gap: 8,
          overflowX: 'auto',
          overflowY: 'hidden',
          alignItems: 'center',
          padding: '4px 0',
          scrollbarWidth: 'thin',
        }}
      >
        {frameOrder.length === 0 ? (
          <div style={{ color: '#64748b', fontSize: 11, fontStyle: 'italic', margin: 'auto' }}>
            Chưa có frame nào. Hãy nạp video và trích xuất chuỗi frame để hiển thị tại đây.
          </div>
        ) : (
          frameOrder.map((frameIdx, seqIdx) => {
            const frame = frames[frameIdx];
            if (!frame) return null;

            const isSelected = selectedFrameIndex === frameIdx;
            const isCurrentlyPlaying = isPlaying && activePlaybackIndex === seqIdx;

            return (
              <div
                key={frame.id || `frame_${seqIdx}`}
                onClick={() => onSelectFrameIndex(frameIdx)}
                style={{
                  width: 84,
                  height: '100%',
                  background: isSelected
                    ? 'rgba(2, 132, 199, 0.25)'
                    : isCurrentlyPlaying
                    ? 'rgba(239, 68, 68, 0.25)'
                    : 'rgba(30, 41, 59, 0.5)',
                  border: isSelected
                    ? '2px solid #38bdf8'
                    : isCurrentlyPlaying
                    ? '2px solid #ef4444'
                    : '1px solid rgba(255, 255, 255, 0.1)',
                  borderRadius: 6,
                  display: 'flex',
                  flexDirection: 'column',
                  padding: 3,
                  boxSizing: 'border-box',
                  cursor: 'pointer',
                  flexShrink: 0,
                  position: 'relative',
                  transition: 'all 0.15s ease',
                  boxShadow: isSelected
                    ? '0 0 12px rgba(56, 189, 248, 0.4)'
                    : isCurrentlyPlaying
                    ? '0 0 12px rgba(239, 68, 68, 0.4)'
                    : 'none',
                }}
              >
                {/* Frame Index & Duration Badge */}
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    fontSize: 8,
                    fontWeight: 700,
                    color: isSelected ? '#38bdf8' : '#94a3b8',
                    marginBottom: 2,
                    padding: '0 2px',
                  }}
                >
                  <span>F{seqIdx + 1}</span>
                  <span style={{ fontSize: 7, color: '#64748b' }}>{frame.durationMs}ms</span>
                </div>

                {/* Thumbnail Image */}
                <div
                  style={{
                    flex: 1,
                    width: '100%',
                    minHeight: 0,
                    backgroundImage:
                      'linear-gradient(45deg, #182032 25%, transparent 25%), linear-gradient(-45deg, #182032 25%, transparent 25%), linear-gradient(45deg, transparent 75%, #182032 75%), linear-gradient(-45deg, transparent 75%, #182032 75%)',
                    backgroundSize: '8px 8px',
                    backgroundPosition: '0 0, 0 4px, 4px -4px, -4px 0px',
                    backgroundColor: '#0b0f19',
                    borderRadius: 4,
                    overflow: 'hidden',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  <img
                    src={
                      previewDisplayMode === 'transparent'
                        ? frame.transparentDataUrl
                        : frame.originalDataUrl
                    }
                    alt={`Frame ${seqIdx + 1}`}
                    style={{
                      maxWidth: '100%',
                      maxHeight: '100%',
                      objectFit: 'contain',
                      transform: `${frame.flipX ? 'scaleX(-1)' : ''}`,
                    }}
                  />
                </div>

                {/* Quick Frame Action Toolbar */}
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    marginTop: 2,
                    paddingTop: 1,
                  }}
                >
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      if (seqIdx > 0) onMoveFrame(seqIdx, seqIdx - 1);
                    }}
                    disabled={seqIdx === 0}
                    title="Dời sang trái"
                    style={{
                      background: 'transparent',
                      border: 'none',
                      color: seqIdx > 0 ? '#94a3b8' : '#475569',
                      cursor: seqIdx > 0 ? 'pointer' : 'default',
                      padding: 1,
                      fontSize: 8,
                    }}
                  >
                    ◀
                  </button>

                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      onDuplicateFrame(frameIdx);
                    }}
                    title="Nhân bản frame"
                    style={{
                      background: 'transparent',
                      border: 'none',
                      color: '#94a3b8',
                      cursor: 'pointer',
                      padding: 1,
                    }}
                  >
                    <Copy size={9} />
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
                    }}
                  >
                    <Trash2 size={9} />
                  </button>

                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      if (seqIdx < frameOrder.length - 1) onMoveFrame(seqIdx, seqIdx + 1);
                    }}
                    disabled={seqIdx === frameOrder.length - 1}
                    title="Dời sang phải"
                    style={{
                      background: 'transparent',
                      border: 'none',
                      color: seqIdx < frameOrder.length - 1 ? '#94a3b8' : '#475569',
                      cursor: seqIdx < frameOrder.length - 1 ? 'pointer' : 'default',
                      padding: 1,
                      fontSize: 8,
                    }}
                  >
                    ▶
                  </button>
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Scroll Right Button */}
      <button
        onClick={scrollRight}
        title="Cuộn sang phải"
        style={{
          width: 24,
          height: '100%',
          background: 'rgba(255, 255, 255, 0.03)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          borderRadius: 4,
          color: '#94a3b8',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          cursor: 'pointer',
          flexShrink: 0,
        }}
      >
        <ChevronRight size={16} />
      </button>
    </div>
  );
};
