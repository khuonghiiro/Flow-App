import React from 'react';
import {
  Scissors,
  User,
  Eye,
  Smile,
  Shield,
  Sparkles,
  Zap,
  Layers,
} from 'lucide-react';
import { Character2DAssembly, Character2DPartType } from '../../../types/scene2d';

export interface EquipSlotItem {
  slot: Character2DPartType;
  label: string;
  subLabel: string;
  iconType: 'hair_front' | 'hair_back' | 'head' | 'eyes' | 'mouth' | 'outfit' | 'body' | 'weapon';
  zDefault: number;
}

export const CATALOG_LEFT_WING: EquipSlotItem[] = [
  { slot: 'toc_truoc', label: 'Tóc Mái', subLabel: 'Front Hair', iconType: 'hair_front', zDefault: 7 },
  { slot: 'dau', label: 'Khung Đầu', subLabel: 'Head/Face', iconType: 'head', zDefault: 5 },
  { slot: 'mat', label: 'Cặp Mắt', subLabel: 'Eyes & Blink', iconType: 'eyes', zDefault: 6 },
  { slot: 'mieng', label: 'Khẩu Hình', subLabel: 'Mouth / Voice', iconType: 'mouth', zDefault: 6 },
];

export const CATALOG_RIGHT_WING: EquipSlotItem[] = [
  { slot: 'toc_sau', label: 'Tóc Sau', subLabel: 'Back Hair', iconType: 'hair_back', zDefault: 1 },
  { slot: 'trang_phuc', label: 'Trang Phục', subLabel: 'Outfit / Robe', iconType: 'outfit', zDefault: 4 },
  { slot: 'than_co_ban', label: 'Thân Mình', subLabel: 'Body Base', iconType: 'body', zDefault: 3 },
  { slot: 'vu_khi', label: 'Vũ Khí', subLabel: 'Weapon / Prop', iconType: 'weapon', zDefault: 8 },
];

const renderSlotIcon = (type: EquipSlotItem['iconType'], isSelected: boolean) => {
  const color = isSelected ? '#38bdf8' : '#94a3b8';
  switch (type) {
    case 'hair_front':
    case 'hair_back':
      return <Scissors size={18} color={color} />;
    case 'head':
      return <User size={18} color={color} />;
    case 'eyes':
      return <Eye size={18} color={color} />;
    case 'mouth':
      return <Smile size={18} color={color} />;
    case 'outfit':
      return <Layers size={18} color={color} />;
    case 'body':
      return <Shield size={18} color={color} />;
    case 'weapon':
      return <Zap size={18} color={color} />;
    default:
      return <Sparkles size={18} color={color} />;
  }
};

interface CharacterEquipWingProps {
  side: 'left' | 'right';
  assembly: Character2DAssembly;
  selectedSlot: Character2DPartType;
  onSelectSlot: (slot: Character2DPartType) => void;
}

export const CharacterEquipWing: React.FC<CharacterEquipWingProps> = ({
  side,
  assembly,
  selectedSlot,
  onSelectSlot,
}) => {
  const slots = side === 'left' ? CATALOG_LEFT_WING : CATALOG_RIGHT_WING;

  return (
    <div
      style={{
        width: 88,
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        background: 'rgba(8, 13, 26, 0.92)',
        padding: 6,
        borderRadius: 8,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        justifyContent: 'space-around',
      }}
    >
      <div style={{ fontSize: 9, fontWeight: 700, color: '#38bdf8', textAlign: 'center' }}>
        {side === 'left' ? '👤 ĐẦU/MẶT' : '👕 THÂN/KHÍ'}
      </div>

      {slots.map((item) => {
        const isSelected = selectedSlot === item.slot;
        const partConfig = assembly.parts[item.slot];
        const hasImage = Boolean(partConfig?.path);

        return (
          <div
            key={item.slot}
            onClick={() => onSelectSlot(item.slot)}
            style={{
              height: 74,
              borderRadius: 6,
              background: isSelected
                ? 'rgba(56, 189, 248, 0.25)'
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
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              padding: 4,
              gap: 3,
              position: 'relative',
              transition: 'all 0.15s ease',
            }}
            title={`Ô trang bị: ${item.label}`}
          >
            <div
              style={{
                width: 38,
                height: 38,
                borderRadius: 4,
                background: isSelected ? 'rgba(56, 189, 248, 0.15)' : '#040711',
                border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
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
                renderSlotIcon(item.iconType, isSelected)
              )}
            </div>

            <div
              style={{
                fontSize: 9.5,
                fontWeight: isSelected ? 700 : 600,
                color: isSelected ? '#38bdf8' : '#f8fafc',
                textAlign: 'center',
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                maxWidth: '100%',
              }}
            >
              {item.label}
            </div>

            <span
              style={{
                position: 'absolute',
                top: 2,
                right: 3,
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
  );
};
