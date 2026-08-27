import React, { useState, useRef, useEffect, useCallback } from 'react';
import {
  Sparkles,
  Upload,
  Download,
  Copy,
  Check,
  Layers,
  ZoomIn,
  ZoomOut,
  Maximize2,
  RefreshCw,
  FileCode,
  ArrowRight,
  Sliders,
  Palette,
  Image as ImageIcon,
  Bone,
  Grid,
} from 'lucide-react';

interface ImageToSvgVectorizerTabProps {
  onTransferToRigAssembler?: (svgUrl: string) => void;
  onTransferToGridSlicer?: (svgUrl: string) => void;
}

export const ImageToSvgVectorizerTab: React.FC<ImageToSvgVectorizerTabProps> = ({
  onTransferToRigAssembler,
  onTransferToGridSlicer,
}) => {
  // Source & SVG state
  const [sourceImageUrl, setSourceImageUrl] = useState<string>('/demo_rig/hand_000_front.jpg');
  const [svgOutput, setSvgOutput] = useState<string | null>(null);
  const [svgDataUrl, setSvgDataUrl] = useState<string | null>(null);
  const [isConverting, setIsConverting] = useState<boolean>(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [copied, setCopied] = useState<boolean>(false);

  // VTracer Parameter Settings
  const [preset, setPreset] = useState<'ultra_match' | 'anime' | 'detailed' | 'flat' | 'lineart'>('ultra_match');
  const [colorPrecision, setColorPrecision] = useState<number>(8);
  const [filterSpeckle, setFilterSpeckle] = useState<number>(1);
  const [cornerThreshold, setCornerThreshold] = useState<number>(22);
  const [lengthThreshold, setLengthThreshold] = useState<number>(1.4);
  const [layerDifference, setLayerDifference] = useState<number>(2);
  const [edgeSmoothing, setEdgeSmoothing] = useState<number>(1.0);
  const [colorMode, setColorMode] = useState<'color' | 'binary'>('color');
  const [hierarchical, setHierarchical] = useState<'stacked' | 'cutout'>('stacked');

  // Viewport comparison state
  const [viewMode, setViewMode] = useState<'side_by_side' | 'svg_only' | 'raster_only' | 'split'>('side_by_side');
  const [splitPos, setSplitPos] = useState<number>(50); // 0..100%
  const [zoomLevel, setZoomLevel] = useState<number>(1.0);
  const [metaStats, setMetaStats] = useState<{ pathCount: number; sizeKb: number; timeMs: number } | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);

  // Sample quick images
  const sampleImages = [
    { label: '🖐️ Bàn Tay Xòe Anime', url: '/demo_rig/hand_000_front.jpg' },
    { label: '✊ Nắm Tay Anime', url: '/demo_rig/hand_045_three_quarter.jpg' },
    { label: '✌️ Chữ V Anime', url: '/demo_rig/hand_090_profile.jpg' },
    { label: '👤 Sau Lưng Anime', url: '/demo_rig/hand_180_back.jpg' },
  ];

  // Apply preset values
  const applyPreset = (p: 'ultra_match' | 'anime' | 'detailed' | 'flat' | 'lineart') => {
    setPreset(p);
    if (p === 'ultra_match') {
      setColorPrecision(8);
      setFilterSpeckle(1);
      setCornerThreshold(22);
      setLengthThreshold(1.4);
      setLayerDifference(2);
      setEdgeSmoothing(1.0);
      setColorMode('color');
      setHierarchical('stacked');
    } else if (p === 'anime') {
      setColorPrecision(8);
      setFilterSpeckle(2);
      setCornerThreshold(28);
      setLengthThreshold(2.0);
      setLayerDifference(5);
      setEdgeSmoothing(1.5);
      setColorMode('color');
      setHierarchical('stacked');
    } else if (p === 'detailed') {
      setColorPrecision(8);
      setFilterSpeckle(1);
      setCornerThreshold(24);
      setLengthThreshold(1.5);
      setLayerDifference(3);
      setEdgeSmoothing(1.0);
      setColorMode('color');
      setHierarchical('stacked');
    } else if (p === 'flat') {
      setColorPrecision(6);
      setFilterSpeckle(6);
      setCornerThreshold(60);
      setLengthThreshold(4.0);
      setLayerDifference(12);
      setEdgeSmoothing(2.0);
      setColorMode('color');
      setHierarchical('cutout');
    } else if (p === 'lineart') {
      setColorPrecision(2);
      setFilterSpeckle(4);
      setCornerThreshold(35);
      setLengthThreshold(2.5);
      setLayerDifference(16);
      setEdgeSmoothing(1.5);
      setColorMode('binary');
      setHierarchical('stacked');
    }
  };

  // Helper to convert any image URL/path to base64 DataURL
  const imageToBase64 = async (src: string): Promise<string> => {
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

  // ─── Convert Image to SVG via VTracer Endpoint ────────────────────
  const handleVectorize = useCallback(async () => {
    if (!sourceImageUrl) return;
    setIsConverting(true);
    setErrorMsg(null);
    const startTime = performance.now();

    try {
      // Ensure image is converted to Base64 before sending
      const base64Data = await imageToBase64(sourceImageUrl);

      // 1. Try Python Sidecar VTracer API (Fastest & Highest Quality)
      const res = await fetch('http://127.0.0.1:5050/api/vectorize', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          image: base64Data,
          colorPrecision,
          filterSpeckle,
          cornerThreshold,
          lengthThreshold,
          layerDifference,
          edgeSmoothing,
          colorMode,
          hierarchical,
        }),
      });

      if (res.ok) {
        const data = await res.json();
        if (data.success && data.svg) {
          const elapsed = Math.round(performance.now() - startTime);
          setSvgOutput(data.svg);
          const blob = new Blob([data.svg], { type: 'image/svg+xml' });
          const url = URL.createObjectURL(blob);
          setSvgDataUrl(url);

          // Calculate path count
          const pathMatches = data.svg.match(/<path/g);
          const pathCount = pathMatches ? pathMatches.length : 0;
          setMetaStats({
            pathCount,
            sizeKb: Math.round((data.sizeBytes / 1024) * 10) / 10,
            timeMs: elapsed,
          });
          setIsConverting(false);
          return;
        } else {
          throw new Error(data.error || 'Server VTracer trả về lỗi');
        }
      }

      throw new Error(`Server VTracer phản hồi mã lỗi ${res.status}`);
    } catch (err: any) {
      console.warn('VTracer conversion error:', err);
      setErrorMsg(err.message || 'Lỗi khi chuyển đổi SVG');
      setIsConverting(false);
    }
  }, [sourceImageUrl, colorPrecision, filterSpeckle, cornerThreshold, lengthThreshold, layerDifference, colorMode, hierarchical]);

  // Auto-convert on initial load
  useEffect(() => {
    handleVectorize();
  }, []);

  // Handle image file upload
  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (event) => {
      if (event.target?.result) {
        setSourceImageUrl(event.target.result as string);
        setSvgOutput(null);
        setSvgDataUrl(null);
      }
    };
    reader.readAsDataURL(file);
  };

  // Download SVG file
  const handleDownloadSvg = () => {
    if (!svgOutput) return;
    const blob = new Blob([svgOutput], { type: 'image/svg+xml;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `vtracer_vectorized_${Date.now()}.svg`;
    a.click();
    URL.revokeObjectURL(url);
  };

  // Copy SVG Code
  const handleCopySvgCode = () => {
    if (!svgOutput) return;
    navigator.clipboard.writeText(svgOutput);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div
      style={{
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        background: '#070b14',
        color: '#f8fafc',
        overflow: 'hidden',
      }}
    >
      {/* ─── TOP TOOLBAR ────────────────────────────────────────── */}
      <div
        style={{
          height: 48,
          background: 'rgba(15, 23, 42, 0.9)',
          borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 16px',
        }}
      >
        {/* Preset Selectors */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
            <Sparkles size={14} /> MẪU THIẾT LẬP:
          </span>
          {[
            { id: 'ultra_match', label: '🔥 Khớp Màu 100% (Ultra Match)' },
            { id: 'anime', label: '🌸 Anime / Manga' },
            { id: 'detailed', label: '🎨 Tranh Chi Tiết' },
            { id: 'flat', label: '🖌️ Mảng Phẳng / Logo' },
            { id: 'lineart', label: '🖋️ Nét Vẽ Line-Art' },
          ].map((p) => (
            <button
              key={p.id}
              onClick={() => applyPreset(p.id as any)}
              style={{
                padding: '4px 10px',
                borderRadius: 6,
                fontSize: 10,
                fontWeight: preset === p.id ? 700 : 600,
                border: preset === p.id ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.08)',
                background: preset === p.id ? 'rgba(56, 189, 248, 0.2)' : 'rgba(30, 41, 59, 0.5)',
                color: preset === p.id ? '#38bdf8' : '#94a3b8',
                cursor: 'pointer',
                transition: 'all 0.15s',
              }}
            >
              {p.label}
            </button>
          ))}
        </div>

        {/* Action Buttons */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <button
            onClick={handleVectorize}
            disabled={isConverting}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '6px 14px',
              borderRadius: 6,
              fontSize: 11,
              fontWeight: 700,
              border: 'none',
              background: isConverting
                ? '#475569'
                : 'linear-gradient(135deg, #0284c7, #06b6d4)',
              color: '#ffffff',
              cursor: isConverting ? 'not-allowed' : 'pointer',
              boxShadow: '0 4px 12px rgba(2, 132, 199, 0.35)',
            }}
          >
            <RefreshCw size={13} className={isConverting ? 'animate-spin' : ''} />
            {isConverting ? 'Đang Vector Hóa...' : '⚡ Chuyển Đổi VTracer'}
          </button>

          {svgOutput && (
            <>
              <button
                onClick={handleDownloadSvg}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 5,
                  padding: '6px 12px',
                  borderRadius: 6,
                  fontSize: 11,
                  fontWeight: 600,
                  border: '1px solid rgba(16, 185, 129, 0.4)',
                  background: 'rgba(16, 185, 129, 0.15)',
                  color: '#34d399',
                  cursor: 'pointer',
                }}
              >
                <Download size={13} /> Tải .SVG
              </button>

              <button
                onClick={handleCopySvgCode}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 5,
                  padding: '6px 12px',
                  borderRadius: 6,
                  fontSize: 11,
                  fontWeight: 600,
                  border: '1px solid rgba(255, 255, 255, 0.1)',
                  background: 'rgba(30, 41, 59, 0.5)',
                  color: copied ? '#34d399' : '#94a3b8',
                  cursor: 'pointer',
                }}
              >
                {copied ? <Check size={13} /> : <Copy size={13} />}
                {copied ? 'Đã Sao Chép!' : 'Copy Code'}
              </button>
            </>
          )}
        </div>
      </div>

      {/* ─── MAIN WORKSPACE (SIDEBAR + VIEWPORT) ─────────────────── */}
      <div style={{ flex: 1, display: 'grid', gridTemplateColumns: '320px 1fr', overflow: 'hidden' }}>
        
        {/* LEFT CONTROL SIDEBAR */}
        <div
          style={{
            background: 'rgba(15, 23, 42, 0.75)',
            borderRight: '1px solid rgba(255, 255, 255, 0.08)',
            padding: 16,
            display: 'flex',
            flexDirection: 'column',
            gap: 14,
            overflowY: 'auto',
          }}
        >
          {/* Upload Image Card */}
          <div
            style={{
              background: 'rgba(30, 41, 59, 0.4)',
              border: '1px dashed rgba(56, 189, 248, 0.4)',
              borderRadius: 10,
              padding: 14,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              gap: 8,
              cursor: 'pointer',
              transition: 'all 0.2s',
            }}
            onClick={() => fileInputRef.current?.click()}
          >
            <Upload size={24} color="#38bdf8" />
            <div style={{ fontSize: 11, fontWeight: 700, color: '#f8fafc' }}>
              Tải Ảnh Lên (PNG, JPG, WebP)
            </div>
            <div style={{ fontSize: 9, color: '#64748b' }}>Nhấp để chọn ảnh từ máy tính</div>
            <input
              type="file"
              ref={fileInputRef}
              onChange={handleFileUpload}
              accept="image/*"
              style={{ display: 'none' }}
            />
          </div>

          {/* Quick Sample Selector */}
          <div>
            <div style={{ fontSize: 10, fontWeight: 700, color: '#f59e0b', marginBottom: 6 }}>
              🖼️ HOẶC CHỌN ẢNH MẪU:
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 6 }}>
              {sampleImages.map((s, idx) => (
                <button
                  key={idx}
                  onClick={() => {
                    setSourceImageUrl(s.url);
                    setSvgOutput(null);
                    setSvgDataUrl(null);
                  }}
                  style={{
                    padding: '6px 8px',
                    borderRadius: 6,
                    fontSize: 9,
                    fontWeight: 600,
                    border: sourceImageUrl === s.url ? '1px solid #f59e0b' : '1px solid rgba(255, 255, 255, 0.06)',
                    background: sourceImageUrl === s.url ? 'rgba(245, 158, 11, 0.15)' : 'rgba(30, 41, 59, 0.4)',
                    color: sourceImageUrl === s.url ? '#fbbf24' : '#94a3b8',
                    cursor: 'pointer',
                    textAlign: 'left',
                    whiteSpace: 'nowrap',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                  }}
                >
                  {s.label}
                </button>
              ))}
            </div>
          </div>

          {/* VTracer Fine-Tuning Parameters */}
          <div
            style={{
              background: 'rgba(15, 23, 42, 0.6)',
              border: '1px solid rgba(255, 255, 255, 0.06)',
              borderRadius: 10,
              padding: 12,
              display: 'flex',
              flexDirection: 'column',
              gap: 12,
            }}
          >
            <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
              <Sliders size={13} /> THÔNG SỐ VTRACER (RUST)
            </div>

            {/* Color Precision */}
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
                <span>Độ Chính Xác Màu (Color Precision)</span>
                <strong style={{ color: '#38bdf8' }}>{colorPrecision}</strong>
              </div>
              <input
                type="range"
                min="2"
                max="16"
                value={colorPrecision}
                onChange={(e) => setColorPrecision(Number(e.target.value))}
                style={{ width: '100%', accentColor: '#0284c7', cursor: 'pointer' }}
              />
            </div>

            {/* Filter Speckle */}
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
                <span>Lọc Nhiễu Điểm Nhỏ (Speckle Filter)</span>
                <strong style={{ color: '#38bdf8' }}>{filterSpeckle} px</strong>
              </div>
              <input
                type="range"
                min="0"
                max="24"
                value={filterSpeckle}
                onChange={(e) => setFilterSpeckle(Number(e.target.value))}
                style={{ width: '100%', accentColor: '#0284c7', cursor: 'pointer' }}
              />
            </div>

            {/* Corner Threshold */}
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
                <span>Độ Nhọn Góc (Corner Threshold)</span>
                <strong style={{ color: '#38bdf8' }}>{cornerThreshold}°</strong>
              </div>
              <input
                type="range"
                min="10"
                max="180"
                value={cornerThreshold}
                onChange={(e) => setCornerThreshold(Number(e.target.value))}
                style={{ width: '100%', accentColor: '#0284c7', cursor: 'pointer' }}
              />
            </div>

            {/* Length Threshold */}
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
                <span>Độ Mượt Đoạn Cong (Spline Length)</span>
                <strong style={{ color: '#38bdf8' }}>{lengthThreshold}</strong>
              </div>
              <input
                type="range"
                min="1.0"
                max="8.0"
                step="0.5"
                value={lengthThreshold}
                onChange={(e) => setLengthThreshold(Number(e.target.value))}
                style={{ width: '100%', accentColor: '#0284c7', cursor: 'pointer' }}
              />
            </div>

            {/* Layer Difference (Gradient Smoothness) */}
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
                <span>Độ Mịn Chuyển Màu (Layer Difference)</span>
                <strong style={{ color: '#34d399' }}>{layerDifference} (Càng nhỏ càng mịn)</strong>
              </div>
              <input
                type="range"
                min="2"
                max="24"
                value={layerDifference}
                onChange={(e) => setLayerDifference(Number(e.target.value))}
                style={{ width: '100%', accentColor: '#10b981', cursor: 'pointer' }}
              />
            </div>

            {/* Edge Contour Smoothing (Mịn Viền Biên) */}
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
                <span>🪄 Mịn Viền Viền (Edge Smoothing)</span>
                <strong style={{ color: '#f59e0b' }}>{edgeSmoothing} px</strong>
              </div>
              <input
                type="range"
                min="0"
                max="5"
                step="0.5"
                value={edgeSmoothing}
                onChange={(e) => setEdgeSmoothing(Number(e.target.value))}
                style={{ width: '100%', accentColor: '#f59e0b', cursor: 'pointer' }}
              />
            </div>

            {/* Hierarchical Mode */}
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: 10 }}>
              <span style={{ color: '#94a3b8' }}>Xếp Lớp (Hierarchy):</span>
              <div style={{ display: 'flex', gap: 4 }}>
                <button
                  onClick={() => setHierarchical('stacked')}
                  style={{
                    padding: '3px 8px',
                    borderRadius: 4,
                    fontSize: 9,
                    fontWeight: hierarchical === 'stacked' ? 700 : 500,
                    border: 'none',
                    background: hierarchical === 'stacked' ? '#0284c7' : 'rgba(255,255,255,0.08)',
                    color: '#fff',
                    cursor: 'pointer',
                  }}
                >
                  Xếp Chồng
                </button>
                <button
                  onClick={() => setHierarchical('cutout')}
                  style={{
                    padding: '3px 8px',
                    borderRadius: 4,
                    fontSize: 9,
                    fontWeight: hierarchical === 'cutout' ? 700 : 500,
                    border: 'none',
                    background: hierarchical === 'cutout' ? '#0284c7' : 'rgba(255,255,255,0.08)',
                    color: '#fff',
                    cursor: 'pointer',
                  }}
                >
                  Cắt Rỗng
                </button>
              </div>
            </div>
          </div>

          {/* Quick Transfer Actions */}
          {svgDataUrl && (
            <div
              style={{
                background: 'rgba(139, 92, 246, 0.1)',
                border: '1px solid rgba(139, 92, 246, 0.3)',
                borderRadius: 10,
                padding: 12,
                display: 'flex',
                flexDirection: 'column',
                gap: 8,
              }}
            >
              <div style={{ fontSize: 10, fontWeight: 700, color: '#c4b5fd' }}>
                🚀 CHUYỂN SVG SANG CÁC TAB KHÁC:
              </div>
              {onTransferToRigAssembler && (
                <button
                  onClick={() => onTransferToRigAssembler(svgDataUrl)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '6px 10px',
                    borderRadius: 6,
                    fontSize: 10,
                    fontWeight: 600,
                    border: '1px solid rgba(245, 158, 11, 0.4)',
                    background: 'rgba(245, 158, 11, 0.15)',
                    color: '#fbbf24',
                    cursor: 'pointer',
                  }}
                >
                  <span style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                    <Bone size={12} /> Sang Tab 1.5 (Gắn Xương)
                  </span>
                  <ArrowRight size={12} />
                </button>
              )}
              {onTransferToGridSlicer && (
                <button
                  onClick={() => onTransferToGridSlicer(svgDataUrl)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '6px 10px',
                    borderRadius: 6,
                    fontSize: 10,
                    fontWeight: 600,
                    border: '1px solid rgba(2, 132, 199, 0.4)',
                    background: 'rgba(2, 132, 199, 0.15)',
                    color: '#38bdf8',
                    cursor: 'pointer',
                  }}
                >
                  <span style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                    <Grid size={12} /> Sang Tab 1 (Cắt Lưới 3D)
                  </span>
                  <ArrowRight size={12} />
                </button>
              )}
            </div>
          )}
        </div>

        {/* RIGHT DUAL VIEWPORT STAGE */}
        <div style={{ display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          
          {/* Viewport Sub-header */}
          <div
            style={{
              height: 38,
              background: 'rgba(15, 23, 42, 0.8)',
              borderBottom: '1px solid rgba(255, 255, 255, 0.06)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              padding: '0 16px',
            }}
          >
            {/* View Mode Switcher */}
            <div style={{ display: 'flex', gap: 4 }}>
              {[
                { id: 'side_by_side', label: '⚡ So Sánh 2 Bên' },
                { id: 'svg_only', label: '✨ Vector SVG' },
                { id: 'raster_only', label: '🖼️ Ảnh Gốc' },
              ].map((v) => (
                <button
                  key={v.id}
                  onClick={() => setViewMode(v.id as any)}
                  style={{
                    padding: '3px 10px',
                    borderRadius: 5,
                    fontSize: 10,
                    fontWeight: viewMode === v.id ? 700 : 500,
                    border: 'none',
                    background: viewMode === v.id ? '#0284c7' : 'transparent',
                    color: viewMode === v.id ? '#ffffff' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  {v.label}
                </button>
              ))}
            </div>

            {/* Metrics & Zoom */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              {metaStats && (
                <div style={{ display: 'flex', gap: 10, fontSize: 10, color: '#34d399', fontWeight: 600 }}>
                  <span>📦 {metaStats.sizeKb} KB</span>
                  <span>〰️ {metaStats.pathCount} Paths</span>
                  <span>⚡ {metaStats.timeMs} ms</span>
                </div>
              )}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                <button
                  onClick={() => setZoomLevel((z) => Math.max(0.5, z - 0.25))}
                  style={{
                    padding: '2px 6px',
                    borderRadius: 4,
                    border: '1px solid rgba(255,255,255,0.1)',
                    background: 'rgba(30,41,59,0.5)',
                    color: '#fff',
                    cursor: 'pointer',
                  }}
                >
                  <ZoomOut size={12} />
                </button>
                <span style={{ fontSize: 10, minWidth: 40, textAlign: 'center', color: '#94a3b8' }}>
                  {Math.round(zoomLevel * 100)}%
                </span>
                <button
                  onClick={() => setZoomLevel((z) => Math.min(3.0, z + 0.25))}
                  style={{
                    padding: '2px 6px',
                    borderRadius: 4,
                    border: '1px solid rgba(255,255,255,0.1)',
                    background: 'rgba(30,41,59,0.5)',
                    color: '#fff',
                    cursor: 'pointer',
                  }}
                >
                  <ZoomIn size={12} />
                </button>
              </div>
            </div>
          </div>

          {/* Viewport Canvas Stage */}
          <div
            style={{
              flex: 1,
              background: 'radial-gradient(circle at center, #101726 0%, #060911 100%)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              overflow: 'auto',
              padding: 20,
              position: 'relative',
            }}
          >
            {/* Side-by-Side Mode */}
            {viewMode === 'side_by_side' && (
              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: '1fr 1fr',
                  gap: 20,
                  width: '100%',
                  height: '100%',
                  maxHeight: '700px',
                }}
              >
                {/* Left: Original Raster Image */}
                <div
                  style={{
                    background: 'rgba(15, 23, 42, 0.6)',
                    border: '1px solid rgba(255, 255, 255, 0.08)',
                    borderRadius: 12,
                    display: 'flex',
                    flexDirection: 'column',
                    overflow: 'hidden',
                  }}
                >
                  <div
                    style={{
                      height: 28,
                      background: 'rgba(0,0,0,0.4)',
                      padding: '0 12px',
                      display: 'flex',
                      alignItems: 'center',
                      fontSize: 10,
                      fontWeight: 700,
                      color: '#94a3b8',
                    }}
                  >
                    🖼️ ẢNH GỐC (RASTER BITMAP)
                  </div>
                  <div
                    style={{
                      flex: 1,
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      padding: 16,
                      background: '#090d16',
                    }}
                  >
                    <img
                      src={sourceImageUrl}
                      alt="Source"
                      style={{
                        maxWidth: '90%',
                        maxHeight: '520px',
                        objectFit: 'contain',
                        transform: `scale(${zoomLevel})`,
                        transition: 'transform 0.1s',
                      }}
                    />
                  </div>
                </div>

                {/* Right: Vector SVG */}
                <div
                  style={{
                    background: 'rgba(15, 23, 42, 0.6)',
                    border: '1px solid rgba(16, 185, 129, 0.3)',
                    borderRadius: 12,
                    display: 'flex',
                    flexDirection: 'column',
                    overflow: 'hidden',
                  }}
                >
                  <div
                    style={{
                      height: 28,
                      background: 'rgba(16, 185, 129, 0.15)',
                      padding: '0 12px',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      fontSize: 10,
                      fontWeight: 700,
                      color: '#34d399',
                    }}
                  >
                    <span>✨ VECTOR SVG (VTRACER SPLINE)</span>
                    <span style={{ fontSize: 9, opacity: 0.8 }}>Phóng to vô hạn không vỡ</span>
                  </div>
                  <div
                    style={{
                      flex: 1,
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      padding: 16,
                      background: '#ffffff',
                    }}
                  >
                    {isConverting ? (
                      <div style={{ textAlign: 'center', color: '#0284c7' }}>
                        <RefreshCw size={32} className="animate-spin" style={{ margin: '0 auto 8px' }} />
                        <div style={{ fontSize: 12, fontWeight: 700 }}>Đang chạy thuật toán VTracer...</div>
                      </div>
                    ) : errorMsg ? (
                      <div style={{ textAlign: 'center', color: '#ef4444', padding: 20 }}>
                        <div style={{ fontSize: 13, fontWeight: 700, marginBottom: 4 }}>⚠️ Lỗi chuyển đổi</div>
                        <div style={{ fontSize: 11, opacity: 0.9 }}>{errorMsg}</div>
                      </div>
                    ) : svgDataUrl ? (
                      <img
                        src={svgDataUrl}
                        alt="Vector SVG"
                        style={{
                          maxWidth: '90%',
                          maxHeight: '520px',
                          objectFit: 'contain',
                          transform: `scale(${zoomLevel})`,
                          transition: 'transform 0.1s',
                        }}
                      />
                    ) : (
                      <div style={{ color: '#64748b', fontSize: 11 }}>Nhấn "Chuyển Đổi VTracer" để tạo SVG</div>
                    )}
                  </div>
                </div>
              </div>
            )}

            {/* SVG Only Mode */}
            {viewMode === 'svg_only' && svgDataUrl && (
              <div
                style={{
                  width: '100%',
                  height: '100%',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  background: '#ffffff',
                  borderRadius: 12,
                  padding: 20,
                }}
              >
                <img
                  src={svgDataUrl}
                  alt="SVG Full View"
                  style={{
                    maxWidth: '95%',
                    maxHeight: '620px',
                    objectFit: 'contain',
                    transform: `scale(${zoomLevel})`,
                    transition: 'transform 0.1s',
                  }}
                />
              </div>
            )}

            {/* Raster Only Mode */}
            {viewMode === 'raster_only' && (
              <div
                style={{
                  width: '100%',
                  height: '100%',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  background: '#090d16',
                  borderRadius: 12,
                  padding: 20,
                }}
              >
                <img
                  src={sourceImageUrl}
                  alt="Raster Full View"
                  style={{
                    maxWidth: '95%',
                    maxHeight: '620px',
                    objectFit: 'contain',
                    transform: `scale(${zoomLevel})`,
                    transition: 'transform 0.1s',
                  }}
                />
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
