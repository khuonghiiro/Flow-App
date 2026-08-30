// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { GridCategoryDefinition } from '../../../../core/assets/GridSliceRegistry';
import { ChromaProcessOptions, processCellChromaAndDespeckle } from '../../../../core/utils/ChromaDespeckleProcessor';
import { SlicerUploadedImageItem } from '../hooks/useSlicerMultiImageGallery';
import { cleanOuterEdgeDarkBorders } from './slicerPixelEraserHelper';

/**
 * Calculates normalized cell bounds [0..1] for any grid category
 */
function getNormalizedCellBounds(
  cell: { row: number; col: number },
  category: GridCategoryDefinition,
  colDividers: number[],
  rowDividers: number[],
  refWidth: number,
  refHeight: number
): { x0Norm: number; x1Norm: number; y0Norm: number; y1Norm: number } {
  const numCols = Math.max(1, category.cols || 4);
  const numRows = Math.max(1, category.rows || 1);

  let x0Norm = cell.col / numCols;
  let x1Norm = (cell.col + 1) / numCols;
  let y0Norm = cell.row / numRows;
  let y1Norm = (cell.row + 1) / numRows;

  // If custom dividers exist on reference image
  if (refWidth > 0 && colDividers.length > cell.col + 1) {
    x0Norm = Math.max(0, Math.min(1, colDividers[cell.col] / refWidth));
    x1Norm = Math.max(0, Math.min(1, colDividers[cell.col + 1] / refWidth));
  }
  if (refHeight > 0 && rowDividers.length > cell.row + 1) {
    y0Norm = Math.max(0, Math.min(1, rowDividers[cell.row] / refHeight));
    y1Norm = Math.max(0, Math.min(1, rowDividers[cell.row + 1] / refHeight));
  }

  return { x0Norm, x1Norm, y0Norm, y1Norm };
}

/**
 * Slices an individual image into ordered cell frames (left to right, top to bottom)
 * with paddingInset border trimming and chroma transparency processing.
 */
export async function sliceSingleImageWithGrid(
  imageItem: SlicerUploadedImageItem,
  category: GridCategoryDefinition,
  colDividers: number[],
  rowDividers: number[],
  paddingInset: number,
  chromaOptions: ChromaProcessOptions
): Promise<string[]> {
  const url = imageItem.originalUrl || imageItem.url || imageItem.transparentUrl;
  if (!url) return [];

  const img = new Image();
  img.crossOrigin = 'anonymous';
  await new Promise<void>((resolve, reject) => {
    img.onload = () => resolve();
    img.onerror = () => reject();
    img.src = url;
  });

  const w = img.naturalWidth || img.width;
  const h = img.naturalHeight || img.height;
  const pad = Math.max(0, paddingInset);

  // Single full image category
  if (category.id === 'single_full_image' || !category.cells || category.cells.length === 0) {
    const safePadX = Math.min(pad, Math.floor(w / 4));
    const safePadY = Math.min(pad, Math.floor(h / 4));
    const cw = Math.max(10, w - safePadX * 2);
    const ch = Math.max(10, h - safePadY * 2);

    const canvas = document.createElement('canvas');
    canvas.width = cw;
    canvas.height = ch;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return [];

    ctx.drawImage(img, safePadX, safePadY, cw, ch, 0, 0, cw, ch);
    processCellChromaAndDespeckle(ctx, cw, ch, chromaOptions);
    return [canvas.toDataURL('image/png')];
  }

  // Sort cells in natural order: row ascending, then col ascending (Left -> Right, Top -> Bottom)
  const sortedCells = [...category.cells].sort((a, b) => {
    if (a.row !== b.row) return a.row - b.row;
    return a.col - b.col;
  });

  const numCols = Math.max(1, category.cols || 4);
  const numRows = Math.max(1, category.rows || 1);

  const safeCols =
    colDividers && colDividers.length === numCols + 1 && !colDividers.some(isNaN)
      ? colDividers
      : Array.from({ length: numCols + 1 }, (_, i) => Math.round((i * w) / numCols));

  const safeRows =
    rowDividers && rowDividers.length === numRows + 1 && !rowDividers.some(isNaN)
      ? rowDividers
      : Array.from({ length: numRows + 1 }, (_, i) => Math.round((i * h) / numRows));

  const frames: string[] = [];
  const refW = safeCols[safeCols.length - 1] || w;
  const refH = safeRows[safeRows.length - 1] || h;

  for (const cell of sortedCells) {
    const bounds = getNormalizedCellBounds(cell, category, safeCols, safeRows, refW, refH);

    const rawX0 = Math.round(bounds.x0Norm * w);
    const rawX1 = Math.round(bounds.x1Norm * w);
    const rawY0 = Math.round(bounds.y0Norm * h);
    const rawY1 = Math.round(bounds.y1Norm * h);

    const rawW = rawX1 - rawX0;
    const rawH = rawY1 - rawY0;

    // Ensure padding does not exceed half of cell dimensions
    const safePadX = Math.min(pad, Math.floor(rawW / 3));
    const safePadY = Math.min(pad, Math.floor(rawH / 3));

    const x0 = rawX0 + safePadX;
    const y0 = rawY0 + safePadY;
    const cw = Math.max(10, rawW - safePadX * 2);
    const ch = Math.max(10, rawH - safePadY * 2);

    const canvas = document.createElement('canvas');
    canvas.width = cw;
    canvas.height = ch;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (ctx) {
      ctx.drawImage(img, x0, y0, cw, ch, 0, 0, cw, ch);
      processCellChromaAndDespeckle(ctx, cw, ch, chromaOptions);
      cleanOuterEdgeDarkBorders(ctx, cw, ch, Math.max(3, Math.min(6, safePadX)));
      frames.push(canvas.toDataURL('image/png'));
    }
  }

  return frames;
}

/**
 * Slices multiple checked images in chronological order and flattens all frames
 * Example: Image 1 (4 columns) -> [f1, f2, f3, f4] + Image 2 (4 columns) -> [f5, f6, f7, f8]
 */
export async function sliceMultipleImagesSequentially(
  imageList: SlicerUploadedImageItem[],
  checkedImageIds: Set<string>,
  category: GridCategoryDefinition,
  colDividers: number[],
  rowDividers: number[],
  paddingInset: number,
  chromaOptions: ChromaProcessOptions
): Promise<string[]> {
  const targetItems = imageList.filter((item) => checkedImageIds.has(item.id));
  if (targetItems.length === 0) return [];

  const allFrames: string[] = [];

  for (const item of targetItems) {
    try {
      const itemCols = item.customColDividers || colDividers;
      const itemRows = item.customRowDividers || rowDividers;
      const itemFrames = await sliceSingleImageWithGrid(
        item,
        category,
        itemCols,
        itemRows,
        paddingInset,
        chromaOptions
      );
      allFrames.push(...itemFrames);
    } catch (err) {
      console.warn('Failed to slice image item:', item.id, err);
    }
  }

  return allFrames;
}
