import React from 'react';
import { 
  CloudRain, Sun, Wind, CloudFog, Compass, Droplets, 
  Cloud, CloudSun, CloudLightning, Sunset, Sunrise, Moon, Layers,
  Image, RotateCw, Upload, Zap, Sparkles, Check
} from 'lucide-react';
import { EnvironmentOverride } from '../types/scene';

interface WeatherControlPanelProps {
  override: EnvironmentOverride;
  onChange: (override: EnvironmentOverride) => void;
}

export const WeatherControlPanel: React.FC<WeatherControlPanelProps> = ({ override, onChange }) => {
  const currentSkyTime = override.sky_time || 'noon';
  const isRainEnabled = override.rain_enabled ?? (override.rain_intensity !== undefined && override.rain_intensity > 0.01);
  const rainIntensity = override.rain_intensity ?? 0;
  const rainDarkness = override.rain_darkness ?? (rainIntensity > 0 ? Math.min(1.0, Math.pow(rainIntensity, 1.4) * 1.25) : 0);
  const windIntensity = override.wind_intensity ?? 0.3;
  const windDirection = override.wind_direction ?? 45;
  const fogDensity = override.fog_density ?? 0.012;
  const cloudCoverage = override.cloud_coverage ?? 0.6;
  const cloudType = override.cloud_type || 'cumulus';
  const cloudLayers = override.cloud_layers ?? 3;
  const cloudAltitude = override.cloud_altitude ?? 1.0;
  const cloudShadowDarkness = override.cloud_shadow_darkness ?? 0.85;
  
  // Lightning State
  const isLightningEnabled = override.lightning_enabled ?? (override.lightning_preset !== 'none');
  const activeLightningPreset = override.lightning_preset || (override.lightning_enabled === false ? 'none' : 'scattered_strikes');
  const lightningFrequency = override.lightning_frequency ?? 0;
  const lightningCloudIntensity = override.lightning_cloud_intensity ?? 1.0;
  const lightningStrikeIntensity = override.lightning_strike_intensity ?? 1.0;

  // Helper text for Rain Level
  const getRainLevelText = (val: number) => {
    if (val <= 0.02) return 'Tắt (Không mưa)';
    if (val <= 0.3) return '🌦️ Mưa phùn nhẹ hạt mềm (Nắng/Trăng vẫn rọi sáng)';
    if (val <= 0.7) return '🌧️ Mưa rào màn nước tự nhiên (Mây đen vừa, trời dịu mát)';
    return '⛈️ Mưa to xối xả - Giông bão cuồng phong (Mây đen kịt, tắt nắng)';
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
    if (val <= 0.35) return '🌤️ Mây rải rác nhẹ (Ít mây)';
    if (val <= 0.75) return '⛅ Mây bồng bềnh điện ảnh (Nhiều mây)';
    return '☁️ Mây phủ kín vòm trời (100% bao phủ)';
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
  const applyPreset = (preset: 'sunny' | 'sunset' | 'night' | 'drizzle' | 'heavy_rain' | 'storm') => {
    switch (preset) {
      case 'sunny':
        onChange({
          ...override,
          enabled: true,
          weather_preset: 'sunny',
          sky_time: 'noon',
          sun_position: 0.5,
          rain_enabled: false,
          rain_intensity: 0,
          rain_darkness: 0,
          wind_intensity: 0.15,
          wind_direction: 45,
          cloud_coverage: 0.30,
          cloud_type: 'cumulus',
          cloud_layers: 2,
          fog_density: 0.008,
          lightning_enabled: false,
          lightning_preset: 'none',
          lightning_frequency: 0,
        });
        break;
      case 'sunset':
        onChange({
          ...override,
          enabled: true,
          weather_preset: 'sunset',
          sky_time: 'sunset',
          sun_position: 0.85,
          rain_enabled: false,
          rain_intensity: 0,
          rain_darkness: 0,
          wind_intensity: 0.25,
          wind_direction: 90,
          cloud_coverage: 0.55,
          cloud_type: 'sunset_glow',
          cloud_layers: 3,
          fog_density: 0.012,
          lightning_enabled: false,
          lightning_preset: 'none',
          lightning_frequency: 0,
        });
        break;
      case 'night':
        onChange({
          ...override,
          enabled: true,
          weather_preset: 'night',
          sky_time: 'night',
          sun_position: 0.95,
          rain_enabled: false,
          rain_intensity: 0,
          rain_darkness: 0,
          wind_intensity: 0.20,
          wind_direction: 45,
          cloud_coverage: 0.40,
          cloud_type: 'cumulus',
          cloud_layers: 2,
          fog_density: 0.010,
          lightning_enabled: false,
          lightning_preset: 'none',
          lightning_frequency: 0,
        });
        break;
      case 'drizzle':
        onChange({
          ...override,
          enabled: true,
          weather_preset: 'drizzle',
          sky_time: 'noon',
          sun_position: 0.48,
          rain_enabled: true,
          rain_intensity: 0.25,
          rain_darkness: 0.18,
          wind_intensity: 0.35,
          wind_direction: 45,
          cloud_coverage: 0.55,
          cloud_type: 'cumulus',
          cloud_layers: 2,
          fog_density: 0.014,
          lightning_enabled: false,
          lightning_preset: 'none',
          lightning_frequency: 0,
        });
        break;
      case 'heavy_rain':
        onChange({
          ...override,
          enabled: true,
          weather_preset: 'heavy_rain',
          sky_time: 'overcast',
          sun_position: 0.4,
          rain_enabled: true,
          rain_intensity: 0.70,
          rain_darkness: 0.65,
          wind_intensity: 0.65,
          wind_direction: 60,
          cloud_coverage: 0.88,
          cloud_type: 'cumulonimbus',
          cloud_layers: 4,
          fog_density: 0.024,
          lightning_enabled: true,
          lightning_preset: 'scattered_strikes',
          lightning_frequency: 4.5,
          lightning_cloud_intensity: 1.0,
          lightning_strike_intensity: 1.0,
        });
        break;
      case 'storm':
        onChange({
          ...override,
          enabled: true,
          weather_preset: 'storm',
          sky_time: 'overcast',
          sun_position: 0.1,
          rain_enabled: true,
          rain_intensity: 1.0,
          rain_darkness: 1.0,
          wind_intensity: 0.95,
          wind_direction: 75,
          cloud_coverage: 1.0,
          cloud_type: 'cumulonimbus',
          cloud_layers: 6,
          fog_density: 0.035,
          lightning_enabled: true,
          lightning_preset: 'heavy_storm',
          lightning_frequency: 2.0,
          lightning_cloud_intensity: 1.5,
          lightning_strike_intensity: 1.5,
        });
        break;
    }
  };

  const activeWeatherPreset = override.weather_preset || (
    override.sky_time === 'sunset' ? 'sunset' :
    override.sky_time === 'night' ? 'night' :
    (override.rain_intensity && override.rain_intensity >= 0.85) ? 'storm' :
    (override.rain_intensity && override.rain_intensity >= 0.45) ? 'heavy_rain' :
    (override.rain_intensity && override.rain_intensity > 0.05) ? 'drizzle' :
    'sunny'
  );

  return (
    <div className="panel" style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
      {/* Panel Header */}
      <div className="panel-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h3 style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: 13, margin: 0 }}>
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
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: '11px', color: '#94a3b8', fontWeight: 600 }}>Cài đặt nhanh thời tiết</label>
              <span style={{ fontSize: '9px', color: '#cbd5e1' }}>
                {activeWeatherPreset === 'custom' ? '⚙️ Đang tùy chỉnh' : ''}
              </span>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 5 }}>
              {[
                { key: 'sunny', label: '☀️ Nắng Đẹp', desc: 'Nắng đẹp quang đãng', activeColor: '#38bdf8' },
                { key: 'sunset', label: '🌅 Hoàng Hôn', desc: 'Hoàng hôn chiều tà', activeColor: '#f97316' },
                { key: 'night', label: '🌙 Ban Đêm', desc: 'Đêm trăng thanh', activeColor: '#818cf8' },
                { key: 'drizzle', label: '🌦️ Mưa Phùn', desc: 'Mưa phùn hạt nhỏ', activeColor: '#0ea5e9' },
                { key: 'heavy_rain', label: '🌧️ Mưa Rào', desc: 'Mưa rào hạt dày', activeColor: '#0284c7' },
                { key: 'storm', label: '⛈️ Bão Lớn', desc: 'Bão giông cuồng phong', activeColor: '#ef4444' },
              ].map((p) => {
                const isActive = (override.weather_preset || activeWeatherPreset) === p.key;
                return (
                  <button
                    key={p.key}
                    type="button"
                    style={{
                      padding: '7px 4px',
                      fontSize: 10,
                      fontWeight: isActive ? 700 : 500,
                      textAlign: 'center',
                      borderRadius: 6,
                      border: `1px solid ${isActive ? p.activeColor : 'rgba(255, 255, 255, 0.1)'}`,
                      background: isActive 
                        ? `linear-gradient(135deg, ${p.activeColor}38, ${p.activeColor}18)` 
                        : 'rgba(30, 41, 59, 0.6)',
                      color: isActive ? '#ffffff' : '#94a3b8',
                      boxShadow: isActive ? `0 0 10px ${p.activeColor}55` : 'none',
                      cursor: 'pointer',
                      display: 'flex',
                      flexDirection: 'column',
                      alignItems: 'center',
                      justifyContent: 'center',
                      gap: 2,
                      transition: 'all 0.2s cubic-bezier(0.4, 0, 0.2, 1)',
                    }}
                    onClick={() => applyPreset(p.key as any)}
                    title={p.desc}
                  >
                    <span>{p.label}</span>
                    {isActive && (
                      <span style={{ fontSize: '8px', color: p.activeColor, fontWeight: 800 }}>
                        ● Đang Bật
                      </span>
                    )}
                  </button>
                );
              })}
            </div>
          </div>

          <div style={{ borderTop: '1px solid rgba(255, 255, 255, 0.08)' }} />

          {/* ══════════════════════════════════════════════════ */}
          {/* 1. KHUNG THỜI GIAN & BẦU TRỜI (TIME OF DAY & SUN)   */}
          {/* ══════════════════════════════════════════════════ */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8, background: 'rgba(255, 255, 255, 0.02)', padding: 10, borderRadius: 8, border: '1px solid rgba(255, 255, 255, 0.08)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: '11px', color: '#fbbf24', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 5 }}>
                <Sun size={13} color="#f59e0b" /> 1. KHUNG THỜI GIAN & BẦU TRỜI
              </label>
            </div>

            {/* Real-time Sun Lighting & Shadows on Map Toggle */}
            <div style={{ 
              display: 'flex', 
              justifyContent: 'space-between', 
              alignItems: 'center', 
              background: (override.map_dynamic_lighting ?? true) ? 'rgba(245, 158, 11, 0.12)' : 'rgba(30, 41, 59, 0.5)', 
              padding: '7px 9px', 
              borderRadius: 6, 
              border: `1px solid ${(override.map_dynamic_lighting ?? true) ? '#f59e0b' : 'rgba(255, 255, 255, 0.1)'}` 
            }}>
              <div style={{ display: 'flex', flexDirection: 'column' }}>
                <span style={{ fontSize: 10, fontWeight: 700, color: (override.map_dynamic_lighting ?? true) ? '#fef08a' : '#cbd5e1' }}>
                  Ánh sáng & Bóng đổ thời gian thực trên Map
                </span>
                <span style={{ fontSize: 9, color: (override.map_dynamic_lighting ?? true) ? '#fed7aa' : '#64748b' }}>
                  {(override.map_dynamic_lighting ?? true) ? '● Bật: Nhận hướng mặt trời & bóng râm mây' : '○ Tắt: Giữ ánh sáng nướng sẵn gốc của Map'}
                </span>
              </div>
              <label style={{ display: 'flex', alignItems: 'center', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={override.map_dynamic_lighting ?? true}
                  onChange={(e) => onChange({ ...override, map_dynamic_lighting: e.target.checked })}
                  style={{ accentColor: '#f59e0b', width: 15, height: 15, cursor: 'pointer' }}
                />
              </label>
            </div>

            {/* 3D Local Lights (PointLights / SpotLights / Lanterns / Chandeliers) */}
            <div style={{ 
              display: 'flex', 
              flexDirection: 'column',
              gap: 6,
              background: (override.point_lights_enabled ?? true) ? 'rgba(234, 179, 8, 0.09)' : 'rgba(30, 41, 59, 0.5)', 
              padding: '7px 9px', 
              borderRadius: 6, 
              border: `1px solid ${(override.point_lights_enabled ?? true) ? '#eab308' : 'rgba(255, 255, 255, 0.1)'}` 
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div style={{ display: 'flex', flexDirection: 'column' }}>
                  <span style={{ fontSize: 10, fontWeight: 700, color: (override.point_lights_enabled ?? true) ? '#fef08a' : '#cbd5e1' }}>
                    💡 Nguồn Sáng 3D Cục Bộ (Đèn lồng / Đèn chùm / Đuốc)
                  </span>
                  <span style={{ fontSize: 9, color: (override.point_lights_enabled ?? true) ? '#fef9c3' : '#64748b' }}>
                    {(override.point_lights_enabled ?? true) ? '● Bật: Phát sáng 3D rọi sàn & vật thể xung quanh' : '○ Tắt: Tắt toàn bộ đèn 3D'}
                  </span>
                </div>
                <label style={{ display: 'flex', alignItems: 'center', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={override.point_lights_enabled ?? true}
                    onChange={(e) => onChange({ ...override, point_lights_enabled: e.target.checked })}
                    style={{ accentColor: '#eab308', width: 15, height: 15, cursor: 'pointer' }}
                  />
                </label>
              </div>

              {(override.point_lights_enabled ?? true) && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 3, marginTop: 2 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9, color: '#fef08a' }}>
                    <span>Cường độ sáng đèn 3D:</span>
                    <span style={{ fontWeight: 700 }}>{Math.round((override.point_lights_intensity ?? 1.0) * 100)}%</span>
                  </div>
                  <input
                    type="range"
                    min="0.1" max="2.5" step="0.05"
                    value={override.point_lights_intensity ?? 1.0}
                    onChange={(e) => onChange({ ...override, point_lights_intensity: parseFloat(e.target.value) })}
                    style={{ width: '100%', accentColor: '#eab308', cursor: 'pointer' }}
                  />
                </div>
              )}
            </div>

            {/* Sky Time Tags */}
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
                      backgroundColor: isActive ? 'rgba(245, 158, 11, 0.25)' : 'rgba(30, 41, 59, 0.6)',
                      borderColor: isActive ? '#f59e0b' : 'rgba(255, 255, 255, 0.1)',
                      color: isActive ? '#fef08a' : '#94a3b8',
                      boxShadow: isActive ? '0 0 8px rgba(245, 158, 11, 0.3)' : 'none',
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

            {/* Sun Position Slider */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 3, marginTop: 4 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#fed7aa' }}>
                <span>Quỹ đạo Mặt trời / Mặt trăng:</span>
                <span style={{ fontWeight: 600, color: '#fb923c' }}>
                  {(() => {
                    const pos = override.sun_position ?? 0.5;
                    if (pos <= 0.24) return `🌅 Đông (Bình Minh ${Math.round(pos * 100)}%)`;
                    if (pos <= 0.68) return `☀️ Đỉnh Trời (Giữa Trưa ${Math.round(pos * 100)}%)`;
                    if (pos <= 0.88) return `🌇 Tây (Hoàng Hôn ${Math.round(pos * 100)}%)`;
                    return `🌙 Đêm Tối (${Math.round(pos * 100)}%)`;
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
            </div>

            {/* 360° Skybox Texture Presets */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 4 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontSize: 10, color: '#94a3b8' }}>Skybox 360° Môi Trường:</span>
                <span style={{ fontSize: 9, color: '#64748b' }}>{override.skybox_type || 'none'}</span>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 3 }}>
                {[
                  { key: 'none', label: 'Thủ Công' },
                  { key: 'day', label: 'Nắng Ngày' },
                  { key: 'morning', label: 'Bình Minh' },
                  { key: 'night', label: 'Đêm Sao' },
                ].map((sb) => {
                  const isSbActive = (override.skybox_type || 'none') === sb.key;
                  return (
                    <button
                      key={sb.key}
                      type="button"
                      style={{
                        padding: '4px',
                        fontSize: 9,
                        borderRadius: 4,
                        border: `1px solid ${isSbActive ? '#f59e0b' : 'rgba(255,255,255,0.08)'}`,
                        background: isSbActive ? 'rgba(245, 158, 11, 0.2)' : 'rgba(0,0,0,0.3)',
                        color: isSbActive ? '#fef08a' : '#94a3b8',
                        boxShadow: isSbActive ? '0 0 6px rgba(245, 158, 11, 0.25)' : 'none',
                        cursor: 'pointer',
                      }}
                      onClick={() => onChange({ ...override, skybox_type: sb.key as any })}
                    >
                      {sb.label}
                    </button>
                  );
                })}
              </div>
            </div>
          </div>

          {/* ══════════════════════════════════════════════════ */}
          {/* 2. HỆ THỐNG MÂY BỒNG BỀNH & ĐỔ BÓNG 3D               */}
          {/* ══════════════════════════════════════════════════ */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8, background: 'rgba(56, 189, 248, 0.04)', padding: 10, borderRadius: 8, border: '1px solid rgba(56, 189, 248, 0.15)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: '11px', color: '#38bdf8', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 5 }}>
                <Cloud size={13} color="#38bdf8" /> 2. MÂY BỒNG BỀNH & ĐỔ BÓNG 3D
              </label>
              <span style={{ fontSize: '10px', color: '#bae6fd', fontWeight: 600 }}>
                {cloudLayers} Tầng ({Math.round(cloudCoverage * 100)}%)
              </span>
            </div>

            {/* Cloud Type Tags */}
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
              {[
                { key: 'cumulus', label: '☁️ Mây Bồng Bềnh', desc: 'Cụm mây tích 3D tự nhiên' },
                { key: 'cumulonimbus', label: '⛈️ Mây Vũ Tích', desc: 'Mây bão dày cuồn cuộn' },
                { key: 'multi_layered', label: '⛅ Mây Đa Tầng', desc: 'Nhiều tầng mây đan xen' },
                { key: 'sunset_glow', label: '🌅 Mây Hoàng Hôn', desc: 'Mây ráng hồng trải rộng' },
              ].map((c) => {
                const isActive = cloudType === c.key;
                return (
                  <button
                    key={c.key}
                    type="button"
                    className={`btn-secondary ${isActive ? 'active' : ''}`}
                    style={{
                      padding: '4px 8px',
                      fontSize: 10,
                      fontWeight: isActive ? 700 : 500,
                      borderRadius: 14,
                      backgroundColor: isActive ? 'rgba(56, 189, 248, 0.3)' : 'rgba(30, 41, 59, 0.6)',
                      borderColor: isActive ? '#38bdf8' : 'rgba(255, 255, 255, 0.1)',
                      color: isActive ? '#ffffff' : '#94a3b8',
                      boxShadow: isActive ? '0 0 8px rgba(56, 189, 248, 0.3)' : 'none',
                      cursor: 'pointer',
                    }}
                    onClick={() => onChange({ ...override, cloud_type: c.key as any })}
                    title={c.desc}
                  >
                    <span>{c.label}</span>
                  </button>
                );
              })}
            </div>

            {/* Cloud Coverage Slider */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#e2e8f0' }}>
                <span>Độ phủ mây bầu trời:</span>
                <span style={{ color: '#38bdf8', fontWeight: 600 }}>{Math.round(cloudCoverage * 100)}%</span>
              </div>
              <div style={{ fontSize: 9, color: '#94a3b8', fontStyle: 'italic' }}>
                {getCloudCoverageText(cloudCoverage)}
              </div>
              <input 
                type="range" 
                min="0" max="1" step="0.01"
                value={cloudCoverage}
                onChange={(e) => onChange({ ...override, cloud_coverage: parseFloat(e.target.value), weather_preset: 'custom' })}
                style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
              />
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8, color: '#64748b' }}>
                <span>0% Trong xanh</span>
                <span>35% Mây vừa</span>
                <span>70% Nhiều mây</span>
                <span>100% Phủ kín vòm trời</span>
              </div>
            </div>

            {/* Cloud Layers Slider */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2, marginTop: 4 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#e2e8f0' }}>
                <span>Số tầng mây (65m – 120m):</span>
                <span style={{ color: '#38bdf8', fontWeight: 600 }}>{cloudLayers} Tầng</span>
              </div>
              <input 
                type="range" 
                min="1" max="6" step="1"
                value={cloudLayers}
                onChange={(e) => onChange({ ...override, cloud_layers: parseInt(e.target.value, 10), weather_preset: 'custom' })}
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
                <span>Độ cao trần mây:</span>
                <span>{Math.round(cloudAltitude * 100)}% ({Math.round(cloudAltitude * 90)}m)</span>
              </div>
              <input 
                type="range" 
                min="0.6" max="1.6" step="0.05"
                value={cloudAltitude}
                onChange={(e) => onChange({ ...override, cloud_altitude: parseFloat(e.target.value), weather_preset: 'custom' })}
                style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
              />
            </div>

            {/* Cloud Shadow Darkness on 3D Objects Slider */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2, marginTop: 4 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#e2e8f0' }}>
                <span>Độ đậm bóng râm mây (Bóng râm trên vật thể 3D):</span>
                <span style={{ color: '#38bdf8', fontWeight: 600 }}>{Math.round(cloudShadowDarkness * 100)}%</span>
              </div>
              <div style={{ fontSize: 9, color: '#94a3b8' }}>
                {cloudShadowDarkness <= 0.2
                  ? '🌤️ Rất dịu / Thoang thoảng (Tia nắng chiếu xuyên qua)'
                  : cloudShadowDarkness <= 0.55
                    ? '⛅ Bóng râm nhẹ tự nhiên (Dịu mát cảnh quan)'
                    : cloudShadowDarkness <= 0.88
                      ? '☁️ Bóng râm điện ảnh chân thực (Tương phản rõ nét - Mặc định)'
                      : '⛈️ Bóng râm sâu đậm (Tương phản gắt / Trời râm mát đậm)'}
              </div>
              <input 
                type="range" 
                min="0" max="1" step="0.02"
                value={cloudShadowDarkness}
                onChange={(e) => onChange({ ...override, cloud_shadow_darkness: parseFloat(e.target.value), weather_preset: 'custom' })}
                style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
              />
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8, color: '#64748b' }}>
                <span>0% Tắt bóng râm</span>
                <span>40% Nhẹ nhàng</span>
                <span>85% Mặc định</span>
                <span>100% Đậm sâu</span>
              </div>
            </div>
          </div>

          {/* ══════════════════════════════════════════════════════════════════ */}
          {/* 3. CHẾ ĐỘ MƯA & GIÔNG BÃO SẤM SÉT (PRECIPITATION & THUNDERSTORM)   */}
          {/* ══════════════════════════════════════════════════════════════════ */}
          <div style={{ 
            display: 'flex', 
            flexDirection: 'column', 
            gap: 8, 
            background: isRainEnabled ? 'rgba(56, 189, 248, 0.08)' : 'rgba(255, 255, 255, 0.02)', 
            padding: 10, 
            borderRadius: 8, 
            border: `1px solid ${isRainEnabled ? 'rgba(56, 189, 248, 0.35)' : 'rgba(255, 255, 255, 0.08)'}`,
            boxShadow: isRainEnabled ? '0 0 12px rgba(56, 189, 248, 0.12)' : 'none',
            transition: 'all 0.25s'
          }}>
            {/* Precipitation Header & Main Toggle */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: '11px', color: isRainEnabled ? '#38bdf8' : '#94a3b8', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 5 }}>
                <Droplets size={13} color={isRainEnabled ? '#38bdf8' : '#64748b'} /> 3. CHẾ ĐỘ MƯA, GIÔNG BÃO & SẤM SÉT
              </label>

              <button
                type="button"
                style={{
                  padding: '4px 12px',
                  fontSize: 10,
                  fontWeight: 700,
                  borderRadius: 14,
                  border: `1px solid ${isRainEnabled ? '#38bdf8' : '#475569'}`,
                  background: isRainEnabled 
                    ? 'linear-gradient(135deg, rgba(56, 189, 248, 0.35), rgba(14, 165, 233, 0.2))' 
                    : 'rgba(0,0,0,0.3)',
                  color: isRainEnabled ? '#ffffff' : '#64748b',
                  boxShadow: isRainEnabled ? '0 0 10px rgba(56, 189, 248, 0.35)' : 'none',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 5,
                  transition: 'all 0.2s',
                }}
                onClick={() => {
                  const nextRain = !isRainEnabled;
                  if (nextRain) {
                    const nextIntensity = rainIntensity > 0 ? rainIntensity : 0.45;
                    const nextCoverage = Math.max(0.65, override.cloud_coverage ?? 0.65);
                    const nextDarkness = rainDarkness > 0 ? rainDarkness : 0.45;
                    onChange({
                      ...override,
                      rain_enabled: true,
                      rain_intensity: nextIntensity,
                      cloud_coverage: nextCoverage,
                      rain_darkness: nextDarkness,
                      lightning_enabled: override.lightning_enabled ?? true,
                    });
                  } else {
                    onChange({
                      ...override,
                      rain_enabled: false,
                      rain_intensity: 0,
                      lightning_enabled: false,
                    });
                  }
                }}
              >
                {isRainEnabled ? '🌧️ ĐANG BẬT MƯA' : 'TẮT MƯA'}
              </button>
            </div>

            {isRainEnabled ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 2 }}>
                {/* 3.1 Rain Intensity Slider (Auto-adjusts coverage & darkness) */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#e2e8f0' }}>
                    <span>Cường độ hạt mưa (To / Nhỏ):</span>
                    <span style={{ color: '#38bdf8', fontWeight: 600 }}>{Math.round(rainIntensity * 100)}%</span>
                  </div>
                  <div style={{ fontSize: 9, color: '#bae6fd', fontStyle: 'italic' }}>
                    {getRainLevelText(rainIntensity)}
                  </div>
                  <input 
                    type="range" 
                    min="0.05" max="1" step="0.01"
                    value={rainIntensity}
                    onChange={(e) => {
                      const val = parseFloat(e.target.value);
                      let autoCoverage: number;
                      let autoDarkness: number;

                      if (val <= 0.35) {
                        autoCoverage = 0.35 + (val / 0.35) * 0.25; // 0.35 -> 0.60
                        autoDarkness = (val / 0.35) * 0.28;        // 0.00 -> 0.28
                      } else if (val <= 0.8) {
                        autoCoverage = 0.60 + ((val - 0.35) / 0.45) * 0.35; // 0.60 -> 0.95
                        autoDarkness = 0.28 + ((val - 0.35) / 0.45) * 0.62; // 0.28 -> 0.90
                      } else {
                        autoCoverage = 1.0; // 100% vòm trời
                        autoDarkness = 1.0; // Đen kịt giông tố
                      }

                      onChange({
                        ...override,
                        rain_intensity: val,
                        cloud_coverage: parseFloat(autoCoverage.toFixed(2)),
                        rain_darkness: parseFloat(autoDarkness.toFixed(2)),
                      });
                    }}
                    style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
                  />
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8, color: '#64748b' }}>
                    <span>10% Mưa phùn</span>
                    <span>45% Mưa vừa</span>
                    <span>75% Mưa to</span>
                    <span>100% Bão xối xả (Phủ kín mây đen)</span>
                  </div>
                </div>

                {/* 3.2 Rain Cloud Darkness Slider */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#e2e8f0' }}>
                    <span>Độ đen của mây khi mưa:</span>
                    <span style={{ color: '#38bdf8', fontWeight: 600 }}>{Math.round(rainDarkness * 100)}%</span>
                  </div>
                  <div style={{ fontSize: 9, color: '#94a3b8' }}>
                    {rainDarkness <= 0.25 
                      ? '🌤️ Mây nhạt màu / bồng bềnh nhẹ, tia nắng vẫn chiếu qua dịu dàng'
                      : rainDarkness <= 0.65 
                        ? '⛅ Mây xám than chì, che bớt tia nắng chói' 
                        : '⛈️ Mây đen kịt giông tố, che khuất hoàn toàn tia nắng'}
                  </div>
                  <input 
                    type="range" 
                    min="0" max="1" step="0.05"
                    value={rainDarkness}
                    onChange={(e) => onChange({ ...override, rain_darkness: parseFloat(e.target.value), weather_preset: 'custom' })}
                    style={{ width: '100%', accentColor: '#38bdf8', cursor: 'pointer' }}
                  />
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8, color: '#64748b' }}>
                    <span>0% Mây nhạt dịu</span>
                    <span>50% Xám than</span>
                    <span>100% Đen kịt giông bão</span>
                  </div>
                </div>

                {/* 3.2b Rain Collision Quality Slider (Performance) */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#e2e8f0' }}>
                    <span>⚡ Chất lượng va chạm mưa:</span>
                    <span style={{ color: '#f59e0b', fontWeight: 600 }}>{override.rain_collision_quality ?? 2}/10</span>
                  </div>
                  <div style={{ fontSize: 9, color: '#94a3b8' }}>
                    {(override.rain_collision_quality ?? 2) === 0
                      ? '🚀 Tắt va chạm — FPS cao nhất, mưa xuyên qua mọi vật'
                      : (override.rain_collision_quality ?? 2) <= 2
                        ? '⚖️ Tiết kiệm — 2 raycasts/frame, đủ mượt cho card yếu'
                        : (override.rain_collision_quality ?? 2) <= 5
                          ? '🎯 Cân bằng — grid fill nhanh, mưa chính xác hơn'
                          : '💎 Chất lượng cao — 10 raycasts/frame, cần card khỏe'}
                  </div>
                  <input 
                    type="range" 
                    min="0" max="10" step="1"
                    value={override.rain_collision_quality ?? 2}
                    onChange={(e) => onChange({ ...override, rain_collision_quality: parseInt(e.target.value), weather_preset: 'custom' })}
                    style={{ width: '100%', accentColor: '#f59e0b', cursor: 'pointer' }}
                  />
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8, color: '#64748b' }}>
                    <span>0 Tắt (nhanh nhất)</span>
                    <span>2 Mặc định</span>
                    <span>5 Cân bằng</span>
                    <span>10 Max (card khỏe)</span>
                  </div>
                </div>

                {/* 3.3 SẤM CHỚP & TIA SÉT (LIGHTNING PRESETS & FINE-TUNING) */}
                <div style={{ 
                  display: 'flex', 
                  flexDirection: 'column', 
                  gap: 8, 
                  background: 'rgba(234, 179, 8, 0.06)', 
                  padding: 10, 
                  borderRadius: 8, 
                  border: '1px solid rgba(234, 179, 8, 0.25)',
                  marginTop: 4
                }}>
                  {/* Lightning Header */}
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <label style={{ fontSize: '11px', color: '#facc15', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 5 }}>
                      <Zap size={13} color="#facc15" /> SẤM CHỚP & TIA SÉT 3D
                    </label>
                    <span style={{ fontSize: '9px', color: '#fef08a', fontWeight: 600 }}>
                      {activeLightningPreset === 'none' ? '🚫 Đang tắt sét' : (lightningFrequency === 0 ? '⚡ Tự động theo mưa' : `⚡ ${lightningFrequency.toFixed(1)}s`)}
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
                              <span style={{ fontSize: '8px', color: '#facc15', fontWeight: 800, display: 'flex', alignItems: 'center', gap: 2 }}>
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
                          min="0" max="15" step="0.5"
                          value={lightningFrequency}
                          onChange={(e) => onChange({ 
                            ...override, 
                            lightning_frequency: parseFloat(e.target.value),
                            lightning_preset: 'custom',
                            lightning_enabled: true
                          })}
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
                          min="0" max="2" step="0.05"
                          value={lightningCloudIntensity}
                          onChange={(e) => onChange({ 
                            ...override, 
                            lightning_cloud_intensity: parseFloat(e.target.value),
                            lightning_preset: 'custom',
                            lightning_enabled: true
                          })}
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
                          min="0" max="2" step="0.05"
                          value={lightningStrikeIntensity}
                          onChange={(e) => onChange({ 
                            ...override, 
                            lightning_strike_intensity: parseFloat(e.target.value),
                            lightning_preset: 'custom',
                            lightning_enabled: true
                          })}
                          style={{ width: '100%', accentColor: '#facc15', cursor: 'pointer' }}
                        />
                      </div>
                    </div>
                  )}
                </div>
              </div>
            ) : (
              <div style={{ fontSize: 10, color: '#64748b', fontStyle: 'italic', padding: '2px 0' }}>
                Trời khô ráo, không có mưa (Nhấn <b>"TẮT MƯA"</b> ở góc phải để bật mưa và sấm sét).
              </div>
            )}
          </div>

          {/* ══════════════════════════════════════════════════ */}
          {/* 4. SỨC GIÓ & HƯỚNG GIÓ THỔI (WIND DYNAMICS)         */}
          {/* ══════════════════════════════════════════════════ */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8, background: 'rgba(168, 85, 247, 0.04)', padding: 10, borderRadius: 8, border: '1px solid rgba(168, 85, 247, 0.15)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: '11px', color: '#c084fc', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 5 }}>
                <Wind size={13} color="#a855f7" /> 4. SỨC GIÓ & HƯỚNG GIÓ THỔI
              </label>
              <span style={{ fontSize: '10px', fontWeight: 700, color: '#c084fc' }}>
                {Math.round(windIntensity * 100)}% ({getWindDirectionText(windDirection)})
              </span>
            </div>

            {/* Wind Intensity Slider */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#e2e8f0' }}>
                <span>Sức gió (Tốc độ mây trôi & Mưa tạt xiên):</span>
                <span style={{ color: '#c084fc', fontWeight: 600 }}>{Math.round(windIntensity * 100)}%</span>
              </div>
              <div style={{ fontSize: 9, color: '#e9d5ff', fontStyle: 'italic' }}>
                {getWindLevelText(windIntensity)} (Vận tốc ~{Math.round(windIntensity * 32)} m/s)
              </div>
              <input 
                type="range" 
                min="0" max="1" step="0.01"
                value={windIntensity}
                onChange={(e) => onChange({ ...override, wind_intensity: parseFloat(e.target.value) })}
                style={{ width: '100%', accentColor: '#a855f7', cursor: 'pointer' }}
              />
            </div>

            {/* Wind Direction Slider */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2, marginTop: 4 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8' }}>
                <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}><Compass size={11} /> Hướng gió (Góc 360°):</span>
                <span style={{ color: '#e2e8f0', fontWeight: 600 }}>{getWindDirectionText(windDirection)}</span>
              </div>
              <input 
                type="range" 
                min="0" max="360" step="5"
                value={windDirection}
                onChange={(e) => onChange({ ...override, wind_direction: parseFloat(e.target.value) })}
                style={{ width: '100%', accentColor: '#a855f7', cursor: 'pointer' }}
              />
            </div>
          </div>

          {/* ══════════════════════════════════════════════════ */}
          {/* 5. SƯƠNG MÙ KHÍ QUYỂN (ATMOSPHERIC FOG)              */}
          {/* ══════════════════════════════════════════════════ */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4, background: 'rgba(255, 255, 255, 0.02)', padding: 10, borderRadius: 8, border: '1px solid rgba(255, 255, 255, 0.08)' }}>
            <label style={{ fontSize: '11px', color: '#94a3b8', display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ display: 'flex', alignItems: 'center', gap: '4px', fontWeight: 700, color: '#cbd5e1' }}><CloudFog size={12}/> 5. SƯƠNG MÙ KHÍ QUYỂN</span>
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
