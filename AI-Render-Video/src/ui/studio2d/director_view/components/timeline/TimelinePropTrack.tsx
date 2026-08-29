import React from 'react';
import { Trees } from 'lucide-react';
import { ScenePropItem, MultiAngleDirectorShot } from '../../../../../types/studio2d_director';
import { PROP_GROWTH_OPTIONS, formatTimeWithMs } from './timelineConstants';

interface TimelinePropTrackProps {
  props: ScenePropItem[];
  currentTime: number;
  totalDuration: number;
  shotTimeline: { shot: MultiAngleDirectorShot; start: number; end: number }[];
  onSelectShot: (shotId: string) => void;
  onTrackClick: (e: React.MouseEvent<HTMLDivElement>) => void;
  onOpenPropModal: (propId: string, shotId: string, time: number, currentStage: any) => void;
}

export const TimelinePropTrack: React.FC<TimelinePropTrackProps> = ({
  props,
  currentTime,
  totalDuration,
  shotTimeline,
  onSelectShot,
  onTrackClick,
  onOpenPropModal,
}) => {
  if (!props || props.length === 0) return null;

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        height: 28,
        borderRadius: 4,
        background: 'rgba(34, 197, 94, 0.05)',
        border: '1px solid rgba(34, 197, 94, 0.18)',
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
          color: '#4ade80',
          overflow: 'hidden',
          whiteSpace: 'nowrap',
        }}
      >
        <span style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
          <Trees size={13} color="#4ade80" />
          <span>Cây Cối & Đạo Cụ</span>
        </span>

        <button
          onClick={(e) => {
            e.stopPropagation();
            const currentItem = shotTimeline.find((st) => currentTime >= st.start && currentTime < st.end) || shotTimeline[0];
            const firstProp = props[0];
            if (firstProp) {
              const curStage = currentItem?.shot.props?.[firstProp.id]?.growthStage || firstProp.growthStage || 'normal';
              onOpenPropModal(firstProp.id, currentItem?.shot.id || '', currentTime, curStage);
            }
          }}
          title="Gắn trạng thái lớn/ra hoa/kết trái tại thời điểm này"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 17,
            height: 17,
            borderRadius: 3,
            background: 'rgba(34, 197, 94, 0.25)',
            border: '1px solid rgba(34, 197, 94, 0.45)',
            color: '#4ade80',
            fontSize: 11,
            fontWeight: 700,
            cursor: 'pointer',
            flexShrink: 0,
          }}
        >
          +
        </button>
      </div>

      {/* Lane with Exact Timestamp Prop Growth Pins */}
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

        {shotTimeline.map(({ shot, start, end }, idx) => {
          const startPct = totalDuration > 0 ? (start / totalDuration) * 100 : 0;
          const widthPct = totalDuration > 0 ? (shot.durationSeconds / totalDuration) * 100 : 0;
          const propItems = Object.values(shot.props || {});
          const activeGrowthProp = propItems.find((p) => p.growthStage && p.growthStage !== 'normal') || propItems[0] || props[0];
          const stageId = activeGrowthProp?.growthStage || 'normal';
          const stageInfo = PROP_GROWTH_OPTIONS.find((g) => g.id === stageId) || PROP_GROWTH_OPTIONS[6];
          const isCurrentActive = currentTime >= start && currentTime < end;

          return (
            <React.Fragment key={`prop_frag_${shot.id}`}>
              {/* Span bar */}
              <div
                style={{
                  position: 'absolute',
                  left: `${startPct}%`,
                  width: `${widthPct}%`,
                  height: 14,
                  borderRadius: 3,
                  background: isCurrentActive ? `${stageInfo.color}35` : `${stageInfo.color}15`,
                  border: isCurrentActive ? `1px solid ${stageInfo.color}` : `1px dashed ${stageInfo.color}40`,
                  boxShadow: isCurrentActive ? `0 0 8px ${stageInfo.color}55` : 'none',
                  pointerEvents: 'none',
                }}
              />

              {/* Exact Timestamp Prop Milestone Pin */}
              <div
                onClick={(e) => {
                  e.stopPropagation();
                  onSelectShot(shot.id);
                  const targetP = props[0];
                  if (targetP) {
                    onOpenPropModal(targetP.id, shot.id, start, stageId);
                  }
                }}
                title={`Đạo cụ/Cây cối ở mốc ${start.toFixed(2)}s (${formatTimeWithMs(start)}): ${stageInfo.label}. Bấm vào mốc để đổi trạng thái.`}
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
                    ? `linear-gradient(135deg, ${stageInfo.color}, #0f172a)`
                    : 'rgba(15, 23, 42, 0.92)',
                  border: `1.5px solid ${stageInfo.color}`,
                  boxShadow: isCurrentActive ? `0 0 10px ${stageInfo.color}` : '0 2px 4px rgba(0,0,0,0.5)',
                  cursor: 'pointer',
                  transition: 'all 0.15s ease',
                }}
              >
                <span style={{ fontSize: 13, lineHeight: 1 }}>{stageInfo.icon}</span>
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
