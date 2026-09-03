export type PromptStepCategory =
  | 'step1_character'
  | 'step2_walk'
  | 'step3_actions'
  | 'step4_face'
  | 'step5_weapons'
  | 'step4_weapons';

export interface PromptItem {
  id: string;
  title: string;
  subtitle: string;
  stepCategory: PromptStepCategory;
  stepLabel: string;
  icon: string;
  promptType: 'image' | 'video' | 'attachment';
  rawPrompt: string;
  negativePrompt: string;
  infoNote: string;
  videoGuide?: {
    duration: string;
    fps: string;
    camera: string;
    loopType: string;
    keyPoints: string[];
  };
  tags: string[];
}

export interface PromptCustomizerValues {
  characterName: string;
  style: string;
  gender: string;
  age: string;
  hairStyleColor: string;
  outfitDescription: string;
  primaryColor: string;
  accentColor: string;
  skinTone: string;
  weaponType: string;
  spellElement: string;
  chromaBgHex: string;
}
