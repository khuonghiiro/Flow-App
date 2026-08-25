import { useState, useRef, useCallback } from 'react';
import { detectAndFitGridDividers } from '../../../../core/utils/GridAutoFitDetector';
import { GridCellDefinition } from '../../../../core/assets/GridSliceRegistry';

export function useSlicerDividers() {
  const [colDividers, setColDividers] = useState<number[]>([]);
  const [rowDividers, setRowDividers] = useState<number[]>([]);
  const draggingDividerRef = useRef<{ type: 'col' | 'row'; index: number } | null>(null);

  // Initialize Default Uniform Dividers
  const initUniformDividers = useCallback((width: number, height: number, cols: number, rows: number) => {
    const colStep = width / cols;
    const rowStep = height / rows;
    const colsArr: number[] = [];
    for (let c = 0; c <= cols; c++) {
      colsArr.push(Math.round(c * colStep));
    }
    const rowsArr: number[] = [];
    for (let r = 0; r <= rows; r++) {
      rowsArr.push(Math.round(r * rowStep));
    }
    setColDividers(colsArr);
    setRowDividers(rowsArr);
  }, []);

  // Smart Content-Aware Auto-Fit Grid Dividers for AI-generated Sprite Sheets
  const autoFitDividers = useCallback(
    (
      img: HTMLImageElement,
      cols: number,
      rows: number,
      keyType: 'chroma_green' | 'pure_white' | 'custom' = 'chroma_green',
      keyHex = '#00ff00'
    ) => {
      if (cols <= 1 && rows <= 1) {
        initUniformDividers(img.width, img.height, cols, rows);
        return;
      }
      try {
        const result = detectAndFitGridDividers(img, cols, rows, keyType, keyHex);
        setColDividers(result.colDividers);
        setRowDividers(result.rowDividers);
      } catch (err) {
        console.warn('AutoFit grid detection failed, using uniform grid:', err);
        initUniformDividers(img.width, img.height, cols, rows);
      }
    },
    [initUniformDividers]
  );

  const adjustColWidth = useCallback((selectedCell: GridCellDefinition | null, deltaPx: number) => {
    if (!selectedCell) return;
    const c = selectedCell.col;
    setColDividers((prev) => {
      const next = [...prev];
      if (c + 1 < next.length) {
        next[c + 1] = Math.max(next[c] + 15, next[c + 1] + deltaPx);
      }
      return next;
    });
  }, []);

  const resetAllDividers = useCallback(
    (img: HTMLImageElement | null, cols: number, rows: number) => {
      if (img) {
        initUniformDividers(img.width, img.height, cols, rows);
      }
    },
    [initUniformDividers]
  );

  return {
    colDividers,
    setColDividers,
    rowDividers,
    setRowDividers,
    draggingDividerRef,
    initUniformDividers,
    autoFitDividers,
    adjustColWidth,
    resetAllDividers,
  };
}
