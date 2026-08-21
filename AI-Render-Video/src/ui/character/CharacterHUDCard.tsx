import React from 'react';
import { Edit3, Zap } from 'lucide-react';
import { CharacterSkillItem } from '../CharacterAssetRegistry';

interface CharacterHUDCardProps {
  charName: string;
  charAge: number | string;
  charHeightCm: number;
  charGender: 'male' | 'female' | 'unisex';
  charEducation: string;
  charOccupation: string;
  charFaction: string;
  charPersonality: string;
  skills: CharacterSkillItem[];
  onOpenEditModal: () => void;
}

export const CharacterHUDCard: React.FC<CharacterHUDCardProps> = ({
  charName,
  charAge,
  charHeightCm,
  charGender,
  charEducation,
  charOccupation,
  charFaction,
  charPersonality,
  skills,
  onOpenEditModal,
}) => {
  return (
    <div
      style={{
        padding: '6px 12px',
        background: 'rgba(15, 23, 42, 0.95)',
        borderTop: '1px solid rgba(255, 255, 255, 0.08)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: 8,
        flexShrink: 0,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, overflow: 'hidden' }}>
        <div
          style={{
            width: 32,
            height: 32,
            borderRadius: 6,
            background: 'rgba(56, 189, 248, 0.15)',
            border: '1px solid rgba(56, 189, 248, 0.3)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 16,
            flexShrink: 0,
          }}
        >
          {charGender === 'female' ? '👩' : '🧑'}
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ fontSize: 12, fontWeight: 700, color: '#f8fafc' }}>
              {charName || 'Chưa đặt tên'}
            </span>
            <span style={{ fontSize: 10, color: '#38bdf8', fontWeight: 600 }}>
              {charAge} tuổi • {charHeightCm}cm
            </span>
            <span
              style={{
                fontSize: 9,
                padding: '1px 6px',
                borderRadius: 10,
                background: 'rgba(168, 85, 247, 0.2)',
                color: '#c084fc',
                fontWeight: 600,
              }}
            >
              {charEducation}
            </span>
            {skills.length > 0 && (
              <span
                style={{
                  fontSize: 9,
                  padding: '1px 6px',
                  borderRadius: 10,
                  background: 'rgba(234, 179, 8, 0.2)',
                  color: '#facc15',
                  fontWeight: 600,
                  display: 'flex',
                  alignItems: 'center',
                  gap: 2,
                }}
              >
                <Zap size={9} /> {skills.length} Kỹ Năng
              </span>
            )}
          </div>
          <span
            style={{
              fontSize: 10,
              color: '#94a3b8',
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
            }}
          >
            💼 {charOccupation} • 🏛️ {charFaction} • 🧠 {charPersonality}
          </span>
        </div>
      </div>

      <button
        onClick={onOpenEditModal}
        title="Chỉnh sửa hồ sơ thông tin chi tiết, kỹ năng & thuộc tính"
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 4,
          padding: '4px 10px',
          borderRadius: 5,
          background: 'rgba(56, 189, 248, 0.15)',
          border: '1px solid rgba(56, 189, 248, 0.3)',
          color: '#38bdf8',
          fontSize: 11,
          fontWeight: 600,
          cursor: 'pointer',
          flexShrink: 0,
        }}
      >
        <Edit3 size={12} /> Sửa Hồ Sơ & Kỹ Năng
      </button>
    </div>
  );
};
