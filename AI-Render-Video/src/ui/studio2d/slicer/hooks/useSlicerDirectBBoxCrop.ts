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
  checkedImageIds?: Set<string>;
  imageList?: SlicerUploadedImageItem[];
}

interface CropResult {
  croppedOriginalUrl: string;
  croppedTransparentUrl: string | null;
  width: number;
  height: number;
}

/**
 * Helper to crop an individual image item by Bounding Box
 */
async function cropImageByBBox(
  srcUrl: string,
  transparentSrc: string | null,
  padding: number,
  chromaOpts?: ChromaProcessOptions,
  isTransparentMode: boolean = false
): Promise<CropResult | null> {
  const baseImg = new Image();
  baseImg.crossOrigin = 'anonymous';
  await new Promise<void>((resolve, reject) => {
    baseImg.onload = () => resolve();
    baseImg.onerror = () => reject();
    baseImg.src = srcUrl;
  });

  const imgW = baseImg.naturalWidth || baseImg.width;
  const imgH = baseImg.naturalHeight || baseImg.height;
  if (!imgW || !imgH) return null;

  const rect = detectImageBBoxRect(baseImg, chromaOpts, padding, 20);
  if (!rect) return null;

  const safeLeft = Math.max(0, Math.min(imgW - 1, rect.left));
  const safeTop = Math.max(0, Math.min(imgH - 1, rect.top));
  const safeWidth = Math.max(1, Math.min(imgW - safeLeft, rect.width));
  const safeHeight = Math.max(1, Math.min(imgH - safeTop, rect.height));

  // 1. Cropped Original Canvas
  const origCanvas = document.createElement('canvas');
  origCanvas.width = safeWidth;
  origCanvas.height = safeHeight;
  const origCtx = origCanvas.getContext('2d', { willReadFrequently: true });
  if (!origCtx) return null;
  origCtx.drawImage(baseImg, safeLeft, safeTop, safeWidth, safeHeight, 0, 0, safeWidth, safeHeight);
  const croppedOriginalUrl = origCanvas.toDataURL('image/png');

  // 2. Cropped Transparent Canvas
  let croppedTransparentUrl: string | null = null;
  if (transparentSrc || isTransparentMode) {
    const transCanvas = document.createElement('canvas');
    transCanvas.width = safeWidth;
    transCanvas.height = safeHeight;
    const transCtx = transCanvas.getContext('2d', { willReadFrequently: true });
    if (transCtx) {
      if (transparentSrc && transparentSrc !== srcUrl) {
        const transImg = new Image();
        transImg.crossOrigin = 'anonymous';
        await new Promise<void>((resolve) => {
          transImg.onload = () => resolve();
          transImg.onerror = () => resolve();
          transImg.src = transparentSrc;
        });
        if (transImg.naturalWidth) {
          transCtx.drawImage(transImg, safeLeft, safeTop, safeWidth, safeHeight, 0, 0, safeWidth, safeHeight);
        } else {
          transCtx.drawImage(origCanvas, 0, 0);
          if (chromaOpts) {
            processCellChromaAndDespeckle(transCtx, safeWidth, safeHeight, chromaOpts);
          }
        }
      } else {
        transCtx.drawImage(origCanvas, 0, 0);
        if (chromaOpts) {
          processCellChromaAndDespeckle(transCtx, safeWidth, safeHeight, chromaOpts);
        }
      }
      croppedTransparentUrl = transCanvas.toDataURL('image/png');
    }
  }

  return {
    croppedOriginalUrl,
    croppedTransparentUrl,
    width: safeWidth,
    height: safeHeight,
  };
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
  checkedImageIds,
  imageList = [],
}: UseSlicerDirectBBoxCropProps) {
  const [isDirectBBoxCropActive, setIsDirectBBoxCropActive] = useState<boolean>(false);
  const [directBBoxPadding, setDirectBBoxPadding] = useState<number>(10);
  const currentBBoxRectRef = useRef<PaddedCropRect | null>(null);

  const handleToggleDirectBBoxCrop = useCallback(() => {
    setIsDirectBBoxCropActive((prev) => !prev);
  }, []);

  const handleApplyDirectBBoxCrop = useCallback(async () => {
    // Mode 1: Multi-Image BBox Crop (when 1 or more images are checked)
    if (checkedImageIds && checkedImageIds.size > 0 && imageList.length > 0) {
      pushUndoState(`Cắt BBox cho ${checkedImageIds.size} ảnh`);
      const updatedMap = new Map<string, CropResult>();

      for (const id of checkedImageIds) {
        const item = imageList.find((it) => it.id === id);
        if (!item) continue;

        try {
          const rawSrc = item.originalUrl || item.url;
          const transSrc = item.isTransparentSeparated && item.transparentUrl ? item.transparentUrl : null;
          const result = await cropImageByBBox(
            rawSrc,
            transSrc,
            directBBoxPadding,
            chromaOptions,
            previewDisplayMode === 'transparent'
          );
          if (result) {
            updatedMap.set(item.id, result);
          }
        } catch (err) {
          console.warn('Failed to crop BBox for item:', id, err);
        }
      }

      if (updatedMap.size > 0) {
        setImageList((prev) =>
          prev.map((item) => {
            if (updatedMap.has(item.id)) {
              const res = updatedMap.get(item.id)!;
              const hasTrans = Boolean(res.croppedTransparentUrl);
              const activeUrl =
                previewDisplayMode === 'transparent' && res.croppedTransparentUrl
                  ? res.croppedTransparentUrl
                  : res.croppedOriginalUrl;

              return {
                ...item,
                url: activeUrl,
                originalUrl: res.croppedOriginalUrl,
                transparentUrl: res.croppedTransparentUrl || item.transparentUrl,
                isTransparentSeparated: hasTrans,
                width: res.width,
                height: res.height,
                aspectRatio: res.width / res.height,
                aspectRatioLabel: detectAspectRatioLabel(res.width, res.height),
              };
            }
            return item;
          })
        );

        // Update active image if it was in the crop list
        if (activeImageId && updatedMap.has(activeImageId)) {
          const activeCrop = updatedMap.get(activeImageId)!;
          const activeUrl =
            previewDisplayMode === 'transparent' && activeCrop.croppedTransparentUrl
              ? activeCrop.croppedTransparentUrl
              : activeCrop.croppedOriginalUrl;

          const nextImg = new Image();
          nextImg.crossOrigin = 'anonymous';
          nextImg.onload = () => {
            loadedImageRef.current = nextImg;
            setLoadedImage(nextImg);
            setUserUploadedImageUrl(activeUrl);
            initUniformDividers(activeCrop.width, activeCrop.height, currentCategory.cols, currentCategory.rows);
          };
          nextImg.src = activeUrl;
        }

        setIsDirectBBoxCropActive(false);
        showToast(`✓ Đã cắt BBox thành công cho ${updatedMap.size} ảnh!`, 'redo');
        return;
      }
    }

    // Mode 2: Single Image BBox Crop (when on main canvas)
    const img = loadedImage || loadedImageRef.current;
    if (!img) return;

    let rect = currentBBoxRectRef.current;
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
      cropCtx.drawImage(fullCanvas, safeLeft, safeTop, safeWidth, safeHeight, 0, 0, safeWidth, safeHeight);
    } else {
      if (chromaOptions) {
        processCellChromaAndDespeckle(fullCtx, imgW, imgH, chromaOptions);
      }
      cropCtx.drawImage(fullCanvas, safeLeft, safeTop, safeWidth, safeHeight, 0, 0, safeWidth, safeHeight);
    }

    const dataUrl = croppedCanvas.toDataURL('image/png');
    pushUndoState(previewDisplayMode === 'original' ? 'Cắt ảnh gốc theo BBox' : 'Cắt ảnh trong suốt theo BBox');

    const nextImg = new Image();
    nextImg.crossOrigin = 'anonymous';
    nextImg.onload = () => {
      loadedImageRef.current = nextImg;
      setLoadedImage(nextImg);
      setUserUploadedImageUrl(dataUrl);
      initUniformDividers(nextImg.naturalWidth || nextImg.width, nextImg.naturalHeight || nextImg.height, currentCategory.cols, currentCategory.rows);
    };
    nextImg.src = dataUrl;

    if (selectedCatId === 'single_full_image' && previewDisplayMode === 'transparent') {
      slicedCanvasesRef.current.set('0_0', croppedCanvas);
      setSlicedResults(new Map([['0_0', dataUrl]]));
      setHasExplicitlySliced(true);
    }

    if (activeImageId) {
      setImageList((prev) =>
        prev.map((item) =>
          item.id === activeImageId
            ? {
                ...item,
                url: dataUrl,
                originalUrl: previewDisplayMode === 'original' ? dataUrl : item.originalUrl,
                transparentUrl: previewDisplayMode === 'transparent' ? dataUrl : item.transparentUrl,
                isTransparentSeparated: previewDisplayMode === 'transparent' ? true : item.isTransparentSeparated,
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
    checkedImageIds,
    imageList,
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
