import { useCallback } from 'react';
import { GridCategoryDefinition, GridCellDefinition } from '../../../../core/assets/GridSliceRegistry';
import { STANDARD_ANGLE_DEFINITIONS, CheckerboardTheme } from '../../../../core/assets/slicer/SlicerAngleConstants';
import { PaddedCropRect, detectImageBBoxRect } from '../../../../core/utils/PixelBoundingBoxAlgorithms';
import { ChromaProcessOptions } from '../../../../core/utils/ChromaDespeckleProcessor';

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
  enableSmartCrop?: boolean;
  smartCropPadding?: number;
  cellCropRectsRef?: React.MutableRefObject<Map<string, PaddedCropRect>>;
  colDividers: number[];
  rowDividers: number[];
  selectedCell: GridCellDefinition | null;
  isDirectBBoxCropActive?: boolean;
  directBBoxPadding?: number;
  currentBBoxRectRef?: React.MutableRefObject<PaddedCropRect | null>;
  keyColorType?: 'chroma_green' | 'pure_white' | 'custom';
  keyColorHex?: string;
  isolationMode?: 'all' | 'outer_only';
  tolerance?: number;
  feather?: number;
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
  enableSmartCrop = false,
  smartCropPadding = 2,
  cellCropRectsRef,
  colDividers,
  rowDividers,
  selectedCell,
  isDirectBBoxCropActive = false,
  directBBoxPadding = 0,
  currentBBoxRectRef,
  keyColorType,
  keyColorHex,
  isolationMode,
  tolerance,
  feather,
}: UseSlicerCanvasDrawingProps) {
  const redrawCanvas = useCallback(
    (modeOverride?: 'transparent' | 'original') => {
      const canvas = imageCanvasRef.current;
      const img = loadedImageRef.current || loadedImage;
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

      if (effectiveMode === 'transparent') {
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

        if (hasExplicitlySliced || slicedCanvases.size > 0) {
          const isSingleOnly = currentCategory.id === 'single_full_image' || (slicedCanvases.has('0_0') && slicedCanvases.size === 1);
          if (isSingleOnly) {
            const singleCanvas = slicedCanvases.get('0_0');
            if (singleCanvas) {
              const pad = Math.max(0, paddingInset);
              const cropRect = cellCropRectsRef?.current?.get('0_0');
              if (enableSmartCrop && cropRect) {
                const drawX = pad + cropRect.left;
                const drawY = pad + cropRect.top;
                ctx.drawImage(singleCanvas, drawX, drawY, cropRect.width, cropRect.height);

                // Draw Smart Crop boundary box
                ctx.strokeStyle = '#c084fc';
                ctx.lineWidth = 1.5;
                ctx.setLineDash([4, 4]);
                ctx.strokeRect(drawX, drawY, cropRect.width, cropRect.height);
                ctx.setLineDash([]);
              } else {
                ctx.drawImage(singleCanvas, pad, pad, Math.max(10, canvas.width - pad * 2), Math.max(10, canvas.height - pad * 2));
              }
            } else {
              ctx.drawImage(img, 0, 0);
            }
          } else {
            const numCols = Math.max(1, currentCategory.cols || 4);
            const numRows = Math.max(1, currentCategory.rows || 1);
            const baseW = colDividers.length > numCols ? colDividers[colDividers.length - 1] : canvas.width;
            const baseH = rowDividers.length > numRows ? rowDividers[rowDividers.length - 1] : canvas.height;

            const getColX = (c: number) => {
              if (colDividers.length > c && baseW > 0) {
                return Math.round((colDividers[c] / baseW) * canvas.width);
              }
              return Math.round((c * canvas.width) / numCols);
            };

            const getRowY = (r: number) => {
              if (rowDividers.length > r && baseH > 0) {
                return Math.round((rowDividers[r] / baseH) * canvas.height);
              }
              return Math.round((r * canvas.height) / numRows);
            };

            currentCategory.cells.forEach((cell) => {
              const key = `${cell.row}_${cell.col}`;
              const cellCanvas = slicedCanvases.get(key);
              if (cellCanvas && colDividers.length > cell.col + 1 && rowDividers.length > cell.row + 1) {
                const pad = Math.max(0, paddingInset);
                const rawX0 = getColX(cell.col);
                const rawX1 = getColX(cell.col + 1);
                const rawY0 = getRowY(cell.row);
                const rawY1 = getRowY(cell.row + 1);
                const rawW = rawX1 - rawX0;
                const rawH = rawY1 - rawY0;
                const safePadX = Math.min(pad, Math.floor(rawW / 3));
                const safePadY = Math.min(pad, Math.floor(rawH / 3));
                const cellW = Math.max(10, rawW - safePadX * 2);
                const cellH = Math.max(10, rawH - safePadY * 2);
                const cropRect = cellCropRectsRef?.current?.get(key);

                if (enableSmartCrop && cropRect) {
                  const drawX = rawX0 + safePadX + cropRect.left;
                  const drawY = rawY0 + safePadY + cropRect.top;
                  ctx.drawImage(cellCanvas, drawX, drawY, cropRect.width, cropRect.height);

                  // Draw Smart Crop dashed boundary box
                  ctx.strokeStyle = '#a855f7';
                  ctx.lineWidth = 1.5;
                  ctx.setLineDash([4, 3]);
                  ctx.strokeRect(drawX, drawY, cropRect.width, cropRect.height);
                  ctx.setLineDash([]);
                } else {
                  ctx.drawImage(cellCanvas, rawX0 + safePadX, rawY0 + safePadY, cellW, cellH);
                }
              }
            });
          }
        } else {
          ctx.drawImage(img, 0, 0);
        }
      } else {
        ctx.drawImage(img, 0, 0);
      }

      // Draw Grid Dividers & Badges (Only in original mode or before slicing to avoid drawing lines over transparent characters)
      if (currentCategory.id !== 'single_full_image') {
        if (effectiveMode === 'original' || !hasExplicitlySliced) {
          const numCols = Math.max(1, currentCategory.cols || 4);
          const numRows = Math.max(1, currentCategory.rows || 1);
          const baseW = colDividers.length > numCols ? colDividers[colDividers.length - 1] : canvas.width;
          const baseH = rowDividers.length > numRows ? rowDividers[rowDividers.length - 1] : canvas.height;

          const getColX = (c: number) => {
            if (colDividers.length > c && baseW > 0) {
              return Math.round((colDividers[c] / baseW) * canvas.width);
            }
            return Math.round((c * canvas.width) / numCols);
          };

          const getRowY = (r: number) => {
            if (rowDividers.length > r && baseH > 0) {
              return Math.round((rowDividers[r] / baseH) * canvas.height);
            }
            return Math.round((r * canvas.height) / numRows);
          };

          const baseLw = Math.max(3.5, Math.round(canvas.width / 240));
          const dashLen = Math.max(10, Math.round(canvas.width / 60));

          const drawDualDashLine = (x1: number, y1: number, x2: number, y2: number, isBorder = false) => {
            if (isBorder) {
              ctx.setLineDash([]);
              ctx.lineDashOffset = 0;
              ctx.lineWidth = baseLw + 3;
              ctx.strokeStyle = '#000000';
              ctx.beginPath();
              ctx.moveTo(x1, y1);
              ctx.lineTo(x2, y2);
              ctx.stroke();

              ctx.lineWidth = baseLw;
              ctx.strokeStyle = '#38bdf8';
              ctx.beginPath();
              ctx.moveTo(x1, y1);
              ctx.lineTo(x2, y2);
              ctx.stroke();
            } else {
              // Lớp nền đen viền ngoài tạo tương phản cao
              ctx.lineWidth = baseLw + 2.5;
              ctx.strokeStyle = '#000000';
              ctx.setLineDash([]);
              ctx.lineDashOffset = 0;
              ctx.beginPath();
              ctx.moveTo(x1, y1);
              ctx.lineTo(x2, y2);
              ctx.stroke();

              // Lớp 1: Nét đứt màu đen
              ctx.lineWidth = baseLw;
              ctx.strokeStyle = '#000000';
              ctx.setLineDash([dashLen, dashLen]);
              ctx.lineDashOffset = 0;
              ctx.beginPath();
              ctx.moveTo(x1, y1);
              ctx.lineTo(x2, y2);
              ctx.stroke();

              // Lớp 2: Nét đứt màu trắng xen kẽ
              ctx.lineWidth = baseLw;
              ctx.strokeStyle = '#ffffff';
              ctx.setLineDash([dashLen, dashLen]);
              ctx.lineDashOffset = dashLen;
              ctx.beginPath();
              ctx.moveTo(x1, y1);
              ctx.lineTo(x2, y2);
              ctx.stroke();

              // Reset dash và offset
              ctx.setLineDash([]);
              ctx.lineDashOffset = 0;
            }
          };

          for (let c = 0; c <= numCols; c++) {
            const x = getColX(c);
            drawDualDashLine(x, 0, x, canvas.height, c === 0 || c === numCols);
          }
          for (let r = 0; r <= numRows; r++) {
            const y = getRowY(r);
            drawDualDashLine(0, y, canvas.width, y, r === 0 || r === numRows);
          }
          ctx.setLineDash([]);
          ctx.lineDashOffset = 0;

          // Draw Padding Inset green guides inside each cell
          const pad = Math.max(0, paddingInset);
          if (pad > 0) {
            const padLw = Math.max(2.5, Math.round(canvas.width / 300));
            const padDash = Math.max(6, Math.round(dashLen / 2));
            ctx.strokeStyle = '#22c55e';
            ctx.lineWidth = padLw;
            ctx.setLineDash([padDash, padDash]);
            currentCategory.cells.forEach((cell) => {
              if (colDividers.length > cell.col + 1 && rowDividers.length > cell.row + 1) {
                const rawX0 = getColX(cell.col);
                const rawX1 = getColX(cell.col + 1);
                const rawY0 = getRowY(cell.row);
                const rawY1 = getRowY(cell.row + 1);
                const rawW = rawX1 - rawX0;
                const rawH = rawY1 - rawY0;
                const safePadX = Math.min(pad, Math.floor(rawW / 3));
                const safePadY = Math.min(pad, Math.floor(rawH / 3));
                ctx.strokeRect(rawX0 + safePadX, rawY0 + safePadY, Math.max(4, rawW - safePadX * 2), Math.max(4, rawH - safePadY * 2));
              }
            });
            ctx.setLineDash([]);
          }

          // Angle Badges
          currentCategory.cells.forEach((cell) => {
            if (colDividers.length > cell.col + 1 && rowDividers.length > cell.row + 1) {
              const x0 = getColX(cell.col);
              const x1 = getColX(cell.col + 1);
              const y0 = getRowY(cell.row);
              const y1 = getRowY(cell.row + 1);
              const w = x1 - x0;
              const h = y1 - y0;
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
                ctx.fillText(badgeText, x0 + 10, y0 + 17);
              }
            }
          });

          // Selected Cell Highlight
          if (selectedCell && colDividers.length > selectedCell.col + 1 && rowDividers.length > selectedCell.row + 1) {
            const x0 = getColX(selectedCell.col);
            const x1 = getColX(selectedCell.col + 1);
            const y0 = getRowY(selectedCell.row);
            const y1 = getRowY(selectedCell.row + 1);
            const w = x1 - x0;
            const h = y1 - y0;
            ctx.fillStyle = 'rgba(56, 189, 248, 0.22)';
            ctx.fillRect(x0, y0, w, h);
            ctx.strokeStyle = '#38bdf8';
            ctx.lineWidth = 2.0;
            ctx.strokeRect(x0, y0, w, h);
          }
        }
      }

      // Draw Direct Bounding Box Crop Overlay (when active)
      if (isDirectBBoxCropActive && canvas.width > 0 && canvas.height > 0) {
        const chromaOpts: ChromaProcessOptions | undefined = keyColorType
          ? {
              keyColorType,
              keyColorHex: keyColorHex || '#00ff00',
              isolationMode: isolationMode || 'all',
              tolerance: tolerance ?? 35,
              feather: feather ?? 2,
              shadowRetention: 0,
              strokeWidth: 0,
              strokeColorHex: '#000000',
              despeckleSize: 0,
              whiteSpeckleSensitivity: 0,
              keepLargestIslandOnly: false,
              fringeColorType: 'chroma_green',
              fringeColorHex: '#00ff00',
              defringeStrength: 0,
              edgeChoke: 0,
              edgeSmooth: 0,
              smoothColorType: 'black',
              smoothColorHex: '#000000',
              cleanupMode: 'all',
            }
          : undefined;

        const rect = detectImageBBoxRect(img, chromaOpts, directBBoxPadding, 20);
        if (currentBBoxRectRef) {
          currentBBoxRectRef.current = rect;
        }
        if (rect) {
          // Semi-transparent dark mask outside crop region
          ctx.fillStyle = 'rgba(0, 0, 0, 0.45)';
          // Top
          ctx.fillRect(0, 0, canvas.width, rect.top);
          // Bottom
          ctx.fillRect(0, rect.bottom + 1, canvas.width, canvas.height - (rect.bottom + 1));
          // Left
          ctx.fillRect(0, rect.top, rect.left, rect.height);
          // Right
          ctx.fillRect(rect.right + 1, rect.top, canvas.width - (rect.right + 1), rect.height);

          // Alternating black & white dashed border (Marching ants / Dual dash)
          const bboxLw = Math.max(3.5, Math.round(canvas.width / 240));
          const bboxDash = Math.max(10, Math.round(canvas.width / 60));

          // Layer 1: Black dash
          ctx.lineWidth = bboxLw;
          ctx.strokeStyle = '#000000';
          ctx.setLineDash([bboxDash, bboxDash]);
          ctx.lineDashOffset = 0;
          ctx.strokeRect(rect.left, rect.top, rect.width, rect.height);

          // Layer 2: White dash
          ctx.lineWidth = bboxLw;
          ctx.strokeStyle = '#ffffff';
          ctx.setLineDash([bboxDash, bboxDash]);
          ctx.lineDashOffset = bboxDash;
          ctx.strokeRect(rect.left, rect.top, rect.width, rect.height);

          ctx.setLineDash([]);
          ctx.lineDashOffset = 0;

          // Draw dimension badge at top-left of crop box
          const badgeText = `✂️ BBox: ${rect.width}×${rect.height}px (+${directBBoxPadding}px)`;
          ctx.font = 'bold 11px sans-serif';
          const badgeMetrics = ctx.measureText(badgeText);
          const bW = badgeMetrics.width + 12;
          const bH = 20;
          const bX = Math.max(4, Math.min(canvas.width - bW - 4, rect.left));
          const bY = Math.max(4, rect.top > bH + 4 ? rect.top - bH - 2 : rect.top + 4);

          ctx.fillStyle = 'rgba(15, 23, 42, 0.9)';
          ctx.fillRect(bX, bY, bW, bH);
          ctx.strokeStyle = '#c084fc';
          ctx.lineWidth = 1.5;
          ctx.strokeRect(bX, bY, bW, bH);
          ctx.fillStyle = '#f0abfc';
          ctx.fillText(badgeText, bX + 6, bY + 14);
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
      isDirectBBoxCropActive,
      directBBoxPadding,
    ]
  );

  return { redrawCanvas };
}
