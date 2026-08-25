import React, { useState } from 'react';
import { Grid, X, Check, Sparkles, Sliders } from 'lucide-react';
import { saveCachedGridConfig } from '../../../core/assets/slicer/SlicerAngleConstants';

interface GridTablePickerModalProps {
  isOpen: boolean;
  onClose: () => void;
  currentRows: number;
  currentCols: number;
  onSelectGrid: (rows: number, cols: number) => void;
}

const MATRIX_MAX_ROWS = 8;
const MATRIX_MAX_COLS = 8;

const QUICK_PRESETS = [
  { label: '🖼️ 1 Ô Đơn (Toàn ảnh)', rows: 1, cols: 1, desc: 'Tắt chia lưới, giữ nguyên 100% ảnh gốc' },
  { label: '✂️ 2 Ô Ngang Khít Ảnh (1×2)', rows: 1, cols: 2, desc: 'Chia đôi theo chiều ngang (Trái - Phải)' },
  { label: '✂️ 2 Ô Dọc Khít Ảnh (2×1)', rows: 2, cols: 1, desc: 'Chia đôi theo chiều dọc (Trên - Dưới)' },
  { label: '🎬 4 Ô Vuông HD (2×2)', rows: 2, cols: 2, desc: '4 góc quay trọng tâm (512x512)' },
  { label: '🎬 6 Góc Điện Ảnh (2×3)', rows: 2, cols: 3, desc: 'Chuẩn 6 góc quay Anime tiêu chuẩn' },
  { label: '📐 8 Ô Đa Năng (2×4)', rows: 2, cols: 4, desc: '8 góc quay chi tiết toàn thân' },
  { label: '💇 12 Ô Bóc Tách (3×4)', rows: 3, cols: 4, desc: 'Lưới bóc tách linh kiện đa tầng' },
  { label: '💇 20 Ô Đa Tầng (4×5)', rows: 4, cols: 5, desc: 'Lưới tóc / ngũ quan siêu bóc tách 20 ô' },
];

export const GridTablePickerModal: React.FC<GridTablePickerModalProps> = ({
  isOpen,
  onClose,
  currentRows,
  currentCols,
  onSelectGrid,
}) => {
  const [hoveredRows, setHoveredRows] = useState<number | null>(null);
  const [hoveredCols, setHoveredCols] = useState<number | null>(null);

  const [inputRows, setInputRows] = useState<number>(currentRows);
  const [inputCols, setInputCols] = useState<number>(currentCols);

  if (!isOpen) return null;

  const activeR = hoveredRows !== null ? hoveredRows : inputRows;
  const activeC = hoveredCols !== null ? hoveredCols : inputCols;

  const handleApply = (r: number, c: number) => {
    const validR = Math.max(1, Math.min(12, r));
    const validC = Math.max(1, Math.min(12, c));
    saveCachedGridConfig(validR, validC);
    onSelectGrid(validR, validC);
    onClose();
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 10000,
        background: 'rgba(3, 7, 18, 0.85)',
        backdropFilter: 'blur(12px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 16,
      }}
      onClick={onClose}
    >
      <div
        style={{
          width: '95vw',
          maxWidth: '560px',
          background: '#0b0f19',
          border: '1px solid rgba(56, 189, 248, 0.35)',
          borderRadius: 12,
          boxShadow: '0 20px 50px rgba(0, 0, 0, 0.85), 0 0 30px rgba(56, 189, 248, 0.2)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
          fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div
          style={{
            padding: '12px 16px',
            background: 'rgba(15, 23, 42, 0.95)',
            borderBottom: '1px solid rgba(255, 255, 255, 0.1)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div
              style={{
                width: 28,
                height: 28,
                borderRadius: 6,
                background: 'linear-gradient(135deg, #0284c7, #38bdf8)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#fff',
              }}
            >
              <Grid size={16} />
            </div>
            <div>
              <div style={{ fontSize: 13, fontWeight: 800, color: '#f8fafc' }}>
                Chọn Ma Trận Khung Lưới Cắt (Table Grid Selector)
              </div>
              <div style={{ fontSize: 10, color: '#94a3b8' }}>
                Rê chuột chọn số dòng × cột như bảng Word hoặc chọn preset nhanh
              </div>
            </div>
          </div>

          <button
            onClick={onClose}
            style={{
              width: 28,
              height: 28,
              borderRadius: 6,
              background: 'rgba(255, 255, 255, 0.05)',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              color: '#94a3b8',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer',
            }}
          >
            <X size={14} />
          </button>
        </div>

        {/* Body */}
        <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 14 }}>
          {/* Active Hover / Selection Display Banner */}
          <div
            style={{
              padding: '10px 14px',
              borderRadius: 8,
              background: 'rgba(56, 189, 248, 0.1)',
              border: '1px solid rgba(56, 189, 248, 0.3)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <Sparkles size={16} color="#38bdf8" />
              <span style={{ fontSize: 12, fontWeight: 700, color: '#f0f9ff' }}>
                Ma trận: <b style={{ color: '#38bdf8' }}>{activeR} Hàng × {activeC} Cột</b>
              </span>
            </div>
            <span
              style={{
                fontSize: 11,
                fontWeight: 700,
                padding: '2px 8px',
                borderRadius: 4,
                background: 'rgba(56, 189, 248, 0.2)',
                color: '#38bdf8',
                border: '1px solid rgba(56, 189, 248, 0.4)',
              }}
            >
              {activeR * activeC === 1 ? '1 Ảnh đơn duy nhất' : `Tổng ${activeR * activeC} Ô Cắt`}
            </span>
          </div>

          {/* MS Word Style Interactive Matrix */}
          <div
            style={{
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              gap: 4,
              padding: 12,
              background: '#070b14',
              borderRadius: 8,
              border: '1px solid rgba(255, 255, 255, 0.08)',
            }}
            onMouseLeave={() => {
              setHoveredRows(null);
              setHoveredCols(null);
            }}
          >
            {Array.from({ length: MATRIX_MAX_ROWS }).map((_, rIdx) => (
              <div key={rIdx} style={{ display: 'flex', gap: 4 }}>
                {Array.from({ length: MATRIX_MAX_COLS }).map((_, cIdx) => {
                  const r = rIdx + 1;
                  const c = cIdx + 1;
                  const isHighlighted = r <= activeR && c <= activeC;

                  return (
                    <div
                      key={cIdx}
                      onMouseEnter={() => {
                        setHoveredRows(r);
                        setHoveredCols(c);
                      }}
                      onClick={() => handleApply(r, c)}
                      style={{
                        width: 32,
                        height: 32,
                        borderRadius: 4,
                        cursor: 'pointer',
                        transition: 'all 0.1s ease',
                        background: isHighlighted
                          ? 'linear-gradient(135deg, rgba(2, 132, 199, 0.85), rgba(56, 189, 248, 0.95))'
                          : 'rgba(255, 255, 255, 0.04)',
                        border: isHighlighted
                          ? '1.5px solid #38bdf8'
                          : '1px solid rgba(255, 255, 255, 0.1)',
                        boxShadow: isHighlighted ? '0 0 10px rgba(56, 189, 248, 0.5)' : 'none',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                      }}
                      title={`${r} Hàng × ${c} Cột (${r * c} ô)`}
                    >
                      {isHighlighted && (
                        <span style={{ fontSize: 9, fontWeight: 800, color: '#ffffff' }}>
                          {r},{c}
                        </span>
                      )}
                    </div>
                  );
                })}
              </div>
            ))}
          </div>

          {/* Quick Presets Grid */}
          <div>
            <div style={{ fontSize: 11, fontWeight: 700, color: '#94a3b8', marginBottom: 6 }}>
              ⚡ Cấu Hình Nhanh Thông Dụng:
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
              {QUICK_PRESETS.map((preset) => {
                const isSelected = currentRows === preset.rows && currentCols === preset.cols;
                return (
                  <button
                    key={`${preset.rows}x${preset.cols}_${preset.label}`}
                    onClick={() => handleApply(preset.rows, preset.cols)}
                    style={{
                      padding: '7px 10px',
                      borderRadius: 6,
                      background: isSelected ? 'rgba(2, 132, 199, 0.25)' : 'rgba(255, 255, 255, 0.03)',
                      border: isSelected ? '1.5px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.08)',
                      color: isSelected ? '#38bdf8' : '#e2e8f0',
                      cursor: 'pointer',
                      textAlign: 'left',
                      display: 'flex',
                      flexDirection: 'column',
                      gap: 2,
                      transition: 'all 0.15s ease',
                    }}
                  >
                    <div style={{ fontSize: 11, fontWeight: 700 }}>{preset.label}</div>
                    <div style={{ fontSize: 9.5, color: '#94a3b8' }}>{preset.desc}</div>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Manual Input Controls */}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              padding: '8px 12px',
              background: '#070b14',
              borderRadius: 6,
              border: '1px solid rgba(255, 255, 255, 0.06)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <Sliders size={14} color="#94a3b8" />
              <span style={{ fontSize: 11, color: '#94a3b8' }}>Nhập số thủ công:</span>
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                <span style={{ fontSize: 10.5, color: '#cbd5e1' }}>Hàng:</span>
                <input
                  type="number"
                  min={1}
                  max={12}
                  value={inputRows}
                  onChange={(e) => setInputRows(Math.max(1, Math.min(12, parseInt(e.target.value) || 1)))}
                  style={{
                    width: 44,
                    height: 26,
                    textAlign: 'center',
                    background: '#0b1329',
                    border: '1px solid #0284c7',
                    borderRadius: 4,
                    color: '#38bdf8',
                    fontSize: 11,
                    fontWeight: 700,
                  }}
                />
              </div>

              <span style={{ color: '#64748b' }}>×</span>

              <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                <span style={{ fontSize: 10.5, color: '#cbd5e1' }}>Cột:</span>
                <input
                  type="number"
                  min={1}
                  max={12}
                  value={inputCols}
                  onChange={(e) => setInputCols(Math.max(1, Math.min(12, parseInt(e.target.value) || 1)))}
                  style={{
                    width: 44,
                    height: 26,
                    textAlign: 'center',
                    background: '#0b1329',
                    border: '1px solid #0284c7',
                    borderRadius: 4,
                    color: '#38bdf8',
                    fontSize: 11,
                    fontWeight: 700,
                  }}
                />
              </div>

              <button
                onClick={() => handleApply(inputRows, inputCols)}
                style={{
                  padding: '5px 12px',
                  borderRadius: 5,
                  background: 'linear-gradient(135deg, #0284c7, #0369a1)',
                  color: '#fff',
                  border: 'none',
                  fontSize: 11,
                  fontWeight: 700,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                }}
              >
                <Check size={12} /> Áp Dụng
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
