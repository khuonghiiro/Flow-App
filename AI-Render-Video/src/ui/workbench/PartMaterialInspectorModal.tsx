import React, { useState } from 'react';
import {
  X,
  Palette,
  Sparkles,
  Layers,
  Eye,
  EyeOff,
  RotateCcw,
  Sliders,
  Sun,
  Flame,
  Zap,
} from 'lucide-react';
import { SelectedPartInfo } from './Interactive3DPartSelector';
import {
  MaterialOverrideEngine,
  PRESET_COLOR_PALETTES,
} from '../../core/materials/MaterialOverrideEngine';
import { PartMaterialCustomization } from '../../types/scene';

export interface PartMaterialInspectorModalProps {
  selectedPart: SelectedPartInfo | null;
  onClose: () => void;
  onApplyOverride: (meshKey: string, override: PartMaterialCustomization) => void;
  onResetOverride: (meshKey: string) => void;
}

export const PartMaterialInspectorModal: React.FC<PartMaterialInspectorModalProps> = ({
  selectedPart,
  onClose,
  onApplyOverride,
  onResetOverride,
}) => {
  if (!selectedPart) return null;

  const [color, setColor] = useState(selectedPart.initialColor || '#ffffff');
  const [roughness, setRoughness] = useState(selectedPart.initialRoughness ?? 0.55);
  const [metalness, setMetalness] = useState(selectedPart.initialMetalness ?? 0.05);
  const [emissive, setEmissive] = useState(selectedPart.initialEmissive || '#000000');
  const [emissiveIntensity, setEmissiveIntensity] = useState(
    selectedPart.initialEmissiveIntensity ?? 0.0
  );
  const [isGlowEnabled, setIsGlowEnabled] = useState(emissiveIntensity > 0.1);
  const [wireframe, setWireframe] = useState(selectedPart.initialWireframe ?? false);
  const [visible, setVisible] = useState(selectedPart.initialVisible ?? true);

  // Sync state when selected part changes
  React.useEffect(() => {
    setColor(selectedPart.initialColor || '#ffffff');
    setRoughness(selectedPart.initialRoughness ?? 0.55);
    setMetalness(selectedPart.initialMetalness ?? 0.05);
    setEmissive(selectedPart.initialEmissive || '#000000');
    setEmissiveIntensity(selectedPart.initialEmissiveIntensity ?? 0.0);
    setIsGlowEnabled((selectedPart.initialEmissiveIntensity ?? 0.0) > 0.1);
    setWireframe(selectedPart.initialWireframe ?? false);
    setVisible(selectedPart.initialVisible ?? true);
  }, [selectedPart.meshKey]);

  const handleColorChange = (newColor: string) => {
    setColor(newColor);
    onApplyOverride(selectedPart.meshKey, {
      color: newColor,
      roughness,
      metalness,
      emissive: isGlowEnabled ? emissive : '#000000',
      emissiveIntensity: isGlowEnabled ? emissiveIntensity : 0,
      wireframe,
      visible,
    });
  };

  const handleRoughnessChange = (val: number) => {
    setRoughness(val);
    onApplyOverride(selectedPart.meshKey, {
      color,
      roughness: val,
      metalness,
      emissive: isGlowEnabled ? emissive : '#000000',
      emissiveIntensity: isGlowEnabled ? emissiveIntensity : 0,
      wireframe,
      visible,
    });
  };

  const handleMetalnessChange = (val: number) => {
    setMetalness(val);
    onApplyOverride(selectedPart.meshKey, {
      color,
      roughness,
      metalness: val,
      emissive: isGlowEnabled ? emissive : '#000000',
      emissiveIntensity: isGlowEnabled ? emissiveIntensity : 0,
      wireframe,
      visible,
    });
  };

  const handleGlowToggle = (enabled: boolean) => {
    setIsGlowEnabled(enabled);
    const newIntensity = enabled ? Math.max(0.8, emissiveIntensity) : 0;
    const newEmissive = enabled && emissive === '#000000' ? color : emissive;
    setEmissiveIntensity(newIntensity);
    setEmissive(newEmissive);
    onApplyOverride(selectedPart.meshKey, {
      color,
      roughness,
      metalness,
      emissive: enabled ? newEmissive : '#000000',
      emissiveIntensity: newIntensity,
      wireframe,
      visible,
    });
  };

  const handleEmissiveColorChange = (newEmissive: string) => {
    setEmissive(newEmissive);
    onApplyOverride(selectedPart.meshKey, {
      color,
      roughness,
      metalness,
      emissive: newEmissive,
      emissiveIntensity,
      wireframe,
      visible,
    });
  };

  const handleEmissiveIntensityChange = (val: number) => {
    setEmissiveIntensity(val);
    onApplyOverride(selectedPart.meshKey, {
      color,
      roughness,
      metalness,
      emissive,
      emissiveIntensity: val,
      wireframe,
      visible,
    });
  };

  const handleWireframeToggle = (wf: boolean) => {
    setWireframe(wf);
    onApplyOverride(selectedPart.meshKey, {
      color,
      roughness,
      metalness,
      emissive: isGlowEnabled ? emissive : '#000000',
      emissiveIntensity: isGlowEnabled ? emissiveIntensity : 0,
      wireframe: wf,
      visible,
    });
  };

  const handleVisibilityToggle = () => {
    const nextVis = !visible;
    setVisible(nextVis);
    onApplyOverride(selectedPart.meshKey, {
      color,
      roughness,
      metalness,
      emissive: isGlowEnabled ? emissive : '#000000',
      emissiveIntensity: isGlowEnabled ? emissiveIntensity : 0,
      wireframe,
      visible: nextVis,
    });
  };

  const handleApplyPreset = (preset: typeof PRESET_COLOR_PALETTES[0]) => {
    setColor(preset.color);
    setRoughness(preset.roughness);
    setMetalness(preset.metalness);
    const hasGlow = Boolean(preset.emissive && preset.emissiveIntensity && preset.emissiveIntensity > 0);
    setIsGlowEnabled(hasGlow);
    setEmissive(preset.emissive || '#000000');
    setEmissiveIntensity(preset.emissiveIntensity || 0);

    onApplyOverride(selectedPart.meshKey, {
      color: preset.color,
      roughness: preset.roughness,
      metalness: preset.metalness,
      emissive: preset.emissive || '#000000',
      emissiveIntensity: preset.emissiveIntensity || 0,
      wireframe,
      visible,
    });
  };

  const handleReset = () => {
    setColor('#ffffff');
    setRoughness(0.55);
    setMetalness(0.05);
    setEmissive('#000000');
    setEmissiveIntensity(0);
    setIsGlowEnabled(false);
    setWireframe(false);
    setVisible(true);
    onResetOverride(selectedPart.meshKey);
  };

  return (
    <div
      style={{
        position: 'absolute',
        top: 12,
        right: 12,
        width: 320,
        maxHeight: 'calc(100% - 24px)',
        overflowY: 'auto',
        background: 'rgba(15, 23, 42, 0.94)',
        backdropFilter: 'blur(16px)',
        border: '1px solid rgba(56, 189, 248, 0.35)',
        borderRadius: 12,
        boxShadow: '0 20px 40px -8px rgba(0, 0, 0, 0.75), 0 0 20px rgba(56, 189, 248, 0.15)',
        zIndex: 50,
        display: 'flex',
        flexDirection: 'column',
        gap: 12,
        padding: 14,
        color: '#f8fafc',
        fontFamily: 'Inter, system-ui, sans-serif',
        fontSize: 12,
      }}
    >
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderBottom: '1px solid rgba(255,255,255,0.1)', paddingBottom: 8 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span style={{ fontSize: 16 }}>{selectedPart.categoryIcon}</span>
          <div>
            <div style={{ fontWeight: 700, color: '#38bdf8', fontSize: 13 }}>
              {selectedPart.displayName}
            </div>
            <div style={{ fontSize: 10, color: '#94a3b8' }}>
              Danh mục: {selectedPart.categoryLabel}
            </div>
          </div>
        </div>
        <button
          onClick={onClose}
          style={{
            background: 'transparent',
            border: 'none',
            color: '#94a3b8',
            cursor: 'pointer',
            padding: 4,
            borderRadius: 4,
            display: 'flex',
            alignItems: 'center',
          }}
          title="Đóng bảng chỉnh sửa"
        >
          <X size={16} />
        </button>
      </div>

      {/* Quick Color Palette Presets */}
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 5, color: '#cbd5e1', fontWeight: 600, marginBottom: 6 }}>
          <Palette size={13} style={{ color: '#38bdf8' }} />
          <span>Preset Màu Nhanh</span>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 6 }}>
          {PRESET_COLOR_PALETTES.map((p) => (
            <button
              key={p.id}
              onClick={() => handleApplyPreset(p)}
              style={{
                height: 32,
                borderRadius: 6,
                background: p.previewGradient,
                border: color.toLowerCase() === p.color.toLowerCase() ? '2px solid #38bdf8' : '1px solid rgba(255,255,255,0.15)',
                cursor: 'pointer',
                transition: 'all 0.15s ease',
                boxShadow: color.toLowerCase() === p.color.toLowerCase() ? '0 0 10px rgba(56, 189, 248, 0.6)' : 'none',
              }}
              title={p.name}
            />
          ))}
        </div>
      </div>

      {/* Custom Color Tint Picker */}
      <div>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 4 }}>
          <span style={{ color: '#cbd5e1', fontWeight: 600 }}>Màu Nhuộm (Color Tint)</span>
          <span style={{ color: '#38bdf8', fontFamily: 'monospace', fontWeight: 700 }}>{color.toUpperCase()}</span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <input
            type="color"
            value={color}
            onChange={(e) => handleColorChange(e.target.value)}
            style={{
              width: 44,
              height: 32,
              borderRadius: 6,
              border: '1px solid rgba(255,255,255,0.2)',
              cursor: 'pointer',
              background: 'transparent',
              padding: 0,
            }}
          />
          <input
            type="text"
            value={color}
            onChange={(e) => handleColorChange(e.target.value)}
            style={{
              flex: 1,
              background: 'rgba(0,0,0,0.4)',
              border: '1px solid rgba(255,255,255,0.15)',
              borderRadius: 6,
              padding: '6px 8px',
              color: '#f8fafc',
              fontFamily: 'monospace',
              fontSize: 11,
            }}
          />
        </div>
      </div>

      {/* PBR Roughness & Metalness */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, background: 'rgba(0,0,0,0.25)', padding: 10, borderRadius: 8 }}>
        {/* Roughness */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
            <span style={{ color: '#94a3b8' }}>Độ Nhám (Roughness):</span>
            <span style={{ color: '#f1f5f9', fontWeight: 600 }}>{roughness.toFixed(2)}</span>
          </div>
          <input
            type="range"
            min="0"
            max="1"
            step="0.02"
            value={roughness}
            onChange={(e) => handleRoughnessChange(parseFloat(e.target.value))}
            style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
          />
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#64748b' }}>
            <span>Bóng láng (0.0)</span>
            <span>Lì nhám (1.0)</span>
          </div>
        </div>

        {/* Metalness */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
            <span style={{ color: '#94a3b8' }}>Độ Kim Loại (Metalness):</span>
            <span style={{ color: '#f1f5f9', fontWeight: 600 }}>{metalness.toFixed(2)}</span>
          </div>
          <input
            type="range"
            min="0"
            max="1"
            step="0.02"
            value={metalness}
            onChange={(e) => handleMetalnessChange(parseFloat(e.target.value))}
            style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
          />
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#64748b' }}>
            <span>Vải / Da (0.0)</span>
            <span>Kim loại (1.0)</span>
          </div>
        </div>
      </div>

      {/* Cyberpunk Emissive Glow */}
      <div style={{ background: 'rgba(0,0,0,0.25)', padding: 10, borderRadius: 8 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 5, color: '#cbd5e1', fontWeight: 600 }}>
            <Zap size={13} style={{ color: '#f59e0b' }} />
            <span>Phát Sáng Neon (Glow)</span>
          </div>
          <label style={{ display: 'flex', alignItems: 'center', gap: 4, cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={isGlowEnabled}
              onChange={(e) => handleGlowToggle(e.target.checked)}
              style={{ accentColor: '#f59e0b' }}
            />
            <span style={{ fontSize: 10, color: isGlowEnabled ? '#f59e0b' : '#64748b' }}>
              {isGlowEnabled ? 'BẬT' : 'TẮT'}
            </span>
          </label>
        </div>

        {isGlowEnabled && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 6 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input
                type="color"
                value={emissive}
                onChange={(e) => handleEmissiveColorChange(e.target.value)}
                style={{
                  width: 36,
                  height: 28,
                  borderRadius: 4,
                  border: '1px solid rgba(255,255,255,0.2)',
                  cursor: 'pointer',
                  background: 'transparent',
                }}
              />
              <span style={{ fontSize: 11, color: '#94a3b8' }}>Màu Đèn Neon</span>
            </div>

            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 2 }}>
                <span>Cường độ sáng:</span>
                <span style={{ color: '#f59e0b', fontWeight: 700 }}>{emissiveIntensity.toFixed(1)}x</span>
              </div>
              <input
                type="range"
                min="0.1"
                max="5.0"
                step="0.1"
                value={emissiveIntensity}
                onChange={(e) => handleEmissiveIntensityChange(parseFloat(e.target.value))}
                style={{ width: '100%', accentColor: '#f59e0b', cursor: 'pointer' }}
              />
            </div>
          </div>
        )}
      </div>

      {/* Visibility & Wireframe Utilities */}
      <div style={{ display: 'flex', gap: 8 }}>
        <button
          onClick={handleVisibilityToggle}
          style={{
            flex: 1,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 5,
            padding: '7px 10px',
            borderRadius: 6,
            background: visible ? 'rgba(56, 189, 248, 0.15)' : 'rgba(239, 68, 68, 0.15)',
            border: visible ? '1px solid rgba(56, 189, 248, 0.4)' : '1px solid rgba(239, 68, 68, 0.4)',
            color: visible ? '#38bdf8' : '#ef4444',
            cursor: 'pointer',
            fontWeight: 600,
            fontSize: 11,
          }}
        >
          {visible ? <Eye size={13} /> : <EyeOff size={13} />}
          <span>{visible ? 'Đang Hiện' : 'Đã Ẩn'}</span>
        </button>

        <button
          onClick={() => handleWireframeToggle(!wireframe)}
          style={{
            flex: 1,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 5,
            padding: '7px 10px',
            borderRadius: 6,
            background: wireframe ? 'rgba(168, 85, 247, 0.2)' : 'rgba(255, 255, 255, 0.06)',
            border: wireframe ? '1px solid #a855f7' : '1px solid rgba(255, 255, 255, 0.1)',
            color: wireframe ? '#c084fc' : '#cbd5e1',
            cursor: 'pointer',
            fontWeight: 600,
            fontSize: 11,
          }}
        >
          <Layers size={13} />
          <span>Khung Dây</span>
        </button>
      </div>

      {/* Reset Button */}
      <button
        onClick={handleReset}
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 5,
          padding: '8px',
          borderRadius: 6,
          background: 'rgba(255, 255, 255, 0.05)',
          border: '1px solid rgba(255, 255, 255, 0.12)',
          color: '#94a3b8',
          cursor: 'pointer',
          fontSize: 11,
          fontWeight: 500,
          transition: 'all 0.15s ease',
        }}
      >
        <RotateCcw size={12} />
        <span>Hoàn Tác Về Mặc Định (Reset)</span>
      </button>
    </div>
  );
};
