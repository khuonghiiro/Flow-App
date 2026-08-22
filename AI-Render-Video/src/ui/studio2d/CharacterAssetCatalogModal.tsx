import React, { useState, useEffect } from 'react';
import { X, Box, Check } from 'lucide-react';
import {
  Character2DAssembly,
  Character2DPartType,
  CharacterResourceKit,
} from '../../types/scene2d';
import {
  loadAllResourceKits,
  deleteCustomResourceKit,
  applyKitToAssembly,
} from '../../core/assets/CharacterKitStorage';
import { CharacterEquipSlotHUD } from './catalog/CharacterEquipSlotHUD';
import { CharacterViewport3DCanvas } from './catalog/CharacterViewport3DCanvas';
import { CharacterMaterialCatalogShelf } from './catalog/CharacterMaterialCatalogShelf';
import { CharacterStructureTunerPanel } from './catalog/CharacterStructureTunerPanel';

export interface CharacterAssetCatalogModalProps {
  isOpen: boolean;
  onClose: () => void;
  currentAssembly: Character2DAssembly;
  onApplyAssembly: (updated: Character2DAssembly) => void;
}

export const CharacterAssetCatalogModal: React.FC<CharacterAssetCatalogModalProps> = ({
  isOpen,
  onClose,
  currentAssembly,
  onApplyAssembly,
}) => {
  const [allKits, setAllKits] = useState<CharacterResourceKit[]>([]);
  const [selectedSlot, setSelectedSlot] = useState<Character2DPartType>('toc_truoc');
  const [notification, setNotification] = useState<string | null>(null);

  // Load kits from persistent storage
  useEffect(() => {
    if (isOpen) {
      const loaded = loadAllResourceKits();
      setAllKits(loaded);
    }
  }, [isOpen]);

  if (!isOpen) return null;

  /**
   * Applies / mixes a specific slot part from a kit into current assembly
   */
  const handleMixPartFromKit = (kit: CharacterResourceKit, slot: Character2DPartType) => {
    const sourcePart = kit.parts[slot];
    if (!sourcePart) {
      if (slot === 'toc_truoc' && kit.parts.toc_sau) {
        const updated = applyKitToAssembly(currentAssembly, kit);
        onApplyAssembly(updated);
        setNotification(`Đã trang bị trọn bộ "${kit.name}" vào nhân vật!`);
        setTimeout(() => setNotification(null), 3000);
        return;
      }
      return;
    }

    const updatedParts = {
      ...currentAssembly.parts,
      [slot]: {
        ...(currentAssembly.parts[slot] || {
          offset: [0, 0],
          scale: [1, 1],
          rotation: 0,
          pivot: [0.5, 0.5],
          flipX: false,
          flipY: false,
          z_index: 5,
          z_depth_3d: 0,
          opacity: 1,
        }),
        ...sourcePart,
        angles: {
          ...(currentAssembly.parts[slot]?.angles || {}),
          ...(sourcePart.angles || {}),
        },
      },
    };

    // If equipping front hair, also equip back hair from the same kit if available
    if (slot === 'toc_truoc' && kit.parts.toc_sau) {
      updatedParts.toc_sau = {
        ...(currentAssembly.parts.toc_sau || {
          offset: [0, -70],
          scale: [1.1, 1.15],
          rotation: 0,
          pivot: [0.5, 0.2],
          flipX: false,
          flipY: false,
          z_index: 1,
          z_depth_3d: -0.045,
          opacity: 1,
        }),
        ...kit.parts.toc_sau,
      };
    }

    onApplyAssembly({
      ...currentAssembly,
      parts: updatedParts,
      updated_at: new Date().toISOString(),
    });

    setNotification(`Đã trang bị "${kit.name}" vào ô [${slot.toUpperCase()}]!`);
    setTimeout(() => setNotification(null), 3000);
  };

  /**
   * Delete a custom kit
   */
  const handleDeleteCustomKit = (kitId: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (window.confirm('Bạn có chắc muốn xóa bộ linh kiện này khỏi kho tài nguyên?')) {
      deleteCustomResourceKit(kitId);
      setAllKits(loadAllResourceKits());
    }
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 10000,
        background: 'rgba(3, 7, 18, 0.94)',
        backdropFilter: 'blur(20px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 10,
      }}
    >
      <div
        style={{
          width: '97vw',
          maxWidth: '1600px',
          height: '95vh',
          background: '#0b1120',
          border: '1px solid rgba(56, 189, 248, 0.35)',
          borderRadius: 12,
          boxShadow: '0 25px 60px rgba(0,0,0,0.85), 0 0 35px rgba(56, 189, 248, 0.25)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}
      >
        {/* Modal Header */}
        <div
          style={{
            padding: '10px 18px',
            background: 'rgba(15, 23, 42, 0.95)',
            borderBottom: '1px solid rgba(255, 255, 255, 0.1)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div
              style={{
                width: 32,
                height: 32,
                borderRadius: 8,
                background: 'linear-gradient(135deg, #0284c7, #a855f7)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#fff',
                boxShadow: '0 0 14px rgba(56, 189, 248, 0.4)',
              }}
            >
              <Box size={17} />
            </div>
            <div>
              <h2 style={{ fontSize: 13.5, fontWeight: 700, color: '#f8fafc', margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
                XƯỞNG LẮP RÁP & TRANG BỊ NHÂN VẬT 3D (CHARACTER CUSTOMIZER & MIXER)
              </h2>
              <p style={{ fontSize: 10.5, color: '#94a3b8', margin: 0 }}>
                Chọn ô trang bị ở Cột 1 để xem và lắp các mẫu vật liệu từ Cột 2, xoay 3D 360° để kiểm tra độ khớp
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            style={{
              padding: '6px',
              borderRadius: 6,
              background: 'rgba(255, 255, 255, 0.05)',
              color: '#94a3b8',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              cursor: 'pointer',
            }}
            title="Đóng hộp thoại"
          >
            <X size={16} />
          </button>
        </div>

        {/* Notification Toast */}
        {notification && (
          <div
            style={{
              padding: '5px 16px',
              background: 'linear-gradient(90deg, #059669, #10b981)',
              color: '#fff',
              fontSize: 10.5,
              fontWeight: 700,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
            }}
          >
            <Check size={13} /> {notification}
          </div>
        )}

        {/* 2-Column Master Layout */}
        <div
          style={{
            flex: 1,
            display: 'grid',
            gridTemplateColumns: '58% 42%',
            gap: 12,
            padding: 12,
            overflow: 'hidden',
          }}
        >
          {/* ─── CỘT 1: Không gian 3D / Canvas & Các Ô Vuông Trang Bị ── */}
          <div
            style={{
              display: 'flex',
              flexDirection: 'column',
              gap: 10,
              overflow: 'hidden',
            }}
          >
            {/* Viewport 3D / Canvas */}
            <CharacterViewport3DCanvas assembly={currentAssembly} />

            {/* Các ô vuông trang bị nhân vật RPG */}
            <CharacterEquipSlotHUD
              assembly={currentAssembly}
              selectedSlot={selectedSlot}
              onSelectSlot={setSelectedSlot}
            />
          </div>

          {/* ─── CỘT 2: Danh Mục Vật Liệu & Bảng Tinh Chỉnh Cấu Trúc ─── */}
          <div
            style={{
              display: 'flex',
              flexDirection: 'column',
              gap: 10,
              overflow: 'hidden',
            }}
          >
            {/* Danh mục vật liệu cho ô trang bị đang chọn */}
            <CharacterMaterialCatalogShelf
              selectedSlot={selectedSlot}
              allKits={allKits}
              onMixPartFromKit={handleMixPartFromKit}
              onDeleteCustomKit={handleDeleteCustomKit}
            />

            {/* Bảng tinh chỉnh cấu trúc cho ô trang bị đang chọn */}
            <CharacterStructureTunerPanel
              selectedSlot={selectedSlot}
              assembly={currentAssembly}
              onChangeAssembly={onApplyAssembly}
              onSaveCompleted={() => {
                setNotification('Đã xác nhận và ráp diện mạo này vào nhân vật thành công!');
                setTimeout(() => setNotification(null), 3000);
              }}
            />
          </div>
        </div>
      </div>
    </div>
  );
};
