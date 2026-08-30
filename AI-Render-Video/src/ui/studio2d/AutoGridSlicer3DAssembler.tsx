// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React, { useState, useRef, useEffect, useCallback, useMemo } from 'react';
import { Character2DAssembly, Character2DPartType } from '../../types/scene2d';
import { parsePartFilename, ParsedPartFilenameInfo } from '../../core/assets/Asset2DRegistry';
import { PaddedCropRect } from '../../core/utils/PixelBoundingBoxAlgorithms';
import {
  CheckerboardTheme,
  loadCachedCheckerTheme,
  saveCachedCheckerTheme,
  getAngleDefinitionById,
} from '../../core/assets/slicer/SlicerAngleConstants';

// Subcomponents & Custom Hooks
import { SlicerThreeColumnLayout } from './slicer/components/SlicerThreeColumnLayout';
import { SlicerMetadataHeaderBar } from './slicer/SlicerMetadataHeaderBar';
import { SlicerWorkbenchModals } from './slicer/modals/SlicerWorkbenchModals';
import { useSlicerFilterStates } from './slicer/hooks/useSlicerFilterStates';
import { useSlicerDividers } from './slicer/hooks/useSlicerDividers';
import { useSlicerUndoRedoManager } from './slicer/hooks/useSlicerUndoRedoManager';
import { useSlicerCanvasDrawing } from './slicer/hooks/useSlicerCanvasDrawing';
import { useSlicerSlicingPipeline } from './slicer/hooks/useSlicerSlicingPipeline';
import { useSlicerAIMatting } from './slicer/hooks/useSlicerAIMatting';
import { useSlicerCanvasInteraction } from './slicer/hooks/useSlicerCanvasInteraction';
import { useSlicerMultiImageGallery } from './slicer/hooks/useSlicerMultiImageGallery';
import { useSlicerBatchSeparation } from './slicer/hooks/useSlicerBatchSeparation';
import { useSlicerDirectBBoxCrop } from './slicer/hooks/useSlicerDirectBBoxCrop';
import { useSlicerModalsState } from './slicer/hooks/useSlicerModalsState';
import { useSlicerCategoryState } from './slicer/hooks/useSlicerCategoryState';
import { useSlicer3DEngine } from './slicer/hooks/useSlicer3DEngine';
import { useSlicerEyedropperManager } from './slicer/hooks/useSlicerEyedropperManager';
import { useSlicerAnimationTransfer } from './slicer/hooks/useSlicerAnimationTransfer';
import { useSlicerImageSync } from './slicer/hooks/useSlicerImageSync';

interface AutoGridSlicer3DAssemblerProps {
  currentAssembly: Character2DAssembly;
  onApplyAssembly: (updatedAssembly: Character2DAssembly) => void;
  onSwitchToAssemblyTab?: () => void;
  onTransferToAnimationSlicer?: (data: { frames?: string[]; spriteSheetUrl?: string }) => void;
  externalImageUrl?: string | null;
  externalCategoryId?: string | null;
}

export const AutoGridSlicer3DAssembler: React.FC<AutoGridSlicer3DAssemblerProps> = ({
  currentAssembly,
  onApplyAssembly,
  onSwitchToAssemblyTab,
  onTransferToAnimationSlicer,
  externalImageUrl,
  externalCategoryId,
}) => {
  // 1. Source Image States
  const [userUploadedImageUrl, setUserUploadedImageUrl] = useState<string | null>(null);
  const [loadedImage, setLoadedImage] = useState<HTMLImageElement | null>(null);
  const loadedImageRef = useRef<HTMLImageElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [uploadedFileMetadata, setUploadedFileMetadata] = useState<ParsedPartFilenameInfo | null>(null);

  // 2. Category & Grid Matrix State Hook
  const {
    selectedCatId,
    setSelectedCatId,
    currentCategory,
    singleImageSlot,
    setSingleImageSlot,
    singleImageAngle,
    setSingleImageAngle,
    handleToggleSingleImageMode,
  } = useSlicerCategoryState({ externalCategoryId });

  // 3. Grid Dividers Hook
  const {
    colDividers,
    setColDividers,
    rowDividers,
    setRowDividers,
    selectedCell,
    setSelectedCell,
    adjustColWidth,
    initUniformDividers,
    autoFitDividers,
    resetAllDividers,
  } = useSlicerDividers();

  // 4. Chroma & Filter States Hook
  const filterStates = useSlicerFilterStates();
  const {
    keyColorType,
    setKeyColorType,
    keyColorHex,
    setKeyColorHex,
    isolationMode,
    setIsolationMode,
    tolerance,
    setTolerance,
    feather,
    setFeather,
    shadowRetention,
    setShadowRetention,
    strokeWidth,
    setStrokeWidth,
    strokeColorHex,
    setStrokeColorHex,
    bgCleanupSubTab,
    setBgCleanupSubTab,
    cleanupMode,
    setCleanupMode,
    fringeColorType,
    setFringeColorType,
    fringeColorHex,
    setFringeColorHex,
    defringeStrength,
    setDefringeStrength,
    edgeChoke,
    setEdgeChoke,
    edgeSmooth,
    setEdgeSmooth,
    smoothColorType,
    setSmoothColorType,
    smoothColorHex,
    setSmoothColorHex,
    despeckleSize,
    setDespeckleSize,
    whiteSpeckleSensitivity,
    setWhiteSpeckleSensitivity,
    darkSpeckleSensitivity,
    setDarkSpeckleSensitivity,
    chromaOptions,
    paddingInset,
    setPaddingInset,
    enableSmartCrop,
    setEnableSmartCrop,
    smartCropPadding,
    setSmartCropPadding,
    targetCategory,
  } = filterStates;

  // 5. Canvas & Processing Refs
  const imageCanvasRef = useRef<HTMLCanvasElement | null>(null);
  const slicedCanvasesRef = useRef<Map<string, HTMLCanvasElement>>(new Map());
  const cellCropRectsRef = useRef<Map<string, PaddedCropRect>>(new Map());
  const draggingDividerRef = useRef<{ isCol: boolean; index: number } | null>(null);

  const [previewDisplayMode, setPreviewDisplayMode] = useState<'transparent' | 'original'>('transparent');
  const [checkerTheme, setCheckerTheme] = useState<CheckerboardTheme>(loadCachedCheckerTheme());
  const [slicedResults, setSlicedResults] = useState<Map<string, string>>(new Map());
  const [hasExplicitlySliced, setHasExplicitlySliced] = useState<boolean>(false);
  const [isProcessing, setIsProcessing] = useState<boolean>(false);
  const [assemblySuccess, setAssemblySuccess] = useState<boolean>(false);

  // 6. Multi-Image Gallery Hook
  const {
    imageList,
    setImageList,
    activeImageId,
    activeImageIdRef,
    activePartId,
    partGroups,
    handleSelectImage: baseSelectImage,
    handleSelectPart,
    handleAddFiles,
    handleRemoveImage,
    handleClearAll,
  } = useSlicerMultiImageGallery({
    onAutoDetectImage: (file) => {
      const meta = parsePartFilename(file.name);
      if (meta) {
        setUploadedFileMetadata(meta);
        const def = getAngleDefinitionById(meta.angle_id);
        setSingleImageAngle(def.angle);
        if (meta.part_id) setSingleImageSlot(meta.part_id as Character2DPartType);
      }
    },
  });

  const [checkedImageIds, setCheckedImageIds] = useState<Set<string>>(new Set());
  const checkedImageItems = useMemo(
    () => imageList.filter((item) => checkedImageIds.has(item.id)),
    [imageList, checkedImageIds]
  );

  // 7. 3D Engine Hook
  const { threeContainerRef, threeEngineRef, billboardOrientation } = useSlicer3DEngine({
    currentAssembly,
  });

  // 8. Undo/Redo Manager Hook
  const {
    undoStack,
    redoStack,
    historyToast,
    showToast,
    pushUndoState,
    handleUndo,
    handleRedo,
  } = useSlicerUndoRedoManager({
    userUploadedImageUrl,
    setUserUploadedImageUrl,
    selectedCatId,
    setSelectedCatId,
    keyColorType,
    setKeyColorType,
    keyColorHex,
    setKeyColorHex,
    isolationMode,
    setIsolationMode,
    tolerance,
    setTolerance,
    feather,
    setFeather,
    shadowRetention,
    setShadowRetention,
    strokeWidth,
    setStrokeWidth,
    strokeColorHex,
    setStrokeColorHex,
    bgCleanupSubTab,
    setBgCleanupSubTab,
    cleanupMode,
    setCleanupMode,
    fringeColorType,
    setFringeColorType,
    fringeColorHex,
    setFringeColorHex,
    defringeStrength,
    setDefringeStrength,
    edgeChoke,
    setEdgeChoke,
    edgeSmooth,
    setEdgeSmooth,
    smoothColorType,
    setSmoothColorType,
    smoothColorHex,
    setSmoothColorHex,
    despeckleSize,
    setDespeckleSize,
    whiteSpeckleSensitivity,
    setWhiteSpeckleSensitivity,
    darkSpeckleSensitivity,
    setDarkSpeckleSensitivity,
    paddingInset,
    setPaddingInset,
    colDividers,
    setColDividers,
    rowDividers,
    setRowDividers,
    enableSmartCrop,
    setEnableSmartCrop,
    smartCropPadding,
    setSmartCropPadding,
    singleImageSlot,
    setSingleImageSlot,
    singleImageAngle,
    setSingleImageAngle,
    previewDisplayMode,
    setPreviewDisplayMode,
    checkerTheme,
    setCheckerTheme,
    slicedResults,
    setSlicedResults,
    currentAssembly,
    onApplyAssembly,
    threeEngineRef,
  });

  // 9. Canvas Drawing Hook
  const { redrawCanvas } = useSlicerCanvasDrawing({
    imageCanvasRef,
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

  useEffect(() => {
    redrawCanvas();
  }, [colDividers, rowDividers, redrawCanvas]);

  // 10. Slicing Pipeline Hook
  const { handleAutoSliceAndAssemble } = useSlicerSlicingPipeline({
    userUploadedImageUrl,
    loadedImageRef,
    currentCategory,
    colDividers,
    rowDividers,
    paddingInset,
    chromaOptions,
    cellCropRectsRef,
    enableSmartCrop,
    smartCropPadding,
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
  });

  // 11. Eyedropper Manager Hook
  const {
    isEyedropperActive,
    setIsEyedropperActive,
    eyedropperTarget,
    setEyedropperTarget,
    eyedropperHoverColor,
    setEyedropperHoverColor,
    handlePickColor,
  } = useSlicerEyedropperManager({
    hasExplicitlySliced,
    handleAutoSliceAndAssemble,
    setSmoothColorType,
    setSmoothColorHex,
    setFringeColorType,
    setFringeColorHex,
    setKeyColorType,
    setKeyColorHex,
  });

  // 12. Image Sync & Dividers Manager Hook
  const {
    dividerSyncMode,
    handleToggleDividerSyncMode,
    handleUpdateItemImage,
    handleUpdateItemDividers,
    handleSelectImage,
    handleFullClear,
  } = useSlicerImageSync({
    imageList,
    setImageList,
    activeImageIdRef,
    loadedImageRef,
    setLoadedImage,
    setUserUploadedImageUrl,
    setUploadedFileMetadata,
    setHasExplicitlySliced,
    setSlicedResults,
    slicedCanvasesRef,
    setPreviewDisplayMode,
    currentCategory,
    setSelectedCatId,
    setColDividers,
    setRowDividers,
    initUniformDividers,
    redrawCanvas,
    pushUndoState,
    showToast,
    baseSelectImage,
    handleClearAll,
    externalImageUrl,
    externalCategoryId,
  });

  // 13. Batch Separation Hook
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
    setCheckedImageIds,
  });

  // 14. Direct BBox Crop Hook
  const {
    isDirectBBoxCropActive,
    directBBoxPadding,
    setDirectBBoxPadding,
    handleToggleDirectBBoxCrop,
    handleApplyDirectBBoxCrop,
  } = useSlicerDirectBBoxCrop({
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
  });

  // 15. Modals State Hook
  const {
    isCatalogOpen,
    setIsCatalogOpen,
    isSaveKitModalOpen,
    setIsSaveKitModalOpen,
    isTablePickerOpen,
    setIsTablePickerOpen,
    isSmartCropOpen,
    setIsSmartCropOpen,
    isJsonImportOpen,
    setIsJsonImportOpen,
    isEraserOpen,
    editingCellDef,
    editingCellOriginalDataUrl,
    openCellPixelEditor,
    closeEraser,
  } = useSlicerModalsState({
    loadedImageRef,
    colDividers,
    rowDividers,
    paddingInset,
  });

  // 16. AI Matting Hook
  const { isAIRunning, aiModel, setAiModel, handleRunAIMatting } = useSlicerAIMatting({
    loadedImageRef,
    setUserUploadedImageUrl,
    onAutoSliceAndAssemble: handleAutoSliceAndAssemble,
    showToast,
  });

  // 17. Canvas Interaction Hook (Divider dragging, Commit Base)
  const {
    handleCanvasMouseDown,
    handleCanvasMouseMove,
    handleCommitAsNewBase,
  } = useSlicerCanvasInteraction({
    imageCanvasRef,
    loadedImageRef,
    setUserUploadedImageUrl,
    setLoadedImage,
    isEyedropperActive,
    setIsEyedropperActive,
    eyedropperTarget: eyedropperTarget === 'smooth' ? 'smooth' : eyedropperTarget === 'fringe' ? 'fringe' : 'chroma',
    setEyedropperHoverColor: (c) => setEyedropperHoverColor(c ? c.hex : null),
    setSmoothColorType: (t) => setSmoothColorType(t as any),
    setSmoothColorHex,
    setFringeColorType: (t) => setFringeColorType(t === 'white' ? 'white' : t === 'black' ? 'black' : 'custom'),
    setFringeColorHex,
    setKeyColorType: (t) => setKeyColorType(t === 'white' ? 'white' : t === 'green' ? 'green' : 'custom'),
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
    setEditingCellDef: () => {},
    setEditingCellOriginalDataUrl: () => {},
    setIsEraserOpen: () => {},
    setPreviewDisplayMode,
    redrawCanvas,
  });

  const [eraserMode, setEraserMode] = useState<'off' | 'brush' | 'box'>('off');
  const [eraserBrushSize, setEraserBrushSize] = useState<number>(20);

  const handleToggleCheckerTheme = useCallback(() => {
    setCheckerTheme((prev) => {
      const next = prev === 'light' ? 'dark' : 'light';
      saveCachedCheckerTheme(next as CheckerboardTheme);
      return next;
    });
  }, []);

  // 18. Animation Transfer Hook
  const { handleTransferToAnimationSlicer } = useSlicerAnimationTransfer({
    imageList,
    checkedImageIds,
    activeImageId,
    currentCategory,
    colDividers,
    rowDividers,
    paddingInset,
    chromaOptions,
    onTransferToAnimationSlicer,
  });

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        width: '100%',
        background: '#090d16',
        color: '#f8fafc',
        fontFamily: "'Inter', sans-serif",
        overflow: 'hidden',
        position: 'relative',
      }}
    >
      <SlicerMetadataHeaderBar
        selectedCategoryTitle={currentCategory.title}
        selectedCategoryIcon={currentCategory.icon}
        currentAngleId={singleImageAngle}
        currentSlotId={singleImageSlot}
        billboardOrientation={billboardOrientation}
        totalLoadedImages={imageList.length}
        activeImageName={imageList.find((it) => it.id === activeImageId)?.name}
        is3DModeActive={Boolean(currentAssembly)}
        onSwitchToAssemblyTab={onSwitchToAssemblyTab}
      />

      <div style={{ flex: 1, height: '100%', minHeight: 0, display: 'flex', flexDirection: 'column', padding: 12, overflow: 'hidden', boxSizing: 'border-box' }}>
        <SlicerThreeColumnLayout
          showGallery={imageList.length > 0}
          sidebarProps={{
            currentCategory,
            onOpenGridTablePicker: () => setIsTablePickerOpen(true),
            singleImageSlot,
            singleImageAngle,
            onUpdateSingleImageAngle: (ang) => {
              setSingleImageAngle(ang);
              if (hasExplicitlySliced) handleAutoSliceAndAssemble();
            },
            onUpdateSingleImageSlot: (slot) => {
              setSingleImageSlot(slot);
              if (hasExplicitlySliced) handleAutoSliceAndAssemble();
            },
            uploadedFileMetadata,
            onApplyDetectedMetadata: () => {
              if (!uploadedFileMetadata && !userUploadedImageUrl) return;
              const meta = uploadedFileMetadata || (userUploadedImageUrl ? parsePartFilename(userUploadedImageUrl) : null);
              if (meta) {
                const def = getAngleDefinitionById(meta.angle_id);
                setSingleImageAngle(def.angle);
                if (meta.part_id) setSingleImageSlot(meta.part_id as Character2DPartType);
                showToast(`✓ Đã nhận diện: ${meta.part_name} (${meta.angle_name})`, 'redo');
                if (hasExplicitlySliced) handleAutoSliceAndAssemble();
              }
            },
            onOpenJsonImportModal: () => setIsJsonImportOpen(true),
            userUploadedImageUrl,
            totalLoadedCount: imageList.length,
            onFileUpload: (e) => {
              const files = e.target.files;
              if (files && files.length > 0) {
                pushUndoState(`Tải ${files.length} ảnh`);
                handleAddFiles(files);
              }
            },
            onClearImage: handleFullClear,
            fileInputRef,
            filterStates,
            onRunAIMatting: () => {
              pushUndoState('AI Tách Nền (GPU)');
              handleRunAIMatting();
            },
            isAIRunning,
            onRunDespeckleOnly: () => handleAutoSliceAndAssemble(),
            onApplyAsNewBaseImage: handleCommitAsNewBase,
            enableSmartCrop,
            setEnableSmartCrop,
            smartCropPadding,
            setSmartCropPadding,
            isProcessing,
            assemblySuccess,
            onAutoSliceAndAssemble: () => {
              pushUndoState('Tách nền & Lưu kho');
              const isMultiCellGrid = currentCategory.id !== 'single_full_image' && currentCategory.cells && currentCategory.cells.length > 1;
              if (isMultiCellGrid) {
                if (checkedImageIds.size > 0) {
                  handleBatchSeparateImages(Array.from(checkedImageIds));
                } else if (activeImageIdRef.current) {
                  handleBatchSeparateImages([activeImageIdRef.current]);
                } else {
                  handleAutoSliceAndAssemble();
                }
              } else {
                handleAutoSliceAndAssemble();
              }
            },
            onCommitSliderChange: (overrides) => {
              pushUndoState('Điều chỉnh thông số');
              const currentActiveId = activeImageIdRef.current;
              const currentItem = imageList.find((it) => it.id === currentActiveId);
              const sourceUrl = currentItem?.originalUrl || currentItem?.url || userUploadedImageUrl;

              if (hasExplicitlySliced) {
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

                if (checkedImageIds.size > 0) {
                  handleBatchSeparateImages(Array.from(checkedImageIds), overrides);
                } else if (activeImageIdRef.current) {
                  handleBatchSeparateImages([activeImageIdRef.current], overrides);
                }
              } else {
                redrawCanvas('original');
              }
            },
            slicedCount: slicedResults.size,
            totalCellCount: currentCategory.id === 'single_full_image' ? 1 : currentCategory.cells.length,
            onOpenSaveKitModal: () => setIsSaveKitModalOpen(true),
            onOpenCatalogModal: () => setIsCatalogOpen(true),
            onTransferToAnimationSlicer: handleTransferToAnimationSlicer,
            checkedCount: checkedImageIds.size,
            checkedImageIds,
            onBatchSeparateChecked: async () => {
              if (checkedImageIds.size > 0) {
                await handleBatchSeparateImages(Array.from(checkedImageIds));
              }
            },
            isBatchProcessing,
          }}
          canvasProps={{
            imageCanvasRef,
            hasImage: Boolean(userUploadedImageUrl),
            loadedImage,
            loadedImageRef,
            previewDisplayMode,
            setPreviewDisplayMode,
            hasExplicitlySliced,
            slicedCanvasesRef,
            handleAutoSliceAndAssemble,
            redrawCanvas,
            checkerTheme,
            handleToggleCheckerTheme,
            selectedCatId,
            handleToggleSingleImageMode,
            setIsTablePickerOpen,
            isEyedropperActive,
            eyedropperTarget,
            eyedropperHoverColor,
            handlePickColor,
            setEyedropperHoverColor,
            currentCategory,
            keyColorType,
            keyColorHex,
            autoFitDividers,
            initUniformDividers,
            canUndo: undoStack.length > 0,
            canRedo: redoStack.length > 0,
            handleUndo,
            handleRedo,
            historyToast,
            handleCanvasMouseDown,
            handleCanvasMouseMove,
            draggingDividerRef,
            selectedCell,
            setSelectedCell,
            openCellPixelEditor,
            isDirectBBoxCropActive,
            handleToggleDirectBBoxCrop,
            directBBoxPadding,
            setDirectBBoxPadding,
            handleApplyDirectBBoxCrop,
            paddingInset,
            chromaOptions,
            imageList,
            checkedImageIds,
            activeImageId,
            handleSelectImage,
            setCheckedImageIds,
            eraserMode,
            setEraserMode,
            eraserBrushSize,
            setEraserBrushSize,
            handleUpdateItemImage,
            slicedResults,
            adjustColWidth,
            resetAllDividers,
            colDividers,
            rowDividers,
            setColDividers,
            setRowDividers,
            dividerSyncMode,
            onToggleDividerSyncMode: handleToggleDividerSyncMode,
            onUpdateItemDividers: handleUpdateItemDividers,
          }}
          galleryProps={{
            partGroups,
            activePartId,
            activeImageId,
            onSelectPart: handleSelectPart,
            onSelectImage: handleSelectImage,
            onRemoveImage: handleRemoveImage,
            onClearAll: handleFullClear,
            onOpenAddFiles: () => fileInputRef.current?.click(),
            totalImagesCount: imageList.length,
            previewDisplayMode,
            chromaOpts: chromaOptions,
            onBatchSeparateImages: handleBatchSeparateImages,
            isBatchProcessing,
            onCheckedIdsChange: (ids) => setCheckedImageIds(ids),
            checkedImageIds,
            isProcessing,
            assemblySuccess,
            slicedCount: slicedResults.size,
            totalCellCount: currentCategory.id === 'single_full_image' ? 1 : (currentCategory.cells?.length || currentCategory.cols * currentCategory.rows),
            onAutoSliceAndAssemble: handleAutoSliceAndAssemble,
            onOpenCatalogModal: () => setIsCatalogOpen(true),
            onOpenSaveKitModal: () => setIsSaveKitModalOpen(true),
            onTransferToAnimationSlicer: handleTransferToAnimationSlicer,
            onSwitchToAssemblyTab,
            isDirectBBoxCropActive,
            directBBoxPadding,
            setDirectBBoxPadding,
            onToggleDirectBBoxCrop: handleToggleDirectBBoxCrop,
            onApplyDirectBBoxCrop: handleApplyDirectBBoxCrop,
          }}
        />
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
        isSmartCropOpen={isSmartCropOpen}
        onCloseSmartCrop={() => setIsSmartCropOpen(false)}
        onApplySmartCropPadding={(newPad) => {
          setSmartCropPadding(newPad);
          setIsSmartCropOpen(false);
          handleAutoSliceAndAssemble();
        }}
        isTablePickerOpen={isTablePickerOpen}
        onCloseTablePicker={() => setIsTablePickerOpen(false)}
        currentCategory={currentCategory}
        onSelectTableLayout={(cols, rows) => {
          setIsTablePickerOpen(false);
          initUniformDividers(loadedImageRef.current?.width || 800, loadedImageRef.current?.height || 600, cols, rows);
        }}
        isCatalogOpen={isCatalogOpen}
        onCloseCatalog={() => setIsCatalogOpen(false)}
        isSaveKitModalOpen={isSaveKitModalOpen}
        onCloseSaveKitModal={() => setIsSaveKitModalOpen(false)}
        currentAssembly={currentAssembly}
        onApplyAssembly={(assembly) => {
          onApplyAssembly(assembly);
          if (threeEngineRef.current) threeEngineRef.current.setAssembly(assembly);
        }}
        slicedResults={slicedResults}
        checkedImageItems={checkedImageItems}
        allImages={imageList}
        categoryLabel={currentCategory.name}
        targetCategory={targetCategory}
        isJsonImportOpen={isJsonImportOpen}
        onCloseJsonImport={() => setIsJsonImportOpen(false)}
        selectedCatId={selectedCatId}
        onApplyJsonImport={(assembly) => {
          setIsJsonImportOpen(false);
          onApplyAssembly(assembly);
          showToast('✓ Đã nạp cấu hình nhân vật JSON thành công!', 'redo');
        }}
        threeContainerRef={threeContainerRef}
        threeEngineRef={threeEngineRef}
        aiModel={aiModel}
        setAiModel={setAiModel}
        handleAutoSliceAndAssemble={() => handleAutoSliceAndAssemble()}
        hasExplicitlySliced={hasExplicitlySliced}
        redrawCanvas={redrawCanvas}
        showToast={showToast}
      />
    </div>
  );
};
