import React from 'react';
import { Sliders, RotateCcw, Compass, Check } from 'lucide-react';
import {
  Character2DAssembly,
  Character2DPartType,
  Character2DAngle,
} from '../../../types/scene2d';

interface CharacterStructureTunerPanelProps {
  selectedSlot: Character2DPartType;
  assembly: Character2DAssembly;
  onChangeAssembly: (updated: Character2DAssembly) => void;
  onSaveCompleted: () => void;
}

export const CharacterStructureTunerPanel: React.FC<CharacterStructureTunerPanelProps> = ({
  selectedSlot,
  assembly,
  onChangeAssembly,
  onSaveCompleted,
}) => {
  const partConfig = assembly.parts[selectedSlot] || {
    path: '',
    offset: [0, 0],
    scale: [1, 1],
    rotation: 0,
    pivot: [0.5, 0.5],
    flipX: false,
    flipY: false,
    z_index: 5,
    z_depth_3d: 0,
    opacity: 1,
  };

  const handleUpdate = (key: string, val: unknown) => {
    const nextParts = {
      ...assembly.parts,
      [selectedSlot]: {
        ...partConfig,
        [key]: val,
      },
    };
    onChangeAssembly({
      ...assembly,
      parts: nextParts,
      updated_at: new Date().toISOString(),
    });
  };

  const handleResetSlot = () => {
    handleUpdate('offset', [0, 0]);
    handleUpdate('scale', [1, 1]);
    handleUpdate('rotation', 0);
  };

  return (
    <div
      style={{
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        background: '#0a0f1d',
        padding: 10,
        borderRadius: 8,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        overflowY: 'auto',
      }}
    >
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <Sliders size={14} color="#38bdf8" />
          <span style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8' }}>
            TINH CHỈNH CẤU TRÚC VẬT LIỆU: [{selectedSlot.toUpperCase()}]
          </span>
        </div>

        <button
          onClick={handleResetSlot}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 3,
            padding: '2px 6px',
            fontSize: 9.5,
            borderRadius: 4,
            background: 'rgba(255,255,255,0.05)',
            color: '#94a3b8',
            border: '1px solid rgba(255,255,255,0.1)',
            cursor: 'pointer',
          }}
        >
          <RotateCcw size={10} /> Đặt lại
        </button>
      </div>

      {/* Sliders Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
        {/* Offset Y */}
        <div style={{ background: 'rgba(0,0,0,0.3)', padding: 6, borderRadius: 5 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#e2e8f0', marginBottom: 2 }}>
            <span>Độ Cao (Offset Y):</span>
            <span style={{ color: '#38bdf8', fontWeight: 700 }}>{partConfig.offset[1]}px</span>
          </div>
          <input
            type="range"
            min="-250"
            max="250"
            value={partConfig.offset[1]}
            onChange={(e) => handleUpdate('offset', [partConfig.offset[0], parseInt(e.target.value)])}
            style={{ width: '100%' }}
          />
        </div>

        {/* Offset X */}
        <div style={{ background: 'rgba(0,0,0,0.3)', padding: 6, borderRadius: 5 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#e2e8f0', marginBottom: 2 }}>
            <span>Lệch Ngang (Offset X):</span>
            <span style={{ color: '#38bdf8', fontWeight: 700 }}>{partConfig.offset[0]}px</span>
          </div>
          <input
            type="range"
            min="-150"
            max="150"
            value={partConfig.offset[0]}
            onChange={(e) => handleUpdate('offset', [parseInt(e.target.value), partConfig.offset[1]])}
            style={{ width: '100%' }}
          />
        </div>

        {/* Scale X */}
        <div style={{ background: 'rgba(0,0,0,0.3)', padding: 6, borderRadius: 5 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#e2e8f0', marginBottom: 2 }}>
            <span>Độ Phồng Ngang (Scale X):</span>
            <span style={{ color: '#4ade80', fontWeight: 700 }}>{Math.round(partConfig.scale[0] * 100)}%</span>
          </div>
          <input
            type="range"
            min="50"
            max="180"
            value={Math.round(partConfig.scale[0] * 100)}
            onChange={(e) => handleUpdate('scale', [parseInt(e.target.value) / 100, partConfig.scale[1]])}
            style={{ width: '100%' }}
          />
        </div>

        {/* Scale Y */}
        <div style={{ background: 'rgba(0,0,0,0.3)', padding: 6, borderRadius: 5 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#e2e8f0', marginBottom: 2 }}>
            <span>Độ Dài Dọc (Scale Y):</span>
            <span style={{ color: '#4ade80', fontWeight: 700 }}>{Math.round(partConfig.scale[1] * 100)}%</span>
          </div>
          <input
            type="range"
            min="50"
            max="180"
            value={Math.round(partConfig.scale[1] * 100)}
            onChange={(e) => handleUpdate('scale', [partConfig.scale[0], parseInt(e.target.value) / 100])}
            style={{ width: '100%' }}
          />
        </div>

        {/* Z-Index */}
        <div style={{ background: 'rgba(0,0,0,0.3)', padding: 6, borderRadius: 5 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#e2e8f0', marginBottom: 2 }}>
            <span>Thứ Tự Lớp 2D (Z-Index):</span>
            <span style={{ color: '#f59e0b', fontWeight: 700 }}>Lớp {partConfig.z_index}</span>
          </div>
          <input
            type="range"
            min="1"
            max="10"
            value={partConfig.z_index}
            onChange={(e) => handleUpdate('z_index', parseInt(e.target.value))}
            style={{ width: '100%' }}
          />
        </div>

        {/* Z-Depth 3D */}
        <div style={{ background: 'rgba(0,0,0,0.3)', padding: 6, borderRadius: 5 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#e2e8f0', marginBottom: 2 }}>
            <span>Độ Sâu 3D (Z-Depth):</span>
            <span style={{ color: '#ec4899', fontWeight: 700 }}>{(partConfig.z_depth_3d || 0).toFixed(3)}m</span>
          </div>
          <input
            type="range"
            min="-80"
            max="80"
            value={Math.round((partConfig.z_depth_3d || 0) * 1000)}
            onChange={(e) => handleUpdate('z_depth_3d', parseInt(e.target.value) / 1000)}
            style={{ width: '100%' }}
          />
        </div>
      </div>

      {/* 3D Multi-Angle Inspector Preview for this slot */}
      <div style={{ background: 'rgba(0,0,0,0.25)', padding: 6, borderRadius: 6, border: '1px solid rgba(255,255,255,0.04)' }}>
        <div style={{ fontSize: 9.5, fontWeight: 700, color: '#94a3b8', marginBottom: 4, display: 'flex', alignItems: 'center', gap: 4 }}>
          <Compass size={11} /> CÁC GÓC QUAY 3D CỦA VẬT LIỆU NÀY:
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 4 }}>
          {[
            { angle: 'front' as Character2DAngle, label: '0° Chính diện' },
            { angle: 'three_quarter_left' as Character2DAngle, label: '45° 3/4 Trái' },
            { angle: 'profile_left' as Character2DAngle, label: '90° Ngang' },
            { angle: 'back' as Character2DAngle, label: '180° Sau lưng' },
          ].map((item) => {
            const img = partConfig.angles?.[item.angle] || (item.angle === 'front' ? partConfig.path : undefined);
            return (
              <div
                key={item.angle}
                style={{
                  height: 48,
                  borderRadius: 4,
                  background: '#040711',
                  border: '1px solid rgba(255,255,255,0.06)',
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  justifyContent: 'center',
                  padding: 2,
                }}
              >
                {img ? (
                  <img src={img} alt={item.label} style={{ maxWidth: '90%', maxHeight: '70%', objectFit: 'contain' }} />
                ) : (
                  <span style={{ fontSize: 8, color: '#475569' }}>Chưa có</span>
                )}
                <span style={{ fontSize: 7.5, color: '#94a3b8' }}>{item.label}</span>
              </div>
            );
          })}
        </div>
      </div>

      {/* Save Completed Button */}
      <button
        onClick={onSaveCompleted}
        style={{
          marginTop: 'auto',
          padding: '8px 12px',
          fontSize: 11,
          fontWeight: 700,
          borderRadius: 6,
          background: 'linear-gradient(135deg, #10b981, #059669)',
          color: '#fff',
          border: 'none',
          cursor: 'pointer',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 6,
          boxShadow: '0 4px 12px rgba(16, 185, 129, 0.3)',
        }}
      >
        <Check size={14} /> XÁC NHẬN & RÁP DIỆN MẠO NÀY VÀO NHÂN VẬT
      </button>
    </div>
  );
};
