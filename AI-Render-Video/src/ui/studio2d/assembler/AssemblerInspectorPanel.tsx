import React, { useState } from 'react';
import {
  Sliders,
  RotateCcw,
  Play,
  Pause,
  Save,
  ChevronDown,
  ChevronUp,
} from 'lucide-react';
import {
  Character2DAssembly,
  Character2DPartType,
  Character2DPartConfig,
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
  isSaving,
  onSaveCharacter,
}) => {
  const [isStructureOpen, setIsStructureOpen] = useState<boolean>(true);

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
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        background: '#0a0f1d',
        padding: 8,
        borderRadius: 8,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        flexShrink: 0,
      }}
    >
      {/* 1. Diễn Hoạt & Thử Nghiệm (Compact Auto-fit Height) */}
      <div style={{ background: 'rgba(0,0,0,0.35)', padding: '6px 8px', borderRadius: 6, display: 'flex', flexDirection: 'column', gap: 4 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ fontSize: 10, fontWeight: 700, color: '#38bdf8' }}>
            🎬 DIỄN HOẠT & THỬ NGHIỆM ĐỘNG HỌC
          </span>

          <button
            onClick={() => setIsPlaying((p) => !p)}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 3,
              padding: '2px 7px',
              fontSize: 9.5,
              fontWeight: 700,
              borderRadius: 4,
              background: isPlaying ? 'rgba(239, 68, 68, 0.2)' : 'rgba(34, 197, 94, 0.2)',
              color: isPlaying ? '#ef4444' : '#22c55e',
              border: isPlaying ? '1px solid rgba(239,68,68,0.4)' : '1px solid rgba(34,197,94,0.4)',
              cursor: 'pointer',
            }}
          >
            {isPlaying ? <Pause size={9} /> : <Play size={9} />}
            {isPlaying ? 'Tạm Dừng' : 'Chạy Diễn Hoạt'}
          </button>
        </div>

        {/* Animation Mode Buttons in 1 compact row */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 4 }}>
          {[
            { id: 'breathe' as const, label: 'Thở' },
            { id: 'talk' as const, label: 'Thoại' },
            { id: 'combat_slash' as const, label: 'Chém' },
            { id: 'idle' as const, label: 'Tĩnh' },
          ].map((mode) => (
            <button
              key={mode.id}
              onClick={() => setAnimMode(mode.id)}
              style={{
                padding: '3px',
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

      {/* 2. Tinh Chỉnh Cấu Trúc (Compact Toggle Section) */}
      <div style={{ background: 'rgba(0,0,0,0.3)', padding: 6, borderRadius: 6, display: 'flex', flexDirection: 'column', gap: 4 }}>
        <div
          onClick={() => setIsStructureOpen((o) => !o)}
          style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', cursor: 'pointer' }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <Sliders size={11} color="#38bdf8" />
            <span style={{ fontSize: 10, fontWeight: 700, color: '#e2e8f0' }}>
              CẤU TRÚC: [{selectedSlot.toUpperCase()}]
            </span>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <button
              onClick={(e) => {
                e.stopPropagation();
                handleResetSlot();
              }}
              style={{
                padding: '1px 5px',
                fontSize: 8.5,
                borderRadius: 3,
                background: 'rgba(255,255,255,0.05)',
                color: '#94a3b8',
                border: '1px solid rgba(255,255,255,0.1)',
                cursor: 'pointer',
              }}
            >
              <RotateCcw size={8} /> Đặt lại
            </button>
            {isStructureOpen ? <ChevronUp size={12} color="#94a3b8" /> : <ChevronDown size={12} color="#94a3b8" />}
          </div>
        </div>

        {isStructureOpen && (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 4, marginTop: 2 }}>
            {/* Offset Y */}
            <div style={{ background: 'rgba(0,0,0,0.25)', padding: 3, borderRadius: 3 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5, color: '#cbd5e1' }}>
                <span>Dọc (Y):</span>
                <span style={{ color: '#38bdf8', fontWeight: 700 }}>{selectedPart.offset[1]}px</span>
              </div>
              <input
                type="range"
                min="-200"
                max="200"
                value={selectedPart.offset[1]}
                onChange={(e) => updatePartConfig({ offset: [selectedPart.offset[0], parseInt(e.target.value)] })}
                style={{ width: '100%' }}
              />
            </div>

            {/* Offset X */}
            <div style={{ background: 'rgba(0,0,0,0.25)', padding: 3, borderRadius: 3 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5, color: '#cbd5e1' }}>
                <span>Ngang (X):</span>
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
            <div style={{ background: 'rgba(0,0,0,0.25)', padding: 3, borderRadius: 3 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5, color: '#cbd5e1' }}>
                <span>Phồng:</span>
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
            <div style={{ background: 'rgba(0,0,0,0.25)', padding: 3, borderRadius: 3 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5, color: '#cbd5e1' }}>
                <span>Dài:</span>
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
            <div style={{ background: 'rgba(0,0,0,0.25)', padding: 3, borderRadius: 3 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5, color: '#cbd5e1' }}>
                <span>Lớp Z:</span>
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
            <div style={{ background: 'rgba(0,0,0,0.25)', padding: 3, borderRadius: 3 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5, color: '#cbd5e1' }}>
                <span>Z-Index:</span>
                <span style={{ color: '#ec4899', fontWeight: 700 }}>{Math.round(selectedPart.z_depth_3d || 0)}</span>
              </div>
              <input
                type="range"
                min="-60"
                max="60"
                value={Math.round(selectedPart.z_depth_3d || 0)}
                onChange={(e) => updatePartConfig({ z_depth_3d: parseInt(e.target.value, 10) })}
                style={{ width: '100%' }}
              />
            </div>
          </div>
        )}
      </div>

      {/* 3. Action Save Button */}
      <button
        onClick={onSaveCharacter}
        disabled={isSaving}
        style={{
          padding: '6px 10px',
          fontSize: 10.5,
          fontWeight: 700,
          borderRadius: 6,
          background: 'linear-gradient(135deg, #10b981, #059669)',
          color: '#fff',
          border: 'none',
          cursor: isSaving ? 'wait' : 'pointer',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 5,
          boxShadow: '0 3px 10px rgba(16, 185, 129, 0.3)',
        }}
      >
        <Save size={12} /> {isSaving ? 'Đang lưu...' : 'LƯU NHÂN VẬT VÀO DỰ ÁN'}
      </button>
    </div>
  );
};
