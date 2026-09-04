export type PromptStepCategory =
  | 'step1_character'
  | 'step2_walk'
  | 'step3_actions'
  | 'step4_face'
  | 'step5_weapons'
  | 'step4_weapons';

/** Loại pipeline sinh ảnh/video của AI */
export type GenerationMode = 'text_to_image' | 'image_to_image' | 'image_to_video' | 'text_to_video';

/** Tỷ lệ khung hình */
export type AspectRatio = '1:1' | '3:4' | '4:3' | '9:16' | '16:9';

export interface PromptItem {
  id: string;
  title: string;
  subtitle: string;
  stepCategory: PromptStepCategory;
  stepLabel: string;
  icon: string;
  promptType: 'image' | 'video' | 'attachment';
  /** Pipeline sinh: text→image, image→image, image→video, text→video */
  generationMode: GenerationMode;
  /** Tỷ lệ khung hình mặc định */
  aspectRatio: AspectRatio;
  /** ID của prompt ảnh tham chiếu góc cơ thể (vd: 'angle0', 'character_base') */
  refAngleImageId?: string;
  /** Nhãn hiển thị ảnh tham chiếu (vd: '0° Chính Diện') */
  refAngleLabel?: string;
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
