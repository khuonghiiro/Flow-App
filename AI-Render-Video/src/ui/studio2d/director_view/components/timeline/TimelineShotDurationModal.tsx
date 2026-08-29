import React from 'react';
import { Clock, X } from 'lucide-react';

interface TimelineShotDurationModalProps {
  modalState: {
    isOpen: boolean;
    shotId: string;
    duration: number;
    title: string;
  };
  onClose: () => void;
  onChangeDuration: (dur: number) => void;
  onApplyDuration: (shotId: string, duration: number) => void;
}

export const TimelineShotDurationModal: React.FC<TimelineShotDurationModalProps> = ({
  modalState,
  onClose,
  onChangeDuration,
  onApplyDuration,
}) => {
  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 9999,
        background: 'rgba(0, 0, 0, 0.72)',
        backdropFilter: 'blur(6px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
      onClick={onClose}
    >
      <div
        style={{
          background: 'linear-gradient(180deg, #0f172a 0%, #090d16 100%)',
          border: '1px solid rgba(56, 189, 248, 0.5)',
          boxShadow: '0 16px 50px rgba(0, 0, 0, 0.9), 0 0 30px rgba(56, 189, 248, 0.25)',
          borderRadius: 14,
          padding: 18,
          width: 380,
          maxWidth: '92vw',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 14, fontWeight: 700, color: '#38bdf8' }}>
            <Clock size={18} /> Chỉnh Độ Dài Thời Gian Shot
          </div>
          <button
            onClick={onClose}
            style={{ background: 'transparent', border: 'none', color: '#94a3b8', cursor: 'pointer' }}
          >
            <X size={18} />
          </button>
        </div>

        <div style={{ fontSize: 11.5, color: '#94a3b8', marginBottom: 14 }}>
          Shot: <strong style={{ color: '#fff' }}>{modalState.title}</strong>
        </div>

        {/* Duration Input & Stepper */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10, marginBottom: 16 }}>
          <button
            onClick={() => onChangeDuration(Math.max(0.5, parseFloat((modalState.duration - 0.5).toFixed(1))))}
            style={{
              width: 36,
              height: 36,
              borderRadius: 8,
              background: 'rgba(255, 255, 255, 0.08)',
              border: '1px solid rgba(255, 255, 255, 0.15)',
              color: '#fff',
              fontSize: 18,
              fontWeight: 700,
              cursor: 'pointer',
            }}
          >
            -
          </button>

          <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <input
              type="number"
              min="0.5"
              max="60"
              step="0.5"
              value={modalState.duration}
              onChange={(e) => onChangeDuration(parseFloat(e.target.value) || 0.5)}
              style={{
                width: 90,
                textAlign: 'center',
                padding: '8px 10px',
                borderRadius: 8,
                background: 'rgba(2, 6, 23, 0.9)',
                border: '1.5px solid #38bdf8',
                color: '#38bdf8',
                fontSize: 18,
                fontWeight: 800,
                outline: 'none',
              }}
            />
            <span style={{ fontSize: 14, fontWeight: 700, color: '#94a3b8' }}>giây</span>
          </div>

          <button
            onClick={() => onChangeDuration(Math.min(60, parseFloat((modalState.duration + 0.5).toFixed(1))))}
            style={{
              width: 36,
              height: 36,
              borderRadius: 8,
              background: 'rgba(255, 255, 255, 0.08)',
              border: '1px solid rgba(255, 255, 255, 0.15)',
              color: '#fff',
              fontSize: 18,
              fontWeight: 700,
              cursor: 'pointer',
            }}
          >
            +
          </button>
        </div>

        {/* Quick Presets */}
        <div style={{ display: 'flex', justifyContent: 'center', gap: 6, marginBottom: 16 }}>
          {[1.0, 2.0, 3.5, 5.0, 8.0, 10.0].map((s) => (
            <button
              key={s}
              onClick={() => onChangeDuration(s)}
              style={{
                padding: '4px 8px',
                borderRadius: 6,
                background: modalState.duration === s ? 'rgba(56, 189, 248, 0.3)' : 'rgba(255, 255, 255, 0.05)',
                border: modalState.duration === s ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.1)',
                color: modalState.duration === s ? '#38bdf8' : '#e2e8f0',
                fontSize: 11,
                fontWeight: 700,
                cursor: 'pointer',
              }}
            >
              {s}s
            </button>
          ))}
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          <button
            onClick={onClose}
            style={{
              padding: '6px 14px',
              borderRadius: 6,
              background: 'rgba(255, 255, 255, 0.06)',
              border: '1px solid rgba(255, 255, 255, 0.12)',
              color: '#94a3b8',
              fontSize: 11.5,
              cursor: 'pointer',
            }}
          >
            Hủy
          </button>
          <button
            onClick={() => onApplyDuration(modalState.shotId, modalState.duration)}
            style={{
              padding: '6px 16px',
              borderRadius: 6,
              background: 'linear-gradient(135deg, #0284c7, #38bdf8)',
              border: 'none',
              color: '#fff',
              fontSize: 11.5,
              fontWeight: 700,
              cursor: 'pointer',
            }}
          >
            Lưu Thời Lượng
          </button>
        </div>
      </div>
    </div>
  );
};
