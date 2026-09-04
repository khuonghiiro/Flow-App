import React, { useState } from 'react';
import { PROMPT_ITEMS, DEFAULT_CUSTOMIZER_VALUES, formatPromptWithCustomizer } from './prompt/promptData';
import { PromptCustomizerValues } from './prompt/types';
import { PromptHeader } from './prompt/PromptHeader';
import { PromptSidebar } from './prompt/PromptSidebar';
import { PromptViewer } from './prompt/PromptViewer';
import { PromptCustomizerBar } from './prompt/PromptCustomizerBar';
import { SkillTreeCanvas } from './prompt/tree/SkillTreeCanvas';

import { ChevronRight, ChevronLeft } from 'lucide-react';

const PROMPT_ALIAS_MAP: Record<string, string> = {
  character_angle0: 'angle0',
  character_angle45: 'angle45',
  character_angle90: 'angle90',
  character_angle135: 'angle135',
  character_angle180: 'angle180',
};

export const AIPromptGenerator2D: React.FC = () => {
  const [selectedId, setSelectedId] = useState<string>('character_base');
  const [showCustomizer, setShowCustomizer] = useState<boolean>(false);
  const [customizerValues, setCustomizerValues] = useState<PromptCustomizerValues>(DEFAULT_CUSTOMIZER_VALUES);
  const [copiedQuick, setCopiedQuick] = useState<boolean>(false);
  const [viewMode, setViewMode] = useState<'skill_tree' | 'classic_list'>('skill_tree');
  const [isDetailsCollapsed, setIsDetailsCollapsed] = useState<boolean>(false);


  const resolvedId = PROMPT_ALIAS_MAP[selectedId] || selectedId;
  const currentItem = PROMPT_ITEMS.find((it) => it.id === resolvedId) || PROMPT_ITEMS[0];

  const handleCopyActivePrompt = () => {
    const text = formatPromptWithCustomizer(currentItem.rawPrompt, customizerValues, currentItem.id);
    navigator.clipboard.writeText(text);
    setCopiedQuick(true);
    setTimeout(() => setCopiedQuick(false), 2000);
  };

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        gap: 10,
        padding: '8px 12px',
        background: '#040711',
        overflow: 'hidden',
        color: '#f8fafc',
      }}
    >
      {/* ─── Top Header Banner with View Mode Switcher ─── */}
      <PromptHeader
        activeTitle={currentItem.title}
        totalCount={PROMPT_ITEMS.length}
        showCustomizer={showCustomizer}
        onToggleCustomizer={() => setShowCustomizer((prev) => !prev)}
        onCopyActive={handleCopyActivePrompt}
        copied={copiedQuick}
        viewMode={viewMode}
        onToggleViewMode={() =>
          setViewMode((prev) => (prev === 'skill_tree' ? 'classic_list' : 'skill_tree'))
        }

      />

      {/* ─── Expandable Parameter Customizer ─── */}
      {showCustomizer && (
        <PromptCustomizerBar
          values={customizerValues}
          onChange={setCustomizerValues}
          onReset={() => setCustomizerValues(DEFAULT_CUSTOMIZER_VALUES)}
        />
      )}

      {/* ─── Main Content Area: Skill Tree vs Classic List ─── */}
      {viewMode === 'skill_tree' ? (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: isDetailsCollapsed ? '1fr' : '1fr minmax(380px, 440px)',
            gap: 12,
            flex: 1,
            minHeight: 0,
            overflow: 'hidden',
            position: 'relative',
          }}
        >
          {/* Skill Tree Canvas Map */}
          <div style={{ position: 'relative', height: '100%', minWidth: 0, overflow: 'hidden' }}>
            <SkillTreeCanvas
              selectedPromptId={selectedId}
              onSelectPrompt={(id) => {
                setSelectedId(id);
                if (isDetailsCollapsed) setIsDetailsCollapsed(false);
              }}
            />

            {/* Toggle Button to Expand / Collapse Side Details */}
            <button
              onClick={() => setIsDetailsCollapsed((prev) => !prev)}
              title={isDetailsCollapsed ? 'Hiện khung chi tiết Prompt' : 'Thu gọn xem toàn màn hình Cây Kỹ Năng'}
              style={{
                position: 'absolute',
                top: 14,
                right: 14,
                zIndex: 20,
                padding: '6px 10px',
                borderRadius: 6,
                background: 'rgba(15, 23, 42, 0.9)',
                border: '1px solid rgba(255, 255, 255, 0.15)',
                color: '#e2e8f0',
                fontSize: 11,
                fontWeight: 700,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                backdropFilter: 'blur(8px)',
                boxShadow: '0 4px 14px rgba(0,0,0,0.4)',
              }}
            >
              {isDetailsCollapsed ? (
                <>
                  <ChevronLeft size={14} /> Mở Bảng Prompt
                </>
              ) : (
                <>
                  <ChevronRight size={14} /> Thu Gọn
                </>
              )}
            </button>
          </div>

          {/* Right Content: Active Prompt Details & Copying */}
          {!isDetailsCollapsed && (
            <div style={{ height: '100%', minWidth: 0, overflowY: 'auto' }}>
              <PromptViewer
                item={currentItem}
                customizerValues={customizerValues}
                onSelectPrompt={setSelectedId}
              />
            </div>
          )}
        </div>
      ) : (
        /* Classic Two-Column List Mode */
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'minmax(300px, 360px) 1fr',
            gap: 12,
            flex: 1,
            minHeight: 0,
            overflow: 'hidden',
          }}
        >
          {/* Left Sidebar: Categories & Prompts Selection */}
          <PromptSidebar
            items={PROMPT_ITEMS}
            selectedId={selectedId}
            onSelect={setSelectedId}
          />

          {/* Right Content: Active Prompt Details & Copying */}
          <PromptViewer
            item={currentItem}
            customizerValues={customizerValues}
            onSelectPrompt={setSelectedId}
          />
        </div>
      )}

    </div>
  );
};
