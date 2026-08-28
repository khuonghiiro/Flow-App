import React, { useState, useEffect } from 'react';
import {
  Palette,
  Sparkles,
  Layers,
  Eye,
  EyeOff,
  RotateCcw,
  Sliders,
  Zap,
  CheckCircle,
  ChevronDown,
  Info,
  Flame,
} from 'lucide-react';
import {
  MaterialOverrideEngine,
  PRESET_COLOR_PALETTES,
  ColorPresetPalette,
} from '../../core/materials/MaterialOverrideEngine';
import { PartMaterialCustomization, CharacterAssembly } from '../../types/scene';
import { SelectedPartInfo } from '../workbench/Interactive3DPartSelector';

export interface AvailablePartItem {
  key: string;
  name: string;
  categoryLabel: string;
  categoryIcon: string;
}

export interface PartMaterialPanelProps {
  assembly: CharacterAssembly;
  selectedPart: SelectedPartInfo | null;
  onSelectPartKey: (partKey: string) => void;
  onApplyOverride: (meshKey: string, override: PartMaterialCustomization) => void;
  onResetOverride: (meshKey: string) => void;
  availableParts?: AvailablePartItem[];
}

export const PartMaterialPanel: React.FC<PartMaterialPanelProps> = ({
  assembly,
  selectedPart,
  onSelectPartKey,
  onApplyOverride,
  onResetOverride,
  availableParts = [],
}) => {
  // Determine currently active part key
  const activeKey = selectedPart?.meshKey || (availableParts.length > 0 ? availableParts[0].key : 'default');
  const activeOverride = (assembly.material_overrides || {})[activeKey] || {};

  const activeDisplayName =
    selectedPart?.displayName ||
    availableParts.find((p) => p.key === activeKey)?.name ||
    activeKey;

  const activeCategoryIcon =
    selectedPart?.categoryIcon ||
    availableParts.find((p) => p.key === activeKey)?.categoryIcon ||
    '✨';

  // Read state strictly from saved override (or initial fallback)
  const [color, setColor] = useState<string>(activeOverride.color || selectedPart?.initialColor || '#ffffff');
  const [roughness, setRoughness] = useState<number>(activeOverride.roughness ?? selectedPart?.initialRoughness ?? 0.6);
  const [metalness, setMetalness] = useState<number>(activeOverride.metalness ?? selectedPart?.initialMetalness ?? 0.05);
  const [emissive, setEmissive] = useState<string>(activeOverride.emissive || selectedPart?.initialEmissive || '#000000');
  const [emissiveIntensity, setEmissiveIntensity] = useState<number>(activeOverride.emissiveIntensity ?? selectedPart?.initialEmissiveIntensity ?? 0.0);
  const [isGlowEnabled, setIsGlowEnabled] = useState<boolean>(
    Boolean(activeOverride.emissive && activeOverride.emissive !== '#000000' && (activeOverride.emissiveIntensity || 0) > 0.05)
  );
  const [wireframe, setWireframe] = useState<boolean>(activeOverride.wireframe ?? selectedPart?.initialWireframe ?? false);
  const [visible, setVisible] = useState<boolean>(activeOverride.visible ?? selectedPart?.initialVisible ?? true);

  // Sync state whenever activeKey or assembly.material_overrides changes
  useEffect(() => {
    const saved = (assembly.material_overrides || {})[activeKey] || {};
    setColor(saved.color || selectedPart?.initialColor || '#ffffff');
    setRoughness(saved.roughness ?? selectedPart?.initialRoughness ?? 0.6);
    setMetalness(saved.metalness ?? selectedPart?.initialMetalness ?? 0.05);
    setEmissive(saved.emissive || selectedPart?.initialEmissive || '#000000');
    setEmissiveIntensity(saved.emissiveIntensity ?? selectedPart?.initialEmissiveIntensity ?? 0.0);
    setIsGlowEnabled(Boolean(saved.emissive && saved.emissive !== '#000000' && (saved.emissiveIntensity || 0) > 0.05));
    setWireframe(saved.wireframe ?? selectedPart?.initialWireframe ?? false);
    setVisible(saved.visible ?? selectedPart?.initialVisible ?? true);
  }, [activeKey, assembly.material_overrides]);

  // Commit update helper: Only called on pointer release, blur, or direct button click
  const commitValues = (explicitUpdates?: Partial<PartMaterialCustomization>) => {
    const finalConfig: PartMaterialCustomization = {
      color: explicitUpdates?.color !== undefined ? explicitUpdates.color : color,
      roughness: explicitUpdates?.roughness !== undefined ? explicitUpdates.roughness : roughness,
      metalness: explicitUpdates?.metalness !== undefined ? explicitUpdates.metalness : metalness,
      emissive: explicitUpdates?.emissive !== undefined ? explicitUpdates.emissive : (isGlowEnabled ? emissive : '#000000'),
      emissiveIntensity: explicitUpdates?.emissiveIntensity !== undefined ? explicitUpdates.emissiveIntensity : (isGlowEnabled ? emissiveIntensity : 0),
      wireframe: explicitUpdates?.wireframe !== undefined ? explicitUpdates.wireframe : wireframe,
      visible: explicitUpdates?.visible !== undefined ? explicitUpdates.visible : visible,
    };
    onApplyOverride(activeKey, finalConfig);
  };

  const handleGlowToggle = (enabled: boolean) => {
    setIsGlowEnabled(enabled);
    const newIntensity = enabled ? Math.max(1.0, emissiveIntensity || 1.0) : 0;
    const newEmissive = enabled ? (emissive === '#000000' ? (color === '#ffffff' ? '#00f0ff' : color) : emissive) : '#000000';
    setEmissiveIntensity(newIntensity);
    setEmissive(newEmissive);
    commitValues({ emissive: newEmissive, emissiveIntensity: newIntensity });
  };

  const handleWireframeToggle = (wf: boolean) => {
    setWireframe(wf);
    commitValues({ wireframe: wf });
  };

  const handleVisibilityToggle = () => {
    const nextVis = !visible;
    setVisible(nextVis);
    commitValues({ visible: nextVis });
  };

  const handleApplyPreset = (p: ColorPresetPalette) => {
    setColor(p.color);
    setRoughness(p.roughness);
    setMetalness(p.metalness);
    const hasGlow = Boolean(p.emissive && p.emissiveIntensity && p.emissiveIntensity > 0);
    setIsGlowEnabled(hasGlow);
    setEmissive(p.emissive || '#000000');
    setEmissiveIntensity(p.emissiveIntensity || 0);

    onApplyOverride(activeKey, {
      color: p.color,
      roughness: p.roughness,
      metalness: p.metalness,
      emissive: p.emissive || '#000000',
      emissiveIntensity: p.emissiveIntensity || 0,
      wireframe,
      visible,
    });
  };

  const handleReset = () => {
    setColor('#ffffff');
    setRoughness(0.6);
    setMetalness(0.05);
    setEmissive('#000000');
    setEmissiveIntensity(0);
    setIsGlowEnabled(false);
    setWireframe(false);
    setVisible(true);
    onResetOverride(activeKey);
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14, color: '#f8fafc', padding: 2 }}>
      {/* 1. Header & Active Part Selector Dropdown */}
      <div
        style={{
          background: 'rgba(30, 41, 59, 0.65)',
          border: '1px solid rgba(56, 189, 248, 0.3)',
          borderRadius: 10,
          padding: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 8,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ fontSize: 18 }}>{activeCategoryIcon}</span>
            <div>
              <div style={{ fontWeight: 700, color: '#38bdf8', fontSize: 13 }}>
                {activeDisplayName}
              </div>
              <div style={{ fontSize: 10, color: '#94a3b8' }}>
                Key ID: <code style={{ color: '#cbd5e1' }}>{activeKey}</code>
              </div>
            </div>
          </div>

          <div style={{ display: 'flex', gap: 6 }}>
            <button
              onClick={handleVisibilityToggle}
              title={visible ? 'Ẩn bộ phận này' : 'Hiện bộ phận này'}
              style={{
                background: visible ? 'rgba(56, 189, 248, 0.15)' : 'rgba(239, 68, 68, 0.15)',
                border: visible ? '1px solid rgba(56, 189, 248, 0.35)' : '1px solid rgba(239, 68, 68, 0.35)',
                color: visible ? '#38bdf8' : '#ef4444',
                padding: '4px 8px',
                borderRadius: 6,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                fontSize: 10,
                fontWeight: 600,
              }}
            >
              {visible ? <Eye size={12} /> : <EyeOff size={12} />}
              <span>{visible ? 'Hiện' : 'Ẩn'}</span>
            </button>

            <button
              onClick={() => handleWireframeToggle(!wireframe)}
              title="Khung dây wireframe"
              style={{
                background: wireframe ? 'rgba(168, 85, 247, 0.25)' : 'rgba(255,255,255,0.05)',
                border: wireframe ? '1px solid #a855f7' : '1px solid rgba(255,255,255,0.1)',
                color: wireframe ? '#c084fc' : '#94a3b8',
                padding: '4px 8px',
                borderRadius: 6,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                fontSize: 10,
                fontWeight: 600,
              }}
            >
              <Layers size={12} />
              <span>Lưới</span>
            </button>
          </div>
        </div>

        {/* Quick Part Switcher Pills */}
        {availableParts.length > 1 && (
          <div>
            <div style={{ fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
              Chọn bộ phận nhanh trong set:
            </div>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5 }}>
              {availableParts.map((p) => {
                const isSelected = p.key === activeKey;
                return (
                  <button
                    key={p.key}
                    onClick={() => onSelectPartKey(p.key)}
                    style={{
                      padding: '3px 8px',
                      borderRadius: 6,
                      fontSize: 10,
                      fontWeight: isSelected ? 700 : 500,
                      background: isSelected ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.05)',
                      border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                      color: isSelected ? '#38bdf8' : '#cbd5e1',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 4,
                    }}
                  >
                    <span>{p.categoryIcon}</span>
                    <span>{p.name}</span>
                  </button>
                );
              })}
            </div>
          </div>
        )}
      </div>

      {/* 2. Realistic Preset Color Palettes */}
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 5, color: '#cbd5e1', fontWeight: 600, fontSize: 12, marginBottom: 8 }}>
          <Palette size={14} style={{ color: '#38bdf8' }} />
          <span>Bảng Màu & Chất Liệu Chuẩn Thực Tế</span>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 8 }}>
          {PRESET_COLOR_PALETTES.map((p) => {
            const isMatch = color.toLowerCase() === p.color.toLowerCase();
            return (
              <button
                key={p.id}
                onClick={() => handleApplyPreset(p)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 8,
                  padding: '6px 8px',
                  borderRadius: 8,
                  background: isMatch ? 'rgba(56, 189, 248, 0.15)' : 'rgba(30, 41, 59, 0.5)',
                  border: isMatch ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                  cursor: 'pointer',
                  textAlign: 'left',
                  transition: 'all 0.15s ease',
                }}
              >
                <div
                  style={{
                    width: 26,
                    height: 26,
                    borderRadius: 6,
                    background: p.previewGradient,
                    border: '1px solid rgba(255,255,255,0.2)',
                    flexShrink: 0,
                    boxShadow: isMatch ? '0 0 10px rgba(56, 189, 248, 0.5)' : 'none',
                  }}
                />
                <div style={{ overflow: 'hidden', flex: 1 }}>
                  <div style={{ fontSize: 11, fontWeight: 700, color: isMatch ? '#38bdf8' : '#f1f5f9', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                    {p.name}
                  </div>
                  <div style={{ fontSize: 9, color: '#94a3b8', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                    {p.description}
                  </div>
                </div>
              </button>
            );
          })}
        </div>
      </div>

      {/* 3. Custom Color Tint Picker */}
      <div style={{ background: 'rgba(30, 41, 59, 0.5)', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 10, padding: 10 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
          <span style={{ color: '#cbd5e1', fontWeight: 600, fontSize: 12 }}>Tùy Chọn Màu Tự Do (Color Picker)</span>
          <span style={{ color: '#38bdf8', fontFamily: 'monospace', fontWeight: 700, fontSize: 12 }}>{color.toUpperCase()}</span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <input
            type="color"
            value={color}
            onChange={(e) => setColor(e.target.value)}
            onPointerUp={() => commitValues({ color })}
            onMouseUp={() => commitValues({ color })}
            onBlur={() => commitValues({ color })}
            style={{
              width: 50,
              height: 36,
              borderRadius: 8,
              border: '1px solid rgba(255,255,255,0.25)',
              cursor: 'pointer',
              background: 'transparent',
              padding: 0,
            }}
          />
          <input
            type="text"
            value={color}
            onChange={(e) => setColor(e.target.value)}
            onBlur={() => commitValues({ color })}
            onKeyDown={(e) => { if (e.key === 'Enter') commitValues({ color }); }}
            style={{
              flex: 1,
              background: 'rgba(15, 23, 42, 0.8)',
              border: '1px solid rgba(255,255,255,0.15)',
              borderRadius: 6,
              padding: '8px 10px',
              color: '#f8fafc',
              fontFamily: 'monospace',
              fontSize: 12,
            }}
          />
        </div>
      </div>

      {/* 4. PBR Roughness & Metalness Sliders */}
      <div style={{ background: 'rgba(30, 41, 59, 0.5)', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 10, padding: 10, display: 'flex', flexDirection: 'column', gap: 10 }}>
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 3, fontSize: 11 }}>
            <span style={{ color: '#cbd5e1', fontWeight: 600 }}>Độ Nhám (Roughness):</span>
            <span style={{ color: '#38bdf8', fontWeight: 700 }}>{roughness.toFixed(2)}</span>
          </div>
          <input
            type="range"
            min="0"
            max="1"
            step="0.01"
            value={roughness}
            onChange={(e) => setRoughness(parseFloat(e.target.value))}
            onPointerUp={() => commitValues({ roughness })}
            onMouseUp={() => commitValues({ roughness })}
            onTouchEnd={() => commitValues({ roughness })}
            onKeyUp={() => commitValues({ roughness })}
            style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
          />
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#64748b', marginTop: 2 }}>
            <span>Bóng láng (0.0)</span>
            <span>Vải lì / Da nhám (1.0)</span>
          </div>
        </div>

        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 3, fontSize: 11 }}>
            <span style={{ color: '#cbd5e1', fontWeight: 600 }}>Độ Kim Loại (Metalness):</span>
            <span style={{ color: '#38bdf8', fontWeight: 700 }}>{metalness.toFixed(2)}</span>
          </div>
          <input
            type="range"
            min="0"
            max="1"
            step="0.01"
            value={metalness}
            onChange={(e) => setMetalness(parseFloat(e.target.value))}
            onPointerUp={() => commitValues({ metalness })}
            onMouseUp={() => commitValues({ metalness })}
            onTouchEnd={() => commitValues({ metalness })}
            onKeyUp={() => commitValues({ metalness })}
            style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
          />
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#64748b', marginTop: 2 }}>
            <span>Vải thông thường (0.0)</span>
            <span>Giáp Kim Loại (1.0)</span>
          </div>
        </div>
      </div>

      {/* 5. Cyberpunk Emissive Glow */}
      <div style={{ background: 'rgba(30, 41, 59, 0.5)', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 10, padding: 10 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 5, color: '#cbd5e1', fontWeight: 600, fontSize: 12 }}>
            <Zap size={14} style={{ color: '#f59e0b' }} />
            <span>Phát Sáng Neon (Cyberpunk Glow)</span>
          </div>
          <label style={{ display: 'flex', alignItems: 'center', gap: 4, cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={isGlowEnabled}
              onChange={(e) => handleGlowToggle(e.target.checked)}
              style={{ accentColor: '#f59e0b' }}
            />
            <span style={{ fontSize: 11, fontWeight: 700, color: isGlowEnabled ? '#f59e0b' : '#64748b' }}>
              {isGlowEnabled ? 'BẬT' : 'TẮT'}
            </span>
          </label>
        </div>

        {isGlowEnabled && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 8 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <input
                type="color"
                value={emissive}
                onChange={(e) => setEmissive(e.target.value)}
                onPointerUp={() => commitValues({ emissive })}
                onMouseUp={() => commitValues({ emissive })}
                onBlur={() => commitValues({ emissive })}
                style={{
                  width: 44,
                  height: 32,
                  borderRadius: 6,
                  border: '1px solid rgba(255,255,255,0.2)',
                  cursor: 'pointer',
                  background: 'transparent',
                }}
              />
              <div>
                <div style={{ fontSize: 11, color: '#f8fafc', fontWeight: 600 }}>Màu Đèn Neon Phát Sáng</div>
                <div style={{ fontSize: 9, color: '#94a3b8' }}>Mã màu: {emissive.toUpperCase()}</div>
              </div>
            </div>

            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 2 }}>
                <span>Cường độ phát sáng:</span>
                <span style={{ color: '#f59e0b', fontWeight: 700 }}>{emissiveIntensity.toFixed(1)}x</span>
              </div>
              <input
                type="range"
                min="0.1"
                max="5.0"
                step="0.1"
                value={emissiveIntensity}
                onChange={(e) => setEmissiveIntensity(parseFloat(e.target.value))}
                onPointerUp={() => commitValues({ emissiveIntensity })}
                onMouseUp={() => commitValues({ emissiveIntensity })}
                onTouchEnd={() => commitValues({ emissiveIntensity })}
                onKeyUp={() => commitValues({ emissiveIntensity })}
                style={{ width: '100%', accentColor: '#f59e0b', cursor: 'pointer' }}
              />
            </div>
          </div>
        )}
      </div>

      {/* 6. Reset Button */}
      <button
        onClick={handleReset}
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 6,
          padding: '10px',
          borderRadius: 8,
          background: 'rgba(255, 255, 255, 0.05)',
          border: '1px solid rgba(255, 255, 255, 0.12)',
          color: '#94a3b8',
          cursor: 'pointer',
          fontSize: 11,
          fontWeight: 600,
          transition: 'all 0.15s ease',
        }}
      >
        <RotateCcw size={13} />
        <span>Hoàn Tác Về Mặc Định (Reset)</span>
      </button>
    </div>
  );
};
