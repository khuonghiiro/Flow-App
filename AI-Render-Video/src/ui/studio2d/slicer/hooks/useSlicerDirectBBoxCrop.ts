// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { useState, useCallback, useRef } from 'react';
import { PaddedCropRect, detectImageBBoxRect } from '../../../../core/utils/PixelBoundingBoxAlgorithms';
import { ChromaProcessOptions } from '../../../../core/utils/ChromaDespeckleProcessor';
import { SlicerUploadedImageItem, detectAspectRatioLabel } from './useSlicerMultiImageGallery';
import { GridCategoryDefinition } from '../../../../core/assets/GridSliceRegistry';

export interface UseSlicerDirectBBoxCropProps {
  loadedImage: HTMLImageElement | null;
  loadedImageRef: React.MutableRefObject<HTMLImageElement | null>;
  imageCanvasRef?: React.RefObject<HTMLCanvasElement>;
  setUserUploadedImageUrl: (url: string | null) => void;
  setLoadedImage: (img: HTMLImageElement | null) => void;
  previewDisplayMode: 'transparent' | 'original';
  setPreviewDisplayMode: (mode: 'transparent' | 'original') => void;
  chromaOptions: ChromaProcessOptions;
  currentCategory: GridCategoryDefinition;
  selectedCatId: string;
  initUniformDividers: (width: number, height: number, cols: number, rows: number) => void;
  slicedCanvasesRef: React.MutableRefObject<Map<string, HTMLCanvasElement>>;
  setSlicedResults: (results: Map<string, string>) => void;
  setHasExplicitlySliced: (sliced: boolean) => void;
  pushUndoState: (actionName: string) => void;
  showToast: (message: string, type: 'undo' | 'redo') => void;
  activeImageId: string | null;
  imageList: SlicerUploadedImageItem[];
  setImageList: React.Dispatch<React.SetStateAction<SlicerUploadedImageItem[]>>;
  checkedImageIds?: Set<string>;
}

interface CropResult {
  croppedOriginalUrl: string;
  croppedTransparentUrl: string | null;
  width: number;
  height: number;
}

/**
 * Pure Bounding Box Cropper: ONLY crops the rectangular region of the image without running chroma keying.
 */
async function cropImageByBBox(
  srcUrl: string,
  transparentSrc: string | null,
  padding: number,
  chromaOpts?: ChromaProcessOptions
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

  // 2. Cropped Transparent Canvas ONLY if transparentSrc already existed
  let croppedTransparentUrl: string | null = null;
  if (transparentSrc && transparentSrc !== srcUrl) {
    const transCanvas = document.createElement('canvas');
    transCanvas.width = safeWidth;
    transCanvas.height = safeHeight;
    const transCtx = transCanvas.getContext('2d', { willReadFrequently: true });
    if (transCtx) {
      const transImg = new Image();
      transImg.crossOrigin = 'anonymous';
      await new Promise<void>((resolve) => {
        transImg.onload = () => resolve();
        transImg.onerror = () => resolve();
        transImg.src = transparentSrc;
      });
      if (transImg.naturalWidth) {
        transCtx.drawImage(transImg, safeLeft, safeTop, safeWidth, safeHeight, 0, 0, safeWidth, safeHeight);
        croppedTransparentUrl = transCanvas.toDataURL('image/png');
      }
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
  imageCanvasRef,
  setUserUploadedImageUrl,
  setLoadedImage,
  previewDisplayMode,
  setPreviewDisplayMode,
  chromaOptions,
  currentCategory,
  selectedCatId,
  initUniformDividers,
  slicedCanvasesRef,
  setSlicedResults,
  setHasExplicitlySliced,
  pushUndoState,
  showToast,
  activeImageId,
  imageList,
  setImageList,
  checkedImageIds,
}: UseSlicerDirectBBoxCropProps) {
  const [isDirectBBoxCropActive, setIsDirectBBoxCropActive] = useState<boolean>(false);
  const [directBBoxPadding, setDirectBBoxPadding] = useState<number>(4);
  const currentBBoxRectRef = useRef<PaddedCropRect | null>(null);

  const handleToggleDirectBBoxCrop = useCallback(() => {
    setIsDirectBBoxCropActive((prev) => !prev);
  }, []);

  const handleApplyDirectBBoxCrop = useCallback(async () => {
    // Mode 1: Multi-Image BBox Crop (when checked images exist)
    if (checkedImageIds && checkedImageIds.size > 0 && imageList.length > 0) {
      pushUndoState(`Cắt BBox cho ${checkedImageIds.size} ảnh`);
      const updatedMap = new Map<string, CropResult>();

      for (const id of checkedImageIds) {
        const item = imageList.find((it) => it.id === id);
        if (!item) continue;

        try {
          const rawSrc = item.url || item.originalUrl;
          const transSrc = item.isTransparentSeparated && item.transparentUrl ? item.transparentUrl : null;
          const result = await cropImageByBBox(rawSrc, transSrc, directBBoxPadding, chromaOptions);
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
              return {
                ...item,
                url: res.croppedOriginalUrl,
                originalUrl: res.croppedOriginalUrl,
                transparentUrl: res.croppedTransparentUrl || undefined,
                isTransparentSeparated: Boolean(res.croppedTransparentUrl),
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
          const activeUrl = activeCrop.croppedOriginalUrl;

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

        setPreviewDisplayMode('original');
        setHasExplicitlySliced(false);
        slicedCanvasesRef.current.clear();
        setSlicedResults(new Map());
        setIsDirectBBoxCropActive(false);
        showToast(`✓ Đã cắt BBox gọn gàng cho ${updatedMap.size} ảnh!`, 'redo');
        return;
      }
    }

    // Mode 2: Single Image BBox Crop (on main canvas)
    const canvas = imageCanvasRef?.current;
    const img = loadedImage || loadedImageRef.current;
    const sourceElement = (canvas && canvas.width > 0 && canvas.height > 0) ? canvas : img;
    if (!sourceElement) return;

    let rect = currentBBoxRectRef.current;
    if (!rect) {
      rect = detectImageBBoxRect(sourceElement, chromaOptions, directBBoxPadding, 20);
    }

    if (!rect) {
      showToast('⚠️ Không tìm thấy đối tượng trong ảnh để cắt!', 'undo');
      return;
    }

    const imgW = sourceElement instanceof HTMLCanvasElement ? sourceElement.width : (sourceElement.naturalWidth || sourceElement.width);
    const imgH = sourceElement instanceof HTMLCanvasElement ? sourceElement.height : (sourceElement.naturalHeight || sourceElement.height);
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
    fullCtx.drawImage(sourceElement, 0, 0, imgW, imgH);

    // Purely crop the rectangle from current edited image
    cropCtx.drawImage(fullCanvas, safeLeft, safeTop, safeWidth, safeHeight, 0, 0, safeWidth, safeHeight);

    const dataUrl = croppedCanvas.toDataURL('image/png');
    pushUndoState('Cắt ảnh theo BBox');

    const nextImg = new Image();
    nextImg.crossOrigin = 'anonymous';
    nextImg.onload = () => {
      loadedImageRef.current = nextImg;
      setLoadedImage(nextImg);
      setUserUploadedImageUrl(dataUrl);
      initUniformDividers(nextImg.naturalWidth || nextImg.width, nextImg.naturalHeight || nextImg.height, currentCategory.cols, currentCategory.rows);
    };
    nextImg.src = dataUrl;

    setPreviewDisplayMode('original');
    setHasExplicitlySliced(false);
    slicedCanvasesRef.current.clear();
    setSlicedResults(new Map());

    if (activeImageId) {
      setImageList((prev) =>
        prev.map((item) =>
          item.id === activeImageId
            ? {
                ...item,
                url: dataUrl,
                originalUrl: dataUrl,
                transparentUrl: undefined,
                isTransparentSeparated: false,
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
    showToast(`✓ Đã cắt BBox ảnh thành công (${rect.width}×${rect.height}px)!`, 'redo');
  }, [
    checkedImageIds,
    imageList,
    loadedImage,
    loadedImageRef,
    imageCanvasRef,
    chromaOptions,
    directBBoxPadding,
    activeImageId,
    pushUndoState,
    setImageList,
    showToast,
    setUserUploadedImageUrl,
    setLoadedImage,
    initUniformDividers,
    currentCategory.cols,
    currentCategory.rows,
    slicedCanvasesRef,
    setSlicedResults,
    setHasExplicitlySliced,
    setPreviewDisplayMode,
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
