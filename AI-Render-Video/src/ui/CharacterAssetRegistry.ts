/**
 * CharacterAssetRegistry.ts
 * 
 * Centralized registry of all character modular parts organized by category.
 * Dynamically builds categories from asset_structure.json / asset_manifest.json.
 * Supports Vietnamese taxonomy and gender filtering rules.
 */

export type AssetGender = 'male' | 'female' | 'unisex';

export interface CharacterPartItem {
  id: string;
  name: string;
  path: string;
  preview: string;
  gender: AssetGender;
  format?: string;
  sizeMB?: string;
  description?: string;
}

export interface CharacterCategory {
  id: string;
  label: string;
  icon: string;       // Emoji icon for the vertical tab
  items: CharacterPartItem[];
}

// ─── Default Fallback Items ──────────────────────────────
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

export const CHARACTER_CATEGORIES: CharacterCategory[] = [
  { id: 'than_co_ban', label: 'Thân Cơ Bản', icon: '🧍', items: BASE_BODIES },
  { id: 'trang_phuc',  label: 'Trang Phục',  icon: '👘', items: COSTUMES },
  { id: 'khuon_mat',   label: 'Khuôn Mặt',   icon: '🎭', items: FACES },
  { id: 'kieu_toc',    label: 'Kiểu Tóc',    icon: '💇', items: [] },
  { id: 'long_may',    label: 'Lông Mày',    icon: '🤨', items: [] },
  { id: 'mat',         label: 'Mắt',         icon: '👁️', items: [] },
  { id: 'mui',         label: 'Mũi',         icon: '👃', items: [] },
  { id: 'mieng',       label: 'Miệng',       icon: '👄', items: [] },
  { id: 'mu_non',      label: 'Mũ & Nón',    icon: '🎩', items: [] },
  { id: 'giay_dep',    label: 'Giày Dép',    icon: '👟', items: [] },
  { id: 'phu_kien',    label: 'Phụ Kiện',    icon: '💍', items: [] },
  { id: 'kieu_rau',    label: 'Râu',         icon: '🧔', items: [] },
];

/**
 * Fetch and dynamically build character categories from live manifest & structure JSON
 */
export async function fetchLiveCharacterCategories(): Promise<CharacterCategory[]> {
  try {
    const res = await fetch(`/assets/asset_manifest.json?t=${Date.now()}`);
    if (!res.ok) return CHARACTER_CATEGORIES;
    const manifest = await res.json();
    const chars = manifest.characters || {};
    const structure = manifest.structure?.character_structure;

    const parseCharList = (list: any[], fallbackGender: AssetGender = 'unisex'): CharacterPartItem[] => {
      if (!Array.isArray(list)) return [];
      return list.map((item: any) => {
        const pathLower = (item.relPath || '').toLowerCase();
        let gender: AssetGender = item.gender || fallbackGender;
        if (!item.gender) {
          if (pathLower.includes('/male/') || pathLower.includes('/man/') || pathLower.includes('/nam/')) gender = 'male';
          else if (pathLower.includes('/female/') || pathLower.includes('/woman/') || pathLower.includes('/nu/')) gender = 'female';
        }

        return {
          id: item.id || item.name,
          name: item.name || formatDisplayName(item.filename || item.relPath),
          path: item.relPath ? (item.relPath.startsWith('assets/') ? item.relPath : `assets/${item.relPath}`) : '',
          preview: item.previewUrl ? (item.previewUrl.startsWith('/') ? item.previewUrl : `/${item.previewUrl}`) : '',
          gender,
          format: item.format || 'GLB',
          sizeMB: item.sizeMB || '0.00',
        };
      });
    };

    // If structure categories are defined in JSON, build dynamically from them
    if (structure && Array.isArray(structure.categories)) {
      return structure.categories.map((catConfig: any) => {
        // Collect items matching this category ID or folder aliases
        const aliases = [catConfig.id, catConfig.folder, ...(catConfig.folder_aliases || [])];
        let matchedItems: any[] = [];
        for (const alias of aliases) {
          if (chars[alias] && Array.isArray(chars[alias])) {
            matchedItems = chars[alias];
            break;
          }
        }

        // Check fallback mappings
        if (matchedItems.length === 0) {
          if (catConfig.id === 'than_co_ban') matchedItems = chars.base_bodies || [];
          else if (catConfig.id === 'trang_phuc') matchedItems = chars.costumes || [];
          else if (catConfig.id === 'khuon_mat') matchedItems = chars.faces || [];
          else if (catConfig.id === 'kieu_toc') matchedItems = chars.hairstyles || [];
          else if (catConfig.id === 'kieu_rau') matchedItems = chars.beards || [];
          else if (catConfig.id === 'phu_kien') matchedItems = chars.accessories || [];
          else if (catConfig.id === 'mu_non') matchedItems = chars.hats || [];
          else if (catConfig.id === 'giay_dep') matchedItems = chars.shoes || [];
        }

        const parsed = parseCharList(matchedItems, catConfig.default_gender || 'unisex');

        // Include default presets if empty for key base categories
        let finalItems = parsed;
        if (finalItems.length === 0) {
          if (catConfig.id === 'than_co_ban') finalItems = BASE_BODIES;
          else if (catConfig.id === 'trang_phuc') finalItems = COSTUMES;
          else if (catConfig.id === 'khuon_mat') finalItems = FACES;
        }

        return {
          id: catConfig.id,
          label: catConfig.label,
          icon: catConfig.icon || '🧍',
          items: finalItems,
        };
      });
    }

    // Default mapping fallback
    return [
      { id: 'than_co_ban', label: 'Thân Cơ Bản', icon: '🧍', items: parseCharList(chars.base_bodies || chars.than_co_ban, 'male').concat(BASE_BODIES).filter((v, i, a) => a.findIndex(t => t.path === v.path) === i) },
      { id: 'trang_phuc',  label: 'Trang Phục',  icon: '👘', items: parseCharList(chars.costumes || chars.trang_phuc, 'male').concat(COSTUMES).filter((v, i, a) => a.findIndex(t => t.path === v.path) === i) },
      { id: 'khuon_mat',   label: 'Khuôn Mặt',   icon: '🎭', items: parseCharList(chars.faces || chars.khuon_mat, 'male').concat(FACES).filter((v, i, a) => a.findIndex(t => t.path === v.path) === i) },
      { id: 'kieu_toc',    label: 'Kiểu Tóc',    icon: '💇', items: parseCharList(chars.hairstyles || chars.kieu_toc, 'unisex') },
      { id: 'long_may',    label: 'Lông Mày',    icon: '🤨', items: parseCharList(chars.eyebrows || chars.long_may, 'unisex') },
      { id: 'mat',         label: 'Mắt',         icon: '👁️', items: parseCharList(chars.eyes || chars.mat, 'unisex') },
      { id: 'mui',         label: 'Mũi',         icon: '👃', items: parseCharList(chars.noses || chars.mui, 'unisex') },
      { id: 'mieng',       label: 'Miệng',       icon: '👄', items: parseCharList(chars.mouths || chars.mieng, 'unisex') },
      { id: 'mu_non',      label: 'Mũ & Nón',    icon: '🎩', items: parseCharList(chars.hats || chars.mu_non, 'unisex') },
      { id: 'giay_dep',    label: 'Giày Dép',    icon: '👟', items: parseCharList(chars.shoes || chars.giay_dep, 'unisex') },
      { id: 'phu_kien',    label: 'Phụ Kiện',    icon: '💍', items: parseCharList(chars.accessories || chars.phu_kien, 'unisex') },
      { id: 'kieu_rau',    label: 'Râu',         icon: '🧔', items: parseCharList(chars.beards || chars.kieu_rau, 'male') },
    ];
  } catch (err) {
    console.warn('Using default character category fallback:', err);
    return CHARACTER_CATEGORIES;
  }
}

function formatDisplayName(filename: string): string {
  if (!filename) return 'Tài Nguyên';
  return filename
    .replace(/\.[^/.]+$/, '')
    .replace(/_/g, ' ')
    .replace(/-/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

/**
 * Filter items by gender toggle.
 * When gender is 'male', includes 'male' and 'unisex'.
 * When gender is 'female', includes 'female' and 'unisex'.
 */
export function filterByGender(
  items: CharacterPartItem[],
  gender: 'male' | 'female'
): CharacterPartItem[] {
  return items.filter((i) => i.gender === gender || i.gender === 'unisex');
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

export interface CharacterSkillItem {
  id?: string;
  name: string;
  level?: number;
  type?: string;
  description?: string;
}

export interface CharacterProfileJSON {
  version: '2.0' | '1.0';
  name: string;
  age?: number;
  gender: 'male' | 'female' | 'unisex';
  height_cm?: number;
  education_level?: string;
  occupation?: string;
  faction?: string;
  personality?: string;
  biography?: string;
  voice_style?: string;
  power_level?: number;
  element?: string;
  skills?: CharacterSkillItem[];
  custom_attributes?: Record<string, any>;

  base_body: string;
  costume: string;
  face: string;
  hairstyle: string;
  face_sliders: FaceSliderConfig;
  ai_description: string;
  preview_image?: string;
  created_at: string;
}

export function buildCharacterProfile(
  name: string,
  baseBody: string,
  costume: string,
  face: string,
  hairstyle: string,
  sliders: FaceSliderConfig,
  aiDescription: string,
  extraProfile?: {
    age?: number;
    gender?: 'male' | 'female' | 'unisex';
    height_cm?: number;
    education_level?: string;
    occupation?: string;
    faction?: string;
    personality?: string;
    biography?: string;
    voice_style?: string;
    power_level?: number;
    element?: string;
    skills?: CharacterSkillItem[];
    custom_attributes?: Record<string, any>;
  }
): CharacterProfileJSON {
  const gender = extraProfile?.gender || (baseBody.includes('manekina') || baseBody.includes('/nu/') ? 'female' : 'male');
  return {
    version: '2.0',
    name,
    age: extraProfile?.age,
    gender,
    height_cm: extraProfile?.height_cm || (gender === 'male' ? 178 : 165),
    education_level: extraProfile?.education_level,
    occupation: extraProfile?.occupation,
    faction: extraProfile?.faction,
    personality: extraProfile?.personality,
    biography: extraProfile?.biography,
    voice_style: extraProfile?.voice_style,
    power_level: extraProfile?.power_level,
    element: extraProfile?.element,
    skills: extraProfile?.skills || [],
    custom_attributes: extraProfile?.custom_attributes || {},
    base_body: baseBody,
    costume,
    face,
    hairstyle,
    face_sliders: { ...sliders },
    ai_description: aiDescription || extraProfile?.biography || '',
    created_at: new Date().toISOString(),
  };
}

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
