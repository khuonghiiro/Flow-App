import { PromptItem } from '../../types';

export const DEFEND_PROMPTS: PromptItem[] = [
  {
    id: 'defend_angle0',
    title: 'Thủ / Đỡ Đòn - 0° Chính Diện (Guard / Defend Front View)',
    subtitle: 'Nhân vật hạ thấp trọng tâm, bắt chéo hai tay trước ngực kết ấn đỡ đòn 2-3s lặp',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '🛡️',
    promptType: 'video',
    tags: ['defend', 'guard', 'block', 'shield', '0deg', 'front', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front View',
      loopType: 'Seamless Guard Loop',
      keyPoints: [
        'Hai cánh tay bắt chéo tạo hình chữ X trước ngực',
        'Khuỵu gối hạ thấp trọng tâm kiên cố',
        'Áo và tóc lay nhẹ trong luồng khí phòng ngự',
      ],
    },
    infoNote: '💡 Hoạt ảnh thế thủ chính diện 0°, tạo thành vòng lặp đỡ đòn bất khả xâm phạm.',
    negativePrompt: `eyes, mouth, nose, relaxed arms, open arms, falling, attacking, 3D CGI, blurry`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop DEFENSIVE GUARD / BLOCKING STANCE (0° FRONT VIEW).
Use the 0° front faceless reference as LOCKED IDENTITY.

ACTION PATTERN (0° FRONT GUARD):
- Stance: Knees bent deep, feet shoulder-width apart firmly rooted to ground
- Arms: Forearms raised and crossed in front of chest (X-block guard), fists clenched tight
- Motion: Subtle breathing tension, muscles taut, holding steadfast against unseen pressure
- Secondary: Robe sleeves and hair flutter backward slightly from defensive aura push
- Loop: Start and end frames match smoothly for continuous defensive loop

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
  {
    id: 'defend_angle45',
    title: 'Thủ Nghiêng - 45° Xoay Trái (Guard / Defend 45° Three-Quarter View)',
    subtitle: 'Xoay nghiêng 45° thủ vững, tay trái đưa lên chắn phía trước, tay phải bảo vệ hạ bộ',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '🛡️',
    promptType: 'video',
    tags: ['defend', 'guard', '45deg', 'left', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 45° Left View',
      loopType: 'Seamless Guard Loop',
      keyPoints: ['Hạ trọng tâm góc 45°', 'Tay trước che đầu mặt, tay sau che sườn'],
    },
    infoNote: '💡 Thế thủ kinh điển trong võ thuật và anime phong cách tiên hiệp.',
    negativePrompt: `eyes, mouth, nose, relaxed, falling, 3D CGI, low quality`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop THREE-QUARTER DEFENSIVE STANCE (45° LEFT VIEW).
Use the 45° faceless reference.

ACTION PATTERN (45° VIEW):
- Stance: 45° deep martial arts guard, leading left knee bent, rear right leg braced
- Arms: Left forearm held high in front of face, right forearm protecting ribs/waist
- Motion: Controlled micro-adjustments, ready to deflect incoming attacks, rhythmic breathing
- Seamless loop matching beginning and end

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
  {
    id: 'defend_angle90',
    title: 'Dựng Khiên Đỡ - 90° Nhìn Ngang (Shield Block 90° Side Profile)',
    subtitle: 'Thế thủ ngang 90°, toàn bộ thân dồn lực về trước đẩy khiên chắn vô hình',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '🛡️',
    promptType: 'video',
    tags: ['defend', 'shield', 'block', '90deg', 'side', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Left Side View',
      loopType: 'Seamless Guard Loop',
      keyPoints: ['Thân gập nhẹ về phía trước đỡ lực', 'Chân sau đạp sâu giữ trụ'],
    },
    infoNote: '💡 Thích hợp để ghép khiên năng lượng hoặc kết giới phòng thủ ở mặt phẳng bên.',
    negativePrompt: `eyes, mouth, nose, standing relaxed, falling, 3D CGI, blurry`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop SIDE PROFILE BRACING GUARD (90° FULL SIDE VIEW).
Use the 90° side reference image.

ACTION PATTERN (90° SIDE VIEW):
- Stance: Leaning forward against invisible oncoming force, back leg straight, front knee deeply bent
- Arms: Both arms pushed forward holding an invisible barrier/shield against the left
- Motion: Steady resistance, minor muscular tremors absorbing tension, flowing hair streamed behind
- Seamless loop

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
  {
    id: 'defend_angle135',
    title: 'Thủ Lưng Chéo - 135° Lưng Phải (Guard 135° Back-Right View)',
    subtitle: 'Thế thủ nhìn từ lưng lệch phải 135°, cảnh giác sẵn sàng xoay người phản đòn',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '🛡️',
    promptType: 'video',
    tags: ['defend', 'guard', '135deg', 'back_right', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 135° Back-Right View',
      loopType: 'Seamless Guard Loop',
      keyPoints: ['Cảnh giác qua vai', 'Cơ lưng căng siết trong thế thủ'],
    },
    infoNote: '💡 Thế thủ quay lưng nửa người tạo cảm giác nhân vật phòng thủ bảo vệ đồng đội phía sau.',
    negativePrompt: `eyes, face front, standing straight, 3D CGI, low quality`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop DEFENSIVE STANCE (135° BACK-RIGHT VIEW).
Use the 135° back-right reference image.

ACTION PATTERN (135° VIEW):
- Stance: Guarding stance viewed from behind-right, shoulders tensed, arms raised in guard
- Motion: Subtle weight shifting between feet, back ribbons fluttering, high readiness
- Seamless loop

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
  {
    id: 'defend_angle180',
    title: 'Thủ Khóa Lưng - 180° Sau Lưng (Rear Guard 180° Back View)',
    subtitle: 'Thế thủ nhìn từ sau lưng 180°, hai tay bắt chéo thủ phía trước nhìn từ sau',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '🛡️',
    promptType: 'video',
    tags: ['defend', 'guard', '180deg', 'rear', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 180° Rear View',
      loopType: 'Seamless Guard Loop',
      keyPoints: ['Hai bả vai siết chặt', 'Trọng tâm hạ thấp vững chãi'],
    },
    infoNote: '💡 Thế thủ nhìn trọn vẹn từ sau lưng.',
    negativePrompt: `face visible, front view, standing loose, 3D CGI, low quality`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop REAR-VIEW DEFENSIVE ANCHOR (180° BACK VIEW).
Use the 180° back reference image.

ACTION PATTERN (180° VIEW):
- Stance: Symmetrical wide stance from behind, knees bent, shoulders squared firmly
- Motion: Immovable posture, subtle breathing, hair framing the defensive silhouette
- Seamless loop

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
];
