import React, { useState, useRef, useEffect, useCallback } from 'react';
import {
  Character2DAssembly,
  Character2DAngle,
  Character2DPartType,
  CharacterResourceCategory,
  CharacterResourceKit,
} from '../../types/scene2d';
import {
  GRID_CATEGORY_DEFINITIONS,
  GridCategoryDefinition,
  GridCellDefinition,
  generateDemoGridSpriteSheet,
} from '../../core/assets/GridSliceRegistry';
import demoHairMultiAngleSheet from '../../assets/demo_hair_multi_angle_sheet.jpg';
import demoChibiHairSpriteSheet from '../../assets/demo_chibi_hair_sprite_sheet.jpg';
import {
  ThreeMultiAngleBillboardEngine,
  AngleDetectionResult,
} from '../../core/engine2d/ThreeMultiAngleBillboardEngine';
import { CellPixelEraserModal } from './CellPixelEraserModal';
import { CharacterAssetCatalogModal } from './CharacterAssetCatalogModal';
import { MultiAngleTunerModal } from './MultiAngleTunerModal';
import { saveCustomResourceKit } from '../../core/assets/CharacterKitStorage';
import { processCellChromaAndDespeckle } from '../../core/utils/ChromaDespeckleProcessor';
import { PART_HIERARCHY_CONFIG } from '../../core/assets/Asset2DRegistry';

// Subcomponents
import { SlicerSidebarControls } from './slicer/SlicerSidebarControls';
import { SlicerCellAdjustmentBar } from './slicer/SlicerCellAdjustmentBar';
import { SlicerInteractiveCanvas } from './slicer/SlicerInteractiveCanvas';
import { Slicer3DTurntablePreview } from './slicer/Slicer3DTurntablePreview';

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
  // Slicing & Category Configuration
  const [selectedCatId, setSelectedCatId] = useState<string>('hair_multi_angle_grid');
  const [activeDemoKey, setActiveDemoKey] = useState<'default' | 'chibi'>('chibi');
  const [keyColorType, setKeyColorType] = useState<'chroma_green' | 'pure_white' | 'custom'>('chroma_green');
  const [keyColorHex, setKeyColorHex] = useState<string>('#00ff00');
  const [isolationMode, setIsolationMode] = useState<'all' | 'outer_only'>('outer_only');
  const [tolerance, setTolerance] = useState<number>(38);
  const [feather, setFeather] = useState<number>(1);
  const [strokeWidth, setStrokeWidth] = useState<number>(0);
  const [strokeColorHex, setStrokeColorHex] = useState<string>('#000000');
  const [bgCleanupSubTab, setBgCleanupSubTab] = useState<'chroma' | 'despeckle'>('chroma');
  const [despeckleSize, setDespeckleSize] = useState<number>(18);
  const [whiteSpeckleSensitivity, setWhiteSpeckleSensitivity] = useState<number>(45);
  const [keepLargestIslandOnly, setKeepLargestIslandOnly] = useState<boolean>(false);
  const [isCumulativeProcessing, setIsCumulativeProcessing] = useState<boolean>(false);

  // Sync snapshot when cumulative mode is toggled
  const toggleCumulativeProcessing = (enabled: boolean) => {
    setIsCumulativeProcessing(enabled);
    if (enabled) {
      // Snapshot current sliced canvases as stable cumulative base
      const snapshot = new Map<string, HTMLCanvasElement>();
      slicedCanvasesRef.current.forEach((canvas, key) => {
        const copy = document.createElement('canvas');
        copy.width = canvas.width;
        copy.height = canvas.height;
        const cCtx = copy.getContext('2d');
        if (cCtx) cCtx.drawImage(canvas, 0, 0);
        snapshot.set(key, copy);
      });
      cumulativeBaseCanvasesRef.current = snapshot;
    }
  };
  const [paddingInset, setPaddingInset] = useState<number>(0);

  // User upload & Slicing state
  const [userUploadedImageUrl, setUserUploadedImageUrl] = useState<string | null>(null);
  const [slicedResults, setSlicedResults] = useState<Map<string, string>>(new Map());
  const [selectedCell, setSelectedCell] = useState<GridCellDefinition | null>(null);
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
  const [timeOfDay, setTimeOfDay] = useState<number>(0);

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
  const [loadedImage, setLoadedImage] = useState<HTMLImageElement | null>(null);
  const slicedCanvasesRef = useRef<Map<string, HTMLCanvasElement>>(new Map());
  const cumulativeBaseCanvasesRef = useRef<Map<string, HTMLCanvasElement>>(new Map());

  // Interactive Grid Dividers: X coords for cols & Y coords for rows
  const [colDividers, setColDividers] = useState<number[]>([]);
  const [rowDividers, setRowDividers] = useState<number[]>([]);
  const draggingDividerRef = useRef<{ type: 'col' | 'row'; index: number } | null>(null);

  // Pixel Eraser Modal state
  const [isEraserOpen, setIsEraserOpen] = useState<boolean>(false);
  const [editingCellDef, setEditingCellDef] = useState<GridCellDefinition | null>(null);
  const [editingCellOriginalDataUrl, setEditingCellOriginalDataUrl] = useState<string>('');

  // Catalog, Save Kit & Multi-Angle Tuner Modal state
  const [isTunerOpen, setIsTunerOpen] = useState<boolean>(false);
  const [isCatalogOpen, setIsCatalogOpen] = useState<boolean>(false);
  const [isSaveKitModalOpen, setIsSaveKitModalOpen] = useState<boolean>(false);
  const [saveKitName, setSaveKitName] = useState<string>('');
  const [saveKitCategory, setSaveKitCategory] = useState<CharacterResourceCategory>('toc');
  const [saveKitAuthor, setSaveKitAuthor] = useState<string>('AI Master');
  const [saveKitDescription, setSaveKitDescription] = useState<string>('');

  // Initialize Default Uniform Dividers
  const initUniformDividers = useCallback((width: number, height: number, cols: number, rows: number) => {
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

  // Initialize 3D Engine
  useEffect(() => {
    if (threeContainerRef.current && !threeEngineRef.current) {
      threeEngineRef.current = new ThreeMultiAngleBillboardEngine(
        threeContainerRef.current,
        (res: AngleDetectionResult) => {
          setActiveAngleInfo(res);
          setTurntableAngle(res.angleDeg);
        }
      );
      if (currentAssembly) {
        threeEngineRef.current.setAssembly(currentAssembly);
      }
    }

    return () => {
      if (threeEngineRef.current) {
        threeEngineRef.current.dispose();
        threeEngineRef.current = null;
      }
    };
  }, []);

  // Sync 3D engine with currentAssembly updates
  useEffect(() => {
    if (threeEngineRef.current && currentAssembly) {
      threeEngineRef.current.setAssembly(currentAssembly);
    }
  }, [currentAssembly]);

  // Load Sprite Sheet Image
  useEffect(() => {
    const img = new Image();
    img.crossOrigin = 'anonymous';

    img.onload = () => {
      loadedImageRef.current = img;
      setLoadedImage(img);
      initUniformDividers(img.width, img.height, currentCategory.cols, currentCategory.rows);
      setPreviewDisplayMode('original');
      setHasExplicitlySliced(false);
      slicedCanvasesRef.current.clear();
      setSlicedResults(new Map());
    };

    if (userUploadedImageUrl) {
      img.src = userUploadedImageUrl;
    } else {
      if (currentCategory.id === 'hair_multi_angle_grid') {
        img.src = activeDemoKey === 'chibi' ? demoChibiHairSpriteSheet : demoHairMultiAngleSheet;
      } else {
        img.src = generateDemoGridSpriteSheet(currentCategory.id);
      }
    }
  }, [userUploadedImageUrl, activeDemoKey, selectedCatId, currentCategory, initUniformDividers]);

  // Redraw Canvas & Grid Dividers
  const redrawCanvas = useCallback(() => {
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

    if (previewDisplayMode === 'transparent' && hasExplicitlySliced) {
      // Checkerboard transparency pattern
      const size = 16;
      for (let x = 0; x < canvas.width; x += size) {
        for (let y = 0; y < canvas.height; y += size) {
          ctx.fillStyle = (Math.floor(x / size) + Math.floor(y / size)) % 2 === 0 ? '#182030' : '#0c1220';
          ctx.fillRect(x, y, size, size);
        }
      }

      currentCategory.cells.forEach((cell) => {
        const key = `${cell.row}_${cell.col}`;
        const cellCanvas = slicedCanvasesRef.current.get(key);
        if (cellCanvas && colDividers.length > cell.col + 1 && rowDividers.length > cell.row + 1) {
          const pad = Math.max(0, paddingInset);
          const rawX0 = colDividers[cell.col];
          const rawY0 = rowDividers[cell.row];
          const rawW = colDividers[cell.col + 1] - rawX0;
          const rawH = rowDividers[cell.row + 1] - rawY0;
          const x0 = rawX0 + pad;
          const y0 = rawY0 + pad;
          const w = Math.max(10, rawW - pad * 2);
          const h = Math.max(10, rawH - pad * 2);
          ctx.drawImage(cellCanvas, x0, y0, w, h);
        }
      });
    } else {
      ctx.drawImage(img, 0, 0);
    }

    // Draw Grid Lines (Alternating Black & White Dashes for 100% Contrast on Any Background)
    const drawDualDashLine = (x1: number, y1: number, x2: number, y2: number, isBorder = false) => {
      if (isBorder) {
        ctx.lineWidth = 2.5;
        ctx.setLineDash([]);
        ctx.strokeStyle = '#000000';
        ctx.beginPath();
        ctx.moveTo(x1, y1);
        ctx.lineTo(x2, y2);
        ctx.stroke();

        ctx.lineWidth = 1.2;
        ctx.strokeStyle = '#38bdf8';
        ctx.beginPath();
        ctx.moveTo(x1, y1);
        ctx.lineTo(x2, y2);
        ctx.stroke();
      } else {
        // Pass 1: Solid Black Base Dash
        ctx.lineWidth = 2.0;
        ctx.strokeStyle = '#000000';
        ctx.setLineDash([6, 6]);
        ctx.lineDashOffset = 0;
        ctx.beginPath();
        ctx.moveTo(x1, y1);
        ctx.lineTo(x2, y2);
        ctx.stroke();

        // Pass 2: Interleaved White Dash
        ctx.lineWidth = 2.0;
        ctx.strokeStyle = '#ffffff';
        ctx.setLineDash([6, 6]);
        ctx.lineDashOffset = 6;
        ctx.beginPath();
        ctx.moveTo(x1, y1);
        ctx.lineTo(x2, y2);
        ctx.stroke();
      }
    };

    colDividers.forEach((x, c) => {
      const isBorder = c === 0 || c === colDividers.length - 1;
      drawDualDashLine(x, 0, x, canvas.height, isBorder);
    });

    rowDividers.forEach((y, r) => {
      const isBorder = r === 0 || r === rowDividers.length - 1;
      drawDualDashLine(0, y, canvas.width, y, isBorder);
    });
    ctx.setLineDash([]);
    ctx.lineDashOffset = 0;

    // Highlight Selected Cell with High-Contrast Dual Stroke
    if (selectedCell && colDividers.length > selectedCell.col + 1 && rowDividers.length > selectedCell.row + 1) {
      const x0 = colDividers[selectedCell.col];
      const y0 = rowDividers[selectedCell.row];
      const w = colDividers[selectedCell.col + 1] - x0;
      const h = rowDividers[selectedCell.row + 1] - y0;

      ctx.fillStyle = 'rgba(56, 189, 248, 0.22)';
      ctx.fillRect(x0, y0, w, h);

      // Outer black border
      ctx.strokeStyle = '#000000';
      ctx.lineWidth = 3.5;
      ctx.setLineDash([]);
      ctx.strokeRect(x0, y0, w, h);

      // Inner glowing cyan border
      ctx.strokeStyle = '#38bdf8';
      ctx.lineWidth = 2.0;
      ctx.strokeRect(x0, y0, w, h);
    }
  }, [loadedImage, previewDisplayMode, hasExplicitlySliced, currentCategory, colDividers, rowDividers, selectedCell, slicedResults, paddingInset]);

  useEffect(() => {
    redrawCanvas();
  }, [redrawCanvas]);

  // Mouse drag & drop dividers handler
  const handleCanvasMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = imageCanvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    const mouseX = (e.clientX - rect.left) * scaleX;
    const mouseY = (e.clientY - rect.top) * scaleY;

    // Check Col dividers
    for (let c = 1; c < colDividers.length - 1; c++) {
      if (Math.abs(colDividers[c] - mouseX) <= 12) {
        draggingDividerRef.current = { type: 'col', index: c };
        return;
      }
    }

    // Check Row dividers
    for (let r = 1; r < rowDividers.length - 1; r++) {
      if (Math.abs(rowDividers[r] - mouseY) <= 12) {
        draggingDividerRef.current = { type: 'row', index: r };
        return;
      }
    }

    // Select Cell clicked
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
      if (found) {
        setSelectedCell(found);
      }
    }
  };

  const handleCanvasMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = imageCanvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    const mouseX = (e.clientX - rect.left) * scaleX;
    const mouseY = (e.clientY - rect.top) * scaleY;

    if (draggingDividerRef.current) {
      if (draggingDividerRef.current.type === 'col') {
        const idx = draggingDividerRef.current.index;
        const min = colDividers[idx - 1] + 15;
        const max = colDividers[idx + 1] - 15;
        const newX = Math.max(min, Math.min(max, Math.round(mouseX)));
        setColDividers((prev) => {
          const next = [...prev];
          next[idx] = newX;
          return next;
        });
      } else {
        const idx = draggingDividerRef.current.index;
        const min = rowDividers[idx - 1] + 15;
        const max = rowDividers[idx + 1] - 15;
        const newY = Math.max(min, Math.min(max, Math.round(mouseY)));
        setRowDividers((prev) => {
          const next = [...prev];
          next[idx] = newY;
          return next;
        });
      }
      return;
    }

    // Change Cursor when hovering dividers
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
  };

  const handleCanvasMouseUp = () => {
    draggingDividerRef.current = null;
  };

  // Open Pixel Eraser Modal on Double Click
  const handleCanvasDoubleClick = () => {
    if (selectedCell) {
      openCellPixelEditor(selectedCell);
    }
  };

  const openCellPixelEditor = (cell: GridCellDefinition) => {
    const key = `${cell.row}_${cell.col}`;
    const dataUrl = slicedResults.get(key);
    if (!dataUrl && loadedImageRef.current) {
      // Create quick slice for this cell with padding inset
      const pad = Math.max(0, paddingInset);
      const rawX0 = colDividers[cell.col];
      const rawY0 = rowDividers[cell.row];
      const rawW = colDividers[cell.col + 1] - rawX0;
      const rawH = rowDividers[cell.row + 1] - rawY0;
      const x0 = rawX0 + pad;
      const y0 = rawY0 + pad;
      const w = Math.max(10, rawW - pad * 2);
      const h = Math.max(10, rawH - pad * 2);
      const tempCanvas = document.createElement('canvas');
      tempCanvas.width = w;
      tempCanvas.height = h;
      const tempCtx = tempCanvas.getContext('2d');
      if (tempCtx) {
        tempCtx.drawImage(loadedImageRef.current, x0, y0, w, h, 0, 0, w, h);
        setEditingCellOriginalDataUrl(tempCanvas.toDataURL());
      }
    } else if (dataUrl) {
      setEditingCellOriginalDataUrl(dataUrl);
    }
    setEditingCellDef(cell);
    setIsEraserOpen(true);
  };

  // Auto Slice & Assemble Algorithm using High-Performance Chroma & Despeckle Processor
  const handleAutoSliceAndAssemble = useCallback((overrides?: Partial<Parameters<typeof processCellChromaAndDespeckle>[3]>) => {
    const img = loadedImageRef.current;
    if (!img) return;

    const effTol = overrides?.tolerance !== undefined ? overrides.tolerance : tolerance;
    const effFeather = overrides?.feather !== undefined ? overrides.feather : feather;
    const effStrokeW = overrides?.strokeWidth !== undefined ? overrides.strokeWidth : strokeWidth;
    const effStrokeColor = overrides?.strokeColorHex !== undefined ? overrides.strokeColorHex : strokeColorHex;
    const effKeyType = overrides?.keyColorType !== undefined ? overrides.keyColorType : keyColorType;
    const effKeyHex = overrides?.keyColorHex !== undefined ? overrides.keyColorHex : keyColorHex;
    const effIsoMode = overrides?.isolationMode !== undefined ? overrides.isolationMode : isolationMode;
    const effDespeckle = overrides?.despeckleSize !== undefined ? overrides.despeckleSize : despeckleSize;
    const effWhiteSens = overrides?.whiteSpeckleSensitivity !== undefined ? overrides.whiteSpeckleSensitivity : whiteSpeckleSensitivity;
    const effKeepLargest = overrides?.keepLargestIslandOnly !== undefined ? overrides.keepLargestIslandOnly : keepLargestIslandOnly;

    setIsProcessing(true);
    setTimeout(() => {
      const results = new Map<string, string>();
      const updatedAssembly: Character2DAssembly = JSON.parse(JSON.stringify(currentAssembly));

      currentCategory.cells.forEach((cell) => {
        if (colDividers.length <= cell.col + 1 || rowDividers.length <= cell.row + 1) return;

        const pad = Math.max(0, paddingInset);
        const rawX0 = colDividers[cell.col];
        const rawY0 = rowDividers[cell.row];
        const rawW = colDividers[cell.col + 1] - rawX0;
        const rawH = rowDividers[cell.row + 1] - rawY0;
        const x0 = rawX0 + pad;
        const y0 = rawY0 + pad;
        const w = Math.max(10, rawW - pad * 2);
        const h = Math.max(10, rawH - pad * 2);

        const cellCanvas = document.createElement('canvas');
        cellCanvas.width = w;
        cellCanvas.height = h;
        const ctx = cellCanvas.getContext('2d');
        if (!ctx) return;

        const key = `${cell.row}_${cell.col}`;
        const cumulativeBase = cumulativeBaseCanvasesRef.current.get(key);

        if (isCumulativeProcessing && cumulativeBase) {
          // Draw from stable snapshot of previous pass (prevents compounding erosion on slider moves!)
          ctx.drawImage(cumulativeBase, 0, 0, w, h);
        } else {
          // Draw from original raw sprite sheet (with padding inset)
          ctx.drawImage(img, x0, y0, w, h, 0, 0, w, h);
        }

        // Run full Chroma Key, Feathering, Despeckle & Noise Filtering with latest effective values
        processCellChromaAndDespeckle(ctx, w, h, {
          keyColorType: effKeyType,
          keyColorHex: effKeyHex,
          isolationMode: effIsoMode,
          tolerance: effTol,
          feather: effFeather,
          strokeWidth: effStrokeW,
          strokeColorHex: effStrokeColor,
          despeckleSize: effDespeckle,
          whiteSpeckleSensitivity: effWhiteSens,
          keepLargestIslandOnly: effKeepLargest,
        });

        const dataUrl = cellCanvas.toDataURL('image/png');
        results.set(key, dataUrl);
        slicedCanvasesRef.current.set(key, cellCanvas);

        // Map to 3D Assembly part (ensure part exists in assembly)
        if (cell.partSlot) {
          const hierarchy = PART_HIERARCHY_CONFIG[cell.partSlot];
          if (!updatedAssembly.parts[cell.partSlot]) {
            updatedAssembly.parts[cell.partSlot] = {
              path: dataUrl,
              offset: hierarchy?.defaultOffset ? [...hierarchy.defaultOffset] : [0, 0],
              scale: [1, 1],
              rotation: 0,
              pivot: hierarchy?.defaultPivot ? [...hierarchy.defaultPivot] : [0.5, 0.5],
              flipX: false,
              flipY: false,
              z_index: hierarchy?.defaultZ ?? 1,
              z_depth_3d: hierarchy?.defaultZDepth3D ?? 0,
              opacity: 1,
              angles: {},
            };
          }
          const part = updatedAssembly.parts[cell.partSlot]!;
          if (!part.offset || (part.offset[0] === 0 && part.offset[1] === 0 && hierarchy?.defaultOffset)) {
            part.offset = [...hierarchy.defaultOffset];
          }
          if (cell.angle === 'front' || cell.col === 0) {
            part.path = dataUrl;
          }
          if (cell.angle) {
            if (!part.angles) part.angles = {};
            part.angles[cell.angle] = dataUrl;
            if (cell.mirrorAngle) {
              part.angles[cell.mirrorAngle] = dataUrl;
            }
          }
        }
      });

      setSlicedResults(results);
      setHasExplicitlySliced(true);
      setPreviewDisplayMode('transparent');
      setAssemblySuccess(true);
      setIsProcessing(false);

      onApplyAssembly(updatedAssembly);
      if (threeEngineRef.current) {
        threeEngineRef.current.setAssembly(updatedAssembly);
      }
      redrawCanvas();
    }, 20);
  }, [
    currentAssembly,
    currentCategory,
    colDividers,
    rowDividers,
    keyColorType,
    keyColorHex,
    isolationMode,
    tolerance,
    feather,
    strokeWidth,
    strokeColorHex,
    despeckleSize,
    whiteSpeckleSensitivity,
    keepLargestIslandOnly,
    isCumulativeProcessing,
    paddingInset,
    onApplyAssembly,
    redrawCanvas,
  ]);

  // Apply current sliced / processed / erased results as the new Base Image
  const handleApplyAsNewBaseImage = useCallback(() => {
    const baseImg = loadedImageRef.current;
    if (!baseImg) return;

    const compositeCanvas = document.createElement('canvas');
    compositeCanvas.width = baseImg.width;
    compositeCanvas.height = baseImg.height;
    const cCtx = compositeCanvas.getContext('2d');
    if (!cCtx) return;

    // Draw base image first
    cCtx.drawImage(baseImg, 0, 0);

    // Overlay all sliced / processed / erased cells onto their exact grid positions
    currentCategory.cells.forEach((cell) => {
      const key = `${cell.row}_${cell.col}`;
      const cellCanvas = slicedCanvasesRef.current.get(key);
      if (cellCanvas && colDividers.length > cell.col + 1 && rowDividers.length > cell.row + 1) {
        const pad = Math.max(0, paddingInset);
        const rawX0 = colDividers[cell.col];
        const rawY0 = rowDividers[cell.row];
        const rawW = colDividers[cell.col + 1] - rawX0;
        const rawH = rowDividers[cell.row + 1] - rawY0;
        const x0 = rawX0 + pad;
        const y0 = rawY0 + pad;
        const w = Math.max(10, rawW - pad * 2);
        const h = Math.max(10, rawH - pad * 2);
        cCtx.clearRect(rawX0, rawY0, rawW, rawH);
        cCtx.drawImage(cellCanvas, x0, y0, w, h);
      }
    });

    const newBaseDataUrl = compositeCanvas.toDataURL('image/png');
    
    // Create new Image object to replace loadedImageRef and update state
    const newImg = new Image();
    newImg.crossOrigin = 'anonymous';
    newImg.onload = () => {
      loadedImageRef.current = newImg;
      setLoadedImage(newImg);
      setUserUploadedImageUrl(newBaseDataUrl);
      slicedCanvasesRef.current.clear();
      setPreviewDisplayMode('original');
      setHasExplicitlySliced(false);
      redrawCanvas();
    };
    newImg.src = newBaseDataUrl;
  }, [currentCategory, colDividers, rowDividers, redrawCanvas]);

  // Reset cumulative cache to original raw slices
  const handleResetToRawSlices = useCallback(() => {
    slicedCanvasesRef.current.clear();
    setIsCumulativeProcessing(false);
    handleAutoSliceAndAssemble();
  }, [handleAutoSliceAndAssemble]);

  // Adjust Column Width
  const adjustColWidth = (deltaPx: number) => {
    if (!selectedCell) return;
    const c = selectedCell.col;
    setColDividers((prev) => {
      const next = [...prev];
      if (c + 1 < next.length) {
        next[c + 1] = Math.max(next[c] + 15, next[c + 1] + deltaPx);
      }
      return next;
    });
    if (hasExplicitlySliced) {
      handleAutoSliceAndAssemble();
    }
  };

  // Reset All Dividers
  const resetAllDividers = () => {
    const img = loadedImageRef.current;
    if (img) {
      initUniformDividers(img.width, img.height, currentCategory.cols, currentCategory.rows);
      if (hasExplicitlySliced) {
        handleAutoSliceAndAssemble();
      }
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', gap: 8, padding: 8, background: '#040711', overflow: 'hidden' }}>
      {/* Main 3-Column Studio Grid: 260px Sidebar, 1fr Interactive Canvas, 400px 3D Preview */}
      <div style={{ flex: 1, display: 'grid', gridTemplateColumns: '260px 1fr 400px', gap: 8, minHeight: 0 }}>
        {/* Left Column: Slicer Controls & Filters */}
        <SlicerSidebarControls
          selectedCatId={selectedCatId}
          onSelectCatId={setSelectedCatId}
          userUploadedImageUrl={userUploadedImageUrl}
          onFileUpload={(e) => {
            const file = e.target.files?.[0];
            if (file) {
              const url = URL.createObjectURL(file);
              setUserUploadedImageUrl(url);
              setHasExplicitlySliced(false);
              setSlicedResults(new Map());
              slicedCanvasesRef.current.clear();
              setIsCumulativeProcessing(false);
              setPreviewDisplayMode('original');
              setAssemblySuccess(false);
            }
          }}
          onResetToDemoImage={(key = 'chibi') => {
            setUserUploadedImageUrl(null);
            setActiveDemoKey(key);
            setHasExplicitlySliced(false);
            setSlicedResults(new Map());
            slicedCanvasesRef.current.clear();
            setIsCumulativeProcessing(false);
            setPreviewDisplayMode('original');
            setAssemblySuccess(false);
          }}
          fileInputRef={fileInputRef}
          keyColorType={keyColorType}
          setKeyColorType={setKeyColorType}
          keyColorHex={keyColorHex}
          setKeyColorHex={setKeyColorHex}
          isolationMode={isolationMode}
          setIsolationMode={setIsolationMode}
          tolerance={tolerance}
          setTolerance={setTolerance}
          feather={feather}
          setFeather={setFeather}
          strokeWidth={strokeWidth}
          setStrokeWidth={setStrokeWidth}
          strokeColorHex={strokeColorHex}
          setStrokeColorHex={setStrokeColorHex}
          bgCleanupSubTab={bgCleanupSubTab}
          setBgCleanupSubTab={setBgCleanupSubTab}
          despeckleSize={despeckleSize}
          setDespeckleSize={setDespeckleSize}
          whiteSpeckleSensitivity={whiteSpeckleSensitivity}
          setWhiteSpeckleSensitivity={setWhiteSpeckleSensitivity}
          keepLargestIslandOnly={keepLargestIslandOnly}
          setKeepLargestIslandOnly={setKeepLargestIslandOnly}
          isCumulativeProcessing={isCumulativeProcessing}
          setIsCumulativeProcessing={toggleCumulativeProcessing}
          onResetToRawSlices={handleResetToRawSlices}
          onApplyAsNewBaseImage={handleApplyAsNewBaseImage}
          paddingInset={paddingInset}
          setPaddingInset={setPaddingInset}
          isProcessing={isProcessing}
          assemblySuccess={assemblySuccess}
          onAutoSliceAndAssemble={handleAutoSliceAndAssemble}
          onCommitSliderChange={(overrides) => {
            if (hasExplicitlySliced) {
              handleAutoSliceAndAssemble(overrides);
            }
          }}
          slicedCount={slicedResults.size}
          totalCellCount={currentCategory.cells.length}
          onOpenSaveKitModal={() => {
            setSaveKitName(currentCategory.label);
            setIsSaveKitModalOpen(true);
          }}
          onOpenCatalogModal={() => setIsCatalogOpen(true)}
        />

        {/* Center Column: Interactive Canvas Slicer & Adjuster */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6, minHeight: 0, overflow: 'hidden' }}>
          <SlicerCellAdjustmentBar
            selectedCell={selectedCell}
            slicedCellDataUrl={selectedCell ? slicedResults.get(`${selectedCell.row}_${selectedCell.col}`) : undefined}
            onOpenCellPixelEditor={openCellPixelEditor}
            onAdjustColWidth={adjustColWidth}
            onResetAllDividers={resetAllDividers}
          />

          <SlicerInteractiveCanvas
            imageCanvasRef={imageCanvasRef}
            previewDisplayMode={previewDisplayMode}
            setPreviewDisplayMode={setPreviewDisplayMode}
            hasExplicitlySliced={hasExplicitlySliced}
            currentCategory={currentCategory}
            onMouseDown={handleCanvasMouseDown}
            onDoubleClick={handleCanvasDoubleClick}
            onMouseMove={handleCanvasMouseMove}
            onMouseUp={handleCanvasMouseUp}
          />
        </div>

        {/* Right Column: 3D Turntable Preview, 360° & 2D Backgrounds (Spacious 400px) */}
        <Slicer3DTurntablePreview
          threeContainerRef={threeContainerRef}
          threeEngineRef={threeEngineRef}
          activeAngleInfo={activeAngleInfo}
          turntableAngle={turntableAngle}
          setTurntableAngle={setTurntableAngle}
          timeOfDay={timeOfDay}
          setTimeOfDay={setTimeOfDay}
          slicedResults={slicedResults}
          currentCategory={currentCategory}
          selectedCell={selectedCell}
          onSelectCell={setSelectedCell}
          onOpenSaveKitModal={() => {
            setSaveKitName(currentCategory.label);
            setIsSaveKitModalOpen(true);
          }}
          onOpenCatalogModal={() => setIsCatalogOpen(true)}
          onOpenTunerModal={() => setIsTunerOpen(true)}
        />
      </div>

      {/* Pixel Eraser Modal */}
      {isEraserOpen && editingCellDef && (
        <CellPixelEraserModal
          isOpen={isEraserOpen}
          cellTitle={`${editingCellDef.label} [${editingCellDef.row + 1}, ${editingCellDef.col + 1}]`}
          initialImageDataUrl={editingCellOriginalDataUrl}
          onClose={() => setIsEraserOpen(false)}
          onSave={(editedDataUrl: string) => {
            if (!editingCellDef) return;
            const key = `${editingCellDef.row}_${editingCellDef.col}`;
            setSlicedResults((prev) => {
              const next = new Map(prev);
              next.set(key, editedDataUrl);
              return next;
            });
            const pImg = new Image();
            pImg.onload = () => {
              const c = document.createElement('canvas');
              c.width = pImg.width;
              c.height = pImg.height;
              const ctx = c.getContext('2d');
              if (ctx) {
                ctx.drawImage(pImg, 0, 0);
                slicedCanvasesRef.current.set(key, c);
                redrawCanvas();
              }
            };
            pImg.src = editedDataUrl;

            // Update 3D Assembly immediately
            const updatedAssembly: Character2DAssembly = JSON.parse(JSON.stringify(currentAssembly));
            if (editingCellDef.partSlot && updatedAssembly.parts[editingCellDef.partSlot]) {
              const part = updatedAssembly.parts[editingCellDef.partSlot]!;
              part.path = editedDataUrl;
              if (editingCellDef.angle) {
                if (!part.angles) part.angles = {};
                part.angles[editingCellDef.angle] = editedDataUrl;
              }
            }
            onApplyAssembly(updatedAssembly);
            if (threeEngineRef.current) {
              threeEngineRef.current.setAssembly(updatedAssembly);
            }
            setIsEraserOpen(false);
          }}
        />
      )}

      {/* Resource Catalog Modal */}
      {isCatalogOpen && (
        <CharacterAssetCatalogModal
          isOpen={isCatalogOpen}
          onClose={() => setIsCatalogOpen(false)}
          currentAssembly={currentAssembly}
          onApplyAssembly={(updated) => {
            onApplyAssembly(updated);
            if (threeEngineRef.current) {
              threeEngineRef.current.setAssembly(updated);
            }
          }}
        />
      )}

      {/* Multi-Angle Part & Hair Layer Tuner Modal */}
      {isTunerOpen && (
        <MultiAngleTunerModal
          isOpen={isTunerOpen}
          onClose={() => setIsTunerOpen(false)}
          currentAssembly={currentAssembly}
          activeCameraAngle={activeAngleInfo.discreteAngle}
          onApplyAssembly={(updated) => {
            onApplyAssembly(updated);
            if (threeEngineRef.current) {
              threeEngineRef.current.setAssembly(updated);
            }
          }}
          onJumpToAngle={(deg, isTop) => {
            setTurntableAngle(deg);
            if (threeEngineRef.current) {
              threeEngineRef.current.jumpToAngle(deg, isTop);
            }
          }}
        />
      )}

      {/* Save Kit Modal */}
      {isSaveKitModalOpen && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.75)', backdropFilter: 'blur(6px)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#0b1329', padding: 18, borderRadius: 10, border: '1px solid #38bdf8', width: 380, display: 'flex', flexDirection: 'column', gap: 12, boxShadow: '0 8px 32px rgba(0,0,0,0.8)' }}>
            <div style={{ fontSize: 13.5, fontWeight: 700, color: '#38bdf8' }}>💾 Lưu Bộ Linh Kiện Mới Vào Kho:</div>
            <div>
              <label style={{ fontSize: 10.5, color: '#94a3b8', display: 'block', marginBottom: 4 }}>Tên bộ:</label>
              <input
                type="text"
                value={saveKitName}
                onChange={(e) => setSaveKitName(e.target.value)}
                style={{ width: '100%', padding: '7px 10px', fontSize: 11.5, background: '#0f172a', color: '#fff', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6 }}
              />
            </div>
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 6 }}>
              <button
                onClick={() => setIsSaveKitModalOpen(false)}
                style={{ padding: '7px 14px', fontSize: 11, borderRadius: 5, background: 'rgba(255,255,255,0.1)', color: '#cbd5e1', border: 'none', cursor: 'pointer' }}
              >
                Hủy
              </button>
              <button
                onClick={() => {
                  const partsMap: Record<string, string> = {};
                  slicedResults.forEach((val, key) => {
                    partsMap[key] = val;
                  });
                  saveCustomResourceKit({
                    id: `kit_${Date.now()}`,
                    name: saveKitName || 'Bộ Linh Kiện Mới',
                    category: saveKitCategory,
                    categoryLabel: currentCategory.label,
                    previewImage: slicedResults.get('0_0') || '',
                    description: saveKitDescription,
                    parts: partsMap as any,
                    createdAt: new Date().toISOString(),
                  });
                  setIsSaveKitModalOpen(false);
                  alert('✓ Đã lưu bộ linh kiện vào kho thành công!');
                }}
                style={{ padding: '7px 16px', fontSize: 11.5, fontWeight: 700, borderRadius: 5, background: 'linear-gradient(135deg, #0284c7, #2563eb)', color: '#fff', border: 'none', cursor: 'pointer', boxShadow: '0 2px 10px rgba(2, 132, 199, 0.4)' }}
              >
                Lưu Vào Kho
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
