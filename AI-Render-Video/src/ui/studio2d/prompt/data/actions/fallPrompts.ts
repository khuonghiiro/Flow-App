import { PromptItem } from '../../types';

export const FALL_PROMPTS: PromptItem[] = [
  {
    id: 'fall_angle0',
    title: 'Ngã / Đổ Sụp - 0° Chính Diện (Knockdown / Fall Front View)',
    subtitle: 'Nhân vật mất đà trượt chân ngã sụp về phía trước, tiếp đất chấn động 3-4s lặp',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '💥',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle0',
    refAngleLabel: '0° Chính Diện',
    tags: ['fall', 'knockdown', 'tumble', 'collapse', '0deg', 'front', 'video'],
    videoGuide: {
      duration: '3 - 4 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front View',
      loopType: 'Action Loop with Recovery or Impact Freeze',
      keyPoints: [
        'Mất thăng bằng, đầu gối khụy xuống trước',
        'Cả người đổ sụp về phía trước, hai tay chạm đất chống đỡ',
        'Tà áo và tóc bay xốc lên rồi buông rơi theo quán tính',
      ],
    },
    infoNote: '💡 Hoạt ảnh nhân vật ngã quỵ hoặc trượt ngã trực diện đối diện camera.',
    negativePrompt: `eyes, eyebrows, mouth, nose, facial expression, smooth flying, floating, upright standing, stable pose, 3D CGI rendering, photorealism, low quality`,
    rawPrompt: `TASK: Image-to-video 3-4 second animation: FALL / KNOCKDOWN FORWARD COLLAPSE (0° DIRECT FRONT VIEW).
Use the 0° front faceless reference image as LOCKED IDENTITY SOURCE.

CHARACTER LOCK: Keep faceless smooth head (no eyes/mouth/nose), hairstyle, hair color, skin tone, body proportions, costume, colors EXACTLY as in reference.

ACTION PATTERN (0° FRONT VIEW FALL):
- Frame 0-1s: Sudden loss of balance, knees buckle inward, torso drops rapidly downward
- Frame 1-2.5s: Impact ground forward, palms slap the green floor to cushion impact, robe hems spread out dynamically
- Frame 2.5-4s: Settle onto knees and palms, breathing heavily, head hung low facing ground
- Secondary: Hair flies upward on impact and settles, robe ribbons flutter and drop

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
  {
    id: 'fall_angle45',
    title: 'Ngã Trượt - 45° Xoay Trái (Fall / Knockdown 45° Three-Quarter View)',
    subtitle: 'Nhân vật ngã trượt chéo 45° sang bên trái, lảo đảo rồi đổ người chạm sàn',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '💥',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle45',
    refAngleLabel: '45° Nghiêng Trái',
    tags: ['fall', 'knockdown', 'stumble', '45deg', 'left', 'video'],
    videoGuide: {
      duration: '3 - 4 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 45° Left View',
      loopType: 'Impact Action Loop',
      keyPoints: [
        'Người nghiêng chéo 45° sang trái khi ngã',
        'Tay trái vung ra đỡ sàn, chân phải trượt duỗi về sau',
      ],
    },
    infoNote: '💡 Hoạt ảnh ngã theo góc nghiêng 45° rất hay dùng trong các cảnh chiến đấu anime.',
    negativePrompt: `eyes, mouth, nose, standing still, levitation, rotating camera, 3D CGI, blurry`,
    rawPrompt: `TASK: Image-to-video 3-4 second animation: DIAGONAL FALL / SLIDE KNOCKDOWN (45° LEFT THREE-QUARTER VIEW).
Use the 45° faceless reference as LOCKED IDENTITY.

ACTION PATTERN (45° VIEW):
- Frame 0-1s: Stumble diagonally backward-left, arms swing to regain balance but fail
- Frame 1-2.5s: Hips and knees hit the ground at 45° angle, left hand posts on floor, right arm covers torso
- Frame 2.5-4s: Slide slightly on floor, robe folds gather, hair swings forward across shoulder

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
  {
    id: 'fall_angle90',
    title: 'Ngã Ngửa / Ngã Lăn - 90° Nhìn Ngang (Knockdown Fall 90° Side Profile)',
    subtitle: 'Nhân vật bị đẩy ngã ngửa ra sau hoặc chúi đầu về trước góc ngang 90°',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '💥',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle90',
    refAngleLabel: '90° Nhìn Ngang',
    tags: ['fall', 'knockdown', 'side', '90deg', 'video'],
    videoGuide: {
      duration: '3 - 4 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Left Side View',
      loopType: 'Action Loop',
      keyPoints: [
        'Chuyển động từ đứng sang nằm ngang rõ rệt trên trục ngang',
        'Lưng hoặc bụng tiếp đất, nảy nhẹ một nhịp quán tính',
      ],
    },
    infoNote: '💡 Góc 90° thể hiện rõ toàn bộ đường cong cơ thể khi ngã đập đất.',
    negativePrompt: `eyes, mouth, nose, standing upright, flying, 3D CGI, photorealism`,
    rawPrompt: `TASK: Image-to-video 3-4 second animation: SIDE PROFILE KNOCKDOWN FALL (90° FULL SIDE VIEW).
Use the 90° side profile reference image.

ACTION PATTERN (90° SIDE VIEW):
- Frame 0-1s: Body tilted backward sharply from standing, legs sweep out from under
- Frame 1-2s: Back and shoulders crash into the ground horizontally, elbows bracing
- Frame 2-4s: Small bounce from impact, legs relax flat on floor, long back hair spreads out underneath

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
  {
    id: 'fall_angle135',
    title: 'Ngã Lưng Chéo - 135° Lưng Phải (Fall 135° Back-Right View)',
    subtitle: 'Nhân vật ngã gục từ góc lưng lệch phải 135°, lưng tiếp đất',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '💥',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle135',
    refAngleLabel: '135° Lưng Phải',
    tags: ['fall', 'knockdown', '135deg', 'back_right', 'video'],
    videoGuide: {
      duration: '3 - 4 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 135° Back-Right View',
      loopType: 'Action Loop',
      keyPoints: ['Lưng nghiêng tiếp đất', 'Áo choàng xòe rộng về phía sau'],
    },
    infoNote: '💡 Góc 135° ngã cho phép quan sát chi tiết chuyển động của tà áo và tóc sau lưng.',
    negativePrompt: `eyes, face visible, front view, standing still, 3D CGI, low quality`,
    rawPrompt: `TASK: Image-to-video 3-4 second animation: REAR-ANGLE FALL / KNOCKDOWN (135° BACK-RIGHT VIEW).
Use the 135° back-right reference image.

ACTION PATTERN (135° VIEW):
- Frame 0-1.5s: Kneeling failure, collapsing backward towards right camera edge
- Frame 1.5-3s: Back of torso lands, hair whips across shoulders, robes billow outwards
- Frame 3-4s: Stillness on ground, gentle settling of floating sleeves

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
  {
    id: 'fall_angle180',
    title: 'Ngã Đổ Sau Lưng - 180° Sau Lưng (Fall 180° Rear View)',
    subtitle: 'Nhân vật đổ gục hoàn toàn nhìn từ sau lưng 180°, gối khụy người gập xuống',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '💥',
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: 'angle180',
    refAngleLabel: '180° Sau Lưng',
    tags: ['fall', 'collapse', '180deg', 'rear', 'video'],
    videoGuide: {
      duration: '3 - 4 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 180° Rear View',
      loopType: 'Action Loop',
      keyPoints: ['Cột sống uốn gập khi sụp xuống', 'Tóc xõa phủ trọn lưng'],
    },
    infoNote: '💡 Góc 180° thể hiện sự thất bại / kiệt sức khi bị đánh từ phía trước nhìn từ sau.',
    negativePrompt: `face visible, front view, standing, 3D CGI, low quality`,
    rawPrompt: `TASK: Image-to-video 3-4 second animation: DIRECT REAR COLLAPSE / FALL (180° BACK VIEW).
Use the 180° rear reference image.

ACTION PATTERN (180° VIEW):
- Frame 0-1.5s: Spine bends forward as knees hit floor, symmetrical drop from behind
- Frame 1.5-3s: Torso folds flat to ground away from camera, arms spread out at sides
- Frame 3-4s: Final resting pose on stomach/knees, back hair cascades over shoulders

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
];
