import {
  Character2DPartType,
  Character2DAssembly,
  Map2DPreset,
} from '../../../types/scene2d';

// ─── Procedural Vector Texture Generator for Multi-Angle Instant Preview ─────
export const generateDemoPartSvg = (
  slot: Character2DPartType,
  gender: 'nam' | 'nu' | 'chung' = 'nam',
  angle: 'front' | 'three_quarter' | 'profile' | 'back' = 'front'
): string => {
  const isMale = gender === 'nam';
  const skinColor = '#ffdfba';
  const hairColor = isMale ? '#1e293b' : '#312e81';
  const clothColor = isMale ? '#0284c7' : '#9333ea';

  let svgContent = '';

  // 1. SIDE PROFILE (90° View with prominent nose, chin, and ear)
  if (angle === 'profile') {
    switch (slot) {
      case 'dau':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 180" width="160" height="180">
          <path d="M100 30 C100 15, 50 15, 30 50 C20 75, 22 95, 24 105 L8 118 L24 125 L16 136 L26 142 C24 155, 36 165, 55 165 C80 165, 105 140, 110 95 C112 70, 110 40, 100 30 Z" fill="${skinColor}" stroke="#d4a373" stroke-width="2.5"/>
          <path d="M85 95 C98 95, 98 125, 85 125 C80 125, 78 115, 80 108 Z" fill="${skinColor}" stroke="#d4a373" stroke-width="2"/>
          <path d="M60 160 L60 180 L90 180 L90 160" fill="${skinColor}"/>
        </svg>`;
        break;
      case 'mat':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 110 40" width="110" height="40">
          <path d="M25 15 L45 20 L27 28 Z" fill="#ffffff" stroke="#1e293b" stroke-width="1.8"/>
          <circle cx="34" cy="20" r="5" fill="#0284c7"/>
          <circle cx="36" cy="18" r="1.8" fill="#ffffff"/>
          <path d="M20 10 Q38 6 52 14" stroke="#1e293b" stroke-width="2.5" fill="none" stroke-linecap="round"/>
        </svg>`;
        break;
      case 'mieng':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 45 25" width="45" height="25">
          <path d="M10 12 L25 12" stroke="#be123c" stroke-width="2.5" stroke-linecap="round"/>
        </svg>`;
        break;
      case 'mui':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 25" width="20" height="25">
          <path d="M4 4 L14 18 L6 18" stroke="#d4a373" stroke-width="2" fill="none" stroke-linecap="round"/>
        </svg>`;
        break;
      case 'toc_truoc':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 170 95" width="170" height="95">
          <path d="M20 40 Q75 5 120 40 Q95 25 70 65 Q45 35 20 40 Z" fill="${hairColor}" stroke="#0f172a" stroke-width="2"/>
        </svg>`;
        break;
      case 'toc_sau':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 190 260" width="190" height="260">
          <path d="M70 30 Q120 5 150 40 Q165 150 115 250 Q75 230 70 30 Z" fill="${hairColor}" opacity="0.95"/>
        </svg>`;
        break;
      case 'than_co_ban':
      case 'trang_phuc':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 210" width="160" height="210">
          <path d="M40 15 L105 15 L95 195 L50 195 Z" fill="${slot === 'than_co_ban' ? skinColor : clothColor}" stroke="#ffffff" stroke-width="2"/>
          <rect x="42" y="95" width="60" height="18" fill="#fbbf24" rx="3"/>
        </svg>`;
        break;
      default:
        svgContent = generateDemoPartSvg(slot, gender, 'front');
    }
  } else if (angle === 'back') {
    // 2. BACK VIEW (180°)
    switch (slot) {
      case 'dau':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 180" width="160" height="180">
          <path d="M30 60 C30 15, 130 15, 130 60 C130 110, 105 150, 80 165 C55 150, 30 110, 30 60 Z" fill="${skinColor}"/>
          <path d="M25 90 C20 90, 20 115, 28 115 Z" fill="${skinColor}" stroke="#d4a373" stroke-width="1.8"/>
          <path d="M135 90 C140 90, 140 115, 132 115 Z" fill="${skinColor}" stroke="#d4a373" stroke-width="1.8"/>
        </svg>`;
        break;
      case 'mat':
      case 'mieng':
      case 'mui':
      case 'toc_truoc':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10" width="10" height="10"></svg>`;
        break;
      case 'toc_sau':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 190 260" width="190" height="260">
          <path d="M20 25 Q95 -10 170 25 Q185 150 150 255 Q95 230 40 255 Q5 150 20 25 Z" fill="${hairColor}" stroke="#0f172a" stroke-width="2"/>
        </svg>`;
        break;
      case 'than_co_ban':
      case 'trang_phuc':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 210" width="160" height="210">
          <path d="M25 15 L135 15 L145 195 L15 195 Z" fill="${clothColor}" stroke="#ffffff" stroke-width="2"/>
          <rect x="35" y="95" width="90" height="18" fill="#fbbf24" rx="3"/>
          <path d="M80 15 L80 195" stroke="#0284c7" stroke-width="2.5"/>
        </svg>`;
        break;
      default:
        svgContent = generateDemoPartSvg(slot, gender, 'front');
    }
  } else {
    // 3. FRONT VIEW (0°) & 3/4 VIEW
    switch (slot) {
      case 'dau':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 180" width="160" height="180">
          <path d="M30 60 C30 15, 130 15, 130 60 C130 110, 105 150, 80 165 C55 150, 30 110, 30 60 Z" fill="${skinColor}" stroke="#d4a373" stroke-width="2.5"/>
          <path d="M60 150 L60 180 L100 180 L100 150" fill="${skinColor}" opacity="0.95"/>
        </svg>`;
        break;
      case 'mat':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 110 40" width="110" height="40">
          <ellipse cx="30" cy="20" rx="14" ry="10" fill="#ffffff" stroke="#1e293b" stroke-width="1.8"/>
          <circle cx="30" cy="20" r="6" fill="#0284c7"/>
          <circle cx="32" cy="18" r="2" fill="#ffffff"/>
          <ellipse cx="80" cy="20" rx="14" ry="10" fill="#ffffff" stroke="#1e293b" stroke-width="1.8"/>
          <circle cx="80" cy="20" r="6" fill="#0284c7"/>
          <circle cx="82" cy="18" r="2" fill="#ffffff"/>
          <path d="M15 8 Q30 4 45 8" stroke="#1e293b" stroke-width="2.5" fill="none" stroke-linecap="round"/>
          <path d="M65 8 Q80 4 95 8" stroke="#1e293b" stroke-width="2.5" fill="none" stroke-linecap="round"/>
        </svg>`;
        break;
      case 'mieng':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 45 25" width="45" height="25">
          <path d="M12 10 Q22.5 20 33 10" stroke="#be123c" stroke-width="2.5" fill="none" stroke-linecap="round"/>
          <path d="M15 10 Q22.5 15 30 10" fill="#fda4af" opacity="0.7"/>
        </svg>`;
        break;
      case 'mui':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 25" width="20" height="25">
          <path d="M10 5 L8 18 L13 18" stroke="#d4a373" stroke-width="2" fill="none" stroke-linecap="round"/>
        </svg>`;
        break;
      case 'toc_truoc':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 170 95" width="170" height="95">
          <path d="M15 45 Q85 -10 155 45 Q130 20 105 60 Q85 25 65 65 Q40 25 15 45 Z" fill="${hairColor}" stroke="#0f172a" stroke-width="2"/>
        </svg>`;
        break;
      case 'toc_sau':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 190 260" width="190" height="260">
          <path d="M20 25 Q95 -10 170 25 Q185 150 150 255 Q95 230 40 255 Q5 150 20 25 Z" fill="${hairColor}" opacity="0.95"/>
        </svg>`;
        break;
      case 'than_co_ban':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 140 180" width="140" height="180">
          <path d="M30 15 L110 15 L100 165 L40 165 Z" fill="${skinColor}" stroke="#d4a373" stroke-width="2"/>
        </svg>`;
        break;
      case 'trang_phuc':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 210" width="160" height="210">
          <path d="M25 15 L135 15 L145 195 L15 195 Z" fill="${clothColor}" stroke="#ffffff" stroke-width="2"/>
          <path d="M65 15 L80 85 L95 15" fill="#f8fafc" stroke="#38bdf8" stroke-width="1.8"/>
          <rect x="35" y="95" width="90" height="18" fill="#fbbf24" rx="3"/>
        </svg>`;
        break;
      case 'canh_tay_trai':
      case 'canh_tay_phai':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 45 130" width="45" height="130">
          <rect x="10" y="8" width="25" height="110" rx="12" fill="${skinColor}" stroke="#d4a373" stroke-width="1.8"/>
          <rect x="7" y="8" width="31" height="40" rx="6" fill="${clothColor}"/>
        </svg>`;
        break;
      case 'cang_chan_trai':
      case 'cang_chan_phai':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 45 140" width="45" height="140">
          <rect x="10" y="8" width="25" height="115" rx="10" fill="${skinColor}" stroke="#d4a373" stroke-width="1.8"/>
          <rect x="6" y="105" width="33" height="28" rx="5" fill="#1e293b"/>
        </svg>`;
        break;
      case 'vu_khi':
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 50 200" width="50" height="200">
          <rect x="22" y="10" width="6" height="40" rx="2" fill="#78350f"/>
          <circle cx="25" cy="8" r="5" fill="#fbbf24"/>
          <rect x="12" y="48" width="26" height="8" rx="2" fill="#fbbf24"/>
          <path d="M25 56 L32 190 L18 190 Z" fill="#e2e8f0" stroke="#38bdf8" stroke-width="1.8"/>
        </svg>`;
        break;
      default:
        svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100" height="100">
          <rect x="10" y="10" width="80" height="80" rx="10" fill="#334155" stroke="#64748b" stroke-width="2"/>
          <text x="50" y="55" font-family="sans-serif" font-size="12" fill="#94a3b8" text-anchor="middle">${slot}</text>
        </svg>`;
    }
  }

  return `data:image/svg+xml;utf8,${encodeURIComponent(svgContent.trim())}`;
};

// ─── Default Sample 2D Characters with Multi-Angle Presets ───────
export const EMPTY_CHARACTER_ASSEMBLY: Character2DAssembly = {
  id: 'char_custom_canvas',
  name: 'Nhân Vật Tự Lắp Ráp',
  gender: 'chung',
  style: 'tu_tien',
  base_scale: 1.0,
  layer_depth_spacing: 1.0,
  parts: {},
  created_at: new Date().toISOString(),
};

export const DEFAULT_SAMPLE_CHARACTERS_2D: Character2DAssembly[] = [
  EMPTY_CHARACTER_ASSEMBLY,
];

// ─── Default Sample 2D Parallax Maps ─────────────────────────────
export const DEFAULT_SAMPLE_MAPS_2D: Map2DPreset[] = [
  {
    id: 'map_truc_lam_tu_tien',
    name: 'Trúc Lâm Tiên Cảnh',
    description: 'Rừng trúc huyền ảo với mây trôi và ánh hoàng hôn buông xuống.',
    layers: [
      {
        id: 'sky',
        name: 'Bầu Trời Hoàng Hôn',
        type: 'sky',
        path: 'data:image/svg+xml;utf8,' + encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080"><defs><linearGradient id="g" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#1e1b4b"/><stop offset="50%" stop-color="#7c2d12"/><stop offset="100%" stop-color="#ea580c"/></linearGradient></defs><rect width="1920" height="1080" fill="url(#g)"/><circle cx="960" cy="540" r="160" fill="#fef08a" opacity="0.7"/></svg>`),
        parallax_factor: 0.1,
        offset: [0, 0],
        scale: [1, 1],
        opacity: 1,
        z_index: 1,
        scroll_speed_x: 5,
      },
      {
        id: 'mountains',
        name: 'Núi Tiên Hậu Cảnh',
        type: 'background',
        path: 'data:image/svg+xml;utf8,' + encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080"><path d="M0 600 Q400 300 800 650 Q1300 200 1920 600 L1920 1080 L0 1080 Z" fill="#312e81" opacity="0.6"/></svg>`),
        parallax_factor: 0.3,
        offset: [0, 50],
        scale: [1, 1],
        opacity: 0.85,
        z_index: 2,
      },
      {
        id: 'ground_temple',
        name: 'Sân Trúc Lâm Trung Cảnh',
        type: 'midground',
        path: 'data:image/svg+xml;utf8,' + encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080"><rect x="0" y="720" width="1920" height="360" fill="#0f172a"/><rect x="0" y="700" width="1920" height="30" fill="#065f46"/><g fill="#047857"><rect x="200" y="200" width="20" height="520" rx="10"/><rect x="500" y="150" width="24" height="570" rx="12"/><rect x="1400" y="180" width="22" height="540" rx="11"/><rect x="1700" y="220" width="18" height="500" rx="9"/></g></svg>`),
        parallax_factor: 1.0,
        offset: [0, 0],
        scale: [1, 1],
        opacity: 1,
        z_index: 3,
      },
      {
        id: 'leaves_foreground',
        name: 'Lá Trúc Tiền Cảnh',
        type: 'foreground',
        path: 'data:image/svg+xml;utf8,' + encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080"><path d="M-50 -50 Q200 300 400 100 Q150 50 -50 -50 Z" fill="#064e3b" opacity="0.9"/><path d="M1970 -50 Q1700 350 1500 120 Q1800 60 1970 -50 Z" fill="#064e3b" opacity="0.9"/></svg>`),
        parallax_factor: 1.8,
        offset: [0, 0],
        scale: [1, 1],
        opacity: 0.95,
        z_index: 10,
      },
    ],
    atmosphere: {
      weather: 'falling_leaves',
      fog_opacity: 0.2,
      lighting_tint: '#ffe4cc',
      ambient_audio: 'asset_2ds/am_thanh/sfx_moi_truong/gio_rung_truc.mp3',
    },
  },
];
