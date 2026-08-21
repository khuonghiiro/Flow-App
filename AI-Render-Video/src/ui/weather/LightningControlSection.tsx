import React from 'react';
import { Zap } from 'lucide-react';
import { EnvironmentOverride } from '../../types/scene';

interface LightningControlSectionProps {
  override: EnvironmentOverride;
  onChange: (override: EnvironmentOverride) => void;
}

export const LightningControlSection: React.FC<LightningControlSectionProps> = ({
  override,
  onChange,
}) => {
  const activeLightningPreset = override.lightning_preset || (override.lightning_enabled === false ? 'none' : 'scattered_strikes');
  const lightningFrequency = override.lightning_frequency ?? 0;
  const lightningCloudIntensity = override.lightning_cloud_intensity ?? 1.0;
  const lightningStrikeIntensity = override.lightning_strike_intensity ?? 1.0;

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        background: 'rgba(234, 179, 8, 0.06)',
        padding: 10,
        borderRadius: 8,
        border: '1px solid rgba(234, 179, 8, 0.25)',
        marginTop: 4,
      }}
    >
      {/* Lightning Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <label
          style={{
            fontSize: '11px',
            color: '#facc15',
            fontWeight: 700,
            display: 'flex',
            alignItems: 'center',
            gap: 5,
          }}
        >
          <Zap size={13} color="#facc15" /> SẤM CHỚP & TIA SÉT 3D
        </label>
        <span style={{ fontSize: '9px', color: '#fef08a', fontWeight: 600 }}>
          {activeLightningPreset === 'none'
            ? '🚫 Đang tắt sét'
            : lightningFrequency === 0
            ? '⚡ Tự động theo mưa'
            : `⚡ ${lightningFrequency.toFixed(1)}s`}
        </span>
      </div>

      {/* Lightning Preset Tags */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
        <div style={{ fontSize: '10px', color: '#cbd5e1' }}>Cài đặt sẵn sấm chớp & tia sét:</div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 4 }}>
          {[
            { key: 'none', label: '🚫 Tắt Sét', freq: 0, cloudInt: 0, strikeInt: 0, enabled: false },
            { key: 'sheet_only', label: '☁️ Chớp Mây', freq: 6.0, cloudInt: 1.0, strikeInt: 0.0, enabled: true },
            { key: 'scattered_strikes', label: '⚡ Sét Rải Rác', freq: 4.5, cloudInt: 1.0, strikeInt: 1.0, enabled: true },
            { key: 'heavy_storm', label: '⛈️ Bão Dồn Dập', freq: 2.0, cloudInt: 1.5, strikeInt: 1.5, enabled: true },
          ].map((p) => {
            const isCurrentActive = activeLightningPreset === p.key;
            return (
              <button
                key={p.key}
                type="button"
                style={{
                  padding: '6px 2px',
                  fontSize: '9px',
                  fontWeight: isCurrentActive ? 700 : 500,
                  borderRadius: 6,
                  border: `1px solid ${isCurrentActive ? '#facc15' : 'rgba(255, 255, 255, 0.12)'}`,
                  background: isCurrentActive
                    ? 'linear-gradient(135deg, rgba(250, 204, 21, 0.32), rgba(234, 179, 8, 0.18))'
                    : 'rgba(0, 0, 0, 0.35)',
                  color: isCurrentActive ? '#fef08a' : '#94a3b8',
                  cursor: 'pointer',
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 2,
                  boxShadow: isCurrentActive ? '0 0 10px rgba(250, 204, 21, 0.4)' : 'none',
                  transition: 'all 0.15s ease-in-out',
                }}
                onClick={() => {
                  onChange({
                    ...override,
                    lightning_enabled: p.enabled,
                    lightning_preset: p.key as any,
                    lightning_frequency: p.freq,
                    lightning_cloud_intensity: p.cloudInt,
                    lightning_strike_intensity: p.strikeInt,
                  });
                }}
              >
                <span>{p.label}</span>
                {isCurrentActive && (
                  <span
                    style={{
                      fontSize: '8px',
                      color: '#facc15',
                      fontWeight: 800,
                      display: 'flex',
                      alignItems: 'center',
                      gap: 2,
                    }}
                  >
                    ● Đang Chọn
                  </span>
                )}
              </button>
            );
          })}
        </div>
      </div>

      {/* Sliders for Deep Lightning Tuning */}
      {activeLightningPreset !== 'none' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 4 }}>
          {/* Slider 1: Thời gian ngẫu nhiên sấm chớp */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#e2e8f0' }}>
              <span>Tần suất sấm sét:</span>
              <span style={{ color: '#facc15', fontWeight: 600 }}>
                {lightningFrequency === 0 ? 'Tự động theo mưa' : `${lightningFrequency.toFixed(1)}s (±25%)`}
              </span>
            </div>
            <input
              type="range"
              min="0"
              max="15"
              step="0.5"
              value={lightningFrequency}
              onChange={(e) =>
                onChange({
                  ...override,
                  lightning_frequency: parseFloat(e.target.value),
                  lightning_preset: 'custom',
                  lightning_enabled: true,
                })
              }
              style={{ width: '100%', accentColor: '#facc15', cursor: 'pointer' }}
            />
          </div>

          {/* Slider 2: Nhịp & Độ sáng sấm chớp trên mây */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#e2e8f0' }}>
              <span>Nhịp & Ánh chớp trong mây (Cloud Sheet):</span>
              <span style={{ color: '#facc15', fontWeight: 600 }}>
                {Math.round(lightningCloudIntensity * 100)}%
              </span>
            </div>
            <input
              type="range"
              min="0"
              max="2"
              step="0.05"
              value={lightningCloudIntensity}
              onChange={(e) =>
                onChange({
                  ...override,
                  lightning_cloud_intensity: parseFloat(e.target.value),
                  lightning_preset: 'custom',
                  lightning_enabled: true,
                })
              }
              style={{ width: '100%', accentColor: '#facc15', cursor: 'pointer' }}
            />
          </div>

          {/* Slider 3: Tia sét đánh xuống dưới & Chiếu sáng vật thể 3D */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#e2e8f0' }}>
              <span>Tia sét 3D đánh xuống đất & Rọi sáng vật thể:</span>
              <span style={{ color: '#facc15', fontWeight: 600 }}>
                {Math.round(lightningStrikeIntensity * 100)}%
              </span>
            </div>
            <input
              type="range"
              min="0"
              max="2"
              step="0.05"
              value={lightningStrikeIntensity}
              onChange={(e) =>
                onChange({
                  ...override,
                  lightning_strike_intensity: parseFloat(e.target.value),
                  lightning_preset: 'custom',
                  lightning_enabled: true,
                })
              }
              style={{ width: '100%', accentColor: '#facc15', cursor: 'pointer' }}
            />
          </div>
        </div>
      )}
    </div>
  );
};
