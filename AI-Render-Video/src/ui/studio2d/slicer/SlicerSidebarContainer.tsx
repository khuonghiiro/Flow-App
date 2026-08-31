import React from 'react';
import { Character2DAngle, Character2DPartType } from '../../../types/scene2d';
import { GridCategoryDefinition } from '../../../core/assets/GridSliceRegistry';
import { SlicerSidebarControls } from './SlicerSidebarControls';
import { useSlicerFilterStates } from './hooks/useSlicerFilterStates';
import { ChromaProcessOptions } from '../../../core/utils/ChromaDespeckleProcessor';

export interface SlicerSidebarContainerProps {
  targetCategory: string;
  onSelectTargetCategory: (cat: string) => void;
  selectedCatId: string;
  onSelectCatId: (catId: string) => void;
  customCategory: GridCategoryDefinition | null;
  singleImageAngle: Character2DAngle;
  onUpdateSingleImageAngle: (ang: Character2DAngle) => void;
  singleImageSlot: Character2DPartType;
  onUpdateSingleImageSlot: (slot: Character2DPartType) => void;
  onAutoDetectAngleFromFilename?: () => void;
  onOpenJsonImportModal?: () => void;
  userUploadedImageUrl: string | null;
  totalLoadedCount: number;
  onFileUpload: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onClearImage: () => void;
  fileInputRef: React.RefObject<HTMLInputElement>;
  filterStates: ReturnType<typeof useSlicerFilterStates>;
  isEyedropperActive?: boolean;
  setIsEyedropperActive?: (v: boolean) => void;
  eyedropperTarget?: 'chroma' | 'fringe' | 'smooth';
  setEyedropperTarget?: (t: 'chroma' | 'fringe' | 'smooth') => void;
  onRunAIMatting: () => void;
  isAIRunning: boolean;
  onRunDespeckleOnly: () => void;
  onApplyAsNewBaseImage: () => void;
  enableSmartCrop: boolean;
  setEnableSmartCrop: (v: boolean) => void;
  smartCropPadding: number;
  setSmartCropPadding: (v: number) => void;
  isProcessing: boolean;
  assemblySuccess: boolean;
  onAutoSliceAndAssemble: () => void;
  onCommitSliderChange: (overrides?: Partial<ChromaProcessOptions>) => void;
  slicedCount: number;
  totalCellCount: number;
  onOpenSaveKitModal: () => void;
  onOpenCatalogModal: () => void;
  onTransferToAnimationSlicer?: () => void;
  checkedCount?: number;
  checkedImageIds?: Set<string>;
  onBatchSeparateChecked?: () => Promise<void>;
  isBatchProcessing?: boolean;
}

export const SlicerSidebarContainer: React.FC<SlicerSidebarContainerProps> = ({
  targetCategory,
  onSelectTargetCategory,
  selectedCatId,
  onSelectCatId,
  customCategory,
  singleImageAngle,
  onUpdateSingleImageAngle,
  singleImageSlot,
  onUpdateSingleImageSlot,
  onAutoDetectAngleFromFilename,
  onOpenJsonImportModal,
  userUploadedImageUrl,
  totalLoadedCount,
  onFileUpload,
  onClearImage,
  fileInputRef,
  filterStates,
  isEyedropperActive: isEyedropperActiveProp,
  setIsEyedropperActive: setIsEyedropperActiveProp,
  eyedropperTarget: eyedropperTargetProp,
  setEyedropperTarget: setEyedropperTargetProp,
  onRunAIMatting,
  isAIRunning,
  onRunDespeckleOnly,
  onApplyAsNewBaseImage,
  onTransferToAnimationSlicer,
  enableSmartCrop,
  setEnableSmartCrop,
  smartCropPadding,
  setSmartCropPadding,
  isProcessing,
  assemblySuccess,
  onAutoSliceAndAssemble,
  onCommitSliderChange,
  slicedCount,
  totalCellCount,
  onOpenSaveKitModal,
  onOpenCatalogModal,
  checkedCount = 0,
  checkedImageIds,
  onBatchSeparateChecked,
  isBatchProcessing = false,
}) => {
  const {
    isEyedropperActive: internalIsEyedropperActive,
    setIsEyedropperActive: internalSetIsEyedropperActive,
    keyColorType, setKeyColorType,
    keyColorHex, setKeyColorHex,
    isolationMode, setIsolationMode,
    tolerance, setTolerance,
    feather, setFeather,
    shadowRetention, setShadowRetention,
    strokeWidth, setStrokeWidth,
    strokeColorHex, setStrokeColorHex,
    bgCleanupSubTab, setBgCleanupSubTab,
    aiModel, setAiModel,
    aiScope, setAiScope,
    aiServerStatus,
    despeckleSize, setDespeckleSize,
    whiteSpeckleSensitivity, setWhiteSpeckleSensitivity,
    keepLargestIslandOnly, setKeepLargestIslandOnly,
    eyedropperTarget: internalEyedropperTarget,
    setEyedropperTarget: internalSetEyedropperTarget,
    cleanupMode, setCleanupMode,
    fringeColorType, setFringeColorType,
    fringeColorHex, setFringeColorHex,
    defringeStrength, setDefringeStrength,
    edgeChoke, setEdgeChoke,
    edgeSmooth, setEdgeSmooth,
    smoothColorType, setSmoothColorType,
    smoothColorHex, setSmoothColorHex,
    paddingInset, setPaddingInset,
  } = filterStates;

  const isEyedropperActive = isEyedropperActiveProp !== undefined ? isEyedropperActiveProp : internalIsEyedropperActive;
  const setIsEyedropperActive = setIsEyedropperActiveProp !== undefined ? setIsEyedropperActiveProp : internalSetIsEyedropperActive;
  const eyedropperTarget = eyedropperTargetProp !== undefined ? eyedropperTargetProp : internalEyedropperTarget;
  const setEyedropperTarget = setEyedropperTargetProp !== undefined ? setEyedropperTargetProp : internalSetEyedropperTarget;

  return (
    <SlicerSidebarControls
      targetCategory={targetCategory}
      onSelectTargetCategory={onSelectTargetCategory}
      selectedCatId={selectedCatId}
      onSelectCatId={onSelectCatId}
      customCategory={customCategory}
      singleImageAngle={singleImageAngle}
      onUpdateSingleImageAngle={onUpdateSingleImageAngle}
      singleImageSlot={singleImageSlot}
      onUpdateSingleImageSlot={onUpdateSingleImageSlot}
      onAutoDetectAngleFromFilename={onAutoDetectAngleFromFilename}
      onOpenJsonImportModal={onOpenJsonImportModal}
      userUploadedImageUrl={userUploadedImageUrl}
      totalLoadedCount={totalLoadedCount}
      onFileUpload={onFileUpload}
      onClearImage={onClearImage}
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
      onRunAIMatting={onRunAIMatting}
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
      onRunDespeckleOnly={onRunDespeckleOnly}
      onApplyAsNewBaseImage={onApplyAsNewBaseImage}
      paddingInset={paddingInset}
      setPaddingInset={setPaddingInset}
      enableSmartCrop={enableSmartCrop}
      setEnableSmartCrop={setEnableSmartCrop}
      smartCropPadding={smartCropPadding}
      setSmartCropPadding={setSmartCropPadding}
      isProcessing={isProcessing}
      assemblySuccess={assemblySuccess}
      onAutoSliceAndAssemble={onAutoSliceAndAssemble}
      onCommitSliderChange={onCommitSliderChange}
      slicedCount={slicedCount}
      totalCellCount={totalCellCount}
      onOpenSaveKitModal={onOpenSaveKitModal}
      onOpenCatalogModal={onOpenCatalogModal}
      onTransferToAnimationSlicer={onTransferToAnimationSlicer}
      checkedCount={checkedCount}
      onBatchSeparateChecked={onBatchSeparateChecked}
      isBatchProcessing={isBatchProcessing}
    />
  );
};
