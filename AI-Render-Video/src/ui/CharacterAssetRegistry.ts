/**
 * CharacterAssetRegistry.ts
 * 
 * Centralized registry of all character modular parts organized by category.
 * Each category maps to a vertical tab in the Character Workbench UI.
 * Items include gender tags for filtering.
 */

export type AssetGender = 'male' | 'female' | 'unisex';

export interface CharacterPartItem {
  id: string;
  name: string;
  path: string;
  preview: string;
  gender: AssetGender;
  /** Optional description for AI context */
  description?: string;
}

export interface CharacterCategory {
  id: string;
  label: string;
  icon: string;       // Emoji icon for the vertical tab
  items: CharacterPartItem[];
}

// ─── Base Bodies ─────────────────────────────────────────
const BASE_BODIES: CharacterPartItem[] = [
  {
    id: 'body_male',
    name: 'Manekin Body (Nam)',
    path: 'assets/characters/base_bodies/man/body_base_-_manekin.glb',
    preview: '/assets/characters/base_bodies/man/body_base_-_manekin.png',
    gender: 'male',
  },
  {
    id: 'body_female',
    name: 'Manekina Body (Nữ)',
    path: 'assets/characters/base_bodies/male/body_base_-_manekina.glb',
    preview: '/assets/characters/base_bodies/male/body_base_-_manekina.png',
    gender: 'female',
  },
];

// ─── Costumes / Trang Phục ──────────────────────────────
const COSTUMES: CharacterPartItem[] = [
  {
    id: 'costume_amber_man',
    name: 'Amber Nectar (Hổ Phách Nam)',
    path: 'assets/characters/costumes/man/amber_nectar_-_manekin.glb',
    preview: '/assets/characters/costumes/man/amber_nectar_-_manekin.png',
    gender: 'male',
  },
  {
    id: 'costume_precision_female',
    name: 'Precision Strike (Tinh Nhuệ Nữ)',
    path: 'assets/characters/costumes/male/precision_strike_-_manekina.glb',
    preview: '/assets/characters/costumes/male/precision_strike_-_manekina.png',
    gender: 'female',
  },
  {
    id: 'costume_scary_cat_man',
    name: 'Scary Cat (Hắc Miêu Nam)',
    path: 'assets/characters/costumes/man/scary_cat_-_manekin.glb',
    preview: '/assets/characters/costumes/man/scary_cat_-_manekin.png',
    gender: 'male',
  },
  {
    id: 'costume_sleuth_man',
    name: 'Sleuth\'s Verdict (Thám Tử Nam)',
    path: 'assets/characters/costumes/man/sleuths_verdict_-_manekin.glb',
    preview: '/assets/characters/costumes/man/sleuths_verdict_-_manekin.png',
    gender: 'male',
  },
  {
    id: 'costume_precision_man',
    name: 'Precision Strike (Tinh Nhuệ Nam)',
    path: 'assets/characters/costumes/man/precision_strike_-_manekin.glb',
    preview: '/assets/characters/costumes/man/precision_strike_-_manekin.png',
    gender: 'male',
  },
  {
    id: 'costume_amber_female',
    name: 'Amber Nectar (Hổ Phách Nữ)',
    path: 'assets/characters/costumes/male/amber_nectar_-_manekina.glb',
    preview: '/assets/characters/costumes/male/amber_nectar_-_manekina.png',
    gender: 'female',
  },
  {
    id: 'costume_scary_cat_female',
    name: 'Scary Cat (Hắc Miêu Nữ)',
    path: 'assets/characters/costumes/male/scary_cat_-_manekina.glb',
    preview: '/assets/characters/costumes/male/scary_cat_-_manekina.png',
    gender: 'female',
  },
];

// ─── Faces / Khuôn Mặt ─────────────────────────────────
const FACES: CharacterPartItem[] = [
  {
    id: 'face_dawnbreaker_man',
    name: 'Dawnbreaker (Bình Minh Nam)',
    path: 'assets/characters/faces/man/dawnbreaker_-_manekin.glb',
    preview: '/assets/characters/faces/man/dawnbreaker_-_manekin.png',
    gender: 'male',
  },
  {
    id: 'face_starlight_female',
    name: 'Starlight Fragments (Tinh Tú Nữ)',
    path: 'assets/characters/faces/male/starlight_fragments_-_manekin.glb',
    preview: '/assets/characters/faces/male/starlight_fragments_-_manekin.png',
    gender: 'female',
  },
];

// ─── Hairstyles / Kiểu Tóc ─────────────────────────────
const HAIRSTYLES: CharacterPartItem[] = [];

// ─── Eyebrows / Lông Mày (placeholder — controlled via sliders) ──
const EYEBROWS: CharacterPartItem[] = [];

// ─── Eyes / Mắt (placeholder — controlled via sliders) ──
const EYES: CharacterPartItem[] = [];

// ─── Nose / Mũi (placeholder — controlled via sliders) ──
const NOSES: CharacterPartItem[] = [];

// ─── Mouth / Miệng (placeholder — controlled via sliders) ──
const MOUTHS: CharacterPartItem[] = [];

// ─── Hats / Mũ ──────────────────────────────────────────
const HATS: CharacterPartItem[] = [];

// ─── Shoes / Giày Dép ───────────────────────────────────
const SHOES: CharacterPartItem[] = [];

// ─── Accessories / Phụ Kiện ─────────────────────────────
const ACCESSORIES: CharacterPartItem[] = [];

// ─── Beards / Râu ───────────────────────────────────────
const BEARDS: CharacterPartItem[] = [];

/**
 * Full ordered list of all character part categories.
 * Each becomes a vertical tab in the Workbench UI.
 */
export const CHARACTER_CATEGORIES: CharacterCategory[] = [
  { id: 'body',        label: 'Thân',           icon: '🧍', items: BASE_BODIES },
  { id: 'face',        label: 'Khuôn Mặt',      icon: '🎭', items: FACES },
  { id: 'costume',     label: 'Trang Phục',      icon: '👘', items: COSTUMES },
  { id: 'hairstyle',   label: 'Kiểu Tóc',       icon: '💇', items: HAIRSTYLES },
  { id: 'eyebrow',     label: 'Lông Mày',       icon: '🤨', items: EYEBROWS },
  { id: 'eye',         label: 'Mắt',            icon: '👁️', items: EYES },
  { id: 'nose',        label: 'Mũi',            icon: '👃', items: NOSES },
  { id: 'mouth',       label: 'Miệng',          icon: '👄', items: MOUTHS },
  { id: 'hat',         label: 'Mũ & Nón',       icon: '🎩', items: HATS },
  { id: 'shoe',        label: 'Giày Dép',       icon: '👟', items: SHOES },
  { id: 'accessory',   label: 'Phụ Kiện',       icon: '💍', items: ACCESSORIES },
  { id: 'beard',       label: 'Râu',            icon: '🧔', items: BEARDS },
];

/**
 * Filter items by gender.
 * 'unisex' items appear for both male and female filters.
 */
export function filterByGender(
  items: CharacterPartItem[],
  gender: 'male' | 'female'
): CharacterPartItem[] {
  return items.filter((i) => i.gender === gender || i.gender === 'unisex');
}

/**
 * Get a category by its ID.
 */
export function getCategoryById(id: string): CharacterCategory | undefined {
  return CHARACTER_CATEGORIES.find((c) => c.id === id);
}

// ─── Character Profile JSON (for cross-scene persistence) ────────

export interface FaceSliderConfig {
  baseFaceOpacity: number;
  eyebrowOpacity: number;
  pupilOpacity: number;
  noseOpacity: number;
  mouthOpacity: number;
  skinSmoothness: number;
  costumeOpacity: number;
}

export const DEFAULT_FACE_SLIDERS: FaceSliderConfig = {
  baseFaceOpacity: 1.0,
  eyebrowOpacity: 1.0,
  pupilOpacity: 1.0,
  noseOpacity: 1.0,
  mouthOpacity: 1.0,
  skinSmoothness: 0.75,
  costumeOpacity: 1.0,
};

export interface CharacterProfileJSON {
  version: '1.0';
  name: string;
  base_body: string;
  costume: string;
  face: string;
  hairstyle: string;
  gender: 'male' | 'female';
  face_sliders: FaceSliderConfig;
  ai_description: string;
  created_at: string;
}

/**
 * Build a full character profile JSON from current workbench state.
 */
export function buildCharacterProfile(
  name: string,
  baseBody: string,
  costume: string,
  face: string,
  hairstyle: string,
  sliders: FaceSliderConfig,
  aiDescription: string
): CharacterProfileJSON {
  return {
    version: '1.0',
    name,
    base_body: baseBody,
    costume,
    face,
    hairstyle,
    gender: baseBody.includes('manekina') ? 'female' : 'male',
    face_sliders: { ...sliders },
    ai_description: aiDescription,
    created_at: new Date().toISOString(),
  };
}

/**
 * Download a character profile as a JSON file.
 */
export function downloadCharacterProfile(profile: CharacterProfileJSON): void {
  const safeName = profile.name
    .replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF]/g, '_')
    .substring(0, 40);
  const filename = `character_${safeName}_${Date.now()}.json`;
  const blob = new Blob([JSON.stringify(profile, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
