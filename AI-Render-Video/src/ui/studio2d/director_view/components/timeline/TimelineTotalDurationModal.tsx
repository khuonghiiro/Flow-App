import React, { useState } from 'react';
import { Clock, X, Check } from 'lucide-react';

interface TimelineTotalDurationModalProps {
  isOpen: boolean;
  currentDuration: number;
  onClose: () => void;
  onApply: (newDuration: number) => void;
}

export const TimelineTotalDurationModal: React.FC<TimelineTotalDurationModalProps> = ({
  isOpen,
  currentDuration,
  onClose,
  onApply,
}) => {
  const [duration, setDuration] = useState(currentDuration);

  if (!isOpen) return null;

  const presets = [3, 5, 8, 10, 15, 20, 30, 60];

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 9999,
        background: 'rgba(0, 0, 0, 0.75)',
        backdropFilter: 'blur(4px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <div
        style={{
          width: 360,
          background: 'linear-gradient(180deg, #0f172a 0%, #090d16 100%)',
          border: '1px solid rgba(56, 189, 248, 0.4)',
          borderRadius: 12,
          padding: 16,
          boxShadow: '0 20px 40px rgba(0,0,0,0.8), 0 0 25px rgba(56,189,248,0.2)',
          display: 'flex',
          flexDirection: 'column',
          gap: 12,
        }}
      >
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ fontSize: 13, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Clock size={15} /> Đặt Tổng Thời Lượng Video
          </span>
          <button onClick={onClose} style={{ background: 'transparent', border: 'none', color: '#94a3b8', cursor: 'pointer' }}>
            <X size={16} />
          </button>
        </div>

        <div style={{ fontSize: 11, color: '#94a3b8' }}>
          Toàn bộ các dòng (nhân vật, cây cối, camera) sẽ tự động khớp và chạy ghép lại theo tổng thời lượng này để xuất video hoàn chỉnh.
        </div>

        {/* Input */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: 12, color: '#e2e8f0', fontWeight: 600 }}>Thời lượng (giây):</span>
          <input
            type="number"
            min={1}
            max={300}
            step={0.5}
            value={duration}
            onChange={(e) => setDuration(Math.max(1, parseFloat(e.target.value) || 1))}
            style={{
              flex: 1,
              background: 'rgba(255, 255, 255, 0.06)',
              border: '1px solid rgba(56, 189, 248, 0.4)',
              borderRadius: 6,
              color: '#38bdf8',
              padding: '6px 10px',
              fontSize: 14,
              fontWeight: 800,
              fontFamily: 'monospace',
              outline: 'none',
            }}
          />
        </div>

        {/* Quick Presets */}
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
          {presets.map((p) => (
            <button
              key={p}
              onClick={() => setDuration(p)}
              style={{
                padding: '4px 8px',
                borderRadius: 5,
                background: duration === p ? '#38bdf8' : 'rgba(255,255,255,0.06)',
                color: duration === p ? '#090d16' : '#cbd5e1',
                border: '1px solid rgba(255,255,255,0.1)',
                fontSize: 11,
                fontWeight: 700,
                cursor: 'pointer',
              }}
            >
              {p}s
            </button>
          ))}
        </div>

        {/* Action Buttons */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 4 }}>
          <button
            onClick={onClose}
            style={{
              padding: '6px 12px',
              borderRadius: 6,
              background: 'rgba(255,255,255,0.08)',
              border: 'none',
              color: '#94a3b8',
              fontSize: 11,
              fontWeight: 600,
              cursor: 'pointer',
            }}
          >
            Hủy
          </button>
          <button
            onClick={() => onApply(duration)}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '6px 14px',
              borderRadius: 6,
              background: 'linear-gradient(135deg, #0284c7, #38bdf8)',
              border: 'none',
              color: '#fff',
              fontSize: 11,
              fontWeight: 700,
              cursor: 'pointer',
              boxShadow: '0 0 12px rgba(56, 189, 248, 0.4)',
            }}
          >
            <Check size={13} /> Áp dụng
          </button>
        </div>
      </div>
    </div>
  );
};
