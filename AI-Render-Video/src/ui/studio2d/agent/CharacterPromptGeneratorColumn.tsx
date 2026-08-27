import React, { useState } from 'react';
import {
  Sparkles,
  Upload,
  Wand2,
  Image as ImageIcon,
  ArrowRight,
  Palette,
  Check,
  RefreshCw,
  Sliders,
} from 'lucide-react';
import {
  CHARACTER_STYLE_PRESETS,
  CharacterPresetOption,
} from '../../../core/utils/AntigravityDecomposerService';

import { generateCharacterWithAI } from '../../../core/utils/AntigravityAIImageGenerator';

interface CharacterPromptGeneratorColumnProps {
  characterImageUrl: string | null;
  onCharacterGenerated: (imageUrl: string, promptText: string) => void;
  onSendToDecomposer: () => void;
  isGenerating: boolean;
  setIsGenerating: (generating: boolean) => void;
}

export const CharacterPromptGeneratorColumn: React.FC<CharacterPromptGeneratorColumnProps> = ({
  characterImageUrl,
  onCharacterGenerated,
  onSendToDecomposer,
  isGenerating,
  setIsGenerating,
}) => {
  const [characterPrompt, setCharacterPrompt] = useState<string>(
    'Nữ hiệp sĩ anime tóc đỏ rực rỡ, áo giáp bạc ánh kim, kiếm phát sáng, mắt xanh biếc'
  );
  const [bgType, setBgType] = useState<'chroma_green' | 'pure_white'>('chroma_green');
  const [aspectRatio, setAspectRatio] = useState<'16:9' | '1:1' | '3:4' | '9:16'>('16:9');
  const [statusMessage, setStatusMessage] = useState<string>('');

  // Handle File Upload from computer
  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      const url = URL.createObjectURL(file);
      onCharacterGenerated(url, file.name);
    }
  };

  // Handle Generate Character with Real AI / Smart Procedural Synthesizer
  const handleGenerateCharacter = async () => {
    if (!characterPrompt.trim()) return;
    setIsGenerating(true);
    setStatusMessage('Đang gửi prompt đến Antigravity AI Engine...');

    try {
      const result = await generateCharacterWithAI(
        {
          prompt: characterPrompt,
          bgType,
          aspectRatio,
        },
        (msg) => setStatusMessage(msg)
      );

      onCharacterGenerated(result.imageUrl, characterPrompt);
      setStatusMessage('✓ Đã tạo thành công nhân vật theo prompt!');
    } catch (err: any) {
      console.error('Generation failed:', err);
      setStatusMessage('Lỗi sinh ảnh, vui lòng thử lại.');
    } finally {
      setIsGenerating(false);
    }
  };

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        background: '#090d16',
        borderRadius: 10,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        overflow: 'hidden',
      }}
    >
      {/* Column Header */}
      <div
        style={{
          padding: '10px 14px',
          background: 'rgba(15, 23, 42, 0.8)',
          borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <div
            style={{
              width: 26,
              height: 26,
              borderRadius: 6,
              background: 'linear-gradient(135deg, #0284c7, #2563eb)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#fff',
              fontSize: 12,
              fontWeight: 800,
            }}
          >
            1
          </div>
          <div>
            <div style={{ fontSize: 12.5, fontWeight: 700, color: '#f8fafc' }}>
              Tạo Nhân Vật Gốc Bằng AI
            </div>
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>
              Gõ mô tả hoặc tải ảnh từ máy tính
            </div>
          </div>
        </div>
        <label
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '4px 8px',
            borderRadius: 5,
            background: 'rgba(255, 255, 255, 0.06)',
            border: '1px solid rgba(255, 255, 255, 0.12)',
            color: '#cbd5e1',
            fontSize: 10.5,
            fontWeight: 600,
            cursor: 'pointer',
          }}
        >
          <Upload size={12} /> Tải ảnh lên
          <input
            type="file"
            accept=".jpg,.jpeg,.png,.webp,.gif,.bmp"
            onChange={handleFileUpload}
            style={{ display: 'none' }}
          />
        </label>
      </div>

      {/* Column Body: Scrollable */}
      <div
        style={{
          flex: 1,
          padding: 12,
          display: 'flex',
          flexDirection: 'column',
          gap: 12,
          overflowY: 'auto',
        }}
      >
        {/* Prompt Input Box */}
        <div>
          <div style={{ fontSize: 10.5, fontWeight: 600, color: '#cbd5e1', marginBottom: 5, display: 'flex', alignItems: 'center', gap: 5 }}>
            <Wand2 size={12} color="#a855f7" /> Prompt mô tả nhân vật:
          </div>
          <textarea
            value={characterPrompt}
            onChange={(e) => setCharacterPrompt(e.target.value)}
            rows={4}
            placeholder="Nhập mô tả ngoại hình, trang phục, màu tóc, vũ khí của nhân vật..."
            style={{
              width: '100%',
              padding: '8px 10px',
              borderRadius: 6,
              background: '#040711',
              border: '1px solid rgba(255,255,255,0.14)',
              color: '#f8fafc',
              fontSize: 11,
              lineHeight: 1.4,
              resize: 'none',
              outline: 'none',
              boxSizing: 'border-box',
            }}
          />
        </div>

        {/* Options Row: Background & Aspect Ratio */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
          <div>
            <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 3 }}>
              Màu nền tách phông:
            </label>
            <select
              value={bgType}
              onChange={(e) => setBgType(e.target.value as any)}
              style={{
                width: '100%',
                height: 28,
                padding: '0 6px',
                borderRadius: 5,
                background: '#040711',
                border: '1px solid rgba(255,255,255,0.12)',
                color: '#38bdf8',
                fontSize: 10.5,
              }}
            >
              <option value="chroma_green">🟩 Nền Xanh Lá (#00FF00)</option>
              <option value="pure_white">⬜ Nền Trắng (#FFFFFF)</option>
            </select>
          </div>

          <div>
            <label style={{ fontSize: 10, color: '#94a3b8', display: 'block', marginBottom: 3 }}>
              Tỷ lệ khung hình:
            </label>
            <select
              value={aspectRatio}
              onChange={(e) => setAspectRatio(e.target.value as any)}
              style={{
                width: '100%',
                height: 28,
                padding: '0 6px',
                borderRadius: 5,
                background: '#040711',
                border: '1px solid rgba(255,255,255,0.12)',
                color: '#cbd5e1',
                fontSize: 10.5,
              }}
            >
              <option value="16:9">16:9 Rộng Ngang (Chuẩn Sprite Điện Ảnh)</option>
              <option value="1:1">1:1 Vuông (Square)</option>
              <option value="3:4">3:4 Chân dung (Portrait)</option>
              <option value="9:16">9:16 Toàn thân (Full Body)</option>
            </select>
          </div>
        </div>

        {/* Generate Character Button */}
        <button
          onClick={handleGenerateCharacter}
          disabled={isGenerating || !characterPrompt.trim()}
          style={{
            height: 34,
            borderRadius: 6,
            background: isGenerating
              ? 'rgba(255,255,255,0.1)'
              : 'linear-gradient(135deg, #0284c7 0%, #2563eb 100%)',
            color: '#ffffff',
            fontSize: 11.5,
            fontWeight: 700,
            border: '1px solid rgba(255,255,255,0.2)',
            cursor: isGenerating ? 'not-allowed' : 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 6,
            boxShadow: '0 2px 10px rgba(2, 132, 199, 0.35)',
          }}
        >
          {isGenerating ? (
            <>
              <RefreshCw size={14} className="animate-spin" /> Antigravity đang sinh nhân vật...
            </>
          ) : (
            <>
              <Sparkles size={14} /> ⚡ Tạo Nhân Vật Mới Bằng AI
            </>
          )}
        </button>

        {statusMessage && (
          <div
            style={{
              fontSize: 10,
              color: statusMessage.startsWith('✓') ? '#4ade80' : '#38bdf8',
              textAlign: 'center',
              fontWeight: 600,
              padding: '2px 6px',
            }}
          >
            {statusMessage}
          </div>
        )}

        {/* Character Preview Canvas Box */}
        <div
          style={{
            flex: 1,
            minHeight: 180,
            borderRadius: 8,
            border: '1px dashed rgba(255, 255, 255, 0.15)',
            background: '#040711',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            position: 'relative',
            overflow: 'hidden',
          }}
        >
          {characterImageUrl ? (
            <img
              src={characterImageUrl}
              alt="Character Source"
              style={{
                maxWidth: '100%',
                maxHeight: '100%',
                objectFit: 'contain',
              }}
            />
          ) : (
            <div style={{ textAlign: 'center', color: '#64748b' }}>
              <ImageIcon size={32} style={{ margin: '0 auto 6px', opacity: 0.5 }} />
              <div style={{ fontSize: 11 }}>Chưa có nhân vật nào được chọn</div>
              <div style={{ fontSize: 9.5 }}>Nhấn Tạo AI hoặc tải ảnh từ máy tính</div>
            </div>
          )}
        </div>
      </div>

      {/* Column Footer: Send to Column 2 Button */}
      <div
        style={{
          padding: '10px 12px',
          background: 'rgba(15, 23, 42, 0.9)',
          borderTop: '1px solid rgba(255, 255, 255, 0.08)',
        }}
      >
        <button
          onClick={onSendToDecomposer}
          disabled={!characterImageUrl}
          style={{
            width: '100%',
            height: 36,
            borderRadius: 6,
            background: characterImageUrl
              ? 'linear-gradient(135deg, #10b981 0%, #059669 100%)'
              : 'rgba(255,255,255,0.06)',
            color: characterImageUrl ? '#ffffff' : '#64748b',
            fontSize: 11.5,
            fontWeight: 700,
            border: characterImageUrl ? '1px solid #34d399' : 'none',
            cursor: characterImageUrl ? 'pointer' : 'not-allowed',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 6,
            boxShadow: characterImageUrl ? '0 2px 10px rgba(16, 185, 129, 0.35)' : 'none',
          }}
        >
          <span>🚀 Chuyển Sang Cột 2 (Tách Chi Tiết)</span>
          <ArrowRight size={14} />
        </button>
      </div>
    </div>
  );
};
