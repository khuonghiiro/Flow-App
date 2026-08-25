import React from 'react';
import { Eraser, Camera, Tag, X, RotateCcw, Sliders, ChevronDown } from 'lucide-react';
import { GridCellDefinition } from '../../../core/assets/GridSliceRegistry';
import { Character2DAngle, Character2DPartType } from '../../../types/scene2d';
import { STANDARD_ANGLE_DEFINITIONS } from '../../../core/assets/slicer/SlicerAngleConstants';

interface SlicerCellAdjustmentBarProps {
  selectedCell: GridCellDefinition | null;
  slicedCellDataUrl: string | undefined;
  onOpenCellPixelEditor: (cell: GridCellDefinition) => void;
  onAdjustColWidth: (deltaPx: number) => void;
  onResetAllDividers: () => void;
  onUpdateCellAngle?: (cell: GridCellDefinition, angle: Character2DAngle, mirrorAngle?: Character2DAngle) => void;
  onUpdateCellSlot?: (cell: GridCellDefinition, partSlot: Character2DPartType) => void;
  onClose?: () => void;
}

const PART_SLOT_OPTIONS: { id: Character2DPartType; label: string }[] = [
  { id: 'toc_truoc', label: '✂️ Mái Tóc Trước (Front Bangs)' },
  { id: 'toc_sau', label: '🌊 Suối Tóc Sau (Back Hair)' },
  { id: 'khuon_mat', label: '🎭 Khuôn Mặt (Face Base)' },
  { id: 'mat', label: '👁️ Đôi Mắt (Eyes)' },
  { id: 'than_co_ban', label: '🥋 Thân Áo (Torso)' },
  { id: 'canh_tay_trai', label: '💪 Cánh Tay Trái (L-Arm)' },
  { id: 'canh_tay_phai', label: '💪 Cánh Tay Phải (R-Arm)' },
  { id: 'dui_trai', label: '🦵 Chân Trái (L-Leg)' },
  { id: 'dui_phai', label: '🦵 Chân Phải (R-Leg)' },
  { id: 'ao_choang', label: '🧣 Áo Choàng (Cape/Robe)' },
  { id: 'vu_khi', label: '⚔️ Vũ Khí (Weapon)' },
];

export const SlicerCellAdjustmentBar: React.FC<SlicerCellAdjustmentBarProps> = ({
  selectedCell,
  slicedCellDataUrl,
  onOpenCellPixelEditor,
  onAdjustColWidth,
  onResetAllDividers,
  onUpdateCellAngle,
  onUpdateCellSlot,
  onClose,
}) => {
  if (!selectedCell) {
    return (
      <div
        style={{
          background: 'rgba(15, 23, 42, 0.75)',
          backdropFilter: 'blur(8px)',
          padding: '6px 12px',
          borderRadius: 8,
          border: '1px solid rgba(255, 255, 255, 0.08)',
          fontSize: 10,
          color: '#94a3b8',
          display: 'flex',
          alignItems: 'center',
          gap: 6,
        }}
      >
        <span>💡</span>
        <span>
          <i>Mẹo: Nhấp chọn một ô để gán góc quay, đổi linh kiện, chỉnh độ rộng cột hoặc mở cọ tẩy pixel thừa.</i>
        </span>
      </div>
    );
  }

  return (
    <div
      style={{
        background: 'linear-gradient(135deg, rgba(15, 23, 42, 0.94) 0%, rgba(20, 30, 50, 0.96) 100%)',
        backdropFilter: 'blur(16px)',
        padding: '7px 12px',
        borderRadius: 10,
        border: '1px solid rgba(56, 189, 248, 0.28)',
        boxShadow: '0 10px 28px -4px rgba(0, 0, 0, 0.6), 0 0 16px rgba(56, 189, 248, 0.1)',
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        animation: 'fadeIn 0.15s ease-out',
      }}
    >
      {/* Top Row: Cell badge + Angle + Slot + Eraser Action + Close */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, flexWrap: 'wrap' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          {/* Cell Coordinate Badge */}
          <div
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 5,
              padding: '3px 8px',
              borderRadius: 6,
              background: 'linear-gradient(135deg, rgba(14, 165, 233, 0.25), rgba(2, 132, 199, 0.15))',
              border: '1px solid rgba(56, 189, 248, 0.4)',
              color: '#38bdf8',
              fontSize: 10.5,
              fontWeight: 800,
              letterSpacing: '0.02em',
            }}
          >
            <span style={{ fontSize: 11 }}>🔲</span>
            <span>Ô [{selectedCell.row + 1}, {selectedCell.col + 1}]</span>
          </div>

          {/* Angle Selector */}
          <div
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 5,
              background: 'rgba(2, 132, 199, 0.12)',
              padding: '2px 6px',
              borderRadius: 6,
              border: '1px solid rgba(56, 189, 248, 0.25)',
            }}
          >
            <Camera size={11} color="#38bdf8" />
            <span style={{ fontSize: 9.5, color: '#94a3b8', fontWeight: 600 }}>Góc:</span>
            <select
              value={selectedCell.angle || 'front'}
              onChange={(e) => {
                const targetAngle = e.target.value as Character2DAngle;
                const def = STANDARD_ANGLE_DEFINITIONS.find((a) => a.angle === targetAngle);
                if (onUpdateCellAngle && def) {
                  onUpdateCellAngle(selectedCell, def.angle, def.mirrorAngle);
                }
              }}
              style={{
                background: '#070f1e',
                color: '#38bdf8',
                border: '1px solid rgba(56, 189, 248, 0.3)',
                borderRadius: 4,
                fontSize: 10,
                fontWeight: 700,
                padding: '2px 4px',
                cursor: 'pointer',
                outline: 'none',
              }}
            >
              {STANDARD_ANGLE_DEFINITIONS.map((ang) => (
                <option key={ang.id} value={ang.angle}>
                  {ang.iconText} {ang.label}
                </option>
              ))}
            </select>
          </div>

          {/* Slot Selector */}
          <div
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 5,
              background: 'rgba(168, 85, 247, 0.1)',
              padding: '2px 6px',
              borderRadius: 6,
              border: '1px solid rgba(168, 85, 247, 0.25)',
            }}
          >
            <Tag size={11} color="#c084fc" />
            <span style={{ fontSize: 9.5, color: '#c084fc', fontWeight: 600 }}>Slot:</span>
            <select
              value={selectedCell.partSlot || 'toc_truoc'}
              onChange={(e) => {
                const slot = e.target.value as Character2DPartType;
                if (onUpdateCellSlot) {
                  onUpdateCellSlot(selectedCell, slot);
                }
              }}
              style={{
                background: '#0e0b1f',
                color: '#e9d5ff',
                border: '1px solid rgba(168, 85, 247, 0.3)',
                borderRadius: 4,
                fontSize: 10,
                fontWeight: 600,
                padding: '2px 4px',
                cursor: 'pointer',
                outline: 'none',
                maxWidth: 160,
              }}
            >
              {PART_SLOT_OPTIONS.map((slot) => (
                <option key={slot.id} value={slot.id}>
                  {slot.label}
                </option>
              ))}
            </select>
          </div>
        </div>

        {/* Right Actions: Thumbnail + Eraser + Dismiss */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          {slicedCellDataUrl && (
            <img
              src={slicedCellDataUrl}
              alt="Cell preview"
              style={{
                width: 26,
                height: 26,
                objectFit: 'contain',
                background: '#040711',
                borderRadius: 4,
                border: '1px solid rgba(56, 189, 248, 0.3)',
              }}
            />
          )}

          <button
            onClick={() => onOpenCellPixelEditor(selectedCell)}
            style={{
              padding: '4px 10px',
              fontSize: 10,
              fontWeight: 700,
              borderRadius: 6,
              background: 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)',
              color: '#ffffff',
              border: '1px solid rgba(56, 189, 248, 0.4)',
              cursor: 'pointer',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 4,
              boxShadow: '0 2px 8px rgba(2, 132, 199, 0.35)',
              transition: 'all 0.15s ease',
            }}
            title="Mở cọ tẩy xóa pixel thừa cho riêng ô này (hoặc nhấp đúp trên ảnh)"
          >
            <Eraser size={11} />
            <span>Cọ Tẩy Pixel</span>
          </button>

          {onClose && (
            <button
              onClick={onClose}
              style={{
                padding: '3px 5px',
                borderRadius: 5,
                background: 'rgba(255, 255, 255, 0.06)',
                color: '#94a3b8',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                cursor: 'pointer',
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
              title="Đóng thanh điều chỉnh ô"
            >
              <X size={12} />
            </button>
          )}
        </div>
      </div>

      {/* Bottom Row: Width Adjustment Segmented Bar + Reset Grid */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 8,
          borderTop: '1px solid rgba(255, 255, 255, 0.07)',
          paddingTop: 5,
          fontSize: 9.5,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
          <span style={{ color: '#94a3b8', fontWeight: 600, display: 'inline-flex', alignItems: 'center', gap: 3 }}>
            <Sliders size={10} color="#38bdf8" /> Nới rộng cột {selectedCell.col + 1}:
          </span>

          <div
            style={{
              display: 'inline-flex',
              background: 'rgba(15, 23, 42, 0.8)',
              padding: 2,
              borderRadius: 5,
              border: '1px solid rgba(255, 255, 255, 0.1)',
              gap: 2,
            }}
          >
            <button
              onClick={() => onAdjustColWidth(-15)}
              style={{
                padding: '2px 7px',
                fontSize: 9,
                fontWeight: 700,
                borderRadius: 3,
                background: 'rgba(255, 255, 255, 0.05)',
                color: '#cbd5e1',
                border: 'none',
                cursor: 'pointer',
              }}
              title="Thu hẹp cột -15px"
            >
              -15px
            </button>
            <button
              onClick={() => onAdjustColWidth(15)}
              style={{
                padding: '2px 7px',
                fontSize: 9,
                fontWeight: 700,
                borderRadius: 3,
                background: '#0284c7',
                color: '#ffffff',
                border: 'none',
                cursor: 'pointer',
              }}
              title="Nới rộng cột +15px"
            >
              +15px
            </button>
            <button
              onClick={() => onAdjustColWidth(30)}
              style={{
                padding: '2px 7px',
                fontSize: 9,
                fontWeight: 700,
                borderRadius: 3,
                background: '#0369a1',
                color: '#ffffff',
                border: 'none',
                cursor: 'pointer',
              }}
              title="Nới rộng cột +30px"
            >
              +30px
            </button>
          </div>

          <span style={{ color: '#64748b', fontSize: 8.5, marginLeft: 4 }}>
            (Nhấp đúp chuột trên ô để tẩy nhanh)
          </span>
        </div>

        <button
          onClick={onResetAllDividers}
          style={{
            padding: '2px 8px',
            fontSize: 9,
            fontWeight: 600,
            borderRadius: 4,
            background: 'rgba(239, 68, 68, 0.1)',
            color: '#f87171',
            border: '1px solid rgba(239, 68, 68, 0.25)',
            cursor: 'pointer',
            display: 'inline-flex',
            alignItems: 'center',
            gap: 3,
          }}
          title="Chia lại tất cả các ô đều nhau"
        >
          <RotateCcw size={9} />
          <span>Reset Lưới Đều</span>
        </button>
      </div>
    </div>
  );
};
