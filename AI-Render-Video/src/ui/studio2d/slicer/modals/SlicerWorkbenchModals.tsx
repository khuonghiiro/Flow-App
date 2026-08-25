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
  isTunerOpen: boolean;
  onCloseTuner: () => void;
  activeAngleInfo: AngleDetectionResult;
  onJumpToAngle: (deg: number, isTop?: boolean) => void;

  // Save Kit
  isSaveKitModalOpen: boolean;
  onCloseSaveKitModal: () => void;
  slicedResults: Map<string, string>;
  categoryLabel: string;
  targetCategory: string;

  // Table Picker
  isTablePickerOpen: boolean;
  onCloseTablePicker: () => void;
  currentCategory: GridCategoryDefinition;
  onSelectGridMatrix: (rows: number, cols: number) => void;

  // Json Import
  isJsonImportOpen: boolean;
  onCloseJsonImport: () => void;
  selectedCatId: string;
  setSingleImageAngle: (ang: Character2DAngle) => void;
  setSingleImageSlot: (slot: Character2DPartType) => void;
  hasExplicitlySliced: boolean;
  handleAutoSliceAndAssemble: () => void;
  redrawCanvas: () => void;
  showToast: (msg: string, type: 'undo' | 'redo') => void;
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
  isTunerOpen,
  onCloseTuner,
  activeAngleInfo,
  onJumpToAngle,
  isSaveKitModalOpen,
  onCloseSaveKitModal,
  slicedResults,
  categoryLabel,
  targetCategory,
  isTablePickerOpen,
  onCloseTablePicker,
  currentCategory,
  onSelectGridMatrix,
  isJsonImportOpen,
  onCloseJsonImport,
  selectedCatId,
  setSingleImageAngle,
  setSingleImageSlot,
  hasExplicitlySliced,
  handleAutoSliceAndAssemble,
  redrawCanvas,
  showToast,
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

      {isTunerOpen && (
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
        />
      )}

      {isTablePickerOpen && (
        <GridTablePickerModal
          isOpen={isTablePickerOpen}
          onClose={onCloseTablePicker}
          currentRows={currentCategory.rows}
          currentCols={currentCategory.cols}
          onSelectGrid={onSelectGridMatrix}
        />
      )}

      {isJsonImportOpen && (
        <JsonPromptImportModal
          isOpen={isJsonImportOpen}
          onClose={onCloseJsonImport}
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
                  if (item.name || item.part_name)
                    cell.label = `${item.part_name || item.name} (${item.angle_label || item.angle_id || ''})`;
                }
              });
            }
            showToast(`✓ Đã nạp ${metadataList.length} metadata từ JSON Tab 4!`, 'redo');
            if (hasExplicitlySliced) handleAutoSliceAndAssemble();
            else redrawCanvas();
          }}
        />
      )}
    </>
  );
};
