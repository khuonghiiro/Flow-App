import { PromptItem } from '../../types';

export const IDLE_PROMPTS: PromptItem[] = [
  {
    id: 'idle_angle0',
    title: 'Đứng Yên - 0° Chính Diện (Idle Front View)',
    subtitle: 'Đứng yên tự nhiên góc 0° (Thở nhẹ, tà áo lay nhẹ)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Đứng Yên Theo Góc',
    icon: '🧍',
    promptType: 'video',
    tags: ['idle', '0deg', 'front', 'breathe', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Subtle Loop',
      keyPoints: ['Đứng yên thẳng hướng camera', 'Thở nhẹ nhàng', 'Tóc bay nhẹ'],
    },
    infoNote: '💡 Trạng thái chờ mặc định của nhân vật ở góc chính diện.',
    negativePrompt: `eyes, eyebrows, mouth, nose, walking, running, jumping, violent motion`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 0° FRONT IDLE ANIMATION.
Use the 0° front reference image as LOCKED IDENTITY SOURCE.

MOTION:
- Subtle breathing chest expansion/contraction
- Arms relaxed at sides, gentle fabric sway in soft breeze
- Faceless blank head centered

CONSTRAINTS:
- 0° front view
- Chroma green #00FF00
- Seamless loop`,
  },
  {
    id: 'idle_angle45',
    title: 'Đứng Yên - 45° Xoay Trái (Idle 45° Left)',
    subtitle: 'Đứng yên tự nhiên góc 3/4 chéo trái',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Đứng Yên Theo Góc',
    icon: '🧍',
    promptType: 'video',
    tags: ['idle', '45deg', 'isometric', 'breathe', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 45° Left',
      loopType: 'Subtle Loop',
      keyPoints: ['Tư thế đứng 3/4 tự nhiên', 'Thở nhẹ', 'Tà áo lay nhẹ'],
    },
    infoNote: '💡 Trạng thái đứng chờ lý tưởng cho nhân vật trong game 2.5D Isometric.',
    negativePrompt: `eyes, eyebrows, mouth, nose, moving away, turning body, dancing`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 45° THREE-QUARTER LEFT IDLE ANIMATION.
Use the 45° left reference image as LOCKED IDENTITY SOURCE.

MOTION:
- Character standing poised at 45° left angle
- Subtle weight shift and gentle breathing motion

CONSTRAINTS:
- 45° left angle
- Faceless blank head
- Chroma green #00FF00
- Seamless loop`,
  },
  {
    id: 'idle_angle90',
    title: 'Đứng Yên - 90° Side Profile (Idle Side View)',
    subtitle: 'Đứng yên tự nhiên nhìn ngang 90°',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Đứng Yên Theo Góc',
    icon: '🧍',
    promptType: 'video',
    tags: ['idle', '90deg', 'side', 'profile'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 90° Profile',
      loopType: 'Subtle Loop',
      keyPoints: ['Đứng yên nhìn ngang bên trái', 'Thở nhẹ'],
    },
    infoNote: '💡 Dùng cho trạng thái đứng nghỉ trong game màn hình ngang.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning to camera, walking`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 90° SIDE PROFILE IDLE ANIMATION.
Use 90° side reference. Subtle breathing motion in pure 90° left profile. Chroma green #00FF00. Seamless loop.`,
  },
  {
    id: 'idle_angle135',
    title: 'Đứng Yên - 135° Lưng Lệch Phải (Idle 135° Back-Right)',
    subtitle: 'Đứng yên tự nhiên nhìn từ sau lưng lệch xoay về CẠNH PHẢI ảnh (Thở nhẹ, tà áo lay)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Đứng Yên Theo Góc',
    icon: '🧍',
    promptType: 'video',
    tags: ['idle', '135deg', 'back_right', 'breathe', 'loop', 'right'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 135° Back-Right',
      loopType: 'Subtle Loop',
      keyPoints: ['Đứng yên góc 135° chéo sau lưng hướng sang phải', 'Nhịp thở nhẹ nhàng', 'Tóc sau và vạt áo đung đưa theo gió'],
    },
    infoNote: '💡 Trạng thái đứng chờ lý tưởng cho nhân vật nhìn từ góc 3/4 phía sau lệch sang phải.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning to front, showing face, turning left, walking, running, violent motion`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 135° BACK-RIGHT IDLE ANIMATION.
Use 135° back-right reference image as LOCKED IDENTITY SOURCE.

MOTION (135° BACK-RIGHT IDLE):
- Character standing poised at 135° back-right angle (angled away towards upper-right, facing right edge)
- Back of shoulders and right profile visible; NO facial features shown
- Subtle breathing rise and fall of shoulders and back
- Back sash, long hair, and sleeve edges flutter softly in gentle ambient breeze

CONSTRAINTS:
- Strictly maintain 135° back-right orientation (facing right edge)
- Faceless back head, no turning
- Pure chroma green #00FF00 background
- Seamless loop`,
  },
  {
    id: 'idle_angle180',
    title: 'Đứng Yên - 180° Sau Lưng (Idle Back View)',
    subtitle: 'Đứng yên tự nhiên nhìn thẳng từ sau lưng 180°',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Đứng Yên Theo Góc',
    icon: '🧍',
    promptType: 'video',
    tags: ['idle', '180deg', 'back', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 180° Back',
      loopType: 'Subtle Loop',
      keyPoints: ['Đứng quay lưng thẳng', 'Tóc sau bay nhẹ theo gió'],
    },
    infoNote: '💡 Dùng cho cảnh nhân vật đứng quay lưng ngắm cảnh hoặc chờ xuất kích.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning head, showing face`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 180° REAR IDLE ANIMATION.
Use 180° back reference. Subtle breathing and hair sway viewed from behind. Chroma green #00FF00. Seamless loop.`,
  },
];
