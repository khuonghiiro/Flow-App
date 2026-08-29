import React, { useRef } from 'react';
import { Eye, EyeOff } from 'lucide-react';
import { Actor2DProfile, MultiAngleDirectorShot } from '../../../../../types/studio2d_director';
import { ACTION_OPTIONS, formatTimeWithMs } from './timelineConstants';

interface TimelineActorTrackProps {
  actor: Actor2DProfile;
  selectedActorId?: string | null;
  activeShotId?: string;
  currentTime: number;
  totalDuration: number;
  shotTimeline: { shot: MultiAngleDirectorShot; start: number; end: number }[];
  onSelectActor?: (actorId: string) => void;
  onSelectShot: (shotId: string) => void;
  onTrackClick: (e: React.MouseEvent<HTMLDivElement>) => void;
  onOpenActionModal: (actorId: string, shotId: string, time: number, currentPose: any) => void;
  onOpenVisibilityModal: (actorId: string, actorName: string, from: number, to: number) => void;
  onOpenContextMenu: (e: React.MouseEvent, actorId: string, actorName: string, shotId: string, time: number) => void;
}

export const TimelineActorTrack: React.FC<TimelineActorTrackProps> = ({
  actor,
  selectedActorId,
  activeShotId,
  currentTime,
  totalDuration,
  shotTimeline,
  onSelectActor,
  onSelectShot,
  onTrackClick,
  onOpenActionModal,
  onOpenVisibilityModal,
  onOpenContextMenu,
}) => {
  const isActorSelected = selectedActorId === actor.id;
  const laneRef = useRef<HTMLDivElement>(null);

  // Determine visibility bounds for actor from shots or default
  const firstShotActor = shotTimeline[0]?.shot.actors[actor.id];
  const visFrom = firstShotActor?.visibleFrom ?? 0;
  const visTo = firstShotActor?.visibleTo ?? totalDuration;
  const isCurrentlyVisible = currentTime >= visFrom && currentTime <= visTo;

  const handleLaneClick = (e: React.MouseEvent<HTMLDivElement>) => {
    onSelectActor?.(actor.id);
    onTrackClick(e);
  };

  const fromPct = totalDuration > 0 ? (visFrom / totalDuration) * 100 : 0;
  const toPct = totalDuration > 0 ? (visTo / totalDuration) * 100 : 100;
  const activeWidthPct = Math.max(0, toPct - fromPct);

  return (
    <div
      onClick={() => onSelectActor?.(actor.id)}
      style={{
        display: 'flex',
        alignItems: 'center',
        height: 28,
        borderRadius: 4,
        background: isActorSelected ? 'rgba(30, 41, 59, 0.9)' : 'rgba(15, 23, 42, 0.6)',
        border: isActorSelected ? '1px solid rgba(56, 189, 248, 0.45)' : '1px solid rgba(255, 255, 255, 0.05)',
        borderLeft: isActorSelected ? '3.5px solid #38bdf8' : '3.5px solid transparent',
        flexShrink: 0,
        transition: 'all 0.15s ease',
      }}
    >
      {/* Left Header */}
      <div
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
          color: isActorSelected ? '#38bdf8' : '#cbd5e1',
          overflow: 'hidden',
          whiteSpace: 'nowrap',
        }}
      >
        <span style={{ display: 'flex', alignItems: 'center', gap: 5, overflow: 'hidden', textOverflow: 'ellipsis' }}>
          <span style={{ fontSize: 13 }}>{actor.avatarIcon || '👤'}</span>
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>{actor.name}</span>
          {!isCurrentlyVisible && (
            <span style={{ fontSize: 8, color: '#64748b', display: 'flex', alignItems: 'center', gap: 2 }} title="Đang ẩn tại giây hiện tại">
              <EyeOff size={10} />
            </span>
          )}
        </span>

        <div style={{ display: 'flex', alignItems: 'center', gap: 3 }}>
          {/* Visibility Window Settings Button */}
          <button
            onClick={(e) => {
              e.stopPropagation();
              onSelectActor?.(actor.id);
              onOpenVisibilityModal(actor.id, actor.name, visFrom, visTo);
            }}
            title={`Chỉnh thời gian xuất hiện của ${actor.name} (${visFrom}s -> ${visTo}s)`}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: 17,
              height: 17,
              borderRadius: 3,
              background: 'rgba(255, 255, 255, 0.06)',
              border: '1px solid rgba(255, 255, 255, 0.12)',
              color: '#94a3b8',
              cursor: 'pointer',
            }}
          >
            <Eye size={10} />
          </button>

          {/* Add Action Milestone Button */}
          <button
            onClick={(e) => {
              e.stopPropagation();
              onSelectActor?.(actor.id);
              const currentItem = shotTimeline.find((st) => currentTime >= st.start && currentTime < st.end) || shotTimeline[0];
              const curPose = currentItem?.shot.actors[actor.id]?.actionPose || 'idle_breathe';
              onOpenActionModal(actor.id, currentItem?.shot.id || '', currentTime, curPose);
            }}
            title={`Gắn mốc động tác cho ${actor.name} tại giây ${currentTime.toFixed(2)}s`}
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
            }}
          >
            +
          </button>
        </div>
      </div>

      {/* Right Timeline Lane */}
      <div
        ref={laneRef}
        onClick={handleLaneClick}
        onContextMenu={(e) => {
          e.preventDefault();
          onSelectActor?.(actor.id);
          const currentItem = shotTimeline.find((st) => currentTime >= st.start && currentTime < st.end) || shotTimeline[0];
          onOpenContextMenu(e, actor.id, actor.name, currentItem?.shot.id || '', currentTime);
        }}
        style={{
          flex: 1,
          height: '100%',
          position: 'relative',
          display: 'flex',
          alignItems: 'center',
          cursor: 'crosshair',
        }}
      >
        {/* Inactive Pre-Visibility Shading */}
        {visFrom > 0 && (
          <div
            style={{
              position: 'absolute',
              left: 0,
              width: `${fromPct}%`,
              top: 0,
              bottom: 0,
              background: 'repeating-linear-gradient(45deg, rgba(0,0,0,0.35), rgba(0,0,0,0.35) 4px, rgba(0,0,0,0.5) 4px, rgba(0,0,0,0.5) 8px)',
              display: 'flex',
              alignItems: 'center',
              paddingLeft: 4,
              fontSize: 8,
              color: '#64748b',
              pointerEvents: 'none',
            }}
          >
            Ẩn đến {visFrom}s
          </div>
        )}

        {/* Active Visible Span Background */}
        <div
          style={{
            position: 'absolute',
            left: `${fromPct}%`,
            width: `${activeWidthPct}%`,
            top: 2,
            bottom: 2,
            borderRadius: 3,
            background: 'rgba(56, 189, 248, 0.08)',
            border: '1px solid rgba(56, 189, 248, 0.25)',
            pointerEvents: 'none',
          }}
        />

        {/* Inactive Post-Visibility Shading */}
        {visTo < totalDuration && (
          <div
            style={{
              position: 'absolute',
              left: `${toPct}%`,
              right: 0,
              top: 0,
              bottom: 0,
              background: 'repeating-linear-gradient(45deg, rgba(0,0,0,0.35), rgba(0,0,0,0.35) 4px, rgba(0,0,0,0.5) 4px, rgba(0,0,0,0.5) 8px)',
              display: 'flex',
              alignItems: 'center',
              paddingLeft: 4,
              fontSize: 8,
              color: '#64748b',
              pointerEvents: 'none',
            }}
          >
            Ẩn sau {visTo}s
          </div>
        )}

        {/* Render Action Milestones inside Visible Range */}
        {shotTimeline.map(({ shot, start, end }) => {
          if (end < visFrom || start > visTo) return null;
          const startPct = totalDuration > 0 ? (start / totalDuration) * 100 : 0;
          const actorState = shot.actors[actor.id] || { actionPose: 'idle_breathe' };
          const poseInfo = ACTION_OPTIONS.find((a) => a.id === actorState.actionPose) || ACTION_OPTIONS[3];
          const isCurrentActive = isActorSelected && currentTime >= start && currentTime < end;
          const hasDialogueThisActor = shot.speakerActorId === actor.id && Boolean(shot.dialogueText);

          return (
            <div
              key={`act_pin_${actor.id}_${shot.id}`}
              onClick={(e) => {
                e.stopPropagation();
                onSelectActor?.(actor.id);
                onOpenActionModal(actor.id, shot.id, start, actorState.actionPose);
              }}
              onContextMenu={(e) => {
                e.preventDefault();
                e.stopPropagation();
                onSelectActor?.(actor.id);
                onOpenContextMenu(e, actor.id, actor.name, shot.id, start);
              }}
              title={`${actor.name} tại mốc ${start.toFixed(2)}s (${formatTimeWithMs(start)}): ${poseInfo.label}${hasDialogueThisActor ? ` (Thoại: "${shot.dialogueText}")` : ''}. Bấm để đổi hành động.`}
              style={{
                position: 'absolute',
                left: `${startPct}%`,
                transform: 'translateX(-50%)',
                zIndex: isCurrentActive ? 15 : 10,
                display: 'flex',
                alignItems: 'center',
                gap: 3,
                padding: '2px 4px',
                borderRadius: 4,
                background: 'rgba(15, 23, 42, 0.95)',
                border: isCurrentActive ? `1.5px solid #38bdf8` : `1px solid ${poseInfo.color}88`,
                boxShadow: isCurrentActive ? '0 2px 8px rgba(56, 189, 248, 0.4)' : '0 2px 4px rgba(0,0,0,0.5)',
                cursor: 'pointer',
              }}
            >
              <span style={{ fontSize: 13, lineHeight: 1 }}>{poseInfo.icon}</span>
              {hasDialogueThisActor && (
                <span style={{ fontSize: 10, lineHeight: 1 }} title={`Lời thoại: "${shot.dialogueText}"`}>
                  💬
                </span>
              )}
              <span style={{ fontSize: 7.5, fontFamily: 'monospace', fontWeight: 700, color: '#e2e8f0' }}>
                {start.toFixed(1)}s
              </span>
            </div>
          );
        })}

        {/* Local Active Marker on THIS focused actor track ONLY */}
        {isActorSelected && totalDuration > 0 && (
          <div
            style={{
              position: 'absolute',
              top: 0,
              bottom: 0,
              left: `${(currentTime / totalDuration) * 100}%`,
              width: 1.5,
              background: '#f43f5e',
              pointerEvents: 'none',
              zIndex: 25,
            }}
          />
        )}
      </div>
    </div>
  );
};
