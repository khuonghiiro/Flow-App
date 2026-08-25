import React from 'react';
import { Layers, Plus, Trash2, CheckCircle2, Image as ImageIcon, X } from 'lucide-react';
import { SlicerPartGroupItem, SlicerUploadedImageItem } from './hooks/useSlicerMultiImageGallery';

export interface SlicerLoadedImagesTabsProps {
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

export const SlicerLoadedImagesTabs: React.FC<SlicerLoadedImagesTabsProps> = ({
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
        background: 'linear-gradient(180deg, rgba(15, 23, 42, 0.95) 0%, rgba(10, 15, 30, 0.98) 100%)',
        borderRadius: 8,
        border: '1px solid rgba(56, 189, 248, 0.35)',
        padding: '8px 10px',
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        boxShadow: '0 4px 16px rgba(0,0,0,0.4)',
      }}
    >
      {/* Top Bar: Title, Part Tabs & Actions */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 6, borderBottom: '1px solid rgba(255,255,255,0.08)', paddingBottom: 6 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
          <span style={{ fontSize: 11, fontWeight: 800, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
            <Layers size={13} color="#38bdf8" /> BỘ SƯU TẬP ẢNH ({totalImagesCount} ảnh / {partGroups.length} chi tiết):
          </span>

          {/* Part Tabs */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, flexWrap: 'wrap' }}>
            {partGroups.map((group) => {
              const isActive = (activePartId || (partGroups[0] && partGroups[0].part_id)) === group.part_id;
              return (
                <button
                  key={group.part_id}
                  onClick={() => onSelectPart(group.part_id)}
                  style={{
                    padding: '3px 8px',
                    borderRadius: 5,
                    fontSize: 10.5,
                    fontWeight: 700,
                    cursor: 'pointer',
                    background: isActive ? 'linear-gradient(135deg, #0284c7 0%, #2563eb 100%)' : 'rgba(255, 255, 255, 0.06)',
                    color: isActive ? '#ffffff' : '#cbd5e1',
                    border: isActive ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.12)',
                    display: 'flex',
                    alignItems: 'center',
                    gap: 4,
                    boxShadow: isActive ? '0 0 10px rgba(56, 189, 248, 0.35)' : 'none',
                    transition: 'all 0.15s ease',
                  }}
                >
                  <span>{group.icon}</span>
                  <span>{group.part_name}</span>
                  <span
                    style={{
                      background: isActive ? 'rgba(0, 0, 0, 0.3)' : 'rgba(255, 255, 255, 0.12)',
                      padding: '1px 5px',
                      borderRadius: 10,
                      fontSize: 9.5,
                      fontWeight: 800,
                    }}
                  >
                    {group.images.length}
                  </span>
                </button>
              );
            })}
          </div>
        </div>

        {/* Global Action Buttons */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <button
            onClick={onOpenAddFiles}
            style={{
              height: 24,
              padding: '0 8px',
              fontSize: 10,
              fontWeight: 700,
              borderRadius: 4,
              background: 'rgba(34, 197, 94, 0.15)',
              color: '#4ade80',
              border: '1px solid rgba(34, 197, 94, 0.4)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 3,
            }}
            title="Tải thêm ảnh vào bộ sưu tập"
          >
            <Plus size={12} /> Thêm ảnh
          </button>
          <button
            onClick={onClearAll}
            style={{
              height: 24,
              padding: '0 6px',
              fontSize: 10,
              fontWeight: 600,
              borderRadius: 4,
              background: 'rgba(239, 68, 68, 0.12)',
              color: '#f87171',
              border: '1px solid rgba(239, 68, 68, 0.3)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 3,
            }}
            title="Xóa tất cả ảnh đã tải"
          >
            <Trash2 size={11} /> Xóa hết
          </button>
        </div>
      </div>

      {/* Angles & Variants Filmstrip / Grid for Active Part */}
      {currentGroup && (
        <div
          style={{
            display: 'flex',
            alignItems: 'stretch',
            gap: 8,
            overflowX: 'auto',
            paddingBottom: 4,
          }}
        >
          {currentGroup.images.map((item) => {
            const isSelected = item.id === activeImageId;
            const angleName = item.metadata?.angle_name || 'Góc tự do';
            const variantIdx = item.metadata?.variant_index;

            return (
              <div
                key={item.id}
                onClick={() => onSelectImage(item.id)}
                style={{
                  minWidth: 130,
                  maxWidth: 160,
                  background: isSelected ? 'rgba(2, 132, 199, 0.2)' : 'rgba(30, 41, 59, 0.5)',
                  border: isSelected ? '1.5px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.12)',
                  borderRadius: 6,
                  padding: 6,
                  cursor: 'pointer',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 4,
                  position: 'relative',
                  boxShadow: isSelected ? '0 0 12px rgba(56, 189, 248, 0.3)' : 'none',
                  transition: 'all 0.15s ease',
                }}
              >
                {/* Delete button on card */}
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    onRemoveImage(item.id);
                  }}
                  style={{
                    position: 'absolute',
                    top: 3,
                    right: 3,
                    width: 18,
                    height: 18,
                    borderRadius: 9,
                    background: 'rgba(0, 0, 0, 0.6)',
                    color: '#94a3b8',
                    border: 'none',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    zIndex: 2,
                  }}
                  title="Xóa ảnh này"
                >
                  <X size={11} />
                </button>

                {/* Thumbnail Image Container with native ratio display */}
                <div
                  style={{
                    width: '100%',
                    height: 75,
                    background: '#090d16',
                    borderRadius: 4,
                    overflow: 'hidden',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    border: '1px solid rgba(255, 255, 255, 0.08)',
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
                        fontSize: 8.5,
                        fontWeight: 800,
                        padding: '1px 4px',
                        borderRadius: 3,
                        display: 'flex',
                        alignItems: 'center',
                        gap: 2,
                      }}
                    >
                      <CheckCircle2 size={9} /> ĐANG XEM
                    </div>
                  )}
                </div>

                {/* Angle & Metadata Badges */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2 }}>
                    <span
                      style={{
                        fontSize: 9.5,
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
                          fontSize: 8.5,
                          fontWeight: 800,
                          background: 'rgba(245, 158, 11, 0.25)',
                          color: '#fbbf24',
                          border: '1px solid rgba(245, 158, 11, 0.4)',
                          padding: '0 4px',
                          borderRadius: 3,
                        }}
                      >
                        #{variantIdx}
                      </span>
                    )}
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: 8.5, color: '#94a3b8' }}>
                    <span style={{ fontFamily: 'monospace', color: '#34d399', fontWeight: 600 }}>
                      {item.aspectRatioLabel}
                    </span>
                    <span>{item.width}×{item.height}</span>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
