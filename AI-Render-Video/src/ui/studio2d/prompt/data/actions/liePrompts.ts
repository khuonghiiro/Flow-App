import { PromptItem } from '../../types';

export const LIE_PROMPTS: PromptItem[] = [
  {
    id: 'lie_angle0',
    title: 'Nằm - 0° Chính Diện (Lie Front - Không Giường)',
    subtitle: 'Nằm ngửa nhìn từ góc thẳng/trên xuống (HOÀN TOÀN KHÔNG HIỆN GIƯỜNG/NỆM)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nằm Theo Góc',
    icon: '🛌',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle0',
    refAngleLabel: '0° Chính Diện',
    tags: ['lie', '0deg', 'front', 'top_down', 'invisible_bed', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 0° Top-Down',
      loopType: 'Subtle Loop',
      keyPoints: ['Nằm ngửa thư thái chính diện', 'Không có giường/gối', 'Ngực phập phồng thở nhẹ'],
    },
    infoNote: '💡 Giường hoàn toàn vô hình! Dễ dàng ghép nhân vật nằm lên bãi cỏ, sàn mây hoặc giường ngủ.',
    negativePrompt: `eyes, eyebrows, mouth, nose, standing, sitting, visible bed, mattress, pillow, blanket, furniture`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 0° FRONT LYING ON BACK ON INVISIBLE SURFACE.
Use reference image as LOCKED IDENTITY SOURCE.

CRITICAL INSTRUCTION — INVISIBLE BED / NO FURNITURE:
- Character is lying flat on back facing DIRECTLY UPWARD (0° front orientation) on an INVISIBLE surface
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
    title: 'Nằm - 45° Xoay Trái (Lie 45° Left - Không Giường)',
    subtitle: 'Nằm nghiêng hoặc ngửa góc 3/4 có chiều sâu phối cảnh trên mặt phẳng vô hình',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nằm Theo Góc',
    icon: '🛌',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle45',
    refAngleLabel: '45° Nghiêng Trái',
    tags: ['lie', '45deg', 'isometric', 'perspective', 'invisible_bed', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 45° View',
      loopType: 'Subtle Loop',
      keyPoints: ['Nằm góc 3/4 chéo trái có độ nghiêng', 'Không có giường', 'Tóc xõa tự nhiên'],
    },
    infoNote: '💡 Góc 3/4 tạo cảm giác nằm nghỉ ngơi thư thái và có chiều sâu không gian tốt nhất.',
    negativePrompt: `eyes, eyebrows, mouth, nose, visible bed, pillow, sheets, standing, sitting`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 45° THREE-QUARTER LEFT LYING DOWN ON INVISIBLE SURFACE.
Use reference image as LOCKED IDENTITY SOURCE.

CRITICAL: Character lying down at 45° isometric perspective angle to the left on an INVISIBLE surface (NO bed/mattress/pillow visible).
- Pure chroma green #00FF00 surrounding
- Hair and robes draped naturally
- Subtle peaceful breathing motion
- Seamless loop`,
  },
  {
    id: 'lie_angle90',
    title: 'Nằm - 90° Side Profile (Lie Side - Không Giường)',
    subtitle: 'Nằm ngang hoàn toàn nhìn từ bên cạnh trên mặt phẳng vô hình',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nằm Theo Góc',
    icon: '🛌',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle90',
    refAngleLabel: '90° Nhìn Ngang',
    tags: ['lie', '90deg', 'side', 'profile', 'invisible_bed', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 90° Horizontal',
      loopType: 'Subtle Loop',
      keyPoints: ['Nằm ngang thẳng trục 90°', 'Không có đồ vật/giường', 'Thở nhịp nhàng'],
    },
    infoNote: '💡 Phù hợp cho các cảnh nhân vật nằm dưỡng thương hoặc nghỉ ngơi trong game màn hình ngang.',
    negativePrompt: `eyes, eyebrows, mouth, nose, visible bed, visible floor, standing, walking`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 90° SIDE VIEW LYING DOWN ON INVISIBLE SURFACE.
Use reference image as LOCKED IDENTITY SOURCE.

CRITICAL: Character lying down horizontally in pure 90° side profile on an INVISIBLE surface (NO bed visible).
- Pure solid chroma green #00FF00 background
- Subtle chest breathing motion
- Seamless loop`,
  },
  {
    id: 'lie_angle135',
    title: 'Nằm - 135° Lưng Lệch Phải (Lie 135° Back-Right - Không Giường)',
    subtitle: 'Tư thế nằm nhìn từ góc 135° chéo sau lưng/đầu xoay về CẠNH PHẢI ảnh',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nằm Theo Góc',
    icon: '🛌',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle135',
    refAngleLabel: '135° Lưng Phải',
    tags: ['lie', '135deg', 'back_right', 'invisible_bed', 'right', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 135° Back-Right',
      loopType: 'Subtle Loop',
      keyPoints: ['Nằm nhìn từ góc 3/4 phía sau hướng sang phải', 'Không hiển thị giường/gối', 'Thân người thả lỏng, thở nhẹ nhàng'],
    },
    infoNote: '💡 Giường vô hình! Góc nằm 135° nhìn từ sau lưng/đầu góc chéo, thuận tiện ghép đè lên thảm cỏ hoặc giường trong không gian 3D.',
    negativePrompt: `eyes, eyebrows, mouth, nose, standing, sitting, visible bed, mattress, pillow, blanket, furniture, floor, showing face, turning left`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 135° BACK-RIGHT VIEW LYING DOWN ON INVISIBLE SURFACE.
Use reference image as LOCKED IDENTITY SOURCE.

CRITICAL INSTRUCTION — INVISIBLE BED / NO FURNITURE:
- Character is lying down at a 135° back-right angle (facing towards right edge, viewed from behind/head quarter) on an INVISIBLE surface
- NO bed, NO mattress, NO pillow, NO blanket, NO furniture rendered
- Solid chroma-key green #00FF00 fills entire surrounding space
- Back robes draped naturally on the invisible plane, gentle rhythmic chest breathing rise and fall
- Hair spreads softly across the invisible ground plane

CONSTRAINTS:
- Strict 135° back-right perspective (facing towards right edge)
- Faceless blank head, no facial features
- Pure chroma green #00FF00 background
- 100% seamless looping`,
  },
  {
    id: 'lie_angle180',
    title: 'Nằm - 180° Sau Lưng (Lie Back View - Không Giường)',
    subtitle: 'Tư thế nằm nhìn thẳng từ phía sau/phía đỉnh đầu (HOÀN TOÀN KHÔNG HIỆN GIƯỜNG)',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Nằm Theo Góc',
    icon: '🛌',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle180',
    refAngleLabel: '180° Sau Lưng',
    tags: ['lie', '180deg', 'back', 'top_down', 'invisible_bed', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 180° Back',
      loopType: 'Subtle Loop',
      keyPoints: ['Nằm nhìn thẳng từ đỉnh đầu hoặc từ sau lưng', 'Không hiển thị giường/nệm', 'Thở nhịp nhàng'],
    },
    infoNote: '💡 Góc nằm 180° nhìn từ phía sau/đỉnh đầu, thích hợp cho các cảnh nhân vật ngất xỉu hoặc nằm dưỡng thương nhìn từ phía sau.',
    negativePrompt: `eyes, eyebrows, mouth, nose, standing, sitting, visible bed, mattress, pillow, blanket, turning around, showing face`,
    rawPrompt: `TASK: Image-to-video animation, 2-3 second seamless loop 180° REAR/HEAD VIEW LYING DOWN ON INVISIBLE SURFACE.
Use 180° back reference image as LOCKED IDENTITY SOURCE.

CRITICAL INSTRUCTION — INVISIBLE SURFACE:
- Character lying flat viewed directly from 180° (from behind/top of head looking down along the body) on an INVISIBLE surface
- NO bed, NO mattress, NO pillow, NO furniture rendered
- Solid chroma-key green #00FF00 fills all space beneath and around
- Symmetrical posture, spine aligned, hair resting on surface
- Subtle gentle breathing rise and fall

CONSTRAINTS:
- Pure 180° rear view, NO facial features
- Pure chroma green #00FF00
- Seamless loop`,
  },
];
