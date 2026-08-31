import React from 'react';
import { RefreshCw, Wand2 } from 'lucide-react';
import { PromptCustomizerValues } from './types';
import { DEFAULT_CUSTOMIZER_VALUES } from './promptData';

interface PromptCustomizerBarProps {
  values: PromptCustomizerValues;
  onChange: (next: PromptCustomizerValues) => void;
  onReset: () => void;
}

export const PromptCustomizerBar: React.FC<PromptCustomizerBarProps> = ({
  values,
  onChange,
  onReset,
}) => {
  const updateField = (field: keyof PromptCustomizerValues, val: string) => {
    onChange({
      ...values,
      [field]: val,
    });
  };

  return (
    <div
      style={{
        background: 'rgba(15, 23, 42, 0.95)',
        border: '1px solid rgba(56, 189, 248, 0.25)',
        borderRadius: 10,
        padding: '12px 16px',
        marginBottom: 12,
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        boxShadow: '0 4px 20px rgba(0, 0, 0, 0.4)',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, fontWeight: 700, color: '#38bdf8' }}>
          <Wand2 size={14} />
          <span>Bộ Tùy Biến Thông Số Nhân Vật & Hoạt Ảnh (Tự động thay thế vào Prompt)</span>
        </div>
        <button
          onClick={onReset}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '3px 8px',
            fontSize: 10.5,
            borderRadius: 4,
            border: '1px solid rgba(255, 255, 255, 0.15)',
            background: 'rgba(255, 255, 255, 0.05)',
            color: '#94a3b8',
            cursor: 'pointer',
          }}
        >
          <RefreshCw size={11} /> Mặc Định
        </button>
      </div>

      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
          gap: 8,
        }}
      >
        {/* Style */}
        <div>
          <label style={{ fontSize: 10, fontWeight: 700, color: '#94a3b8', display: 'block', marginBottom: 2 }}>
            Phong Cách Nghệ Thuật:
          </label>
          <input
            type="text"
            value={values.style}
            onChange={(e) => updateField('style', e.target.value)}
            style={{
              width: '100%',
              height: 28,
              padding: '2px 8px',
              fontSize: 11,
              background: '#090d16',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              borderRadius: 4,
              color: '#f8fafc',
            }}
          />
        </div>

        {/* Gender & Age */}
        <div>
          <label style={{ fontSize: 10, fontWeight: 700, color: '#94a3b8', display: 'block', marginBottom: 2 }}>
            Giới Tính & Độ Tuổi:
          </label>
          <div style={{ display: 'flex', gap: 4 }}>
            <select
              value={values.gender}
              onChange={(e) => updateField('gender', e.target.value)}
              style={{
                width: '45%',
                height: 28,
                padding: '2px 4px',
                fontSize: 10.5,
                background: '#090d16',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                borderRadius: 4,
                color: '#f8fafc',
              }}
            >
              <option value="female">Nữ (Female)</option>
              <option value="male">Nam (Male)</option>
              <option value="non-binary">Khác</option>
            </select>
            <input
              type="text"
              value={values.age}
              onChange={(e) => updateField('age', e.target.value)}
              placeholder="Tuổi..."
              style={{
                width: '55%',
                height: 28,
                padding: '2px 6px',
                fontSize: 10.5,
                background: '#090d16',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                borderRadius: 4,
                color: '#f8fafc',
              }}
            />
          </div>
        </div>

        {/* Hair Style & Color */}
        <div>
          <label style={{ fontSize: 10, fontWeight: 700, color: '#94a3b8', display: 'block', marginBottom: 2 }}>
            Kiểu & Màu Tóc:
          </label>
          <input
            type="text"
            value={values.hairStyleColor}
            onChange={(e) => updateField('hairStyleColor', e.target.value)}
            style={{
              width: '100%',
              height: 28,
              padding: '2px 8px',
              fontSize: 11,
              background: '#090d16',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              borderRadius: 4,
              color: '#f8fafc',
            }}
          />
        </div>

        {/* Outfit Description */}
        <div>
          <label style={{ fontSize: 10, fontWeight: 700, color: '#94a3b8', display: 'block', marginBottom: 2 }}>
            Trang Phục & Màu Chủ Đạo:
          </label>
          <input
            type="text"
            value={values.outfitDescription}
            onChange={(e) => updateField('outfitDescription', e.target.value)}
            style={{
              width: '100%',
              height: 28,
              padding: '2px 8px',
              fontSize: 11,
              background: '#090d16',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              borderRadius: 4,
              color: '#f8fafc',
            }}
          />
        </div>

        {/* Weapon Type */}
        <div>
          <label style={{ fontSize: 10, fontWeight: 700, color: '#94a3b8', display: 'block', marginBottom: 2 }}>
            Vũ Khí Mặc Định:
          </label>
          <input
            type="text"
            value={values.weaponType}
            onChange={(e) => updateField('weaponType', e.target.value)}
            style={{
              width: '100%',
              height: 28,
              padding: '2px 8px',
              fontSize: 11,
              background: '#090d16',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              borderRadius: 4,
              color: '#f8fafc',
            }}
          />
        </div>

        {/* Chroma Background */}
        <div>
          <label style={{ fontSize: 10, fontWeight: 700, color: '#94a3b8', display: 'block', marginBottom: 2 }}>
            Mã Màu Nền Tách Nền (Chroma Key):
          </label>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <input
              type="color"
              value={values.chromaBgHex}
              onChange={(e) => updateField('chromaBgHex', e.target.value)}
              style={{
                width: 30,
                height: 28,
                padding: 0,
                background: 'none',
                border: 'none',
                cursor: 'pointer',
              }}
            />
            <input
              type="text"
              value={values.chromaBgHex}
              onChange={(e) => updateField('chromaBgHex', e.target.value)}
              style={{
                flex: 1,
                height: 28,
                padding: '2px 8px',
                fontSize: 11,
                background: '#090d16',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                borderRadius: 4,
                color: '#34d399',
                fontFamily: 'monospace',
                fontWeight: 700,
              }}
            />
          </div>
        </div>
      </div>
    </div>
  );
};
