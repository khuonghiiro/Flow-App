import { useState, useRef, useCallback } from 'react';
import { detectAndFitGridDividers } from '../../../../core/utils/GridAutoFitDetector';
import { GridCellDefinition } from '../../../../core/assets/GridSliceRegistry';

export function useSlicerDividers() {
  const [colDividers, setColDividers] = useState<number[]>([]);
  const [rowDividers, setRowDividers] = useState<number[]>([]);
  const draggingDividerRef = useRef<{ type: 'col' | 'row'; index: number } | null>(null);

  // Initialize Default Uniform Dividers (Chia đều tuyệt đối các ô trong 1 ảnh)
  const initUniformDividers = useCallback((width: number, height: number, cols: number, rows: number) => {
    if (!width || !height || cols < 1 || rows < 1) return;
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
      const w = img.naturalWidth || img.width;
      const h = img.naturalHeight || img.height;
      if (cols <= 1 && rows <= 1) {
        initUniformDividers(w, h, cols, rows);
        return;
      }
      try {
        const result = detectAndFitGridDividers(img, cols, rows, keyType, keyHex);
        setColDividers(result.colDividers);
        setRowDividers(result.rowDividers);
      } catch (err) {
        console.warn('AutoFit grid detection failed, using uniform grid:', err);
        initUniformDividers(w, h, cols, rows);
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
        const w = img.naturalWidth || img.width;
        const h = img.naturalHeight || img.height;
        initUniformDividers(w, h, cols, rows);
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
