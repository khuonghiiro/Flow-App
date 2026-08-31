import { PromptItem } from '../types';

export const WALK_PROMPTS: PromptItem[] = [
  {
    id: 'walk_general',
    title: 'Đi Bộ Tổng Quát (Walk Cycle Standard)',
    subtitle: 'Prompt tạo video lặp vô tận (Seamless Loop 3-4s) cho hoạt ảnh đi bộ',
    stepCategory: 'step2_walk',
    stepLabel: 'Bước 2: Đi Bộ',
    icon: '🚶',
    promptType: 'video',
    tags: ['walk', 'loop', 'animation', 'video', 'movement', 'step'],
    videoGuide: {
      duration: '3 - 4 giây',
      fps: '24 hoặc 30 FPS',
      camera: 'Static Lock (Khóa tĩnh tuyệt đối)',
      loopType: 'Seamless Loop (Khung hình 1 khớp chính xác khung hình cuối)',
      keyPoints: [
        'Nhân vật bước đi tại chỗ (In-place walk, không trượt khỏi màn hình)',
        'Chỉ có chân và tay đánh nhịp nhịp nhàng',
        'Thân trên và đầu giữ cố định, không lắc lư quá đà',
        'Tà áo và tóc chuyển động nhấp nhô theo nhịp chân',
      ],
    },
    infoNote: '💡 Tạo video hoạt ảnh 3-4 giây lặp vô tận. Sau đó đưa vào Tab 1.2 hoặc 1.3 để trích xuất từng frame thành Sprite Sheet!',
    negativePrompt: `eyes, eyebrows, mouth, nose, facial expression, body rotation, body twisting, turning, body sway, hip movement, body lean, head bob, exaggerated bounce, upper body movement, sideways stepping, strafing, circular movement, dancing, jumping, extra gestures, unnatural movement, imagined details, added props`,
    rawPrompt: `TASK: Image-to-video animation, 3-4 second seamless loop WALK CYCLE.
Use the faceless reference image as LOCKED IDENTITY SOURCE.

CHARACTER LOCK: Keep faceless smooth head (no eyes/mouth/nose), hairstyle, hair color, skin tone, body
proportions, costume, colors, accessories EXACTLY as in reference.
No new elements, no redesign, no style drift.

CRITICAL CONSTRAINT — NO BODY ROTATION:
- Character body stays at same angle throughout entire video
- ONLY legs move (stepping forward in the direction they face)
- ONLY arms swing naturally with legs
- Head stays still
- Torso stays straight up

WALK PATTERN:
- Walking in place (no moving on screen)
- Left leg steps forward → right leg steps forward (repeat 2 cycles)
- Even, controlled steps, natural timing
- Knees bend naturally when stepping
- Arms swing forward-backward in sync with opposite leg

SECONDARY MOTION:
- Hair: small natural sway with each step
- Bangs: move together with hair, stay visible
- Robe/skirt: ripples slightly with leg movement
- Ribbons/jewelry: minimal sway in sync with walking rhythm
- NO hip swaying, NO dancing, NO exaggerated motion

ANIMATION STYLE:
- Realistic 2D walk cycle (not exaggerated bounce)
- Natural timing, clear foot placement
- Smooth, not floaty or stiff
- Consistent speed throughout loop

SEAMLESS LOOP:
- Frame 1 must match final frame exactly
- Same leg position at start and end
- No jump cut, no stutter

BACKGROUND: Flat chroma-key green (#00FF00), unchanged.

CAMERA: Completely static.`,
  },
  {
    id: 'walk_angle0',
    title: 'Đi Bộ - 0° Chính Diện (Walk Front View)',
    subtitle: 'Hoạt ảnh đi bộ tiến thẳng về phía trước đối diện camera',
    stepCategory: 'step2_walk',
    stepLabel: 'Bước 2: Đi Bộ Theo Góc',
    icon: '⬇️',
    promptType: 'video',
    tags: ['walk', '0deg', 'front', 'loop', 'in-place'],
    videoGuide: {
      duration: '3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Seamless Loop',
      keyPoints: ['Hai chân bước tới trước so le', 'Bàn chân chạm đất nhịp nhàng', 'Góc nhìn 0° trực diện không xoay góc'],
    },
    infoNote: '💡 Dùng ảnh tham chiếu góc 0° để render video đi bộ hướng về phía camera.',
    negativePrompt: `eyes, eyebrows, mouth, nose, side stepping, turning around, changing camera angle, tilting, jumping, running, background moving, disappearing limbs`,
    rawPrompt: `TASK: Image-to-video animation, 3 second seamless loop FRONT VIEW WALK CYCLE (0°).
Use the 0° front view reference image as LOCKED IDENTITY SOURCE.

MOTION SPECIFICATIONS (0° FRONT WALK):
- Character walks in place facing DIRECTLY at camera
- Left foot steps forward and down → right foot steps forward and down
- Symmetrical alternating leg movement with realistic forward perspective
- Arms swing naturally forward and back at sides
- Torso remains upright with subtle vertical breathing bounce
- Head stays centered facing camera (faceless smooth canvas)
- Robes and long hair sway gently in sync with foot strikes

CRITICAL CONSTRAINTS:
- Do NOT rotate body or turn away from 0° front view
- Do NOT slide across screen (pure walk in place)
- Seamless 100% loop: first frame matches last frame seamlessly
- Background stays solid chroma green #00FF00 throughout

CAMERA: Completely locked static camera.`,
  },
  {
    id: 'walk_angle45',
    title: 'Đi Bộ - 45° Xoay Trái (Walk 45° Left)',
    subtitle: 'Hoạt ảnh đi bộ góc chéo 3/4 trái (Góc nhìn phổ biến nhất)',
    stepCategory: 'step2_walk',
    stepLabel: 'Bước 2: Đi Bộ Theo Góc',
    icon: '↙️',
    promptType: 'video',
    tags: ['walk', '45deg', 'isometric', 'loop', 'three_quarter'],
    videoGuide: {
      duration: '3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 45° Left',
      loopType: 'Seamless Loop',
      keyPoints: ['Chân trái và chân phải sải bước chéo 45°', 'Tay đánh nhịp đối xứng', 'Tà áo bay nhẹ về sau'],
    },
    infoNote: '💡 Góc 45° Sâu (Deep Isometric): Góc tối ưu nhất cho game nhập vai 2.5D Isometric và di chuyển cảnh quan.',
    negativePrompt: `shallow angle, 20 degree angle, 30 degree angle, almost front view, rotating to 90 degrees, turning to front, eyes, eyebrows, mouth, nose, swaying hips wildly, flying, sliding horizontally`,
    rawPrompt: `TASK: Image-to-video animation, 3 second seamless loop DEEP 45° THREE-QUARTER LEFT WALK CYCLE.
Use the 45° left reference image as LOCKED IDENTITY SOURCE.

MOTION SPECIFICATIONS (DEEP 45° LEFT WALK):
- Character walks in place maintaining constant DEEP 45-degree isometric angle to the left (halfway between front and profile)
- Left leg (closer to camera) and right leg (farther) step forward smoothly in alternating diagonal stride
- Clear knee bend and foot roll on each step
- Arms swing forward and backward in natural counter-motion to legs
- Fabric, sleeves, and ribbons trail slightly backward as legs push forward
- Hair sways gently in rhythm with steps

CRITICAL CONSTRAINTS:
- Keep true 45° angle strict from frame 1 to final frame (NO shallow angle drift to 20°-30°)
- Walking in place (in-situ loop)
- Perfect loop matching start and end frames
- Pure chroma-key green #00FF00 background

CAMERA: Locked static perspective.`,
  },
  {
    id: 'walk_angle90',
    title: 'Đi Bộ - 90° Side Profile (Walk Side Left)',
    subtitle: 'Hoạt ảnh đi bộ nhìn ngang toàn phần (Side-scroller Walk)',
    stepCategory: 'step2_walk',
    stepLabel: 'Bước 2: Đi Bộ Theo Góc',
    icon: '⬅️',
    promptType: 'video',
    tags: ['walk', '90deg', 'side', 'profile', 'platformer'],
    videoGuide: {
      duration: '3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Profile',
      loopType: 'Seamless Loop',
      keyPoints: ['Biên độ sải chân rõ ràng nhất ở góc 90°', 'Tay vung trước sau tối đa', 'Đầu giữ thẳng nhìn sang trái'],
    },
    infoNote: '💡 Góc 90° Profile cho thấy toàn bộ chu kỳ Walk Cycle: Contact -> Down -> Pass -> Up -> Contact.',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning towards camera, turning to back, erratic bouncing, floating feet, background changing`,
    rawPrompt: `TASK: Image-to-video animation, 3 second seamless loop 90° FULL SIDE PROFILE WALK CYCLE.
Use the 90° side profile reference image as LOCKED IDENTITY SOURCE.

MOTION SPECIFICATIONS (90° SIDE PROFILE):
- Full 2D side view walking cycle moving leftward in place
- Left leg steps forward → heel strike → foot rolls flat → pushes back → right leg swings forward
- Clear contact, down, passing, and up phases of classic animation walk cycle
- Front arm and back arm swing with clear arcs opposite to leg strides
- Torso maintains slight natural forward lean of walking
- Long hair and robe hems trail slightly behind movement vector

CRITICAL CONSTRAINTS:
- Strictly remain 90° side profile without showing 3/4 or front features
- In-place walk cycle (zero screen translation)
- Seamless looping at boundary frames
- Clean chroma green #00FF00 background

CAMERA: Static orthographic side lock.`,
  },
  {
    id: 'walk_angle135',
    title: 'Đi Bộ - 135° Lưng Lệch Phải (Walk 135° Back-Right)',
    subtitle: 'Hoạt ảnh đi bộ nhìn từ phía sau lưng lệch xoay về CẠNH PHẢI ảnh',
    stepCategory: 'step2_walk',
    stepLabel: 'Bước 2: Đi Bộ Theo Góc',
    icon: '↘️',
    promptType: 'video',
    tags: ['walk', '135deg', 'back_right', 'loop', 'right'],
    videoGuide: {
      duration: '3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 135° Back-Right',
      loopType: 'Seamless Loop',
      keyPoints: ['Bóng lưng và tóc bay nhẹ', 'Gót chân nhấc lên từ phía sau hướng về phải', 'Giữ nguyên góc xoay 135°'],
    },
    infoNote: '💡 Dùng cho các cảnh nhân vật đi dần ra xa về phía bên phải bản đồ.',
    negativePrompt: `turning to full back 180, turning to side 90, showing face, drifting off center, turning left`,
    rawPrompt: `TASK: Image-to-video animation, 3 second seamless loop 135° BACK-RIGHT WALK CYCLE.
Use the 135° back-right reference image as LOCKED IDENTITY SOURCE.

MOTION SPECIFICATIONS (135° BACK-RIGHT):
- Character walks in place angled away from camera towards upper-right (facing right edge)
- Back of character visible, right shoulder and right leg prominent
- Heel lift and leg push-off visible from behind in alternating rhythm
- Back ribbons, hair, and sash sway softly with torso weight shifts
- Character strides steadily without wandering

CRITICAL CONSTRAINTS:
- Maintain 135° back-right orientation throughout (facing towards right edge)
- In-place walk cycle
- Flawless seamless loop
- Chroma green #00FF00 background

CAMERA: Static lock.`,
  },
  {
    id: 'walk_angle180',
    title: 'Đi Bộ - 180° Sau Lưng (Walk Back View)',
    subtitle: 'Hoạt ảnh đi bộ nhìn thẳng từ phía sau lưng (Đi vào chiều sâu)',
    stepCategory: 'step2_walk',
    stepLabel: 'Bước 2: Đi Bộ Theo Góc',
    icon: '⬆️',
    promptType: 'video',
    tags: ['walk', '180deg', 'back', 'loop'],
    videoGuide: {
      duration: '3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 180° Back',
      loopType: 'Seamless Loop',
      keyPoints: ['Hai chân sải so le nhìn từ sau', 'Tóc dài và tà áo sau đung đưa nhịp nhàng', 'Không quay đầu lại'],
    },
    infoNote: '💡 Hoàn hảo cho cảnh nhân vật quay lưng bước đi hoặc đi vào cổng tiên môn, hẻm núi.',
    negativePrompt: `turning around, head turning to face camera, asymmetrical limping, tilting shoulders`,
    rawPrompt: `TASK: Image-to-video animation, 3 second seamless loop 180° BACK VIEW WALK CYCLE.
Use the 180° back view reference image as LOCKED IDENTITY SOURCE.

MOTION SPECIFICATIONS (180° BACK VIEW):
- Character walks in place facing DIRECTLY AWAY from camera (180°)
- Alternating stepping motion viewed from behind
- Left leg steps forward → right leg steps forward in balanced symmetry
- Hair and back robes sway left and right in harmonic synchronization with footsteps
- Spine and head stay upright and centered

CRITICAL CONSTRAINTS:
- Strictly 180° rear view, NO turning or showing face
- Walking in place on green screen #00FF00
- 100% seamless looping

CAMERA: Static locked rear view.`,
  },
];
