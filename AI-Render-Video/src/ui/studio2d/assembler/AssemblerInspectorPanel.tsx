import React from 'react';
import {
  Sliders,
  RotateCcw,
  Play,
  Pause,
  Sparkles,
  Save,
  Compass,
  Check,
} from 'lucide-react';
import {
  Character2DAssembly,
  Character2DPartType,
  Character2DPartConfig,
  Character2DAngle,
} from '../../../types/scene2d';
import { PART_HIERARCHY_CONFIG, generateDemoPartSvg } from '../../../core/assets/Asset2DRegistry';

interface AssemblerInspectorPanelProps {
  assembly: Character2DAssembly;
  selectedSlot: Character2DPartType;
  onChangeAssembly: (updated: Character2DAssembly) => void;
  animMode: 'idle' | 'breathe' | 'talk' | 'combat_slash';
  setAnimMode: (m: 'idle' | 'breathe' | 'talk' | 'combat_slash') => void;
  isPlaying: boolean;
  setIsPlaying: (p: boolean | ((prev: boolean) => boolean)) => void;
  isBlinking: boolean;
  setIsBlinking: (b: boolean | ((prev: boolean) => boolean)) => void;
  isTalking: boolean;
  setIsTalking: (t: boolean | ((prev: boolean) => boolean)) => void;
  isSaving: boolean;
  onSaveCharacter: () => void;
}

export const AssemblerInspectorPanel: React.FC<AssemblerInspectorPanelProps> = ({
  assembly,
  selectedSlot,
  onChangeAssembly,
  animMode,
  setAnimMode,
  isPlaying,
  setIsPlaying,
  isBlinking,
  setIsBlinking,
  isTalking,
  setIsTalking,
  isSaving,
  onSaveCharacter,
}) => {
  const selectedPart = assembly.parts[selectedSlot] || {
    path: '',
    offset: PART_HIERARCHY_CONFIG[selectedSlot]?.defaultOffset || [0, 0],
    scale: [1, 1],
    rotation: 0,
    pivot: PART_HIERARCHY_CONFIG[selectedSlot]?.defaultPivot || [0.5, 0.5],
    flipX: false,
    flipY: false,
    z_index: PART_HIERARCHY_CONFIG[selectedSlot]?.defaultZ || 5,
    z_depth_3d: PART_HIERARCHY_CONFIG[selectedSlot]?.defaultZDepth3D || 0,
    opacity: 1,
  };

  const updatePartConfig = (updates: Partial<Character2DPartConfig>) => {
    onChangeAssembly({
      ...assembly,
      parts: {
        ...assembly.parts,
        [selectedSlot]: {
          ...selectedPart,
          ...updates,
        },
      },
    });
  };

  const handleResetSlot = () => {
    const hierarchy = PART_HIERARCHY_CONFIG[selectedSlot];
    const defSvg = generateDemoPartSvg(selectedSlot, assembly.gender || 'nam', 'front');
    updatePartConfig({
      path: defSvg,
      offset: hierarchy?.defaultOffset || [0, 0],
      scale: [1, 1],
      rotation: selectedSlot === 'canh_tay_trai' ? 12 : selectedSlot === 'canh_tay_phai' ? -18 : selectedSlot === 'vu_khi' ? -15 : 0,
      flipX: false,
      z_depth_3d: hierarchy?.defaultZDepth3D || 0,
      z_index: hierarchy?.defaultZ || 5,
    });
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
      {/* 1. Animation Player Toolbar */}
      <div style={{ background: 'rgba(0,0,0,0.35)', padding: 8, borderRadius: 6, display: 'flex', flexDirection: 'column', gap: 6 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ fontSize: 10, fontWeight: 700, color: '#38bdf8' }}>
            🎬 DIỄN HOẠT & THỬ NGHIỆM ĐỘNG HỌC
          </div>

          <button
            onClick={() => setIsPlaying((p) => !p)}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '3px 8px',
              fontSize: 10,
              fontWeight: 700,
              borderRadius: 4,
              background: isPlaying ? 'rgba(239, 68, 68, 0.2)' : 'rgba(34, 197, 94, 0.2)',
              color: isPlaying ? '#ef4444' : '#22c55e',
              border: isPlaying ? '1px solid rgba(239,68,68,0.4)' : '1px solid rgba(34,197,94,0.4)',
              cursor: 'pointer',
            }}
          >
            {isPlaying ? <Pause size={10} /> : <Play size={10} />}
            {isPlaying ? 'Tạm Dừng' : 'Chạy Diễn Hoạt'}
          </button>
        </div>

        {/* Animation Mode Pills */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 4 }}>
          {[
            { id: 'breathe' as const, label: 'Thở / Nhịp' },
            { id: 'talk' as const, label: 'Nói Thoại' },
            { id: 'combat_slash' as const, label: 'Chém Kiếm' },
            { id: 'idle' as const, label: 'Đứng Yên' },
          ].map((mode) => (
            <button
              key={mode.id}
              onClick={() => setAnimMode(mode.id)}
              style={{
                padding: '4px',
                fontSize: 9.5,
                fontWeight: animMode === mode.id ? 700 : 500,
                borderRadius: 4,
                border: animMode === mode.id ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
                background: animMode === mode.id ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.02)',
                color: animMode === mode.id ? '#38bdf8' : '#94a3b8',
                cursor: 'pointer',
              }}
            >
              {mode.label}
            </button>
          ))}
        </div>
      </div>

      {/* 2. Sliders Grid for Active Slot */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ fontSize: 10.5, fontWeight: 700, color: '#38bdf8' }}>
            TINH CHỈNH CẤU TRÚC: [{selectedSlot.toUpperCase()}]
          </span>
          <button
            onClick={handleResetSlot}
            style={{
              padding: '2px 6px',
              fontSize: 9,
              borderRadius: 3,
              background: 'rgba(255,255,255,0.05)',
              color: '#94a3b8',
              border: '1px solid rgba(255,255,255,0.1)',
              cursor: 'pointer',
            }}
          >
            <RotateCcw size={9} /> Khôi phục
          </button>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          {/* Offset Y */}
          <div style={{ background: 'rgba(0,0,0,0.3)', padding: 5, borderRadius: 4 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#e2e8f0', marginBottom: 2 }}>
              <span>Độ Cao (Offset Y):</span>
              <span style={{ color: '#38bdf8', fontWeight: 700 }}>{selectedPart.offset[1]}px</span>
            </div>
            <input
              type="range"
              min="-250"
              max="250"
              value={selectedPart.offset[1]}
              onChange={(e) => updatePartConfig({ offset: [selectedPart.offset[0], parseInt(e.target.value)] })}
              style={{ width: '100%' }}
            />
          </div>

          {/* Offset X */}
          <div style={{ background: 'rgba(0,0,0,0.3)', padding: 5, borderRadius: 4 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#e2e8f0', marginBottom: 2 }}>
              <span>Lệch Ngang (Offset X):</span>
              <span style={{ color: '#38bdf8', fontWeight: 700 }}>{selectedPart.offset[0]}px</span>
            </div>
            <input
              type="range"
              min="-150"
              max="150"
              value={selectedPart.offset[0]}
              onChange={(e) => updatePartConfig({ offset: [parseInt(e.target.value), selectedPart.offset[1]] })}
              style={{ width: '100%' }}
            />
          </div>

          {/* Scale X */}
          <div style={{ background: 'rgba(0,0,0,0.3)', padding: 5, borderRadius: 4 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#e2e8f0', marginBottom: 2 }}>
              <span>Phồng Ngang (Scale X):</span>
              <span style={{ color: '#4ade80', fontWeight: 700 }}>{Math.round(selectedPart.scale[0] * 100)}%</span>
            </div>
            <input
              type="range"
              min="50"
              max="180"
              value={Math.round(selectedPart.scale[0] * 100)}
              onChange={(e) => updatePartConfig({ scale: [parseInt(e.target.value) / 100, selectedPart.scale[1]] })}
              style={{ width: '100%' }}
            />
          </div>

          {/* Scale Y */}
          <div style={{ background: 'rgba(0,0,0,0.3)', padding: 5, borderRadius: 4 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#e2e8f0', marginBottom: 2 }}>
              <span>Độ Dài (Scale Y):</span>
              <span style={{ color: '#4ade80', fontWeight: 700 }}>{Math.round(selectedPart.scale[1] * 100)}%</span>
            </div>
            <input
              type="range"
              min="50"
              max="180"
              value={Math.round(selectedPart.scale[1] * 100)}
              onChange={(e) => updatePartConfig({ scale: [selectedPart.scale[0], parseInt(e.target.value) / 100] })}
              style={{ width: '100%' }}
            />
          </div>

          {/* Z-Index */}
          <div style={{ background: 'rgba(0,0,0,0.3)', padding: 5, borderRadius: 4 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#e2e8f0', marginBottom: 2 }}>
              <span>Thứ Tự Lớp 2D (Z-Index):</span>
              <span style={{ color: '#f59e0b', fontWeight: 700 }}>Lớp {selectedPart.z_index}</span>
            </div>
            <input
              type="range"
              min="1"
              max="10"
              value={selectedPart.z_index}
              onChange={(e) => updatePartConfig({ z_index: parseInt(e.target.value) })}
              style={{ width: '100%' }}
            />
          </div>

          {/* Z-Depth 3D */}
          <div style={{ background: 'rgba(0,0,0,0.3)', padding: 5, borderRadius: 4 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#e2e8f0', marginBottom: 2 }}>
              <span>Độ Sâu 3D (Z-Depth):</span>
              <span style={{ color: '#ec4899', fontWeight: 700 }}>{(selectedPart.z_depth_3d || 0).toFixed(3)}m</span>
            </div>
            <input
              type="range"
              min="-80"
              max="80"
              value={Math.round((selectedPart.z_depth_3d || 0) * 1000)}
              onChange={(e) => updatePartConfig({ z_depth_3d: parseInt(e.target.value) / 1000 })}
              style={{ width: '100%' }}
            />
          </div>
        </div>
      </div>

      {/* Save Button */}
      <button
        onClick={onSaveCharacter}
        disabled={isSaving}
        style={{
          marginTop: 'auto',
          padding: '8px 12px',
          fontSize: 11,
          fontWeight: 700,
          borderRadius: 6,
          background: 'linear-gradient(135deg, #10b981, #059669)',
          color: '#fff',
          border: 'none',
          cursor: isSaving ? 'wait' : 'pointer',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 6,
          boxShadow: '0 4px 12px rgba(16, 185, 129, 0.3)',
        }}
      >
        <Save size={13} /> {isSaving ? 'Đang lưu...' : 'LƯU NHÂN VẬT VÀO DỰ ÁN'}
      </button>
    </div>
  );
};
