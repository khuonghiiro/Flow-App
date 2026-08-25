import { useState, useRef, useCallback } from 'react';
import { GridCategoryDefinition } from '../../../../core/assets/GridSliceRegistry';
import { ChromaProcessOptions, processCellChromaAndDespeckle } from '../../../../core/utils/ChromaDespeckleProcessor';
import { PaddedCropRect, detectImageBBoxRect } from '../../../../core/utils/PixelBoundingBoxAlgorithms';
import { SlicerUploadedImageItem, detectAspectRatioLabel } from './useSlicerMultiImageGallery';

export interface UseSlicerDirectBBoxCropProps {
  loadedImage: HTMLImageElement | null;
  loadedImageRef: React.MutableRefObject<HTMLImageElement | null>;
  setLoadedImage: (img: HTMLImageElement | null) => void;
  setUserUploadedImageUrl: (url: string | null) => void;
  previewDisplayMode: 'transparent' | 'original';
  currentCategory: GridCategoryDefinition;
  initUniformDividers: (width: number, height: number, cols: number, rows: number) => void;
  slicedCanvasesRef: React.MutableRefObject<Map<string, HTMLCanvasElement>>;
  setSlicedResults: (results: Map<string, string>) => void;
  setHasExplicitlySliced: (sliced: boolean) => void;
  selectedCatId: string;
  activeImageId: string | null;
  setImageList: React.Dispatch<React.SetStateAction<SlicerUploadedImageItem[]>>;
  pushUndoState: (label?: string) => void;
  showToast: (message: string, type: 'undo' | 'redo') => void;
  chromaOptions?: ChromaProcessOptions;
}

export function useSlicerDirectBBoxCrop({
  loadedImage,
  loadedImageRef,
  setLoadedImage,
  setUserUploadedImageUrl,
  previewDisplayMode,
  currentCategory,
  initUniformDividers,
  slicedCanvasesRef,
  setSlicedResults,
  setHasExplicitlySliced,
  selectedCatId,
  activeImageId,
  setImageList,
  pushUndoState,
  showToast,
  chromaOptions,
}: UseSlicerDirectBBoxCropProps) {
  const [isDirectBBoxCropActive, setIsDirectBBoxCropActive] = useState<boolean>(false);
  const [directBBoxPadding, setDirectBBoxPadding] = useState<number>(10);
  const currentBBoxRectRef = useRef<PaddedCropRect | null>(null);

  const handleToggleDirectBBoxCrop = useCallback(() => {
    setIsDirectBBoxCropActive((prev) => !prev);
  }, []);

  const handleApplyDirectBBoxCrop = useCallback(() => {
    const img = loadedImage || loadedImageRef.current;
    if (!img) return;

    // 1. Get the EXACT rect that is currently displayed on the screen
    let rect = currentBBoxRectRef.current;

    // Fallback if not cached
    if (!rect) {
      rect = detectImageBBoxRect(img, chromaOptions, directBBoxPadding, 20);
    }

    if (!rect) {
      showToast('⚠️ Không tìm thấy đối tượng trong ảnh để cắt!', 'undo');
      return;
    }

    const imgW = img.naturalWidth || img.width;
    const imgH = img.naturalHeight || img.height;

    const safeLeft = Math.max(0, Math.min(imgW - 1, rect.left));
    const safeTop = Math.max(0, Math.min(imgH - 1, rect.top));
    const safeWidth = Math.max(1, Math.min(imgW - safeLeft, rect.width));
    const safeHeight = Math.max(1, Math.min(imgH - safeTop, rect.height));

    const croppedCanvas = document.createElement('canvas');
    croppedCanvas.width = safeWidth;
    croppedCanvas.height = safeHeight;
    const cropCtx = croppedCanvas.getContext('2d', { willReadFrequently: true });
    if (!cropCtx) return;

    const fullCanvas = document.createElement('canvas');
    fullCanvas.width = imgW;
    fullCanvas.height = imgH;
    const fullCtx = fullCanvas.getContext('2d', { willReadFrequently: true });
    if (!fullCtx) return;
    fullCtx.drawImage(img, 0, 0, imgW, imgH);

    if (previewDisplayMode === 'original') {
      // Cắt trực tiếp từ ảnh gốc (chỉ cắt ảnh, giữ nguyên nền)
      cropCtx.drawImage(fullCanvas, safeLeft, safeTop, safeWidth, safeHeight, 0, 0, safeWidth, safeHeight);
    } else {
      // Cắt từ ảnh đã tách nền trong suốt
      if (chromaOptions) {
        processCellChromaAndDespeckle(fullCtx, imgW, imgH, chromaOptions);
      }
      cropCtx.drawImage(fullCanvas, safeLeft, safeTop, safeWidth, safeHeight, 0, 0, safeWidth, safeHeight);
    }

    const dataUrl = croppedCanvas.toDataURL('image/png');

    pushUndoState(previewDisplayMode === 'original' ? 'Cắt ảnh gốc theo BBox' : 'Cắt ảnh trong suốt theo BBox');

    // Load and update image immediately
    const nextImg = new Image();
    nextImg.crossOrigin = 'anonymous';
    nextImg.onload = () => {
      loadedImageRef.current = nextImg;
      setLoadedImage(nextImg);
      setUserUploadedImageUrl(dataUrl);
      const w = nextImg.naturalWidth || nextImg.width;
      const h = nextImg.naturalHeight || nextImg.height;
      initUniformDividers(w, h, currentCategory.cols, currentCategory.rows);
    };
    nextImg.src = dataUrl;

    if (selectedCatId === 'single_full_image' && previewDisplayMode === 'transparent') {
      slicedCanvasesRef.current.set('0_0', croppedCanvas);
      setSlicedResults(new Map([['0_0', dataUrl]]));
      setHasExplicitlySliced(true);
    }

    // Update in multi-image gallery if active
    if (activeImageId) {
      setImageList((prev) =>
        prev.map((item) =>
          item.id === activeImageId
            ? {
                ...item,
                url: dataUrl,
                width: rect.width,
                height: rect.height,
                aspectRatio: rect.width / rect.height,
                aspectRatioLabel: detectAspectRatioLabel(rect.width, rect.height),
              }
            : item
        )
      );
    }

    setIsDirectBBoxCropActive(false);
    showToast(`✓ Đã cắt ảnh thành công (${rect.width}×${rect.height}px)!`, 'redo');
  }, [
    loadedImage,
    loadedImageRef,
    previewDisplayMode,
    chromaOptions,
    selectedCatId,
    directBBoxPadding,
    activeImageId,
    pushUndoState,
    setImageList,
    showToast,
    setSlicedResults,
    setHasExplicitlySliced,
    setLoadedImage,
    setUserUploadedImageUrl,
    initUniformDividers,
    currentCategory.cols,
    currentCategory.rows,
    slicedCanvasesRef,
  ]);

  return {
    isDirectBBoxCropActive,
    directBBoxPadding,
    setDirectBBoxPadding,
    currentBBoxRectRef,
    handleToggleDirectBBoxCrop,
    handleApplyDirectBBoxCrop,
  };
}
