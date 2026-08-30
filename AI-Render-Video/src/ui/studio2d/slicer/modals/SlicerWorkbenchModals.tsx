// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React from 'react';
import { Character2DAssembly, Character2DAngle, Character2DPartType } from '../../../../types/scene2d';
import { GridCategoryDefinition, GridCellDefinition } from '../../../../core/assets/GridSliceRegistry';
import { CellPixelEraserModal } from '../../CellPixelEraserModal';
import { CharacterAssetCatalogModal } from '../../CharacterAssetCatalogModal';
import { MultiAngleTunerModal } from '../../MultiAngleTunerModal';
import { GridTablePickerModal } from '../GridTablePickerModal';
import { JsonPromptImportModal, ParsedJsonMetadataItem } from '../JsonPromptImportModal';
import { SlicerSaveKitModal } from './SlicerSaveKitModal';
import { ThreeMultiAngleBillboardEngine, AngleDetectionResult } from '../../../../core/engine2d/ThreeMultiAngleBillboardEngine';
import { SlicerUploadedImageItem } from '../hooks/useSlicerMultiImageGallery';

export interface SlicerWorkbenchModalsProps {
  // Eraser
  isEraserOpen: boolean;
  editingCellDef: GridCellDefinition | null;
  editingCellOriginalDataUrl: string;
  onCloseEraser: () => void;
  onSaveEraserDataUrl: (key: string, dataUrl: string) => void;

  // Catalog
  isCatalogOpen: boolean;
  onCloseCatalog: () => void;
  currentAssembly: Character2DAssembly;
  onApplyAssembly: (updated: Character2DAssembly) => void;
  threeEngineRef: React.RefObject<ThreeMultiAngleBillboardEngine | null>;

  // Tuner
  isTunerOpen?: boolean;
  onCloseTuner?: () => void;
  activeAngleInfo?: AngleDetectionResult;
  onJumpToAngle?: (deg: number, isTop?: boolean) => void;

  // Save Kit
  isSaveKitModalOpen: boolean;
  onCloseSaveKitModal: () => void;
  slicedResults: Map<string, string>;
  categoryLabel?: string;
  targetCategory?: string;
  checkedImageItems?: SlicerUploadedImageItem[];
  allImages?: SlicerUploadedImageItem[];

  // Table Picker
  isTablePickerOpen: boolean;
  onCloseTablePicker: () => void;
  currentCategory?: GridCategoryDefinition;
  onSelectTableLayout?: (cols: number, rows: number) => void;
  onSelectGridMatrix?: (rows: number, cols: number) => void;

  // Smart Crop
  isSmartCropOpen?: boolean;
  onCloseSmartCrop?: () => void;
  onApplySmartCropPadding?: (newPad: number) => void;

  // Json Import
  isJsonImportOpen: boolean;
  onCloseJsonImport: () => void;
  onApplyJsonImport?: (assembly: Character2DAssembly) => void;
  selectedCatId?: string;
  setSingleImageAngle?: (ang: Character2DAngle) => void;
  setSingleImageSlot?: (slot: Character2DPartType) => void;
  hasExplicitlySliced?: boolean;
  handleAutoSliceAndAssemble?: () => void;
  redrawCanvas?: () => void;
  showToast?: (msg: string, type: 'undo' | 'redo') => void;

  // 3D & AI Matting Props
  threeContainerRef?: React.RefObject<HTMLDivElement | null>;
  aiModel?: string;
  setAiModel?: (m: string) => void;
}

export const SlicerWorkbenchModals: React.FC<SlicerWorkbenchModalsProps> = ({
  isEraserOpen,
  editingCellDef,
  editingCellOriginalDataUrl,
  onCloseEraser,
  onSaveEraserDataUrl,
  isCatalogOpen,
  onCloseCatalog,
  currentAssembly,
  onApplyAssembly,
  threeEngineRef,
  isTunerOpen = false,
  onCloseTuner = () => {},
  activeAngleInfo,
  onJumpToAngle = () => {},
  isSaveKitModalOpen,
  onCloseSaveKitModal,
  slicedResults,
  categoryLabel = 'Cắt lưới 2D',
  targetCategory = 'custom',
  checkedImageItems = [],
  allImages = [],
  isTablePickerOpen,
  onCloseTablePicker,
  currentCategory,
  onSelectTableLayout,
  onSelectGridMatrix,
  isJsonImportOpen,
  onCloseJsonImport,
  onApplyJsonImport,
  selectedCatId,
  setSingleImageAngle,
  setSingleImageSlot,
  hasExplicitlySliced = false,
  handleAutoSliceAndAssemble = () => {},
  redrawCanvas = () => {},
  showToast = () => {},
}) => {
  return (
    <>
      {isEraserOpen && editingCellDef && (
        <CellPixelEraserModal
          isOpen={isEraserOpen}
          onClose={onCloseEraser}
          cellTitle={editingCellDef.label || `Ô [${editingCellDef.row + 1}, ${editingCellDef.col + 1}]`}
          initialImageDataUrl={editingCellOriginalDataUrl}
          onSave={(newDataUrl: string) => {
            const key = `${editingCellDef.row}_${editingCellDef.col}`;
            onSaveEraserDataUrl(key, newDataUrl);
          }}
        />
      )}

      {isCatalogOpen && (
        <CharacterAssetCatalogModal
          isOpen={isCatalogOpen}
          onClose={onCloseCatalog}
          currentAssembly={currentAssembly}
          onApplyAssembly={(updated: Character2DAssembly) => {
            onApplyAssembly(updated);
            if (threeEngineRef.current) threeEngineRef.current.setAssembly(updated);
          }}
        />
      )}

      {isTunerOpen && activeAngleInfo && (
        <MultiAngleTunerModal
          isOpen={isTunerOpen}
          onClose={onCloseTuner}
          currentAssembly={currentAssembly}
          activeCameraAngle={activeAngleInfo.discreteAngle}
          onApplyAssembly={(updated: Character2DAssembly) => {
            onApplyAssembly(updated);
            if (threeEngineRef.current) threeEngineRef.current.setAssembly(updated);
          }}
          onJumpToAngle={onJumpToAngle}
        />
      )}

      {isSaveKitModalOpen && (
        <SlicerSaveKitModal
          isOpen={isSaveKitModalOpen}
          onClose={onCloseSaveKitModal}
          slicedResults={slicedResults}
          categoryLabel={categoryLabel}
          initialTargetCategory={targetCategory}
          checkedImageItems={checkedImageItems}
          allImages={allImages}
        />
      )}

      {isTablePickerOpen && (
        <GridTablePickerModal
          isOpen={isTablePickerOpen}
          onClose={onCloseTablePicker}
          currentRows={currentCategory?.rows || 1}
          currentCols={currentCategory?.cols || 1}
          onSelectGrid={(rows: number, cols: number) => {
            if (onSelectTableLayout) {
              onSelectTableLayout(cols, rows);
            } else if (onSelectGridMatrix) {
              onSelectGridMatrix(rows, cols);
            }
          }}
        />
      )}

      {isJsonImportOpen && (
        <JsonPromptImportModal
          isOpen={isJsonImportOpen}
          onClose={onCloseJsonImport}
          onApplyJsonMetadata={(metadataList: ParsedJsonMetadataItem[]) => {
            if (!metadataList || metadataList.length === 0) return;
            if (onApplyJsonImport) {
              // Legacy direct json handler
            }
            if (selectedCatId === 'single_full_image') {
              const first = metadataList[0];
              if (first.angle && setSingleImageAngle) setSingleImageAngle(first.angle);
              if (first.part_id && setSingleImageSlot) setSingleImageSlot(first.part_id);
            } else if (currentCategory && currentCategory.cells) {
              metadataList.forEach((item, idx) => {
                if (idx < currentCategory.cells.length) {
                  const cell = currentCategory.cells[idx];
                  if (item.angle) cell.angle = item.angle;
                  if (item.part_id) cell.partSlot = item.part_id;
                  if (item.name || item.part_name)
                    cell.label = `${item.part_name || item.name} (${item.angle_label || item.angle_id || ''})`;
                }
              });
            }
            showToast(`✓ Đã nạp ${metadataList.length} metadata từ JSON!`, 'redo');
            if (hasExplicitlySliced) handleAutoSliceAndAssemble();
            else redrawCanvas();
          }}
        />
      )}
    </>
  );
};
