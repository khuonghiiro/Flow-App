import { useState, useRef, useCallback } from 'react';
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

export interface UseSlicerSlicingPipelineProps {
  loadedImageRef: React.MutableRefObject<HTMLImageElement | null>;
  currentAssembly: Character2DAssembly;
  currentCategory: GridCategoryDefinition;
  colDividers: number[];
  rowDividers: number[];
  keyColorType: 'chroma_green' | 'pure_white' | 'custom';
  keyColorHex: string;
  isolationMode: 'all' | 'outer_only';
  tolerance: number;
  feather: number;
  shadowRetention: number;
  strokeWidth: number;
  strokeColorHex: string;
  despeckleSize: number;
  whiteSpeckleSensitivity: number;
  keepLargestIslandOnly: boolean;
  fringeColorType: 'chroma_green' | 'pure_white' | 'pure_black' | 'custom';
  fringeColorHex: string;
  defringeStrength: number;
  edgeChoke: number;
  edgeSmooth: number;
  smoothColorType: 'black' | 'white' | 'auto' | 'custom';
  smoothColorHex: string;
  cleanupMode: 'all' | 'defringe' | 'smooth' | 'despeckle';
  paddingInset: number;
  enableSmartCrop?: boolean;
  smartCropPadding?: number;
  cellCropRectsRef?: React.MutableRefObject<Map<string, PaddedCropRect>>;
  singleImageSlot: Character2DPartType;
  singleImageAngle: Character2DAngle;
  onApplyAssembly: (updated: Character2DAssembly) => void;
  threeEngineRef: React.RefObject<ThreeMultiAngleBillboardEngine | null>;
  redrawCanvas: (modeOverride?: 'transparent' | 'original') => void;
  onSliceSuccess?: (results: Map<string, string>) => void;
}

export function useSlicerSlicingPipeline({
  loadedImageRef,
  currentAssembly,
  currentCategory,
  colDividers,
  rowDividers,
  keyColorType,
  keyColorHex,
  isolationMode,
  tolerance,
  feather,
  shadowRetention,
  strokeWidth,
  strokeColorHex,
  despeckleSize,
  whiteSpeckleSensitivity,
  keepLargestIslandOnly,
  fringeColorType,
  fringeColorHex,
  defringeStrength,
  edgeChoke,
  edgeSmooth,
  smoothColorType,
  smoothColorHex,
  cleanupMode,
  paddingInset,
  enableSmartCrop = false,
  smartCropPadding = 2,
  cellCropRectsRef,
  singleImageSlot,
  singleImageAngle,
  onApplyAssembly,
  threeEngineRef,
  redrawCanvas,
  onSliceSuccess,
}: UseSlicerSlicingPipelineProps) {
  const [slicedResults, setSlicedResults] = useState<Map<string, string>>(new Map());
  const [hasExplicitlySliced, setHasExplicitlySliced] = useState<boolean>(false);
  const [isProcessing, setIsProcessing] = useState<boolean>(false);
  const [assemblySuccess, setAssemblySuccess] = useState<boolean>(false);
  const [previewDisplayMode, setPreviewDisplayMode] = useState<'transparent' | 'original'>('original');
  const slicedCanvasesRef = useRef<Map<string, HTMLCanvasElement>>(new Map());

  const handleAutoSliceAndAssemble = useCallback(
    (overrides?: Partial<ChromaProcessOptions>) => {
      const img = loadedImageRef.current;
      if (!img) return;

      setIsProcessing(true);
      setTimeout(() => {
        const opts: ChromaProcessOptions = {
          keyColorType: overrides?.keyColorType ?? keyColorType,
          keyColorHex: overrides?.keyColorHex ?? keyColorHex,
          isolationMode: overrides?.isolationMode ?? isolationMode,
          tolerance: overrides?.tolerance ?? tolerance,
          feather: overrides?.feather ?? feather,
          shadowRetention: overrides?.shadowRetention ?? shadowRetention,
          strokeWidth: overrides?.strokeWidth ?? strokeWidth,
          strokeColorHex: overrides?.strokeColorHex ?? strokeColorHex,
          despeckleSize: overrides?.despeckleSize ?? despeckleSize,
          whiteSpeckleSensitivity: overrides?.whiteSpeckleSensitivity ?? whiteSpeckleSensitivity,
          keepLargestIslandOnly: overrides?.keepLargestIslandOnly ?? keepLargestIslandOnly,
          fringeColorType: overrides?.fringeColorType ?? fringeColorType,
          fringeColorHex: overrides?.fringeColorHex ?? fringeColorHex,
          defringeStrength: overrides?.defringeStrength ?? defringeStrength,
          edgeChoke: overrides?.edgeChoke ?? edgeChoke,
          edgeSmooth: overrides?.edgeSmooth ?? edgeSmooth,
          smoothColorType: overrides?.smoothColorType ?? smoothColorType,
          smoothColorHex: overrides?.smoothColorHex ?? smoothColorHex,
          cleanupMode: overrides?.cleanupMode ?? cleanupMode,
        };

        const pad = Math.max(0, paddingInset);
        const results = new Map<string, string>();
        const updatedAssembly: Character2DAssembly = JSON.parse(JSON.stringify(currentAssembly));

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
          }
        } else {
          currentCategory.cells.forEach((cell) => {
            if (colDividers.length <= cell.col + 1 || rowDividers.length <= cell.row + 1) return;
            const rawX0 = colDividers[cell.col];
            const rawY0 = rowDividers[cell.row];
            const x0 = rawX0 + pad;
            const y0 = rawY0 + pad;
            const w = Math.max(10, colDividers[cell.col + 1] - rawX0 - pad * 2);
            const h = Math.max(10, rowDividers[cell.row + 1] - rawY0 - pad * 2);

            const canvas = document.createElement('canvas');
            canvas.width = w;
            canvas.height = h;
            const ctx = canvas.getContext('2d', { willReadFrequently: true });
            if (ctx) {
              ctx.drawImage(img, x0, y0, w, h, 0, 0, w, h);
              processCellChromaAndDespeckle(ctx, w, h, opts);

              const key = `${cell.row}_${cell.col}`;
              let finalCanvas = canvas;
              let dataUrl = canvas.toDataURL('image/png');

              if (enableSmartCrop) {
                const bbox = detectPixelContentBoundingBox(ctx, w, h, 10);
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
      }, 20);
    },
    [
      loadedImageRef,
      currentAssembly,
      currentCategory,
      colDividers,
      rowDividers,
      keyColorType,
      keyColorHex,
      isolationMode,
      tolerance,
      feather,
      shadowRetention,
      strokeWidth,
      strokeColorHex,
      despeckleSize,
      whiteSpeckleSensitivity,
      keepLargestIslandOnly,
      fringeColorType,
      fringeColorHex,
      defringeStrength,
      edgeChoke,
      edgeSmooth,
      smoothColorType,
      smoothColorHex,
      cleanupMode,
      paddingInset,
      enableSmartCrop,
      smartCropPadding,
      cellCropRectsRef,
      singleImageSlot,
      singleImageAngle,
      onApplyAssembly,
      threeEngineRef,
      redrawCanvas,
      onSliceSuccess,
    ]
  );

  return {
    slicedResults,
    setSlicedResults,
    slicedCanvasesRef,
    hasExplicitlySliced,
    setHasExplicitlySliced,
    isProcessing,
    setIsProcessing,
    assemblySuccess,
    setAssemblySuccess,
    previewDisplayMode,
    setPreviewDisplayMode,
    handleAutoSliceAndAssemble,
  };
}
