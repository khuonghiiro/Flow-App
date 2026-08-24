import { AIPartPromptConfig } from '../../../types/scene2d';
import { AIPromptResult } from './PromptLabelHelpers';
import { buildStep1MasterPrompt } from './Step1MasterPromptBuilder';
import { buildStep2DecomposedPrompt } from './Step2DecomposedPromptBuilder';
import { buildGridSheetsPrompt } from './GridSheetsPromptBuilder';

/**
 * Main dispatcher function for building 2D AI prompts across Workflow Steps and Sheet Types
 */
export const buildAIPromptForPart = (config: AIPartPromptConfig): AIPromptResult => {
  const sheet = config.sheet_type || 'hair_multi_angle_grid';

  // Step 1: Master Character Turnaround Sheet
  if (config.workflow_step === 'step1_master_character') {
    return buildStep1MasterPrompt(config);
  }

  // Step 2: Decomposed Isolated Parts (1:1, 1x4 Turnaround, 2x3 Multi-Angle Sheet)
  if (
    sheet === 'single_isolated_1x1' ||
    sheet === 'single_part' ||
    sheet === 'seamless_turnaround_1x4' ||
    sheet === 'cinematic_single_part_2x3' ||
    sheet === 'cinematic_single_part_2x2' ||
    sheet === 'modular_bangs_3x1' ||
    sheet === 'modular_bangs_2x2' ||
    sheet === 'modular_backhair_3x1' ||
    sheet === 'modular_backhair_2x2' ||
    sheet === 'modular_torso_armor_3x1' ||
    sheet === 'modular_weapon_2x2'
  ) {
    return buildStep2DecomposedPrompt(config);
  }

  // Step 3 / Legacy Multi-Cell Grid Sheets
  return buildGridSheetsPrompt(config, sheet);
};
