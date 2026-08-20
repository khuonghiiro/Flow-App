import React, { useState } from 'react';
import { 
  Sun, Zap, Flame, Lightbulb, Trash2, Plus, Copy, Check, 
  Eye, Sliders, Target, Sparkles, Compass, ShieldAlert,
  Flashlight, Lamp, Trees, Radio, Palette
} from 'lucide-react';
import { MasterSceneConfig, PropLightConfig } from '../types/scene';
import { PlacedProp } from '../types/map_preset';

interface LightingStudioPanelProps {
  scene: MasterSceneConfig;
  onUpdateScene: (updated: MasterSceneConfig) => void;
  selectedObjectId?: string | null;
  onFocusObject?: (pos: [number, number, number]) => void;
}

const DEFAULT_VILLAGE_PROPS: PlacedProp[] = [
  { id: 'placed_tree_oak_01', asset_path: 'props/nature/tree_sakura.glb', position: [4, 0, -3], type: 'nature', is_obstacle: true },
  { id: 'placed_chair_01', asset_path: 'props/furniture/chair_wooden.glb', position: [-4, 0, -2], type: 'furniture', is_obstacle: true },
  { id: 'placed_farm_plot_01', asset_path: 'props/nature/farm_plot.glb', position: [0, 0, -5], type: 'nature', is_obstacle: true },
  { id: 'placed_duck_01', asset_path: 'props/nature/duck.glb', position: [0.8, 0, -2.5], type: 'animal', is_obstacle: false },
  { id: 'placed_lantern_stand_01', asset_path: 'props/furniture/lantern_stand.glb', position: [-3.2, 0, -2.0], type: 'furniture', is_obstacle: true },
];

const PRESET_LIGHT_COLORS = [
  { name: 'Lửa Đuốc Ấm', hex: '#ff6611', icon: '🔥' },
  { name: 'Đèn Lồng Vàng', hex: '#ffaa33', icon: '🏮' },
  { name: 'Đèn Chùm Gothic', hex: '#ffdd88', icon: '⛪' },
  { name: 'Nắng Ban Mai', hex: '#fff4cc', icon: '☀️' },
  { name: 'Trăng Đêm Lam', hex: '#38bdf8', icon: '🌙' },
  { name: 'Ma Thuật Tím', hex: '#c084fc', icon: '🔮' },
  { name: 'Băng Hàn Ngọc', hex: '#34d399', icon: '💎' },
  { name: 'Huyết Lệnh Đỏ', hex: '#ef4444', icon: '🩸' },
  { name: 'Trắng Pha Lê', hex: '#ffffff', icon: '⚪' },
];

const LIGHT_STYLE_PRESETS: Array<{
  id: string;
  name: string;
  desc: string;
  icon: string;
  config: Partial<PropLightConfig>;
}> = [
  {
    id: 'tree_aura',
    name: '✨ Hào Quang Tán Cây / Cây Phát Sáng',
    desc: 'Tỏa sáng 360° vùng siêu rộng bao phủ toàn bộ tán cây, nhuộm màu không gian xung quanh',
    icon: '🌳',
    config: {
      type: 'point',
      preset: 'tree_aura',
      color: '#34d399',
      intensity: 3.8,
      distance: 30.0,
      decay: 1.2,
      flicker: false,
      offset: [0, 3.5, 0],
    },
  },
  {
    id: 'flashlight',
    name: '🔦 Đèn Pin / Đèn Pha Chiếu Xa (Spotlight)',
    desc: 'Tia sáng gom góc hẹp (25°), chiếu xa 35m về phía trước rọi sáng mục tiêu',
    icon: '🔦',
    config: {
      type: 'spot',
      preset: 'flashlight',
      color: '#ffffff',
      intensity: 5.5,
      distance: 38.0,
      decay: 2.0,
      spot_angle: 0.35,
      spot_penumbra: 0.3,
      target_direction: [0, -1, 10],
      flicker: false,
      offset: [0, 1.2, 0],
    },
  },
  {
    id: 'street_lamp',
    name: '🏮 Cột Đèn Đường / Đèn Cao Áp (Rọi Xuống)',
    desc: 'Nguồn sáng rọi từ trên cao xuống mặt đường theo hình nón rộng (45°) có quầng sáng mềm',
    icon: '🏮',
    config: {
      type: 'spot',
      preset: 'street_lamp',
      color: '#ffaa33',
      intensity: 4.5,
      distance: 20.0,
      decay: 2.0,
      spot_angle: 0.8,
      spot_penumbra: 0.7,
      target_direction: [0, -10, 0],
      flicker: false,
      offset: [0, 4.0, 0],
    },
  },
  {
    id: 'flame',
    name: '🔥 Ngọn Đuốc / Nến / Đèn Dầu (Bập Bùng)',
    desc: 'Tỏa sáng ấm áp 360° cự ly vừa (12m) kèm hiệu ứng ngọn lửa bập bùng tự nhiên',
    icon: '🔥',
    config: {
      type: 'point',
      preset: 'lantern',
      color: '#ff6611',
      intensity: 2.8,
      distance: 12.0,
      decay: 2.0,
      flicker: true,
      offset: [0, 1.8, 0],
    },
  },
  {
    id: 'magic_crystal',
    name: '🔮 Tinh Thể Ma Thuật / Hào Quang Năng Lượng',
    desc: 'Ánh sáng tím / lam huyền ảo tỏa sáng lung linh cho các bảo vật hoặc góc thần bí',
    icon: '🔮',
    config: {
      type: 'point',
      preset: 'magic_crystal',
      color: '#c084fc',
      intensity: 4.0,
      distance: 18.0,
      decay: 1.5,
      flicker: true,
      offset: [0, 1.0, 0],
    },
  },
];

export const LightingStudioPanel: React.FC<LightingStudioPanelProps> = ({
  scene,
  onUpdateScene,
  selectedObjectId,
  onFocusObject,
}) => {
  const [copiedSuccess, setCopiedSuccess] = useState(false);
  const [activeTab, setActiveTab] = useState<'scene_lights' | 'prop_lights'>('prop_lights');

  const customLights: PropLightConfig[] = scene.environment.weather?.custom_lights || [];
  
  // If scene has placed_props use it; otherwise fallback to defaultVillageProps
  const placedProps: PlacedProp[] = (scene.environment.placed_props && scene.environment.placed_props.length > 0)
    ? scene.environment.placed_props
    : DEFAULT_VILLAGE_PROPS;

  // Update a custom light
  const handleUpdateCustomLight = (index: number, updated: PropLightConfig) => {
    const nextLights = [...customLights];
    nextLights[index] = updated;

    const nextScene: MasterSceneConfig = {
      ...scene,
      environment: {
        ...scene.environment,
        weather: {
          ...scene.environment.weather,
          custom_lights: nextLights,
        },
      },
    };
    onUpdateScene(nextScene);
  };

  // Add a new custom light
  const handleAddCustomLight = (presetId?: string) => {
    const foundPreset = LIGHT_STYLE_PRESETS.find((p) => p.id === presetId);
    const newLight: PropLightConfig = {
      type: foundPreset?.config.type || 'point',
      preset: foundPreset?.config.preset || 'custom',
      color: foundPreset?.config.color || '#ffaa33',
      intensity: foundPreset?.config.intensity ?? 3.0,
      distance: foundPreset?.config.distance ?? 14.0,
      decay: foundPreset?.config.decay ?? 2.0,
      flicker: foundPreset?.config.flicker ?? false,
      offset: foundPreset?.config.offset || [0, 3.5, 0],
      spot_angle: foundPreset?.config.spot_angle,
      spot_penumbra: foundPreset?.config.spot_penumbra,
      target_direction: foundPreset?.config.target_direction,
      cast_shadow: false,
    };

    const nextLights = [...customLights, newLight];
    const nextScene: MasterSceneConfig = {
      ...scene,
      environment: {
        ...scene.environment,
        weather: {
          ...scene.environment.weather,
          custom_lights: nextLights,
          point_lights_enabled: true,
        },
      },
    };
    onUpdateScene(nextScene);
  };

  // Delete a custom light
  const handleDeleteCustomLight = (index: number) => {
    const nextLights = customLights.filter((_, i) => i !== index);
    const nextScene: MasterSceneConfig = {
      ...scene,
      environment: {
        ...scene.environment,
        weather: {
          ...scene.environment.weather,
          custom_lights: nextLights,
        },
      },
    };
    onUpdateScene(nextScene);
  };

  // Update light attached to a prop
  const handleUpdatePropLight = (propId: string, lightConfig: PropLightConfig | undefined) => {
    const nextProps = placedProps.map((p) => {
      if (p.id === propId) {
        return { ...p, light: lightConfig };
      }
      return p;
    });

    const nextScene: MasterSceneConfig = {
      ...scene,
      environment: {
        ...scene.environment,
        placed_props: nextProps,
      },
    };
    onUpdateScene(nextScene);
  };

  // Apply a style preset to a prop light
  const handleApplyPresetToProp = (propId: string, presetId: string) => {
    const foundPreset = LIGHT_STYLE_PRESETS.find((p) => p.id === presetId);
    if (!foundPreset) return;

    const prop = placedProps.find((p) => p.id === propId);
    const isTree = (prop?.asset_path || '').includes('tree') || propId.includes('tree');

    const config: PropLightConfig = {
      ...foundPreset.config,
      offset: isTree ? [0, 3.5, 0] : (foundPreset.config.offset || [0, 1.8, 0]),
    };

    handleUpdatePropLight(propId, config);
  };

  // Copy lights config as JSON for AI Prompt / Script
  const handleCopyJSON = () => {
    const data = {
      custom_lights: customLights,
      prop_lights: placedProps.filter((p) => p.light).map((p) => ({ prop_id: p.id, light: p.light })),
    };
    navigator.clipboard.writeText(JSON.stringify(data, null, 2));
    setCopiedSuccess(true);
    setTimeout(() => setCopiedSuccess(false), 2000);
  };

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      height: '100%',
      backgroundColor: 'rgba(15, 23, 42, 0.95)',
      color: '#f8fafc',
      fontFamily: 'Inter, system-ui, sans-serif',
      fontSize: '12px',
      overflow: 'hidden',
    }}>
      {/* Panel Header */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '10px 16px',
        borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
        background: 'linear-gradient(90deg, rgba(30, 41, 59, 0.85), rgba(15, 23, 42, 0.85))',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <div style={{
            width: 30, height: 30, borderRadius: 6,
            background: 'linear-gradient(135deg, #f59e0b, #ea580c)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            boxShadow: '0 0 14px rgba(245, 158, 11, 0.45)',
          }}>
            <Lightbulb size={18} color="#ffffff" />
          </div>
          <div>
            <div style={{ fontWeight: 700, fontSize: '13px', color: '#fef08a', display: 'flex', alignItems: 'center', gap: 6 }}>
              Studio Ánh Sáng & Hiệu Ứng Nguồn Sáng 3D
              <span style={{ fontSize: '9px', background: 'rgba(245, 158, 11, 0.2)', color: '#fbbf24', padding: '1px 7px', borderRadius: 10, border: '1px solid rgba(245, 158, 11, 0.4)' }}>
                {customLights.length + placedProps.filter(p => p.light).length} Nguồn Sáng
              </span>
            </div>
            <div style={{ fontSize: '10px', color: '#94a3b8' }}>
              Gắn hào quang cây, đèn pin chiếu xa, cột đèn đường, đuốc lửa bập bùng & nguồn sáng tự do
            </div>
          </div>
        </div>

        {/* Action Buttons */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <button
            onClick={() => handleAddCustomLight('flashlight')}
            style={{
              display: 'flex', alignItems: 'center', gap: 4,
              padding: '6px 10px', borderRadius: 6,
              background: 'rgba(56, 189, 248, 0.15)', border: '1px solid rgba(56, 189, 248, 0.3)',
              color: '#38bdf8', fontWeight: 600, fontSize: '11px', cursor: 'pointer',
            }}
            title="Thêm Đèn Pin / Đèn Pha Chiếu Xa"
          >
            🔦 + Đèn Pin Chiếu Xa
          </button>

          <button
            onClick={() => handleAddCustomLight('street_lamp')}
            style={{
              display: 'flex', alignItems: 'center', gap: 4,
              padding: '6px 10px', borderRadius: 6,
              background: 'rgba(245, 158, 11, 0.15)', border: '1px solid rgba(245, 158, 11, 0.3)',
              color: '#fef08a', fontWeight: 600, fontSize: '11px', cursor: 'pointer',
            }}
            title="Thêm Cột Đèn Đường Rọi Xuống"
          >
            🏮 + Cột Đèn Đường
          </button>

          <button
            onClick={() => handleAddCustomLight('flame')}
            style={{
              display: 'flex', alignItems: 'center', gap: 4,
              padding: '6px 12px', borderRadius: 6,
              background: 'linear-gradient(135deg, #f59e0b, #d97706)',
              border: 'none', color: '#000000', fontWeight: 700, fontSize: '11px',
              cursor: 'pointer', boxShadow: '0 0 10px rgba(245, 158, 11, 0.3)',
            }}
          >
            <Plus size={13} /> Thêm Đèn Mới
          </button>

          <button
            onClick={handleCopyJSON}
            style={{
              display: 'flex', alignItems: 'center', gap: 4,
              padding: '6px 10px', borderRadius: 6,
              background: 'rgba(30, 41, 59, 0.8)', border: '1px solid rgba(255, 255, 255, 0.1)',
              color: copiedSuccess ? '#4ade80' : '#cbd5e1', fontSize: '11px', cursor: 'pointer',
            }}
            title="Sao chép toàn bộ cấu hình đèn dưới dạng JSON"
          >
            {copiedSuccess ? <Check size={13} color="#4ade80" /> : <Copy size={13} />}
            {copiedSuccess ? 'Đã Sao Chép!' : 'Xuất JSON'}
          </button>
        </div>
      </div>

      {/* Sub Tabs */}
      <div style={{
        display: 'flex',
        gap: '4px',
        padding: '6px 16px',
        borderBottom: '1px solid rgba(255, 255, 255, 0.06)',
        background: 'rgba(15, 23, 42, 0.6)',
      }}>
        <button
          onClick={() => setActiveTab('prop_lights')}
          style={{
            padding: '6px 14px', borderRadius: 4, border: 'none', fontSize: '11px', fontWeight: activeTab === 'prop_lights' ? 700 : 500,
            background: activeTab === 'prop_lights' ? 'rgba(245, 158, 11, 0.2)' : 'transparent',
            color: activeTab === 'prop_lights' ? '#fef08a' : '#94a3b8',
            cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 5,
          }}
        >
          🌳 Gắn Đèn Vào Cây & Vật Thể Props ({placedProps.filter(p => p.light).length}/{placedProps.length})
        </button>

        <button
          onClick={() => setActiveTab('scene_lights')}
          style={{
            padding: '6px 14px', borderRadius: 4, border: 'none', fontSize: '11px', fontWeight: activeTab === 'scene_lights' ? 700 : 500,
            background: activeTab === 'scene_lights' ? 'rgba(245, 158, 11, 0.2)' : 'transparent',
            color: activeTab === 'scene_lights' ? '#fef08a' : '#94a3b8',
            cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 5,
          }}
        >
          🏰 Đèn Độc Lập Trong Không Gian / Map ({customLights.length})
        </button>
      </div>

      {/* Tab Content List */}
      <div style={{ flex: 1, overflowY: 'auto', padding: '12px 16px', display: 'flex', flexDirection: 'column', gap: 12 }}>
        
        {/* TAB 1: Gắn Đèn Vào Cây & Vật Thể Props */}
        {activeTab === 'prop_lights' && (
          <>
            <div style={{ fontSize: '11px', color: '#94a3b8', background: 'rgba(30, 41, 59, 0.4)', padding: '8px 12px', borderRadius: 6, border: '1px solid rgba(255, 255, 255, 0.05)' }}>
              💡 <b>Hướng dẫn:</b> Bạn có thể chọn bất kỳ cây cối, đèn lồng, đuốc, ghế hay vật thể 3D nào bên dưới để bật phát sáng. Khi di chuyển vật thể trong 3D, ánh sáng sẽ tự động bám theo và rọi sáng môi trường xung quanh.
            </div>

            {placedProps.map((prop) => {
              const hasLight = Boolean(prop.light);
              const isTree = (prop.asset_path || '').includes('tree') || prop.id.includes('tree');
              const isLantern = (prop.asset_path || '').includes('lantern') || prop.id.includes('lantern');

              const light = prop.light || (isTree ? {
                type: 'point',
                preset: 'tree_aura',
                color: '#34d399',
                intensity: 3.5,
                distance: 28.0,
                decay: 1.2,
                flicker: false,
                offset: [0, 3.5, 0],
              } : {
                type: 'point',
                preset: 'lantern',
                color: '#ffaa33',
                intensity: 3.0,
                distance: 12.0,
                decay: 2.0,
                flicker: true,
                offset: [0, 1.8, 0],
              });

              return (
                <div
                  key={prop.id}
                  style={{
                    background: hasLight ? 'rgba(30, 41, 59, 0.75)' : 'rgba(15, 23, 42, 0.45)',
                    border: `1px solid ${hasLight ? 'rgba(245, 158, 11, 0.35)' : 'rgba(255, 255, 255, 0.06)'}`,
                    borderRadius: 8,
                    padding: 12,
                    display: 'flex',
                    flexDirection: 'column',
                    gap: 10,
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span style={{ fontSize: '16px' }}>{isTree ? '🌳' : isLantern ? '🏮' : '📦'}</span>
                      <div>
                        <span style={{ fontSize: '12px', fontWeight: 700, color: hasLight ? '#fef08a' : '#f8fafc' }}>
                          {prop.id}
                        </span>
                        <span style={{ fontSize: '10px', color: '#64748b', marginLeft: 6 }}>
                          ({prop.asset_path.split('/').pop()})
                        </span>
                      </div>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      {onFocusObject && (
                        <button
                          onClick={() => onFocusObject(prop.position)}
                          style={{
                            padding: '4px 8px', borderRadius: 4, background: 'rgba(255, 255, 255, 0.06)',
                            border: '1px solid rgba(255, 255, 255, 0.1)', color: '#94a3b8', fontSize: '10px',
                            cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 4,
                          }}
                        >
                          <Target size={11} /> Nhìn Vào
                        </button>
                      )}

                      <label style={{
                        display: 'flex', alignItems: 'center', gap: 6, fontSize: '10px', cursor: 'pointer',
                        padding: '4px 10px', borderRadius: 14,
                        background: hasLight ? 'rgba(245, 158, 11, 0.25)' : 'rgba(255, 255, 255, 0.05)',
                        border: `1px solid ${hasLight ? '#f59e0b' : 'rgba(255, 255, 255, 0.1)'}`,
                        color: hasLight ? '#fef08a' : '#94a3b8',
                        fontWeight: 700,
                      }}>
                        <input
                          type="checkbox"
                          checked={hasLight}
                          onChange={(e) => handleUpdatePropLight(prop.id, e.target.checked ? light : undefined)}
                          style={{ display: 'none' }}
                        />
                        <Lightbulb size={12} color={hasLight ? '#f59e0b' : '#94a3b8'} />
                        {hasLight ? '● Đang Phát Sáng' : '+ Gắn Nguồn Sáng'}
                      </label>
                    </div>
                  </div>

                  {/* If Light is enabled on this prop, show full controls & presets */}
                  {hasLight && (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 10, borderTop: '1px solid rgba(255, 255, 255, 0.08)', paddingTop: 10 }}>
                      
                      {/* Presets Row */}
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                        <span style={{ fontSize: '10px', color: '#94a3b8' }}>Kiểu ánh sáng:</span>
                        {LIGHT_STYLE_PRESETS.map((pst) => (
                          <button
                            key={pst.id}
                            onClick={() => handleApplyPresetToProp(prop.id, pst.id)}
                            style={{
                              padding: '3px 8px', borderRadius: 4, fontSize: '10px', cursor: 'pointer',
                              background: light.preset === pst.id ? 'rgba(245, 158, 11, 0.25)' : 'rgba(255, 255, 255, 0.05)',
                              border: `1px solid ${light.preset === pst.id ? '#f59e0b' : 'rgba(255, 255, 255, 0.1)'}`,
                              color: light.preset === pst.id ? '#fef08a' : '#cbd5e1',
                              fontWeight: light.preset === pst.id ? 700 : 500,
                            }}
                            title={pst.desc}
                          >
                            {pst.icon} {pst.name.split(' (')[0]}
                          </button>
                        ))}
                      </div>

                      {/* Color Palette */}
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                        <span style={{ fontSize: '10px', color: '#94a3b8' }}>Màu sắc:</span>
                        <input
                          type="color"
                          value={light.color || '#ffaa33'}
                          onChange={(e) => handleUpdatePropLight(prop.id, { ...light, color: e.target.value })}
                          style={{ width: 26, height: 22, border: 'none', borderRadius: 4, cursor: 'pointer', background: 'transparent' }}
                        />
                        {PRESET_LIGHT_COLORS.map((p, pIdx) => (
                          <button
                            key={pIdx}
                            onClick={() => handleUpdatePropLight(prop.id, { ...light, color: p.hex })}
                            style={{
                              padding: '2px 6px', borderRadius: 4, fontSize: '9px', cursor: 'pointer',
                              background: light.color === p.hex ? `${p.hex}33` : 'rgba(255, 255, 255, 0.05)',
                              border: `1px solid ${light.color === p.hex ? p.hex : 'rgba(255, 255, 255, 0.1)'}`,
                              color: light.color === p.hex ? '#ffffff' : '#94a3b8',
                            }}
                            title={p.name}
                          >
                            {p.icon} {p.name}
                          </button>
                        ))}
                      </div>

                      {/* Sliders: Intensity & Range */}
                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                        {/* Intensity */}
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '10px', color: '#cbd5e1' }}>
                            <span>Cường độ sáng:</span>
                            <span style={{ fontWeight: 700, color: '#fef08a' }}>{(light.intensity ?? 3.0).toFixed(1)} Lux</span>
                          </div>
                          <input
                            type="range" min="0.1" max="10.0" step="0.1"
                            value={light.intensity ?? 3.0}
                            onChange={(e) => handleUpdatePropLight(prop.id, { ...light, intensity: parseFloat(e.target.value) })}
                            style={{ accentColor: '#f59e0b', cursor: 'pointer' }}
                          />
                        </div>

                        {/* Distance */}
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '10px', color: '#cbd5e1' }}>
                            <span>Tầm xa phủ sáng:</span>
                            <span style={{ fontWeight: 700, color: '#38bdf8' }}>{(light.distance ?? 14.0).toFixed(1)}m</span>
                          </div>
                          <input
                            type="range" min="2.0" max="50.0" step="1.0"
                            value={light.distance ?? 14.0}
                            onChange={(e) => handleUpdatePropLight(prop.id, { ...light, distance: parseFloat(e.target.value) })}
                            style={{ accentColor: '#38bdf8', cursor: 'pointer' }}
                          />
                        </div>
                      </div>

                      {/* Toggles */}
                      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                        <label style={{
                          display: 'flex', alignItems: 'center', gap: 5, fontSize: '10px', cursor: 'pointer',
                          padding: '3px 8px', borderRadius: 4,
                          background: light.flicker ? 'rgba(234, 88, 12, 0.2)' : 'rgba(255, 255, 255, 0.05)',
                          border: `1px solid ${light.flicker ? '#ea580c' : 'rgba(255, 255, 255, 0.1)'}`,
                          color: light.flicker ? '#fed7aa' : '#94a3b8',
                        }}>
                          <input
                            type="checkbox"
                            checked={Boolean(light.flicker)}
                            onChange={(e) => handleUpdatePropLight(prop.id, { ...light, flicker: e.target.checked })}
                            style={{ display: 'none' }}
                          />
                          <Flame size={12} color={light.flicker ? '#ea580c' : '#94a3b8'} />
                          {light.flicker ? '🔥 Bập Bùng Lửa Thật' : 'Sáng Đều'}
                        </label>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </>
        )}

        {/* TAB 2: Custom Map Lights */}
        {activeTab === 'scene_lights' && (
          <>
            {customLights.length === 0 ? (
              <div style={{ textAlign: 'center', padding: '30px 10px', color: '#64748b' }}>
                <Lightbulb size={32} style={{ opacity: 0.3, marginBottom: 8 }} />
                <div>Chưa có nguồn sáng 3D độc lập nào trong Map.</div>
                <div style={{ fontSize: '10px', marginTop: 4 }}>Bấm nút bên dưới để thêm nguồn sáng đèn pin, cột đèn, hoặc đốm sáng tự do!</div>
                <div style={{ display: 'flex', justifyContent: 'center', gap: 8, marginTop: 12 }}>
                  <button
                    onClick={() => handleAddCustomLight('flashlight')}
                    style={{
                      padding: '6px 14px', borderRadius: 6,
                      background: 'rgba(56, 189, 248, 0.15)', border: '1px solid #38bdf8',
                      color: '#38bdf8', fontWeight: 600, fontSize: '11px', cursor: 'pointer',
                    }}
                  >
                    🔦 Thêm Đèn Pin Chiếu Xa
                  </button>
                  <button
                    onClick={() => handleAddCustomLight('street_lamp')}
                    style={{
                      padding: '6px 14px', borderRadius: 6,
                      background: 'rgba(245, 158, 11, 0.15)', border: '1px solid #f59e0b',
                      color: '#fef08a', fontWeight: 600, fontSize: '11px', cursor: 'pointer',
                    }}
                  >
                    🏮 Thêm Cột Đèn Đường
                  </button>
                </div>
              </div>
            ) : (
              customLights.map((light, idx) => {
                const pos = light.offset || [0, 3, 0];
                return (
                  <div
                    key={idx}
                    style={{
                      background: 'rgba(30, 41, 59, 0.5)',
                      border: '1px solid rgba(255, 255, 255, 0.08)',
                      borderRadius: 8,
                      padding: 12,
                      display: 'flex',
                      flexDirection: 'column',
                      gap: 10,
                    }}
                  >
                    {/* Header */}
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                        <div style={{
                          width: 14, height: 14, borderRadius: '50%',
                          background: light.color || '#ffaa33',
                          boxShadow: `0 0 10px ${light.color || '#ffaa33'}`,
                        }} />
                        <span style={{ fontWeight: 700, color: '#f8fafc', fontSize: '12px' }}>
                          Nguồn Sáng 3D #{idx + 1} ({light.type === 'spot' ? 'Spot Light' : 'Point Light'})
                        </span>
                      </div>

                      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                        {onFocusObject && (
                          <button
                            onClick={() => onFocusObject([pos[0], pos[1], pos[2]])}
                            style={{
                              padding: '3px 7px', borderRadius: 4, background: 'rgba(255, 255, 255, 0.06)',
                              border: '1px solid rgba(255, 255, 255, 0.1)', color: '#94a3b8', fontSize: '10px',
                              cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 4,
                            }}
                          >
                            <Target size={11} /> Nhìn Vào
                          </button>
                        )}
                        <button
                          onClick={() => handleDeleteCustomLight(idx)}
                          style={{
                            padding: '3px 7px', borderRadius: 4, background: 'rgba(239, 68, 68, 0.15)',
                            border: '1px solid rgba(239, 68, 68, 0.3)', color: '#f87171', fontSize: '10px',
                            cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 4,
                          }}
                        >
                          <Trash2 size={11} /> Xóa
                        </button>
                      </div>
                    </div>

                    {/* Presets Row */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                      <span style={{ fontSize: '10px', color: '#94a3b8' }}>Kiểu ánh sáng:</span>
                      {LIGHT_STYLE_PRESETS.map((pst) => (
                        <button
                          key={pst.id}
                          onClick={() => handleUpdateCustomLight(idx, { ...light, ...pst.config, offset: pos })}
                          style={{
                            padding: '2px 7px', borderRadius: 4, fontSize: '9px', cursor: 'pointer',
                            background: light.preset === pst.id ? 'rgba(245, 158, 11, 0.25)' : 'rgba(255, 255, 255, 0.05)',
                            border: `1px solid ${light.preset === pst.id ? '#f59e0b' : 'rgba(255, 255, 255, 0.1)'}`,
                            color: light.preset === pst.id ? '#fef08a' : '#cbd5e1',
                            fontWeight: light.preset === pst.id ? 700 : 500,
                          }}
                          title={pst.desc}
                        >
                          {pst.icon} {pst.name.split(' (')[0]}
                        </button>
                      ))}
                    </div>

                    {/* Color Presets */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                      <span style={{ fontSize: '10px', color: '#94a3b8' }}>Màu sắc:</span>
                      <input
                        type="color"
                        value={light.color || '#ffaa33'}
                        onChange={(e) => handleUpdateCustomLight(idx, { ...light, color: e.target.value })}
                        style={{ width: 26, height: 22, border: 'none', borderRadius: 4, cursor: 'pointer', background: 'transparent' }}
                      />
                      {PRESET_LIGHT_COLORS.map((p, pIdx) => (
                        <button
                          key={pIdx}
                          onClick={() => handleUpdateCustomLight(idx, { ...light, color: p.hex })}
                          style={{
                            padding: '2px 6px', borderRadius: 4, fontSize: '9px', cursor: 'pointer',
                            background: light.color === p.hex ? `${p.hex}33` : 'rgba(255, 255, 255, 0.05)',
                            border: `1px solid ${light.color === p.hex ? p.hex : 'rgba(255, 255, 255, 0.1)'}`,
                            color: light.color === p.hex ? '#ffffff' : '#94a3b8',
                          }}
                          title={p.name}
                        >
                          {p.icon} {p.name}
                        </button>
                      ))}
                    </div>

                    {/* Sliders: Intensity & Range */}
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                      {/* Intensity */}
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '10px', color: '#cbd5e1' }}>
                          <span>Cường độ sáng (Lux):</span>
                          <span style={{ fontWeight: 700, color: '#fef08a' }}>{(light.intensity ?? 3.0).toFixed(1)}</span>
                        </div>
                        <input
                          type="range" min="0.1" max="10.0" step="0.1"
                          value={light.intensity ?? 3.0}
                          onChange={(e) => handleUpdateCustomLight(idx, { ...light, intensity: parseFloat(e.target.value) })}
                          style={{ accentColor: '#f59e0b', cursor: 'pointer' }}
                        />
                      </div>

                      {/* Distance */}
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '10px', color: '#cbd5e1' }}>
                          <span>Tầm xa chiếu sáng (Mét):</span>
                          <span style={{ fontWeight: 700, color: '#38bdf8' }}>{(light.distance ?? 14.0).toFixed(1)}m</span>
                        </div>
                        <input
                          type="range" min="2.0" max="50.0" step="1.0"
                          value={light.distance ?? 14.0}
                          onChange={(e) => handleUpdateCustomLight(idx, { ...light, distance: parseFloat(e.target.value) })}
                          style={{ accentColor: '#38bdf8', cursor: 'pointer' }}
                        />
                      </div>
                    </div>

                    {/* Position XYZ & Toggles */}
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr) auto', gap: 8, alignItems: 'center' }}>
                      {['X', 'Y', 'Z'].map((axis, aIdx) => (
                        <div key={axis} style={{ display: 'flex', alignItems: 'center', gap: 4, background: 'rgba(0, 0, 0, 0.3)', padding: '3px 6px', borderRadius: 4 }}>
                          <span style={{ fontSize: '10px', color: aIdx === 0 ? '#f87171' : aIdx === 1 ? '#4ade80' : '#60a5fa', fontWeight: 700 }}>
                            {axis}:
                          </span>
                          <input
                            type="number" step="0.5"
                            value={pos[aIdx]}
                            onChange={(e) => {
                              const val = parseFloat(e.target.value) || 0;
                              const newPos: [number, number, number] = [...pos];
                              newPos[aIdx] = val;
                              handleUpdateCustomLight(idx, { ...light, offset: newPos });
                            }}
                            style={{
                              width: '100%', background: 'transparent', border: 'none', color: '#ffffff', fontSize: '11px', outline: 'none',
                            }}
                          />
                        </div>
                      ))}

                      {/* Flicker Flame Toggle */}
                      <label style={{
                        display: 'flex', alignItems: 'center', gap: 5, fontSize: '10px', cursor: 'pointer',
                        padding: '4px 8px', borderRadius: 4,
                        background: light.flicker ? 'rgba(234, 88, 12, 0.2)' : 'rgba(255, 255, 255, 0.05)',
                        border: `1px solid ${light.flicker ? '#ea580c' : 'rgba(255, 255, 255, 0.1)'}`,
                        color: light.flicker ? '#fed7aa' : '#94a3b8',
                      }}>
                        <input
                          type="checkbox"
                          checked={Boolean(light.flicker)}
                          onChange={(e) => handleUpdateCustomLight(idx, { ...light, flicker: e.target.checked })}
                          style={{ display: 'none' }}
                        />
                        <Flame size={12} color={light.flicker ? '#ea580c' : '#94a3b8'} />
                        {light.flicker ? 'Bập Bùng Lửa' : 'Sáng Đều'}
                      </label>
                    </div>
                  </div>
                );
              })
            )}
          </>
        )}
      </div>
    </div>
  );
};
