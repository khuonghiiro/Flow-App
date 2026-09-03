import { PromptItem } from '../../types';
import { FACE_0_PROMPTS } from './face0Prompts';
import { FACE_45_PROMPTS } from './face45Prompts';
import { FACE_90_PROMPTS } from './face90Prompts';
import { FACE_135_PROMPTS } from './face135Prompts';

/**
 * Danh sách toàn bộ Single Expression Prompts (Biểu Cảm Đơn):
 * - 0° Chính diện (12 biểu cảm: Chớp, Cười, Thoại, Giận, Shock, Buồn, Nháy, Khóc, Sợ, Smirk, Shy, Focus)
 * - 45° Xoay trái (11 biểu cảm: Chớp, Cười, Thoại, Giận, Shock, Buồn, Nháy, Khóc, Smirk, Sợ, Shy)
 * - 90° Nhìn ngang (7 biểu cảm: Chớp, Thoại, Cười, Giận, Shock, Buồn, Khóc)
 * - 135° Ngoái nhìn (4 biểu cảm: Chớp, Cười, Thoại, Cảnh giác)
 */
export const FACE_SINGLE_EXPRESSION_PROMPTS: PromptItem[] = [
  ...FACE_0_PROMPTS,
  ...FACE_45_PROMPTS,
  ...FACE_90_PROMPTS,
  ...FACE_135_PROMPTS,
];

export {
  FACE_0_PROMPTS,
  FACE_45_PROMPTS,
  FACE_90_PROMPTS,
  FACE_135_PROMPTS,
};
