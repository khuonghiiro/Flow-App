import { useCallback } from 'react';
import { GridCategoryDefinition, GridCellDefinition } from '../../../../core/assets/GridSliceRegistry';
import { ChromaProcessOptions } from '../../../../core/utils/ChromaDespeckleProcessor';

export interface UseSlicerCanvasInteractionProps {
  imageCanvasRef: React.RefObject<HTMLCanvasElement>;
  loadedImageRef: React.MutableRefObject<HTMLImageElement | null>;
  setLoadedImage: (img: HTMLImageElement | null) => void;
  setUserUploadedImageUrl: (url: string | null) => void;
  isEyedropperActive: boolean;
  setIsEyedropperActive: (v: boolean) => void;
  eyedropperTarget: 'chroma' | 'fringe' | 'smooth';
  setEyedropperHoverColor: (c: { hex: string; r: number; g: number; b: number; x: number; y: number } | null) => void;
  setSmoothColorType: (t: 'black' | 'white' | 'auto' | 'custom') => void;
  setSmoothColorHex: (hex: string) => void;
  setFringeColorType: (t: 'chroma_green' | 'pure_white' | 'pure_black' | 'custom') => void;
  setFringeColorHex: (hex: string) => void;
  setKeyColorType: (t: 'chroma_green' | 'pure_white' | 'custom') => void;
  setKeyColorHex: (hex: string) => void;
  hasExplicitlySliced: boolean;
  setHasExplicitlySliced: (v: boolean) => void;
  handleAutoSliceAndAssemble: (overrides?: Partial<ChromaProcessOptions>) => void;
  colDividers: number[];
  setColDividers: React.Dispatch<React.SetStateAction<number[]>>;
  rowDividers: number[];
  setRowDividers: React.Dispatch<React.SetStateAction<number[]>>;
  draggingDividerRef: React.MutableRefObject<{ type: 'col' | 'row'; index: number } | null>;
  currentCategory: GridCategoryDefinition;
  setSelectedCell: (cell: GridCellDefinition | null) => void;
  slicedResults: Map<string, string>;
  setSlicedResults: (map: Map<string, string>) => void;
  slicedCanvasesRef: React.MutableRefObject<Map<string, HTMLCanvasElement>>;
  paddingInset: number;
  setEditingCellDef: (cell: GridCellDefinition | null) => void;
  setEditingCellOriginalDataUrl: (url: string) => void;
  setIsEraserOpen: (v: boolean) => void;
  setPreviewDisplayMode: (mode: 'transparent' | 'original') => void;
  redrawCanvas: (mode?: 'transparent' | 'original') => void;
}

export function useSlicerCanvasInteraction({
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
}: UseSlicerCanvasInteractionProps) {
  const handleCanvasMouseDown = useCallback(
    (e: React.MouseEvent<HTMLCanvasElement>) => {
      const canvas = imageCanvasRef.current;
      if (!canvas) return;
      const rect = canvas.getBoundingClientRect();
      const scaleX = canvas.width / rect.width;
      const scaleY = canvas.height / rect.height;
      const mouseX = (e.clientX - rect.left) * scaleX;
      const mouseY = (e.clientY - rect.top) * scaleY;

      if (isEyedropperActive) {
        const ctx = canvas.getContext('2d', { willReadFrequently: true });
        if (ctx && mouseX >= 0 && mouseX < canvas.width && mouseY >= 0 && mouseY < canvas.height) {
          const pixel = ctx.getImageData(Math.floor(mouseX), Math.floor(mouseY), 1, 1).data;
          const hex = `#${((1 << 24) + (pixel[0] << 16) + (pixel[1] << 8) + pixel[2]).toString(16).slice(1).toUpperCase()}`;
          setIsEyedropperActive(false);
          setEyedropperHoverColor(null);
          if (eyedropperTarget === 'smooth') {
            setSmoothColorType('custom');
            setSmoothColorHex(hex);
            if (hasExplicitlySliced) handleAutoSliceAndAssemble({ smoothColorType: 'custom', smoothColorHex: hex });
          } else if (eyedropperTarget === 'fringe') {
            setFringeColorType('custom');
            setFringeColorHex(hex);
            if (hasExplicitlySliced) handleAutoSliceAndAssemble({ fringeColorType: 'custom', fringeColorHex: hex });
          } else {
            setKeyColorType('custom');
            setKeyColorHex(hex);
            if (hasExplicitlySliced) handleAutoSliceAndAssemble({ keyColorType: 'custom', keyColorHex: hex });
          }
        }
        return;
      }

      for (let c = 1; c < colDividers.length - 1; c++) {
        if (Math.abs(colDividers[c] - mouseX) <= 12) {
          draggingDividerRef.current = { type: 'col', index: c };
          return;
        }
      }
      for (let r = 1; r < rowDividers.length - 1; r++) {
        if (Math.abs(rowDividers[r] - mouseY) <= 12) {
          draggingDividerRef.current = { type: 'row', index: r };
          return;
        }
      }

      let clickedCol = -1;
      for (let c = 0; c < colDividers.length - 1; c++) {
        if (mouseX >= colDividers[c] && mouseX <= colDividers[c + 1]) {
          clickedCol = c;
          break;
        }
      }
      let clickedRow = -1;
      for (let r = 0; r < rowDividers.length - 1; r++) {
        if (mouseY >= rowDividers[r] && mouseY <= rowDividers[r + 1]) {
          clickedRow = r;
          break;
        }
      }
      if (clickedCol !== -1 && clickedRow !== -1) {
        const found = currentCategory.cells.find((cell) => cell.row === clickedRow && cell.col === clickedCol);
        if (found) setSelectedCell(found);
      }
    },
    [
      imageCanvasRef,
      isEyedropperActive,
      setIsEyedropperActive,
      setEyedropperHoverColor,
      eyedropperTarget,
      setSmoothColorType,
      setSmoothColorHex,
      setFringeColorType,
      setFringeColorHex,
      setKeyColorType,
      setKeyColorHex,
      hasExplicitlySliced,
      handleAutoSliceAndAssemble,
      colDividers,
      rowDividers,
      draggingDividerRef,
      currentCategory,
      setSelectedCell,
    ]
  );

  const handleCanvasMouseMove = useCallback(
    (e: React.MouseEvent<HTMLCanvasElement>) => {
      const canvas = imageCanvasRef.current;
      if (!canvas) return;
      const rect = canvas.getBoundingClientRect();
      const mouseX = (e.clientX - rect.left) * (canvas.width / rect.width);
      const mouseY = (e.clientY - rect.top) * (canvas.height / rect.height);

      if (isEyedropperActive) {
        const ctx = canvas.getContext('2d', { willReadFrequently: true });
        if (ctx && mouseX >= 0 && mouseX < canvas.width && mouseY >= 0 && mouseY < canvas.height) {
          const pixel = ctx.getImageData(Math.floor(mouseX), Math.floor(mouseY), 1, 1).data;
          const hex = `#${((1 << 24) + (pixel[0] << 16) + (pixel[1] << 8) + pixel[2]).toString(16).slice(1).toUpperCase()}`;
          setEyedropperHoverColor({ hex, r: pixel[0], g: pixel[1], b: pixel[2], x: e.clientX, y: e.clientY });
        }
        canvas.style.cursor = 'crosshair';
        return;
      }

      if (draggingDividerRef.current) {
        if (draggingDividerRef.current.type === 'col') {
          const idx = draggingDividerRef.current.index;
          const newX = Math.max(colDividers[idx - 1] + 15, Math.min(colDividers[idx + 1] - 15, Math.round(mouseX)));
          setColDividers((prev) => {
            const next = [...prev];
            next[idx] = newX;
            return next;
          });
        } else {
          const idx = draggingDividerRef.current.index;
          const newY = Math.max(rowDividers[idx - 1] + 15, Math.min(rowDividers[idx + 1] - 15, Math.round(mouseY)));
          setRowDividers((prev) => {
            const next = [...prev];
            next[idx] = newY;
            return next;
          });
        }
        return;
      }

      for (let c = 1; c < colDividers.length - 1; c++) {
        if (Math.abs(colDividers[c] - mouseX) <= 8) {
          canvas.style.cursor = 'col-resize';
          return;
        }
      }
      for (let r = 1; r < rowDividers.length - 1; r++) {
        if (Math.abs(rowDividers[r] - mouseY) <= 8) {
          canvas.style.cursor = 'row-resize';
          return;
        }
      }
      canvas.style.cursor = 'pointer';
    },
    [imageCanvasRef, isEyedropperActive, setEyedropperHoverColor, draggingDividerRef, colDividers, rowDividers, setColDividers, setRowDividers]
  );

  const openCellPixelEditor = useCallback(
    (cell: GridCellDefinition) => {
      const key = `${cell.row}_${cell.col}`;
      const dataUrl = slicedResults.get(key);
      if (!dataUrl && loadedImageRef.current) {
        const pad = Math.max(0, paddingInset);
        const rawX0 = colDividers[cell.col];
        const rawY0 = rowDividers[cell.row];
        const w = Math.max(10, colDividers[cell.col + 1] - rawX0 - pad * 2);
        const h = Math.max(10, rowDividers[cell.row + 1] - rawY0 - pad * 2);
        const tempCanvas = document.createElement('canvas');
        tempCanvas.width = w;
        tempCanvas.height = h;
        const tempCtx = tempCanvas.getContext('2d');
        if (tempCtx) {
          tempCtx.drawImage(loadedImageRef.current, rawX0 + pad, rawY0 + pad, w, h, 0, 0, w, h);
          setEditingCellOriginalDataUrl(tempCanvas.toDataURL());
        }
      } else if (dataUrl) {
        setEditingCellOriginalDataUrl(dataUrl);
      }
      setEditingCellDef(cell);
      setIsEraserOpen(true);
    },
    [slicedResults, loadedImageRef, paddingInset, colDividers, rowDividers, setEditingCellOriginalDataUrl, setEditingCellDef, setIsEraserOpen]
  );

  const handleCommitAsNewBase = useCallback(() => {
    const img = loadedImageRef.current;
    if (!img || (!hasExplicitlySliced && slicedCanvasesRef.current.size === 0)) return;
    const fullCanvas = document.createElement('canvas');
    fullCanvas.width = img.width;
    fullCanvas.height = img.height;
    const fCtx = fullCanvas.getContext('2d');
    if (!fCtx) return;

    if (currentCategory.id === 'single_full_image') {
      const single = slicedCanvasesRef.current.get('0_0');
      if (single) fCtx.drawImage(single, paddingInset, paddingInset, img.width - paddingInset * 2, img.height - paddingInset * 2);
    } else {
      currentCategory.cells.forEach((cell) => {
        const cellCanvas = slicedCanvasesRef.current.get(`${cell.row}_${cell.col}`);
        if (cellCanvas && colDividers.length > cell.col + 1 && rowDividers.length > cell.row + 1) {
          const rawX0 = colDividers[cell.col];
          const rawY0 = rowDividers[cell.row];
          fCtx.drawImage(
            cellCanvas,
            rawX0 + paddingInset,
            rawY0 + paddingInset,
            colDividers[cell.col + 1] - rawX0 - paddingInset * 2,
            rowDividers[cell.row + 1] - rawY0 - paddingInset * 2
          );
        }
      });
    }

    const newBaseUrl = fullCanvas.toDataURL('image/png');
    const newImg = new Image();
    newImg.crossOrigin = 'anonymous';
    newImg.onload = () => {
      loadedImageRef.current = newImg;
      setLoadedImage(newImg);
      setUserUploadedImageUrl(newBaseUrl);
      setHasExplicitlySliced(false);
      slicedCanvasesRef.current.clear();
      setSlicedResults(new Map());
      setPreviewDisplayMode('original');
      redrawCanvas('original');
    };
    newImg.src = newBaseUrl;
  }, [
    loadedImageRef,
    hasExplicitlySliced,
    slicedCanvasesRef,
    currentCategory,
    paddingInset,
    colDividers,
    rowDividers,
    setLoadedImage,
    setUserUploadedImageUrl,
    setHasExplicitlySliced,
    setSlicedResults,
    setPreviewDisplayMode,
    redrawCanvas,
  ]);

  return {
    handleCanvasMouseDown,
    handleCanvasMouseMove,
    openCellPixelEditor,
    handleCommitAsNewBase,
  };
}
