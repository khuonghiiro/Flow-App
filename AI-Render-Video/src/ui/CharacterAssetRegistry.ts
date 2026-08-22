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
  presetData?: any;
}

export interface CharacterCategory {
  id: string;
  label: string;
  icon: string;       // Emoji icon for the vertical tab
  items: CharacterPartItem[];
}

// Default empty categories fallback
export const CHARACTER_CATEGORIES: CharacterCategory[] = [];

/**
 * Fetch and dynamically build character categories from live manifest & structure JSON
 */
export async function fetchLiveCharacterCategories(): Promise<CharacterCategory[]> {
  try {
    let manifest: any = {};
    try {
      const res = await fetch(`/assets/asset_manifest.json?t=${Date.now()}`);
      if (res.ok) manifest = await res.json();
    } catch {}

    const chars = manifest.characters || {};
    let structure = manifest.structure?.character_structure;
    if (!structure) {
      try {
        const sRes = await fetch(`/assets/asset_structure.json?t=${Date.now()}`);
        if (sRes.ok) {
          const sJson = await sRes.json();
          structure = sJson.character_structure;
        }
      } catch {}
    }

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

    // Build categories directly from asset_structure.json
    if (structure && Array.isArray(structure.categories)) {
      const definedCategories: CharacterCategory[] = structure.categories.map((catConfig: any) => {
        // Collect items matching this category ID or folder aliases
        const aliases = [catConfig.id, catConfig.folder, ...(catConfig.folder_aliases || [])];
        let matchedItems: any[] = [];
        for (const alias of aliases) {
          if (chars[alias] && Array.isArray(chars[alias])) {
            matchedItems = chars[alias];
            break;
          }
        }

        const parsed = parseCharList(matchedItems, catConfig.default_gender || 'unisex');
        let finalItems = parsed;

        if (catConfig.id === '_lap_rap' || catConfig.id === 'nhan_vat_lap_rap') {
          const customAssembled: CharacterPartItem[] = [];
          try {
            const raw = localStorage.getItem('custom_character_presets');
            if (raw) {
              const list = JSON.parse(raw);
              if (Array.isArray(list)) {
                list.forEach((p: any, idx: number) => {
                  const preview = p.preview || (p.profile?.preview_image) || (p.costume ? (p.costume.endsWith('.glb') ? p.costume.replace('.glb', '.png') : p.costume) : (p.body?.endsWith('.glb') ? p.body.replace('.glb', '.png') : ''));
                  const bodyPath = p.body || p.base_body || p.than_co_ban || '';
                  customAssembled.push({
                    id: p.id || `custom_preset_${idx}`,
                    name: p.name || `Nhân Vật ${idx + 1}`,
                    path: bodyPath,
                    preview: preview && !preview.startsWith('/') && !preview.startsWith('data:') ? `/${preview}` : preview,
                    gender: p.gender || 'unisex',
                    format: 'JSON',
                    sizeMB: '0.01',
                    description: `Nhân vật đã phối đồ`,
                    presetData: p,
                  });
                });
              }
            }
          } catch {}
          finalItems = [...customAssembled, ...parsed];
        }

        return {
          id: catConfig.id,
          label: catConfig.label,
          icon: catConfig.icon || '📦',
          items: finalItems,
        };
      });

      // Auto-discover extra folders in chars that are not in asset_structure.json
      const handledKeys = new Set(structure.categories.flatMap((c: any) => [c.id, c.folder, ...(c.folder_aliases || [])]));
      for (const [folderKey, folderItems] of Object.entries(chars)) {
        if (!handledKeys.has(folderKey) && Array.isArray(folderItems) && folderItems.length > 0) {
          definedCategories.push({
            id: folderKey,
            label: formatDisplayName(folderKey),
            icon: '📦',
            items: parseCharList(folderItems, 'unisex'),
          });
        }
      }

      return definedCategories;
    }

    return [];
  } catch (err) {
    console.warn('Using default character category fallback:', err);
    return [];
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

import type { CharacterAssembly, FaceSliderConfig } from '../types/scene';
export type { FaceSliderConfig };

export const DEFAULT_FACE_SLIDERS: FaceSliderConfig = {
  baseFaceOpacity: 0.0,
  eyebrowOpacity: 1.0,
  pupilOpacity: 1.0,
  noseOpacity: 0.0,
  mouthOpacity: 0.0,
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
  version: string;
  name: string;
  age?: number;
  gender: 'male' | 'female' | 'unisex';
  height_cm: number;
  education_level?: string;
  occupation?: string;
  faction?: string;
  personality?: string;
  biography?: string;
  voice_style?: string;
  power_level?: number;
  element?: string;
  skills: CharacterSkillItem[];
  custom_attributes: Record<string, any>;
  base_body?: string;
  costume?: string;
  face?: string;
  hairstyle?: string;
  assembly: CharacterAssembly;
  face_sliders: FaceSliderConfig;
  preview_image?: string;
  ai_description: string;
  created_at: string;
  [key: string]: any;
}

export function buildCharacterProfile(
  name: string,
  assembly: CharacterAssembly,
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
    [key: string]: any;
  }
): CharacterProfileJSON {
  const baseBody = (typeof assembly?.than_co_ban === 'string' ? assembly.than_co_ban : typeof assembly?.base_body === 'string' ? assembly.base_body : '') || '';
  const costume = (typeof assembly?.trang_phuc === 'string' ? assembly.trang_phuc : typeof assembly?.costume === 'string' ? assembly.costume : '') || '';
  const face = (typeof assembly?.khuon_mat === 'string' ? assembly.khuon_mat : typeof assembly?.face === 'string' ? assembly.face : '') || '';
  const hairstyle = (typeof assembly?.kieu_toc === 'string' ? assembly.kieu_toc : typeof assembly?.hairstyle === 'string' ? assembly.hairstyle : '') || '';
  const sliders = assembly?.sliders || DEFAULT_FACE_SLIDERS;
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
    assembly: { ...assembly },
    face_sliders: { ...sliders },
    ai_description: aiDescription || extraProfile?.biography || '',
    created_at: new Date().toISOString(),
    ...assembly,
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
