import React, { useState, useRef } from 'react';
import { formatTimeWithMs } from './timelineConstants';

interface TimelineTrackRulerProps {
  currentTime: number;
  totalDuration: number;
  onSeek: (time: number) => void;
}

export const TimelineTrackRuler: React.FC<TimelineTrackRulerProps> = ({
  currentTime,
  totalDuration,
  onSeek,
}) => {
  const [hoverTime, setHoverTime] = useState<number | null>(null);
  const [hoverX, setHoverX] = useState<number>(0);
  const rulerRef = useRef<HTMLDivElement>(null);

  const calculateTimeFromX = (clientX: number) => {
    if (!rulerRef.current || totalDuration <= 0) return 0;
    const rect = rulerRef.current.getBoundingClientRect();
    const x = clientX - rect.left;
    const pct = Math.max(0, Math.min(1, x / rect.width));
    return pct * totalDuration;
  };

  const handleMouseDown = (e: React.MouseEvent<HTMLDivElement>) => {
    e.preventDefault();
    const newTime = calculateTimeFromX(e.clientX);
    onSeek(newTime);

    const handleMouseMove = (moveEvent: MouseEvent) => {
      const draggedTime = calculateTimeFromX(moveEvent.clientX);
      onSeek(draggedTime);
      if (rulerRef.current) {
        const rect = rulerRef.current.getBoundingClientRect();
        setHoverX(moveEvent.clientX - rect.left);
        setHoverTime(draggedTime);
      }
    };

    const handleMouseUp = () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('mouseup', handleMouseUp);
    };

    window.addEventListener('mousemove', handleMouseMove);
    window.addEventListener('mouseup', handleMouseUp);
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!rulerRef.current) return;
    const rect = rulerRef.current.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const pct = Math.max(0, Math.min(1, x / rect.width));
    setHoverX(x);
    setHoverTime(pct * totalDuration);
  };

  const handleMouseLeave = () => {
    setHoverTime(null);
  };

  // Determine tick step based on total duration
  const subTickStep = totalDuration > 30 ? 0.5 : totalDuration > 15 ? 0.25 : 0.1;
  const totalSubTicks = Math.ceil(totalDuration / subTickStep);
  const activePct = totalDuration > 0 ? (currentTime / totalDuration) * 100 : 0;

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        height: 28,
        background: 'rgba(9, 13, 22, 0.95)',
        borderBottom: '1px solid rgba(56, 189, 248, 0.25)',
        flexShrink: 0,
        userSelect: 'none',
      }}
    >
      {/* Header */}
      <div
        style={{
          width: 140,
          flexShrink: 0,
          fontSize: 9.5,
          fontWeight: 800,
          color: '#38bdf8',
          display: 'flex',
          alignItems: 'center',
          gap: 4,
          paddingLeft: 6,
          letterSpacing: '0.5px',
        }}
      >
        <span>📏 GIÂY (s) & ms</span>
      </div>

      {/* Precision Ruler Lane with drag & hover */}
      <div
        ref={rulerRef}
        onMouseDown={handleMouseDown}
        onMouseMove={handleMouseMove}
        onMouseLeave={handleMouseLeave}
        style={{
          flex: 1,
          height: '100%',
          position: 'relative',
          cursor: 'ew-resize',
          overflow: 'hidden',
        }}
      >
        {/* Render Ruler Marks */}
        {Array.from({ length: totalSubTicks + 1 }).map((_, idx) => {
          const t = idx * subTickStep;
          if (t > totalDuration) return null;
          const leftPct = totalDuration > 0 ? (t / totalDuration) * 100 : 0;
          const isMajorSecond = Math.abs(t - Math.round(t)) < 0.001;
          const isHalfSecond = Math.abs(t % 0.5) < 0.001 && !isMajorSecond;

          return (
            <div
              key={idx}
              style={{
                position: 'absolute',
                left: `${leftPct}%`,
                top: 0,
                bottom: 0,
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'space-between',
                pointerEvents: 'none',
              }}
            >
              {/* Number centered along the vertical ruler tick line */}
              {isMajorSecond ? (
                <span
                  style={{
                    transform: 'translateX(-50%)',
                    fontSize: 10,
                    fontWeight: 800,
                    color: '#38bdf8',
                    fontFamily: 'monospace',
                    lineHeight: '12px',
                  }}
                >
                  {Math.round(t)}
                </span>
              ) : isHalfSecond && totalDuration <= 15 ? (
                <span
                  style={{
                    transform: 'translateX(-50%)',
                    fontSize: 7.5,
                    fontWeight: 600,
                    color: '#64748b',
                    fontFamily: 'monospace',
                    lineHeight: '12px',
                  }}
                >
                  .5
                </span>
              ) : (
                <div style={{ height: 12 }} />
              )}

              {/* Tick Notch Line */}
              <div
                style={{
                  width: isMajorSecond ? 1.5 : 1,
                  height: isMajorSecond ? 10 : isHalfSecond ? 6 : 3.5,
                  background: isMajorSecond
                    ? '#38bdf8'
                    : isHalfSecond
                    ? 'rgba(56, 189, 248, 0.5)'
                    : 'rgba(255, 255, 255, 0.15)',
                }}
              />
            </div>
          );
        })}

        {/* Active Time Indicator on Ruler */}
        {totalDuration > 0 && (
          <div
            style={{
              position: 'absolute',
              top: 0,
              bottom: 0,
              left: `${activePct}%`,
              width: 1.5,
              background: '#f43f5e',
              boxShadow: '0 0 8px #f43f5e',
              pointerEvents: 'none',
              zIndex: 35,
            }}
          >
            <div
              style={{
                position: 'absolute',
                top: 1,
                left: 4,
                background: 'linear-gradient(135deg, #f43f5e, #be123c)',
                color: '#fff',
                padding: '1px 5px',
                borderRadius: 3,
                border: '1px solid #fda4af',
                fontSize: 9,
                fontWeight: 800,
                fontFamily: 'monospace',
                whiteSpace: 'nowrap',
                boxShadow: '0 2px 8px rgba(244, 63, 94, 0.6)',
              }}
            >
              ⏱️ {formatTimeWithMs(currentTime)}
            </div>
          </div>
        )}

        {/* Live Hover Tooltip & Red Hairline on Hover */}
        {hoverTime !== null && (
          <div
            style={{
              position: 'absolute',
              top: 0,
              bottom: 0,
              left: hoverX,
              width: 1,
              background: 'rgba(244, 63, 94, 0.5)',
              pointerEvents: 'none',
              zIndex: 30,
            }}
          >
            <div
              style={{
                position: 'absolute',
                top: 1,
                left: 4,
                background: 'rgba(15, 23, 42, 0.95)',
                color: '#fda4af',
                padding: '1px 5px',
                borderRadius: 3,
                border: '1px solid rgba(244, 63, 94, 0.4)',
                fontSize: 8.5,
                fontWeight: 700,
                fontFamily: 'monospace',
                whiteSpace: 'nowrap',
              }}
            >
              {formatTimeWithMs(hoverTime)}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
