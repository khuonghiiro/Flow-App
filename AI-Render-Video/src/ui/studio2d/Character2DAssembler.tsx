import React, { useState } from 'react';
import {
  Character2DAssembly,
  Character2DPartType,
  CharacterResourceKit,
} from '../../types/scene2d';
import { PART_HIERARCHY_CONFIG } from '../../core/assets/Asset2DRegistry';
import { applyKitToAssembly } from '../../core/assets/CharacterKitStorage';
import { AssemblerMainViewport } from './assembler/AssemblerMainViewport';
import { AssemblerEquipHUD } from './assembler/AssemblerEquipHUD';
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
          z_depth_3d: -0.045,
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
    <div style={{ display: 'grid', gridTemplateColumns: '58% 42%', gap: 12, height: '100%', overflow: 'hidden' }}>
      {/* ─── CỘT 1: Không gian 3D / Canvas & Các Ô Vuông Trang Bị ── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10, overflow: 'hidden' }}>
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

        {/* Các ô vuông trang bị nhân vật RPG */}
        <AssemblerEquipHUD
          assembly={assembly}
          selectedSlot={selectedSlot}
          onSelectSlot={setSelectedSlot}
        />
      </div>

      {/* ─── CỘT 2: Danh Mục Vật Liệu & Bảng Tinh Chỉnh Cấu Trúc ─── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10, overflow: 'hidden' }}>
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

        {/* Material Catalog Drawer for selected slot */}
        <AssemblerMaterialDrawer
          selectedSlot={selectedSlot}
          onApplyKitToSlot={handleApplyKitToSlot}
          onOpenFullCatalog={() => setIsCatalogOpen(true)}
        />

        {/* Structure & Animation Inspector */}
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
