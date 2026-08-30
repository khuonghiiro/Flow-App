// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { useCallback } from 'react';
import { Character2DAssembly } from '../../../../types/scene2d';
import { useSlicerUndoRedo, SlicerHistorySnapshot } from './useSlicerUndoRedo';
import { ThreeMultiAngleBillboardEngine } from '../../../../core/engine2d/ThreeMultiAngleBillboardEngine';

export interface SlicerUndoRedoManagerProps {
  userUploadedImageUrl?: string | null;
  setUserUploadedImageUrl?: (url: string | null) => void;
  selectedCatId?: string;
  setSelectedCatId?: (id: string) => void;
  keyColorType?: 'green' | 'blue' | 'magenta' | 'black' | 'white' | 'custom';
  setKeyColorType?: (t: any) => void;
  keyColorHex?: string;
  setKeyColorHex?: (h: string) => void;
  isolationMode?: 'chroma' | 'flood';
  setIsolationMode?: (m: 'chroma' | 'flood') => void;
  tolerance?: number;
  setTolerance?: (t: number) => void;
  feather?: number;
  setFeather?: (f: number) => void;
  shadowRetention?: number;
  setShadowRetention?: (s: number) => void;
  strokeWidth?: number;
  setStrokeWidth?: (w: number) => void;
  strokeColorHex?: string;
  setStrokeColorHex?: (c: string) => void;
  bgCleanupSubTab?: 'fringe' | 'smooth' | 'despeckle' | 'edges' | 'all';
  setBgCleanupSubTab?: (t: any) => void;
  cleanupMode?: 'soft' | 'aggressive' | 'smart';
  setCleanupMode?: (m: 'soft' | 'aggressive' | 'smart') => void;
  fringeColorType?: 'auto' | 'black' | 'white' | 'custom';
  setFringeColorType?: (t: any) => void;
  fringeColorHex?: string;
  setFringeColorHex?: (h: string) => void;
  defringeStrength?: number;
  setDefringeStrength?: (s: number) => void;
  edgeChoke?: number;
  setEdgeChoke?: (c: number) => void;
  edgeSmooth?: number;
  setEdgeSmooth?: (s: number) => void;
  smoothColorType?: 'matte' | 'black' | 'white' | 'custom';
  setSmoothColorType?: (t: any) => void;
  smoothColorHex?: string;
  setSmoothColorHex?: (h: string) => void;
  despeckleSize?: number;
  setDespeckleSize?: (s: number) => void;
  whiteSpeckleSensitivity?: number;
  setWhiteSpeckleSensitivity?: (s: number) => void;
  darkSpeckleSensitivity?: number;
  setDarkSpeckleSensitivity?: (s: number) => void;
  keepLargestIslandOnly?: boolean;
  setKeepLargestIslandOnly?: (k: boolean) => void;
  paddingInset?: number;
  setPaddingInset?: (p: number) => void;
  colDividers?: number[];
  setColDividers?: (divs: number[]) => void;
  rowDividers?: number[];
  setRowDividers?: (divs: number[]) => void;
  slicedResults?: Map<string, string>;
  setSlicedResults?: React.Dispatch<React.SetStateAction<Map<string, string>>>;
  previewDisplayMode?: 'transparent' | 'original';
  setPreviewDisplayMode?: (mode: 'transparent' | 'original') => void;
  hasExplicitlySliced?: boolean;
  setHasExplicitlySliced?: (sliced: boolean) => void;
  currentAssembly?: Character2DAssembly;
  onApplyAssembly?: (updated: Character2DAssembly) => void;
  threeEngineRef?: React.RefObject<ThreeMultiAngleBillboardEngine | null>;
  checkerTheme?: string;
  setCheckerTheme?: (t: any) => void;
  singleImageSlot?: any;
  setSingleImageSlot?: (s: any) => void;
  singleImageAngle?: any;
  setSingleImageAngle?: (a: any) => void;
  enableSmartCrop?: boolean;
  setEnableSmartCrop?: (e: boolean) => void;
  smartCropPadding?: number;
  setSmartCropPadding?: (p: number) => void;
}

export function useSlicerUndoRedoManager({
  userUploadedImageUrl = null,
  setUserUploadedImageUrl,
  selectedCatId = 'all_parts_8_angles',
  setSelectedCatId,
  keyColorType = 'green',
  setKeyColorType,
  keyColorHex = '#00ff00',
  setKeyColorHex,
  isolationMode = 'chroma',
  setIsolationMode,
  tolerance = 45,
  setTolerance,
  feather = 1.0,
  setFeather,
  shadowRetention = 0,
  setShadowRetention,
  strokeWidth = 0,
  setStrokeWidth,
  strokeColorHex = '#000000',
  setStrokeColorHex,
  bgCleanupSubTab = 'fringe',
  setBgCleanupSubTab,
  cleanupMode = 'soft',
  setCleanupMode,
  fringeColorType = 'auto',
  setFringeColorType,
  fringeColorHex = '#ffffff',
  setFringeColorHex,
  defringeStrength = 0,
  setDefringeStrength,
  edgeChoke = 0,
  setEdgeChoke,
  edgeSmooth = 0,
  setEdgeSmooth,
  smoothColorType = 'matte',
  setSmoothColorType,
  smoothColorHex = '#ffffff',
  setSmoothColorHex,
  despeckleSize = 0,
  setDespeckleSize,
  whiteSpeckleSensitivity = 30,
  setWhiteSpeckleSensitivity,
  keepLargestIslandOnly = false,
  setKeepLargestIslandOnly,
  paddingInset = 0,
  setPaddingInset,
  colDividers = [],
  setColDividers,
  rowDividers = [],
  setRowDividers,
  slicedResults,
  setSlicedResults,
  previewDisplayMode = 'transparent',
  setPreviewDisplayMode,
  hasExplicitlySliced = false,
  setHasExplicitlySliced,
  currentAssembly,
  onApplyAssembly,
  threeEngineRef,
}: SlicerUndoRedoManagerProps) {
  const getCurrentSnapshot = useCallback(
    (label?: string): SlicerHistorySnapshot => ({
      timestamp: Date.now(),
      label,
      userUploadedImageUrl,
      selectedCatId,
      keyColorType: keyColorType as any,
      keyColorHex,
      isolationMode,
      tolerance,
      feather,
      shadowRetention,
      strokeWidth,
      strokeColorHex,
      bgCleanupSubTab: bgCleanupSubTab as any,
      cleanupMode,
      fringeColorType: fringeColorType as any,
      fringeColorHex,
      defringeStrength,
      edgeChoke,
      edgeSmooth,
      smoothColorType: smoothColorType as any,
      smoothColorHex,
      despeckleSize,
      whiteSpeckleSensitivity,
      keepLargestIslandOnly,
      paddingInset,
      colDividers: Array.isArray(colDividers) ? [...colDividers] : [],
      rowDividers: Array.isArray(rowDividers) ? [...rowDividers] : [],
      slicedResults: slicedResults && typeof slicedResults.entries === 'function' ? Array.from(slicedResults.entries()) : [],
      previewDisplayMode,
      hasExplicitlySliced: !!hasExplicitlySliced,
      currentAssembly: currentAssembly ? JSON.parse(JSON.stringify(currentAssembly)) : undefined,
    }),
    [
      userUploadedImageUrl,
      selectedCatId,
      keyColorType,
      keyColorHex,
      isolationMode,
      tolerance,
      feather,
      shadowRetention,
      strokeWidth,
      strokeColorHex,
      bgCleanupSubTab,
      cleanupMode,
      fringeColorType,
      fringeColorHex,
      defringeStrength,
      edgeChoke,
      edgeSmooth,
      smoothColorType,
      smoothColorHex,
      despeckleSize,
      whiteSpeckleSensitivity,
      keepLargestIslandOnly,
      paddingInset,
      colDividers,
      rowDividers,
      slicedResults,
      previewDisplayMode,
      hasExplicitlySliced,
      currentAssembly,
    ]
  );

  const applySnapshot = useCallback(
    (snap: SlicerHistorySnapshot) => {
      if (setUserUploadedImageUrl && snap.userUploadedImageUrl !== userUploadedImageUrl) {
        setUserUploadedImageUrl(snap.userUploadedImageUrl);
      }
      if (setSelectedCatId) setSelectedCatId(snap.selectedCatId);
      if (setKeyColorType) setKeyColorType(snap.keyColorType);
      if (setKeyColorHex) setKeyColorHex(snap.keyColorHex);
      if (setIsolationMode) setIsolationMode(snap.isolationMode);
      if (setTolerance) setTolerance(snap.tolerance);
      if (setFeather) setFeather(snap.feather);
      if (setShadowRetention) setShadowRetention(snap.shadowRetention);
      if (setStrokeWidth) setStrokeWidth(snap.strokeWidth);
      if (setStrokeColorHex) setStrokeColorHex(snap.strokeColorHex);
      if (setBgCleanupSubTab) setBgCleanupSubTab(snap.bgCleanupSubTab);
      if (setCleanupMode) setCleanupMode(snap.cleanupMode);
      if (setFringeColorType) setFringeColorType(snap.fringeColorType);
      if (setFringeColorHex) setFringeColorHex(snap.fringeColorHex);
      if (setDefringeStrength) setDefringeStrength(snap.defringeStrength);
      if (setEdgeChoke) setEdgeChoke(snap.edgeChoke);
      if (setEdgeSmooth) setEdgeSmooth(snap.edgeSmooth);
      if (setSmoothColorType && snap.smoothColorType) setSmoothColorType(snap.smoothColorType);
      if (setSmoothColorHex && snap.smoothColorHex) setSmoothColorHex(snap.smoothColorHex);
      if (setDespeckleSize) setDespeckleSize(snap.despeckleSize);
      if (setWhiteSpeckleSensitivity) setWhiteSpeckleSensitivity(snap.whiteSpeckleSensitivity);
      if (setKeepLargestIslandOnly) setKeepLargestIslandOnly(snap.keepLargestIslandOnly);
      if (setPaddingInset) setPaddingInset(snap.paddingInset);
      if (setColDividers && snap.colDividers) setColDividers(snap.colDividers);
      if (setRowDividers && snap.rowDividers) setRowDividers(snap.rowDividers);
      if (setSlicedResults && snap.slicedResults) setSlicedResults(new Map(snap.slicedResults));
      if (setPreviewDisplayMode) setPreviewDisplayMode(snap.previewDisplayMode);
      if (setHasExplicitlySliced) setHasExplicitlySliced(snap.hasExplicitlySliced);
      if (onApplyAssembly && snap.currentAssembly) onApplyAssembly(snap.currentAssembly);
      if (threeEngineRef?.current && snap.currentAssembly) {
        threeEngineRef.current.setAssembly(snap.currentAssembly);
      }
    },
    [
      userUploadedImageUrl,
      onApplyAssembly,
      setColDividers,
      setRowDividers,
      setSlicedResults,
      setPreviewDisplayMode,
      setHasExplicitlySliced,
      setKeyColorType,
      setKeyColorHex,
      setIsolationMode,
      setTolerance,
      setFeather,
      setShadowRetention,
      setStrokeWidth,
      setStrokeColorHex,
      setBgCleanupSubTab,
      setCleanupMode,
      setFringeColorType,
      setFringeColorHex,
      setDefringeStrength,
      setEdgeChoke,
      setEdgeSmooth,
      setSmoothColorType,
      setSmoothColorHex,
      setDespeckleSize,
      setWhiteSpeckleSensitivity,
      setKeepLargestIslandOnly,
      setPaddingInset,
      setSelectedCatId,
      threeEngineRef,
      setUserUploadedImageUrl,
    ]
  );

  const { undoStack, redoStack, historyToast, showToast, pushUndoState, handleUndo, handleRedo } = useSlicerUndoRedo({
    getCurrentSnapshot,
    applySnapshot,
  });

  return {
    undoStack,
    redoStack,
    historyToast,
    showToast,
    pushUndoState,
    handleUndo,
    handleRedo,
  };
}
