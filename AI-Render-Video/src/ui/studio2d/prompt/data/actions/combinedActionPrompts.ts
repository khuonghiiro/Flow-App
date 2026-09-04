import { PromptItem } from '../../types';

const ANGLES = [
  { deg: '0', label: '0° Chính Diện', cam: 'Static Lock 0° Front View', tag: '0deg', note: 'trực diện' },
  { deg: '45', label: '45° Xoay Trái', cam: 'Static Lock 45° Left Three-Quarter View', tag: '45deg', note: 'chéo 3/4 bên trái' },
  { deg: '90', label: '90° Nhìn Ngang', cam: 'Static Lock 90° Side Profile View', tag: '90deg', note: 'nhìn ngang mặt bên' },
  { deg: '135', label: '135° Lưng Phải', cam: 'Static Lock 135° Back-Right View', tag: '135deg', note: 'lệch sau lưng phải' },
  { deg: '180', label: '180° Sau Lưng', cam: 'Static Lock 180° Direct Rear View', tag: '180deg', note: 'sau lưng hoàn toàn' },
];

function createCombinedActionGroup(
  baseId: string,
  titleName: string,
  actionDesc: string,
  motionTimeline: string,
  icon: string,
  noToolConstraint = false
): PromptItem[] {
  return ANGLES.map((a) => ({
    id: `${baseId}_angle${a.deg}`,
    title: `Kết Hợp: ${titleName} - ${a.label}`,
    subtitle: `${actionDesc} nhìn từ góc ${a.note} 2-3s lặp vô tận`,
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Động Tác Kết Hợp',
    icon,
    promptType: 'video',
    generationMode: 'image_to_video',
    aspectRatio: '9:16',
    refAngleImageId: `angle${a.deg}`,
    refAngleLabel: a.label,
    tags: ['combined', baseId, `${a.deg}deg`, 'video'],
    videoGuide: {
      duration: '2.5 - 3 giây',
      fps: '24 / 30 FPS',
      camera: a.cam,
      loopType: 'Seamless Loop',
      keyPoints: [
        `Góc nhìn ${a.label}`,
        `Cơ thể chuyển động toàn thân trên trục ${a.deg}°`,
        noToolConstraint ? 'KHÔNG render dụng cụ/vũ khí (chỉ có tư thế tay)' : actionDesc,
      ],
    },
    infoNote: `💡 Động tác kết hợp ${titleName} chuẩn góc nhìn ${a.label}.`,
    negativePrompt: noToolConstraint
      ? `eyes, mouth, nose, tool, weapon, shovel, hoe, axe, stick, wood block, 3D CGI`
      : `eyes, mouth, nose, head hitting ground, falling, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2.5-3 second animation: ${titleName.toUpperCase()} (${a.label.toUpperCase()}).
Use the ${a.deg}° faceless anime reference image as LOCKED IDENTITY SOURCE.

CHARACTER LOCK:
- Faceless clean smooth head (NO eyes/mouth/nose), locked daoist robes and hair
- Camera perspective locked strictly to ${a.deg}°
${noToolConstraint ? '- IMPORTANT CONSTRAINT: DO NOT RENDER ANY TOOLS/WEAPONS. Hands hold imaginary tool shaft.' : ''}

MOTION PATTERN (${a.deg}° VIEW):
${motionTimeline}

SEAMLESS LOOP: Flawless loop recovery. Chroma green #00FF00 background.`,
  }));
}

const BOW_PROMPTS = createCombinedActionGroup(
  'bow',
  'Chắp Tay Vái Chào',
  'Hai tay chắp trước ngực cúi gập người cung kính',
  '- Frame 0-0.8s: Hands clasp together at mid-chest in martial salute\n- Frame 0.8-1.8s: Upper torso and head bow smoothly ~35° in deep respect\n- Frame 1.8-2.8s: Straightens upright, releasing hands to sides\n- Flowing sleeves drape downwards during bow',
  '🙇',
  false
);

const HOE_PROMPTS = createCombinedActionGroup(
  'hoe',
  'Cuốc Đất Thể Nghiệm (Không Dụng Cụ)',
  'Tư thế hai tay cầm chuôi vô hình vung lên bổ xuống cuốc đất',
  '- Staggered stance, hands grasp an invisible shaft\n- Frame 0-0.8s: Torso twists slightly back, lifting hands to shoulder height\n- Frame 0.8-1.6s: Powerful forward flexion at waist, driving hands down to floor level\n- Frame 1.6-2.5s: Rebounds smoothly to reset position\n- Sleeves pull taut and flutter on downstroke',
  '⛏️',
  true
);

const CHOP_PROMPTS = createCombinedActionGroup(
  'chop',
  'Chẻ Củi Bổ Rìu (Không Dụng Cụ)',
  'Hai tay nâng cao quá đầu dồn lực bổ thẳng xuống chẻ củi',
  '- Feet shoulder-width apart\n- Frame 0-0.9s: Both hands raise high overhead, spine extends back in tension\n- Frame 0.9-1.5s: Explosive downward stroke, knees flex deeper, driving hands down\n- Frame 1.5-2.5s: Recoil pause, raises arms smoothly back overhead\n- Sleeves and hair whip upward on downward snap',
  '🪵',
  true
);

const MEDITATE_PROMPTS = createCombinedActionGroup(
  'meditate',
  'Vận Công Đả Tọa',
  'Ngồi xếp bằng hoa sen, hai tay kết ấn điều hòa linh khí',
  '- Seated in cross-legged lotus position\n- Hands in meditation mudra before lower dantian\n- Frame 0-1.5s: Hands gently float up 5cm with deep inhalation\n- Frame 1.5-3.0s: Hands descend softly with calm exhalation\n- Ambient levitation micro-vibration of robes',
  '✨',
  false
);

export const COMBINED_ACTION_PROMPTS: PromptItem[] = [
  ...BOW_PROMPTS,
  ...HOE_PROMPTS,
  ...CHOP_PROMPTS,
  ...MEDITATE_PROMPTS,
  // Aliases for legacy IDs
  { ...BOW_PROMPTS[0], id: 'action_bow_salute' },
  { ...HOE_PROMPTS[0], id: 'action_hoe_soil' },
  { ...CHOP_PROMPTS[0], id: 'action_chop_wood' },
  { ...MEDITATE_PROMPTS[0], id: 'action_meditate_channel' },
];
