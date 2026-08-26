import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Check } from 'lucide-react';
import { SlicerUploadedImageItem } from '../hooks/useSlicerMultiImageGallery';
import { PaddedCropRect, detectImageBBoxRect } from '../../../../core/utils/PixelBoundingBoxAlgorithms';
import { ChromaProcessOptions } from '../../../../core/utils/ChromaDespeckleProcessor';

interface SlicerGridCardWithBBoxProps {
  item: SlicerUploadedImageItem;
  isActive: boolean;
  isEyedropperActive: boolean;
  isDirectBBoxCropActive: boolean;
  directBBoxPadding: number;
  chromaOptions?: ChromaProcessOptions;
  checkerTheme: 'dark' | 'light';
  previewDisplayMode: 'transparent' | 'original';
  onClick: (e: React.MouseEvent<HTMLDivElement>, item: SlicerUploadedImageItem) => void;
  onMouseMove: (e: React.MouseEvent<HTMLDivElement>) => void;
  onToggleCheckedItem?: (id: string) => void;
}

export const SlicerGridCardWithBBox: React.FC<SlicerGridCardWithBBoxProps> = ({
  item,
  isActive,
  isEyedropperActive,
  isDirectBBoxCropActive,
  directBBoxPadding,
  chromaOptions,
  checkerTheme,
  previewDisplayMode,
  onClick,
  onMouseMove,
  onToggleCheckedItem,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [bboxRect, setBboxRect] = useState<PaddedCropRect | null>(null);
  const [naturalSize, setNaturalSize] = useState<{ width: number; height: number }>({ width: 0, height: 0 });

  const isSep = Boolean(item.isTransparentSeparated && item.transparentUrl);
  const sourceUrl =
    previewDisplayMode === 'original' && item.originalUrl
      ? item.originalUrl
      : isSep
      ? item.transparentUrl!
      : item.originalUrl || item.url;

  // Redraw canvas whenever image, bbox, or padding changes
  useEffect(() => {
    let isMounted = true;
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      if (!isMounted) return;
      const w = img.naturalWidth || img.width;
      const h = img.naturalHeight || img.height;
      setNaturalSize({ width: w, height: h });

      const canvas = canvasRef.current;
      if (!canvas) return;
      canvas.width = w;
      canvas.height = h;
      const ctx = canvas.getContext('2d', { willReadFrequently: true });
      if (!ctx) return;

      // Draw the image
      ctx.clearRect(0, 0, w, h);
      ctx.drawImage(img, 0, 0);

      // If BBox is active, calculate and draw BBox overlay with alternating black/white dashed border
      if (isDirectBBoxCropActive) {
        const rect = detectImageBBoxRect(img, chromaOptions, directBBoxPadding, 20);
        if (isMounted) setBboxRect(rect);

        if (rect) {
          // Dim mask outside crop region
          ctx.fillStyle = 'rgba(0, 0, 0, 0.45)';
          // Top
          ctx.fillRect(0, 0, w, rect.top);
          // Bottom
          ctx.fillRect(0, rect.bottom + 1, w, h - (rect.bottom + 1));
          // Left
          ctx.fillRect(0, rect.top, rect.left, rect.height);
          // Right
          ctx.fillRect(rect.right + 1, rect.top, w - (rect.right + 1), rect.height);

          // Alternating black & white dashed border (Marching ants / Dual dash)
          const lw = Math.max(2.5, Math.round(w / 120));
          ctx.lineWidth = lw;

          // Layer 1: Black dash
          ctx.strokeStyle = '#000000';
          ctx.setLineDash([6, 6]);
          ctx.lineDashOffset = 0;
          ctx.strokeRect(rect.left, rect.top, rect.width, rect.height);

          // Layer 2: White dash
          ctx.strokeStyle = '#ffffff';
          ctx.setLineDash([6, 6]);
          ctx.lineDashOffset = 6;
          ctx.strokeRect(rect.left, rect.top, rect.width, rect.height);

          ctx.setLineDash([]);
        }
      } else {
        if (isMounted) setBboxRect(null);
      }
    };
    img.src = sourceUrl;

    return () => {
      isMounted = false;
    };
  }, [sourceUrl, isDirectBBoxCropActive, directBBoxPadding, chromaOptions]);

  return (
    <div
      onClick={(e) => onClick(e, item)}
      onMouseMove={onMouseMove}
      style={{
        borderRadius: 8,
        border: isActive
          ? '2px solid #38bdf8'
          : isSep
          ? '1.5px solid rgba(74,222,128,0.4)'
          : '1.5px solid rgba(255,255,255,0.12)',
        background:
          isSep || previewDisplayMode === 'transparent'
            ? checkerTheme === 'light'
              ? 'repeating-conic-gradient(#e2e8f0 0% 25%, #ffffff 0% 50%) 50% / 12px 12px'
              : 'repeating-conic-gradient(#1e293b 0% 25%, #0f172a 0% 50%) 50% / 12px 12px'
            : checkerTheme === 'light'
            ? 'rgba(245, 245, 250, 0.9)'
            : 'rgba(15, 23, 42, 0.7)',
        padding: 6,
        cursor: isEyedropperActive ? 'crosshair' : 'pointer',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 4,
        boxShadow: isActive ? '0 0 12px rgba(56,189,248,0.4)' : '0 2px 8px rgba(0,0,0,0.3)',
        transition: 'all 0.15s ease',
        position: 'relative',
        height: '100%',
        minHeight: 160,
        boxSizing: 'border-box',
      }}
      title={
        isEyedropperActive
          ? `${item.name} • Nhấp để hút màu từ ảnh này`
          : isDirectBBoxCropActive && bboxRect
          ? `${item.name} • BBox: ${bboxRect.width}×${bboxRect.height}px`
          : `${item.name} • Nhấp để sửa riêng • Ctrl+Click để bỏ chọn`
      }
    >
      {/* Checkbox button */}
      {!isEyedropperActive && (
        <div
          onClick={(e) => {
            e.stopPropagation();
            onToggleCheckedItem?.(item.id);
          }}
          style={{
            position: 'absolute',
            top: 4,
            left: 4,
            zIndex: 9,
            width: 16,
            height: 16,
            borderRadius: 4,
            background: '#0284c7',
            border: '1px solid #38bdf8',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: 'pointer',
            boxShadow: '0 2px 4px rgba(0,0,0,0.4)',
          }}
          title="Bỏ chọn ảnh này"
        >
          <Check size={11} color="#ffffff" strokeWidth={3} />
        </div>
      )}

      {/* Top right badges */}
      {isDirectBBoxCropActive && bboxRect && (
        <div
          style={{
            position: 'absolute',
            top: 4,
            right: 4,
            zIndex: 8,
            background: 'rgba(15, 23, 42, 0.92)',
            color: '#c084fc',
            fontSize: 8.5,
            fontWeight: 800,
            padding: '1px 5px',
            borderRadius: 3,
            border: '1px solid rgba(192, 132, 252, 0.5)',
            fontFamily: 'monospace',
            boxShadow: '0 2px 6px rgba(0,0,0,0.6)',
            pointerEvents: 'none',
          }}
        >
          ✂️ {bboxRect.width}×{bboxRect.height}
        </div>
      )}

      {isSep && !isDirectBBoxCropActive && (
        <div
          style={{
            position: 'absolute',
            top: 4,
            right: 4,
            zIndex: 8,
            background: 'rgba(34,197,94,0.9)',
            color: '#fff',
            fontSize: 8,
            fontWeight: 700,
            padding: '1px 4px',
            borderRadius: 3,
          }}
        >
          ✨ Đã tách
        </div>
      )}

      {/* Middle Image Viewport: Canvas fits 100% inside card without cut-off */}
      <div
        style={{
          flex: 1,
          width: '100%',
          minHeight: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          position: 'relative',
          padding: '2px 0',
        }}
      >
        <canvas
          ref={canvasRef}
          style={{
            maxWidth: '100%',
            maxHeight: '100%',
            width: 'auto',
            height: 'auto',
            objectFit: 'contain',
            borderRadius: 4,
            display: 'block',
            pointerEvents: 'none',
          }}
        />
      </div>

      {/* Bottom Part Name Tag */}
      <div
        style={{
          fontSize: 9,
          fontWeight: 700,
          color: isActive ? '#38bdf8' : '#94a3b8',
          textAlign: 'center',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
          width: '100%',
          flexShrink: 0,
          lineHeight: '13px',
        }}
      >
        {item.metadata?.part_name || item.name}
      </div>
    </div>
  );
};
