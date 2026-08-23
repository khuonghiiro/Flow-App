import React from 'react';
import { Compass } from 'lucide-react';
import { Character2DAngle, Character2DAssembly, Character2DPartType } from '../../../types/scene2d';

export interface AngleMenuItem {
  id: Character2DAngle;
  label: string;
  compass: string;
  deg: number;
  isTop?: boolean;
  category: 'horizontal' | 'top_down';
}

export const CAMERA_ANGLES_MENU: AngleMenuItem[] = [
  // Horizontal 360°
  { id: 'front', label: 'Chính Diện (0°)', compass: 'S', deg: 0, category: 'horizontal' },
  { id: 'three_quarter_left', label: 'Nghiêng Trái (45°)', compass: 'SE', deg: 45, category: 'horizontal' },
  { id: 'profile_left', label: 'Ngang Trái (90°)', compass: 'E', deg: 90, category: 'horizontal' },
  { id: 'back_three_quarter_left', label: 'Sau Trái (135°)', compass: 'NE', deg: 135, category: 'horizontal' },
  { id: 'back', label: 'Sau Lưng (180°)', compass: 'N', deg: 180, category: 'horizontal' },
  { id: 'back_three_quarter_right', label: 'Sau Phải (225°)', compass: 'NW', deg: 225, category: 'horizontal' },
  { id: 'profile_right', label: 'Ngang Phải (270°)', compass: 'W', deg: 270, category: 'horizontal' },
  { id: 'three_quarter_right', label: 'Nghiêng Phải (315°)', compass: 'SW', deg: 315, category: 'horizontal' },

  // Top-Down Bird's Eye
  { id: 'top_down', label: 'Đỉnh Đầu 0°', compass: '👑 0°', deg: 0, isTop: true, category: 'top_down' },
  { id: 'top_down_three_quarter_left', label: 'Đỉnh Nghiêng Trái 45°', compass: '👑 45°', deg: 45, isTop: true, category: 'top_down' },
  { id: 'top_down_profile_left', label: 'Đỉnh Ngang Trái 90°', compass: '👑 90°', deg: 90, isTop: true, category: 'top_down' },
  { id: 'top_down_back', label: 'Đỉnh Sau Lưng 180°', compass: '👑 180°', deg: 180, isTop: true, category: 'top_down' },
];

interface TunerAngleSidebarProps {
  selectedAngle: Character2DAngle;
  onSelectAngle: (item: AngleMenuItem) => void;
  selectedSlot: Character2DPartType;
  assemblyDraft: Character2DAssembly;
}

export const TunerAngleSidebar: React.FC<TunerAngleSidebarProps> = ({
  selectedAngle,
  onSelectAngle,
  selectedSlot,
  assemblyDraft,
}) => {
  return (
    <div
      style={{
        padding: '12px 10px',
        background: 'rgba(9, 14, 28, 0.95)',
        borderRight: '1px solid rgba(255, 255, 255, 0.08)',
        display: 'flex',
        flexDirection: 'column',
        gap: 4,
        overflowY: 'auto',
      }}
    >
      <div style={{ fontSize: 10.5, fontWeight: 700, color: '#38bdf8', marginBottom: 4, display: 'flex', alignItems: 'center', gap: 5 }}>
        <Compass size={13} /> 🎥 GÓC QUAY CAMERA:
      </div>

      {/* Horizontal Angles Category */}
      <div style={{ fontSize: 9.5, fontWeight: 600, color: '#64748b', marginTop: 4, marginBottom: 2 }}>
        XOAY NGANG 360°:
      </div>
      {CAMERA_ANGLES_MENU.filter((a) => a.category === 'horizontal').map((ang) => {
        const isSelected = selectedAngle === ang.id;
        const hasOverride = Boolean(assemblyDraft.parts[selectedSlot]?.angle_overrides?.[ang.id]);

        return (
          <button
            key={ang.id}
            onClick={() => onSelectAngle(ang)}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              padding: '7px 10px',
              borderRadius: 6,
              border: isSelected ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
              background: isSelected ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.02)',
              color: isSelected ? '#38bdf8' : '#cbd5e1',
              cursor: 'pointer',
              fontSize: 10.5,
              fontWeight: isSelected ? 700 : 500,
              textAlign: 'left',
              boxShadow: isSelected ? '0 0 10px rgba(56, 189, 248, 0.2)' : 'none',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ fontSize: 9, padding: '1px 5px', borderRadius: 3, background: isSelected ? '#0284c7' : 'rgba(255,255,255,0.08)', color: '#fff', fontWeight: 700 }}>
                {ang.compass}
              </span>
              <span>{ang.label}</span>
            </div>
            {hasOverride && <span style={{ fontSize: 9, color: '#4ade80', fontWeight: 700 }}>•</span>}
          </button>
        );
      })}

      {/* Top-Down Angles Category */}
      <div style={{ fontSize: 9.5, fontWeight: 600, color: '#64748b', marginTop: 10, marginBottom: 2 }}>
        SOI ĐỈNH ĐẦU TỪ TRÊN XUỐNG:
      </div>
      {CAMERA_ANGLES_MENU.filter((a) => a.category === 'top_down').map((ang) => {
        const isSelected = selectedAngle === ang.id;
        const hasOverride = Boolean(assemblyDraft.parts[selectedSlot]?.angle_overrides?.[ang.id]);

        return (
          <button
            key={ang.id}
            onClick={() => onSelectAngle(ang)}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              padding: '7px 10px',
              borderRadius: 6,
              border: isSelected ? '1.5px solid #eab308' : '1px solid rgba(255,255,255,0.06)',
              background: isSelected ? 'rgba(234, 179, 8, 0.2)' : 'rgba(255,255,255,0.02)',
              color: isSelected ? '#facc15' : '#cbd5e1',
              cursor: 'pointer',
              fontSize: 10.5,
              fontWeight: isSelected ? 700 : 500,
              textAlign: 'left',
              boxShadow: isSelected ? '0 0 10px rgba(234, 179, 8, 0.2)' : 'none',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ fontSize: 9, padding: '1px 5px', borderRadius: 3, background: isSelected ? '#ca8a04' : 'rgba(255,255,255,0.08)', color: '#fff', fontWeight: 700 }}>
                {ang.compass}
              </span>
              <span>{ang.label}</span>
            </div>
            {hasOverride && <span style={{ fontSize: 9, color: '#4ade80', fontWeight: 700 }}>•</span>}
          </button>
        );
      })}
    </div>
  );
};
