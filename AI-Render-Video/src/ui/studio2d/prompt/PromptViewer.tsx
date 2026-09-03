import React, { useState } from 'react';
import { Copy, Check, Info, Video, ShieldAlert, Film, Sparkles } from 'lucide-react';
import { PromptItem, PromptCustomizerValues } from './types';
import { formatPromptWithCustomizer, getSiblingAnglePrompts } from './promptData';

interface PromptViewerProps {
  item: PromptItem;
  customizerValues: PromptCustomizerValues;
  onSelectPrompt?: (id: string) => void;
}

export const PromptViewer: React.FC<PromptViewerProps> = ({
  item,
  customizerValues,
  onSelectPrompt,
}) => {
  const [copiedPrompt, setCopiedPrompt] = useState(false);
  const [copiedNegative, setCopiedNegative] = useState(false);

  const siblingAngles = getSiblingAnglePrompts(item.id);
  const processedPrompt = formatPromptWithCustomizer(item.rawPrompt, customizerValues, item.id);

  const handleCopyPrompt = () => {
    navigator.clipboard.writeText(processedPrompt);
    setCopiedPrompt(true);
    setTimeout(() => setCopiedPrompt(false), 2000);
  };

  const handleCopyNegative = () => {
    navigator.clipboard.writeText(item.negativePrompt);
    setCopiedNegative(true);
    setTimeout(() => setCopiedNegative(false), 2000);
  };

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        background: '#0e1626',
        borderRadius: 10,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        padding: '16px 20px',
        overflowY: 'auto',
        gap: 14,
      }}
    >
      {/* ─── Top Header Info ─── */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: 10,
          borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
          paddingBottom: 12,
        }}
      >
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: 20 }}>{item.icon}</span>
            <h2 style={{ fontSize: 16, fontWeight: 800, color: '#f8fafc', margin: 0 }}>
              {item.title}
            </h2>
            <span
              style={{
                fontSize: 10,
                fontWeight: 700,
                padding: '2px 8px',
                borderRadius: 20,
                background:
                  item.promptType === 'video'
                    ? 'rgba(52, 211, 153, 0.2)'
                    : item.promptType === 'attachment'
                    ? 'rgba(192, 132, 252, 0.2)'
                    : 'rgba(56, 189, 248, 0.2)',
                color:
                  item.promptType === 'video'
                    ? '#34d399'
                    : item.promptType === 'attachment'
                    ? '#c084fc'
                    : '#38bdf8',
                border:
                  item.promptType === 'video'
                    ? '1px solid rgba(52, 211, 153, 0.4)'
                    : item.promptType === 'attachment'
                    ? '1px solid rgba(192, 132, 252, 0.4)'
                    : '1px solid rgba(56, 189, 248, 0.4)',
              }}
            >
              {item.stepLabel} • {item.promptType === 'video' ? 'Video Hoạt Ảnh' : item.promptType === 'attachment' ? 'Gắn Linh Kiện' : 'Ảnh 2D Gốc'}
            </span>
          </div>
          <div style={{ fontSize: 11.5, color: '#94a3b8', marginTop: 4 }}>
            {item.subtitle}
          </div>
        </div>

        <button
          onClick={handleCopyPrompt}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            padding: '8px 18px',
            borderRadius: 8,
            fontSize: 12,
            fontWeight: 700,
            cursor: 'pointer',
            border: 'none',
            background: copiedPrompt
              ? 'linear-gradient(135deg, #10b981, #059669)'
              : 'linear-gradient(135deg, #6366f1, #7c3aed)',
            color: '#ffffff',
            boxShadow: copiedPrompt
              ? '0 0 16px rgba(16, 185, 129, 0.5)'
              : '0 0 14px rgba(99, 102, 241, 0.35)',
            transition: 'all 0.2s ease',
          }}
        >
          {copiedPrompt ? <Check size={15} /> : <Copy size={15} />}
          {copiedPrompt ? '✅ Đã Copy Prompt!' : '📋 Copy Prompt'}
        </button>
      </div>

      {/* ─── Angle Selector Bar (0° | 45° | 90° | 135° | 180°) ─── */}
      {siblingAngles.length > 1 && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 10,
            flexWrap: 'wrap',
            padding: '8px 14px',
            background: 'rgba(15, 23, 42, 0.85)',
            borderRadius: 8,
            border: '1px solid rgba(56, 189, 248, 0.25)',
            boxShadow: '0 2px 8px rgba(0, 0, 0, 0.3)',
          }}
        >
          <span
            style={{
              fontSize: 11,
              fontWeight: 700,
              color: '#38bdf8',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              letterSpacing: '0.02em',
            }}
          >
            📐 Góc Camera ({siblingAngles.length} góc):
          </span>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            {siblingAngles.map((ang) => {
              const isActive = ang.isCurrent;
              return (
                <button
                  key={ang.id}
                  onClick={() => onSelectPrompt?.(ang.id)}
                  style={{
                    padding: '4px 10px',
                    borderRadius: 6,
                    fontSize: 11,
                    fontWeight: isActive ? 800 : 600,
                    cursor: 'pointer',
                    transition: 'all 0.18s ease',
                    border: isActive
                      ? '1px solid #38bdf8'
                      : '1px solid rgba(255, 255, 255, 0.12)',
                    background: isActive
                      ? 'linear-gradient(135deg, rgba(56, 189, 248, 0.3), rgba(14, 165, 233, 0.45))'
                      : 'rgba(30, 41, 59, 0.6)',
                    color: isActive ? '#38bdf8' : '#cbd5e1',
                    boxShadow: isActive ? '0 0 10px rgba(56, 189, 248, 0.4)' : 'none',
                  }}
                >
                  {ang.label}
                </button>
              );
            })}
          </div>
        </div>
      )}

      {/* ─── Prompt Text Box ─── */}
      <div
        style={{
          position: 'relative',
          background: '#090f1d',
          border: '1px solid rgba(99, 102, 241, 0.4)',
          borderRadius: 8,
          padding: '16px',
          boxShadow: 'inset 0 2px 8px rgba(0, 0, 0, 0.5)',
        }}
      >
        <button
          onClick={handleCopyPrompt}
          style={{
            position: 'absolute',
            top: 10,
            right: 10,
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '5px 12px',
            borderRadius: 5,
            fontSize: 11,
            fontWeight: 700,
            border: '1px solid rgba(99, 102, 241, 0.5)',
            background: copiedPrompt ? '#10b981' : '#6366f1',
            color: '#ffffff',
            cursor: 'pointer',
            zIndex: 5,
            boxShadow: '0 2px 10px rgba(0, 0, 0, 0.3)',
            transition: 'all 0.2s ease',
          }}
        >
          {copiedPrompt ? <Check size={13} /> : <Copy size={13} />}
          {copiedPrompt ? 'Đã Copy!' : 'Copy'}
        </button>

        <pre
          style={{
            fontFamily: '"JetBrains Mono", Consolas, "Courier New", monospace',
            fontSize: 11.5,
            lineHeight: 1.55,
            color: '#f1f5f9',
            whiteSpace: 'pre-wrap',
            wordWrap: 'break-word',
            margin: 0,
            maxHeight: 380,
            overflowY: 'auto',
            paddingRight: 10,
          }}
        >
          {processedPrompt}
        </pre>
      </div>

      {/* ─── Info Note Box ─── */}
      {item.infoNote && (
        <div
          style={{
            display: 'flex',
            alignItems: 'flex-start',
            gap: 10,
            background: 'rgba(30, 58, 95, 0.5)',
            borderLeft: '4px solid #f59e0b',
            borderRight: '1px solid rgba(245, 158, 11, 0.2)',
            borderTop: '1px solid rgba(245, 158, 11, 0.2)',
            borderBottom: '1px solid rgba(245, 158, 11, 0.2)',
            padding: '10px 14px',
            borderRadius: '0 8px 8px 0',
            fontSize: 11.5,
            color: '#fef3c7',
            lineHeight: 1.5,
          }}
        >
          <Info size={16} color="#f59e0b" style={{ flexShrink: 0, marginTop: 1 }} />
          <div>{item.infoNote}</div>
        </div>
      )}

      {/* ─── Video Animation Guide (If Video) ─── */}
      {item.videoGuide && (
        <div
          style={{
            background: 'rgba(15, 23, 42, 0.7)',
            border: '1px solid rgba(52, 211, 153, 0.3)',
            borderRadius: 8,
            padding: '12px 16px',
            display: 'flex',
            flexDirection: 'column',
            gap: 8,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, fontWeight: 700, color: '#34d399' }}>
            <Film size={15} />
            <span>Thông Số Video Khuyến Nghị (Dành cho Kling AI, Haiper, Luma, Runway, Hailuo, Sora):</span>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 8, fontSize: 11, color: '#cbd5e1' }}>
            <div style={{ background: 'rgba(0,0,0,0.3)', padding: '6px 10px', borderRadius: 5 }}>
              ⏱️ <b>Thời lượng:</b> {item.videoGuide.duration}
            </div>
            <div style={{ background: 'rgba(0,0,0,0.3)', padding: '6px 10px', borderRadius: 5 }}>
              🎞️ <b>Tốc độ khung hình:</b> {item.videoGuide.fps}
            </div>
            <div style={{ background: 'rgba(0,0,0,0.3)', padding: '6px 10px', borderRadius: 5 }}>
              📷 <b>Góc máy:</b> {item.videoGuide.camera}
            </div>
            <div style={{ background: 'rgba(0,0,0,0.3)', padding: '6px 10px', borderRadius: 5 }}>
              🔄 <b>Chu kỳ:</b> {item.videoGuide.loopType}
            </div>
          </div>

          <ul style={{ margin: '4px 0 0 18px', padding: 0, fontSize: 11, color: '#94a3b8', lineHeight: 1.4 }}>
            {item.videoGuide.keyPoints.map((pt, idx) => (
              <li key={idx}>{pt}</li>
            ))}
          </ul>
        </div>
      )}

      {/* ─── Negative Prompt Card ─── */}
      <div
        style={{
          background: 'rgba(15, 23, 42, 0.7)',
          border: '1px solid rgba(239, 68, 68, 0.25)',
          borderRadius: 8,
          padding: '12px 16px',
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 11.5, fontWeight: 700, color: '#f87171' }}>
            <ShieldAlert size={14} />
            <span>Negative Prompt (Từ khóa loại trừ):</span>
          </div>

          <button
            onClick={handleCopyNegative}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '3px 8px',
              fontSize: 10,
              fontWeight: 700,
              borderRadius: 4,
              border: '1px solid rgba(239, 68, 68, 0.4)',
              background: copiedNegative ? '#10b981' : 'rgba(239, 68, 68, 0.15)',
              color: '#ffffff',
              cursor: 'pointer',
            }}
          >
            {copiedNegative ? <Check size={11} /> : <Copy size={11} />}
            {copiedNegative ? 'Đã Copy!' : 'Copy Negative Prompt'}
          </button>
        </div>

        <div
          style={{
            fontFamily: '"JetBrains Mono", Consolas, monospace',
            fontSize: 10.5,
            color: '#cbd5e1',
            background: 'rgba(0, 0, 0, 0.4)',
            padding: '8px 10px',
            borderRadius: 5,
            lineHeight: 1.4,
          }}
        >
          {item.negativePrompt}
        </div>
      </div>
    </div>
  );
};
