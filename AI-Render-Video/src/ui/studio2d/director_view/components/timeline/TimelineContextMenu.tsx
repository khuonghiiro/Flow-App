import React, { useEffect, useRef } from 'react';
import { Zap, MessageSquare, Clock, X } from 'lucide-react';
import { formatTimecode } from './timelineConstants';

interface TimelineContextMenuProps {
  menuState: {
    x: number;
    y: number;
    actorId: string;
    actorName: string;
    shotId: string;
    time: number;
  };
  onClose: () => void;
  onOpenActionModal: () => void;
  onOpenDialogueModal: () => void;
  onOpenDurationModal: () => void;
}

export const TimelineContextMenu: React.FC<TimelineContextMenuProps> = ({
  menuState,
  onClose,
  onOpenActionModal,
  onOpenDialogueModal,
  onOpenDurationModal,
}) => {
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleOutsideClick = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        onClose();
      }
    };
    window.addEventListener('mousedown', handleOutsideClick);
    return () => window.removeEventListener('mousedown', handleOutsideClick);
  }, [onClose]);

  // Adjust menu position so it doesn't overflow screen boundaries
  const left = Math.min(window.innerWidth - 220, menuState.x);
  const top = Math.min(window.innerHeight - 200, menuState.y);

  return (
    <div
      ref={menuRef}
      style={{
        position: 'fixed',
        left,
        top,
        zIndex: 99999,
        background: 'linear-gradient(180deg, #0f172a 0%, #090d16 100%)',
        border: '1px solid rgba(56, 189, 248, 0.4)',
        boxShadow: '0 10px 30px rgba(0, 0, 0, 0.85), 0 0 20px rgba(56, 189, 248, 0.15)',
        borderRadius: 8,
        padding: '6px 4px',
        minWidth: 200,
        userSelect: 'none',
        animation: 'fadeIn 0.1s ease-out',
      }}
    >
      {/* Title */}
      <div
        style={{
          fontSize: 9.5,
          fontWeight: 700,
          color: '#94a3b8',
          padding: '4px 8px 6px',
          borderBottom: '1px solid rgba(255,255,255,0.08)',
          marginBottom: 4,
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}
      >
        <span>{menuState.actorName} ({formatTimecode(menuState.time)})</span>
        <button
          onClick={onClose}
          style={{ background: 'transparent', border: 'none', color: '#64748b', cursor: 'pointer', padding: 0 }}
        >
          <X size={12} />
        </button>
      </div>

      {/* Menu Options */}
      <button
        onClick={() => {
          onClose();
          onOpenActionModal();
        }}
        style={{
          width: '100%',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          padding: '6px 8px',
          borderRadius: 5,
          background: 'transparent',
          border: 'none',
          color: '#38bdf8',
          fontSize: 11,
          fontWeight: 600,
          cursor: 'pointer',
          textAlign: 'left',
        }}
        onMouseEnter={(e) => (e.currentTarget.style.background = 'rgba(56, 189, 248, 0.15)')}
        onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
      >
        <Zap size={13} color="#38bdf8" /> Gắn / Đổi Hành Động
      </button>

      <button
        onClick={() => {
          onClose();
          onOpenDialogueModal();
        }}
        style={{
          width: '100%',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          padding: '6px 8px',
          borderRadius: 5,
          background: 'transparent',
          border: 'none',
          color: '#d8b4fe',
          fontSize: 11,
          fontWeight: 600,
          cursor: 'pointer',
          textAlign: 'left',
        }}
        onMouseEnter={(e) => (e.currentTarget.style.background = 'rgba(168, 85, 247, 0.15)')}
        onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
      >
        <MessageSquare size={13} color="#c084fc" /> Gắn / Sửa Lời Thoại
      </button>

      <button
        onClick={() => {
          onClose();
          onOpenDurationModal();
        }}
        style={{
          width: '100%',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          padding: '6px 8px',
          borderRadius: 5,
          background: 'transparent',
          border: 'none',
          color: '#e2e8f0',
          fontSize: 11,
          fontWeight: 600,
          cursor: 'pointer',
          textAlign: 'left',
        }}
        onMouseEnter={(e) => (e.currentTarget.style.background = 'rgba(255, 255, 255, 0.08)')}
        onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
      >
        <Clock size={13} color="#94a3b8" /> Đổi Thời Lượng Video
      </button>
    </div>
  );
};
