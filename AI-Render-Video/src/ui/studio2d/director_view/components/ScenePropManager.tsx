import React, { useState } from 'react';
import {
  Trees,
  Plus,
  Trash2,
  Copy,
  Layers,
  Upload,
  Move,
  Sliders,
  Eye,
  EyeOff,
  Sparkles,
  ChevronUp,
  ChevronDown,
  SunMedium,
  Zap,
} from 'lucide-react';
import {
  ScenePropItem,
  Director2DProject,
} from '../../../../types/studio2d_director';

interface ScenePropManagerProps {
  props: ScenePropItem[];
  selectedPropId: string | null;
  onSelectProp: (propId: string) => void;
  onUpdateProps: (props: ScenePropItem[]) => void;
}

/**
 * High-quality SVG Generators for Scene Props (Bamboo tree, Peach blossom, Rocks, Lantern, Foreground branch, VFX)
 */
export function generatePresetPropSvg(type: string): string {
  let svg = '';
  switch (type) {
    case 'tree_bamboo':
      svg = `
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 360" width="160" height="360">
          <g stroke="#042f2e" stroke-linecap="round">
            <line x1="80" y1="360" x2="80" y2="40" stroke-width="12"/>
            <line x1="75" y1="360" x2="60" y2="100" stroke-width="8"/>
            <line x1="85" y1="360" x2="105" y2="80" stroke-width="9"/>
          </g>
          <!-- Bamboo Knots -->
          <line x1="70" y1="300" x2="90" y2="300" stroke="#065f46" stroke-width="4"/>
          <line x1="72" y1="240" x2="88" y2="240" stroke="#065f46" stroke-width="4"/>
          <line x1="74" y1="170" x2="86" y2="170" stroke="#065f46" stroke-width="4"/>
          <line x1="75" y1="100" x2="85" y2="100" stroke="#065f46" stroke-width="4"/>
          <!-- Lush Foliage Leaves -->
          <path d="M80 120 Q130 90 155 120 Q125 135 80 120 Z" fill="#059669"/>
          <path d="M80 160 Q30 130 5 160 Q35 175 80 160 Z" fill="#047857"/>
          <path d="M80 80 Q130 50 150 75 Q120 95 80 80 Z" fill="#10b981"/>
          <path d="M80 220 Q20 200 0 230 Q40 240 80 220 Z" fill="#065f46"/>
          <path d="M80 40 Q80 0 100 20 Q90 40 80 40 Z" fill="#34d399"/>
        </svg>
      `.trim();
      break;

    case 'tree_peach':
      svg = `
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 240 320" width="240" height="320">
          <!-- Gnarled Bonsai Ancient Trunk -->
          <path d="M120 320 Q100 240 140 180 T90 80 Q110 40 120 20" stroke="#451a03" stroke-width="18" fill="none" stroke-linecap="round"/>
          <path d="M140 180 Q200 150 220 120" stroke="#451a03" stroke-width="10" fill="none" stroke-linecap="round"/>
          <path d="M110 130 Q40 110 20 80" stroke="#451a03" stroke-width="9" fill="none" stroke-linecap="round"/>
          <!-- Pink Peach Blossom Clouds -->
          <circle cx="120" cy="50" r="45" fill="#f472b6" opacity="0.85"/>
          <circle cx="95" cy="40" r="35" fill="#fb7185" opacity="0.8"/>
          <circle cx="145" cy="45" r="35" fill="#fbcfe8" opacity="0.9"/>
          <circle cx="210" cy="110" r="32" fill="#f472b6" opacity="0.85"/>
          <circle cx="35" cy="80" r="30" fill="#fbcfe8" opacity="0.85"/>
          <!-- Falling Petals -->
          <circle cx="80" cy="180" r="4" fill="#fb7185"/>
          <circle cx="160" cy="220" r="3.5" fill="#f472b6"/>
          <circle cx="190" cy="260" r="4" fill="#fbcfe8"/>
        </svg>
      `.trim();
      break;

    case 'rock_mystic':
      svg = `
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 180 140" width="180" height="140">
          <defs>
            <linearGradient id="rockGrad" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#334155"/>
              <stop offset="60%" stop-color="#1e293b"/>
              <stop offset="100%" stop-color="#090d16"/>
            </linearGradient>
          </defs>
          <ellipse cx="90" cy="130" rx="80" ry="10" fill="rgba(0,0,0,0.5)"/>
          <path d="M20 130 L40 60 L85 20 L130 40 L165 90 L150 130 Z" fill="url(#rockGrad)" stroke="#475569" stroke-width="2"/>
          <path d="M40 60 L90 80 L130 40" stroke="#64748b" stroke-width="1.5" fill="none"/>
          <path d="M90 80 L110 130" stroke="#64748b" stroke-width="1.5" fill="none"/>
          <!-- Moss Accent -->
          <ellipse cx="70" cy="40" rx="15" ry="5" fill="#15803d" opacity="0.7"/>
          <ellipse cx="140" cy="85" rx="12" ry="4" fill="#166534" opacity="0.7"/>
        </svg>
      `.trim();
      break;

    case 'lantern_glowing':
      svg = `
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 180" width="100" height="180">
          <defs>
            <radialGradient id="lanternAura" cx="50%" cy="50%" r="50%">
              <stop offset="0%" stop-color="#f59e0b" stop-opacity="0.9"/>
              <stop offset="50%" stop-color="#f59e0b" stop-opacity="0.3"/>
              <stop offset="100%" stop-color="#f59e0b" stop-opacity="0"/>
            </radialGradient>
          </defs>
          <!-- Aura Glow -->
          <circle cx="50" cy="90" r="48" fill="url(#lanternAura)"/>
          <!-- String -->
          <line x1="50" y1="0" x2="50" y2="40" stroke="#78350f" stroke-width="2.5"/>
          <!-- Roof Cap -->
          <path d="M25 45 Q50 35 75 45 L70 52 L30 52 Z" fill="#451a03" stroke="#78350f"/>
          <!-- Lantern Body -->
          <ellipse cx="50" cy="90" rx="28" ry="36" fill="#dc2626" stroke="#991b1b" stroke-width="2"/>
          <ellipse cx="50" cy="90" rx="14" ry="36" fill="#ef4444" stroke="#b91c1c" stroke-width="1.5"/>
          <rect x="44" y="80" width="12" height="20" fill="#fef08a" rx="2"/>
          <!-- Bottom Fringe -->
          <rect x="35" y="126" width="30" height="8" fill="#451a03"/>
          <line x1="50" y1="134" x2="50" y2="175" stroke="#dc2626" stroke-width="3" stroke-linecap="round"/>
        </svg>
      `.trim();
      break;

    case 'branch_foreground':
      svg = `
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 320 180" width="320" height="180">
          <!-- Overhanging Foreground Tree Branch (High Depth of Field) -->
          <path d="M320 0 Q200 40 120 20 T0 80" stroke="#1c1917" stroke-width="14" fill="none" stroke-linecap="round"/>
          <path d="M180 30 Q140 90 90 120" stroke="#1c1917" stroke-width="8" fill="none" stroke-linecap="round"/>
          <!-- Leaves Cluster -->
          <ellipse cx="60" cy="90" rx="35" ry="20" fill="#064e3b" opacity="0.95"/>
          <ellipse cx="110" cy="115" rx="30" ry="18" fill="#047857" opacity="0.9"/>
          <ellipse cx="140" cy="80" rx="25" ry="15" fill="#059669" opacity="0.85"/>
          <ellipse cx="230" cy="40" rx="40" ry="22" fill="#064e3b" opacity="0.9"/>
        </svg>
      `.trim();
      break;

    case 'vfx_energy_slash':
      svg = `
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200" width="200" height="200">
          <defs>
            <radialGradient id="slashGlow" cx="50%" cy="50%" r="50%">
              <stop offset="0%" stop-color="#38bdf8" stop-opacity="0.9"/>
              <stop offset="50%" stop-color="#0284c7" stop-opacity="0.4"/>
              <stop offset="100%" stop-color="#0284c7" stop-opacity="0"/>
            </radialGradient>
          </defs>
          <circle cx="100" cy="100" r="90" fill="url(#slashGlow)"/>
          <!-- Curved Energy Blade Arc -->
          <path d="M20 180 Q100 20 180 30 Q100 60 20 180 Z" fill="#67e8f9" filter="drop-shadow(0 0 10px #38bdf8)"/>
          <path d="M35 165 Q100 35 170 42 Q105 70 35 165 Z" fill="#ffffff"/>
        </svg>
      `.trim();
      break;

    default:
      svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><circle cx="50" cy="50" r="40" fill="#38bdf8"/></svg>`;
  }
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
}

const PRESET_PROPS: { type: string; name: string; category: ScenePropItem['category']; icon: string; defaultScale: [number, number]; defaultZ: number; defaultParallax: number }[] = [
  { type: 'tree_bamboo', name: 'Rặng Trúc Cổ Thụ', category: 'tree', icon: '🎋', defaultScale: [1.2, 1.2], defaultZ: 6, defaultParallax: 0.45 },
  { type: 'tree_peach', name: 'Cây Đào Tiên Hoa Nở', category: 'tree', icon: '🌸', defaultScale: [1.3, 1.3], defaultZ: 7, defaultParallax: 0.5 },
  { type: 'rock_mystic', name: 'Khối Kỳ Thạch Phong Vân', category: 'rock', icon: '🪨', defaultScale: [1.0, 1.0], defaultZ: 8, defaultParallax: 0.6 },
  { type: 'lantern_glowing', name: 'Đèn Lồng Cổ Trang', category: 'item', icon: '🏮', defaultScale: [0.8, 0.8], defaultZ: 12, defaultParallax: 0.9 },
  { type: 'branch_foreground', name: 'Cành Lá Tiền Cảnh (Đè Trước)', category: 'foreground', icon: '🌿', defaultScale: [1.6, 1.6], defaultZ: 25, defaultParallax: 1.4 },
  { type: 'vfx_energy_slash', name: 'Kiếm Khí Lam Quang (VFX)', category: 'vfx', icon: '⚡', defaultScale: [1.1, 1.1], defaultZ: 22, defaultParallax: 1.0 },
];

export const ScenePropManager: React.FC<ScenePropManagerProps> = ({
  props = [],
  selectedPropId,
  onSelectProp,
  onUpdateProps,
}) => {
  const selectedProp = props.find((p) => p.id === selectedPropId) || props[0];

  const handleAddPreset = (preset: (typeof PRESET_PROPS)[0]) => {
    const newId = `prop_${Date.now().toString().slice(-4)}`;
    const newProp: ScenePropItem = {
      id: newId,
      name: `${preset.name} #${props.length + 1}`,
      category: preset.category,
      path: generatePresetPropSvg(preset.type),
      position: [Math.round((Math.random() - 0.5) * 400), 20],
      scale: preset.defaultScale,
      rotation: 0,
      zIndex: preset.defaultZ,
      opacity: 1.0,
      visible: true,
      parallaxFactor: preset.defaultParallax,
      blendMode: preset.category === 'vfx' ? 'screen' : 'normal',
    };
    onUpdateProps([...props, newProp]);
    onSelectProp(newId);
  };

  const handleUploadCustomProp = (file: File) => {
    const reader = new FileReader();
    reader.onload = (e) => {
      const dataUrl = e.target?.result as string;
      if (dataUrl) {
        const isGif = file.type.includes('gif') || file.name.toLowerCase().endsWith('.gif');
        const newId = `custom_prop_${Date.now().toString().slice(-4)}`;
        const newProp: ScenePropItem = {
          id: newId,
          name: file.name.replace(/\.[^/.]+$/, '').slice(0, 18),
          category: isGif ? 'vfx' : 'custom',
          path: dataUrl,
          position: [0, 0],
          scale: [1.0, 1.0],
          rotation: 0,
          zIndex: 12,
          opacity: 1.0,
          visible: true,
          parallaxFactor: 0.8,
          blendMode: 'normal',
        };
        onUpdateProps([...props, newProp]);
        onSelectProp(newId);
      }
    };
    reader.readAsDataURL(file);
  };

  const handleDeleteProp = (id: string) => {
    const filtered = props.filter((p) => p.id !== id);
    onUpdateProps(filtered);
    if (selectedPropId === id && filtered.length > 0) {
      onSelectProp(filtered[0].id);
    }
  };

  const handleDuplicateProp = (p: ScenePropItem) => {
    const newId = `prop_${Date.now().toString().slice(-4)}`;
    const copy: ScenePropItem = {
      ...p,
      id: newId,
      name: `${p.name} (Bản sao)`,
      position: [p.position[0] + 30, p.position[1] + 20],
    };
    onUpdateProps([...props, copy]);
    onSelectProp(newId);
  };

  const handleUpdateSelectedProp = (updates: Partial<ScenePropItem>) => {
    if (!selectedProp) return;
    const updated = props.map((p) => (p.id === selectedProp.id ? { ...p, ...updates } : p));
    onUpdateProps(updated);
  };

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        background: 'rgba(15, 23, 42, 0.85)',
        border: '1px solid rgba(255, 255, 255, 0.08)',
        borderRadius: 8,
        padding: 12,
        maxHeight: '100%',
        overflowY: 'auto',
      }}
    >
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Trees size={13} /> GHÉP CÂY CỐI & ĐẠO CỤ BỐI CẢNH
        </div>
      </div>

      {/* 1-Click Preset Props Library */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
        <span style={{ fontSize: 9.5, color: '#94a3b8', fontWeight: 600 }}>Thêm nhanh đạo cụ phong cảnh tiên hiệp:</span>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 4 }}>
          {PRESET_PROPS.map((preset) => (
            <button
              key={preset.type}
              onClick={() => handleAddPreset(preset)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '5px 4px',
                borderRadius: 4,
                background: 'rgba(255,255,255,0.04)',
                border: '1px solid rgba(255,255,255,0.08)',
                color: '#e2e8f0',
                fontSize: 9,
                fontWeight: 600,
                cursor: 'pointer',
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}
            >
              <span>{preset.icon}</span>
              <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>{preset.name.split(' ')[0]}</span>
            </button>
          ))}
        </div>
      </div>

      {/* Upload Custom Image / GIF Button */}
      <label
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 6,
          padding: '6px 10px',
          borderRadius: 6,
          background: 'linear-gradient(135deg, rgba(2, 132, 199, 0.3), rgba(56, 189, 248, 0.2))',
          border: '1px solid #38bdf8',
          color: '#38bdf8',
          fontSize: 10,
          fontWeight: 700,
          cursor: 'pointer',
          textAlign: 'center',
        }}
      >
        <Upload size={12} /> ➕ Tải Ảnh PNG / GIF Riêng Vào Cảnh
        <input
          type="file"
          accept="image/*"
          style={{ display: 'none' }}
          onChange={(e) => {
            const f = e.target.files?.[0];
            if (f) handleUploadCustomProp(f);
            e.target.value = '';
          }}
        />
      </label>

      {/* Active Scene Props List */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4, maxHeight: 150, overflowY: 'auto' }}>
        <span style={{ fontSize: 9.5, color: '#94a3b8', fontWeight: 600 }}>
          Đạo cụ đang có trên sân khấu ({props.length}):
        </span>
        {props.length === 0 ? (
          <div style={{ padding: 10, textAlign: 'center', color: '#64748b', fontSize: 9.5, background: 'rgba(2,6,23,0.5)', borderRadius: 4 }}>
            Chưa có cây cối hay đạo cụ nào. Bấm vào các nút mẫu ở trên để thêm vào cảnh!
          </div>
        ) : (
          props
            .slice()
            .reverse()
            .map((item) => {
              const isSelected = item.id === selectedPropId;
              const isForeground = item.zIndex > 15;

              return (
                <div
                  key={item.id}
                  onClick={() => onSelectProp(item.id)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '5px 8px',
                    borderRadius: 4,
                    background: isSelected ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255, 255, 255, 0.03)',
                    border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.06)',
                    cursor: 'pointer',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <span style={{ fontSize: 11 }}>
                      {item.category === 'tree' ? '🎋' : item.category === 'rock' ? '🪨' : item.category === 'vfx' ? '✨' : '🌿'}
                    </span>
                    <span style={{ fontSize: 10, fontWeight: isSelected ? 700 : 500, color: isSelected ? '#38bdf8' : '#e2e8f0' }}>
                      {item.name}
                    </span>
                    {isForeground && (
                      <span style={{ fontSize: 7.5, background: '#ca8a04', color: '#000', padding: '1px 3px', borderRadius: 2, fontWeight: 700 }}>
                        Tiền Cảnh
                      </span>
                    )}
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: 3 }}>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        handleDuplicateProp(item);
                      }}
                      title="Nhân bản đạo cụ này"
                      style={{ background: 'none', border: 'none', color: '#94a3b8', cursor: 'pointer', padding: 2 }}
                    >
                      <Copy size={11} />
                    </button>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        handleUpdateSelectedProp({ visible: !item.visible });
                      }}
                      style={{ background: 'none', border: 'none', color: item.visible ? '#4ade80' : '#64748b', cursor: 'pointer', padding: 2 }}
                    >
                      {item.visible ? <Eye size={11} /> : <EyeOff size={11} />}
                    </button>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        handleDeleteProp(item.id);
                      }}
                      style={{ background: 'none', border: 'none', color: '#ef4444', cursor: 'pointer', padding: 2 }}
                    >
                      <Trash2 size={11} />
                    </button>
                  </div>
                </div>
              );
            })
        )}
      </div>

      {/* Selected Prop Deep Inspector & Controls */}
      {selectedProp && (
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: 8,
            background: 'rgba(2, 6, 23, 0.65)',
            padding: 10,
            borderRadius: 6,
            border: '1px solid rgba(255, 255, 255, 0.08)',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 10, fontWeight: 700, color: '#4ade80' }}>
              🔧 TINH CHỈNH: {selectedProp.name}
            </span>
          </div>

          {/* Scale Slider (Thu nhỏ / Phóng to) */}
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
              <span style={{ fontSize: 9, color: '#94a3b8' }}>Thu nhỏ / Phóng to (Scale):</span>
              <span style={{ fontSize: 9.5, color: '#38bdf8', fontWeight: 700 }}>{selectedProp.scale[0].toFixed(2)}x</span>
            </div>
            <input
              type="range"
              min="0.15"
              max="3.5"
              step="0.05"
              value={selectedProp.scale[0]}
              onChange={(e) => {
                const s = parseFloat(e.target.value);
                handleUpdateSelectedProp({ scale: [s, s] });
              }}
              style={{ width: '100%', accentColor: '#38bdf8' }}
            />
          </div>

          {/* Position X, Y Sliders */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
            <div>
              <span style={{ fontSize: 9, color: '#94a3b8' }}>Vị trí X ({selectedProp.position[0]}px):</span>
              <input
                type="range"
                min="-500"
                max="500"
                value={selectedProp.position[0]}
                onChange={(e) => handleUpdateSelectedProp({ position: [parseInt(e.target.value), selectedProp.position[1]] })}
                style={{ width: '100%', accentColor: '#38bdf8' }}
              />
            </div>
            <div>
              <span style={{ fontSize: 9, color: '#94a3b8' }}>Vị trí Y ({selectedProp.position[1]}px):</span>
              <input
                type="range"
                min="-300"
                max="300"
                value={selectedProp.position[1]}
                onChange={(e) => handleUpdateSelectedProp({ position: [selectedProp.position[0], parseInt(e.target.value)] })}
                style={{ width: '100%', accentColor: '#38bdf8' }}
              />
            </div>
          </div>

          {/* Rotation & Flip Horizontal */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
            <div>
              <span style={{ fontSize: 9, color: '#94a3b8' }}>Xoay góc ({selectedProp.rotation}°):</span>
              <input
                type="range"
                min="-180"
                max="180"
                value={selectedProp.rotation}
                onChange={(e) => handleUpdateSelectedProp({ rotation: parseInt(e.target.value) })}
                style={{ width: '100%', accentColor: '#eab308' }}
              />
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, paddingTop: 14 }}>
              <input
                type="checkbox"
                checked={selectedProp.flipX || false}
                onChange={(e) => handleUpdateSelectedProp({ flipX: e.target.checked })}
                style={{ cursor: 'pointer', accentColor: '#38bdf8' }}
              />
              <span style={{ fontSize: 9.5, color: '#cbd5e1' }}>Lật ngang (Flip)</span>
            </div>
          </div>

          {/* Layer Stacking Order (Z-Index) & Parallax Depth */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
            <div>
              <span style={{ fontSize: 9, color: '#94a3b8' }}>Đè lớp (Z-Index: {selectedProp.zIndex}):</span>
              <div style={{ display: 'flex', gap: 3, marginTop: 2 }}>
                <button
                  onClick={() => handleUpdateSelectedProp({ zIndex: 5 })}
                  style={{
                    flex: 1,
                    padding: '3px 2px',
                    fontSize: 8.5,
                    fontWeight: selectedProp.zIndex <= 10 ? 700 : 500,
                    background: selectedProp.zIndex <= 10 ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.04)',
                    border: selectedProp.zIndex <= 10 ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
                    color: selectedProp.zIndex <= 10 ? '#38bdf8' : '#94a3b8',
                    borderRadius: 3,
                    cursor: 'pointer',
                  }}
                >
                  Sau nhân vật
                </button>
                <button
                  onClick={() => handleUpdateSelectedProp({ zIndex: 25 })}
                  style={{
                    flex: 1,
                    padding: '3px 2px',
                    fontSize: 8.5,
                    fontWeight: selectedProp.zIndex > 15 ? 700 : 500,
                    background: selectedProp.zIndex > 15 ? 'rgba(234, 179, 8, 0.25)' : 'rgba(255,255,255,0.04)',
                    border: selectedProp.zIndex > 15 ? '1px solid #facc15' : '1px solid rgba(255,255,255,0.06)',
                    color: selectedProp.zIndex > 15 ? '#facc15' : '#94a3b8',
                    borderRadius: 3,
                    cursor: 'pointer',
                  }}
                >
                  Tiền cảnh đè trước
                </button>
              </div>
            </div>

            <div>
              <span style={{ fontSize: 9, color: '#94a3b8' }}>Hòa trộn (Blend Mode):</span>
              <select
                value={selectedProp.blendMode || 'normal'}
                onChange={(e) => handleUpdateSelectedProp({ blendMode: e.target.value as any })}
                style={{ width: '100%', padding: '3px 4px', fontSize: 9.5, background: '#090d16', border: '1px solid #334155', color: '#38bdf8', borderRadius: 4, marginTop: 2 }}
              >
                <option value="normal">Bình thường (Normal)</option>
                <option value="screen">Phát sáng (Screen - VFX)</option>
                <option value="multiply">Bóng đổ (Multiply)</option>
                <option value="lighter">Rực lửa (Lighter)</option>
              </select>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
