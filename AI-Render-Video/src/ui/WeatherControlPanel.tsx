import React from 'react';
import { CloudRain, Sun, Wind, CloudFog } from 'lucide-react';
import { EnvironmentOverride } from '../types/scene';

interface WeatherControlPanelProps {
  override: EnvironmentOverride;
  onChange: (override: EnvironmentOverride) => void;
}

export const WeatherControlPanel: React.FC<WeatherControlPanelProps> = ({ override, onChange }) => {
  return (
    <div className="panel" style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
      <div className="panel-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h3 style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <CloudRain size={16} className="icon-glow" /> Thời Tiết & Môi Trường
        </h3>
        
        <label 
          style={{ 
            display: 'flex', 
            alignItems: 'center', 
            gap: '8px', 
            cursor: 'pointer', 
            fontSize: '12px',
            background: override.enabled ? 'rgba(56, 189, 248, 0.15)' : 'rgba(30, 41, 59, 0.8)',
            padding: '4px 10px',
            borderRadius: '16px',
            border: `1px solid ${override.enabled ? '#38bdf8' : '#475569'}`,
            color: override.enabled ? '#38bdf8' : '#94a3b8',
            transition: 'all 0.2s',
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
          {override.enabled ? 'Đang Bật' : 'Đã Tắt'}
        </label>
      </div>

      {override.enabled ? (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
          
          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            <label style={{ fontSize: '12px', color: '#94a3b8' }}>Chế độ bầu trời</label>
            <select 
              className="form-input" 
              value={override.sky_time}
              onChange={(e) => onChange({ ...override, sky_time: e.target.value as any })}
            >
              <option value="sunrise">Bình Minh (Sunrise)</option>
              <option value="noon">Giữa Trưa (Noon)</option>
              <option value="sunset">Hoàng Hôn (Sunset)</option>
              <option value="night">Ban Đêm (Night)</option>
            </select>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            <label style={{ fontSize: '12px', color: '#94a3b8', display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}><Sun size={12}/> Vị Trí Mặt Trời</span>
              <span>{Math.round((override.sun_position ?? 0.5) * 100)}%</span>
            </label>
            <input 
              type="range" 
              min="0" max="1" step="0.01"
              value={override.sun_position ?? 0.5}
              onChange={(e) => onChange({ ...override, sun_position: parseFloat(e.target.value) })}
              style={{ width: '100%' }}
            />
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            <label style={{ fontSize: '12px', color: '#94a3b8', display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}><CloudFog size={12}/> Sương Mù</span>
              <span>{((override.fog_density ?? 0.012) * 1000).toFixed(1)}</span>
            </label>
            <input 
              type="range" 
              min="0" max="0.05" step="0.001"
              value={override.fog_density ?? 0.012}
              onChange={(e) => onChange({ ...override, fog_density: parseFloat(e.target.value) })}
              style={{ width: '100%' }}
            />
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            <label style={{ fontSize: '12px', color: '#94a3b8', display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}><Wind size={12}/> Sức Gió</span>
              <span>{Math.round((override.wind_intensity ?? 0.3) * 100)}%</span>
            </label>
            <input 
              type="range" 
              min="0" max="1" step="0.01"
              value={override.wind_intensity ?? 0.3}
              onChange={(e) => onChange({ ...override, wind_intensity: parseFloat(e.target.value) })}
              style={{ width: '100%' }}
            />
          </div>

        </div>
      ) : (
        <div style={{ padding: '24px', textAlign: 'center', color: '#64748b', fontSize: '13px', background: 'rgba(0,0,0,0.2)', borderRadius: '8px' }}>
          Đang sử dụng cấu hình môi trường từ Kịch bản JSON.
          <br/><br/>
          Bật "Tự Do Cấu Hình" để ghi đè thời tiết.
        </div>
      )}
    </div>
  );
};
