import { PromptItem } from '../../types';

export const RUN_PROMPTS: PromptItem[] = [
  {
    id: 'run_angle0',
    title: 'Chạy - 0° Chính Diện (Run Front View)',
    subtitle: 'Chạy nhanh về phía trước đối diện camera (Tại chỗ, có suspension phase)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Chạy Theo Góc',
    icon: '🏃',
    promptType: 'video',
    tags: ['run', '0deg', 'front', 'fast', 'loop', 'in-place'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Seamless Fast Loop',
      keyPoints: ['Chạy tới trước tại chỗ đối diện camera', 'Hai chân so le bật rời mặt đất', 'Tà áo và tóc tung bay'],
    },
    infoNote: '💡 Chạy góc 0° đối diện camera với nhịp bước dồn dập, tốc độ cao.',
    negativePrompt: `eyes, eyebrows, mouth, nose, side stepping, body rotation, walking speed, flying, levitating, background moving`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop FRONT VIEW (0°) RUN CYCLE.
Use the 0° front reference image as LOCKED IDENTITY SOURCE.

MOTION (0° FRONT RUN):
- Character runs in place facing DIRECTLY towards camera
- Fast alternating leg strides with suspension phase (both feet leave ground briefly)
- Arms pump forward and backward vigorously in counter-motion to legs
- Torso has slight forward lean with rhythmic vertical bounce
- Hair and sleeves stream and ripple with running momentum
- Faceless blank head stays centered

CONSTRAINTS:
- Do NOT rotate body away from 0° front view
- Running in place on chroma green #00FF00
- 100% seamless looping`,
  },
  {
    id: 'run_angle45',
    title: 'Chạy - 45° Xoay Trái (Run 45° Left)',
    subtitle: 'Chạy nhanh góc 3/4 chéo trái (Phù hợp nhất cho game 2.5D Isometric)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Chạy Theo Góc',
    icon: '🏃',
    promptType: 'video',
    tags: ['run', '45deg', 'isometric', 'fast', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 45° Left',
      loopType: 'Seamless Fast Loop',
      keyPoints: ['Sải chân mạnh mẽ góc 45°', 'Góc nhìn 3/4 năng động', 'Tà áo bay về sau'],
    },
    infoNote: '💡 Góc 45° là góc chạy phổ biến nhất trong các hoạt ảnh nhập vai và hành động.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning to 90 degrees, turning to front, erratic bouncing, distorted limbs`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 45° THREE-QUARTER LEFT RUN CYCLE.
Use the 45° left reference image as LOCKED IDENTITY SOURCE.

MOTION (45° LEFT RUN):
- Character runs in place maintaining fixed 45-degree angle to the left
- Powerful alternating strides with clear suspension phase
- Front leg and back leg work dynamically with deep knee flex
- Arms pump vigorously in running rhythm
- Flowing robes and hair ribbons trail backward dynamically

CONSTRAINTS:
- Maintain strict 45° angle throughout (NO drift)
- Running in place on chroma green #00FF00
- Seamless loop at start/end`,
  },
  {
    id: 'run_angle90',
    title: 'Chạy - 90° Side Profile (Run Side Left)',
    subtitle: 'Chạy nhanh nhìn ngang toàn phần (Side-scroller Run)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Chạy Theo Góc',
    icon: '🏃',
    promptType: 'video',
    tags: ['run', '90deg', 'side', 'profile', 'platformer'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Profile',
      loopType: 'Seamless Fast Loop',
      keyPoints: ['Biên độ sải chân và đánh tay tối đa', 'Nhìn ngang toàn diện bên trái', 'Trọng lực chân thực'],
    },
    infoNote: '💡 Góc 90° nhìn ngang giúp cắt khung animation chạy chuẩn cho game 2D platformer.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning to front, turning to rear, floating feet, sliding`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 90° FULL SIDE PROFILE RUN CYCLE.
Use the 90° side profile reference image as LOCKED IDENTITY SOURCE.

MOTION (90° SIDE RUN):
- Full 2D side view run cycle moving leftward in place
- High knee lift, powerful backward push-off, clear aerial suspension phase
- Front and back arms pump with wide athletic arcs
- Torso has energetic forward lean
- Hair and robes whip backward from wind resistance

CONSTRAINTS:
- Strictly 90° side profile (zero angle rotation)
- In-place run cycle on chroma green #00FF00
- Perfect seamless loop`,
  },
  {
    id: 'run_angle135',
    title: 'Chạy - 135° Lưng Lệch Phải (Run 135° Back-Right)',
    subtitle: 'Chạy nhanh nhìn từ phía sau lưng xoay về CẠNH PHẢI ảnh',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Chạy Theo Góc',
    icon: '🏃',
    promptType: 'video',
    tags: ['run', '135deg', 'back_right', 'loop', 'right'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 135° Back-Right',
      loopType: 'Seamless Fast Loop',
      keyPoints: ['Chạy chéo xa dần camera về phía phải', 'Bóng lưng và tóc tung bay', 'Chân đạp mạnh về sau'],
    },
    infoNote: '💡 Dùng cho các cảnh nhân vật chạy truy đuổi hoặc rút lui theo đường chéo sang phải.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning to front, showing face, turning to 90 degrees, turning left`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 135° BACK-RIGHT RUN CYCLE.
Use the 135° back-right reference image as LOCKED IDENTITY SOURCE.

MOTION (135° BACK-RIGHT RUN):
- Character runs in place angled away from camera towards upper-right (facing right edge)
- Vigorous leg drive and heel kick viewed from behind
- Back sash, hair, and sleeves stream back with rapid momentum
- Energetic rhythmic tempo

CONSTRAINTS:
- Maintain 135° back-right angle throughout (facing right edge)
- Running in place on chroma green #00FF00
- Seamless looping`,
  },
  {
    id: 'run_angle180',
    title: 'Chạy - 180° Sau Lưng (Run Back View)',
    subtitle: 'Chạy nhanh nhìn thẳng từ sau lưng (Chạy vào chiều sâu)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Chạy Theo Góc',
    icon: '🏃',
    promptType: 'video',
    tags: ['run', '180deg', 'back', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 180° Back',
      loopType: 'Seamless Fast Loop',
      keyPoints: ['Chạy thẳng về phía xa', 'Hai chân so le đối xứng nhìn từ sau', 'Tóc dài và tà áo sau tung bay'],
    },
    infoNote: '💡 Thích hợp cho cảnh nhân vật chạy tiến vào chiến trường hoặc đi vào sâu trong phó bản.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning around, showing face, asymmetrical stride`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 180° REAR VIEW RUN CYCLE.
Use the 180° back view reference image as LOCKED IDENTITY SOURCE.

MOTION (180° REAR RUN):
- Character runs in place facing DIRECTLY AWAY from camera (180°)
- Powerful alternating foot drive and suspension seen from behind
- Symmetrical arm pumping and dynamic robe billowing
- Hair streams straight back in wind

CONSTRAINTS:
- Pure 180° back view, NO head turning
- In-place run cycle on chroma green #00FF00
- Seamless looping`,
  },
];
