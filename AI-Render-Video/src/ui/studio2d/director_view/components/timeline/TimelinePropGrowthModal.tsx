import React from 'react';
import { Trees, X } from 'lucide-react';
import { PropGrowthStage, Director2DProject } from '../../../../../types/studio2d_director';
import { PROP_GROWTH_OPTIONS, formatTimecode } from './timelineConstants';

interface TimelinePropGrowthModalProps {
  modalState: {
    isOpen: boolean;
    propId: string;
    targetShotId: string;
    targetTime: number;
    currentStage: PropGrowthStage;
  };
  project: Director2DProject;
  onClose: () => void;
  onApplyPropGrowth: (stage: PropGrowthStage) => void;
}

export const TimelinePropGrowthModal: React.FC<TimelinePropGrowthModalProps> = ({
  modalState,
  project,
  onClose,
  onApplyPropGrowth,
}) => {
  const prop = project.props.find((p) => p.id === modalState.propId);

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
          border: '1px solid rgba(34, 197, 94, 0.5)',
          boxShadow: '0 16px 50px rgba(0, 0, 0, 0.9), 0 0 30px rgba(34, 197, 94, 0.25)',
          borderRadius: 14,
          padding: 18,
          width: 480,
          maxWidth: '92vw',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 14, fontWeight: 700, color: '#4ade80' }}>
            <Trees size={18} /> Giai Đoạn Sinh Trưởng: {prop?.name || 'Cây Cối / Đạo Cụ'}
          </div>
          <button
            onClick={onClose}
            style={{ background: 'transparent', border: 'none', color: '#94a3b8', cursor: 'pointer' }}
          >
            <X size={18} />
          </button>
        </div>

        <div style={{ fontSize: 11.5, color: '#94a3b8', marginBottom: 14 }}>
          Cài đặt giai đoạn sinh trưởng, lớn dần, nở hoa hoặc ra quả tại mốc: <strong style={{ color: '#4ade80' }}>{formatTimecode(modalState.targetTime)}</strong>
        </div>

        {/* Prop Growth Options */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 10, marginBottom: 16 }}>
          {PROP_GROWTH_OPTIONS.map((opt) => {
            const isSelected = modalState.currentStage === opt.id;
            return (
              <button
                key={opt.id}
                onClick={() => onApplyPropGrowth(opt.id)}
                style={{
                  display: 'flex',
                  alignItems: 'flex-start',
                  gap: 10,
                  padding: '10px 12px',
                  borderRadius: 10,
                  background: isSelected ? `${opt.color}30` : 'rgba(255, 255, 255, 0.04)',
                  border: isSelected ? `2px solid ${opt.color}` : '1px solid rgba(255, 255, 255, 0.1)',
                  boxShadow: isSelected ? `0 0 14px ${opt.color}66` : 'none',
                  color: isSelected ? '#fff' : '#cbd5e1',
                  cursor: 'pointer',
                  textAlign: 'left',
                  transition: 'all 0.15s',
                }}
              >
                <span style={{ fontSize: 24, lineHeight: 1 }}>{opt.icon}</span>
                <div>
                  <div style={{ fontSize: 12, fontWeight: 700, color: isSelected ? opt.color : '#f1f5f9' }}>
                    {opt.label}
                  </div>
                  <div style={{ fontSize: 9.5, color: '#94a3b8', marginTop: 3 }}>{opt.desc}</div>
                </div>
              </button>
            );
          })}
        </div>

        <div style={{ borderTop: '1px solid rgba(255,255,255,0.08)', paddingTop: 12, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontSize: 10.5, color: '#64748b' }}>
            🌸 Cây cối trên sân khấu 2D sẽ tự động chuyển hoạt ảnh tương ứng
          </span>
          <button
            onClick={onClose}
            style={{
              padding: '6px 14px',
              borderRadius: 6,
              background: 'rgba(255, 255, 255, 0.08)',
              border: '1px solid rgba(255, 255, 255, 0.15)',
              color: '#e2e8f0',
              fontSize: 11.5,
              cursor: 'pointer',
            }}
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
};
