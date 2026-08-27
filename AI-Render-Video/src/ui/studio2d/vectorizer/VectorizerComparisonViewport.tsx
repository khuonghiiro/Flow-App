import React from 'react';
import { ZoomIn, ZoomOut, RefreshCw } from 'lucide-react';
import { VectorizerViewMode, VectorizerMetaStats } from './types';

interface VectorizerComparisonViewportProps {
  sourceImageUrl: string;
  svgDataUrl: string | null;
  isConverting: boolean;
  errorMsg: string | null;
  viewMode: VectorizerViewMode;
  onSetViewMode: (mode: VectorizerViewMode) => void;
  splitPos: number;
  onSetSplitPos: (pos: number) => void;
  zoomLevel: number;
  onSetZoomLevel: (update: (prev: number) => number) => void;
  metaStats: VectorizerMetaStats | null;
}

export const VectorizerComparisonViewport: React.FC<VectorizerComparisonViewportProps> = ({
  sourceImageUrl,
  svgDataUrl,
  isConverting,
  errorMsg,
  viewMode,
  onSetViewMode,
  splitPos,
  onSetSplitPos,
  zoomLevel,
  onSetZoomLevel,
  metaStats,
}) => {
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
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
          flexShrink: 0,
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
              onClick={() => onSetViewMode(v.id as VectorizerViewMode)}
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
              onClick={() => onSetZoomLevel((z) => Math.max(0.5, z - 0.25))}
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
              onClick={() => onSetZoomLevel((z) => Math.min(3.0, z + 0.25))}
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
                  overflow: 'hidden',
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
                  overflow: 'hidden',
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
              overflow: 'hidden',
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
              overflow: 'hidden',
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
  );
};
