// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { useCallback } from 'react';
import { Character2DAssembly, Character2DAngle, Character2DPartType } from '../../../../types/scene2d';
import { GridCategoryDefinition } from '../../../../core/assets/GridSliceRegistry';
import { processCellChromaAndDespeckle, ChromaProcessOptions } from '../../../../core/utils/ChromaDespeckleProcessor';
import { PART_HIERARCHY_CONFIG } from '../../../../core/assets/Asset2DRegistry';
import { ThreeMultiAngleBillboardEngine } from '../../../../core/engine2d/ThreeMultiAngleBillboardEngine';
import {
  detectPixelContentBoundingBox,
  cropCanvasWithPadding,
  PaddedCropRect,
} from '../../../../core/utils/PixelBoundingBoxAlgorithms';
import { cleanOuterEdgeDarkBorders } from '../utils/slicerPixelEraserHelper';
import { SlicerUploadedImageItem, detectAspectRatioLabel } from './useSlicerMultiImageGallery';
import { loadSafeImage } from '../utils/slicerImageLoaderHelper';

export interface UseSlicerSlicingPipelineProps {
  userUploadedImageUrl?: string | null;
  loadedImageRef: React.MutableRefObject<HTMLImageElement | null>;
  currentCategory: GridCategoryDefinition;
  colDividers: number[];
  rowDividers: number[];
  paddingInset: number;
  chromaOptions: ChromaProcessOptions;
  cellCropRectsRef?: React.MutableRefObject<Map<string, PaddedCropRect>>;
  enableSmartCrop?: boolean;
  smartCropPadding?: number;
  slicedCanvasesRef: React.MutableRefObject<Map<string, HTMLCanvasElement>>;
  setSlicedResults: React.Dispatch<React.SetStateAction<Map<string, string>>>;
  setPreviewDisplayMode: React.Dispatch<React.SetStateAction<'transparent' | 'original'>>;
  setHasExplicitlySliced: React.Dispatch<React.SetStateAction<boolean>>;
  setIsProcessing: React.Dispatch<React.SetStateAction<boolean>>;
  setAssemblySuccess: React.Dispatch<React.SetStateAction<boolean>>;
  redrawCanvas: (modeOverride?: 'transparent' | 'original') => void;
  showToast?: (message: string, type: 'undo' | 'redo') => void;
  currentAssembly: Character2DAssembly;
  onApplyAssembly: (updated: Character2DAssembly) => void;
  threeEngineRef: React.RefObject<ThreeMultiAngleBillboardEngine | null>;
  singleImageSlot: Character2DPartType;
  singleImageAngle: Character2DAngle;
  activeImageIdRef?: React.MutableRefObject<string | null>;
  setImageList?: React.Dispatch<React.SetStateAction<SlicerUploadedImageItem[]>>;
  setCheckedImageIds?: React.Dispatch<React.SetStateAction<Set<string>>>;
  setSelectedCatId?: (id: string) => void;
  onSliceSuccess?: (results: Map<string, string>) => void;
}

export function useSlicerSlicingPipeline({
  userUploadedImageUrl,
  loadedImageRef,
  currentCategory,
  colDividers,
  rowDividers,
  paddingInset,
  chromaOptions,
  cellCropRectsRef,
  enableSmartCrop = false,
  smartCropPadding = 2,
  slicedCanvasesRef,
  setSlicedResults,
  setPreviewDisplayMode,
  setHasExplicitlySliced,
  setIsProcessing,
  setAssemblySuccess,
  redrawCanvas,
  showToast,
  currentAssembly,
  onApplyAssembly,
  threeEngineRef,
  singleImageSlot,
  singleImageAngle,
  activeImageIdRef,
  setImageList,
  setCheckedImageIds,
  setSelectedCatId,
  onSliceSuccess,
}: UseSlicerSlicingPipelineProps) {
  const handleAutoSliceAndAssemble = useCallback(
    async (overrides?: Partial<ChromaProcessOptions>) => {
      let img = loadedImageRef.current;
      if (!img && userUploadedImageUrl) {
        try {
          img = await loadSafeImage(userUploadedImageUrl);
          loadedImageRef.current = img;
        } catch {
          img = null;
        }
      }
      if (!img) {
        if (showToast) showToast('⚠️ Vui lòng chọn hoặc tải ảnh trước khi bóc tách!', 'undo');
        return;
      }

      setIsProcessing(true);

      const opts: ChromaProcessOptions = {
        ...chromaOptions,
        ...(overrides || {}),
      };

      const pad = Math.max(0, paddingInset);
      const results = new Map<string, string>();
      const updatedAssembly: Character2DAssembly = JSON.parse(JSON.stringify(currentAssembly));
      const newFrameItems: SlicerUploadedImageItem[] = [];

      const activeId = activeImageIdRef?.current;

      if (currentCategory.id === 'single_full_image') {
        const w = Math.max(10, img.width - pad * 2);
        const h = Math.max(10, img.height - pad * 2);
        const canvas = document.createElement('canvas');
        canvas.width = w;
        canvas.height = h;
        const ctx = canvas.getContext('2d', { willReadFrequently: true });
        if (ctx) {
          ctx.drawImage(img, pad, pad, w, h, 0, 0, w, h);
          processCellChromaAndDespeckle(ctx, w, h, opts);
          cleanOuterEdgeDarkBorders(ctx, w, h, Math.max(3, Math.min(6, pad)));

          let finalCanvas = canvas;
          let dataUrl = canvas.toDataURL('image/png');

          if (enableSmartCrop) {
            const bbox = detectPixelContentBoundingBox(ctx, w, h, 10);
            if (bbox.hasContent) {
              const cropped = cropCanvasWithPadding(canvas, bbox, smartCropPadding);
              finalCanvas = cropped.croppedCanvas;
              dataUrl = cropped.dataUrl;
              if (cellCropRectsRef) {
                cellCropRectsRef.current.set('0_0', cropped.rect);
              }
            } else if (cellCropRectsRef) {
              cellCropRectsRef.current.delete('0_0');
            }
          } else if (cellCropRectsRef) {
            cellCropRectsRef.current.delete('0_0');
          }

          results.set('0_0', dataUrl);
          slicedCanvasesRef.current.set('0_0', finalCanvas);

          const slot = singleImageSlot;
          const hierarchy = PART_HIERARCHY_CONFIG[slot];
          if (!updatedAssembly.parts[slot]) {
            updatedAssembly.parts[slot] = {
              path: dataUrl,
              offset: hierarchy?.defaultOffset ? [...hierarchy.defaultOffset] : [0, 0],
              scale: [1, 1],
              rotation: 0,
              pivot: hierarchy?.defaultPivot ? [...hierarchy.defaultPivot] : [0.5, 0.5],
              flipX: false,
              flipY: false,
              z_index: hierarchy?.defaultZ ?? 1,
              z_depth_3d: hierarchy?.defaultZDepth3D ?? 0,
              opacity: 1,
              angles: {},
            };
          }
          const part = updatedAssembly.parts[slot]!;
          if (singleImageAngle === 'front') part.path = dataUrl;
          if (!part.angles) part.angles = {};
          part.angles[singleImageAngle] = dataUrl;

          // Update active image item in multi-image gallery
          if (activeId && setImageList) {
            setImageList((prev) =>
              prev.map((it) => (it.id === activeId ? { ...it, url: dataUrl, transparentUrl: dataUrl, isTransparentSeparated: true } : it))
            );
          }
        }
      } else {
        const numCols = Math.max(1, currentCategory.cols || 4);
        const numRows = Math.max(1, currentCategory.rows || 1);
        const imgW = img.naturalWidth || img.width;
        const imgH = img.naturalHeight || img.height;
        const baseW = colDividers.length > numCols ? colDividers[colDividers.length - 1] : imgW;
        const baseH = rowDividers.length > numRows ? rowDividers[rowDividers.length - 1] : imgH;

        const sortedCells = [...currentCategory.cells].sort((a, b) => {
          if (a.row !== b.row) return a.row - b.row;
          return a.col - b.col;
        });

        sortedCells.forEach((cell, idx) => {
          let rawX0 = Math.round((cell.col * imgW) / numCols);
          let rawX1 = Math.round(((cell.col + 1) * imgW) / numCols);
          let rawY0 = Math.round((cell.row * imgH) / numRows);
          let rawY1 = Math.round(((cell.row + 1) * imgH) / numRows);

          if (colDividers.length > cell.col + 1 && baseW > 0) {
            rawX0 = Math.round((colDividers[cell.col] / baseW) * imgW);
            rawX1 = Math.round((colDividers[cell.col + 1] / baseW) * imgW);
          }
          if (rowDividers.length > cell.row + 1 && baseH > 0) {
            rawY0 = Math.round((rowDividers[cell.row] / baseH) * imgH);
            rawY1 = Math.round((rowDividers[cell.row + 1] / baseH) * imgH);
          }

          const rawW = rawX1 - rawX0;
          const rawH = rawY1 - rawY0;
          const safePadX = Math.min(pad, Math.floor(rawW / 2.5));
          const safePadY = Math.min(pad, Math.floor(rawH / 2.5));

          const x0 = rawX0 + safePadX;
          const y0 = rawY0 + safePadY;
          const cellW = Math.max(10, rawW - safePadX * 2);
          const cellH = Math.max(10, rawH - safePadY * 2);

          const canvas = document.createElement('canvas');
          canvas.width = cellW;
          canvas.height = cellH;
          const ctx = canvas.getContext('2d', { willReadFrequently: true });
          if (ctx) {
            ctx.drawImage(img, x0, y0, cellW, cellH, 0, 0, cellW, cellH);
            processCellChromaAndDespeckle(ctx, cellW, cellH, opts);
            cleanOuterEdgeDarkBorders(ctx, cellW, cellH, Math.max(2, Math.min(5, safePadX)));

            const key = `${cell.row}_${cell.col}`;
            let finalCanvas = canvas;
            let dataUrl = canvas.toDataURL('image/png');

            if (enableSmartCrop) {
              const bbox = detectPixelContentBoundingBox(ctx, cellW, cellH, 10);
              if (bbox.hasContent) {
                const cropped = cropCanvasWithPadding(canvas, bbox, smartCropPadding);
                finalCanvas = cropped.croppedCanvas;
                dataUrl = cropped.dataUrl;
                if (cellCropRectsRef) {
                  cellCropRectsRef.current.set(key, cropped.rect);
                }
              } else if (cellCropRectsRef) {
                cellCropRectsRef.current.delete(key);
              }
            } else if (cellCropRectsRef) {
              cellCropRectsRef.current.delete(key);
            }

            results.set(key, dataUrl);
            slicedCanvasesRef.current.set(key, finalCanvas);

            // Create individual frame item for gallery & multi-card grid
            const frameNumber = idx + 1;
            const frameLabel = cell.partSlot ? cell.partSlot : `Frame_${String(frameNumber).padStart(2, '0')}`;
            const frameItem: SlicerUploadedImageItem = {
              id: `${activeId || 'img'}_f${frameNumber}_${Date.now()}_${idx}`,
              name: `${frameLabel}.png`,
              url: dataUrl,
              originalUrl: dataUrl,
              transparentUrl: dataUrl,
              isTransparentSeparated: true,
              isFrameItem: true,
              parentSheetOriginalUrl: userUploadedImageUrl || img.src,
              cellRow: cell.row,
              cellCol: cell.col,
              parentNumCols: numCols,
              parentNumRows: numRows,
              filterConfig: { ...opts },
              metadata: cell.partSlot ? { part_id: cell.partSlot, part_name: cell.partSlot, group_id: 'general', group_name: 'Linh kiện', angle_id: cell.angle || 'front', angle_name: cell.angle || 'Trước' } : null,
              width: cellW,
              height: cellH,
              aspectRatio: cellW / (cellH || 1),
              aspectRatioLabel: detectAspectRatioLabel(cellW, cellH),
            };
            newFrameItems.push(frameItem);

            if (cell.partSlot) {
              const hierarchy = PART_HIERARCHY_CONFIG[cell.partSlot];
              if (!updatedAssembly.parts[cell.partSlot]) {
                updatedAssembly.parts[cell.partSlot] = {
                  path: dataUrl,
                  offset: hierarchy?.defaultOffset ? [...hierarchy.defaultOffset] : [0, 0],
                  scale: [1, 1],
                  rotation: 0,
                  pivot: hierarchy?.defaultPivot ? [...hierarchy.defaultPivot] : [0.5, 0.5],
                  flipX: false,
                  flipY: false,
                  z_index: hierarchy?.defaultZ ?? 1,
                  z_depth_3d: hierarchy?.defaultZDepth3D ?? 0,
                  opacity: 1,
                  angles: {},
                };
              }
              const part = updatedAssembly.parts[cell.partSlot]!;
              if (cell.angle === 'front' || cell.col === 0) part.path = dataUrl;
              if (cell.angle) {
                if (!part.angles) part.angles = {};
                part.angles[cell.angle] = dataUrl;
              }
            }
          }
        });

        // Expand sliced frame items into imageList so Column 2 and Column 3 show individual pieces
        if (newFrameItems.length > 0 && setImageList) {
          setImageList((prev) => {
            if (activeId) {
              const nextList: SlicerUploadedImageItem[] = [];
              for (const it of prev) {
                if (it.id === activeId) {
                  nextList.push(...newFrameItems);
                } else {
                  nextList.push(it);
                }
              }
              return nextList.length > 0 ? nextList : newFrameItems;
            }
            return newFrameItems;
          });

          if (setCheckedImageIds) {
            setCheckedImageIds(new Set(newFrameItems.map((f) => f.id)));
          }

          if (setSelectedCatId) {
            setSelectedCatId('single_full_image');
          }

          const firstFrame = newFrameItems[0];
          if (firstFrame) {
            if (activeImageIdRef) activeImageIdRef.current = firstFrame.id;
            try {
              loadSafeImage(firstFrame.url).then((nextImg) => {
                loadedImageRef.current = nextImg;
              }).catch(() => {});
            } catch {}
          }
        }
      }

      setSlicedResults(results);
      setHasExplicitlySliced(true);
      setPreviewDisplayMode('transparent');
      setAssemblySuccess(true);
      setIsProcessing(false);
      onApplyAssembly(updatedAssembly);
      if (threeEngineRef.current) threeEngineRef.current.setAssembly(updatedAssembly);
      if (onSliceSuccess) onSliceSuccess(results);
      redrawCanvas('transparent');
      if (showToast) {
        showToast(
          newFrameItems.length > 0
            ? `✓ Đã bóc tách thành công ${newFrameItems.length} ảnh linh kiện!`
            : `✓ Đã bóc tách thành công (${results.size} ô)!`,
          'redo'
        );
      }
    },
    [
      userUploadedImageUrl,
      loadedImageRef,
      currentAssembly,
      currentCategory,
      colDividers,
      rowDividers,
      paddingInset,
      chromaOptions,
      enableSmartCrop,
      smartCropPadding,
      cellCropRectsRef,
      singleImageSlot,
      singleImageAngle,
      onApplyAssembly,
      threeEngineRef,
      redrawCanvas,
      onSliceSuccess,
      showToast,
      setSlicedResults,
      setHasExplicitlySliced,
      setPreviewDisplayMode,
      setAssemblySuccess,
      setIsProcessing,
      slicedCanvasesRef,
      activeImageIdRef,
      setImageList,
      setCheckedImageIds,
      setSelectedCatId,
    ]
  );

  return {
    handleAutoSliceAndAssemble,
  };
}
