// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { useState, useCallback } from 'react';
import { Character2DAngle, Character2DPartType } from '../../../../types/scene2d';
import { GRID_CATEGORY_DEFINITIONS, GridCategoryDefinition, GridCellDefinition } from '../../../../core/assets/GridSliceRegistry';
import {
  loadCachedSingleMode,
  saveCachedSingleMode,
  loadCachedGridConfig,
  saveCachedGridConfig,
  createDynamicGridCategory,
} from '../../../../core/assets/slicer/SlicerAngleConstants';

export interface UseSlicerCategoryStateProps {
  externalCategoryId?: string;
  loadedImageRef?: React.MutableRefObject<HTMLImageElement | null>;
  loadedImage?: HTMLImageElement | null;
  initUniformDividers?: (width: number, height: number, cols: number, rows: number) => void;
  setSelectedCell?: (cell: GridCellDefinition | null) => void;
  setHasExplicitlySliced?: (sliced: boolean) => void;
  slicedCanvasesRef?: React.MutableRefObject<Map<string, HTMLCanvasElement>>;
  setSlicedResults?: (results: Map<string, string>) => void;
  setPreviewDisplayMode?: (mode: 'transparent' | 'original') => void;
  redrawCanvas?: (mode?: 'transparent' | 'original') => void;
}

export function useSlicerCategoryState({
  externalCategoryId,
  loadedImageRef,
  loadedImage,
  initUniformDividers,
  setSelectedCell,
  setHasExplicitlySliced,
  slicedCanvasesRef,
  setSlicedResults,
  setPreviewDisplayMode,
  redrawCanvas,
}: UseSlicerCategoryStateProps = {}) {
  const [lastUsedGridCatId, setLastUsedGridCatId] = useState<string>(() => {
    const cachedGrid = loadCachedGridConfig();
    return cachedGrid ? `custom_grid_${cachedGrid.rows}x${cachedGrid.cols}` : externalCategoryId || 'cinematic_single_part_2x3';
  });

  const [selectedCatId, setSelectedCatId] = useState<string>(() => {
    if (loadCachedSingleMode()) return 'single_full_image';
    if (externalCategoryId) return externalCategoryId;
    const cachedGrid = loadCachedGridConfig();
    return cachedGrid ? `custom_grid_${cachedGrid.rows}x${cachedGrid.cols}` : 'cinematic_single_part_2x3';
  });

  const [targetCategory, setTargetCategory] = useState<string>(() => {
    try {
      return localStorage.getItem('flowmy_slicer_target_category') || 'character';
    } catch {
      return 'character';
    }
  });

  const handleSelectTargetCategory = useCallback((cat: string) => {
    setTargetCategory(cat);
    try {
      localStorage.setItem('flowmy_slicer_target_category', cat);
    } catch {}
  }, []);

  const [customCategory, setCustomCategory] = useState<GridCategoryDefinition | null>(() => {
    const cached = loadCachedGridConfig();
    return cached ? createDynamicGridCategory(cached.rows, cached.cols) : null;
  });

  const [singleImageAngle, setSingleImageAngle] = useState<Character2DAngle>('front');
  const [singleImageSlot, setSingleImageSlot] = useState<Character2DPartType>('than_co_ban');

  const currentCategory: GridCategoryDefinition =
    customCategory && selectedCatId === customCategory.id
      ? customCategory
      : GRID_CATEGORY_DEFINITIONS.find((c) => c.id === selectedCatId) || GRID_CATEGORY_DEFINITIONS[0];

  const handleSelectCatId = useCallback(
    (newCatId: string) => {
      setSelectedCatId(newCatId);
      if (newCatId !== 'single_full_image') {
        setLastUsedGridCatId(newCatId);
        saveCachedSingleMode(false);
      } else {
        saveCachedSingleMode(true);
      }
      const cat =
        customCategory && customCategory.id === newCatId
          ? customCategory
          : GRID_CATEGORY_DEFINITIONS.find((c) => c.id === newCatId) || GRID_CATEGORY_DEFINITIONS[0];
      const img = loadedImage || loadedImageRef?.current;
      const w = img?.naturalWidth || img?.width || 800;
      const h = img?.naturalHeight || img?.height || 600;
      if (initUniformDividers) {
        initUniformDividers(w, h, cat.cols, cat.rows);
      }
      if (setSelectedCell) setSelectedCell(null);
      if (setHasExplicitlySliced) setHasExplicitlySliced(false);
      if (slicedCanvasesRef?.current) slicedCanvasesRef.current.clear();
      if (setSlicedResults) setSlicedResults(new Map());
      if (setPreviewDisplayMode) setPreviewDisplayMode('original');
      if (redrawCanvas) redrawCanvas('original');
    },
    [loadedImage, loadedImageRef, initUniformDividers, customCategory, setSelectedCell, setHasExplicitlySliced, slicedCanvasesRef, setSlicedResults, setPreviewDisplayMode, redrawCanvas]
  );

  const handleToggleSingleImageMode = useCallback(() => {
    if (selectedCatId === 'single_full_image') {
      const targetCatId =
        customCategory?.id ||
        (lastUsedGridCatId !== 'single_full_image' ? lastUsedGridCatId : 'cinematic_single_part_2x3');
      saveCachedSingleMode(false);
      handleSelectCatId(targetCatId);
    } else {
      setLastUsedGridCatId(selectedCatId);
      saveCachedSingleMode(true);
      handleSelectCatId('single_full_image');
    }
  }, [selectedCatId, customCategory, lastUsedGridCatId, handleSelectCatId]);

  const handleSelectGridMatrix = useCallback(
    (rows: number, cols: number) => {
      const newCat = createDynamicGridCategory(rows, cols);
      setCustomCategory(newCat);
      setLastUsedGridCatId(newCat.id);
      saveCachedSingleMode(false);
      saveCachedGridConfig(rows, cols);
      setSelectedCatId(newCat.id);
      const img = loadedImage || loadedImageRef?.current;
      const w = img?.naturalWidth || img?.width || 800;
      const h = img?.naturalHeight || img?.height || 600;
      if (initUniformDividers) {
        initUniformDividers(w, h, newCat.cols, newCat.rows);
      }
      if (setSelectedCell) setSelectedCell(null);
      if (setHasExplicitlySliced) setHasExplicitlySliced(false);
      if (slicedCanvasesRef?.current) slicedCanvasesRef.current.clear();
      if (setSlicedResults) setSlicedResults(new Map());
      if (setPreviewDisplayMode) setPreviewDisplayMode('original');
      if (redrawCanvas) redrawCanvas('original');
    },
    [loadedImage, loadedImageRef, initUniformDividers, setSelectedCell, setHasExplicitlySliced, slicedCanvasesRef, setSlicedResults, setPreviewDisplayMode, redrawCanvas]
  );

  return {
    lastUsedGridCatId,
    setLastUsedGridCatId,
    selectedCatId,
    setSelectedCatId,
    targetCategory,
    setTargetCategory,
    handleSelectTargetCategory,
    customCategory,
    setCustomCategory,
    singleImageAngle,
    setSingleImageAngle,
    singleImageSlot,
    setSingleImageSlot,
    currentCategory,
    handleSelectCatId,
    handleToggleSingleImageMode,
    handleSelectGridMatrix,
  };
}
