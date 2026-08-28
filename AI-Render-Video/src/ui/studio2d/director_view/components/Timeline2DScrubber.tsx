import React from 'react';
import {
  Play,
  Pause,
  RotateCcw,
  Film,
  Camera,
  MessageSquare,
  Sparkles,
  Zap,
  Repeat,
} from 'lucide-react';
import {
  Director2DProject,
  MultiAngleDirectorShot,
} from '../../../../types/studio2d_director';

interface Timeline2DScrubberProps {
  project: Director2DProject;
  currentTime: number;
  isPlaying: boolean;
  onTogglePlay: () => void;
  onSeek: (time: number) => void;
  activeShotId: string;
  onSelectShot: (shotId: string) => void;
}

export const Timeline2DScrubber: React.FC<Timeline2DScrubberProps> = ({
  project,
  currentTime,
  isPlaying,
  onTogglePlay,
  onSeek,
  activeShotId,
  onSelectShot,
}) => {
  const totalDuration = project.shots.reduce((sum, s) => sum + s.durationSeconds, 0);

  // Compute start & end timestamps for each shot
  let accumulatedTime = 0;
  const shotTimeline = project.shots.map((shot) => {
    const start = accumulatedTime;
    const end = accumulatedTime + shot.durationSeconds;
    accumulatedTime = end;
    return { shot, start, end };
  });

  const handleTrackClick = (e: React.MouseEvent<HTMLDivElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const clickX = e.clientX - rect.left;
    const pct = Math.max(0, Math.min(1, clickX / rect.width));
    onSeek(pct * totalDuration);
  };

  // Format digital timecode (MM:SS.ms)
  const formatTimecode = (sec: number) => {
    const m = Math.floor(sec / 60);
    const s = Math.floor(sec % 60);
    const ms = Math.floor((sec % 1) * 100);
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}.${ms.toString().padStart(2, '0')}`;
  };

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        background: 'linear-gradient(180deg, rgba(15, 23, 42, 0.95) 0%, rgba(9, 13, 22, 0.98) 100%)',
        border: '1px solid rgba(56, 189, 248, 0.2)',
        borderRadius: 10,
        padding: '10px 14px',
        boxShadow: '0 8px 30px rgba(0, 0, 0, 0.7), 0 0 20px rgba(56, 189, 248, 0.05)',
      }}
    >
      {/* ─── TOP PLAYBACK TOOLBAR ──────────────────────────────────────── */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          {/* Play/Pause Button */}
          <button
            onClick={onTogglePlay}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: 34,
              height: 34,
              borderRadius: 8,
              background: isPlaying
                ? 'linear-gradient(135deg, #ef4444, #dc2626)'
                : 'linear-gradient(135deg, #0284c7, #38bdf8)',
              color: '#fff',
              border: 'none',
              cursor: 'pointer',
              boxShadow: isPlaying ? '0 0 14px rgba(239,68,68,0.5)' : '0 0 14px rgba(56,189,248,0.5)',
              transition: 'all 0.15s',
            }}
          >
            {isPlaying ? <Pause size={16} /> : <Play size={16} style={{ marginLeft: 2 }} />}
          </button>

          {/* Reset to Start */}
          <button
            onClick={() => onSeek(0)}
            title="Quay lại đầu video"
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: 28,
              height: 28,
              borderRadius: 6,
              background: 'rgba(255,255,255,0.05)',
              border: '1px solid rgba(255,255,255,0.1)',
              color: '#94a3b8',
              cursor: 'pointer',
            }}
          >
            <RotateCcw size={12} />
          </button>

          {/* Glowing Digital Timecode */}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              background: 'rgba(2, 6, 23, 0.85)',
              border: '1px solid rgba(56, 189, 248, 0.3)',
              borderRadius: 6,
              padding: '4px 10px',
              fontFamily: 'monospace',
              fontSize: 12,
              fontWeight: 700,
              color: '#38bdf8',
              boxShadow: '0 0 8px rgba(56, 189, 248, 0.15) inset',
            }}
          >
            <span>{formatTimecode(currentTime)}</span>
            <span style={{ color: '#475569' }}>/</span>
            <span style={{ color: '#94a3b8' }}>{formatTimecode(totalDuration)}</span>
          </div>
        </div>

        {/* Center/Right Timeline Badge */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 11, color: '#94a3b8' }}>
            <Film size={13} color="#38bdf8" /> Multi-Track 2.5D Studio ({project.shots.length} shots)
          </div>
        </div>
      </div>

      {/* ─── MULTI-TRACK PROFESSIONAL TIMELINE LANES ─────────────────── */}
      <div
        onClick={handleTrackClick}
        style={{
          position: 'relative',
          display: 'flex',
          flexDirection: 'column',
          gap: 3,
          background: 'rgba(2, 6, 23, 0.9)',
          borderRadius: 8,
          border: '1px solid rgba(255, 255, 255, 0.1)',
          padding: '6px 4px',
          cursor: 'pointer',
          userSelect: 'none',
          overflow: 'hidden',
        }}
      >
        {/* Lane 1: Camera & Shot Lane */}
        <div style={{ position: 'relative', height: 26, display: 'flex', width: '100%', gap: 2 }}>
          {shotTimeline.map(({ shot, start, end }, idx) => {
            const widthPct = totalDuration > 0 ? (shot.durationSeconds / totalDuration) * 100 : 0;
            const isSelected = shot.id === activeShotId;
            const isTopDown = (shot.camera.pitchStart ?? 0) >= 45;

            return (
              <div
                key={shot.id}
                onClick={(e) => {
                  e.stopPropagation();
                  onSelectShot(shot.id);
                  onSeek(start);
                }}
                style={{
                  width: `${widthPct}%`,
                  height: '100%',
                  background: isSelected
                    ? 'linear-gradient(135deg, rgba(2, 132, 199, 0.4), rgba(56, 189, 248, 0.25))'
                    : 'rgba(255, 255, 255, 0.04)',
                  border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.08)',
                  borderRadius: 4,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '0 6px',
                  fontSize: 9.5,
                  fontWeight: 700,
                  color: isSelected ? '#38bdf8' : '#cbd5e1',
                  overflow: 'hidden',
                  whiteSpace: 'nowrap',
                }}
              >
                <span>
                  #{idx + 1} {shot.title.split(':')[0]}
                </span>
                <span style={{ fontSize: 8.5, color: isTopDown ? '#facc15' : '#4ade80' }}>
                  {isTopDown ? '👑 Top' : `🎥 ${shot.camera.angleStart}°`}
                </span>
              </div>
            );
          })}
        </div>

        {/* Lane 2: Dialogue & Subtitles Lane */}
        <div style={{ position: 'relative', height: 20, display: 'flex', width: '100%', gap: 2 }}>
          {shotTimeline.map(({ shot }, idx) => {
            const widthPct = totalDuration > 0 ? (shot.durationSeconds / totalDuration) * 100 : 0;
            const hasDialogue = Boolean(shot.dialogueText);

            return (
              <div
                key={`dlg_${shot.id}`}
                style={{
                  width: `${widthPct}%`,
                  height: '100%',
                  background: hasDialogue ? 'rgba(168, 85, 247, 0.18)' : 'transparent',
                  border: hasDialogue ? '1px dashed rgba(168, 85, 247, 0.4)' : 'none',
                  borderRadius: 3,
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                  padding: '0 6px',
                  fontSize: 8.5,
                  color: '#c084fc',
                  overflow: 'hidden',
                  whiteSpace: 'nowrap',
                  textOverflow: 'ellipsis',
                }}
              >
                {hasDialogue && (
                  <>
                    <MessageSquare size={10} style={{ flexShrink: 0 }} />
                    <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>{shot.dialogueText}</span>
                  </>
                )}
              </div>
            );
          })}
        </div>

        {/* High-Precision Glowing Playhead Needle */}
        {totalDuration > 0 && (
          <div
            style={{
              position: 'absolute',
              top: 0,
              bottom: 0,
              left: `${(currentTime / totalDuration) * 100}%`,
              width: 2,
              background: '#38bdf8',
              boxShadow: '0 0 8px #38bdf8, 0 0 14px rgba(56, 189, 248, 0.8)',
              pointerEvents: 'none',
              zIndex: 20,
            }}
          >
            {/* Playhead Handle Diamond */}
            <div
              style={{
                position: 'absolute',
                top: -3,
                left: -4,
                width: 10,
                height: 10,
                background: '#38bdf8',
                transform: 'rotate(45deg)',
                boxShadow: '0 0 8px #38bdf8',
              }}
            />
          </div>
        )}
      </div>

      {/* Smooth Range Slider */}
      <input
        type="range"
        min="0"
        max={totalDuration || 1}
        step="0.02"
        value={currentTime}
        onChange={(e) => onSeek(parseFloat(e.target.value))}
        style={{
          width: '100%',
          height: 4,
          accentColor: '#38bdf8',
          cursor: 'pointer',
          margin: 0,
        }}
      />
    </div>
  );
};
