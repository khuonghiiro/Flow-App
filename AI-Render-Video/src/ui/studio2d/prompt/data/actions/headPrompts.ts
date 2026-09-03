import { PromptItem } from '../../types';

export const HEAD_PROMPTS: PromptItem[] = [
  {
    id: 'head_shake',
    title: 'Lắc Đầu (Head Shake)',
    subtitle: 'Lắc đầu từ chối, nghi vấn hoặc không đồng ý',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Biểu Cảm Đầu',
    icon: '🤨',
    promptType: 'video',
    tags: ['head_shake', 'no', 'disagree', 'expression', 'dialogue'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 FPS',
      camera: 'Static Lock (Medium / Close-up)',
      loopType: 'Seamless Loop',
      keyPoints: ['Thân mình giữ nguyên', 'Chỉ có đầu xoay trái phải 15-20 độ', 'Tóc đung đưa theo quán tính đầu'],
    },
    infoNote: '💡 Hoạt ảnh chuyển động đầu cho các đoạn hội thoại: phủ nhận, từ chối hoặc lắc đầu bất lực.',
    negativePrompt: `body moving, body rotation, leaning, arms flailing, legs moving, distorted neck`,
    rawPrompt: `TASK: Image-to-video animation, 1.5-2 second seamless loop HEAD SHAKE ANIMATION.
Use the faceless reference image as LOCKED IDENTITY SOURCE. ONLY move head.

HEAD SHAKE MOTION:
- Body and torso stay completely still
- ONLY the head rotates left 15-20° → returns to center → rotates right 15-20°
- Natural rhythmic head rotation (expressing "no" or disagreement)
- Hair and hairpins swing naturally following head momentum

SEAMLESS LOOP:
- Frame 1 matches final frame with head centered
- Chroma green #00FF00 background`,
  },
  {
    id: 'head_nod',
    title: 'Gật Đầu (Head Nod)',
    subtitle: 'Gật đầu đồng ý, tán thành, tự tin hoặc chào hỏi',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Biểu Cảm Đầu',
    icon: '✅',
    promptType: 'video',
    tags: ['head_nod', 'yes', 'agree', 'expression', 'dialogue'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 FPS',
      camera: 'Static Lock (Medium / Close-up)',
      loopType: 'Seamless Loop',
      keyPoints: ['Đầu gật nhẹ xuống rồi ngước lên', 'Tóc mái rủ nhẹ'],
    },
    infoNote: '💡 Dùng cho các cảnh nhận nhiệm vụ, đồng ý thỏa thuận hoặc đáp lại câu chào.',
    negativePrompt: `body swinging, erratic shaking, side to side rotation, extreme neck bending`,
    rawPrompt: `TASK: Image-to-video animation, 1.5-2 second seamless loop HEAD NOD ANIMATION.
Use the faceless reference image as LOCKED IDENTITY SOURCE. ONLY move head.

HEAD NOD MOTION:
- Body stays stable
- ONLY the head tilts forward (chin down slightly) → returns to center → tilts up slightly
- Smooth, natural nodding motion (expressing approval, acknowledgment, or agreement)
- Front bangs and hair accessories nod smoothly with motion

SEAMLESS LOOP:
- Returns smoothly to center resting pose at loop point
- Chroma green #00FF00 background`,
  },
  {
    id: 'look_aside',
    title: 'Ngó Sang (Look Aside)',
    subtitle: 'Xoay nhẹ đầu quan sát xung quanh một cách cảnh giác',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Biểu Cảm Đầu',
    icon: '👀',
    promptType: 'video',
    tags: ['look_aside', 'glance', 'observe', 'head_turn', 'curious'],
    videoGuide: {
      duration: '2 giây',
      fps: '24 FPS',
      camera: 'Static Lock',
      loopType: 'Seamless Loop',
      keyPoints: ['Đầu xoay nhẹ sang bên 20 độ', 'Chờ một nhịp rồi quay về chính diện'],
    },
    infoNote: '💡 Tạo cảm giác nhân vật sống động, quan sát môi trường xung quanh.',
    negativePrompt: `body turning wildly, walking away, blurry silhouette`,
    rawPrompt: `TASK: Image-to-video animation, 2 second seamless loop LOOK ASIDE / HEAD TURN ANIMATION.
Use the faceless reference image as LOCKED IDENTITY SOURCE.

MOTION:
- Body remains facing forward
- Head turns ~20° to side, holds for 0.5s in observant stance
- Head returns smoothly to center

SEAMLESS LOOP:
- Ends on center forward pose matching initial frame
- Chroma green #00FF00 background`,
  },
];
