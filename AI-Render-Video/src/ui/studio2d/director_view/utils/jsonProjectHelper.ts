// JSON Project Exporter, Importer & Default Multi-Angle Project Templates with Cinematic Art & Props
import { Director2DProject, StandardHorizontalAngle, STANDARD_8_ANGLES, TOP_DOWN_ANGLES, LayerPartConfig, ScenePropItem } from '../../../../types/studio2d_director';

/**
 * Creates High-Quality Illustrated SVG for a Character at Specific Angles (Tu-Tien Donghua Style)
 */
export function generateAngleSvgSample(charName: string, color: string, angle: StandardHorizontalAngle): string {
  const isTopDown = angle.startsWith('top_down');
  const isBack = angle === 'back' || angle === 'back_three_quarter_left' || angle === 'back_three_quarter_right' || angle === 'top_down_back';
  const isSide = angle === 'profile_left' || angle === 'profile_right' || angle === 'top_down_profile_left' || angle === 'top_down_profile_right';
  const isThreeQuarter = angle.includes('three_quarter');

  const isTieuDao = charName.includes('Tiêu Dao') || color.includes('0284c7') || color.includes('38bdf8');
  const robeColor1 = isTieuDao ? '#0284c7' : '#991b1b';
  const robeColor2 = isTieuDao ? '#0369a1' : '#450a0a';
  const auraColor = isTieuDao ? '#38bdf8' : '#ec4899';
  const swordColor = isTieuDao ? '#67e8f9' : '#f43f5e';

  if (isTopDown) {
    const crownX = isSide ? 68 : isThreeQuarter ? 64 : 60;
    const crownY = isBack ? 75 : 65;

    const svg = `
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 140 200" width="140" height="200">
        <defs>
          <radialGradient id="auraTop_${color.replace('#','')}" cx="50%" cy="50%" r="50%">
            <stop offset="0%" stop-color="${auraColor}" stop-opacity="0.5"/>
            <stop offset="100%" stop-color="${auraColor}" stop-opacity="0"/>
          </radialGradient>
          <linearGradient id="robeTopGrad_${color.replace('#','')}" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stop-color="${robeColor1}"/>
            <stop offset="100%" stop-color="${robeColor2}"/>
          </linearGradient>
        </defs>
        <ellipse cx="70" cy="110" rx="65" ry="40" fill="url(#auraTop_${color.replace('#','')})"/>
        <ellipse cx="70" cy="160" rx="55" ry="18" fill="rgba(0,0,0,0.5)"/>
        <ellipse cx="70" cy="115" rx="56" ry="32" fill="url(#robeTopGrad_${color.replace('#','')})" stroke="#0f172a" stroke-width="2.5"/>
        <path d="M45 115 Q70 145 95 115" stroke="#f59e0b" stroke-width="3.5" fill="none"/>
        <path d="M50 120 Q70 150 90 120" stroke="#fef08a" stroke-width="1.5" fill="none"/>
        <path d="M30 120 Q70 170 110 120 Q70 185 30 120 Z" fill="${robeColor2}" opacity="0.8"/>
        <ellipse cx="${crownX}" cy="${crownY}" rx="26" ry="28" fill="#0f172a" stroke="#020617" stroke-width="2"/>
        <circle cx="${crownX}" cy="${crownY - 6}" r="14" fill="#020617"/>
        <circle cx="${crownX}" cy="${crownY - 6}" r="6" fill="#f59e0b"/>
        <line x1="${crownX - 22}" y1="${crownY - 6}" x2="${crownX + 22}" y2="${crownY - 6}" stroke="#67e8f9" stroke-width="3.5" stroke-linecap="round"/>
        <rect x="20" y="80" width="10" height="48" rx="4" fill="${swordColor}" stroke="#fff" stroke-width="1"/>
        <circle cx="25" cy="75" r="6" fill="#fbbf24"/>
        <rect x="15" y="4" width="110" height="18" rx="4" fill="rgba(15,23,42,0.9)" stroke="${auraColor}" stroke-width="1"/>
        <text x="70" y="16" font-size="9" font-family="sans-serif" font-weight="bold" fill="#facc15" text-anchor="middle">👑 ĐỈNH ĐẦU ${angle.replace('top_down_', '').toUpperCase()}</text>
      </svg>
    `.trim();
    return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
  }

  // Horizontal Angles
  const faceX = isSide ? (angle === 'profile_left' ? 58 : 42) : isThreeQuarter ? (angle.includes('left') ? 54 : 46) : 50;
  const eyeLeft = isSide ? '' : `<ellipse cx="${faceX - 7}" cy="48" rx="3.5" ry="4.5" fill="#0f172a"/><circle cx="${faceX - 6}" cy="46" r="1.5" fill="#fff"/><ellipse cx="${faceX - 7}" cy="50" rx="2" ry="1" fill="${auraColor}"/>`;
  const eyeRight = `<ellipse cx="${faceX + 7}" cy="48" rx="3.5" ry="4.5" fill="#0f172a"/><circle cx="${faceX + 8}" cy="46" r="1.5" fill="#fff"/><ellipse cx="${faceX + 7}" cy="50" rx="2" ry="1" fill="${auraColor}"/>`;
  const brows = isSide ? `<line x1="${faceX + 3}" y1="42" x2="${faceX + 11}" y2="40" stroke="#0f172a" stroke-width="2"/>` : `<line x1="${faceX - 11}" y1="41" x2="${faceX - 3}" y2="43" stroke="#0f172a" stroke-width="2"/><line x1="${faceX + 3}" y1="43" x2="${faceX + 11}" y2="41" stroke="#0f172a" stroke-width="2"/>`;
  const mouth = isBack ? '' : `<path d="M${faceX - 4} 58 Q${faceX} 62 ${faceX + 4} 58" stroke="#991b1b" stroke-width="2" fill="none"/>`;

  const robePath = isSide
    ? 'M38 75 Q28 130 22 190 L85 190 Q80 130 68 75 Z'
    : 'M32 75 Q12 130 18 190 L88 190 Q94 130 74 75 Z';

  const swordFront = !isBack ? `
    <g filter="drop-shadow(0 0 6px ${swordColor})">
      <line x1="18" y1="35" x2="82" y2="185" stroke="${swordColor}" stroke-width="4.5" stroke-linecap="round"/>
      <line x1="18" y1="35" x2="82" y2="185" stroke="#ffffff" stroke-width="2" stroke-linecap="round"/>
      <circle cx="18" cy="35" r="5" fill="#fbbf24"/>
      <line x1="12" y1="40" x2="24" y2="30" stroke="#f59e0b" stroke-width="3"/>
    </g>` : '';

  const swordBack = isBack ? `
    <g filter="drop-shadow(0 0 6px ${swordColor})">
      <line x1="22" y1="30" x2="78" y2="180" stroke="${swordColor}" stroke-width="4" stroke-linecap="round"/>
      <line x1="22" y1="30" x2="78" y2="180" stroke="#ffffff" stroke-width="1.5" stroke-linecap="round"/>
      <circle cx="22" cy="30" r="5" fill="#fbbf24"/>
    </g>` : '';

  const svg = `
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 110 210" width="110" height="210">
      <defs>
        <radialGradient id="charAura_${color.replace('#','')}" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stop-color="${auraColor}" stop-opacity="0.45"/>
          <stop offset="100%" stop-color="${auraColor}" stop-opacity="0"/>
        </radialGradient>
        <linearGradient id="charRobeGrad_${color.replace('#','')}" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stop-color="${robeColor1}"/>
          <stop offset="60%" stop-color="${robeColor2}"/>
          <stop offset="100%" stop-color="#020617"/>
        </linearGradient>
      </defs>
      <ellipse cx="55" cy="115" rx="52" ry="85" fill="url(#charAura_${color.replace('#','')})"/>
      <ellipse cx="55" cy="196" rx="40" ry="10" fill="rgba(0,0,0,0.45)"/>
      ${swordBack}
      <path d="${robePath}" fill="url(#charRobeGrad_${color.replace('#','')})" stroke="#0f172a" stroke-width="2"/>
      <path d="M42 75 L55 110 L68 75" stroke="#f59e0b" stroke-width="2.5" fill="none"/>
      <rect x="34" y="105" width="38" height="12" rx="3" fill="#f59e0b" stroke="#78350f" stroke-width="1.5"/>
      <rect x="46" y="107" width="14" height="8" rx="2" fill="#67e8f9"/>
      <ellipse cx="${faceX}" cy="50" rx="19" ry="23" fill="#fed7aa" stroke="#78350f" stroke-width="1.5"/>
      ${isBack ? `
        <path d="M30 38 Q50 15 70 38 Q82 85 72 110 Q50 120 28 110 Q18 85 30 38 Z" fill="#0f172a"/>
        <circle cx="50" cy="22" r="11" fill="#020617"/>
        <line x1="34" y1="22" x2="66" y2="22" stroke="#67e8f9" stroke-width="3" stroke-linecap="round"/>
      ` : `
        <path d="M28 48 Q50 20 72 48 Q66 32 50 34 Q34 32 28 48 Z" fill="#0f172a"/>
        <path d="M30 48 Q35 70 34 85 Q30 70 30 48 Z" fill="#0f172a"/>
        <path d="M70 48 Q65 70 66 85 Q70 70 70 48 Z" fill="#0f172a"/>
        <circle cx="50" cy="24" r="10" fill="#020617"/>
        <circle cx="50" cy="24" r="4" fill="#f59e0b"/>
        <line x1="36" y1="24" x2="64" y2="24" stroke="#67e8f9" stroke-width="2.5" stroke-linecap="round"/>
        ${brows}
        ${isSide ? eyeRight : `${eyeLeft}${eyeRight}`}
        ${mouth}
      `}
      ${swordFront}
      <rect x="15" y="2" width="80" height="15" rx="4" fill="rgba(15,23,42,0.9)" stroke="${auraColor}" stroke-width="0.8"/>
      <text x="55" y="13" font-size="8" font-family="sans-serif" font-weight="bold" fill="#38bdf8" text-anchor="middle">${angle.toUpperCase()}</text>
    </svg>
  `.trim();

  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
}

/**
 * Generates High-Quality Panoramic Background (Chinese Donghua Bamboo Mountain Setting)
 */
export function generateBackgroundSvg(title: string, color1: string, color2: string): string {
  const svg = `
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1280 720" width="1280" height="720">
      <defs>
        <linearGradient id="skyGrad" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" stop-color="#090d16"/>
          <stop offset="35%" stop-color="#1e1b4b"/>
          <stop offset="70%" stop-color="#064e3b"/>
          <stop offset="100%" stop-color="#020617"/>
        </linearGradient>
        <radialGradient id="moonGlow" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stop-color="#fef08a" stop-opacity="0.95"/>
          <stop offset="40%" stop-color="#fef08a" stop-opacity="0.3"/>
          <stop offset="100%" stop-color="#fef08a" stop-opacity="0"/>
        </radialGradient>
        <linearGradient id="mistGrad" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" stop-color="rgba(56, 189, 248, 0)"/>
          <stop offset="50%" stop-color="rgba(56, 189, 248, 0.15)"/>
          <stop offset="100%" stop-color="rgba(56, 189, 248, 0)"/>
        </linearGradient>
      </defs>
      <rect width="1280" height="720" fill="url(#skyGrad)"/>
      <circle cx="1020" cy="180" r="140" fill="url(#moonGlow)"/>
      <circle cx="1020" cy="180" r="65" fill="#fef9c3"/>
      <path d="M0 400 Q180 200 420 380 T880 320 T1280 390 L1280 720 L0 720 Z" fill="#0f172a" opacity="0.6"/>
      <path d="M0 480 Q260 310 580 460 T1150 420 L1280 480 L1280 720 L0 720 Z" fill="#091428" opacity="0.85"/>
      <path d="M820 340 L850 310 L880 340 L860 380 L830 370 Z" fill="#091428" opacity="0.9"/>
      <path d="M320 360 L345 335 L370 360 L355 390 Z" fill="#091428" opacity="0.9"/>
      <rect x="845" y="280" width="20" height="30" fill="#020617"/>
      <path d="M835 285 Q855 270 875 285 L870 290 Q855 280 840 290 Z" fill="#f59e0b"/>
      <rect x="0" y="460" width="1280" height="120" fill="url(#mistGrad)"/>
      <g stroke="#042f2e" stroke-linecap="round">
        <line x1="60" y1="720" x2="80" y2="350" stroke-width="10"/>
        <line x1="100" y1="720" x2="115" y2="300" stroke-width="8"/>
        <line x1="135" y1="720" x2="145" y2="400" stroke-width="6"/>
        <line x1="1180" y1="720" x2="1160" y2="320" stroke-width="11"/>
        <line x1="1220" y1="720" x2="1205" y2="280" stroke-width="8"/>
      </g>
      <path d="M80 360 Q120 350 140 370 Q110 380 80 360 Z" fill="#065f46"/>
      <path d="M115 320 Q155 310 175 330 Q145 340 115 320 Z" fill="#047857"/>
      <path d="M1160 330 Q1120 320 1100 340 Q1130 350 1160 330 Z" fill="#065f46"/>
      <rect x="0" y="600" width="1280" height="120" fill="rgba(6, 182, 212, 0.12)"/>
      <ellipse cx="1020" cy="660" rx="90" ry="16" fill="rgba(254, 240, 138, 0.25)"/>
      <rect x="520" y="660" width="240" height="32" rx="6" fill="rgba(2, 6, 23, 0.7)" stroke="rgba(56, 189, 248, 0.3)" stroke-width="1"/>
      <text x="640" y="682" font-size="13" font-family="serif" font-weight="bold" fill="#38bdf8" text-anchor="middle" letter-spacing="4">${title}</text>
    </svg>
  `.trim();

  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
}

/**
 * Generates sample Scene Props
 */
export function generatePresetPropSvg(type: string): string {
  let svg = '';
  switch (type) {
    case 'tree_bamboo':
      svg = `
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 360" width="160" height="360">
          <g stroke="#042f2e" stroke-linecap="round">
            <line x1="80" y1="360" x2="80" y2="40" stroke-width="12"/>
            <line x1="75" y1="360" x2="60" y2="100" stroke-width="8"/>
            <line x1="85" y1="360" x2="105" y2="80" stroke-width="9"/>
          </g>
          <line x1="70" y1="300" x2="90" y2="300" stroke="#065f46" stroke-width="4"/>
          <line x1="72" y1="240" x2="88" y2="240" stroke="#065f46" stroke-width="4"/>
          <line x1="74" y1="170" x2="86" y2="170" stroke="#065f46" stroke-width="4"/>
          <line x1="75" y1="100" x2="85" y2="100" stroke="#065f46" stroke-width="4"/>
          <path d="M80 120 Q130 90 155 120 Q125 135 80 120 Z" fill="#059669"/>
          <path d="M80 160 Q30 130 5 160 Q35 175 80 160 Z" fill="#047857"/>
          <path d="M80 80 Q130 50 150 75 Q120 95 80 80 Z" fill="#10b981"/>
          <path d="M80 220 Q20 200 0 230 Q40 240 80 220 Z" fill="#065f46"/>
          <path d="M80 40 Q80 0 100 20 Q90 40 80 40 Z" fill="#34d399"/>
        </svg>
      `.trim();
      break;

    case 'tree_peach':
      svg = `
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 240 320" width="240" height="320">
          <path d="M120 320 Q100 240 140 180 T90 80 Q110 40 120 20" stroke="#451a03" stroke-width="18" fill="none" stroke-linecap="round"/>
          <path d="M140 180 Q200 150 220 120" stroke="#451a03" stroke-width="10" fill="none" stroke-linecap="round"/>
          <path d="M110 130 Q40 110 20 80" stroke="#451a03" stroke-width="9" fill="none" stroke-linecap="round"/>
          <circle cx="120" cy="50" r="45" fill="#f472b6" opacity="0.85"/>
          <circle cx="95" cy="40" r="35" fill="#fb7185" opacity="0.8"/>
          <circle cx="145" cy="45" r="35" fill="#fbcfe8" opacity="0.9"/>
          <circle cx="210" cy="110" r="32" fill="#f472b6" opacity="0.85"/>
          <circle cx="35" cy="80" r="30" fill="#fbcfe8" opacity="0.85"/>
          <circle cx="80" cy="180" r="4" fill="#fb7185"/>
          <circle cx="160" cy="220" r="3.5" fill="#f472b6"/>
        </svg>
      `.trim();
      break;

    case 'rock_mystic':
      svg = `
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 180 140" width="180" height="140">
          <ellipse cx="90" cy="130" rx="80" ry="10" fill="rgba(0,0,0,0.5)"/>
          <path d="M20 130 L40 60 L85 20 L130 40 L165 90 L150 130 Z" fill="#1e293b" stroke="#475569" stroke-width="2"/>
          <ellipse cx="70" cy="40" rx="15" ry="5" fill="#15803d" opacity="0.7"/>
        </svg>
      `.trim();
      break;

    case 'lantern_glowing':
      svg = `
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 180" width="100" height="180">
          <circle cx="50" cy="90" r="45" fill="#f59e0b" opacity="0.5"/>
          <line x1="50" y1="0" x2="50" y2="40" stroke="#78350f" stroke-width="2.5"/>
          <ellipse cx="50" cy="90" rx="28" ry="36" fill="#dc2626" stroke="#991b1b" stroke-width="2"/>
          <rect x="44" y="80" width="12" height="20" fill="#fef08a" rx="2"/>
          <line x1="50" y1="134" x2="50" y2="175" stroke="#dc2626" stroke-width="3" stroke-linecap="round"/>
        </svg>
      `.trim();
      break;

    case 'branch_foreground':
      svg = `
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 320 180" width="320" height="180">
          <path d="M320 0 Q200 40 120 20 T0 80" stroke="#1c1917" stroke-width="14" fill="none" stroke-linecap="round"/>
          <ellipse cx="60" cy="90" rx="35" ry="20" fill="#064e3b" opacity="0.95"/>
          <ellipse cx="110" cy="115" rx="30" ry="18" fill="#047857" opacity="0.9"/>
          <ellipse cx="140" cy="80" rx="25" ry="15" fill="#059669" opacity="0.85"/>
          <ellipse cx="230" cy="40" rx="40" ry="22" fill="#064e3b" opacity="0.9"/>
        </svg>
      `.trim();
      break;

    default:
      svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><circle cx="50" cy="50" r="40" fill="#38bdf8"/></svg>`;
  }
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
}

/**
 * Creates default sample multi-angle sprites for a character
 */
export function createSampleSprites(charName: string, baseColor: string): Record<StandardHorizontalAngle, string> {
  const map: Partial<Record<StandardHorizontalAngle, string>> = {};
  for (const ang of STANDARD_8_ANGLES) {
    map[ang.id] = generateAngleSvgSample(charName, baseColor, ang.id);
  }
  for (const ang of TOP_DOWN_ANGLES) {
    map[ang.id] = generateAngleSvgSample(charName, baseColor, ang.id);
  }
  return map as Record<StandardHorizontalAngle, string>;
}

/**
 * Built-in Sample Project with Multi-Angle Reverse Shot Dialogue, Scene Props & Donghua Easing
 */
export const DEFAULT_2D_DIRECTOR_PROJECT: Director2DProject = {
  version: '2.5.0',
  projectId: 'project_tu_tien_donghua',
  title: 'Trúc Lâm Quyết Đấu: Tiêu Dao vs Hắc Y (Donghua 2.5D Motion Comic)',
  description: 'Hoạt ảnh tu tiên Trung Quốc chuyển cảnh mượt mà: Shot 1 cận cảnh Kiếm Khách -> Shot 2 đảo góc 180° Ma Tôn phản bác -> Shot 3 góc đỉnh đầu tung chiêu kiếm khí.',
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  stageWidth: 1280,
  stageHeight: 720,
  backgroundLayers: [
    {
      id: 'bg_truc_lam',
      name: 'Bối Cảnh Trúc Lâm Tiên Giới',
      path: generateBackgroundSvg('TRÚC LÂM CẤM ĐỊA (1280x720)', '#1e1b4b', '#065f46'),
      parallaxFactor: 0.35,
      opacity: 1.0,
      offset: [0, 0],
    },
  ],
  props: [
    {
      id: 'prop_truc_1',
      name: 'Rặng Trúc Cổ Thụ Tả Hữu',
      category: 'tree',
      path: generatePresetPropSvg('tree_bamboo'),
      position: [-380, -30],
      scale: [1.2, 1.2],
      rotation: 0,
      zIndex: 6, // Behind actors
      opacity: 1.0,
      visible: true,
      parallaxFactor: 0.45,
    },
    {
      id: 'prop_peach_1',
      name: 'Cây Đào Tiên Hoa Nở',
      category: 'tree',
      path: generatePresetPropSvg('tree_peach'),
      position: [370, -20],
      scale: [1.1, 1.1],
      rotation: 0,
      zIndex: 7, // Behind actors
      opacity: 1.0,
      visible: true,
      parallaxFactor: 0.5,
    },
    {
      id: 'prop_rock_1',
      name: 'Khối Kỳ Thạch Phong Vân',
      category: 'rock',
      path: generatePresetPropSvg('rock_mystic'),
      position: [-220, 60],
      scale: [0.9, 0.9],
      rotation: 0,
      zIndex: 8, // Ground level
      opacity: 1.0,
      visible: true,
      parallaxFactor: 0.65,
    },
    {
      id: 'prop_lantern_1',
      name: 'Đèn Lồng Tiên Đạo',
      category: 'item',
      path: generatePresetPropSvg('lantern_glowing'),
      position: [-320, -110],
      scale: [0.75, 0.75],
      rotation: -5,
      zIndex: 12,
      opacity: 1.0,
      visible: true,
      parallaxFactor: 0.8,
      blendMode: 'normal',
    },
    {
      id: 'prop_branch_fg',
      name: 'Cành Trúc Tiền Cảnh (Đè Trước Ống Kính)',
      category: 'foreground',
      path: generatePresetPropSvg('branch_foreground'),
      position: [240, -160],
      scale: [1.5, 1.5],
      rotation: 5,
      zIndex: 26, // In front of all actors!
      opacity: 0.95,
      visible: true,
      parallaxFactor: 1.4, // Deep foreground parallax
    },
  ],
  actors: [
    {
      id: 'char_tieu_dao',
      name: 'Tiêu Dao Kiếm Khách',
      avatarIcon: '🗡️',
      baseScale: 1.6,
      autoMirrorSymmetry: true,
      sprites: createSampleSprites('Tiêu Dao', '#0284c7'),
    },
    {
      id: 'char_hac_y',
      name: 'Hắc Y Ma Tôn',
      avatarIcon: '🦹',
      baseScale: 1.65,
      autoMirrorSymmetry: true,
      sprites: createSampleSprites('Hắc Y Ma Tôn', '#dc2626'),
    },
  ],
  shots: [
    {
      id: 'shot_1',
      title: 'Shot 1: Tiêu Dao cảnh báo (Chính diện 0° - Đối thủ quay lưng 180°)',
      durationSeconds: 3.5,
      camera: {
        angleStart: 0,
        angleEnd: 0,
        pitchStart: 0,
        pitchEnd: 0,
        zoomStart: 1.05,
        zoomEnd: 1.3,
        panStart: [-70, 0],
        panEnd: [-90, -20],
        shakeIntensity: 0.0,
      },
      actors: {
        char_tieu_dao: {
          actorId: 'char_tieu_dao',
          worldFacingAngle: 0,
          positionStart: [-160, 40],
          positionEnd: [-160, 40],
          scale: 1.6,
          zIndex: 10,
          actionPose: 'talk_dialogue',
        },
        char_hac_y: {
          actorId: 'char_hac_y',
          worldFacingAngle: 180,
          positionStart: [180, 70],
          positionEnd: [180, 70],
          scale: 2.1,
          zIndex: 20,
          actionPose: 'idle_breathe',
        },
      },
      speakerActorId: 'char_tieu_dao',
      dialogueText: 'Dám xâm phạm cấm địa Trúc Lâm, ngươi không sợ một đi không trở lại sao?!',
      sfxSoundUrl: 'asset_2ds/am_thanh/sfx_combat/rut_kiem.mp3',
      transitionIn: 'fade_black',
    },
    {
      id: 'shot_2',
      title: 'Shot 2: Hắc Y phản bác (Đảo góc 180° Over-The-Shoulder - Hắc Y Chính diện 0°)',
      durationSeconds: 3.5,
      camera: {
        angleStart: 180,
        angleEnd: 180,
        pitchStart: 0,
        pitchEnd: 0,
        zoomStart: 1.15,
        zoomEnd: 1.42,
        panStart: [80, 0],
        panEnd: [60, -20],
        shakeIntensity: 0.0,
      },
      actors: {
        char_hac_y: {
          actorId: 'char_hac_y',
          worldFacingAngle: 180,
          positionStart: [160, 40],
          positionEnd: [160, 40],
          scale: 1.65,
          zIndex: 10,
          actionPose: 'talk_dialogue',
        },
        char_tieu_dao: {
          actorId: 'char_tieu_dao',
          worldFacingAngle: 0,
          positionStart: [-180, 70],
          positionEnd: [-180, 70],
          scale: 2.1,
          zIndex: 20,
          actionPose: 'idle_breathe',
        },
      },
      speakerActorId: 'char_hac_y',
      dialogueText: 'Hừ! Chỉ dựa vào kiếm pháp nửa mùa của ngươi mà cũng đòi cản bước bản tôn?',
      sfxSoundUrl: 'asset_2ds/am_thanh/sfx_combat/cuoi_khay.mp3',
      transitionIn: 'whip_pan',
    },
    {
      id: 'shot_3',
      title: 'Shot 3: Góc Đỉnh Đầu Xuống Tung Chiêu (Top-Down 👑 60° - Kiếm Khí Bộc Phát)',
      durationSeconds: 3.2,
      camera: {
        angleStart: 45,
        angleEnd: 45,
        pitchStart: 60,
        pitchEnd: 60,
        zoomStart: 1.25,
        zoomEnd: 1.6,
        panStart: [0, -30],
        panEnd: [0, 0],
        shakeIntensity: 0.0,
      },
      actors: {
        char_tieu_dao: {
          actorId: 'char_tieu_dao',
          worldFacingAngle: 45,
          positionStart: [-110, 30],
          positionEnd: [-20, 30],
          scale: 1.6,
          zIndex: 15,
          actionPose: 'combat_slash',
        },
        char_hac_y: {
          actorId: 'char_hac_y',
          worldFacingAngle: 135,
          positionStart: [180, 30],
          positionEnd: [200, 30],
          scale: 1.5,
          zIndex: 10,
          actionPose: 'shocked_back',
        },
      },
      speakerActorId: 'char_tieu_dao',
      dialogueText: 'Thanh Phong Nhất Kiếm! Tiếp chiêu!',
      sfxSoundUrl: 'asset_2ds/am_thanh/sfx_combat/chem_kiem_no.mp3',
      transitionIn: 'flash_white',
    },
  ],
};

/**
 * Downloads Project JSON to client machine
 */
export function exportProjectToJson(project: Director2DProject, filename?: string): void {
  const jsonStr = JSON.stringify(project, null, 2);
  const blob = new Blob([jsonStr], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename || `${project.projectId || '2d_animation_project'}_${Date.now()}.json`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

/**
 * Parses and validates an uploaded JSON Project string
 */
export function importProjectFromJson(jsonText: string): Director2DProject {
  try {
    const data = JSON.parse(jsonText);
    if (!data.shots || !Array.isArray(data.shots)) {
      throw new Error('Dữ liệu JSON không hợp lệ: Thiếu danh sách shots.');
    }
    return {
      ...DEFAULT_2D_DIRECTOR_PROJECT,
      ...data,
      props: data.props || DEFAULT_2D_DIRECTOR_PROJECT.props,
      updatedAt: new Date().toISOString(),
    };
  } catch (err: any) {
    throw new Error(`Lỗi đọc file JSON: ${err.message}`);
  }
}
