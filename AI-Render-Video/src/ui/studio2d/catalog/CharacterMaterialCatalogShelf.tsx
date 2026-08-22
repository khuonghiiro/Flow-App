import React, { useState } from 'react';
import { Sparkles, Trash2, FolderOpen } from 'lucide-react';
import {
  Character2DPartType,
  CharacterResourceCategory,
  CharacterResourceKit,
} from '../../../types/scene2d';
import { RESOURCE_CATEGORIES } from '../../../core/assets/CharacterKitStorage';

interface CharacterMaterialCatalogShelfProps {
  selectedSlot: Character2DPartType;
  allKits: CharacterResourceKit[];
  onMixPartFromKit: (kit: CharacterResourceKit, slot: Character2DPartType) => void;
  onDeleteCustomKit: (kitId: string, e: React.MouseEvent) => void;
}

export const CharacterMaterialCatalogShelf: React.FC<CharacterMaterialCatalogShelfProps> = ({
  selectedSlot,
  allKits,
  onMixPartFromKit,
  onDeleteCustomKit,
}) => {
  // Helper to get natural category from slot
  const getSlotDefaultCategory = (slot: Character2DPartType): CharacterResourceCategory => {
    if (slot === 'toc_truoc' || slot === 'toc_sau') return 'toc';
    if (slot === 'mat') return 'mat';
    if (slot === 'mieng') return 'mieng';
    if (slot === 'dau' || slot === 'khuon_mat' || slot === 'mui') return 'khuon_mat';
    if (slot === 'trang_phuc') return 'trang_phuc';
    if (slot === 'vu_khi') return 'vu_khi';
    return 'custom_slices';
  };

  const defaultCat = getSlotDefaultCategory(selectedSlot);
  const [filterCategory, setFilterCategory] = useState<CharacterResourceCategory | 'all'>(defaultCat);

  // Sync category when selected slot changes
  React.useEffect(() => {
    setFilterCategory(getSlotDefaultCategory(selectedSlot));
  }, [selectedSlot]);

  // Filter kits based on category or active slot availability
  const filteredKits = allKits.filter((k) => {
    if (filterCategory === 'all') return true;
    if (filterCategory === 'custom_slices') {
      return k.category === 'custom_slices' || k.id.startsWith('custom_');
    }
    return k.category === filterCategory || k.parts[selectedSlot];
  });

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        background: '#0a0f1d',
        padding: 10,
        borderRadius: 8,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        maxHeight: 240,
        overflow: 'hidden',
      }}
    >
      {/* Shelf Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <FolderOpen size={14} color="#38bdf8" />
          <span style={{ fontSize: 11, fontWeight: 700, color: '#f8fafc' }}>
            DANH MỤC VẬT LIỆU CHO Ô: <span style={{ color: '#38bdf8' }}>[{selectedSlot.toUpperCase()}]</span>
          </span>
        </div>
        <span style={{ fontSize: 9.5, color: '#94a3b8' }}>
          {filteredKits.length} mẫu có sẵn
        </span>
      </div>

      {/* Category Filter Pills */}
      <div
        style={{
          display: 'flex',
          gap: 4,
          overflowX: 'auto',
          paddingBottom: 2,
        }}
      >
        <button
          onClick={() => setFilterCategory('all')}
          style={{
            padding: '2px 7px',
            fontSize: 9.5,
            fontWeight: filterCategory === 'all' ? 700 : 500,
            borderRadius: 4,
            border: filterCategory === 'all' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
            background: filterCategory === 'all' ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.03)',
            color: filterCategory === 'all' ? '#38bdf8' : '#94a3b8',
            cursor: 'pointer',
            whiteSpace: 'nowrap',
          }}
        >
          Tất cả
        </button>

        {RESOURCE_CATEGORIES.map((cat) => {
          const isActive = filterCategory === cat.id;
          return (
            <button
              key={cat.id}
              onClick={() => setFilterCategory(cat.id)}
              style={{
                padding: '2px 7px',
                fontSize: 9.5,
                fontWeight: isActive ? 700 : 500,
                borderRadius: 4,
                border: isActive ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
                background: isActive ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.03)',
                color: isActive ? '#38bdf8' : '#94a3b8',
                cursor: 'pointer',
                whiteSpace: 'nowrap',
                display: 'flex',
                alignItems: 'center',
                gap: 3,
              }}
            >
              <span>{cat.icon}</span>
              <span>{cat.label}</span>
            </button>
          );
        })}
      </div>

      {/* Material Cards Grid */}
      <div
        style={{
          flex: 1,
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(130px, 1fr))',
          gap: 6,
          overflowY: 'auto',
          padding: 4,
        }}
      >
        {filteredKits.map((kit) => {
          const isCustom = kit.id.startsWith('custom_') || kit.category === 'custom_slices';

          return (
            <div
              key={kit.id}
              onClick={() => onMixPartFromKit(kit, selectedSlot)}
              style={{
                background: 'rgba(15, 23, 42, 0.75)',
                border: '1px solid rgba(56, 189, 248, 0.2)',
                borderRadius: 6,
                padding: 6,
                cursor: 'pointer',
                display: 'flex',
                flexDirection: 'column',
                gap: 4,
                transition: 'all 0.15s ease',
              }}
              title={`Bấm để trang bị "${kit.name}" vào ô ${selectedSlot}`}
            >
              {/* Thumbnail */}
              <div
                style={{
                  height: 60,
                  borderRadius: 4,
                  background: '#040711',
                  border: '1px solid rgba(255,255,255,0.06)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  overflow: 'hidden',
                  position: 'relative',
                }}
              >
                {kit.previewImage ? (
                  <img
                    src={kit.previewImage}
                    alt={kit.name}
                    style={{ maxWidth: '85%', maxHeight: '85%', objectFit: 'contain' }}
                  />
                ) : (
                  <span style={{ fontSize: 18 }}>🎨</span>
                )}

                {kit.angleCount && (
                  <span
                    style={{
                      position: 'absolute',
                      top: 2,
                      right: 2,
                      fontSize: 7.5,
                      fontWeight: 700,
                      padding: '1px 3px',
                      borderRadius: 3,
                      background: 'rgba(2, 132, 199, 0.85)',
                      color: '#fff',
                    }}
                  >
                    {kit.angleCount}G
                  </span>
                )}
              </div>

              {/* Title */}
              <div
                style={{
                  fontSize: 9.5,
                  fontWeight: 700,
                  color: '#f8fafc',
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                }}
              >
                {kit.name}
              </div>

              {/* Equip Action */}
              <div style={{ display: 'flex', gap: 4, marginTop: 'auto' }}>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    onMixPartFromKit(kit, selectedSlot);
                  }}
                  style={{
                    flex: 1,
                    padding: '3px',
                    fontSize: 9,
                    fontWeight: 700,
                    borderRadius: 3,
                    border: 'none',
                    background: 'linear-gradient(135deg, #0284c7, #2563eb)',
                    color: '#fff',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 3,
                  }}
                >
                  <Sparkles size={9} /> Trang Bị
                </button>

                {isCustom && (
                  <button
                    onClick={(e) => onDeleteCustomKit(kit.id, e)}
                    style={{
                      padding: '2px 5px',
                      fontSize: 9,
                      borderRadius: 3,
                      background: 'rgba(239, 68, 68, 0.15)',
                      color: '#f87171',
                      border: 'none',
                      cursor: 'pointer',
                    }}
                    title="Xóa bộ tự tạo này"
                  >
                    <Trash2 size={9} />
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
