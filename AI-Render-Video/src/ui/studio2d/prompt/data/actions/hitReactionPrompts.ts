import { PromptItem } from '../../types';

export const HIT_REACTION_PROMPTS: PromptItem[] = [
  {
    id: 'hit_angle0',
    title: 'Bị Trúng Đòn - 0° Chính Diện (Hit Reaction / Hurt Front View)',
    subtitle: 'Nhân vật bị đòn đánh trúng trực diện, thân ngửa về sau, giật lùi 2 bước khựng lại',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '⚡',
    promptType: 'video',
    tags: ['hit', 'hurt', 'damage', 'reaction', '0deg', 'front', 'combat', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front View',
      loopType: 'Action Recovery Loop',
      keyPoints: [
        'Giật ngửa ngực đột ngột khi trúng chấn động',
        'Hai tay co giật về trước ngực để chống đỡ',
        'Trượt lùi 1-2 bước trên mặt sàn rồi lấy lại thế đứng',
      ],
    },
    infoNote: '💡 Hoạt ảnh nhận sát thương trực diện 0°, tối ưu cho game đối kháng và hoạt hình hành động.',
    negativePrompt: `eyes, mouth, nose, calm standing, attacking, weapon swing, 3D CGI, photorealism, low quality`,
    rawPrompt: `TASK: Image-to-video 2-3 second animation: DIRECT HIT REACTION / HURT STAGGER (0° FRONT VIEW).
Use the 0° front faceless reference image.

CHARACTER LOCK: Keep faceless smooth head (no eyes/mouth/nose), hairstyle, outfit, colors EXACTLY identical to reference.

ACTION PATTERN (0° FRONT VIEW HIT):
- Frame 0-0.4s: IMPACT! Chest snaps backward violently, head jolts back, shoulders tense up
- Frame 0.4-1.2s: Both feet slide backward on ground with friction, arms instinctively raise to guard chest
- Frame 1.2-2.5s: Regain balance, panting stance, weight shifts forward back into combat ready stance
- Secondary: Hair and robe sleeves violently whip backward on impact, then swing forward during recovery

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
  {
    id: 'hit_angle45',
    title: 'Bị Trúng Đòn - 45° Xoay Trái (Hit Reaction 45° Three-Quarter View)',
    subtitle: 'Trúng đòn lệch vai, cơ thể xoay giật chéo 45° sang bên trái lảo đảo chống đỡ',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '⚡',
    promptType: 'video',
    tags: ['hit', 'hurt', 'damage', '45deg', 'left', 'combat', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 45° Left View',
      loopType: 'Action Recovery Loop',
      keyPoints: [
        'Vai trái giật mạnh ra sau theo góc 45°',
        'Chân trái lùi bước dài tạo điểm tựa giữ thăng bằng',
      ],
    },
    infoNote: '💡 Góc 45° bị đánh trúng tạo cảm giác lực tác động ba chiều chân thực.',
    negativePrompt: `eyes, mouth, nose, stationary pose, smooth walk, 3D CGI, blurry`,
    rawPrompt: `TASK: Image-to-video 2-3 second animation: DIAGONAL HIT REACTION / STAGGER (45° LEFT VIEW).
Use the 45° faceless reference image.

ACTION PATTERN (45° VIEW):
- Frame 0-0.5s: Impact on front shoulder, torso twists sharply left-backward, arms flail outward
- Frame 0.5-1.5s: Stumbles back on rear foot, knees bend deeply to absorb momentum
- Frame 1.5-3s: Braces with front hand, breathes heavily, steadies posture in 45° combat stance

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
  {
    id: 'hit_angle90',
    title: 'Bị Đánh Bật Lùi - 90° Nhìn Ngang (Knockback Stagger 90° Side Profile)',
    subtitle: 'Lực đánh đẩy trượt nhân vật lùi về sau trên trục ngang 90°, thân gập cong chống đỡ',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '⚡',
    promptType: 'video',
    tags: ['hit', 'knockback', 'stagger', '90deg', 'side', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Left Side View',
      loopType: 'Action Recovery Loop',
      keyPoints: [
        'Toàn thân bị đẩy lùi sang phải (ngược chiều mặt nhìn)',
        'Gót chân ma sát lướt trên mặt đất',
      ],
    },
    infoNote: '💡 Góc 90° mô tả hoàn hảo quãng đường trượt lùi khi bị đối thủ tung đòn cực mạnh.',
    negativePrompt: `eyes, mouth, nose, forward dash, jump, 3D CGI, low quality`,
    rawPrompt: `TASK: Image-to-video 2-3 second animation: HORIZONTAL KNOCKBACK SLIDE (90° FULL SIDE VIEW).
Use the 90° side profile reference image.

ACTION PATTERN (90° SIDE VIEW):
- Frame 0-0.4s: Severe impact at stomach/chest level, torso bends backward at 30° angle
- Frame 0.4-1.2s: Both boots slide backward to the right across the ground, kicking up dust imaginary
- Frame 1.2-2.5s: Back foot digs in to stop momentum, torso recovers forward into defensive crouch

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
  {
    id: 'hit_angle135',
    title: 'Bị Đánh Lưng Chéo - 135° Lưng Phải (Hit Reaction 135° Back-Right View)',
    subtitle: 'Bị trúng đòn từ phía trước nhìn qua vai lưng lệch phải 135°, lưng cong gồng chịu lực',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '⚡',
    promptType: 'video',
    tags: ['hit', 'hurt', '135deg', 'back_right', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 135° Back-Right View',
      loopType: 'Action Recovery Loop',
      keyPoints: ['Tà áo xốc về phía người xem', 'Cột sống giật lùi qua góc nhìn lưng'],
    },
    infoNote: '💡 Góc 135° mang lại góc nhìn điện ảnh kịch tính khi quay lưng về camera.',
    negativePrompt: `face visible, front view, standing still, 3D CGI, blurry`,
    rawPrompt: `TASK: Image-to-video 2-3 second animation: OVER-SHOULDER HIT REACTION (135° BACK-RIGHT VIEW).
Use the 135° back-right reference image.

ACTION PATTERN (135° VIEW):
- Frame 0-0.5s: Upper back snaps toward camera as character absorbs frontal strike
- Frame 0.5-1.5s: Staggers backward toward right, hair flares upward, shoulders hunch
- Frame 1.5-3s: Recovers balance, right hand clenches, turns slightly back to face threat

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
  {
    id: 'hit_angle180',
    title: 'Bị Đánh Từ Sau Lưng - 180° Sau Lưng (Back Attack Reaction 180° Rear View)',
    subtitle: 'Nhân vật bị tập kích đánh trúng vào lưng từ góc 180°, gập người về phía trước',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác',
    icon: '⚡',
    promptType: 'video',
    tags: ['hit', 'back_stab', 'hurt', '180deg', 'rear', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 180° Rear View',
      loopType: 'Action Recovery Loop',
      keyPoints: [
        'Lưng bị đánh trúng, người gập cong về trước',
        'Hai tay vung ra trước ngực, chân bước vội đỡ đà',
      ],
    },
    infoNote: '💡 Động tác bị đánh lén từ sau lưng hoặc bị đẩy mạnh về phía trước.',
    negativePrompt: `face visible, front view, upright calm, 3D CGI, low quality`,
    rawPrompt: `TASK: Image-to-video 2-3 second animation: REAR STRIKE IMPACT REACTION (180° PERFECT BACK VIEW).
Use the 180° rear reference image.

ACTION PATTERN (180° VIEW):
- Frame 0-0.5s: Impact directly between shoulder blades, spine curves forward, arms fly outward
- Frame 0.5-1.5s: Stumbles 2 paces forward away from camera, head dips low
- Frame 1.5-3s: Halts momentum, straightens back slowly, turns head slightly in anger

BACKGROUND: Pure chroma-key green #00FF00.`,
  },
];
