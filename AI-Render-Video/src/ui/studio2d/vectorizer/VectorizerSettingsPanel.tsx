import React from 'react';
import { Upload, Sliders, Bone, Grid, ArrowRight } from 'lucide-react';
import { VectorizerParams, SampleImageItem, SAMPLE_IMAGES } from './types';

interface VectorizerSettingsPanelProps {
  sourceImageUrl: string;
  params: VectorizerParams;
  onUpdateParam: <K extends keyof VectorizerParams>(key: K, value: VectorizerParams[K]) => void;
  onFileUpload: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onSelectSample: (url: string) => void;
  fileInputRef: React.RefObject<HTMLInputElement | null>;
  svgDataUrl: string | null;
  onTransferToRigAssembler?: (svgUrl: string) => void;
  onTransferToGridSlicer?: (svgUrl: string) => void;
}

export const VectorizerSettingsPanel: React.FC<VectorizerSettingsPanelProps> = ({
  sourceImageUrl,
  params,
  onUpdateParam,
  onFileUpload,
  onSelectSample,
  fileInputRef,
  svgDataUrl,
  onTransferToRigAssembler,
  onTransferToGridSlicer,
}) => {
  return (
    <div
      style={{
        background: 'rgba(15, 23, 42, 0.75)',
        borderRight: '1px solid rgba(255, 255, 255, 0.08)',
        padding: 16,
        display: 'flex',
        flexDirection: 'column',
        gap: 14,
        overflowY: 'auto',
        width: 320,
        flexShrink: 0,
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
          Tải Ảnh hoặc File SVG (PNG, JPG, SVG)
        </div>
        <div style={{ fontSize: 9, color: '#64748b' }}>Nhấp để chọn ảnh hoặc kéo thả SVG</div>
        <input
          type="file"
          ref={fileInputRef as any}
          onChange={onFileUpload}
          accept="image/*,.svg"
          style={{ display: 'none' }}
        />
      </div>

      {/* Quick Sample Selector */}
      <div>
        <div style={{ fontSize: 10, fontWeight: 700, color: '#f59e0b', marginBottom: 6 }}>
          🖼️ HOẶC CHỌN ẢNH MẪU:
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 6 }}>
          {SAMPLE_IMAGES.map((s: SampleImageItem, idx: number) => (
            <button
              key={idx}
              onClick={() => onSelectSample(s.url)}
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
            <strong style={{ color: '#38bdf8' }}>{params.colorPrecision}</strong>
          </div>
          <input
            type="range"
            min="2"
            max="16"
            value={params.colorPrecision}
            onChange={(e) => onUpdateParam('colorPrecision', Number(e.target.value))}
            style={{ width: '100%', accentColor: '#0284c7', cursor: 'pointer' }}
          />
        </div>

        {/* Filter Speckle */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
            <span>Lọc Nhiễu Điểm Nhỏ (Speckle Filter)</span>
            <strong style={{ color: '#38bdf8' }}>{params.filterSpeckle} px</strong>
          </div>
          <input
            type="range"
            min="0"
            max="24"
            value={params.filterSpeckle}
            onChange={(e) => onUpdateParam('filterSpeckle', Number(e.target.value))}
            style={{ width: '100%', accentColor: '#0284c7', cursor: 'pointer' }}
          />
        </div>

        {/* Corner Threshold */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
            <span>Độ Nhọn Góc (Corner Threshold)</span>
            <strong style={{ color: '#38bdf8' }}>{params.cornerThreshold}°</strong>
          </div>
          <input
            type="range"
            min="10"
            max="180"
            value={params.cornerThreshold}
            onChange={(e) => onUpdateParam('cornerThreshold', Number(e.target.value))}
            style={{ width: '100%', accentColor: '#0284c7', cursor: 'pointer' }}
          />
        </div>

        {/* Length Threshold */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
            <span>Độ Mượt Đoạn Cong (Spline Length)</span>
            <strong style={{ color: '#38bdf8' }}>{params.lengthThreshold}</strong>
          </div>
          <input
            type="range"
            min="1.0"
            max="8.0"
            step="0.5"
            value={params.lengthThreshold}
            onChange={(e) => onUpdateParam('lengthThreshold', Number(e.target.value))}
            style={{ width: '100%', accentColor: '#0284c7', cursor: 'pointer' }}
          />
        </div>

        {/* Layer Difference (Gradient Smoothness) */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
            <span>Độ Mịn Chuyển Màu (Layer Difference)</span>
            <strong style={{ color: '#34d399' }}>{params.layerDifference} (Càng nhỏ càng mịn)</strong>
          </div>
          <input
            type="range"
            min="2"
            max="24"
            value={params.layerDifference}
            onChange={(e) => onUpdateParam('layerDifference', Number(e.target.value))}
            style={{ width: '100%', accentColor: '#10b981', cursor: 'pointer' }}
          />
        </div>

        {/* Edge Contour Smoothing (Mịn Viền Biên) */}
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
            <span>🪄 Mịn Viền (Edge Smoothing)</span>
            <strong style={{ color: '#f59e0b' }}>{params.edgeSmoothing} px</strong>
          </div>
          <input
            type="range"
            min="0"
            max="5"
            step="0.5"
            value={params.edgeSmoothing}
            onChange={(e) => onUpdateParam('edgeSmoothing', Number(e.target.value))}
            style={{ width: '100%', accentColor: '#f59e0b', cursor: 'pointer' }}
          />
        </div>

        {/* Hierarchical Mode */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: 10 }}>
          <span style={{ color: '#94a3b8' }}>Xếp Lớp (Hierarchy):</span>
          <div style={{ display: 'flex', gap: 4 }}>
            <button
              onClick={() => onUpdateParam('hierarchical', 'stacked')}
              style={{
                padding: '3px 8px',
                borderRadius: 4,
                fontSize: 9,
                fontWeight: params.hierarchical === 'stacked' ? 700 : 500,
                border: 'none',
                background: params.hierarchical === 'stacked' ? '#0284c7' : 'rgba(255,255,255,0.08)',
                color: '#fff',
                cursor: 'pointer',
              }}
            >
              Xếp Chồng
            </button>
            <button
              onClick={() => onUpdateParam('hierarchical', 'cutout')}
              style={{
                padding: '3px 8px',
                borderRadius: 4,
                fontSize: 9,
                fontWeight: params.hierarchical === 'cutout' ? 700 : 500,
                border: 'none',
                background: params.hierarchical === 'cutout' ? '#0284c7' : 'rgba(255,255,255,0.08)',
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
  );
};
