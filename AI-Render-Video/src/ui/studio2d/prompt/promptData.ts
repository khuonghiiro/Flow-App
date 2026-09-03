import { PromptItem, PromptCustomizerValues } from './types';
import { CHARACTER_PROMPTS } from './data/characterPrompts';
import { WALK_PROMPTS } from './data/walkPrompts';
import { ACTION_PROMPTS } from './data/actionPrompts';
import { FACE_PROMPTS } from './data/facePrompts';
import { WEAPON_PROMPTS } from './data/weaponPrompts';

export const DEFAULT_CUSTOMIZER_VALUES: PromptCustomizerValues = {
  characterName: 'Lâm Tiêu (Lin Xiao)',
  style: '2D Xianxia/Fantasy anime chibi style, bold clean linework, flat cel-shaded coloring',
  gender: 'female',
  age: 'young adult (18-20)',
  hairStyleColor: 'Long silky platinum white hair with twin front braids, glowing jade hairpin',
  outfitDescription: 'Xianxia flowing silk daoist robes with wide sleeves, floating ribbons',
  primaryColor: 'Pure White & Soft Cyan Jade (#00E5FF)',
  accentColor: 'Soft Platinum & Lilac Purple',
  skinTone: 'Fair porcelain skin tone',
  weaponType: 'Enchanted Flying Sword (Lam Ngoc Kiem)',
  spellElement: 'Ice & Cyan Frost Aura',
  chromaBgHex: '#00FF00',
};

export function formatPromptWithCustomizer(
  rawPrompt: string,
  customizerValues: PromptCustomizerValues,
  itemId?: string
): string {
  let text = rawPrompt;
  if (!customizerValues) return text;
  text = text.replace(/#00FF00/g, customizerValues.chromaBgHex || '#00FF00');
  if (itemId === 'character_base' || itemId?.startsWith('angle') || itemId?.startsWith('character_angle')) {
    text = text
      .replace(/\[SPECIFY: young teen \/ young adult \/ mature adult\]/g, customizerValues.age || 'young adult')
      .replace(/\[SPECIFY: male \/ female \/ non-binary\]/g, customizerValues.gender || 'female')
      .replace(/\[SPECIFY: long\/short, wavy\/straight, specific style name[^\n\]]*\]/g, customizerValues.hairStyleColor || 'long silky hair')
      .replace(/\[SPECIFY: robes \/ dress \/ armor \/ casual \/ hybrid\]/g, customizerValues.outfitDescription || 'silk robes')
      .replace(/\[SPECIFY: 2-3 main colors\]/g, customizerValues.primaryColor || 'white and cyan jade')
      .replace(/\[SPECIFY: 1-2 accent colors\]/g, customizerValues.accentColor || 'lilac purple')
      .replace(/\[SPECIFY: fair porcelain \/ olive \/ tan \/ dark \/ supernatural tone\]/g, customizerValues.skinTone || 'fair porcelain');
  }
  return text;
}

export const PROMPT_ITEMS: PromptItem[] = [
  ...CHARACTER_PROMPTS,
  ...WALK_PROMPTS,
  ...ACTION_PROMPTS,
  ...FACE_PROMPTS,
  ...WEAPON_PROMPTS,
];

export interface SiblingAngle {
  deg: string;
  label: string;
  id: string;
  isCurrent: boolean;
}

const ANGLE_DEG_LABELS: Record<string, string> = {
  '0': '0° Chính Diện',
  '45': '45° Nghiêng Trái',
  '90': '90° Nhìn Ngang',
  '135': '135° Lưng Phải',
  '180': '180° Sau Lưng',
};

const ALIAS_FAMILY_MAP: Record<string, string> = {
  hand_clap: 'clap_angle0',
  hand_behind_back: 'back_angle0',
  hand_stroke_chin: 'chin_angle0',
  hand_clench_fist: 'fist_angle0',
  hand_open_palm: 'palm_angle0',
  hand_wave: 'wave_angle0',
  leg_high_kick: 'kick_angle0',
  leg_roundhouse: 'roundhouse_angle0',
  leg_kneel_one: 'kneel_angle0',
  leg_kneel_seiza: 'seiza_angle0',
  leg_stomp: 'stomp_angle0',
  action_bow_salute: 'bow_angle0',
  action_hoe_soil: 'hoe_angle0',
  action_chop_wood: 'chop_angle0',
  action_meditate_channel: 'meditate_angle0',
  face_funny: 'face_0_funny',
  face_sinister: 'face_0_sinister',
  face_mysterious: 'face_0_mysterious',
  face_disdain: 'face_0_disdain',
  face_dizzy: 'face_0_dizzy',
};

export function getSiblingAnglePrompts(currentId: string): SiblingAngle[] {
  const resolvedId = ALIAS_FAMILY_MAP[currentId] || currentId;
  const promptMap = new Map(PROMPT_ITEMS.map((p) => [p.id, p]));
  const results: SiblingAngle[] = [];

  // Case 1: action prompt with _angle{deg} (e.g. walk_angle0, clap_angle45, hoe_angle90)
  const actionMatch = resolvedId.match(/^(.*_angle)(\d+)$/);
  if (actionMatch) {
    const prefix = actionMatch[1];
    const currentDeg = actionMatch[2];
    const angles = ['0', '45', '90', '135', '180'];
    for (const deg of angles) {
      const targetId = `${prefix}${deg}`;
      if (promptMap.has(targetId)) {
        results.push({
          deg,
          label: ANGLE_DEG_LABELS[deg] || `${deg}°`,
          id: targetId,
          isCurrent: deg === currentDeg || targetId === currentId,
        });
      }
    }
    if (results.length > 1) return results;
  }

  // Case 2: face prompt with face_{deg}_{name} (e.g. face_0_sinister, face_45_funny)
  const faceMatch = resolvedId.match(/^face_(\d+)_(.+)$/);
  if (faceMatch) {
    const currentDeg = faceMatch[1];
    const exprName = faceMatch[2];
    const angles = ['0', '45', '90', '135'];
    for (const deg of angles) {
      const targetId = `face_${deg}_${exprName}`;
      if (promptMap.has(targetId)) {
        results.push({
          deg,
          label: ANGLE_DEG_LABELS[deg] || `${deg}°`,
          id: targetId,
          isCurrent: deg === currentDeg || targetId === currentId,
        });
      }
    }
    if (results.length > 1) return results;
  }

  // Case 3: character angles (angle0, angle45, angle90, angle135, angle180)
  const charMatch = resolvedId.match(/^angle(\d+)$/);
  if (charMatch) {
    const currentDeg = charMatch[1];
    const angles = ['0', '45', '90', '135', '180'];
    for (const deg of angles) {
      const targetId = `angle${deg}`;
      if (promptMap.has(targetId)) {
        results.push({
          deg,
          label: ANGLE_DEG_LABELS[deg] || `${deg}°`,
          id: targetId,
          isCurrent: deg === currentDeg || targetId === currentId,
        });
      }
    }
    if (results.length > 1) return results;
  }

  return results;
}
