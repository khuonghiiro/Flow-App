import { PromptItem } from '../../types';

const ANGLES = [
  { deg: '0', label: '0° Chính Diện', cam: 'Static Lock 0° Front View', tag: '0deg', note: 'trực diện' },
  { deg: '45', label: '45° Xoay Trái', cam: 'Static Lock 45° Left Three-Quarter View', tag: '45deg', note: 'chéo 3/4 bên trái' },
  { deg: '90', label: '90° Nhìn Ngang', cam: 'Static Lock 90° Side Profile View', tag: '90deg', note: 'nhìn ngang mặt bên' },
  { deg: '135', label: '135° Lưng Phải', cam: 'Static Lock 135° Back-Right View', tag: '135deg', note: 'lệch sau lưng phải' },
  { deg: '180', label: '180° Sau Lưng', cam: 'Static Lock 180° Direct Rear View', tag: '180deg', note: 'sau lưng hoàn toàn' },
];

function createHandActionGroup(
  baseId: string,
  titleName: string,
  actionDesc: string,
  motionTimeline: string,
  icon: string
): PromptItem[] {
  return ANGLES.map((a) => ({
    id: `${baseId}_angle${a.deg}`,
    title: `Tay: ${titleName} - ${a.label}`,
    subtitle: `${actionDesc} nhìn từ góc ${a.note} 2-3s lặp vô tận`,
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác Tay',
    icon,
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: `angle${a.deg}`,
    refAngleLabel: a.label,
    tags: ['hand', baseId, `${a.deg}deg`, 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: a.cam,
      loopType: 'Seamless Loop',
      keyPoints: [`Góc nhìn ${a.label}`, `Thân người giữ vững trục ${a.deg}°`, actionDesc],
    },
    infoNote: `💡 Động tác ${titleName} chuẩn góc nhìn ${a.label}.`,
    negativePrompt: `eyes, mouth, nose, walking, distorted hands, extra fingers, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second animation: ${titleName.toUpperCase()} (${a.label.toUpperCase()}).
📎 REFERENCE IMAGE REQUIRED: Use the ${a.deg}° body angle reference image as the START FRAME.
The AI video generator (Kling/Veo/Hailuo) MUST receive this reference image as input.

CHARACTER LOCK:
- Faceless smooth head (NO eyes/mouth/nose), locked daoist robes and hair
- Torso locked along ${a.deg}° camera perspective

MOTION PATTERN (${a.deg}° VIEW):
${motionTimeline}

SEAMLESS LOOP: Flawless loop point. Chroma green #00FF00 background.`,
  }));
}

const CLAP_PROMPTS = createHandActionGroup(
  'clap',
  'Vỗ Tay',
  'Hai tay nâng ngang ngực vỗ nhịp nhàng',
  '- Frame 0-1s: Forearms raise to chest, palms clap together rhythmically twice\n- Frame 1-2.5s: Gentle acoustic rebound, hands separate slightly and clap third time\n- Frame 2.5-3s: Returns to initial chest height resting pose\n- Sleeves flutter with air resistance on each clap impact',
  '👏'
);

const BACK_PROMPTS = createHandActionGroup(
  'back',
  'Chắp Tay Sau Lưng',
  'Hai tay chắp sau hông toát lên phong thái cao nhân',
  '- Both arms clasp gracefully behind lower back, one hand resting over wrist\n- Torso upright and dignified, gentle rhythmic breathing (chest rises/falls 1cm)\n- Wide sleeve hems drape gracefully behind hips and flutter in light mountain breeze\n- Returns seamlessly to starting pose',
  '🧘'
);

const CHIN_PROMPTS = createHandActionGroup(
  'chin',
  'Vuốt Cằm Suy Tư',
  'Một tay nâng lên vuốt nhẹ cằm suy ngẫm',
  '- Frame 0-1s: Right hand raises, index finger and thumb brush softly along chin edge\n- Frame 1-2s: Head tilts slightly in contemplation, fingers gently tap jawline\n- Frame 2-3s: Hand smoothly returns to resting thought pose\n- Left arm supports right elbow',
  '🤔'
);

const FIST_PROMPTS = createHandActionGroup(
  'fist',
  'Nắm Chặt Nắm Đấm',
  'Bàn tay siết chặt nắm đấm bộc phát ý chí',
  '- Frame 0-1s: Open palm at mid-chest slowly curls fingers into a firm tight fist\n- Frame 1-2s: Fist trembles with controlled tension and inner energy resolve\n- Frame 2-3s: Fingers relax slightly, resetting smoothly for loop',
  '✊'
);

const PALM_PROMPTS = createHandActionGroup(
  'palm',
  'Xòe Lòng Bàn Tay',
  'Bàn tay xòe ngửa lòng bàn tay đón nhận hoặc triệu hồi',
  '- Frame 0-1s: Forearm extends forward, palm rotates upward facing sky\n- Frame 1-2s: Slender curved anime fingers unfurl gracefully as if holding magic aura\n- Frame 2-3s: Hand hovers steadily, then retracts softly to starting stance',
  '🖐️'
);

const WAVE_PROMPTS = createHandActionGroup(
  'wave',
  'Vẫy Tay Chào',
  'Nâng tay ngang vai vẫy chào vui vẻ',
  '- Frame 0-0.8s: Hand raises to head level, palm facing camera\n- Frame 0.8-2.2s: Hand waves side-to-side in cheerful 30° rhythmic oscillation\n- Frame 2.2-3s: Lowers hand smoothly back to side resting posture',
  '👋'
);

export const HAND_PROMPTS: PromptItem[] = [
  ...CLAP_PROMPTS,
  ...BACK_PROMPTS,
  ...CHIN_PROMPTS,
  ...FIST_PROMPTS,
  ...PALM_PROMPTS,
  ...WAVE_PROMPTS,
  // Aliases for legacy IDs
  { ...CLAP_PROMPTS[0], id: 'hand_clap' },
  { ...BACK_PROMPTS[0], id: 'hand_behind_back' },
  { ...CHIN_PROMPTS[0], id: 'hand_stroke_chin' },
  { ...FIST_PROMPTS[0], id: 'hand_clench_fist' },
  { ...PALM_PROMPTS[0], id: 'hand_open_palm' },
  { ...WAVE_PROMPTS[0], id: 'hand_wave' },
];
