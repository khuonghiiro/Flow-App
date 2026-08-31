// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// 3-Pin Video Timeline Range Bar (Customizable Scan Duration Input)
// =========================================================================================
import React, { useRef, useState, useCallback, useEffect } from 'react';
import {
  Play,
  Pause,
  Repeat,
  MapPin,
  Square,
  Loader,
  Target,
  Sparkles,
} from 'lucide-react';
import { VideoMetadata } from '../../../../types/video_slicer';

export interface VideoSlicerTimelineRangeBarProps {
  videoMetadata: VideoMetadata | null;
  videoCurrentTime: number;
  onUserSeekVideoTime: (time: number) => void;
  startTime: number;
  setStartTime: (time: number) => void;
  endTime: number;
  setEndTime: (time: number) => void;
  isVideoPlaying: boolean;
  onTogglePlayVideo: () => void;
  isLooping: boolean;
  setIsLooping: (loop: boolean) => void;

  // Auto Loop Matcher Props
  isAutoFindEnd: boolean;
  setIsAutoFindEnd: (v: boolean) => void;
  maxSearchDuration: number;
  setMaxSearchDuration: (v: number) => void;
  isSearchingEnd: boolean;
  searchProgress: number;
  searchStatusText: string;
  onTriggerSearchEnd: () => void;
  onStopSearch: () => void;
  onStartPinReleased: (newStartTime: number) => void;
}

export const VideoSlicerTimelineRangeBar: React.FC<VideoSlicerTimelineRangeBarProps> = ({
  videoMetadata,
  videoCurrentTime,
  onUserSeekVideoTime,
  startTime,
  setStartTime,
  endTime,
  setEndTime,
  isVideoPlaying,
  onTogglePlayVideo,
  isLooping,
  setIsLooping,
  isAutoFindEnd,
  setIsAutoFindEnd,
  maxSearchDuration,
  setMaxSearchDuration,
  isSearchingEnd,
  searchProgress,
  searchStatusText,
  onTriggerSearchEnd,
  onStopSearch,
  onStartPinReleased,
}) => {
  const trackRef = useRef<HTMLDivElement>(null);
  const [draggingPin, setDraggingPin] = useState<'start' | 'end' | 'play' | null>(null);

  const duration = Math.max(0.1, videoMetadata?.duration || 1);
  const clipEnd = Math.min(endTime || duration, duration);
  const clipStart = Math.max(0, Math.min(startTime, clipEnd));

  const startPercent = Math.max(0, Math.min(100, (clipStart / duration) * 100));
  const endPercent = Math.max(0, Math.min(100, (clipEnd / duration) * 100));
  const playPercent = Math.max(0, Math.min(100, (videoCurrentTime / duration) * 100));

  // Convert mouse X client position to seconds on timeline
  const getTimeFromMouseEvent = useCallback(
    (e: MouseEvent | React.MouseEvent): number => {
      if (!trackRef.current) return 0;
      const rect = trackRef.current.getBoundingClientRect();
      const x = Math.max(0, Math.min(e.clientX - rect.left, rect.width));
      const ratio = x / rect.width;
      return ratio * duration;
    },
    [duration]
  );

  // Global mouse handlers for smooth pin dragging
  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (!draggingPin) return;
      const t = getTimeFromMouseEvent(e);

      if (draggingPin === 'start') {
        const newStart = Math.max(0, Math.min(t, clipEnd - 0.05));
        setStartTime(Number(newStart.toFixed(2)));
      } else if (draggingPin === 'end') {
        const newEnd = Math.min(duration, Math.max(t, clipStart + 0.05));
        setEndTime(Number(newEnd.toFixed(2)));
      } else if (draggingPin === 'play') {
        const newPlay = Math.max(0, Math.min(t, duration));
        onUserSeekVideoTime(Number(newPlay.toFixed(2)));
      }
    };

    const handleMouseUp = () => {
      if (draggingPin === 'start') {
        // Trigger auto search if enabled
        onStartPinReleased(clipStart);
      }
      if (draggingPin) setDraggingPin(null);
    };

    if (draggingPin) {
      window.addEventListener('mousemove', handleMouseMove);
      window.addEventListener('mouseup', handleMouseUp);
    }
    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('mouseup', handleMouseUp);
    };
  }, [draggingPin, getTimeFromMouseEvent, clipStart, clipEnd, duration, setStartTime, setEndTime, onUserSeekVideoTime, onStartPinReleased]);

  return (
    <div
      style={{
        width: '100%',
        background: 'rgba(11, 15, 25, 0.98)',
        borderTop: '1px solid rgba(255, 255, 255, 0.1)',
        padding: '8px 12px',
        boxSizing: 'border-box',
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        userSelect: 'none',
        flexShrink: 0,
      }}
    >
      {/* ─── 1. TIMELINE CONTROLS & AUTO LOOP MATCHER BAR ─────── */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, flexWrap: 'wrap' }}>
        {/* Left: Play/Pause & Auto Find Loop Controls */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <button
            onClick={onTogglePlayVideo}
            disabled={!videoMetadata}
            style={{
              background: isVideoPlaying ? '#ef4444' : '#0284c7',
              border: 'none',
              borderRadius: 5,
              color: '#fff',
              padding: '4px 10px',
              fontSize: 10,
              fontWeight: 700,
              cursor: videoMetadata ? 'pointer' : 'default',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              boxShadow: isVideoPlaying ? '0 0 10px rgba(239, 68, 68, 0.4)' : '0 0 8px rgba(2, 132, 199, 0.3)',
            }}
          >
            {isVideoPlaying ? <Pause size={12} /> : <Play size={12} />}
            {isVideoPlaying ? 'Dừng' : 'Phát'}
          </button>

          {/* Toggle Auto Find Loop End on Start Pin Release */}
          <button
            onClick={() => setIsAutoFindEnd(!isAutoFindEnd)}
            title="Tự động tìm Ghim End khớp vòng lặp khi thả Ghim Start"
            style={{
              background: isAutoFindEnd ? 'rgba(245, 158, 11, 0.25)' : 'rgba(255, 255, 255, 0.05)',
              border: `1px solid ${isAutoFindEnd ? '#f59e0b' : 'rgba(255, 255, 255, 0.1)'}`,
              borderRadius: 4,
              color: isAutoFindEnd ? '#fbbf24' : '#94a3b8',
              padding: '3px 7px',
              fontSize: 9,
              fontWeight: 700,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            <Target size={11} /> Auto Tìm End: {isAutoFindEnd ? 'Bật' : 'Tắt'}
          </button>

          {/* Custom Search Duration Textbox */}
          <div
            title="Nhập số giây tối đa để quét tìm frame khớp loop tính từ mốc Start"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 3,
              background: 'rgba(0, 0, 0, 0.4)',
              border: '1px solid rgba(255, 255, 255, 0.12)',
              borderRadius: 4,
              padding: '2px 5px',
              fontSize: 9,
              color: '#94a3b8',
            }}
          >
            <span>Quét:</span>
            <input
              type="number"
              min={0.5}
              max={Math.max(10, Math.ceil(duration))}
              step={0.5}
              value={maxSearchDuration}
              onChange={(e) => setMaxSearchDuration(Math.max(0.5, Number(e.target.value)))}
              style={{
                width: 36,
                background: '#090e1a',
                border: '1px solid rgba(56, 189, 248, 0.3)',
                borderRadius: 3,
                color: '#38bdf8',
                fontSize: 9,
                fontWeight: 700,
                textAlign: 'center',
                padding: '1px 2px',
              }}
            />
            <span>giây</span>
          </div>

          {/* Manual Search Button / Cancel Search Button */}
          {isSearchingEnd ? (
            <button
              onClick={onStopSearch}
              title="Dừng tìm kiếm ngay lập tức"
              style={{
                background: '#dc2626',
                border: 'none',
                borderRadius: 4,
                color: '#fff',
                padding: '3px 8px',
                fontSize: 9,
                fontWeight: 700,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                boxShadow: '0 0 8px rgba(220, 38, 38, 0.5)',
              }}
            >
              <Square size={10} fill="#fff" /> Dừng Tìm ({searchProgress}%)
            </button>
          ) : (
            <button
              onClick={onTriggerSearchEnd}
              disabled={!videoMetadata}
              title={`Quét từng frame trong ${maxSearchDuration}s tiếp theo để tìm frame khớp Start nhất`}
              style={{
                background: 'rgba(16, 185, 129, 0.2)',
                border: '1px solid rgba(16, 185, 129, 0.3)',
                borderRadius: 4,
                color: '#34d399',
                padding: '3px 8px',
                fontSize: 9,
                fontWeight: 700,
                cursor: videoMetadata ? 'pointer' : 'default',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
              }}
            >
              <Sparkles size={11} /> Quét Tìm End
            </button>
          )}
        </div>

        {/* Center: Search Status Text or Time Info */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 10, color: '#94a3b8' }}>
          {isSearchingEnd ? (
            <span style={{ color: '#fbbf24', display: 'flex', alignItems: 'center', gap: 4, fontWeight: 600 }}>
              <Loader size={11} className="spin" /> {searchStatusText}
            </span>
          ) : (
            <span>
              Đoạn cắt:{' '}
              <strong style={{ color: '#38bdf8', fontFamily: 'monospace' }}>
                {clipStart.toFixed(2)}s
              </strong>{' '}
              ➜{' '}
              <strong style={{ color: '#f87171', fontFamily: 'monospace' }}>
                {clipEnd.toFixed(2)}s
              </strong>{' '}
              <span style={{ color: '#34d399' }}>({(clipEnd - clipStart).toFixed(2)}s)</span>
            </span>
          )}
        </div>

        {/* Right: Loop Video Button */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <button
            onClick={() => setIsLooping(!isLooping)}
            title={isLooping ? 'Đang bật lặp video liên tục' : 'Bật lặp video liên tục'}
            style={{
              background: isLooping ? 'rgba(16, 185, 129, 0.25)' : 'rgba(255, 255, 255, 0.05)',
              border: `1px solid ${isLooping ? '#10b981' : 'rgba(255, 255, 255, 0.1)'}`,
              borderRadius: 4,
              color: isLooping ? '#34d399' : '#94a3b8',
              padding: '4px 8px',
              fontSize: 9,
              fontWeight: 700,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            <Repeat size={11} /> Lặp Video: {isLooping ? 'Bật' : 'Tắt'}
          </button>
        </div>
      </div>

      {/* ─── 2. 58PX TALL 3-PIN TIMELINE TRACK CANVAS ──────────── */}
      <div
        ref={trackRef}
        onMouseDown={(e) => {
          // If user clicks track background directly, seek playhead
          const t = getTimeFromMouseEvent(e);
          onUserSeekVideoTime(Number(t.toFixed(2)));
          setDraggingPin('play');
        }}
        style={{
          position: 'relative',
          width: '100%',
          height: 58,
          background: '#070a12',
          borderRadius: 8,
          border: '1.5px solid rgba(255, 255, 255, 0.15)',
          cursor: 'pointer',
          overflow: 'visible',
          marginTop: 2,
          boxShadow: 'inset 0 2px 6px rgba(0, 0, 0, 0.6)',
        }}
      >
        {/* Highlighted Selected Range Shading */}
        <div
          style={{
            position: 'absolute',
            left: `${startPercent}%`,
            width: `${Math.max(0, endPercent - startPercent)}%`,
            top: 0,
            bottom: 0,
            background: 'linear-gradient(90deg, rgba(2, 132, 199, 0.35), rgba(16, 185, 129, 0.35))',
            borderLeft: '2.5px solid #38bdf8',
            borderRight: '2.5px solid #ef4444',
            pointerEvents: 'none',
            boxShadow: '0 0 14px rgba(56, 189, 248, 0.25)',
          }}
        />

        {/* Center Timeline Guideline */}
        <div
          style={{
            position: 'absolute',
            left: 0,
            right: 0,
            top: 28,
            height: 2,
            background: 'rgba(255, 255, 255, 0.18)',
            pointerEvents: 'none',
          }}
        />

        {/* ─── TOP PIN 1: GHIM START (IN-POINT) ────────────────── */}
        <div
          onMouseDown={(e) => {
            e.stopPropagation();
            setDraggingPin('start');
          }}
          title={`Ghim Bắt Đầu: ${clipStart.toFixed(2)}s (Kéo để chỉnh mốc Start)`}
          style={{
            position: 'absolute',
            left: `${startPercent}%`,
            top: -7,
            transform: 'translateX(-50%)',
            zIndex: 15,
            cursor: 'ew-resize',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
          }}
        >
          <div
            style={{
              padding: '2px 6px',
              borderRadius: 4,
              background: 'linear-gradient(135deg, #0284c7, #0369a1)',
              color: '#fff',
              fontSize: 9,
              fontWeight: 800,
              boxShadow: '0 2px 8px rgba(2, 132, 199, 0.6)',
              display: 'flex',
              alignItems: 'center',
              gap: 3,
              border: '1px solid rgba(255, 255, 255, 0.3)',
            }}
          >
            <MapPin size={9} /> Start: {clipStart.toFixed(2)}s
          </div>
          <div style={{ width: 3, height: 26, background: '#38bdf8', boxShadow: '0 0 6px #38bdf8' }} />
        </div>

        {/* ─── TOP PIN 2: GHIM END (OUT-POINT) ─────────────────── */}
        <div
          onMouseDown={(e) => {
            e.stopPropagation();
            setDraggingPin('end');
          }}
          title={`Ghim Kết Thúc: ${clipEnd.toFixed(2)}s (Kéo để chỉnh mốc End)`}
          style={{
            position: 'absolute',
            left: `${endPercent}%`,
            top: -7,
            transform: 'translateX(-50%)',
            zIndex: 15,
            cursor: 'ew-resize',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
          }}
        >
          <div
            style={{
              padding: '2px 6px',
              borderRadius: 4,
              background: 'linear-gradient(135deg, #dc2626, #b91c1c)',
              color: '#fff',
              fontSize: 9,
              fontWeight: 800,
              boxShadow: '0 2px 8px rgba(220, 38, 38, 0.6)',
              display: 'flex',
              alignItems: 'center',
              gap: 3,
              border: '1px solid rgba(255, 255, 255, 0.3)',
            }}
          >
            <MapPin size={9} /> End: {clipEnd.toFixed(2)}s
          </div>
          <div style={{ width: 3, height: 26, background: '#ef4444', boxShadow: '0 0 6px #ef4444' }} />
        </div>

        {/* ─── BOTTOM PIN 3: GHIM PLAY (PLAYHEAD NEEDLE) ───────── */}
        <div
          onMouseDown={(e) => {
            e.stopPropagation();
            setDraggingPin('play');
          }}
          title={`Vị trí phát: ${videoCurrentTime.toFixed(2)}s`}
          style={{
            position: 'absolute',
            left: `${playPercent}%`,
            bottom: -7,
            transform: 'translateX(-50%)',
            zIndex: 20,
            cursor: 'ew-resize',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
          }}
        >
          <div style={{ width: 3, height: 28, background: '#fbbf24', boxShadow: '0 0 10px rgba(251, 191, 36, 0.9)' }} />
          <div
            style={{
              padding: '2px 7px',
              borderRadius: 4,
              background: 'linear-gradient(135deg, #f59e0b, #d97706)',
              color: '#000',
              fontSize: 9,
              fontWeight: 900,
              boxShadow: '0 2px 10px rgba(245, 158, 11, 0.7)',
              display: 'flex',
              alignItems: 'center',
              gap: 3,
              border: '1px solid #fff',
            }}
          >
            ▲ Phát: {videoCurrentTime.toFixed(2)}s
          </div>
        </div>
      </div>
    </div>
  );
};
