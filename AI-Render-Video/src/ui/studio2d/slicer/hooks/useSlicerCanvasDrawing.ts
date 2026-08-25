import { useCallback } from 'react';
import { GridCategoryDefinition, GridCellDefinition } from '../../../../core/assets/GridSliceRegistry';
import { STANDARD_ANGLE_DEFINITIONS, CheckerboardTheme } from '../../../../core/assets/slicer/SlicerAngleConstants';

export interface UseSlicerCanvasDrawingProps {
  imageCanvasRef: React.RefObject<HTMLCanvasElement>;
  loadedImage: HTMLImageElement | null;
  loadedImageRef: React.MutableRefObject<HTMLImageElement | null>;
  previewDisplayMode: 'transparent' | 'original';
  hasExplicitlySliced: boolean;
  slicedCanvasesRef: React.MutableRefObject<Map<string, HTMLCanvasElement>>;
  checkerTheme: CheckerboardTheme;
  currentCategory: GridCategoryDefinition;
  paddingInset: number;
  colDividers: number[];
  rowDividers: number[];
  selectedCell: GridCellDefinition | null;
}

export function useSlicerCanvasDrawing({
  imageCanvasRef,
  loadedImage,
  loadedImageRef,
  previewDisplayMode,
  hasExplicitlySliced,
  slicedCanvasesRef,
  checkerTheme,
  currentCategory,
  paddingInset,
  colDividers,
  rowDividers,
  selectedCell,
}: UseSlicerCanvasDrawingProps) {
  const redrawCanvas = useCallback(
    (modeOverride?: 'transparent' | 'original') => {
      const canvas = imageCanvasRef.current;
      const img = loadedImage || loadedImageRef.current;
      if (!canvas || !img) return;
      const ctx = canvas.getContext('2d');
      if (!ctx) return;

      if (canvas.width !== img.width || canvas.height !== img.height) {
        canvas.width = img.width;
        canvas.height = img.height;
      }
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      const effectiveMode = modeOverride ?? previewDisplayMode;
      const slicedCanvases = slicedCanvasesRef.current || new Map<string, HTMLCanvasElement>();

      if (effectiveMode === 'transparent' && (hasExplicitlySliced || slicedCanvases.size > 0)) {
        const size = 16;
        const isLight = checkerTheme === 'light';
        const colorA = isLight ? '#ffffff' : '#182030';
        const colorB = isLight ? '#e2e8f0' : '#0c1220';
        for (let x = 0; x < canvas.width; x += size) {
          for (let y = 0; y < canvas.height; y += size) {
            ctx.fillStyle = (Math.floor(x / size) + Math.floor(y / size)) % 2 === 0 ? colorA : colorB;
            ctx.fillRect(x, y, size, size);
          }
        }

        if (currentCategory.id === 'single_full_image') {
          const singleCanvas = slicedCanvases.get('0_0');
          if (singleCanvas) {
            const pad = Math.max(0, paddingInset);
            ctx.drawImage(singleCanvas, pad, pad, Math.max(10, canvas.width - pad * 2), Math.max(10, canvas.height - pad * 2));
          }
        } else {
          currentCategory.cells.forEach((cell) => {
            const cellCanvas = slicedCanvases.get(`${cell.row}_${cell.col}`);
            if (cellCanvas && colDividers.length > cell.col + 1 && rowDividers.length > cell.row + 1) {
              const pad = Math.max(0, paddingInset);
              const rawX0 = colDividers[cell.col];
              const rawY0 = rowDividers[cell.row];
              ctx.drawImage(
                cellCanvas,
                rawX0 + pad,
                rawY0 + pad,
                Math.max(10, colDividers[cell.col + 1] - rawX0 - pad * 2),
                Math.max(10, rowDividers[cell.row + 1] - rawY0 - pad * 2)
              );
            }
          });
        }
      } else {
        ctx.drawImage(img, 0, 0);
      }

      // Draw Grid Dividers & Badges
      if (currentCategory.id !== 'single_full_image') {
        const drawDualDashLine = (x1: number, y1: number, x2: number, y2: number, isBorder = false) => {
          if (isBorder) {
            ctx.setLineDash([]);
            ctx.lineDashOffset = 0;
            ctx.lineWidth = 3.0;
            ctx.strokeStyle = '#000000';
            ctx.beginPath();
            ctx.moveTo(x1, y1);
            ctx.lineTo(x2, y2);
            ctx.stroke();

            ctx.lineWidth = 1.5;
            ctx.strokeStyle = '#38bdf8';
            ctx.beginPath();
            ctx.moveTo(x1, y1);
            ctx.lineTo(x2, y2);
            ctx.stroke();
          } else {
            // Lớp 1: Nét đứt màu đen [8, 8] offset 0
            ctx.lineWidth = 2.5;
            ctx.strokeStyle = '#000000';
            ctx.setLineDash([8, 8]);
            ctx.lineDashOffset = 0;
            ctx.beginPath();
            ctx.moveTo(x1, y1);
            ctx.lineTo(x2, y2);
            ctx.stroke();

            // Lớp 2: Nét đứt màu trắng [8, 8] offset 8 xen kẽ chính xác vào khoảng trống
            ctx.lineWidth = 2.5;
            ctx.strokeStyle = '#ffffff';
            ctx.setLineDash([8, 8]);
            ctx.lineDashOffset = 8;
            ctx.beginPath();
            ctx.moveTo(x1, y1);
            ctx.lineTo(x2, y2);
            ctx.stroke();

            // Reset dash và offset
            ctx.setLineDash([]);
            ctx.lineDashOffset = 0;
          }
        };

        colDividers.forEach((x, c) => drawDualDashLine(x, 0, x, canvas.height, c === 0 || c === colDividers.length - 1));
        rowDividers.forEach((y, r) => drawDualDashLine(0, y, canvas.width, y, r === 0 || r === rowDividers.length - 1));
        ctx.setLineDash([]);
        ctx.lineDashOffset = 0;

        // Angle Badges
        currentCategory.cells.forEach((cell) => {
          if (colDividers.length > cell.col + 1 && rowDividers.length > cell.row + 1) {
            const x0 = colDividers[cell.col];
            const y0 = rowDividers[cell.row];
            const w = colDividers[cell.col + 1] - x0;
            const h = rowDividers[cell.row + 1] - y0;
            if (w > 45 && h > 28) {
              const angleDef = STANDARD_ANGLE_DEFINITIONS.find((a) => a.angle === cell.angle) || STANDARD_ANGLE_DEFINITIONS[0];
              const badgeText = angleDef.shortLabel || '0° Front';
              ctx.font = 'bold 10.5px sans-serif';
              const textMetrics = ctx.measureText(badgeText);
              const badgeW = Math.max(50, textMetrics.width + 12);
              ctx.fillStyle = 'rgba(11, 15, 25, 0.85)';
              ctx.fillRect(x0 + 5, y0 + 5, badgeW, 18);
              ctx.strokeStyle = '#38bdf8';
              ctx.lineWidth = 1;
              ctx.strokeRect(x0 + 5, y0 + 5, badgeW, 18);
              ctx.fillStyle = '#38bdf8';
              ctx.fillText(badgeText, x0 + 10, y0 + 18);
            }
          }
        });

        // Selected Cell Highlight
        if (selectedCell && colDividers.length > selectedCell.col + 1 && rowDividers.length > selectedCell.row + 1) {
          const x0 = colDividers[selectedCell.col];
          const y0 = rowDividers[selectedCell.row];
          const w = colDividers[selectedCell.col + 1] - x0;
          const h = rowDividers[selectedCell.row + 1] - y0;
          ctx.fillStyle = 'rgba(56, 189, 248, 0.22)';
          ctx.fillRect(x0, y0, w, h);
          ctx.strokeStyle = '#38bdf8';
          ctx.lineWidth = 2.0;
          ctx.strokeRect(x0, y0, w, h);
        }
      }
    },
    [
      imageCanvasRef,
      loadedImage,
      loadedImageRef,
      previewDisplayMode,
      hasExplicitlySliced,
      slicedCanvasesRef,
      checkerTheme,
      currentCategory,
      paddingInset,
      colDividers,
      rowDividers,
      selectedCell,
    ]
  );

  return { redrawCanvas };
}
