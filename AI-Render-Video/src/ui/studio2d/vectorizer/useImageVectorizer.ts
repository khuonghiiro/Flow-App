import { useState, useRef, useEffect, useCallback } from 'react';
import { getSidecarApiUrl } from '../../../core/config/envConfig';
import {
  VectorizerPreset,
  VectorizerViewMode,
  VectorizerParams,
  VectorizerMetaStats,
  VECTORIZER_PRESETS,
} from './types';
import { traceImageToSvgClientSide } from './clientVectorizer';

// Helper to convert any image URL/path to base64 DataURL
export const convertImageToBase64 = async (src: string): Promise<string> => {
  if (src.startsWith('data:image')) return src;
  try {
    const res = await fetch(src);
    const blob = await res.blob();
    return await new Promise<string>((resolve, reject) => {
      const reader = new FileReader();
      reader.onloadend = () => resolve(reader.result as string);
      reader.onerror = reject;
      reader.readAsDataURL(blob);
    });
  } catch {
    // Fallback via HTML Image & Canvas
    return await new Promise<string>((resolve) => {
      const img = new Image();
      img.crossOrigin = 'anonymous';
      img.onload = () => {
        const canvas = document.createElement('canvas');
        canvas.width = img.naturalWidth || img.width || 512;
        canvas.height = img.naturalHeight || img.height || 512;
        const ctx = canvas.getContext('2d');
        if (ctx) {
          ctx.drawImage(img, 0, 0);
          resolve(canvas.toDataURL('image/png'));
        } else {
          resolve(src);
        }
      };
      img.onerror = () => resolve(src);
      img.src = src;
    });
  }
};

export const useImageVectorizer = (initialImageUrl: string = '/demo_rig/hand_000_front.jpg') => {
  const [sourceImageUrl, setSourceImageUrl] = useState<string>(initialImageUrl);
  const [svgOutput, setSvgOutput] = useState<string | null>(null);
  const [svgDataUrl, setSvgDataUrl] = useState<string | null>(null);
  const [isConverting, setIsConverting] = useState<boolean>(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [copied, setCopied] = useState<boolean>(false);

  // Preset & Params
  const [preset, setPreset] = useState<VectorizerPreset>('ultra_match');
  const [params, setParams] = useState<VectorizerParams>({
    colorPrecision: 8,
    filterSpeckle: 2,
    cornerThreshold: 28,
    lengthThreshold: 1.6,
    layerDifference: 6,
    edgeSmoothing: 0.0,
    colorMode: 'color',
    hierarchical: 'stacked',
  });

  // Viewport comparison state
  const [viewMode, setViewMode] = useState<VectorizerViewMode>('side_by_side');
  const [splitPos, setSplitPos] = useState<number>(50);
  const [zoomLevel, setZoomLevel] = useState<number>(1.0);
  const [metaStats, setMetaStats] = useState<VectorizerMetaStats | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);

  // Apply preset
  const applyPreset = useCallback((p: VectorizerPreset) => {
    setPreset(p);
    const found = VECTORIZER_PRESETS.find((item) => item.id === p);
    if (found) {
      setParams({ ...found.params });
    }
  }, []);

  const updateParam = useCallback(<K extends keyof VectorizerParams>(key: K, value: VectorizerParams[K]) => {
    setParams((prev) => ({ ...prev, [key]: value }));
  }, []);

  // Vectorize via VTracer endpoint with automatic Client-side Fallback
  const handleVectorize = useCallback(async () => {
    if (!sourceImageUrl) return;
    setIsConverting(true);
    setErrorMsg(null);
    const startTime = performance.now();

    let base64Data = sourceImageUrl;
    try {
      base64Data = await convertImageToBase64(sourceImageUrl);
    } catch (b64Err) {
      console.warn('Base64 conversion warning:', b64Err);
    }

    // 1. Primary: High-Precision Rust VTracer on local Python sidecar
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 3500);

      const res = await fetch(getSidecarApiUrl('/api/vectorize'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          image: base64Data,
          preset: preset,
          colorPrecision: params.colorPrecision,
          filterSpeckle: params.filterSpeckle,
          cornerThreshold: params.cornerThreshold,
          lengthThreshold: params.lengthThreshold,
          layerDifference: params.layerDifference,
          edgeSmoothing: params.edgeSmoothing,
          colorMode: params.colorMode,
          hierarchical: params.hierarchical,
        }),
        signal: controller.signal,
      });
      clearTimeout(timeoutId);

      if (res.ok) {
        const data = await res.json();
        if (data.success && data.svg) {
          const elapsed = Math.round(performance.now() - startTime);
          setSvgOutput(data.svg);
          const blob = new Blob([data.svg], { type: 'image/svg+xml' });
          const url = URL.createObjectURL(blob);
          setSvgDataUrl(url);

          const pathMatches = data.svg.match(/<path/g);
          const pathCount = pathMatches ? pathMatches.length : 0;
          setMetaStats({
            pathCount,
            sizeKb: Math.round(((data.sizeBytes || data.svg.length) / 1024) * 10) / 10,
            timeMs: elapsed,
            engine: data.engine || 'Hierarchical VectorAI Engine',
          });
          setIsConverting(false);
          return;
        }
      }
    } catch (sidecarErr) {
      console.warn('Sidecar server offline/timeout, seamlessly falling back to client-side vectorizer:', sidecarErr);
    }

    // 2. Fallback: Client-Side ImageTracerJS Engine
    try {
      const clientSvg = await traceImageToSvgClientSide(base64Data, params);
      const elapsed = Math.round(performance.now() - startTime);
      setSvgOutput(clientSvg);
      const blob = new Blob([clientSvg], { type: 'image/svg+xml' });
      const url = URL.createObjectURL(blob);
      setSvgDataUrl(url);

      const pathMatches = clientSvg.match(/<path/g);
      const pathCount = pathMatches ? pathMatches.length : 0;
      setMetaStats({
        pathCount,
        sizeKb: Math.round((clientSvg.length / 1024) * 10) / 10,
        timeMs: elapsed,
        engine: 'ImageTracerJS (Client Engine)',
      });
      setIsConverting(false);
    } catch (clientErr: any) {
      console.error('Vectorization error (both server and client failed):', clientErr);
      setErrorMsg(clientErr.message || 'Lỗi khi chuyển đổi ảnh sang SVG');
      setIsConverting(false);
    }
  }, [sourceImageUrl, params]);

  // Initial vectorize on mount
  useEffect(() => {
    handleVectorize();
  }, []);

  // Upload handler (Supports PNG, JPG, WebP and Direct SVG Import)
  const handleFileUpload = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.type === 'image/svg+xml' || file.name.toLowerCase().endsWith('.svg')) {
      const reader = new FileReader();
      reader.onload = (event) => {
        const svgText = event.target?.result as string;
        if (svgText) {
          setSvgOutput(svgText);
          const blob = new Blob([svgText], { type: 'image/svg+xml' });
          const url = URL.createObjectURL(blob);
          setSvgDataUrl(url);
          setSourceImageUrl(url);
          const pathMatches = svgText.match(/<path/g);
          setMetaStats({
            pathCount: pathMatches ? pathMatches.length : 0,
            sizeKb: Math.round((svgText.length / 1024) * 10) / 10,
            timeMs: 0,
            engine: 'Imported AI SVG (Direct Vector)',
          });
        }
      };
      reader.readAsText(file);
      return;
    }

    const reader = new FileReader();
    reader.onload = (event) => {
      if (event.target?.result) {
        setSourceImageUrl(event.target.result as string);
        setSvgOutput(null);
        setSvgDataUrl(null);
      }
    };
    reader.readAsDataURL(file);
  }, []);

  // Select Sample Image
  const handleSelectSample = useCallback((url: string) => {
    setSourceImageUrl(url);
    setSvgOutput(null);
    setSvgDataUrl(null);
  }, []);

  // Download SVG
  const handleDownloadSvg = useCallback(() => {
    if (!svgOutput) return;
    const blob = new Blob([svgOutput], { type: 'image/svg+xml;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `vtracer_vectorized_${Date.now()}.svg`;
    a.click();
    URL.revokeObjectURL(url);
  }, [svgOutput]);

  // Copy SVG Code
  const handleCopySvgCode = useCallback(() => {
    if (!svgOutput) return;
    navigator.clipboard.writeText(svgOutput);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }, [svgOutput]);

  return {
    sourceImageUrl,
    setSourceImageUrl,
    svgOutput,
    svgDataUrl,
    isConverting,
    errorMsg,
    copied,
    preset,
    applyPreset,
    params,
    updateParam,
    viewMode,
    setViewMode,
    splitPos,
    setSplitPos,
    zoomLevel,
    setZoomLevel,
    metaStats,
    fileInputRef,
    handleVectorize,
    handleFileUpload,
    handleSelectSample,
    handleDownloadSvg,
    handleCopySvgCode,
  };
};
