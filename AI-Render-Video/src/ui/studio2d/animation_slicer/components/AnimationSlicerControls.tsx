// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React, { useRef } from 'react';
import {
  Scissors,
  Upload,
  Layers,
  Columns,
  Grid,
  RotateCcw,
  Sparkles,
  Plus,
  ArrowRight,
  FolderOpen,
} from 'lucide-react';

interface AnimationSlicerControlsProps {
  sliceMode: 'column' | 'grid';
  columnsCount: number;
  rowsCount: number;
  enableSmartCrop: boolean;
  isProcessing: boolean;
  hasImageLoaded: boolean;
  totalFramesCount: number;
  onSelectSliceMode: (mode: 'column' | 'grid') => void;
  onSetColumnsCount: (cols: number) => void;
  onSetGridMatrix: (rows: number, cols: number) => void;
  onToggleSmartCrop: () => void;
  onUploadSpriteSheet: (file: File) => void;
  onUploadMultipleFrames: (files: FileList) => void;
  onExecuteSlice: () => void;
  onResetDividers: () => void;
  onOpenTab1ForChroma?: () => void;
}

const COLUMN_PRESETS = [
  { cols: 4, label: '4 Cột (16:9)', desc: '4 frame hành động' },
  { cols: 6, label: '6 Cột', desc: '6 frame liên tiếp' },
  { cols: 8, label: '8 Cột', desc: '8 frame mượt' },
  { cols: 10, label: '10 Cột', desc: '10 frame chi tiết' },
  { cols: 12, label: '12 Cột', desc: '12 frame 60fps' },
];

const GRID_PRESETS = [
  { rows: 2, cols: 2, label: '2 × 2 (4 frames)' },
  { rows: 2, cols: 3, label: '2 × 3 (6 frames)' },
  { rows: 3, cols: 3, label: '3 × 3 (9 frames)' },
  { rows: 2, cols: 4, label: '2 × 4 (8 frames)' },
  { rows: 4, cols: 4, label: '4 × 4 (16 frames)' },
];

export const AnimationSlicerControls: React.FC<AnimationSlicerControlsProps> = ({
  sliceMode,
  columnsCount,
  rowsCount,
  enableSmartCrop,
  isProcessing,
  hasImageLoaded,
  totalFramesCount,
  onSelectSliceMode,
  onSetColumnsCount,
  onSetGridMatrix,
  onToggleSmartCrop,
  onUploadSpriteSheet,
  onUploadMultipleFrames,
  onExecuteSlice,
  onResetDividers,
  onOpenTab1ForChroma,
}) => {
  const spriteFileInputRef = useRef<HTMLInputElement>(null);
  const multiFileInputRef = useRef<HTMLInputElement>(null);

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        background: 'rgba(15, 23, 42, 0.85)',
        border: '1px solid rgba(56, 189, 248, 0.2)',
        borderRadius: 8,
        padding: 10,
        height: '100%',
        overflowY: 'auto',
      }}
    >
      {/* ─── SECTION 1: IMPORT & UPLOAD OPTIONS ───────────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <span style={{ fontSize: 10, fontWeight: 700, color: '#38bdf8' }}>
          1. TẢI ẢNH HOẶC KHUNG HÌNH:
        </span>

        {/* Upload Sprite Sheet Button */}
        <input
          ref={spriteFileInputRef}
          type="file"
          accept="image/*"
          style={{ display: 'none' }}
          onChange={(e) => {
            if (e.target.files && e.target.files[0]) {
              onUploadSpriteSheet(e.target.files[0]);
            }
          }}
        />

        {/* Upload Multiple Single PNG Frames */}
        <input
          ref={multiFileInputRef}
          type="file"
          accept="image/*"
          multiple
          style={{ display: 'none' }}
          onChange={(e) => {
            if (e.target.files && e.target.files.length > 0) {
              onUploadMultipleFrames(e.target.files);
            }
          }}
        />

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 5 }}>
          <button
            onClick={() => spriteFileInputRef.current?.click()}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              padding: '7px 4px',
              borderRadius: 6,
              background: 'linear-gradient(135deg, #0284c7, #38bdf8)',
              border: 'none',
              color: '#fff',
              fontSize: 10,
              fontWeight: 700,
              cursor: 'pointer',
              boxShadow: '0 2px 8px rgba(56, 189, 248, 0.3)',
            }}
          >
            <Upload size={12} /> Tải Sprite Sheet
          </button>

          <button
            onClick={() => multiFileInputRef.current?.click()}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              padding: '7px 4px',
              borderRadius: 6,
              background: 'linear-gradient(135deg, #7c3aed, #a855f7)',
              border: 'none',
              color: '#fff',
              fontSize: 10,
              fontWeight: 700,
              cursor: 'pointer',
              boxShadow: '0 2px 8px rgba(168, 85, 247, 0.3)',
            }}
          >
            <FolderOpen size={12} /> Tải Nhiều Ảnh Lẻ
          </button>
        </div>
      </div>

      {/* ─── SECTION 2: SLICING MODE & PRESETS ────────────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6, background: 'rgba(2, 6, 23, 0.5)', padding: 8, borderRadius: 6, border: '1px solid rgba(255,255,255,0.05)' }}>
        <span style={{ fontSize: 10, fontWeight: 700, color: '#facc15' }}>
          2. CƠ CHẾ CHIA CỘT / LƯỚI FRAME:
        </span>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 4 }}>
          <button
            onClick={() => onSelectSliceMode('column')}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              padding: '5px',
              borderRadius: 4,
              fontSize: 10,
              fontWeight: sliceMode === 'column' ? 700 : 500,
              background: sliceMode === 'column' ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.03)',
              border: sliceMode === 'column' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
              color: sliceMode === 'column' ? '#38bdf8' : '#cbd5e1',
              cursor: 'pointer',
            }}
          >
            <Columns size={11} /> Cắt Theo Cột
          </button>

          <button
            onClick={() => onSelectSliceMode('grid')}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              padding: '5px',
              borderRadius: 4,
              fontSize: 10,
              fontWeight: sliceMode === 'grid' ? 700 : 500,
              background: sliceMode === 'grid' ? 'rgba(168, 85, 247, 0.25)' : 'rgba(255,255,255,0.03)',
              border: sliceMode === 'grid' ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.06)',
              color: sliceMode === 'grid' ? '#c084fc' : '#cbd5e1',
              cursor: 'pointer',
            }}
          >
            <Grid size={11} /> Ma Trận Lưới
          </button>
        </div>

        {/* Column Presets */}
        {sliceMode === 'column' ? (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 3 }}>
            {COLUMN_PRESETS.map((p) => {
              const isActive = columnsCount === p.cols;
              return (
                <button
                  key={p.cols}
                  onClick={() => onSetColumnsCount(p.cols)}
                  style={{
                    padding: '4px 2px',
                    borderRadius: 4,
                    fontSize: 9,
                    fontWeight: isActive ? 700 : 500,
                    background: isActive ? 'rgba(56, 189, 248, 0.3)' : 'rgba(255,255,255,0.02)',
                    border: isActive ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.04)',
                    color: isActive ? '#38bdf8' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  {p.label}
                </button>
              );
            })}
          </div>
        ) : (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 3 }}>
            {GRID_PRESETS.map((p) => {
              const isActive = rowsCount === p.rows && columnsCount === p.cols;
              return (
                <button
                  key={`${p.rows}x${p.cols}`}
                  onClick={() => onSetGridMatrix(p.rows, p.cols)}
                  style={{
                    padding: '4px',
                    borderRadius: 4,
                    fontSize: 9,
                    fontWeight: isActive ? 700 : 500,
                    background: isActive ? 'rgba(168, 85, 247, 0.3)' : 'rgba(255,255,255,0.02)',
                    border: isActive ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.04)',
                    color: isActive ? '#c084fc' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  {p.label}
                </button>
              );
            })}
          </div>
        )}

        {/* Smart Auto-Trim Bounding Box */}
        <label style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: 9.5, color: '#cbd5e1', cursor: 'pointer', marginTop: 2 }}>
          <input
            type="checkbox"
            checked={enableSmartCrop}
            onChange={onToggleSmartCrop}
            style={{ accentColor: '#4ade80' }}
          />
          <span>Tự động gọt sát viền trong suốt (Auto-Trim)</span>
        </label>
      </div>

      {/* ─── SECTION 3: SLICING ACTION BUTTONS ───────────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 5, marginTop: 'auto' }}>
        <button
          onClick={onExecuteSlice}
          disabled={!hasImageLoaded || isProcessing}
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 6,
            padding: '9px',
            borderRadius: 6,
            background: hasImageLoaded ? 'linear-gradient(135deg, #10b981, #059669)' : 'rgba(255,255,255,0.05)',
            border: 'none',
            color: hasImageLoaded ? '#ffffff' : '#64748b',
            fontSize: 11,
            fontWeight: 700,
            cursor: hasImageLoaded ? 'pointer' : 'not-allowed',
            boxShadow: hasImageLoaded ? '0 2px 10px rgba(16, 185, 129, 0.4)' : 'none',
          }}
        >
          <Scissors size={13} />
          {isProcessing ? 'Đang cắt...' : '⚡ Cắt & Tạo Chuỗi Frame'}
        </button>

        <button
          onClick={onResetDividers}
          disabled={!hasImageLoaded}
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 4,
            padding: '4px',
            borderRadius: 4,
            background: 'none',
            border: '1px solid rgba(255,255,255,0.08)',
            color: '#94a3b8',
            fontSize: 9,
            cursor: hasImageLoaded ? 'pointer' : 'not-allowed',
          }}
        >
          <RotateCcw size={10} /> Đặt lại vạch chia đều nhau
        </button>

        {/* Tip / Shortcut to Tab 1 if user needs AI chroma keying */}
        {onOpenTab1ForChroma && (
          <button
            onClick={onOpenTab1ForChroma}
            style={{
              marginTop: 4,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              padding: '4px',
              borderRadius: 4,
              background: 'rgba(56, 189, 248, 0.08)',
              border: '1px dashed rgba(56, 189, 248, 0.3)',
              color: '#38bdf8',
              fontSize: 8.5,
              cursor: 'pointer',
            }}
          >
            <Sparkles size={10} /> Cần tách nền xanh/trắng? Sang Tab 1 <ArrowRight size={9} />
          </button>
        )}
      </div>
    </div>
  );
};
