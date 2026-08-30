import React from 'react';
import { Scissors, Check, Sparkles } from 'lucide-react';

export interface SlicerGalleryBBoxCardProps {
  isDirectBBoxCropActive?: boolean;
  directBBoxPadding?: number;
  setDirectBBoxPadding?: (pad: number) => void;
  onToggleDirectBBoxCrop?: () => void;
  onApplyDirectBBoxCrop?: () => void;
  checkedCount?: number;
}

export const SlicerGalleryBBoxCard: React.FC<SlicerGalleryBBoxCardProps> = ({
  isDirectBBoxCropActive = false,
  directBBoxPadding = 0,
  setDirectBBoxPadding,
  onToggleDirectBBoxCrop,
  onApplyDirectBBoxCrop,
  checkedCount = 0,
}) => {
  return (
    <div
      style={{
        background: 'linear-gradient(180deg, rgba(30, 20, 50, 0.75) 0%, rgba(15, 23, 42, 0.95) 100%)',
        borderRadius: 8,
        border: isDirectBBoxCropActive
          ? '1.5px solid #c084fc'
          : '1px solid rgba(192, 132, 252, 0.3)',
        padding: 8,
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        boxShadow: isDirectBBoxCropActive
          ? '0 4px 16px rgba(0, 0, 0, 0.5), 0 0 12px rgba(192, 132, 252, 0.2)'
          : '0 4px 12px rgba(0, 0, 0, 0.3)',
        boxSizing: 'border-box',
        flexShrink: 0,
        transition: 'all 0.2s ease',
      }}
    >
      {/* Header: Title + Toggle Switch */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          paddingBottom: 4,
          borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
        }}
      >
        <span
          style={{
            fontSize: 10,
            fontWeight: 700,
            color: '#e9d5ff',
            display: 'flex',
            alignItems: 'center',
            gap: 4,
          }}
        >
          <Scissors size={12} color="#c084fc" /> TẠO KHUNG BOUNDING BOX (BBOX)
        </span>

        {onToggleDirectBBoxCrop && (
          <button
            onClick={onToggleDirectBBoxCrop}
            style={{
              height: 20,
              padding: '0 6px',
              fontSize: 9,
              fontWeight: 700,
              borderRadius: 4,
              border: isDirectBBoxCropActive ? '1px solid #c084fc' : '1px solid rgba(255, 255, 255, 0.15)',
              background: isDirectBBoxCropActive ? 'rgba(192, 132, 252, 0.3)' : 'rgba(255, 255, 255, 0.06)',
              color: isDirectBBoxCropActive ? '#f3e8ff' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 3,
              transition: 'all 0.15s ease',
            }}
            title={isDirectBBoxCropActive ? 'Tắt chế độ tạo khung BBox' : 'Bật chế độ tạo khung BBox'}
          >
            {isDirectBBoxCropActive ? '🟢 BẬT' : '⚪ TẮT'}
          </button>
        )}
      </div>

      {/* Padding Slider & Quick Presets */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#cbd5e1' }}>
          <span style={{ color: '#c084fc', fontWeight: 600 }}>Khoảng cách lề (Padding):</span>
          <span style={{ color: '#4ade80', fontWeight: 800, fontFamily: 'monospace' }}>
            +{directBBoxPadding}px
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
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

        <div style={{ display: 'flex', gap: 2 }}>
          {[0, 5, 10, 15, 20, 30].map((val) => (
            <button
              key={val}
              onClick={() => setDirectBBoxPadding && setDirectBBoxPadding(val)}
              style={{
                flex: 1,
                height: 18,
                fontSize: 8.5,
                fontWeight: 600,
                borderRadius: 3,
                background: directBBoxPadding === val ? '#9333ea' : 'rgba(255,255,255,0.06)',
                color: directBBoxPadding === val ? '#ffffff' : '#cbd5e1',
                border: directBBoxPadding === val ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.08)',
                cursor: 'pointer',
                padding: 0,
              }}
            >
              {val === 0 ? '0' : `+${val}`}
            </button>
          ))}
        </div>
      </div>

      {/* Action Button: Apply BBox to image */}
      {onApplyDirectBBoxCrop && (
        <button
          onClick={onApplyDirectBBoxCrop}
          style={{
            height: 28,
            fontSize: 10,
            fontWeight: 700,
            borderRadius: 5,
            background: 'linear-gradient(135deg, #9333ea 0%, #7c3aed 100%)',
            color: '#ffffff',
            border: '1px solid #c084fc',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 5,
            boxShadow: '0 2px 8px rgba(147, 51, 234, 0.4)',
            marginTop: 2,
            transition: 'all 0.15s ease',
          }}
          title="Tạo khung bbox và cắt theo khung viền đã nhận diện (không khử nền)"
        >
          <Check size={12} />
          {checkedCount > 1
            ? `Áp Dụng Khung BBox (${checkedCount} ảnh)`
            : 'Áp Dụng Khung BBox Vào Ảnh'}
        </button>
      )}
    </div>
  );
};
