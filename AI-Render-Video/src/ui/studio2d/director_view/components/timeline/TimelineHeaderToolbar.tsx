import React from 'react';
import {
  Play,
  Pause,
  RotateCcw,
  Film,
  MessageSquare,
  Zap,
  ChevronLeft,
  ChevronRight,
  Clock,
  Maximize2,
  Minimize2,
} from 'lucide-react';
import { Director2DProject, MultiAngleDirectorShot } from '../../../../../types/studio2d_director';
import { formatTimecode } from './timelineConstants';

interface TimelineHeaderToolbarProps {
  project: Director2DProject;
  currentTime: number;
  totalDuration: number;
  isPlaying: boolean;
  activeShotObj?: MultiAngleDirectorShot;
  selectedActorId?: string | null;
  isExpanded: boolean;
  onTogglePlay: () => void;
  onSeek: (time: number) => void;
  onToggleExpand: () => void;
  onOpenActionModal: () => void;
  onOpenDialogueModal: () => void;
  onOpenDurationModal: () => void;
  onAddNewShot: (duration: number) => void;
}

export const TimelineHeaderToolbar: React.FC<TimelineHeaderToolbarProps> = ({
  project,
  currentTime,
  totalDuration,
  isPlaying,
  activeShotObj,
  selectedActorId,
  isExpanded,
  onTogglePlay,
  onSeek,
  onToggleExpand,
  onOpenActionModal,
  onOpenDialogueModal,
  onOpenDurationModal,
  onAddNewShot,
}) => {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
      {/* Left Playback Controls */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <button
          onClick={onTogglePlay}
          title={isPlaying ? 'Tạm dừng (Space)' : 'Phát kịch bản (Space)'}
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 36,
            height: 36,
            borderRadius: 8,
            background: isPlaying
              ? 'linear-gradient(135deg, #ef4444, #dc2626)'
              : 'linear-gradient(135deg, #0284c7, #38bdf8)',
            color: '#fff',
            border: 'none',
            cursor: 'pointer',
            boxShadow: isPlaying ? '0 0 14px rgba(239,68,68,0.6)' : '0 0 14px rgba(56,189,248,0.6)',
            transition: 'all 0.15s',
          }}
        >
          {isPlaying ? <Pause size={18} /> : <Play size={18} style={{ marginLeft: 2 }} />}
        </button>

        <button
          onClick={() => onSeek(0)}
          title="Quay lại đầu video (0.00s)"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 30,
            height: 30,
            borderRadius: 6,
            background: 'rgba(255,255,255,0.05)',
            border: '1px solid rgba(255,255,255,0.12)',
            color: '#94a3b8',
            cursor: 'pointer',
          }}
        >
          <RotateCcw size={13} />
        </button>

        <button
          onClick={() => onSeek(Math.max(0, currentTime - 0.5))}
          title="Lùi 0.5 giây"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 28,
            height: 30,
            borderRadius: 6,
            background: 'rgba(255,255,255,0.04)',
            border: '1px solid rgba(255,255,255,0.08)',
            color: '#94a3b8',
            cursor: 'pointer',
          }}
        >
          <ChevronLeft size={14} />
        </button>

        <button
          onClick={() => onSeek(Math.min(totalDuration, currentTime + 0.5))}
          title="Tiến 0.5 giây"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 28,
            height: 30,
            borderRadius: 6,
            background: 'rgba(255,255,255,0.04)',
            border: '1px solid rgba(255,255,255,0.08)',
            color: '#94a3b8',
            cursor: 'pointer',
          }}
        >
          <ChevronRight size={14} />
        </button>

        {/* Digital Timecode */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            background: 'rgba(2, 6, 23, 0.9)',
            border: '1px solid rgba(56, 189, 248, 0.4)',
            borderRadius: 8,
            padding: '4px 10px',
            fontFamily: 'monospace',
            fontSize: 12,
            fontWeight: 700,
            color: '#38bdf8',
            boxShadow: '0 0 10px rgba(56, 189, 248, 0.2) inset',
          }}
        >
          <span>{formatTimecode(currentTime)}</span>
          <span style={{ color: '#475569' }}>/</span>
          <span style={{ color: '#94a3b8' }}>{formatTimecode(totalDuration)}</span>
        </div>

        {/* Shot Duration Pill */}
        {activeShotObj && (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              background: 'rgba(56, 189, 248, 0.12)',
              border: '1px solid rgba(56, 189, 248, 0.35)',
              borderRadius: 8,
              padding: '3px 8px',
              fontSize: 11,
              color: '#e2e8f0',
              fontWeight: 600,
            }}
          >
            <Film size={12} color="#38bdf8" />
            <span style={{ maxWidth: 150, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {activeShotObj.title}
            </span>
            <button
              onClick={onOpenDurationModal}
              title="Bấm để chỉnh sửa độ dài giây của Shot này"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 3,
                padding: '2px 6px',
                borderRadius: 4,
                background: 'rgba(56, 189, 248, 0.2)',
                border: '1px solid #38bdf8',
                color: '#38bdf8',
                fontSize: 10,
                fontWeight: 700,
                cursor: 'pointer',
              }}
            >
              <Clock size={10} /> {activeShotObj.durationSeconds}s
            </button>
          </div>
        )}
      </div>

      {/* Right Controls: Actions, Presets, Expand Button */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
        {selectedActorId && (
          <button
            onClick={onOpenActionModal}
            title={`Gắn động tác cho nhân vật đang chọn tại ${formatTimecode(currentTime)}`}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '5px 10px',
              borderRadius: 6,
              background: 'linear-gradient(135deg, rgba(2, 132, 199, 0.35), rgba(56, 189, 248, 0.25))',
              border: '1px solid rgba(56, 189, 248, 0.5)',
              color: '#38bdf8',
              fontSize: 11,
              fontWeight: 700,
              cursor: 'pointer',
            }}
          >
            <Zap size={13} /> Gắn Động Tác
          </button>
        )}

        <button
          onClick={onOpenDialogueModal}
          title="Gắn / Sửa lời thoại tại giây này"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 5,
            padding: '5px 10px',
            borderRadius: 6,
            background: 'linear-gradient(135deg, rgba(168, 85, 247, 0.3), rgba(236, 72, 153, 0.25))',
            border: '1px solid rgba(168, 85, 247, 0.45)',
            color: '#d8b4fe',
            fontSize: 11,
            fontWeight: 700,
            cursor: 'pointer',
          }}
        >
          <MessageSquare size={13} /> Gắn Thoại
        </button>

        {/* Flexible Add Shot Presets */}
        <div style={{ display: 'flex', alignItems: 'center', background: 'rgba(255,255,255,0.05)', borderRadius: 6, border: '1px solid rgba(255,255,255,0.12)', padding: '2px 4px', gap: 3 }}>
          <span style={{ fontSize: 9.5, color: '#94a3b8', fontWeight: 600, paddingLeft: 2 }}>+Shot:</span>
          {[1, 2, 3, 5, 10].map((sec) => (
            <button
              key={sec}
              onClick={() => onAddNewShot(sec)}
              title={`Thêm đoạn diễn hoạt mới dài ${sec} giây`}
              style={{
                padding: '3px 6px',
                borderRadius: 4,
                background: sec === 3 ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.06)',
                border: sec === 3 ? '1px solid #38bdf8' : 'none',
                color: sec === 3 ? '#38bdf8' : '#e2e8f0',
                fontSize: 10,
                fontWeight: 700,
                cursor: 'pointer',
              }}
            >
              +{sec}s
            </button>
          ))}
        </div>

        {/* Expand / Collapse Height Toggle */}
        <button
          onClick={onToggleExpand}
          title={isExpanded ? 'Thu gọn chiều cao Timeline' : 'Mở rộng chiều cao Timeline để xem nhiều dòng hơn'}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '5px 8px',
            borderRadius: 6,
            background: isExpanded ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255, 255, 255, 0.05)',
            border: isExpanded ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.12)',
            color: isExpanded ? '#38bdf8' : '#94a3b8',
            fontSize: 11,
            fontWeight: 700,
            cursor: 'pointer',
          }}
        >
          {isExpanded ? <Minimize2 size={13} /> : <Maximize2 size={13} />}
          <span>{isExpanded ? 'Thu gọn' : 'Mở rộng'}</span>
        </button>
      </div>
    </div>
  );
};
