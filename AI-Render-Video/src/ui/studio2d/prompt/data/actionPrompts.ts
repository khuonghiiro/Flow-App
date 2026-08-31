import { PromptItem } from '../types';

export const ACTION_PROMPTS: PromptItem[] = [
  // ─── CHẠY (RUN CYCLE) THEO CÁC GÓC ───
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
    title: 'Chạy - 135° Lưng Lệch (Run 135° Back-Left)',
    subtitle: 'Chạy nhanh hướng chéo lên trên từ phía sau lưng',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Chạy Theo Góc',
    icon: '🏃',
    promptType: 'video',
    tags: ['run', '135deg', 'back_left', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 135° Back-Left',
      loopType: 'Seamless Fast Loop',
      keyPoints: ['Chạy chéo xa dần camera', 'Bóng lưng và tóc tung bay', 'Chân đạp mạnh về sau'],
    },
    infoNote: '💡 Dùng cho các cảnh nhân vật chạy truy đuổi hoặc rút lui theo đường chéo.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning to front, showing face, turning to 90 degrees`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 135° BACK-LEFT RUN CYCLE.
Use the 135° back-left reference image as LOCKED IDENTITY SOURCE.

MOTION (135° BACK-LEFT RUN):
- Character runs in place angled away from camera towards upper-left
- Vigorous leg drive and heel kick viewed from behind
- Back sash, hair, and sleeves stream back with rapid momentum
- Energetic rhythmic tempo

CONSTRAINTS:
- Maintain 135° back-left angle throughout
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

  // ─── NGỒI (SITTING - KHÔNG GHẾ) THEO CÁC GÓC ───
  {
    id: 'sit_angle0',
    title: 'Ngồi - 0° Chính Diện (Sit Front - Không Ghế)',
    subtitle: 'Tư thế ngồi trực diện 0° lơ lửng trên nền xanh (HOÀN TOÀN KHÔNG HIỆN GHẾ)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Ngồi Theo Góc',
    icon: '🪑',
    promptType: 'video',
    tags: ['sit', '0deg', 'front', 'invisible_chair', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Subtle Loop',
      keyPoints: ['Nhân vật ngồi chính diện trên ghế vô hình', 'Không có ghế/sàn', 'Thở nhẹ nhàng'],
    },
    infoNote: '💡 ĐẶC BIỆT: Ghế hoàn toàn vô hình! Bạn có thể ghép đè nhân vật ngồi lên bất kỳ ngai vàng hoặc ghế 3D.',
    negativePrompt: `eyes, eyebrows, mouth, nose, standing, walking, visible chair, visible stool, visible bench, throne, furniture, floor`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 0° FRONT SITTING ON INVISIBLE CHAIR.
Use the 0° front reference image as LOCKED IDENTITY SOURCE.

CRITICAL INSTRUCTION — INVISIBLE CHAIR / NO FURNITURE:
- Character is posed sitting facing DIRECTLY AT CAMERA (0°) on an INVISIBLE chair
- NO chair, NO stool, NO bench, NO throne, NO furniture of any kind is rendered
- Pure solid chroma-key green #00FF00 fills all space beneath and around character
- Thighs horizontal, knees bent 90°, hands resting symmetrically on lap
- Subtle breathing chest motion, gentle hair flutter

CONSTRAINTS:
- Pure 0° front view
- Faceless blank head
- Pure chroma green #00FF00 with NO shadows
- Seamless loop`,
  },
  {
    id: 'sit_angle45',
    title: 'Ngồi - 45° Xoay Trái (Sit 45° Left - Không Ghế)',
    subtitle: 'Tư thế ngồi góc 3/4 trái trên ghế vô hình (Góc ngồi đẹp nhất)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Ngồi Theo Góc',
    icon: '🪑',
    promptType: 'video',
    tags: ['sit', '45deg', 'isometric', 'invisible_chair', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 45° Left',
      loopType: 'Subtle Loop',
      keyPoints: ['Tư thế ngồi 3/4 thanh lịch', 'Không có ghế', 'Dễ dàng ghép vào bối cảnh 3D'],
    },
    infoNote: '💡 Góc ngồi 45° tạo chiều sâu tốt nhất khi ghép vào xe ngựa, bàn trà hoặc ngai thất.',
    negativePrompt: `eyes, eyebrows, mouth, nose, standing, walking, visible chair, visible furniture, floor props`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 45° THREE-QUARTER LEFT SITTING ON INVISIBLE CHAIR.
Use the 45° left reference image as LOCKED IDENTITY SOURCE.

CRITICAL INSTRUCTION — INVISIBLE CHAIR:
- Character posed sitting at 45-degree angle to left as if on an invisible seat
- NO chair or furniture rendered; pure chroma green #00FF00 surrounding
- Legs angled naturally, arms resting on lap or invisible armrests
- Calm, poised posture with subtle breathing motion

CONSTRAINTS:
- Strict 45° angle
- Faceless blank head
- Chroma green #00FF00
- Seamless loop`,
  },
  {
    id: 'sit_angle90',
    title: 'Ngồi - 90° Side Profile (Sit Side - Không Ghế)',
    subtitle: 'Tư thế ngồi nhìn ngang 90° trên ghế vô hình',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Ngồi Theo Góc',
    icon: '🪑',
    promptType: 'video',
    tags: ['sit', '90deg', 'side', 'profile', 'invisible_chair'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 90° Profile',
      loopType: 'Subtle Loop',
      keyPoints: ['Lưng thẳng, đùi ngang 90°', 'Ghế hoàn toàn vô hình', 'Nhìn ngang sang trái'],
    },
    infoNote: '💡 Tư thế ngồi nhìn ngang giúp bạn căn góc chính xác khi đặt nhân vật lên ghế cạnh cửa sổ.',
    negativePrompt: `eyes, eyebrows, mouth, nose, visible chair, visible bench, floor, turning to front`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 90° SIDE PROFILE SITTING ON INVISIBLE CHAIR.
Use the 90° side profile reference image as LOCKED IDENTITY SOURCE.

CRITICAL INSTRUCTION — INVISIBLE CHAIR:
- Character posed in pure 90° left side view sitting on an invisible chair
- NO furniture visible; 100% chroma green #00FF00 background
- Back straight, thighs horizontal, knees at 90° angle
- Hands folded on lap, subtle breathing rise and fall

CONSTRAINTS:
- Pure 90° side profile
- Faceless blank head
- Chroma green #00FF00
- Seamless loop`,
  },
  {
    id: 'sit_angle135',
    title: 'Ngồi - 135° Lưng Lệch (Sit 135° - Không Ghế)',
    subtitle: 'Tư thế ngồi nhìn từ sau lệch trái trên ghế vô hình',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Ngồi Theo Góc',
    icon: '🪑',
    promptType: 'video',
    tags: ['sit', '135deg', 'back_left', 'invisible_chair'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 135°',
      loopType: 'Subtle Loop',
      keyPoints: ['Ngồi nhìn từ phía sau 3/4', 'Không hiển thị ghế', 'Tóc sau rủ tự nhiên'],
    },
    infoNote: '💡 Thích hợp cho các góc quay camera đặt từ sau lưng nhân vật đang ngồi đàm đạo.',
    negativePrompt: `eyes, eyebrows, mouth, nose, visible chair, visible stool, showing face`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 135° BACK-LEFT SITTING ON INVISIBLE CHAIR.
Use the 135° back-left reference image as LOCKED IDENTITY SOURCE.

CRITICAL: Character sitting at 135° back-left orientation on an INVISIBLE chair (NO chair/furniture visible).
- Pure chroma green #00FF00 background
- Back robes draped naturally, subtle breathing motion
- Seamless loop`,
  },
  {
    id: 'sit_angle180',
    title: 'Ngồi - 180° Sau Lưng (Sit Back - Không Ghế)',
    subtitle: 'Tư thế ngồi nhìn thẳng từ sau lưng 180° trên ghế vô hình',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Ngồi Theo Góc',
    icon: '🪑',
    promptType: 'video',
    tags: ['sit', '180deg', 'back', 'invisible_chair'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 180° Back',
      loopType: 'Subtle Loop',
      keyPoints: ['Lưng thẳng nhìn từ sau', 'Ghế vô hình', 'Không quay đầu lại'],
    },
    infoNote: '💡 Dùng cho cảnh nhân vật ngồi ngắm phong cảnh hoặc quay lưng về phía người chơi.',
    negativePrompt: `eyes, eyebrows, mouth, nose, visible chair, turning around, showing face`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 180° REAR VIEW SITTING ON INVISIBLE CHAIR.
Use the 180° back view reference image as LOCKED IDENTITY SOURCE.

CRITICAL: Character sitting facing directly away from camera (180°) on an INVISIBLE chair (NO chair rendered).
- Pure chroma green #00FF00 background
- Symmetrical spine alignment, back of hair and robes visible
- Seamless loop`,
  },

  // ─── NẰM (LYING DOWN - KHÔNG GIƯỜNG) THEO CÁC GÓC ───
  {
    id: 'lie_angle0',
    title: 'Nằm - Góc Trực Diện (Lie Front - Không Giường)',
    subtitle: 'Nằm ngửa nhìn từ góc thẳng/trên xuống (HOÀN TOÀN KHÔNG HIỆN GIƯỜNG/NỆM)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nằm Theo Góc',
    icon: '🛌',
    promptType: 'video',
    tags: ['lie', 'front', 'top_down', 'invisible_bed', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock',
      loopType: 'Subtle Loop',
      keyPoints: ['Nằm ngửa thư thái', 'Không có giường/gối', 'Ngực phập phồng thở nhẹ'],
    },
    infoNote: '💡 Giường hoàn toàn vô hình! Dễ dàng ghép nhân vật nằm lên bãi cỏ, sàn mây hoặc giường ngủ.',
    negativePrompt: `eyes, eyebrows, mouth, nose, standing, sitting, visible bed, mattress, pillow, blanket, furniture`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop LYING ON BACK ON INVISIBLE SURFACE.
Use reference image as LOCKED IDENTITY SOURCE.

CRITICAL INSTRUCTION — INVISIBLE BED / NO FURNITURE:
- Character is lying flat on back on an INVISIBLE surface
- NO bed, NO mattress, NO pillow, NO blanket rendered
- Solid chroma-key green #00FF00 fills entire space
- Body extended horizontally, arms resting at sides or on stomach
- Gentle rhythmic chest breathing rise and fall

CONSTRAINTS:
- Faceless blank head
- Pure chroma green #00FF00
- Seamless loop`,
  },
  {
    id: 'lie_angle45',
    title: 'Nằm - Góc 3/4 Phối Cảnh (Lie 3/4 - Không Giường)',
    subtitle: 'Nằm nghiêng hoặc ngửa góc 3/4 có chiều sâu phối cảnh',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nằm Theo Góc',
    icon: '🛌',
    promptType: 'video',
    tags: ['lie', '45deg', 'perspective', 'invisible_bed'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 3/4 View',
      loopType: 'Subtle Loop',
      keyPoints: ['Nằm góc 3/4 có độ nghiêng', 'Không có giường', 'Tóc xõa tự nhiên'],
    },
    infoNote: '💡 Góc 3/4 tạo cảm giác nằm nghỉ ngơi thư thái và có chiều sâu không gian tốt nhất.',
    negativePrompt: `eyes, eyebrows, mouth, nose, visible bed, pillow, sheets, standing, sitting`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 3/4 PERSPECTIVE LYING DOWN ON INVISIBLE SURFACE.
Use reference image as LOCKED IDENTITY SOURCE.

CRITICAL: Character lying down at 3/4 perspective angle on an INVISIBLE surface (NO bed/mattress/pillow visible).
- Pure chroma green #00FF00 surrounding
- Hair and robes draped naturally
- Subtle peaceful breathing motion
- Seamless loop`,
  },
  {
    id: 'lie_angle90',
    title: 'Nằm - Góc Nhìn Ngang (Lie Side Profile - Không Giường)',
    subtitle: 'Nằm ngang hoàn toàn nhìn từ bên cạnh',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nằm Theo Góc',
    icon: '🛌',
    promptType: 'video',
    tags: ['lie', '90deg', 'side', 'invisible_bed'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 90° Horizontal',
      loopType: 'Subtle Loop',
      keyPoints: ['Nằm ngang thẳng trục', 'Không có đồ vật/giường', 'Thở nhịp nhàng'],
    },
    infoNote: '💡 Phù hợp cho các cảnh nhân vật nằm dưỡng thương hoặc nghỉ ngơi trong game màn hình ngang.',
    negativePrompt: `eyes, eyebrows, mouth, nose, visible bed, visible floor, standing, walking`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 90° SIDE VIEW LYING DOWN ON INVISIBLE SURFACE.
Use reference image as LOCKED IDENTITY SOURCE.

CRITICAL: Character lying down horizontally in pure side profile on an INVISIBLE surface (NO bed visible).
- Pure solid chroma green #00FF00 background
- Subtle chest breathing motion
- Seamless loop`,
  },

  // ─── NHẢY (JUMP ANIMATION) THEO CÁC GÓC ───
  {
    id: 'jump_angle0',
    title: 'Nhảy - 0° Chính Diện (Jump Front View)',
    subtitle: 'Chu kỳ nhảy 4 pha đối diện camera (Chuẩn bị -> Bật nhảy -> Trên không -> Tiếp đất)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nhảy Theo Góc',
    icon: '⬆️',
    promptType: 'video',
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
    id: 'jump_angle180',
    title: 'Nhảy - 180° Sau Lưng (Jump Back View)',
    subtitle: 'Chu kỳ nhảy nhìn thẳng từ sau lưng',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nhảy Theo Góc',
    icon: '⬆️',
    promptType: 'video',
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

  // ─── ĐỨNG YÊN (IDLE ANIMATION) THEO CÁC GÓC ───
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

  // ─── ĐÁNH CÔNG (ATTACK) THEO CÁC GÓC ───
  {
    id: 'attack_angle0',
    title: 'Đánh Công - 0° Chính Diện (Attack Front View)',
    subtitle: 'Vung đòn chém/chưởng lực thẳng về phía trước đối diện camera',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Tấn Công Theo Góc',
    icon: '⚔️',
    promptType: 'video',
    tags: ['attack', '0deg', 'front', 'combat', 'strike'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Combat Loop',
      keyPoints: ['Tung đòn thẳng về phía trước', 'Vũ khí vô hình', 'Tà áo bay mạnh theo lực vung'],
    },
    infoNote: '💡 Đánh công với vũ khí vô hình ở góc 0° đối diện người xem.',
    negativePrompt: `eyes, eyebrows, mouth, nose, stumbling, falling, realistic blood`,
    rawPrompt: `TASK: Image-to-video animation, 1.5-2 second seamless loop 0° FRONT COMBAT STRIKE.
Use 0° front reference. Dynamic strike motion with invisible weapon towards camera. Snappy combat timing, resets to stance. Chroma green #00FF00. Seamless loop.`,
  },
  {
    id: 'attack_angle45',
    title: 'Đánh Công - 45° Xoay Trái (Attack 45° Left)',
    subtitle: 'Vung đòn chém/phát lực góc 3/4 chéo trái (Góc xuất chiêu đẹp nhất)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Tấn Công Theo Góc',
    icon: '⚔️',
    promptType: 'video',
    tags: ['attack', '45deg', 'isometric', 'combat', 'slash'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 45° Left',
      loopType: 'Combat Loop',
      keyPoints: ['Vung kiếm chém góc chéo 45°', 'Thân người xoay trợ lực', 'Khôi phục thế thủ'],
    },
    infoNote: '💡 Góc xuất chiêu 45° cho thấy rõ toàn bộ động tác vung tay và xoay hông phát lực.',
    negativePrompt: `eyes, eyebrows, mouth, nose, distorted limbs, weak strike`,
    rawPrompt: `TASK: Image-to-video animation, 1.5-2 second seamless loop 45° THREE-QUARTER LEFT COMBAT ATTACK.
Use 45° left reference. Powerful strike with invisible weapon at 45° left angle. Torso rotates for power, recovers cleanly. Chroma green #00FF00. Seamless loop.`,
  },
  {
    id: 'attack_angle90',
    title: 'Đánh Công - 90° Side Profile (Attack Side View)',
    subtitle: 'Vung đòn chém/phóng lực nhìn ngang 90° (Side-scroller Attack)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Tấn Công Theo Góc',
    icon: '⚔️',
    promptType: 'video',
    tags: ['attack', '90deg', 'side', 'combat', 'platformer'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Profile',
      loopType: 'Combat Loop',
      keyPoints: ['Vung đòn chém ngang màn hình', 'Trọng tâm dồn về chân trước', 'Phục vụ game 2D'],
    },
    infoNote: '💡 Góc chém 90° nhìn ngang chuẩn xác cho các combo liên hoàn trong game đi cảnh.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning to camera, falling over`,
    rawPrompt: `TASK: Image-to-video animation, 1.5-2 second seamless loop 90° SIDE PROFILE ATTACK ANIMATION.
Use 90° side reference. Full side view strike motion with invisible weapon. Chroma green #00FF00. Seamless loop.`,
  },
  {
    id: 'attack_angle180',
    title: 'Đánh Công - 180° Sau Lưng (Attack Back View)',
    subtitle: 'Vung đòn chém/phát lực nhìn từ phía sau lưng',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Tấn Công Theo Góc',
    icon: '⚔️',
    promptType: 'video',
    tags: ['attack', '180deg', 'back', 'combat'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 180° Back',
      loopType: 'Combat Loop',
      keyPoints: ['Đánh công nhìn từ phía sau', 'Tà áo và tóc vung mạnh'],
    },
    infoNote: '💡 Dùng cho cảnh nhân vật quay lưng chém trúng quái vật phía trước.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning around, showing face`,
    rawPrompt: `TASK: Image-to-video animation, 1.5-2 second seamless loop 180° REAR ATTACK ANIMATION.
Use 180° back reference. Strike motion directed forward viewed from behind. Chroma green #00FF00. Seamless loop.`,
  },

  // ─── BIỂU CẢM ĐẦU (HEAD MOTIONS) ───
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
