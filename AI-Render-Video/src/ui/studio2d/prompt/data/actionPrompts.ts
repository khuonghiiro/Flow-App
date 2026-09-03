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
import { HAND_PROMPTS } from './actions/handPrompts';
import { LEG_PROMPTS } from './actions/legPrompts';
import { COMBINED_ACTION_PROMPTS } from './actions/combinedActionPrompts';

/**
 * Danh sách toàn bộ Prompt Động Tác (Step 3 Actions):
 * - 5 góc: Run, Sit, Lie, Jump, Idle, Attack, Head, Fall, Hit, Defend
 * - Nhóm động tác chi tiết: Tay (Vỗ tay, sau lưng, vuốt cằm...), Chân (Đá cao, xoay, quỳ...), Kết hợp (Vái chào, cuốc đất, chẻ củi, đả tọa...)
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
  ...HAND_PROMPTS,
  ...LEG_PROMPTS,
  ...COMBINED_ACTION_PROMPTS,
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
  HAND_PROMPTS,
  LEG_PROMPTS,
  COMBINED_ACTION_PROMPTS,
};
