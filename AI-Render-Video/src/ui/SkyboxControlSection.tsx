import React, { useRef, useState } from 'react';
import { Upload, Image, Sparkles, Check, RefreshCw, Layers } from 'lucide-react';
import { EnvironmentOverride } from '../types/scene';
import { SkyboxManager, SkyboxItem } from '../core/assets/SkyboxManager';

interface SkyboxControlSectionProps {
  override: EnvironmentOverride;
  onChange: (override: EnvironmentOverride) => void;
}

export const SkyboxControlSection: React.FC<SkyboxControlSectionProps> = ({
  override,
  onChange,
}) => {
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [selectedTimeFilter, setSelectedTimeFilter] = useState<string>('all');
  const [selectedCloudFilter, setSelectedCloudFilter] = useState<string>('all');
  const [showCatalogExplorer, setShowCatalogExplorer] = useState<boolean>(false);

  const currentSkyboxType = override.skybox_type || 'none';
  const isAutoMode = currentSkyboxType === 'auto';
  const isCustomMode = currentSkyboxType === 'custom';

  // Handle local image file upload from disk
  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      const objectUrl = URL.createObjectURL(file);
      onChange({
        ...override,
        skybox_type: 'custom',
        skybox_url: objectUrl,
        weather_preset: 'custom',
      });
    }
  };

  // Filter catalog items
  const filteredCatalog = SkyboxManager.CATALOG.filter((item) => {
    if (selectedTimeFilter !== 'all' && item.timeCategory !== selectedTimeFilter) {
      return false;
    }
    if (selectedCloudFilter !== 'all' && item.cloudLevel !== selectedCloudFilter) {
      return false;
    }
    return true;
  });

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        background: 'rgba(245, 158, 11, 0.05)',
        padding: 8,
        borderRadius: 6,
        border: '1px solid rgba(245, 158, 11, 0.18)',
      }}
    >
      {/* Hidden File Input for Local Skybox Upload */}
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*,.hdr,.exr"
        style={{ display: 'none' }}
        onChange={handleFileUpload}
      />

      {/* Header & Upload Button */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <span style={{ fontSize: 10, color: '#fef08a', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 4 }}>
          <Image size={12} color="#f59e0b" /> Skybox 360° Môi Trường
        </span>
        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          style={{
            padding: '3px 8px',
            fontSize: 9,
            fontWeight: 700,
            borderRadius: 4,
            background: 'linear-gradient(135deg, rgba(245, 158, 11, 0.3), rgba(217, 119, 6, 0.5))',
            border: '1px solid #f59e0b',
            color: '#ffffff',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            boxShadow: '0 0 8px rgba(245, 158, 11, 0.3)',
          }}
          title="Chọn ảnh 360° (.png, .jpg, .hdr, .exr) từ máy tính"
        >
          <Upload size={11} /> Nhập Skybox từ máy
        </button>
      </div>

      {/* Quick Mode Buttons (Auto, None, Presets) */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 3 }}>
        {/* Auto Mode Button */}
        <button
          type="button"
          style={{
            padding: '4px',
            fontSize: 9,
            fontWeight: isAutoMode ? 700 : 500,
            borderRadius: 4,
            border: `1px solid ${isAutoMode ? '#38bdf8' : 'rgba(255,255,255,0.08)'}`,
            background: isAutoMode ? 'rgba(56, 189, 248, 0.25)' : 'rgba(0,0,0,0.3)',
            color: isAutoMode ? '#38bdf8' : '#94a3b8',
            boxShadow: isAutoMode ? '0 0 8px rgba(56, 189, 248, 0.4)' : 'none',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 3,
          }}
          onClick={() => onChange({ ...override, skybox_type: 'auto', weather_preset: 'custom' })}
          title="Tự động chọn ảnh từ thư mục assets/SkyBoxs theo thời gian & độ phủ mây"
        >
          <RefreshCw size={10} /> ⚡ Tự Động
        </button>

        {/* None / Manual Color */}
        <button
          type="button"
          style={{
            padding: '4px',
            fontSize: 9,
            fontWeight: currentSkyboxType === 'none' ? 700 : 500,
            borderRadius: 4,
            border: `1px solid ${currentSkyboxType === 'none' ? '#f59e0b' : 'rgba(255,255,255,0.08)'}`,
            background: currentSkyboxType === 'none' ? 'rgba(245, 158, 11, 0.2)' : 'rgba(0,0,0,0.3)',
            color: currentSkyboxType === 'none' ? '#fef08a' : '#94a3b8',
            cursor: 'pointer',
          }}
          onClick={() => onChange({ ...override, skybox_type: 'none', weather_preset: 'custom' })}
          title="Không dùng Skybox 360, dùng màu nền 3D theo giờ"
        >
          Tắt (Màu 3D)
        </button>

        {/* Day Preset */}
        <button
          type="button"
          style={{
            padding: '4px',
            fontSize: 9,
            fontWeight: currentSkyboxType === 'day' ? 700 : 500,
            borderRadius: 4,
            border: `1px solid ${currentSkyboxType === 'day' ? '#f59e0b' : 'rgba(255,255,255,0.08)'}`,
            background: currentSkyboxType === 'day' ? 'rgba(245, 158, 11, 0.2)' : 'rgba(0,0,0,0.3)',
            color: currentSkyboxType === 'day' ? '#fef08a' : '#94a3b8',
            cursor: 'pointer',
          }}
          onClick={() => onChange({ ...override, skybox_type: 'day', weather_preset: 'custom' })}
        >
          Nắng Ngày
        </button>

        {/* Night Preset */}
        <button
          type="button"
          style={{
            padding: '4px',
            fontSize: 9,
            fontWeight: currentSkyboxType === 'night' ? 700 : 500,
            borderRadius: 4,
            border: `1px solid ${currentSkyboxType === 'night' ? '#f59e0b' : 'rgba(255,255,255,0.08)'}`,
            background: currentSkyboxType === 'night' ? 'rgba(245, 158, 11, 0.2)' : 'rgba(0,0,0,0.3)',
            color: currentSkyboxType === 'night' ? '#fef08a' : '#94a3b8',
            cursor: 'pointer',
          }}
          onClick={() => onChange({ ...override, skybox_type: 'night', weather_preset: 'custom' })}
        >
          Đêm Sao
        </button>
      </div>

      {/* Auto Dynamic Mode Active Indicator */}
      {isAutoMode && (
        <div
          style={{
            padding: '4px 8px',
            fontSize: 9,
            borderRadius: 4,
            background: 'rgba(56, 189, 248, 0.12)',
            border: '1px solid rgba(56, 189, 248, 0.3)',
            color: '#7dd3fc',
            display: 'flex',
            alignItems: 'center',
            gap: 4,
          }}
        >
          <Sparkles size={11} color="#38bdf8" />
          <span>Đang tự động lấy ảnh từ <code>assets/SkyBoxs/</code> theo thanh trượt Giờ & Mây!</span>
        </div>
      )}

      {/* Custom Uploaded Skybox Info */}
      {isCustomMode && override.skybox_url && (
        <div
          style={{
            padding: '4px 8px',
            fontSize: 9,
            borderRadius: 4,
            background: 'rgba(34, 197, 94, 0.12)',
            border: '1px solid rgba(34, 197, 94, 0.3)',
            color: '#86efac',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <span>✓ Đang áp dụng Skybox tải lên từ máy tính</span>
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            style={{
              background: 'transparent',
              border: 'none',
              color: '#4ade80',
              cursor: 'pointer',
              textDecoration: 'underline',
              fontSize: 9,
            }}
          >
            Đổi ảnh
          </button>
        </div>
      )}

      {/* Toggle Catalog Explorer */}
      <button
        type="button"
        onClick={() => setShowCatalogExplorer(!showCatalogExplorer)}
        style={{
          padding: '3px 6px',
          fontSize: 9,
          borderRadius: 4,
          background: 'rgba(255, 255, 255, 0.05)',
          border: '1px solid rgba(255, 255, 255, 0.1)',
          color: '#cbd5e1',
          cursor: 'pointer',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginTop: 2,
        }}
      >
        <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          <Layers size={11} /> 📂 Duyệt Thư Viện Thư Mục SkyBoxs ({SkyboxManager.CATALOG.length} ảnh)
        </span>
        <span style={{ fontSize: 8, color: '#94a3b8' }}>
          {showCatalogExplorer ? '▲ Thu gọn' : '▼ Mở danh sách'}
        </span>
      </button>

      {/* Hierarchical Skybox Explorer */}
      {showCatalogExplorer && (
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: 6,
            background: 'rgba(15, 23, 42, 0.8)',
            padding: 8,
            borderRadius: 6,
            border: '1px solid rgba(255, 255, 255, 0.1)',
            maxHeight: 220,
            overflowY: 'auto',
          }}
        >
          {/* Time Filter Row */}
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3 }}>
            <span style={{ fontSize: 9, color: '#94a3b8', width: '100%' }}>Lọc theo Thời Điểm:</span>
            <button
              type="button"
              style={{
                padding: '2px 5px', fontSize: 8, borderRadius: 3, cursor: 'pointer',
                background: selectedTimeFilter === 'all' ? '#f59e0b' : 'rgba(255,255,255,0.06)',
                color: selectedTimeFilter === 'all' ? '#000000' : '#cbd5e1',
                fontWeight: selectedTimeFilter === 'all' ? 700 : 400,
                border: 'none',
              }}
              onClick={() => setSelectedTimeFilter('all')}
            >
              Tất cả
            </button>
            {SkyboxManager.CATEGORIES.map((cat) => (
              <button
                key={cat.key}
                type="button"
                style={{
                  padding: '2px 5px', fontSize: 8, borderRadius: 3, cursor: 'pointer',
                  background: selectedTimeFilter === cat.key ? '#f59e0b' : 'rgba(255,255,255,0.06)',
                  color: selectedTimeFilter === cat.key ? '#000000' : '#cbd5e1',
                  fontWeight: selectedTimeFilter === cat.key ? 700 : 400,
                  border: 'none',
                }}
                onClick={() => setSelectedTimeFilter(cat.key)}
              >
                {cat.label.split(' ')[0]} {cat.label.split(' ')[1]}
              </button>
            ))}
          </div>

          {/* Cloud Level Filter Row */}
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3 }}>
            <span style={{ fontSize: 9, color: '#94a3b8', width: '100%' }}>Lọc theo Độ Phủ Mây:</span>
            <button
              type="button"
              style={{
                padding: '2px 5px', fontSize: 8, borderRadius: 3, cursor: 'pointer',
                background: selectedCloudFilter === 'all' ? '#38bdf8' : 'rgba(255,255,255,0.06)',
                color: selectedCloudFilter === 'all' ? '#000000' : '#cbd5e1',
                fontWeight: selectedCloudFilter === 'all' ? 700 : 400,
                border: 'none',
              }}
              onClick={() => setSelectedCloudFilter('all')}
            >
              Tất cả
            </button>
            {SkyboxManager.CLOUD_LEVELS.map((cl) => (
              <button
                key={cl.key}
                type="button"
                style={{
                  padding: '2px 5px', fontSize: 8, borderRadius: 3, cursor: 'pointer',
                  background: selectedCloudFilter === cl.key ? '#38bdf8' : 'rgba(255,255,255,0.06)',
                  color: selectedCloudFilter === cl.key ? '#000000' : '#cbd5e1',
                  fontWeight: selectedCloudFilter === cl.key ? 700 : 400,
                  border: 'none',
                }}
                onClick={() => setSelectedCloudFilter(cl.key)}
              >
                {cl.label.split(' ')[0]} {cl.label.split(' ')[1]}
              </button>
            ))}
          </div>

          {/* Skybox List */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 3, marginTop: 4 }}>
            {filteredCatalog.map((item) => {
              const isSelected = override.skybox_url === item.url || override.skybox_type === item.id;
              return (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => {
                    onChange({
                      ...override,
                      skybox_type: 'custom',
                      skybox_url: item.url,
                      weather_preset: 'custom',
                    });
                  }}
                  style={{
                    padding: '4px 6px',
                    borderRadius: 4,
                    background: isSelected ? 'rgba(245, 158, 11, 0.25)' : 'rgba(255, 255, 255, 0.04)',
                    border: `1px solid ${isSelected ? '#f59e0b' : 'rgba(255, 255, 255, 0.08)'}`,
                    color: isSelected ? '#fef08a' : '#e2e8f0',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    textAlign: 'left',
                    fontSize: 9,
                  }}
                >
                  <span style={{ fontWeight: isSelected ? 700 : 500 }}>
                    {item.name}
                  </span>
                  <span style={{ fontSize: 8, color: '#94a3b8' }}>
                    {item.timeCategory} / {item.cloudLevel}
                  </span>
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
};
