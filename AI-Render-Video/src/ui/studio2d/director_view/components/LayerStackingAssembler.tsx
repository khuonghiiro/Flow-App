import React, { useState } from 'react';
import {
  Layers,
  Plus,
  Trash2,
  ChevronUp,
  ChevronDown,
  Eye,
  EyeOff,
  Upload,
  Move,
  Sliders,
  Sparkles,
} from 'lucide-react';
import {
  Actor2DProfile,
  LayerPartConfig,
} from '../../../../types/studio2d_director';

interface LayerStackingAssemblerProps {
  actor: Actor2DProfile;
  selectedPartId: string | null;
  onSelectPart: (partId: string) => void;
  onUpdateActor: (updated: Actor2DProfile) => void;
}

const PART_CATEGORIES: { id: LayerPartConfig['category']; label: string; icon: string }[] = [
  { id: 'toc_sau', label: 'Tóc Sau (Back Hair)', icon: '🌊' },
  { id: 'than', label: 'Thân Người (Torso)', icon: '🧍' },
  { id: 'khuon_mat', label: 'Khuôn Mặt (Face)', icon: '✨' },
  { id: 'mat', label: 'Đôi Mắt (Eyes)', icon: '👀' },
  { id: 'mieng', label: 'Khuôn Miệng (Mouth)', icon: '👄' },
  { id: 'toc_truoc', label: 'Mái Trước (Bangs)', icon: '💇' },
  { id: 'trang_phuc', label: 'Áo Trang Phục (Robes)', icon: '👘' },
  { id: 'tay', label: 'Cánh Tay (Arms)', icon: '💪' },
  { id: 'vu_khi', label: 'Vũ Khí (Weapon)', icon: '⚔️' },
  { id: 'vfx', label: 'Hiệu Ứng VFX (Aura)', icon: '🔥' },
  { id: 'custom', label: 'Lớp Tự Do (Custom)', icon: '🧩' },
];

export const LayerStackingAssembler: React.FC<LayerStackingAssemblerProps> = ({
  actor,
  selectedPartId,
  onSelectPart,
  onUpdateActor,
}) => {
  const parts = actor.parts || [];
  const selectedPart = parts.find((p) => p.id === selectedPartId) || parts[0];

  const updateParts = (newParts: LayerPartConfig[]) => {
    onUpdateActor({
      ...actor,
      parts: newParts,
    });
  };

  const handleAddPart = () => {
    const newId = `part_${Date.now().toString().slice(-4)}`;
    const newPart: LayerPartConfig = {
      id: newId,
      name: `Lớp Ghép #${parts.length + 1}`,
      category: 'custom',
      path: '',
      offset: [0, 0],
      scale: [1, 1],
      rotation: 0,
      zIndex: parts.length + 1,
      opacity: 1,
      visible: true,
    };
    updateParts([...parts, newPart]);
    onSelectPart(newId);
  };

  const handleDeletePart = (id: string) => {
    const filtered = parts.filter((p) => p.id !== id);
    updateParts(filtered);
    if (selectedPartId === id && filtered.length > 0) {
      onSelectPart(filtered[0].id);
    }
  };

  const handleMoveLayer = (index: number, direction: 'up' | 'down') => {
    if (direction === 'up' && index === 0) return;
    if (direction === 'down' && index === parts.length - 1) return;

    const targetIdx = direction === 'up' ? index - 1 : index + 1;
    const clone = [...parts];
    const temp = clone[index];
    clone[index] = clone[targetIdx];
    clone[targetIdx] = temp;

    // Reassign zIndexes
    const reindexed = clone.map((p, idx) => ({ ...p, zIndex: idx + 1 }));
    updateParts(reindexed);
  };

  const handleUploadPartImage = (file: File) => {
    if (!selectedPart) return;
    const reader = new FileReader();
    reader.onload = (e) => {
      const dataUrl = e.target?.result as string;
      if (dataUrl) {
        const updated = parts.map((p) => (p.id === selectedPart.id ? { ...p, path: dataUrl } : p));
        updateParts(updated);
      }
    };
    reader.readAsDataURL(file);
  };

  const handleUpdateSelectedPart = (updates: Partial<LayerPartConfig>) => {
    if (!selectedPart) return;
    const updated = parts.map((p) => (p.id === selectedPart.id ? { ...p, ...updates } : p));
    updateParts(updated);
  };

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        background: 'rgba(15, 23, 42, 0.85)',
        border: '1px solid rgba(255, 255, 255, 0.08)',
        borderRadius: 8,
        padding: 12,
        maxHeight: '100%',
        overflowY: 'auto',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Layers size={13} /> LỒNG GHÉP CÁC LỚP ẢNH (LAYER STACK)
        </div>
        <button
          onClick={handleAddPart}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '3px 8px',
            borderRadius: 4,
            background: 'linear-gradient(135deg, #0284c7, #38bdf8)',
            border: 'none',
            color: '#fff',
            fontSize: 9.5,
            fontWeight: 700,
            cursor: 'pointer',
          }}
        >
          <Plus size={11} /> Thêm Lớp Ảnh
        </button>
      </div>

      {/* Layer Stack Reorder List */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4, maxHeight: 180, overflowY: 'auto' }}>
        {parts.length === 0 ? (
          <div style={{ padding: 12, textAlign: 'center', color: '#64748b', fontSize: 10, background: 'rgba(2,6,23,0.5)', borderRadius: 6 }}>
            Chưa có lớp lồng ghép riêng lẻ. Bấm "Thêm Lớp Ảnh" để bắt đầu cắt ghép từng bộ phận (Tóc, Thân, Mặt, Áo, Vũ khí).
          </div>
        ) : (
          parts
            .slice()
            .reverse() // Render top layers at the top of list
            .map((part, revIdx) => {
              const actualIdx = parts.length - 1 - revIdx;
              const isSelected = part.id === selectedPartId;
              const cat = PART_CATEGORIES.find((c) => c.id === part.category);

              return (
                <div
                  key={part.id}
                  onClick={() => onSelectPart(part.id)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '6px 8px',
                    borderRadius: 6,
                    background: isSelected ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255, 255, 255, 0.03)',
                    border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.06)',
                    cursor: 'pointer',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <span style={{ fontSize: 12 }}>{cat?.icon || '🧩'}</span>
                    <span style={{ fontSize: 10.5, fontWeight: isSelected ? 700 : 500, color: isSelected ? '#38bdf8' : '#e2e8f0' }}>
                      {part.name}
                    </span>
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                    {/* Layer Reorder Arrows */}
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        handleMoveLayer(actualIdx, 'down');
                      }}
                      title="Đưa lớp này lên trên (Z-index cao hơn)"
                      style={{ background: 'none', border: 'none', color: '#94a3b8', cursor: 'pointer', padding: 1 }}
                    >
                      <ChevronUp size={13} />
                    </button>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        handleMoveLayer(actualIdx, 'up');
                      }}
                      title="Đưa lớp này xuống dưới (Z-index thấp hơn)"
                      style={{ background: 'none', border: 'none', color: '#94a3b8', cursor: 'pointer', padding: 1 }}
                    >
                      <ChevronDown size={13} />
                    </button>
                    {/* Visibility Toggle */}
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        handleUpdateSelectedPart({ visible: !part.visible });
                      }}
                      style={{ background: 'none', border: 'none', color: part.visible ? '#4ade80' : '#64748b', cursor: 'pointer', padding: 1 }}
                    >
                      {part.visible ? <Eye size={12} /> : <EyeOff size={12} />}
                    </button>
                    {/* Delete */}
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        handleDeletePart(part.id);
                      }}
                      style={{ background: 'none', border: 'none', color: '#ef4444', cursor: 'pointer', padding: 1 }}
                    >
                      <Trash2 size={12} />
                    </button>
                  </div>
                </div>
              );
            })
        )}
      </div>

      {/* Selected Layer Properties Inspector */}
      {selectedPart && (
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: 8,
            background: 'rgba(2, 6, 23, 0.6)',
            padding: 10,
            borderRadius: 6,
            border: '1px solid rgba(255, 255, 255, 0.06)',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 10.5, fontWeight: 700, color: '#4ade80' }}>
              🔧 TINH CHỈNH LỚP: {selectedPart.name}
            </span>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
            <div>
              <label style={{ fontSize: 9, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Tên Lớp:</label>
              <input
                type="text"
                value={selectedPart.name}
                onChange={(e) => handleUpdateSelectedPart({ name: e.target.value })}
                style={{ width: '100%', padding: '3px 6px', fontSize: 10, background: '#090d16', border: '1px solid #334155', color: '#fff', borderRadius: 4 }}
              />
            </div>
            <div>
              <label style={{ fontSize: 9, color: '#94a3b8', display: 'block', marginBottom: 2 }}>Loại Bộ Phận:</label>
              <select
                value={selectedPart.category}
                onChange={(e) => handleUpdateSelectedPart({ category: e.target.value as any })}
                style={{ width: '100%', padding: '3px 4px', fontSize: 10, background: '#090d16', border: '1px solid #334155', color: '#38bdf8', borderRadius: 4 }}
              >
                {PART_CATEGORIES.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.icon} {c.label}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Upload Image for Layer */}
          <label
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 5,
              padding: '6px',
              borderRadius: 4,
              background: selectedPart.path ? 'rgba(34, 197, 94, 0.15)' : 'rgba(56, 189, 248, 0.15)',
              border: selectedPart.path ? '1px solid #22c55e' : '1px solid #38bdf8',
              color: selectedPart.path ? '#4ade80' : '#38bdf8',
              fontSize: 10,
              fontWeight: 700,
              cursor: 'pointer',
              textAlign: 'center',
            }}
          >
            <Upload size={12} /> {selectedPart.path ? 'Đổi Ảnh Cắt Cho Lớp Này' : 'Tải Ảnh Cắt Cho Lớp Này'}
            <input
              type="file"
              accept="image/*"
              style={{ display: 'none' }}
              onChange={(e) => {
                const f = e.target.files?.[0];
                if (f) handleUploadPartImage(f);
                e.target.value = '';
              }}
            />
          </label>

          {/* Position Offset X, Y */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
            <div>
              <span style={{ fontSize: 9, color: '#94a3b8' }}>Dịch X ({selectedPart.offset[0]}px):</span>
              <input
                type="range"
                min="-200"
                max="200"
                value={selectedPart.offset[0]}
                onChange={(e) => handleUpdateSelectedPart({ offset: [parseInt(e.target.value), selectedPart.offset[1]] })}
                style={{ width: '100%', accentColor: '#38bdf8' }}
              />
            </div>
            <div>
              <span style={{ fontSize: 9, color: '#94a3b8' }}>Dịch Y ({selectedPart.offset[1]}px):</span>
              <input
                type="range"
                min="-200"
                max="200"
                value={selectedPart.offset[1]}
                onChange={(e) => handleUpdateSelectedPart({ offset: [selectedPart.offset[0], parseInt(e.target.value)] })}
                style={{ width: '100%', accentColor: '#38bdf8' }}
              />
            </div>
          </div>

          {/* Scale & Rotation */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
            <div>
              <span style={{ fontSize: 9, color: '#94a3b8' }}>Tỉ Lệ Scale ({selectedPart.scale[0].toFixed(2)}x):</span>
              <input
                type="range"
                min="0.2"
                max="3.0"
                step="0.05"
                value={selectedPart.scale[0]}
                onChange={(e) => {
                  const s = parseFloat(e.target.value);
                  handleUpdateSelectedPart({ scale: [s, s] });
                }}
                style={{ width: '100%', accentColor: '#a855f7' }}
              />
            </div>
            <div>
              <span style={{ fontSize: 9, color: '#94a3b8' }}>Xoay Góc ({selectedPart.rotation}°):</span>
              <input
                type="range"
                min="-180"
                max="180"
                value={selectedPart.rotation}
                onChange={(e) => handleUpdateSelectedPart({ rotation: parseInt(e.target.value) })}
                style={{ width: '100%', accentColor: '#eab308' }}
              />
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
