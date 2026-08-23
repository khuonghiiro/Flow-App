import React, { useState, useRef, useEffect, useCallback } from 'react';
import {
  Scissors,
  Sparkles,
  Crop,
  Download,
  Upload,
  Check,
  RotateCcw,
  Sliders,
  Eye,
  Layers,
  Save,
} from 'lucide-react';
import { Character2DPartType, StandardCropPreset } from '../../types/scene2d';
import { STANDARD_CROP_PRESETS } from '../../core/assets/Asset2DRegistry';

interface ImageSegmenterCropperProps {
  onApplyPartToAssembly?: (slot: Character2DPartType, dataUrl: string) => void;
}

export const ImageSegmenterCropper: React.FC<ImageSegmenterCropperProps> = ({
  onApplyPartToAssembly,
}) => {
  const [sourceImage, setSourceImage] = useState<string | null>(null);
  const [selectedPreset, setSelectedPreset] = useState<StandardCropPreset>(STANDARD_CROP_PRESETS[0]);
  const [targetSlot, setTargetSlot] = useState<Character2DPartType>('dau');

  // Background removal / chroma threshold parameters
  const [isRemoveBgActive, setIsRemoveBgActive] = useState<boolean>(true);
  const [tolerance, setTolerance] = useState<number>(35); // Color threshold tolerance 0-100
  const [feather, setFeather] = useState<number>(2); // Edge smoothing px
  const [keyColor, setKeyColor] = useState<string>('#ffffff'); // Default pure white background
  const [brightness, setBrightness] = useState<number>(100);
  const [contrast, setContrast] = useState<number>(100);

  // Crop rectangle state [x, y, width, height] in source image pixels
  const [cropRect, setCropRect] = useState<{ x: number; y: number; w: number; h: number }>({
    x: 50,
    y: 50,
    w: 200,
    h: 200,
  });

  const [isDragging, setIsDragging] = useState<boolean>(false);
  const [dragStart, setDragStart] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [previewResult, setPreviewResult] = useState<string | null>(null);
  const [appliedSuccess, setAppliedSuccess] = useState<boolean>(false);

  const imageCanvasRef = useRef<HTMLCanvasElement>(null);
  const resultCanvasRef = useRef<HTMLCanvasElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const loadedImageRef = useRef<HTMLImageElement | null>(null);

  // Load initial demo image if none loaded
  useEffect(() => {
    const defaultDemoUrl = 'data:image/svg+xml;utf8,' + encodeURIComponent(`
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 800" width="600" height="800">
        <rect width="600" height="800" fill="#ffffff"/>
        <!-- Head -->
        <circle cx="300" cy="220" r="110" fill="#ffdfba" stroke="#d4a373" stroke-width="4"/>
        <ellipse cx="260" cy="210" rx="16" ry="12" fill="#1e293b"/>
        <ellipse cx="340" cy="210" rx="16" ry="12" fill="#1e293b"/>
        <circle cx="264" cy="207" r="4" fill="#ffffff"/>
        <circle cx="344" cy="207" r="4" fill="#ffffff"/>
        <path d="M285 240 L300 240 L295 220" stroke="#d4a373" stroke-width="3" fill="none"/>
        <path d="M275 270 Q300 290 325 270" stroke="#be123c" stroke-width="4" fill="none"/>
        <!-- Hair -->
        <path d="M190 200 Q300 80 410 200 Q370 140 330 220 Q300 150 270 220 Q230 140 190 200 Z" fill="#1e293b"/>
        <!-- Torso -->
        <path d="M200 340 L400 340 L430 650 L170 650 Z" fill="#0284c7" stroke="#38bdf8" stroke-width="3"/>
        <rect x="220" y="460" width="160" height="30" fill="#fbbf24" rx="6"/>
      </svg>
    `);
    loadImageFromUrl(defaultDemoUrl);
  }, []);

  const loadImageFromUrl = (url: string) => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      loadedImageRef.current = img;
      setSourceImage(url);
      setCropRect({
        x: Math.round(img.width * 0.2),
        y: Math.round(img.height * 0.1),
        w: Math.round(img.width * 0.6),
        h: Math.round(img.width * 0.6),
      });
      processImageAndCrop();
    };
    img.src = url;
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = (ev) => {
        if (ev.target?.result) {
          loadImageFromUrl(ev.target.result as string);
        }
      };
      reader.readAsDataURL(file);
    }
  };

  // Preset selection handler
  const handleSelectPreset = (preset: StandardCropPreset) => {
    setSelectedPreset(preset);
    if (preset.category === 'nhan_vat' && preset.slot !== 'map_layer' && preset.slot !== 'prop' && preset.slot !== 'vfx') {
      setTargetSlot(preset.slot);
    }
    if (loadedImageRef.current) {
      const img = loadedImageRef.current;
      const targetW = Math.min(img.width * 0.8, preset.width);
      const targetH = targetW / preset.aspectRatio;
      setCropRect({
        x: Math.max(0, Math.round((img.width - targetW) / 2)),
        y: Math.max(0, Math.round((img.height - targetH) / 2)),
        w: Math.round(targetW),
        h: Math.round(targetH),
      });
    }
  };

  /**
   * Processes image: Removes background based on chroma key + tolerance, then extracts cropped bounding box
   */
  const processImageAndCrop = useCallback(() => {
    const img = loadedImageRef.current;
    if (!img) return;

    // 1. Draw source onto main canvas
    const canvas = imageCanvasRef.current;
    if (!canvas) return;
    canvas.width = img.width;
    canvas.height = img.height;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    ctx.drawImage(img, 0, 0);

    // 2. Perform Chroma / Background Removal
    if (isRemoveBgActive) {
      const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
      const data = imgData.data;

      // Parse keyColor (hex #ffffff -> rgb)
      const kr = parseInt(keyColor.slice(1, 3), 16) || 255;
      const kg = parseInt(keyColor.slice(3, 5), 16) || 255;
      const kb = parseInt(keyColor.slice(5, 7), 16) || 255;
      const tolDist = (tolerance / 100) * 441.67; // max Euclidean distance between colors is sqrt(255^2*3) ~ 441.67

      for (let i = 0; i < data.length; i += 4) {
        const r = data[i];
        const g = data[i + 1];
        const b = data[i + 2];

        // Euclidean distance in RGB color space
        const dist = Math.sqrt((r - kr) ** 2 + (g - kg) ** 2 + (b - kb) ** 2);

        if (dist < tolDist) {
          // Transparent background
          data[i + 3] = 0;
        } else {
          // Smooth edge alpha feathering
          if (dist < tolDist + feather * 10) {
            const alphaFactor = (dist - tolDist) / (feather * 10);
            data[i + 3] = Math.round(data[i + 3] * alphaFactor);
          }
          
          // Auto Despill (Khử ám xanh) nếu Key Color là Xanh lá (#00FF00)
          // Rất quan trọng để các viền tóc không bị dính vệt xanh bán trong suốt
          if (kg > kr + 50 && kg > kb + 50 && data[i + 3] > 0) {
            const maxRB = Math.max(r, b);
            if (g > maxRB) {
              data[i + 1] = maxRB; // Ép kênh Green xuống bằng Red hoặc Blue
            }
          }
        }
      }
      ctx.putImageData(imgData, 0, 0);
    }

    // 3. Render Cropped Result Canvas
    const resultCanvas = resultCanvasRef.current;
    if (resultCanvas) {
      resultCanvas.width = Math.max(1, cropRect.w);
      resultCanvas.height = Math.max(1, cropRect.h);
      const rCtx = resultCanvas.getContext('2d');
      if (rCtx) {
        rCtx.clearRect(0, 0, resultCanvas.width, resultCanvas.height);
        rCtx.drawImage(
          canvas,
          cropRect.x,
          cropRect.y,
          cropRect.w,
          cropRect.h,
          0,
          0,
          resultCanvas.width,
          resultCanvas.height
        );
        const dataUrl = resultCanvas.toDataURL('image/png');
        setPreviewResult(dataUrl);
      }
    }
  }, [cropRect, isRemoveBgActive, tolerance, feather, keyColor]);

  useEffect(() => {
    processImageAndCrop();
  }, [processImageAndCrop]);

  const handleApplyToAssembly = () => {
    if (previewResult && onApplyPartToAssembly) {
      onApplyPartToAssembly(targetSlot, previewResult);
      setAppliedSuccess(true);
      setTimeout(() => setAppliedSuccess(false), 2000);
    }
  };

  const handleDownloadCropped = () => {
    if (!previewResult) return;
    const a = document.createElement('a');
    a.href = previewResult;
    a.download = `part_${targetSlot}_${Date.now()}.png`;
    a.click();
  };

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '320px 1fr 340px', gap: 16, height: '100%', overflow: 'hidden' }}>
      {/* Hidden File Input */}
      <input type="file" ref={fileInputRef} accept="image/*" style={{ display: 'none' }} onChange={handleFileUpload} />

      {/* ─── LEFT COLUMN: Presets & Controls ─────────────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 14, overflowY: 'auto', paddingRight: 4 }}>
        {/* Upload Button */}
        <div style={{ display: 'flex', gap: 8 }}>
          <button
            onClick={() => fileInputRef.current?.click()}
            style={{
              flex: 1,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 8,
              padding: '10px 14px',
              borderRadius: 8,
              background: 'linear-gradient(135deg, #0284c7, #2563eb)',
              color: '#ffffff',
              fontWeight: 600,
              fontSize: 12,
              border: 'none',
              cursor: 'pointer',
              boxShadow: '0 4px 12px rgba(2, 132, 199, 0.3)',
            }}
          >
            <Upload size={14} /> Tải Ảnh Lên Để Tách
          </button>
        </div>

        {/* Standard Preset Selector */}
        <div style={{ background: 'rgba(15, 23, 42, 0.7)', padding: 12, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)' }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', marginBottom: 8, display: 'flex', alignItems: 'center', gap: 6 }}>
            <Crop size={13} /> KHUNG CẮT CHUẨN (PRESETS)
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            {STANDARD_CROP_PRESETS.map((preset) => (
              <button
                key={preset.id}
                onClick={() => handleSelectPreset(preset)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '6px 10px',
                  borderRadius: 6,
                  fontSize: 11,
                  textAlign: 'left',
                  background: selectedPreset.id === preset.id ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.03)',
                  border: selectedPreset.id === preset.id ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.05)',
                  color: selectedPreset.id === preset.id ? '#ffffff' : '#94a3b8',
                  cursor: 'pointer',
                }}
              >
                <span>{preset.label}</span>
                <span style={{ fontSize: 10, color: '#64748b' }}>{preset.width}x{preset.height}</span>
              </button>
            ))}
          </div>
        </div>

        {/* Background Removal Controls */}
        <div style={{ background: 'rgba(15, 23, 42, 0.7)', padding: 12, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
            <div style={{ fontSize: 11, fontWeight: 700, color: '#a855f7', display: 'flex', alignItems: 'center', gap: 6 }}>
              <Sparkles size={13} /> TÁCH NỀN TỰ ĐỘNG (CHROMA)
            </div>
            <input
              type="checkbox"
              checked={isRemoveBgActive}
              onChange={(e) => setIsRemoveBgActive(e.target.checked)}
              style={{ cursor: 'pointer' }}
            />
          </div>

          {isRemoveBgActive && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
                  <span>Ngưỡng màu nền (Tolerance):</span>
                  <span style={{ color: '#c084fc', fontWeight: 600 }}>{tolerance}%</span>
                </div>
                <input
                  type="range"
                  min="0"
                  max="100"
                  value={tolerance}
                  onChange={(e) => setTolerance(parseInt(e.target.value))}
                  onMouseUp={processImageAndCrop}
                  onTouchEnd={processImageAndCrop}
                  style={{ width: '100%', cursor: 'pointer' }}
                />
              </div>

              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
                  <span>Độ mềm viền (Feather):</span>
                  <span style={{ color: '#c084fc', fontWeight: 600 }}>{feather}px</span>
                </div>
                <input
                  type="range"
                  min="0"
                  max="20"
                  value={feather}
                  onChange={(e) => setFeather(parseInt(e.target.value))}
                  onMouseUp={processImageAndCrop}
                  onTouchEnd={processImageAndCrop}
                  style={{ width: '100%', cursor: 'pointer' }}
                />
              </div>

              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span style={{ fontSize: 10, color: '#94a3b8' }}>Màu nền cần xóa:</span>
                <input
                  type="color"
                  value={keyColor}
                  onChange={(e) => setKeyColor(e.target.value)}
                  style={{ width: 36, height: 24, padding: 0, border: 'none', borderRadius: 4, cursor: 'pointer' }}
                />
              </div>
            </div>
          )}
        </div>
      </div>

      {/* ─── CENTER COLUMN: Interactive Cropping Area ────────────────── */}
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          background: 'rgba(11, 15, 25, 0.95)',
          borderRadius: 8,
          border: '1px solid rgba(255,255,255,0.08)',
          position: 'relative',
          overflow: 'hidden',
          padding: 16,
        }}
      >
        <div style={{ position: 'relative', maxWidth: '100%', maxHeight: '100%' }}>
          {/* Main Processing Canvas with Checkerboard Background for Alpha Preview */}
          <canvas
            ref={imageCanvasRef}
            style={{
              display: 'block',
              maxWidth: '100%',
              maxHeight: '520px',
              backgroundImage: 'linear-gradient(45deg, #1e293b 25%, transparent 25%), linear-gradient(-45deg, #1e293b 25%, transparent 25%), linear-gradient(45deg, transparent 75%, #1e293b 75%), linear-gradient(-45deg, transparent 75%, #1e293b 75%)',
              backgroundSize: '16px 16px',
              backgroundPosition: '0 0, 0 8px, 8px -8px, -8px 0px',
              backgroundColor: '#0f172a',
              borderRadius: 6,
              boxShadow: '0 8px 24px rgba(0,0,0,0.5)',
            }}
          />

          {/* Interactive Bounding Box Overlay */}
          {loadedImageRef.current && (
            <div
              style={{
                position: 'absolute',
                left: `${(cropRect.x / (loadedImageRef.current.width || 1)) * 100}%`,
                top: `${(cropRect.y / (loadedImageRef.current.height || 1)) * 100}%`,
                width: `${(cropRect.w / (loadedImageRef.current.width || 1)) * 100}%`,
                height: `${(cropRect.h / (loadedImageRef.current.height || 1)) * 100}%`,
                border: '2px solid #38bdf8',
                boxShadow: '0 0 12px rgba(56, 189, 248, 0.6), inset 0 0 8px rgba(56, 189, 248, 0.2)',
                boxSizing: 'border-box',
                cursor: 'move',
                pointerEvents: 'auto',
              }}
              onMouseDown={(e) => {
                setIsDragging(true);
                setDragStart({ x: e.clientX, y: e.clientY });
              }}
            >
              <div style={{ position: 'absolute', top: -20, left: 0, background: '#38bdf8', color: '#000', fontSize: 10, fontWeight: 700, padding: '1px 6px', borderRadius: 3 }}>
                {selectedPreset.label}
              </div>
            </div>
          )}
        </div>

        {/* Sliders for precise manual Box placement */}
        <div style={{ display: 'flex', gap: 12, marginTop: 12, width: '100%', maxWidth: 500 }}>
          <div style={{ flex: 1, fontSize: 10, color: '#94a3b8' }}>
            Vị trí X: {cropRect.x}px
            <input
              type="range"
              min="0"
              max={loadedImageRef.current?.width || 800}
              value={cropRect.x}
              onChange={(e) => setCropRect((prev) => ({ ...prev, x: parseInt(e.target.value) }))}
              style={{ width: '100%' }}
            />
          </div>
          <div style={{ flex: 1, fontSize: 10, color: '#94a3b8' }}>
            Vị trí Y: {cropRect.y}px
            <input
              type="range"
              min="0"
              max={loadedImageRef.current?.height || 800}
              value={cropRect.y}
              onChange={(e) => setCropRect((prev) => ({ ...prev, y: parseInt(e.target.value) }))}
              style={{ width: '100%' }}
            />
          </div>
          <div style={{ flex: 1, fontSize: 10, color: '#94a3b8' }}>
            Kích cỡ Size: {cropRect.w}px
            <input
              type="range"
              min="32"
              max={loadedImageRef.current?.width || 800}
              value={cropRect.w}
              onChange={(e) => {
                const nw = parseInt(e.target.value);
                setCropRect((prev) => ({ ...prev, w: nw, h: Math.round(nw / selectedPreset.aspectRatio) }));
              }}
              style={{ width: '100%' }}
            />
          </div>
        </div>
      </div>

      {/* ─── RIGHT COLUMN: Cropped Result & Assignment ────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12, background: 'rgba(15, 23, 42, 0.7)', padding: 14, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)' }}>
        <div style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Scissors size={14} /> KẾT QUẢ CẮT TRONG SUỐT
        </div>

        {/* Cropped Preview Display Canvas */}
        <div
          style={{
            flex: 1,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            minHeight: 200,
            borderRadius: 6,
            backgroundImage: 'linear-gradient(45deg, #1e293b 25%, transparent 25%), linear-gradient(-45deg, #1e293b 25%, transparent 25%), linear-gradient(45deg, transparent 75%, #1e293b 75%), linear-gradient(-45deg, transparent 75%, #1e293b 75%)',
            backgroundSize: '12px 12px',
            backgroundColor: '#0f172a',
            border: '1px solid rgba(255,255,255,0.1)',
            padding: 8,
          }}
        >
          <canvas ref={resultCanvasRef} style={{ maxWidth: '100%', maxHeight: '200px', objectFit: 'contain' }} />
        </div>

        {/* Target Slot Assignment */}
        <div>
          <label style={{ fontSize: 11, color: '#94a3b8', display: 'block', marginBottom: 4 }}>
            Gán vào vị trí linh kiện (Slot):
          </label>
          <select
            value={targetSlot}
            onChange={(e) => setTargetSlot(e.target.value as Character2DPartType)}
            style={{
              width: '100%',
              padding: '6px 8px',
              fontSize: 11,
              background: '#0f172a',
              color: '#ffffff',
              border: '1px solid rgba(255,255,255,0.2)',
              borderRadius: 6,
            }}
          >
            <option value="dau">Đầu & Cằm (dau)</option>
            <option value="mat">Mắt (mat)</option>
            <option value="mieng">Miệng (mieng)</option>
            <option value="mui">Mũi (mui)</option>
            <option value="toc_truoc">Tóc Trước (toc_truoc)</option>
            <option value="toc_sau">Tóc Sau (toc_sau)</option>
            <option value="than_co_ban">Thân Cơ Bản (than_co_ban)</option>
            <option value="canh_tay_trai">Cánh Tay Trái (canh_tay_trai)</option>
            <option value="canh_tay_phai">Cánh Tay Phải (canh_tay_phai)</option>
            <option value="cang_chan_trai">Cẳng Chân Trái (cang_chan_trai)</option>
            <option value="cang_chan_phai">Cẳng Chân Phải (cang_chan_phai)</option>
            <option value="trang_phuc">Trang Phục (trang_phuc)</option>
            <option value="vu_khi">Vũ Khí (vu_khi)</option>
          </select>
        </div>

        {/* Action Buttons */}
        <button
          onClick={handleApplyToAssembly}
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 8,
            padding: '10px 14px',
            borderRadius: 6,
            background: appliedSuccess ? '#22c55e' : 'linear-gradient(135deg, #10b981, #059669)',
            color: '#ffffff',
            fontWeight: 700,
            fontSize: 12,
            border: 'none',
            cursor: 'pointer',
            transition: 'all 0.2s',
            boxShadow: '0 4px 12px rgba(16, 185, 129, 0.3)',
          }}
        >
          {appliedSuccess ? <Check size={14} /> : <Layers size={14} />}
          {appliedSuccess ? 'Đã Gán Vào Bàn Lắp Ráp!' : 'Gán Linh Kiện Này Vào Nhân Vật'}
        </button>

        <button
          onClick={handleDownloadCropped}
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 8,
            padding: '8px 12px',
            borderRadius: 6,
            background: 'rgba(255,255,255,0.05)',
            border: '1px solid rgba(255,255,255,0.1)',
            color: '#e2e8f0',
            fontSize: 11,
            cursor: 'pointer',
          }}
        >
          <Download size={13} /> Tải File PNG Về Máy
        </button>
      </div>
    </div>
  );
};
