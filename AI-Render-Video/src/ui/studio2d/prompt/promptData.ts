import { PromptItem, PromptCustomizerValues } from './types';
import { CHARACTER_PROMPTS } from './data/characterPrompts';
import { WALK_PROMPTS } from './data/walkPrompts';
import { ACTION_PROMPTS } from './data/actionPrompts';
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
  if (itemId === 'character_base') {
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
  ...WEAPON_PROMPTS,
];
