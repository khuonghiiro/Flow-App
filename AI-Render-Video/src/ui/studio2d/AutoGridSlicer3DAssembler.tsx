import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Character2DAssembly, Character2DPartType } from '../../types/scene2d';
import { GridCellDefinition } from '../../core/assets/GridSliceRegistry';
import { parsePartFilename, ParsedPartFilenameInfo } from '../../core/assets/Asset2DRegistry';
import { PaddedCropRect } from '../../core/utils/PixelBoundingBoxAlgorithms';
import {
  CheckerboardTheme,
  loadCachedCheckerTheme,
  saveCachedCheckerTheme,
  getAngleDefinitionById,
} from '../../core/assets/slicer/SlicerAngleConstants';

// Subcomponents & Custom Hooks
import { SlicerSidebarContainer } from './slicer/SlicerSidebarContainer';
import { SlicerCellAdjustmentBar } from './slicer/SlicerCellAdjustmentBar';
import { SlicerInteractiveCanvas } from './slicer/SlicerInteractiveCanvas';
import { SlicerVerticalGalleryColumn } from './slicer/SlicerVerticalGalleryColumn';
import { SlicerMetadataHeaderBar } from './slicer/SlicerMetadataHeaderBar';
import { SlicerWorkbenchModals } from './slicer/modals/SlicerWorkbenchModals';
import { useSlicerFilterStates } from './slicer/hooks/useSlicerFilterStates';
import { useSlicerDividers } from './slicer/hooks/useSlicerDividers';
import { useSlicerUndoRedo, SlicerHistorySnapshot } from './slicer/hooks/useSlicerUndoRedo';
import { useSlicerCanvasDrawing } from './slicer/hooks/useSlicerCanvasDrawing';
import { useSlicerSlicingPipeline } from './slicer/hooks/useSlicerSlicingPipeline';
import { useSlicerAIMatting } from './slicer/hooks/useSlicerAIMatting';
import { useSlicerCanvasInteraction } from './slicer/hooks/useSlicerCanvasInteraction';
import { useSlicerMultiImageGallery, SlicerUploadedImageItem } from './slicer/hooks/useSlicerMultiImageGallery';
import { useSlicerBatchSeparation } from './slicer/hooks/useSlicerBatchSeparation';
import { useSlicerDirectBBoxCrop } from './slicer/hooks/useSlicerDirectBBoxCrop';
import { useSlicerModalsState } from './slicer/hooks/useSlicerModalsState';
import { useSlicerCategoryState } from './slicer/hooks/useSlicerCategoryState';
import { useSlicer3DEngine } from './slicer/hooks/useSlicer3DEngine';

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
  // Image & Selection States
  const [userUploadedImageUrl, setUserUploadedImageUrl] = useState<string | null>(null);
  const [uploadedFileMetadata, setUploadedFileMetadata] = useState<ParsedPartFilenameInfo | null>(null);
  const [loadedImage, setLoadedImage] = useState<HTMLImageElement | null>(null);
  const [selectedCell, setSelectedCell] = useState<GridCellDefinition | null>(null);
  const [checkerTheme, setCheckerTheme] = useState<CheckerboardTheme>(loadCachedCheckerTheme);

  // Refs
  const imageCanvasRef = useRef<HTMLCanvasElement>(null);
  const loadedImageRef = useRef<HTMLImageElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const activeImageIdRef = useRef<string | null>(null);

  // Smart Auto-Trim Bounding Box States
  const [enableSmartCrop, setEnableSmartCrop] = useState<boolean>(false);
  const [smartCropPadding, setSmartCropPadding] = useState<number>(2);
  const cellCropRectsRef = useRef<Map<string, PaddedCropRect>>(new Map());

  // 1. Filter & Color Tuning States
  const filterStates = useSlicerFilterStates();
  const {
    keyColorType, keyColorHex, isolationMode, tolerance, feather,
    shadowRetention, strokeWidth, strokeColorHex, bgCleanupSubTab,
    aiModel, aiScope, despeckleSize, whiteSpeckleSensitivity, keepLargestIslandOnly,
    eyedropperTarget, setEyedropperTarget, cleanupMode, setCleanupMode,
    fringeColorType, setFringeColorType, fringeColorHex, setFringeColorHex,
    defringeStrength, setDefringeStrength, edgeChoke, setEdgeChoke, edgeSmooth, setEdgeSmooth,
    smoothColorType, setSmoothColorType, smoothColorHex, setSmoothColorHex,
    paddingInset, setPaddingInset, chromaOptions,
    isEyedropperActive, setIsEyedropperActive, eyedropperHoverColor, setEyedropperHoverColor,
    setKeyColorType, setKeyColorHex, setIsolationMode, setTolerance, setFeather,
    setShadowRetention, setStrokeWidth, setStrokeColorHex, setBgCleanupSubTab,
    setAiModel, setAiScope, setAiServerStatus, setDespeckleSize,
    setWhiteSpeckleSensitivity, setKeepLargestIslandOnly,
    applyFilterConfig, resetToDefaults,
  } = filterStates;

  // 2. Dividers Hook
  const { colDividers, setColDividers, rowDividers, setRowDividers, draggingDividerRef, initUniformDividers, autoFitDividers, adjustColWidth, resetAllDividers } = useSlicerDividers();

  // 3. Category & Grid Config Hook
  const categoryState = useSlicerCategoryState({
    externalCategoryId,
    loadedImageRef,
    loadedImage,
    initUniformDividers,
    setSelectedCell,
    setHasExplicitlySliced: (sliced) => setHasExplicitlySliced(sliced),
    slicedCanvasesRef: { current: new Map() } as any,
    setSlicedResults: (results) => setSlicedResults(results),
    setPreviewDisplayMode: (mode) => setPreviewDisplayMode(mode),
  });
  const {
    selectedCatId, setSelectedCatId, targetCategory, handleSelectTargetCategory,
    customCategory, singleImageAngle, setSingleImageAngle, singleImageSlot, setSingleImageSlot,
    currentCategory, handleSelectCatId, handleToggleSingleImageMode, handleSelectGridMatrix,
  } = categoryState;

  // 4. 3D Engine Hook
  const { activeAngleInfo, turntableAngle, setTurntableAngle, threeEngineRef } = useSlicer3DEngine({
    currentAssembly,
  });

  // 5. Multi-Image Gallery Hook
  const handleSelectGalleryImageRef = useRef<((item: SlicerUploadedImageItem) => void) | null>(null);
  const {
    imageList, setImageList, activeImageId, activePartId,
    partGroups, handleAddFiles, handleSelectImage, handleSelectPart,
    handleRemoveImage, handleClearAll,
  } = useSlicerMultiImageGallery({
    onSelectActiveImage: (item) => handleSelectGalleryImageRef.current?.(item),
  });

  useEffect(() => {
    activeImageIdRef.current = activeImageId;
  }, [activeImageId]);

  // 6. Slicing Pipeline Hook
  const {
    slicedResults, setSlicedResults, slicedCanvasesRef, hasExplicitlySliced,
    setHasExplicitlySliced, isProcessing, assemblySuccess, previewDisplayMode,
    setPreviewDisplayMode, handleAutoSliceAndAssemble,
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
    onSliceSuccess: (results) => {
      const currentActiveId = activeImageIdRef.current;
      if (currentActiveId && results.size > 0) {
        const primaryUrl = results.get('0_0') || results.values().next().value;
        if (primaryUrl) {
          setImageList((prev) =>
            prev.map((it) =>
              it.id === currentActiveId
                ? {
                    ...it,
                    originalUrl: it.originalUrl || it.url,
                    url: primaryUrl,
                    transparentUrl: primaryUrl,
                    isTransparentSeparated: true,
                    filterConfig: { ...chromaOptions },
                  }
                : it
            )
          );
        }
      }
    },
  });

  // 7. Canvas Drawing Hook
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
    keyColorType,
    keyColorHex,
    isolationMode,
    tolerance,
    feather,
  });

  // 8. Undo/Redo Hook
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
      userUploadedImageUrl, selectedCatId, keyColorType, keyColorHex, isolationMode,
      tolerance, feather, shadowRetention, strokeWidth, strokeColorHex, bgCleanupSubTab,
      cleanupMode, fringeColorType, fringeColorHex, defringeStrength, edgeChoke,
      edgeSmooth, smoothColorType, smoothColorHex, despeckleSize, whiteSpeckleSensitivity,
      keepLargestIslandOnly, paddingInset, colDividers, rowDividers, slicedResults,
      previewDisplayMode, hasExplicitlySliced, currentAssembly,
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
    [userUploadedImageUrl, onApplyAssembly, setColDividers, setRowDividers, setSlicedResults, setPreviewDisplayMode, setHasExplicitlySliced, setKeyColorType, setKeyColorHex, setIsolationMode, setTolerance, setFeather, setShadowRetention, setStrokeWidth, setStrokeColorHex, setBgCleanupSubTab, setCleanupMode, setFringeColorType, setFringeColorHex, setDefringeStrength, setEdgeChoke, setEdgeSmooth, setSmoothColorType, setSmoothColorHex, setDespeckleSize, setWhiteSpeckleSensitivity, setKeepLargestIslandOnly, setPaddingInset, setSelectedCatId, threeEngineRef]
  );

  const { undoStack, redoStack, historyToast, showToast, pushUndoState, handleUndo, handleRedo } = useSlicerUndoRedo({
    getCurrentSnapshot,
    applySnapshot,
  });

  // 9. Multi-Image Gallery Active Selection Handler
  const handleSelectGalleryImage = useCallback(
    (item: SlicerUploadedImageItem) => {
      pushUndoState(`Xem ảnh ${item.name}`);

      // Save outgoing image's filter configuration
      const prevActiveId = activeImageIdRef.current;
      if (prevActiveId && prevActiveId !== item.id) {
        setImageList((prev) =>
          prev.map((it) =>
            it.id === prevActiveId
              ? { ...it, filterConfig: { ...chromaOptions } }
              : it
          )
        );
      }

      // Restore incoming item's filter configuration (or reset to defaults if not yet separated)
      if (item.filterConfig) {
        applyFilterConfig(item.filterConfig);
      } else {
        resetToDefaults();
      }

      const isSeparated = Boolean(item.isTransparentSeparated && item.transparentUrl);
      const targetUrl = isSeparated ? item.transparentUrl! : (item.originalUrl || item.url);

      setUserUploadedImageUrl(targetUrl);
      setUploadedFileMetadata(item.metadata);

      const baseImg = new Image();
      baseImg.crossOrigin = 'anonymous';
      baseImg.onload = () => {
        loadedImageRef.current = baseImg;
        setLoadedImage(baseImg);
        const w = baseImg.naturalWidth || baseImg.width;
        const h = baseImg.naturalHeight || baseImg.height;
        initUniformDividers(w, h, currentCategory.cols, currentCategory.rows);

        if (isSeparated && item.transparentUrl) {
          setPreviewDisplayMode('transparent');
          setHasExplicitlySliced(true);
          slicedCanvasesRef.current.clear();

          const sc = document.createElement('canvas');
          sc.width = w;
          sc.height = h;
          const sctx = sc.getContext('2d');
          if (sctx) {
            sctx.drawImage(baseImg, 0, 0);
            slicedCanvasesRef.current.set('0_0', sc);
            setSlicedResults(new Map([['0_0', item.transparentUrl]]));
          }
          redrawCanvas('transparent');
        } else {
          setPreviewDisplayMode('original');
          setHasExplicitlySliced(false);
          slicedCanvasesRef.current.clear();
          setSlicedResults(new Map());
          redrawCanvas('original');
        }
      };
      baseImg.src = targetUrl;

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
    [pushUndoState, chromaOptions, applyFilterConfig, resetToDefaults, initUniformDividers, currentCategory.cols, currentCategory.rows, setHasExplicitlySliced, setSlicedResults, setPreviewDisplayMode, redrawCanvas, setSingleImageSlot, setSingleImageAngle, slicedCanvasesRef, setImageList]
  );

  useEffect(() => {
    handleSelectGalleryImageRef.current = handleSelectGalleryImage;
  }, [handleSelectGalleryImage]);

  // 10. Batch Background Separation Hook
  const { isBatchProcessing, handleBatchSeparateImages } = useSlicerBatchSeparation({
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
  });

  // 11. Direct BBox Crop Hook
  const {
    isDirectBBoxCropActive, directBBoxPadding, setDirectBBoxPadding,
    handleToggleDirectBBoxCrop, handleApplyDirectBBoxCrop,
  } = useSlicerDirectBBoxCrop({
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
  });

  // 12. Modals State Hook
  const {
    isTablePickerOpen, setIsTablePickerOpen,
    isJsonImportOpen, setIsJsonImportOpen,
    isEraserOpen, setIsEraserOpen,
    editingCellDef, setEditingCellDef,
    editingCellOriginalDataUrl,
    isTunerOpen, setIsTunerOpen,
    isCatalogOpen, setIsCatalogOpen,
    isSaveKitModalOpen, setIsSaveKitModalOpen,
    openEraser, closeEraser,
  } = useSlicerModalsState();

  // 13. AI Matting Hook
  const { isAIRunning, handleRunAIMatting } = useSlicerAIMatting({
    loadedImageRef,
    aiModel,
    setUserUploadedImageUrl,
    onAutoSliceAndAssemble: handleAutoSliceAndAssemble,
  });

  // 14. Canvas Interaction Hook
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
    setEditingCellOriginalDataUrl: (url) => openEraser(selectedCell!, url),
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
  }, [setAiServerStatus]);

  // External Image Listeners (for single external image prop)
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

      const extImg = new Image();
      extImg.crossOrigin = 'anonymous';
      extImg.onload = () => {
        loadedImageRef.current = extImg;
        setLoadedImage(extImg);
        initUniformDividers(extImg.width, extImg.height, currentCategory.cols, currentCategory.rows);
        redrawCanvas('original');
      };
      extImg.src = externalImageUrl;
    }
  }, [externalImageUrl, externalCategoryId, setHasExplicitlySliced, setSlicedResults, setPreviewDisplayMode, setSelectedCatId, initUniformDividers, currentCategory.cols, currentCategory.rows, redrawCanvas, slicedCanvasesRef]);

  const handleToggleCheckerTheme = useCallback(() => {
    setCheckerTheme((prev) => {
      const next = prev === 'light' ? 'dark' : 'light';
      saveCachedCheckerTheme(next);
      return next;
    });
  }, []);

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
      <SlicerMetadataHeaderBar metadata={uploadedFileMetadata} />

      {/* Main Studio Grid: Sidebar, Canvas, Grid Gallery */}
      <div style={{ flex: 1, display: 'grid', gridTemplateColumns: imageList.length > 0 ? '380px 1fr 310px' : '440px 1fr', gap: 12, minHeight: 0 }}>
        {/* Column 1: Slicer Controls */}
        <SlicerSidebarContainer
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
          filterStates={filterStates}
          onRunAIMatting={() => { pushUndoState('AI Tách Nền (GPU)'); handleRunAIMatting(); }}
          isAIRunning={isAIRunning}
          onRunDespeckleOnly={() => handleAutoSliceAndAssemble()}
          onApplyAsNewBaseImage={handleCommitAsNewBase}
          enableSmartCrop={enableSmartCrop}
          setEnableSmartCrop={setEnableSmartCrop}
          smartCropPadding={smartCropPadding}
          setSmartCropPadding={setSmartCropPadding}
          isProcessing={isProcessing}
          assemblySuccess={assemblySuccess}
          onAutoSliceAndAssemble={() => { pushUndoState('Tách nền & Lưu kho'); handleAutoSliceAndAssemble(); }}
          onCommitSliderChange={(overrides) => {
            pushUndoState('Điều chỉnh thông số');
            const currentActiveId = activeImageIdRef.current;
            const currentItem = imageList.find((it) => it.id === currentActiveId);
            const sourceUrl = currentItem?.originalUrl || currentItem?.url || userUploadedImageUrl;

            if (sourceUrl) {
              const baseImg = new Image();
              baseImg.crossOrigin = 'anonymous';
              baseImg.onload = () => {
                loadedImageRef.current = baseImg;
                handleAutoSliceAndAssemble(overrides);
              };
              baseImg.src = sourceUrl;
            } else {
              handleAutoSliceAndAssemble(overrides);
            }
          }}
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

        {/* Column 3: Vertical Gallery Column */}
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
            chromaOpts={chromaOptions}
            onBatchSeparateImages={handleBatchSeparateImages}
            isBatchProcessing={isBatchProcessing}
          />
        )}
      </div>

      {/* Modals Subcomponent */}
      <SlicerWorkbenchModals
        isEraserOpen={isEraserOpen}
        editingCellDef={editingCellDef}
        editingCellOriginalDataUrl={editingCellOriginalDataUrl}
        onCloseEraser={closeEraser}
        onSaveEraserDataUrl={(key, newDataUrl) => {
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
        isCatalogOpen={isCatalogOpen}
        onCloseCatalog={() => setIsCatalogOpen(false)}
        currentAssembly={currentAssembly}
        onApplyAssembly={(updated: Character2DAssembly) => {
          onApplyAssembly(updated);
          if (threeEngineRef.current) threeEngineRef.current.setAssembly(updated);
        }}
        threeEngineRef={threeEngineRef}
        isTunerOpen={isTunerOpen}
        onCloseTuner={() => setIsTunerOpen(false)}
        activeAngleInfo={activeAngleInfo}
        onJumpToAngle={(deg, isTop) => {
          setTurntableAngle(deg);
          if (threeEngineRef.current) threeEngineRef.current.jumpToAngle(deg, isTop);
        }}
        isSaveKitModalOpen={isSaveKitModalOpen}
        onCloseSaveKitModal={() => setIsSaveKitModalOpen(false)}
        slicedResults={slicedResults}
        categoryLabel={currentCategory.label}
        targetCategory={targetCategory}
        isTablePickerOpen={isTablePickerOpen}
        onCloseTablePicker={() => setIsTablePickerOpen(false)}
        currentCategory={currentCategory}
        onSelectGridMatrix={handleSelectGridMatrix}
        isJsonImportOpen={isJsonImportOpen}
        onCloseJsonImport={() => setIsJsonImportOpen(false)}
        selectedCatId={selectedCatId}
        setSingleImageAngle={setSingleImageAngle}
        setSingleImageSlot={setSingleImageSlot}
        hasExplicitlySliced={hasExplicitlySliced}
        handleAutoSliceAndAssemble={() => handleAutoSliceAndAssemble()}
        redrawCanvas={() => redrawCanvas()}
        showToast={showToast}
      />
    </div>
  );
};
