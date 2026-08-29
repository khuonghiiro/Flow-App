import React from 'react';
import {
  Play,
  Pause,
  RotateCcw,
  MessageSquare,
  Zap,
  ChevronLeft,
  ChevronRight,
  Clock,
  Maximize2,
  Minimize2,
  Plus,
} from 'lucide-react';
import { Director2DProject } from '../../../../../types/studio2d_director';
import { formatTimecode } from './timelineConstants';

interface TimelineHeaderToolbarProps {
  project: Director2DProject;
  currentTime: number;
  totalDuration: number;
  isPlaying: boolean;
  selectedActorId?: string | null;
  isExpanded: boolean;
  onTogglePlay: () => void;
  onSeek: (time: number) => void;
  onToggleExpand: () => void;
  onOpenActionModal: () => void;
  onOpenDialogueModal: () => void;
  onOpenTotalDurationModal: () => void;
  onExtendDuration: (secondsToAdd: number) => void;
}

export const TimelineHeaderToolbar: React.FC<TimelineHeaderToolbarProps> = ({
  project,
  currentTime,
  totalDuration,
  isPlaying,
  selectedActorId,
  isExpanded,
  onTogglePlay,
  onSeek,
  onToggleExpand,
  onOpenActionModal,
  onOpenDialogueModal,
  onOpenTotalDurationModal,
  onExtendDuration,
}) => {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
      {/* Left Playback Controls & Video Duration */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <button
          onClick={onTogglePlay}
          title={isPlaying ? 'Tạm dừng video (Space)' : 'Phát toàn bộ kịch bản ghép video (Space)'}
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
            boxShadow: isPlaying ? '0 0 14px rgba(239,68,68,0.6)' : '0 0 14px rgba(56,189,248,0.6)',
            transition: 'all 0.15s',
          }}
        >
          {isPlaying ? <Pause size={17} /> : <Play size={17} style={{ marginLeft: 2 }} />}
        </button>

        <button
          onClick={() => onSeek(0)}
          title="Quay về đầu video (0.00s)"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 28,
            height: 28,
            borderRadius: 6,
            background: 'rgba(255,255,255,0.05)',
            border: '1px solid rgba(255,255,255,0.12)',
            color: '#94a3b8',
            cursor: 'pointer',
          }}
        >
          <RotateCcw size={12} />
        </button>

        <button
          onClick={() => onSeek(Math.max(0, currentTime - 0.5))}
          title="Lùi 0.5 giây"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 26,
            height: 28,
            borderRadius: 6,
            background: 'rgba(255,255,255,0.04)',
            border: '1px solid rgba(255,255,255,0.08)',
            color: '#94a3b8',
            cursor: 'pointer',
          }}
        >
          <ChevronLeft size={13} />
        </button>

        <button
          onClick={() => onSeek(Math.min(totalDuration, currentTime + 0.5))}
          title="Tiến 0.5 giây"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 26,
            height: 28,
            borderRadius: 6,
            background: 'rgba(255,255,255,0.04)',
            border: '1px solid rgba(255,255,255,0.08)',
            color: '#94a3b8',
            cursor: 'pointer',
          }}
        >
          <ChevronRight size={13} />
        </button>

        {/* Digital Timecode */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            background: 'rgba(2, 6, 23, 0.9)',
            border: '1px solid rgba(56, 189, 248, 0.4)',
            borderRadius: 6,
            padding: '3px 8px',
            fontFamily: 'monospace',
            fontSize: 11.5,
            fontWeight: 700,
            color: '#38bdf8',
            boxShadow: '0 0 10px rgba(56, 189, 248, 0.2) inset',
          }}
        >
          <span>{formatTimecode(currentTime)}</span>
          <span style={{ color: '#475569' }}>/</span>
          <span style={{ color: '#94a3b8' }}>{formatTimecode(totalDuration)}</span>
        </div>

        {/* Total Video Duration Editor Pill */}
        <button
          onClick={onOpenTotalDurationModal}
          title="Bấm để chỉnh tổng thời lượng video hoàn chỉnh"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 5,
            padding: '3px 8px',
            borderRadius: 6,
            background: 'rgba(56, 189, 248, 0.15)',
            border: '1px solid #38bdf8',
            color: '#38bdf8',
            fontSize: 11,
            fontWeight: 800,
            cursor: 'pointer',
          }}
        >
          <Clock size={12} />
          <span>Tổng video: {totalDuration.toFixed(1)}s</span>
          <span style={{ fontSize: 9, opacity: 0.8 }}>✏️</span>
        </button>
      </div>

      {/* Right Controls: Actions, Presets, Expand Button */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
        {selectedActorId && (
          <button
            onClick={onOpenActionModal}
            title={`Gắn động tác tại ${formatTimecode(currentTime)}`}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '4px 9px',
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
            padding: '4px 9px',
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

        {/* Quick Extend Video Duration */}
        <div style={{ display: 'flex', alignItems: 'center', background: 'rgba(255,255,255,0.05)', borderRadius: 6, border: '1px solid rgba(255,255,255,0.1)', padding: '2px 4px', gap: 3 }}>
          <span style={{ fontSize: 9.5, color: '#94a3b8', fontWeight: 600, paddingLeft: 2 }}>Tăng:</span>
          {[2, 5, 10].map((sec) => (
            <button
              key={sec}
              onClick={() => onExtendDuration(sec)}
              title={`Kéo dài thêm ${sec} giây cho video`}
              style={{
                padding: '2px 6px',
                borderRadius: 4,
                background: 'rgba(255,255,255,0.06)',
                border: 'none',
                color: '#e2e8f0',
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
            padding: '4px 8px',
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
