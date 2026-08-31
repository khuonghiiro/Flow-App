// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { useState, useCallback, useRef, useEffect } from 'react';
import { SlicerUploadedImageItem } from './useSlicerMultiImageGallery';
import { GridCategoryDefinition } from '../../../../core/assets/GridSliceRegistry';
import { parsePartFilename, ParsedPartFilenameInfo } from '../../../../core/assets/Asset2DRegistry';
import { loadSafeImage } from '../utils/slicerImageLoaderHelper';

export interface UseSlicerImageSyncProps {
  imageList: SlicerUploadedImageItem[];
  setImageList: React.Dispatch<React.SetStateAction<SlicerUploadedImageItem[]>>;
  activeImageIdRef: React.MutableRefObject<string | null>;
  loadedImageRef: React.MutableRefObject<HTMLImageElement | null>;
  setLoadedImage: (img: HTMLImageElement | null) => void;
  setUserUploadedImageUrl: (url: string | null) => void;
  setUploadedFileMetadata: (meta: ParsedPartFilenameInfo | null) => void;
  setHasExplicitlySliced: (val: boolean) => void;
  setSlicedResults: React.Dispatch<React.SetStateAction<Map<string, string>>>;
  slicedCanvasesRef: React.MutableRefObject<Map<string, HTMLCanvasElement>>;
  setPreviewDisplayMode: (mode: 'transparent' | 'original') => void;
  currentCategory: GridCategoryDefinition;
  setSelectedCatId: (id: string) => void;
  setColDividers: (divs: number[]) => void;
  setRowDividers: (divs: number[]) => void;
  initUniformDividers: (w: number, h: number, cols: number, rows: number) => void;
  redrawCanvas: (mode?: 'transparent' | 'original') => void;
  pushUndoState: (label: string) => void;
  showToast: (msg: string, type: 'undo' | 'redo') => void;
  baseSelectImage: (item: SlicerUploadedImageItem) => void;
  handleClearAll: () => void;
  externalImageUrl?: string | null;
  externalCategoryId?: string | null;
}

export function useSlicerImageSync({
  imageList,
  setImageList,
  activeImageIdRef,
  loadedImageRef,
  setLoadedImage,
  setUserUploadedImageUrl,
  setUploadedFileMetadata,
  setHasExplicitlySliced,
  setSlicedResults,
  slicedCanvasesRef,
  setPreviewDisplayMode,
  currentCategory,
  setSelectedCatId,
  setColDividers,
  setRowDividers,
  initUniformDividers,
  redrawCanvas,
  pushUndoState,
  showToast,
  baseSelectImage,
  handleClearAll,
  externalImageUrl,
  externalCategoryId,
}: UseSlicerImageSyncProps) {
  const [dividerSyncMode, setDividerSyncMode] = useState<'all' | 'single'>('single');

  const handleToggleDividerSyncMode = useCallback(() => {
    setDividerSyncMode((prev) => {
      const next = prev === 'single' ? 'all' : 'single';
      showToast(
        next === 'single'
          ? '🎯 Chế độ: Chỉnh lưới RIÊNG TỪNG ẢNH (Không ảnh hưởng các ảnh khác)'
          : '🌐 Chế độ: Chỉnh lưới ĐỒNG BỘ TOÀN BỘ ẢNH',
        'redo'
      );
      return next;
    });
  }, [showToast]);

  const handleUpdateItemImage = useCallback(
    async (id: string, newDataUrl: string) => {
      setImageList((prev) =>
        prev.map((item) =>
          item.id === id
            ? {
                ...item,
                url: newDataUrl,
                originalUrl: newDataUrl,
                transparentUrl: item.isTransparentSeparated ? newDataUrl : item.transparentUrl,
              }
            : item
        )
      );
      if (id === activeImageIdRef.current) {
        try {
          const nextImg = await loadSafeImage(newDataUrl);
          loadedImageRef.current = nextImg;
          setLoadedImage(nextImg);
          redrawCanvas();
        } catch (err) {
          console.warn('Failed to update active image from dataUrl:', err);
        }
      }
    },
    [activeImageIdRef, redrawCanvas, setImageList, loadedImageRef, setLoadedImage]
  );

  const handleUpdateItemDividers = useCallback(
    (itemId: string, newColDividers?: number[], newRowDividers?: number[]) => {
      if (dividerSyncMode === 'all') {
        if (newColDividers) setColDividers(newColDividers);
        if (newRowDividers) setRowDividers(newRowDividers);
        setImageList((prev) =>
          prev.map((it) => ({
            ...it,
            customColDividers: newColDividers ? [...newColDividers] : it.customColDividers,
            customRowDividers: newRowDividers ? [...newRowDividers] : it.customRowDividers,
          }))
        );
      } else {
        setImageList((prev) =>
          prev.map((it) =>
            it.id === itemId
              ? {
                  ...it,
                  customColDividers: newColDividers ? [...newColDividers] : it.customColDividers,
                  customRowDividers: newRowDividers ? [...newRowDividers] : it.customRowDividers,
                }
              : it
          )
        );
      }
    },
    [dividerSyncMode, setColDividers, setRowDividers, setImageList]
  );

  const handleSelectImage = useCallback(
    async (item: SlicerUploadedImageItem) => {
      baseSelectImage(item);
      const targetUrl = item.url || item.originalUrl;
      if (targetUrl) {
        setUserUploadedImageUrl(targetUrl);
        try {
          const img = await loadSafeImage(targetUrl);
          loadedImageRef.current = img;
          setLoadedImage(img);
          if (item.customColDividers && item.customColDividers.length > 0) {
            setColDividers(item.customColDividers);
          } else {
            initUniformDividers(
              img.naturalWidth || img.width,
              img.naturalHeight || img.height,
              currentCategory.cols,
              currentCategory.rows
            );
          }
          if (item.customRowDividers && item.customRowDividers.length > 0) {
            setRowDividers(item.customRowDividers);
          }
          redrawCanvas(item.isTransparentSeparated ? 'transparent' : 'original');
        } catch (err) {
          console.warn('Failed to load selected image:', err);
        }
      }
    },
    [
      baseSelectImage,
      currentCategory.cols,
      currentCategory.rows,
      initUniformDividers,
      setColDividers,
      setRowDividers,
      redrawCanvas,
      loadedImageRef,
      setLoadedImage,
      setUserUploadedImageUrl,
    ]
  );

  // Auto-sync loadedImage if imageList is populated and loadedImage is null
  useEffect(() => {
    if (imageList.length > 0 && !loadedImageRef.current) {
      const targetItem = imageList.find((it) => it.id === activeImageIdRef.current) || imageList[0];
      if (targetItem) {
        handleSelectImage(targetItem);
      }
    }
  }, [imageList, handleSelectImage]);

  // External Image Listeners
  const lastLoadedExternalUrlRef = useRef<string | null>(null);
  useEffect(() => {
    if (externalImageUrl && externalImageUrl !== lastLoadedExternalUrlRef.current) {
      lastLoadedExternalUrlRef.current = externalImageUrl;
      setUploadedFileMetadata(parsePartFilename(externalImageUrl));
      setUserUploadedImageUrl(externalImageUrl);
      setHasExplicitlySliced(false);
      setSlicedResults(new Map());
      slicedCanvasesRef.current.clear();
      setPreviewDisplayMode('original');
      if (externalCategoryId) setSelectedCatId(externalCategoryId);

      loadSafeImage(externalImageUrl)
        .then((extImg) => {
          loadedImageRef.current = extImg;
          setLoadedImage(extImg);
          initUniformDividers(extImg.width, extImg.height, currentCategory.cols, currentCategory.rows);
          redrawCanvas('original');
        })
        .catch((err) => {
          console.warn('Failed to load external image:', err);
        });
    }
  }, [
    externalImageUrl,
    externalCategoryId,
    setHasExplicitlySliced,
    setSlicedResults,
    setPreviewDisplayMode,
    setSelectedCatId,
    initUniformDividers,
    currentCategory.cols,
    currentCategory.rows,
    redrawCanvas,
    slicedCanvasesRef,
    loadedImageRef,
    setLoadedImage,
    setUploadedFileMetadata,
    setUserUploadedImageUrl,
  ]);

  const handleFullClear = useCallback(() => {
    pushUndoState('Xóa tất cả ảnh');
    handleClearAll();
    setUserUploadedImageUrl(null);
    setUploadedFileMetadata(null);
    setHasExplicitlySliced(false);
    setSlicedResults(new Map());
    slicedCanvasesRef.current.clear();
    setPreviewDisplayMode('original');
  }, [
    pushUndoState,
    handleClearAll,
    setUserUploadedImageUrl,
    setUploadedFileMetadata,
    setHasExplicitlySliced,
    setSlicedResults,
    setPreviewDisplayMode,
    slicedCanvasesRef,
  ]);

  return {
    dividerSyncMode,
    handleToggleDividerSyncMode,
    handleUpdateItemImage,
    handleUpdateItemDividers,
    handleSelectImage,
    handleFullClear,
  };
}
