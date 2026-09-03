import { PromptItem } from '../types';
import { RUN_PROMPTS } from './actions/runPrompts';
import { SIT_PROMPTS } from './actions/sitPrompts';
import { LIE_PROMPTS } from './actions/liePrompts';
import { JUMP_PROMPTS } from './actions/jumpPrompts';
import { IDLE_PROMPTS } from './actions/idlePrompts';
import { ATTACK_PROMPTS } from './actions/attackPrompts';
import { HEAD_PROMPTS } from './actions/headPrompts';
import { FALL_PROMPTS } from './actions/fallPrompts';
import { HIT_REACTION_PROMPTS } from './actions/hitReactionPrompts';
import { DEFEND_PROMPTS } from './actions/defendPrompts';

/**
 * Danh sách toàn bộ Prompt Động Tác (Step 3 Actions) theo chuẩn 5 góc:
 * 0° (Chính diện), 45° (Xoay trái), 90° (Nhìn ngang), 135° (Lưng lệch phải), 180° (Sau lưng).
 */
export const ACTION_PROMPTS: PromptItem[] = [
  ...RUN_PROMPTS,
  ...SIT_PROMPTS,
  ...LIE_PROMPTS,
  ...JUMP_PROMPTS,
  ...IDLE_PROMPTS,
  ...ATTACK_PROMPTS,
  ...HEAD_PROMPTS,
  ...FALL_PROMPTS,
  ...HIT_REACTION_PROMPTS,
  ...DEFEND_PROMPTS,
];

export {
  RUN_PROMPTS,
  SIT_PROMPTS,
  LIE_PROMPTS,
  JUMP_PROMPTS,
  IDLE_PROMPTS,
  ATTACK_PROMPTS,
  HEAD_PROMPTS,
  FALL_PROMPTS,
  HIT_REACTION_PROMPTS,
  DEFEND_PROMPTS,
};
