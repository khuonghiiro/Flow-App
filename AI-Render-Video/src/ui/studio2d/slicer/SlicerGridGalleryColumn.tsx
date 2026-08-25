import React, { useState, useEffect, useMemo, useRef } from 'react';
import {
  Layers,
  Plus,
  Trash2,
  ChevronLeft,
  ChevronRight,
  CheckCircle2,
  X,
  Sparkles,
  Info,
} from 'lucide-react';
import { SlicerPartGroupItem, SlicerUploadedImageItem } from './hooks/useSlicerMultiImageGallery';
import { ChromaProcessOptions, processCellChromaAndDespeckle } from '../../../core/utils/ChromaDespeckleProcessor';

export interface SlicerGridGalleryColumnProps {
  partGroups: SlicerPartGroupItem[];
  activePartId: string | null;
  activeImageId: string | null;
  onSelectPart: (partId: string) => void;
  onSelectImage: (imageId: string) => void;
  onRemoveImage: (imageId: string) => void;
  onClearAll: () => void;
  onOpenAddFiles: () => void;
  totalImagesCount: number;
  previewDisplayMode?: 'original' | 'transparent';
  chromaOpts?: ChromaProcessOptions;
}

const PAGE_SIZE = 20; // Maximum 20 images per grid page (4 columns x 5 rows)

export const SlicerGridGalleryColumn: React.FC<SlicerGridGalleryColumnProps> = ({
  partGroups,
  activePartId,
  activeImageId,
  onSelectPart,
  onSelectImage,
  onRemoveImage,
  onClearAll,
  onOpenAddFiles,
  totalImagesCount,
  previewDisplayMode = 'original',
  chromaOpts,
}) => {
  const [currentPage, setCurrentPage] = useState<number>(0);
  const [hoveredItem, setHoveredItem] = useState<SlicerUploadedImageItem | null>(null);
  const [processedThumbs, setProcessedThumbs] = useState<Map<string, string>>(new Map());

  // Current active part group
  const currentGroup = partGroups.find((g) => g.part_id === activePartId) || partGroups[0];
  const allImages = currentGroup ? currentGroup.images : [];
  const totalPages = Math.max(1, Math.ceil(allImages.length / PAGE_SIZE));

  // Reset or clamp current page if images change
  useEffect(() => {
    if (currentPage >= totalPages) {
      setCurrentPage(Math.max(0, totalPages - 1));
    }
  }, [totalPages, currentPage]);

  // Sliced list of images for current page (Max 20)
  const pageImages = useMemo(() => {
    const start = currentPage * PAGE_SIZE;
    return allImages.slice(start, start + PAGE_SIZE);
  }, [allImages, currentPage]);

  // On-demand Chroma processing ONLY for the visible 20 items on the active page
  useEffect(() => {
    if (previewDisplayMode !== 'transparent' || !chromaOpts?.keyColorType) {
      setProcessedThumbs(new Map());
      return;
    }

    let isMounted = true;
    const processActivePage = async () => {
      const results = new Map<string, string>();
      for (const item of pageImages) {
        if (!isMounted) break;
        try {
          const img = new Image();
          img.crossOrigin = 'anonymous';
          await new Promise<void>((resolve, reject) => {
            img.onload = () => resolve();
            img.onerror = () => reject();
            img.src = item.url;
          });

          const w = Math.min(80, img.naturalWidth || 80);
          const h = Math.round((w / (img.naturalWidth || 1)) * (img.naturalHeight || 1));
          const canvas = document.createElement('canvas');
          canvas.width = w;
          canvas.height = h;
          const ctx = canvas.getContext('2d', { willReadFrequently: true });
          if (ctx) {
            ctx.drawImage(img, 0, 0, w, h);
            processCellChromaAndDespeckle(ctx, w, h, chromaOpts);
            results.set(item.id, canvas.toDataURL('image/png'));
          }
        } catch {
          // Fallback to original url
          results.set(item.id, item.url);
        }
      }
      if (isMounted) setProcessedThumbs(results);
    };

    processActivePage();
    return () => {
      isMounted = false;
    };
  }, [pageImages, previewDisplayMode, chromaOpts]);

  if (totalImagesCount === 0) return null;

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        background: 'linear-gradient(135deg, rgba(15, 23, 42, 0.94) 0%, rgba(20, 30, 50, 0.96) 100%)',
        backdropFilter: 'blur(12px)',
        borderRadius: 10,
        border: '1px solid rgba(56, 189, 248, 0.28)',
        boxShadow: '0 8px 32px rgba(0, 0, 0, 0.5), 0 0 16px rgba(56, 189, 248, 0.08)',
        padding: '8px 8px',
        width: 228,
        minWidth: 228,
        maxWidth: 228,
        height: '100%',
        minHeight: 0,
        overflow: 'hidden',
        boxSizing: 'border-box',
        fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
      }}
    >
      {/* 1. Header: Title + Action buttons */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 4, paddingBottom: 4, borderBottom: '1px solid rgba(255,255,255,0.08)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 4, minWidth: 0 }}>
          <Layers size={13} color="#38bdf8" />
          <span style={{ fontSize: 10.5, fontWeight: 800, color: '#38bdf8', whiteSpace: 'nowrap' }}>
            GRID ẢNH ({allImages.length})
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 3 }}>
          <button
            onClick={onOpenAddFiles}
            style={{
              height: 20,
              padding: '0 6px',
              fontSize: 9,
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
            title="Thêm ảnh vào kho"
          >
            <Plus size={10} /> Thêm
          </button>
          <button
            onClick={onClearAll}
            style={{
              height: 20,
              width: 20,
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
            <Trash2 size={10} />
          </button>
        </div>
      </div>

      {/* 2. Group filter chips (if multiple part groups exist) */}
      {partGroups.length > 1 && (
        <div style={{ display: 'flex', gap: 3, overflowX: 'auto', paddingBottom: 3, borderBottom: '1px solid rgba(255,255,255,0.05)' }}>
          {partGroups.map((group) => {
            const isActive = (activePartId || partGroups[0]?.part_id) === group.part_id;
            return (
              <button
                key={group.part_id}
                onClick={() => {
                  onSelectPart(group.part_id);
                  setCurrentPage(0);
                }}
                style={{
                  padding: '2px 5px',
                  borderRadius: 4,
                  fontSize: 8.5,
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
                <span style={{ fontSize: 7.5, opacity: 0.8 }}>({group.images.length})</span>
              </button>
            );
          })}
        </div>
      )}

      {/* 3. Slider / Pagination Navigation Bar */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          background: 'rgba(2, 132, 199, 0.12)',
          border: '1px solid rgba(56, 189, 248, 0.25)',
          borderRadius: 6,
          padding: '2px 4px',
        }}
      >
        <button
          onClick={() => setCurrentPage((p) => Math.max(0, p - 1))}
          disabled={currentPage === 0}
          style={{
            width: 22,
            height: 20,
            borderRadius: 4,
            border: 'none',
            background: currentPage === 0 ? 'transparent' : 'rgba(56, 189, 248, 0.2)',
            color: currentPage === 0 ? '#475569' : '#38bdf8',
            cursor: currentPage === 0 ? 'not-allowed' : 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
          title="Trang trước (20 ảnh)"
        >
          <ChevronLeft size={13} />
        </button>

        <span style={{ fontSize: 9.5, fontWeight: 700, color: '#e2e8f0' }}>
          Trang <span style={{ color: '#38bdf8' }}>{currentPage + 1}</span> / {totalPages}{' '}
          <span style={{ fontSize: 8.5, color: '#94a3b8' }}>
            ({currentPage * PAGE_SIZE + 1} - {Math.min((currentPage + 1) * PAGE_SIZE, allImages.length)})
          </span>
        </span>

        <button
          onClick={() => setCurrentPage((p) => Math.min(totalPages - 1, p + 1))}
          disabled={currentPage >= totalPages - 1}
          style={{
            width: 22,
            height: 20,
            borderRadius: 4,
            border: 'none',
            background: currentPage >= totalPages - 1 ? 'transparent' : 'rgba(56, 189, 248, 0.2)',
            color: currentPage >= totalPages - 1 ? '#475569' : '#38bdf8',
            cursor: currentPage >= totalPages - 1 ? 'not-allowed' : 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
          title="Trang tiếp theo (20 ảnh)"
        >
          <ChevronRight size={13} />
        </button>
      </div>

      {/* 4. Fixed 20-Slot Grid Container (4 columns x 5 rows) */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(4, 1fr)',
          gridTemplateRows: 'repeat(5, 48px)',
          gap: 5,
          width: '100%',
          flexShrink: 0,
        }}
      >
        {Array.from({ length: PAGE_SIZE }).map((_, idx) => {
          const item = pageImages[idx];

          if (!item) {
            // Empty placeholder slot
            return (
              <div
                key={`empty_${idx}`}
                onClick={onOpenAddFiles}
                style={{
                  height: 48,
                  borderRadius: 6,
                  border: '1px dashed rgba(255, 255, 255, 0.1)',
                  background: 'rgba(0, 0, 0, 0.2)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: 'rgba(255, 255, 255, 0.15)',
                  fontSize: 10,
                  cursor: 'pointer',
                  transition: 'all 0.1s ease',
                }}
                title="Bấm để tải thêm ảnh"
              >
                +
              </div>
            );
          }

          const isSelected = item.id === activeImageId;
          const displayUrl = processedThumbs.get(item.id) || item.url;
          const angleName = item.metadata?.angle_name || 'Góc tự do';

          return (
            <div
              key={item.id}
              onClick={() => onSelectImage(item.id)}
              onMouseEnter={() => setHoveredItem(item)}
              onMouseLeave={() => setHoveredItem(null)}
              style={{
                height: 48,
                background: isSelected ? 'rgba(2, 132, 199, 0.28)' : 'rgba(15, 23, 42, 0.7)',
                border: isSelected ? '1.5px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.12)',
                borderRadius: 6,
                padding: 2,
                cursor: 'pointer',
                position: 'relative',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                overflow: 'hidden',
                boxShadow: isSelected ? '0 0 10px rgba(56, 189, 248, 0.4)' : 'none',
                transition: 'all 0.12s ease',
              }}
              title={`${item.name} (${angleName})`}
            >
              {/* Image Thumbnail with object-fit: contain */}
              <img
                src={displayUrl}
                alt={item.name}
                style={{
                  maxWidth: '100%',
                  maxHeight: '100%',
                  objectFit: 'contain',
                  imageRendering: 'crisp-edges',
                  borderRadius: 3,
                }}
              />

              {/* Selected indicator badge */}
              {isSelected && (
                <div
                  style={{
                    position: 'absolute',
                    top: 2,
                    left: 2,
                    background: '#0284c7',
                    borderRadius: 3,
                    padding: 1,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  <CheckCircle2 size={9} color="#ffffff" />
                </div>
              )}

              {/* Quick delete button */}
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onRemoveImage(item.id);
                }}
                style={{
                  position: 'absolute',
                  top: 2,
                  right: 2,
                  width: 14,
                  height: 14,
                  borderRadius: 7,
                  background: 'rgba(0, 0, 0, 0.75)',
                  color: '#f87171',
                  border: 'none',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  opacity: isSelected ? 1 : 0.6,
                }}
                title="Xóa ảnh"
              >
                <X size={8} />
              </button>
            </div>
          );
        })}
      </div>

      {/* 5. Hover Info Bar / Active Image Metadata */}
      <div
        style={{
          marginTop: 'auto',
          padding: '4px 6px',
          background: 'rgba(0, 0, 0, 0.4)',
          borderRadius: 6,
          border: '1px solid rgba(255, 255, 255, 0.06)',
          fontSize: 8.5,
          color: '#94a3b8',
          display: 'flex',
          flexDirection: 'column',
          gap: 2,
        }}
      >
        {hoveredItem ? (
          <>
            <div style={{ fontWeight: 700, color: '#38bdf8', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
              📐 {hoveredItem.metadata?.angle_name || hoveredItem.name}
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', color: '#64748b' }}>
              <span style={{ color: '#34d399' }}>{hoveredItem.aspectRatioLabel}</span>
              <span>{hoveredItem.width}×{hoveredItem.height}px</span>
            </div>
          </>
        ) : (
          <div style={{ color: '#64748b', fontStyle: 'italic', textAlign: 'center' }}>
            Rê chuột lên ảnh để xem góc & kích thước
          </div>
        )}
      </div>
    </div>
  );
};
