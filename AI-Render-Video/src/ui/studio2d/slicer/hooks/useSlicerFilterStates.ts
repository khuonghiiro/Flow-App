import { useState, useMemo, useCallback } from 'react';
import { ChromaProcessOptions } from '../../../../core/utils/ChromaDespeckleProcessor';

export const DEFAULT_SLICER_FILTER_OPTIONS: ChromaProcessOptions = {
  keyColorType: 'chroma_green',
  keyColorHex: '#00ff00',
  isolationMode: 'all',
  tolerance: 35,
  feather: 2,
  shadowRetention: 100,
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
};

export function useSlicerFilterStates() {
  const [keyColorType, setKeyColorType] = useState<'chroma_green' | 'pure_white' | 'custom'>('chroma_green');
  const [keyColorHex, setKeyColorHex] = useState<string>('#00ff00');
  const [isEyedropperActive, setIsEyedropperActive] = useState<boolean>(false);
  const [eyedropperHoverColor, setEyedropperHoverColor] = useState<{ hex: string; r: number; g: number; b: number; x: number; y: number } | null>(null);
  const [isolationMode, setIsolationMode] = useState<'all' | 'outer_only'>('all');
  const [tolerance, setTolerance] = useState<number>(35);
  const [feather, setFeather] = useState<number>(2);
  const [shadowRetention, setShadowRetention] = useState<number>(100);
  const [strokeWidth, setStrokeWidth] = useState<number>(0);
  const [strokeColorHex, setStrokeColorHex] = useState<string>('#000000');
  const [bgCleanupSubTab, setBgCleanupSubTab] = useState<'chroma' | 'despeckle' | 'ai_matting'>('chroma');
  const [aiModel, setAiModel] = useState<string>('birefnet-general');
  const [aiScope, setAiScope] = useState<'full_image' | 'all' | 'selected'>('full_image');
  const [aiServerStatus, setAiServerStatus] = useState<'online' | 'offline' | 'checking'>('checking');
  const [despeckleSize, setDespeckleSize] = useState<number>(0);
  const [whiteSpeckleSensitivity, setWhiteSpeckleSensitivity] = useState<number>(0);
  const [keepLargestIslandOnly, setKeepLargestIslandOnly] = useState<boolean>(false);

  // Defringe & Edge Smoothing states
  const [eyedropperTarget, setEyedropperTarget] = useState<'chroma' | 'fringe' | 'smooth'>('chroma');
  const [cleanupMode, setCleanupMode] = useState<'all' | 'defringe' | 'smooth' | 'despeckle'>('all');
  const [fringeColorType, setFringeColorType] = useState<'chroma_green' | 'pure_white' | 'pure_black' | 'custom'>('chroma_green');
  const [fringeColorHex, setFringeColorHex] = useState<string>('#00ff00');
  const [defringeStrength, setDefringeStrength] = useState<number>(0);
  const [edgeChoke, setEdgeChoke] = useState<number>(0);
  const [edgeSmooth, setEdgeSmooth] = useState<number>(0);
  const [smoothColorType, setSmoothColorType] = useState<'black' | 'white' | 'auto' | 'custom'>('black');
  const [smoothColorHex, setSmoothColorHex] = useState<string>('#000000');
  const [paddingInset, setPaddingInset] = useState<number>(0);

  const applyFilterConfig = useCallback((cfg?: Partial<ChromaProcessOptions>) => {
    const opts = { ...DEFAULT_SLICER_FILTER_OPTIONS, ...cfg };
    setKeyColorType(opts.keyColorType || 'chroma_green');
    setKeyColorHex(opts.keyColorHex || '#00ff00');
    setIsolationMode(opts.isolationMode || 'all');
    setTolerance(opts.tolerance ?? 35);
    setFeather(opts.feather ?? 2);
    setShadowRetention(opts.shadowRetention ?? 100);
    setStrokeWidth(opts.strokeWidth ?? 0);
    setStrokeColorHex(opts.strokeColorHex || '#000000');
    setDespeckleSize(opts.despeckleSize ?? 0);
    setWhiteSpeckleSensitivity(opts.whiteSpeckleSensitivity ?? 0);
    setKeepLargestIslandOnly(opts.keepLargestIslandOnly ?? false);
    setFringeColorType(opts.fringeColorType || 'chroma_green');
    setFringeColorHex(opts.fringeColorHex || '#00ff00');
    setDefringeStrength(opts.defringeStrength ?? 0);
    setEdgeChoke(opts.edgeChoke ?? 0);
    setEdgeSmooth(opts.edgeSmooth ?? 0);
    setSmoothColorType(opts.smoothColorType || 'black');
    setSmoothColorHex(opts.smoothColorHex || '#000000');
    setCleanupMode(opts.cleanupMode || 'all');
  }, []);

  const resetToDefaults = useCallback(() => {
    applyFilterConfig(DEFAULT_SLICER_FILTER_OPTIONS);
  }, [applyFilterConfig]);

  const chromaOptions: ChromaProcessOptions = useMemo(
    () => ({
      keyColorType,
      keyColorHex: keyColorHex || '#00ff00',
      isolationMode,
      tolerance: tolerance ?? 35,
      feather: feather ?? 2,
      shadowRetention: shadowRetention ?? 100,
      strokeWidth: strokeWidth ?? 0,
      strokeColorHex: strokeColorHex || '#000000',
      despeckleSize: despeckleSize ?? 0,
      whiteSpeckleSensitivity: whiteSpeckleSensitivity ?? 0,
      keepLargestIslandOnly: keepLargestIslandOnly ?? false,
      fringeColorType,
      fringeColorHex: fringeColorHex || '#00ff00',
      defringeStrength: defringeStrength ?? 0,
      edgeChoke: edgeChoke ?? 0,
      edgeSmooth: edgeSmooth ?? 0,
      smoothColorType,
      smoothColorHex: smoothColorHex || '#000000',
      cleanupMode,
    }),
    [
      keyColorType,
      keyColorHex,
      isolationMode,
      tolerance,
      feather,
      shadowRetention,
      strokeWidth,
      strokeColorHex,
      despeckleSize,
      whiteSpeckleSensitivity,
      keepLargestIslandOnly,
      fringeColorType,
      fringeColorHex,
      defringeStrength,
      edgeChoke,
      edgeSmooth,
      smoothColorType,
      smoothColorHex,
      cleanupMode,
    ]
  );

  return {
    keyColorType,
    setKeyColorType,
    keyColorHex,
    setKeyColorHex,
    isEyedropperActive,
    setIsEyedropperActive,
    eyedropperHoverColor,
    setEyedropperHoverColor,
    isolationMode,
    setIsolationMode,
    tolerance,
    setTolerance,
    feather,
    setFeather,
    shadowRetention,
    setShadowRetention,
    strokeWidth,
    setStrokeWidth,
    strokeColorHex,
    setStrokeColorHex,
    bgCleanupSubTab,
    setBgCleanupSubTab,
    aiModel,
    setAiModel,
    aiScope,
    setAiScope,
    aiServerStatus,
    setAiServerStatus,
    despeckleSize,
    setDespeckleSize,
    whiteSpeckleSensitivity,
    setWhiteSpeckleSensitivity,
    keepLargestIslandOnly,
    setKeepLargestIslandOnly,
    eyedropperTarget,
    setEyedropperTarget,
    cleanupMode,
    setCleanupMode,
    fringeColorType,
    setFringeColorType,
    fringeColorHex,
    setFringeColorHex,
    defringeStrength,
    setDefringeStrength,
    edgeChoke,
    setEdgeChoke,
    edgeSmooth,
    setEdgeSmooth,
    smoothColorType,
    setSmoothColorType,
    smoothColorHex,
    setSmoothColorHex,
    paddingInset,
    setPaddingInset,
    chromaOptions,
    applyFilterConfig,
    resetToDefaults,
  };
}
