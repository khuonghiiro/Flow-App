import React, { useState, useEffect } from 'react';
import { Sparkles, FolderOpen, Trash2 } from 'lucide-react';
import {
  Character2DAssembly,
  Character2DPartType,
  CharacterResourceCategory,
  CharacterResourceKit,
} from '../../../types/scene2d';
import {
  RESOURCE_CATEGORIES,
  loadAllResourceKits,
  deleteCustomResourceKit,
} from '../../../core/assets/CharacterKitStorage';

interface AssemblerMaterialDrawerProps {
  selectedSlot: Character2DPartType;
  onApplyKitToSlot: (kit: CharacterResourceKit, slot: Character2DPartType) => void;
  onOpenFullCatalog: () => void;
}

export const AssemblerMaterialDrawer: React.FC<AssemblerMaterialDrawerProps> = ({
  selectedSlot,
  onApplyKitToSlot,
  onOpenFullCatalog,
}) => {
  const [allKits, setAllKits] = useState<CharacterResourceKit[]>([]);

  // Slot to default category
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
  const [filterCat, setFilterCat] = useState<CharacterResourceCategory | 'all'>(defaultCat);

  useEffect(() => {
    setAllKits(loadAllResourceKits());
  }, []);

  useEffect(() => {
    setFilterCat(getSlotDefaultCategory(selectedSlot));
  }, [selectedSlot]);

  const filteredKits = allKits.filter((k) => {
    if (filterCat === 'all') return true;
    if (filterCat === 'custom_slices') {
      return k.category === 'custom_slices' || k.id.startsWith('custom_');
    }
    return k.category === filterCat || k.parts[selectedSlot];
  });

  const handleDeleteKit = (kitId: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (window.confirm('Bạn có chắc muốn xóa bộ linh kiện này khỏi kho tài nguyên?')) {
      deleteCustomResourceKit(kitId);
      setAllKits(loadAllResourceKits());
    }
  };

  return (
    <div
      style={{
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        background: '#0a0f1d',
        padding: 10,
        borderRadius: 8,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        minHeight: 0,
        overflow: 'hidden',
      }}
    >
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <FolderOpen size={14} color="#38bdf8" />
          <span style={{ fontSize: 11.5, fontWeight: 700, color: '#f8fafc' }}>
            DANH MỤC VẬT LIỆU CHO Ô: <span style={{ color: '#38bdf8' }}>[{selectedSlot.toUpperCase()}]</span>
          </span>
        </div>

        <button
          onClick={onOpenFullCatalog}
          style={{
            padding: '3px 9px',
            fontSize: 10,
            fontWeight: 700,
            borderRadius: 4,
            background: 'linear-gradient(135deg, #7c3aed, #9333ea)',
            color: '#fff',
            border: 'none',
            cursor: 'pointer',
            boxShadow: '0 0 10px rgba(168, 85, 247, 0.3)',
          }}
        >
          🛍️ Mở Toàn Bộ Kho
        </button>
      </div>

      {/* Category Pills */}
      <div
        style={{
          display: 'flex',
          gap: 4,
          overflowX: 'auto',
          paddingBottom: 2,
        }}
      >
        <button
          onClick={() => setFilterCat('all')}
          style={{
            padding: '3px 8px',
            fontSize: 9.5,
            fontWeight: filterCat === 'all' ? 700 : 500,
            borderRadius: 4,
            border: filterCat === 'all' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
            background: filterCat === 'all' ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.03)',
            color: filterCat === 'all' ? '#38bdf8' : '#94a3b8',
            cursor: 'pointer',
            whiteSpace: 'nowrap',
          }}
        >
          Tất cả ({allKits.length})
        </button>

        {RESOURCE_CATEGORIES.map((cat) => {
          const isActive = filterCat === cat.id;
          const count = allKits.filter((k) => (cat.id === 'custom_slices' ? k.category === 'custom_slices' || k.id.startsWith('custom_') : k.category === cat.id)).length;
          return (
            <button
              key={cat.id}
              onClick={() => setFilterCat(cat.id)}
              style={{
                padding: '3px 8px',
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
                gap: 4,
              }}
            >
              <span>{cat.icon}</span>
              <span>{cat.label}</span>
              <span style={{ fontSize: 8.5, opacity: 0.8 }}>({count})</span>
            </button>
          );
        })}
      </div>

      {/* Cards Grid - Expands to fill available width and height */}
      <div
        style={{
          flex: 1,
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(130px, 1fr))',
          gap: 8,
          overflowY: 'auto',
          padding: 2,
        }}
      >
        {filteredKits.map((kit) => {
          const isCustom = kit.id.startsWith('custom_') || kit.category === 'custom_slices';

          return (
            <div
              key={kit.id}
              onClick={() => onApplyKitToSlot(kit, selectedSlot)}
              style={{
                background: 'rgba(15, 23, 42, 0.85)',
                border: '1px solid rgba(56, 189, 248, 0.22)',
                borderRadius: 6,
                padding: 6,
                cursor: 'pointer',
                display: 'flex',
                flexDirection: 'column',
                gap: 4,
                transition: 'all 0.15s ease',
              }}
              title={`Trang bị ${kit.name} vào ô ${selectedSlot}`}
            >
              <div
                style={{
                  height: 75,
                  borderRadius: 4,
                  background: '#040711',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  overflow: 'hidden',
                  position: 'relative',
                }}
              >
                {kit.previewImage ? (
                  <img src={kit.previewImage} alt={kit.name} style={{ maxWidth: '85%', maxHeight: '85%', objectFit: 'contain' }} />
                ) : (
                  <span style={{ fontSize: 22 }}>🎨</span>
                )}
                {kit.angleCount && (
                  <span
                    style={{
                      position: 'absolute',
                      top: 2,
                      right: 2,
                      fontSize: 7.5,
                      fontWeight: 700,
                      padding: '1px 4px',
                      borderRadius: 3,
                      background: 'rgba(2, 132, 199, 0.85)',
                      color: '#fff',
                    }}
                  >
                    {kit.angleCount} Góc 3D
                  </span>
                )}
              </div>

              <div style={{ fontSize: 10, fontWeight: 700, color: '#f8fafc', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                {kit.name}
              </div>

              <div style={{ display: 'flex', gap: 4, marginTop: 'auto' }}>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    onApplyKitToSlot(kit, selectedSlot);
                  }}
                  style={{
                    flex: 1,
                    padding: '3px 4px',
                    fontSize: 9.5,
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
                  <Sparkles size={10} /> Ráp Vào
                </button>
                {isCustom && (
                  <button
                    onClick={(e) => handleDeleteKit(kit.id, e)}
                    style={{ padding: '2px 5px', fontSize: 9, borderRadius: 3, background: 'rgba(239,68,68,0.15)', color: '#f87171', border: 'none', cursor: 'pointer' }}
                  >
                    <Trash2 size={10} />
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
