import React from 'react';
import { Eraser } from 'lucide-react';
import { GridCellDefinition } from '../../../core/assets/GridSliceRegistry';

interface SlicerCellAdjustmentBarProps {
  selectedCell: GridCellDefinition | null;
  slicedCellDataUrl: string | undefined;
  onOpenCellPixelEditor: (cell: GridCellDefinition) => void;
  onAdjustColWidth: (deltaPx: number) => void;
  onResetAllDividers: () => void;
}

export const SlicerCellAdjustmentBar: React.FC<SlicerCellAdjustmentBarProps> = ({
  selectedCell,
  slicedCellDataUrl,
  onOpenCellPixelEditor,
  onAdjustColWidth,
  onResetAllDividers,
}) => {
  if (!selectedCell) {
    return (
      <div style={{ background: '#0b1329', padding: '6px 10px', borderRadius: 6, border: '1px solid rgba(255,255,255,0.08)', fontSize: 9.5, color: '#94a3b8' }}>
        💡 <i>Mẹo: Rê chuột vào các đường kẻ nét đứt bên trong khung ảnh (sẽ hiện con trỏ ↔ hoặc ↕), bấm giữ và kéo để co giãn kích thước các hàng/cột tự do mà không làm dịch chuyển ảnh.</i>
      </div>
    );
  }

  return (
    <div style={{ background: '#0b1329', padding: '6px 10px', borderRadius: 6, border: '1px solid #0284c7', display: 'flex', flexDirection: 'column', gap: 6 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 6 }}>
        <div>
          <div style={{ fontSize: 10.5, fontWeight: 700, color: '#38bdf8' }}>
            Đang chọn: [{selectedCell.row + 1}, {selectedCell.col + 1}] - {selectedCell.label}
            <span style={{ fontSize: 9.5, color: '#94a3b8', marginLeft: 6, fontWeight: 400 }}>
              (Nhấp đúp vào ô trên ảnh để mở cọ tẩy xóa pixel thừa)
            </span>
          </div>
          <div style={{ fontSize: 9.5, color: '#94a3b8' }}>
            Vị trí Slot: <b>{selectedCell.partSlot}</b> • Góc Camera: <b>{selectedCell.angle || '0°'}</b>
          </div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          {slicedCellDataUrl && (
            <img
              src={slicedCellDataUrl}
              alt="Cell preview"
              style={{ width: 32, height: 32, objectFit: 'contain', background: '#000', borderRadius: 4, border: '1px solid rgba(255,255,255,0.2)' }}
            />
          )}

          {/* Open Pixel Eraser Modal Button */}
          <button
            onClick={() => onOpenCellPixelEditor(selectedCell)}
            style={{
              padding: '5px 8px',
              fontSize: 10,
              fontWeight: 700,
              borderRadius: 5,
              background: '#0284c7',
              color: '#ffffff',
              border: 'none',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            <Eraser size={12} /> Cọ Tẩy Pixel Ô Này
          </button>
        </div>
      </div>

      {/* Quick Divider Adjustment Buttons */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 4, flexWrap: 'wrap' }}>
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
            padding: '2px 6px',
            fontSize: 9.5,
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
