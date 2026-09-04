import { PromptItem } from '../../types';

export const ATTACK_PROMPTS: PromptItem[] = [
  {
    id: 'attack_angle0',
    title: 'Đánh Công - 0° Chính Diện (Attack Front View)',
    subtitle: 'Vung đòn chém/chưởng lực thẳng về phía trước đối diện camera',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Tấn Công Theo Góc',
    icon: '⚔️',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle0',
    refAngleLabel: '0° Chính Diện',
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
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle45',
    refAngleLabel: '45° Nghiêng Trái',
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
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle90',
    refAngleLabel: '90° Nhìn Ngang',
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
    id: 'attack_angle135',
    title: 'Đánh Công - 135° Lưng Lệch Phải (Attack 135° Back-Right)',
    subtitle: 'Vung đòn chém/phát lực nhìn từ sau lưng lệch xoay về CẠNH PHẢI ảnh',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Tấn Công Theo Góc',
    icon: '⚔️',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle135',
    refAngleLabel: '135° Lưng Phải',
    tags: ['attack', '135deg', 'back_right', 'combat', 'slash', 'right'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 135° Back-Right',
      loopType: 'Combat Loop',
      keyPoints: ['Vung đòn chém chéo hướng sang phải nhìn từ sau', 'Thân sau xoay phát lực mạnh mẽ', 'Tà áo và tóc tung bay theo lực chém'],
    },
    infoNote: '💡 Đánh công góc 135° nhìn từ phía sau hướng sang phải, tái hiện góc nhìn của camera theo sau lưng nhân vật (Over-the-shoulder).',
    negativePrompt: `eyes, eyebrows, mouth, nose, turning to front, showing face, turning left, distorted limbs, falling over`,
    rawPrompt: `TASK: Image-to-video animation, 1.5-2 second seamless loop 135° BACK-RIGHT COMBAT ATTACK.
Use 135° back-right reference image as LOCKED IDENTITY SOURCE.

MOTION (135° BACK-RIGHT ATTACK):
- Dynamic combat strike angled towards upper-right (facing right edge) viewed from behind-right (135°)
- Torso coils and unleashes powerful slash/strike with invisible weapon forward-right
- Back muscles and robes twist dynamically to show kinetic force
- Hair and sleeves whip dramatically with the strike momentum
- Crisp martial recovery returning to ready stance

CONSTRAINTS:
- Strict 135° back-right angle (facing right edge, NO face visible)
- Chroma green #00FF00 background
- Seamless loop`,
  },
  {
    id: 'attack_angle180',
    title: 'Đánh Công - 180° Sau Lưng (Attack Back View)',
    subtitle: 'Vung đòn chém/phát lực nhìn từ phía sau lưng',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Tấn Công Theo Góc',
    icon: '⚔️',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle180',
    refAngleLabel: '180° Sau Lưng',
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
];
