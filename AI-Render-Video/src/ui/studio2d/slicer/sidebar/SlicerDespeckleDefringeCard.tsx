import React from 'react';
import { Pipette, Sparkles } from 'lucide-react';
import { ChromaProcessOptions } from '../../../../core/utils/ChromaDespeckleProcessor';

export interface SlicerDespeckleDefringeCardProps {
  cleanupMode: 'all' | 'defringe' | 'smooth' | 'despeckle';
  setCleanupMode: (m: 'all' | 'defringe' | 'smooth' | 'despeckle') => void;
  fringeColorType: 'chroma_green' | 'pure_white' | 'pure_black' | 'custom';
  setFringeColorType: (t: 'chroma_green' | 'pure_white' | 'pure_black' | 'custom') => void;
  fringeColorHex: string;
  setFringeColorHex: (hex: string) => void;
  isEyedropperActive?: boolean;
  setIsEyedropperActive?: (active: boolean) => void;
  eyedropperTarget?: 'chroma' | 'fringe' | 'smooth';
  setEyedropperTarget?: (t: 'chroma' | 'fringe' | 'smooth') => void;
  defringeStrength: number;
  setDefringeStrength: (v: number) => void;
  edgeChoke: number;
  setEdgeChoke: (v: number) => void;
  edgeSmooth: number;
  setEdgeSmooth: (v: number) => void;
  smoothColorType: 'black' | 'white' | 'auto' | 'custom';
  setSmoothColorType: (t: 'black' | 'white' | 'auto' | 'custom') => void;
  smoothColorHex: string;
  setSmoothColorHex: (hex: string) => void;
  despeckleSize: number;
  setDespeckleSize: (v: number) => void;
  whiteSpeckleSensitivity: number;
  setWhiteSpeckleSensitivity: (v: number) => void;
  keepLargestIslandOnly: boolean;
  setKeepLargestIslandOnly: (v: boolean) => void;
  isProcessing: boolean;
  onRunDespeckleOnly?: () => void;
  onCommitSliderChange?: (overrides?: Partial<ChromaProcessOptions>) => void;
}

export const SlicerDespeckleDefringeCard: React.FC<SlicerDespeckleDefringeCardProps> = ({
  cleanupMode,
  setCleanupMode,
  fringeColorType,
  setFringeColorType,
  fringeColorHex,
  setFringeColorHex,
  isEyedropperActive,
  setIsEyedropperActive,
  eyedropperTarget,
  setEyedropperTarget,
  defringeStrength,
  setDefringeStrength,
  edgeChoke,
  setEdgeChoke,
  edgeSmooth,
  setEdgeSmooth,
  smoothColorType,
  setSmoothColorType,
  smoothColorHex,
  setSmoothColorHex,
  despeckleSize,
  setDespeckleSize,
  whiteSpeckleSensitivity,
  setWhiteSpeckleSensitivity,
  keepLargestIslandOnly,
  setKeepLargestIslandOnly,
  isProcessing,
  onRunDespeckleOnly,
  onCommitSliderChange,
}) => {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
      {/* Cleanup Mode Selector Chips */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.15fr 1.15fr 1fr', gap: 4 }}>
        {[
          { id: 'all', label: '⚡ Tất cả' },
          { id: 'defringe', label: '🎨 Khử màu' },
          { id: 'smooth', label: '✨ Mịn viền' },
          { id: 'despeckle', label: '🧹 Lọc bụi' },
        ].map((m) => (
          <button
            key={m.id}
            onClick={() => {
              setCleanupMode(m.id as any);
              if (onCommitSliderChange) onCommitSliderChange({ cleanupMode: m.id as any });
            }}
            style={{
              height: 26,
              fontSize: 9,
              fontWeight: 600,
              borderRadius: 4,
              border: cleanupMode === m.id ? '1.5px solid #10b981' : '1px solid rgba(255,255,255,0.08)',
              background: cleanupMode === m.id ? 'rgba(16, 185, 129, 0.25)' : 'rgba(0,0,0,0.3)',
              color: cleanupMode === m.id ? '#4ade80' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            {m.label}
          </button>
        ))}
      </div>

      {/* SECTION 1: KHỬ MÀU VIỀN BÁM (COLOR DEFRINGE) */}
      {(cleanupMode === 'all' || cleanupMode === 'defringe') && (
        <div style={{ background: 'rgba(15, 23, 42, 0.65)', padding: 8, borderRadius: 7, border: '1px solid rgba(16, 185, 129, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 10, color: '#e2e8f0', fontWeight: 600 }}>🎨 Màu viền rác cần khử:</span>
            <div style={{ display: 'flex', alignItems: 'center', gap: 4, padding: '2px 6px', background: '#090e1a', borderRadius: 4, border: '1px solid rgba(255,255,255,0.15)' }}>
              <div
                style={{
                  width: 12,
                  height: 12,
                  borderRadius: 2,
                  background: fringeColorType === 'chroma_green' ? '#00ff00' : fringeColorType === 'pure_white' ? '#ffffff' : fringeColorType === 'pure_black' ? '#000000' : fringeColorHex,
                  border: '1px solid rgba(255,255,255,0.4)',
                }}
              />
              <span style={{ fontSize: 9.5, fontFamily: 'monospace', fontWeight: 700, color: '#4ade80' }}>
                {fringeColorType === 'chroma_green' ? '#00FF00' : fringeColorType === 'pure_white' ? '#FFFFFF' : fringeColorType === 'pure_black' ? '#000000' : fringeColorHex.toUpperCase()}
              </span>
            </div>
          </div>

          {/* Fringe Presets Grid */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 4 }}>
            {[
              { id: 'chroma_green', label: 'Xanh lá', color: '#22c55e' },
              { id: 'pure_white', label: 'Trắng', color: '#ffffff' },
              { id: 'pure_black', label: 'Đen', color: '#000000' },
              { id: 'custom', label: 'Tùy chọn', color: '#38bdf8' },
            ].map((p) => (
              <button
                key={p.id}
                onClick={() => {
                  setFringeColorType(p.id as any);
                  if (onCommitSliderChange) onCommitSliderChange({ fringeColorType: p.id as any, fringeColorHex });
                }}
                style={{
                  height: 28,
                  fontSize: 9.5,
                  fontWeight: 600,
                  borderRadius: 5,
                  border: fringeColorType === p.id ? '1.5px solid #10b981' : '1px solid rgba(255,255,255,0.1)',
                  background: fringeColorType === p.id ? 'rgba(16, 185, 129, 0.25)' : 'rgba(0,0,0,0.3)',
                  color: fringeColorType === p.id ? '#4ade80' : '#94a3b8',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 3,
                }}
              >
                <span style={{ width: 7, height: 7, borderRadius: '50%', background: p.color, border: p.id === 'pure_white' ? '1px solid #666' : 'none' }} />
                {p.label}
              </button>
            ))}
          </div>

          {/* Eyedropper & Custom Hex Input */}
          <div style={{ display: 'flex', gap: 5, alignItems: 'center', background: 'rgba(0,0,0,0.35)', padding: 5, borderRadius: 5, border: '1px solid rgba(255,255,255,0.06)' }}>
            <button
              onClick={() => {
                if (setEyedropperTarget) setEyedropperTarget('fringe');
                if (setIsEyedropperActive) setIsEyedropperActive(!isEyedropperActive);
              }}
              style={{
                flex: 1,
                height: 30,
                fontSize: 10,
                fontWeight: 600,
                borderRadius: 4,
                background: isEyedropperActive && eyedropperTarget === 'fringe'
                  ? 'linear-gradient(135deg, #10b981 0%, #059669 100%)'
                  : 'linear-gradient(135deg, rgba(16,185,129,0.2) 0%, rgba(5,150,105,0.2) 100%)',
                color: isEyedropperActive && eyedropperTarget === 'fringe' ? '#ffffff' : '#4ade80',
                border: isEyedropperActive && eyedropperTarget === 'fringe' ? '1.5px solid #34d399' : '1px solid rgba(16,185,129,0.4)',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 5,
              }}
            >
              <Pipette size={13} />
              {isEyedropperActive && eyedropperTarget === 'fringe' ? '🎯 Đang hút...' : '💧 Hút màu viền rác'}
            </button>

            <input
              type="color"
              value={fringeColorType === 'chroma_green' ? '#00ff00' : fringeColorType === 'pure_white' ? '#ffffff' : fringeColorType === 'pure_black' ? '#000000' : fringeColorHex}
              onChange={(e) => {
                setFringeColorType('custom');
                setFringeColorHex(e.target.value);
                if (onCommitSliderChange) onCommitSliderChange({ fringeColorType: 'custom', fringeColorHex: e.target.value });
              }}
              style={{ width: 30, height: 30, padding: 0, border: 'none', borderRadius: 4, cursor: 'pointer', background: 'transparent' }}
            />
            <input
              type="text"
              value={fringeColorHex}
              onChange={(e) => {
                setFringeColorType('custom');
                setFringeColorHex(e.target.value);
                if (onCommitSliderChange && e.target.value.length === 7) {
                  onCommitSliderChange({ fringeColorType: 'custom', fringeColorHex: e.target.value });
                }
              }}
              style={{ width: 68, height: 30, padding: '0 5px', fontSize: 10, background: '#090e1a', color: '#4ade80', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 4, fontFamily: 'monospace', textAlign: 'center', fontWeight: 600 }}
              placeholder="#00FF00"
            />
          </div>

          {/* Defringe Strength Slider */}
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#cbd5e1', marginBottom: 2 }}>
              <span style={{ fontWeight: 600, color: '#4ade80' }}>⚡ Độ mạnh khử viền (Defringe):</span>
              <span style={{ color: '#4ade80', fontWeight: 700 }}>{defringeStrength}%</span>
            </div>
            <input
              type="range"
              min="0"
              max="100"
              value={defringeStrength}
              onChange={(e) => setDefringeStrength(parseInt(e.target.value))}
              onPointerUp={() => onCommitSliderChange && onCommitSliderChange({ defringeStrength })}
              style={{ width: '100%', accentColor: '#4ade80' }}
            />
          </div>
        </div>
      )}

      {/* SECTION 2: GỌT & LÀM MỊN VIỀN SƯỢNG (EDGE CHOKE & SMOOTH) */}
      {(cleanupMode === 'all' || cleanupMode === 'smooth') && (
        <div style={{ background: 'rgba(15, 23, 42, 0.65)', padding: 8, borderRadius: 7, border: '1px solid rgba(56, 189, 248, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
          {/* Edge Choke */}
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#cbd5e1', marginBottom: 2 }}>
              <span style={{ fontWeight: 600, color: '#38bdf8' }}>✂️ Gọt lùi viền sượng (Edge Choke):</span>
              <span style={{ color: '#38bdf8', fontWeight: 700 }}>{edgeChoke}px</span>
            </div>
            <input
              type="range"
              min="0"
              max="10"
              step="1"
              value={edgeChoke}
              onChange={(e) => setEdgeChoke(parseInt(e.target.value))}
              onPointerUp={() => onCommitSliderChange && onCommitSliderChange({ edgeChoke })}
              style={{ width: '100%', accentColor: '#38bdf8' }}
            />
          </div>

          {/* Smooth Color Presets */}
          <div style={{ background: 'rgba(0,0,0,0.3)', padding: '6px 8px', borderRadius: 6, border: '1px solid rgba(56,189,248,0.15)', display: 'flex', flexDirection: 'column', gap: 5 }}>
            <span style={{ fontSize: 9.5, fontWeight: 700, color: '#38bdf8' }}>🎨 Màu viền làm mịn:</span>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 3 }}>
              {[
                { id: 'black', label: 'Đen', color: '#000000' },
                { id: 'white', label: 'Trắng', color: '#ffffff' },
                { id: 'auto', label: 'Màu gốc', color: 'transparent' },
                { id: 'custom', label: 'Tùy chọn', color: smoothColorHex || '#a855f7' },
              ].map((item) => (
                <button
                  key={item.id}
                  onClick={() => {
                    setSmoothColorType(item.id as any);
                    if (onCommitSliderChange) onCommitSliderChange({ smoothColorType: item.id as any, smoothColorHex });
                  }}
                  style={{
                    padding: '4px 2px',
                    fontSize: 9,
                    fontWeight: smoothColorType === item.id ? 700 : 500,
                    borderRadius: 4,
                    background: smoothColorType === item.id ? 'rgba(56,189,248,0.25)' : 'rgba(255,255,255,0.05)',
                    color: smoothColorType === item.id ? '#38bdf8' : '#94a3b8',
                    border: smoothColorType === item.id ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 3,
                  }}
                >
                  {item.label}
                </button>
              ))}
            </div>
          </div>

          {/* Edge Smooth */}
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#cbd5e1', marginBottom: 2 }}>
              <span style={{ fontWeight: 600, color: '#38bdf8' }}>✨ Làm mịn & Khử răng cưa:</span>
              <span style={{ color: '#38bdf8', fontWeight: 700 }}>{edgeSmooth}px</span>
            </div>
            <input
              type="range"
              min="0"
              max="10"
              step="1"
              value={edgeSmooth}
              onChange={(e) => setEdgeSmooth(parseInt(e.target.value))}
              onPointerUp={() => onCommitSliderChange && onCommitSliderChange({ edgeSmooth })}
              style={{ width: '100%', accentColor: '#38bdf8' }}
            />
          </div>
        </div>
      )}

      {/* SECTION 3: LỌC BỤI & ĐỐM VỤN */}
      {(cleanupMode === 'all' || cleanupMode === 'despeckle') && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#cbd5e1', marginBottom: 2 }}>
              <span style={{ fontWeight: 600, color: '#4ade80' }}>🧹 Khử đốm rác nhỏ (Despeckle):</span>
              <span style={{ color: '#4ade80', fontWeight: 700 }}>{despeckleSize}px</span>
            </div>
            <input
              type="range"
              min="0"
              max="100"
              value={despeckleSize}
              onChange={(e) => setDespeckleSize(parseInt(e.target.value))}
              onPointerUp={() => onCommitSliderChange && onCommitSliderChange({ despeckleSize })}
              style={{ width: '100%', accentColor: '#4ade80' }}
            />
          </div>

          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#cbd5e1', marginBottom: 2 }}>
              <span style={{ fontWeight: 600, color: '#4ade80' }}>✨ Khử hạt bụi sáng:</span>
              <span style={{ color: '#4ade80', fontWeight: 700 }}>{whiteSpeckleSensitivity}%</span>
            </div>
            <input
              type="range"
              min="0"
              max="100"
              value={whiteSpeckleSensitivity}
              onChange={(e) => setWhiteSpeckleSensitivity(parseInt(e.target.value))}
              onPointerUp={() => onCommitSliderChange && onCommitSliderChange({ whiteSpeckleSensitivity })}
              style={{ width: '100%', accentColor: '#4ade80' }}
            />
          </div>

          <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 9.5, color: '#cbd5e1', cursor: 'pointer', background: 'rgba(0,0,0,0.3)', padding: 6, borderRadius: 5, border: '1px solid rgba(255,255,255,0.06)' }}>
            <input
              type="checkbox"
              checked={keepLargestIslandOnly}
              onChange={(e) => {
                setKeepLargestIslandOnly(e.target.checked);
                if (onCommitSliderChange) onCommitSliderChange({ keepLargestIslandOnly: e.target.checked });
              }}
              style={{ accentColor: '#4ade80' }}
            />
            🏝️ Chỉ giữ cụm linh kiện lớn nhất
          </label>
        </div>
      )}

      {/* Quick Action Button */}
      <button
        onClick={() => {
          if (onRunDespeckleOnly) onRunDespeckleOnly();
        }}
        disabled={isProcessing}
        style={{
          width: '100%',
          height: 34,
          fontSize: 11,
          fontWeight: 700,
          borderRadius: 6,
          background: 'linear-gradient(135deg, #10b981 0%, #059669 100%)',
          color: '#ffffff',
          border: '1px solid rgba(52, 211, 153, 0.4)',
          cursor: isProcessing ? 'not-allowed' : 'pointer',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 5,
          boxShadow: '0 2px 10px rgba(16, 185, 129, 0.35)',
        }}
      >
        <Sparkles size={14} /> 🧹 Áp dụng khử rác & Làm mượt viền ngay
      </button>
    </div>
  );
};
