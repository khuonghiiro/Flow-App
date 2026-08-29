import React from 'react';
import { MessageSquare } from 'lucide-react';
import { Actor2DProfile, MultiAngleDirectorShot } from '../../../../../types/studio2d_director';
import { formatTimeWithMs } from './timelineConstants';

interface TimelineDialogueTrackProps {
  actors: Actor2DProfile[];
  currentTime: number;
  totalDuration: number;
  shotTimeline: { shot: MultiAngleDirectorShot; start: number; end: number }[];
  onSelectShot: (shotId: string) => void;
  onTrackClick: (e: React.MouseEvent<HTMLDivElement>) => void;
  onOpenDialogueModal: (shotId: string, time: number, text: string, speakerId: string) => void;
}

export const TimelineDialogueTrack: React.FC<TimelineDialogueTrackProps> = ({
  actors,
  currentTime,
  totalDuration,
  shotTimeline,
  onSelectShot,
  onTrackClick,
  onOpenDialogueModal,
}) => {
  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        height: 28,
        borderRadius: 4,
        background: 'rgba(168, 85, 247, 0.05)',
        border: '1px solid rgba(168, 85, 247, 0.18)',
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
          fontSize: 10.5,
          fontWeight: 700,
          color: '#d8b4fe',
          overflow: 'hidden',
          whiteSpace: 'nowrap',
        }}
      >
        <span style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
          <MessageSquare size={13} color="#c084fc" />
          <span>Lời Thoại</span>
        </span>

        <button
          onClick={(e) => {
            e.stopPropagation();
            const currentItem = shotTimeline.find((st) => currentTime >= st.start && currentTime < st.end) || shotTimeline[0];
            onOpenDialogueModal(
              currentItem?.shot.id || '',
              currentTime,
              currentItem?.shot.dialogueText || '',
              currentItem?.shot.speakerActorId || actors[0]?.id || ''
            );
          }}
          title="Gắn / Sửa lời thoại tại giây này"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 17,
            height: 17,
            borderRadius: 3,
            background: 'rgba(168, 85, 247, 0.25)',
            border: '1px solid rgba(168, 85, 247, 0.45)',
            color: '#d8b4fe',
            fontSize: 11,
            fontWeight: 700,
            cursor: 'pointer',
            flexShrink: 0,
          }}
        >
          +
        </button>
      </div>

      {/* Lane with Exact Timestamp Dialogue Pins */}
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
        <div style={{ position: 'absolute', inset: '11px 0', height: 4, background: 'rgba(255, 255, 255, 0.05)', borderRadius: 2 }} />

        {shotTimeline.map(({ shot, start, end }) => {
          const startPct = totalDuration > 0 ? (start / totalDuration) * 100 : 0;
          const widthPct = totalDuration > 0 ? (shot.durationSeconds / totalDuration) * 100 : 0;
          const hasDialogue = Boolean(shot.dialogueText);
          const speaker = actors.find((a) => a.id === shot.speakerActorId);
          const isCurrentActive = currentTime >= start && currentTime < end;

          return (
            <React.Fragment key={`dlg_frag_${shot.id}`}>
              {/* Span bar for active dialogue */}
              {hasDialogue && (
                <div
                  style={{
                    position: 'absolute',
                    left: `${startPct}%`,
                    width: `${widthPct}%`,
                    height: 14,
                    borderRadius: 3,
                    background: isCurrentActive ? 'rgba(168, 85, 247, 0.35)' : 'rgba(168, 85, 247, 0.15)',
                    border: isCurrentActive ? '1px solid #c084fc' : '1px dashed rgba(168, 85, 247, 0.4)',
                    boxShadow: isCurrentActive ? '0 0 8px rgba(168, 85, 247, 0.6)' : 'none',
                    pointerEvents: 'none',
                  }}
                />
              )}

              {/* Exact Timestamp Dialogue Milestone Pin */}
              <div
                onClick={(e) => {
                  e.stopPropagation();
                  onSelectShot(shot.id);
                  onOpenDialogueModal(shot.id, start, shot.dialogueText || '', shot.speakerActorId || actors[0]?.id || '');
                }}
                title={
                  hasDialogue
                    ? `Thoại tại mốc ${start.toFixed(2)}s (${formatTimeWithMs(start)}) của ${speaker?.name || 'Nhân vật'}: "${shot.dialogueText}". Bấm vào mốc để sửa.`
                    : `Mốc ${start.toFixed(2)}s: Bấm để thêm thoại`
                }
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
                  background: hasDialogue
                    ? isCurrentActive
                      ? 'linear-gradient(135deg, #a855f7, #0f172a)'
                      : 'rgba(15, 23, 42, 0.92)'
                    : 'rgba(15, 23, 42, 0.6)',
                  border: hasDialogue ? '1.5px solid #c084fc' : '1px dashed rgba(255, 255, 255, 0.2)',
                  boxShadow: hasDialogue && isCurrentActive ? '0 0 10px #c084fc' : '0 2px 4px rgba(0,0,0,0.5)',
                  cursor: 'pointer',
                  transition: 'all 0.15s ease',
                }}
              >
                {hasDialogue ? (
                  <>
                    <span style={{ fontSize: 11 }}>{speaker?.avatarIcon || '👤'}</span>
                    <span style={{ fontSize: 11 }}>💬</span>
                    <span style={{ fontSize: 7.5, fontFamily: 'monospace', fontWeight: 700, color: '#e9d5ff' }}>
                      {start.toFixed(1)}s
                    </span>
                  </>
                ) : (
                  <span style={{ fontSize: 7.5, color: '#64748b', padding: '0 2px' }}>+{start.toFixed(1)}s</span>
                )}
              </div>
            </React.Fragment>
          );
        })}
      </div>
    </div>
  );
};
