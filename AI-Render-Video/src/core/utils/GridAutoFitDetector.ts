/**
 * GridAutoFitDetector
 * Content-Aware Grid Divider Detection & Auto-Fitting for AI-generated Sprite Sheets.
 * Scans image pixels, calculates foreground density projections, detects gutters and gaps,
 * and automatically snaps grid dividers to perfectly frame each sprite without clipping.
 */

export interface AutoFitGridResult {
  colDividers: number[];
  rowDividers: number[];
  contentBounds: {
    minX: number;
    maxX: number;
    minY: number;
    maxY: number;
  };
  detectedGaps: {
    colValleys: number[];
    rowValleys: number[];
  };
}

/**
 * Checks if a pixel matches the background / chroma key color
 */
function isBackgroundPixel(
  r: number,
  g: number,
  b: number,
  a: number,
  keyType: 'chroma_green' | 'pure_white' | 'custom',
  keyHex: string
): boolean {
  if (a < 20) return true; // transparent

  if (keyType === 'chroma_green') {
    // Pure or studio chroma green (#00FF00, #00E000, etc.)
    return g > 110 && g > r * 1.25 && g > b * 1.25;
  } else if (keyType === 'pure_white') {
    // Pure white or near-white background (#FFFFFF, #FAFAFA)
    return r > 240 && g > 240 && b > 240;
  } else {
    // Custom hex color
    const hex = keyHex.replace('#', '');
    const kr = parseInt(hex.substring(0, 2) || '00', 16);
    const kg = parseInt(hex.substring(2, 4) || 'ff', 16);
    const kb = parseInt(hex.substring(4, 6) || '00', 16);
    const dist = Math.sqrt((r - kr) ** 2 + (g - kg) ** 2 + (b - kb) ** 2);
    return dist < 45;
  }
}

/**
 * Detects content bounds and optimal grid dividers for an image
 */
export function detectAndFitGridDividers(
  imageSource: HTMLImageElement | HTMLCanvasElement,
  cols: number,
  rows: number,
  keyType: 'chroma_green' | 'pure_white' | 'custom' = 'chroma_green',
  keyHex: string = '#00ff00'
): AutoFitGridResult {
  const width = imageSource.width;
  const height = imageSource.height;

  // Single cell fallback
  if (cols <= 1 && rows <= 1) {
    return {
      colDividers: [0, width],
      rowDividers: [0, height],
      contentBounds: { minX: 0, maxX: width, minY: 0, maxY: height },
      detectedGaps: { colValleys: [], rowValleys: [] },
    };
  }

  // Draw to offscreen canvas to get pixel data
  const offscreen = document.createElement('canvas');
  offscreen.width = width;
  offscreen.height = height;
  const ctx = offscreen.getContext('2d', { willReadFrequently: true });

  if (!ctx) {
    // Fallback uniform dividers
    const uniformCols = Array.from({ length: cols + 1 }, (_, i) => Math.round((i * width) / cols));
    const uniformRows = Array.from({ length: rows + 1 }, (_, i) => Math.round((i * height) / rows));
    return {
      colDividers: uniformCols,
      rowDividers: uniformRows,
      contentBounds: { minX: 0, maxX: width, minY: 0, maxY: height },
      detectedGaps: { colValleys: [], rowValleys: [] },
    };
  }

  ctx.drawImage(imageSource, 0, 0);
  const imgData = ctx.getImageData(0, 0, width, height);
  const data = imgData.data;

  // 1. Calculate 1D Projection Density along X and Y axes
  const colDensity = new Float32Array(width);
  const rowDensity = new Float32Array(height);

  let globalMinX = width;
  let globalMaxX = 0;
  let globalMinY = height;
  let globalMaxY = 0;

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = (y * width + x) * 4;
      const r = data[idx];
      const g = data[idx + 1];
      const b = data[idx + 2];
      const a = data[idx + 3];

      if (!isBackgroundPixel(r, g, b, a, keyType, keyHex)) {
        colDensity[x]++;
        rowDensity[y]++;

        if (x < globalMinX) globalMinX = x;
        if (x > globalMaxX) globalMaxX = x;
        if (y < globalMinY) globalMinY = y;
        if (y > globalMaxY) globalMaxY = y;
      }
    }
  }

  // Handle completely blank / key image
  if (globalMinX > globalMaxX || globalMinY > globalMaxY) {
    const uniformCols = Array.from({ length: cols + 1 }, (_, i) => Math.round((i * width) / cols));
    const uniformRows = Array.from({ length: rows + 1 }, (_, i) => Math.round((i * height) / rows));
    return {
      colDividers: uniformCols,
      rowDividers: uniformRows,
      contentBounds: { minX: 0, maxX: width, minY: 0, maxY: height },
      detectedGaps: { colValleys: [], rowValleys: [] },
    };
  }

  // Add generous safety margin around global content (16px buffer)
  const safetyPadding = 16;
  const startX = Math.max(0, globalMinX - safetyPadding);
  const endX = Math.min(width, globalMaxX + safetyPadding);
  const startY = Math.max(0, globalMinY - safetyPadding);
  const endY = Math.min(height, globalMaxY + safetyPadding);

  const activeW = endX - startX;
  const activeH = endY - startY;

  // 2. Smooth density arrays using 1D Gaussian kernel for robust valley finding
  const smoothCol = smooth1D(colDensity, 11);
  const smoothRow = smooth1D(rowDensity, 11);

  // 3. Find optimal Column Dividers with safety padding
  const finalColDividers: number[] = [0];
  const colValleys: number[] = [];

  for (let c = 1; c < cols; c++) {
    const nominalX = startX + Math.round((c * activeW) / cols);
    const searchRadius = Math.round(activeW / (cols * 2.8));
    const minSearch = Math.max(0, nominalX - searchRadius);
    const maxSearch = Math.min(width - 1, nominalX + searchRadius);

    let bestX = nominalX;
    let minVal = Infinity;

    for (let x = minSearch; x <= maxSearch; x++) {
      if (smoothCol[x] < minVal) {
        minVal = smoothCol[x];
        bestX = x;
      }
    }

    // Find the center of the valley (gap between sprites)
    let valleyStart = bestX;
    let valleyEnd = bestX;
    while (valleyStart > minSearch && smoothCol[valleyStart - 1] <= minVal * 1.1 + 0.15) {
      valleyStart--;
    }
    while (valleyEnd < maxSearch && smoothCol[valleyEnd + 1] <= minVal * 1.1 + 0.15) {
      valleyEnd++;
    }

    // Place divider right in the center of the background gutter for maximum safety margin
    const optimalX = Math.round((valleyStart + valleyEnd) / 2);
    finalColDividers.push(optimalX);
    colValleys.push(optimalX);
  }
  finalColDividers.push(width);

  // 4. Find optimal Row Dividers with safety padding
  const finalRowDividers: number[] = [0];
  const rowValleys: number[] = [];

  for (let r = 1; r < rows; r++) {
    const nominalY = startY + Math.round((r * activeH) / rows);
    const searchRadius = Math.round(activeH / (rows * 2.8));
    const minSearch = Math.max(0, nominalY - searchRadius);
    const maxSearch = Math.min(height - 1, nominalY + searchRadius);

    let bestY = nominalY;
    let minVal = Infinity;

    for (let y = minSearch; y <= maxSearch; y++) {
      if (smoothRow[y] < minVal) {
        minVal = smoothRow[y];
        bestY = y;
      }
    }

    // Find center of horizontal background valley
    let valleyStart = bestY;
    let valleyEnd = bestY;
    while (valleyStart > minSearch && smoothRow[valleyStart - 1] <= minVal * 1.1 + 0.15) {
      valleyStart--;
    }
    while (valleyEnd < maxSearch && smoothRow[valleyEnd + 1] <= minVal * 1.1 + 0.15) {
      valleyEnd++;
    }

    const optimalY = Math.round((valleyStart + valleyEnd) / 2);
    finalRowDividers.push(optimalY);
    rowValleys.push(optimalY);
  }
  finalRowDividers.push(height);

  return {
    colDividers: finalColDividers,
    rowDividers: finalRowDividers,
    contentBounds: {
      minX: globalMinX,
      maxX: globalMaxX,
      minY: globalMinY,
      maxY: globalMaxY,
    },
    detectedGaps: {
      colValleys,
      rowValleys,
    },
  };
}

/**
 * 1D Box / Gaussian Smoothing helper
 */
function smooth1D(arr: Float32Array, radius: number): Float32Array {
  const result = new Float32Array(arr.length);
  const n = arr.length;
  for (let i = 0; i < n; i++) {
    let sum = 0;
    let count = 0;
    const start = Math.max(0, i - radius);
    const end = Math.min(n - 1, i + radius);
    for (let j = start; j <= end; j++) {
      sum += arr[j];
      count++;
    }
    result[i] = count > 0 ? sum / count : 0;
  }
  return result;
}
