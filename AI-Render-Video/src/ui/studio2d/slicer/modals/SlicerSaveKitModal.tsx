import React, { useState } from 'react';
import { CharacterResourceCategory } from '../../../../types/scene2d';
import { saveCustomResourceKit } from '../../../../core/assets/CharacterKitStorage';
import { OBJECT_GENRE_OPTIONS } from '../sidebar/SlicerSourceImageCard';

export interface SlicerSaveKitModalProps {
  isOpen: boolean;
  onClose: () => void;
  slicedResults: Map<string, string>;
  categoryLabel: string;
  initialTargetCategory?: string;
}

export const SlicerSaveKitModal: React.FC<SlicerSaveKitModalProps> = ({
  isOpen,
  onClose,
  slicedResults,
  categoryLabel,
  initialTargetCategory = 'character',
}) => {
  const [saveKitName, setSaveKitName] = useState<string>('');
  const [saveKitGenre, setSaveKitGenre] = useState<string>(initialTargetCategory);
  const [saveKitCategory, setSaveKitCategory] = useState<CharacterResourceCategory>('toc');
  const [saveKitDescription, setSaveKitDescription] = useState<string>('');

  if (!isOpen) return null;

  const handleSave = () => {
    const partsMap: Record<string, string> = {};
    slicedResults.forEach((val, key) => {
      partsMap[key] = val;
    });
    saveCustomResourceKit({
      id: `kit_${Date.now()}`,
      name: saveKitName || 'Bộ Linh Kiện Mới',
      category: saveKitCategory,
      categoryLabel: `${OBJECT_GENRE_OPTIONS.find((g) => g.id === saveKitGenre)?.label || 'Đối Tượng'} - ${categoryLabel}`,
      previewImage: slicedResults.get('0_0') || '',
      description: saveKitDescription,
      parts: partsMap as any,
      createdAt: new Date().toISOString(),
    });
    onClose();
    alert('✓ Đã lưu bộ linh kiện vào kho thành công!');
  };

  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.75)', backdropFilter: 'blur(6px)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
      <div style={{ background: '#0b1329', padding: 18, borderRadius: 10, border: '1px solid #38bdf8', width: 400, display: 'flex', flexDirection: 'column', gap: 12, boxShadow: '0 8px 32px rgba(0,0,0,0.8)' }}>
        <div style={{ fontSize: 13.5, fontWeight: 700, color: '#38bdf8' }}>💾 Lưu Bộ Linh Kiện Mới Vào Kho:</div>
        
        <div>
          <label style={{ fontSize: 10.5, color: '#94a3b8', display: 'block', marginBottom: 4 }}>Tên bộ:</label>
          <input
            type="text"
            value={saveKitName}
            onChange={(e) => setSaveKitName(e.target.value)}
            placeholder="Ví dụ: Tóc Kiếm Khách Đỏ"
            style={{ width: '100%', padding: '7px 10px', fontSize: 11.5, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6, boxSizing: 'border-box' }}
          />
        </div>

        <div>
          <label style={{ fontSize: 10.5, color: '#cbd5e1', display: 'block', marginBottom: 4, fontWeight: 700 }}>
            📂 Thể Loại Đối Tượng:
          </label>
          <select
            value={saveKitGenre}
            onChange={(e) => setSaveKitGenre(e.target.value)}
            style={{ width: '100%', padding: '7px 10px', fontSize: 11, background: '#0f172a', color: '#34d399', border: '1.5px solid rgba(52, 211, 153, 0.5)', borderRadius: 6, boxSizing: 'border-box', fontWeight: 600 }}
          >
            {OBJECT_GENRE_OPTIONS.map((g) => (
              <option key={g.id} value={g.id}>
                {g.label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label style={{ fontSize: 10.5, color: '#94a3b8', display: 'block', marginBottom: 4 }}>Phân loại linh kiện:</label>
          <select
            value={saveKitCategory}
            onChange={(e) => setSaveKitCategory(e.target.value as CharacterResourceCategory)}
            style={{ width: '100%', padding: '6px 10px', fontSize: 11, background: '#0f172a', color: '#38bdf8', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6, boxSizing: 'border-box' }}
          >
            <option value="toc">💇 Mái Tóc & Đuôi Tóc</option>
            <option value="khuon_mat">👤 Khuôn Mặt & Biểu Cảm</option>
            <option value="than_co_ban">🥋 Thân Thể & Dáng Đứng</option>
            <option value="trang_phuc">🧥 Trang Phục & Áo Choàng</option>
            <option value="phu_kien">⚔️ Vũ Khí & Phụ Kiện</option>
          </select>
        </div>

        <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 6 }}>
          <button
            onClick={onClose}
            style={{ padding: '7px 14px', fontSize: 11, borderRadius: 5, background: 'rgba(255,255,255,0.1)', color: '#cbd5e1', border: 'none', cursor: 'pointer' }}
          >
            Hủy
          </button>
          <button
            onClick={handleSave}
            style={{ padding: '7px 16px', fontSize: 11.5, fontWeight: 700, borderRadius: 5, background: 'linear-gradient(135deg, #0284c7, #2563eb)', color: '#fff', border: 'none', cursor: 'pointer', boxShadow: '0 2px 10px rgba(2, 132, 199, 0.4)' }}
          >
            Lưu Vào Kho
          </button>
        </div>
      </div>
    </div>
  );
};
