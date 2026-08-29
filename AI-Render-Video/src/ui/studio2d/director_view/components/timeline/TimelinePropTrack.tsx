import React from 'react';
import { Trees, Eye, EyeOff } from 'lucide-react';
import { ScenePropItem, MultiAngleDirectorShot } from '../../../../../types/studio2d_director';
import { PROP_GROWTH_OPTIONS, formatTimeWithMs } from './timelineConstants';

interface TimelinePropTrackProps {
  props: ScenePropItem[];
  selectedPropId?: string | null;
  currentTime: number;
  totalDuration: number;
  shotTimeline: { shot: MultiAngleDirectorShot; start: number; end: number }[];
  onSelectShot: (shotId: string) => void;
  onSelectProp?: (propId: string) => void;
  onTrackClick: (e: React.MouseEvent<HTMLDivElement>) => void;
  onOpenPropModal: (propId: string, shotId: string, time: number, currentStage: any) => void;
  onOpenVisibilityModal?: (propId: string, propName: string, from: number, to: number) => void;
}

export const TimelinePropTrack: React.FC<TimelinePropTrackProps> = ({
  props,
  selectedPropId,
  currentTime,
  totalDuration,
  shotTimeline,
  onSelectShot,
  onSelectProp,
  onTrackClick,
  onOpenPropModal,
  onOpenVisibilityModal,
}) => {
  if (!props || props.length === 0) return null;
  const isPropSelected = Boolean(selectedPropId);
  const firstProp = props[0];

  const visFrom = firstProp?.visibleFrom ?? 0;
  const visTo = firstProp?.visibleTo ?? totalDuration;
  const isCurrentlyVisible = currentTime >= visFrom && currentTime <= visTo;

  const fromPct = totalDuration > 0 ? (visFrom / totalDuration) * 100 : 0;
  const toPct = totalDuration > 0 ? (visTo / totalDuration) * 100 : 100;
  const activeWidthPct = Math.max(0, toPct - fromPct);

  return (
    <div
      onClick={() => {
        if (firstProp) onSelectProp?.(firstProp.id);
      }}
      style={{
        display: 'flex',
        alignItems: 'center',
        height: 28,
        borderRadius: 4,
        background: isPropSelected ? 'rgba(30, 41, 59, 0.9)' : 'rgba(15, 23, 42, 0.6)',
        border: isPropSelected ? '1px solid rgba(74, 222, 128, 0.45)' : '1px solid rgba(255, 255, 255, 0.05)',
        borderLeft: isPropSelected ? '3.5px solid #4ade80' : '3.5px solid transparent',
        flexShrink: 0,
        transition: 'all 0.15s ease',
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
          fontWeight: isPropSelected ? 700 : 600,
          color: isPropSelected ? '#4ade80' : '#86efac',
          overflow: 'hidden',
          whiteSpace: 'nowrap',
          cursor: 'pointer',
        }}
      >
        <span style={{ display: 'flex', alignItems: 'center', gap: 5, overflow: 'hidden', textOverflow: 'ellipsis' }}>
          <Trees size={13} color="#4ade80" />
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>Cây Cối & Đạo Cụ</span>
          {!isCurrentlyVisible && (
            <span style={{ fontSize: 8, color: '#64748b' }} title="Đang ẩn tại giây hiện tại">
              <EyeOff size={10} />
            </span>
          )}
        </span>

        <div style={{ display: 'flex', alignItems: 'center', gap: 3 }}>
          {/* Visibility Setting */}
          {firstProp && (
            <button
              onClick={(e) => {
                e.stopPropagation();
                onSelectProp?.(firstProp.id);
                onOpenVisibilityModal?.(firstProp.id, firstProp.name || 'Cây Cối & Đạo Cụ', visFrom, visTo);
              }}
              title={`Chỉnh thời gian xuất hiện của đạo cụ (${visFrom}s -> ${visTo}s)`}
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
          )}

          {/* Add Prop Lifecycle Milestone */}
          <button
            onClick={(e) => {
              e.stopPropagation();
              if (firstProp) onSelectProp?.(firstProp.id);
              const currentItem = shotTimeline.find((st) => currentTime >= st.start && currentTime < st.end) || shotTimeline[0];
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
              background: 'rgba(34, 197, 94, 0.2)',
              border: '1px solid rgba(34, 197, 94, 0.4)',
              color: '#4ade80',
              fontSize: 11,
              fontWeight: 700,
              cursor: 'pointer',
            }}
          >
            +
          </button>
        </div>
      </div>

      {/* Lane with Exact Timestamp Prop Growth Pins */}
      <div
        onClick={(e) => {
          if (firstProp) onSelectProp?.(firstProp.id);
          onTrackClick(e);
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
            background: 'rgba(34, 197, 94, 0.08)',
            border: '1px solid rgba(34, 197, 94, 0.25)',
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

        {/* Render Lifecycle Milestones */}
        {shotTimeline.map(({ shot, start, end }) => {
          if (end < visFrom || start > visTo) return null;
          const startPct = totalDuration > 0 ? (start / totalDuration) * 100 : 0;
          const propItems = Object.values(shot.props || {});
          const activeGrowthProp = propItems.find((p) => p.growthStage && p.growthStage !== 'normal') || propItems[0] || props[0];
          const stageId = activeGrowthProp?.growthStage || 'normal';
          const stageInfo = PROP_GROWTH_OPTIONS.find((g) => g.id === stageId) || PROP_GROWTH_OPTIONS[6];
          const isCurrentActive = isPropSelected && currentTime >= start && currentTime < end;

          return (
            <div
              key={`prop_pin_${shot.id}`}
              onClick={(e) => {
                e.stopPropagation();
                if (firstProp) {
                  onSelectProp?.(firstProp.id);
                  onOpenPropModal(firstProp.id, shot.id, start, stageId);
                }
              }}
              title={`Đạo cụ ở mốc ${start.toFixed(2)}s (${formatTimeWithMs(start)}): ${stageInfo.label}. Bấm để đổi trạng thái.`}
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
                background: 'rgba(15, 23, 42, 0.95)',
                border: isCurrentActive ? '1.5px solid #4ade80' : `1px solid ${stageInfo.color}88`,
                boxShadow: isCurrentActive ? '0 2px 8px rgba(34, 197, 94, 0.4)' : '0 2px 4px rgba(0,0,0,0.5)',
                cursor: 'pointer',
              }}
            >
              <span style={{ fontSize: 13, lineHeight: 1 }}>{stageInfo.icon}</span>
              <span style={{ fontSize: 7.5, fontFamily: 'monospace', fontWeight: 700, color: '#e2e8f0' }}>
                {start.toFixed(1)}s
              </span>
            </div>
          );
        })}

        {/* Local Active Marker on THIS focused prop track ONLY */}
        {isPropSelected && totalDuration > 0 && (
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
