import { PromptItem } from '../../types';

export const SIT_PROMPTS: PromptItem[] = [
  {
    id: 'sit_angle0',
    title: 'Ngồi - 0° Chính Diện (Sit Front - Không Ghế)',
    subtitle: 'Tư thế ngồi trực diện 0° lơ lửng trên nền xanh (HOÀN TOÀN KHÔNG HIỆN GHẾ)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Ngồi Theo Góc',
    icon: '🪑',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle0',
    refAngleLabel: '0° Chính Diện',
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
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle45',
    refAngleLabel: '45° Nghiêng Trái',
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
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle90',
    refAngleLabel: '90° Nhìn Ngang',
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
    title: 'Ngồi - 135° Lưng Lệch Phải (Sit 135° Back-Right - Không Ghế)',
    subtitle: 'Tư thế ngồi nhìn từ sau lệch xoay về CẠNH PHẢI ảnh trên ghế vô hình',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Ngồi Theo Góc',
    icon: '🪑',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle135',
    refAngleLabel: '135° Lưng Phải',
    tags: ['sit', '135deg', 'back_right', 'invisible_chair', 'right'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 135° Back-Right',
      loopType: 'Subtle Loop',
      keyPoints: ['Ngồi nhìn từ phía sau 3/4 hướng sang phải', 'Không hiển thị ghế', 'Tóc sau rủ tự nhiên'],
    },
    infoNote: '💡 Thích hợp cho các góc quay camera đặt từ sau lưng nhân vật đang ngồi đàm đạo lệch sang phải.',
    negativePrompt: `eyes, eyebrows, mouth, nose, visible chair, visible stool, showing face, turning left`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 135° BACK-RIGHT SITTING ON INVISIBLE CHAIR.
Use the 135° back-right reference image as LOCKED IDENTITY SOURCE.

CRITICAL: Character sitting at 135° back-right orientation (facing right edge) on an INVISIBLE chair (NO chair/furniture visible).
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
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle180',
    refAngleLabel: '180° Sau Lưng',
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
];
