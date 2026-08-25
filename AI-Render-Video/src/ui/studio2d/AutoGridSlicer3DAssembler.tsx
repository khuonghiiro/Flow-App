import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Character2DAssembly, Character2DAngle, Character2DPartType } from '../../types/scene2d';
import { GRID_CATEGORY_DEFINITIONS, GridCategoryDefinition, GridCellDefinition } from '../../core/assets/GridSliceRegistry';
import { ThreeMultiAngleBillboardEngine, AngleDetectionResult } from '../../core/engine2d/ThreeMultiAngleBillboardEngine';
import { CellPixelEraserModal } from './CellPixelEraserModal';
import { CharacterAssetCatalogModal } from './CharacterAssetCatalogModal';
import { MultiAngleTunerModal } from './MultiAngleTunerModal';
import { parsePartFilename, ParsedPartFilenameInfo } from '../../core/assets/Asset2DRegistry';
import { GridTablePickerModal } from './slicer/GridTablePickerModal';
import { JsonPromptImportModal, ParsedJsonMetadataItem } from './slicer/JsonPromptImportModal';
import { SlicerSaveKitModal } from './slicer/modals/SlicerSaveKitModal';
import { processCellChromaAndDespeckle } from '../../core/utils/ChromaDespeckleProcessor';
import {
  PaddedCropRect,
  detectImageBBoxRect,
  detectPixelContentBoundingBox,
  cropCanvasWithPadding,
} from '../../core/utils/PixelBoundingBoxAlgorithms';
import {
  CheckerboardTheme,
  loadCachedCheckerTheme,
  saveCachedCheckerTheme,
  loadCachedSingleMode,
  saveCachedSingleMode,
  loadCachedGridConfig,
  createDynamicGridCategory,
  STANDARD_ANGLE_DEFINITIONS,
  getAngleDefinitionById,
} from '../../core/assets/slicer/SlicerAngleConstants';

// Subcomponents & Custom Hooks
import { SlicerSidebarControls } from './slicer/SlicerSidebarControls';
import { SlicerCellAdjustmentBar } from './slicer/SlicerCellAdjustmentBar';
import { SlicerInteractiveCanvas } from './slicer/SlicerInteractiveCanvas';
import { Slicer3DTurntablePreview } from './slicer/Slicer3DTurntablePreview';
import { SlicerVerticalGalleryColumn } from './slicer/SlicerVerticalGalleryColumn';
import { useSlicerDividers } from './slicer/hooks/useSlicerDividers';
import { useSlicerUndoRedo, SlicerHistorySnapshot } from './slicer/hooks/useSlicerUndoRedo';
import { useSlicerCanvasDrawing } from './slicer/hooks/useSlicerCanvasDrawing';
import { useSlicerSlicingPipeline } from './slicer/hooks/useSlicerSlicingPipeline';
import { useSlicerAIMatting } from './slicer/hooks/useSlicerAIMatting';
import { useSlicerCanvasInteraction } from './slicer/hooks/useSlicerCanvasInteraction';
import { useSlicerMultiImageGallery, SlicerUploadedImageItem, detectAspectRatioLabel } from './slicer/hooks/useSlicerMultiImageGallery';

interface AutoGridSlicer3DAssemblerProps {
  currentAssembly: Character2DAssembly;
  onApplyAssembly: (updatedAssembly: Character2DAssembly) => void;
  onSwitchToAssemblyTab?: () => void;
  externalImageUrl?: string | null;
  externalCategoryId?: string;
}

export const AutoGridSlicer3DAssembler: React.FC<AutoGridSlicer3DAssemblerProps> = ({
  currentAssembly,
  onApplyAssembly,
  onSwitchToAssemblyTab,
  externalImageUrl,
  externalCategoryId,
}) => {
  // Category & Single Mode State
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

  // Filter & Color Tuning States
  const [keyColorType, setKeyColorType] = useState<'chroma_green' | 'pure_white' | 'custom'>('chroma_green');
  const [keyColorHex, setKeyColorHex] = useState<string>('#00ff00');
  const [isEyedropperActive, setIsEyedropperActive] = useState<boolean>(false);
  const [eyedropperHoverColor, setEyedropperHoverColor] = useState<{ hex: string; r: number; g: number; b: number; x: number; y: number } | null>(null);
  const [isolationMode, setIsolationMode] = useState<'all' | 'outer_only'>('all');
  const [tolerance, setTolerance] = useState<number>(1);
  const [feather, setFeather] = useState<number>(0);
  const [shadowRetention, setShadowRetention] = useState<number>(100);
  const [strokeWidth, setStrokeWidth] = useState<number>(0);
  const [strokeColorHex, setStrokeColorHex] = useState<string>('#000000');
  const [bgCleanupSubTab, setBgCleanupSubTab] = useState<'chroma' | 'despeckle' | 'ai_matting'>('chroma');
  const [aiModel, setAiModel] = useState<string>('birefnet-general');
  const [aiScope, setAiScope] = useState<'full_image' | 'all' | 'selected'>('full_image');
  const [aiServerStatus, setAiServerStatus] = useState<'online' | 'offline' | 'checking'>('checking');
  const [despeckleSize, setDespeckleSize] = useState<number>(0);
  const [whiteSpeckleSensitivity, setWhiteSpeckleSensitivity] = useState<number>(0);
  const [keepLargestIslandOnly, setKeepLargestIslandOnly] = useState<boolean>(false);

  // Defringe & Edge Smoothing states
  const [eyedropperTarget, setEyedropperTarget] = useState<'chroma' | 'fringe' | 'smooth'>('chroma');
  const [cleanupMode, setCleanupMode] = useState<'all' | 'defringe' | 'smooth' | 'despeckle'>('all');
  const [fringeColorType, setFringeColorType] = useState<'chroma_green' | 'pure_white' | 'pure_black' | 'custom'>('chroma_green');
  const [fringeColorHex, setFringeColorHex] = useState<string>('#00ff00');
  const [defringeStrength, setDefringeStrength] = useState<number>(0);
  const [edgeChoke, setEdgeChoke] = useState<number>(0);
  const [edgeSmooth, setEdgeSmooth] = useState<number>(0);
  const [smoothColorType, setSmoothColorType] = useState<'black' | 'white' | 'auto' | 'custom'>('black');
  const [smoothColorHex, setSmoothColorHex] = useState<string>('#000000');
  const [paddingInset, setPaddingInset] = useState<number>(0);

  // Image & Selection
  const [userUploadedImageUrl, setUserUploadedImageUrl] = useState<string | null>(null);
  const [uploadedFileMetadata, setUploadedFileMetadata] = useState<ParsedPartFilenameInfo | null>(null);
  const [loadedImage, setLoadedImage] = useState<HTMLImageElement | null>(null);
  const [selectedCell, setSelectedCell] = useState<GridCellDefinition | null>(null);

  // 3D Engine State
  const [activeAngleInfo, setActiveAngleInfo] = useState<AngleDetectionResult>({
    angleDeg: 0,
    discreteAngle: 'front',
    angleLabel: 'Chính diện (Front 0°)',
    compassDirection: 'S',
  });
  const [turntableAngle, setTurntableAngle] = useState<number>(0);
  const [timeOfDay, setTimeOfDay] = useState<number>(0);

  // Refs & Modals
  const imageCanvasRef = useRef<HTMLCanvasElement>(null);
  const threeContainerRef = useRef<HTMLDivElement>(null);
  const threeEngineRef = useRef<ThreeMultiAngleBillboardEngine | null>(null);
  const loadedImageRef = useRef<HTMLImageElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [checkerTheme, setCheckerTheme] = useState<CheckerboardTheme>(loadCachedCheckerTheme);
  const [customCategory, setCustomCategory] = useState<GridCategoryDefinition | null>(() => {
    const cached = loadCachedGridConfig();
    return cached ? createDynamicGridCategory(cached.rows, cached.cols) : null;
  });
  const [isTablePickerOpen, setIsTablePickerOpen] = useState<boolean>(false);
  const [isJsonImportOpen, setIsJsonImportOpen] = useState<boolean>(false);
  const [singleImageAngle, setSingleImageAngle] = useState<Character2DAngle>('front');
  const [singleImageSlot, setSingleImageSlot] = useState<Character2DPartType>('than_co_ban');
  const [isEraserOpen, setIsEraserOpen] = useState<boolean>(false);
  const [editingCellDef, setEditingCellDef] = useState<GridCellDefinition | null>(null);
  const [editingCellOriginalDataUrl, setEditingCellOriginalDataUrl] = useState<string>('');
  const [isTunerOpen, setIsTunerOpen] = useState<boolean>(false);
  const [isCatalogOpen, setIsCatalogOpen] = useState<boolean>(false);
  const [isSaveKitModalOpen, setIsSaveKitModalOpen] = useState<boolean>(false);

  // Direct Interactive Bounding Box Auto-Trim States
  const [isDirectBBoxCropActive, setIsDirectBBoxCropActive] = useState<boolean>(false);
  const [directBBoxPadding, setDirectBBoxPadding] = useState<number>(10);
  const currentBBoxRectRef = useRef<PaddedCropRect | null>(null);

  // Smart Auto-Trim Bounding Box States
  const [enableSmartCrop, setEnableSmartCrop] = useState<boolean>(false);
  const [smartCropPadding, setSmartCropPadding] = useState<number>(2);
  const cellCropRectsRef = useRef<Map<string, PaddedCropRect>>(new Map());

  const currentCategory: GridCategoryDefinition =
    customCategory && selectedCatId === customCategory.id
      ? customCategory
      : GRID_CATEGORY_DEFINITIONS.find((c) => c.id === selectedCatId) || GRID_CATEGORY_DEFINITIONS[0];

  // 1. Dividers Hook
  const { colDividers, setColDividers, rowDividers, setRowDividers, draggingDividerRef, initUniformDividers, autoFitDividers, adjustColWidth, resetAllDividers } = useSlicerDividers();

  // 2. Slicing Pipeline Hook
  const {
    slicedResults,
    setSlicedResults,
    slicedCanvasesRef,
    hasExplicitlySliced,
    setHasExplicitlySliced,
    isProcessing,
    assemblySuccess,
    previewDisplayMode,
    setPreviewDisplayMode,
    handleAutoSliceAndAssemble,
  } = useSlicerSlicingPipeline({
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
    redrawCanvas: (m) => redrawCanvas(m),
  });

  // 3. Canvas Drawing Hook
  const { redrawCanvas } = useSlicerCanvasDrawing({
    imageCanvasRef,
    loadedImage,
    loadedImageRef,
    previewDisplayMode,
    hasExplicitlySliced,
    slicedCanvasesRef,
    checkerTheme,
    currentCategory,
    paddingInset,
    enableSmartCrop,
    smartCropPadding,
    cellCropRectsRef,
    colDividers,
    rowDividers,
    selectedCell,
    isDirectBBoxCropActive,
    directBBoxPadding,
    currentBBoxRectRef,
    keyColorType,
    keyColorHex,
    isolationMode,
    tolerance,
    feather,
  });

  // 4. Undo/Redo Hook
  const getCurrentSnapshot = useCallback(
    (label?: string): SlicerHistorySnapshot => ({
      timestamp: Date.now(),
      label,
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
      colDividers: [...colDividers],
      rowDividers: [...rowDividers],
      slicedResults: Array.from(slicedResults.entries()),
      previewDisplayMode,
      hasExplicitlySliced,
      currentAssembly: JSON.parse(JSON.stringify(currentAssembly)),
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
      if (snap.userUploadedImageUrl !== userUploadedImageUrl) setUserUploadedImageUrl(snap.userUploadedImageUrl);
      setSelectedCatId(snap.selectedCatId);
      setKeyColorType(snap.keyColorType);
      setKeyColorHex(snap.keyColorHex);
      setIsolationMode(snap.isolationMode);
      setTolerance(snap.tolerance);
      setFeather(snap.feather);
      setShadowRetention(snap.shadowRetention);
      setStrokeWidth(snap.strokeWidth);
      setStrokeColorHex(snap.strokeColorHex);
      setBgCleanupSubTab(snap.bgCleanupSubTab);
      setCleanupMode(snap.cleanupMode);
      setFringeColorType(snap.fringeColorType);
      setFringeColorHex(snap.fringeColorHex);
      setDefringeStrength(snap.defringeStrength);
      setEdgeChoke(snap.edgeChoke);
      setEdgeSmooth(snap.edgeSmooth);
      if (snap.smoothColorType) setSmoothColorType(snap.smoothColorType);
      if (snap.smoothColorHex) setSmoothColorHex(snap.smoothColorHex);
      setDespeckleSize(snap.despeckleSize);
      setWhiteSpeckleSensitivity(snap.whiteSpeckleSensitivity);
      setKeepLargestIslandOnly(snap.keepLargestIslandOnly);
      setPaddingInset(snap.paddingInset);
      setColDividers(snap.colDividers);
      setRowDividers(snap.rowDividers);
      setSlicedResults(new Map(snap.slicedResults));
      setPreviewDisplayMode(snap.previewDisplayMode);
      setHasExplicitlySliced(snap.hasExplicitlySliced);
      onApplyAssembly(snap.currentAssembly);
      if (threeEngineRef.current) threeEngineRef.current.setAssembly(snap.currentAssembly);
    },
    [userUploadedImageUrl, onApplyAssembly, setColDividers, setRowDividers, setSlicedResults, setPreviewDisplayMode, setHasExplicitlySliced]
  );

  const { undoStack, redoStack, historyToast, showToast, pushUndoState, handleUndo, handleRedo } = useSlicerUndoRedo({
    getCurrentSnapshot,
    applySnapshot,
  });

  // 4b. Multi-Image Gallery Hook
  const handleSelectGalleryImage = useCallback(
    (item: SlicerUploadedImageItem) => {
      pushUndoState(`Xem ảnh ${item.name}`);
      setUserUploadedImageUrl(item.url);
      setUploadedFileMetadata(item.metadata);
      setHasExplicitlySliced(false);
      setSlicedResults(new Map());
      slicedCanvasesRef.current.clear();
      setPreviewDisplayMode('original');

      if (item.metadata) {
        if (item.metadata.part_id) {
          setSingleImageSlot(item.metadata.part_id as Character2DPartType);
        }
        if (item.metadata.angle_id) {
          const def = getAngleDefinitionById(item.metadata.angle_id);
          setSingleImageAngle(def.angle);
        }
      }
    },
    [pushUndoState, setHasExplicitlySliced, setSlicedResults, setPreviewDisplayMode]
  );

  const {
    imageList,
    setImageList,
    activeImageId,
    activePartId,
    partGroups,
    activeImage,
    handleAddFiles,
    handleSelectImage,
    handleSelectPart,
    handleRemoveImage,
    handleClearAll,
  } = useSlicerMultiImageGallery({
    onSelectActiveImage: handleSelectGalleryImage,
  });

  // 5. AI Matting Hook
  const { isAIRunning, handleRunAIMatting } = useSlicerAIMatting({
    loadedImageRef,
    aiModel,
    setUserUploadedImageUrl,
    onAutoSliceAndAssemble: handleAutoSliceAndAssemble,
  });

  // 6. Canvas Interaction Hook
  const { handleCanvasMouseDown, handleCanvasMouseMove, openCellPixelEditor, handleCommitAsNewBase } = useSlicerCanvasInteraction({
    imageCanvasRef,
    loadedImageRef,
    setLoadedImage,
    setUserUploadedImageUrl,
    isEyedropperActive,
    setIsEyedropperActive,
    eyedropperTarget,
    setEyedropperHoverColor,
    setSmoothColorType,
    setSmoothColorHex,
    setFringeColorType,
    setFringeColorHex,
    setKeyColorType,
    setKeyColorHex,
    hasExplicitlySliced,
    setHasExplicitlySliced,
    handleAutoSliceAndAssemble,
    colDividers,
    setColDividers,
    rowDividers,
    setRowDividers,
    draggingDividerRef,
    currentCategory,
    setSelectedCell,
    slicedResults,
    setSlicedResults,
    slicedCanvasesRef,
    paddingInset,
    setEditingCellDef,
    setEditingCellOriginalDataUrl,
    setIsEraserOpen,
    setPreviewDisplayMode,
    redrawCanvas,
  });

  // AI Server Health check
  useEffect(() => {
    const checkServer = async () => {
      try {
        const res = await fetch('http://127.0.0.1:5000/api/status', { method: 'GET' });
        setAiServerStatus(res.ok ? 'online' : 'offline');
      } catch {
        setAiServerStatus('offline');
      }
    };
    checkServer();
    const interval = setInterval(checkServer, 5000);
    return () => clearInterval(interval);
  }, []);

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
    }
  }, [externalImageUrl, externalCategoryId]);

  // Initialize 3D Engine
  useEffect(() => {
    if (threeContainerRef.current && !threeEngineRef.current) {
      threeEngineRef.current = new ThreeMultiAngleBillboardEngine(threeContainerRef.current, (res: AngleDetectionResult) => {
        setActiveAngleInfo(res);
        setTurntableAngle(res.angleDeg);
      });
      if (currentAssembly) threeEngineRef.current.setAssembly(currentAssembly);
    }
    return () => {
      if (threeEngineRef.current) {
        threeEngineRef.current.dispose();
        threeEngineRef.current = null;
      }
    };
  }, []);

  useEffect(() => {
    if (threeEngineRef.current && currentAssembly) threeEngineRef.current.setAssembly(currentAssembly);
  }, [currentAssembly]);

  // Load Image
  useEffect(() => {
    if (!userUploadedImageUrl) {
      loadedImageRef.current = null;
      setLoadedImage(null);
      setPreviewDisplayMode('original');
      setHasExplicitlySliced(false);
      slicedCanvasesRef.current.clear();
      setSlicedResults(new Map());
      return;
    }
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      loadedImageRef.current = img;
      setLoadedImage(img);
      const w = img.naturalWidth || img.width;
      const h = img.naturalHeight || img.height;
      initUniformDividers(w, h, currentCategory.cols, currentCategory.rows);
      setHasExplicitlySliced(false);
      slicedCanvasesRef.current.clear();
      setSlicedResults(new Map());
    };
    img.src = userUploadedImageUrl;
  }, [userUploadedImageUrl, initUniformDividers, currentCategory.cols, currentCategory.rows, setHasExplicitlySliced, setPreviewDisplayMode, setSlicedResults]);

  const handleSelectCatId = useCallback(
    (newCatId: string) => {
      setSelectedCatId(newCatId);
      if (newCatId !== 'single_full_image') {
        setLastUsedGridCatId(newCatId);
        saveCachedSingleMode(false);
      } else {
        saveCachedSingleMode(true);
      }
      const cat = customCategory && customCategory.id === newCatId ? customCategory : GRID_CATEGORY_DEFINITIONS.find((c) => c.id === newCatId) || GRID_CATEGORY_DEFINITIONS[0];
      const img = loadedImage || loadedImageRef.current;
      if (img) {
        const w = img.naturalWidth || img.width;
        const h = img.naturalHeight || img.height;
        initUniformDividers(w, h, cat.cols, cat.rows);
      }
      setSelectedCell(null);
      setHasExplicitlySliced(false);
      slicedCanvasesRef.current.clear();
      setSlicedResults(new Map());
      setPreviewDisplayMode('original');
    },
    [loadedImage, initUniformDividers, customCategory, setHasExplicitlySliced, setPreviewDisplayMode, setSlicedResults]
  );

  const handleToggleSingleImageMode = useCallback(() => {
    if (selectedCatId === 'single_full_image') {
      const targetCatId = customCategory?.id || (lastUsedGridCatId !== 'single_full_image' ? lastUsedGridCatId : 'cinematic_single_part_2x3');
      saveCachedSingleMode(false);
      handleSelectCatId(targetCatId);
    } else {
      setLastUsedGridCatId(selectedCatId);
      saveCachedSingleMode(true);
      handleSelectCatId('single_full_image');
    }
  }, [selectedCatId, customCategory, lastUsedGridCatId, handleSelectCatId]);

  const handleToggleCheckerTheme = useCallback(() => {
    setCheckerTheme((prev) => {
      const next = prev === 'light' ? 'dark' : 'light';
      saveCachedCheckerTheme(next);
      return next;
    });
  }, []);

  const handleSelectGridMatrix = useCallback(
    (rows: number, cols: number) => {
      const newCat = createDynamicGridCategory(rows, cols);
      setCustomCategory(newCat);
      setLastUsedGridCatId(newCat.id);
      saveCachedSingleMode(false);
      setSelectedCatId(newCat.id);
      const img = loadedImage || loadedImageRef.current;
      if (img) {
        const w = img.naturalWidth || img.width;
        const h = img.naturalHeight || img.height;
        initUniformDividers(w, h, newCat.cols, newCat.rows);
      }
      setSelectedCell(null);
      setHasExplicitlySliced(false);
      slicedCanvasesRef.current.clear();
      setSlicedResults(new Map());
      setPreviewDisplayMode('original');
    },
    [loadedImage, initUniformDividers, setHasExplicitlySliced, setPreviewDisplayMode, setSlicedResults]
  );

  const handleToggleDirectBBoxCrop = useCallback(() => {
    setIsDirectBBoxCropActive((prev) => !prev);
  }, []);

  const handleApplyDirectBBoxCrop = useCallback(() => {
    const img = loadedImage || loadedImageRef.current;
    if (!img) return;

    const chromaOpts = keyColorType
      ? {
          keyColorType,
          keyColorHex: keyColorHex || '#00ff00',
          isolationMode: isolationMode || 'all',
          tolerance: tolerance ?? 35,
          feather: feather ?? 2,
          shadowRetention: shadowRetention ?? 0,
          strokeWidth: strokeWidth ?? 0,
          strokeColorHex: strokeColorHex || '#000000',
          despeckleSize: despeckleSize ?? 0,
          whiteSpeckleSensitivity: whiteSpeckleSensitivity ?? 0,
          keepLargestIslandOnly: keepLargestIslandOnly ?? false,
          fringeColorType: fringeColorType || 'chroma_green',
          fringeColorHex: fringeColorHex || '#00ff00',
          defringeStrength: defringeStrength ?? 0,
          edgeChoke: edgeChoke ?? 0,
          edgeSmooth: edgeSmooth ?? 0,
          smoothColorType: smoothColorType || 'black',
          smoothColorHex: smoothColorHex || '#000000',
          cleanupMode: cleanupMode || 'all',
        }
      : undefined;

    // 1. Get the EXACT rect that is currently displayed on the screen
    let rect = currentBBoxRectRef.current;

    // Fallback if not cached
    if (!rect) {
      rect = detectImageBBoxRect(img, chromaOpts, directBBoxPadding, 20);
    }

    if (!rect) {
      showToast('⚠️ Không tìm thấy đối tượng trong ảnh để cắt!', 'undo');
      return;
    }

    const imgW = img.naturalWidth || img.width;
    const imgH = img.naturalHeight || img.height;

    // Safety clamp rect within image bounds
    const safeLeft = Math.max(0, Math.min(imgW - 1, rect.left));
    const safeTop = Math.max(0, Math.min(imgH - 1, rect.top));
    const safeWidth = Math.max(1, Math.min(imgW - safeLeft, rect.width));
    const safeHeight = Math.max(1, Math.min(imgH - safeTop, rect.height));

    // 2 LOẠI CẮT ĐỘC LẬP:
    // 1. Chế độ xem Ảnh Gốc (previewDisplayMode === 'original'):
    //    -> Cắt trực tiếp từ ảnh gốc (giữ nguyên màu nền gốc, chỉ cắt khung ảnh).
    // 2. Chế độ xem Đã Tách Nền (previewDisplayMode === 'transparent'):
    //    -> Cắt từ ảnh đã tách nền trong suốt.
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
      cropCtx.drawImage(
        fullCanvas,
        safeLeft,
        safeTop,
        safeWidth,
        safeHeight,
        0,
        0,
        safeWidth,
        safeHeight
      );
    } else {
      // Cắt từ ảnh đã tách nền trong suốt
      if (chromaOpts) {
        processCellChromaAndDespeckle(fullCtx, imgW, imgH, chromaOpts);
      }
      cropCtx.drawImage(
        fullCanvas,
        safeLeft,
        safeTop,
        safeWidth,
        safeHeight,
        0,
        0,
        safeWidth,
        safeHeight
      );
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
    selectedCatId,
    directBBoxPadding,
    activeImageId,
    pushUndoState,
    setImageList,
    showToast,
    setSlicedResults,
    setHasExplicitlySliced,
  ]);

  useEffect(() => {
    redrawCanvas();
  }, [redrawCanvas]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', background: '#070b14', color: '#f8fafc', padding: 10, gap: 10, overflow: 'hidden', boxSizing: 'border-box' }}>
      {/* Toast Notification */}
      {historyToast && (
        <div style={{ position: 'fixed', top: 16, left: '50%', transform: 'translateX(-50%)', background: historyToast.type === 'undo' ? 'rgba(239, 68, 68, 0.95)' : 'rgba(16, 185, 129, 0.95)', color: '#fff', padding: '6px 16px', borderRadius: 6, fontSize: 12, fontWeight: 700, zIndex: 9999, boxShadow: '0 4px 16px rgba(0,0,0,0.5)' }}>
          {historyToast.message}
        </div>
      )}

      {/* Auto Detect Metadata Header Bar */}
      {uploadedFileMetadata && (
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '5px 12px', background: 'linear-gradient(90deg, rgba(2, 132, 199, 0.18) 0%, rgba(139, 92, 246, 0.18) 100%)', borderRadius: 6, border: '1px solid rgba(56, 189, 248, 0.35)', fontSize: 11, color: '#e0f2fe' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <span style={{ fontWeight: 800, color: '#38bdf8' }}>🏷️ TỰ ĐỘNG NHẬN DIỆN TỆP:</span>
            <span style={{ background: 'linear-gradient(135deg, #0284c7 0%, #2563eb 100%)', color: '#fff', padding: '2px 8px', borderRadius: 4, fontWeight: 700 }}>
              {uploadedFileMetadata.part_name} ({uploadedFileMetadata.part_id})
            </span>
            <span>• Góc quay: <b>{uploadedFileMetadata.angle_name}</b></span>
            {uploadedFileMetadata.variant_index && (
              <span style={{ background: 'rgba(245, 158, 11, 0.25)', color: '#fbbf24', border: '1px solid rgba(245, 158, 11, 0.4)', padding: '1px 6px', borderRadius: 4, fontWeight: 700, fontSize: 10 }}>
                Biến thể #{uploadedFileMetadata.variant_index}
              </span>
            )}
            <span>• Nhóm: <b>{uploadedFileMetadata.group_name}</b></span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            {uploadedFileMetadata.original_filename && uploadedFileMetadata.original_filename !== uploadedFileMetadata.canonical_filename && (
              <span style={{ fontSize: 9.5, color: '#64748b', fontFamily: 'monospace' }} title="Tệp nguồn thực tế">
                [{uploadedFileMetadata.original_filename}] ➔
              </span>
            )}
            <span style={{ fontSize: 10, color: '#38bdf8', fontFamily: 'monospace', fontWeight: 600 }} title="Tên tệp chuẩn dùng trong Pipeline">
              {uploadedFileMetadata.canonical_filename || uploadedFileMetadata.save_filename}
            </span>
          </div>
        </div>
      )}

      {/* Main Studio Grid: Sidebar, Canvas, Vertical Gallery (when images exist), 3D Turntable Preview */}
      <div style={{ flex: 1, display: 'grid', gridTemplateColumns: imageList.length > 0 ? '370px 1fr 228px 350px' : '410px 1fr 380px', gap: 10, minHeight: 0 }}>
        {/* Column 1: Slicer Controls */}
        <SlicerSidebarControls
          targetCategory={targetCategory}
          onSelectTargetCategory={handleSelectTargetCategory}
          selectedCatId={selectedCatId}
          onSelectCatId={(catId) => { pushUndoState('Đổi cấu trúc lưới'); handleSelectCatId(catId); }}
          customCategory={customCategory}
          singleImageAngle={singleImageAngle}
          onUpdateSingleImageAngle={(ang) => { setSingleImageAngle(ang); if (hasExplicitlySliced) handleAutoSliceAndAssemble(); }}
          singleImageSlot={singleImageSlot}
          onUpdateSingleImageSlot={(slot) => { setSingleImageSlot(slot); if (hasExplicitlySliced) handleAutoSliceAndAssemble(); }}
          onAutoDetectAngleFromFilename={() => {
            if (!uploadedFileMetadata && !userUploadedImageUrl) return;
            const meta = uploadedFileMetadata || (userUploadedImageUrl ? parsePartFilename(userUploadedImageUrl) : null);
            if (!meta) return;
            if (selectedCatId === 'single_full_image') {
              const def = getAngleDefinitionById(meta.angle_id);
              setSingleImageAngle(def.angle);
              if (meta.part_id) setSingleImageSlot(meta.part_id as Character2DPartType);
              showToast(`✓ Đã nhận diện: ${meta.part_name} (${meta.angle_name})`, 'redo');
              if (hasExplicitlySliced) handleAutoSliceAndAssemble();
            }
          }}
          onOpenJsonImportModal={() => setIsJsonImportOpen(true)}
          userUploadedImageUrl={userUploadedImageUrl}
          totalLoadedCount={imageList.length}
          onFileUpload={(e) => {
            const files = e.target.files;
            if (files && files.length > 0) {
              pushUndoState(`Tải ${files.length} ảnh`);
              handleAddFiles(files);
            }
          }}
          onClearImage={() => {
            pushUndoState('Xóa ảnh');
            handleClearAll();
            setUserUploadedImageUrl(null);
            setUploadedFileMetadata(null);
            setHasExplicitlySliced(false);
            setSlicedResults(new Map());
            slicedCanvasesRef.current.clear();
            setPreviewDisplayMode('original');
          }}
          fileInputRef={fileInputRef}
          isEyedropperActive={isEyedropperActive}
          setIsEyedropperActive={setIsEyedropperActive}
          keyColorType={keyColorType}
          setKeyColorType={setKeyColorType}
          keyColorHex={keyColorHex}
          setKeyColorHex={setKeyColorHex}
          isolationMode={isolationMode}
          setIsolationMode={setIsolationMode}
          tolerance={tolerance}
          setTolerance={setTolerance}
          feather={feather}
          setFeather={setFeather}
          shadowRetention={shadowRetention}
          setShadowRetention={setShadowRetention}
          strokeWidth={strokeWidth}
          setStrokeWidth={setStrokeWidth}
          strokeColorHex={strokeColorHex}
          setStrokeColorHex={setStrokeColorHex}
          bgCleanupSubTab={bgCleanupSubTab}
          setBgCleanupSubTab={setBgCleanupSubTab}
          aiModel={aiModel}
          setAiModel={setAiModel}
          aiScope={aiScope}
          setAiScope={setAiScope}
          aiServerStatus={aiServerStatus}
          onRunAIMatting={() => { pushUndoState('AI Tách Nền (GPU)'); handleRunAIMatting(); }}
          isAIRunning={isAIRunning}
          despeckleSize={despeckleSize}
          setDespeckleSize={setDespeckleSize}
          whiteSpeckleSensitivity={whiteSpeckleSensitivity}
          setWhiteSpeckleSensitivity={setWhiteSpeckleSensitivity}
          keepLargestIslandOnly={keepLargestIslandOnly}
          setKeepLargestIslandOnly={setKeepLargestIslandOnly}
          eyedropperTarget={eyedropperTarget}
          setEyedropperTarget={setEyedropperTarget}
          cleanupMode={cleanupMode}
          setCleanupMode={setCleanupMode}
          fringeColorType={fringeColorType}
          setFringeColorType={setFringeColorType}
          fringeColorHex={fringeColorHex}
          setFringeColorHex={setFringeColorHex}
          defringeStrength={defringeStrength}
          setDefringeStrength={setDefringeStrength}
          edgeChoke={edgeChoke}
          setEdgeChoke={setEdgeChoke}
          edgeSmooth={edgeSmooth}
          setEdgeSmooth={setEdgeSmooth}
          smoothColorType={smoothColorType}
          setSmoothColorType={setSmoothColorType}
          smoothColorHex={smoothColorHex}
          setSmoothColorHex={setSmoothColorHex}
          onRunDespeckleOnly={() => handleAutoSliceAndAssemble()}
          onApplyAsNewBaseImage={handleCommitAsNewBase}
          paddingInset={paddingInset}
          setPaddingInset={setPaddingInset}
          enableSmartCrop={enableSmartCrop}
          setEnableSmartCrop={setEnableSmartCrop}
          smartCropPadding={smartCropPadding}
          setSmartCropPadding={setSmartCropPadding}
          isProcessing={isProcessing}
          assemblySuccess={assemblySuccess}
          onAutoSliceAndAssemble={() => { pushUndoState('Bóc tách & Lắp ráp 3D'); handleAutoSliceAndAssemble(); }}
          onCommitSliderChange={(overrides) => { pushUndoState('Điều chỉnh thông số'); handleAutoSliceAndAssemble(overrides); }}
          slicedCount={slicedResults.size}
          totalCellCount={currentCategory.id === 'single_full_image' ? 1 : currentCategory.cells.length}
          onOpenSaveKitModal={() => setIsSaveKitModalOpen(true)}
          onOpenCatalogModal={() => setIsCatalogOpen(true)}
        />

        {/* Column 2: Interactive Canvas */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, minHeight: 0, overflow: 'hidden' }}>
          <SlicerInteractiveCanvas
            imageCanvasRef={imageCanvasRef}
            hasImage={Boolean(userUploadedImageUrl)}
            loadedImage={loadedImage}
            previewDisplayMode={previewDisplayMode}
            setPreviewDisplayMode={setPreviewDisplayMode}
            onTogglePreviewDisplayMode={(mode) => {
              setPreviewDisplayMode(mode);
              if (mode === 'transparent') {
                if (!hasExplicitlySliced || slicedCanvasesRef.current.size === 0) handleAutoSliceAndAssemble();
                else redrawCanvas('transparent');
              } else redrawCanvas('original');
            }}
            hasExplicitlySliced={hasExplicitlySliced}
            checkerTheme={checkerTheme}
            onToggleCheckerTheme={handleToggleCheckerTheme}
            isSingleImageMode={selectedCatId === 'single_full_image'}
            onToggleSingleImageMode={handleToggleSingleImageMode}
            onOpenGridTablePicker={() => setIsTablePickerOpen(true)}
            isEyedropperActive={isEyedropperActive}
            eyedropperTarget={eyedropperTarget}
            eyedropperHoverColor={eyedropperHoverColor}
            currentCategory={currentCategory}
            onAutoFitGrid={() => {
              const img = loadedImage || loadedImageRef.current;
              if (img) autoFitDividers(img, currentCategory.cols, currentCategory.rows, keyColorType, keyColorHex);
            }}
            onResetUniformGrid={() => {
              const img = loadedImage || loadedImageRef.current;
              if (img) initUniformDividers(img.width, img.height, currentCategory.cols, currentCategory.rows);
            }}
            canUndo={undoStack.length > 0}
            canRedo={redoStack.length > 0}
            onUndo={handleUndo}
            onRedo={handleRedo}
            historyToast={historyToast}
            onMouseDown={handleCanvasMouseDown}
            onMouseMove={handleCanvasMouseMove}
            onMouseLeave={() => { setEyedropperHoverColor(null); draggingDividerRef.current = null; }}
            onMouseUp={() => { draggingDividerRef.current = null; }}
            onDoubleClick={() => { if (selectedCell) openCellPixelEditor(selectedCell); }}
            isDirectBBoxCropActive={isDirectBBoxCropActive}
            onToggleDirectBBoxCrop={handleToggleDirectBBoxCrop}
            directBBoxPadding={directBBoxPadding}
            setDirectBBoxPadding={setDirectBBoxPadding}
            onApplyDirectBBoxCrop={handleApplyDirectBBoxCrop}
          />

          {selectedCell && (
            <SlicerCellAdjustmentBar
              selectedCell={selectedCell}
              slicedCellDataUrl={slicedResults.get(`${selectedCell.row}_${selectedCell.col}`)}
              onOpenCellPixelEditor={(cell) => openCellPixelEditor(cell)}
              onAdjustColWidth={(delta) => { adjustColWidth(selectedCell, delta); if (hasExplicitlySliced) handleAutoSliceAndAssemble(); }}
              onResetAllDividers={() => { resetAllDividers(loadedImageRef.current, currentCategory.cols, currentCategory.rows); if (hasExplicitlySliced) handleAutoSliceAndAssemble(); }}
              onUpdateCellAngle={(cell, angle, mirror) => {
                cell.angle = angle;
                cell.mirrorAngle = mirror;
                if (hasExplicitlySliced) handleAutoSliceAndAssemble();
                else redrawCanvas();
              }}
              onUpdateCellSlot={(cell, slot) => { cell.partSlot = slot; if (hasExplicitlySliced) handleAutoSliceAndAssemble(); }}
              onClose={() => setSelectedCell(null)}
            />
          )}
        </div>

        {/* Column 2.5: Vertical Gallery Column (left of 3D Turntable Preview) */}
        {imageList.length > 0 && (
          <SlicerVerticalGalleryColumn
            partGroups={partGroups}
            activePartId={activePartId}
            activeImageId={activeImageId}
            onSelectPart={handleSelectPart}
            onSelectImage={handleSelectImage}
            onRemoveImage={handleRemoveImage}
            onClearAll={() => {
              pushUndoState('Xóa tất cả ảnh');
              handleClearAll();
              setUserUploadedImageUrl(null);
              setUploadedFileMetadata(null);
              setHasExplicitlySliced(false);
              setSlicedResults(new Map());
              slicedCanvasesRef.current.clear();
              setPreviewDisplayMode('original');
            }}
            onOpenAddFiles={() => fileInputRef.current?.click()}
            totalImagesCount={imageList.length}
            previewDisplayMode={previewDisplayMode}
            chromaOpts={
              keyColorType
                ? {
                    keyColorType,
                    keyColorHex: keyColorHex || '#00ff00',
                    isolationMode: isolationMode || 'all',
                    tolerance: tolerance ?? 35,
                    feather: feather ?? 2,
                    shadowRetention: 0,
                    strokeWidth: 0,
                    strokeColorHex: '#000000',
                    despeckleSize: 0,
                    whiteSpeckleSensitivity: 0,
                    keepLargestIslandOnly: false,
                    fringeColorType: 'chroma_green',
                    fringeColorHex: '#00ff00',
                    defringeStrength: 0,
                    edgeChoke: 0,
                    edgeSmooth: 0,
                    smoothColorType: 'black',
                    smoothColorHex: '#000000',
                    cleanupMode: 'all',
                  }
                : undefined
            }
          />
        )}

        {/* Column 3: 3D Turntable Preview */}
        <Slicer3DTurntablePreview
          threeContainerRef={threeContainerRef}
          threeEngineRef={threeEngineRef}
          activeAngleInfo={activeAngleInfo}
          turntableAngle={turntableAngle}
          setTurntableAngle={(deg) => {
            setTurntableAngle(deg);
            if (threeEngineRef.current) threeEngineRef.current.jumpToAngle(deg, false);
          }}
          timeOfDay={timeOfDay}
          setTimeOfDay={(t) => {
            setTimeOfDay(t);
            if (threeEngineRef.current) threeEngineRef.current.setTimeOfDay(t);
          }}
          slicedResults={slicedResults}
          currentCategory={currentCategory}
          selectedCell={selectedCell}
          onSelectCell={(cell) => setSelectedCell(cell)}
          onOpenSaveKitModal={() => setIsSaveKitModalOpen(true)}
          onOpenCatalogModal={() => setIsCatalogOpen(true)}
          onOpenTunerModal={() => setIsTunerOpen(true)}
        />
      </div>

      {/* Modals */}

      {isEraserOpen && editingCellDef && (
        <CellPixelEraserModal
          isOpen={isEraserOpen}
          onClose={() => { setIsEraserOpen(false); setEditingCellDef(null); }}
          cellTitle={editingCellDef.label || `Ô [${editingCellDef.row + 1}, ${editingCellDef.col + 1}]`}
          initialImageDataUrl={editingCellOriginalDataUrl}
          onSave={(newDataUrl) => {
            const key = `${editingCellDef.row}_${editingCellDef.col}`;
            setSlicedResults((prev) => new Map(prev).set(key, newDataUrl));
            const img = new Image();
            img.onload = () => {
              const canvas = document.createElement('canvas');
              canvas.width = img.width;
              canvas.height = img.height;
              const ctx = canvas.getContext('2d');
              if (ctx) {
                ctx.drawImage(img, 0, 0);
                slicedCanvasesRef.current.set(key, canvas);
                redrawCanvas('transparent');
              }
            };
            img.src = newDataUrl;
          }}
        />
      )}

      {isCatalogOpen && (
        <CharacterAssetCatalogModal
          isOpen={isCatalogOpen}
          onClose={() => setIsCatalogOpen(false)}
          currentAssembly={currentAssembly}
          onApplyAssembly={(updated: Character2DAssembly) => {
            onApplyAssembly(updated);
            if (threeEngineRef.current) threeEngineRef.current.setAssembly(updated);
          }}
        />
      )}

      {isTunerOpen && (
        <MultiAngleTunerModal
          isOpen={isTunerOpen}
          onClose={() => setIsTunerOpen(false)}
          currentAssembly={currentAssembly}
          activeCameraAngle={activeAngleInfo.discreteAngle}
          onApplyAssembly={(updated) => {
            onApplyAssembly(updated);
            if (threeEngineRef.current) threeEngineRef.current.setAssembly(updated);
          }}
          onJumpToAngle={(deg, isTop) => {
            setTurntableAngle(deg);
            if (threeEngineRef.current) threeEngineRef.current.jumpToAngle(deg, isTop);
          }}
        />
      )}

      {isSaveKitModalOpen && (
        <SlicerSaveKitModal
          isOpen={isSaveKitModalOpen}
          onClose={() => setIsSaveKitModalOpen(false)}
          slicedResults={slicedResults}
          categoryLabel={currentCategory.label}
          initialTargetCategory={targetCategory}
        />
      )}

      {isTablePickerOpen && (
        <GridTablePickerModal
          isOpen={isTablePickerOpen}
          onClose={() => setIsTablePickerOpen(false)}
          currentRows={currentCategory.rows}
          currentCols={currentCategory.cols}
          onSelectGrid={handleSelectGridMatrix}
        />
      )}

      {isJsonImportOpen && (
        <JsonPromptImportModal
          isOpen={isJsonImportOpen}
          onClose={() => setIsJsonImportOpen(false)}
          onApplyJsonMetadata={(metadataList: ParsedJsonMetadataItem[]) => {
            if (!metadataList || metadataList.length === 0) return;
            if (selectedCatId === 'single_full_image') {
              const first = metadataList[0];
              if (first.angle) setSingleImageAngle(first.angle);
              if (first.part_id) setSingleImageSlot(first.part_id);
            } else {
              metadataList.forEach((item, idx) => {
                if (idx < currentCategory.cells.length) {
                  const cell = currentCategory.cells[idx];
                  if (item.angle) cell.angle = item.angle;
                  if (item.part_id) cell.partSlot = item.part_id;
                  if (item.name || item.part_name) cell.label = `${item.part_name || item.name} (${item.angle_label || item.angle_id || ''})`;
                }
              });
            }
            showToast(`✓ Đã nạp ${metadataList.length} metadata từ JSON Tab 4!`, 'redo');
            if (hasExplicitlySliced) handleAutoSliceAndAssemble();
            else redrawCanvas();
          }}
        />
      )}
    </div>
  );
};
