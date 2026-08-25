import React from 'react';
import { Layers, Upload, Camera, FileCode, Trash2 } from 'lucide-react';
import { GRID_CATEGORY_DEFINITIONS, GridCategoryDefinition } from '../../../../core/assets/GridSliceRegistry';
import { STANDARD_ANGLE_DEFINITIONS } from '../../../../core/assets/slicer/SlicerAngleConstants';
import { Character2DAngle, Character2DPartType } from '../../../../types/scene2d';

export interface SlicerSourceImageCardProps {
  selectedCatId: string;
  onSelectCatId: (id: string) => void;
  customCategory?: GridCategoryDefinition | null;
  singleImageAngle?: Character2DAngle;
  onUpdateSingleImageAngle?: (angle: Character2DAngle) => void;
  singleImageSlot?: Character2DPartType;
  onUpdateSingleImageSlot?: (slot: Character2DPartType) => void;
  onAutoDetectAngleFromFilename?: () => void;
  onOpenJsonImportModal?: () => void;
  userUploadedImageUrl: string | null;
  onFileUpload: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onClearImage?: () => void;
  fileInputRef: React.RefObject<HTMLInputElement>;
}

export const SlicerSourceImageCard: React.FC<SlicerSourceImageCardProps> = ({
  selectedCatId,
  onSelectCatId,
  customCategory,
  singleImageAngle = 'front',
  onUpdateSingleImageAngle,
  singleImageSlot = 'than_co_ban',
  onUpdateSingleImageSlot,
  onAutoDetectAngleFromFilename,
  onOpenJsonImportModal,
  userUploadedImageUrl,
  onFileUpload,
  onClearImage,
  fileInputRef,
}) => {
  const currentCat =
    customCategory && customCategory.id === selectedCatId
      ? customCategory
      : GRID_CATEGORY_DEFINITIONS.find((c) => c.id === selectedCatId) || GRID_CATEGORY_DEFINITIONS[0];

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
            background: selectedCatId === 'single_full_image' ? 'rgba(34, 197, 94, 0.2)' : 'rgba(56, 189, 248, 0.15)',
            color: selectedCatId === 'single_full_image' ? '#4ade80' : '#38bdf8',
            border: selectedCatId === 'single_full_image' ? '1px solid #22c55e' : '1px solid rgba(56, 189, 248, 0.3)',
          }}
        >
          {selectedCatId === 'single_full_image' ? '🖼️ 1 Ảnh đơn' : `${currentCat.rows} Hàng × ${currentCat.cols} Cột`}
        </span>
      </div>

      {/* Category Preset Dropdown */}
      <div style={{ display: 'flex', gap: 6 }}>
        <select
          value={selectedCatId}
          onChange={(e) => onSelectCatId(e.target.value)}
          style={{
            flex: 1,
            height: 34,
            padding: '0 10px',
            fontSize: 11,
            background: '#0b1329',
            color: '#38bdf8',
            border: '1.5px solid #0284c7',
            borderRadius: 6,
            fontWeight: 600,
            cursor: 'pointer',
            outline: 'none',
            boxSizing: 'border-box',
            minWidth: 0,
          }}
        >
          {customCategory && !GRID_CATEGORY_DEFINITIONS.some((c) => c.id === customCategory.id) && (
            <option value={customCategory.id}>
              ⭐ {customCategory.label} ({customCategory.rows}x{customCategory.cols})
            </option>
          )}
          {GRID_CATEGORY_DEFINITIONS.map((cat) => (
            <option key={cat.id} value={cat.id}>
              {cat.label} ({cat.rows}x{cat.cols})
            </option>
          ))}
        </select>
      </div>

      {/* Single Image Mode Angle & Slot Configurator */}
      {selectedCatId === 'single_full_image' && (
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

      {/* Auto Angle Detection & Tab 4 Metadata Sync Buttons */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
        <button
          onClick={onAutoDetectAngleFromFilename}
          disabled={!userUploadedImageUrl}
          style={{
            height: 30,
            fontSize: 10,
            fontWeight: 700,
            borderRadius: 5,
            background: userUploadedImageUrl
              ? 'linear-gradient(135deg, rgba(16, 185, 129, 0.25), rgba(6, 182, 212, 0.25))'
              : 'rgba(255,255,255,0.03)',
            color: userUploadedImageUrl ? '#4ade80' : '#475569',
            border: userUploadedImageUrl ? '1px solid rgba(16, 185, 129, 0.4)' : '1px solid rgba(255,255,255,0.08)',
            cursor: userUploadedImageUrl ? 'pointer' : 'not-allowed',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 4,
            boxShadow: userUploadedImageUrl ? '0 0 8px rgba(74, 222, 128, 0.2)' : 'none',
            transition: 'all 0.15s ease',
          }}
          title="Tự động nhận diện góc quay và slot từ tên file ảnh tải lên (chuẩn Tab 4)"
        >
          ⚡ Đặt Góc Theo Tên Ảnh
        </button>

        {onOpenJsonImportModal && (
          <button
            onClick={onOpenJsonImportModal}
            style={{
              height: 30,
              fontSize: 10,
              fontWeight: 700,
              borderRadius: 5,
              background: 'rgba(168, 85, 247, 0.2)',
              color: '#c084fc',
              border: '1px solid rgba(168, 85, 247, 0.4)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              boxShadow: '0 0 8px rgba(168, 85, 247, 0.2)',
              transition: 'all 0.15s ease',
            }}
            title="Dán cấu trúc JSON prompt từ Tab 4 để tự động gán metadata cho các góc quay"
          >
            <FileCode size={12} /> Nạp JSON Tab 4
          </button>
        )}
      </div>

      {/* Upload Main Action Button & Clear */}
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
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
          <Upload size={14} /> {userUploadedImageUrl ? '📤 Tải ảnh khác lên' : '📤 Tải ảnh Sprite Sheet lên'}
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
