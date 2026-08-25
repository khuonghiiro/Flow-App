import { useState, useCallback } from 'react';
import { GridCellDefinition } from '../../../../core/assets/GridSliceRegistry';

export function useSlicerModalsState() {
  const [isTablePickerOpen, setIsTablePickerOpen] = useState<boolean>(false);
  const [isJsonImportOpen, setIsJsonImportOpen] = useState<boolean>(false);
  const [isEraserOpen, setIsEraserOpen] = useState<boolean>(false);
  const [editingCellDef, setEditingCellDef] = useState<GridCellDefinition | null>(null);
  const [editingCellOriginalDataUrl, setEditingCellOriginalDataUrl] = useState<string>('');
  const [isTunerOpen, setIsTunerOpen] = useState<boolean>(false);
  const [isCatalogOpen, setIsCatalogOpen] = useState<boolean>(false);
  const [isSaveKitModalOpen, setIsSaveKitModalOpen] = useState<boolean>(false);

  const openEraser = useCallback((cellDef: GridCellDefinition, dataUrl: string) => {
    setEditingCellDef(cellDef);
    setEditingCellOriginalDataUrl(dataUrl);
    setIsEraserOpen(true);
  }, []);

  const closeEraser = useCallback(() => {
    setIsEraserOpen(false);
    setEditingCellDef(null);
  }, []);

  return {
    isTablePickerOpen,
    setIsTablePickerOpen,
    isJsonImportOpen,
    setIsJsonImportOpen,
    isEraserOpen,
    setIsEraserOpen,
    editingCellDef,
    setEditingCellDef,
    editingCellOriginalDataUrl,
    setEditingCellOriginalDataUrl,
    isTunerOpen,
    setIsTunerOpen,
    isCatalogOpen,
    setIsCatalogOpen,
    isSaveKitModalOpen,
    setIsSaveKitModalOpen,
    openEraser,
    closeEraser,
  };
}
