import { useState, useCallback } from 'react';
import { Character2DAssembly, Character2DPartType, Character2DAngle } from '../../../../types/scene2d';
import { GridCategoryDefinition } from '../../../../core/assets/GridSliceRegistry';
import { ChromaProcessOptions, processCellChromaAndDespeckle } from '../../../../core/utils/ChromaDespeckleProcessor';
import { SlicerUploadedImageItem } from './useSlicerMultiImageGallery';
import { ThreeMultiAngleBillboardEngine } from '../../../../core/engine2d/ThreeMultiAngleBillboardEngine';
import { getAngleDefinitionById } from '../../../../core/assets/slicer/SlicerAngleConstants';

export interface UseSlicerBatchSeparationProps {
  imageList: SlicerUploadedImageItem[];
  setImageList: React.Dispatch<React.SetStateAction<SlicerUploadedImageItem[]>>;
  activeImageIdRef: React.MutableRefObject<string | null>;
  activeImageId: string | null;
  loadedImageRef: React.MutableRefObject<HTMLImageElement | null>;
  setLoadedImage: (img: HTMLImageElement | null) => void;
  setUserUploadedImageUrl: (url: string | null) => void;
  setPreviewDisplayMode: (mode: 'transparent' | 'original') => void;
  setHasExplicitlySliced: (sliced: boolean) => void;
  slicedCanvasesRef: React.MutableRefObject<Map<string, HTMLCanvasElement>>;
  setSlicedResults: (results: Map<string, string>) => void;
  redrawCanvas: (modeOverride?: 'transparent' | 'original') => void;
  showToast: (message: string, type: 'undo' | 'redo') => void;
  pushUndoState: (label?: string) => void;
  currentCategory: GridCategoryDefinition;
  colDividers: number[];
  rowDividers: number[];
  paddingInset: number;
  chromaOptions: ChromaProcessOptions;
  currentAssembly: Character2DAssembly;
  onApplyAssembly: (updated: Character2DAssembly) => void;
  threeEngineRef: React.RefObject<ThreeMultiAngleBillboardEngine | null>;
  singleImageSlot: Character2DPartType;
  singleImageAngle: Character2DAngle;
}

export function useSlicerBatchSeparation({
  imageList,
  setImageList,
  activeImageIdRef,
  activeImageId,
  loadedImageRef,
  setLoadedImage,
  setUserUploadedImageUrl,
  setPreviewDisplayMode,
  setHasExplicitlySliced,
  slicedCanvasesRef,
  setSlicedResults,
  redrawCanvas,
  showToast,
  pushUndoState,
  currentCategory,
  colDividers,
  rowDividers,
  paddingInset,
  chromaOptions,
  currentAssembly,
  onApplyAssembly,
  threeEngineRef,
  singleImageSlot,
  singleImageAngle,
}: UseSlicerBatchSeparationProps) {
  const [isBatchProcessing, setIsBatchProcessing] = useState<boolean>(false);

  const handleBatchSeparateImages = useCallback(
    async (imageIds: string[]) => {
      if (imageIds.length === 0) return;
      setIsBatchProcessing(true);
      pushUndoState(`Tách nền ${imageIds.length} ảnh`);

      const updatedMap = new Map<string, string>();

      for (const id of imageIds) {
        const item = imageList.find((it) => it.id === id);
        if (!item) continue;

        try {
          const img = new Image();
          img.crossOrigin = 'anonymous';
          await new Promise<void>((resolve, reject) => {
            img.onload = () => resolve();
            img.onerror = () => reject();
            img.src = item.originalUrl || item.url;
          });

          const w = img.naturalWidth || img.width;
          const h = img.naturalHeight || img.height;
          const canvas = document.createElement('canvas');
          canvas.width = w;
          canvas.height = h;
          const ctx = canvas.getContext('2d', { willReadFrequently: true });
          if (ctx) {
            ctx.drawImage(img, 0, 0, w, h);
            // Use item's specific filterConfig if available, otherwise global chromaOptions
            const opts = item.filterConfig || chromaOptions;
            processCellChromaAndDespeckle(ctx, w, h, opts);
            const dataUrl = canvas.toDataURL('image/png');
            updatedMap.set(id, dataUrl);
          }
        } catch (err) {
          console.warn('Failed to separate background for item:', id, err);
        }
      }

      // Update imageList with the separated transparent dataUrls & saved filterConfig
      setImageList((prev) =>
        prev.map((item) => {
          if (updatedMap.has(item.id)) {
            const transUrl = updatedMap.get(item.id)!;
            return {
              ...item,
              originalUrl: item.originalUrl || item.url,
              url: transUrl,
              transparentUrl: transUrl,
              isTransparentSeparated: true,
              filterConfig: item.filterConfig ? { ...item.filterConfig } : { ...chromaOptions },
            };
          }
          return item;
        })
      );

      // Determine which image should be displayed on the canvas
      const currentActiveId = activeImageIdRef.current || activeImageId || imageList[0]?.id;
      const targetActiveId =
        currentActiveId && updatedMap.has(currentActiveId)
          ? currentActiveId
          : imageIds.find((id) => updatedMap.has(id)) || currentActiveId;

      if (targetActiveId && updatedMap.has(targetActiveId)) {
        const newUrl = updatedMap.get(targetActiveId)!;
        const nextImg = new Image();
        nextImg.crossOrigin = 'anonymous';
        await new Promise<void>((res) => {
          nextImg.onload = () => res();
          nextImg.onerror = () => res();
          nextImg.src = newUrl;
        });

        loadedImageRef.current = nextImg;
        setLoadedImage(nextImg);
        setUserUploadedImageUrl(newUrl);
        setPreviewDisplayMode('transparent');
        setHasExplicitlySliced(true);

        const pad = Math.max(0, paddingInset);
        const w = nextImg.naturalWidth || nextImg.width;
        const h = nextImg.naturalHeight || nextImg.height;

        const results = new Map<string, string>();
        slicedCanvasesRef.current.clear();

        const sc = document.createElement('canvas');
        sc.width = w;
        sc.height = h;
        const sctx = sc.getContext('2d');
        if (sctx) {
          sctx.drawImage(nextImg, 0, 0);
          slicedCanvasesRef.current.set('0_0', sc);
          results.set('0_0', newUrl);
        }

        if (currentCategory.id !== 'single_full_image') {
          currentCategory.cells.forEach((cell) => {
            if (colDividers.length <= cell.col + 1 || rowDividers.length <= cell.row + 1) return;
            const rawX0 = colDividers[cell.col];
            const rawY0 = rowDividers[cell.row];
            const cellW = Math.max(10, colDividers[cell.col + 1] - rawX0 - pad * 2);
            const cellH = Math.max(10, rowDividers[cell.row + 1] - rawY0 - pad * 2);
            const cellCanvas = document.createElement('canvas');
            cellCanvas.width = cellW;
            cellCanvas.height = cellH;
            const cCtx = cellCanvas.getContext('2d');
            if (cCtx) {
              cCtx.drawImage(nextImg, rawX0 + pad, rawY0 + pad, cellW, cellH, 0, 0, cellW, cellH);
              const key = `${cell.row}_${cell.col}`;
              slicedCanvasesRef.current.set(key, cellCanvas);
              results.set(key, cellCanvas.toDataURL('image/png'));
            }
          });
        }

        setSlicedResults(results);

        // Update 3D Engine assembly if item has slot / angle metadata
        const currentItem = imageList.find((it) => it.id === targetActiveId);
        const slot = (currentItem?.metadata?.part_id as Character2DPartType) || singleImageSlot;
        const angle = currentItem?.metadata?.angle_id
          ? getAngleDefinitionById(currentItem.metadata.angle_id).angle
          : singleImageAngle;
        if (slot) {
          const updatedAssembly: Character2DAssembly = JSON.parse(JSON.stringify(currentAssembly));
          if (!updatedAssembly.parts[slot]) {
            updatedAssembly.parts[slot] = {
              path: newUrl,
              offset: [0, 0],
              scale: [1, 1],
              rotation: 0,
              pivot: [0.5, 0.5],
              flipX: false,
              flipY: false,
              z_index: 1,
              z_depth_3d: 0,
              opacity: 1,
              angles: {},
            };
          }
          const part = updatedAssembly.parts[slot]!;
          if (angle === 'front') part.path = newUrl;
          if (!part.angles) part.angles = {};
          part.angles[angle] = newUrl;
          onApplyAssembly(updatedAssembly);
          if (threeEngineRef.current) threeEngineRef.current.setAssembly(updatedAssembly);
        }

        // Force canvas redraw in transparent mode immediately
        redrawCanvas('transparent');
      }

      setIsBatchProcessing(false);
      showToast(`✓ Đã tách nền thành công cho ${updatedMap.size} ảnh!`, 'redo');
    },
    [
      imageList,
      activeImageIdRef,
      activeImageId,
      loadedImageRef,
      setLoadedImage,
      setUserUploadedImageUrl,
      setPreviewDisplayMode,
      setHasExplicitlySliced,
      slicedCanvasesRef,
      setSlicedResults,
      redrawCanvas,
      showToast,
      pushUndoState,
      currentCategory,
      colDividers,
      rowDividers,
      paddingInset,
      chromaOptions,
      currentAssembly,
      onApplyAssembly,
      threeEngineRef,
      singleImageSlot,
      singleImageAngle,
      setImageList,
    ]
  );

  return {
    isBatchProcessing,
    handleBatchSeparateImages,
  };
}
