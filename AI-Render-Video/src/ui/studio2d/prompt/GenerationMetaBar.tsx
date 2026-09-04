import React from 'react';
import { Image, ArrowRight, Ratio } from 'lucide-react';
import type { PromptItem, GenerationMode, AspectRatio } from './types';

interface GenerationMetaBarProps {
  item: PromptItem;
  onSelectPrompt?: (id: string) => void;
}

const MODE_CONFIG: Record<GenerationMode, { icon: string; label: string; color: string; bg: string; border: string }> = {
  text_to_image: {
    icon: '🖼️',
    label: 'Tạo Ảnh Từ Văn Bản (Text → Image)',
    color: '#38bdf8',
    bg: 'rgba(56, 189, 248, 0.15)',
    border: 'rgba(56, 189, 248, 0.4)',
  },
  image_to_image: {
    icon: '🔄',
    label: 'Tạo Ảnh Từ Ảnh Tham Chiếu (Image → Image)',
    color: '#a78bfa',
    bg: 'rgba(167, 139, 250, 0.15)',
    border: 'rgba(167, 139, 250, 0.4)',
  },
  image_to_video: {
    icon: '🎬',
    label: 'Tạo Video Từ Ảnh Tham Chiếu (Image → Video)',
    color: '#34d399',
    bg: 'rgba(52, 211, 153, 0.15)',
    border: 'rgba(52, 211, 153, 0.4)',
  },
  text_to_video: {
    icon: '📹',
    label: 'Tạo Video Từ Văn Bản (Text → Video)',
    color: '#fbbf24',
    bg: 'rgba(251, 191, 36, 0.15)',
    border: 'rgba(251, 191, 36, 0.4)',
  },
};

const ASPECT_OPTIONS: AspectRatio[] = ['1:1', '3:4', '4:3', '9:16', '16:9'];

export const GenerationMetaBar: React.FC<GenerationMetaBarProps> = ({
  item,
  onSelectPrompt,
}) => {
  const mode = item.generationMode;
  const modeInfo = MODE_CONFIG[mode];

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
      }}
    >
      {/* ─── Row: Generation Mode Badge + Aspect Ratio ─── */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 10,
          flexWrap: 'wrap',
        }}
      >
        {/* Generation Mode Badge */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            padding: '5px 12px',
            borderRadius: 6,
            fontSize: 11,
            fontWeight: 700,
            background: modeInfo.bg,
            color: modeInfo.color,
            border: `1px solid ${modeInfo.border}`,
            boxShadow: `0 0 8px ${modeInfo.bg}`,
          }}
        >
          <span style={{ fontSize: 14 }}>{modeInfo.icon}</span>
          <span>{modeInfo.label}</span>
        </div>

        {/* Aspect Ratio Display */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            padding: '5px 10px',
            borderRadius: 6,
            fontSize: 11,
            fontWeight: 700,
            background: 'rgba(30, 41, 59, 0.7)',
            color: '#94a3b8',
            border: '1px solid rgba(255, 255, 255, 0.1)',
          }}
        >
          <Ratio size={13} />
          <span>Tỷ Lệ:</span>
          {ASPECT_OPTIONS.map((ratio) => {
            const isActive = ratio === item.aspectRatio;
            return (
              <span
                key={ratio}
                style={{
                  padding: '2px 8px',
                  borderRadius: 4,
                  fontSize: 10,
                  fontWeight: isActive ? 800 : 500,
                  background: isActive
                    ? 'linear-gradient(135deg, rgba(99, 102, 241, 0.4), rgba(124, 58, 237, 0.5))'
                    : 'transparent',
                  color: isActive ? '#e0e7ff' : '#64748b',
                  border: isActive
                    ? '1px solid rgba(99, 102, 241, 0.6)'
                    : '1px solid transparent',
                  cursor: 'default',
                  transition: 'all 0.15s ease',
                }}
              >
                {ratio}
              </span>
            );
          })}
        </div>
      </div>

      {/* ─── Reference Image Info Panel ─── */}
      {item.refAngleImageId && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 10,
            padding: '8px 14px',
            borderRadius: 8,
            background: 'linear-gradient(135deg, rgba(167, 139, 250, 0.1), rgba(99, 102, 241, 0.08))',
            border: '1px solid rgba(167, 139, 250, 0.35)',
            boxShadow: '0 2px 10px rgba(167, 139, 250, 0.15)',
          }}
        >
          <Image size={16} color="#a78bfa" style={{ flexShrink: 0 }} />
          <div style={{ flex: 1 }}>
            <div style={{ fontSize: 11.5, fontWeight: 700, color: '#c4b5fd' }}>
              📎 Cần ảnh tham chiếu: {item.refAngleLabel || item.refAngleImageId}
            </div>
            <div style={{ fontSize: 10.5, color: '#94a3b8', marginTop: 2, lineHeight: 1.4 }}>
              {mode === 'image_to_image'
                ? 'Upload/attach ảnh nhân vật gốc đã tạo trước đó làm đầu vào cho AI Image Generator.'
                : 'Upload/attach ảnh góc cơ thể tương ứng làm START FRAME cho AI Video Generator (Kling/Veo/Hailuo).'}
            </div>
          </div>

          {/* Navigation button to reference prompt */}
          {onSelectPrompt && (
            <button
              onClick={() => onSelectPrompt(item.refAngleImageId!)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '5px 10px',
                borderRadius: 6,
                fontSize: 10,
                fontWeight: 700,
                cursor: 'pointer',
                border: '1px solid rgba(167, 139, 250, 0.5)',
                background: 'rgba(167, 139, 250, 0.15)',
                color: '#c4b5fd',
                transition: 'all 0.15s ease',
                whiteSpace: 'nowrap',
              }}
              onMouseEnter={(e) => {
                e.currentTarget.style.background = 'rgba(167, 139, 250, 0.3)';
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.background = 'rgba(167, 139, 250, 0.15)';
              }}
            >
              <ArrowRight size={12} />
              Xem prompt tạo ảnh gốc
            </button>
          )}
        </div>
      )}
    </div>
  );
};
