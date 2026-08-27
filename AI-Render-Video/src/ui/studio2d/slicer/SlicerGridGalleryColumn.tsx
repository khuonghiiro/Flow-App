import React, { useState, useEffect, useMemo, useCallback } from 'react';
import {
  Layers,
  Plus,
  Trash2,
  ChevronLeft,
  ChevronRight,
  CheckCircle2,
  X,
  Sparkles,
  CheckSquare,
  Square,
  RefreshCw,
  Download,
  FolderDown,
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
  onBatchSeparateImages?: (imageIds: string[]) => Promise<void>;
  isBatchProcessing?: boolean;
  onCheckedIdsChange?: (ids: Set<string>) => void;
  checkedImageIds?: Set<string>;
}

const PAGE_SIZE = 20; // 20 images per grid page (4 columns x 5 rows)

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
  onBatchSeparateImages,
  isBatchProcessing = false,
  onCheckedIdsChange,
  checkedImageIds: externalCheckedIds,
}) => {
  const [currentPage, setCurrentPage] = useState<number>(0);
  const [hoveredItem, setHoveredItem] = useState<SlicerUploadedImageItem | null>(null);
  const [internalSelectedIds, setInternalSelectedIds] = useState<Set<string>>(new Set());
  const selectedIds = externalCheckedIds ?? internalSelectedIds;
  const [isSavingBatch, setIsSavingBatch] = useState<boolean>(false);
  const [saveMsg, setSaveMsg] = useState<string | null>(null);

  const setSelectedIds = useCallback((updater: Set<string> | ((prev: Set<string>) => Set<string>)) => {
    const doUpdate = (prev: Set<string>) => {
      const next = typeof updater === 'function' ? updater(prev) : updater;
      onCheckedIdsChange?.(next);
      return next;
    };
    setInternalSelectedIds(doUpdate);
  }, [onCheckedIdsChange]);

  // Flatten all images from all groups (no categorization)
  const allImages = useMemo(() => {
    const seen = new Set<string>();
    const flat: SlicerUploadedImageItem[] = [];
    for (const g of partGroups) {
      for (const img of g.images) {
        if (!seen.has(img.id)) {
          seen.add(img.id);
          flat.push(img);
        }
      }
    }
    return flat;
  }, [partGroups]);
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

  const pageImageIds = useMemo(() => pageImages.map((it) => it.id), [pageImages]);

  // Are all images on this page selected?
  const isAllPageSelected = useMemo(() => {
    if (pageImageIds.length === 0) return false;
    return pageImageIds.every((id) => selectedIds.has(id));
  }, [pageImageIds, selectedIds]);

  const handleToggleSelectAllPage = () => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (isAllPageSelected) {
        pageImageIds.forEach((id) => next.delete(id));
      } else {
        pageImageIds.forEach((id) => next.add(id));
      }
      return next;
    });
  };

  const handleToggleSelectItem = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  /** Batch save checked images to a folder via File System Access API or fallback download */
  const handleBatchSaveImages = useCallback(async () => {
    const targetIds = selectedIds.size > 0 ? Array.from(selectedIds) : allImages.map((i) => i.id);
    if (targetIds.length === 0) return;

    const imagesToSave = allImages.filter((img) => targetIds.includes(img.id));
    if (imagesToSave.length === 0) return;

    setIsSavingBatch(true);
    setSaveMsg(null);

    try {
      // Try File System Access API (Chrome/Edge)
      if ('showDirectoryPicker' in window) {
        const dirHandle = await (window as any).showDirectoryPicker({ mode: 'readwrite' });
        let saved = 0;
        for (const img of imagesToSave) {
          const dataUrl = img.transparentUrl || img.url;
          if (!dataUrl) continue;
          const blob = await (await fetch(dataUrl)).blob();
          const ext = dataUrl.startsWith('data:image/png') ? '.png' : '.png';
          const safeName = (img.name || `image_${img.id}`).replace(/\.[^.]+$/, '') + ext;
          const fileHandle = await dirHandle.getFileHandle(safeName, { create: true });
          const writable = await fileHandle.createWritable();
          await writable.write(blob);
          await writable.close();
          saved++;
        }
        setSaveMsg(`✓ Đã lưu ${saved} ảnh vào thư mục!`);
        setTimeout(() => setSaveMsg(null), 3500);
      } else {
        // Fallback: download each file individually
        for (const img of imagesToSave) {
          const dataUrl = img.transparentUrl || img.url;
          if (!dataUrl) continue;
          const link = document.createElement('a');
          link.href = dataUrl;
          link.download = (img.name || `image_${img.id}`).replace(/\.[^.]+$/, '') + '.png';
          link.click();
          await new Promise((r) => setTimeout(r, 200)); // throttle downloads
        }
        setSaveMsg(`✓ Đã tải ${imagesToSave.length} ảnh!`);
        setTimeout(() => setSaveMsg(null), 3500);
      }
    } catch (err: any) {
      if (err?.name !== 'AbortError') {
        console.error('[BatchSave] Error:', err);
        setSaveMsg('❌ Lưu thất bại!');
        setTimeout(() => setSaveMsg(null), 3000);
      }
    } finally {
      setIsSavingBatch(false);
    }
  }, [selectedIds, allImages]);

  if (totalImagesCount === 0) return null;

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        background: 'linear-gradient(135deg, rgba(15, 23, 42, 0.95) 0%, rgba(20, 30, 50, 0.97) 100%)',
        backdropFilter: 'blur(16px)',
        borderRadius: 10,
        border: '1.5px solid rgba(56, 189, 248, 0.3)',
        boxShadow: '0 8px 32px rgba(0, 0, 0, 0.6), 0 0 20px rgba(56, 189, 248, 0.1)',
        padding: '8px 10px',
        width: 310,
        minWidth: 310,
        maxWidth: 310,
        height: '100%',
        minHeight: 0,
        overflow: 'hidden',
        boxSizing: 'border-box',
        fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
      }}
    >
      {/* 1. Header: Title + Action buttons */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 4, paddingBottom: 4, borderBottom: '1px solid rgba(255,255,255,0.08)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 5, minWidth: 0 }}>
          <Layers size={14} color="#38bdf8" />
          <span style={{ fontSize: 11, fontWeight: 800, color: '#38bdf8', whiteSpace: 'nowrap' }}>
            GRID ẢNH ({allImages.length})
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          <button
            onClick={onOpenAddFiles}
            style={{
              height: 22,
              padding: '0 8px',
              fontSize: 9.5,
              fontWeight: 700,
              borderRadius: 4,
              background: 'rgba(34, 197, 94, 0.2)',
              color: '#4ade80',
              border: '1px solid rgba(34, 197, 94, 0.4)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 3,
            }}
            title="Thêm ảnh vào kho"
          >
            <Plus size={11} /> Thêm ảnh
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

      {/* Save Toast */}
      {saveMsg && (
        <div style={{
          padding: '4px 10px',
          borderRadius: 5,
          background: saveMsg.startsWith('✓')
            ? 'linear-gradient(90deg, rgba(5, 150, 105, 0.3), rgba(16, 185, 129, 0.2))'
            : 'rgba(239, 68, 68, 0.2)',
          border: `1px solid ${saveMsg.startsWith('✓') ? 'rgba(16, 185, 129, 0.4)' : 'rgba(239, 68, 68, 0.4)'}`,
          color: saveMsg.startsWith('✓') ? '#34d399' : '#f87171',
          fontSize: 9.5,
          fontWeight: 700,
          textAlign: 'center' as const,
        }}>
          {saveMsg}
        </div>
      )}

      {/* Group filter tabs removed — showing all images flat */}

      {/* 3. Slider / Pagination Bar */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          background: 'rgba(2, 132, 199, 0.12)',
          border: '1px solid rgba(56, 189, 248, 0.25)',
          borderRadius: 6,
          padding: '3px 6px',
        }}
      >
        <button
          onClick={() => setCurrentPage((p) => Math.max(0, p - 1))}
          disabled={currentPage === 0}
          style={{
            width: 24,
            height: 22,
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
          <ChevronLeft size={14} />
        </button>

        <span style={{ fontSize: 10, fontWeight: 700, color: '#e2e8f0' }}>
          Trang <span style={{ color: '#38bdf8' }}>{currentPage + 1}</span> / {totalPages}{' '}
          <span style={{ fontSize: 9, color: '#94a3b8' }}>
            ({currentPage * PAGE_SIZE + 1} - {Math.min((currentPage + 1) * PAGE_SIZE, allImages.length)})
          </span>
        </span>

        <button
          onClick={() => setCurrentPage((p) => Math.min(totalPages - 1, p + 1))}
          disabled={currentPage >= totalPages - 1}
          style={{
            width: 24,
            height: 22,
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
          <ChevronRight size={14} />
        </button>
      </div>

      {/* 4. Batch Select & Background Removal Actions */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          background: 'rgba(0, 0, 0, 0.3)',
          borderRadius: 6,
          padding: '4px 6px',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          gap: 6,
        }}
      >
        <button
          onClick={handleToggleSelectAllPage}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 5,
            background: 'transparent',
            border: 'none',
            color: isAllPageSelected ? '#38bdf8' : '#cbd5e1',
            fontSize: 9.5,
            fontWeight: 700,
            cursor: 'pointer',
            padding: 0,
          }}
          title={isAllPageSelected ? 'Bỏ chọn tất cả' : 'Chọn tất cả ảnh trên trang này'}
        >
          {isAllPageSelected ? <CheckSquare size={13} color="#38bdf8" /> : <Square size={13} color="#64748b" />}
          <span>Chọn trang ({pageImages.length})</span>
        </button>

        {onBatchSeparateImages && (
          <button
            onClick={async () => {
              const targetIds =
                selectedIds.size > 0
                  ? Array.from(selectedIds)
                  : activeImageId
                  ? [activeImageId]
                  : pageImages[0]
                  ? [pageImages[0].id]
                  : [];
              if (targetIds.length > 0) {
                await onBatchSeparateImages(targetIds);
              }
            }}
            disabled={isBatchProcessing || pageImages.length === 0}
            style={{
              padding: '3px 8px',
              fontSize: 9.5,
              fontWeight: 700,
              borderRadius: 5,
              background: isBatchProcessing
                ? 'rgba(255,255,255,0.1)'
                : 'linear-gradient(135deg, #0284c7 0%, #7c3aed 100%)',
              color: '#ffffff',
              border: '1px solid rgba(255, 255, 255, 0.2)',
              cursor: isBatchProcessing ? 'not-allowed' : 'pointer',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 4,
              boxShadow: '0 2px 8px rgba(124, 58, 237, 0.35)',
            }}
            title={
              selectedIds.size > 0
                ? `Tách nền ${selectedIds.size} ảnh đã tích chọn`
                : 'Tách nền ảnh đang hiển thị'
            }
          >
            {isBatchProcessing ? (
              <>
                <RefreshCw size={10} className="animate-spin" /> Đang tách...
              </>
            ) : selectedIds.size > 0 ? (
              <>
                <Sparkles size={10} /> Tách nền ({selectedIds.size} đã chọn)
              </>
            ) : (
              <>
                <Sparkles size={10} /> Tách ảnh đang xem
              </>
            )}
          </button>
        )}

        {/* Batch Save Button */}
        <button
          onClick={handleBatchSaveImages}
          disabled={isSavingBatch || allImages.length === 0}
          style={{
            padding: '3px 7px',
            fontSize: 9.5,
            fontWeight: 700,
            borderRadius: 5,
            background: isSavingBatch
              ? 'rgba(255,255,255,0.1)'
              : 'linear-gradient(135deg, #059669 0%, #10b981 100%)',
            color: '#ffffff',
            border: '1px solid rgba(255, 255, 255, 0.2)',
            cursor: isSavingBatch ? 'not-allowed' : 'pointer',
            display: 'inline-flex',
            alignItems: 'center',
            gap: 4,
            boxShadow: '0 2px 8px rgba(5, 150, 105, 0.35)',
          }}
          title={
            selectedIds.size > 0
              ? `Lưu ${selectedIds.size} ảnh đã chọn vào thư mục`
              : `Lưu tất cả ${allImages.length} ảnh vào thư mục`
          }
        >
          {isSavingBatch ? (
            <>
              <RefreshCw size={10} className="animate-spin" /> Đang lưu...
            </>
          ) : selectedIds.size > 0 ? (
            <>
              <FolderDown size={10} /> Lưu ({selectedIds.size})
            </>
          ) : (
            <>
              <FolderDown size={10} /> Lưu tất cả
            </>
          )}
        </button>
      </div>

      {/* 5. Fixed 20-Slot Grid Container (4 columns x 5 rows, 66px x 62px per cell) */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(4, 1fr)',
          gridTemplateRows: 'repeat(5, 62px)',
          gap: 6,
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
                  height: 62,
                  borderRadius: 6,
                  border: '1.5px dashed rgba(255, 255, 255, 0.12)',
                  background: 'rgba(0, 0, 0, 0.25)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: 'rgba(255, 255, 255, 0.2)',
                  fontSize: 14,
                  fontWeight: 600,
                  cursor: 'pointer',
                  transition: 'all 0.15s ease',
                }}
                title="Bấm để tải thêm ảnh"
              >
                +
              </div>
            );
          }

          const isSelected = item.id === activeImageId;
          const isChecked = selectedIds.has(item.id);
          const isSeparated = Boolean(item.isTransparentSeparated && item.transparentUrl);
          const displayUrl = isSeparated ? item.transparentUrl! : (item.originalUrl || item.url);
          const angleName = item.metadata?.angle_name || 'Góc tự do';

          return (
            <div
              key={item.id}
              onClick={(e) => {
                if (e.ctrlKey || e.metaKey) {
                  handleToggleSelectItem(item.id, e);
                } else {
                  onSelectImage(item.id);
                }
              }}
              onMouseEnter={() => setHoveredItem(item)}
              onMouseLeave={() => setHoveredItem(null)}
              style={{
                height: 62,
                background: isSelected
                  ? 'rgba(2, 132, 199, 0.3)'
                  : isSeparated
                  ? 'repeating-conic-gradient(#1e293b 0% 25%, #0f172a 0% 50%) 50% / 10px 10px'
                  : 'rgba(15, 23, 42, 0.8)',
                border: isSelected
                  ? '2px solid #38bdf8'
                  : isChecked
                  ? '1.5px solid #a855f7'
                  : isSeparated
                  ? '1px solid rgba(74, 222, 128, 0.35)'
                  : '1px solid rgba(255, 255, 255, 0.15)',
                borderRadius: 6,
                padding: 3,
                cursor: 'pointer',
                position: 'relative',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                overflow: 'hidden',
                boxShadow: isSelected ? '0 0 12px rgba(56, 189, 248, 0.45)' : 'none',
                transition: 'all 0.12s ease',
              }}
              title={`${item.name} (${angleName})${isSeparated ? ' • ĐÃ TÁCH NỀN' : ''}`}
            >
              {/* Top-Left: Selection Checkbox */}
              <div
                onClick={(e) => handleToggleSelectItem(item.id, e)}
                style={{
                  position: 'absolute',
                  top: 2,
                  left: 2,
                  zIndex: 4,
                  background: isChecked ? '#0284c7' : 'rgba(0, 0, 0, 0.7)',
                  borderRadius: 3,
                  width: 15,
                  height: 15,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  border: isChecked ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.3)',
                  cursor: 'pointer',
                }}
                title={isChecked ? 'Bỏ chọn ảnh này' : 'Chọn ảnh này để tách nền'}
              >
                {isChecked ? <CheckCircle2 size={10} color="#ffffff" /> : <div style={{ width: 6, height: 6 }} />}
              </div>

              {/* Top-Right: High-contrast X Delete Button */}
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onRemoveImage(item.id);
                }}
                style={{
                  position: 'absolute',
                  top: 2,
                  right: 2,
                  width: 17,
                  height: 17,
                  borderRadius: 4,
                  background: 'rgba(239, 68, 68, 0.9)',
                  color: '#ffffff',
                  border: '1px solid rgba(255, 255, 255, 0.4)',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  zIndex: 5,
                  boxShadow: '0 1px 4px rgba(0, 0, 0, 0.5)',
                  transition: 'transform 0.1s ease',
                }}
                title="Xóa ảnh này khỏi danh sách"
              >
                <X size={10} strokeWidth={3} />
              </button>

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

              {/* Bottom-Left: Selected viewing badge */}
              {isSelected && (
                <div
                  style={{
                    position: 'absolute',
                    bottom: 2,
                    left: 2,
                    background: '#0284c7',
                    color: '#ffffff',
                    fontSize: 7.5,
                    fontWeight: 800,
                    padding: '1px 3px',
                    borderRadius: 2,
                    zIndex: 3,
                  }}
                >
                  XEM
                </div>
              )}

              {/* Bottom-Right: Separated indicator */}
              {isSeparated && !isSelected && (
                <div
                  style={{
                    position: 'absolute',
                    bottom: 2,
                    right: 2,
                    background: 'rgba(34, 197, 94, 0.25)',
                    color: '#4ade80',
                    fontSize: 7.5,
                    fontWeight: 700,
                    padding: '1px 3px',
                    borderRadius: 2,
                    border: '1px solid rgba(74, 222, 128, 0.4)',
                    zIndex: 3,
                  }}
                  title="Ảnh đã được tách nền trong suốt"
                >
                  ✨ ĐÃ TÁCH
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* 6. Hover Info Bar / Active Image Metadata */}
      <div
        style={{
          marginTop: 'auto',
          padding: '4px 8px',
          background: 'rgba(0, 0, 0, 0.45)',
          borderRadius: 6,
          border: '1px solid rgba(255, 255, 255, 0.08)',
          fontSize: 9,
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
              <span style={{ color: '#34d399', fontWeight: 600 }}>{hoveredItem.aspectRatioLabel}</span>
              <span>{hoveredItem.width}×{hoveredItem.height}px</span>
            </div>
          </>
        ) : (
          <div style={{ color: '#64748b', fontStyle: 'italic', textAlign: 'center', display: 'flex', justifyContent: 'space-between' }}>
            <span>Đã chọn: <b style={{ color: '#38bdf8' }}>{selectedIds.size}</b> ảnh</span>
            <span>Tích chọn để tách nền loạt</span>
          </div>
        )}
      </div>
    </div>
  );
};
