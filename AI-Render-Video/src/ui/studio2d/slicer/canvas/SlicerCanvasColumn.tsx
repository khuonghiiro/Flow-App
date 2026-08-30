// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React from 'react';
import { SlicerInteractiveCanvas } from '../SlicerInteractiveCanvas';
import { SlicerCellAdjustmentBar } from '../SlicerCellAdjustmentBar';
import { GridCategoryDefinition, GridCellDefinition } from '../../../../core/assets/GridSliceRegistry';
import { ChromaProcessOptions } from '../../../../core/utils/ChromaDespeckleProcessor';
import { SlicerUploadedImageItem } from '../hooks/useSlicerMultiImageGallery';

export interface SlicerCanvasColumnProps {
  imageCanvasRef: React.RefObject<HTMLCanvasElement>;
  hasImage: boolean;
  loadedImage: HTMLImageElement | null;
  loadedImageRef: React.MutableRefObject<HTMLImageElement | null>;
  previewDisplayMode: 'transparent' | 'original';
  setPreviewDisplayMode: (mode: 'transparent' | 'original') => void;
  hasExplicitlySliced: boolean;
  slicedCanvasesRef: React.MutableRefObject<Map<string, HTMLCanvasElement>>;
  handleAutoSliceAndAssemble: (overrides?: Partial<ChromaProcessOptions>) => void;
  redrawCanvas: (modeOverride?: 'transparent' | 'original') => void;
  checkerTheme: 'dark' | 'light';
  handleToggleCheckerTheme: () => void;
  selectedCatId: string;
  handleToggleSingleImageMode: () => void;
  setIsTablePickerOpen: (open: boolean) => void;
  isEyedropperActive: boolean;
  eyedropperTarget: 'key' | 'fringe' | 'smooth' | null;
  eyedropperHoverColor: string | null;
  handlePickColor: (hex: string) => void;
  setEyedropperHoverColor: (color: string | null) => void;
  currentCategory: GridCategoryDefinition;
  keyColorType: 'green' | 'magenta' | 'black' | 'white' | 'custom';
  keyColorHex: string;
  autoFitDividers: (img: HTMLImageElement, cols: number, rows: number, keyType: string, keyHex: string) => void;
  initUniformDividers: (w: number, h: number, cols: number, rows: number) => void;
  canUndo: boolean;
  canRedo: boolean;
  handleUndo: () => void;
  handleRedo: () => void;
  historyToast: { message: string; type: 'undo' | 'redo' } | null;
  handleCanvasMouseDown: (e: React.MouseEvent<HTMLCanvasElement>) => void;
  handleCanvasMouseMove: (e: React.MouseEvent<HTMLCanvasElement>) => void;
  draggingDividerRef: React.MutableRefObject<unknown>;
  selectedCell: GridCellDefinition | null;
  setSelectedCell: (cell: GridCellDefinition | null) => void;
  openCellPixelEditor: (cell: GridCellDefinition) => void;
  isDirectBBoxCropActive: boolean;
  handleToggleDirectBBoxCrop: () => void;
  directBBoxPadding: number;
  setDirectBBoxPadding: (pad: number) => void;
  handleApplyDirectBBoxCrop: () => void;
  paddingInset: number;
  chromaOptions: ChromaProcessOptions;
  imageList: SlicerUploadedImageItem[];
  checkedImageIds: Set<string>;
  activeImageId: string | null;
  handleSelectImage: (item: SlicerUploadedImageItem) => void;
  setCheckedImageIds: React.Dispatch<React.SetStateAction<Set<string>>>;
  eraserMode: 'off' | 'brush' | 'box';
  setEraserMode: (mode: 'off' | 'brush' | 'box') => void;
  eraserBrushSize: number;
  setEraserBrushSize: (size: number) => void;
  handleUpdateItemImage: (id: string, newDataUrl: string) => void;
  slicedResults: Map<string, string>;
  adjustColWidth: (cell: GridCellDefinition, delta: number) => void;
  resetAllDividers: (img: HTMLImageElement | null, cols: number, rows: number) => void;
  colDividers?: number[];
  rowDividers?: number[];
  setColDividers?: React.Dispatch<React.SetStateAction<number[]>>;
  setRowDividers?: React.Dispatch<React.SetStateAction<number[]>>;
  dividerSyncMode?: 'all' | 'single';
  onToggleDividerSyncMode?: () => void;
  onUpdateItemDividers?: (itemId: string, newColDividers?: number[], newRowDividers?: number[]) => void;
}

export const SlicerCanvasColumn: React.FC<SlicerCanvasColumnProps> = ({
  imageCanvasRef,
  hasImage,
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
  canUndo,
  canRedo,
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
  onToggleDividerSyncMode,
  onUpdateItemDividers,
}) => {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8, minHeight: 0, overflow: 'hidden' }}>
      <SlicerInteractiveCanvas
        imageCanvasRef={imageCanvasRef}
        hasImage={hasImage}
        loadedImage={loadedImage}
        previewDisplayMode={previewDisplayMode}
        setPreviewDisplayMode={setPreviewDisplayMode}
        onTogglePreviewDisplayMode={(mode) => {
          setPreviewDisplayMode(mode);
          redrawCanvas(mode);
        }}
        checkerTheme={checkerTheme}
        onToggleCheckerTheme={handleToggleCheckerTheme}
        onOpenGridTablePicker={() => setIsTablePickerOpen(true)}
        isSingleImageMode={selectedCatId === 'single_full_image'}
        onToggleSingleImageMode={handleToggleSingleImageMode}
        hasExplicitlySliced={hasExplicitlySliced}
        isEyedropperActive={isEyedropperActive}
        eyedropperTarget={eyedropperTarget}
        eyedropperHoverColor={eyedropperHoverColor}
        onPickColor={handlePickColor}
        onHoverColor={setEyedropperHoverColor}
        currentCategory={currentCategory}
        onAutoFitGrid={() => {
          const img = loadedImage || loadedImageRef.current;
          if (img) autoFitDividers(img, currentCategory.cols, currentCategory.rows, keyColorType, keyColorHex);
        }}
        onResetUniformGrid={() => {
          const img = loadedImage || loadedImageRef.current;
          if (img) initUniformDividers(img.width, img.height, currentCategory.cols, currentCategory.rows);
        }}
        canUndo={canUndo}
        canRedo={canRedo}
        onUndo={handleUndo}
        onRedo={handleRedo}
        historyToast={historyToast}
        onMouseDown={handleCanvasMouseDown}
        onMouseMove={handleCanvasMouseMove}
        onMouseLeave={() => {
          setEyedropperHoverColor(null);
          draggingDividerRef.current = null;
        }}
        onMouseUp={() => {
          draggingDividerRef.current = null;
        }}
        onDoubleClick={() => {
          if (selectedCell) openCellPixelEditor(selectedCell);
        }}
        isDirectBBoxCropActive={isDirectBBoxCropActive}
        onToggleDirectBBoxCrop={handleToggleDirectBBoxCrop}
        directBBoxPadding={directBBoxPadding}
        setDirectBBoxPadding={setDirectBBoxPadding}
        onApplyDirectBBoxCrop={handleApplyDirectBBoxCrop}
        paddingInset={paddingInset}
        chromaOptions={chromaOptions}
        checkedImageItems={imageList.filter((it) => checkedImageIds.has(it.id))}
        activeImageId={activeImageId ?? undefined}
        onSelectCheckedImage={handleSelectImage}
        onToggleCheckedItem={(id) => {
          setCheckedImageIds((prev) => {
            const next = new Set(prev);
            if (next.has(id)) next.delete(id);
            else next.add(id);
            return next;
          });
        }}
        onClearCheckedImages={() => setCheckedImageIds(new Set())}
        eraserMode={eraserMode}
        setEraserMode={setEraserMode}
        eraserBrushSize={eraserBrushSize}
        setEraserBrushSize={setEraserBrushSize}
        onUpdateItemImage={handleUpdateItemImage}
        colDividers={colDividers}
        rowDividers={rowDividers}
        setColDividers={setColDividers}
        setRowDividers={setRowDividers}
        dividerSyncMode={dividerSyncMode}
        onToggleDividerSyncMode={onToggleDividerSyncMode}
        onUpdateItemDividers={onUpdateItemDividers}
      />

      {selectedCell && (
        <SlicerCellAdjustmentBar
          selectedCell={selectedCell}
          slicedCellDataUrl={slicedResults.get(`${selectedCell.row}_${selectedCell.col}`)}
          onOpenCellPixelEditor={(cell) => openCellPixelEditor(cell)}
          onAdjustColWidth={(delta) => {
            adjustColWidth(selectedCell, delta);
            if (hasExplicitlySliced) handleAutoSliceAndAssemble();
          }}
          onResetAllDividers={() => {
            resetAllDividers(loadedImageRef.current, currentCategory.cols, currentCategory.rows);
            if (hasExplicitlySliced) handleAutoSliceAndAssemble();
          }}
          onUpdateCellAngle={(cell, angle, mirror) => {
            cell.angle = angle;
            cell.mirrorAngle = mirror;
            if (hasExplicitlySliced) handleAutoSliceAndAssemble();
            else redrawCanvas();
          }}
          onUpdateCellSlot={(cell, slot) => {
            cell.partSlot = slot;
            if (hasExplicitlySliced) handleAutoSliceAndAssemble();
          }}
          onClose={() => setSelectedCell(null)}
        />
      )}
    </div>
  );
};
