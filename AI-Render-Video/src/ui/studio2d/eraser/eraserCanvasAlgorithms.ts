/**
 * Pixel Eraser Canvas Algorithms & Image Processing
 * Gaussian soft brush, Magic wand BFS, Smart despeckle & Green spill removal
 */

export interface PhotoshopStrokeOptions {
  hardness: number; // 0..100
  brushSize: number;
  flow: number; // 5..100
  opacity: number; // 5..100
}

/**
 * Renders the continuous vector stroke path with Gaussian blur feathered edge onto maskCanvas,
 * then composites onto main canvas bounded strictly by opacity (Photoshop Continuous Stroke Algorithm)
 */
export function renderPhotoshopStrokePath(
  mainCanvas: HTMLCanvasElement,
  initialImageData: ImageData,
  maskCanvas: HTMLCanvasElement,
  points: { x: number; y: number }[],
  options: PhotoshopStrokeOptions
): void {
  if (points.length === 0) return;

  maskCanvas.width = mainCanvas.width;
  maskCanvas.height = mainCanvas.height;
  const maskCtx = maskCanvas.getContext('2d');
  if (!maskCtx) return;

  maskCtx.clearRect(0, 0, maskCanvas.width, maskCanvas.height);

  const hardRatio = Math.max(0, Math.min(1, options.hardness / 100));
  const blurPx = (1 - hardRatio) * (options.brushSize * 0.35);
  const coreWidth = Math.max(1, options.brushSize - blurPx * 1.6);
  const flowAlpha = Math.max(0.05, Math.min(1, options.flow / 100));

  maskCtx.save();
  if (blurPx > 0.4) {
    maskCtx.filter = `blur(${blurPx.toFixed(1)}px)`;
  }
  maskCtx.fillStyle = `rgba(0, 0, 0, ${flowAlpha})`;
  maskCtx.strokeStyle = `rgba(0, 0, 0, ${flowAlpha})`;
  maskCtx.lineWidth = coreWidth;
  maskCtx.lineCap = 'round';
  maskCtx.lineJoin = 'round';

  if (points.length === 1) {
    maskCtx.beginPath();
    maskCtx.arc(points[0].x, points[0].y, coreWidth / 2, 0, Math.PI * 2);
    maskCtx.fill();
  } else {
    maskCtx.beginPath();
    maskCtx.moveTo(points[0].x, points[0].y);
    for (let i = 1; i < points.length; i++) {
      const prev = points[i - 1];
      const curr = points[i];
      const midX = (prev.x + curr.x) / 2;
      const midY = (prev.y + curr.y) / 2;
      maskCtx.quadraticCurveTo(prev.x, prev.y, midX, midY);
    }
    maskCtx.lineTo(points[points.length - 1].x, points[points.length - 1].y);
    maskCtx.stroke();

    maskCtx.beginPath();
    maskCtx.arc(points[0].x, points[0].y, coreWidth / 2, 0, Math.PI * 2);
    maskCtx.arc(points[points.length - 1].x, points[points.length - 1].y, coreWidth / 2, 0, Math.PI * 2);
    maskCtx.fill();
  }
  maskCtx.restore();

  const mainCtx = mainCanvas.getContext('2d', { willReadFrequently: true });
  if (!mainCtx) return;

  mainCtx.putImageData(initialImageData, 0, 0);
  mainCtx.save();
  mainCtx.globalAlpha = Math.max(0.01, Math.min(1, options.opacity / 100));
  mainCtx.globalCompositeOperation = 'destination-out';
  mainCtx.drawImage(maskCanvas, 0, 0);
  mainCtx.restore();
}

/**
 * Magic Wand Color Flood Fill Erase using Queue BFS
 */
export function executeMagicWandErase(
  canvas: HTMLCanvasElement,
  startX: number,
  startY: number,
  tolerance: number
): void {
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  if (!ctx) return;

  const w = canvas.width;
  const h = canvas.height;
  const imgData = ctx.getImageData(0, 0, w, h);
  const data = imgData.data;

  const targetIdx = (startY * w + startX) * 4;
  const targetR = data[targetIdx];
  const targetG = data[targetIdx + 1];
  const targetB = data[targetIdx + 2];
  const targetA = data[targetIdx + 3];

  if (targetA === 0) return;

  const visited = new Uint8Array(w * h);
  const queue = new Int32Array(w * h);
  let head = 0;
  let tail = 0;

  const startPos = startY * w + startX;
  visited[startPos] = 1;
  queue[tail++] = startPos;

  const tolSq = tolerance * tolerance * 3;

  while (head < tail) {
    const curr = queue[head++];
    const cx = curr % w;
    const cy = Math.floor(curr / w);
    const idx = curr * 4;

    const r = data[idx];
    const g = data[idx + 1];
    const b = data[idx + 2];
    const a = data[idx + 3];

    const distSq = (r - targetR) ** 2 + (g - targetG) ** 2 + (b - targetB) ** 2;

    if (distSq <= tolSq && a > 0) {
      data[idx + 3] = 0; // Erase pixel

      const neighbors = [
        cx > 0 ? curr - 1 : -1,
        cx < w - 1 ? curr + 1 : -1,
        cy > 0 ? curr - w : -1,
        cy < h - 1 ? curr + w : -1,
      ];

      for (let n = 0; n < 4; n++) {
        const nIdx = neighbors[n];
        if (nIdx >= 0 && !visited[nIdx]) {
          visited[nIdx] = 1;
          queue[tail++] = nIdx;
        }
      }
    }
  }

  ctx.putImageData(imgData, 0, 0);
}

/**
 * Smart Auto-Despeckle Island Removal Algorithm
 */
export function executeSmartDespeckle(canvas: HTMLCanvasElement): void {
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  if (!ctx) return;

  const w = canvas.width;
  const h = canvas.height;
  const imgData = ctx.getImageData(0, 0, w, h);
  const data = imgData.data;

  const visited = new Uint8Array(w * h);
  const allComponents: { pixels: number[]; isWhite: boolean }[] = [];
  let maxSize = 0;

  for (let i = 0; i < w * h; i++) {
    if (visited[i] || data[i * 4 + 3] <= 10) continue;

    const compQueue = new Int32Array(w * h);
    let qHead = 0;
    let qTail = 0;
    const currentPixels: number[] = [];
    let whiteCount = 0;

    visited[i] = 1;
    compQueue[qTail++] = i;

    while (qHead < qTail) {
      const curr = compQueue[qHead++];
      currentPixels.push(curr);

      const r = data[curr * 4];
      const g = data[curr * 4 + 1];
      const b = data[curr * 4 + 2];
      if (r > 240 && g > 240 && b > 240) whiteCount++;

      const cx = curr % w;
      const cy = Math.floor(curr / w);

      const neighbors = [
        cx > 0 ? curr - 1 : -1,
        cx < w - 1 ? curr + 1 : -1,
        cy > 0 ? curr - w : -1,
        cy < h - 1 ? curr + w : -1,
        cx > 0 && cy > 0 ? curr - w - 1 : -1,
        cx < w - 1 && cy > 0 ? curr - w + 1 : -1,
        cx > 0 && cy < h - 1 ? curr + w - 1 : -1,
        cx < w - 1 && cy < h - 1 ? curr + w + 1 : -1,
      ];

      for (let n = 0; n < 8; n++) {
        const nIdx = neighbors[n];
        if (nIdx >= 0 && !visited[nIdx] && data[nIdx * 4 + 3] > 10) {
          visited[nIdx] = 1;
          compQueue[qTail++] = nIdx;
        }
      }
    }

    if (currentPixels.length > maxSize) maxSize = currentPixels.length;
    allComponents.push({
      pixels: currentPixels,
      isWhite: whiteCount / currentPixels.length > 0.5,
    });
  }

  for (const comp of allComponents) {
    if (comp.pixels.length < Math.min(60, maxSize * 0.15) || (comp.isWhite && comp.pixels.length < 120)) {
      for (let p = 0; p < comp.pixels.length; p++) {
        data[comp.pixels[p] * 4 + 3] = 0;
      }
    }
  }

  ctx.putImageData(imgData, 0, 0);
}

/**
 * Green Spill & Green Tint Removal Algorithm
 */
export function executeDespillGreen(canvas: HTMLCanvasElement): void {
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  if (!ctx) return;

  const w = canvas.width;
  const h = canvas.height;
  const imgData = ctx.getImageData(0, 0, w, h);
  const data = imgData.data;

  for (let i = 0; i < data.length; i += 4) {
    if (data[i + 3] === 0) continue;
    const r = data[i];
    const g = data[i + 1];
    const b = data[i + 2];

    const maxRB = Math.max(r, b);
    if (g > maxRB) {
      data[i + 1] = maxRB;
    }
  }

  ctx.putImageData(imgData, 0, 0);
}
