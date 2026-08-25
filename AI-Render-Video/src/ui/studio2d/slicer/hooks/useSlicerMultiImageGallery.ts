import { useState, useCallback, useMemo } from 'react';
import { parsePartFilename, ParsedPartFilenameInfo } from '../../../../core/assets/prompt_builders/PartFilenameParser';
import { ChromaProcessOptions } from '../../../../core/utils/ChromaDespeckleProcessor';

export interface SlicerUploadedImageItem {
  id: string;
  url: string;
  originalUrl?: string;
  transparentUrl?: string;
  isTransparentSeparated?: boolean;
  filterConfig?: ChromaProcessOptions;
  file?: File;
  name: string;
  metadata: ParsedPartFilenameInfo | null;
  width: number;
  height: number;
  aspectRatio: number;
  aspectRatioLabel: string;
}

export interface SlicerPartGroupItem {
  part_id: string;
  part_name: string;
  group_id: string;
  group_name: string;
  icon: string;
  images: SlicerUploadedImageItem[];
}

export function detectAspectRatioLabel(width: number, height: number): string {
  if (!width || !height) return '1:1';
  const ratio = width / height;
  if (Math.abs(ratio - 1.0) < 0.06) return '1:1';
  if (Math.abs(ratio - 0.75) < 0.06) return '3:4';
  if (Math.abs(ratio - 1.333) < 0.06) return '4:3';
  if (Math.abs(ratio - 0.5625) < 0.06) return '9:16';
  if (Math.abs(ratio - 1.777) < 0.06) return '16:9';
  return `${width}x${height}`;
}

const PART_ICONS: Record<string, string> = {
  toc_truoc: '💇',
  toc_sau: '🌊',
  khuon_mat_no_face: '👤',
  khuon_mat: '👤',
  trong_den_iris: '🔮',
  trong_trang: '⚪',
  diem_sang_mat: '✨',
  mi_mat: '👁️',
  long_may: '✏️',
  mui: '👃',
  doi_tai: '👂',
  mieng: '👄',
  mat: '👀',
  than_co_ban: '🥋',
  canh_tay_trai: '🦾',
  cang_tay_trai: '🦾',
  ban_tay_trai: '🖐️',
  canh_tay_phai: '🦾',
  cang_tay_phai: '🦾',
  ban_tay_phai: '🖐️',
  dui_trai: '🦵',
  cang_chan_trai: '🥾',
  dui_phai: '🦵',
  cang_chan_phai: '🥾',
  ao_choang: '👘',
  vu_khi: '🗡️',
  master: '🌟',
};

function readImageDimensions(url: string): Promise<{ width: number; height: number }> {
  return new Promise((resolve) => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      resolve({ width: img.naturalWidth || 1024, height: img.naturalHeight || 1024 });
    };
    img.onerror = () => {
      resolve({ width: 1024, height: 1024 });
    };
    img.src = url;
  });
}

export function useSlicerMultiImageGallery(options: {
  onSelectActiveImage?: (image: SlicerUploadedImageItem) => void;
}) {
  const [imageList, setImageList] = useState<SlicerUploadedImageItem[]>([]);
  const [activeImageId, setActiveImageId] = useState<string | null>(null);
  const [activePartId, setActivePartId] = useState<string | null>(null);

  // Group images by detected part_id (or generic group if unrecognized)
  const partGroups = useMemo<SlicerPartGroupItem[]>(() => {
    const map = new Map<string, SlicerPartGroupItem>();

    imageList.forEach((item) => {
      const partId = item.metadata?.part_id || 'other_unassigned';
      const partName = item.metadata?.part_name || (item.metadata?.is_master_character ? 'Nhân Vật Gốc (Master)' : 'Chưa phân loại');
      const groupId = item.metadata?.group_id || 'general';
      const groupName = item.metadata?.group_name || 'Khác';
      const icon = PART_ICONS[partId] || (item.metadata?.is_master_character ? '🌟' : '📁');

      if (!map.has(partId)) {
        map.set(partId, {
          part_id: partId,
          part_name: partName,
          group_id: groupId,
          group_name: groupName,
          icon,
          images: [],
        });
      }
      map.get(partId)!.images.push(item);
    });

    return Array.from(map.values());
  }, [imageList]);

  // Active image item
  const activeImage = useMemo(() => {
    return imageList.find((it) => it.id === activeImageId) || (imageList.length > 0 ? imageList[0] : null);
  }, [imageList, activeImageId]);

  // Handle adding multiple files
  const handleAddFiles = useCallback(
    async (files: FileList | File[]) => {
      const fileArr = Array.from(files).filter((f) => f.type.startsWith('image/'));
      if (fileArr.length === 0) return;

      const newItems: SlicerUploadedImageItem[] = [];
      for (const file of fileArr) {
        const id = `${file.name}_${Date.now()}_${Math.random().toString(36).substring(2, 6)}`;
        const url = URL.createObjectURL(file);
        const metadata = parsePartFilename(file.name);
        const { width, height } = await readImageDimensions(url);
        const aspectRatio = width / (height || 1);
        const aspectRatioLabel = detectAspectRatioLabel(width, height);

        newItems.push({
          id,
          url,
          originalUrl: url,
          file,
          name: file.name,
          metadata,
          width,
          height,
          aspectRatio,
          aspectRatioLabel,
        });
      }

      setImageList((prev) => {
        const combined = [...prev, ...newItems];
        return combined;
      });

      if (newItems.length > 0) {
        const first = newItems[0];
        setActiveImageId(first.id);
        const partId = first.metadata?.part_id || 'other_unassigned';
        setActivePartId(partId);
        if (options.onSelectActiveImage) {
          options.onSelectActiveImage(first);
        }
      }
    },
    [options]
  );

  // Switch active image
  const handleSelectImage = useCallback(
    (imageId: string) => {
      const found = imageList.find((it) => it.id === imageId);
      if (found) {
        setActiveImageId(imageId);
        const partId = found.metadata?.part_id || 'other_unassigned';
        setActivePartId(partId);
        if (options.onSelectActiveImage) {
          options.onSelectActiveImage(found);
        }
      }
    },
    [imageList, options]
  );

  // Switch active part tab
  const handleSelectPart = useCallback(
    (partId: string) => {
      setActivePartId(partId);
      const group = partGroups.find((g) => g.part_id === partId);
      if (group && group.images.length > 0) {
        const firstInGroup = group.images[0];
        setActiveImageId(firstInGroup.id);
        if (options.onSelectActiveImage) {
          options.onSelectActiveImage(firstInGroup);
        }
      }
    },
    [partGroups, options]
  );

  // Remove single image
  const handleRemoveImage = useCallback(
    (imageId: string) => {
      setImageList((prev) => {
        const next = prev.filter((it) => it.id !== imageId);
        if (activeImageId === imageId) {
          const fallback = next.length > 0 ? next[0] : null;
          setActiveImageId(fallback ? fallback.id : null);
          setActivePartId(fallback?.metadata?.part_id || null);
          if (fallback && options.onSelectActiveImage) {
            options.onSelectActiveImage(fallback);
          }
        }
        return next;
      });
    },
    [activeImageId, options]
  );

  // Clear all images
  const handleClearAll = useCallback(() => {
    setImageList([]);
    setActiveImageId(null);
    setActivePartId(null);
  }, []);

  return {
    imageList,
    setImageList,
    activeImageId,
    activePartId,
    partGroups,
    activeImage,
    handleAddFiles,
    handleSelectImage,
    handleSelectPart,
    handleRemoveImage,
    handleClearAll,
  };
}
