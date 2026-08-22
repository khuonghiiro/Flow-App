import React from 'react';
import { Character2DAssembly, Character2DPartType } from '../../../types/scene2d';

export interface EquipSlotItem {
  slot: Character2DPartType;
  label: string;
  subLabel: string;
  icon: string;
  zDefault: number;
}

export const EQUIP_SLOTS_DEF: EquipSlotItem[] = [
  { slot: 'toc_truoc', label: 'Tóc Mái', subLabel: 'Front Hair', icon: '💇', zDefault: 7 },
  { slot: 'toc_sau', label: 'Tóc Sau', subLabel: 'Back Hair', icon: '💇', zDefault: 1 },
  { slot: 'dau', label: 'Khung Đầu', subLabel: 'Head / Face', icon: '👤', zDefault: 5 },
  { slot: 'mat', label: 'Cặp Mắt', subLabel: 'Eyes & Blink', icon: '👀', zDefault: 6 },
  { slot: 'mieng', label: 'Khẩu Hình', subLabel: 'Mouth / Voice', icon: '👄', zDefault: 6 },
  { slot: 'trang_phuc', label: 'Trang Phục', subLabel: 'Outfit / Robe', icon: '👕', zDefault: 4 },
  { slot: 'than_co_ban', label: 'Thân Mình', subLabel: 'Body Base', icon: '🧍', zDefault: 3 },
  { slot: 'vu_khi', label: 'Vũ Khí', subLabel: 'Weapon / Prop', icon: '⚔️', zDefault: 8 },
];

interface CharacterEquipSlotHUDProps {
  assembly: Character2DAssembly;
  selectedSlot: Character2DPartType;
  onSelectSlot: (slot: Character2DPartType) => void;
}

export const CharacterEquipSlotHUD: React.FC<CharacterEquipSlotHUDProps> = ({
  assembly,
  selectedSlot,
  onSelectSlot,
}) => {
  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        background: 'rgba(8, 13, 26, 0.9)',
        padding: 8,
        borderRadius: 8,
        border: '1px solid rgba(255, 255, 255, 0.08)',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <span style={{ fontSize: 10.5, fontWeight: 700, color: '#38bdf8', letterSpacing: 0.5 }}>
          🎒 CÁC Ô TRANG BỊ NHÂN VẬT (EQUIPMENT SLOTS)
        </span>
        <span style={{ fontSize: 9.5, color: '#64748b' }}>
          Chọn ô để thay đổi vật liệu
        </span>
      </div>

      {/* Grid of Square Equipment Slots */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(4, 1fr)',
          gap: 6,
        }}
      >
        {EQUIP_SLOTS_DEF.map((item) => {
          const isSelected = selectedSlot === item.slot;
          const partConfig = assembly.parts[item.slot];
          const hasImage = Boolean(partConfig?.path);

          return (
            <div
              key={item.slot}
              onClick={() => onSelectSlot(item.slot)}
              style={{
                height: 64,
                borderRadius: 6,
                background: isSelected
                  ? 'rgba(56, 189, 248, 0.22)'
                  : hasImage
                  ? 'rgba(15, 23, 42, 0.85)'
                  : 'rgba(0, 0, 0, 0.45)',
                border: isSelected
                  ? '2px solid #38bdf8'
                  : hasImage
                  ? '1px solid rgba(56, 189, 248, 0.4)'
                  : '1px dashed rgba(255, 255, 255, 0.15)',
                boxShadow: isSelected ? '0 0 12px rgba(56, 189, 248, 0.5)' : 'none',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                padding: '4px 6px',
                gap: 6,
                transition: 'all 0.15s ease-in-out',
                position: 'relative',
                overflow: 'hidden',
              }}
              title={`Ô trang bị: ${item.label} (${item.subLabel})`}
            >
              {/* Thumbnail / Icon */}
              <div
                style={{
                  width: 38,
                  height: 38,
                  borderRadius: 4,
                  background: '#040711',
                  border: '1px solid rgba(255,255,255,0.08)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  flexShrink: 0,
                  overflow: 'hidden',
                }}
              >
                {hasImage ? (
                  <img
                    src={partConfig?.path}
                    alt={item.label}
                    style={{ maxWidth: '90%', maxHeight: '90%', objectFit: 'contain' }}
                  />
                ) : (
                  <span style={{ fontSize: 16 }}>{item.icon}</span>
                )}
              </div>

              {/* Labels & State */}
              <div style={{ display: 'flex', flexDirection: 'column', overflow: 'hidden', minWidth: 0 }}>
                <div
                  style={{
                    fontSize: 10,
                    fontWeight: isSelected ? 700 : 600,
                    color: isSelected ? '#38bdf8' : '#f8fafc',
                    whiteSpace: 'nowrap',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                  }}
                >
                  {item.label}
                </div>
                <div style={{ fontSize: 8.5, color: '#94a3b8', whiteSpace: 'nowrap' }}>
                  {hasImage ? '✓ Đã trang bị' : 'Trống'}
                </div>
              </div>

              {/* Z-Index Badge */}
              <span
                style={{
                  position: 'absolute',
                  top: 2,
                  right: 4,
                  fontSize: 7.5,
                  fontWeight: 700,
                  color: isSelected ? '#38bdf8' : '#64748b',
                }}
              >
                Z:{partConfig?.z_index ?? item.zDefault}
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
};
