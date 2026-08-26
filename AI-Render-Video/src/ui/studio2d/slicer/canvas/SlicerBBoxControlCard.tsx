import React from 'react';
import { Scissors, X, Check } from 'lucide-react';

interface SlicerBBoxControlCardProps {
  isDirectBBoxCropActive: boolean;
  hasImage: boolean;
  checkedCount: number;
  directBBoxPadding: number;
  setDirectBBoxPadding?: (pad: number) => void;
  onToggleDirectBBoxCrop?: () => void;
  onApplyDirectBBoxCrop?: () => void;
}

export const SlicerBBoxControlCard: React.FC<SlicerBBoxControlCardProps> = ({
  isDirectBBoxCropActive,
  hasImage,
  checkedCount,
  directBBoxPadding,
  setDirectBBoxPadding,
  onToggleDirectBBoxCrop,
  onApplyDirectBBoxCrop,
}) => {
  if (!isDirectBBoxCropActive || (!hasImage && checkedCount === 0)) {
    return null;
  }

  return (
    <div
      style={{
        position: 'absolute',
        bottom: 12,
        left: 12,
        zIndex: 35,
        background: 'rgba(15, 23, 42, 0.95)',
        backdropFilter: 'blur(12px)',
        border: '1.5px solid #c084fc',
        borderRadius: 8,
        padding: '10px 12px',
        boxShadow: '0 8px 32px rgba(0,0,0,0.8), 0 0 16px rgba(192, 132, 252, 0.3)',
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        minWidth: 260,
        maxWidth: 320,
        fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
      }}
    >
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          borderBottom: '1px solid rgba(255,255,255,0.1)',
          paddingBottom: 5,
        }}
      >
        <div
          style={{
            fontSize: 11,
            fontWeight: 700,
            color: '#e9d5ff',
            display: 'flex',
            alignItems: 'center',
            gap: 5,
          }}
        >
          <Scissors size={13} color="#c084fc" />{' '}
          {checkedCount > 1 ? `✂️ Cắt Bounding Box (${checkedCount} ảnh)` : '✂️ Cắt Bounding Box Tự Động'}
        </div>
        {onToggleDirectBBoxCrop && (
          <button
            onClick={onToggleDirectBBoxCrop}
            style={{
              background: 'none',
              border: 'none',
              color: '#94a3b8',
              cursor: 'pointer',
              padding: 2,
              display: 'flex',
              alignItems: 'center',
            }}
            title="Đóng / Tắt chế độ cắt"
          >
            <X size={13} />
          </button>
        )}
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            fontSize: 10,
            color: '#cbd5e1',
          }}
        >
          <span style={{ color: '#c084fc', fontWeight: 600 }}>Khoảng cách lề (Padding):</span>
          <span style={{ color: '#4ade80', fontWeight: 800, fontFamily: 'monospace' }}>
            +{directBBoxPadding}px
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <input
            type="range"
            min="0"
            max="50"
            step="1"
            value={directBBoxPadding}
            onChange={(e) => setDirectBBoxPadding && setDirectBBoxPadding(parseInt(e.target.value, 10))}
            style={{ flex: 1, accentColor: '#a855f7', cursor: 'pointer' }}
          />
        </div>

        <div style={{ display: 'flex', gap: 3, flexWrap: 'wrap' }}>
          {[0, 5, 10, 15, 20, 30].map((val) => (
            <button
              key={val}
              onClick={() => setDirectBBoxPadding && setDirectBBoxPadding(val)}
              style={{
                flex: 1,
                height: 20,
                fontSize: 9,
                fontWeight: 600,
                borderRadius: 3,
                background: directBBoxPadding === val ? '#9333ea' : 'rgba(255,255,255,0.06)',
                color: directBBoxPadding === val ? '#ffffff' : '#cbd5e1',
                border: directBBoxPadding === val ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.08)',
                cursor: 'pointer',
              }}
            >
              {val === 0 ? '0px' : `+${val}px`}
            </button>
          ))}
        </div>
      </div>

      {onApplyDirectBBoxCrop && (
        <button
          onClick={onApplyDirectBBoxCrop}
          style={{
            height: 30,
            fontSize: 11,
            fontWeight: 700,
            borderRadius: 6,
            background: 'linear-gradient(135deg, #9333ea 0%, #7c3aed 100%)',
            color: '#ffffff',
            border: '1px solid #c084fc',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 5,
            boxShadow: '0 2px 10px rgba(147, 51, 234, 0.4)',
            transition: 'all 0.15s ease',
          }}
          title={
            checkedCount > 1
              ? `Cắt gọt ${checkedCount} ảnh theo Bounding Box`
              : 'Cắt gọt ảnh theo Bounding Box và thay thế làm ảnh nguồn hiện tại'
          }
        >
          <Check size={13} /> {checkedCount > 1 ? `✓ Áp dụng cắt (${checkedCount} ảnh)` : '✓ Áp dụng cắt ảnh'}
        </button>
      )}
    </div>
  );
};
