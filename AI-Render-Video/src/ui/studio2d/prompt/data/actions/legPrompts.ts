import { PromptItem } from '../../types';

const ANGLES = [
  { deg: '0', label: '0° Chính Diện', cam: 'Static Lock 0° Front View', tag: '0deg', note: 'trực diện' },
  { deg: '45', label: '45° Xoay Trái', cam: 'Static Lock 45° Left Three-Quarter View', tag: '45deg', note: 'chéo 3/4 bên trái' },
  { deg: '90', label: '90° Nhìn Ngang', cam: 'Static Lock 90° Side Profile View', tag: '90deg', note: 'nhìn ngang mặt bên' },
  { deg: '135', label: '135° Lưng Phải', cam: 'Static Lock 135° Back-Right View', tag: '135deg', note: 'lệch sau lưng phải' },
  { deg: '180', label: '180° Sau Lưng', cam: 'Static Lock 180° Direct Rear View', tag: '180deg', note: 'sau lưng hoàn toàn' },
];

function createLegActionGroup(
  baseId: string,
  titleName: string,
  actionDesc: string,
  motionTimeline: string,
  icon: string
): PromptItem[] {
  return ANGLES.map((a) => ({
    id: `${baseId}_angle${a.deg}`,
    title: `Chân: ${titleName} - ${a.label}`,
    subtitle: `${actionDesc} nhìn từ góc ${a.note} 2-3s lặp vô tận`,
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác Chân',
    icon,
    promptType: 'video',
    tags: ['leg', baseId, `${a.deg}deg`, 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: a.cam,
      loopType: 'Seamless Loop',
      keyPoints: [`Góc nhìn ${a.label}`, `Thân người giữ vững góc ${a.deg}°`, actionDesc],
    },
    infoNote: `💡 Động tác chân ${titleName} chuẩn góc nhìn ${a.label}.`,
    negativePrompt: `eyes, mouth, nose, falling over, distorted legs, extra feet, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second animation: ${titleName.toUpperCase()} (${a.label.toUpperCase()}).
Use the ${a.deg}° faceless anime reference image as LOCKED IDENTITY SOURCE. ONLY ANIMATE LEGS & BODY.

CHARACTER LOCK:
- Faceless clean smooth head (NO eyes/mouth/nose), locked daoist robes and hair
- Camera perspective locked strictly to ${a.deg}°

MOTION PATTERN (${a.deg}° VIEW):
${motionTimeline}

SEAMLESS LOOP: Flawless loop recovery. Chroma green #00FF00 background.`,
  }));
}

const KICK_PROMPTS = createLegActionGroup(
  'kick',
  'Cú Đá Cao',
  'Trụ vững một chân, vung chân kia đá thẳng lên cao uy lực',
  '- Frame 0-0.5s: Anchor leg roots into ground, kicking knee chambers to chest\n- Frame 0.5-1.2s: Foot snaps upward reaching vertical head-height extension\n- Frame 1.2-2.2s: Recoils knee swiftly, foot lands firmly into balanced stance\n- Robe skirts whip upward dynamically following the kicking arc',
  '🦵'
);

const ROUNDHOUSE_PROMPTS = createLegActionGroup(
  'roundhouse',
  'Đá Xoay Vòng',
  'Xoay hông tung cú đá tạt ngang đẹp mắt',
  '- Frame 0-0.6s: Pivots on ball of support foot, hips whip into horizontal sweep\n- Frame 0.6-1.4s: Leg sweeps in wide circular arc, instep pointing cleanly\n- Frame 1.4-2.5s: Snaps leg back, recovers into athletic stance\n- Robe hem billows in centrifugal arc',
  '⚡'
);

const KNEEL_PROMPTS = createLegActionGroup(
  'kneel',
  'Quỳ Một Gối',
  'Hạ trọng tâm quỳ một đầu gối xuống sàn cung kính',
  '- Frame 0-1.2s: One leg steps back, knee gently touches green ground\n- Frame 1.2-2.5s: Other knee bent 90° forward, holds reverent knight posture\n- Frame 2.5-3.5s: Rises smoothly back to standing upright stance\n- Robe skirts drape gracefully around the kneeling knee',
  '🧎'
);

const SEIZA_PROMPTS = createLegActionGroup(
  'seiza',
  'Quỳ Gối Tọa Thiền',
  'Hai đầu gối khép lại quỳ trên gót chân thanh tịnh',
  '- Seated in formal seiza posture (both knees folded neatly on green floor)\n- Torso upright, hands rest flat on thighs, peaceful breathing motion\n- Robes pool smoothly around feet in neat folds\n- Seamless breathing hold loop',
  '🧘'
);

const STOMP_PROMPTS = createLegActionGroup(
  'stomp',
  'Dậm Chân Uy Lực',
  'Nâng cao bàn chân dậm mạnh xuống sàn kích phát kình lực',
  '- Frame 0-0.5s: Leg raises sharply with knee high\n- Frame 0.5-1.0s: Foot stomps downward powerfully striking the ground\n- Frame 1.0-2.0s: Micro-shockwave throughout body, cloth fringes ripple\n- Resets smoothly to ready stance',
  '💥'
);

export const LEG_PROMPTS: PromptItem[] = [
  ...KICK_PROMPTS,
  ...ROUNDHOUSE_PROMPTS,
  ...KNEEL_PROMPTS,
  ...SEIZA_PROMPTS,
  ...STOMP_PROMPTS,
  // Aliases for legacy IDs
  { ...KICK_PROMPTS[0], id: 'leg_high_kick' },
  { ...ROUNDHOUSE_PROMPTS[0], id: 'leg_roundhouse' },
  { ...KNEEL_PROMPTS[0], id: 'leg_kneel_one' },
  { ...SEIZA_PROMPTS[0], id: 'leg_kneel_seiza' },
  { ...STOMP_PROMPTS[0], id: 'leg_stomp' },
];
