import React, { useState, useEffect } from 'react';
import {
  CharacterPromptGeneratorColumn,
} from './CharacterPromptGeneratorColumn';
import {
  AIPartDecomposerColumn,
} from './AIPartDecomposerColumn';
import {
  DecomposedPartsStagingColumn,
} from './DecomposedPartsStagingColumn';
import {
  CHARACTER_STYLE_PRESETS,
  PART_DECOMPOSITION_TEMPLATES,
  DecomposedPartItem,
  stitchDecomposedPartsToSpriteSheet,
} from '../../../core/utils/AntigravityDecomposerService';

interface AIAntigravityDecomposerPanelProps {
  onTransferToGridSlicer: (
    spriteSheetUrl: string,
    gridCols: number,
    gridRows: number,
    categoryId?: string
  ) => void;
}

export const AIAntigravityDecomposerPanel: React.FC<AIAntigravityDecomposerPanelProps> = ({
  onTransferToGridSlicer,
}) => {
  // 1. Column 1: Character Source State
  const [characterImageUrl, setCharacterImageUrl] = useState<string | null>(
    CHARACTER_STYLE_PRESETS[0].thumbnailUrl
  );
  const [isGeneratingCharacter, setIsGeneratingCharacter] = useState<boolean>(false);

  // 2. Column 2: Decomposition Configuration & AI Log State
  const [selectedTemplateIds, setSelectedTemplateIds] = useState<Set<string>>(
    new Set(['hair_front', 'head_base', 'eyes_pair', 'torso_body', 'arm_left', 'arm_right', 'legs_pair'])
  );
  const [customDecompositionPrompt, setCustomDecompositionPrompt] = useState<string>('');
  const [isDecomposing, setIsDecomposing] = useState<boolean>(false);
  const [agentLogs, setAgentLogs] = useState<string[]>([
    '🤖 Antigravity AI Agent v2.4 initialized and ready.',
  ]);

  // 3. Column 3: Decomposed Parts Staging State
  const [decomposedParts, setDecomposedParts] = useState<DecomposedPartItem[]>([]);
  const [spriteSheetPreviewUrl, setSpriteSheetPreviewUrl] = useState<string | null>(null);
  const [isStitching, setIsStitching] = useState<boolean>(false);

  // Toggle template selection in Column 2
  const handleToggleTemplate = (templateId: string) => {
    setSelectedTemplateIds((prev) => {
      const next = new Set(prev);
      if (next.has(templateId)) {
        next.delete(templateId);
      } else {
        next.add(templateId);
      }
      return next;
    });
  };

  const handleSelectAllTemplates = () => {
    setSelectedTemplateIds(new Set(PART_DECOMPOSITION_TEMPLATES.map((t) => t.id)));
  };

  const handleClearAllTemplates = () => {
    setSelectedTemplateIds(new Set());
  };

  // Handle Character Generation in Column 1
  const handleCharacterGenerated = (imageUrl: string, promptText: string) => {
    setCharacterImageUrl(imageUrl);
    setAgentLogs((prev) => [
      ...prev,
      `[Character Loaded] Image ready for component decomposition.`,
    ]);
  };

  // Handle Send to Column 2
  const handleSendToDecomposer = () => {
    setAgentLogs((prev) => [
      ...prev,
      `[Workflow] Character transferred to Column 2 for decomposition.`,
    ]);
  };

  // Handle Decomposition Execution in Column 2
  const handleExecuteDecomposition = () => {
    if (!characterImageUrl) return;

    setIsDecomposing(true);
    setAgentLogs((prev) => [
      ...prev,
      `[Decompose] Antigravity Agent starting segmentation pipeline...`,
      `[Analysis] Detecting character facial boundary, hair layers, and torso geometry...`,
    ]);

    setTimeout(() => {
      setAgentLogs((prev) => [
        ...prev,
        `[Tool Call] isolate_character_layers(sub_elements=[${Array.from(selectedTemplateIds).join(', ')}])`,
        `[Matting] Performing optical color unmixing and despill on green screen...`,
      ]);

      setTimeout(() => {
        // Generate simulated decomposed parts based on selected templates
        const selectedTemplates = PART_DECOMPOSITION_TEMPLATES.filter((t) =>
          selectedTemplateIds.has(t.id)
        );

        const newParts: DecomposedPartItem[] = selectedTemplates.map((template) => ({
          id: `part_${template.id}_${Date.now()}`,
          name: template.name,
          category: template.category,
          slotName: template.slotName,
          imageUrl: characterImageUrl,
          zIndex: template.zIndex,
          selected: true,
          notes: template.promptGuidance,
        }));

        setDecomposedParts(newParts);
        setIsDecomposing(false);
        setAgentLogs((prev) => [
          ...prev,
          `✓ Hoàn tất bóc tách ${newParts.length} linh kiện! Đã chuyển sang Cột 3.`,
        ]);
      }, 1200);
    }, 1000);
  };

  // Re-stitch Sprite Sheet preview whenever decomposedParts change
  useEffect(() => {
    const activeParts = decomposedParts.filter((p) => p.selected);
    if (activeParts.length === 0) {
      setSpriteSheetPreviewUrl(null);
      return;
    }

    setIsStitching(true);
    stitchDecomposedPartsToSpriteSheet(activeParts, 3)
      .then(({ spriteSheetDataUrl }) => {
        setSpriteSheetPreviewUrl(spriteSheetDataUrl);
      })
      .catch((err) => {
        console.error('Error stitching parts:', err);
      })
      .finally(() => {
        setIsStitching(false);
      });
  }, [decomposedParts]);

  // Handle Toggle Part Visibility
  const handleTogglePartVisibility = (partId: string) => {
    setDecomposedParts((prev) =>
      prev.map((part) =>
        part.id === partId ? { ...part, selected: !part.selected } : part
      )
    );
  };

  // Handle Delete Part
  const handleDeletePart = (partId: string) => {
    setDecomposedParts((prev) => prev.filter((part) => part.id !== partId));
  };

  // Handle Transfer to Grid Slicer & 3D Assembler
  const handleTransferToGridSlicer = async () => {
    const activeParts = decomposedParts.filter((p) => p.selected);
    if (activeParts.length === 0) return;

    try {
      const { spriteSheetDataUrl, cols, rows } =
        await stitchDecomposedPartsToSpriteSheet(activeParts, 3);
      onTransferToGridSlicer(spriteSheetDataUrl, cols, rows, 'chibi_3x3');
    } catch (err) {
      console.error('Failed to transfer to grid slicer:', err);
    }
  };

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        background: '#040711',
        overflow: 'hidden',
        padding: 8,
      }}
    >
      {/* 3-Column Grid Layout */}
      <div
        style={{
          flex: 1,
          display: 'grid',
          gridTemplateColumns: '1fr 1fr 1fr',
          gap: 10,
          minHeight: 0,
        }}
      >
        {/* Column 1: Character Prompt & Source Canvas */}
        <CharacterPromptGeneratorColumn
          characterImageUrl={characterImageUrl}
          onCharacterGenerated={handleCharacterGenerated}
          onSendToDecomposer={handleSendToDecomposer}
          isGenerating={isGeneratingCharacter}
          setIsGenerating={setIsGeneratingCharacter}
        />

        {/* Column 2: AI Part Decomposer & Layer Extraction */}
        <AIPartDecomposerColumn
          characterImageUrl={characterImageUrl}
          selectedTemplateIds={selectedTemplateIds}
          onToggleTemplate={handleToggleTemplate}
          onSelectAllTemplates={handleSelectAllTemplates}
          onClearAllTemplates={handleClearAllTemplates}
          customDecompositionPrompt={customDecompositionPrompt}
          setCustomDecompositionPrompt={setCustomDecompositionPrompt}
          isDecomposing={isDecomposing}
          onExecuteDecomposition={handleExecuteDecomposition}
          agentLogs={agentLogs}
        />

        {/* Column 3: Decomposed Parts Staging & 3D Bridge */}
        <DecomposedPartsStagingColumn
          decomposedParts={decomposedParts}
          onTogglePartVisibility={handleTogglePartVisibility}
          onDeletePart={handleDeletePart}
          onTransferToGridSlicer={handleTransferToGridSlicer}
          isStitching={isStitching}
          spriteSheetPreviewUrl={spriteSheetPreviewUrl}
        />
      </div>
    </div>
  );
};
