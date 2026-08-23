import React, { useState } from 'react';
import {
  Character2DAssembly,
  Character2DPartType,
  CharacterResourceKit,
} from '../../types/scene2d';
import { PART_HIERARCHY_CONFIG } from '../../core/assets/Asset2DRegistry';
import { applyKitToAssembly } from '../../core/assets/CharacterKitStorage';
import { AssemblerMainViewport } from './assembler/AssemblerMainViewport';
import { AssemblerEquipWing } from './assembler/AssemblerEquipHUD';
import { AssemblerMaterialDrawer } from './assembler/AssemblerMaterialDrawer';
import { AssemblerInspectorPanel } from './assembler/AssemblerInspectorPanel';
import { CharacterAssetCatalogModal } from './CharacterAssetCatalogModal';

interface Character2DAssemblerProps {
  assembly: Character2DAssembly;
  onChangeAssembly: (updated: Character2DAssembly) => void;
  onSaveCharacter?: (saved: Character2DAssembly) => void;
}

export const Character2DAssembler: React.FC<Character2DAssemblerProps> = ({
  assembly,
  onChangeAssembly,
  onSaveCharacter,
}) => {
  const [selectedSlot, setSelectedSlot] = useState<Character2DPartType>('toc_truoc');
  const [animMode, setAnimMode] = useState<'idle' | 'breathe' | 'talk' | 'combat_slash'>('breathe');
  const [isPlaying, setIsPlaying] = useState<boolean>(true);
  const [isBlinking, setIsBlinking] = useState<boolean>(false);
  const [isTalking, setIsTalking] = useState<boolean>(false);
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [saveSuccessMsg, setSaveSuccessMsg] = useState<string | null>(null);
  const [isCatalogOpen, setIsCatalogOpen] = useState<boolean>(false);

  // Auto-align all parts to natural anatomical offsets
  const handleAutoAlignAnatomy = () => {
    const nextParts = { ...assembly.parts };
    Object.keys(PART_HIERARCHY_CONFIG).forEach((key) => {
      const slot = key as Character2DPartType;
      const def = PART_HIERARCHY_CONFIG[slot];
      if (nextParts[slot]) {
        nextParts[slot] = {
          ...nextParts[slot],
          offset: def.defaultOffset,
          pivot: def.defaultPivot,
          z_index: def.defaultZ,
          z_depth_3d: def.defaultZDepth3D || 0,
        };
      }
    });
    onChangeAssembly({
      ...assembly,
      parts: nextParts,
      updated_at: new Date().toISOString(),
    });
    setSaveSuccessMsg('Đã tự động căn chuẩn giải phẫu!');
    setTimeout(() => setSaveSuccessMsg(null), 2500);
  };

  // Mix / Equip a part from a resource kit to the active slot
  const handleApplyKitToSlot = (kit: CharacterResourceKit, slot: Character2DPartType) => {
    const sourcePart = kit.parts[slot];
    if (!sourcePart) {
      if (slot === 'toc_truoc' && kit.parts.toc_sau) {
        const updated = applyKitToAssembly(assembly, kit);
        onChangeAssembly(updated);
        setSaveSuccessMsg(`Đã ráp trọn bộ "${kit.name}"!`);
        setTimeout(() => setSaveSuccessMsg(null), 2500);
        return;
      }
      return;
    }

    const updatedParts = {
      ...assembly.parts,
      [slot]: {
        ...(assembly.parts[slot] || {
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
          ...(assembly.parts[slot]?.angles || {}),
          ...(sourcePart.angles || {}),
        },
      },
    };

    if (slot === 'toc_truoc' && kit.parts.toc_sau) {
      updatedParts.toc_sau = {
        ...(assembly.parts.toc_sau || {
          offset: [0, -70],
          scale: [1.1, 1.15],
          rotation: 0,
          pivot: [0.5, 0.2],
          flipX: false,
          flipY: false,
          z_index: 1,
          z_depth_3d: -1,
          opacity: 1,
        }),
        ...kit.parts.toc_sau,
      };
    }

    onChangeAssembly({
      ...assembly,
      parts: updatedParts,
      updated_at: new Date().toISOString(),
    });

    setSaveSuccessMsg(`Đã trang bị "${kit.name}" vào ô [${slot.toUpperCase()}]!`);
    setTimeout(() => setSaveSuccessMsg(null), 2500);
  };

  // Save Character handler
  const handleSaveCharacter = async () => {
    setIsSaving(true);
    try {
      if (onSaveCharacter) {
        await onSaveCharacter(assembly);
      }
      setSaveSuccessMsg('Đã lưu cấu hình nhân vật thành công!');
      setTimeout(() => setSaveSuccessMsg(null), 3000);
    } catch (err) {
      console.error('[Character2DAssembler] Save failed:', err);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '46% 54%', gap: 10, height: '100%', overflow: 'hidden' }}>
      {/* ─── CỘT 1: Viewport Trung Tâm Kẹp Giữa 2 Cánh Ô Trang Bị (Trái & Phải) ── */}
      <div style={{ display: 'flex', gap: 6, height: '100%', overflow: 'hidden' }}>
        {/* Cánh Trái: Đầu & Mặt (4 ô vuông) */}
        <AssemblerEquipWing
          side="left"
          assembly={assembly}
          selectedSlot={selectedSlot}
          onSelectSlot={setSelectedSlot}
        />

        {/* Viewport Ở Giữa (Canvas 2D / Không gian 3D) */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', minWidth: 0 }}>
          <AssemblerMainViewport
            assembly={assembly}
            selectedSlot={selectedSlot}
            onChangeAssembly={onChangeAssembly}
            animMode={animMode}
            isPlaying={isPlaying}
            isBlinking={isBlinking}
            isTalking={isTalking}
            onAutoAlignAnatomy={handleAutoAlignAnatomy}
          />
        </div>

        {/* Cánh Phải: Thân, Áo, Tóc Sau, Vũ Khí (4 ô vuông) */}
        <AssemblerEquipWing
          side="right"
          assembly={assembly}
          selectedSlot={selectedSlot}
          onSelectSlot={setSelectedSlot}
        />
      </div>

      {/* ─── CỘT 2: Danh Mục Vật Liệu (Chiếm toàn bộ không gian) + Bảng Điều Khiển Co Dãn Gọn Gàng ─── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, height: '100%', overflow: 'hidden' }}>
        {/* Toast */}
        {saveSuccessMsg && (
          <div
            style={{
              padding: '4px 12px',
              borderRadius: 6,
              background: 'linear-gradient(90deg, #059669, #10b981)',
              color: '#fff',
              fontSize: 10.5,
              fontWeight: 700,
              textAlign: 'center',
            }}
          >
            ✓ {saveSuccessMsg}
          </div>
        )}

        {/* Danh Mục Vật Liệu (Chiếm toàn bộ width & height còn lại) */}
        <AssemblerMaterialDrawer
          selectedSlot={selectedSlot}
          onApplyKitToSlot={handleApplyKitToSlot}
          onOpenFullCatalog={() => setIsCatalogOpen(true)}
        />

        {/* Diễn Hoạt & Tinh Chỉnh Cấu Trúc (Co dãn gọn gàng) */}
        <AssemblerInspectorPanel
          assembly={assembly}
          selectedSlot={selectedSlot}
          onChangeAssembly={onChangeAssembly}
          animMode={animMode}
          setAnimMode={setAnimMode}
          isPlaying={isPlaying}
          setIsPlaying={setIsPlaying}
          isBlinking={isBlinking}
          setIsBlinking={setIsBlinking}
          isTalking={isTalking}
          setIsTalking={setIsTalking}
          isSaving={isSaving}
          onSaveCharacter={handleSaveCharacter}
        />
      </div>

      {/* Full Asset Catalog Modal */}
      {isCatalogOpen && (
        <CharacterAssetCatalogModal
          isOpen={isCatalogOpen}
          onClose={() => setIsCatalogOpen(false)}
          currentAssembly={assembly}
          onApplyAssembly={onChangeAssembly}
        />
      )}
    </div>
  );
};
