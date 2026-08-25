import React from 'react';
import { Sparkles, RefreshCw } from 'lucide-react';

export interface SlicerAIMattingCardProps {
  aiServerStatus: 'online' | 'offline' | 'checking';
  aiModel: string;
  setAiModel: (m: string) => void;
  aiScope: 'full_image' | 'all' | 'selected';
  setAiScope: (s: 'full_image' | 'all' | 'selected') => void;
  totalCellCount: number;
  isAIRunning: boolean;
  onRunAIMatting: () => void;
}

export const SlicerAIMattingCard: React.FC<SlicerAIMattingCardProps> = ({
  aiServerStatus,
  aiModel,
  setAiModel,
  aiScope,
  setAiScope,
  totalCellCount,
  isAIRunning,
  onRunAIMatting,
}) => {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      {/* Server Status Header */}
      <div
        style={{
          background: aiServerStatus === 'online' ? 'rgba(34, 197, 94, 0.15)' : 'rgba(239, 68, 68, 0.15)',
          border: aiServerStatus === 'online' ? '1px solid #22c55e' : '1px solid #ef4444',
          padding: '6px 8px',
          borderRadius: 6,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: 9.5, fontWeight: 600, color: aiServerStatus === 'online' ? '#4ade80' : '#f87171' }}>
          <span style={{ width: 8, height: 8, borderRadius: '50%', background: aiServerStatus === 'online' ? '#22c55e' : '#ef4444', display: 'inline-block', boxShadow: aiServerStatus === 'online' ? '0 0 8px #22c55e' : 'none' }}></span>
          {aiServerStatus === 'online' ? 'GPU CUDA sẵn sàng (RTX 3060)' : 'Chưa khởi động Server AI Local'}
        </div>
        <span style={{ fontSize: 8.5, color: '#94a3b8' }}>:5000</span>
      </div>

      {/* Model Selection */}
      <div>
        <div style={{ fontSize: 9.5, color: '#c084fc', marginBottom: 4, fontWeight: 600, display: 'flex', alignItems: 'center', gap: 4 }}>
          <Sparkles size={12} /> Chọn Model AI chuyên dụng:
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          {[
            { id: 'isnet-anime', name: '🌸 ISNet-Anime', desc: 'Chuyên bóc tách Anime, 2D Sprite & Lineart', tag: '⚡ 0.15s' },
            { id: 'birefnet-general', name: '🌟 BiRefNet General', desc: 'SOTA 2025 - Chuẩn từng sợi tóc & viền mềm', tag: '💎 Chuẩn nét' },
            { id: 'u2net', name: '⚡ U2Net Standard', desc: 'Bóc tách nền tổng quát cân bằng', tag: '⚡ 0.15s' },
            { id: 'birefnet-portrait', name: '👑 BiRefNet Portrait', desc: 'Chuyên chân dung, mái tóc & trang phục', tag: 'Chân Dung' },
          ].map((m) => (
            <div
              key={m.id}
              onClick={() => setAiModel(m.id)}
              style={{
                background: aiModel === m.id ? 'rgba(168, 85, 247, 0.25)' : 'rgba(0,0,0,0.3)',
                border: aiModel === m.id ? '1.5px solid #a855f7' : '1px solid rgba(255,255,255,0.06)',
                borderRadius: 5,
                padding: '5px 7px',
                cursor: 'pointer',
                display: 'flex',
                flexDirection: 'column',
                gap: 2,
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontSize: 10, fontWeight: 600, color: aiModel === m.id ? '#f3e8ff' : '#cbd5e1' }}>{m.name}</span>
                <span style={{ fontSize: 8, padding: '1px 4px', borderRadius: 3, background: aiModel === m.id ? '#9333ea' : 'rgba(255,255,255,0.1)', color: '#fff' }}>{m.tag}</span>
              </div>
              <div style={{ fontSize: 8.5, color: aiModel === m.id ? '#d8b4fe' : '#64748b' }}>{m.desc}</div>
            </div>
          ))}
        </div>
      </div>

      {/* Scope Selection */}
      <div>
        <div style={{ fontSize: 9.5, color: '#94a3b8', marginBottom: 4, fontWeight: 600 }}>Phạm vi xử lý:</div>
        <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr 1fr', gap: 5 }}>
          <button
            onClick={() => setAiScope('full_image')}
            style={{
              height: 30,
              fontSize: 9.5,
              fontWeight: 600,
              borderRadius: 5,
              border: aiScope === 'full_image' ? '1.5px solid #a855f7' : '1px solid rgba(255,255,255,0.1)',
              background: aiScope === 'full_image' ? 'rgba(168, 85, 247, 0.3)' : 'rgba(0,0,0,0.3)',
              color: aiScope === 'full_image' ? '#f3e8ff' : '#cbd5e1',
              cursor: 'pointer',
            }}
          >
            🖼️ Toàn bộ ảnh
          </button>
          <button
            onClick={() => setAiScope('all')}
            style={{
              height: 30,
              fontSize: 9.5,
              fontWeight: 600,
              borderRadius: 5,
              border: aiScope === 'all' ? '1.5px solid #a855f7' : '1px solid rgba(255,255,255,0.1)',
              background: aiScope === 'all' ? 'rgba(168, 85, 247, 0.25)' : 'rgba(0,0,0,0.3)',
              color: aiScope === 'all' ? '#f3e8ff' : '#94a3b8',
              cursor: 'pointer',
            }}
          >
            🧩 Từng ô ({totalCellCount})
          </button>
          <button
            onClick={() => setAiScope('selected')}
            style={{
              height: 30,
              fontSize: 9.5,
              fontWeight: 600,
              borderRadius: 5,
              border: aiScope === 'selected' ? '1.5px solid #a855f7' : '1px solid rgba(255,255,255,0.1)',
              background: aiScope === 'selected' ? 'rgba(168, 85, 247, 0.25)' : 'rgba(0,0,0,0.3)',
              color: aiScope === 'selected' ? '#f3e8ff' : '#94a3b8',
              cursor: 'pointer',
            }}
          >
            🎯 Ô đang chọn
          </button>
        </div>
      </div>

      {/* Run AI Button */}
      <button
        onClick={onRunAIMatting}
        disabled={isAIRunning}
        style={{
          width: '100%',
          height: 36,
          fontSize: 11,
          fontWeight: 700,
          borderRadius: 6,
          background: isAIRunning
            ? 'rgba(255,255,255,0.1)'
            : 'linear-gradient(135deg, #9333ea 0%, #d946ef 100%)',
          color: '#ffffff',
          border: '1px solid rgba(255,255,255,0.3)',
          cursor: isAIRunning ? 'not-allowed' : 'pointer',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 6,
          boxShadow: '0 3px 12px rgba(147, 51, 234, 0.45)',
        }}
      >
        {isAIRunning ? (
          <>
            <RefreshCw size={14} className="animate-spin" /> Đang chạy Model AI GPU...
          </>
        ) : (
          <>
            <Sparkles size={14} /> 🚀 Tách nền bằng Model AI (GPU)
          </>
        )}
      </button>

      {/* Server Offline Tip */}
      {aiServerStatus !== 'online' && (
        <div style={{ background: 'rgba(0,0,0,0.4)', padding: 7, borderRadius: 5, border: '1px dashed #a855f7', fontSize: 8.5, color: '#d8b4fe' }}>
          <div style={{ fontWeight: 600, marginBottom: 3, color: '#f3e8ff' }}>💡 Cách chạy Server AI trên máy:</div>
          <div>Chạy file <code style={{ background: '#1e1b4b', padding: '1px 4px', borderRadius: 3, color: '#38bdf8' }}>run_ai_matting_server.bat</code> trong thư mục dự án.</div>
        </div>
      )}
    </div>
  );
};
