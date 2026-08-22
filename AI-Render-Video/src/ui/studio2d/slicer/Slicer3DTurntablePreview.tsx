import React, { useState } from 'react';
import { Compass, Sparkles, Save, FolderOpen, Sliders } from 'lucide-react';
import { AngleDetectionResult, ThreeMultiAngleBillboardEngine, Background2DMode } from '../../../core/engine2d/ThreeMultiAngleBillboardEngine';
import { GridCategoryDefinition, GridCellDefinition } from '../../../core/assets/GridSliceRegistry';

interface Slicer3DTurntablePreviewProps {
  threeContainerRef: React.RefObject<HTMLDivElement>;
  threeEngineRef: React.RefObject<ThreeMultiAngleBillboardEngine | null>;
  activeAngleInfo: AngleDetectionResult;
  turntableAngle: number;
  setTurntableAngle: (val: number) => void;
  timeOfDay: number;
  setTimeOfDay: (hour: number) => void;
  slicedResults: Map<string, string>;
  currentCategory: GridCategoryDefinition;
  selectedCell: GridCellDefinition | null;
  onSelectCell: (cell: GridCellDefinition) => void;
  onOpenSaveKitModal?: () => void;
  onOpenCatalogModal?: () => void;
  onOpenTunerModal?: () => void;
}

export const Slicer3DTurntablePreview: React.FC<Slicer3DTurntablePreviewProps> = ({
  threeContainerRef,
  threeEngineRef,
  activeAngleInfo,
  turntableAngle,
  setTurntableAngle,
  timeOfDay,
  setTimeOfDay,
  slicedResults,
  currentCategory,
  selectedCell,
  onSelectCell,
  onOpenSaveKitModal,
  onOpenCatalogModal,
  onOpenTunerModal,
}) => {
  const [activeBgMode, setActiveBgMode] = useState<Background2DMode>('checkerboard');

  const handleSelectBgMode = (mode: Background2DMode) => {
    setActiveBgMode(mode);
    if (threeEngineRef.current) {
      threeEngineRef.current.setBackgroundMode(mode);
    }
  };

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        background: 'rgba(15, 23, 42, 0.85)',
        padding: 12,
        borderRadius: 10,
        border: '1px solid rgba(56, 189, 248, 0.2)',
        overflow: 'hidden',
        height: '100%',
        boxShadow: '0 8px 32px rgba(0, 0, 0, 0.4)',
      }}
    >
      {/* 1. Header with Catalog & Save Kit Actions */}
      <div style={{ display: 'flex', fontSize: 11.5, fontWeight: 700, color: '#38bdf8', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <Compass size={15} /> XEM TRƯỚC 3D ĐA GÓC
          <span style={{ fontSize: 9.5, padding: '2px 7px', borderRadius: 4, background: 'rgba(56, 189, 248, 0.18)', color: '#38bdf8', fontWeight: 600 }}>
            {activeAngleInfo.compassDirection} • {activeAngleInfo.angleDeg}°
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          {onOpenCatalogModal && (
            <button
              onClick={onOpenCatalogModal}
              style={{
                padding: '3px 7px',
                fontSize: 9.5,
                fontWeight: 600,
                borderRadius: 4,
                background: 'rgba(255,255,255,0.08)',
                color: '#e2e8f0',
                border: '1px solid rgba(255,255,255,0.15)',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 3,
              }}
              title="Mở kho tài nguyên linh kiện đã lưu"
            >
              <FolderOpen size={11} /> Kho
            </button>
          )}

          {onOpenSaveKitModal && slicedResults.size > 0 && (
            <button
              onClick={onOpenSaveKitModal}
              style={{
                padding: '3px 8px',
                fontSize: 9.5,
                fontWeight: 700,
                borderRadius: 4,
                background: 'linear-gradient(135deg, #8b5cf6, #6d28d9)',
                color: '#ffffff',
                border: 'none',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 3,
                boxShadow: '0 2px 8px rgba(139, 92, 246, 0.4)',
              }}
              title="Lưu toàn bộ linh kiện đã cắt vào kho"
            >
              <Save size={11} /> Lưu Kit
            </button>
          )}
        </div>
      </div>

      {/* 2. 3D WebGL Container */}
      <div
        ref={threeContainerRef}
        style={{
          width: '100%',
          height: 245,
          flexShrink: 0,
          borderRadius: 8,
          overflow: 'hidden',
          border: '1px solid rgba(56, 189, 248, 0.35)',
          background: '#060913',
          boxShadow: 'inset 0 0 20px rgba(0,0,0,0.8)',
        }}
      />

      {/* 3. Angle Turntable & 2D Background Controls */}
      <div style={{ flexShrink: 0, display: 'flex', flexDirection: 'column', gap: 6 }}>
        {/* Turntable Slider */}
        <div style={{ background: 'rgba(0,0,0,0.3)', padding: '6px 8px', borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 3 }}>
            <span>{activeAngleInfo.discreteAngle.startsWith('top_down') ? '👑 Xoay 360° Quanh Đỉnh Đầu:' : '🌐 Xoay Camera 360°:'}</span>
            <span style={{ color: activeAngleInfo.discreteAngle.startsWith('top_down') ? '#f59e0b' : '#38bdf8', fontWeight: 700 }}>
              {activeAngleInfo.angleLabel}
            </span>
          </div>
          <input
            type="range"
            min="0"
            max="360"
            value={turntableAngle}
            onChange={(e) => {
              const val = parseInt(e.target.value);
              setTurntableAngle(val);
              if (threeEngineRef.current) {
                const isTop = activeAngleInfo.discreteAngle.startsWith('top_down');
                threeEngineRef.current.jumpToAngle(val, isTop);
              }
            }}
            style={{ width: '100%' }}
          />

          {/* Quick Angle Buttons */}
          <div style={{ display: 'flex', gap: 3, marginTop: 4, flexWrap: 'wrap' }}>
            {[
              { label: '0° Thẳng', deg: 0, isTop: false },
              { label: '45° 3/4', deg: 45, isTop: false },
              { label: '👂 90° Tai', deg: 90, isTop: false },
              { label: '180° Sau', deg: 180, isTop: false },
              { label: '👑 Đỉnh 0°', deg: 0, isTop: true },
              { label: '👑 Đỉnh 45°', deg: 45, isTop: true },
              { label: '👑 Đỉnh 90°', deg: 90, isTop: true },
            ].map((btn) => (
              <button
                key={btn.label}
                onClick={() => {
                  setTurntableAngle(btn.deg);
                  if (threeEngineRef.current) {
                    threeEngineRef.current.jumpToAngle(btn.deg, btn.isTop);
                  }
                }}
                style={{
                  flex: 1,
                  padding: '3px 4px',
                  fontSize: 9,
                  fontWeight: 600,
                  borderRadius: 4,
                  background: btn.isTop ? 'rgba(245, 158, 11, 0.15)' : 'rgba(255,255,255,0.06)',
                  color: btn.isTop ? '#fbbf24' : '#e2e8f0',
                  border: btn.isTop ? '1px solid rgba(245, 158, 11, 0.4)' : '1px solid rgba(255,255,255,0.1)',
                  cursor: 'pointer',
                  whiteSpace: 'nowrap',
                }}
              >
                {btn.label}
              </button>
            ))}
          </div>
        </div>

        {/* 2D Background Switcher (Clean 2D Art Inspection) & Multi-Angle Layer Tuner Trigger */}
        <div style={{ padding: '6px 8px', background: 'rgba(0,0,0,0.3)', borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)', display: 'flex', flexDirection: 'column', gap: 5 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 10, color: '#94a3b8', fontWeight: 600 }}>🖼️ Nền 2D Soi Chi Tiết:</span>
            {onOpenTunerModal && (
              <button
                onClick={onOpenTunerModal}
                style={{
                  padding: '2px 7px',
                  fontSize: 9.5,
                  fontWeight: 700,
                  borderRadius: 4,
                  background: 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)',
                  color: '#ffffff',
                  border: '1px solid rgba(56, 189, 248, 0.4)',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                  boxShadow: '0 2px 6px rgba(2, 132, 199, 0.3)',
                }}
                title="Mở bảng tinh chỉnh lệch tóc, phóng to/thu nhỏ hoặc ẩn tóc thừa cho từng góc"
              >
                <Sliders size={11} /> 🎛️ Cân Chỉnh Tóc Theo Góc
              </button>
            )}
          </div>

          {/* 5 Solid 2D Background Modes */}
          <div style={{ display: 'flex', gap: 3 }}>
            {[
              { id: 'checkerboard' as Background2DMode, label: '🏁 Caro Alpha' },
              { id: 'dark' as Background2DMode, label: '⚫ Tối' },
              { id: 'slate' as Background2DMode, label: '🔵 Xám Xanh' },
              { id: 'white' as Background2DMode, label: '⚪ Trắng' },
              { id: 'chroma' as Background2DMode, label: '🟢 Chroma' },
            ].map((bg) => (
              <button
                key={bg.id}
                onClick={() => handleSelectBgMode(bg.id)}
                style={{
                  flex: 1,
                  padding: '3px 2px',
                  fontSize: 8.5,
                  fontWeight: 600,
                  borderRadius: 4,
                  background: activeBgMode === bg.id ? '#0284c7' : 'rgba(255,255,255,0.06)',
                  color: activeBgMode === bg.id ? '#ffffff' : '#cbd5e1',
                  border: activeBgMode === bg.id ? '1px solid #38bdf8' : 'none',
                  cursor: 'pointer',
                  whiteSpace: 'nowrap',
                }}
              >
                {bg.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* 4. Sliced Thumbnails Strip - Stretched Evenly with Balanced Height */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0, overflow: 'hidden', background: 'rgba(0,0,0,0.3)', padding: 8, borderRadius: 8, border: '1px solid rgba(255,255,255,0.06)' }}>
        <div style={{ fontSize: 10.5, fontWeight: 700, color: '#94a3b8', marginBottom: 6, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span>Linh kiện đã bóc tách ({slicedResults.size}/{currentCategory.cells.length} ô):</span>
          {slicedResults.size > 0 && (
            <span style={{ fontSize: 9, color: '#4ade80', fontWeight: 600 }}>✓ Tách nền 100%</span>
          )}
        </div>

        <div
          style={{
            flex: 1,
            display: 'grid',
            gridTemplateColumns: 'repeat(5, 1fr)',
            gap: 6,
            overflowY: 'auto',
            alignContent: 'start',
            paddingRight: 2,
          }}
        >
          {currentCategory.cells.map((cell) => {
            const key = `${cell.row}_${cell.col}`;
            const dataUrl = slicedResults.get(key);
            const isSelected = selectedCell?.row === cell.row && selectedCell?.col === cell.col;

            return (
              <div
                key={key}
                onClick={() => onSelectCell(cell)}
                style={{
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  background: isSelected ? 'rgba(56, 189, 248, 0.22)' : 'rgba(15, 23, 42, 0.7)',
                  borderRadius: 6,
                  padding: 3,
                  border: isSelected ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                  cursor: 'pointer',
                  boxShadow: isSelected ? '0 0 10px rgba(56, 189, 248, 0.35)' : 'none',
                  transition: 'all 0.15s ease',
                }}
                title={cell.label}
              >
                {/* Thumbnail Box with Checkerboard pattern */}
                <div
                  style={{
                    width: '100%',
                    height: 44,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    background: '#040711',
                    backgroundImage: 'linear-gradient(45deg, #111827 25%, transparent 25%), linear-gradient(-45deg, #111827 25%, transparent 25%), linear-gradient(45deg, transparent 75%, #111827 75%), linear-gradient(-45deg, transparent 75%, #111827 75%)',
                    backgroundSize: '10px 10px',
                    backgroundPosition: '0 0, 0 5px, 5px -5px, -5px 0px',
                    borderRadius: 4,
                    overflow: 'hidden',
                  }}
                >
                  {dataUrl ? (
                    <img
                      src={dataUrl}
                      alt={cell.label}
                      style={{ maxWidth: '92%', maxHeight: '92%', objectFit: 'contain' }}
                    />
                  ) : (
                    <span style={{ fontSize: 8.5, color: '#475569', fontWeight: 600 }}>R{cell.row + 1}C{cell.col + 1}</span>
                  )}
                </div>

                <span
                  style={{
                    fontSize: 8,
                    fontWeight: 600,
                    color: isSelected ? '#38bdf8' : '#94a3b8',
                    marginTop: 3,
                    textAlign: 'center',
                    whiteSpace: 'nowrap',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    width: '100%',
                  }}
                >
                  {cell.angle ? cell.angle.replace('top_down_', '👑 ').replace('three_quarter_', '¾ ').replace('profile_', '90° ') : `C${cell.col + 1}`}
                </span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
