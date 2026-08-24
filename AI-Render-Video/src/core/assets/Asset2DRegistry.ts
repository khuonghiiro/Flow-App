// ─── Flow-App 2D Asset Registry (Modular Architecture) ──────────────────
// Refactored in accordance with flowmy-standards (max 200-600 lines per file).

// 1. Standard Crop & Part Hierarchy Presets
export {
  STANDARD_CROP_PRESETS,
  PART_HIERARCHY_CONFIG,
} from './presets/StandardCropPresets';

// 2. Demo Character & Parallax Map Presets
export {
  generateDemoPartSvg,
  EMPTY_CHARACTER_ASSEMBLY,
  DEFAULT_SAMPLE_CHARACTERS_2D,
  DEFAULT_SAMPLE_MAPS_2D,
} from './presets/DemoCharacterPresets';

// 3. Prompt Label Helpers & Types
export {
  type AIPromptResult,
  getSheetTypeLabel,
  getStyleLabel,
  getGenderLabel,
  getEyeShapeLabels,
  getEyeColorLabels,
  getNoseLabels,
  getMouthLabels,
  getCostumeLabels,
  getPropLabels,
  getHairLengthLabels,
  getHairColorLabels,
  getHairTextureLabels,
  getHairAccessoryLabels,
  getBodyProportionLabels,
} from './prompt_builders/PromptLabelHelpers';

// 4. Filename Auto-Detection Parser
export {
  type ParsedPartFilenameInfo,
  parsePartFilename,
} from './prompt_builders/PartFilenameParser';

// 5. Specialized Prompt Builders
export { buildStep1MasterPrompt } from './prompt_builders/Step1MasterPromptBuilder';
export {
  type Asset2DComponentDef,
  getComponentDef,
  buildStep2DecomposedPrompt,
} from './prompt_builders/Step2DecomposedPromptBuilder';
export { buildGridSheetsPrompt } from './prompt_builders/GridSheetsPromptBuilder';

// 6. Main Orchestrator Function
export { buildAIPromptForPart } from './prompt_builders/AIPromptBuilder';
