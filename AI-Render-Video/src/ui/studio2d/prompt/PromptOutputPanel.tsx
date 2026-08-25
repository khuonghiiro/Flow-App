import React from 'react';
import { BookOpen, Check, Copy } from 'lucide-react';

interface PromptOutputPanelProps {
  workflowTab: 'step1_master' | 'step2_decomposed_parts' | 'step3_actions';
  promptFormatTab: 'en' | 'vi' | 'json';
  setPromptFormatTab: (tab: 'en' | 'vi' | 'json') => void;
  activePromptText: string;
  copiedPrompt: string | null;
  handleCopy: (text: string, id: string) => void;
  batchCount: number;
  setBatchCount: (count: number) => void;
  includeBasePrompt: boolean;
  setIncludeBasePrompt: (inc: boolean) => void;
  jsonExportScope: 'component_all_angles' | 'single_angle' | 'group_all_parts';
  setJsonExportScope: (scope: 'component_all_angles' | 'single_angle' | 'group_all_parts') => void;
  step2Layout: string;
  activeGroupItemCount: number;
  onOpenSchemaGuide: () => void;
  cameraGuide?: string;
}

export const PromptOutputPanel: React.FC<PromptOutputPanelProps> = ({
  workflowTab,
  promptFormatTab,
  setPromptFormatTab,
  activePromptText,
  copiedPrompt,
  handleCopy,
  batchCount,
  setBatchCount,
  includeBasePrompt,
  setIncludeBasePrompt,
  jsonExportScope,
  setJsonExportScope,
  step2Layout,
  activeGroupItemCount,
  onOpenSchemaGuide,
  cameraGuide,
}) => {
  const charLength = activePromptText.length;
  const isOverLimit = charLength > 4000;

  const renderHeaderTitle = () => {
    if (workflowTab === 'step1_master') {
      return '👤 BƯỚC 1: PROMPT BẢNG XOAY NHÂN VẬT GỐC (16:9 — 5 GÓC + ĐỈNH ĐẦU)';
    }
    if (workflowTab === 'step2_decomposed_parts') {
      if (step2Layout === 'single_isolated_1x1') {
        return '✂️ BƯỚC 2: BÓC TÁCH ẢNH ĐƠN 4K BIỆT LẬP (KHÔNG LƯỚI)';
      }
      if (step2Layout === 'seamless_turnaround_1x4') {
        return '✂️ BƯỚC 2: CHUỖI XOAY 4 GÓC LIỀN MẠCH (1 HÀNG)';
      }
      return '✂️ BƯỚC 2: BÓC TÁCH 6 GÓC ĐIỆN ẢNH (LƯỚI 2×3)';
    }
    return '⚔️ BƯỚC 3: KỊCH BẢN HÀNH ĐỘNG & BIỂU CẢM 4K';
  };

  const getCopyButtonLabel = () => {
    if (copiedPrompt === 'main_copy') {
      return '✓ ĐÃ SAO CHÉP THÀNH CÔNG!';
    }
    if (workflowTab === 'step3_actions') {
      return '🎬 SAO CHÉP KỊCH BẢN HÀNH ĐỘNG 4K';
    }
    if (workflowTab === 'step1_master') {
      return promptFormatTab === 'vi'
        ? '📋 SAO CHÉP PROMPT TIẾNG VIỆT (BẢNG XOAY 16:9)'
        : '📋 SAO CHÉP PROMPT TIẾNG ANH (TURNAROUND 16:9 — MIDJOURNEY / SD / GEMINI)';
    }
    // Step 2
    if (promptFormatTab === 'en') {
      return '📋 SAO CHÉP PROMPT TIẾNG ANH LINH KIỆN';
    }
    if (promptFormatTab === 'vi') {
      return '📋 SAO CHÉP PROMPT TIẾNG VIỆT LINH KIỆN';
    }
    if (jsonExportScope === 'single_angle') {
      return '🔍 SAO CHÉP JSON (1 GÓC ĐƠN ĐANG CHỌN)';
    }
    if (jsonExportScope === 'component_all_angles') {
      return '🎯 SAO CHÉP JSON BỘ PHẬN ĐANG CHỌN (TẤT CẢ GÓC)';
    }
    return `📦 SAO CHÉP JSON TOÀN BỘ NHÓM (${activeGroupItemCount} CHI TIẾT)`;
  };

  const getCopyButtonBg = () => {
    if (copiedPrompt === 'main_copy') return '#22c55e';
    if (workflowTab === 'step1_master') return 'linear-gradient(135deg, #0284c7 0%, #2563eb 100%)';
    if (workflowTab === 'step3_actions') return 'linear-gradient(135deg, #a855f7 0%, #7e22ce 100%)';
    // Step 2
    if (promptFormatTab === 'json') {
      if (jsonExportScope === 'single_angle') return 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)';
      if (jsonExportScope === 'component_all_angles') return 'linear-gradient(135deg, #059669 0%, #047857 100%)';
      return 'linear-gradient(135deg, #7c3aed 0%, #6d28d9 100%)';
    }
    return 'linear-gradient(135deg, #0284c7 0%, #2563eb 100%)';
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
        padding: 14,
        overflow: 'hidden',
      }}
    >
      {/* 1. Header Bar with Tabs & Character Counter */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: 10,
          paddingBottom: 8,
          borderBottom: '1px solid rgba(255,255,255,0.08)',
          gap: 8,
          flexWrap: 'wrap',
        }}
      >
        <div style={{ fontSize: 12, fontWeight: 800, color: '#f8fafc', display: 'flex', alignItems: 'center', gap: 6 }}>
          {renderHeaderTitle()}
        </div>

        {workflowTab !== 'step3_actions' && (
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            {/* Character Limit Badge */}
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '3px 8px',
                borderRadius: 6,
                fontSize: 10,
                fontWeight: 700,
                background: !isOverLimit ? 'rgba(34, 197, 94, 0.15)' : 'rgba(239, 68, 68, 0.25)',
                border: !isOverLimit ? '1px solid rgba(34, 197, 94, 0.4)' : '1px solid #ef4444',
                color: !isOverLimit ? '#4ade80' : '#f87171',
              }}
              title="Độ dài khuyến nghị tối đa 4,000 ký tự cho mỗi prompt"
            >
              <span>🍌 Độ dài:</span>
              <span>{charLength.toLocaleString()} / 4,000 ký tự</span>
              <span>{!isOverLimit ? '✅ Hợp lệ' : '⚠️ Quá tải'}</span>
            </div>

            {/* Language / Format Switcher */}
            <div style={{ display: 'flex', gap: 3, background: 'rgba(255,255,255,0.05)', padding: 2, borderRadius: 6 }}>
              {workflowTab === 'step2_decomposed_parts' && (
                <button
                  onClick={() => setPromptFormatTab('json')}
                  style={{
                    padding: '3px 8px',
                    fontSize: 10.5,
                    fontWeight: 700,
                    borderRadius: 4,
                    border: 'none',
                    background: promptFormatTab === 'json' ? '#0284c7' : 'transparent',
                    color: promptFormatTab === 'json' ? '#fff' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  🍌 JSON Banana Pro
                </button>
              )}
              <button
                onClick={() => setPromptFormatTab('en')}
                style={{
                  padding: '3px 8px',
                  fontSize: 10.5,
                  fontWeight: 700,
                  borderRadius: 4,
                  border: 'none',
                  background: promptFormatTab === 'en' ? '#0284c7' : 'transparent',
                  color: promptFormatTab === 'en' ? '#fff' : '#94a3b8',
                  cursor: 'pointer',
                }}
              >
                🇺🇸 Tiếng Anh
              </button>
              <button
                onClick={() => setPromptFormatTab('vi')}
                style={{
                  padding: '3px 8px',
                  fontSize: 10.5,
                  fontWeight: 700,
                  borderRadius: 4,
                  border: 'none',
                  background: promptFormatTab === 'vi' ? '#0284c7' : 'transparent',
                  color: promptFormatTab === 'vi' ? '#fff' : '#94a3b8',
                  cursor: 'pointer',
                }}
              >
                🇻🇳 Tiếng Việt
              </button>
            </div>
          </div>
        )}
      </div>

      {/* 2. Control Toolbar (Chỉ hiển thị cho Step 2) */}
      {workflowTab === 'step2_decomposed_parts' && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: 8,
            padding: '8px 10px',
            background: 'rgba(2, 132, 199, 0.08)',
            borderRadius: 8,
            border: '1px solid rgba(56, 189, 248, 0.2)',
            marginBottom: 10,
          }}
        >
          {/* Synchronized Count Input Section (Max 4: 1, 2, 3, 4) */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ fontSize: 10.5, fontWeight: 800, color: '#facc15' }}>
              🔢 Số ảnh sinh (Count 1-4):
            </span>
            <input
              type="number"
              min={1}
              max={4}
              value={batchCount}
              onChange={(e) => setBatchCount(Math.min(4, Math.max(1, parseInt(e.target.value) || 1)))}
              style={{
                width: 40,
                height: 24,
                padding: '0 4px',
                textAlign: 'center',
                fontSize: 11,
                fontWeight: 700,
                background: '#040711',
                color: '#facc15',
                border: '1px solid rgba(250, 204, 21, 0.5)',
                borderRadius: 4,
              }}
              title="Đồng bộ số lượng biến thể ảnh sinh (count tối đa 4) cho toàn bộ item trong JSON"
            />
            {/* Quick Count Preset Pills: 1, 2, 3, 4 */}
            <div style={{ display: 'flex', gap: 3 }}>
              {[1, 2, 3, 4].map((c) => (
                <button
                  key={c}
                  onClick={() => setBatchCount(c)}
                  style={{
                    padding: '2px 6px',
                    fontSize: 9.5,
                    fontWeight: 700,
                    borderRadius: 3,
                    border: batchCount === c ? '1px solid #eab308' : '1px solid rgba(255,255,255,0.08)',
                    background: batchCount === c ? '#eab308' : 'rgba(255,255,255,0.06)',
                    color: batchCount === c ? '#000' : '#cbd5e1',
                    cursor: 'pointer',
                  }}
                >
                  {c}
                </button>
              ))}
            </div>
          </div>

          {/* Toggles & Schema Guide Button */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            {/* Base Prompt Checkbox Toggle */}
            <label
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 5,
                fontSize: 10.5,
                fontWeight: 600,
                color: includeBasePrompt ? '#38bdf8' : '#94a3b8',
                cursor: 'pointer',
                userSelect: 'none',
              }}
              title="Kèm base_prompt mô tả toàn diện nhân vật để đồng bộ ngoại hình"
            >
              <input
                type="checkbox"
                checked={includeBasePrompt}
                onChange={(e) => setIncludeBasePrompt(e.target.checked)}
                style={{ cursor: 'pointer', accentColor: '#0284c7' }}
              />
              <span>{includeBasePrompt ? '✅ base_prompt' : '⚡ Bỏ base_prompt'}</span>
            </label>

            <button
              onClick={onOpenSchemaGuide}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '3px 8px',
                borderRadius: 4,
                fontSize: 10,
                fontWeight: 700,
                background: 'rgba(168, 85, 247, 0.2)',
                border: '1px solid rgba(168, 85, 247, 0.4)',
                color: '#c084fc',
                cursor: 'pointer',
              }}
            >
              <BookOpen size={12} /> Tra cứu ý nghĩa trường JSON
            </button>
          </div>

          {/* Scope Switcher for Step 2 */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 5, width: '100%', paddingTop: 5, borderTop: '1px dashed rgba(255,255,255,0.08)', flexWrap: 'wrap' }}>
            <span style={{ fontSize: 10, color: '#38bdf8', fontWeight: 800 }}>📋 Phạm vi xuất JSON:</span>
            <button
              onClick={() => {
                setJsonExportScope('single_angle');
                setPromptFormatTab('json');
              }}
              style={{
                padding: '4px 10px',
                fontSize: 10.5,
                fontWeight: 800,
                borderRadius: 6,
                border: promptFormatTab === 'json' && jsonExportScope === 'single_angle' ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                background: promptFormatTab === 'json' && jsonExportScope === 'single_angle' ? '#0284c7' : 'rgba(255,255,255,0.06)',
                color: promptFormatTab === 'json' && jsonExportScope === 'single_angle' ? '#fff' : '#94a3b8',
                cursor: 'pointer',
                boxShadow: promptFormatTab === 'json' && jsonExportScope === 'single_angle' ? '0 2px 8px rgba(2, 132, 199, 0.4)' : 'none',
              }}
              title="Chỉ xuất 1 góc đơn đang chọn"
            >
              🔍 1 Góc Đơn Đang Chọn
            </button>
            <button
              onClick={() => {
                setJsonExportScope('component_all_angles');
                setPromptFormatTab('json');
              }}
              style={{
                padding: '4px 10px',
                fontSize: 10.5,
                fontWeight: 800,
                borderRadius: 6,
                border: promptFormatTab === 'json' && jsonExportScope === 'component_all_angles' ? '1.5px solid #34d399' : '1px solid rgba(255,255,255,0.08)',
                background: promptFormatTab === 'json' && jsonExportScope === 'component_all_angles' ? '#059669' : 'rgba(255,255,255,0.06)',
                color: promptFormatTab === 'json' && jsonExportScope === 'component_all_angles' ? '#fff' : '#94a3b8',
                cursor: 'pointer',
                boxShadow: promptFormatTab === 'json' && jsonExportScope === 'component_all_angles' ? '0 2px 8px rgba(5, 150, 105, 0.4)' : 'none',
              }}
              title="Xuất tất cả góc quay của đúng linh kiện đang chọn"
            >
              🎯 Bộ Phận Đang Chọn (Đủ Các Góc)
            </button>
            <button
              onClick={() => {
                setJsonExportScope('group_all_parts');
                setPromptFormatTab('json');
              }}
              style={{
                padding: '4px 10px',
                fontSize: 10.5,
                fontWeight: 800,
                borderRadius: 6,
                border: promptFormatTab === 'json' && jsonExportScope === 'group_all_parts' ? '1.5px solid #c084fc' : '1px solid rgba(255,255,255,0.08)',
                background: promptFormatTab === 'json' && jsonExportScope === 'group_all_parts' ? '#7c3aed' : 'rgba(255,255,255,0.06)',
                color: promptFormatTab === 'json' && jsonExportScope === 'group_all_parts' ? '#fff' : '#94a3b8',
                cursor: 'pointer',
                boxShadow: promptFormatTab === 'json' && jsonExportScope === 'group_all_parts' ? '0 2px 8px rgba(124, 58, 237, 0.4)' : 'none',
              }}
              title="Xuất trọn bộ tất cả linh kiện trong nhóm hiện tại"
            >
              📦 Toàn Bộ Nhóm Linh Kiện
            </button>
          </div>
        </div>
      )}

      {/* 3. Main Dynamic Copy Button */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 10 }}>
        <button
          onClick={() => handleCopy(activePromptText, 'main_copy')}
          style={{
            flex: 1,
            height: 42,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 8,
            fontSize: 12,
            fontWeight: 800,
            borderRadius: 8,
            background: getCopyButtonBg(),
            color: '#fff',
            border: 'none',
            cursor: 'pointer',
            boxShadow:
              promptFormatTab === 'json' && workflowTab === 'step2_decomposed_parts' && jsonExportScope === 'group_all_parts'
                ? '0 3px 12px rgba(124, 58, 237, 0.4)'
                : '0 3px 12px rgba(2, 132, 199, 0.4)',
            transition: 'all 0.2s',
          }}
        >
          {copiedPrompt === 'main_copy' ? <Check size={16} /> : <Copy size={16} />}
          {getCopyButtonLabel()}
        </button>
      </div>

      {/* 4. Prompt / JSON Content Viewer */}
      <div
        style={{
          flex: 1,
          background: '#040711',
          borderRadius: 8,
          border: '1px solid rgba(56, 189, 248, 0.25)',
          padding: 14,
          overflowY: 'auto',
        }}
      >
        <pre
          style={{
            fontSize: 11.5,
            color: '#e0f2fe',
            lineHeight: 1.65,
            margin: 0,
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-word',
            fontFamily: 'Consolas, Monaco, "Courier New", monospace',
          }}
        >
          {activePromptText}
        </pre>

        {workflowTab === 'step3_actions' && cameraGuide && (
          <div style={{ marginTop: 14, padding: 10, background: 'rgba(168, 85, 247, 0.15)', borderRadius: 6, border: '1px solid rgba(168, 85, 247, 0.3)', fontSize: 11, color: '#e9d5ff' }}>
            🎥 <b>Hướng dẫn đạo diễn chuyển cảnh:</b> {cameraGuide}
          </div>
        )}
      </div>
    </div>
  );
};
