import React, { useRef } from 'react';
import { Play, Pause, RotateCcw, FastForward, Repeat, Camera, MessageSquare, Swords, Sparkles, User } from 'lucide-react';
import { MasterSceneConfig } from '../types/scene';

interface TimelineScrubberProps {
  scene: MasterSceneConfig;
  currentTime: number;
  isPlaying: boolean;
  playbackRate: number;
  isLooping: boolean;
  onTogglePlay: () => void;
  onSeek: (time: number) => void;
  onToggleLoop: () => void;
  onChangePlaybackRate: (rate: number) => void;
}

export const TimelineScrubber: React.FC<TimelineScrubberProps> = ({
  scene,
  currentTime,
  isPlaying,
  playbackRate,
  isLooping,
  onTogglePlay,
  onSeek,
  onToggleLoop,
  onChangePlaybackRate,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const duration = scene.duration || 25.0;

  const handleLaneClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!containerRef.current) return;
    const rect = containerRef.current.getBoundingClientRect();
    const clickX = e.clientX - rect.left;
    const progress = Math.max(0, Math.min(1, clickX / rect.width));
    onSeek(progress * duration);
  };

  const formatTime = (sec: number) => {
    const m = Math.floor(sec / 60).toString().padStart(2, '0');
    const s = Math.floor(sec % 60).toString().padStart(2, '0');
    const cs = Math.floor((sec % 1) * 100).toString().padStart(2, '0');
    return `${m}:${s}.${cs}`;
  };

  const progressPercent = (currentTime / duration) * 100;

  return (
    <div className="timeline-panel">
      {/* Top Toolbar */}
      <div className="timeline-toolbar">
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <div className="timeline-timecode">
            {formatTime(currentTime)} / {formatTime(duration)}
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            {[0.5, 1.0, 1.5, 2.0].map((rate) => (
              <button
                key={rate}
                className={`btn-secondary ${playbackRate === rate ? 'active' : ''}`}
                style={{
                  padding: '2px 6px',
                  fontSize: 10,
                  backgroundColor: playbackRate === rate ? 'rgba(99, 102, 241, 0.3)' : undefined,
                }}
                onClick={() => onChangePlaybackRate(rate)}
              >
                {rate}x
              </button>
            ))}
          </div>
        </div>

        {/* Playback Controls */}
        <div className="timeline-playback-controls">
          <button className="btn-icon" onClick={() => onSeek(0)} title="Về đầu">
            <RotateCcw size={15} />
          </button>
          <button
            className={`btn-icon play-btn ${isPlaying ? 'active' : ''}`}
            onClick={onTogglePlay}
            title={isPlaying ? 'Tạm dừng' : 'Phát kịch bản'}
          >
            {isPlaying ? <Pause size={18} /> : <Play size={18} style={{ marginLeft: 2 }} />}
          </button>
          <button
            className="btn-icon"
            onClick={() => onSeek(Math.min(duration, currentTime + 2))}
            title="Tua +2s"
          >
            <FastForward size={15} />
          </button>
          <button
            className={`btn-icon ${isLooping ? 'active' : ''}`}
            onClick={onToggleLoop}
            style={{ color: isLooping ? '#38bdf8' : undefined }}
            title="Lặp lại timeline"
          >
            <Repeat size={15} />
          </button>
        </div>

        <div style={{ fontSize: 11, color: '#94a3b8' }}>
          Master Scene: <strong style={{ color: '#f8fafc' }}>{scene.scene_id}</strong> ({scene.fps} FPS)
        </div>
      </div>

      {/* Multi-Track Scrubber Area */}
      <div className="timeline-tracks-container">
        {/* Track 1: Camera */}
        <div className="timeline-track-row">
          <div className="timeline-track-label">
            <Camera size={12} color="#38bdf8" /> Camera
          </div>
          <div className="timeline-track-lane" ref={containerRef} onClick={handleLaneClick}>
            {(scene.camera_tracks || []).map((t, idx) => {
              const left = (t.start / duration) * 100;
              const width = ((t.end - t.start) / duration) * 100;
              return (
                <div
                  key={idx}
                  className="timeline-block camera"
                  style={{ left: `${left}%`, width: `${width}%` }}
                  title={`${t.shot_type} (${t.start}s - ${t.end}s)`}
                >
                  {t.shot_type}
                </div>
              );
            })}
            <div
              className="timeline-playhead-line"
              style={{ left: `${progressPercent}%` }}
            >
              <div className="timeline-playhead-handle" />
            </div>
          </div>
        </div>

        {/* Track 2: Dialogues */}
        <div className="timeline-track-row">
          <div className="timeline-track-label">
            <MessageSquare size={12} color="#eab308" /> Thoại & CC
          </div>
          <div className="timeline-track-lane" onClick={handleLaneClick}>
            {(scene.dialogues_manifest || []).map((dlg) => {
              const dur = dlg.actual_duration || dlg.estimated_duration || 3.0;
              const left = (dlg.start_time / duration) * 100;
              const width = (dur / duration) * 100;
              return (
                <div
                  key={dlg.line_id}
                  className="timeline-block dialogue"
                  style={{ left: `${left}%`, width: `${width}%` }}
                  title={`[${dlg.speaker_name}]: ${dlg.text}`}
                >
                  [{dlg.speaker_name}] {dlg.text}
                </div>
              );
            })}
            <div className="timeline-playhead-line" style={{ left: `${progressPercent}%` }} />
          </div>
        </div>

        {/* Track 3: Combat Choreography */}
        <div className="timeline-track-row">
          <div className="timeline-track-label">
            <Swords size={12} color="#f43f5e" /> Combat Hit
          </div>
          <div className="timeline-track-lane" onClick={handleLaneClick}>
            {scene.actors.flatMap((a) =>
              (a.tracks.combat_actions || []).map((cb, idx) => {
                const left = (cb.start_time / duration) * 100;
                const width = ((cb.impact_time - cb.start_time + 1.5) / duration) * 100;
                return (
                  <div
                    key={`${a.id}_cb_${idx}`}
                    className="timeline-block combat"
                    style={{ left: `${left}%`, width: `${width}%` }}
                    title={`Impact at ${cb.impact_time}s -> ${cb.target.reaction_anim}`}
                  >
                    💥 {cb.anim} (Hit {cb.impact_time}s)
                  </div>
                );
              })
            )}
            <div className="timeline-playhead-line" style={{ left: `${progressPercent}%` }} />
          </div>
        </div>

        {/* Track 4: World Events (Farming) */}
        <div className="timeline-track-row">
          <div className="timeline-track-label">
            <Sparkles size={12} color="#10b981" /> Mầm Cây
          </div>
          <div className="timeline-track-lane" onClick={handleLaneClick}>
            {(scene.dynamic_world_events || []).map((ev, idx) => {
              return (
                <div
                  key={idx}
                  className="timeline-block event"
                  style={{ left: '8%', width: '64%' }}
                  title="Farming Growth: Seed -> Sprout -> Crop"
                >
                  🌱 Nảy mầm & lớn lên (2s - 18s)
                </div>
              );
            })}
            <div className="timeline-playhead-line" style={{ left: `${progressPercent}%` }} />
          </div>
        </div>
      </div>
    </div>
  );
};
