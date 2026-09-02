import React, { useState, useEffect } from 'react';
import {
  Save,
  Plus,
  X,
  User,
  Package,
  Compass,
  Folder,
  Check,
  Sparkles,
} from 'lucide-react';
import { CharacterResourceCategory } from '../../../../types/scene2d';
import { saveCustomResourceKit } from '../../../../core/assets/CharacterKitStorage';
import { SlicerUploadedImageItem } from '../hooks/useSlicerMultiImageGallery';
import { slugifyVietnamese } from '../../animation_slicer/utils/slugifyHelper';
import { formatVietnameseDisplayName } from '../../animation_slicer/components/AnimationPoseSaveModal';

export interface SlicerSaveKitModalProps {
  isOpen: boolean;
  onClose: () => void;
  slicedResults?: Map<string, string>;
  categoryLabel?: string;
  initialTargetCategory?: string;
  checkedImageItems?: SlicerUploadedImageItem[];
  allImages?: SlicerUploadedImageItem[];
  activeAngleDeg?: number;
  onSaveSuccess?: (msg: string) => void;
}

const DEFAULT_PART_CATEGORIES = [
  { id: 'toc_truoc', label: '💇 Mái Tóc Trước', category: 'toc' },
  { id: 'toc_sau', label: '💇 Tóc Sau & Đuôi Tóc', category: 'toc' },
  { id: 'khuon_mat', label: '👤 Khuôn Mặt & Biểu Cảm', category: 'khuon_mat' },
  { id: 'mat', label: '👁️ Mắt & Con Ngươi', category: 'khuon_mat' },
  { id: 'mieng', label: '👄 Khuôn Miệng', category: 'khuon_mat' },
  { id: 'mui', label: '👃 Dáng Mũi', category: 'khuon_mat' },
  { id: 'than_co_ban', label: '🥋 Thân Thể & Dáng Đứng', category: 'than_co_ban' },
  { id: 'trang_phuc', label: '🧥 Trang Phục & Áo Choàng', category: 'trang_phuc' },
  { id: 'canh_tay', label: '💪 Cánh Tay', category: 'than_co_ban' },
  { id: 'ban_tay', label: '🖐️ Bàn Tay', category: 'than_co_ban' },
  { id: 'dui', label: '🦵 Bắp Đùi', category: 'than_co_ban' },
  { id: 'cang_chan', label: '🦶 Cẳng Chân & Bàn Chân', category: 'than_co_ban' },
  { id: 'vu_khi', label: '⚔️ Vũ Khí & Đạo Cụ', category: 'phu_kien' },
  { id: 'hieu_ung', label: '✨ Hiệu Ứng Phép Thuật', category: 'phu_kien' },
];

const DEFAULT_ANGLES = [
  { id: 'khong_goc', deg: -1, label: 'Không phân góc' },
  { id: 'chinh_dien', deg: 0, label: 'Chính Diện (0°)' },
  { id: 'cheo_truoc_trai', deg: 45, label: 'Chéo Trước Trái (45°)' },
  { id: 'ngang_trai', deg: 90, label: 'Ngang Trái (90°)' },
  { id: 'cheo_sau_trai', deg: 135, label: 'Chéo Sau Trái (135°)' },
  { id: 'sau_lung', deg: 180, label: 'Sau Lưng (180°)' },
  { id: 'cheo_sau_phai', deg: 225, label: 'Chéo Sau Phải (225°)' },
  { id: 'ngang_phai', deg: 270, label: 'Ngang Phải (270°)' },
  { id: 'cheo_truoc_phai', deg: 315, label: 'Chéo Trước Phải (315°)' },
];

export const SlicerSaveKitModal: React.FC<SlicerSaveKitModalProps> = ({
  isOpen,
  onClose,
  slicedResults = new Map(),
  categoryLabel = 'Linh Kiện',
  initialTargetCategory = 'character',
  checkedImageItems = [],
  allImages = [],
  activeAngleDeg = 0,
  onSaveSuccess,
}) => {
  // 1. Characters list
  const [characterList, setCharacterList] = useState<string[]>(['nhan_vat_chinh', 'ton_ngo_khong', 'tieu_viem']);
  const [selectedCharacter, setSelectedCharacter] = useState<string>('nhan_vat_chinh');
  const [isCreatingNewChar, setIsCreatingNewChar] = useState<boolean>(false);
  const [newCharName, setNewCharName] = useState<string>('');

  // 2. Part / Component category
  const [selectedPartId, setSelectedPartId] = useState<string>('toc_truoc');
  const [isCreatingNewPart, setIsCreatingNewPart] = useState<boolean>(false);
  const [newPartName, setNewPartName] = useState<string>('');

  // 3. Angle
  const [selectedAngleDeg, setSelectedAngleDeg] = useState<number>(activeAngleDeg ?? 0);
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [saveToast, setSaveToast] = useState<string | null>(null);

  // Fetch available characters from API
  useEffect(() => {
    if (!isOpen) return;
    fetch('/api/list-2d-characters')
      .then((res) => res.json())
      .then((data) => {
        if (Array.isArray(data) && data.length > 0) {
          setCharacterList(data);
          if (!data.includes(selectedCharacter)) {
            setSelectedCharacter(data[0]);
          }
        }
      })
      .catch(() => {});
  }, [isOpen]);

  if (!isOpen) return null;

  // Determine images to save: checked images, or active images, or sliced results
  const imagesToSave: { id: string; name: string; url: string; base64?: string }[] = [];
  if (checkedImageItems.length > 0) {
    checkedImageItems.forEach((item) => {
      imagesToSave.push({
        id: item.id,
        name: item.name || `part_${item.id}`,
        url: item.transparentUrl || item.originalUrl || item.url,
      });
    });
  } else if (allImages.length > 0) {
    allImages.forEach((item) => {
      imagesToSave.push({
        id: item.id,
        name: item.name || `part_${item.id}`,
        url: item.transparentUrl || item.originalUrl || item.url,
      });
    });
  } else if (slicedResults.size > 0) {
    slicedResults.forEach((val, key) => {
      imagesToSave.push({
        id: key,
        name: `part_${key}`,
        url: val,
      });
    });
  }

  const finalCharSlug = isCreatingNewChar
    ? slugifyVietnamese(newCharName || 'nhan_vat_moi')
    : selectedCharacter || 'nhan_vat_chinh';

  const finalPartSlug = isCreatingNewPart
    ? slugifyVietnamese(newPartName || 'linh_kien_moi')
    : selectedPartId || 'linh_kien';

  const finalPartDisplayName = isCreatingNewPart
    ? newPartName
    : DEFAULT_PART_CATEGORIES.find((p) => p.id === selectedPartId)?.label || selectedPartId;

  const activeAngle = DEFAULT_ANGLES.find((a) => a.deg === selectedAngleDeg) || DEFAULT_ANGLES[1];
  const angleSlug = activeAngle.deg >= 0 ? activeAngle.id : '';

  const targetFolderPath = `asset_2ds/chi_tiet_nhan_vat/${finalPartSlug}${angleSlug ? `/${angleSlug}` : ''}`;

  // Handle Save
  const handleSave = async () => {
    if (isCreatingNewChar && !newCharName.trim()) {
      alert('Vui lòng nhập tên nhân vật mới!');
      return;
    }
    if (isCreatingNewPart && !newPartName.trim()) {
      alert('Vui lòng nhập tên linh kiện / chi tiết mới!');
      return;
    }

    if (imagesToSave.length === 0) {
      alert('Không có ảnh nào để lưu!');
      return;
    }

    setIsSaving(true);
    setSaveToast(`Đang lưu ${imagesToSave.length} linh kiện vào ${targetFolderPath}...`);

    try {
      // 1. Save to Disk via Backend Vite API
      const response = await fetch('/api/save-2d-parts', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          genre: 'chi_tiet_nhan_vat',
          character: finalCharSlug,
          partCategory: finalPartSlug,
          partDisplayName: finalPartDisplayName,
          angleSlug: angleSlug,
          items: imagesToSave,
        }),
      });

      const resData = await response.json();
      if (!resData.success) {
        throw new Error(resData.error || 'Lỗi lưu file');
      }

      // 2. Also register in local app Resource Kit Storage
      const partsMap: Record<string, string> = {};
      imagesToSave.forEach((item, idx) => {
        partsMap[`${finalPartSlug}_${idx}`] = item.url;
      });

      const selectedDef = DEFAULT_PART_CATEGORIES.find((p) => p.id === selectedPartId);
      const kitCategory = (selectedDef?.category || 'phu_kien') as CharacterResourceCategory;

      saveCustomResourceKit({
        id: `kit_${finalCharSlug}_${finalPartSlug}_${Date.now()}`,
        name: `${formatVietnameseDisplayName(finalCharSlug)} - ${finalPartDisplayName}`,
        category: kitCategory,
        categoryLabel: `Nhân Vật - ${finalPartDisplayName}`,
        previewImage: imagesToSave[0]?.url || '',
        description: `Linh kiện ${finalPartDisplayName} của ${finalCharSlug}`,
        parts: partsMap as any,
        createdAt: new Date().toISOString(),
      });

      const successMsg = `✓ Đã lưu thành công ${imagesToSave.length} ảnh vào thư mục "${finalPartDisplayName}" (${resData.targetDir})!`;
      setSaveToast(successMsg);
      onSaveSuccess?.(successMsg);

      setTimeout(() => {
        setIsSaving(false);
        setSaveToast(null);
        onClose();
      }, 1400);
    } catch (err: any) {
      setIsSaving(false);
      setSaveToast(`❌ Lỗi: ${err?.message || 'Không thể lưu file'}`);
      setTimeout(() => setSaveToast(null), 3000);
    }
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 10000,
        background: 'rgba(2, 6, 23, 0.88)',
        backdropFilter: 'blur(16px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 16,
      }}
    >
      <div
        style={{
          width: '90vw',
          maxWidth: '680px',
          background: '#090d16',
          border: '1.5px solid rgba(56, 189, 248, 0.4)',
          borderRadius: 12,
          boxShadow: '0 24px 60px rgba(0, 0, 0, 0.9), 0 0 30px rgba(56, 189, 248, 0.2)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}
      >
        {/* Header */}
        <div
          style={{
            height: 48,
            background: 'rgba(15, 23, 42, 0.95)',
            borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0 16px',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div
              style={{
                width: 28,
                height: 28,
                borderRadius: 6,
                background: 'linear-gradient(135deg, #0284c7, #10b981)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#fff',
              }}
            >
              <Save size={15} />
            </div>
            <div style={{ fontSize: 13, fontWeight: 700, color: '#f8fafc' }}>
              Lưu Linh Kiện Vào Kho Chi Tiết (asset_2ds/chi_tiet_nhan_vat)
            </div>
            <span
              style={{
                fontSize: 10,
                fontWeight: 700,
                color: '#38bdf8',
                background: 'rgba(56, 189, 248, 0.15)',
                border: '1px solid rgba(56, 189, 248, 0.3)',
                padding: '2px 8px',
                borderRadius: 4,
              }}
            >
              {imagesToSave.length} ảnh
            </span>
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
            <X size={15} />
          </button>
        </div>

        {/* Body */}
        <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 12, overflowY: 'auto', maxHeight: '78vh' }}>
          {/* Thumbnails Preview Strip */}
          {imagesToSave.length > 0 && (
            <div
              style={{
                display: 'flex',
                gap: 6,
                overflowX: 'auto',
                padding: 6,
                background: 'rgba(0, 0, 0, 0.4)',
                borderRadius: 8,
                border: '1px solid rgba(255, 255, 255, 0.08)',
              }}
            >
              {imagesToSave.map((item, idx) => (
                <div
                  key={`${item.id}_${idx}`}
                  style={{
                    flexShrink: 0,
                    width: 44,
                    height: 44,
                    borderRadius: 6,
                    overflow: 'hidden',
                    border: '1px solid rgba(56, 189, 248, 0.35)',
                    background: 'repeating-conic-gradient(#1e293b 0% 25%, #0f172a 0% 50%) 50% / 8px 8px',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    padding: 2,
                    boxSizing: 'border-box',
                  }}
                  title={item.name}
                >
                  <img src={item.url} alt={item.name} style={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain' }} />
                </div>
              ))}
            </div>
          )}

          {/* 1. Character Selector */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
                <User size={13} /> 1. Nhân Vật (Character):
              </label>
              <button
                onClick={() => setIsCreatingNewChar(!isCreatingNewChar)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 3,
                  padding: '2px 8px',
                  borderRadius: 4,
                  fontSize: 10,
                  fontWeight: 600,
                  background: isCreatingNewChar ? 'rgba(239, 68, 68, 0.2)' : 'rgba(56, 189, 248, 0.15)',
                  border: isCreatingNewChar ? '1px solid #ef4444' : '1px solid #38bdf8',
                  color: isCreatingNewChar ? '#fca5a5' : '#38bdf8',
                  cursor: 'pointer',
                }}
              >
                {isCreatingNewChar ? <X size={10} /> : <Plus size={10} />}
                {isCreatingNewChar ? 'Chọn nhân vật có sẵn' : 'Tạo nhân vật mới'}
              </button>
            </div>

            {isCreatingNewChar ? (
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <input
                  type="text"
                  value={newCharName}
                  onChange={(e) => setNewCharName(e.target.value)}
                  placeholder="Ví dụ: Tôn Ngộ Không, Tiêu Viêm, Nữ Hiệp Áo Trắng..."
                  style={{
                    flex: 1,
                    background: '#0d1527',
                    border: '1px solid #38bdf8',
                    borderRadius: 6,
                    padding: '6px 10px',
                    fontSize: 11,
                    color: '#f8fafc',
                    outline: 'none',
                  }}
                />
                <span style={{ fontSize: 10, color: '#94a3b8', background: 'rgba(255,255,255,0.05)', padding: '6px 8px', borderRadius: 4 }}>
                  Folder: <b style={{ color: '#38bdf8' }}>{slugifyVietnamese(newCharName || 'nhan_vat_moi')}</b>
                </span>
              </div>
            ) : (
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                {characterList.map((char) => (
                  <button
                    key={char}
                    onClick={() => setSelectedCharacter(char)}
                    style={{
                      padding: '5px 12px',
                      borderRadius: 6,
                      fontSize: 11,
                      fontWeight: 600,
                      background: selectedCharacter === char ? 'rgba(56, 189, 248, 0.3)' : 'rgba(255, 255, 255, 0.04)',
                      border: selectedCharacter === char ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.08)',
                      color: selectedCharacter === char ? '#38bdf8' : '#cbd5e1',
                      cursor: 'pointer',
                    }}
                  >
                    👤 {formatVietnameseDisplayName(char)}
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* 2. Part / Component Category Combobox */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ fontSize: 11, fontWeight: 700, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 4 }}>
                <Package size={13} /> 2. Bộ Phận / Chi Tiết (Part Category):
              </label>
              <button
                onClick={() => setIsCreatingNewPart(!isCreatingNewPart)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 3,
                  padding: '2px 8px',
                  borderRadius: 4,
                  fontSize: 10,
                  fontWeight: 600,
                  background: isCreatingNewPart ? 'rgba(239, 68, 68, 0.2)' : 'rgba(74, 222, 128, 0.15)',
                  border: isCreatingNewPart ? '1px solid #ef4444' : '1px solid #4ade80',
                  color: isCreatingNewPart ? '#fca5a5' : '#4ade80',
                  cursor: 'pointer',
                }}
              >
                {isCreatingNewPart ? <X size={10} /> : <Plus size={10} />}
                {isCreatingNewPart ? 'Chọn linh kiện có sẵn' : 'Tạo tên linh kiện mới'}
              </button>
            </div>

            {isCreatingNewPart ? (
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <input
                  type="text"
                  value={newPartName}
                  onChange={(e) => setNewPartName(e.target.value)}
                  placeholder="Ví dụ: Vương Miện Hoàng Kim, Cánh Rồng, Áo Giáp Sắt..."
                  style={{
                    flex: 1,
                    background: '#0d1527',
                    border: '1px solid #4ade80',
                    borderRadius: 6,
                    padding: '6px 10px',
                    fontSize: 11,
                    color: '#f8fafc',
                    outline: 'none',
                  }}
                />
                <span style={{ fontSize: 10, color: '#94a3b8', background: 'rgba(255,255,255,0.05)', padding: '6px 8px', borderRadius: 4 }}>
                  Folder: <b style={{ color: '#4ade80' }}>{slugifyVietnamese(newPartName || 'linh_kien_moi')}</b>
                </span>
              </div>
            ) : (
              <select
                value={selectedPartId}
                onChange={(e) => setSelectedPartId(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  fontSize: 11.5,
                  fontWeight: 600,
                  background: '#0d1527',
                  color: '#4ade80',
                  border: '1.5px solid rgba(74, 222, 128, 0.5)',
                  borderRadius: 6,
                  outline: 'none',
                  cursor: 'pointer',
                }}
              >
                {DEFAULT_PART_CATEGORIES.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.label}
                  </option>
                ))}
              </select>
            )}
          </div>

          {/* 3. Angle Selector */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <label style={{ fontSize: 11, fontWeight: 700, color: '#facc15', display: 'flex', alignItems: 'center', gap: 4 }}>
              <Compass size={13} /> 3. Góc Nhìn (Horizontal Angle):
            </label>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 5 }}>
              {DEFAULT_ANGLES.map((ang) => {
                const isSelected = selectedAngleDeg === ang.deg;
                return (
                  <button
                    key={ang.id}
                    onClick={() => setSelectedAngleDeg(ang.deg)}
                    style={{
                      padding: '5px 8px',
                      borderRadius: 6,
                      fontSize: 10,
                      fontWeight: 600,
                      background: isSelected ? 'rgba(250, 204, 21, 0.25)' : 'rgba(255, 255, 255, 0.04)',
                      border: isSelected ? '1px solid #facc15' : '1px solid rgba(255, 255, 255, 0.08)',
                      color: isSelected ? '#fef08a' : '#94a3b8',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      gap: 4,
                    }}
                  >
                    {ang.label}
                  </button>
                );
              })}
            </div>
          </div>

          {/* Target Folder Path Preview */}
          <div
            style={{
              padding: '8px 12px',
              background: 'rgba(0, 0, 0, 0.5)',
              borderRadius: 6,
              border: '1px solid rgba(56, 189, 248, 0.25)',
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              fontSize: 10.5,
              color: '#94a3b8',
            }}
          >
            <Folder size={14} color="#38bdf8" />
            <span>Thư mục lưu trữ:</span>
            <code style={{ color: '#34d399', fontWeight: 700, fontFamily: 'monospace' }}>
              {targetFolderPath}
            </code>
          </div>

          {/* Toast Msg */}
          {saveToast && (
            <div
              style={{
                padding: '6px 12px',
                borderRadius: 6,
                background: saveToast.startsWith('✓')
                  ? 'rgba(34, 197, 94, 0.2)'
                  : saveToast.startsWith('❌')
                  ? 'rgba(239, 68, 68, 0.2)'
                  : 'rgba(56, 189, 248, 0.2)',
                border: `1px solid ${saveToast.startsWith('✓') ? '#22c55e' : saveToast.startsWith('❌') ? '#ef4444' : '#38bdf8'}`,
                color: saveToast.startsWith('✓') ? '#4ade80' : saveToast.startsWith('❌') ? '#fca5a5' : '#38bdf8',
                fontSize: 11,
                fontWeight: 700,
                textAlign: 'center',
              }}
            >
              {saveToast}
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div
          style={{
            height: 52,
            background: 'rgba(15, 23, 42, 0.95)',
            borderTop: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'flex-end',
            padding: '0 16px',
            gap: 8,
          }}
        >
          <button
            onClick={onClose}
            disabled={isSaving}
            style={{
              padding: '7px 16px',
              fontSize: 11,
              borderRadius: 6,
              background: 'rgba(255, 255, 255, 0.08)',
              color: '#cbd5e1',
              border: '1px solid rgba(255, 255, 255, 0.15)',
              cursor: isSaving ? 'not-allowed' : 'pointer',
            }}
          >
            Hủy
          </button>
          <button
            onClick={handleSave}
            disabled={isSaving || imagesToSave.length === 0}
            style={{
              padding: '7px 20px',
              fontSize: 11.5,
              fontWeight: 700,
              borderRadius: 6,
              background: isSaving
                ? 'rgba(255, 255, 255, 0.1)'
                : 'linear-gradient(135deg, #0284c7 0%, #10b981 100%)',
              color: '#ffffff',
              border: 'none',
              cursor: isSaving || imagesToSave.length === 0 ? 'not-allowed' : 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              boxShadow: '0 2px 12px rgba(2, 132, 199, 0.4)',
            }}
          >
            <Check size={14} />
            <span>{isSaving ? 'Đang lưu...' : `Lưu ${imagesToSave.length} Linh Kiện Vào Thư Mục`}</span>
          </button>
        </div>
      </div>
    </div>
  );
};
