import React, { useState } from 'react';
import { Eye, EyeOff, Clock, X, Check } from 'lucide-react';
import { formatTimecode } from './timelineConstants';

interface TimelineVisibilityModalProps {
  isOpen: boolean;
  title: string;
  totalDuration: number;
  currentTime: number;
  initialFrom: number;
  initialTo: number;
  onClose: () => void;
  onApply: (from: number, to: number) => void;
}

export const TimelineVisibilityModal: React.FC<TimelineVisibilityModalProps> = ({
  isOpen,
  title,
  totalDuration,
  currentTime,
  initialFrom,
  initialTo,
  onClose,
  onApply,
}) => {
  const [from, setFrom] = useState(initialFrom);
  const [to, setTo] = useState(Math.min(totalDuration, initialTo || totalDuration));

  if (!isOpen) return null;

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
          width: 380,
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
            <Eye size={15} /> Đặt Khoảng Thời Gian Xuất Hiện
          </span>
          <button onClick={onClose} style={{ background: 'transparent', border: 'none', color: '#94a3b8', cursor: 'pointer' }}>
            <X size={16} />
          </button>
        </div>

        <div style={{ fontSize: 11, color: '#cbd5e1' }}>
          Đối tượng: <b style={{ color: '#38bdf8' }}>{title}</b>
        </div>
        <div style={{ fontSize: 10, color: '#94a3b8' }}>
          Ngoài khoảng thời gian này, đối tượng sẽ tự động ẩn trên màn hình và không hiển thị cho đến khi đến giây bắt đầu.
        </div>

        {/* Inputs */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <span style={{ fontSize: 10.5, color: '#94a3b8', fontWeight: 600 }}>Xuất hiện từ (giây):</span>
            <input
              type="number"
              min={0}
              max={to}
              step={0.5}
              value={from}
              onChange={(e) => setFrom(Math.max(0, parseFloat(e.target.value) || 0))}
              style={{
                background: 'rgba(255, 255, 255, 0.06)',
                border: '1px solid rgba(56, 189, 248, 0.4)',
                borderRadius: 6,
                color: '#38bdf8',
                padding: '6px 10px',
                fontSize: 13,
                fontWeight: 800,
                fontFamily: 'monospace',
                outline: 'none',
              }}
            />
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <span style={{ fontSize: 10.5, color: '#94a3b8', fontWeight: 600 }}>Ẩn/Hết lúc (giây):</span>
            <input
              type="number"
              min={from}
              max={totalDuration}
              step={0.5}
              value={to}
              onChange={(e) => setTo(Math.min(totalDuration, Math.max(from, parseFloat(e.target.value) || totalDuration)))}
              style={{
                background: 'rgba(255, 255, 255, 0.06)',
                border: '1px solid rgba(56, 189, 248, 0.4)',
                borderRadius: 6,
                color: '#38bdf8',
                padding: '6px 10px',
                fontSize: 13,
                fontWeight: 800,
                fontFamily: 'monospace',
                outline: 'none',
              }}
            />
          </div>
        </div>

        {/* Presets */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <span style={{ fontSize: 10, color: '#64748b', fontWeight: 600 }}>Phím tắt nhanh:</span>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            <button
              onClick={() => {
                setFrom(0);
                setTo(totalDuration);
              }}
              style={{
                padding: '4px 8px',
                borderRadius: 5,
                background: 'rgba(255,255,255,0.06)',
                color: '#cbd5e1',
                border: '1px solid rgba(255,255,255,0.1)',
                fontSize: 10,
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              Toàn bộ video (0s - {totalDuration}s)
            </button>
            <button
              onClick={() => {
                setFrom(Number(currentTime.toFixed(1)));
                setTo(totalDuration);
              }}
              style={{
                padding: '4px 8px',
                borderRadius: 5,
                background: 'rgba(56,189,248,0.15)',
                color: '#38bdf8',
                border: '1px solid rgba(56,189,248,0.4)',
                fontSize: 10,
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              Bắt đầu từ giây {currentTime.toFixed(1)}s
            </button>
          </div>
        </div>

        {/* Actions */}
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
            onClick={() => onApply(from, to)}
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
            <Check size={13} /> Lưu Khoảng Thời Gian
          </button>
        </div>
      </div>
    </div>
  );
};
