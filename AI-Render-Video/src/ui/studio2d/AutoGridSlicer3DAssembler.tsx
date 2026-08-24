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
import {
  ThreeMultiAngleBillboardEngine,
  AngleDetectionResult,
} from '../../core/engine2d/ThreeMultiAngleBillboardEngine';
import { CellPixelEraserModal } from './CellPixelEraserModal';
import { CharacterAssetCatalogModal } from './CharacterAssetCatalogModal';
import { MultiAngleTunerModal } from './MultiAngleTunerModal';
import { saveCustomResourceKit } from '../../core/assets/CharacterKitStorage';
import { processCellChromaAndDespeckle } from '../../core/utils/ChromaDespeckleProcessor';
import { detectAndFitGridDividers } from '../../core/utils/GridAutoFitDetector';
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
  externalImageUrl?: string | null;
  externalCategoryId?: string;
}

export const AutoGridSlicer3DAssembler: React.FC<AutoGridSlicer3DAssemblerProps> = ({
  currentAssembly,
  onApplyAssembly,
  onSwitchToAssemblyTab,
  externalImageUrl,
  externalCategoryId,
}) => {
  // Slicing & Category Configuration (Default to modern Cinematic 6-Angle Grid for single components)
  const [selectedCatId, setSelectedCatId] = useState<string>(externalCategoryId || 'cinematic_single_part_2x3');
  const [activeDemoKey, setActiveDemoKey] = useState<'default' | 'chibi' | 'irregular_ai'>('chibi');
  const [keyColorType, setKeyColorType] = useState<'chroma_green' | 'pure_white' | 'custom'>('chroma_green');
  const [keyColorHex, setKeyColorHex] = useState<string>('#00ff00');
  const [isEyedropperActive, setIsEyedropperActive] = useState<boolean>(false);
  const [eyedropperHoverColor, setEyedropperHoverColor] = useState<{ hex: string; r: number; g: number; b: number; x: number; y: number } | null>(null);
  const [isolationMode, setIsolationMode] = useState<'all' | 'outer_only'>('outer_only');
  const [tolerance, setTolerance] = useState<number>(38);
  const [feather, setFeather] = useState<number>(1);
  const [shadowRetention, setShadowRetention] = useState<number>(0);
  const [strokeWidth, setStrokeWidth] = useState<number>(0);
  const [strokeColorHex, setStrokeColorHex] = useState<string>('#000000');
  const [bgCleanupSubTab, setBgCleanupSubTab] = useState<'chroma' | 'despeckle' | 'ai_matting'>('chroma');
  const [aiModel, setAiModel] = useState<string>('birefnet-general');
  const [aiScope, setAiScope] = useState<'full_image' | 'all' | 'selected'>('full_image');
  const [aiServerStatus, setAiServerStatus] = useState<'online' | 'offline' | 'checking'>('checking');
  const [isAIRunning, setIsAIRunning] = useState<boolean>(false);
  const [despeckleSize, setDespeckleSize] = useState<number>(0);
  const [whiteSpeckleSensitivity, setWhiteSpeckleSensitivity] = useState<number>(0);
  const [keepLargestIslandOnly, setKeepLargestIslandOnly] = useState<boolean>(false);

  // Advanced Despeckle, Color Defringe & Edge Smoothing states
  const [eyedropperTarget, setEyedropperTarget] = useState<'chroma' | 'fringe'>('chroma');
  const [cleanupMode, setCleanupMode] = useState<'all' | 'defringe' | 'smooth' | 'despeckle'>('all');
  const [fringeColorType, setFringeColorType] = useState<'chroma_green' | 'pure_white' | 'pure_black' | 'custom'>('chroma_green');
  const [fringeColorHex, setFringeColorHex] = useState<string>('#00ff00');
  const [defringeStrength, setDefringeStrength] = useState<number>(60);
  const [edgeChoke, setEdgeChoke] = useState<number>(0);
  const [edgeSmooth, setEdgeSmooth] = useState<number>(2);
  const [paddingInset, setPaddingInset] = useState<number>(0);

  // User upload & Slicing state
  const [userUploadedImageUrl, setUserUploadedImageUrl] = useState<string | null>(null);
  const [slicedResults, setSlicedResults] = useState<Map<string, string>>(new Map());
  const [selectedCell, setSelectedCell] = useState<GridCellDefinition | null>(null);
  const [isProcessing, setIsProcessing] = useState<boolean>(false);
  const [assemblySuccess, setAssemblySuccess] = useState<boolean>(false);


  // Auto-check AI Server status on mount and when tab changes
  useEffect(() => {
    const checkServer = async () => {
      try {
        const res = await fetch('http://127.0.0.1:5000/api/status', { method: 'GET' });
        if (res.ok) {
          setAiServerStatus('online');
        } else {
          setAiServerStatus('offline');
        }
      } catch (e) {
        setAiServerStatus('offline');
      }
    };
    checkServer();
    const interval = setInterval(checkServer, 5000);
    return () => clearInterval(interval);
  }, []);

  // Listen to external image transferred from Tab 0 (Antigravity Decomposer)
  useEffect(() => {
    if (externalImageUrl) {
      setUserUploadedImageUrl(externalImageUrl);
      setHasExplicitlySliced(false);
      setSlicedResults(new Map());
      slicedCanvasesRef.current.clear();
      setPreviewDisplayMode('original');
      setAssemblySuccess(false);
      if (externalCategoryId) {
        setSelectedCatId(externalCategoryId);
      }
    }
  }, [externalImageUrl, externalCategoryId]);

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

  // Smart Content-Aware Auto-Fit Grid Dividers for AI-generated Sprite Sheets
  const autoFitDividers = useCallback(
    (img: HTMLImageElement, cols: number, rows: number, keyType = keyColorType, keyHex = keyColorHex) => {
      if (cols <= 1 && rows <= 1) {
        initUniformDividers(img.width, img.height, cols, rows);
        return;
      }
      try {
        const result = detectAndFitGridDividers(img, cols, rows, keyType, keyHex);
        setColDividers(result.colDividers);
        setRowDividers(result.rowDividers);
      } catch (err) {
        console.warn('AutoFit grid detection failed, using uniform grid:', err);
        initUniformDividers(img.width, img.height, cols, rows);
      }
    },
    [keyColorType, keyColorHex, initUniformDividers]
  );

  const handleAutoFitGrid = useCallback(() => {
    const img = loadedImage || loadedImageRef.current;
    if (!img) return;
    autoFitDividers(img, currentCategory.cols, currentCategory.rows, keyColorType, keyColorHex);
  }, [loadedImage, currentCategory, keyColorType, keyColorHex, autoFitDividers]);

  const handleResetUniformGrid = useCallback(() => {
    const img = loadedImage || loadedImageRef.current;
    if (!img) return;
    initUniformDividers(img.width, img.height, currentCategory.cols, currentCategory.rows);
  }, [loadedImage, currentCategory, initUniformDividers]);

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

  // Load Sprite Sheet Image (Only runs when uploaded image URL actually changes)
  useEffect(() => {
    if (!userUploadedImageUrl) {
      loadedImageRef.current = null;
      setLoadedImage(null);
      setPreviewDisplayMode('original');
      setHasExplicitlySliced(false);
      slicedCanvasesRef.current.clear();
      setSlicedResults(new Map());
      const canvas = imageCanvasRef.current;
      if (canvas) {
        const ctx = canvas.getContext('2d');
        if (ctx) {
          ctx.clearRect(0, 0, canvas.width, canvas.height);
        }
      }
      return;
    }

    const img = new Image();
    img.crossOrigin = 'anonymous';

    img.onload = () => {
      loadedImageRef.current = img;
      setLoadedImage(img);
      // Auto-fit grid to match AI sprite dimensions and content gutters
      autoFitDividers(img, currentCategory.cols, currentCategory.rows, keyColorType, keyColorHex);
      setPreviewDisplayMode('original');
      setHasExplicitlySliced(false);
      slicedCanvasesRef.current.clear();
      setSlicedResults(new Map());
    };

    img.onerror = () => {
      loadedImageRef.current = null;
      setLoadedImage(null);
    };

    img.src = userUploadedImageUrl;
  }, [userUploadedImageUrl]);

  // Handle Category / Mode Switching (Single Image vs Grid Sprite)
  const handleSelectCatId = useCallback((newCatId: string) => {
    setSelectedCatId(newCatId);
    const cat = GRID_CATEGORY_DEFINITIONS.find((c) => c.id === newCatId) || GRID_CATEGORY_DEFINITIONS[0];
    const img = loadedImage || loadedImageRef.current;
    if (img) {
      autoFitDividers(img, cat.cols, cat.rows, keyColorType, keyColorHex);
    }
    setSelectedCell(null);
    setHasExplicitlySliced(false);
    slicedCanvasesRef.current.clear();
    setSlicedResults(new Map());
    setPreviewDisplayMode('original');
  }, [loadedImage, autoFitDividers, keyColorType, keyColorHex]);

  // Redraw Canvas & Grid Dividers
  const redrawCanvas = useCallback((modeOverride?: 'transparent' | 'original') => {
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

    if (effectiveMode === 'transparent' && (hasExplicitlySliced || slicedCanvasesRef.current.size > 0)) {
      // Checkerboard transparency pattern
      const size = 16;
      for (let x = 0; x < canvas.width; x += size) {
        for (let y = 0; y < canvas.height; y += size) {
          ctx.fillStyle = (Math.floor(x / size) + Math.floor(y / size)) % 2 === 0 ? '#182030' : '#0c1220';
          ctx.fillRect(x, y, size, size);
        }
      }

      if (currentCategory.id === 'single_full_image') {
        const singleCanvas = slicedCanvasesRef.current.get('0_0');
        if (singleCanvas) {
          const pad = Math.max(0, paddingInset);
          const x0 = pad;
          const y0 = pad;
          const w = Math.max(10, canvas.width - pad * 2);
          const h = Math.max(10, canvas.height - pad * 2);
          ctx.drawImage(singleCanvas, x0, y0, w, h);
        }
      } else {
        currentCategory.cells.forEach((cell) => {
          const key = `${cell.row}_${cell.col}`;
          const cellCanvas = slicedCanvasesRef.current.get(key);
          if (cellCanvas) {
            const pad = Math.max(0, paddingInset);
            let x0 = 0, y0 = 0, w = canvas.width, h = canvas.height;
            if (colDividers.length > cell.col + 1 && rowDividers.length > cell.row + 1) {
              const rawX0 = colDividers[cell.col];
              const rawY0 = rowDividers[cell.row];
              const rawW = colDividers[cell.col + 1] - rawX0;
              const rawH = rowDividers[cell.row + 1] - rawY0;
              x0 = rawX0 + pad;
              y0 = rawY0 + pad;
              w = Math.max(10, rawW - pad * 2);
              h = Math.max(10, rawH - pad * 2);
            }
            ctx.drawImage(cellCanvas, x0, y0, w, h);
          }
        });
      }
    } else {
      ctx.drawImage(img, 0, 0);
    }

    // Draw Grid Lines (ONLY when NOT single full image)
    if (currentCategory.id !== 'single_full_image') {
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
    }
  }, [loadedImage, previewDisplayMode, hasExplicitlySliced, currentCategory, colDividers, rowDividers, selectedCell, slicedResults, paddingInset]);

  useEffect(() => {
    redrawCanvas();
  }, [redrawCanvas]);

  // Mouse drag & drop dividers and Eyedropper handler
  const handleCanvasMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = imageCanvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    const mouseX = (e.clientX - rect.left) * scaleX;
    const mouseY = (e.clientY - rect.top) * scaleY;

    // Eyedropper Mode: Sample exact pixel color directly from image canvas
    if (isEyedropperActive) {
      const ctx = canvas.getContext('2d', { willReadFrequently: true });
      if (ctx && mouseX >= 0 && mouseX < canvas.width && mouseY >= 0 && mouseY < canvas.height) {
        const pixel = ctx.getImageData(Math.floor(mouseX), Math.floor(mouseY), 1, 1).data;
        const hex = `#${((1 << 24) + (pixel[0] << 16) + (pixel[1] << 8) + pixel[2]).toString(16).slice(1).toUpperCase()}`;
        setIsEyedropperActive(false);
        setEyedropperHoverColor(null);
        if (eyedropperTarget === 'fringe') {
          setFringeColorType('custom');
          setFringeColorHex(hex);
          if (hasExplicitlySliced) {
            handleAutoSliceAndAssemble({ fringeColorType: 'custom', fringeColorHex: hex });
          }
        } else {
          setKeyColorType('custom');
          setKeyColorHex(hex);
          if (hasExplicitlySliced) {
            handleAutoSliceAndAssemble({ keyColorType: 'custom', keyColorHex: hex });
          }
        }
      }
      return;
    }

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

    // Eyedropper Live Preview Loupe
    if (isEyedropperActive) {
      const ctx = canvas.getContext('2d', { willReadFrequently: true });
      if (ctx && mouseX >= 0 && mouseX < canvas.width && mouseY >= 0 && mouseY < canvas.height) {
        const pixel = ctx.getImageData(Math.floor(mouseX), Math.floor(mouseY), 1, 1).data;
        const hex = `#${((1 << 24) + (pixel[0] << 16) + (pixel[1] << 8) + pixel[2]).toString(16).slice(1).toUpperCase()}`;
        setEyedropperHoverColor({
          hex,
          r: pixel[0],
          g: pixel[1],
          b: pixel[2],
          x: e.clientX,
          y: e.clientY,
        });
      }
      canvas.style.cursor = 'crosshair';
      return;
    }

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

  const handleCanvasMouseLeave = () => {
    setEyedropperHoverColor(null);
    draggingDividerRef.current = null;
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


  // Pure In-Browser Client-Side Fast BFS Matting (0 Server Required, 10ms, Zero VRAM, Protects Eye Whites)
  const handleFastBFSMatting = useCallback(() => {
    const img = loadedImageRef.current;
    if (!img) return;

    setIsProcessing(true);
    try {
      // 1. Create canvas for full image
      const fullCanvas = document.createElement('canvas');
      fullCanvas.width = img.width;
      fullCanvas.height = img.height;
      const ctx = fullCanvas.getContext('2d');
      if (!ctx) {
        setIsProcessing(false);
        return;
      }

      ctx.drawImage(img, 0, 0);

      // 2. Run pure client-side Smart BFS Floodfill
      processCellChromaAndDespeckle(ctx, img.width, img.height, {
        keyColorType: keyColorType,
        keyColorHex: keyColorHex,
        isolationMode: 'outer_only', // Guarantees eye whites & inner white clothes are 100% preserved!
        tolerance: tolerance || 38,
        feather: feather || 1,
        strokeWidth: strokeWidth || 0,
        strokeColorHex: strokeColorHex || '#000000',
        despeckleSize: despeckleSize || 18,
        whiteSpeckleSensitivity: whiteSpeckleSensitivity || 45,
        keepLargestIslandOnly: keepLargestIslandOnly || false,
      });

      const cleanDataUrl = fullCanvas.toDataURL('image/png');
      const cleanImg = new Image();
      cleanImg.onload = () => {
        loadedImageRef.current = cleanImg;
        setLoadedImage(cleanImg);
        setUserUploadedImageUrl(cleanDataUrl);

        // Slice cells
        const pad = Math.max(0, paddingInset);
        const updatedAssembly: Character2DAssembly = JSON.parse(JSON.stringify(currentAssembly));
        const nextResults = new Map<string, string>();

        for (const cell of currentCategory.cells) {
          if (colDividers.length <= cell.col + 1 || rowDividers.length <= cell.row + 1) continue;

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
          const cCtx = cellCanvas.getContext('2d');
          if (!cCtx) continue;
          cCtx.drawImage(cleanImg, x0, y0, w, h, 0, 0, w, h);
          const cellDataUrl = cellCanvas.toDataURL('image/png');

          const key = `${cell.row}_${cell.col}`;
          nextResults.set(key, cellDataUrl);
          slicedCanvasesRef.current.set(key, cellCanvas);

          if (cell.partSlot) {
            const hierarchy = PART_HIERARCHY_CONFIG[cell.partSlot];
            if (!updatedAssembly.parts[cell.partSlot]) {
              updatedAssembly.parts[cell.partSlot] = {
                path: cellDataUrl,
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
            if (cell.angle === 'front' || cell.col === 0) {
              part.path = cellDataUrl;
            }
            if (cell.angle) {
              if (!part.angles) part.angles = {};
              part.angles[cell.angle] = cellDataUrl;
            }
          }
        }

        setSlicedResults(nextResults);
        setHasExplicitlySliced(true);
        setPreviewDisplayMode('transparent');
        setAssemblySuccess(true);
        onApplyAssembly(updatedAssembly);
        if (threeEngineRef.current) {
          threeEngineRef.current.setAssembly(updatedAssembly);
        }
        redrawCanvas();
        setIsProcessing(false);
      };
      cleanImg.src = cleanDataUrl;
    } catch (err: any) {
      console.error('Fast BFS Matting error:', err);
      setIsProcessing(false);
    }
  }, [keyColorType, keyColorHex, tolerance, feather, strokeWidth, strokeColorHex, despeckleSize, whiteSpeckleSensitivity, keepLargestIslandOnly, paddingInset, currentAssembly, currentCategory, colDividers, rowDividers, onApplyAssembly, redrawCanvas]);

  // AI Matting Handler (BiRefNet / ISNet-Anime on GPU)
  const handleRunAIMatting = async () => {
    const img = loadedImageRef.current;
    if (!img) {
      alert('Vui lòng tải ảnh Sprite Sheet lên trước khi bóc tách!');
      return;
    }

    // Ping server check
    try {
      const ping = await fetch('http://127.0.0.1:5000/api/status');
      if (!ping.ok) throw new Error('offline');
    } catch {
      alert('Chưa kết nối được Server AI! Vui lòng khởi động file "run_ai_matting_server.bat" hoặc chạy lệnh "python server_ai_matting.py" trong thư mục dự án.');
      return;
    }

    setIsAIRunning(true);

    // ==========================================
    // MODE 1: TÁCH NỀN TOÀN BỘ ẢNH GỐC (FULL IMAGE)
    // ==========================================
    if (aiScope === 'full_image') {
      try {
        const fullCanvas = document.createElement('canvas');
        fullCanvas.width = img.width;
        fullCanvas.height = img.height;
        const fCtx = fullCanvas.getContext('2d');
        if (!fCtx) throw new Error('Không thể tạo canvas');
        fCtx.drawImage(img, 0, 0);
        const fullDataUrl = fullCanvas.toDataURL('image/png');

        const response = await fetch('http://127.0.0.1:5000/api/remove-bg', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            image: fullDataUrl,
            model: aiModel,
            alpha_matting: false,
          }),
        });

        const data = await response.json();
        if (!data.success || !data.result) {
          throw new Error(data.error || 'AI Server không trả về kết quả');
        }

        const cleanFullImg = new Image();
        await new Promise<void>((resolve, reject) => {
          cleanFullImg.onload = () => resolve();
          cleanFullImg.onerror = reject;
          cleanFullImg.src = data.result;
        });

        loadedImageRef.current = cleanFullImg;
        setUserUploadedImageUrl(data.result);

        const pad = Math.max(0, paddingInset);
        const updatedAssembly: Character2DAssembly = JSON.parse(JSON.stringify(currentAssembly));
        const nextResults = new Map<string, string>();

        for (const cell of currentCategory.cells) {
          if (colDividers.length <= cell.col + 1 || rowDividers.length <= cell.row + 1) continue;

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
          const cCtx = cellCanvas.getContext('2d');
          if (!cCtx) continue;
          cCtx.drawImage(cleanFullImg, x0, y0, w, h, 0, 0, w, h);
          const cellDataUrl = cellCanvas.toDataURL('image/png');

          const key = `${cell.row}_${cell.col}`;
          nextResults.set(key, cellDataUrl);
          slicedCanvasesRef.current.set(key, cellCanvas);

          if (cell.partSlot) {
            const hierarchy = PART_HIERARCHY_CONFIG[cell.partSlot];
            if (!updatedAssembly.parts[cell.partSlot]) {
              updatedAssembly.parts[cell.partSlot] = {
                path: cellDataUrl,
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
            if (cell.angle === 'front' || cell.col === 0) {
              part.path = cellDataUrl;
            }
            if (cell.angle) {
              if (!part.angles) part.angles = {};
              part.angles[cell.angle] = cellDataUrl;
            }
          }
        }

        setSlicedResults(nextResults);
        setHasExplicitlySliced(true);
        setPreviewDisplayMode('transparent');
        setAssemblySuccess(true);
        onApplyAssembly(updatedAssembly);
        if (threeEngineRef.current) {
          threeEngineRef.current.setAssembly(updatedAssembly);
        }
        redrawCanvas();
        return;
      } catch (err: any) {
        console.error('Full AI Matting error:', err);
        alert('Có lỗi xảy ra khi bóc tách toàn bộ ảnh: ' + (err.message || err));
        return;
      } finally {
        setIsAIRunning(false);
      }
    }

    // ==========================================
    // MODE 2: TÁCH THEO TỪNG Ô / Ô ĐANG CHỌN
    // ==========================================
    const targetCells = (aiScope === 'selected' && selectedCell)
      ? [selectedCell]
      : currentCategory.cells;

    const pad = Math.max(0, paddingInset);
    const updatedAssembly: Character2DAssembly = JSON.parse(JSON.stringify(currentAssembly));
    const nextResults = new Map(slicedResults);

    try {
      for (let i = 0; i < targetCells.length; i++) {
        const cell = targetCells[i];
        if (colDividers.length <= cell.col + 1 || rowDividers.length <= cell.row + 1) continue;

        const rawX0 = colDividers[cell.col];
        const rawY0 = rowDividers[cell.row];
        const rawW = colDividers[cell.col + 1] - rawX0;
        const rawH = rowDividers[cell.row + 1] - rawY0;
        const x0 = rawX0 + pad;
        const y0 = rawY0 + pad;
        const w = Math.max(10, rawW - pad * 2);
        const h = Math.max(10, rawH - pad * 2);

        // Crop cell to base64
        const cellCanvas = document.createElement('canvas');
        cellCanvas.width = w;
        cellCanvas.height = h;
        const ctx = cellCanvas.getContext('2d');
        if (!ctx) continue;
        ctx.drawImage(img, x0, y0, w, h, 0, 0, w, h);
        const inputDataUrl = cellCanvas.toDataURL('image/png');

        // Call AI Matting API
        const response = await fetch('http://127.0.0.1:5000/api/remove-bg', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            image: inputDataUrl,
            model: aiModel,
            alpha_matting: false,
          }),
        });

        const data = await response.json();
        if (data.success && data.result) {
          const key = `${cell.row}_${cell.col}`;
          nextResults.set(key, data.result);

          // Update cached canvas
          const pImg = new Image();
          await new Promise<void>((resolve) => {
            pImg.onload = () => {
              const c = document.createElement('canvas');
              c.width = w;
              c.height = h;
              const cCtx = c.getContext('2d');
              if (cCtx) cCtx.drawImage(pImg, 0, 0, w, h);
              slicedCanvasesRef.current.set(key, c);
              resolve();
            };
            pImg.src = data.result;
          });

          // Map to 3D Assembly
          if (cell.partSlot) {
            const hierarchy = PART_HIERARCHY_CONFIG[cell.partSlot];
            if (!updatedAssembly.parts[cell.partSlot]) {
              updatedAssembly.parts[cell.partSlot] = {
                path: data.result,
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
            if (cell.angle === 'front' || cell.col === 0) {
              part.path = data.result;
            }
            if (cell.angle) {
              if (!part.angles) part.angles = {};
              part.angles[cell.angle] = data.result;
            }
          }
        }
      }

      setSlicedResults(nextResults);
      setHasExplicitlySliced(true);
      setPreviewDisplayMode('transparent');
      setAssemblySuccess(true);
      onApplyAssembly(updatedAssembly);
      if (threeEngineRef.current) {
        threeEngineRef.current.setAssembly(updatedAssembly);
      }
      redrawCanvas();
    } catch (err: any) {
      console.error('AI Matting error:', err);
      alert('Có lỗi xảy ra khi bóc tách bằng AI: ' + (err.message || err));
    } finally {
      setIsAIRunning(false);
    }
  };

  // Auto Slice & Assemble Algorithm using High-Performance Chroma & Despeckle Processor
  const handleAutoSliceAndAssemble = useCallback((overrides?: Partial<Parameters<typeof processCellChromaAndDespeckle>[3]>) => {
    const img = loadedImageRef.current;
    if (!img) return;

    const effTol = overrides?.tolerance !== undefined ? overrides.tolerance : tolerance;
    const effFeather = overrides?.feather !== undefined ? overrides.feather : feather;
    const effShadowRetention = overrides?.shadowRetention !== undefined ? overrides.shadowRetention : shadowRetention;
    const effStrokeW = overrides?.strokeWidth !== undefined ? overrides.strokeWidth : strokeWidth;
    const effStrokeColor = overrides?.strokeColorHex !== undefined ? overrides.strokeColorHex : strokeColorHex;
    const effKeyType = overrides?.keyColorType !== undefined ? overrides.keyColorType : keyColorType;
    const effKeyHex = overrides?.keyColorHex !== undefined ? overrides.keyColorHex : keyColorHex;
    const effIsoMode = overrides?.isolationMode !== undefined ? overrides.isolationMode : isolationMode;
    const effDespeckle = overrides?.despeckleSize !== undefined ? overrides.despeckleSize : despeckleSize;
    const effWhiteSens = overrides?.whiteSpeckleSensitivity !== undefined ? overrides.whiteSpeckleSensitivity : whiteSpeckleSensitivity;
    const effKeepLargest = overrides?.keepLargestIslandOnly !== undefined ? overrides.keepLargestIslandOnly : keepLargestIslandOnly;
    const effFringeType = overrides?.fringeColorType !== undefined ? overrides.fringeColorType : fringeColorType;
    const effFringeHex = overrides?.fringeColorHex !== undefined ? overrides.fringeColorHex : fringeColorHex;
    const effDefringe = overrides?.defringeStrength !== undefined ? overrides.defringeStrength : defringeStrength;
    const effChoke = overrides?.edgeChoke !== undefined ? overrides.edgeChoke : edgeChoke;
    const effSmooth = overrides?.edgeSmooth !== undefined ? overrides.edgeSmooth : edgeSmooth;
    const effCleanupMode = overrides?.cleanupMode !== undefined ? overrides.cleanupMode : cleanupMode;

    const isDespeckleTab = overrides?.cleanupMode !== undefined || overrides?.defringeStrength !== undefined || overrides?.edgeChoke !== undefined || overrides?.edgeSmooth !== undefined || overrides?.despeckleSize !== undefined || bgCleanupSubTab === 'despeckle';
    const activeDefringe = isDespeckleTab ? effDefringe : (overrides?.defringeStrength ?? 0);
    const activeChoke = isDespeckleTab ? effChoke : (overrides?.edgeChoke ?? 0);
    const activeSmooth = isDespeckleTab ? effSmooth : (overrides?.edgeSmooth ?? 0);
    const activeDespeckle = isDespeckleTab ? effDespeckle : (overrides?.despeckleSize ?? 0);
    const activeWhiteSens = isDespeckleTab ? effWhiteSens : (overrides?.whiteSpeckleSensitivity ?? 0);
    const activeKeepLargest = isDespeckleTab ? effKeepLargest : (overrides?.keepLargestIslandOnly ?? false);

    setIsProcessing(true);
    setTimeout(() => {
      const results = new Map<string, string>();
      const updatedAssembly: Character2DAssembly = JSON.parse(JSON.stringify(currentAssembly));

      currentCategory.cells.forEach((cell) => {
        let rawX0 = 0, rawY0 = 0, rawW = img.width, rawH = img.height;
        if (colDividers.length > cell.col + 1 && rowDividers.length > cell.row + 1) {
          rawX0 = colDividers[cell.col];
          rawY0 = rowDividers[cell.row];
          rawW = colDividers[cell.col + 1] - rawX0;
          rawH = rowDividers[cell.row + 1] - rawY0;
        } else if (currentCategory.id !== 'single_full_image') {
          return;
        }

        const pad = Math.max(0, paddingInset);
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
        // Draw from active base image (with padding inset)
        ctx.drawImage(img, x0, y0, w, h, 0, 0, w, h);

        // Run full Chroma Key, Feathering, Despeckle & Noise Filtering with strict Tab-Isolated values
        processCellChromaAndDespeckle(ctx, w, h, {
          keyColorType: effKeyType,
          keyColorHex: effKeyHex,
          isolationMode: effIsoMode,
          tolerance: effTol,
          feather: effFeather,
          shadowRetention: effShadowRetention,
          strokeWidth: effStrokeW,
          strokeColorHex: effStrokeColor,
          despeckleSize: activeDespeckle,
          whiteSpeckleSensitivity: activeWhiteSens,
          keepLargestIslandOnly: activeKeepLargest,
          fringeColorType: effFringeType,
          fringeColorHex: effFringeHex,
          defringeStrength: activeDefringe,
          edgeChoke: activeChoke,
          edgeSmooth: activeSmooth,
          cleanupMode: effCleanupMode,
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
      redrawCanvas('transparent');
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
    paddingInset,
    onApplyAssembly,
    redrawCanvas,
  ]);

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

  // Commit current transparent slice result as the new active base image
  const handleCommitAsNewBase = useCallback(() => {
    const img = loadedImageRef.current;
    if (!img || (!hasExplicitlySliced && slicedCanvasesRef.current.size === 0)) return;

    // 1. Create a composite canvas of current full image / all grid slices
    const fullCanvas = document.createElement('canvas');
    fullCanvas.width = img.width;
    fullCanvas.height = img.height;
    const fCtx = fullCanvas.getContext('2d');
    if (!fCtx) return;

    fCtx.clearRect(0, 0, fullCanvas.width, fullCanvas.height);

    if (currentCategory.id === 'single_full_image') {
      const single = slicedCanvasesRef.current.get('0_0');
      if (single) {
        const pad = Math.max(0, paddingInset);
        fCtx.drawImage(single, pad, pad, Math.max(10, fullCanvas.width - pad * 2), Math.max(10, fullCanvas.height - pad * 2));
      }
    } else {
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
          fCtx.drawImage(cellCanvas, x0, y0, w, h);
        }
      });
    }

    const newBaseUrl = fullCanvas.toDataURL('image/png');

    // 2. Load this new base image into loadedImageRef
    const newImg = new Image();
    newImg.crossOrigin = 'anonymous';
    newImg.onload = () => {
      loadedImageRef.current = newImg;
      setLoadedImage(newImg);
      setUserUploadedImageUrl(newBaseUrl);

      // 3. Pause live dynamic slicing until user initiates next pass
      setHasExplicitlySliced(false);
      slicedCanvasesRef.current.clear();
      setSlicedResults(new Map());
      setPreviewDisplayMode('original');

      // 4. Reset sliders to neutral defaults to avoid compounding filter damage
      setFeather(0);
      setShadowRetention(0);
      setStrokeWidth(0);
      setEdgeChoke(0);
      setDespeckleSize(0);
      setWhiteSpeckleSensitivity(0);
      setKeepLargestIslandOnly(false);

      const canvas = imageCanvasRef.current;
      if (canvas) {
        const ctx = canvas.getContext('2d');
        if (ctx) {
          ctx.clearRect(0, 0, canvas.width, canvas.height);
          ctx.drawImage(newImg, 0, 0);
        }
      }
    };
    newImg.src = newBaseUrl;
  }, [hasExplicitlySliced, currentCategory, colDividers, rowDividers, paddingInset]);

  const handleTogglePreviewDisplayMode = useCallback((mode: 'transparent' | 'original') => {
    setPreviewDisplayMode(mode);
    if (mode === 'transparent') {
      if (!hasExplicitlySliced || slicedCanvasesRef.current.size === 0) {
        handleAutoSliceAndAssemble();
      } else {
        redrawCanvas('transparent');
      }
    } else {
      redrawCanvas('original');
    }
  }, [hasExplicitlySliced, handleAutoSliceAndAssemble, redrawCanvas]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', gap: 8, padding: 8, background: '#040711', overflow: 'hidden' }}>
      {/* Main 3-Column Studio Grid: 410px Spacious Sidebar, 1fr Interactive Canvas, 380px 3D Preview */}
      <div style={{ flex: 1, display: 'grid', gridTemplateColumns: '410px 1fr 380px', gap: 10, minHeight: 0 }}>
        {/* Left Column: Slicer Controls & Filters */}
        <SlicerSidebarControls
          selectedCatId={selectedCatId}
          onSelectCatId={handleSelectCatId}
          userUploadedImageUrl={userUploadedImageUrl}
          onFileUpload={(e) => {
            const file = e.target.files?.[0];
            if (file) {
              const url = URL.createObjectURL(file);
              setUserUploadedImageUrl(url);
              setHasExplicitlySliced(false);
              setSlicedResults(new Map());
              slicedCanvasesRef.current.clear();
              setPreviewDisplayMode('original');
              setAssemblySuccess(false);
            }
          }}
          onResetToDemoImage={(key = 'chibi') => {
            if (key === 'irregular_ai') {
              setSelectedCatId('chibi_3x3');
              setUserUploadedImageUrl('/demo_ai_irregular_spritesheet.png');
              setHasExplicitlySliced(false);
              setSlicedResults(new Map());
              slicedCanvasesRef.current.clear();
              setPreviewDisplayMode('original');
              setAssemblySuccess(false);
              return;
            }
            setActiveDemoKey(key as any);
            const bg = keyColorType === 'pure_white' ? 'pure_white' : 'chroma_green';
            const demoUrl = generateDemoGridSpriteSheet(currentCategory.id, bg, key as any);
            setUserUploadedImageUrl(demoUrl);
            setHasExplicitlySliced(false);
            setSlicedResults(new Map());
            slicedCanvasesRef.current.clear();
            setPreviewDisplayMode('original');
            setAssemblySuccess(false);
          }}
          onClearImage={() => {
            setUserUploadedImageUrl(null);
            setHasExplicitlySliced(false);
            setSlicedResults(new Map());
            slicedCanvasesRef.current.clear();
            setPreviewDisplayMode('original');
            setAssemblySuccess(false);
          }}
          fileInputRef={fileInputRef}
          isEyedropperActive={isEyedropperActive}
          setIsEyedropperActive={setIsEyedropperActive}
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
          shadowRetention={shadowRetention}
          setShadowRetention={setShadowRetention}
          strokeWidth={strokeWidth}
          setStrokeWidth={setStrokeWidth}
          strokeColorHex={strokeColorHex}
          setStrokeColorHex={setStrokeColorHex}
          bgCleanupSubTab={bgCleanupSubTab}
          setBgCleanupSubTab={setBgCleanupSubTab}
          aiModel={aiModel}
          setAiModel={setAiModel}
          aiScope={aiScope}
          setAiScope={setAiScope}
          aiServerStatus={aiServerStatus}
          onRunAIMatting={handleRunAIMatting}
          onRunFastBFSMatting={handleFastBFSMatting}
          isAIRunning={isAIRunning}
          despeckleSize={despeckleSize}
          setDespeckleSize={setDespeckleSize}
          whiteSpeckleSensitivity={whiteSpeckleSensitivity}
          setWhiteSpeckleSensitivity={setWhiteSpeckleSensitivity}
          keepLargestIslandOnly={keepLargestIslandOnly}
          setKeepLargestIslandOnly={setKeepLargestIslandOnly}
          eyedropperTarget={eyedropperTarget}
          setEyedropperTarget={setEyedropperTarget}
          cleanupMode={cleanupMode}
          setCleanupMode={setCleanupMode}
          fringeColorType={fringeColorType}
          setFringeColorType={setFringeColorType}
          fringeColorHex={fringeColorHex}
          setFringeColorHex={setFringeColorHex}
          defringeStrength={defringeStrength}
          setDefringeStrength={setDefringeStrength}
          edgeChoke={edgeChoke}
          setEdgeChoke={setEdgeChoke}
          edgeSmooth={edgeSmooth}
          setEdgeSmooth={setEdgeSmooth}
          onRunDespeckleOnly={() => {
            handleAutoSliceAndAssemble();
          }}
          onApplyAsNewBaseImage={handleCommitAsNewBase}
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
            hasImage={!!loadedImage}
            isEyedropperActive={isEyedropperActive}
            eyedropperTarget={eyedropperTarget}
            eyedropperHoverColor={eyedropperHoverColor}
            previewDisplayMode={previewDisplayMode}
            setPreviewDisplayMode={setPreviewDisplayMode}
            onTogglePreviewDisplayMode={handleTogglePreviewDisplayMode}
            hasExplicitlySliced={hasExplicitlySliced}
            currentCategory={currentCategory}
            onAutoFitGrid={handleAutoFitGrid}
            onResetUniformGrid={handleResetUniformGrid}
            onMouseDown={handleCanvasMouseDown}
            onDoubleClick={handleCanvasDoubleClick}
            onMouseMove={handleCanvasMouseMove}
            onMouseLeave={handleCanvasMouseLeave}
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
