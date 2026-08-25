import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Scissors, Save, Download, X, ZoomIn, ZoomOut, Check, Sliders, Layers, Sparkles } from 'lucide-react';
import {
  detectPixelContentBoundingBox,
  computePaddedBoundingBox,
  cropCanvasWithPadding,
  PixelBoundingBox,
  PaddedCropRect,
} from '../../../../core/utils/PixelBoundingBoxAlgorithms';
import {
  CharacterResourceCategory,
  Character2DPartType,
  Character2DAngle,
  CharacterResourceKit,
} from '../../../../types/scene2d';
import { saveCustomResourceKit, RESOURCE_CATEGORIES } from '../../../../core/assets/CharacterKitStorage';
import { STANDARD_ANGLE_DEFINITIONS } from '../../../../core/assets/slicer/SlicerAngleConstants';

export interface SlicerSmartCropModalProps {
  isOpen: boolean;
  onClose: () => void;
  imageDataUrl: string;
  defaultTitle?: string;
  defaultCategory?: CharacterResourceCategory;
  defaultPartSlot?: Character2DPartType;
  defaultAngle?: Character2DAngle;
  onApplyCroppedImage?: (croppedDataUrl: string, rect: PaddedCropRect) => void;
}

const PART_SLOT_OPTIONS: { id: Character2DPartType; label: string }[] = [
  { id: 'toc_truoc', label: '✂️ Mái Tóc Trước (Front Bangs)' },
  { id: 'toc_sau', label: '🌊 Suối Tóc Sau (Back Hair)' },
  { id: 'khuon_mat', label: '🎭 Khuôn Mặt (Face Base)' },
  { id: 'mat', label: '👁️ Đôi Mắt (Eyes)' },
  { id: 'than_co_ban', label: '🥋 Thân Áo (Torso)' },
  { id: 'canh_tay_trai', label: '💪 Cánh Tay Trái (L-Arm)' },
  { id: 'canh_tay_phai', label: '💪 Cánh Tay Phải (R-Arm)' },
  { id: 'dui_trai', label: '🦵 Chân Trái (L-Leg)' },
  { id: 'dui_phai', label: '🦵 Chân Phải (R-Leg)' },
  { id: 'ao_choang', label: '🧣 Áo Choàng (Cape/Robe)' },
  { id: 'vu_khi', label: '⚔️ Vũ Khí (Weapon)' },
];

export const SlicerSmartCropModal: React.FC<SlicerSmartCropModalProps> = ({
  isOpen,
  onClose,
  imageDataUrl,
  defaultTitle = 'Linh Kiện Cắt Gọt',
  defaultCategory = 'toc',
  defaultPartSlot = 'toc_truoc',
  defaultAngle = 'front',
  onApplyCroppedImage,
}) => {
  const [paddingPx, setPaddingPx] = useState<number>(4);
  const [alphaThreshold, setAlphaThreshold] = useState<number>(10);
  const [zoom, setZoom] = useState<number>(1.5);
  const [saveName, setSaveName] = useState<string>(defaultTitle);
  const [saveCategory, setSaveCategory] = useState<CharacterResourceCategory>(defaultCategory);
  const [savePartSlot, setSavePartSlot] = useState<Character2DPartType>(defaultPartSlot);
  const [saveAngle, setSaveAngle] = useState<Character2DAngle>(defaultAngle);
  const [isSavedSuccess, setIsSavedSuccess] = useState<boolean>(false);

  const sourceCanvasRef = useRef<HTMLCanvasElement | null>(null);
  const previewCanvasRef = useRef<HTMLCanvasElement | null>(null);
  const [detectedBbox, setDetectedBbox] = useState<PixelBoundingBox | null>(null);
  const [imageSize, setImageSize] = useState<{ width: number; height: number }>({ width: 100, height: 100 });

  // Reset defaults when opening with new image
  useEffect(() => {
    if (!isOpen || !imageDataUrl) return;
    setSaveName(defaultTitle);
    setSaveCategory(defaultCategory);
    setSavePartSlot(defaultPartSlot);
    setSaveAngle(defaultAngle);
    setIsSavedSuccess(false);

    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      const w = img.naturalWidth || img.width;
      const h = img.naturalHeight || img.height;
      setImageSize({ width: w, height: h });

      const canvas = document.createElement('canvas');
      canvas.width = w;
      canvas.height = h;
      const ctx = canvas.getContext('2d', { willReadFrequently: true });
      if (ctx) {
        ctx.drawImage(img, 0, 0);
        sourceCanvasRef.current = canvas;
        const bbox = detectPixelContentBoundingBox(ctx, w, h, alphaThreshold);
        setDetectedBbox(bbox);
      }
    };
    img.src = imageDataUrl;
  }, [isOpen, imageDataUrl, defaultTitle, defaultCategory, defaultPartSlot, defaultAngle, alphaThreshold]);

  // Render preview canvas with bounding box overlays
  const renderPreview = useCallback(() => {
    const srcCanvas = sourceCanvasRef.current;
    const prevCanvas = previewCanvasRef.current;
    if (!srcCanvas || !prevCanvas || !detectedBbox) return;

    const w = srcCanvas.width;
    const h = srcCanvas.height;
    prevCanvas.width = w;
    prevCanvas.height = h;
    const ctx = prevCanvas.getContext('2d');
    if (!ctx) return;

    // Draw checkerboard background
    const tileSize = 8;
    for (let y = 0; y < h; y += tileSize) {
      for (let x = 0; x < w; x += tileSize) {
        ctx.fillStyle = (Math.floor(x / tileSize) + Math.floor(y / tileSize)) % 2 === 0 ? '#111827' : '#1e293b';
        ctx.fillRect(x, y, tileSize, tileSize);
      }
    }

    // Draw the image
    ctx.drawImage(srcCanvas, 0, 0);

    if (detectedBbox.hasContent) {
      const paddedRect = computePaddedBoundingBox(detectedBbox, w, h, paddingPx);

      // 1. Darken outer discarded area
      ctx.fillStyle = 'rgba(0, 0, 0, 0.45)';
      // Top
      ctx.fillRect(0, 0, w, paddedRect.top);
      // Bottom
      ctx.fillRect(0, paddedRect.bottom + 1, w, h - (paddedRect.bottom + 1));
      // Left
      ctx.fillRect(0, paddedRect.top, paddedRect.left, paddedRect.height);
      // Right
      ctx.fillRect(paddedRect.right + 1, paddedRect.top, w - (paddedRect.right + 1), paddedRect.height);

      // 2. Inner tight content box (Cyan Dashed)
      ctx.strokeStyle = '#06b6d4';
      ctx.lineWidth = 1;
      ctx.setLineDash([3, 3]);
      ctx.strokeRect(
        detectedBbox.minX,
        detectedBbox.minY,
        detectedBbox.width,
        detectedBbox.height
      );

      // 3. Outer Padded Crop Box (Green Solid with Corner Grips)
      ctx.strokeStyle = '#22c55e';
      ctx.lineWidth = 2;
      ctx.setLineDash([]);
      ctx.strokeRect(
        paddedRect.left,
        paddedRect.top,
        paddedRect.width,
        paddedRect.height
      );

      // Corner Grips
      const gripSize = 6;
      ctx.fillStyle = '#22c55e';
      ctx.fillRect(paddedRect.left - gripSize / 2, paddedRect.top - gripSize / 2, gripSize, gripSize);
      ctx.fillRect(paddedRect.right - gripSize / 2, paddedRect.top - gripSize / 2, gripSize, gripSize);
      ctx.fillRect(paddedRect.left - gripSize / 2, paddedRect.bottom - gripSize / 2, gripSize, gripSize);
      ctx.fillRect(paddedRect.right - gripSize / 2, paddedRect.bottom - gripSize / 2, gripSize, gripSize);

      // Top label with dimension badge
      ctx.fillStyle = 'rgba(15, 23, 42, 0.9)';
      ctx.strokeStyle = '#22c55e';
      ctx.lineWidth = 1;
      const labelText = `✂️ ${paddedRect.width} × ${paddedRect.height} px (+${paddingPx}px padding)`;
      ctx.font = 'bold 11px sans-serif';
      const textWidth = ctx.measureText(labelText).width;
      const badgeX = Math.max(4, Math.min(w - textWidth - 12, paddedRect.left));
      const badgeY = Math.max(16, paddedRect.top - 6);

      ctx.fillRect(badgeX, badgeY - 14, textWidth + 10, 18);
      ctx.strokeRect(badgeX, badgeY - 14, textWidth + 10, 18);
      ctx.fillStyle = '#4ade80';
      ctx.fillText(labelText, badgeX + 5, badgeY - 1);
    }
  }, [detectedBbox, paddingPx]);

  useEffect(() => {
    renderPreview();
  }, [renderPreview]);

  if (!isOpen) return null;

  const paddedRect = detectedBbox
    ? computePaddedBoundingBox(detectedBbox, imageSize.width, imageSize.height, paddingPx)
    : { left: 0, top: 0, right: imageSize.width, bottom: imageSize.height, width: imageSize.width, height: imageSize.height };

  const savedAreaPercentage = detectedBbox && imageSize.width > 0 && imageSize.height > 0
    ? Math.max(0, Math.round((1 - (paddedRect.width * paddedRect.height) / (imageSize.width * imageSize.height)) * 100))
    : 0;

  // Generate cropped Data URL
  const getCroppedDataUrl = (): string => {
    if (!sourceCanvasRef.current || !detectedBbox) return imageDataUrl;
    const { dataUrl } = cropCanvasWithPadding(sourceCanvasRef.current, detectedBbox, paddingPx);
    return dataUrl;
  };

  // Handler: Save to Kit Storage (Kho Linh Kiện)
  const handleSaveToVault = () => {
    const croppedUrl = getCroppedDataUrl();
    const kitId = `custom_kit_${Date.now()}`;
    const newKit: CharacterResourceKit = {
      id: kitId,
      name: saveName || 'Linh Kiện Cắt Gọt',
      category: saveCategory,
      categoryLabel: `${RESOURCE_CATEGORIES.find((c) => c.id === saveCategory)?.label || 'Kho Linh Kiện'} - Cắt Bounding Box`,
      previewImage: croppedUrl,
      description: `Linh kiện đã cắt gọt bỏ khoảng trống thừa (${paddedRect.width}×${paddedRect.height}px, padding: ${paddingPx}px). Góc: ${saveAngle}, Slot: ${savePartSlot}.`,
      parts: {
        [savePartSlot]: {
          path: croppedUrl,
          offset: [0, 0],
          scale: [1, 1],
          rotation: 0,
          pivot: [0.5, 0.5],
          flipX: false,
          flipY: false,
          z_index: 1,
          opacity: 1,
          angles: {
            [saveAngle]: croppedUrl,
          },
        },
      } as any,
      createdAt: new Date().toISOString(),
    };

    saveCustomResourceKit(newKit);
    setIsSavedSuccess(true);
    setTimeout(() => setIsSavedSuccess(false), 3500);
  };

  // Handler: Apply cropped image to Slicer / Cell
  const handleApplyToSlicer = () => {
    const croppedUrl = getCroppedDataUrl();
    if (onApplyCroppedImage) {
      onApplyCroppedImage(croppedUrl, paddedRect);
    }
    onClose();
  };

  // Handler: Download cropped PNG
  const handleDownloadPNG = () => {
    const croppedUrl = getCroppedDataUrl();
    const a = document.createElement('a');
    a.href = croppedUrl;
    a.download = `${saveName.toLowerCase().replace(/\s+/g, '_')}_${paddedRect.width}x${paddedRect.height}.png`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(0, 0, 0, 0.82)',
        backdropFilter: 'blur(8px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1100,
        padding: 16,
      }}
    >
      <div
        style={{
          background: '#070b14',
          border: '1.5px solid #0284c7',
          borderRadius: 12,
          width: '95vw',
          maxWidth: 960,
          maxHeight: '92vh',
          display: 'flex',
          flexDirection: 'column',
          boxShadow: '0 12px 48px rgba(0, 0, 0, 0.9), 0 0 20px rgba(2, 132, 199, 0.3)',
          overflow: 'hidden',
        }}
      >
        {/* Header */}
        <div
          style={{
            padding: '12px 18px',
            background: 'linear-gradient(90deg, #0b1329 0%, #0f172a 100%)',
            borderBottom: '1px solid rgba(2, 132, 199, 0.3)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div style={{ background: 'rgba(56, 189, 248, 0.15)', padding: 6, borderRadius: 6, color: '#38bdf8' }}>
              <Scissors size={18} />
            </div>
            <div>
              <div style={{ fontSize: 13.5, fontWeight: 800, color: '#f8fafc', display: 'flex', alignItems: 'center', gap: 6 }}>
                Cắt Gọt Khung Pixel Thông Minh (Smart Auto-Trim & Bounding Box Cropper)
                <span style={{ fontSize: 10, background: '#0284c7', color: '#fff', padding: '2px 6px', borderRadius: 4, fontWeight: 700 }}>
                  Tab 1 Slicer Tool
                </span>
              </div>
              <div style={{ fontSize: 10, color: '#94a3b8' }}>
                Tự động dò tìm biên độ pixel thực tế của chi tiết và mở rộng khoảng đệm (Padding) để loại bỏ hoàn toàn khoảng trống thừa.
              </div>
            </div>
          </div>
          <button
            onClick={onClose}
            style={{
              background: 'transparent',
              border: 'none',
              color: '#94a3b8',
              cursor: 'pointer',
              padding: 4,
              borderRadius: 4,
            }}
          >
            <X size={18} />
          </button>
        </div>

        {/* Body Content */}
        <div style={{ display: 'flex', flex: 1, minHeight: 0, overflow: 'hidden' }}>
          {/* Left Canvas Preview Area */}
          <div
            style={{
              flex: 1,
              display: 'flex',
              flexDirection: 'column',
              background: '#030712',
              borderRight: '1px solid rgba(255, 255, 255, 0.08)',
              overflow: 'hidden',
            }}
          >
            {/* Canvas Toolbar */}
            <div
              style={{
                padding: '6px 12px',
                background: '#090d16',
                borderBottom: '1px solid rgba(255, 255, 255, 0.06)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                fontSize: 10.5,
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <span style={{ color: '#94a3b8' }}>
                  Kích thước gốc: <b style={{ color: '#f1f5f9' }}>{imageSize.width} × {imageSize.height} px</b>
                </span>
                <span style={{ color: '#06b6d4' }}>➔</span>
                <span style={{ color: '#4ade80' }}>
                  Sau khi cắt: <b>{paddedRect.width} × {paddedRect.height} px</b>
                </span>
                {savedAreaPercentage > 0 && (
                  <span style={{ background: 'rgba(34, 197, 94, 0.15)', color: '#4ade80', padding: '1px 6px', borderRadius: 4, fontSize: 9.5, fontWeight: 700 }}>
                    Tiết kiệm {savedAreaPercentage}% khoảng trống
                  </span>
                )}
              </div>

              {/* Zoom Controls */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                <button
                  onClick={() => setZoom((z) => Math.max(0.5, Number((z - 0.25).toFixed(2))))}
                  style={{ background: '#1e293b', border: '1px solid rgba(255,255,255,0.1)', color: '#cbd5e1', padding: '2px 6px', borderRadius: 4, cursor: 'pointer' }}
                >
                  <ZoomOut size={12} />
                </button>
                <span style={{ fontSize: 10, color: '#38bdf8', minWidth: 35, textAlign: 'center', fontWeight: 700 }}>
                  {Math.round(zoom * 100)}%
                </span>
                <button
                  onClick={() => setZoom((z) => Math.min(4, Number((z + 0.25).toFixed(2))))}
                  style={{ background: '#1e293b', border: '1px solid rgba(255,255,255,0.1)', color: '#cbd5e1', padding: '2px 6px', borderRadius: 4, cursor: 'pointer' }}
                >
                  <ZoomIn size={12} />
                </button>
              </div>
            </div>

            {/* Canvas Viewport */}
            <div
              style={{
                flex: 1,
                overflow: 'auto',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                padding: 20,
              }}
            >
              <div
                style={{
                  transform: `scale(${zoom})`,
                  transformOrigin: 'center center',
                  transition: 'transform 0.1s ease',
                  boxShadow: '0 8px 32px rgba(0,0,0,0.8)',
                  borderRadius: 4,
                  overflow: 'hidden',
                  lineHeight: 0,
                }}
              >
                <canvas ref={previewCanvasRef} />
              </div>
            </div>

            {/* Legend / Info Bar */}
            <div
              style={{
                padding: '6px 14px',
                background: '#070b14',
                borderTop: '1px solid rgba(255, 255, 255, 0.06)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                fontSize: 10,
                color: '#64748b',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <span style={{ display: 'flex', alignItems: 'center', gap: 4, color: '#06b6d4' }}>
                  <span style={{ width: 8, height: 8, border: '1px dashed #06b6d4', display: 'inline-block' }} />
                  Khung viền pixel thực tế (minX: {detectedBbox?.minX ?? 0}, minY: {detectedBbox?.minY ?? 0})
                </span>
                <span style={{ display: 'flex', alignItems: 'center', gap: 4, color: '#22c55e' }}>
                  <span style={{ width: 8, height: 8, border: '1.5px solid #22c55e', display: 'inline-block' }} />
                  Khung cắt xuất file (+{paddingPx}px đệm an toàn)
                </span>
              </div>
              <div>
                Tọa độ cắt: L:{paddedRect.left} | T:{paddedRect.top} | R:{paddedRect.right} | B:{paddedRect.bottom}
              </div>
            </div>
          </div>

          {/* Right Controls & Vault Saving Column */}
          <div
            style={{
              width: 320,
              background: '#090d16',
              padding: 16,
              display: 'flex',
              flexDirection: 'column',
              gap: 14,
              overflowY: 'auto',
            }}
          >
            {/* 1. Slider Padding Control */}
            <div
              style={{
                background: 'rgba(2, 132, 199, 0.08)',
                padding: 12,
                borderRadius: 8,
                border: '1px solid rgba(2, 132, 199, 0.25)',
                display: 'flex',
                flexDirection: 'column',
                gap: 8,
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <label style={{ fontSize: 11, fontWeight: 800, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
                  <Sliders size={13} /> Khoảng Đệm Viền Ngoài (Padding):
                </label>
                <span style={{ fontSize: 12, fontWeight: 900, color: '#4ade80', background: '#0f172a', padding: '2px 8px', borderRadius: 4, border: '1px solid rgba(74, 222, 128, 0.3)' }}>
                  +{paddingPx} px
                </span>
              </div>

              <input
                type="range"
                min={0}
                max={50}
                step={1}
                value={paddingPx}
                onChange={(e) => setPaddingPx(Number(e.target.value))}
                style={{ width: '100%', cursor: 'pointer', accentColor: '#22c55e' }}
              />

              {/* Quick Presets */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, flexWrap: 'wrap' }}>
                {[0, 2, 4, 8, 12, 20].map((val) => (
                  <button
                    key={val}
                    onClick={() => setPaddingPx(val)}
                    style={{
                      flex: 1,
                      padding: '3px 0',
                      fontSize: 9.5,
                      fontWeight: paddingPx === val ? 800 : 500,
                      background: paddingPx === val ? '#0284c7' : '#1e293b',
                      color: paddingPx === val ? '#fff' : '#94a3b8',
                      border: '1px solid rgba(255,255,255,0.08)',
                      borderRadius: 4,
                      cursor: 'pointer',
                    }}
                  >
                    {val === 0 ? 'Sát 0' : `+${val}px`}
                  </button>
                ))}
              </div>
              <div style={{ fontSize: 9.5, color: '#94a3b8', lineHeight: 1.3 }}>
                💡 <i>Mẹo: Đặt khoảng đệm 2px-4px giúp lọn tóc và chi tiết không bị cắt đứt răng cưa ở sát mép ảnh.</i>
              </div>
            </div>

            {/* 2. Save To Vault Settings Form */}
            <div
              style={{
                background: 'rgba(168, 85, 247, 0.06)',
                padding: 12,
                borderRadius: 8,
                border: '1px solid rgba(168, 85, 247, 0.25)',
                display: 'flex',
                flexDirection: 'column',
                gap: 8,
              }}
            >
              <div style={{ fontSize: 11, fontWeight: 800, color: '#c084fc', display: 'flex', alignItems: 'center', gap: 4 }}>
                <Layers size={13} /> Thiết Lập Lưu Vào Kho Linh Kiện:
              </div>

              <div>
                <label style={{ fontSize: 10, color: '#cbd5e1', display: 'block', marginBottom: 2, fontWeight: 600 }}>
                  Tên linh kiện / chi tiết:
                </label>
                <input
                  type="text"
                  value={saveName}
                  onChange={(e) => setSaveName(e.target.value)}
                  placeholder="Ví dụ: Mái Tóc Trước Bạch Kim"
                  style={{
                    width: '100%',
                    padding: '5px 8px',
                    fontSize: 10.5,
                    background: '#040711',
                    color: '#fff',
                    border: '1px solid rgba(255,255,255,0.15)',
                    borderRadius: 5,
                    boxSizing: 'border-box',
                  }}
                />
              </div>

              <div>
                <label style={{ fontSize: 10, color: '#cbd5e1', display: 'block', marginBottom: 2, fontWeight: 600 }}>
                  Thể loại kho lưu trữ:
                </label>
                <select
                  value={saveCategory}
                  onChange={(e) => setSaveCategory(e.target.value as CharacterResourceCategory)}
                  style={{
                    width: '100%',
                    padding: '5px 8px',
                    fontSize: 10.5,
                    background: '#040711',
                    color: '#c084fc',
                    border: '1px solid rgba(168, 85, 247, 0.3)',
                    borderRadius: 5,
                    boxSizing: 'border-box',
                  }}
                >
                  {RESOURCE_CATEGORIES.map((cat) => (
                    <option key={cat.id} value={cat.id}>
                      {cat.icon} {cat.label}
                    </option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
                <div>
                  <label style={{ fontSize: 10, color: '#cbd5e1', display: 'block', marginBottom: 2, fontWeight: 600 }}>
                    Vị trí Slot:
                  </label>
                  <select
                    value={savePartSlot}
                    onChange={(e) => setSavePartSlot(e.target.value as Character2DPartType)}
                    style={{
                      width: '100%',
                      padding: '5px 4px',
                      fontSize: 10,
                      background: '#040711',
                      color: '#f8fafc',
                      border: '1px solid rgba(255,255,255,0.12)',
                      borderRadius: 5,
                      boxSizing: 'border-box',
                    }}
                  >
                    {PART_SLOT_OPTIONS.map((slot) => (
                      <option key={slot.id} value={slot.id}>
                        {slot.label}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label style={{ fontSize: 10, color: '#cbd5e1', display: 'block', marginBottom: 2, fontWeight: 600 }}>
                    Góc quay:
                  </label>
                  <select
                    value={saveAngle}
                    onChange={(e) => setSaveAngle(e.target.value as Character2DAngle)}
                    style={{
                      width: '100%',
                      padding: '5px 4px',
                      fontSize: 10,
                      background: '#040711',
                      color: '#38bdf8',
                      border: '1px solid rgba(56, 189, 248, 0.3)',
                      borderRadius: 5,
                      boxSizing: 'border-box',
                    }}
                  >
                    {STANDARD_ANGLE_DEFINITIONS.map((ang) => (
                      <option key={ang.id} value={ang.angle}>
                        {ang.iconText} {ang.label}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            </div>

            {/* Notification alert on save success */}
            {isSavedSuccess && (
              <div
                style={{
                  background: 'rgba(34, 197, 94, 0.15)',
                  border: '1px solid #22c55e',
                  color: '#4ade80',
                  padding: '8px 10px',
                  borderRadius: 6,
                  fontSize: 10.5,
                  display: 'flex',
                  alignItems: 'center',
                  gap: 6,
                  fontWeight: 700,
                  animation: 'fadeIn 0.2s ease',
                }}
              >
                <Check size={14} /> ✓ Đã lưu vào Kho Linh Kiện thành công!
              </div>
            )}

            {/* 3. Action Buttons */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 'auto' }}>
              {/* Primary: Save to Vault */}
              <button
                onClick={handleSaveToVault}
                style={{
                  width: '100%',
                  padding: '9px 12px',
                  background: 'linear-gradient(135deg, #9333ea 0%, #7e22ce 100%)',
                  color: '#fff',
                  border: '1px solid #c084fc',
                  borderRadius: 6,
                  fontSize: 11,
                  fontWeight: 800,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 6,
                  boxShadow: '0 4px 12px rgba(147, 51, 234, 0.35)',
                }}
              >
                <Save size={14} /> 💾 Cắt & Lưu Vào Kho Linh Kiện
              </button>

              {/* Secondary: Apply to Slicer */}
              {onApplyCroppedImage && (
                <button
                  onClick={handleApplyToSlicer}
                  style={{
                    width: '100%',
                    padding: '8px 12px',
                    background: 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)',
                    color: '#fff',
                    border: '1px solid #38bdf8',
                    borderRadius: 6,
                    fontSize: 10.5,
                    fontWeight: 700,
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 6,
                  }}
                >
                  <Sparkles size={13} /> ✂️ Áp Dụng Lên Ô / Slicer Này
                </button>
              )}

              {/* Tertiary: Download PNG */}
              <button
                onClick={handleDownloadPNG}
                style={{
                  width: '100%',
                  padding: '7px 12px',
                  background: '#1e293b',
                  color: '#cbd5e1',
                  border: '1px solid rgba(255, 255, 255, 0.1)',
                  borderRadius: 6,
                  fontSize: 10.5,
                  fontWeight: 600,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 6,
                }}
              >
                <Download size={13} /> 📥 Tải File PNG Đã Cắt Gọt
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
