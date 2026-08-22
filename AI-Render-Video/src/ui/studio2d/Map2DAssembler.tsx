import React, { useState, useRef, useEffect, useCallback } from 'react';
import {
  Map as MapIcon,
  Layers,
  Play,
  Pause,
  Save,
  Download,
  Plus,
  Trash2,
  Camera,
  Sun,
  CloudRain,
  Eye,
  Sliders,
  Check,
} from 'lucide-react';
import { Map2DPreset, Map2DLayerConfig } from '../../types/scene2d';
import { DEFAULT_SAMPLE_MAPS_2D } from '../../core/assets/Asset2DRegistry';
import { Canvas2DPuppetEngine } from '../../core/engine2d/Canvas2DPuppetEngine';

interface Map2DAssemblerProps {
  currentMap: Map2DPreset;
  onChangeMap: (updated: Map2DPreset) => void;
  onSaveMap?: (saved: Map2DPreset) => void;
}

export const Map2DAssembler: React.FC<Map2DAssemblerProps> = ({
  currentMap,
  onChangeMap,
  onSaveMap,
}) => {
  const [selectedLayerId, setSelectedLayerId] = useState<string>(currentMap.layers[0]?.id || 'sky');
  const [cameraX, setCameraX] = useState<number>(0);
  const [cameraZoom, setCameraZoom] = useState<number>(1.0);
  const [isAutoPan, setIsAutoPan] = useState<boolean>(true);
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [saveSuccessMsg, setSaveSuccessMsg] = useState<string | null>(null);

  const canvasRef = useRef<HTMLCanvasElement>(null);
  const engineRef = useRef<Canvas2DPuppetEngine>(new Canvas2DPuppetEngine());
  const animTimeRef = useRef<number>(0);
  const animFrameIdRef = useRef<number | null>(null);

  const selectedLayer = currentMap.layers.find((l) => l.id === selectedLayerId) || currentMap.layers[0];

  const updateLayer = (layerId: string, updates: Partial<Map2DLayerConfig>) => {
    onChangeMap({
      ...currentMap,
      layers: currentMap.layers.map((l) => (l.id === layerId ? { ...l, ...updates } : l)),
    });
  };

  // Parallax Render Loop
  const renderMapLoop = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    if (isAutoPan) {
      animTimeRef.current += 0.02;
      setCameraX(Math.sin(animTimeRef.current * 0.5) * 150);
    }

    ctx.clearRect(0, 0, canvas.width, canvas.height);

    engineRef.current.renderMap(
      ctx,
      currentMap,
      cameraX,
      0,
      cameraZoom,
      canvas.width,
      canvas.height,
      animTimeRef.current
    );

    animFrameIdRef.current = requestAnimationFrame(renderMapLoop);
  }, [currentMap, cameraX, cameraZoom, isAutoPan]);

  useEffect(() => {
    animFrameIdRef.current = requestAnimationFrame(renderMapLoop);
    return () => {
      if (animFrameIdRef.current) cancelAnimationFrame(animFrameIdRef.current);
    };
  }, [renderMapLoop]);

  const handleSaveMap = async () => {
    setIsSaving(true);
    try {
      const canvas = canvasRef.current;
      const previewImg = canvas ? canvas.toDataURL('image/png') : '';

      const response = await fetch('/api/save-2d-map', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: currentMap.name || 'custom_map_2d',
          mapData: {
            ...currentMap,
            preview_image: `asset_2ds/ban_do/_custom_ban_do/${currentMap.id || 'map_2d'}.png`,
          },
          previewImageBase64: previewImg,
        }),
      });

      if (response.ok) {
        setSaveSuccessMsg('✅ Đã lưu bản đồ vào asset_2ds/ban_do/_custom_ban_do/!');
      } else {
        downloadJsonMap();
        setSaveSuccessMsg('✅ Đã xuất tệp JSON bản đồ!');
      }
    } catch {
      downloadJsonMap();
      setSaveSuccessMsg('✅ Đã xuất tệp JSON bản đồ!');
    } finally {
      setIsSaving(false);
      setTimeout(() => setSaveSuccessMsg(null), 3000);
      if (onSaveMap) onSaveMap(currentMap);
    }
  };

  const downloadJsonMap = () => {
    const dataStr = 'data:text/json;charset=utf-8,' + encodeURIComponent(JSON.stringify(currentMap, null, 2));
    const a = document.createElement('a');
    a.href = dataStr;
    a.download = `${currentMap.id || 'map_2d'}.json`;
    a.click();
  };

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '280px 1fr 300px', gap: 14, height: '100%', overflow: 'hidden' }}>
      {/* ─── LEFT: Map Layer List ─────────────────────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, background: 'rgba(15, 23, 42, 0.7)', padding: 12, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', overflowY: 'auto' }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
          <Layers size={13} /> CÁC TẦNG BẢN ĐỒ (PARALLAX)
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {currentMap.layers.map((layer) => {
            const isSelected = layer.id === selectedLayerId;
            return (
              <div
                key={layer.id}
                onClick={() => setSelectedLayerId(layer.id)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '8px 10px',
                  borderRadius: 6,
                  fontSize: 11,
                  background: isSelected ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.02)',
                  border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.04)',
                  color: isSelected ? '#ffffff' : '#94a3b8',
                  cursor: 'pointer',
                }}
              >
                <div>
                  <div style={{ fontWeight: 600, color: isSelected ? '#38bdf8' : '#e2e8f0' }}>{layer.name}</div>
                  <div style={{ fontSize: 9, color: '#64748b' }}>Độ sâu: {layer.parallax_factor}x | Z:{layer.z_index}</div>
                </div>
                <span style={{ fontSize: 9, background: 'rgba(255,255,255,0.05)', padding: '2px 6px', borderRadius: 4 }}>
                  {layer.type}
                </span>
              </div>
            );
          })}
        </div>
      </div>

      {/* ─── CENTER: Parallax Camera Viewport ─────────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10, position: 'relative' }}>
        <div
          style={{
            flex: 1,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            background: '#090d16',
            borderRadius: 8,
            border: '1px solid rgba(255,255,255,0.08)',
            position: 'relative',
            overflow: 'hidden',
          }}
        >
          <canvas ref={canvasRef} width={800} height={450} style={{ maxWidth: '100%', maxHeight: '100%', borderRadius: 6 }} />

          {/* Toast Msg */}
          {saveSuccessMsg && (
            <div style={{ position: 'absolute', top: 12, background: 'rgba(34, 197, 94, 0.95)', color: '#fff', fontSize: 11, fontWeight: 700, padding: '6px 14px', borderRadius: 20, boxShadow: '0 4px 12px rgba(0,0,0,0.4)' }}>
              {saveSuccessMsg}
            </div>
          )}
        </div>

        {/* Camera Jump Cuts & Parallax Controls */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', background: 'rgba(15, 23, 42, 0.8)', padding: '8px 14px', borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <button
              onClick={() => setIsAutoPan(!isAutoPan)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '5px 10px',
                borderRadius: 5,
                background: isAutoPan ? '#0284c7' : 'rgba(255,255,255,0.1)',
                color: '#fff',
                fontSize: 11,
                fontWeight: 600,
                border: 'none',
                cursor: 'pointer',
              }}
            >
              {isAutoPan ? <Pause size={12} /> : <Play size={12} />}
              {isAutoPan ? 'Tự Động Lia Parallax' : 'Thủ Công'}
            </button>

            {/* Jump Cut Camera Presets */}
            <span style={{ fontSize: 10, color: '#94a3b8', marginLeft: 8 }}>Nhảy góc máy:</span>
            <button
              onClick={() => {
                setIsAutoPan(false);
                setCameraZoom(1.4);
                setCameraX(-40);
              }}
              style={{ padding: '4px 8px', fontSize: 10, borderRadius: 4, background: 'rgba(255,255,255,0.05)', color: '#fff', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
            >
              Cận Cảnh (Close-up)
            </button>
            <button
              onClick={() => {
                setIsAutoPan(false);
                setCameraZoom(1.0);
                setCameraX(0);
              }}
              style={{ padding: '4px 8px', fontSize: 10, borderRadius: 4, background: 'rgba(255,255,255,0.05)', color: '#fff', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
            >
              Trung Cảnh (Medium)
            </button>
            <button
              onClick={() => {
                setIsAutoPan(false);
                setCameraZoom(0.85);
                setCameraX(0);
              }}
              style={{ padding: '4px 8px', fontSize: 10, borderRadius: 4, background: 'rgba(255,255,255,0.05)', color: '#fff', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
            >
              Toàn Cảnh (Wide)
            </button>
          </div>

          <button
            onClick={handleSaveMap}
            disabled={isSaving}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '6px 14px',
              borderRadius: 6,
              background: 'linear-gradient(135deg, #10b981, #059669)',
              color: '#ffffff',
              fontWeight: 700,
              fontSize: 11,
              border: 'none',
              cursor: 'pointer',
              boxShadow: '0 4px 12px rgba(16, 185, 129, 0.3)',
            }}
          >
            <Save size={13} /> {isSaving ? 'Đang Lưu...' : 'Lưu Map 2D'}
          </button>
        </div>
      </div>

      {/* ─── RIGHT: Layer Settings & Atmosphere ──────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12, background: 'rgba(15, 23, 42, 0.7)', padding: 14, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', overflowY: 'auto' }}>
        <div style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Sliders size={14} /> THIẾT LẬP TẦNG & HIỆU ỨNG
        </div>

        {selectedLayer && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <div style={{ fontSize: 11, color: '#94a3b8' }}>
              Đang chỉnh: <b style={{ color: '#fff' }}>{selectedLayer.name}</b>
            </div>

            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
                <span>Hệ số Parallax (Tốc độ trôi theo chiều sâu):</span>
                <span style={{ color: '#38bdf8', fontWeight: 600 }}>{selectedLayer.parallax_factor}x</span>
              </div>
              <input
                type="range"
                min="0"
                max="25"
                value={Math.round(selectedLayer.parallax_factor * 10)}
                onChange={(e) => updateLayer(selectedLayer.id, { parallax_factor: parseInt(e.target.value) / 10 })}
                style={{ width: '100%' }}
              />
            </div>

            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
                <span>Tốc độ tự cuộn mây gió:</span>
                <span style={{ color: '#38bdf8', fontWeight: 600 }}>{selectedLayer.scroll_speed_x || 0} px/s</span>
              </div>
              <input
                type="range"
                min="-50"
                max="50"
                value={selectedLayer.scroll_speed_x || 0}
                onChange={(e) => updateLayer(selectedLayer.id, { scroll_speed_x: parseInt(e.target.value) })}
                style={{ width: '100%' }}
              />
            </div>

            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
                <span>Độ trong suốt (Opacity):</span>
                <span style={{ color: '#38bdf8', fontWeight: 600 }}>{Math.round((selectedLayer.opacity ?? 1) * 100)}%</span>
              </div>
              <input
                type="range"
                min="0"
                max="100"
                value={Math.round((selectedLayer.opacity ?? 1) * 100)}
                onChange={(e) => updateLayer(selectedLayer.id, { opacity: parseInt(e.target.value) / 100 })}
                style={{ width: '100%' }}
              />
            </div>
          </div>
        )}

        <hr style={{ border: 'none', borderTop: '1px solid rgba(255,255,255,0.08)', margin: '4px 0' }} />

        {/* Atmosphere & Lighting */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: '#fbbf24', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Sun size={13} /> ÁNH SÁNG & KHÔNG KHÍ
          </div>

          <div>
            <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 4 }}>Hiệu ứng hạt bay:</label>
            <select
              value={currentMap.atmosphere.weather}
              onChange={(e) =>
                onChangeMap({
                  ...currentMap,
                  atmosphere: { ...currentMap.atmosphere, weather: e.target.value as any },
                })
              }
              style={{ width: '100%', padding: '5px 8px', fontSize: 11, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.2)', borderRadius: 5 }}
            >
              <option value="none">Không có</option>
              <option value="falling_leaves">Lá trúc / Lá cây rơi</option>
              <option value="petals">Cánh hoa đào rơi</option>
              <option value="rain">Mưa rơi</option>
              <option value="thunderstorm">Giông bão sấm sét</option>
              <option value="dust_embers">Bụi sáng / Tàn lửa</option>
            </select>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: 10, color: '#94a3b8' }}>Màu sắc phủ ánh sáng:</span>
            <input
              type="color"
              value={currentMap.atmosphere.lighting_tint || '#ffe4cc'}
              onChange={(e) =>
                onChangeMap({
                  ...currentMap,
                  atmosphere: { ...currentMap.atmosphere, lighting_tint: e.target.value },
                })
              }
              style={{ width: 36, height: 24, padding: 0, border: 'none', borderRadius: 4, cursor: 'pointer' }}
            />
          </div>
        </div>
      </div>
    </div>
  );
};
