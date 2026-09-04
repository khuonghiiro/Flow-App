import { PromptItem } from '../../types';

export const JUMP_PROMPTS: PromptItem[] = [
  {
    id: 'jump_angle0',
    title: 'Nhảy - 0° Chính Diện (Jump Front View)',
    subtitle: 'Chu kỳ nhảy 4 pha đối diện camera (Chuẩn bị -> Bật nhảy -> Trên không -> Tiếp đất)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nhảy Theo Góc',
    icon: '⬆️',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle0',
    refAngleLabel: '0° Chính Diện',
    tags: ['jump', '0deg', 'front', 'action', 'loop'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Snappy Loop',
      keyPoints: ['4 pha rõ rệt', 'Bật nhảy thẳng lên trên tại chỗ', 'Tiếp đất giảm chấn'],
    },
    infoNote: '💡 Nhảy góc 0° trực diện với độ cao 1-1.5 lần thân người, tà áo bung xòe ở đỉnh điểm.',
    negativePrompt: `eyes, eyebrows, mouth, nose, horizontal drifting, weak jump, distorted limbs, falling over`,
    rawPrompt: `TASK: Image-to-video animation, 1.5-2 second seamless loop 0° FRONT JUMP ANIMATION.
Use the 0° front reference image as LOCKED IDENTITY SOURCE.

4 PHASES:
1. Crouch preparation: knees bend, arms swing down
2. Takeoff: powerful upward thrust, arms swing up
3. Apex: mid-air peak (~1-1.5 body heights), hair and robes fan out upward
4. Landing: feet touch down, knees absorb impact, resets to stance

CONSTRAINTS:
- 0° front view throughout
- Faceless blank head
- Chroma green #00FF00
- Seamless loop`,
  },
  {
    id: 'jump_angle45',
    title: 'Nhảy - 45° Xoay Trái (Jump 45° Left)',
    subtitle: 'Chu kỳ nhảy 4 pha góc chéo 3/4 trái (Năng động & kịch tính)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nhảy Theo Góc',
    icon: '⬆️',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle45',
    refAngleLabel: '45° Nghiêng Trái',
    tags: ['jump', '45deg', 'isometric', 'action', 'loop'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 45° Left',
      loopType: 'Snappy Loop',
      keyPoints: ['Tư thế nhảy 3/4 thể hiện rõ lực bật', 'Tà áo bay đẹp mắt', 'Tiếp đất hồi phục'],
    },
    infoNote: '💡 Góc nhảy 45° là góc đẹp nhất để làm skill phi thân hoặc vượt chướng ngại vật.',
    negativePrompt: `eyes, eyebrows, mouth, nose, body twisting, floaty physics, distorted body`,
    rawPrompt: `TASK: Image-to-video animation, 1.5-2 second seamless loop 45° THREE-QUARTER LEFT JUMP ANIMATION.
Use the 45° left reference image as LOCKED IDENTITY SOURCE.

MOTION:
- Dynamic 4-phase vertical jump maintaining 45° left angle
- Snappy takeoff and crisp landing recovery
- Robes and ribbons flutter dramatically during air apex

CONSTRAINTS:
- Maintain 45° left orientation
- Faceless blank head
- Chroma green #00FF00
- Seamless loop`,
  },
  {
    id: 'jump_angle90',
    title: 'Nhảy - 90° Side Profile (Jump Side View)',
    subtitle: 'Chu kỳ nhảy nhìn ngang toàn phần (Side-scroller Jump)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nhảy Theo Góc',
    icon: '⬆️',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle90',
    refAngleLabel: '90° Nhìn Ngang',
    tags: ['jump', '90deg', 'side', 'platformer'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Profile',
      loopType: 'Snappy Loop',
      keyPoints: ['Thấy rõ quỹ đạo nhảy hình parabol', 'Đánh tay và gập gối chuẩn 2D Animation'],
    },
    infoNote: '💡 Góc nhảy 90° phục vụ trực tiếp cho game platformer màn hình ngang.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning to front, weak leap, floating`,
    rawPrompt: `TASK: Image-to-video animation, 1.5-2 second seamless loop 90° FULL SIDE PROFILE JUMP ANIMATION.
Use the 90° side profile reference image as LOCKED IDENTITY SOURCE.

MOTION:
- Pure 2D side profile vertical leap cycle
- Clear preparation, high spring takeoff, airborne apex pose, and knee absorption landing
- Clean anime physics timing

CONSTRAINTS:
- Strictly 90° side profile
- Faceless blank head
- Chroma green #00FF00
- Seamless loop`,
  },
  {
    id: 'jump_angle135',
    title: 'Nhảy - 135° Lưng Lệch Phải (Jump 135° Back-Right)',
    subtitle: 'Chu kỳ nhảy 4 pha nhìn từ phía sau lưng lệch xoay về CẠNH PHẢI ảnh',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nhảy Theo Góc',
    icon: '⬆️',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle135',
    refAngleLabel: '135° Lưng Phải',
    tags: ['jump', '135deg', 'back_right', 'action', 'loop', 'right'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 135° Back-Right',
      loopType: 'Snappy Loop',
      keyPoints: ['Bật nhảy tại chỗ góc chéo sau 135° hướng sang phải', 'Tóc sau và tà áo tung bay lên không trung', 'Tiếp đất hồi phục tư thế'],
    },
    infoNote: '💡 Dùng cho các cảnh nhân vật bật nhảy vượt chướng ngại vật hoặc khinh công di chuyển chéo xa dần sang phải.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning to front, showing face, turning left, erratic landing, distorted body`,
    rawPrompt: `TASK: Image-to-video animation, 1.5-2 second seamless loop 135° BACK-RIGHT JUMP ANIMATION.
Use the 135° back-right reference image as LOCKED IDENTITY SOURCE.

4 PHASES (135° BACK-RIGHT JUMP):
1. Crouch preparation: knees bend, arms swing back, viewed from rear-right
2. Takeoff: explosive upward spring angled away towards upper-right (facing right edge)
3. Apex: mid-air peak (~1-1.5 body heights), back sash, hair ribbons, and flowing robes fan upward dynamically
4. Landing: feet touch down cleanly, knees absorb impact, returns to ready stance

CONSTRAINTS:
- Maintain strict 135° back-right orientation throughout (facing right edge, NO face visible)
- Vertical leap in place on chroma green #00FF00
- Seamless loop matching start and end frames`,
  },
  {
    id: 'jump_angle180',
    title: 'Nhảy - 180° Sau Lưng (Jump Back View)',
    subtitle: 'Chu kỳ nhảy nhìn thẳng từ sau lưng',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nhảy Theo Góc',
    icon: '⬆️',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle180',
    refAngleLabel: '180° Sau Lưng',
    tags: ['jump', '180deg', 'back', 'loop'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 180° Back',
      loopType: 'Snappy Loop',
      keyPoints: ['Nhảy thẳng lên nhìn từ sau', 'Tóc dài và tà áo sau tung bay lên đỉnh'],
    },
    infoNote: '💡 Hoạt ảnh bật nhảy nhìn từ phía sau khi nhân vật nhảy qua tường rào hoặc cổng thành.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning around, showing face, asymmetrical landing`,
    rawPrompt: `TASK: Image-to-video animation, 1.5-2 second seamless loop 180° REAR JUMP ANIMATION.
Use the 180° back reference image as LOCKED IDENTITY SOURCE.

MOTION:
- Vertical jump viewed directly from behind (180°)
- Symmetrical takeoff and landing; robes billow upward at apex

CONSTRAINTS:
- Strictly 180° rear view
- Chroma green #00FF00
- Seamless loop`,
  },
];
