import React from 'react';
import { Eraser, Camera, Tag } from 'lucide-react';
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
}) => {
  if (!selectedCell) {
    return (
      <div style={{ background: '#0b1329', padding: '6px 10px', borderRadius: 6, border: '1px solid rgba(255,255,255,0.08)', fontSize: 9.5, color: '#94a3b8' }}>
        💡 <i>Mẹo: Nhấp chọn một ô bất kỳ để gắn góc quay (0°, 45°, 90°...), đổi linh kiện, hoặc kéo các đường kẻ nét đứt để co giãn kích thước các ô.</i>
      </div>
    );
  }

  const currentAngleDef = STANDARD_ANGLE_DEFINITIONS.find((a) => a.angle === selectedCell.angle) || STANDARD_ANGLE_DEFINITIONS[0];

  return (
    <div style={{ background: '#0b1329', padding: '8px 12px', borderRadius: 8, border: '1.5px solid #0284c7', display: 'flex', flexDirection: 'column', gap: 8 }}>
      {/* Top row: Title + Slot Selector + Angle Selector + Eraser */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
          <div>
            <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8' }}>
              Đang chọn: [{selectedCell.row + 1}, {selectedCell.col + 1}]
            </div>
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>
              (Nhấp đúp trên ảnh để mở cọ tẩy xóa pixel thừa)
            </div>
          </div>

          {/* Quick Angle Selector Dropdown */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, background: 'rgba(2, 132, 199, 0.15)', padding: '2px 6px', borderRadius: 5, border: '1px solid rgba(56, 189, 248, 0.3)' }}>
            <Camera size={12} color="#38bdf8" />
            <span style={{ fontSize: 10, color: '#94a3b8', fontWeight: 600 }}>Góc Quay:</span>
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
                background: '#070b14',
                color: '#38bdf8',
                border: '1px solid #0284c7',
                borderRadius: 4,
                fontSize: 10.5,
                fontWeight: 700,
                padding: '2px 6px',
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

          {/* Quick Part Slot Selector */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, background: 'rgba(255, 255, 255, 0.04)', padding: '2px 6px', borderRadius: 5, border: '1px solid rgba(255, 255, 255, 0.08)' }}>
            <Tag size={12} color="#a855f7" />
            <span style={{ fontSize: 10, color: '#94a3b8', fontWeight: 600 }}>Slot:</span>
            <select
              value={selectedCell.partSlot || 'toc_truoc'}
              onChange={(e) => {
                const slot = e.target.value as Character2DPartType;
                if (onUpdateCellSlot) {
                  onUpdateCellSlot(selectedCell, slot);
                }
              }}
              style={{
                background: '#070b14',
                color: '#e2e8f0',
                border: '1px solid rgba(255,255,255,0.15)',
                borderRadius: 4,
                fontSize: 10.5,
                fontWeight: 600,
                padding: '2px 6px',
                cursor: 'pointer',
                outline: 'none',
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

        {/* Right side: Preview Image & Eraser */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          {slicedCellDataUrl && (
            <img
              src={slicedCellDataUrl}
              alt="Cell preview"
              style={{ width: 32, height: 32, objectFit: 'contain', background: '#000', borderRadius: 4, border: '1px solid rgba(255,255,255,0.2)' }}
            />
          )}

          <button
            onClick={() => onOpenCellPixelEditor(selectedCell)}
            style={{
              padding: '5px 10px',
              fontSize: 10,
              fontWeight: 700,
              borderRadius: 5,
              background: 'linear-gradient(135deg, #0284c7, #0369a1)',
              color: '#ffffff',
              border: 'none',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              boxShadow: '0 2px 8px rgba(2, 132, 199, 0.4)',
            }}
          >
            <Eraser size={12} /> Cọ Tẩy Pixel Ô Này
          </button>
        </div>
      </div>

      {/* Bottom row: Quick Divider Adjustment Buttons */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 4, flexWrap: 'wrap', borderTop: '1px solid rgba(255,255,255,0.06)', paddingTop: 6 }}>
        <span style={{ fontSize: 9.5, color: '#94a3b8' }}>Nới rộng cột {selectedCell.col + 1}:</span>
        <button
          onClick={() => onAdjustColWidth(15)}
          style={{ padding: '2px 6px', fontSize: 9.5, borderRadius: 4, background: '#0284c7', color: '#fff', border: 'none', cursor: 'pointer', fontWeight: 700 }}
        >
          +15px
        </button>
        <button
          onClick={() => onAdjustColWidth(30)}
          style={{ padding: '2px 6px', fontSize: 9.5, borderRadius: 4, background: '#0369a1', color: '#fff', border: 'none', cursor: 'pointer', fontWeight: 700 }}
        >
          +30px
        </button>
        <button
          onClick={() => onAdjustColWidth(-15)}
          style={{ padding: '2px 6px', fontSize: 9.5, borderRadius: 4, background: 'rgba(255,255,255,0.08)', color: '#cbd5e1', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
        >
          -15px
        </button>

        <button
          onClick={onResetAllDividers}
          style={{
            marginLeft: 'auto',
            padding: '2px 8px',
            fontSize: 9.5,
            fontWeight: 600,
            borderRadius: 4,
            background: 'rgba(239, 68, 68, 0.15)',
            color: '#f87171',
            border: '1px solid rgba(239, 68, 68, 0.3)',
            cursor: 'pointer',
          }}
        >
          Reset Lưới
        </button>
      </div>
    </div>
  );
};
