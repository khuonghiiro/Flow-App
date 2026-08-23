import {
  Character2DPartType,
  Character2DAssembly,
  StandardCropPreset,
  Map2DPreset,
  AIPartPromptConfig,
} from '../../types/scene2d';

// ─── Standard Slicing / Bounding Box Presets ─────────────────────
export const STANDARD_CROP_PRESETS: StandardCropPreset[] = [
  {
    id: 'crop_dau',
    label: 'Đầu & Sọ (512x512)',
    category: 'nhan_vat',
    slot: 'dau',
    width: 512,
    height: 512,
    aspectRatio: 1.0,
    suggestedPivot: [0.5, 0.85],
    description: 'Bao gồm phần đầu, cằm, tai, không có tóc phủ.',
  },
  {
    id: 'crop_mat',
    label: 'Cặp Mắt (128x64)',
    category: 'nhan_vat',
    slot: 'mat',
    width: 128,
    height: 64,
    aspectRatio: 2.0,
    suggestedPivot: [0.5, 0.5],
    description: 'Chứa mắt mở hoặc nhắm để đổi trạng thái chớp mắt.',
  },
  {
    id: 'crop_mieng',
    label: 'Khẩu Hình Miệng (128x64)',
    category: 'nhan_vat',
    slot: 'mieng',
    width: 128,
    height: 64,
    aspectRatio: 2.0,
    suggestedPivot: [0.5, 0.5],
    description: 'Khẩu hình nói chuyện A/I/U/E/O, cười, hoặc ngậm miệng.',
  },
  {
    id: 'crop_mui',
    label: 'Mũi (64x64)',
    category: 'nhan_vat',
    slot: 'mui',
    width: 64,
    height: 64,
    aspectRatio: 1.0,
    suggestedPivot: [0.5, 0.5],
    description: 'Sống mũi thẳng hoặc góc nghiêng 3/4.',
  },
  {
    id: 'crop_toc_truoc',
    label: 'Tóc Mái / Tóc Trước (512x512)',
    category: 'nhan_vat',
    slot: 'toc_truoc',
    width: 512,
    height: 512,
    aspectRatio: 1.0,
    suggestedPivot: [0.5, 0.2],
    description: 'Lớp tóc mái phía trước mặt (Z-index cao nhất phần đầu).',
  },
  {
    id: 'crop_toc_sau',
    label: 'Tóc Dài Phía Sau (512x512)',
    category: 'nhan_vat',
    slot: 'toc_sau',
    width: 512,
    height: 512,
    aspectRatio: 1.0,
    suggestedPivot: [0.5, 0.2],
    description: 'Lớp tóc dài xõa sau lưng (Z-index thấp nhất dưới thân).',
  },
  {
    id: 'crop_than',
    label: 'Thân & Ngực Bụng (512x768)',
    category: 'nhan_vat',
    slot: 'than_co_ban',
    width: 512,
    height: 768,
    aspectRatio: 0.667,
    suggestedPivot: [0.5, 0.8],
    description: 'Phần thân mình cơ bản làm trụ gắn các chi.',
  },
  {
    id: 'crop_canh_tay',
    label: 'Cánh Tay Trên (256x512)',
    category: 'nhan_vat',
    slot: 'canh_tay_trai',
    width: 256,
    height: 512,
    aspectRatio: 0.5,
    suggestedPivot: [0.5, 0.15],
    description: 'Từ khớp vai đến khuỷu tay.',
  },
  {
    id: 'crop_cang_tay',
    label: 'Cẳng Tay & Bàn Tay (256x512)',
    category: 'nhan_vat',
    slot: 'cang_tay_trai',
    width: 256,
    height: 512,
    aspectRatio: 0.5,
    suggestedPivot: [0.5, 0.15],
    description: 'Từ khuỷu tay đến bàn tay cầm nắm vũ khí.',
  },
  {
    id: 'crop_cang_chan',
    label: 'Chân & Bàn Chân (256x512)',
    category: 'nhan_vat',
    slot: 'cang_chan_trai',
    width: 256,
    height: 512,
    aspectRatio: 0.5,
    suggestedPivot: [0.5, 0.1],
    description: 'Từ hông hoặc đầu gối xuống bàn chân.',
  },
  {
    id: 'crop_trang_phuc',
    label: 'Trang Phục / Đạo Bào (512x768)',
    category: 'nhan_vat',
    slot: 'trang_phuc',
    width: 512,
    height: 768,
    aspectRatio: 0.667,
    suggestedPivot: [0.5, 0.5],
    description: 'Áo dài, giáp trụ, tà áo bay phủ ngoài thân.',
  },
  {
    id: 'crop_vu_khi',
    label: 'Vũ Khí & Đạo Cụ (512x512)',
    category: 'nhan_vat',
    slot: 'vu_khi',
    width: 512,
    height: 512,
    aspectRatio: 1.0,
    suggestedPivot: [0.2, 0.8],
    description: 'Kiếm, đao, thương, trượng, quạt, pháp bảo.',
  },
  {
    id: 'crop_map_layer',
    label: 'Lớp Bản Đồ Parallax (1920x1080)',
    category: 'ban_do',
    slot: 'map_layer',
    width: 1920,
    height: 1080,
    aspectRatio: 1.778,
    suggestedPivot: [0.5, 0.5],
    description: 'Ảnh kích thước chuẩn 16:9 cho từng tầng bản đồ.',
  },
];

// ─── Default 2D Part Hierarchy & Pivot Presets ───────────────────
export const PART_HIERARCHY_CONFIG: Record<
  Character2DPartType,
  { label: string; defaultZ: number; defaultZDepth3D: number; defaultPivot: [number, number]; defaultOffset: [number, number] }
> = {
  toc_sau: { label: 'Tóc Sau', defaultZ: 1, defaultZDepth3D: 1, defaultPivot: [0.5, 0.15], defaultOffset: [0, -165] },
  than_co_ban: { label: 'Thân Cơ Bản', defaultZ: 2, defaultZDepth3D: 2, defaultPivot: [0.5, 0.5], defaultOffset: [0, 0] },
  dui_trai: { label: 'Đùi Trái', defaultZ: 2, defaultZDepth3D: 2, defaultPivot: [0.5, 0.1], defaultOffset: [-22, 60] },
  dui_phai: { label: 'Đùi Phải', defaultZ: 2, defaultZDepth3D: 2, defaultPivot: [0.5, 0.1], defaultOffset: [22, 60] },
  cang_chan_trai: { label: 'Cẳng Chân Trái', defaultZ: 3, defaultZDepth3D: 2, defaultPivot: [0.5, 0.1], defaultOffset: [-22, 75] },
  cang_chan_phai: { label: 'Cẳng Chân Phải', defaultZ: 3, defaultZDepth3D: 2, defaultPivot: [0.5, 0.1], defaultOffset: [22, 75] },
  trang_phuc: { label: 'Trang Phục', defaultZ: 4, defaultZDepth3D: 4, defaultPivot: [0.5, 0.5], defaultOffset: [0, 5] },
  canh_tay_trai: { label: 'Cánh Tay Trái', defaultZ: 5, defaultZDepth3D: 5, defaultPivot: [0.8, 0.15], defaultOffset: [-55, -60] },
  cang_tay_trai: { label: 'Cẳng Tay Trái', defaultZ: 5, defaultZDepth3D: 6, defaultPivot: [0.8, 0.2], defaultOffset: [-65, 10] },
  ban_tay_trai: { label: 'Bàn Tay Trái', defaultZ: 5, defaultZDepth3D: 7, defaultPivot: [0.5, 0.2], defaultOffset: [-70, 50] },
  dau: { label: 'Đầu & Cằm', defaultZ: 6, defaultZDepth3D: 8, defaultPivot: [0.5, 0.85], defaultOffset: [0, -100] },
  khuon_mat: { label: 'Khuôn Mặt', defaultZ: 7, defaultZDepth3D: 9, defaultPivot: [0.5, 0.5], defaultOffset: [0, -135] },
  mat: { label: 'Mắt (Chớp/Mở)', defaultZ: 8, defaultZDepth3D: 10, defaultPivot: [0.5, 0.5], defaultOffset: [0, -140] },
  mui: { label: 'Mũi', defaultZ: 8, defaultZDepth3D: 11, defaultPivot: [0.5, 0.5], defaultOffset: [0, -125] },
  mieng: { label: 'Miệng (Khẩu hình)', defaultZ: 8, defaultZDepth3D: 10, defaultPivot: [0.5, 0.5], defaultOffset: [0, -110] },
  toc_truoc: { label: 'Tóc Mái Trước', defaultZ: 9, defaultZDepth3D: 12, defaultPivot: [0.5, 0.1], defaultOffset: [0, -170] },
  canh_tay_phai: { label: 'Cánh Tay Phải', defaultZ: 10, defaultZDepth3D: 5, defaultPivot: [0.2, 0.15], defaultOffset: [55, -60] },
  cang_tay_phai: { label: 'Cẳng Tay Phải', defaultZ: 10, defaultZDepth3D: 6, defaultPivot: [0.2, 0.2], defaultOffset: [65, 10] },
  ban_tay_phai: { label: 'Bàn Tay Phải', defaultZ: 10, defaultZDepth3D: 7, defaultPivot: [0.5, 0.2], defaultOffset: [70, 50] },
  vu_khi: { label: 'Vũ Khí / Pháp Bảo', defaultZ: 11, defaultZDepth3D: 13, defaultPivot: [0.5, 0.2], defaultOffset: [72, 35] },
};

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

// ─── AI Prompt Templates Builder Helper with Dual English/Vietnamese, JSON Format & Multi-Angle Grid ──
export interface AIPromptResult {
  promptEnglish: string;
  promptVietnamese: string;
  promptJSON: string;
  promptGemini: string;
  gridStructureGuide: string;
  negativePrompt: string;
  fullCopyText: string;
}

export const getSheetTypeLabel = (s: string) => {
  switch(s) {
    case 'hair_multi_angle_grid': return 'Bảng Tóc Đa Góc (4x5)';
    case 'eyes_grid': return 'Bảng Trạng Thái Mắt & Lông Mày';
    case 'mouth_grid': return 'Bảng Khẩu Hình Miệng Lip-sync';
    case 'nose_chin_grid': return 'Bảng Sống Mũi, Cằm & Tai';
    case 'costume_grid': return 'Bảng Đạo Bào / Trang Phục Rỗng';
    case 'weapons_grid': return 'Bảng Vũ Khí & Pháp Bảo';
    case 'limbs_hands_grid': return 'Bảng Tứ Chi & Bắt Quyết';
    case 'body_turnaround_grid': return 'Bảng Nhân Vật Hoàn Chỉnh';
    case 'step1_master_character': return 'Bảng Thiết Kế Nhân Vật (Master Turnaround Sheet)';
    default: return 'Linh Kiện Đơn Lẻ';
  }
};

export const getStyleLabel = (s: string) => {
  switch(s) {
    case 'tu_tien_manhua': return 'Tu Tiên / Manhua Trung Quốc';
    case 'hoat_hinh_3d_trung_quoc': return 'Hoạt Hình 3D Trung Quốc (3D Donghua)';
    case 'kiem_hiep': return 'Kiếm Hiệp / Cổ Trang';
    case 'anime_action': return 'Anime Action Nhật Bản';
    case 'cyberpunk_anime': return 'Cyberpunk Anime';
    case 'chibi': return 'Chibi Đáng Yêu';
    default: return s;
  }
};

export const getGenderLabel = (g: string) => {
  return g === 'nam' ? 'Nam Tu Sĩ' : g === 'nu' ? 'Nữ Tiên Tử' : 'Chung';
};

export const buildAIPromptForPart = (config: AIPartPromptConfig): AIPromptResult => {
  const sheet = config.sheet_type || 'hair_multi_angle_grid';
  let bgTextEn = '';
  let bgTextVi = '';
  let bgPromptColorEn = '';
  
  switch (config.bg_type) {
    case 'chroma_green':
      bgTextEn = 'isolated on solid flat pure chroma green background #00FF00, uniform flat single color, high contrast edge, strictly flat neutral unlit shading, absolutely zero ambient color spill, no bounce light, no rim lighting, no global illumination, pure matte colors';
      bgTextVi = 'Nền xanh lá Chroma Green (#00FF00) cắt phông 1 click';
      bgPromptColorEn = 'solid pure chroma green background #00FF00';
      break;
    case 'chroma_gray':
      bgTextEn = 'isolated on solid flat neutral dark gray background #333333, uniform flat single color, high contrast edge, zero shadows, no color spill, perfect for white hair extraction';
      bgTextVi = 'Nền xám đậm trung tính (#333333) tuyệt đối không ám màu, lý tưởng để tách tóc trắng/bạc';
      bgPromptColorEn = 'solid neutral dark gray background #333333';
      break;
    case 'pure_black':
      bgTextEn = 'isolated on solid flat pure black background #000000, uniform flat single color, high contrast edge, zero shadows';
      bgTextVi = 'Nền đen tuyền (#000000) không bóng đổ, dùng cho tóc phát sáng';
      bgPromptColorEn = 'solid pure black background #000000';
      break;
    case 'pure_white':
    default:
      bgTextEn = 'isolated on solid pure flat white background #FFFFFF, clean flat cutout, zero drop shadows, no ambient occlusion, strictly neutral lighting';
      bgTextVi = 'Nền trắng tinh khiết (#FFFFFF) đánh sáng trung tính, tuyệt đối không ám màu';
      bgPromptColorEn = 'solid pure white background #FFFFFF';
      break;
  }

  const noTextEn =
    'clean graphic asset only, strictly NO text, NO letters, NO words, NO numbers, NO watermark, NO labels, NO typography, NO captions, NO annotations, NO border writing';

  const styleTextEn =
    config.character_style === 'custom' && config.custom_character_style
      ? config.custom_character_style
      : config.character_style === 'chibi'
      ? 'cute 2D anime chibi character artstyle, adorable 2-head to 3-head body proportions, large sparkling expressive eyes, chubby cheeks, soft cute face, clean bold outlines, vibrant flat cel shading'
      : config.character_style === 'tu_tien_manhua'
      ? 'Chinese xianxia cultivation manhua artstyle, clean vector-like anime line art, crisp flat cel shading'
      : config.character_style === 'anime_action'
      ? 'modern Japanese anime action animation keyframe style, studio trigger crisp aesthetic'
      : config.character_style === 'kiem_hiep'
      ? 'classic wuxia martial arts illustration, clean defined borders, high aesthetic'
      : 'stylized 2D animation sprite asset';

  let promptEnglish = '';
  let promptVietnamese = '';
  let promptJSON = '';
  let gridStructureGuide = '';

  // 0. STEP 1: MASTER CHARACTER TURNAROUND SHEET (BẢNG THIẾT KẾ NHÂN VẬT HOÀN CHỈNH 5 GÓC + ĐỈNH ĐẦU)
  if (config.workflow_step === 'step1_master_character') {
    const ar = config.aspect_ratio || '16:9';
    const isMale = config.gender === 'nam';
    const isChibi = config.character_style === 'chibi';
    const genderTextEn = isChibi
      ? isMale
        ? 'cute adorable chibi boy character, compact kawaii proportions, 2.5 heads tall'
        : 'cute adorable chibi girl fairy, compact kawaii proportions, 2.5 heads tall'
      : isMale
      ? 'handsome male cultivator'
      : 'ethereal celestial female heroine';

    const eyeColEn =
      config.eye_color === 'custom' && config.custom_eye_color
        ? config.custom_eye_color
        : config.eye_color === 'chibi_sweet_pink'
        ? 'sweet strawberry pink iris'
        : config.eye_color === 'crimson_red'
        ? 'crimson red'
        : config.eye_color === 'golden_amber'
        ? 'golden amber'
        : config.eye_color === 'emerald_green'
        ? 'emerald green'
        : config.eye_color === 'obsidian_black'
        ? 'obsidian black'
        : 'azure cyan';

    const eyeShapeEn =
      config.eye_shape === 'custom' && config.custom_eye_shape
        ? config.custom_eye_shape
        : config.eye_shape === 'chibi_sparkling_starry'
        ? 'giant sparkling round starry anime chibi eyes with glossy reflections'
        : config.eye_shape === 'chibi_happy_crescent'
        ? 'happy joyful crescent-shaped curved anime wink eyes'
        : config.eye_shape === 'chibi_pouty_teary'
        ? 'cute puppy teary glossy chibi eyes'
        : config.eye_shape === 'sharp_phoenix'
        ? 'sharp phoenix eyes'
        : config.eye_shape === 'cold_swordsman'
        ? 'cold piercing swordsman eyes'
        : config.eye_shape === 'fox_alluring'
        ? 'alluring fox eyes'
        : isChibi
        ? 'large sparkling round expressive chibi eyes with detailed glossy highlights'
        : 'clear vibrant anime eyes';

    const noseEn =
      config.nose_shape === 'custom' && config.custom_nose_shape
        ? config.custom_nose_shape
        : config.nose_shape === 'chibi_no_nose'
        ? 'classic chibi style with no visible nose'
        : config.nose_shape === 'chibi_tiny_dot'
        ? 'cute microscopic dot anime nose'
        : config.nose_shape === 'sharp_defined'
        ? 'sharp refined nose'
        : config.nose_shape === 'small_delicate'
        ? 'small delicate nose'
        : isChibi
        ? 'cute tiny dot anime nose'
        : 'tall straight delicate nose bridge';

    const mouthEn =
      config.mouth_style === 'custom' && config.custom_mouth_style
        ? config.custom_mouth_style
        : config.mouth_style === 'chibi_cat_mouth'
        ? 'cute playful cat-like :3 smile mouth'
        : config.mouth_style === 'chibi_surprised_o'
        ? 'cute open surprised circle O-mouth'
        : config.mouth_style === 'chibi_puffed_cheek'
        ? 'cute chubby puffed cheeks with pouty mouth'
        : config.mouth_style === 'chibi_big_smile'
        ? 'big joyful open happy anime smile'
        : config.mouth_style === 'confident_smirk'
        ? 'confident subtle smirk'
        : config.mouth_style === 'battle_roar'
        ? 'battle shout'
        : config.mouth_style === 'gentle_smile'
        ? 'gentle serene smile'
        : isChibi
        ? 'cute happy cat-smile mouth'
        : 'calm noble expression';

    const costumeStyleEn =
      config.costume_style === 'custom' && config.custom_costume_style
        ? config.custom_costume_style
        : isChibi
        ? 'cute miniature traditional daoist robes with oversized sleeves and jade pendant'
        : config.costume_style === 'hac_y_ma_dao'
        ? 'dark gothic daoist robes with red flame embroidery'
        : config.costume_style === 'hoang_toc_kim_bao'
        ? 'imperial golden embroidered robes with dragon motifs'
        : config.costume_style === 'bach_y_tien_tu'
        ? 'pure white flowing silk fairy robes'
        : config.costume_style === 'kiem_khach_ao_vai'
        ? 'traditional martial arts wanderer robes with leather vambraces'
        : 'celestial xianxia daoist robes with intricate silk trims and jade pendant sash';

    const costumeColEn = config.costume_color || 'cyan and white with gold accents';
    const propEn =
      config.prop_item === 'custom' && config.custom_prop_item
        ? config.custom_prop_item
        : config.prop_item === 'jade_hairpin'
        ? 'carved jade hairpin and silk ribbons'
        : config.prop_item === 'feather_fan'
        ? 'feather cultivation fan'
        : config.prop_item === 'talisman_scrolls'
        ? 'glowing spiritual talisman scrolls'
        : config.prop_item === 'gourd_wine'
        ? 'celestial wine gourd'
        : 'flying spirit sword glowing with azure sword intent';

    const hairColEn =
      config.hair_color === 'custom' && config.custom_hair_color
        ? config.custom_hair_color
        : config.hair_color === 'silver_white'
        ? 'silver white'
        : config.hair_color === 'crimson_red'
        ? 'fiery red'
        : 'jet black';

    const hairLenEn =
      config.hair_length === 'custom' && config.custom_hair_length
        ? config.custom_hair_length
        : config.hair_length === 'short'
        ? 'stylish short hair'
        : config.hair_length === 'medium_shoulder'
        ? 'shoulder length hair'
        : config.hair_length === 'very_long_flowing'
        ? 'floor-length celestial flowing hair'
        : 'long waist-length flowing hair';

    const hairTexEn =
      config.hair_texture === 'custom' && config.custom_hair_texture
        ? config.custom_hair_texture
        : config.hair_texture === 'wavy_curls'
        ? 'wavy locks'
        : config.hair_texture === 'wild_spiky'
        ? 'action spiky locks'
        : 'straight silky strands';

    const hairAccEn =
      config.hair_accessories === 'custom' && config.custom_hair_accessories
        ? config.custom_hair_accessories
        : config.hair_accessories === 'golden_crown'
        ? 'ornate golden crown'
        : config.hair_accessories === 'flowing_ribbons'
        ? 'fluttering silk ribbons'
        : 'traditional jade hairpin bun';

    promptEnglish = [
      `masterpiece, best quality, ultra detailed 4k resolution, full body and upper body multi-angle character model sheet of ONE SINGLE character`,
      `Chinese 3D Donghua animation style, exquisite breathtaking Wuxia/Xianxia aesthetics, extremely beautiful and flawless facial features (ngũ quan tuyệt mỹ)`,
      `consistent character depicted from 5 angles: front view, 45 degree three-quarter view, 90 degree side profile, 135 degree back three-quarter view, 180 degree rear back view, plus top-down bird's-eye head crown view`,
      `${genderTextEn}, extremely handsome/gorgeous, beautiful facial structure, consistent facial features: ${eyeShapeEn} with glowing ${eyeColEn} iris, ${noseEn}, ${mouthEn}, clean defined ears`,
      `attire: ${costumeStyleEn}, color theme: ${costumeColEn}, wide flowing sleeves, flutter ribbons in spiritual wind`,
      `accessories and weapons: ${propEn}`,
      `hairstyle: ${hairColEn} ${hairLenEn}, ${hairTexEn}, styled with ${hairAccEn}`,
      styleTextEn,
      'solid pure clean white studio background #FFFFFF with high contrast character silhouette, zero clutter, no background scenery',
      `uniform soft studio lighting, crisp cinematic shading, character concept turnaround sheet`,
      `--ar ${ar} --no text typography letters font words labels captions numbers writing watermark signature logo characters subtitle calligraphy heading title annotations alphabet stamp frame border-text`,
    ].join(', ');

    promptVietnamese = `【 BƯỚC 1: BẢNG THIẾT KẾ NHÂN VẬT GỐC ĐẦY ĐỦ 5 GÓC QUAY (MASTER TURNAROUND SHEET) 】
• Phong cách: ${isChibi ? 'Hoạt Hình Chibi Đáng Yêu (2.5 Heads Tall)' : config.character_style}
• Mục tiêu: Sinh ra ảnh nhân vật hoàn chỉnh (Khuôn mặt, Ngũ quan mắt-mũi-miệng-tai, Trang phục đạo bào, Pháp bảo, Mái tóc đồng nhất).
• Nền: Nền trắng Studio tinh khiết (#FFFFFF) tương phản cao, giúp AI vẽ chuẩn màu nhân vật không bị ám màu và cực kỳ dễ phân tách.
• Bố cục 5 góc quay: 0° Chính diện, 45° Nghiêng 3/4, 90° Nhìn ngang tai (Profile), 135° Nghiêng sau, 180° Sau lưng + 1 góc soi đỉnh đầu.
• Hướng dẫn tiếp theo: Sau khi tạo được ảnh nhân vật ưng ý ở Bước này, hãy lưu lại ảnh để nạp vào Bước 2 (Bóc tách tóc 4 tầng đồng bộ 100%).`;

    promptJSON = JSON.stringify({
      project: 'Flow-App 2D Motion Comic Engine',
      workflow_step: 'Step 1 - Master Character Turnaround Reference Sheet',
      title: isChibi
        ? 'Bảng Thiết Kế Nhân Vật Hoạt Hình Chibi 5 Góc Quay (Chibi Turnaround Sheet)'
        : 'Bảng Thiết Kế Nhân Vật Gốc Hoàn Chỉnh 5 Góc Quay (Master Character Turnaround)',
      art_style: isChibi ? 'Cute 2D Anime Chibi (2.5 Heads Tall Proportion)' : 'Chinese Xianxia Cultivation Manhua (Crisp Flat 2D Cel Shading)',
      resolution: '4K Ultra High Definition (3840x2160)',
      aspect_ratio: ar === '16:9' ? '16:9 Landscape Model Sheet' : `${ar} Aspect Ratio`,
      background: 'Solid Clean Pure White Studio (#FFFFFF) with high contrast character silhouette',
      character_design: {
        character_type: isChibi ? 'Chibi Anime Character' : 'Standard Proportions Anime Character',
        gender: isMale ? 'Nam Tu Sĩ (Male Cultivator)' : 'Nữ Tiên Tử (Female Heroine)',
        facial_features: {
          eyes: `${eyeShapeEn} with ${eyeColEn} glowing iris`,
          nose: noseEn,
          mouth: mouthEn,
          ears: 'Clean natural human ears'
        },
        costume_and_robes: {
          style: costumeStyleEn,
          color_palette: costumeColEn
        },
        weapon_and_props: propEn,
        hairstyle: {
          length: hairLenEn,
          color: hairColEn,
          texture: hairTexEn,
          accessories: hairAccEn
        }
      },
      turnaround_camera_angles: [
        '0° Front View (Chính diện)',
        '45° Three-Quarter View (Nghiêng 3/4)',
        '90° Full Side Profile View (Nhìn ngang vành tai 90 độ)',
        '135° Back Three-Quarter View (Nghiêng sau 135 độ)',
        '180° Full Back View (Sau lưng toàn cảnh 180 độ)',
        "Top-Down Bird's Eye View (Đỉnh sọ và búi tóc nhìn từ trên xuống)"
      ],
      instructions_for_ai: 'Generate a consistent turnaround model sheet for the EXACT SAME character across all 5 angles plus 1 top-down head view. Maintain identical costume, hair, and facial proportions across all angles.',
      negative_prompt: 'text, letters, words, labels, watermark, deformed anatomy, extra limbs, bad proportions, blurry, 3D clay render, photorealistic realism',
    }, null, 2);

    const negativePrompt = 'text, letters, words, labels, watermark, signature, deformed anatomy, extra limbs, bad proportions, blurry, 3D clay render, photorealistic realism';
    const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;
    const promptGemini = `Hãy đóng vai chuyên gia thiết kế đồ họa 2D/3D.
Vẽ bảng thiết kế tổng thể (Master Turnaround Sheet) ở 5 góc độ.
Phong cách: ${getStyleLabel(config.character_style)}
Tuyệt đối không vẽ: ${negativePrompt}
Tỷ lệ: ${config.aspect_ratio || '16:9'}.`;
    return { promptEnglish, promptVietnamese, promptJSON, promptGemini, gridStructureGuide, negativePrompt, fullCopyText };
  }

  // 1. HAIR MULTI-ANGLE GRID (4 DÃY TÓC TÁCH LỚP KHÔNG CÓ TAI / KHÔNG CÓ DA MẶT)
  if (sheet === 'hair_multi_angle_grid') {
    const ar = config.aspect_ratio || '1:1';
    const hairLenEn =
      config.hair_length === 'custom' && config.custom_hair_length
        ? config.custom_hair_length
        : config.hair_length === 'short'
        ? 'stylish short spiky anime hair'
        : config.hair_length === 'medium_shoulder'
        ? 'shoulder-length medium layered hair'
        : config.hair_length === 'very_long_flowing'
        ? 'celestial floor-length flowing hair cascading down'
        : config.hair_length === 'top_knot_daoist'
        ? 'traditional xianxia daoist high top-knot bun with silver hairpin'
        : 'long waist-length flowing hair with silver hairpin';

    const hairTexEn =
      config.hair_texture === 'custom' && config.custom_hair_texture
        ? config.custom_hair_texture
        : config.hair_texture === 'wavy_curls'
        ? 'wavy curls'
        : config.hair_texture === 'wild_spiky'
        ? 'action spiky locks'
        : config.hair_texture === 'braided_traditional'
        ? 'traditional braided locks'
        : 'straight silky hair';

    const hairColEn =
      config.hair_color === 'custom' && config.custom_hair_color
        ? config.custom_hair_color
        : config.hair_color === 'silver_white'
        ? 'silver white'
        : config.hair_color === 'crimson_red'
        ? 'fiery crimson red'
        : config.hair_color === 'azure_blue'
        ? 'glowing azure cyan'
        : config.hair_color === 'golden_blonde'
        ? 'golden amber'
        : config.hair_color === 'mystic_purple'
        ? 'mystic violet purple'
        : 'jet black';

    const hairAccessoryEn =
      config.hair_accessories === 'custom' && config.custom_hair_accessories
        ? config.custom_hair_accessories
        : config.hair_accessories === 'jade_hairpin'
        ? 'carved jade and silver hairpin'
        : config.hair_accessories === 'golden_crown'
        ? 'ornate golden hair crown'
        : config.hair_accessories === 'flowing_ribbons'
        ? 'silk ribbons'
        : 'none';

    promptEnglish = [
      `masterpiece, best quality, extremely detailed gorgeous hair design sheet, Chinese 3D Donghua animation style, exquisite Wuxia Xianxia aesthetics`,
      `isolated hair turnaround sprite sheet, 4 rows by 5 columns grid layout on ${bgPromptColorEn}`,
      `strictly headless pure hair assets only, transparent invisible mannequin, no human face, no skin, no body`,
      `Column 1: 0° Front View, Column 2: 45° Three-Quarter View, Column 3: 90° Side Profile View, Column 4: 135° Back Three-Quarter View, Column 5: 180° Back View`,
      `Row 1: front fringe bangs only. Row 2: top crown buns and hairpins. Row 3: complete back hair mantle. Row 4: sideburns and nape tendrils`,
      `${hairColEn} hair, ${hairLenEn}, ${hairTexEn}, accessory: ${hairAccessoryEn}`,
      styleTextEn,
      bgTextEn,
      `flat clean cutout sticker asset, crisp borders, modular puppet assembly ready`,
      `--ar ${ar} --no text typography letters font words labels captions numbers writing watermark signature logo characters subtitle calligraphy heading title annotations alphabet stamp frame border-text human face eyes mouth nose skin body ${config.bg_type === 'chroma_green' || !config.bg_type ? 'green ambient light green color spill green glow green reflection green tinted hair neon glow rim light bounce light global illumination' : 'ambient lighting color spill rim light neon glow'}`,
    ].join(', ');

    const jsonSpec = {
      project: 'Flow-App 2D Motion Comic Engine',
      workflow_step: 'Step 2 - Modular Hair 4-Layer Turnaround Grid Decomposition',
      title: 'Bảng Bóc Tách Tóc 4 Tầng Đồng Bộ Theo Nhân Vật Gốc (Decomposed Hair Grid 4x5)',
      art_style: 'Chinese Xianxia Cultivation Manhua (Crisp Flat 2D Cel Shading)',
      resolution: '4K Ultra High Definition (3840x2160)',
      aspect_ratio: ar === '1:1' ? '1:1 Square Grid (4 Rows x 5 Columns)' : `${ar} Aspect Ratio`,
      background: bgTextEn,
      hair_specifications: {
        color: hairColEn,
        length: hairLenEn,
        texture: hairTexEn,
        accessories: hairAccessoryEn,
      },
      strict_constraints: {
        headless_pure_hair_only: 'Strictly pure hair components only. No human face, no skin, no ears, no body.',
        no_text_or_watermark: 'Zero text, zero labels, zero watermark.',
        ample_margins: 'Ample margins around each hair sprite to prevent clipping at borders.',
      },
      grid_layout_structure: {
        total_rows: 4,
        total_columns: 5,
        camera_angles: [
          '0° Front View (Chính diện)',
          '45° Three-Quarter View (Nghiêng 3/4)',
          '90° Full Side Profile View (Nhìn ngang vành tai 90 độ)',
          '135° Back Three-Quarter View (Nghiêng sau 135 độ)',
          '180° Full Back View (Sau lưng toàn cảnh 180 độ)'
        ],
        rows_definition: [
          {
            row: 'Row 1 - Tóc Mái Trước Trán (Front Bangs Fringe - Thuần túy mái trước)',
            description: '5 floating front fringe bangs pieces, strictly no back of head',
            columns: [
              '0° Front Center-Parted Bangs',
              '45° Angled Bangs',
              '90° Side Profile Thin Slice Bangs Alone',
              '135° Fading Diagonal Bangs Edge',
              '180° Empty Green Cell (Hidden behind head)'
            ],
          },
          {
            row: "Row 2 - Đỉnh Chỏm Đầu / Búi Tóc & Trâm Cài Soi Từ Trên Cao Xuống (Top-Down Bird's Eye Crown)",
            description: "5 top crown views looking straight down from above skull top with hairpin rotating 360 degrees",
            columns: [
              '0° Horizontal Hairpin (Ngang 9h-3h)',
              '45° Diagonal Hairpin (Chéo 7h-1h)',
              '90° Vertical Hairpin (Dọc 12h-6h)',
              '135° Rear Diagonal Hairpin',
              '180° Rear Horizontal Hairpin'
            ],
          },
          {
            row: 'Row 3 - Toàn Bộ Tóc Sau Đầu (Thuần túy tóc sau - Zero tóc mái trước)',
            description: '5 complete back-of-head flowing hair streams including upper back skull dome, pure back hair with zero front bangs',
            columns: [
              '0° Front View Two Shoulder Drapes with Open Face Center',
              '45° Three-Quarter Flowing Drape',
              '90° Side Profile Rear Skull & Flowing S-Curve with Hollow Forehead',
              '135° Rear-Quarter Flowing Mantle',
              '180° Full Wide Symmetrical Back Hair Curtain'
            ],
          },
          {
            row: 'Row 4 - Lọn Tóc Mai 2 Bên Má & Tóc Tơ Chân Gáy (Sideburns & Nape Tendrils)',
            description: '5 floating delicate sideburn cheek tendrils and nape wisps, strictly NO ears, NO skin',
            columns: [
              '0° Two Cheek Strands',
              '45° Single Cheek Strand',
              '90° Single Lateral Ear Strand',
              '135° Behind-Ear Lock',
              '180° Central Neck Nape Wisps'
            ],
          },
        ],
      },
      negative_prompt: 'human face, eyes, mouth, nose, human ears, skin, neck, body, text, watermark, blurry, 3D clay render',
    };
    promptJSON = JSON.stringify(jsonSpec, null, 2);

    promptVietnamese = `【 BẢNG SPRITE LINH KIỆN TÓC 4 DÃY × 5 CỘT (TỶ LỆ ${ar}) 】
• Quy tắc bắt buộc: TÁCH SẠCH $100\\%$ DA MẶT, TAI VÀ CỔ. Mỗi ô có lề xanh rộng rãi để không bị cụt ngọn tóc.
• Nền: ${bgTextVi}.

【 CHI TIẾT 4 HÀNG × 5 GÓC XOAY THEO TỶ LỆ ${ar} 】:
🔹 HÀNG 1: TÓC MÁI TRƯỚC TRÁN (Thuần túy mái trước, không dính tóc sau/gáy)
   - Cột 1: 0° Mái chẻ đôi chính diện ôm 2 bên trán
   - Cột 2: 45° Mái nghiêng 3/4 ôm theo mặt xoay
   - Cột 3: 90° Lát cắt mỏng của mái trước nhìn ngang 90°
   - Cột 4: 135° Mép mái khuất dần phía sau chéo
   - Cột 5: 180° Ô rỗng hoàn toàn (vì mặt quay lưng 180° nên không thấy mái trước)

🔹 HÀNG 2: ĐỈNH ĐẦU / BÚI TÓC SOI THẲNG TỪ TRÊN XUỐNG (Camera Top-Down Bird's Eye View)
   - Cột 1: 0° Trâm cài nằm ngang (9h - 3h)
   - Cột 2: 45° Trâm cài xiên chéo (7h - 1h)
   - Cột 3: 90° Trâm cài thẳng đứng (12h - 6h)
   - Cột 4: 135° Trâm cài xiên sau
   - Cột 5: 180° Trâm cài sau đối xứng

🔹 HÀNG 3: TOÀN BỘ TÓC SAU ĐẦU (Thuần túy tóc sau, tuyệt đối không dính tóc mái trước)
   - Cột 1: 0° Vòm đầu sau + 2 suối tóc rủ vai (khoảng giữa rỗng cho mặt/thân)
   - Cột 2: 45° Vòm đầu sau nghiêng 45° + suối tóc đổ dài chéo
   - Cột 3: 90° Nửa sau sọ đầu + suối tóc cong chữ S sau gáy (trán trước rỗng)
   - Cột 4: 135° Mặt sau vòm đầu + suối tóc sau chéo
   - Cột 5: 180° Trọn vẹn vòm đầu sau + suối tóc phủ rộng kín lưng

🔹 HÀNG 4: LỌN TÓC MAI 2 BÊN MÁ & TÓC TƠ GÁY (Thuần túy tóc, không dính tai)
   - 5 góc lọn tóc mai độc lập ôm 2 bên má và chùm tóc tơ sau gáy để diễn hoạt phất phơ.`;

    gridStructureGuide = `📐 Khung Cắt ${ar}: Lưới 4 Hàng × 5 Cột chuẩn 20 ô linh kiện, tự động khớp vào Tab 1 Cắt Lưới.`;
  } else if (sheet === 'eyes_grid') {
    // 2. EYES GRID (CHỈ MẮT & LÔNG MÀY - KHÔNG CÓ DA MẶT / MŨI)
    const eyeCol =
      config.eye_color === 'crimson_red'
        ? 'fiery crimson red iris'
        : config.eye_color === 'golden_amber'
        ? 'glowing golden amber iris'
        : config.eye_color === 'emerald_green'
        ? 'emerald green iris'
        : 'azure blue glowing iris';

    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio sprite sheet layout`,
      `strictly NO face skin, NO head outline, NO nose, NO mouth, isolated floating eyes and eyebrows component assets only`,
      `${eyeCol}, sharp detailed pupil highlights, clean cel shading`,
      `organized in a clean 4-row by 5-column grid with visible spacing:`,
      `[ROW 1 - OPEN EYES ACROSS CAMERA ANGLES]: Col 1: 0° front symmetrical eyes; Col 2: 45° three-quarter left eyes; Col 3: 90° side profile single eye; Col 4: 135° back glance eye; Col 5: extreme low-angle eyes`,
      `[ROW 2 - BLINKING CLOSED EYELIDS]: Col 1: 0° closed blinking eyelid; Col 2: 45° closed eyelid; Col 3: 90° closed profile eyelid; Col 4: gentle smiling closed eyes; Col 5: tightly shut pain eyelid`,
      `[ROW 3 - EMOTION EYE STATES]: Col 1: fierce combat glowing sword intent iris; Col 2: shocked widened pupil; Col 3: smiling happy eyes; Col 4: cold calculating gaze; Col 5: spiritual awakening glowing aura eyes`,
      `[ROW 4 - EYEBROWS ONLY]: Col 1: neutral swordsman eyebrows; Col 2: furrowed angry eyebrows; Col 3: raised curious eyebrow; Col 4: 90° profile eyebrow; Col 5: battle frown eyebrows`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `flat clean cutout sticker asset, zero shadows, --ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE ĐÔI MẮT & CHỚP MẮT (KHÔNG CÓ DA MẶT) (4K - 16:9) 】
• Quy tắc: Chỉ vẽ tròng mắt, mí mắt và lông mày lơ lửng, KHÔNG CÓ MŨI, KHÔNG CÓ KHUNG MẶT.
• Dãy 1: Mắt mở 5 góc camera (0°, 45°, 90° Profile 1 mắt, 135° Liếc sau, Góc ngước).
• Dãy 2: Mí mắt nhắm chớp mắt (0°, 45°, 90°, Nhắm mắt cười, Nhắm nghiến chặt).
• Dãy 3: Cảm xúc mắt (Rực sáng kiếm ý chiến đấu, Sốc giãn đồng tử, Mắt cười híp mí, Ánh mắt lạnh lùng).
• Dãy 4: Lông mày kiếm hiệp theo các góc biểu cảm.`;

    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Dãy x 5 Cột chuẩn 4K, chứa trọn bộ trạng thái mắt chớp và biểu cảm.`;
  } else if (sheet === 'mouth_grid') {
    // 3. MOUTH GRID (CHỈ MÔI & MIỆNG - KHÔNG CÓ CẰM / MŨI)
    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio sprite sheet layout`,
      `strictly NO chin, NO nose, NO face skin, isolated floating lips and mouth expression sprite sheet only for ${config.gender === 'nam' ? 'male' : 'female'}`,
      `organized in a clean 4-row by 5-column grid with visible spacing:`,
      `[ROW 1 - FRONT 0° LIP-SYNC TALK CYCLE]: Col 1: neutral closed mouth 'M'; Col 2: open mouth 'A'; Col 3: round mouth 'O/U'; Col 4: wide mouth 'I/E'; Col 5: gentle confident smile`,
      `[ROW 2 - ANGLED 45° & 90° PROFILE MOUTH]: Col 1: 45° speaking open; Col 2: 45° closed smirk; Col 3: 90° profile side mouth neutral; Col 4: 90° profile speaking open; Col 5: 90° profile shouting mouth`,
      `[ROW 3 - BATTLE & EMOTION MOUTH STATES]: Col 1: roaring combat battle shout with visible teeth; Col 2: fierce grit teeth in pain; Col 3: broad radiant laughter; Col 4: sarcastic cold smirk; Col 5: gasping shock mouth`,
      `[ROW 4 - COMBAT DETAIL & BLOOD TRACE]: Col 1: subtle blood trickle on lip corner; Col 2: holding jade talisman in mouth; Col 3: heavy panting mouth; Col 4: biting lower lip; Col 5: clenched teeth`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `flat clean cutout sticker asset, zero background clutter, --ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE KHẨU HÌNH MIỆNG (KHÔNG CÓ DA MẶT) (4K - 16:9) 】
• Quy tắc: Chỉ vẽ môi và khoang miệng lơ lửng, KHÔNG VẼ CẰM, KHÔNG VẼ DA MẶT.
• Dãy 1: Chu kỳ khẩu hình nói chuyện 0° chính diện (Âm M ngậm, Âm A mở to, Âm O/U tròn, Âm I/E, Cười nhẹ).
• Dãy 2: Khẩu hình nói chuyện góc nghiêng 45° và 90° Profile.
• Dãy 3: Biểu cảm miệng chiến đấu (Thét gầm tung chiêu, Nghiến răng chịu đau, Cười lớn sảng khoái, Cười khẩy).
• Dãy 4: Chi tiết chiến đấu (Vết máu khóe môi, Ngậm phù chú, Thở dốc, Cắn môi dưới).`;

    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Dãy x 5 Cột chuẩn 4K, hỗ trợ trọn bộ Lip-Sync nói chuyện.`;
  } else if (sheet === 'nose_chin_grid') {
    // 4. NOSE, CHIN & EARS GRID
    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio sprite sheet layout`,
      `isolated 2D anime character nose, chin jawline contour, and ear anatomy sprite sheet for ${config.gender === 'nam' ? 'handsome male' : 'female'} cultivator`,
      `organized in 4 clean rows across camera angles:`,
      `[ROW 1 - NOSE BRIDGES ONLY]: Col 1: 0° front subtle nose contour; Col 2: 45° sharp nose bridge; Col 3: 90° full side profile elegant high nose bridge; Col 4: 135° back nose silhouette; Col 5: low-angle nose shadow`,
      `[ROW 2 - CHIN & JAWLINE CONTOURS (NO HAIR)]: Col 1: 0° front clean oval chin; Col 2: 45° defined angular jawline; Col 3: 90° sharp profile chin and jaw column; Col 4: 135° back jaw curve; Col 5: 180° back neck nape`,
      `[ROW 3 - EARS ONLY (NO HAIR)]: Col 1: 0° front symmetrical ears; Col 2: 45° angled ear; Col 3: 90° full side profile ear with intricate inner helix; Col 4: 135° behind-ear angle; Col 5: 180° back of ear lobe with jade tassel earring`,
      `[ROW 4 - FOREHEAD MARKS & FACIAL DETAILS]: Col 1: glowing celestial forehead mark; Col 2: demonic crimson mark; Col 3: battle scar; Col 4: sweat bead; Col 5: third-eye glyph`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `flat clean vector-like cutout, zero shadows, --ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE SỐNG MŨI, CẰM NHỌN 90°, TAI & THẦN ẤN (4K - 16:9) 】
• Dãy 1: Sống mũi 5 góc (0° Thẳng, 45° Nghiêng, 90° Sống mũi cao thẳng kiếm hiệp, 135°).
• Dãy 2: Khung cằm & Quai hàm không tóc (0° Trái xoan, 45° Viền hàm, 90° Cằm nhọn và xương hàm, 180° Sau gáy).
• Dãy 3: Đôi tai trần không tóc (0° Tai thẳng, 45° Nghiêng, 90° Toàn bộ vành tai, 180° Sau tai).
• Dãy 4: Dấu ấn trán phát sáng, Ma vân, Vết sẹo.`;

    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Dãy x 5 Cột chuẩn 4K, hỗ trợ trọn bộ khung cằm 90° và sống mũi.`;
  } else if (sheet === 'costume_grid') {
    // 5. COSTUME GRID (HOLLOW CLOTHES - NO BODY INSIDE)
    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio sprite sheet layout`,
      `hollow clothes only, empty traditional xianxia daoist robes, strictly NO human body inside, no head, no legs`,
      `color theme: ${config.color_theme || 'celestial azure blue with gold trim and white silk inner layer'}`,
      `organized in 4 rows across 4 camera angles (0° Front, 45° 3/4 View, 90° Side Profile, 180° Back View):`,
      `[ROW 1 - HOLLOW CHEST ROBE & COLLAR]: 0° crossed collar; 45° angled chest embroidery; 90° profile chest; 180° back robe with central jade seam`,
      `[ROW 2 - LOWER ROBE SKIRT]: 0° flowing robe skirt; 45° flaring cloth; 90° profile skirt folds; 180° back cascading robe hem`,
      `[ROW 3 - DETACHED SLEEVES]: 0° hanging wide sleeves; 45° wind-blown sleeves; 90° profile sleeve; 180° back shoulder mantle`,
      `[ROW 4 - WAIST SASH & JADE PENDANT]: 0° golden sash belt with jade pendant; 45° sash knot; 90° side belt; 180° back ribbon bow tie`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `flat clean cutout sticker asset, zero background clutter, --ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE TRANG PHỤC ĐẠO BÀO RỖNG RUỘT (4K - 16:9) 】
• Quy tắc: Trang phục rỗng ruột (không có người/da thịt bên trong) để gắn đè lên thân rối 2D.
• Dãy 1: Cổ áo & Giáp ngực (0°, 45°, 90°, 180° Lưng áo có đường may ngọc bích).
• Dãy 2: Tà áo dưới & Vạt váy lụa (0°, 45°, 90°, 180°).
• Dãy 3: Ống tay áo rộng tách rời (0°, 45°, 90°, 180°).
• Dãy 4: Thắt lưng, Dải lụa bay & Ngọc bội hộ thân.`;

    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Dãy x 4 Cột chuẩn 4K.`;
  } else if (sheet === 'weapons_grid') {
    // 6. WEAPONS GRID
    const wepType =
      config.weapon_type === 'broadsword'
        ? 'ornate cultivation heavy broadsword'
        : config.weapon_type === 'staff'
        ? 'celestial daoist staff'
        : config.weapon_type === 'feather_fan'
        ? 'glowing spiritual feather fan'
        : 'celestial glowing flying sword';
    const wepElem =
      config.weapon_element === 'crimson_flame'
        ? 'blazing crimson fire aura'
        : config.weapon_element === 'frost_ice'
        ? 'crystalline frost ice mist'
        : config.weapon_element === 'golden_radiance'
        ? 'shimmering golden sun radiance'
        : 'crackling azure lightning energy';

    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio sprite sheet layout`,
      `isolated 2D xianxia weapon and prop sprite sheet, ${wepType}`,
      `${wepElem}, intricate gold runes, crystal sharp edge`,
      `organized in 4 clean rows with visible spacing:`,
      `[ROW 1 - FULL WEAPON ACROSS ANGLES]: Col 1: 0° upright front blade; Col 2: 45° diagonal perspective; Col 3: 90° ultra-thin side edge; Col 4: 180° back scabbard angle; Col 5: horizontal flying weapon`,
      `[ROW 2 - HILT, GUARD & SCABBARD]: Col 1: carved jade dragon hilt; Col 2: ornate golden guard cross; Col 3: scabbard with silk tassels; Col 4: pommel ring with red ribbon; Col 5: sword sheath opening`,
      `[ROW 3 - ENERGY AURA & SLASH BLADES]: Col 1: glowing spiritual energy blade; Col 2: curved crescent slash wave VFX; Col 3: piercing thrust aura tip; Col 4: spinning blade formation; Col 5: elemental sparks`,
      `[ROW 4 - HAND GRIP POSES & FLOATING FORMATIONS]: Col 1: hand gripping hilt; Col 2: reverse back-hand grip; Col 3: two-finger sword seal; Col 4: telekinetic flying sword; Col 5: dual wield pair`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `flat clean cutout sticker asset, zero background shadows, --ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE VŨ KHÍ & PHÁP BẢO (4K - 16:9) 】
• Dãy 1: Toàn thân vũ khí nguyên cây (0°, 45°, 90°, 180°, Phi kiếm ngang).
• Dãy 2: Chuôi kiếm rồng chạm ngọc, Chắn tay vàng kim, Bao kiếm có dải lụa đỏ.
• Dãy 3: Kiếm khí & Vệt chém phát sáng.
• Dãy 4: Tư thế tay cầm & Ngự kiếm phi hành.`;

    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Dãy x 5 Cột chuẩn 4K.`;
  } else if (sheet === 'limbs_hands_grid') {
    // 7. LIMBS & HAND SEALS GRID
    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio sprite sheet layout`,
      `isolated 2D anime character limbs, hand seals and leg boots sprite sheet for ${config.gender === 'nam' ? 'male' : 'female'}`,
      `organized in 4 clean rows on uniform background:`,
      `[ROW 1 - ARMS ACROSS ANGLES]: Upper arms and forearms across 0°, 45°, 90°, 180° angles`,
      `[ROW 2 - HAND SEALS ONLY]: Col 1: open relaxed hand; Col 2: tight fist; Col 3: two-finger daoist sword seal gesture; Col 4: open palm spell blast; Col 5: hand gripping weapon hilt`,
      `[ROW 3 - LEGS IN COMBAT POSES]: Legs in standing, bending, jumping, and sweeping kicks`,
      `[ROW 4 - BOOTS ACROSS ANGLES]: Martial arts boots across 0°, 45°, 90°, 180° back heel`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `flat clean cutout sticker asset, zero shadows, --ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE TỨ CHI & BÀN TAY BẮT QUYẾT (4K - 16:9) 】
• Dãy 1: Cánh tay & Cẳng tay các góc quay (0°, 45°, 90°, 180°).
• Dãy 2: Bàn tay các tư thế (Bàn tay mở, Nắm đấm, Bắt quyết kiếm ấn, Phóng chưởng, Cầm kiếm).
• Dãy 3: Cẳng chân theo các thế tấn.
• Dãy 4: Hài / Giày chiến đấu cổ trang các góc (0°, 45°, 90°, 180°).`;

    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Dãy x 5 Cột chuẩn 4K.`;
  } else if (sheet === 'body_turnaround_grid') {
    // 8. FULL CHARACTER MASTER TURNAROUND SHEET
    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio character turnaround sprite sheet`,
      `modular 2D puppet cutout components for ${config.gender === 'nam' ? 'male' : 'female'} character`,
      `color theme: ${config.color_theme || 'celestial cyan blue and gold trim'}`,
      `organized in 4 clean rows across 4 turnaround camera angles (0° Front, 45° 3/4 View, 90° Side Profile, 180° Back View):`,
      `[ROW 1 - HEAD & HAIR]: Head bases and layered hair for 0°, 45°, 90°, 180° back view`,
      `[ROW 2 - TORSO & ROBE OUTFIT]: Traditional xianxia daoist robes across 0°, 45°, 90°, 180° back seam`,
      `[ROW 3 - LIMBS & SLEEVES]: Arms, flowing sleeves, legs and boots for all angles`,
      `[ROW 4 - WEAPON & ACCESSORIES]: Glowing spiritual sword, scabbard, sash belt, jade pendant`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `flat clean cutout puppet pieces, no overlapping parts, --ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG LINH KIỆN TOÀN THÂN & TRANG PHỤC 4 HƯỚNG (4K - 16:9) 】
• Dãy 1: Đầu & Tóc (0° Trước, 45° Nghiêng, 90° Ngang, 180° Sau lưng).
• Dãy 2: Thân & Đạo Bào Trang Phục (0°, 45°, 90°, 180°).
• Dãy 3: Tứ Chi (0°, 45°, 90°, 180°).
• Dãy 4: Vũ Khí & Phụ Kiện (0°, 45°, 90°, 180°).`;

    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Dãy x 4 Cột chuẩn 4K.`;
  } else {
    // SINGLE PART
    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio`,
      `isolated 2D puppet component for ${config.part_type}`,
      `color theme: ${config.color_theme || 'vibrant'}`,
      config.special_features ? `features: ${config.special_features}` : '',
      noTextEn,
      styleTextEn,
      bgTextEn,
      `flat clean vector-like cutout, zero shadows, --ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 LINH KIỆN ĐƠN LẺ (${config.part_type}) (4K - 16:9) 】\n• Phong cách: ${config.character_style}\n• Màu sắc: ${config.color_theme}\n• Nền tách sẵn 4K sạch sẽ.`;
    gridStructureGuide = `📐 Khung Cắt Đơn: Kích thước 4K chuẩn 16:9.`;
  }

  // Ultra strict Negative Prompt for Banana Pro / Midjourney / SD
  const negativePrompt =
    'text, letters, words, writing, captions, labels, watermark, signature, numbers, alphabet, font, row names, column numbers, annotations, border text, letters on image, human ear, ear, skin, face, eyes, nose, mannequin head, full wig, front bangs on row 3, front bangs on back hair, forehead hair on third row, forehead bangs on rear hair layer, different hairstyles on rows, complex background, gradient background, drop shadow on background, anti-aliased green halo, perspective distortion, blurry textures, 3D photorealistic render, low quality, noise, messy borders, cropped off frame';

  if (!promptJSON) {
    promptJSON = JSON.stringify(
      {
        project: 'Flow-App 2D Motion Comic Engine',
        part_type: config.part_type,
        sheet_type: sheet,
        style: config.character_style,
        gender: config.gender,
        prompt: promptEnglish,
        negative_prompt: negativePrompt,
      },
      null,
      2
    );
  }

  if (!promptJSON) {
    promptJSON = JSON.stringify(
      {
        project: 'Flow-App 2D Motion Comic Engine',
        title: getSheetTypeLabel(sheet),
        art_style: getStyleLabel(config.character_style),
        gender: getGenderLabel(config.gender),
        prompt: promptEnglish,
        negative_prompt: negativePrompt,
      },
      null,
      2
    );
  }

  const promptGemini = `Hãy đóng vai một chuyên gia thiết kế nguyên liệu (Asset) đồ họa 2D/3D.
Bạn hãy vẽ ra một bức ảnh theo đúng các yêu cầu cực kỳ khắt khe sau đây:

1. CHỦ ĐỀ CHÍNH: ${getSheetTypeLabel(sheet)}
2. PHONG CÁCH ĐỒ HỌA: ${getStyleLabel(config.character_style)}.
3. THÔNG TIN NHÂN VẬT: Giới tính ${getGenderLabel(config.gender)}. ${config.color_theme ? `Màu chủ đạo: ${config.color_theme}.` : ''}
4. YÊU CẦU NỀN TRỐNG (RẤT QUAN TRỌNG): Vẽ trên nền màu đồng nhất là ${config.bg_type === 'chroma_green' ? 'Xanh lá Chroma Green (#00FF00)' : config.bg_type === 'pure_white' ? 'Trắng tinh (#FFFFFF)' : 'Màu trơn'}. Không được vẽ bóng đổ.
5. CẤU TRÚC LƯỚI / ẢNH:
${gridStructureGuide}
6. NHỮNG ĐIỀU TUYỆT ĐỐI CẤM (NEGATIVE PROMPT): 
Bạn tuyệt đối không được phép vẽ: ${negativePrompt}.
LƯU Ý ĐẶC BIỆT: Nếu đây là bảng bóc tách linh kiện (như Tóc, Mắt, Áo), tuyệt đối KHÔNG được vẽ da mặt hay cơ thể người bên trong. Chỉ vẽ linh kiện lơ lửng tách rời.

Hãy sinh ra bức ảnh với tỷ lệ ${config.aspect_ratio || '16:9'} bám sát hoàn toàn mô tả trên!`;

  const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;

  return {
    promptEnglish,
    promptVietnamese,
    promptJSON,
    promptGemini,
    negativePrompt,
    gridStructureGuide,
    fullCopyText,
  };
};
