import React from 'react';
import { Sparkles, Sliders, Copy, Check } from 'lucide-react';

interface PromptHeaderProps {
  activeTitle: string;
  totalCount: number;
  showCustomizer: boolean;
  onToggleCustomizer: () => void;
  onCopyActive: () => void;
  copied: boolean;
}

export const PromptHeader: React.FC<PromptHeaderProps> = ({
  activeTitle,
  totalCount,
  showCustomizer,
  onToggleCustomizer,
  onCopyActive,
  copied,
}) => {
  return (
    <div
      style={{
        background: 'linear-gradient(135deg, rgba(30, 27, 75, 0.95) 0%, rgba(49, 46, 129, 0.9) 50%, rgba(88, 28, 135, 0.9) 100%)',
        border: '1px solid rgba(168, 85, 247, 0.3)',
        borderRadius: 12,
        padding: '14px 20px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexWrap: 'wrap',
        gap: 12,
        boxShadow: '0 8px 32px rgba(0, 0, 0, 0.37), 0 0 20px rgba(168, 85, 247, 0.15)',
        flexShrink: 0,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
        <div
          style={{
            width: 44,
            height: 44,
            borderRadius: 10,
            background: 'linear-gradient(135deg, #6366f1, #a855f7)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#fff',
            boxShadow: '0 0 16px rgba(168, 85, 247, 0.5)',
            fontSize: 22,
          }}
        >
          🎬
        </div>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <h1 style={{ fontSize: 17, fontWeight: 800, color: '#ffffff', margin: 0, letterSpacing: '0.3px' }}>
              Character Animation Prompts Generator
            </h1>
            <span
              style={{
                fontSize: 10,
                fontWeight: 700,
                padding: '2px 8px',
                borderRadius: 20,
                background: 'rgba(56, 189, 248, 0.2)',
                color: '#38bdf8',
                border: '1px solid rgba(56, 189, 248, 0.4)',
              }}
            >
              {totalCount} Mẫu Prompt Chuẩn
            </span>
          </div>
          <p style={{ fontSize: 11.5, color: '#cbd5e1', margin: '3px 0 0 0', opacity: 0.9 }}>
            Tạo nhân vật • Xoay góc 360° • Đi bộ theo góc • Chạy • Ngồi (không ghế) • Nằm (không giường) • Nhảy • Vũ khí
          </p>
        </div>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <button
          onClick={onToggleCustomizer}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            padding: '8px 14px',
            borderRadius: 8,
            fontSize: 11.5,
            fontWeight: 700,
            cursor: 'pointer',
            border: showCustomizer ? '1px solid #38bdf8' : '1px solid rgba(255, 255, 255, 0.15)',
            background: showCustomizer ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255, 255, 255, 0.07)',
            color: showCustomizer ? '#38bdf8' : '#e2e8f0',
            transition: 'all 0.2s ease',
          }}
        >
          <Sliders size={14} />
          {showCustomizer ? 'Ẩn Bộ Chỉnh Tham Số' : '⚙️ Tùy Biến Thông Số'}
        </button>

        <button
          onClick={onCopyActive}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            padding: '8px 16px',
            borderRadius: 8,
            fontSize: 11.5,
            fontWeight: 700,
            cursor: 'pointer',
            border: 'none',
            background: copied
              ? 'linear-gradient(135deg, #10b981, #059669)'
              : 'linear-gradient(135deg, #6366f1, #8b5cf6)',
            color: '#ffffff',
            boxShadow: copied
              ? '0 0 16px rgba(16, 185, 129, 0.5)'
              : '0 0 16px rgba(99, 102, 241, 0.4)',
            transition: 'all 0.2s ease',
          }}
        >
          {copied ? <Check size={14} /> : <Copy size={14} />}
          {copied ? 'Đã Copy Thành Công!' : '📋 Copy Prompt Nhanh'}
        </button>
      </div>
    </div>
  );
};
