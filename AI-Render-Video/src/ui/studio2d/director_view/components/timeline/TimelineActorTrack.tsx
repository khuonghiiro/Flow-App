import React from 'react';
import { Actor2DProfile, MultiAngleDirectorShot } from '../../../../../types/studio2d_director';
import { ACTION_OPTIONS, formatTimeWithMs } from './timelineConstants';

interface TimelineActorTrackProps {
  actor: Actor2DProfile;
  selectedActorId?: string | null;
  currentTime: number;
  totalDuration: number;
  shotTimeline: { shot: MultiAngleDirectorShot; start: number; end: number }[];
  onSelectActor?: (actorId: string) => void;
  onSelectShot: (shotId: string) => void;
  onTrackClick: (e: React.MouseEvent<HTMLDivElement>) => void;
  onOpenActionModal: (actorId: string, shotId: string, time: number, currentPose: any) => void;
}

export const TimelineActorTrack: React.FC<TimelineActorTrackProps> = ({
  actor,
  selectedActorId,
  currentTime,
  totalDuration,
  shotTimeline,
  onSelectActor,
  onSelectShot,
  onTrackClick,
  onOpenActionModal,
}) => {
  const isActorSelected = selectedActorId === actor.id;

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        height: 28,
        borderRadius: 4,
        background: isActorSelected ? 'rgba(56, 189, 248, 0.08)' : 'rgba(255, 255, 255, 0.02)',
        border: isActorSelected ? '1px solid rgba(56, 189, 248, 0.35)' : '1px solid rgba(255, 255, 255, 0.03)',
        flexShrink: 0,
      }}
    >
      {/* Left Header */}
      <div
        onClick={() => onSelectActor?.(actor.id)}
        style={{
          width: 140,
          flexShrink: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 6px',
          cursor: 'pointer',
          fontSize: 10.5,
          fontWeight: isActorSelected ? 700 : 600,
          color: isActorSelected ? '#38bdf8' : '#e2e8f0',
          overflow: 'hidden',
          whiteSpace: 'nowrap',
        }}
      >
        <span style={{ display: 'flex', alignItems: 'center', gap: 5, overflow: 'hidden', textOverflow: 'ellipsis' }}>
          <span style={{ fontSize: 13 }}>{actor.avatarIcon || '👤'}</span>
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>{actor.name}</span>
        </span>

        <button
          onClick={(e) => {
            e.stopPropagation();
            onSelectActor?.(actor.id);
            const currentItem = shotTimeline.find((st) => currentTime >= st.start && currentTime < st.end) || shotTimeline[0];
            const curPose = currentItem?.shot.actors[actor.id]?.actionPose || 'idle_breathe';
            onOpenActionModal(actor.id, currentItem?.shot.id || '', currentTime, curPose);
          }}
          title={`Gắn động tác cho ${actor.name} tại mốc ${currentTime.toFixed(2)}s`}
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 17,
            height: 17,
            borderRadius: 3,
            background: 'rgba(56, 189, 248, 0.2)',
            border: '1px solid rgba(56, 189, 248, 0.4)',
            color: '#38bdf8',
            fontSize: 11,
            fontWeight: 700,
            cursor: 'pointer',
            flexShrink: 0,
          }}
        >
          +
        </button>
      </div>

      {/* Right Timeline Lane with Exact Timestamp Keyframe Markers */}
      <div
        onClick={onTrackClick}
        style={{
          flex: 1,
          height: '100%',
          position: 'relative',
          display: 'flex',
          alignItems: 'center',
          cursor: 'pointer',
        }}
      >
        {/* Subtle background rail line */}
        <div style={{ position: 'absolute', inset: '11px 0', height: 4, background: 'rgba(255, 255, 255, 0.05)', borderRadius: 2 }} />

        {shotTimeline.map(({ shot, start, end }, idx) => {
          const startPct = totalDuration > 0 ? (start / totalDuration) * 100 : 0;
          const widthPct = totalDuration > 0 ? (shot.durationSeconds / totalDuration) * 100 : 0;
          const actorState = shot.actors[actor.id] || { actionPose: 'idle_breathe' };
          const poseInfo = ACTION_OPTIONS.find((a) => a.id === actorState.actionPose) || ACTION_OPTIONS[3];
          const isCurrentActive = currentTime >= start && currentTime < end;

          return (
            <React.Fragment key={`act_frag_${actor.id}_${shot.id}`}>
              {/* Segment Duration Span Bar */}
              <div
                style={{
                  position: 'absolute',
                  left: `${startPct}%`,
                  width: `${widthPct}%`,
                  height: 16,
                  borderRadius: 3,
                  background: isCurrentActive ? `${poseInfo.color}35` : `${poseInfo.color}15`,
                  border: isCurrentActive ? `1px solid ${poseInfo.color}` : `1px dashed ${poseInfo.color}40`,
                  boxShadow: isCurrentActive ? `0 0 8px ${poseInfo.color}55` : 'none',
                  pointerEvents: 'none',
                }}
              />

              {/* Exact Timestamp Milestone Keyframe Pin */}
              <div
                onClick={(e) => {
                  e.stopPropagation();
                  onSelectShot(shot.id);
                  onSelectActor?.(actor.id);
                  onOpenActionModal(actor.id, shot.id, start, actorState.actionPose);
                }}
                title={`${actor.name} tại mốc ${start.toFixed(2)}s (${formatTimeWithMs(start)}): ${poseInfo.label}. Bấm vào mốc để đổi động tác`}
                style={{
                  position: 'absolute',
                  left: `${startPct}%`,
                  transform: 'translateX(-50%)',
                  zIndex: isCurrentActive ? 15 : 10,
                  display: 'flex',
                  alignItems: 'center',
                  gap: 2,
                  padding: '2px 4px',
                  borderRadius: 4,
                  background: isCurrentActive
                    ? `linear-gradient(135deg, ${poseInfo.color}, #0f172a)`
                    : 'rgba(15, 23, 42, 0.92)',
                  border: `1.5px solid ${poseInfo.color}`,
                  boxShadow: isCurrentActive
                    ? `0 0 10px ${poseInfo.color}`
                    : `0 2px 5px rgba(0,0,0,0.6)`,
                  cursor: 'pointer',
                  transition: 'all 0.15s ease',
                }}
              >
                {/* Milestone Action Icon */}
                <span style={{ fontSize: 13, lineHeight: 1 }}>{poseInfo.icon}</span>
                {/* Timestamp Pill */}
                <span style={{ fontSize: 7.5, fontFamily: 'monospace', fontWeight: 700, color: '#e2e8f0' }}>
                  {start.toFixed(1)}s
                </span>
              </div>
            </React.Fragment>
          );
        })}
      </div>
    </div>
  );
};
