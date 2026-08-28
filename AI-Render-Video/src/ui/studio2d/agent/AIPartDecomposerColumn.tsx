import React, { useState } from 'react';
import {
  Scissors,
  Sparkles,
  Layers,
  Terminal,
  CheckSquare,
  Square,
  Wand2,
  RefreshCw,
  ArrowRight,
  Eye,
  Shield,
  Zap,
} from 'lucide-react';
import {
  PART_DECOMPOSITION_TEMPLATES,
  PartDecompositionTemplate,
  DecomposedPartItem,
} from '../../../core/utils/AntigravityDecomposerService';

interface AIPartDecomposerColumnProps {
  characterImageUrl: string | null;
  selectedTemplateIds: Set<string>;
  onToggleTemplate: (templateId: string) => void;
  onSelectAllTemplates: () => void;
  onClearAllTemplates: () => void;
  customDecompositionPrompt: string;
  setCustomDecompositionPrompt: (prompt: string) => void;
  isDecomposing: boolean;
  onExecuteDecomposition: () => void;
  agentLogs: string[];
}

export const AIPartDecomposerColumn: React.FC<AIPartDecomposerColumnProps> = ({
  characterImageUrl,
  selectedTemplateIds,
  onToggleTemplate,
  onSelectAllTemplates,
  onClearAllTemplates,
  customDecompositionPrompt,
  setCustomDecompositionPrompt,
  isDecomposing,
  onExecuteDecomposition,
  agentLogs,
}) => {
  const [activeCategoryFilter, setActiveCategoryFilter] = useState<
    'all' | 'hair' | 'face' | 'torso' | 'limbs' | 'props'
  >('all');

  const filteredTemplates =
    activeCategoryFilter === 'all'
      ? PART_DECOMPOSITION_TEMPLATES
      : PART_DECOMPOSITION_TEMPLATES.filter(
          (t) => t.category === activeCategoryFilter
        );

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
              background: 'linear-gradient(135deg, #8b5cf6, #d946ef)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#fff',
              fontSize: 12,
              fontWeight: 800,
            }}
          >
            2
          </div>
          <div>
            <div style={{ fontSize: 12.5, fontWeight: 700, color: '#f8fafc' }}>
              AI Tách Linh Kiện & Bộ Phận
            </div>
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>
              Chọn linh kiện cần bóc tách hoặc nhập prompt chi tiết
            </div>
          </div>
        </div>

        {/* Quick Selection Buttons */}
        <div style={{ display: 'flex', gap: 4 }}>
          <button
            onClick={onSelectAllTemplates}
            style={{
              padding: '3px 7px',
              fontSize: 9.5,
              fontWeight: 600,
              borderRadius: 4,
              border: '1px solid rgba(255,255,255,0.1)',
              background: 'rgba(255,255,255,0.06)',
              color: '#cbd5e1',
              cursor: 'pointer',
            }}
          >
            Chọn tất cả
          </button>
          <button
            onClick={onClearAllTemplates}
            style={{
              padding: '3px 7px',
              fontSize: 9.5,
              fontWeight: 600,
              borderRadius: 4,
              border: '1px solid rgba(255,255,255,0.1)',
              background: 'rgba(255,255,255,0.06)',
              color: '#94a3b8',
              cursor: 'pointer',
            }}
          >
            Bỏ chọn
          </button>
        </div>
      </div>

      {/* Column Body: Scrollable */}
      <div
        style={{
          flex: 1,
          padding: 12,
          display: 'flex',
          flexDirection: 'column',
          gap: 10,
          overflowY: 'auto',
        }}
      >
        {/* Category Tabs */}
        <div
          style={{
            display: 'flex',
            gap: 4,
            background: '#040711',
            padding: 3,
            borderRadius: 6,
            border: '1px solid rgba(255,255,255,0.06)',
            overflowX: 'auto',
          }}
        >
          {(
            [
              { id: 'all', label: 'Tất cả' },
              { id: 'hair', label: '💇 Tóc' },
              { id: 'face', label: '👤 Mặt' },
              { id: 'torso', label: '🥋 Thân' },
              { id: 'limbs', label: '🦾 Chi' },
              { id: 'props', label: '⚔️ Phụ kiện' },
            ] as const
          ).map((cat) => (
            <button
              key={cat.id}
              onClick={() => setActiveCategoryFilter(cat.id)}
              style={{
                flex: 1,
                padding: '4px 6px',
                borderRadius: 4,
                border: 'none',
                background:
                  activeCategoryFilter === cat.id ? '#8b5cf6' : 'transparent',
                color: activeCategoryFilter === cat.id ? '#ffffff' : '#94a3b8',
                fontSize: 9.5,
                fontWeight: activeCategoryFilter === cat.id ? 700 : 500,
                cursor: 'pointer',
                whiteSpace: 'nowrap',
              }}
            >
              {cat.label}
            </button>
          ))}
        </div>

        {/* Templates Checklist Grid */}
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(2, 1fr)',
            gap: 6,
            maxHeight: 220,
            overflowY: 'auto',
            paddingRight: 2,
          }}
        >
          {filteredTemplates.map((template) => {
            const isSelected = selectedTemplateIds.has(template.id);
            return (
              <div
                key={template.id}
                onClick={() => onToggleTemplate(template.id)}
                style={{
                  padding: '6px 8px',
                  borderRadius: 6,
                  border: isSelected
                    ? '1.5px solid #a855f7'
                    : '1px solid rgba(255,255,255,0.07)',
                  background: isSelected
                    ? 'rgba(168, 85, 247, 0.15)'
                    : 'rgba(0,0,0,0.3)',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 6,
                }}
              >
                {isSelected ? (
                  <CheckSquare size={13} color="#c084fc" />
                ) : (
                  <Square size={13} color="#64748b" />
                )}
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div
                    style={{
                      fontSize: 10,
                      fontWeight: isSelected ? 700 : 500,
                      color: isSelected ? '#f1f5f9' : '#94a3b8',
                      whiteSpace: 'nowrap',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                    }}
                  >
                    {template.name}
                  </div>
                  <div style={{ fontSize: 8.5, color: '#64748b' }}>
                    Layer Z: {template.zIndex}
                  </div>
                </div>
              </div>
            );
          })}
        </div>

        {/* Custom Prompt for Part Extraction */}
        <div>
          <div
            style={{
              fontSize: 10.5,
              fontWeight: 600,
              color: '#cbd5e1',
              marginBottom: 4,
              display: 'flex',
              alignItems: 'center',
              gap: 5,
            }}
          >
            <Wand2 size={12} color="#ec4899" /> Prompt tùy biến tách linh kiện:
          </div>
          <textarea
            value={customDecompositionPrompt}
            onChange={(e) => setCustomDecompositionPrompt(e.target.value)}
            rows={3}
            placeholder="Ví dụ: Tách riêng áo choàng lụa mỏng bay phất phới, kiếm ánh sáng phát quang, giữ độ trong suốt..."
            style={{
              width: '100%',
              padding: '6px 8px',
              borderRadius: 6,
              background: '#040711',
              border: '1px solid rgba(255,255,255,0.14)',
              color: '#f8fafc',
              fontSize: 10.5,
              lineHeight: 1.35,
              resize: 'none',
              outline: 'none',
              boxSizing: 'border-box',
            }}
          />
        </div>

        {/* Antigravity AI Agent Live Thinking Log */}
        <div
          style={{
            flex: 1,
            minHeight: 120,
            background: '#020617',
            borderRadius: 6,
            border: '1px solid rgba(255,255,255,0.08)',
            padding: 8,
            display: 'flex',
            flexDirection: 'column',
            gap: 4,
            overflowY: 'auto',
          }}
        >
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              fontSize: 9.5,
              fontWeight: 700,
              color: '#38bdf8',
              borderBottom: '1px solid rgba(255,255,255,0.06)',
              paddingBottom: 4,
            }}
          >
            <Terminal size={12} /> Antigravity AI Agent Thought Stream
          </div>
          <div
            style={{
              flex: 1,
              fontFamily: 'monospace',
              fontSize: 9.5,
              color: '#94a3b8',
              lineHeight: 1.4,
              display: 'flex',
              flexDirection: 'column',
              gap: 3,
            }}
          >
            {agentLogs.length === 0 ? (
              <span style={{ color: '#475569' }}>
                Đang chờ lệnh... Chọn linh kiện và bấm nút Tách Linh Kiện bên dưới.
              </span>
            ) : (
              agentLogs.map((log, index) => (
                <div key={index} style={{ color: index === agentLogs.length - 1 ? '#4ade80' : '#94a3b8' }}>
                  {log}
                </div>
              ))
            )}
          </div>
        </div>
      </div>

      {/* Column Footer: Execute Decomposition Button */}
      <div
        style={{
          padding: '10px 12px',
          background: 'rgba(15, 23, 42, 0.9)',
          borderTop: '1px solid rgba(255, 255, 255, 0.08)',
        }}
      >
        <button
          onClick={onExecuteDecomposition}
          disabled={
            !characterImageUrl ||
            (selectedTemplateIds.size === 0 && !customDecompositionPrompt.trim()) ||
            isDecomposing
          }
          style={{
            width: '100%',
            height: 36,
            borderRadius: 6,
            background: isDecomposing
              ? 'rgba(255,255,255,0.1)'
              : 'linear-gradient(135deg, #8b5cf6 0%, #d946ef 100%)',
            color: '#ffffff',
            fontSize: 11.5,
            fontWeight: 700,
            border: '1px solid rgba(255,255,255,0.25)',
            cursor:
              !characterImageUrl || isDecomposing ? 'not-allowed' : 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 6,
            boxShadow: '0 2px 10px rgba(139, 92, 246, 0.35)',
          }}
        >
          {isDecomposing ? (
            <>
              <RefreshCw size={14} className="animate-spin" /> AI đang bóc tách linh kiện ({selectedTemplateIds.size} chi tiết)...
            </>
          ) : (
            <>
              <Sparkles size={14} /> Kích Hoạt Antigravity AI Tách Chi Tiết
            </>
          )}
        </button>
      </div>
    </div>
  );
};
