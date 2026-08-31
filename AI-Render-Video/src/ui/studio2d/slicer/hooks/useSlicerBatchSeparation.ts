// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { useState, useCallback } from 'react';
import { Character2DAssembly, Character2DPartType, Character2DAngle } from '../../../../types/scene2d';
import { GridCategoryDefinition } from '../../../../core/assets/GridSliceRegistry';
import { ChromaProcessOptions, processCellChromaAndDespeckle } from '../../../../core/utils/ChromaDespeckleProcessor';
import { SlicerUploadedImageItem, detectAspectRatioLabel } from './useSlicerMultiImageGallery';
import { ThreeMultiAngleBillboardEngine } from '../../../../core/engine2d/ThreeMultiAngleBillboardEngine';
import { PART_HIERARCHY_CONFIG } from '../../../../core/assets/Asset2DRegistry';
import { cleanOuterEdgeDarkBorders } from '../utils/slicerPixelEraserHelper';
import { loadSafeImage } from '../utils/slicerImageLoaderHelper';

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
  pushUndoState: (actionName: string) => void;
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
  setCheckedImageIds?: React.Dispatch<React.SetStateAction<Set<string>>>;
  setSelectedCatId?: (id: string) => void;
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
  setCheckedImageIds,
  setSelectedCatId,
}: UseSlicerBatchSeparationProps) {
  const [isBatchProcessing, setIsBatchProcessing] = useState<boolean>(false);

  const handleBatchSeparateImages = useCallback(
    async (imageIds: string[], overrideChromaOpts?: Partial<ChromaProcessOptions>) => {
      if (imageIds.length === 0) return;
      setIsBatchProcessing(true);
      pushUndoState(`Tách nền & cắt lưới ${imageIds.length} ảnh`);

      const effectiveOptions: ChromaProcessOptions = {
        ...chromaOptions,
        ...(overrideChromaOpts || {}),
      };

      const isMultiCellGrid =
        currentCategory.id !== 'single_full_image' &&
        currentCategory.cells &&
        currentCategory.cells.length > 1;

      const newFrameItems: SlicerUploadedImageItem[] = [];
      const updatedMap = new Map<string, string>();
      const sliceResultsMap = new Map<string, string>();
      const updatedAssembly: Character2DAssembly = JSON.parse(JSON.stringify(currentAssembly));
      const pad = Math.max(0, paddingInset);

      try {
        for (const id of imageIds) {
          const item = imageList.find((it) => it.id === id);
          if (!item) continue;

          try {
            // Case 1: Item is an already-sliced frame item -> Re-slice cleanly from parent sprite sheet
            if (item.isFrameItem && item.parentSheetOriginalUrl && item.cellCol !== undefined && item.cellRow !== undefined) {
              const parentImg = await loadSafeImage(item.parentSheetOriginalUrl);
              const parentW = parentImg.naturalWidth || parentImg.width;
              const parentH = parentImg.naturalHeight || parentImg.height;
              const numCols = item.parentNumCols || Math.max(1, currentCategory.cols || 4);
              const numRows = item.parentNumRows || Math.max(1, currentCategory.rows || 1);

              const normX0 = item.cellCol / numCols;
              const normX1 = (item.cellCol + 1) / numCols;
              const normY0 = item.cellRow / numRows;
              const normY1 = (item.cellRow + 1) / numRows;

              const rawX0 = Math.round(normX0 * parentW);
              const rawX1 = Math.round(normX1 * parentW);
              const rawY0 = Math.round(normY0 * parentH);
              const rawY1 = Math.round(normY1 * parentH);

              const rawW = rawX1 - rawX0;
              const rawH = rawY1 - rawY0;
              const safePadX = Math.min(pad, Math.floor(rawW / 2.2));
              const safePadY = Math.min(pad, Math.floor(rawH / 2.2));

              const cellW = Math.max(10, rawW - safePadX * 2);
              const cellH = Math.max(10, rawH - safePadY * 2);

              const cellCanvas = document.createElement('canvas');
              cellCanvas.width = cellW;
              cellCanvas.height = cellH;
              const cCtx = cellCanvas.getContext('2d', { willReadFrequently: true });
              if (cCtx) {
                cCtx.drawImage(parentImg, rawX0 + safePadX, rawY0 + safePadY, cellW, cellH, 0, 0, cellW, cellH);
                processCellChromaAndDespeckle(cCtx, cellW, cellH, effectiveOptions);
                cleanOuterEdgeDarkBorders(cCtx, cellW, cellH, Math.max(3, Math.min(6, safePadX)));
                const cellDataUrl = cellCanvas.toDataURL('image/png');
                updatedMap.set(id, cellDataUrl);
                sliceResultsMap.set(`${item.cellRow}_${item.cellCol}`, cellDataUrl);
                slicedCanvasesRef.current.set(`${item.cellRow}_${item.cellCol}`, cellCanvas);
              }
              continue;
            }

            // Case 2: Standard Sprite sheet or single image
            const img = await loadSafeImage(item.originalUrl || item.url);
            const w = img.naturalWidth || img.width;
            const h = img.naturalHeight || img.height;

            if (isMultiCellGrid) {
              const numCols = Math.max(1, currentCategory.cols || 4);
              const numRows = Math.max(1, currentCategory.rows || 1);

              const sortedCells = [...currentCategory.cells].sort((a, b) => {
                if (a.row !== b.row) return a.row - b.row;
                return a.col - b.col;
              });

              const itemCols =
                item.customColDividers && item.customColDividers.length === numCols + 1 && !item.customColDividers.some(isNaN)
                  ? item.customColDividers
                  : colDividers && colDividers.length === numCols + 1 && !colDividers.some(isNaN)
                  ? colDividers
                  : Array.from({ length: numCols + 1 }, (_, i) => Math.round((i * w) / numCols));

              const itemRows =
                item.customRowDividers && item.customRowDividers.length === numRows + 1 && !item.customRowDividers.some(isNaN)
                  ? item.customRowDividers
                  : rowDividers && rowDividers.length === numRows + 1 && !rowDividers.some(isNaN)
                  ? rowDividers
                  : Array.from({ length: numRows + 1 }, (_, i) => Math.round((i * h) / numRows));

              const refW = itemCols[itemCols.length - 1] || w;
              const refH = itemRows[itemRows.length - 1] || h;

              sortedCells.forEach((cell, idx) => {
                let normX0 = cell.col / numCols;
                let normX1 = (cell.col + 1) / numCols;
                let normY0 = cell.row / numRows;
                let normY1 = (cell.row + 1) / numRows;

                if (itemCols.length > cell.col + 1 && refW > 0) {
                  normX0 = itemCols[cell.col] / refW;
                  normX1 = itemCols[cell.col + 1] / refW;
                }
                if (itemRows.length > cell.row + 1 && refH > 0) {
                  normY0 = itemRows[cell.row] / refH;
                  normY1 = itemRows[cell.row + 1] / refH;
                }

                const rawX0 = Math.round(normX0 * w);
                const rawX1 = Math.round(normX1 * w);
                const rawY0 = Math.round(normY0 * h);
                const rawY1 = Math.round(normY1 * h);

                const rawW = rawX1 - rawX0;
                const rawH = rawY1 - rawY0;
                const safePadX = Math.min(pad, Math.floor(rawW / 2.2));
                const safePadY = Math.min(pad, Math.floor(rawH / 2.2));

                const cellW = Math.max(10, rawW - safePadX * 2);
                const cellH = Math.max(10, rawH - safePadY * 2);

                const cellCanvas = document.createElement('canvas');
                cellCanvas.width = cellW;
                cellCanvas.height = cellH;
                const cCtx = cellCanvas.getContext('2d', { willReadFrequently: true });
                if (cCtx) {
                  cCtx.drawImage(img, rawX0 + safePadX, rawY0 + safePadY, cellW, cellH, 0, 0, cellW, cellH);
                  processCellChromaAndDespeckle(cCtx, cellW, cellH, effectiveOptions);
                  cleanOuterEdgeDarkBorders(cCtx, cellW, cellH, Math.max(3, Math.min(6, safePadX)));
                  const cellDataUrl = cellCanvas.toDataURL('image/png');
                  const key = `${cell.row}_${cell.col}`;

                  sliceResultsMap.set(key, cellDataUrl);
                  slicedCanvasesRef.current.set(key, cellCanvas);

                  const frameNumber = idx + 1;
                  const baseName = item.name.replace(/\.[^/.]+$/, '');
                  const frameLabel = cell.partSlot ? cell.partSlot : `Frame_${String(frameNumber).padStart(2, '0')}`;
                  const frameItem: SlicerUploadedImageItem = {
                    id: `${item.id}_f${frameNumber}_${Date.now()}_${idx}`,
                    name: `${baseName}_${frameLabel}.png`,
                    url: cellDataUrl,
                    originalUrl: cellDataUrl,
                    transparentUrl: cellDataUrl,
                    isTransparentSeparated: true,
                    isFrameItem: true,
                    parentSheetOriginalUrl: item.originalUrl || item.url,
                    cellRow: cell.row,
                    cellCol: cell.col,
                    parentNumCols: numCols,
                    parentNumRows: numRows,
                    filterConfig: { ...effectiveOptions },
                    metadata: cell.partSlot ? { ...item.metadata, part_id: cell.partSlot } : item.metadata,
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
                        path: cellDataUrl,
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
                    if (cell.angle === 'front' || cell.col === 0) part.path = cellDataUrl;
                    if (cell.angle) {
                      if (!part.angles) part.angles = {};
                      part.angles[cell.angle] = cellDataUrl;
                    }
                  }
                }
              });
            } else {
              // Single full image
              const cellCanvas = document.createElement('canvas');
              cellCanvas.width = Math.max(10, w - pad * 2);
              cellCanvas.height = Math.max(10, h - pad * 2);
              const cCtx = cellCanvas.getContext('2d', { willReadFrequently: true });
              if (cCtx) {
                cCtx.drawImage(img, pad, pad, cellCanvas.width, cellCanvas.height, 0, 0, cellCanvas.width, cellCanvas.height);
                processCellChromaAndDespeckle(cCtx, cellCanvas.width, cellCanvas.height, effectiveOptions);
                cleanOuterEdgeDarkBorders(cCtx, cellCanvas.width, cellCanvas.height, Math.max(3, Math.min(6, pad)));
                const dataUrl = cellCanvas.toDataURL('image/png');

                updatedMap.set(id, dataUrl);
                sliceResultsMap.set('0_0', dataUrl);
                slicedCanvasesRef.current.set('0_0', cellCanvas);

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
              }
            }
          } catch (err) {
            console.warn('Failed to separate background for item:', id, err);
          }
        }

        // Update image list: If multi-cell grid, expand into sliced frame items!
        if (isMultiCellGrid && newFrameItems.length > 0) {
          const processedSet = new Set(imageIds);
          setImageList((prev) => {
            const nextList: SlicerUploadedImageItem[] = [];
            for (const it of prev) {
              if (processedSet.has(it.id)) {
                const frames = newFrameItems.filter((f) => f.parentSheetOriginalUrl === (it.originalUrl || it.url) || f.id.startsWith(it.id));
                if (frames.length > 0) {
                  nextList.push(...frames);
                } else {
                  nextList.push(it);
                }
              } else {
                nextList.push(it);
              }
            }
            return nextList.length > 0 ? nextList : newFrameItems;
          });

          if (setCheckedImageIds) {
            setCheckedImageIds(new Set(newFrameItems.map((f) => f.id)));
          }

          if (setSelectedCatId) {
            setSelectedCatId('single_full_image');
          }

          const firstFrame = newFrameItems[0];
          if (firstFrame) {
            activeImageIdRef.current = firstFrame.id;
            try {
              const nextImg = await loadSafeImage(firstFrame.url);
              loadedImageRef.current = nextImg;
              setLoadedImage(nextImg);
              setUserUploadedImageUrl(firstFrame.url);
            } catch {}
          }
        } else if (updatedMap.size > 0) {
          setImageList((prev) =>
            prev.map((it) => {
              const newUrl = updatedMap.get(it.id);
              if (newUrl) {
                return {
                  ...it,
                  url: newUrl,
                  transparentUrl: newUrl,
                  isTransparentSeparated: true,
                  filterConfig: { ...effectiveOptions },
                };
              }
              return it;
            })
          );
        }

        if (sliceResultsMap.size > 0) {
          setSlicedResults(sliceResultsMap);
        }

        setPreviewDisplayMode('transparent');
        setHasExplicitlySliced(true);
        onApplyAssembly(updatedAssembly);
        if (threeEngineRef.current) threeEngineRef.current.setAssembly(updatedAssembly);
        redrawCanvas('transparent');
        showToast(
          isMultiCellGrid && newFrameItems.length > 0
            ? `✓ Đã bóc tách thành công ${newFrameItems.length} ảnh linh kiện!`
            : `✓ Đã tách nền thành công ${imageIds.length} ảnh!`,
          'redo'
        );
      } catch (err) {
        console.error('Error during batch separation:', err);
      } finally {
        setIsBatchProcessing(false);
      }
    },
    [
      imageList,
      setImageList,
      currentCategory,
      colDividers,
      rowDividers,
      paddingInset,
      chromaOptions,
      activeImageIdRef,
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
      currentAssembly,
      onApplyAssembly,
      threeEngineRef,
      singleImageSlot,
      singleImageAngle,
      setCheckedImageIds,
      setSelectedCatId,
    ]
  );

  return {
    isBatchProcessing,
    handleBatchSeparateImages,
  };
}
