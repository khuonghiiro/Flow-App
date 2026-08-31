import React, { useState } from 'react';
import { PROMPT_ITEMS, DEFAULT_CUSTOMIZER_VALUES, formatPromptWithCustomizer } from './prompt/promptData';
import { PromptCustomizerValues } from './prompt/types';
import { PromptHeader } from './prompt/PromptHeader';
import { PromptSidebar } from './prompt/PromptSidebar';
import { PromptViewer } from './prompt/PromptViewer';
import { PromptCustomizerBar } from './prompt/PromptCustomizerBar';

export const AIPromptGenerator2D: React.FC = () => {
  const [selectedId, setSelectedId] = useState<string>('character_base');
  const [showCustomizer, setShowCustomizer] = useState<boolean>(false);
  const [customizerValues, setCustomizerValues] = useState<PromptCustomizerValues>(DEFAULT_CUSTOMIZER_VALUES);
  const [copiedQuick, setCopiedQuick] = useState<boolean>(false);

  const currentItem = PROMPT_ITEMS.find((it) => it.id === selectedId) || PROMPT_ITEMS[0];

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
      {/* ─── Top Header Banner ─── */}
      <PromptHeader
        activeTitle={currentItem.title}
        totalCount={PROMPT_ITEMS.length}
        showCustomizer={showCustomizer}
        onToggleCustomizer={() => setShowCustomizer((prev) => !prev)}
        onCopyActive={handleCopyActivePrompt}
        copied={copiedQuick}
      />

      {/* ─── Expandable Parameter Customizer ─── */}
      {showCustomizer && (
        <PromptCustomizerBar
          values={customizerValues}
          onChange={setCustomizerValues}
          onReset={() => setCustomizerValues(DEFAULT_CUSTOMIZER_VALUES)}
        />
      )}

      {/* ─── Two-Column Main Content ─── */}
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
        />
      </div>
    </div>
  );
};
