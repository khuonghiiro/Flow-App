import React from 'react';
import { Pipette, Sliders } from 'lucide-react';
import { ChromaProcessOptions } from '../../../../core/utils/ChromaDespeckleProcessor';

export interface SlicerChromaKeyCardProps {
  keyColorType: 'chroma_green' | 'pure_white' | 'custom';
  setKeyColorType: (type: 'chroma_green' | 'pure_white' | 'custom') => void;
  keyColorHex: string;
  setKeyColorHex: (hex: string) => void;
  isEyedropperActive?: boolean;
  setIsEyedropperActive?: (active: boolean) => void;
  eyedropperTarget?: 'chroma' | 'fringe' | 'smooth';
  setEyedropperTarget?: (target: 'chroma' | 'fringe' | 'smooth') => void;
  isolationMode: 'all' | 'outer_only';
  setIsolationMode: (mode: 'all' | 'outer_only') => void;
  tolerance: number;
  setTolerance: (v: number) => void;
  feather: number;
  setFeather: (v: number) => void;
  shadowRetention: number;
  setShadowRetention: (v: number) => void;
  strokeWidth: number;
  setStrokeWidth: (w: number) => void;
  strokeColorHex: string;
  setStrokeColorHex: (c: string) => void;
  onCommitSliderChange?: (overrides?: Partial<ChromaProcessOptions>) => void;
}

export const SlicerChromaKeyCard: React.FC<SlicerChromaKeyCardProps> = ({
  keyColorType,
  setKeyColorType,
  keyColorHex,
  setKeyColorHex,
  isEyedropperActive,
  setIsEyedropperActive,
  eyedropperTarget,
  setEyedropperTarget,
  isolationMode,
  setIsolationMode,
  tolerance,
  setTolerance,
  feather,
  setFeather,
  shadowRetention,
  setShadowRetention,
  strokeWidth,
  setStrokeWidth,
  strokeColorHex,
  setStrokeColorHex,
  onCommitSliderChange,
}) => {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
      {/* Color Selection & Eyedropper Card */}
      <div style={{ background: 'rgba(15, 23, 42, 0.65)', padding: 9, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', display: 'flex', flexDirection: 'column', gap: 7 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontSize: 10.5, color: '#e2e8f0', fontWeight: 600, display: 'flex', alignItems: 'center', gap: 5 }}>
            🎯 Chọn màu nền cần bóc:
          </span>
          <div style={{ display: 'flex', alignItems: 'center', gap: 5, padding: '2px 7px', background: '#090e1a', borderRadius: 4, border: '1px solid rgba(255,255,255,0.15)' }}>
            <div
              style={{
                width: 14,
                height: 14,
                borderRadius: 3,
                background: keyColorType === 'chroma_green' ? '#00ff00' : keyColorType === 'pure_white' ? '#ffffff' : keyColorHex,
                border: '1px solid rgba(255,255,255,0.4)',
              }}
            />
            <span style={{ fontSize: 10, fontFamily: 'monospace', fontWeight: 700, color: '#38bdf8' }}>
              {keyColorType === 'chroma_green' ? '#00FF00' : keyColorType === 'pure_white' ? '#FFFFFF' : keyColorHex.toUpperCase()}
            </span>
          </div>
        </div>

        {/* Color Presets Grid */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 5 }}>
          <button
            onClick={() => {
              setKeyColorType('chroma_green');
              if (setIsEyedropperActive) setIsEyedropperActive(false);
              if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'chroma_green' });
            }}
            style={{
              height: 30,
              fontSize: 10,
              fontWeight: 600,
              borderRadius: 6,
              border: keyColorType === 'chroma_green' ? '1.5px solid #22c55e' : '1px solid rgba(255,255,255,0.1)',
              background: keyColorType === 'chroma_green' ? 'rgba(34, 197, 94, 0.25)' : 'rgba(0,0,0,0.3)',
              color: keyColorType === 'chroma_green' ? '#4ade80' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
            }}
          >
            <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#22c55e', display: 'inline-block' }} />
            Xanh lá
          </button>

          <button
            onClick={() => {
              setKeyColorType('pure_white');
              if (setIsEyedropperActive) setIsEyedropperActive(false);
              if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'pure_white' });
            }}
            style={{
              height: 30,
              fontSize: 10,
              fontWeight: 600,
              borderRadius: 6,
              border: keyColorType === 'pure_white' ? '1.5px solid #ffffff' : '1px solid rgba(255,255,255,0.1)',
              background: keyColorType === 'pure_white' ? 'rgba(255,255,255,0.2)' : 'rgba(0,0,0,0.3)',
              color: keyColorType === 'pure_white' ? '#ffffff' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
            }}
          >
            <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#ffffff', border: '1px solid #666', display: 'inline-block' }} />
            Trắng tinh
          </button>

          <button
            onClick={() => {
              setKeyColorType('custom');
              if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'custom', keyColorHex });
            }}
            style={{
              height: 30,
              fontSize: 10,
              fontWeight: 600,
              borderRadius: 6,
              border: keyColorType === 'custom' ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
              background: keyColorType === 'custom' ? 'rgba(56, 189, 248, 0.25)' : 'rgba(0,0,0,0.3)',
              color: keyColorType === 'custom' ? '#38bdf8' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
            }}
          >
            🎨 Tùy chọn
          </button>
        </div>

        {/* Eyedropper Row */}
        <div style={{ display: 'flex', gap: 6, alignItems: 'center', background: 'rgba(0,0,0,0.35)', padding: 6, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
          <button
            onClick={() => {
              if (setEyedropperTarget) setEyedropperTarget('chroma');
              if (setIsEyedropperActive) setIsEyedropperActive(!isEyedropperActive);
            }}
            style={{
              flex: 1,
              height: 32,
              fontSize: 10.5,
              fontWeight: 700,
              borderRadius: 5,
              background: isEyedropperActive && eyedropperTarget === 'chroma'
                ? 'linear-gradient(135deg, #f59e0b 0%, #d97706 100%)'
                : 'linear-gradient(135deg, rgba(56,189,248,0.2) 0%, rgba(2,132,199,0.2) 100%)',
              color: isEyedropperActive && eyedropperTarget === 'chroma' ? '#000000' : '#38bdf8',
              border: isEyedropperActive && eyedropperTarget === 'chroma' ? '1.5px solid #fbbf24' : '1px solid rgba(56,189,248,0.4)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
            }}
            title="Hút màu từ ảnh"
          >
            <Pipette size={14} />
            {isEyedropperActive && eyedropperTarget === 'chroma' ? '🎯 Đang hút màu...' : '💧 Hút màu từ ảnh'}
          </button>

          <input
            type="color"
            value={keyColorType === 'chroma_green' ? '#00ff00' : keyColorType === 'pure_white' ? '#ffffff' : keyColorHex}
            onChange={(e) => {
              setKeyColorType('custom');
              setKeyColorHex(e.target.value);
              if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'custom', keyColorHex: e.target.value });
            }}
            style={{ width: 32, height: 32, padding: 0, border: 'none', borderRadius: 4, cursor: 'pointer', background: 'transparent' }}
          />
          <input
            type="text"
            value={keyColorHex}
            onChange={(e) => {
              setKeyColorType('custom');
              setKeyColorHex(e.target.value);
              if (onCommitSliderChange && e.target.value.length === 7) {
                onCommitSliderChange({ keyColorType: 'custom', keyColorHex: e.target.value });
              }
            }}
            style={{ width: 72, height: 32, padding: '0 6px', fontSize: 10.5, background: '#090e1a', color: '#38bdf8', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 4, fontFamily: 'monospace', textAlign: 'center', fontWeight: 600 }}
            placeholder="#000000"
          />
        </div>
      </div>

      {/* Isolation Mode */}
      <div>
        <div style={{ fontSize: 9.5, color: '#94a3b8', marginBottom: 4, fontWeight: 600 }}>Phạm vi bóc tách:</div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.2fr', gap: 5 }}>
          <button
            onClick={() => {
              setIsolationMode('all');
              if (onCommitSliderChange) onCommitSliderChange({ isolationMode: 'all' });
            }}
            style={{
              height: 30,
              fontSize: 10,
              fontWeight: 600,
              borderRadius: 6,
              border: isolationMode === 'all' ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
              background: isolationMode === 'all' ? 'rgba(56, 189, 248, 0.25)' : 'rgba(0,0,0,0.3)',
              color: isolationMode === 'all' ? '#38bdf8' : '#94a3b8',
              cursor: 'pointer',
            }}
          >
            🌐 Toàn bộ ảnh
          </button>
          <button
            onClick={() => {
              setIsolationMode('outer_only');
              if (onCommitSliderChange) onCommitSliderChange({ isolationMode: 'outer_only' });
            }}
            style={{
              height: 30,
              fontSize: 10,
              fontWeight: 600,
              borderRadius: 6,
              border: isolationMode === 'outer_only' ? '1.5px solid #22c55e' : '1px solid rgba(255,255,255,0.08)',
              background: isolationMode === 'outer_only' ? 'rgba(34, 197, 94, 0.25)' : 'rgba(0,0,0,0.3)',
              color: isolationMode === 'outer_only' ? '#4ade80' : '#94a3b8',
              cursor: 'pointer',
            }}
            title="Chỉ tách màu từ 4 cạnh viền ngoài, bảo vệ tròng mắt và trang phục bên trong"
          >
            🛡️ Bảo vệ ruột (BFS)
          </button>
        </div>
      </div>

      {/* Sliders: Tolerance, Feather, Shadow, Stroke */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 7, background: 'rgba(0,0,0,0.3)', padding: 9, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
        {/* Tolerance */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
            <span style={{ fontWeight: 600, color: '#38bdf8' }}>Độ nhạy khử màu (Tolerance):</span>
            <span style={{ color: '#4ade80', fontWeight: 700 }}>{tolerance}%</span>
          </div>
          <input
            type="range"
            min="0"
            max="100"
            value={tolerance}
            onChange={(e) => setTolerance(parseInt(e.target.value, 10))}
            onPointerUp={() => onCommitSliderChange && onCommitSliderChange({ tolerance })}
            style={{ width: '100%', accentColor: '#38bdf8' }}
          />
        </div>

        {/* Feather */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
            <span style={{ fontWeight: 600 }}>Làm mềm biên viền (Feather):</span>
            <span style={{ color: '#38bdf8', fontWeight: 700 }}>{feather}px</span>
          </div>
          <input
            type="range"
            min="0"
            max="10"
            value={feather}
            onChange={(e) => setFeather(parseInt(e.target.value, 10))}
            onPointerUp={() => onCommitSliderChange && onCommitSliderChange({ feather })}
            style={{ width: '100%', accentColor: '#38bdf8' }}
          />
        </div>

        {/* Shadow Retention */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
            <span style={{ fontWeight: 600 }}>Giữ bóng đổ (Shadow Retention):</span>
            <span style={{ color: '#a78bfa', fontWeight: 700 }}>{shadowRetention}%</span>
          </div>
          <input
            type="range"
            min="0"
            max="100"
            value={shadowRetention}
            onChange={(e) => setShadowRetention(parseInt(e.target.value, 10))}
            onPointerUp={() => onCommitSliderChange && onCommitSliderChange({ shadowRetention })}
            style={{ width: '100%', accentColor: '#a78bfa' }}
          />
        </div>

        {/* Stroke Outline */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
            <span style={{ fontWeight: 600 }}>Viền nét Anime (Stroke Outline):</span>
            <span style={{ color: '#f59e0b', fontWeight: 700 }}>{strokeWidth}px</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <input
              type="range"
              min="0"
              max="10"
              value={strokeWidth}
              onChange={(e) => setStrokeWidth(parseInt(e.target.value, 10))}
              onPointerUp={() => onCommitSliderChange && onCommitSliderChange({ strokeWidth })}
              style={{ flex: 1, accentColor: '#f59e0b' }}
            />
            <input
              type="color"
              value={strokeColorHex}
              onChange={(e) => {
                setStrokeColorHex(e.target.value);
                if (onCommitSliderChange) onCommitSliderChange({ strokeColorHex: e.target.value });
              }}
              style={{ width: 22, height: 22, border: 'none', borderRadius: 3, cursor: 'pointer', background: 'transparent' }}
              title="Màu viền Anime"
            />
          </div>
        </div>
      </div>
    </div>
  );
};
