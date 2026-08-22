import React, { useState, useRef, useEffect, useCallback } from 'react';
import {
  Scissors,
  Sparkles,
  Upload,
  Layers,
  Check,
  RotateCcw,
  Sliders,
  Download,
  Eye,
  Compass,
  ArrowRight,
  RefreshCw,
  Eraser,
} from 'lucide-react';
import {
  Character2DAssembly,
  Character2DAngle,
  Character2DPartType,
} from '../../types/scene2d';
import {
  GRID_CATEGORY_DEFINITIONS,
  GridCategoryDefinition,
  GridCellDefinition,
  generateDemoGridSpriteSheet,
} from '../../core/assets/GridSliceRegistry';
import {
  ThreeMultiAngleBillboardEngine,
  AngleDetectionResult,
} from '../../core/engine2d/ThreeMultiAngleBillboardEngine';
import { CellPixelEraserModal } from './CellPixelEraserModal';

interface AutoGridSlicer3DAssemblerProps {
  currentAssembly: Character2DAssembly;
  onApplyAssembly: (updatedAssembly: Character2DAssembly) => void;
  onSwitchToAssemblyTab?: () => void;
}

export const AutoGridSlicer3DAssembler: React.FC<AutoGridSlicer3DAssemblerProps> = ({
  currentAssembly,
  onApplyAssembly,
  onSwitchToAssemblyTab,
}) => {
  const [selectedCatId, setSelectedCatId] = useState<string>('hair_multi_angle_grid');
  const [sourceImage, setSourceImage] = useState<string | null>(null);
  const [userUploadedImageUrl, setUserUploadedImageUrl] = useState<string | null>(null);

  // Background removal parameters
  const [keyColorType, setKeyColorType] = useState<'chroma_green' | 'pure_white' | 'custom'>('chroma_green');
  const [keyColorHex, setKeyColorHex] = useState<string>('#00ff00');
  const [tolerance, setTolerance] = useState<number>(45);
  const [feather, setFeather] = useState<number>(2);

  // Despeckle & Isolated Stray Noise Cleanup filter
  const [bgCleanupSubTab, setBgCleanupSubTab] = useState<'chroma' | 'despeckle'>('chroma');
  const [despeckleSize, setDespeckleSize] = useState<number>(30); // Max isolated component area in pixels to erase (0-250px)
  const [whiteSpeckleSensitivity, setWhiteSpeckleSensitivity] = useState<number>(50); // Sensitivity for isolated white dots (0-100%)
  const [keepLargestIslandOnly, setKeepLargestIslandOnly] = useState<boolean>(false);

  // Grid adjustment offsets (px)
  const [gridMarginX, setGridMarginX] = useState<number>(0);
  const [gridMarginY, setGridMarginY] = useState<number>(0);
  const [gridGapX, setGridGapX] = useState<number>(0);
  const [gridGapY, setGridGapY] = useState<number>(0);

  // Sliced state
  const [selectedCell, setSelectedCell] = useState<GridCellDefinition | null>(null);
  const [slicedResults, setSlicedResults] = useState<Map<string, string>>(new Map());
  const [isProcessing, setIsProcessing] = useState<boolean>(false);
  const [assemblySuccess, setAssemblySuccess] = useState<boolean>(false);

  // 3D Engine State
  const [activeAngleInfo, setActiveAngleInfo] = useState<AngleDetectionResult>({
    angleDeg: 0,
    discreteAngle: 'front',
    angleLabel: 'Chính diện (Front 0°)',
    compassDirection: 'S',
  });
  const [turntableAngle, setTurntableAngle] = useState<number>(0);

  const imageCanvasRef = useRef<HTMLCanvasElement>(null);
  const threeContainerRef = useRef<HTMLDivElement>(null);
  const threeEngineRef = useRef<ThreeMultiAngleBillboardEngine | null>(null);
  const loadedImageRef = useRef<HTMLImageElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const currentCategory: GridCategoryDefinition =
    GRID_CATEGORY_DEFINITIONS.find((c) => c.id === selectedCatId) ||
    GRID_CATEGORY_DEFINITIONS[0];

  const [previewDisplayMode, setPreviewDisplayMode] = useState<'transparent' | 'original'>('original');
  const [hasExplicitlySliced, setHasExplicitlySliced] = useState<boolean>(false);
  const slicedImgElementsRef = useRef<Map<string, HTMLImageElement>>(new Map());

  // Interactive Grid Dividers: X coordinates for columns [X0..X_cols] & Y coordinates for rows [Y0..Y_rows]
  const [colDividers, setColDividers] = useState<number[]>([]);
  const [rowDividers, setRowDividers] = useState<number[]>([]);
  const [hoveredDivider, setHoveredDivider] = useState<{ type: 'col' | 'row'; index: number } | null>(null);
  const [activeDividerDrag, setActiveDividerDrag] = useState<{ type: 'col' | 'row'; index: number } | null>(null);

  // Pixel Eraser & Cleanup Modal for individual slice
  const [editingCellData, setEditingCellData] = useState<{ cell: GridCellDefinition; dataUrl: string } | null>(null);

  /**
   * Initializes standard uniform grid dividers based on category rows/cols and margins
   */
  const initDividers = useCallback(
    (img: HTMLImageElement) => {
      const rows = currentCategory.rows;
      const cols = currentCategory.cols;
      const marginX = gridMarginX;
      const marginY = gridMarginY;
      const usableW = img.width - marginX * 2;
      const usableH = img.height - marginY * 2;
      const cellW = usableW / cols;
      const cellH = usableH / rows;

      const colsArr: number[] = [];
      for (let c = 0; c <= cols; c++) {
        colsArr.push(Math.round(marginX + c * cellW));
      }

      const rowsArr: number[] = [];
      for (let r = 0; r <= rows; r++) {
        rowsArr.push(Math.round(marginY + r * cellH));
      }

      setColDividers(colsArr);
      setRowDividers(rowsArr);
    },
    [currentCategory, gridMarginX, gridMarginY]
  );

  /**
   * Helper to retrieve bounding box of cell [r, c] strictly from the Divider Lines
   */
  const getCellRect = useCallback(
    (r: number, c: number, img: HTMLImageElement): { x: number; y: number; w: number; h: number } => {
      const cols = currentCategory.cols;
      const rows = currentCategory.rows;

      if (colDividers.length === cols + 1 && rowDividers.length === rows + 1) {
        const x = colDividers[c];
        const nextX = colDividers[c + 1];
        const y = rowDividers[r];
        const nextY = rowDividers[r + 1];
        return {
          x: Math.round(x),
          y: Math.round(y),
          w: Math.max(10, Math.round(nextX - x)),
          h: Math.max(10, Math.round(nextY - y)),
        };
      }

      // Default uniform formula fallback
      const cellW = (img.width - gridMarginX * 2) / cols;
      const cellH = (img.height - gridMarginY * 2) / rows;
      return {
        x: Math.round(gridMarginX + c * cellW),
        y: Math.round(gridMarginY + r * cellH),
        w: Math.round(cellW),
        h: Math.round(cellH),
      };
    },
    [colDividers, rowDividers, currentCategory, gridMarginX, gridMarginY]
  );

  /**
   * Samples pixel at top-left corner (4, 4) to auto-detect background color
   */
  const autoDetectBackgroundColor = (img: HTMLImageElement): string => {
    try {
      const c = document.createElement('canvas');
      c.width = img.width;
      c.height = img.height;
      const ctx = c.getContext('2d');
      if (!ctx) return '#00ff00';
      ctx.drawImage(img, 0, 0);
      const pixel = ctx.getImageData(4, 4, 1, 1).data;
      const hex = `#${((1 << 24) + (pixel[0] << 16) + (pixel[1] << 8) + pixel[2]).toString(16).slice(1)}`;
      return hex;
    } catch {
      return '#00ff00';
    }
  };

  const loadImage = (url: string, isUserUpload = false) => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      loadedImageRef.current = img;
      setSourceImage(url);
      setHasExplicitlySliced(false);
      setPreviewDisplayMode('original');
      initDividers(img);

      if (isUserUpload) {
        // Auto detect background color of uploaded image
        const detectedColor = autoDetectBackgroundColor(img);
        setKeyColorHex(detectedColor);

        // Check if detected color is green or white
        const rgb = hexToRgb(detectedColor);
        if (rgb.g > rgb.r + 30 && rgb.g > rgb.b + 30) {
          setKeyColorType('chroma_green');
        } else if (rgb.r > 200 && rgb.g > 200 && rgb.b > 200) {
          setKeyColorType('pure_white');
        } else {
          setKeyColorType('custom');
        }
      }

      drawGridOverlay();
    };
    img.src = url;
  };

  // Initialize or handle category change
  useEffect(() => {
    if (!userUploadedImageUrl) {
      const demoUrl = generateDemoGridSpriteSheet(
        selectedCatId,
        keyColorType === 'chroma_green' ? 'chroma_green' : 'pure_white'
      );
      loadImage(demoUrl, false);
    } else {
      if (loadedImageRef.current) {
        initDividers(loadedImageRef.current);
      }
      drawGridOverlay();
    }
  }, [selectedCatId]);

  // Sync 3D engine container
  useEffect(() => {
    if (threeContainerRef.current && !threeEngineRef.current) {
      threeEngineRef.current = new ThreeMultiAngleBillboardEngine(
        threeContainerRef.current,
        (res) => setActiveAngleInfo(res)
      );
      threeEngineRef.current.setAssembly(currentAssembly);
    }

    return () => {
      if (threeEngineRef.current) {
        threeEngineRef.current.dispose();
        threeEngineRef.current = null;
      }
    };
  }, []);

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = (ev) => {
        if (ev.target?.result) {
          const dataUrl = ev.target.result as string;
          setUserUploadedImageUrl(dataUrl);
          loadImage(dataUrl, true);
        }
      };
      reader.readAsDataURL(file);
      e.target.value = ''; // Reset input to allow re-uploading same file
    }
  };

  /**
   * Draws a dark checkerboard background pattern for transparent alpha verification
   */
  const drawCheckerboard = (
    ctx: CanvasRenderingContext2D,
    x: number,
    y: number,
    w: number,
    h: number,
    size = 14
  ) => {
    ctx.fillStyle = '#0f172a';
    ctx.fillRect(x, y, w, h);
    ctx.fillStyle = '#1e293b';
    for (let cy = y; cy < y + h; cy += size) {
      for (let cx = x; cx < x + w; cx += size) {
        const sw = Math.min(size, x + w - cx);
        const sh = Math.min(size, y + h - cy);
        if ((Math.floor((cx - x) / size) + Math.floor((cy - y) / size)) % 2 === 0) {
          ctx.fillRect(cx, cy, sw, sh);
        }
      }
    }
  };

  /**
   * Draws the imported image with glowing bounding grid lines, divider splitters, and cell badges.
   * NEVER stretches or distorts the underlying sprite sheet image!
   */
  const drawGridOverlay = useCallback(() => {
    const canvas = imageCanvasRef.current;
    const img = loadedImageRef.current;
    if (!canvas || !img) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    canvas.width = img.width;
    canvas.height = img.height;

    const rows = currentCategory.rows;
    const cols = currentCategory.cols;

    const isTransMode = previewDisplayMode === 'transparent' && hasExplicitlySliced && slicedResults.size > 0;

    // 1. Draw background or source image
    if (isTransMode) {
      drawCheckerboard(ctx, 0, 0, canvas.width, canvas.height, 16);
    } else {
      // Draw static full sprite sheet 1:1 scale
      ctx.drawImage(img, 0, 0);
    }

    // 2. Draw each cell (either transparent PNG or original crop without distortion)
    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        const rect = getCellRect(r, c, img);
        const { x, y, w: cellW, h: cellH } = rect;

        const key = `${r}_${c}`;
        const cellDef = currentCategory.cells.find((cell) => cell.row === r && cell.col === c);
        const isSelected = selectedCell?.row === r && selectedCell?.col === c;

        if (isTransMode) {
          // Draw individual cell checkerboard
          drawCheckerboard(ctx, x, y, cellW, cellH, 12);

          const cellImg = slicedImgElementsRef.current.get(key);
          if (cellImg && cellImg.complete && cellImg.width === cellW && cellImg.height === cellH) {
            ctx.drawImage(cellImg, x, y, cellW, cellH);
          } else {
            // Draw exact region from underlying static source image without squishing
            ctx.drawImage(img, x, y, cellW, cellH, x, y, cellW, cellH);
          }
        }

        // Fill background highlight if selected
        if (isSelected) {
          ctx.fillStyle = 'rgba(56, 189, 248, 0.15)';
          ctx.fillRect(x, y, cellW, cellH);
        }

        // Cell badge header
        ctx.fillStyle = isSelected ? '#0284c7' : 'rgba(15, 23, 42, 0.88)';
        ctx.fillRect(x + 2, y + 2, Math.min(cellW - 4, 180), 22);

        ctx.fillStyle = '#ffffff';
        ctx.font = 'bold 11px Inter, sans-serif';
        ctx.fillText(cellDef ? cellDef.label : `[R${r + 1} C${c + 1}]`, x + 6, y + 17);
      }
    }

    // 3. Draw Grid Lines & Interactive Splitters
    if (colDividers.length === cols + 1 && rowDividers.length === rows + 1) {
      const x0 = colDividers[0];
      const xLast = colDividers[cols];
      const y0 = rowDividers[0];
      const yLast = rowDividers[rows];

      // A. Outer Border Box (Fixed / Non-resizable outer edges)
      ctx.strokeStyle = '#0284c7';
      ctx.lineWidth = 2.5;
      ctx.setLineDash([]);
      ctx.strokeRect(x0, y0, xLast - x0, yLast - y0);

      // B. Internal Vertical Dividers [1 .. cols-1]
      for (let c = 1; c < cols; c++) {
        const x = colDividers[c];
        const isHovered = (hoveredDivider?.type === 'col' && hoveredDivider.index === c) || (activeDividerDrag?.type === 'col' && activeDividerDrag.index === c);

        if (isHovered) {
          ctx.strokeStyle = '#38bdf8';
          ctx.lineWidth = 4;
          ctx.shadowColor = '#38bdf8';
          ctx.shadowBlur = 10;
          ctx.beginPath();
          ctx.moveTo(x, y0);
          ctx.lineTo(x, yLast);
          ctx.stroke();
          ctx.shadowBlur = 0;

          // Center Handle Pill with ↔ symbol
          const midY = (y0 + yLast) / 2;
          ctx.fillStyle = '#38bdf8';
          ctx.fillRect(x - 6, midY - 14, 12, 28);
          ctx.fillStyle = '#0f172a';
          ctx.font = 'bold 11px sans-serif';
          ctx.fillText('↔', x - 5, midY + 4);
        } else {
          ctx.strokeStyle = isTransMode ? 'rgba(56, 189, 248, 0.6)' : 'rgba(56, 189, 248, 0.45)';
          ctx.lineWidth = 1.5;
          ctx.setLineDash([6, 4]);
          ctx.beginPath();
          ctx.moveTo(x, y0);
          ctx.lineTo(x, yLast);
          ctx.stroke();
          ctx.setLineDash([]);
        }
      }

      // C. Internal Horizontal Dividers [1 .. rows-1]
      for (let r = 1; r < rows; r++) {
        const y = rowDividers[r];
        const isHovered = (hoveredDivider?.type === 'row' && hoveredDivider.index === r) || (activeDividerDrag?.type === 'row' && activeDividerDrag.index === r);

        if (isHovered) {
          ctx.strokeStyle = '#38bdf8';
          ctx.lineWidth = 4;
          ctx.shadowColor = '#38bdf8';
          ctx.shadowBlur = 10;
          ctx.beginPath();
          ctx.moveTo(x0, y);
          ctx.lineTo(xLast, y);
          ctx.stroke();
          ctx.shadowBlur = 0;

          // Center Handle Pill with ↕ symbol
          const midX = (x0 + xLast) / 2;
          ctx.fillStyle = '#38bdf8';
          ctx.fillRect(midX - 14, y - 6, 28, 12);
          ctx.fillStyle = '#0f172a';
          ctx.font = 'bold 11px sans-serif';
          ctx.fillText('↕', midX - 3, y + 4);
        } else {
          ctx.strokeStyle = isTransMode ? 'rgba(56, 189, 248, 0.6)' : 'rgba(56, 189, 248, 0.45)';
          ctx.lineWidth = 1.5;
          ctx.setLineDash([6, 4]);
          ctx.beginPath();
          ctx.moveTo(x0, y);
          ctx.lineTo(xLast, y);
          ctx.stroke();
          ctx.setLineDash([]);
        }
      }
    }
  }, [currentCategory, getCellRect, selectedCell, previewDisplayMode, hasExplicitlySliced, slicedResults, colDividers, rowDividers, hoveredDivider, activeDividerDrag]);

  useEffect(() => {
    drawGridOverlay();
  }, [drawGridOverlay]);

  const [maskMode, setMaskMode] = useState<'flood_fill_outer' | 'global_color'>('flood_fill_outer');
  const [enableDeSpill, setEnableDeSpill] = useState<boolean>(true);

  /**
   * Helper: Parses hex color to RGB
   */
  const hexToRgb = (hex: string) => {
    const clean = hex.replace('#', '');
    const num = parseInt(clean, 16);
    return {
      r: (num >> 16) & 255,
      g: (num >> 8) & 255,
      b: num & 255,
    };
  };

  /**
   * Performs pixel-level Chroma Key / Transparency Removal on an image cell
   * Uses Flood-Fill Connected Masking from outer borders to PROTECT internal hair strands and details
   */
  const sliceAndRemoveBgCell = (
    img: HTMLImageElement,
    x: number,
    y: number,
    w: number,
    h: number
  ): string => {
    const intW = Math.max(1, Math.round(w));
    const intH = Math.max(1, Math.round(h));
    const offCanvas = document.createElement('canvas');
    offCanvas.width = intW;
    offCanvas.height = intH;
    const offCtx = offCanvas.getContext('2d');
    if (!offCtx) return '';

    offCtx.drawImage(img, x, y, w, h, 0, 0, intW, intH);
    const imgData = offCtx.getImageData(0, 0, intW, intH);
    const data = imgData.data;

    const targetRgb = hexToRgb(keyColorHex);
    const tolThreshold = (tolerance / 100) * 441.67;
    const isGreenKey = targetRgb.g > targetRgb.r + 20 && targetRgb.g > targetRgb.b + 20;

    const totalPixels = intW * intH;
    const isCandidateBg = new Uint8Array(totalPixels);

    // Step 1: Mark all candidate background pixels based on color distance
    for (let i = 0; i < totalPixels; i++) {
      const idx = i * 4;
      const r = data[idx];
      const g = data[idx + 1];
      const b = data[idx + 2];

      const dist = Math.sqrt(
        (r - targetRgb.r) ** 2 +
        (g - targetRgb.g) ** 2 +
        (b - targetRgb.b) ** 2
      );

      if (dist < tolThreshold + feather * 8) {
        isCandidateBg[i] = dist < tolThreshold ? 1 : 2; // 1 = solid bg, 2 = feather edge
      }
    }

    // Step 2: Flood-fill from outer borders to protect enclosed interior regions
    const isConnectedBg = new Uint8Array(totalPixels);

    if (maskMode === 'flood_fill_outer') {
      const queue = new Int32Array(totalPixels);
      let head = 0;
      let tail = 0;

      // Seed from all 4 outer boundaries
      // Top & Bottom edges
      for (let cx = 0; cx < intW; cx++) {
        const topIdx = cx;
        if (isCandidateBg[topIdx] > 0 && !isConnectedBg[topIdx]) {
          isConnectedBg[topIdx] = isCandidateBg[topIdx];
          queue[tail++] = topIdx;
        }
        const btmIdx = (intH - 1) * intW + cx;
        if (isCandidateBg[btmIdx] > 0 && !isConnectedBg[btmIdx]) {
          isConnectedBg[btmIdx] = isCandidateBg[btmIdx];
          queue[tail++] = btmIdx;
        }
      }
      // Left & Right edges
      for (let cy = 0; cy < intH; cy++) {
        const leftIdx = cy * intW;
        if (isCandidateBg[leftIdx] > 0 && !isConnectedBg[leftIdx]) {
          isConnectedBg[leftIdx] = isCandidateBg[leftIdx];
          queue[tail++] = leftIdx;
        }
        const rightIdx = cy * intW + (intW - 1);
        if (isCandidateBg[rightIdx] > 0 && !isConnectedBg[rightIdx]) {
          isConnectedBg[rightIdx] = isCandidateBg[rightIdx];
          queue[tail++] = rightIdx;
        }
      }

      // 4-way BFS flood fill traversal
      while (head < tail) {
        const curr = queue[head++];
        const cx = curr % intW;
        const cy = Math.floor(curr / intW);

        const neighbors = [
          cx > 0 ? curr - 1 : -1,
          cx < intW - 1 ? curr + 1 : -1,
          cy > 0 ? curr - intW : -1,
          cy < intH - 1 ? curr + intW : -1,
        ];

        for (let n = 0; n < 4; n++) {
          const nIdx = neighbors[n];
          if (nIdx >= 0 && !isConnectedBg[nIdx] && isCandidateBg[nIdx] > 0) {
            isConnectedBg[nIdx] = isCandidateBg[nIdx];
            queue[tail++] = nIdx;
          }
        }
      }
    }

    // Step 3: Apply alpha and optional De-Spill
    for (let i = 0; i < totalPixels; i++) {
      const idx = i * 4;
      const bgStatus = maskMode === 'flood_fill_outer' ? isConnectedBg[i] : isCandidateBg[i];

      if (bgStatus === 1) {
        data[idx + 3] = 0; // 100% transparent
      } else if (bgStatus === 2) {
        const r = data[idx];
        const g = data[idx + 1];
        const b = data[idx + 2];
        const dist = Math.sqrt(
          (r - targetRgb.r) ** 2 +
          (g - targetRgb.g) ** 2 +
          (b - targetRgb.b) ** 2
        );
        const alphaFactor = Math.max(0, Math.min(1, (dist - tolThreshold) / (feather * 8)));
        data[idx + 3] = Math.round(data[idx + 3] * alphaFactor);
      }

      // De-Spill green bounce on non-transparent hair edges
      if (enableDeSpill && isGreenKey && data[idx + 3] > 0) {
        const r = data[idx];
        const g = data[idx + 1];
        const b = data[idx + 2];
        const maxOther = Math.max(r, b);
        if (g > maxOther) {
          data[idx + 1] = Math.round(maxOther * 0.85 + g * 0.15); // Suppress green spill
        }
      }
    }

    // Step 4: Despeckle / Isolated Island Removal (Xóa các đốm trắng/hạt rác rải rác không liền mạch)
    if (despeckleSize > 0 || keepLargestIslandOnly || whiteSpeckleSensitivity > 0) {
      const visited = new Uint8Array(totalPixels);
      const compQueue = new Int32Array(totalPixels);
      const allComponents: { pixels: number[]; isWhiteDominated: boolean }[] = [];
      let maxComponentSize = 0;

      for (let i = 0; i < totalPixels; i++) {
        if (data[i * 4 + 3] > 10 && !visited[i]) {
          let qHead = 0;
          let qTail = 0;
          compQueue[qTail++] = i;
          visited[i] = 1;

          const currentPixels: number[] = [];
          let whiteCount = 0;

          while (qHead < qTail) {
            const curr = compQueue[qHead++];
            currentPixels.push(curr);

            const pIdx = curr * 4;
            const r = data[pIdx];
            const g = data[pIdx + 1];
            const b = data[pIdx + 2];
            if (r > 195 && g > 195 && b > 195) {
              whiteCount++;
            }

            const cx = curr % intW;
            const cy = Math.floor(curr / intW);

            // 8-way connectivity
            const neighbors = [
              cx > 0 ? curr - 1 : -1,
              cx < intW - 1 ? curr + 1 : -1,
              cy > 0 ? curr - intW : -1,
              cy < intH - 1 ? curr + intW : -1,
              cx > 0 && cy > 0 ? curr - intW - 1 : -1,
              cx < intW - 1 && cy > 0 ? curr - intW + 1 : -1,
              cx > 0 && cy < intH - 1 ? curr + intW - 1 : -1,
              cx < intW - 1 && cy < intH - 1 ? curr + intW + 1 : -1,
            ];

            for (let n = 0; n < 8; n++) {
              const nIdx = neighbors[n];
              if (nIdx >= 0 && !visited[nIdx] && data[nIdx * 4 + 3] > 10) {
                visited[nIdx] = 1;
                compQueue[qTail++] = nIdx;
              }
            }
          }

          if (currentPixels.length > maxComponentSize) {
            maxComponentSize = currentPixels.length;
          }

          allComponents.push({
            pixels: currentPixels,
            isWhiteDominated: whiteCount / currentPixels.length > 0.5,
          });
        }
      }

      // Erase small or isolated stray components
      for (const comp of allComponents) {
        const isSmallSpeckle = despeckleSize > 0 && comp.pixels.length <= despeckleSize;
        const isStrayWhite = whiteSpeckleSensitivity > 0 && comp.isWhiteDominated && comp.pixels.length <= despeckleSize * 2.5;
        const isNotMainObject = keepLargestIslandOnly && comp.pixels.length < maxComponentSize * 0.35;

        if (isSmallSpeckle || isStrayWhite || isNotMainObject) {
          for (let p = 0; p < comp.pixels.length; p++) {
            data[comp.pixels[p] * 4 + 3] = 0; // Erase stray speckle to 100% transparent!
          }
        }
      }
    }

    offCtx.putImageData(imgData, 0, 0);
    return offCanvas.toDataURL('image/png');
  };

  /**
   * Main Action: Slices all cells in the grid, cleans background, and auto-assembles into 3D Multi-Angle structure
   */
  const handleAutoSliceAndAssemble3D = () => {
    const img = loadedImageRef.current;
    if (!img) return;

    setIsProcessing(true);

    setTimeout(() => {
      const rows = currentCategory.rows;
      const cols = currentCategory.cols;

      const usableW = img.width - gridMarginX * 2 - (cols - 1) * gridGapX;
      const usableH = img.height - gridMarginY * 2 - (rows - 1) * gridGapY;
      const cellW = usableW / cols;
      const cellH = usableH / rows;

      const newResults = new Map<string, string>();
      const updatedAssembly: Character2DAssembly = JSON.parse(JSON.stringify(currentAssembly));

      // Slice each cell
      currentCategory.cells.forEach((cell) => {
        const rect = getCellRect(cell.row, cell.col, img);
        const slicedPng = sliceAndRemoveBgCell(img, rect.x, rect.y, rect.w, rect.h);
        const key = `${cell.row}_${cell.col}`;
        newResults.set(key, slicedPng);

        // Preload Image object for canvas transparent grid rendering
        const cellImg = new Image();
        cellImg.onload = () => {
          drawGridOverlay();
        };
        cellImg.src = slicedPng;
        slicedImgElementsRef.current.set(key, cellImg);

        // Assign to Character Assembly if angle and slot are defined
        if (cell.angle && cell.partSlot) {
          const slot = cell.partSlot;
          const existingPart = updatedAssembly.parts[slot];
          if (!existingPart) {
            updatedAssembly.parts[slot] = {
              path: slicedPng,
              offset: [0, 0],
              scale: [1, 1],
              rotation: 0,
              pivot: [0.5, 0.5],
              flipX: false,
              flipY: false,
              z_index: 5,
              opacity: 1,
              angles: {},
            };
          }

          const targetPart = updatedAssembly.parts[slot]!;
          if (!targetPart.angles) {
            targetPart.angles = {};
          }

          targetPart.angles[cell.angle] = slicedPng;

          // If front angle, set as base path too
          if (cell.angle === 'front') {
            targetPart.path = slicedPng;
          }

          // If mirror angle is defined, assign to opposite side
          if (cell.mirrorAngle) {
            targetPart.angles[cell.mirrorAngle] = slicedPng;
          }
        }
      });

      setSlicedResults(newResults);
      onApplyAssembly(updatedAssembly);

      // Update 3D WebGL Engine
      if (threeEngineRef.current) {
        threeEngineRef.current.setAssembly(updatedAssembly);
      }

      setHasExplicitlySliced(true);
      setPreviewDisplayMode('transparent');
      setIsProcessing(false);
      setAssemblySuccess(true);
      setTimeout(() => setAssemblySuccess(false), 3000);
    }, 100);
  };

  const getCanvasCoords = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = imageCanvasRef.current;
    const img = loadedImageRef.current;
    if (!canvas || !img) return null;

    const rect = canvas.getBoundingClientRect();
    const scaleX = img.width / rect.width;
    const scaleY = img.height / rect.height;

    return {
      x: (e.clientX - rect.left) * scaleX,
      y: (e.clientY - rect.top) * scaleY,
    };
  };

  const handleCanvasMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const coords = getCanvasCoords(e);
    const img = loadedImageRef.current;
    if (!coords || !img) return;

    // 1. If mouse is on a grid divider line, start dragging that divider
    if (hoveredDivider) {
      setActiveDividerDrag({
        type: hoveredDivider.type,
        index: hoveredDivider.index,
      });
      return;
    }

    // 2. Otherwise, check if user clicked on any cell to select it
    const rows = currentCategory.rows;
    const cols = currentCategory.cols;

    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        const rect = getCellRect(r, c, img);
        if (coords.x >= rect.x && coords.x <= rect.x + rect.w && coords.y >= rect.y && coords.y <= rect.y + rect.h) {
          const found = currentCategory.cells.find((cell) => cell.row === r && cell.col === c);
          if (found) {
            setSelectedCell(found);
          }
          return;
        }
      }
    }
  };

  const handleCanvasDoubleClick = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const coords = getCanvasCoords(e);
    const img = loadedImageRef.current;
    if (!coords || !img) return;

    const rows = currentCategory.rows;
    const cols = currentCategory.cols;

    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        const rect = getCellRect(r, c, img);
        if (coords.x >= rect.x && coords.x <= rect.x + rect.w && coords.y >= rect.y && coords.y <= rect.y + rect.h) {
          const found = currentCategory.cells.find((cell) => cell.row === r && cell.col === c);
          if (found) {
            setSelectedCell(found);
            handleOpenCellPixelEditor(found);
          }
          return;
        }
      }
    }
  };

  const handleCanvasMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const coords = getCanvasCoords(e);
    const img = loadedImageRef.current;
    const canvas = imageCanvasRef.current;
    if (!coords || !img || !canvas) return;

    const rows = currentCategory.rows;
    const cols = currentCategory.cols;

    // A. If currently dragging a grid divider line
    if (activeDividerDrag) {
      if (activeDividerDrag.type === 'col') {
        const k = activeDividerDrag.index;
        if (k >= 1 && k < cols && colDividers.length === cols + 1) {
          const minX = colDividers[k - 1] + 25;
          const maxX = colDividers[k + 1] - 25;
          const clampedX = Math.max(minX, Math.min(maxX, coords.x));

          setColDividers((prev) => {
            const next = [...prev];
            next[k] = Math.round(clampedX);
            return next;
          });
        }
      } else if (activeDividerDrag.type === 'row') {
        const k = activeDividerDrag.index;
        if (k >= 1 && k < rows && rowDividers.length === rows + 1) {
          const minY = rowDividers[k - 1] + 25;
          const maxY = rowDividers[k + 1] - 25;
          const clampedY = Math.max(minY, Math.min(maxY, coords.y));

          setRowDividers((prev) => {
            const next = [...prev];
            next[k] = Math.round(clampedY);
            return next;
          });
        }
      }
      return;
    }

    // B. Detect hover near internal divider lines (within 10px)
    if (colDividers.length === cols + 1 && rowDividers.length === rows + 1) {
      const x0 = colDividers[0];
      const xLast = colDividers[cols];
      const y0 = rowDividers[0];
      const yLast = rowDividers[rows];

      // 1. Check internal vertical lines (X1 .. X_cols-1)
      for (let c = 1; c < cols; c++) {
        const lineX = colDividers[c];
        if (Math.abs(coords.x - lineX) <= 10 && coords.y >= y0 - 10 && coords.y <= yLast + 10) {
          canvas.style.cursor = 'col-resize';
          setHoveredDivider({ type: 'col', index: c });
          return;
        }
      }

      // 2. Check internal horizontal lines (Y1 .. Y_rows-1)
      for (let r = 1; r < rows; r++) {
        const lineY = rowDividers[r];
        if (Math.abs(coords.y - lineY) <= 10 && coords.x >= x0 - 10 && coords.x <= xLast + 10) {
          canvas.style.cursor = 'row-resize';
          setHoveredDivider({ type: 'row', index: r });
          return;
        }
      }
    }

    // Default pointer cursor
    canvas.style.cursor = 'pointer';
    if (hoveredDivider) {
      setHoveredDivider(null);
    }
  };

  const handleCanvasMouseUp = () => {
    if (activeDividerDrag) {
      setActiveDividerDrag(null);
    }
  };

  /**
   * Helper to manually adjust divider of selected cell's row or column
   */
  const handleAdjustRowHeight = (deltaH: number) => {
    if (!selectedCell || rowDividers.length === 0) return;
    const r = selectedCell.row;
    const rows = currentCategory.rows;

    // Adjust bottom divider of this row
    if (r + 1 < rows) {
      const k = r + 1;
      const minY = rowDividers[k - 1] + 25;
      const maxY = rowDividers[k + 1] - 25;
      const nextY = Math.max(minY, Math.min(maxY, rowDividers[k] + deltaH));
      setRowDividers((prev) => {
        const next = [...prev];
        next[k] = Math.round(nextY);
        return next;
      });
    } else if (r > 0) {
      // If it's the last row (e.g. Row 3 long hair), move the upper divider upwards to make row 3 taller!
      const k = r;
      const minY = rowDividers[k - 1] + 25;
      const maxY = rowDividers[k + 1] - 25;
      const nextY = Math.max(minY, Math.min(maxY, rowDividers[k] - deltaH));
      setRowDividers((prev) => {
        const next = [...prev];
        next[k] = Math.round(nextY);
        return next;
      });
    }
  };

  const handleAdjustColWidth = (deltaW: number) => {
    if (!selectedCell || colDividers.length === 0) return;
    const c = selectedCell.col;
    const cols = currentCategory.cols;

    if (c + 1 < cols) {
      const k = c + 1;
      const minX = colDividers[k - 1] + 25;
      const maxX = colDividers[k + 1] - 25;
      const nextX = Math.max(minX, Math.min(maxX, colDividers[k] + deltaW));
      setColDividers((prev) => {
        const next = [...prev];
        next[k] = Math.round(nextX);
        return next;
      });
    }
  };

  const handleResetAllDividers = () => {
    if (loadedImageRef.current) {
      initDividers(loadedImageRef.current);
    }
  };

  /**
   * Opens the full Pixel Eraser & Zoom Cleanup Modal for a specific cell
   */
  const handleOpenCellPixelEditor = (cell: GridCellDefinition) => {
    const cellKey = `${cell.row}_${cell.col}`;
    let dataUrl = slicedResults.get(cellKey);

    if (!dataUrl && loadedImageRef.current) {
      const rect = getCellRect(cell.row, cell.col, loadedImageRef.current);
      dataUrl = sliceAndRemoveBgCell(loadedImageRef.current, rect.x, rect.y, rect.w, rect.h);
    }

    if (dataUrl) {
      setEditingCellData({ cell, dataUrl });
    }
  };

  /**
   * Saves updated sliced PNG from Pixel Eraser Modal and syncs directly with 3D Billboard Turntable Engine
   */
  const handleSaveCellPixelEdit = (newDataUrl: string) => {
    if (!editingCellData) return;
    const { cell } = editingCellData;
    const cellKey = `${cell.row}_${cell.col}`;

    // 1. Update slicedResults Map
    setSlicedResults((prev) => {
      const next = new Map(prev);
      next.set(cellKey, newDataUrl);
      return next;
    });

    // 2. Update 3D Character Assembly & Three Engine
    if (cell.partSlot) {
      const updatedAssembly: Character2DAssembly = JSON.parse(JSON.stringify(currentAssembly));
      const slot = cell.partSlot;
      if (!updatedAssembly.parts[slot]) {
        updatedAssembly.parts[slot] = {
          path: newDataUrl,
          offset: [0, 0],
          scale: [1, 1],
          rotation: 0,
          pivot: [0.5, 0.5],
          flipX: false,
          flipY: false,
          z_index: 5,
          opacity: 1,
          angles: {},
        };
      }

      const targetPart = updatedAssembly.parts[slot]!;
      if (!targetPart.angles) targetPart.angles = {};
      if (cell.angle) {
        targetPart.angles[cell.angle] = newDataUrl;

        if (cell.angle === 'front') {
          targetPart.path = newDataUrl;
        }
      }
      if (cell.mirrorAngle) {
        targetPart.angles[cell.mirrorAngle] = newDataUrl;
      }

      onApplyAssembly(updatedAssembly);

      if (threeEngineRef.current) {
        threeEngineRef.current.setAssembly(updatedAssembly);
      }
    }

    // 3. Update cached Image element for grid canvas
    const imgEl = new Image();
    imgEl.onload = () => {
      slicedImgElementsRef.current.set(cellKey, imgEl);
      drawGridOverlay();
    };
    imgEl.src = newDataUrl;

    setEditingCellData(null);
  };

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '380px 1fr 420px', gap: 14, height: '100%', overflow: 'hidden' }}>
      {/* ─── LEFT COLUMN: Category Selector & Slicing Controls ─────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10, overflowY: 'auto', paddingRight: 4 }}>
        {/* Category Selector */}
        <div>
          <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 4, fontWeight: 700 }}>
            1. Chọn Loại Bảng Cần Cắt Ghép:
          </label>
          <select
            value={selectedCatId}
            onChange={(e) => {
              setSelectedCatId(e.target.value);
              setSelectedCell(null);
            }}
            style={{
              width: '100%',
              padding: '7px 8px',
              fontSize: 11,
              background: '#0f172a',
              color: '#38bdf8',
              border: '1px solid #0284c7',
              borderRadius: 6,
              fontWeight: 700,
            }}
          >
            {GRID_CATEGORY_DEFINITIONS.map((cat) => (
              <option key={cat.id} value={cat.id}>
                {cat.label}
              </option>
            ))}
          </select>
          <div style={{ fontSize: 10, color: '#64748b', marginTop: 4 }}>
            {currentCategory.description}
          </div>
        </div>

        {/* Upload or Load Demo */}
        <div style={{ background: 'rgba(15, 23, 42, 0.7)', padding: 10, borderRadius: 6, border: '1px solid rgba(255,255,255,0.08)', display: 'flex', flexDirection: 'column', gap: 8 }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: '#e2e8f0', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <Upload size={13} /> 2. Nhập Ảnh Sprite Sheet AI:
            </span>
            {userUploadedImageUrl && (
              <span style={{ fontSize: 9, color: '#38bdf8', background: 'rgba(56, 189, 248, 0.15)', padding: '2px 5px', borderRadius: 3 }}>
                ✓ Đã tải ảnh riêng
              </span>
            )}
          </div>

          <div style={{ display: 'flex', gap: 6 }}>
            <input type="file" ref={fileInputRef} onChange={handleFileUpload} accept="image/*" style={{ display: 'none' }} />
            <button
              onClick={() => fileInputRef.current?.click()}
              style={{
                flex: 1,
                padding: '7px 10px',
                fontSize: 11,
                fontWeight: 600,
                borderRadius: 5,
                background: userUploadedImageUrl ? '#0284c7' : '#0284c7',
                color: '#fff',
                border: 'none',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 5,
              }}
            >
              <Upload size={12} /> {userUploadedImageUrl ? 'Tải Ảnh Khác' : 'Tải Ảnh Lên'}
            </button>

            <button
              onClick={() => {
                setUserUploadedImageUrl(null);
                const demoUrl = generateDemoGridSpriteSheet(selectedCatId, keyColorType === 'chroma_green' ? 'chroma_green' : 'pure_white');
                loadImage(demoUrl, false);
              }}
              style={{
                padding: '7px 10px',
                fontSize: 11,
                borderRadius: 5,
                background: 'rgba(255,255,255,0.06)',
                color: '#94a3b8',
                border: '1px solid rgba(255,255,255,0.1)',
                cursor: 'pointer',
              }}
              title="Dùng lại ảnh mẫu sprite sheet"
            >
              Ảnh Mẫu
            </button>
          </div>
        </div>

        {/* Chroma Key & Despeckle Cleanup Section */}
        <div style={{ background: 'rgba(34, 197, 94, 0.06)', padding: 10, borderRadius: 6, border: '1px solid rgba(34, 197, 94, 0.2)', display: 'flex', flexDirection: 'column', gap: 8 }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: '#4ade80', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <Scissors size={13} /> 3. Xử Lý Tách Nền & Khử Đốm Rác:
            </span>
          </div>

          {/* Sub-tab Switcher */}
          <div style={{ display: 'flex', gap: 4, background: 'rgba(0,0,0,0.35)', padding: 3, borderRadius: 5, border: '1px solid rgba(255,255,255,0.06)' }}>
            <button
              onClick={() => setBgCleanupSubTab('chroma')}
              style={{
                flex: 1,
                padding: '5px 4px',
                fontSize: 10,
                fontWeight: 700,
                borderRadius: 4,
                border: 'none',
                background: bgCleanupSubTab === 'chroma' ? '#0284c7' : 'transparent',
                color: bgCleanupSubTab === 'chroma' ? '#ffffff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 4,
              }}
            >
              <Scissors size={11} /> 1. Tách Nền Màu
            </button>

            <button
              onClick={() => setBgCleanupSubTab('despeckle')}
              style={{
                flex: 1,
                padding: '5px 4px',
                fontSize: 10,
                fontWeight: 700,
                borderRadius: 4,
                border: 'none',
                background: bgCleanupSubTab === 'despeckle' ? '#0284c7' : 'transparent',
                color: bgCleanupSubTab === 'despeckle' ? '#ffffff' : '#94a3b8',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 4,
              }}
            >
              <Sparkles size={11} /> 2. 🧹 Khử Đốm Trắng/Rác
            </button>
          </div>

          {bgCleanupSubTab === 'chroma' ? (
            /* Sub-tab 1: Chroma Key controls */
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ display: 'flex', gap: 5, alignItems: 'center' }}>
                <button
                  onClick={() => {
                    setKeyColorType('chroma_green');
                    setKeyColorHex('#00ff00');
                    if (hasExplicitlySliced) setTimeout(() => handleAutoSliceAndAssemble3D(), 50);
                  }}
                  style={{
                    flex: 1,
                    padding: '5px',
                    fontSize: 10,
                    fontWeight: 600,
                    borderRadius: 4,
                    border: keyColorType === 'chroma_green' ? '1px solid #22c55e' : '1px solid rgba(255,255,255,0.1)',
                    background: keyColorType === 'chroma_green' ? 'rgba(34, 197, 94, 0.2)' : 'transparent',
                    color: keyColorType === 'chroma_green' ? '#4ade80' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  🟢 Xanh Chroma
                </button>

                <button
                  onClick={() => {
                    setKeyColorType('pure_white');
                    setKeyColorHex('#ffffff');
                    if (hasExplicitlySliced) setTimeout(() => handleAutoSliceAndAssemble3D(), 50);
                  }}
                  style={{
                    flex: 1,
                    padding: '5px',
                    fontSize: 10,
                    fontWeight: 600,
                    borderRadius: 4,
                    border: keyColorType === 'pure_white' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                    background: keyColorType === 'pure_white' ? 'rgba(56, 189, 248, 0.2)' : 'transparent',
                    color: keyColorType === 'pure_white' ? '#38bdf8' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  ⚪ Nền Trắng
                </button>

                {/* Custom Color Eyedropper */}
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 4,
                    background: 'rgba(0,0,0,0.4)',
                    padding: '3px 6px',
                    borderRadius: 4,
                    border: keyColorType === 'custom' ? '1px solid #a855f7' : '1px solid rgba(255,255,255,0.1)',
                  }}
                  title="Chọn màu nền tùy chỉnh"
                >
                  <input
                    type="color"
                    value={keyColorHex}
                    onChange={(e) => {
                      setKeyColorHex(e.target.value);
                      setKeyColorType('custom');
                      if (hasExplicitlySliced) setTimeout(() => handleAutoSliceAndAssemble3D(), 50);
                    }}
                    style={{ width: 20, height: 20, padding: 0, border: 'none', background: 'transparent', cursor: 'pointer' }}
                  />
                </div>
              </div>

              {/* Mask Mode Selector */}
              <div style={{ display: 'flex', gap: 4, background: 'rgba(0,0,0,0.3)', padding: 3, borderRadius: 5, border: '1px solid rgba(255,255,255,0.06)' }}>
                <button
                  onClick={() => {
                    setMaskMode('flood_fill_outer');
                    if (hasExplicitlySliced) setTimeout(() => handleAutoSliceAndAssemble3D(), 50);
                  }}
                  style={{
                    flex: 1,
                    padding: '5px 6px',
                    fontSize: 9.5,
                    fontWeight: 700,
                    borderRadius: 4,
                    border: 'none',
                    background: maskMode === 'flood_fill_outer' ? '#0284c7' : 'transparent',
                    color: maskMode === 'flood_fill_outer' ? '#ffffff' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                  title="Chỉ xóa nền từ mép viền ngoài vào, chống thủng lỗ bên trong tóc/cơ thể"
                >
                  🛡️ Loang Viền (Chống Thủng Lõi)
                </button>
                <button
                  onClick={() => {
                    setMaskMode('global_color');
                    if (hasExplicitlySliced) setTimeout(() => handleAutoSliceAndAssemble3D(), 50);
                  }}
                  style={{
                    flex: 1,
                    padding: '5px 6px',
                    fontSize: 9.5,
                    fontWeight: 600,
                    borderRadius: 4,
                    border: 'none',
                    background: maskMode === 'global_color' ? '#0284c7' : 'transparent',
                    color: maskMode === 'global_color' ? '#ffffff' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                  title="Xóa mọi pixel trùng màu trên toàn bộ ảnh"
                >
                  🌐 Toàn Cục
                </button>
              </div>

              <div style={{ fontSize: 10, color: '#94a3b8' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
                  <span>Độ nhạy tách nền:</span>
                  <span style={{ color: '#4ade80', fontWeight: 700 }}>{tolerance}%</span>
                </div>
                <input
                  type="range"
                  min="5"
                  max="95"
                  value={tolerance}
                  onChange={(e) => setTolerance(parseInt(e.target.value))}
                  onMouseUp={() => {
                    if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                  }}
                  onTouchEnd={() => {
                    if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                  }}
                  style={{ width: '100%', cursor: 'pointer' }}
                />
              </div>

              <div style={{ fontSize: 10, color: '#94a3b8' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
                  <span>Làm mềm viền (Feather):</span>
                  <span style={{ color: '#4ade80', fontWeight: 700 }}>{feather}px</span>
                </div>
                <input
                  type="range"
                  min="0"
                  max="20"
                  value={feather}
                  onChange={(e) => setFeather(parseInt(e.target.value))}
                  onMouseUp={() => {
                    if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                  }}
                  onTouchEnd={() => {
                    if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                  }}
                  style={{ width: '100%', cursor: 'pointer' }}
                />
              </div>

              {/* De-Spill Green Suppression Toggle */}
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 10, color: '#94a3b8', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={enableDeSpill}
                  onChange={(e) => {
                    setEnableDeSpill(e.target.checked);
                    if (hasExplicitlySliced) setTimeout(() => handleAutoSliceAndAssemble3D(), 50);
                  }}
                  style={{ cursor: 'pointer' }}
                />
                <span>Khử ánh phản quang viền tóc (De-Spill)</span>
              </label>
            </div>
          ) : (
            /* Sub-tab 2: Despeckle / Isolated Speckle Filter */
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ fontSize: 10, color: '#94a3b8' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
                  <span>🧹 Kích thước đốm rác cần xóa:</span>
                  <span style={{ color: '#38bdf8', fontWeight: 700 }}>{despeckleSize} px</span>
                </div>
                <input
                  type="range"
                  min="0"
                  max="150"
                  value={despeckleSize}
                  onChange={(e) => setDespeckleSize(parseInt(e.target.value))}
                  onMouseUp={() => {
                    if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                  }}
                  onTouchEnd={() => {
                    if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                  }}
                  style={{ width: '100%', cursor: 'pointer' }}
                />
                <div style={{ fontSize: 9, color: '#64748b', marginTop: 1 }}>
                  Tự động xóa mọi đốm rác/hạt cô lập có diện tích ≤ {despeckleSize}px không nối liền thân tóc.
                </div>
              </div>

              {/* Quick Preset Buttons */}
              <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                <button
                  onClick={() => {
                    setDespeckleSize(0);
                    if (hasExplicitlySliced) setTimeout(() => handleAutoSliceAndAssemble3D(), 50);
                  }}
                  style={{ flex: 1, padding: '4px', fontSize: 9, borderRadius: 3, background: despeckleSize === 0 ? '#0284c7' : 'rgba(255,255,255,0.06)', color: '#fff', border: 'none', cursor: 'pointer', fontWeight: 600 }}
                >
                  Tắt (0)
                </button>
                <button
                  onClick={() => {
                    setDespeckleSize(15);
                    if (hasExplicitlySliced) setTimeout(() => handleAutoSliceAndAssemble3D(), 50);
                  }}
                  style={{ flex: 1, padding: '4px', fontSize: 9, borderRadius: 3, background: despeckleSize === 15 ? '#0284c7' : 'rgba(255,255,255,0.06)', color: '#fff', border: 'none', cursor: 'pointer', fontWeight: 600 }}
                >
                  Nhẹ (15)
                </button>
                <button
                  onClick={() => {
                    setDespeckleSize(40);
                    if (hasExplicitlySliced) setTimeout(() => handleAutoSliceAndAssemble3D(), 50);
                  }}
                  style={{ flex: 1, padding: '4px', fontSize: 9, borderRadius: 3, background: despeckleSize === 40 ? '#0284c7' : 'rgba(255,255,255,0.06)', color: '#fff', border: 'none', cursor: 'pointer', fontWeight: 600 }}
                >
                  Vừa (40)
                </button>
                <button
                  onClick={() => {
                    setDespeckleSize(80);
                    if (hasExplicitlySliced) setTimeout(() => handleAutoSliceAndAssemble3D(), 50);
                  }}
                  style={{ flex: 1, padding: '4px', fontSize: 9, borderRadius: 3, background: despeckleSize === 80 ? '#0284c7' : 'rgba(255,255,255,0.06)', color: '#fff', border: 'none', cursor: 'pointer', fontWeight: 600 }}
                >
                  Mạnh (80)
                </button>
              </div>

              {/* White Speckle Sensitivity Slider */}
              <div style={{ fontSize: 10, color: '#94a3b8', marginTop: 2 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
                  <span>⚪ Độ nhạy khử đốm trắng rải rác:</span>
                  <span style={{ color: '#38bdf8', fontWeight: 700 }}>{whiteSpeckleSensitivity}%</span>
                </div>
                <input
                  type="range"
                  min="0"
                  max="100"
                  value={whiteSpeckleSensitivity}
                  onChange={(e) => setWhiteSpeckleSensitivity(parseInt(e.target.value))}
                  onMouseUp={() => {
                    if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                  }}
                  onTouchEnd={() => {
                    if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                  }}
                  style={{ width: '100%', cursor: 'pointer' }}
                />
              </div>

              {/* Keep Largest Island Only Toggle */}
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 10, color: '#94a3b8', cursor: 'pointer', marginTop: 2 }}>
                <input
                  type="checkbox"
                  checked={keepLargestIslandOnly}
                  onChange={(e) => {
                    setKeepLargestIslandOnly(e.target.checked);
                    if (hasExplicitlySliced) setTimeout(() => handleAutoSliceAndAssemble3D(), 50);
                  }}
                  style={{ cursor: 'pointer' }}
                />
                <span>🛡️ Chỉ giữ lại cụm tóc chính (Xóa 100% đốm bụi bay)</span>
              </label>
            </div>
          )}
        </div>

        {/* Fine-tune Grid Sliders */}
        <div style={{ background: 'rgba(15, 23, 42, 0.7)', padding: 10, borderRadius: 6, border: '1px solid rgba(255,255,255,0.08)', display: 'flex', flexDirection: 'column', gap: 6 }}>
          <div style={{ fontSize: 11, fontWeight: 600, color: '#94a3b8', display: 'flex', alignItems: 'center', gap: 5 }}>
            <Sliders size={12} /> Căn Chỉnh Khung Lưới (Margin / Gap):
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
            <div>
              <label style={{ fontSize: 9, color: '#64748b' }}>Lề X (Margin): {gridMarginX}px</label>
              <input
                type="range"
                min="0"
                max="40"
                value={gridMarginX}
                onChange={(e) => setGridMarginX(parseInt(e.target.value))}
                onMouseUp={() => {
                  if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                }}
                onTouchEnd={() => {
                  if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                }}
                style={{ width: '100%' }}
              />
            </div>
            <div>
              <label style={{ fontSize: 9, color: '#64748b' }}>Lề Y (Margin): {gridMarginY}px</label>
              <input
                type="range"
                min="0"
                max="40"
                value={gridMarginY}
                onChange={(e) => setGridMarginY(parseInt(e.target.value))}
                onMouseUp={() => {
                  if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                }}
                onTouchEnd={() => {
                  if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                }}
                style={{ width: '100%' }}
              />
            </div>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
            <div>
              <label style={{ fontSize: 9, color: '#64748b' }}>Khoảng cách X (Gap): {gridGapX}px</label>
              <input
                type="range"
                min="0"
                max="30"
                value={gridGapX}
                onChange={(e) => setGridGapX(parseInt(e.target.value))}
                onMouseUp={() => {
                  if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                }}
                onTouchEnd={() => {
                  if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                }}
                style={{ width: '100%' }}
              />
            </div>
            <div>
              <label style={{ fontSize: 9, color: '#64748b' }}>Khoảng cách Y (Gap): {gridGapY}px</label>
              <input
                type="range"
                min="0"
                max="30"
                value={gridGapY}
                onChange={(e) => setGridGapY(parseInt(e.target.value))}
                onMouseUp={() => {
                  if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                }}
                onTouchEnd={() => {
                  if (hasExplicitlySliced) handleAutoSliceAndAssemble3D();
                }}
                style={{ width: '100%' }}
              />
            </div>
          </div>
        </div>

        {/* Big Action Button */}
        <button
          onClick={handleAutoSliceAndAssemble3D}
          disabled={isProcessing}
          style={{
            marginTop: 'auto',
            padding: '12px 14px',
            fontSize: 12,
            fontWeight: 800,
            borderRadius: 8,
            background: assemblySuccess ? '#22c55e' : 'linear-gradient(135deg, #0284c7, #0369a1, #38bdf8)',
            color: '#fff',
            border: 'none',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 8,
            boxShadow: '0 6px 20px rgba(2, 132, 199, 0.4)',
            transition: 'all 0.2s',
          }}
        >
          {isProcessing ? <RefreshCw size={15} className="animate-spin" /> : assemblySuccess ? <Check size={15} /> : <Sparkles size={15} />}
          {isProcessing ? 'Đang Tách Nền & Lắp Ghép...' : assemblySuccess ? 'Đã Ghép Xong Bộ Linh Kiện 3D!' : '⚡ TÁCH NỀN & GHÉP BỘ LINH KIỆN 3D'}
        </button>
      </div>

      {/* ─── MIDDLE COLUMN: Visual Grid Overlay Canvas ──────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, background: 'rgba(15, 23, 42, 0.7)', padding: 12, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', overflow: 'hidden' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Layers size={14} /> KHUNG LƯỚI CẮT ({currentCategory.rows} DÃY × {currentCategory.cols} CỘT)
          </div>

          <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
            <button
              onClick={() => {
                setPreviewDisplayMode('transparent');
              }}
              style={{
                padding: '4px 9px',
                fontSize: 10,
                fontWeight: 700,
                borderRadius: 5,
                background: previewDisplayMode === 'transparent' ? '#0284c7' : 'rgba(255,255,255,0.05)',
                color: previewDisplayMode === 'transparent' ? '#ffffff' : '#94a3b8',
                border: previewDisplayMode === 'transparent' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                boxShadow: previewDisplayMode === 'transparent' ? '0 0 10px rgba(56, 189, 248, 0.3)' : 'none',
              }}
            >
              <Sparkles size={11} /> ✨ Đã Tách Nền (Caro)
            </button>

            <button
              onClick={() => {
                setPreviewDisplayMode('original');
              }}
              style={{
                padding: '4px 9px',
                fontSize: 10,
                fontWeight: 600,
                borderRadius: 5,
                background: previewDisplayMode === 'original' ? '#0284c7' : 'rgba(255,255,255,0.05)',
                color: previewDisplayMode === 'original' ? '#ffffff' : '#94a3b8',
                border: previewDisplayMode === 'original' ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                boxShadow: previewDisplayMode === 'original' ? '0 0 10px rgba(56, 189, 248, 0.3)' : 'none',
              }}
            >
              <Eye size={11} /> 👁️ Ảnh Gốc
            </button>
          </div>
        </div>

        {/* Canvas Display */}
        <div
          style={{
            flex: 1,
            position: 'relative',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            overflow: 'auto',
            background: '#090d16',
            borderRadius: 6,
            border: '1px solid rgba(255,255,255,0.1)',
            padding: 8,
          }}
        >
          <canvas
            ref={imageCanvasRef}
            onMouseDown={handleCanvasMouseDown}
            onDoubleClick={handleCanvasDoubleClick}
            onMouseMove={handleCanvasMouseMove}
            onMouseUp={handleCanvasMouseUp}
            onMouseLeave={handleCanvasMouseUp}
            style={{
              maxWidth: '100%',
              maxHeight: '100%',
              objectFit: 'contain',
              boxShadow: '0 4px 20px rgba(0,0,0,0.6)',
            }}
          />
        </div>

        {/* Selected Cell Custom Sizing & Adjustment Banner */}
        {selectedCell ? (
          <div style={{ background: '#0b1329', padding: '8px 12px', borderRadius: 6, border: '1px solid #0284c7', display: 'flex', flexDirection: 'column', gap: 8 }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
              <div>
                <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8' }}>
                  Đang chọn: [{selectedCell.row + 1}, {selectedCell.col + 1}] - {selectedCell.label}
                  <span style={{ fontSize: 10, color: '#94a3b8', marginLeft: 8, fontWeight: 400 }}>
                    (Nhấp đúp vào ô trên ảnh để mở cọ tẩy xóa pixel thừa)
                  </span>
                </div>
                <div style={{ fontSize: 10, color: '#94a3b8' }}>
                  Vị trí Slot: <b>{selectedCell.partSlot}</b> • Góc Camera: <b>{selectedCell.angle || '0°'}</b>
                </div>
              </div>

              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                {slicedResults.get(`${selectedCell.row}_${selectedCell.col}`) && (
                  <img
                    src={slicedResults.get(`${selectedCell.row}_${selectedCell.col}`)}
                    alt="Cell preview"
                    style={{ width: 36, height: 36, objectFit: 'contain', background: '#000', borderRadius: 4, border: '1px solid rgba(255,255,255,0.2)' }}
                  />
                )}

                {/* Open Pixel Eraser Modal Button */}
                <button
                  onClick={() => handleOpenCellPixelEditor(selectedCell)}
                  style={{
                    padding: '5px 12px',
                    fontSize: 11,
                    fontWeight: 700,
                    borderRadius: 5,
                    background: 'linear-gradient(135deg, #0284c7, #2563eb)',
                    color: '#fff',
                    border: '1px solid #38bdf8',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    gap: 6,
                    boxShadow: '0 0 12px rgba(56, 189, 248, 0.4)',
                  }}
                  title="Mở hộp thoại phóng to ô này để dùng cọ tẩy xóa thủ công từng pixel hoặc đốm trắng thừa"
                >
                  <Eraser size={14} /> 🎨 Tẩy / Xóa Chi Tiết Ô Này
                </button>

                <button
                  onClick={handleResetAllDividers}
                  style={{
                    padding: '5px 8px',
                    fontSize: 10,
                    borderRadius: 4,
                    background: 'rgba(255,255,255,0.06)',
                    color: '#94a3b8',
                    border: '1px solid rgba(255,255,255,0.1)',
                    cursor: 'pointer',
                  }}
                  title="Đặt lại toàn bộ đường kẻ về mặc định đều nhau"
                >
                  🔄 Reset Lưới
                </button>
              </div>
            </div>

            {/* Quick Extension Buttons for Long Hair / Row & Column Adjustments */}
            <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap', paddingTop: 4, borderTop: '1px solid rgba(255,255,255,0.06)' }}>
              <span style={{ fontSize: 10, fontWeight: 600, color: '#e2e8f0' }}>🔽 Kéo dài hàng này (Cao):</span>
              <button
                onClick={() => handleAdjustRowHeight(15)}
                style={{ padding: '3px 7px', fontSize: 10, borderRadius: 4, background: '#0284c7', color: '#fff', border: 'none', cursor: 'pointer', fontWeight: 600 }}
              >
                +15px
              </button>
              <button
                onClick={() => handleAdjustRowHeight(30)}
                style={{ padding: '3px 7px', fontSize: 10, borderRadius: 4, background: '#0284c7', color: '#fff', border: 'none', cursor: 'pointer', fontWeight: 700 }}
              >
                +30px
              </button>
              <button
                onClick={() => handleAdjustRowHeight(60)}
                style={{ padding: '3px 7px', fontSize: 10, borderRadius: 4, background: '#0284c7', color: '#fff', border: 'none', cursor: 'pointer', fontWeight: 700 }}
              >
                +60px
              </button>
              <button
                onClick={() => handleAdjustRowHeight(-15)}
                style={{ padding: '3px 7px', fontSize: 10, borderRadius: 4, background: 'rgba(255,255,255,0.08)', color: '#cbd5e1', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
              >
                -15px
              </button>

              <span style={{ fontSize: 10, fontWeight: 600, color: '#e2e8f0', marginLeft: 8 }}>↔️ Chiều rộng cột:</span>
              <button
                onClick={() => handleAdjustColWidth(15)}
                style={{ padding: '3px 7px', fontSize: 10, borderRadius: 4, background: '#0369a1', color: '#fff', border: 'none', cursor: 'pointer', fontWeight: 600 }}
              >
                +15px
              </button>
              <button
                onClick={() => handleAdjustColWidth(30)}
                style={{ padding: '3px 7px', fontSize: 10, borderRadius: 4, background: '#0369a1', color: '#fff', border: 'none', cursor: 'pointer', fontWeight: 700 }}
              >
                +30px
              </button>
              <button
                onClick={() => handleAdjustColWidth(-15)}
                style={{ padding: '3px 7px', fontSize: 10, borderRadius: 4, background: 'rgba(255,255,255,0.08)', color: '#cbd5e1', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
              >
                -15px
              </button>

              <button
                onClick={handleResetAllDividers}
                style={{
                  marginLeft: 'auto',
                  padding: '3px 8px',
                  fontSize: 10,
                  borderRadius: 4,
                  background: 'rgba(239, 68, 68, 0.15)',
                  color: '#f87171',
                  border: '1px solid rgba(239, 68, 68, 0.3)',
                  cursor: 'pointer',
                }}
              >
                Reset Toàn Bộ Lưới
              </button>
            </div>
          </div>
        ) : (
          <div style={{ background: '#0b1329', padding: '8px 12px', borderRadius: 6, border: '1px solid rgba(255,255,255,0.08)', fontSize: 10, color: '#94a3b8' }}>
            💡 <i>Mẹo: Rê chuột vào các đường kẻ nét đứt bên trong khung ảnh (sẽ hiện con trỏ ↔ hoặc ↕), bấm giữ và kéo để co giãn kích thước các hàng/cột tự do mà không làm dịch chuyển ảnh.</i>
          </div>
        )}
      </div>

      {/* ─── RIGHT COLUMN: Live 3D Multi-Angle Turntable Preview ────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10, background: 'rgba(15, 23, 42, 0.7)', padding: 12, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', overflow: 'hidden' }}>
        <div style={{ display: 'flex', fontSize: 12, fontWeight: 700, color: '#38bdf8', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <Compass size={14} /> XEM TRƯỚC 3D ĐA GÓC
          </span>
          <span style={{ fontSize: 10, padding: '2px 6px', borderRadius: 4, background: 'rgba(56, 189, 248, 0.15)', color: '#38bdf8' }}>
            {activeAngleInfo.compassDirection} • {activeAngleInfo.angleDeg}°
          </span>
        </div>

        {/* 3D WebGL Container */}
        <div
          ref={threeContainerRef}
          style={{
            width: '100%',
            height: 250,
            flexShrink: 0,
            borderRadius: 6,
            overflow: 'hidden',
            border: '1px solid rgba(56, 189, 248, 0.3)',
            background: '#090d16',
          }}
        />

        {/* Angle Turntable Slider */}
        <div style={{ flexShrink: 0 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 2 }}>
            <span>Xoay Camera 360°:</span>
            <span style={{ color: '#38bdf8', fontWeight: 600 }}>{activeAngleInfo.angleLabel}</span>
          </div>
          <input
            type="range"
            min="0"
            max="360"
            value={turntableAngle}
            onChange={(e) => {
              const val = parseInt(e.target.value);
              setTurntableAngle(val);
              if (threeEngineRef.current) {
                threeEngineRef.current.jumpToAngle(val);
              }
            }}
            style={{ width: '100%' }}
          />
        </div>

        {/* Sliced Thumbnails Strip - Stretches all the way to bottom */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0, overflow: 'hidden' }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: '#94a3b8', marginBottom: 6, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span>Linh kiện đã bóc tách ({slicedResults.size}/{currentCategory.cells.length} ô):</span>
            {slicedResults.size > 0 && (
              <span style={{ fontSize: 9, color: '#4ade80', fontWeight: 600 }}>✓ Đã tách nền 100%</span>
            )}
          </div>
          <div
            style={{
              flex: 1,
              display: 'grid',
              gridTemplateColumns: 'repeat(5, 1fr)',
              gap: 6,
              overflowY: 'auto',
              padding: 6,
              background: 'rgba(0,0,0,0.3)',
              borderRadius: 6,
              border: '1px solid rgba(255,255,255,0.06)',
            }}
          >
            {currentCategory.cells.map((c) => {
              const key = `${c.row}_${c.col}`;
              const png = slicedResults.get(key);
              return (
                <div
                  key={key}
                  onClick={() => setSelectedCell(c)}
                  title={c.label}
                  style={{
                    height: 56,
                    borderRadius: 4,
                    background: selectedCell?.row === c.row && selectedCell?.col === c.col ? 'rgba(56, 189, 248, 0.25)' : 'rgba(0,0,0,0.5)',
                    border: selectedCell?.row === c.row && selectedCell?.col === c.col ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    justifyContent: 'center',
                    cursor: 'pointer',
                    overflow: 'hidden',
                    padding: 2,
                    transition: 'all 0.15s',
                  }}
                >
                  {png ? (
                    <img src={png} alt={c.label} style={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain' }} />
                  ) : (
                    <span style={{ fontSize: 9, color: '#475569', fontWeight: 600 }}>R{c.row + 1}C{c.col + 1}</span>
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* Switch to Character Assembly Tab Button */}
        {onSwitchToAssemblyTab && (
          <button
            onClick={onSwitchToAssemblyTab}
            style={{
              flexShrink: 0,
              padding: '10px 12px',
              fontSize: 11,
              fontWeight: 700,
              borderRadius: 6,
              background: 'linear-gradient(135deg, #10b981, #059669)',
              color: '#fff',
              border: 'none',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
              boxShadow: '0 4px 12px rgba(16, 185, 129, 0.3)',
            }}
          >
            <ArrowRight size={14} /> Chuyển Sang Bàn Lắp Ráp 2D/3D
          </button>
        )}
      </div>

      {/* Manual Pixel Eraser & Zoom Cleanup Modal */}
      {editingCellData && (
        <CellPixelEraserModal
          isOpen={Boolean(editingCellData)}
          onClose={() => setEditingCellData(null)}
          cellTitle={`Ô [Hàng ${editingCellData.cell.row + 1}, Cột ${editingCellData.cell.col + 1}]: ${editingCellData.cell.label} (${editingCellData.cell.partSlot})`}
          initialImageDataUrl={editingCellData.dataUrl}
          onSave={handleSaveCellPixelEdit}
        />
      )}
    </div>
  );
};
