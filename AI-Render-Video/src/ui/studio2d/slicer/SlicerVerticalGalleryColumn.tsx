import React from 'react';
import { Layers, Plus, Trash2, CheckCircle2, X } from 'lucide-react';
import { SlicerPartGroupItem } from './hooks/useSlicerMultiImageGallery';

export interface SlicerVerticalGalleryColumnProps {
  partGroups: SlicerPartGroupItem[];
  activePartId: string | null;
  activeImageId: string | null;
  onSelectPart: (partId: string) => void;
  onSelectImage: (imageId: string) => void;
  onRemoveImage: (imageId: string) => void;
  onClearAll: () => void;
  onOpenAddFiles: () => void;
  totalImagesCount: number;
}

export const SlicerVerticalGalleryColumn: React.FC<SlicerVerticalGalleryColumnProps> = ({
  partGroups,
  activePartId,
  activeImageId,
  onSelectPart,
  onSelectImage,
  onRemoveImage,
  onClearAll,
  onOpenAddFiles,
  totalImagesCount,
}) => {
  if (totalImagesCount === 0) return null;

  const currentGroup = partGroups.find((g) => g.part_id === activePartId) || partGroups[0];

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        background: 'rgba(15, 23, 42, 0.85)',
        backdropFilter: 'blur(8px)',
        borderRadius: 8,
        border: '1px solid rgba(56, 189, 248, 0.25)',
        padding: '8px 6px',
        minHeight: 0,
        height: '100%',
        overflow: 'hidden',
        boxSizing: 'border-box',
        fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
      }}
    >
      {/* Header: Title + Actions */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          paddingBottom: 6,
          borderBottom: '1px solid rgba(255,255,255,0.08)',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 4, minWidth: 0 }}>
          <Layers size={13} color="#38bdf8" />
          <span
            style={{
              fontSize: 10.5,
              fontWeight: 800,
              color: '#38bdf8',
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
            }}
          >
            ẢNH ({totalImagesCount})
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 3 }}>
          <button
            onClick={onOpenAddFiles}
            style={{
              height: 22,
              padding: '0 5px',
              fontSize: 9.5,
              fontWeight: 700,
              borderRadius: 4,
              background: 'rgba(34, 197, 94, 0.2)',
              color: '#4ade80',
              border: '1px solid rgba(34, 197, 94, 0.4)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 2,
            }}
            title="Thêm ảnh"
          >
            <Plus size={11} /> Thêm
          </button>
          <button
            onClick={onClearAll}
            style={{
              height: 22,
              width: 22,
              borderRadius: 4,
              background: 'rgba(239, 68, 68, 0.15)',
              color: '#f87171',
              border: '1px solid rgba(239, 68, 68, 0.3)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
            title="Xóa tất cả ảnh"
          >
            <Trash2 size={11} />
          </button>
        </div>
      </div>

      {/* Part Filter Chips (if multiple part groups exist) */}
      {partGroups.length > 1 && (
        <div
          style={{
            display: 'flex',
            gap: 3,
            overflowX: 'auto',
            paddingBottom: 4,
            borderBottom: '1px solid rgba(255,255,255,0.05)',
          }}
        >
          {partGroups.map((group) => {
            const isActive = (activePartId || partGroups[0]?.part_id) === group.part_id;
            return (
              <button
                key={group.part_id}
                onClick={() => onSelectPart(group.part_id)}
                style={{
                  padding: '2px 5px',
                  borderRadius: 4,
                  fontSize: 9,
                  fontWeight: 700,
                  whiteSpace: 'nowrap',
                  cursor: 'pointer',
                  background: isActive ? '#0284c7' : 'rgba(255, 255, 255, 0.06)',
                  color: isActive ? '#ffffff' : '#94a3b8',
                  border: isActive ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.08)',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 2,
                }}
              >
                <span>{group.icon}</span>
                <span>{group.part_name}</span>
                <span style={{ fontSize: 8, opacity: 0.8 }}>({group.images.length})</span>
              </button>
            );
          })}
        </div>
      )}

      {/* Vertical Scroll List of Image Thumbnails */}
      <div
        style={{
          flex: 1,
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
          overflowY: 'auto',
          minHeight: 0,
          paddingRight: 2,
        }}
      >
        {currentGroup &&
          currentGroup.images.map((item) => {
            const isSelected = item.id === activeImageId;
            const angleName = item.metadata?.angle_name || 'Góc tự do';
            const variantIdx = item.metadata?.variant_index;

            return (
              <div
                key={item.id}
                onClick={() => onSelectImage(item.id)}
                style={{
                  background: isSelected ? 'rgba(2, 132, 199, 0.22)' : 'rgba(30, 41, 59, 0.5)',
                  border: isSelected ? '1.5px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.1)',
                  borderRadius: 6,
                  padding: 4,
                  cursor: 'pointer',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 3,
                  position: 'relative',
                  boxShadow: isSelected ? '0 0 10px rgba(56, 189, 248, 0.35)' : 'none',
                  transition: 'all 0.12s ease',
                  flexShrink: 0,
                }}
              >
                {/* Delete button */}
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    onRemoveImage(item.id);
                  }}
                  style={{
                    position: 'absolute',
                    top: 2,
                    right: 2,
                    width: 16,
                    height: 16,
                    borderRadius: 8,
                    background: 'rgba(0, 0, 0, 0.65)',
                    color: '#94a3b8',
                    border: 'none',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    zIndex: 3,
                  }}
                  title="Xóa ảnh này"
                >
                  <X size={10} />
                </button>

                {/* Thumbnail Image Container */}
                <div
                  style={{
                    width: '100%',
                    height: 60,
                    background: '#090d16',
                    borderRadius: 4,
                    overflow: 'hidden',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    border: '1px solid rgba(255, 255, 255, 0.06)',
                    position: 'relative',
                  }}
                >
                  <img
                    src={item.url}
                    alt={item.name}
                    style={{
                      maxWidth: '100%',
                      maxHeight: '100%',
                      objectFit: 'contain',
                      imageRendering: 'crisp-edges',
                    }}
                  />
                  {isSelected && (
                    <div
                      style={{
                        position: 'absolute',
                        bottom: 2,
                        left: 2,
                        background: '#0284c7',
                        color: '#fff',
                        fontSize: 8,
                        fontWeight: 800,
                        padding: '1px 3px',
                        borderRadius: 2,
                        display: 'flex',
                        alignItems: 'center',
                        gap: 2,
                      }}
                    >
                      <CheckCircle2 size={8} /> XEM
                    </div>
                  )}
                </div>

                {/* Metadata Badges */}
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2 }}>
                  <span
                    style={{
                      fontSize: 9,
                      fontWeight: 700,
                      color: isSelected ? '#38bdf8' : '#e2e8f0',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                      whiteSpace: 'nowrap',
                    }}
                    title={angleName}
                  >
                    📐 {angleName}
                  </span>
                  {variantIdx && (
                    <span
                      style={{
                        fontSize: 8,
                        fontWeight: 800,
                        background: 'rgba(245, 158, 11, 0.25)',
                        color: '#fbbf24',
                        padding: '0 3px',
                        borderRadius: 2,
                      }}
                    >
                      #{variantIdx}
                    </span>
                  )}
                </div>

                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: 8, color: '#64748b' }}>
                  <span style={{ fontFamily: 'monospace', color: '#34d399' }}>{item.aspectRatioLabel}</span>
                  <span>{item.width}×{item.height}</span>
                </div>
              </div>
            );
          })}
      </div>
    </div>
  );
};
