import React from 'react';
import { Layers, Upload, Camera, Trash2 } from 'lucide-react';
import { STANDARD_ANGLE_DEFINITIONS } from '../../../../core/assets/slicer/SlicerAngleConstants';
import { Character2DAngle, Character2DPartType } from '../../../../types/scene2d';

export interface SlicerSourceImageCardProps {
  isSingleImageMode?: boolean;
  singleImageAngle?: Character2DAngle;
  onUpdateSingleImageAngle?: (angle: Character2DAngle) => void;
  singleImageSlot?: Character2DPartType;
  onUpdateSingleImageSlot?: (slot: Character2DPartType) => void;
  userUploadedImageUrl: string | null;
  totalLoadedCount?: number;
  onFileUpload: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onClearImage?: () => void;
  fileInputRef: React.RefObject<HTMLInputElement>;
}

export const SlicerSourceImageCard: React.FC<SlicerSourceImageCardProps> = ({
  isSingleImageMode = false,
  singleImageAngle = 'front',
  onUpdateSingleImageAngle,
  singleImageSlot = 'than_co_ban',
  onUpdateSingleImageSlot,
  userUploadedImageUrl,
  totalLoadedCount = 0,
  onFileUpload,
  onClearImage,
  fileInputRef,
}) => {
  return (
    <div
      style={{
        background: 'linear-gradient(180deg, rgba(15, 23, 42, 0.7) 0%, rgba(10, 15, 30, 0.8) 100%)',
        borderRadius: 8,
        border: '1px solid rgba(56, 189, 248, 0.25)',
        padding: 10,
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
      }}
    >
      {/* Card Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderBottom: '1px solid rgba(255,255,255,0.06)', paddingBottom: 6 }}>
        <div style={{ fontSize: 11.5, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6, letterSpacing: '0.2px' }}>
          <Layers size={14} color="#38bdf8" /> 1. Nguồn ảnh & Khung lưới
        </div>
        <span
          style={{
            fontSize: 9.5,
            fontWeight: 600,
            padding: '2px 7px',
            borderRadius: 4,
            background: isSingleImageMode ? 'rgba(34, 197, 94, 0.2)' : 'rgba(56, 189, 248, 0.15)',
            color: isSingleImageMode ? '#4ade80' : '#38bdf8',
            border: isSingleImageMode ? '1px solid #22c55e' : '1px solid rgba(56, 189, 248, 0.3)',
          }}
        >
          {isSingleImageMode ? '🖼️ Ảnh Đơn' : '🔲 Lưới Ma Trận'}
        </span>
      </div>

      {/* Single Image Mode Angle & Slot Configurator */}
      {isSingleImageMode && (
        <div style={{ background: 'rgba(34, 197, 94, 0.08)', padding: 8, borderRadius: 6, border: '1px solid rgba(34, 197, 94, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
          <div style={{ fontSize: 10.5, fontWeight: 700, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 4 }}>
            <Camera size={12} /> Cấu hình Góc & Bộ Phận Cho Ảnh Đơn:
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1.1fr 1fr', gap: 6 }}>
            <div>
              <label style={{ fontSize: 9.5, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Góc quay:</label>
              <select
                value={singleImageAngle}
                onChange={(e) => onUpdateSingleImageAngle && onUpdateSingleImageAngle(e.target.value as Character2DAngle)}
                style={{ width: '100%', height: 26, fontSize: 10, background: '#090d16', color: '#38bdf8', border: '1px solid rgba(56, 189, 248, 0.4)', borderRadius: 4, padding: '0 4px' }}
              >
                {STANDARD_ANGLE_DEFINITIONS.map((a) => (
                  <option key={a.id} value={a.angle}>{a.label}</option>
                ))}
              </select>
            </div>
            <div>
              <label style={{ fontSize: 9.5, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Linh kiện slot:</label>
              <select
                value={singleImageSlot}
                onChange={(e) => onUpdateSingleImageSlot && onUpdateSingleImageSlot(e.target.value as Character2DPartType)}
                style={{ width: '100%', height: 26, fontSize: 10, background: '#090d16', color: '#a78bfa', border: '1px solid rgba(167, 139, 250, 0.4)', borderRadius: 4, padding: '0 4px' }}
              >
                <option value="toc_truoc">💇 Mái Tóc Trước</option>
                <option value="toc_sau">💇 Tóc Sau</option>
                <option value="khuon_mat">👤 Khuôn Mặt</option>
                <option value="than_co_ban">🥋 Thân Cơ Bản</option>
                <option value="ao_khoac">🧥 Áo Khoác</option>
                <option value="ao_choang">🧣 Áo Choàng</option>
                <option value="vu_khi">⚔️ Vũ Khí</option>
              </select>
            </div>
          </div>
        </div>
      )}

      {/* Upload Main Action Button & Clear */}
      <input
        ref={fileInputRef}
        type="file"
        accept=".jpg,.jpeg,.png,.webp,.gif,.bmp"
        multiple
        onChange={onFileUpload}
        style={{ display: 'none' }}
      />
      <div style={{ display: 'flex', gap: 6 }}>
        <button
          onClick={() => fileInputRef.current?.click()}
          style={{
            flex: 1,
            height: 34,
            fontSize: 11.5,
            fontWeight: 700,
            borderRadius: 6,
            background: 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)',
            color: '#ffffff',
            border: '1px solid rgba(56, 189, 248, 0.5)',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 6,
            boxShadow: '0 2px 8px rgba(2, 132, 199, 0.35)',
            boxSizing: 'border-box',
          }}
        >
          <Upload size={14} /> {totalLoadedCount > 0 ? `📤 Tải thêm ảnh (${totalLoadedCount} ảnh)` : '📤 Tải ảnh lên (Hỗ trợ chọn nhiều ảnh)'}
        </button>

        {userUploadedImageUrl && onClearImage && (
          <button
            onClick={onClearImage}
            style={{
              height: 34,
              padding: '0 10px',
              fontSize: 10.5,
              fontWeight: 600,
              borderRadius: 6,
              background: 'rgba(239, 68, 68, 0.15)',
              color: '#fca5a5',
              border: '1px solid rgba(239, 68, 68, 0.35)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              boxSizing: 'border-box',
            }}
            title="Xóa ảnh hiện tại và để trống khung làm việc"
          >
            <Trash2 size={13} /> Xóa
          </button>
        )}
      </div>
    </div>
  );
};
