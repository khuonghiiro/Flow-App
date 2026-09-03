import { PromptItem } from '../types';
import { FACE_MASTER_8S_PROMPTS } from './face/master8sPrompts';
import { FACE_SINGLE_EXPRESSION_PROMPTS } from './face/singleExpressionPrompts';

/**
 * Danh mục BƯỚC 4: NGŨ QUAN & BIỂU CẢM THEO CÁC GÓC (FACE & EXPRESSIONS)
 * - Nền xanh nguyên chất #00FF00
 * - CHỈ chứa: Lông mày, mắt, mũi, miệng và đổ bóng tương ứng
 * - TUYỆT ĐỐI KHÔNG chứa: Tóc, tai, viền đầu, cổ hay thân người
 * - Gồm:
 *   1) 4 Master Prompts 8 giây (mỗi góc 1 prompt diễn hoạt 4 sắc thái cảm xúc theo giây)
 *   2) Các Single Expression Prompts (tách lẻ từng biểu cảm 2-3s loop: chớp mắt, cười nói, tức giận, ngạc nhiên...)
 */
export const FACE_PROMPTS: PromptItem[] = [
  ...FACE_MASTER_8S_PROMPTS,
  ...FACE_SINGLE_EXPRESSION_PROMPTS,
];

export { FACE_MASTER_8S_PROMPTS, FACE_SINGLE_EXPRESSION_PROMPTS };
