import React from 'react';
import { Sparkles, RefreshCw, Download, Check, Copy } from 'lucide-react';
import { VectorizerPreset, VECTORIZER_PRESETS } from './types';

interface VectorizerTopToolbarProps {
  preset: VectorizerPreset;
  onSelectPreset: (preset: VectorizerPreset) => void;
  isConverting: boolean;
  onVectorize: () => void;
  svgOutput: string | null;
  onDownloadSvg: () => void;
  onCopySvgCode: () => void;
  copied: boolean;
}

export const VectorizerTopToolbar: React.FC<VectorizerTopToolbarProps> = ({
  preset,
  onSelectPreset,
  isConverting,
  onVectorize,
  svgOutput,
  onDownloadSvg,
  onCopySvgCode,
  copied,
}) => {
  return (
    <div
      style={{
        height: 48,
        background: 'rgba(15, 23, 42, 0.9)',
        borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0 16px',
        flexShrink: 0,
      }}
    >
      {/* Preset Selectors */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, overflowX: 'auto' }}>
        <span
          style={{
            fontSize: 11,
            fontWeight: 700,
            color: '#38bdf8',
            display: 'flex',
            alignItems: 'center',
            gap: 5,
            whiteSpace: 'nowrap',
          }}
        >
          <Sparkles size={14} /> MẪU THIẾT LẬP:
        </span>
        {VECTORIZER_PRESETS.map((p) => (
          <button
            key={p.id}
            onClick={() => onSelectPreset(p.id)}
            style={{
              padding: '4px 10px',
              borderRadius: 6,
              fontSize: 10,
              fontWeight: preset === p.id ? 700 : 600,
              border: preset === p.id ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.08)',
              background: preset === p.id ? 'rgba(56, 189, 248, 0.2)' : 'rgba(30, 41, 59, 0.5)',
              color: preset === p.id ? '#38bdf8' : '#94a3b8',
              cursor: 'pointer',
              whiteSpace: 'nowrap',
              transition: 'all 0.15s',
            }}
          >
            {p.label}
          </button>
        ))}
      </div>

      {/* Action Buttons */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexShrink: 0 }}>
        <button
          onClick={onVectorize}
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
            background: isConverting ? '#475569' : 'linear-gradient(135deg, #0284c7, #06b6d4)',
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
              onClick={onDownloadSvg}
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
              onClick={onCopySvgCode}
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
  );
};
