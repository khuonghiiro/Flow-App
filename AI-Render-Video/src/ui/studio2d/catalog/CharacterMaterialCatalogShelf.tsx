import React, { useState, useEffect } from 'react';
import { Sparkles, FolderOpen, Trash2 } from 'lucide-react';
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

  useEffect(() => {
    setFilterCategory(getSlotDefaultCategory(selectedSlot));
  }, [selectedSlot]);

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
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        background: '#0a0f1d',
        padding: 8,
        borderRadius: 8,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        minHeight: 0,
        overflow: 'hidden',
      }}
    >
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', paddingBottom: 4, borderBottom: '1px solid rgba(255,255,255,0.06)' }}>
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

      {/* 2-SubColumn Layout: Vertical Category Sidebar (Left) + Material Cards Grid (Right) */}
      <div style={{ flex: 1, display: 'grid', gridTemplateColumns: '142px 1fr', gap: 6, minHeight: 0, overflow: 'hidden' }}>
        {/* 1. Tab Dọc Sidebar */}
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: 4,
            background: 'rgba(0, 0, 0, 0.35)',
            padding: 5,
            borderRadius: 6,
            overflowY: 'auto',
            border: '1px solid rgba(255, 255, 255, 0.04)',
          }}
        >
          <button
            onClick={() => setFilterCategory('all')}
            style={{
              padding: '6px 8px',
              fontSize: 11,
              fontWeight: filterCategory === 'all' ? 700 : 600,
              borderRadius: 5,
              border: filterCategory === 'all' ? '1px solid #38bdf8' : '1px solid transparent',
              background: filterCategory === 'all' ? 'rgba(56, 189, 248, 0.22)' : 'transparent',
              color: filterCategory === 'all' ? '#38bdf8' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              textAlign: 'left',
              transition: 'all 0.12s',
            }}
          >
            <span>🌟 Tất cả</span>
            <span style={{ fontSize: 9.5, opacity: 0.85, fontWeight: 700 }}>({allKits.length})</span>
          </button>

          {RESOURCE_CATEGORIES.map((cat) => {
            const isActive = filterCategory === cat.id;
            const count = allKits.filter((k) => (cat.id === 'custom_slices' ? k.category === 'custom_slices' || k.id.startsWith('custom_') : k.category === cat.id)).length;
            return (
              <button
                key={cat.id}
                onClick={() => setFilterCategory(cat.id)}
                style={{
                  padding: '6px 8px',
                  fontSize: 11,
                  fontWeight: isActive ? 700 : 500,
                  borderRadius: 5,
                  border: isActive ? '1px solid #38bdf8' : '1px solid transparent',
                  background: isActive ? 'rgba(56, 189, 248, 0.22)' : 'transparent',
                  color: isActive ? '#38bdf8' : '#94a3b8',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  textAlign: 'left',
                  lineHeight: 1.25,
                  transition: 'all 0.12s',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                  <span style={{ fontSize: 14 }}>{cat.icon}</span>
                  <span style={{ wordBreak: 'break-word', whiteSpace: 'normal' }}>{cat.label}</span>
                </div>
                <span style={{ fontSize: 9.5, opacity: 0.85, fontWeight: 700, marginLeft: 4 }}>({count})</span>
              </button>
            );
          })}
        </div>

        {/* 2. Lưới Card Vật Liệu (Fixed Height, không bị kéo dãn) */}
        <div
          style={{
            flex: 1,
            background: 'rgba(0, 0, 0, 0.25)',
            borderRadius: 6,
            padding: 6,
            overflowY: 'auto',
            border: '1px solid rgba(255, 255, 255, 0.04)',
            minHeight: 0,
          }}
        >
          {filteredKits.length === 0 ? (
            <div
              style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                height: '100%',
                color: '#64748b',
                gap: 6,
                padding: 16,
                textAlign: 'center',
              }}
            >
              <FolderOpen size={24} color="#475569" />
              <div style={{ fontSize: 11, fontWeight: 600, color: '#94a3b8' }}>
                Chưa có linh kiện nào trong mục này
              </div>
              <div style={{ fontSize: 9.5, color: '#64748b', maxWidth: 220 }}>
                Hãy sang <b>Tab 1 [Cắt Lưới & Lắp Ráp 3D]</b> để bóc tách từ ảnh Sprite Sheet và lưu vào kho nhé!
              </div>
            </div>
          ) : (
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fill, minmax(110px, 1fr))',
                gridAutoRows: 'max-content',
                alignContent: 'start',
                gap: 6,
              }}
            >
              {filteredKits.map((kit) => {
                const isCustom = kit.id.startsWith('custom_') || kit.category === 'custom_slices';

                return (
                  <div
                    key={kit.id}
                    onClick={() => onMixPartFromKit(kit, selectedSlot)}
                    style={{
                      height: 124,
                      background: 'rgba(15, 23, 42, 0.85)',
                      border: '1px solid rgba(56, 189, 248, 0.22)',
                      borderRadius: 6,
                      padding: 5,
                      cursor: 'pointer',
                      display: 'flex',
                      flexDirection: 'column',
                      gap: 3,
                      boxSizing: 'border-box',
                      transition: 'all 0.15s ease',
                    }}
                    title={`Trang bị ${kit.name} vào ô ${selectedSlot}`}
                  >
                    <div
                      style={{
                        height: 62,
                        borderRadius: 4,
                        background: '#040711',
                        border: '1px solid rgba(255,255,255,0.06)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        overflow: 'hidden',
                        position: 'relative',
                        flexShrink: 0,
                      }}
                    >
                      {kit.previewImage ? (
                        <img
                          src={kit.previewImage}
                          alt={kit.name}
                          style={{ maxWidth: '88%', maxHeight: '88%', objectFit: 'contain' }}
                        />
                      ) : (
                        <span style={{ fontSize: 20 }}>🎨</span>
                      )}
                      {kit.angleCount && (
                        <span
                          style={{
                            position: 'absolute',
                            top: 2,
                            right: 2,
                            fontSize: 7,
                            fontWeight: 700,
                            padding: '1px 3px',
                            borderRadius: 2,
                            background: 'rgba(2, 132, 199, 0.85)',
                            color: '#fff',
                          }}
                        >
                          {kit.angleCount}G
                        </span>
                      )}
                    </div>

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

                    <div style={{ display: 'flex', gap: 3, marginTop: 'auto' }}>
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          onMixPartFromKit(kit, selectedSlot);
                        }}
                        style={{
                          flex: 1,
                          padding: '3px 4px',
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
                          gap: 2,
                        }}
                      >
                        <Sparkles size={9} /> Ráp
                      </button>

                      {isCustom && (
                        <button
                          onClick={(e) => onDeleteCustomKit(kit.id, e)}
                          style={{
                            padding: '2px 4px',
                            fontSize: 8.5,
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
          )}
        </div>
      </div>
    </div>
  );
};
