import React from 'react';
import { 
  CloudRain, Sun, Wind, CloudFog, Compass, Droplets, 
  Cloud, CloudSun, CloudLightning, Sunset, Sunrise, Moon, Layers,
  Image, RotateCw, Upload, Zap
} from 'lucide-react';
import { EnvironmentOverride } from '../types/scene';

interface WeatherControlPanelProps {
  override: EnvironmentOverride;
  onChange: (override: EnvironmentOverride) => void;
}

export const WeatherControlPanel: React.FC<WeatherControlPanelProps> = ({ override, onChange }) => {
  const currentSkyTime = override.sky_time || 'noon';
  const rainIntensity = override.rain_intensity ?? 0;
  const windIntensity = override.wind_intensity ?? 0.3;
  const windDirection = override.wind_direction ?? 45;
  const fogDensity = override.fog_density ?? 0.012;
  const cloudCoverage = override.cloud_coverage ?? 0.6;
  const cloudType = override.cloud_type || 'multi_layered';
  const cloudLayers = override.cloud_layers ?? 3;
  const cloudAltitude = override.cloud_altitude ?? 1.0;
  const lightningFrequency = override.lightning_frequency ?? 0;
  const lightningCloudIntensity = override.lightning_cloud_intensity ?? 1.0;
  const lightningStrikeIntensity = override.lightning_strike_intensity ?? 1.0;

  // Helper text for Rain Level
  const getRainLevelText = (val: number) => {
    if (val <= 0.02) return 'Tắt (Không mưa)';
    if (val <= 0.3) return '🌦️ Mưa phùn nhẹ hạt mềm';
    if (val <= 0.7) return '🌧️ Mưa rào màn nước tự nhiên';
    return '⛈️ Mưa to xối xả - Bão gió cuồng phong';
  };

  // Helper text for Wind Level
  const getWindLevelText = (val: number) => {
    if (val <= 0.05) return 'Tĩnh lặng (Không gió)';
    if (val <= 0.35) return '🍃 Gió thoảng êm đềm';
    if (val <= 0.7) return '💨 Gió thổi mạnh tạt mưa';
    return '🌪️ Cuồng phong - Gió bão cấp 10';
  };

  // Helper text for Cloud Coverage
  const getCloudCoverageText = (val: number) => {
    if (val <= 0.05) return '☀️ Trời trong xanh (Không mây)';
    if (val <= 0.3) return '🌤️ Mây rải rác nhẹ (Ít mây)';
    if (val <= 0.65) return '⛅ Mây bồng bềnh điện ảnh (Nhiều mây)';
    return '☁️ Mây phủ kín vòm trời (U ám / Giông bão)';
  };

  // Helper text for Wind Direction
  const getWindDirectionText = (deg: number) => {
    const d = (deg % 360 + 360) % 360;
    if (d >= 337.5 || d < 22.5) return 'Bắc (0°)';
    if (d >= 22.5 && d < 67.5) return 'Đông Bắc (45°)';
    if (d >= 67.5 && d < 112.5) return 'Đông (90°)';
    if (d >= 112.5 && d < 157.5) return 'Đông Nam (135°)';
    if (d >= 157.5 && d < 202.5) return 'Nam (180°)';
    if (d >= 202.5 && d < 247.5) return 'Tây Nam (225°)';
    if (d >= 247.5 && d < 292.5) return 'Tây (270°)';
    return 'Tây Bắc (315°)';
  };

  // Quick Preset Handlers
  const applyPreset = (preset: 'sunny' | 'sunset' | 'cloudy' | 'drizzle' | 'heavy_rain' | 'storm') => {
    switch (preset) {
      case 'sunny':
        onChange({
          ...override,
          enabled: true,
          sky_time: 'noon',
          sun_position: 0.5,
          rain_intensity: 0,
          wind_intensity: 0.15,
          wind_direction: 45,
          cloud_coverage: 0.25,
          cloud_type: 'cumulus',
          cloud_layers: 2,
          fog_density: 0.008,
        });
        break;
      case 'sunset':
        onChange({
          ...override,
          enabled: true,
          sky_time: 'sunset',
          sun_position: 0.85,
          rain_intensity: 0,
          wind_intensity: 0.25,
          wind_direction: 90,
          cloud_coverage: 0.55,
          cloud_type: 'sunset_glow',
          cloud_layers: 3,
          fog_density: 0.012,
        });
        break;
      case 'cloudy':
        onChange({
          ...override,
          enabled: true,
          sky_time: 'overcast',
          sun_position: 0.5,
          rain_intensity: 0,
          wind_intensity: 0.35,
          wind_direction: 60,
          cloud_coverage: 0.85,
          cloud_type: 'multi_layered',
          cloud_layers: 3,
          fog_density: 0.018,
        });
        break;
      case 'drizzle':
        onChange({
          ...override,
          enabled: true,
          sky_time: 'noon',
          sun_position: 0.45,
          rain_intensity: 0.25,
          wind_intensity: 0.35,
          wind_direction: 45,
          cloud_coverage: 0.40,
          cloud_type: 'cumulus',
          cloud_layers: 2,
          fog_density: 0.016,
        });
        break;
      case 'heavy_rain':
        onChange({
          ...override,
          enabled: true,
          sky_time: 'overcast',
          sun_position: 0.4,
          rain_intensity: 0.70,
          wind_intensity: 0.65,
          wind_direction: 60,
          cloud_coverage: 0.88,
          cloud_type: 'cumulonimbus',
          cloud_layers: 4,
          fog_density: 0.026,
        });
        break;
      case 'storm':
        onChange({
          ...override,
          enabled: true,
          sky_time: 'overcast',
          sun_position: 0.1,
          rain_intensity: 1.0,
          wind_intensity: 0.95,
          wind_direction: 75,
          cloud_coverage: 1.0,
          cloud_type: 'cumulonimbus',
          cloud_layers: 6,
          fog_density: 0.038,
        });
        break;
    }
  };

  return (
    <div className="panel" style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
      {/* Panel Header */}
      <div className="panel-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h3 style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: 13 }}>
          <CloudRain size={16} className="icon-glow" color="#38bdf8" /> Thời Tiết, Bầu Trời & Mây Đa Tầng
        </h3>
        
        <label 
          style={{ 
            display: 'flex', 
            alignItems: 'center', 
            gap: '8px', 
            cursor: 'pointer', 
            fontSize: '11px',
            background: override.enabled ? 'rgba(56, 189, 248, 0.15)' : 'rgba(30, 41, 59, 0.8)',
            padding: '4px 10px',
            borderRadius: '16px',
            border: `1px solid ${override.enabled ? '#38bdf8' : '#475569'}`,
            color: override.enabled ? '#38bdf8' : '#94a3b8',
            transition: 'all 0.2s',
            fontWeight: 600,
          }}
        >
          <input 
            type="checkbox" 
            checked={override.enabled}
            onChange={(e) => onChange({ ...override, enabled: e.target.checked })}
            style={{ display: 'none' }}
          />
          <div style={{
            width: 8, height: 8, borderRadius: '50%',
            background: override.enabled ? '#38bdf8' : '#64748b',
          }} />
          {override.enabled ? 'Đang Tùy Biến' : 'Mặc Định'}
        </label>
      </div>

      {override.enabled ? (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          
          {/* Quick Weather Presets */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <label style={{ fontSize: '11px', color: '#94a3b8', fontWeight: 600 }}>Cài đặt nhanh thời tiết</label>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 4 }}>
              <button
                className="btn-secondary"
                style={{ padding: '6px 4px', fontSize: 10, textAlign: 'center', borderRadius: 6 }}
                onClick={() => applyPreset('sunny')}
                title="Nắng đẹp quang đãng"
              >
                ☀️ Nắng Đẹp
              </button>
              <button
                className="btn-secondary"
                style={{ padding: '6px 4px', fontSize: 10, textAlign: 'center', borderRadius: 6 }}
                onClick={() => applyPreset('sunset')}
                title="Hoàng hôn chiều tà"
              >
                🌅 Hoàng Hôn
              </button>
              <button
                className="btn-secondary"
                style={{ padding: '6px 4px', fontSize: 10, textAlign: 'center', borderRadius: 6 }}
                onClick={() => applyPreset('cloudy')}
                title="Trời nhiều mây u ám"
              >
                ⛅ Nhiều Mây
              </button>
              <button
                className="btn-secondary"
                style={{ padding: '6px 4px', fontSize: 10, textAlign: 'center', borderRadius: 6 }}
                onClick={() => applyPreset('drizzle')}
                title="Mưa phùn hạt nhỏ"
              >
                🌦️ Mưa Phùn
              </button>
              <button
                className="btn-secondary"
                style={{ padding: '6px 4px', fontSize: 10, textAlign: 'center', borderRadius: 6 }}
                onClick={() => applyPreset('heavy_rain')}
                title="Mưa rào hạt dày"
              >
                🌧️ Mưa Rào
              </button>
              <button
                className="btn-secondary"
                style={{ padding: '6px 4px', fontSize: 10, textAlign: 'center', borderRadius: 6, borderColor: '#ef4444', color: '#fca5a5' }}
                onClick={() => applyPreset('storm')}
                title="Bão giông cuồng phong"
              >
                ⛈️ Bão Lớn
              </button>
            </div>
          </div>

          <div style={{ borderTop: '1px solid rgba(255, 255, 255, 0.08)' }} />

          {/* 1. CHẾ ĐỘ BẦU TRỜI (DẠNG TAG BUTTONS TRỰC QUAN) */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <label style={{ fontSize: '11px', color: '#94a3b8', fontWeight: 600 }}>Chế độ Bầu Trời (Chọn nhanh dạng Tag)</label>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
              {[
                { key: 'noon', label: 'Giữa Trưa', pos: 0.5, icon: <Sun size={12} color="#f59e0b" /> },
                { key: 'sunrise', label: 'Bình Minh', pos: 0.15, icon: <Sunrise size={12} color="#fb923c" /> },
                { key: 'sunset', label: 'Hoàng Hôn', pos: 0.8, icon: <Sunset size={12} color="#f43f5e" /> },
                { key: 'overcast', label: 'Âm U', pos: 0.5, icon: <Cloud size={12} color="#94a3b8" /> },
                { key: 'night', label: 'Ban Đêm', pos: 0.95, icon: <Moon size={12} color="#38bdf8" /> },
              ].map((sky) => {
                const isActive = currentSkyTime === sky.key;
                return (
                  <button
                    key={sky.key}
                    type="button"
                    className={`btn-secondary ${isActive ? 'active' : ''}`}
                    style={{
                      padding: '5px 8px',
                      fontSize: 10,
                      fontWeight: isActive ? 700 : 500,
                      borderRadius: 14,
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: 4,
                      backgroundColor: isActive ? 'rgba(56, 189, 248, 0.2)' : 'rgba(30, 41, 59, 0.6)',
                      borderColor: isActive ? '#38bdf8' : 'rgba(255, 255, 255, 0.1)',
                      color: isActive ? '#ffffff' : '#94a3b8',
                      cursor: 'pointer',
                    }}
                    onClick={() => onChange({ 
                      ...override, 
                      sky_time: sky.key as any,
                      sun_position: sky.pos,
                    })}
                  >
                    {sky.icon}
                    <span>{sky.label}</span>
                  </button>
                );
              })}
            </div>
          </div>

          {/* SLIDER QUỸ ĐẠO MẶT TRỜI & ĐỔ BÓNG THỜI GIAN THỰC (Bình Minh -> Trưa -> Hoàng Hôn -> Đêm) */}
          <div style={{ 
            display: 'flex', 
            flexDirection: 'column', 
            gap: 6, 
            background: 'linear-gradient(135deg, rgba(251, 146, 60, 0.1), rgba(56, 189, 248, 0.05))', 
            padding: '10px 12px', 
            borderRadius: 8, 
            border: '1px solid rgba(251, 146, 60, 0.3)' 
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: '11px', color: '#fed7aa', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 5 }}>
                <Sun size={13} color="#f97316" /> VỊ TRÍ MẶT TRỜI & HƯỚNG BÓNG ĐỔ
              </label>
              <span style={{ fontSize: '10px', fontWeight: 700, color: '#fb923c' }}>
                {(() => {
                  const pos = override.sun_position ?? 0.5;
                  if (pos <= 0.24) return `🌅 Bình Minh (${Math.round(pos * 100)}%)`;
                  if (pos <= 0.68) return `☀️ Giữa Trưa (${Math.round(pos * 100)}%)`;
                  if (pos <= 0.88) return `🌇 Hoàng Hôn (${Math.round(pos * 100)}%)`;
                  return `🌙 Ban Đêm (${Math.round(pos * 100)}%)`;
                })()}
              </span>
            </div>

            <input 
              type="range" 
              min="0" max="1" step="0.01"
              value={override.sun_position ?? 0.5}
              onChange={(e) => {
                const pos = parseFloat(e.target.value);
                let autoSky: any = override.sky_time;
                if (pos <= 0.24) autoSky = 'sunrise';
                else if (pos <= 0.68) autoSky = 'noon';
                else if (pos <= 0.88) autoSky = 'sunset';
                else autoSky = 'night';

                onChange({ 
                  ...override, 
                  sun_position: pos,
                  sky_time: autoSky,
                });
              }}
              style={{ width: '100%', accentColor: '#f97316', cursor: 'pointer' }}
            />

            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#94a3b8' }}>
              <span>🌅 Đông (Mọc)</span>
              <span>☀️ Đỉnh Đầu</span>
              <span>🌇 Tây (Lặn)</span>
              <span>🌙 Nửa Đêm</span>
            </div>
          </div>

          {/* ẢNH NỀN BẦU TRỜI 360° (SKYBOX / EQUIRECTANGULAR PANORAMA - UNITY STYLE) */}
          <div style={{
            display: 'flex',
            flexDirection: 'column',
            gap: 8,
            background: 'linear-gradient(135deg, rgba(56, 189, 248, 0.08), rgba(147, 51, 234, 0.06))',
            padding: '10px 12px',
            borderRadius: 8,
            border: '1px solid rgba(56, 189, 248, 0.2)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: '11px', color: '#bae6fd', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 5 }}>
                <Image size={13} color="#38bdf8" /> ẢNH BẦU TRỜI (SKYBOX 360°)
              </label>
              <span style={{ fontSize: '10px', fontWeight: 600, color: (override.skybox_type && override.skybox_type !== 'none') ? '#38bdf8' : '#94a3b8' }}>
                {(() => {
                  const t = override.skybox_type || 'none';
                  if (t === 'day') return '☀️ Ban Ngày';
                  if (t === 'morning') return '🌅 Sáng Sớm';
                  if (t === 'night') return '🌙 Đêm Sao';
                  if (t === 'space') return '🌌 Vũ Trụ';
                  if (t === 'alien') return '👽 Hành Tinh Lạ';
                  if (t === 'custom') return '📁 Ảnh Tự Tải';
                  return '🎨 Mặc định';
                })()}
              </span>
            </div>

            {/* Skybox Presets Selector */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 4 }}>
              {[
                { key: 'none', label: '🎨 Mặc Định', skyTime: 'noon', sunPos: 0.5 },
                { key: 'day', label: '☀️ Ban Ngày', skyTime: 'noon', sunPos: 0.5 },
                { key: 'morning', label: '🌅 Sáng Sớm', skyTime: 'sunrise', sunPos: 0.15 },
                { key: 'night', label: '🌙 Đêm Sao', skyTime: 'night', sunPos: 0.95 },
                { key: 'space', label: '🌌 Vũ Trụ', skyTime: 'night', sunPos: 0.98 },
                { key: 'alien', label: '👽 Hành Tinh Lạ', skyTime: 'sunset', sunPos: 0.82 },
              ].map((item) => {
                const isSelected = (override.skybox_type || 'none') === item.key;
                return (
                  <button
                    key={item.key}
                    type="button"
                    className="btn-secondary"
                    style={{
                      padding: '6px 4px',
                      fontSize: 9.5,
                      fontWeight: isSelected ? 700 : 500,
                      borderRadius: 6,
                      textAlign: 'center',
                      backgroundColor: isSelected ? 'rgba(56, 189, 248, 0.25)' : 'rgba(30, 41, 59, 0.6)',
                      borderColor: isSelected ? '#38bdf8' : 'rgba(255, 255, 255, 0.08)',
                      color: isSelected ? '#38bdf8' : '#94a3b8',
                      cursor: 'pointer',
                    }}
                    onClick={() => onChange({ 
                      ...override, 
                      enabled: true,
                      skybox_type: item.key as any,
                      ...(item.key !== 'none' ? {
                        sky_time: item.skyTime as any,
                        sun_position: item.sunPos,
                      } : {})
                    })}
                  >
                    {item.label}
                  </button>
                );
              })}
            </div>

            {/* Custom File Upload Button */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 2 }}>
              <label style={{
                flex: 1,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 6,
                background: override.skybox_type === 'custom' ? 'rgba(56, 189, 248, 0.2)' : 'rgba(30, 41, 59, 0.8)',
                padding: '6px 8px',
                borderRadius: 6,
                border: `1px dashed ${override.skybox_type === 'custom' ? '#38bdf8' : '#475569'}`,
                color: override.skybox_type === 'custom' ? '#38bdf8' : '#cbd5e1',
                fontSize: 10,
                fontWeight: 600,
                cursor: 'pointer',
              }}>
                <Upload size={12} />
                <span>{override.skybox_type === 'custom' ? '✅ Đã nạp ảnh Skybox riêng' : '📁 Tải ảnh Skybox từ máy (.png, .jpg)'}</span>
                <input
                  type="file"
                  accept="image/png,image/jpeg,image/webp"
                  style={{ display: 'none' }}
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    if (file) {
                      const reader = new FileReader();
                      reader.onload = (event) => {
                        const url = event.target?.result as string;
                        if (url) {
                          onChange({
                            ...override,
                            skybox_type: 'custom',
                            skybox_url: url,
                          });
                        }
                      };
                      reader.readAsDataURL(file);
                    }
                  }}
                />
              </label>
            </div>

            {/* Skybox Fine-Tuning Sliders (when a Skybox is active) */}
            {override.skybox_type && override.skybox_type !== 'none' && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 4, paddingTop: 6, borderTop: '1px dashed rgba(255, 255, 255, 0.1)' }}>
                {/* Rotation Y Slider */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#94a3b8' }}>
                    <span style={{ display: 'flex', alignItems: 'center', gap: 3 }}>
                      <RotateCw size={10} /> Xoay hướng Skybox (360°):
                    </span>
                    <span style={{ color: '#38bdf8', fontWeight: 600 }}>{Math.round(override.skybox_rotation ?? 0)}°</span>
                  </div>
                  <input
                    type="range"
                    min="0"
                    max="360"
                    step="1"
                    value={override.skybox_rotation ?? 0}
                    onChange={(e) => onChange({ ...override, skybox_rotation: parseFloat(e.target.value) })}
                    style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
                  />
                </div>

                {/* Exposure / Intensity Slider */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#94a3b8' }}>
                    <span>Độ sáng Skybox (Exposure):</span>
                    <span style={{ color: '#38bdf8', fontWeight: 600 }}>{Math.round((override.skybox_exposure ?? 1.0) * 100)}%</span>
                  </div>
                  <input
                    type="range"
                    min="0.2"
                    max="2.5"
                    step="0.05"
                    value={override.skybox_exposure ?? 1.0}
                    onChange={(e) => onChange({ ...override, skybox_exposure: parseFloat(e.target.value) })}
                    style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
                  />
                </div>

                {/* Blur Slider */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#94a3b8' }}>
                    <span>Độ mờ hậu cảnh (Sky Blur):</span>
                    <span style={{ color: '#38bdf8', fontWeight: 600 }}>{Math.round((override.skybox_blur ?? 0) * 100)}%</span>
                  </div>
                  <input
                    type="range"
                    min="0"
                    max="1"
                    step="0.02"
                    value={override.skybox_blur ?? 0}
                    onChange={(e) => onChange({ ...override, skybox_blur: parseFloat(e.target.value) })}
                    style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
                  />
                </div>
              </div>
            )}
          </div>

          <div style={{ borderTop: '1px solid rgba(255, 255, 255, 0.08)' }} />

          {/* 2. HỆ THỐNG MÂY ĐA TẦNG 3D & KIỂU DÁNG MÂY PHONG PHÚ */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10, background: 'rgba(255, 255, 255, 0.03)', padding: 10, borderRadius: 8, border: '1px solid rgba(255, 255, 255, 0.08)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: '11px', color: '#f1f5f9', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 5 }}>
                <Cloud size={13} color="#38bdf8" /> MÂY 3D ĐA TẦNG (Độ Phủ Mây)
              </label>
              <span style={{ fontSize: '11px', fontWeight: 700, color: cloudCoverage > 0 ? '#38bdf8' : '#94a3b8' }}>
                {Math.round(cloudCoverage * 100)}%
              </span>
            </div>

            <div style={{ fontSize: '10px', color: '#cbd5e1', fontStyle: 'italic' }}>
              {getCloudCoverageText(cloudCoverage)}
            </div>

            {/* Cloud Coverage Slider */}
            <input 
              type="range" 
              min="0" max="1" step="0.01"
              value={cloudCoverage}
              onChange={(e) => onChange({ ...override, cloud_coverage: parseFloat(e.target.value) })}
              style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
            />

            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#64748b' }}>
              <span>0% Không mây</span>
              <span>30% Ít mây</span>
              <span>65% Nhiều mây</span>
              <span>100% Phủ kín</span>
            </div>

            {/* Cloud Shape Tags */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 6 }}>
              <label style={{ fontSize: '10px', color: '#94a3b8', display: 'flex', alignItems: 'center', gap: 4 }}>
                <CloudSun size={11} color="#38bdf8" /> Kiểu dáng mây:
              </label>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                {[
                  { key: 'cumulus', label: '☁ Bồng Bềnh (Cumulus)', desc: 'Phồng cuộn, mềm mại tự nhiên' },
                  { key: 'cumulonimbus', label: '⛈ Vũ Tích (Cumulonimbus)', desc: 'Dày đặc, mây giông bão' },
                  { key: 'multi_layered', label: '☁️ Đa Tầng Sâu', desc: 'Các lớp mây đan xen' },
                  { key: 'sunset_glow', label: '🌅 Rực Rỡ Hoàng Hôn', desc: 'Đậm đà đón ánh nắng' },
                ].map(ct => (
                  <button
                    key={ct.key}
                    title={ct.desc}
                    onClick={() => onChange({ ...override, cloud_type: ct.key as EnvironmentOverride['cloud_type'] })}
                    style={{
                      padding: '3px 8px',
                      fontSize: '9px',
                      borderRadius: 12,
                      border: `1px solid ${cloudType === ct.key ? '#38bdf8' : 'rgba(255,255,255,0.12)'}`,
                      background: cloudType === ct.key ? 'rgba(56,189,248,0.2)' : 'rgba(255,255,255,0.04)',
                      color: cloudType === ct.key ? '#7dd3fc' : '#94a3b8',
                      cursor: 'pointer',
                      fontWeight: cloudType === ct.key ? 700 : 400,
                      transition: 'all 0.2s',
                    }}
                  >
                    {ct.label}
                  </button>
                ))}
              </div>
            </div>

            {/* SỐ LƯỢNG TẦNG MÂY KHÍ QUYỂN (SLIDER 1 ĐẾN 6 TẦNG) */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 4 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <label style={{ fontSize: '10px', color: '#94a3b8', display: 'flex', alignItems: 'center', gap: 4 }}>
                  <Layers size={11} color="#38bdf8" /> Số tầng mây khí quyển (Đa Tầng Parallax):
                </label>
                <span style={{ fontSize: '10px', fontWeight: 700, color: '#38bdf8' }}>
                  {cloudLayers} Tầng ({cloudLayers === 1 ? 'Thấp' : cloudLayers <= 3 ? 'Đa tầng vừa' : 'Siêu đa tầng sâu'})
                </span>
              </div>

              <input 
                type="range" 
                min="1" max="6" step="1"
                value={cloudLayers}
                onChange={(e) => onChange({ ...override, cloud_layers: parseInt(e.target.value, 10) })}
                style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
              />

              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8, color: '#64748b' }}>
                <span>1 Tầng</span>
                <span>2 Tầng</span>
                <span>3 Tầng</span>
                <span>4 Tầng</span>
                <span>5 Tầng</span>
                <span>6 Tầng (Cực đại)</span>
              </div>
            </div>


            {/* Cloud Altitude Slider */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2, marginTop: 4 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8' }}>
                <span>Độ cao tầng mây:</span>
                <span>{Math.round(cloudAltitude * 100)}%</span>
              </div>
              <input 
                type="range" 
                min="0.5" max="1.8" step="0.05"
                value={cloudAltitude}
                onChange={(e) => onChange({ ...override, cloud_altitude: parseFloat(e.target.value) })}
                style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
              />
            </div>
          </div>

          {/* 3. MƯA SIÊU CHÂN THỰC (NATURAL WATER DROPLETS & STREAKS) */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6, background: 'rgba(56, 189, 248, 0.05)', padding: 10, borderRadius: 8, border: '1px solid rgba(56, 189, 248, 0.15)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: '11px', color: '#38bdf8', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 5 }}>
                <Droplets size={13} /> CƯỜNG ĐỘ MƯA (To / Nhỏ)
              </label>
              <span style={{ fontSize: '11px', fontWeight: 700, color: rainIntensity > 0 ? '#38bdf8' : '#94a3b8' }}>
                {Math.round(rainIntensity * 100)}%
              </span>
            </div>

            <div style={{ fontSize: '10px', color: rainIntensity > 0 ? '#bae6fd' : '#64748b', fontStyle: 'italic' }}>
              {getRainLevelText(rainIntensity)}
            </div>

            <input 
              type="range" 
              min="0" max="1" step="0.01"
              value={rainIntensity}
              onChange={(e) => onChange({ ...override, rain_intensity: parseFloat(e.target.value) })}
              style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
            />

            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#64748b' }}>
              <span>0% Tắt</span>
              <span>30% Mưa nhỏ</span>
              <span>65% Mưa vừa</span>
              <span>100% Mưa to bão</span>
            </div>
          </div>

          {/* 4. SẤM CHỚP & TIA SÉT 3D CHIẾU RỌI VẬT THỂ (LIGHTNING & THUNDER ENGINE) */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8, background: 'rgba(234, 179, 8, 0.05)', padding: 10, borderRadius: 8, border: '1px solid rgba(234, 179, 8, 0.18)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: '11px', color: '#facc15', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 5 }}>
                <Zap size={13} /> SẤM CHỚP & TIA SÉT 3D
              </label>
              <span style={{ fontSize: '10px', color: '#fef08a', fontWeight: 600 }}>
                {lightningFrequency === 0 ? 'Tự động theo mưa' : `${lightningFrequency.toFixed(1)}s (±25%)`}
              </span>
            </div>

            {/* Slider 1: Thời gian ngẫu nhiên sấm chớp */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#e2e8f0' }}>
                <span>Thời gian ngẫu nhiên xuất hiện sấm sét:</span>
                <span style={{ color: '#facc15', fontWeight: 600 }}>
                  {lightningFrequency === 0 ? 'Tự động theo mưa' : `${lightningFrequency.toFixed(1)}s`}
                </span>
              </div>
              <input 
                type="range" 
                min="0" max="15" step="0.5"
                value={lightningFrequency}
                onChange={(e) => onChange({ ...override, lightning_frequency: parseFloat(e.target.value) })}
                style={{ width: '100%', accentColor: '#facc15', cursor: 'pointer' }}
              />
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8, color: '#94a3b8' }}>
                <span>0s (Tự động)</span>
                <span>1s (Dồn dập)</span>
                <span>5s (Vừa phải)</span>
                <span>15s (Thưa thớt)</span>
              </div>
            </div>

            {/* Slider 2: Nhịp & Độ sáng sấm chớp trên mây */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2, marginTop: 4 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#e2e8f0' }}>
                <span>Nhịp & Ánh chớp trong mây (Cloud Sheet):</span>
                <span style={{ color: '#facc15', fontWeight: 600 }}>
                  {Math.round(lightningCloudIntensity * 100)}%
                </span>
              </div>
              <input 
                type="range" 
                min="0" max="2" step="0.05"
                value={lightningCloudIntensity}
                onChange={(e) => onChange({ ...override, lightning_cloud_intensity: parseFloat(e.target.value) })}
                style={{ width: '100%', accentColor: '#facc15', cursor: 'pointer' }}
              />
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8, color: '#94a3b8' }}>
                <span>0% Tắt chớp mây</span>
                <span>100% Tiêu chuẩn</span>
                <span>200% Cực sáng</span>
              </div>
            </div>

            {/* Slider 3: Tia sét đánh xuống dưới & Chiếu sáng vật thể 3D */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2, marginTop: 4 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#e2e8f0' }}>
                <span>Tia sét đánh xuống & Rọi sáng vật thể 3D:</span>
                <span style={{ color: '#facc15', fontWeight: 600 }}>
                  {Math.round(lightningStrikeIntensity * 100)}%
                </span>
              </div>
              <input 
                type="range" 
                min="0" max="2" step="0.05"
                value={lightningStrikeIntensity}
                onChange={(e) => onChange({ ...override, lightning_strike_intensity: parseFloat(e.target.value) })}
                style={{ width: '100%', accentColor: '#facc15', cursor: 'pointer' }}
              />
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8, color: '#94a3b8' }}>
                <span>0% Chỉ chớp mây</span>
                <span>100% Sét đánh 3D</span>
                <span>200% Sấm sét cực mạnh</span>
              </div>
            </div>
          </div>

          {/* 4. WIND CONTROLS (GIÓ TO HAY NHỎ & HƯỚNG GIÓ THỔI MÂY & MƯA) */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8, background: 'rgba(168, 85, 247, 0.05)', padding: 10, borderRadius: 8, border: '1px solid rgba(168, 85, 247, 0.15)' }}>
            {/* Wind Intensity */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: '11px', color: '#c084fc', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 5 }}>
                <Wind size={13} /> SỨC GIÓ (To / Nhỏ)
              </label>
              <span style={{ fontSize: '11px', fontWeight: 700, color: '#c084fc' }}>
                {Math.round(windIntensity * 100)}%
              </span>
            </div>

            <div style={{ fontSize: '10px', color: '#e9d5ff', fontStyle: 'italic' }}>
              {getWindLevelText(windIntensity)} (Làm mây trôi và hạt mưa bay xiên vút tự nhiên)
            </div>

            <input 
              type="range" 
              min="0" max="1" step="0.01"
              value={windIntensity}
              onChange={(e) => onChange({ ...override, wind_intensity: parseFloat(e.target.value) })}
              style={{ width: '100%', accentColor: '#a855f7', cursor: 'pointer' }}
            />

            {/* Wind Direction */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 4 }}>
              <label style={{ fontSize: '11px', color: '#94a3b8', display: 'flex', alignItems: 'center', gap: 5 }}>
                <Compass size={12} /> Hướng Gió (Góc Mây Trôi & Mưa Tạt)
              </label>
              <span style={{ fontSize: '10px', fontWeight: 600, color: '#cbd5e1' }}>
                {getWindDirectionText(windDirection)}
              </span>
            </div>

            <input 
              type="range" 
              min="0" max="360" step="5"
              value={windDirection}
              onChange={(e) => onChange({ ...override, wind_direction: parseFloat(e.target.value) })}
              style={{ width: '100%', accentColor: '#a855f7', cursor: 'pointer' }}
            />
          </div>

          {/* 5. ATMOSPHERIC FOG */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            <label style={{ fontSize: '11px', color: '#94a3b8', display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}><CloudFog size={12}/> Độ Dày Sương Mù Khí Quyển</span>
              <span>{((fogDensity) * 1000).toFixed(1)}</span>
            </label>
            <input 
              type="range" 
              min="0" max="0.05" step="0.001"
              value={fogDensity}
              onChange={(e) => onChange({ ...override, fog_density: parseFloat(e.target.value) })}
              style={{ width: '100%' }}
            />
          </div>

        </div>
      ) : (
        <div style={{ padding: '24px', textAlign: 'center', color: '#64748b', fontSize: '12px', background: 'rgba(0,0,0,0.2)', borderRadius: '8px' }}>
          Đang sử dụng cấu hình môi trường từ Kịch bản.
          <br/><br/>
          Bật <b>"Tự Do Cấu Hình"</b> ở trên để chọn tag Bầu trời, chỉnh mây đa tầng, mây cụm, mưa rào và sức gió.
        </div>
      )}
    </div>
  );
};
