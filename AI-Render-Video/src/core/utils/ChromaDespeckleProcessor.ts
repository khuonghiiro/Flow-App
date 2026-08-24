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
  shadowRetention?: number;         // 0 to 100% (Bóc tách giữ bóng mờ bán trong suốt / Soft Shadow Unmixing)
  strokeWidth?: number;             // 0 to 15 px (Thêm độ dày viền bao quanh theo màu)
  strokeColorHex?: string;          // Mã màu viền bổ sung (mặc định '#000000' hoặc màu tùy chọn)
  edgeSmoothing?: number;
  despeckleSize: number;            // 0 to 150 px
  whiteSpeckleSensitivity: number;  // 0 to 100%
  keepLargestIslandOnly: boolean;

  // Thuật toán khử rác & Khử viền sượng nâng cao (Advanced Edge & Fringe Cleanup)
  cleanupMode?: 'all' | 'defringe' | 'smooth' | 'despeckle';
  fringeColorType?: 'chroma_green' | 'pure_white' | 'pure_black' | 'custom';
  fringeColorHex?: string;          // Mã màu viền rác cần khử (ví dụ viền xanh, viền trắng, viền đen)
  defringeStrength?: number;        // Độ mạnh khử viền bám màu: 0 to 100%
  edgeChoke?: number;               // Gọt lùi viền sượng: 0 to 5 px
  edgeSmooth?: number;              // Làm mịn & khử răng cưa viền: 0 to 10 px
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

  // 1. Auto-detect Background Color from 4 Corners & Borders if needed
  let sampleR = 0, sampleG = 0, sampleB = 0, sampleCount = 0;
  const sampleIndices = [
    0, // Top-left
    Math.min(totalPixels - 1, width - 1), // Top-right
    Math.min(totalPixels - 1, (height - 1) * width), // Bottom-left
    Math.min(totalPixels - 1, (height - 1) * width + width - 1), // Bottom-right
    Math.min(totalPixels - 1, Math.floor(width / 2)), // Top-mid
    Math.min(totalPixels - 1, (height - 1) * width + Math.floor(width / 2)), // Bottom-mid
    Math.min(totalPixels - 1, Math.floor(height / 2) * width), // Left-mid
    Math.min(totalPixels - 1, Math.floor(height / 2) * width + width - 1), // Right-mid
  ];

  for (const sIdx of sampleIndices) {
    const p = sIdx * 4;
    if (data[p + 3] >= 50) {
      sampleR += data[p];
      sampleG += data[p + 1];
      sampleB += data[p + 2];
      sampleCount++;
    }
  }

  const avgR = sampleCount > 0 ? sampleR / sampleCount : 255;
  const avgG = sampleCount > 0 ? sampleG / sampleCount : 255;
  const avgB = sampleCount > 0 ? sampleB / sampleCount : 255;

  const isCornerPureWhite = avgR > 215 && avgG > 215 && avgB > 215;
  const isCornerGreen = avgG > 90 && (avgG - Math.max(avgR, avgB)) > 25;

  // Resolve Effective Mode
  let effectiveMode = options.keyColorType;
  if (options.keyColorType === 'chroma_green' && isCornerPureWhite) {
    // User loaded a white image but keyColorType was left on chroma_green: auto-correct to pure_white!
    effectiveMode = 'pure_white';
  } else if (options.keyColorType === 'pure_white' && isCornerGreen) {
    // User loaded a green image but keyColorType was pure_white: auto-correct to chroma_green!
    effectiveMode = 'chroma_green';
  }

  // 2. Classify key pixels with strict channel protection (Prevents punching holes into cyan/blue/amber eyes)
  const isKeyPixel = new Uint8Array(totalPixels);

  if (effectiveMode === 'pure_white') {
    // Strict White Background: ALL 3 channels (R, G, B) must be very bright (>= 235..248)
    // Blue/Cyan eye sparkles have low R (50..180) -> NEVER MATCHED!
    // Skin tone has lower G & B (G < 225, B < 210) -> NEVER MATCHED!
    const tolFactor = Math.max(5, Math.min(60, options.tolerance || 38));
    const whiteMinChannel = Math.max(220, 255 - Math.round(tolFactor * 0.45)); // e.g. 255 - 17 = 238

    for (let i = 0; i < totalPixels; i++) {
      const p = i * 4;
      if (data[p + 3] < 15) {
        isKeyPixel[i] = 1;
        continue;
      }
      const r = data[p];
      const g = data[p + 1];
      const b = data[p + 2];

      if (r >= whiteMinChannel && g >= whiteMinChannel && b >= whiteMinChannel) {
        isKeyPixel[i] = 1;
      }
    }
  } else if (effectiveMode === 'chroma_green') {
    // Strict Chroma Green: Green must dominate BOTH Red and Blue
    // Dark Lineart Protection: Thin eyebrows, eyelashes, tears, eyeliner have R,B ~ 0 and low G (0..90).
    // They must NEVER be treated as green background!
    const tolFactor = Math.max(5, Math.min(80, options.tolerance || 38));
    const greenDiffMin = Math.max(16, 42 - Math.round(tolFactor * 0.35)); // e.g. 29
    const minGreenBrightness = Math.max(105, 155 - Math.round(tolFactor * 0.5)); // e.g. 136

    for (let i = 0; i < totalPixels; i++) {
      const p = i * 4;
      if (data[p + 3] < 15) {
        isKeyPixel[i] = 1;
        continue;
      }
      const r = data[p];
      const g = data[p + 1];
      const b = data[p + 2];

      // Dark lineart / eyebrow protection
      const maxRGB = Math.max(r, g, b);
      const lum = 0.299 * r + 0.587 * g + 0.114 * b;
      if (maxRGB < 75 || lum < 60) {
        // Definitely dark lineart stroke, NOT green background!
        continue;
      }

      if (g >= minGreenBrightness && (g - Math.max(r, b)) >= greenDiffMin) {
        isKeyPixel[i] = 1;
      } else if (g > Math.max(r, b) && (g - Math.max(r, b)) >= 18) {
        // Soft shadow & translucent region on green background
        isKeyPixel[i] = 1;
      }
    }
  } else {
    // Custom Hex Color
    const hex = options.keyColorHex.replace('#', '');
    const tR = parseInt(hex.slice(0, 2), 16) || 0;
    const tG = parseInt(hex.slice(2, 4), 16) || 255;
    const tB = parseInt(hex.slice(4, 6), 16) || 0;
    const maxThreshold = ((options.tolerance || 38) / 100) * 200;

    for (let i = 0; i < totalPixels; i++) {
      const p = i * 4;
      if (data[p + 3] < 15) {
        isKeyPixel[i] = 1;
        continue;
      }
      const r = data[p];
      const g = data[p + 1];
      const b = data[p + 2];
      const dist = Math.sqrt((r - tR) * (r - tR) + (g - tG) * (g - tG) + (b - tB) * (b - tB));
      if (dist <= maxThreshold) {
        isKeyPixel[i] = 1;
      }
    }
  }

  // 3. Mode Selection: "all" vs "outer_only" (Contiguous Boundary Flood-Fill)
  const isTransparent = new Uint8Array(totalPixels);

  if (options.isolationMode === 'outer_only') {
    // BFS Flood Fill strictly starting from the 4 outer perimeter edges
    const queue: number[] = [];

    // Pre-seed existing transparent pixels
    for (let i = 0; i < totalPixels; i++) {
      if (data[i * 4 + 3] < 15) {
        isTransparent[i] = 1;
        queue.push(i);
      }
    }

    // Top & Bottom perimeter edges
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

    // Left & Right perimeter edges
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
    for (let i = 0; i < totalPixels; i++) {
      if (isKeyPixel[i]) {
        isTransparent[i] = 1;
      }
    }
  }

  // Apply transparency to outer background with Physical Soft Shadow & Translucent Silk Unmixing
  const shadowRetention = Math.max(0, Math.min(100, options.shadowRetention || 0));

  for (let i = 0; i < totalPixels; i++) {
    const p = i * 4;
    if (isTransparent[i]) {
      if (shadowRetention > 0 && effectiveMode === 'chroma_green') {
        const r = data[p];
        const g = data[p + 1];
        const b = data[p + 2];
        const maxRB = Math.max(r, b);

        // Solid pure green background without shadow
        if (g >= 242 && maxRB <= 20) {
          data[p + 3] = 0;
          continue;
        }

        // Physical shadow opacity on green screen (255 - G) / 255:
        const shadowAlpha = (255.0 - g) / 255.0;

        // Case A: Translucent Glow Rays, Catchlight Star Flare or Colored Silk (R or B is significant)
        if (maxRB > 35) {
          const silkAlpha = maxRB / 235.0;
          const effAlpha = Math.max(shadowAlpha, silkAlpha) * (shadowRetention / 100.0);
          const cleanG = Math.min(g, Math.round(maxRB * 0.95));
          const outA = Math.round(Math.min(255, Math.max(0, effAlpha * 255)));

          if (outA > 8) {
            data[p + 1] = cleanG; // Clean despilled silk color
            data[p + 3] = outA;   // Translucent glow alpha!
            continue;
          }
        }
        // Case B: Soft Shadow & Ambient Occlusion (Low R, B, and G reduced due to shadow darkness)
        else {
          const effAlpha = shadowAlpha * (shadowRetention / 100.0);
          const outA = Math.round(Math.min(255, Math.max(0, effAlpha * 255)));

          if (outA > 8) {
            data[p + 1] = maxRB; // Neutralize green to clean neutral dark shadow
            data[p + 3] = outA;  // Smooth semi-transparent alpha!
            continue;
          }
        }
      }
      data[p + 3] = 0;
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
        if (data[pIdx * 4 + 3] === 0) {
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
        const r = data[p], g = data[p + 1], b = data[p + 2];
        const maxRGB = Math.max(r, g, b);
        const lum = 0.299 * r + 0.587 * g + 0.114 * b;
        const isDarkLine = maxRGB < 85 || lum < 70;

        if (d <= erodeDistanceCutoff && !isDarkLine) {
          // Erode extreme fringe pixels completely (only if NOT a dark lineart stroke!)
          data[p + 3] = 0;
        } else {
          // Smooth alpha ramp from boundary to inner core
          const span = Math.max(1, featherRadius - erodeDistanceCutoff);
          const t = Math.max(0, Math.min(1, (d - erodeDistanceCutoff) / span));
          const smoothAlpha = t * t * (3 - 2 * t);
          if (!isDarkLine) {
            data[p + 3] = Math.round(data[p + 3] * smoothAlpha);
          }

          // Green despill on remaining soft edge pixels
          if (effectiveMode === 'chroma_green') {
            const maxRB = Math.max(r, b);
            if (g > maxRB) {
              data[p + 1] = Math.round(maxRB);
            }
          }
        }
      }
    }
  }

  // 5. Advanced Edge Choke, Color Defringe & Boundary Anti-Aliasing
  applyAdvancedEdgeCleanupAndDefringe(data, width, height, options);

  // 6. White Speckle / Noise Removal (Safeguard pupil reflections & tears)
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
            // Only erase if 3 or more sides are totally empty (pure floating white pixel)
            if (transparentCount >= 3) {
              data[idx + 3] = 0;
            }
          }
        }
      }
    }
  }

  // 7. Despeckle / Connected Component Area Filtering (Guarantees small eyebrows/tears are NEVER erased)
  if (options.despeckleSize > 0 || options.keepLargestIslandOnly) {
    const visited = new Uint8Array(totalPixels);
    const minPixelCount = options.despeckleSize * 4;
    const islands: { size: number; isDarkOrDetail: boolean; pixels: number[] }[] = [];

    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        const pIdx = y * width + x;
        if (visited[pIdx] || data[pIdx * 4 + 3] < 10) continue;

        const queue: number[] = [pIdx];
        visited[pIdx] = 1;
        const currentIsland: number[] = [];
        let darkOrColoredCount = 0;

        while (queue.length > 0) {
          const cur = queue.pop()!;
          currentIsland.push(cur);

          const cp = cur * 4;
          const cr = data[cp], cg = data[cp + 1], cb = data[cp + 2];
          const lum = 0.299 * cr + 0.587 * cg + 0.114 * cb;
          const maxC = Math.max(cr, cg, cb);
          // Detect if pixel is part of dark lineart (eyebrow, eyelash) or colorful eye/tear
          if (maxC < 95 || lum < 80 || Math.abs(cr - cg) > 25 || Math.abs(cb - cg) > 25) {
            darkOrColoredCount++;
          }

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

        const isDarkOrDetail = currentIsland.length > 0 && (darkOrColoredCount / currentIsland.length) >= 0.25;
        islands.push({ size: currentIsland.length, isDarkOrDetail, pixels: currentIsland });
      }
    }

    if (options.keepLargestIslandOnly && islands.length > 0) {
      let largest = islands[0];
      for (const isl of islands) {
        if (isl.size > largest.size) largest = isl;
      }

      for (const isl of islands) {
        // NEVER erase detached islands if they contain dark lineart or facial details!
        if (isl !== largest && !isl.isDarkOrDetail) {
          for (const p of isl.pixels) {
            data[p * 4 + 3] = 0;
          }
        }
      }
    } else if (options.despeckleSize > 0) {
      for (const isl of islands) {
        if (isl.size < minPixelCount && !isl.isDarkOrDetail) {
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

  // 8. Auto Chromatic Reconstruction & Smart Despill for Green Cast Removal
  // Automatically restores natural warm/neutral tones on eyebrows, eyelashes, hair, skin, and clothing borders
  if (effectiveMode === 'chroma_green') {
    for (let i = 0; i < totalPixels; i++) {
      const p = i * 4;
      if (data[p + 3] > 0) {
        const r = data[p];
        const g = data[p + 1];
        const b = data[p + 2];
        const maxRB = Math.max(r, b);
        const avgRB = Math.round((r + b) / 2);

        // Case 1: Dark Lineart / Eyebrows / Eyelashes / Hair (maxRB < 95 or Lum < 80)
        if (maxRB < 95 && g > maxRB) {
          const greenExcess = g - maxRB;
          if (r >= b) {
            // Warm brown/chestnut -> boost R slightly to recover rich warmth
            data[p] = Math.min(255, r + Math.round(greenExcess * 0.35));
            data[p + 1] = Math.min(data[p], Math.round(avgRB * 0.95));
          } else {
            // Charcoal / Black / Navy
            data[p + 1] = Math.round(avgRB * 0.9);
          }
        }
        // Case 2: Skin Tones (R is high, G was contaminated to exceed R or close to R)
        else if (r > 120 && r > b) {
          const maxAllowedG = Math.round(r * 0.82 + b * 0.12);
          if (g > maxAllowedG) {
            data[p + 1] = maxAllowedG;
            data[p] = Math.min(255, r + 4);
          }
        }
        // Case 3: General Subjects (Clothing, Eyes, Accessories with green spill)
        else if (g > maxRB) {
          data[p + 1] = Math.round(maxRB * 0.92);
        }
      }
    }
  }

  // Put image data back
  ctx.putImageData(imgData, 0, 0);
}

/**
 * Advanced Edge Choke, Color Defringe & Boundary Anti-Aliasing
 * Eliminates residual colored halo pixels (e.g. green/white/black/custom fringe)
 * and smooths jagged stepped edges ("khử sượng / răng cưa").
 */
export function applyAdvancedEdgeCleanupAndDefringe(
  data: Uint8ClampedArray,
  width: number,
  height: number,
  options: ChromaProcessOptions
): void {
  const totalPixels = width * height;
  const defringeStrength = Math.min(100, Math.max(0, options.defringeStrength ?? 60));
  const edgeChoke = Math.min(5, Math.max(0, options.edgeChoke ?? 0));
  const edgeSmooth = Math.min(10, Math.max(0, options.edgeSmooth ?? 0));
  const fringeType = options.fringeColorType || (options.keyColorType === 'pure_white' ? 'pure_white' : 'chroma_green');

  // 1. Calculate BFS Distance Transform from Transparent Background (alpha <= 15)
  const edgeDist = new Uint8Array(totalPixels);
  edgeDist.fill(255);
  const queue: number[] = [];

  for (let i = 0; i < totalPixels; i++) {
    if (data[i * 4 + 3] <= 15) {
      edgeDist[i] = 0;
      queue.push(i);
    }
  }

  let head = 0;
  const maxTrackDist = 6;
  while (head < queue.length) {
    const cur = queue[head++];
    const curDist = edgeDist[cur];
    if (curDist >= maxTrackDist) continue;

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
        queue.push(n);
      }
    }
  }

  // 2. Edge Choke: Shave off outermost jagged fringe pixels (protect dark lineart/eyebrows)
  if (edgeChoke > 0) {
    for (let i = 0; i < totalPixels; i++) {
      const d = edgeDist[i];
      if (d > 0 && d <= edgeChoke) {
        const p = i * 4;
        const r = data[p], g = data[p + 1], b = data[p + 2];
        const maxRGB = Math.max(r, g, b);
        const lum = 0.299 * r + 0.587 * g + 0.114 * b;
        if (maxRGB < 85 || lum < 70) {
          // Keep thin black lineart/eyebrow strokes!
          continue;
        }
        data[p + 3] = 0;
      }
    }
  }

  // 3. Color Defringe & Decontamination: Target leftover fringe color along border (dist <= 4)
  if (defringeStrength > 0 || options.fringeColorHex) {
    let targetR = 0, targetG = 255, targetB = 0;
    if (fringeType === 'pure_white') {
      targetR = 255; targetG = 255; targetB = 255;
    } else if (fringeType === 'pure_black') {
      targetR = 0; targetG = 0; targetB = 0;
    } else if (fringeType === 'custom' && options.fringeColorHex) {
      const hex = options.fringeColorHex.replace('#', '');
      targetR = parseInt(hex.slice(0, 2), 16) || 0;
      targetG = parseInt(hex.slice(2, 4), 16) || 0;
      targetB = parseInt(hex.slice(4, 6), 16) || 0;
    }

    const strengthRatio = defringeStrength / 100;

    for (let y = 1; y < height - 1; y++) {
      for (let x = 1; x < width - 1; x++) {
        const pIdx = y * width + x;
        const d = edgeDist[pIdx];
        if (d <= edgeChoke || d > 4) continue;

        const p = pIdx * 4;
        const a = data[p + 3];
        if (a <= 15) continue;

        const r = data[p];
        const g = data[p + 1];
        const b = data[p + 2];

        // Evaluate match confidence (0.0 to 1.0)
        let match = 0;
        if (fringeType === 'chroma_green') {
          const maxRB = Math.max(r, b);
          if (g > maxRB) {
            match = Math.min(1, (g - maxRB) / 35);
          }
        } else if (fringeType === 'pure_white') {
          const minRGB = Math.min(r, g, b);
          if (minRGB > 180) {
            match = Math.min(1, (minRGB - 180) / 75);
          }
        } else if (fringeType === 'pure_black') {
          const maxRGB = Math.max(r, g, b);
          if (maxRGB < 70) {
            match = Math.min(1, (70 - maxRGB) / 70);
          }
        } else {
          // Custom color distance
          const dist = Math.sqrt((r - targetR) ** 2 + (g - targetG) ** 2 + (b - targetB) ** 2);
          if (dist < 130) {
            match = Math.max(0, 1 - dist / 130);
          }
        }

        if (match > 0.15) {
          // Look for adjacent interior core color (dist >= 3)
          let coreR = 0, coreG = 0, coreB = 0, coreCount = 0;
          for (let dy = -2; dy <= 2; dy++) {
            for (let dx = -2; dx <= 2; dx++) {
              const ny = y + dy;
              const nx = x + dx;
              if (ny >= 0 && ny < height && nx >= 0 && nx < width) {
                const nIdx = ny * width + nx;
                if (edgeDist[nIdx] >= 3 && data[nIdx * 4 + 3] > 100) {
                  coreR += data[nIdx * 4];
                  coreG += data[nIdx * 4 + 1];
                  coreB += data[nIdx * 4 + 2];
                  coreCount++;
                }
              }
            }
          }

          const effMatch = match * strengthRatio;
          if (coreCount > 0) {
            const avgCoreR = coreR / coreCount;
            const avgCoreG = coreG / coreCount;
            const avgCoreB = coreB / coreCount;

            data[p] = Math.round(r * (1 - effMatch) + avgCoreR * effMatch);
            data[p + 1] = Math.round(g * (1 - effMatch) + avgCoreG * effMatch);
            data[p + 2] = Math.round(b * (1 - effMatch) + avgCoreB * effMatch);
          } else if (fringeType === 'chroma_green') {
            // Despill green to max(r, b)
            data[p + 1] = Math.round(g * (1 - effMatch) + Math.max(r, b) * effMatch);
          }

          // Soften edge alpha smoothly
          data[p + 3] = Math.round(a * (1 - effMatch * 0.65));
        }
      }
    }
  }

  // 4. Edge Smoothing / Anti-Aliasing (Khử sượng / răng cưa viền)
  if (edgeSmooth > 0) {
    const smoothRadius = Math.min(3, Math.max(1, Math.round(edgeSmooth / 3)));
    const originalAlpha = new Uint8Array(totalPixels);
    for (let i = 0; i < totalPixels; i++) originalAlpha[i] = data[i * 4 + 3];

    for (let y = smoothRadius; y < height - smoothRadius; y++) {
      for (let x = smoothRadius; x < width - smoothRadius; x++) {
        const pIdx = y * width + x;
        const d = edgeDist[pIdx];
        if (d <= edgeChoke || d > 3) continue;

        let alphaSum = 0;
        let count = 0;
        for (let dy = -smoothRadius; dy <= smoothRadius; dy++) {
          for (let dx = -smoothRadius; dx <= smoothRadius; dx++) {
            const nIdx = (y + dy) * width + (x + dx);
            alphaSum += originalAlpha[nIdx];
            count++;
          }
        }

        const avgA = alphaSum / count;
        const origA = originalAlpha[pIdx];
        const blend = Math.min(1, (edgeSmooth / 10) * 0.7);
        data[pIdx * 4 + 3] = Math.round(origA * (1 - blend) + avgA * blend);
      }
    }
  }
}

/**
 * Standalone runner for Despeckle & Edge Cleanup
 */
export function applyStandaloneDespeckleAndEdgeCleanup(
  ctx: CanvasRenderingContext2D,
  width: number,
  height: number,
  options: ChromaProcessOptions
): void {
  const imgData = ctx.getImageData(0, 0, width, height);
  const data = imgData.data;
  const totalPixels = width * height;

  // 1. Edge cleanup & defringe
  applyAdvancedEdgeCleanupAndDefringe(data, width, height, options);

  // 2. White speckles
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

  // 3. Islands
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

  ctx.putImageData(imgData, 0, 0);
}
