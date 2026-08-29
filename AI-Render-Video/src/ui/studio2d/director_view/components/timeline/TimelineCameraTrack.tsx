import React from 'react';
import { Camera } from 'lucide-react';
import { MultiAngleDirectorShot } from '../../../../../types/studio2d_director';

interface TimelineCameraTrackProps {
  activeShotId: string;
  totalDuration: number;
  shotTimeline: { shot: MultiAngleDirectorShot; start: number; end: number }[];
  onSelectShot: (shotId: string) => void;
  onSeek: (time: number) => void;
  onTrackClick: (e: React.MouseEvent<HTMLDivElement>) => void;
}

export const TimelineCameraTrack: React.FC<TimelineCameraTrackProps> = ({
  activeShotId,
  totalDuration,
  shotTimeline,
  onSelectShot,
  onSeek,
  onTrackClick,
}) => {
  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        height: 24,
        borderRadius: 4,
        background: 'rgba(56, 189, 248, 0.04)',
        border: '1px solid rgba(56, 189, 248, 0.12)',
        flexShrink: 0,
      }}
    >
      {/* Header */}
      <div
        style={{
          width: 140,
          flexShrink: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 6px',
          fontSize: 10,
          fontWeight: 700,
          color: '#38bdf8',
        }}
      >
        <span style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
          <Camera size={12} color="#38bdf8" />
          <span>Góc Máy & Shot</span>
        </span>
      </div>

      {/* Lane */}
      <div
        onClick={onTrackClick}
        style={{
          flex: 1,
          height: '100%',
          position: 'relative',
          display: 'flex',
          gap: 2,
          padding: '2px 0',
          cursor: 'pointer',
        }}
      >
        {shotTimeline.map(({ shot, start }, idx) => {
          const widthPct = totalDuration > 0 ? (shot.durationSeconds / totalDuration) * 100 : 0;
          const isSelected = shot.id === activeShotId;
          const isTopDown = (shot.camera.pitchStart ?? 0) >= 45;

          return (
            <div
              key={`cam_${shot.id}`}
              onClick={(e) => {
                e.stopPropagation();
                onSelectShot(shot.id);
                onSeek(start);
              }}
              title={`Shot #${idx + 1}: Góc ${shot.camera.angleStart}°, Pitch: ${shot.camera.pitchStart || 0}°. Bấm để chuyển Shot.`}
              style={{
                width: `${widthPct}%`,
                height: '100%',
                borderRadius: 3,
                background: isSelected
                  ? 'linear-gradient(135deg, rgba(2, 132, 199, 0.4), rgba(56, 189, 248, 0.25))'
                  : 'rgba(255, 255, 255, 0.04)',
                border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.08)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '0 5px',
                fontSize: 8.5,
                fontWeight: 700,
                color: isSelected ? '#38bdf8' : '#94a3b8',
                overflow: 'hidden',
                whiteSpace: 'nowrap',
                cursor: 'pointer',
              }}
            >
              <span>#{idx + 1}</span>
              <span style={{ fontSize: 8, color: isTopDown ? '#facc15' : '#4ade80' }}>
                {isTopDown ? '👑 Top' : `🎥 ${shot.camera.angleStart}°`}
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
};
