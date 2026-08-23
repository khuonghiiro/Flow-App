/**
 * ChromaDespeckleProcessor.ts
 * High-performance 2D Chroma Keying with:
 * 1. Mode "Tách Toàn Bộ" (all) vs "Chỉ Tách Viền Ngoài" (outer_only boundary flood-fill)
 * 2. Edge Fringe Erosion & Anti-Aliased Softening (Feather 0 to 20px)
 * 3. White Speckle & Noise Filtering
 * 4. Despeckle Connected Component Island Cleanup
 */

export interface ChromaProcessOptions {
  keyColorType: 'chroma_green' | 'pure_white' | 'custom';
  keyColorHex: string;
  isolationMode: 'all' | 'outer_only';
  tolerance: number;                // 5 to 100
  feather: number;                  // 0 to 20 px
  strokeWidth?: number;             // 0 to 15 px (Thêm độ dày viền bao quanh theo màu)
  strokeColorHex?: string;          // Mã màu viền bổ sung (mặc định '#000000' hoặc màu tùy chọn)
  edgeSmoothing?: number;
  despeckleSize: number;            // 0 to 150 px
  whiteSpeckleSensitivity: number;  // 0 to 100%
  keepLargestIslandOnly: boolean;
}

export function processCellChromaAndDespeckle(
  ctx: CanvasRenderingContext2D,
  width: number,
  height: number,
  options: ChromaProcessOptions
): void {
  const imgData = ctx.getImageData(0, 0, width, height);
  const data = imgData.data;
  const totalPixels = width * height;

  // 1. Resolve Target Key Color RGB
  let targetR = 0, targetG = 255, targetB = 0;
  if (options.keyColorType === 'pure_white') {
    targetR = 255; targetG = 255; targetB = 255;
  } else if (options.keyColorType === 'custom') {
    const hex = options.keyColorHex.replace('#', '');
    targetR = parseInt(hex.slice(0, 2), 16) || 0;
    targetG = parseInt(hex.slice(2, 4), 16) || 255;
    targetB = parseInt(hex.slice(4, 6), 16) || 0;
  }

  const maxMetric = 441.67;
  const threshold = (options.tolerance / 100) * maxMetric;
  const isGreenKey = options.keyColorType === 'chroma_green' || (targetG > targetR + 40 && targetG > targetB + 40);

  // 2. Classify raw key matches per pixel
  const isKeyPixel = new Uint8Array(totalPixels);

  for (let i = 0; i < totalPixels; i++) {
    const p = i * 4;
    const a = data[p + 3];
    if (a < 15) {
      isKeyPixel[i] = 1;
      continue;
    }
    const r = data[p];
    const g = data[p + 1];
    const b = data[p + 2];

    let metric = 0;
    if (isGreenKey) {
      const dist = Math.sqrt(r * r + (g - 255) * (g - 255) + b * b);
      const greenDom = Math.max(0, g - Math.max(r, b));
      metric = Math.max(0, dist - greenDom * 0.95);
    } else if (options.keyColorType === 'pure_white') {
      const dist = Math.sqrt((r - 255) * (r - 255) + (g - 255) * (g - 255) + (b - 255) * (b - 255));
      const brightness = Math.min(r, Math.min(g, b));
      metric = Math.max(0, dist - (brightness > 190 ? (brightness - 190) * 0.7 : 0));
    } else {
      metric = Math.sqrt(
        Math.pow(r - targetR, 2) +
        Math.pow(g - targetG, 2) +
        Math.pow(b - targetB, 2)
      );
    }

    if (metric <= threshold) {
      isKeyPixel[i] = 1;
    }
  }

  // 3. Mode Selection: "all" vs "outer_only" (Contiguous Boundary Flood-Fill)
  const isTransparent = new Uint8Array(totalPixels);

  if (options.isolationMode === 'outer_only') {
    // BFS Flood Fill from the 4 outer borders and pre-existing transparent areas
    const queue: number[] = [];

    // Pre-seed with any existing transparent pixels (for cumulative / multi-pass keying)
    for (let i = 0; i < totalPixels; i++) {
      if (data[i * 4 + 3] < 15) {
        isTransparent[i] = 1;
        queue.push(i);
      }
    }

    // Top & Bottom edges
    for (let x = 0; x < width; x++) {
      const topIdx = x;
      const bottomIdx = (height - 1) * width + x;
      if (isKeyPixel[topIdx] && !isTransparent[topIdx]) {
        isTransparent[topIdx] = 1;
        queue.push(topIdx);
      }
      if (isKeyPixel[bottomIdx] && !isTransparent[bottomIdx]) {
        isTransparent[bottomIdx] = 1;
        queue.push(bottomIdx);
      }
    }

    // Left & Right edges
    for (let y = 0; y < height; y++) {
      const leftIdx = y * width;
      const rightIdx = y * width + (width - 1);
      if (isKeyPixel[leftIdx] && !isTransparent[leftIdx]) {
        isTransparent[leftIdx] = 1;
        queue.push(leftIdx);
      }
      if (isKeyPixel[rightIdx] && !isTransparent[rightIdx]) {
        isTransparent[rightIdx] = 1;
        queue.push(rightIdx);
      }
    }

    let head = 0;
    while (head < queue.length) {
      const cur = queue[head++];
      const cx = cur % width;
      const cy = Math.floor(cur / width);

      const neighbors = [
        cy > 0 ? (cy - 1) * width + cx : -1,
        cy < height - 1 ? (cy + 1) * width + cx : -1,
        cx > 0 ? cy * width + (cx - 1) : -1,
        cx < width - 1 ? cy * width + (cx + 1) : -1,
      ];

      for (const n of neighbors) {
        if (n !== -1 && !isTransparent[n] && isKeyPixel[n]) {
          isTransparent[n] = 1;
          queue.push(n);
        }
      }
    }
  } else {
    // Mode "all": remove all key pixels everywhere (inside and outside)
    for (let i = 0; i < totalPixels; i++) {
      if (isKeyPixel[i]) {
        isTransparent[i] = 1;
      }
    }
  }

  // Apply initial transparency
  for (let i = 0; i < totalPixels; i++) {
    if (isTransparent[i]) {
      data[i * 4 + 3] = 0;
    }
  }

  // 4. Edge Fringe Erosion & Anti-Aliased Softening (Feather 1 to 20px)
  // Erode residual boundary pixels and apply soft alpha gradient on the border
  if (options.feather > 0) {
    const featherRadius = Math.min(20, Math.max(1, Math.round(options.feather)));
    const erodeDistanceCutoff = Math.floor(featherRadius * 0.25); // Cut off nearest fringe pixels
    const edgeDist = new Uint8Array(totalPixels);
    edgeDist.fill(255);

    // BFS distance queue from transparent boundary
    const distQueue: number[] = [];
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        const pIdx = y * width + x;
        if (isTransparent[pIdx]) {
          edgeDist[pIdx] = 0;
          distQueue.push(pIdx);
        }
      }
    }

    let dHead = 0;
    while (dHead < distQueue.length) {
      const cur = distQueue[dHead++];
      const curDist = edgeDist[cur];
      if (curDist >= featherRadius) continue;

      const cx = cur % width;
      const cy = Math.floor(cur / width);

      const neighbors = [
        cy > 0 ? (cy - 1) * width + cx : -1,
        cy < height - 1 ? (cy + 1) * width + cx : -1,
        cx > 0 ? cy * width + (cx - 1) : -1,
        cx < width - 1 ? cy * width + (cx + 1) : -1,
      ];

      for (const n of neighbors) {
        if (n !== -1 && edgeDist[n] > curDist + 1) {
          edgeDist[n] = curDist + 1;
          distQueue.push(n);
        }
      }
    }

    // Apply edge erosion & smooth alpha ramp
    for (let i = 0; i < totalPixels; i++) {
      const d = edgeDist[i];
      if (d > 0 && d <= featherRadius) {
        const p = i * 4;
        if (d <= erodeDistanceCutoff) {
          // Erode extreme fringe pixels completely
          data[p + 3] = 0;
        } else {
          // Smooth alpha ramp from boundary to inner core
          const span = featherRadius - erodeDistanceCutoff;
          const t = Math.max(0, Math.min(1, (d - erodeDistanceCutoff) / span));
          const smoothAlpha = t * t * (3 - 2 * t);
          data[p + 3] = Math.round(data[p + 3] * smoothAlpha);

          // Green despill on remaining soft edge pixels
          if (isGreenKey) {
            const r = data[p], g = data[p + 1], b = data[p + 2];
            const maxRB = Math.max(r, b);
            if (g > maxRB) {
              data[p + 1] = Math.round(maxRB);
            }
          }
        }
      }
    }
  }


  
  
  // 5. White Speckle / Noise Removal
  if (options.whiteSpeckleSensitivity > 0) {
    const whiteThreshold = 255 - (options.whiteSpeckleSensitivity / 100) * 75;
    for (let y = 1; y < height - 1; y++) {
      for (let x = 1; x < width - 1; x++) {
        const idx = (y * width + x) * 4;
        if (data[idx + 3] > 0) {
          const r = data[idx], g = data[idx + 1], b = data[idx + 2];
          if (r >= whiteThreshold && g >= whiteThreshold && b >= whiteThreshold) {
            const topA = data[((y - 1) * width + x) * 4 + 3];
            const bottomA = data[((y + 1) * width + x) * 4 + 3];
            const leftA = data[(y * width + (x - 1)) * 4 + 3];
            const rightA = data[(y * width + (x + 1)) * 4 + 3];

            const transparentCount = (topA === 0 ? 1 : 0) + (bottomA === 0 ? 1 : 0) + (leftA === 0 ? 1 : 0) + (rightA === 0 ? 1 : 0);
            if (transparentCount >= 2) {
              data[idx + 3] = 0;
            }
          }
        }
      }
    }
  }

  // 6. Despeckle / Connected Component Area Filtering
  if (options.despeckleSize > 0 || options.keepLargestIslandOnly) {
    const visited = new Uint8Array(totalPixels);
    const minPixelCount = options.despeckleSize * 4;
    const islands: { size: number; pixels: number[] }[] = [];

    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        const pIdx = y * width + x;
        if (visited[pIdx] || data[pIdx * 4 + 3] < 10) continue;

        const queue: number[] = [pIdx];
        visited[pIdx] = 1;
        const currentIsland: number[] = [];

        while (queue.length > 0) {
          const cur = queue.pop()!;
          currentIsland.push(cur);

          const cx = cur % width;
          const cy = Math.floor(cur / width);

          const neighbors = [
            cy > 0 ? (cy - 1) * width + cx : -1,
            cy < height - 1 ? (cy + 1) * width + cx : -1,
            cx > 0 ? cy * width + (cx - 1) : -1,
            cx < width - 1 ? cy * width + (cx + 1) : -1,
          ];

          for (const n of neighbors) {
            if (n !== -1 && !visited[n] && data[n * 4 + 3] >= 10) {
              visited[n] = 1;
              queue.push(n);
            }
          }
        }

        islands.push({ size: currentIsland.length, pixels: currentIsland });
      }
    }

    if (options.keepLargestIslandOnly && islands.length > 0) {
      let largest = islands[0];
      for (const isl of islands) {
        if (isl.size > largest.size) largest = isl;
      }

      for (const isl of islands) {
        if (isl !== largest) {
          for (const p of isl.pixels) {
            data[p * 4 + 3] = 0;
          }
        }
      }
    } else if (options.despeckleSize > 0) {
      for (const isl of islands) {
        if (isl.size < minPixelCount) {
          for (const p of isl.pixels) {
            data[p * 4 + 3] = 0;
          }
        }
      }
    }
  }

  
  // 7. Final Step: Outer Stroke & Color Contour Outline
  const strokeW = Math.min(25, Math.max(0, options.strokeWidth || 0));
  if (strokeW > 0) {
    const sHex = (options.strokeColorHex || '#000000').replace('#', '');
    const sR = parseInt(sHex.slice(0, 2), 16) || 0;
    const sG = parseInt(sHex.slice(2, 4), 16) || 0;
    const sB = parseInt(sHex.slice(4, 6), 16) || 0;

    const strokeDist = new Uint8Array(totalPixels);
    strokeDist.fill(255);
    const sQueue: number[] = [];

    // 1. Find all solid subject pixels (alpha > 30) bordering transparent space (alpha <= 30)
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        const idx = y * width + x;
        if (data[idx * 4 + 3] <= 30) continue;

        const hasEmptyNeighbor =
          (y > 0 && data[((y - 1) * width + x) * 4 + 3] <= 30) ||
          (y < height - 1 && data[((y + 1) * width + x) * 4 + 3] <= 30) ||
          (x > 0 && data[(y * width + (x - 1)) * 4 + 3] <= 30) ||
          (x < width - 1 && data[(y * width + (x + 1)) * 4 + 3] <= 30);

        if (hasEmptyNeighbor) {
          strokeDist[idx] = 0;
          sQueue.push(idx);
        }
      }
    }

    // 2. Propagate outward into empty/transparent pixels up to strokeW
    let sHead = 0;
    while (sHead < sQueue.length) {
      const cur = sQueue[sHead++];
      const curDist = strokeDist[cur];
      if (curDist >= strokeW) continue;

      const cx = cur % width;
      const cy = Math.floor(cur / width);

      const neighbors = [
        cy > 0 ? (cy - 1) * width + cx : -1,
        cy < height - 1 ? (cy + 1) * width + cx : -1,
        cx > 0 ? cy * width + (cx - 1) : -1,
        cx < width - 1 ? cy * width + (cx + 1) : -1,
      ];

      for (const n of neighbors) {
        if (n !== -1 && strokeDist[n] > curDist + 1) {
          // Only expand into transparent or semi-transparent outer boundary
          if (data[n * 4 + 3] <= 180) {
            strokeDist[n] = curDist + 1;
            sQueue.push(n);
          }
        }
      }
    }

    // 3. Paint the stroke in transparent / outer boundary pixels with anti-aliased edge
    for (let i = 0; i < totalPixels; i++) {
      const d = strokeDist[i];
      if (d > 0 && d <= strokeW) {
        const p = i * 4;
        const currentA = data[p + 3];

        let targetAlpha = 255;
        if (d === strokeW && strokeW > 1) {
          targetAlpha = 180; // Anti-aliased outer edge
        }

        if (currentA <= 30) {
          data[p] = sR;
          data[p + 1] = sG;
          data[p + 2] = sB;
          data[p + 3] = targetAlpha;
        } else if (currentA < 255) {
          const blendFactor = (255 - currentA) / 255;
          data[p] = Math.round(data[p] * (1 - blendFactor) + sR * blendFactor);
          data[p + 1] = Math.round(data[p + 1] * (1 - blendFactor) + sG * blendFactor);
          data[p + 2] = Math.round(data[p + 2] * (1 - blendFactor) + sB * blendFactor);
          data[p + 3] = Math.max(currentA, targetAlpha);
        }
      }
    }
  }

  // Put image data back
  ctx.putImageData(imgData, 0, 0);
}
