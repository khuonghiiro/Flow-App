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
  khuon_mat_no_face: { label: 'Mặt Trần (No Face)', defaultZ: 7, defaultZDepth3D: 9, defaultPivot: [0.5, 0.5], defaultOffset: [0, -135] },
  mat: { label: 'Đôi Mắt Tổng Hợp', defaultZ: 8, defaultZDepth3D: 10, defaultPivot: [0.5, 0.5], defaultOffset: [0, -140] },
  trong_trang: { label: 'Tròng Trắng (Sclera)', defaultZ: 7.5, defaultZDepth3D: 9.5, defaultPivot: [0.5, 0.5], defaultOffset: [0, -140] },
  trong_den_iris: { label: 'Mống Mắt & Con Ngươi (Iris)', defaultZ: 8, defaultZDepth3D: 10, defaultPivot: [0.5, 0.5], defaultOffset: [0, -140] },
  diem_sang_mat: { label: 'Điểm Sáng / Highlight Mắt', defaultZ: 8.5, defaultZDepth3D: 10.2, defaultPivot: [0.5, 0.5], defaultOffset: [0, -140] },
  mi_mat: { label: 'Mi Mắt & Chớp Mắt', defaultZ: 9, defaultZDepth3D: 10.8, defaultPivot: [0.5, 0.5], defaultOffset: [0, -140] },
  long_may: { label: 'Lông Mày', defaultZ: 8, defaultZDepth3D: 10.5, defaultPivot: [0.5, 0.5], defaultOffset: [0, -145] },
  mui: { label: 'Sống Mũi', defaultZ: 8, defaultZDepth3D: 11, defaultPivot: [0.5, 0.5], defaultOffset: [0, -125] },
  doi_tai: { label: 'Đôi Tai', defaultZ: 7, defaultZDepth3D: 8.5, defaultPivot: [0.5, 0.5], defaultOffset: [0, -135] },
  mui_tai: { label: 'Mũi & Đôi Tai', defaultZ: 8, defaultZDepth3D: 11, defaultPivot: [0.5, 0.5], defaultOffset: [0, -125] },
  mieng: { label: 'Miệng (Khẩu hình)', defaultZ: 8, defaultZDepth3D: 10, defaultPivot: [0.5, 0.5], defaultOffset: [0, -110] },
  toc_truoc: { label: 'Tóc Mái Trước', defaultZ: 9, defaultZDepth3D: 12, defaultPivot: [0.5, 0.1], defaultOffset: [0, -170] },
  canh_tay_phai: { label: 'Cánh Tay Phải', defaultZ: 10, defaultZDepth3D: 5, defaultPivot: [0.2, 0.15], defaultOffset: [55, -60] },
  cang_tay_phai: { label: 'Cẳng Tay Phải', defaultZ: 10, defaultZDepth3D: 6, defaultPivot: [0.2, 0.2], defaultOffset: [65, 10] },
  ban_tay_phai: { label: 'Bàn Tay Phải', defaultZ: 10, defaultZDepth3D: 7, defaultPivot: [0.5, 0.2], defaultOffset: [70, 50] },
  tay_chan: { label: 'Tứ Chi & Bàn Tay', defaultZ: 10, defaultZDepth3D: 7, defaultPivot: [0.5, 0.2], defaultOffset: [0, 20] },
  ao_choang: { label: 'Áo Choàng / Tà Áo', defaultZ: 1, defaultZDepth3D: 0.5, defaultPivot: [0.5, 0.1], defaultOffset: [0, 20] },
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

export const getStyleLabel = (s: string, custom?: string) => {
  if (custom?.trim()) return custom.trim();
  if (!s) return '🌸 Anime Nhật Bản Chuẩn Đẹp';
  switch(s) {
    case 'anime_japan': return '🌸 Anime Nhật Bản Chuẩn Đẹp (Mắt to, sắc nét)';
    case 'auto_detect': return '🤖 AI Tự Hiểu & Sáng Tạo (Anime / Tự Nhiên)';
    case 'chibi': return '🌟 Anime Chibi Đáng Yêu (Đầu to mắt to 2.5D)';
    case 'custom': return custom?.trim() || 'Phong cách tùy chỉnh';
    default: return s;
  }
};

export const getGenderLabel = (g: string) => {
  return g === 'nam' ? 'Nam' : g === 'nu' ? 'Nữ' : 'Chung';
};

export const getEyeShapeLabels = (shape?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!shape) return { vi: 'Mắt anime to tròn sắc nét', en: 'large clear beautiful anime eyes' };
  switch (shape) {
    case 'chibi_sparkling_starry': return { vi: 'Mắt tròn to long lanh ánh sao (Anime)', en: 'giant sparkling round starry anime eyes with glossy highlights' };
    case 'chibi_happy_crescent': return { vi: 'Mắt cười híp cong lưỡi liềm', en: 'happy joyful crescent-shaped anime curved wink eyes' };
    case 'chibi_pouty_teary': return { vi: 'Mắt ươn ướt cún con dễ thương', en: 'cute puppy teary glossy anime eyes' };
    case 'large_clear': return { vi: 'Mắt to tròn trong sáng tinh anh (Anime)', en: 'large beautiful expressive anime eyes with clean pupil highlights' };
    case 'sharp_phoenix': return { vi: 'Mắt phượng sắc sảo cuốn hút', en: 'sharp defined anime eyes with prominent eyelashes' };
    case 'cold_swordsman': return { vi: 'Mắt kiếm khách kiên định lạnh lùng', en: 'cold determined swordsman anime eyes' };
    case 'fox_alluring': return { vi: 'Mắt hồ ly quyến rũ ma mị', en: 'alluring sleek anime eyes' };
    default: return { vi: shape, en: shape };
  }
};

export const getEyeColorLabels = (col?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!col) return { vi: 'Xanh lam ngọc phát sáng', en: 'azure cyan glowing iris' };
  switch (col) {
    case 'azure_blue': return { vi: 'Xanh lam ngọc phát sáng', en: 'glowing azure cyan iris' };
    case 'chibi_sweet_pink': return { vi: 'Hồng dâu tây kẹo ngọt', en: 'sweet strawberry pink iris' };
    case 'golden_amber': return { vi: 'Vàng mật ong hổ phách', en: 'golden amber glowing iris' };
    case 'emerald_green': return { vi: 'Xanh ngọc lục bảo', en: 'emerald green vibrant iris' };
    case 'crimson_red': return { vi: 'Đỏ rực hỏa diệm', en: 'crimson red fiery iris' };
    case 'mystic_purple': return { vi: 'Tím tử linh huyền ảo', en: 'mystic violet purple iris' };
    case 'obsidian_black': return { vi: 'Đen tuyền sâu thẳm', en: 'obsidian black deep glossy iris' };
    default: return { vi: col, en: col };
  }
};

export const getNoseLabels = (nose?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!nose) return { vi: 'Sống mũi thẳng cao thanh tú', en: 'tall straight delicate anime nose bridge' };
  switch (nose) {
    case 'chibi_tiny_dot': return { vi: 'Mũi chấm nhỏ xíu anime', en: 'cute microscopic dot anime nose' };
    case 'chibi_no_nose': return { vi: 'Ẩn mũi (Anime không vẽ mũi)', en: 'classic anime style with no visible nose' };
    case 'small_delicate': return { vi: 'Mũi nhỏ nhắn thanh tú', en: 'small delicate anime nose' };
    case 'sharp_defined': return { vi: 'Sống mũi cao sắc nét', en: 'sharp refined anime nose bridge' };
    case 'straight_high_bridge': return { vi: 'Sống mũi thẳng cao thanh tú', en: 'tall straight delicate anime nose bridge' };
    default: return { vi: nose, en: nose };
  }
};

export const getMouthLabels = (mouth?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!mouth) return { vi: 'Cười nhếch môi tự tin', en: 'confident subtle smirk' };
  switch (mouth) {
    case 'chibi_cat_mouth': return { vi: 'Miệng mèo tinh nghịch :3', en: 'cute playful cat-like :3 smile mouth' };
    case 'chibi_surprised_o': return { vi: 'Miệng chữ O ngơ ngác đáng yêu', en: 'cute open surprised circle O-mouth' };
    case 'chibi_puffed_cheek': return { vi: 'Phồng má ngậm bánh bao dễ thương', en: 'cute chubby puffed cheeks with pouty mouth' };
    case 'chibi_big_smile': return { vi: 'Cười tít mắt hớn hở', en: 'big joyful open happy anime smile' };
    case 'confident_smirk': return { vi: 'Cười nhếch môi tự tin', en: 'confident subtle smirk' };
    case 'gentle_smile': return { vi: 'Nụ cười dịu dàng tươi tắn', en: 'gentle serene sweet anime smile' };
    case 'battle_roar': return { vi: 'Nghiêm nghị tập trung chiến đấu', en: 'determined focused expression' };
    default: return { vi: mouth, en: mouth };
  }
};

export const getCostumeLabels = (costume?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!costume) return { vi: 'Đạo bào tu tiên thướt tha cách tân', en: 'stylish anime costume with flowing sleeves' };
  switch (costume) {
    case 'dao_bao_tien_hiep': return { vi: 'Đạo bào tu tiên thướt tha cách tân', en: 'stylish modern anime costume with flowing sleeves and ornamental trim' };
    case 'bach_y_tien_tu': return { vi: 'Bạch y thanh khiết xếp ly thướt tha', en: 'pure white elegant flowing anime robes with delicate ribbons' };
    case 'kiem_khach_ao_vai': return { vi: 'Trang phục kiếm khách lãng tử phong trần', en: 'stylish anime wanderer adventurer outfit with leather accents' };
    case 'hac_y_ma_dao': return { vi: 'Hắc y huyền bí viền đỏ quyền lực', en: 'dark stylish anime coat with crimson flame trim' };
    case 'hoang_toc_kim_bao': return { vi: 'Kim bào hoàng gia thêu chỉ vàng quý phái', en: 'imperial golden embroidered anime coat with luxury details' };
    default: return { vi: costume, en: costume };
  }
};

export const getPropLabels = (prop?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!prop) return { vi: 'Phi kiếm phát sáng linh lực lam ngọc', en: 'spirit sword glowing with azure energy' };
  switch (prop) {
    case 'flying_sword': return { vi: 'Phi kiếm phát sáng linh lực lam ngọc', en: 'spirit sword glowing with azure energy' };
    case 'feather_fan': return { vi: 'Quạt lông vũ thái cực tiên gia', en: 'celestial feather fan' };
    case 'talisman_scrolls': return { vi: 'Cuộn bùa chú phù lục phát quang', en: 'glowing spiritual talisman scrolls' };
    case 'gourd_wine': return { vi: 'Hồ lô tiên tửu ngọc bích', en: 'celestial wine gourd' };
    case 'jade_hairpin': return { vi: 'Trâm cài ngọc bích đính dải lụa', en: 'carved jade hairpin with silk ribbons' };
    default: return { vi: prop, en: prop };
  }
};

export const getHairLengthLabels = (len?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!len) return { vi: 'Tóc dài ngang lưng suôn mượt', en: 'long waist-length flowing hair' };
  switch (len) {
    case 'short': return { vi: 'Tóc ngắn cá tính năng động', en: 'stylish short anime layered hair' };
    case 'medium_shoulder': return { vi: 'Tóc ngang vai tỉa tầng bồng bềnh', en: 'shoulder-length medium layered anime hair' };
    case 'very_long_flowing': return { vi: 'Tóc dài thướt tha chấm gót', en: 'floor-length flowing hair cascading down' };
    case 'top_knot_daoist': return { vi: 'Búi tóc củ tỏi đỉnh đầu cài trâm', en: 'traditional high top-knot bun with hairpin' };
    case 'long_waist': return { vi: 'Tóc dài ngang lưng suôn mượt', en: 'long waist-length flowing hair' };
    default: return { vi: len, en: len };
  }
};

export const getHairColorLabels = (col?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!col) return { vi: 'Đen tuyền óng ả', en: 'jet black silky' };
  switch (col) {
    case 'jet_black': return { vi: 'Đen tuyền óng ả', en: 'jet black silky' };
    case 'silver_white': return { vi: 'Bạch kim (Trắng bạc lấp lánh)', en: 'silver white luminous' };
    case 'crimson_red': return { vi: 'Đỏ rực hỏa diệm', en: 'fiery crimson red' };
    case 'azure_blue': return { vi: 'Xanh lam ngọc phát sáng', en: 'glowing azure cyan' };
    case 'golden_blonde': return { vi: 'Vàng kim ánh dương', en: 'golden blonde radiant' };
    case 'mystic_purple': return { vi: 'Tím tử linh huyền bí', en: 'mystic violet purple' };
    default: return { vi: col, en: col };
  }
};

export const getHairTextureLabels = (tex?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!tex) return { vi: 'Thẳng mượt như suối lụa', en: 'straight silky smooth strands' };
  switch (tex) {
    case 'wavy_curls': return { vi: 'Xoăn sóng bồng bềnh lượn lờ', en: 'wavy voluminous locks' };
    case 'wild_spiky': return { vi: 'Đánh rối tỉa nhọn năng động', en: 'action spiky locks' };
    case 'braided_traditional': return { vi: 'Tết bím cầu kỳ cách tân', en: 'traditional braided anime strands' };
    case 'straight_silky': return { vi: 'Thẳng mượt như suối lụa', en: 'straight silky smooth strands' };
    default: return { vi: tex, en: tex };
  }
};

export const getHairAccessoryLabels = (acc?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!acc) return { vi: 'Trâm cài ngọc bích đính dải lụa', en: 'carved jade hairpin with delicate silk ribbons' };
  switch (acc) {
    case 'jade_hairpin': return { vi: 'Trâm cài ngọc bích đính dải lụa', en: 'carved jade hairpin with delicate silk ribbons' };
    case 'golden_crown': return { vi: 'Vương miện vàng kim tinh xảo', en: 'ornate golden crown' };
    case 'flowing_ribbons': return { vi: 'Dải lụa mềm bay phất phơ', en: 'fluttering silk ribbons' };
    case 'none': return { vi: 'Không có phụ kiện', en: 'none' };
    default: return { vi: acc, en: acc };
  }
};

export const buildAIPromptForPart = (config: AIPartPromptConfig): AIPromptResult => {
  const sheet = config.sheet_type || 'hair_multi_angle_grid';
  let bgTextEn = 'isolated on solid pure flat white background #FFFFFF, clean flat cutout, zero drop shadows, no ambient occlusion, strictly neutral lighting';
  let bgTextVi = 'Nền trắng tinh khiết (#FFFFFF) phẳng 1 màu, viền tương phản cao dễ cắt';
  let bgPromptColorEn = 'solid pure white background #FFFFFF';
  
  if (config.bg_type === 'chroma_green') {
    bgTextEn = 'isolated on solid flat pure chroma green background #00FF00, uniform flat single color, high contrast edge, strictly flat neutral unlit shading, absolutely zero ambient color spill, zero green fringe on edges, no bounce light, no rim lighting, no global illumination, pure matte colors';
    bgTextVi = 'Nền xanh lá Chroma Green (#00FF00) phẳng 1 màu dứt khoát để cắt phông tức thì';
    bgPromptColorEn = 'solid pure chroma green background #00FF00';
  } else if (config.bg_type === 'chroma_gray') {
    bgTextEn = 'isolated on solid flat neutral dark gray background #333333, uniform flat single color, high contrast edge, zero shadows, no color spill, perfect for white hair extraction';
    bgTextVi = 'Nền xám đậm trung tính (#333333) không bóng đổ, chuẩn bóc tách tóc trắng/bạc';
    bgPromptColorEn = 'solid neutral dark gray background #333333';
  } else if (config.bg_type === 'pure_black') {
    bgTextEn = 'isolated on solid flat pure black background #000000, uniform flat single color, high contrast edge, zero shadows';
    bgTextVi = 'Nền đen tuyền (#000000) không bóng đổ, dùng cho chi tiết phát sáng';
    bgPromptColorEn = 'solid pure black background #000000';
  }

  const noTextEn =
    'clean graphic asset only, strictly NO text, NO letters, NO words, NO numbers, NO watermark, NO labels, NO typography, NO captions, NO annotations, NO border writing';

  const styleTextEn =
    config.character_style === 'custom' && config.custom_character_style?.trim()
      ? config.custom_character_style.trim()
      : config.character_style === 'chibi'
      ? 'cute 2D anime chibi character artstyle, adorable 2-head to 3-head kawaii proportions, giant sparkling expressive anime eyes, chubby cheeks, soft cute face, clean bold outlines, vibrant flat cel shading, authentic Japanese chibi anime illustration'
      : 'masterpiece 2D Japanese anime character artstyle, Kyoto Animation and Ufotable aesthetic, large gorgeous sparkling expressive anime eyes, detailed pupil reflections with multiple light sparkles, clean thick anime lash lines, aesthetic beautiful anime facial proportions, delicate cute anime nose and mouth, sharp crisp anime lineart, flat vibrant cel shading, high quality digital anime illustration';

  const styleLabelVi = getStyleLabel(config.character_style, config.custom_character_style);

  // Label resolvers
  const eyeShapeInfo = getEyeShapeLabels(config.eye_shape, config.custom_eye_shape);
  const eyeColInfo = getEyeColorLabels(config.eye_color, config.custom_eye_color);
  const noseInfo = getNoseLabels(config.nose_shape, config.custom_nose_shape);
  const mouthInfo = getMouthLabels(config.mouth_style, config.custom_mouth_style);
  const costumeInfo = getCostumeLabels(config.costume_style, config.custom_costume_style);
  const costumeColorVi = config.costume_color?.trim() || 'Xanh lam phối trắng viền kim tuyến';
  const propInfo = getPropLabels(config.prop_item, config.custom_prop_item);
  const hairLenInfo = getHairLengthLabels(config.hair_length, config.custom_hair_length);
  const hairColInfo = getHairColorLabels(config.hair_color, config.custom_hair_color);
  const hairTexInfo = getHairTextureLabels(config.hair_texture, config.custom_hair_texture);
  const hairAccInfo = getHairAccessoryLabels(config.hair_accessories, config.custom_hair_accessories);

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
        ? 'cute adorable anime chibi boy protagonist, giant sparkling anime eyes, compact kawaii proportions, 2.5 heads tall'
        : 'cute adorable anime chibi girl fairy heroine, giant sparkling anime eyes, compact kawaii proportions, 2.5 heads tall'
      : isMale
      ? 'handsome attractive 2D Japanese anime male protagonist, large clear expressive anime eyes with sparkling highlights, aesthetic sharp anime jawline, beautiful facial features'
      : 'beautiful charming 2D Japanese anime female heroine, large gorgeous sparkling expressive anime eyes with shining reflections, delicate cute anime features';

    promptEnglish = [
      `masterpiece, best quality, ultra detailed 4k resolution, authentic 2D Japanese anime character turnaround model sheet of ONE SINGLE character`,
      styleTextEn,
      `consistent 2D anime character depicted from 5 angles: 0° front view, 45° three-quarter view, 90° side profile, 135° back three-quarter view, 180° rear back view, plus top-down bird's-eye head crown view`,
      `${genderTextEn}, extremely handsome/gorgeous, beautiful aesthetic anime facial structure, consistent facial features: ${eyeShapeInfo.en} with glowing ${eyeColInfo.en}, ${noseInfo.en}, ${mouthInfo.en}, clean defined ears`,
      `attire: ${costumeInfo.en}, color theme: ${costumeColorVi}, wide flowing sleeves, flutter ribbons in breeze`,
      `accessories and weapons: ${propInfo.en}`,
      `hairstyle: ${hairColInfo.en} ${hairLenInfo.en}, ${hairTexInfo.en}, styled with ${hairAccInfo.en}`,
      'solid pure clean white studio background #FFFFFF with high contrast character silhouette, zero clutter, no background scenery',
      `uniform soft studio lighting, crisp cinematic shading, 2D anime cel shaded character concept turnaround sheet`,
      `strictly NO realistic human face, strictly NO small realistic eyes, strictly NO 3D CGI render, strictly NO photorealism`,
      `--ar ${ar} --no realistic human face small realistic eyes realistic eyes 3d cgi render photorealism live-action western comic ugly anatomy deformed face muddy colors bad eyes realistic skin texture realistic wrinkles dull eyes pores text typography letters font words labels captions numbers writing watermark signature logo characters subtitle calligraphy heading title annotations alphabet stamp frame border-text`,
    ].join(', ');

    promptVietnamese = `【 BẢNG THIẾT KẾ NHÂN VẬT ANIME GỐC ĐA GÓC QUAY (CHARACTER TURNAROUND SHEET - 16:9) 】
• Phong cách: Anime 2D chuẩn Nhật Bản (Kyoto Animation / Ufotable) — Mắt to tròn long lanh có điểm sáng phản chiếu, khuôn mặt thanh tú, nét vẽ 2D sắc nét (Tuyệt đối không vẽ mặt tả thực hay 3D).
• Nhân vật: ${isMale ? 'Nam hiệp khách' : 'Nữ hiệp sĩ/tiên nữ'} (${isChibi ? 'Chibi đáng yêu 2.5 đầu' : 'Anime chuẩn nét đẹp'})
• Khuôn mặt & Ngũ quan: Dáng mắt (${eyeShapeInfo.vi}), Màu mắt (${eyeColInfo.vi}), Sống mũi (${noseInfo.vi}), Khẩu hình (${mouthInfo.vi})
• Trang phục: ${costumeInfo.vi} (Màu sắc & Họa tiết: ${costumeColorVi})
• Mái tóc: Màu (${hairColInfo.vi}), Độ dài (${hairLenInfo.vi}), Chất tóc (${hairTexInfo.vi}), Phụ kiện/Trâm cài (${hairAccInfo.vi})
• Pháp bảo / Vật phẩm cầm: ${propInfo.vi}
• Phông nền: ${bgTextVi}
• Bố cục 5 góc quay: 0° Chính diện (Front), 45° Nghiêng 3/4 (Three-Quarter), 90° Nhìn ngang (Side Profile), 135° Nghiêng sau, 180° Sau lưng (Back) + 1 góc soi đỉnh đầu (Top-Down).
• Lệnh điều khiển AI: Vẽ bảng thiết kế mẫu 2D Anime của ĐÚNG CÙNG 1 NHÂN VẬT ở tất cả các góc quay trên, giữ nguyên 100% trang phục, kiểu tóc và tỷ lệ khuôn mặt.`;

    promptJSON = JSON.stringify({
      project: 'Flow-App 2D Character Generator',
      workflow_step: 'Step 1 - Master Character Turnaround Reference Sheet',
      title: isChibi
        ? 'Bảng Thiết Kế Nhân Vật Hoạt Hình Chibi 5 Góc Quay (Chibi Turnaround Sheet)'
        : 'Bảng Thiết Kế Nhân Vật Anime Nhật Bản 5 Góc Quay (Anime Turnaround Sheet)',
      art_style: '2D Japanese Anime Cel Shading (Kyoto Animation / Ufotable)',
      resolution: '4K Ultra High Definition (3840x2160)',
      aspect_ratio: '16:9 Landscape Model Sheet',
      background: 'Solid Clean Pure White Studio (#FFFFFF) with high contrast character silhouette',
      character_design: {
        character_type: isChibi ? 'Chibi Anime Character' : '2D Japanese Anime Character',
        gender: isMale ? 'Nam (Male)' : 'Nữ (Female)',
        facial_features: {
          eyes: `Large expressive anime eyes - ${eyeShapeInfo.vi} (${eyeShapeInfo.en}) - Màu ${eyeColInfo.vi}`,
          nose: `${noseInfo.vi} (${noseInfo.en})`,
          mouth: `${mouthInfo.vi} (${mouthInfo.en})`,
          ears: 'Clean natural anime ears'
        },
        costume_and_robes: {
          style: costumeInfo.vi,
          color_palette: costumeColorVi
        },
        weapon_and_props: propInfo.vi,
        hairstyle: {
          length: hairLenInfo.vi,
          color: hairColInfo.vi,
          texture: hairTexInfo.vi,
          accessories: hairAccInfo.vi
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
      instructions_for_ai: 'Generate an authentic 2D anime turnaround model sheet for the EXACT SAME character across all 5 angles plus 1 top-down head view. Maintain large expressive sparkling anime eyes, clean lineart, flat cel shading, and identical costume and hair across all angles.',
      negative_prompt: 'realistic human face, small realistic eyes, realistic eyes, 3D CGI render, photorealism, live-action, western comic style, ugly anatomy, deformed face, muddy colors, bad eyes, realistic skin texture, realistic wrinkles, dull eyes, pores, text, labels, watermark, signature',
    }, null, 2);

    const negativePrompt = 'realistic human face, small realistic eyes, realistic eyes, 3D CGI render, photorealism, live-action, western comic style, ugly anatomy, deformed face, muddy colors, bad eyes, realistic skin texture, realistic wrinkles, dull eyes, pores, text, letters, words, labels, watermark, signature, bad proportions, blurry';
    const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;
    const promptGemini = `Hãy đóng vai họa sĩ thiết kế nhân vật Anime 2D hàng đầu của Nhật Bản (Kyoto Animation / Ufotable).\nTạo bảng vẽ mẫu nhân vật 2D Anime (Character Turnaround Model Sheet 16:9) gồm 5 góc quay (Chính diện 0°, Nghiêng 45°, Góc ngang 90°, Nghiêng sau 135°, Sau lưng 180° và Đỉnh đầu):\n- Phong cách: 2D Anime Nhật Bản đích thực, MẮT TO TRÒN LONG LANH có nhiều đốm sáng phản chiếu, đường viền mí mắt sắc nét, sống mũi nhỏ thanh tú, nét vẽ 2D phẳng (Cel shading), TUYỆT ĐỐI KHÔNG VẼ TẢ THỰC, KHÔNG 3D.\n- Nhân vật: ${isMale ? 'Nam hiệp khách tuấn tú' : 'Nữ hiệp sĩ/tiên nữ xinh đẹp'}, thần thái ${mouthInfo.vi}, mắt to ${eyeShapeInfo.vi} màu ${eyeColInfo.vi}.\n- Mái tóc: Màu ${hairColInfo.vi}, độ dài ${hairLenInfo.vi}, kiểu ${hairTexInfo.vi}, phụ kiện ${hairAccInfo.vi}.\n- Trang phục: ${costumeInfo.vi}, tông màu: ${costumeColorVi}.\n- Phụ kiện/Vũ khí: ${propInfo.vi}.\n- Nền: ${bgTextVi}, không vẽ cảnh vật rườm rà.\n- Yêu cầu kỹ thuật: Tỷ lệ 16:9, độ phân giải 4K sắc nét, các góc quay phải thể hiện ĐÚNG CÙNG MỘT NHÂN VẬT. Không chèn chữ hay watermark.`;
    return { promptEnglish, promptVietnamese, promptJSON, promptGemini, gridStructureGuide, negativePrompt, fullCopyText };
  }

  // 1. CINEMATIC SINGLE PART 6-ANGLE GRID (2 ROWS x 3 COLS - 16:9)
  if (
    sheet === 'cinematic_single_part_2x3' ||
    sheet === 'cinematic_single_part_2x2' ||
    sheet === 'modular_bangs_3x1' ||
    sheet === 'modular_bangs_2x2' ||
    sheet === 'modular_backhair_3x1' ||
    sheet === 'modular_backhair_2x2' ||
    sheet === 'modular_torso_armor_3x1' ||
    sheet === 'modular_weapon_2x2'
  ) {
    const ar = config.aspect_ratio || '16:9';
    const isBangs = config.part_type === 'toc_truoc';
    const isBackHair = config.part_type === 'toc_sau';
    const isEyes = config.part_type === 'mat';
    const isSclera = config.part_type === 'trong_trang';
    const isIris = config.part_type === 'trong_den_iris';
    const isHighlight = config.part_type === 'diem_sang_mat';
    const isEyelids = config.part_type === 'mi_mat';
    const isEyebrows = config.part_type === 'long_may';
    const isNose = config.part_type === 'mui';
    const isEars = config.part_type === 'doi_tai' || config.part_type === 'mui_tai';
    const isMouth = config.part_type === 'mieng';
    const isNoFace = config.part_type === 'khuon_mat_no_face' || config.part_type === 'khuon_mat';
    const isTorso = config.part_type === 'than_co_ban';
    const isUpperArmL = config.part_type === 'canh_tay_trai';
    const isForearmL = config.part_type === 'cang_tay_trai';
    const isHandL = config.part_type === 'ban_tay_trai';
    const isUpperArmR = config.part_type === 'canh_tay_phai';
    const isForearmR = config.part_type === 'cang_tay_phai';
    const isHandR = config.part_type === 'ban_tay_phai';
    const isThighL = config.part_type === 'dui_trai';
    const isShinL = config.part_type === 'cang_chan_trai';
    const isThighR = config.part_type === 'dui_phai';
    const isShinR = config.part_type === 'cang_chan_phai';
    const isCloak = config.part_type === 'ao_choang' || config.part_type === 'trang_phuc';

    let partNameVi = 'Mái Tóc Trước';
    let cellRow1En = '';
    let cellRow2En = '';
    let cellRow1Vi = '';
    let cellRow2Vi = '';

    if (isBangs) {
      partNameVi = 'Mái Tóc Trước (Front Bangs)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front symmetric fringe bangs framing brow; Col 2: 45° Three-Quarter bangs curved around brow; Col 3: 90° Side Profile thin front fringe slice`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle top-down view showing bangs roots cascading down onto forehead; Col 2: 👑 Low Angle dramatic upward view from chin showing inner underside of bangs flicking upward; Col 3: 🌅 180° Rear Back View (EMPTY HOLLOW CELL / ZERO BANGS because forehead is 100% hidden behind head)`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Mái trước đối xứng ôm trán)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Mái trước xoay chéo theo góc mặt)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Lát cắt mỏng của mái trước nhìn từ vành tai)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (High Angle: Thấy chân tóc từ đỉnh đầu đổ xuống trán)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Low Angle: Thấy mặt trong và ngọn tóc hất bồng bềnh)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Ô RỖNG KHÔNG CÓ TÓC MÁI vì đầu quay lưng 180° mái trước bị khuất hoàn toàn)`;
    } else if (isBackHair) {
      partNameVi = 'Suối Tóc Sau Lưng (Back Hair Mantle)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front view back hair cascading over both shoulders with hollow face opening in center; Col 2: 45° Three-Quarter flowing hair shifted over shoulder; Col 3: 90° Side Profile rear cranial dome & S-curved hair volume`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle top-down view showing full crown hair dome radiating down; Col 2: 👑 Low Angle dramatic upward view showing billowing long hair flowing dramatically upward; Col 3: 🌅 180° Full Rear Back View covering back directly`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Suối tóc sau buông 2 bên vai, khoảng giữa rỗng để ghép mặt)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Suối tóc sau buông lệch vai)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Nửa sau vòm đầu và suối tóc uốn chữ S)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Vòm tóc đỉnh đầu nhìn từ trên cao rủ xuống)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Suối tóc dài bay bồng bềnh dốc lên trên uy dũng)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Toàn bộ suối tóc phủ kín lưng 180° trực diện)`;
    } else if (isIris) {
      partNameVi = 'Mống Mắt & Con Ngươi Màu (Iris & Pupil Layer - Dễ Dàng Thay Màu)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front symmetrical vibrant colored anime irises with dark pupils and subtle luminous inner gradients (${eyeColInfo.en}), strictly NO eyelids, NO eyelashes, NO white sclera; Col 2: 45° Three-Quarter angled irises; Col 3: 90° Side Profile thin convex iris disc`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle irises looking upward; Col 2: 👑 Low Angle irises looking down; Col 3: 🌅 180° Rear Back View (EMPTY HOLLOW CELL / ZERO IRIS)`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (2 Mống mắt tròn có màu ${eyeColInfo.vi} sắc nét & con ngươi đồng tử, KHÔNG LÔNG MI, KHÔNG TRÒNG TRẮNG để dễ thay màu mắt)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Mống mắt xoay 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Đĩa mống mắt cong nhìn ngang vành tai)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Con ngươi ngước lên trên nhìn camera)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Con ngươi liếc xuống uy nghiêm)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Ô RỖNG KHÔNG CÓ TRÒNG MẮT)`;
    } else if (isSclera) {
      partNameVi = 'Tròng Trắng / Hốc Mắt Lót (Sclera Base Layer)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front pure smooth white sclera eye socket shape with soft top ambient shadow, strictly NO iris, NO pupil, NO eyelashes; Col 2: 45° Three-Quarter sclera base; Col 3: 90° Side Profile sclera slice`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle sclera looking up; Col 2: 👑 Low Angle sclera from below; Col 3: 🌅 180° Rear Back View (EMPTY HOLLOW CELL)`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (2 Hốc tròng trắng mịn màng làm nền lót, bóng đổ nhẹ mí trên, KHÔNG MỐNG MẮT, KHÔNG LÔNG MI)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Tròng trắng xoay nghiêng 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Tròng trắng nhìn ngang)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Tròng trắng nhìn từ trên)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Tròng trắng nhìn từ dưới)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Ô RỖNG KHÔNG CÓ TRÒNG TRẮNG)`;
    } else if (isHighlight) {
      partNameVi = 'Điểm Sáng / Highlight Mắt Lấp Lánh (Eye Sparkles & Highlights)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front crisp pure white circular & starburst eye reflection glints; Col 2: 45° Three-Quarter angled glints; Col 3: 90° Side Profile glint dot`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle glints top; Col 2: 👑 Low Angle glints bottom; Col 3: 🌅 180° Rear Back View (EMPTY HOLLOW CELL)`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Các đốm sáng trắng tròn & ngôi sao lấp lánh phản chiếu ánh sáng linh hoạt bật/tắt)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Đốm sáng xoay nghiêng 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Chấm sáng nhỏ nhìn ngang)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Điểm sáng nhìn từ trên)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Điểm sáng nhìn từ dưới)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Ô RỖNG)`;
    } else if (isEyes) {
      partNameVi = 'Đôi Mắt Tổng Hợp (Full Eyes with Iris & Sclera)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front symmetrical large sparkling anime eyes; Col 2: 45° Three-Quarter eyes (far eye slightly smaller); Col 3: 90° Side Profile single anime eye`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle pupils looking up; Col 2: 👑 Low Angle sharp confident pupils looking down; Col 3: 🌅 180° Rear Back View (EMPTY HOLLOW CELL / ZERO EYES)`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (2 mắt to tròn mở to nhìn thẳng, có điểm sáng phản chiếu)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (2 mắt xoay theo góc mặt nghiêng 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (1 mắt đơn nhìn ngang vành tai)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Đồng tử ngước nhẹ lên trên nhìn camera)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Đồng tử nhìn xuống dưới uy nghiêm)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Ô RỖNG KHÔNG CÓ MẮT)`;
    } else if (isEyelids) {
      partNameVi = 'Mi Mắt & Chớp Mắt (Eyelids & Blink Keyframes)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front 3-stage blink eyelids (Open 100%, Half-closed 50%, Closed curved smile 100%); Col 2: 45° Three-Quarter angled blink eyelids; Col 3: 90° Side Profile eyelash line`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle eyelids looking up; Col 2: 👑 Low Angle downward eyelids; Col 3: 🌅 180° Rear Back View (EMPTY HOLLOW CELL)`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Bộ 3 trạng thái mí mắt: Mở to 100%, Mí khép hờ 50%, Nhắm tịt 100% đường cong cười)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Mi mắt xoay nghiêng 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Đường viền lông mi nhìn từ vành tai)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Mi mắt nhìn từ trên chúc xuống)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Đường mi mắt dưới hất lên)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Ô RỖNG KHÔNG CÓ MI MẮT)`;
    } else if (isEyebrows) {
      partNameVi = 'Cặp Lông Mày (Eyebrows Only)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front swordsman eyebrows; Col 2: 45° Three-Quarter angled eyebrows; Col 3: 90° Side Profile single eyebrow contour`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle furrowed eyebrows looking up; Col 2: 👑 Low Angle sharp resolute battle eyebrows; Col 3: 🌅 180° Rear Back View (EMPTY HOLLOW CELL)`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (2 lông mày sắc nét thanh tú)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Lông mày phối cảnh nghiêng 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (1 lông mày đơn nhìn từ bên sườn trán)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Lông mày nhìn từ trên chúc xuống)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Lông mày nhíu sắc bén oai nghiêm)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Ô RỖNG KHÔNG CÓ LÔNG MÀY)`;
    } else if (isNose) {
      partNameVi = 'Sống Mũi (Nose Only)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front delicate small anime nose dot/shadow; Col 2: 45° Three-Quarter angled nose bridge; Col 3: 90° Side Profile sharp anime nose contour`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle nose top-down view; Col 2: 👑 Low Angle nostrils upward view; Col 3: 🌅 180° Rear Back View (EMPTY HOLLOW CELL)`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Chấm mũi nhỏ thanh tú)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Sống mũi nghiêng 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Sống mũi nhọn thanh tú góc ngang)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Sống mũi nhìn chúc xuống)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Đầu mũi thanh từ dưới hất lên)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Ô RỖNG KHÔNG CÓ MŨI)`;
    } else if (isEars) {
      partNameVi = 'Đôi Tai (Ears Only)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front 2 symmetrical anime ears; Col 2: 45° Three-Quarter single ear; Col 3: 90° Side Profile full side ear with earlobes`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle ears seen from top; Col 2: 👑 Low Angle earlobes seen from below; Col 3: 🌅 180° Rear Back View rear curve of 2 ears`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (2 vành tai đối xứng 2 bên)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (1 vành tai xoay nghiêng)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Vành tai đầy đủ nhìn ngang vành tai)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Đỉnh vành tai nhìn từ trên chúc xuống)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Dái tai nhìn từ dưới lên)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Mặt sau 2 vành tai)`;
    } else if (isMouth) {
      partNameVi = 'Khẩu Hình Miệng & Cười (Mouth & Lips)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front gentle confident smile lip line; Col 2: 45° Three-Quarter speaking mouth; Col 3: 90° Side Profile lips`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle mouth viewed from above; Col 2: 👑 Low Angle shouting battle mouth looking up; Col 3: 🌅 180° Rear Back View (EMPTY HOLLOW CELL)`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Môi cười nhếch nhẹ tự tin)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Khẩu hình miệng xoay nghiêng 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Môi nhìn ngang từ bên má)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Môi nhìn từ trên chúc xuống)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Khẩu hình miệng thét gầm tung chiêu hất lên)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Ô RỖNG KHÔNG CÓ MIỆNG)`;
    } else if (isNoFace) {
      partNameVi = 'Khuôn Mặt Trần Không Ngũ Quan (Blank Face Base - No Face)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front smooth blank anime face shape with V-line jaw, strictly no eyes, no nose, no mouth; Col 2: 45° Three-Quarter blank head base; Col 3: 90° Side Profile blank head silhouette`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle top-down blank forehead & chin; Col 2: 👑 Low Angle jawline and chin contour looking up; Col 3: 🌅 180° Rear Back View blank rear neck & cranial base`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Khuôn mặt trần da trắng mịn, cằm V-line, KHÔNG MẮT MŨI MIỆNG)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Khuôn mặt trần xoay 45° ôm xương hàm)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Khuôn mặt trần nhìn ngang vành tai)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Vòm trán và cằm thu ngắn nhìn từ trên xuống)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Đường viền xương hàm và cằm dưới hất lên trời)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Sau gáy và vòm sọ sau đầu)`;
    } else if (isTorso) {
      partNameVi = 'Thân Ngực & Eo (Torso & Chest Armor - No Limbs)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front torso armor chest & waist only, strictly no arms, no legs, no head; Col 2: 45° Three-Quarter torso armor; Col 3: 90° Side Profile torso armor`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle torso armor looking down at chest & collar; Col 2: 👑 Low Angle heroic torso looking up from waist; Col 3: 🌅 180° Full Rear Back Armor Mantle`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Thân áo giáp ngực & eo, KHÔNG TAY CHÂN, KHÔNG ĐẦU)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Thân áo giáp xoay chéo 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Thân áo giáp nhìn từ bên sườn)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Cầu vai và vòm ngực nhìn từ trên chúc xuống)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Thân áo giáp oai phong từ eo hất lên)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Mặt sau lưng áo giáp)`;
    } else if (isUpperArmL || isUpperArmR) {
      const isL = isUpperArmL;
      partNameVi = isL ? 'Cánh Tay Trái - Bắp Tay (Left Upper Arm: Vai → Khuỷu)' : 'Cánh Tay Phải - Bắp Tay (Right Upper Arm: Vai → Khuỷu)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front ${isL ? 'left' : 'right'} upper bicep arm sleeve segment; Col 2: 45° Three-Quarter upper arm; Col 3: 90° Side Profile upper arm`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle upper arm from shoulder; Col 2: 👑 Low Angle upper arm from elbow; Col 3: 🌅 180° Rear Back View upper arm from back`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Khớp bắp tay từ bả vai đến khuỷu tay)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Bắp tay xoay nghiêng 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Bắp tay nhìn từ bên ngoài)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Cầu vai chúc xuống khuỷu tay)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Bắp tay nhìn từ khuỷu tay hất lên)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Mặt sau bắp tay)`;
    } else if (isForearmL || isForearmR) {
      const isL = isForearmL;
      partNameVi = isL ? 'Cẳng Tay Trái (Left Forearm: Khuỷu → Cổ tay)' : 'Cẳng Tay Phải (Right Forearm: Khuỷu → Cổ tay)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front ${isL ? 'left' : 'right'} forearm sleeve segment; Col 2: 45° Three-Quarter forearm; Col 3: 90° Side Profile forearm`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle forearm; Col 2: 👑 Low Angle forearm; Col 3: 🌅 180° Rear Back View forearm`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Khớp cẳng tay từ khuỷu tay đến cổ tay)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Cẳng tay xoay 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Cẳng tay nhìn ngang)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Cẳng tay nhìn từ trên xuống)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Cẳng tay nhìn từ cổ tay dốc lên)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Mặt sau cẳng tay)`;
    } else if (isHandL || isHandR) {
      const isL = isHandL;
      partNameVi = isL ? 'Bàn Tay Trái (Left Hand & Palm)' : 'Bàn Tay Phải (Right Hand & Palm)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front ${isL ? 'left' : 'right'} open palm / sword seal hand; Col 2: 45° Three-Quarter angled hand; Col 3: 90° Side Profile hand`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle hand top-down; Col 2: 👑 Low Angle hand reaching forward; Col 3: 🌅 180° Rear Back View back of hand`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Bàn tay xòe/bắt quyết kiếm ấn)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Bàn tay xoay chéo 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Cạnh bàn tay nhìn ngang)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Bàn tay nhìn từ trên chúc xuống)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Bàn tay vươn về phía trước tung chưởng)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Mu bàn tay nhìn từ phía sau)`;
    } else if (isThighL || isThighR) {
      const isL = isThighL;
      partNameVi = isL ? 'Đùi Trái (Left Thigh: Hông → Gối)' : 'Đùi Phải (Right Thigh: Hông → Gối)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front ${isL ? 'left' : 'right'} thigh segment; Col 2: 45° Three-Quarter thigh; Col 3: 90° Side Profile thigh`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle thigh; Col 2: 👑 Low Angle thigh; Col 3: 🌅 180° Rear Back View thigh`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Khớp đùi từ hông đến đầu gối)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Đùi xoay 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Đùi nhìn từ bên hông)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Đùi nhìn từ trên xuống)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Đùi nhìn từ đầu gối hất lên)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Mặt sau đùi)`;
    } else if (isShinL || isShinR) {
      const isL = isShinL;
      partNameVi = isL ? 'Cẳng Chân & Giày Trái (Left Shin & Boot: Gối → Gót)' : 'Cẳng Chân & Giày Phải (Right Shin & Boot: Gối → Gót)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front ${isL ? 'left' : 'right'} shin and armored boot; Col 2: 45° Three-Quarter shin & boot; Col 3: 90° Side Profile shin & boot`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle shin & boot; Col 2: 👑 Low Angle shin & boot; Col 3: 🌅 180° Rear Back View shin & boot`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Cẳng chân và ủng giáp từ đầu gối xuống bàn chân)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Cẳng chân và ủng xoay 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Ủng giáp nhìn ngang)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Ủng chân nhìn từ trên chúc xuống)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Ủng chân đứng vững chãi nhìn từ mặt đất)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Gót ủng và bắp chuối sau)`;
    } else if (isCloak) {
      partNameVi = 'Áo Choàng / Tà Áo Bay (Cape & Robe Flow)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front cape shoulders flow; Col 2: 45° Three-Quarter cape; Col 3: 90° Side Profile flowing cape`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle cape from top; Col 2: 👑 Low Angle billowing cape; Col 3: 🌅 180° Full Rear Back Cape covering back`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Tà áo choàng buông 2 bên vai)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Áo choàng bay lệch vai 45°)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Áo choàng bay về sau nhìn ngang)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Vòm áo choàng xòe rộng từ trên)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Tà áo choàng bay phần phật dốc lên trời)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Toàn bộ áo choàng phủ kín lưng)`;
    } else {
      partNameVi = 'Vũ Khí & Pháp Bảo (Weapons & Props)';
      cellRow1En = `[ROW 1 - EYE LEVEL]: Col 1: 0° Front weapon upright; Col 2: 45° Three-Quarter angled weapon; Col 3: 90° Side Profile thin blade edge`;
      cellRow2En = `[ROW 2 - DYNAMIC CINEMATICS]: Col 1: 🦅 High Angle weapon pointing downward; Col 2: 👑 Low Angle dramatic glowing weapon thrusting upward; Col 3: 🌅 180° Sheathed weapon worn on back`;
      cellRow1Vi = `🔹 HÀNG 1 (Góc Ngang Tầm Mắt & Xoay Nghiêng):\n     - Ô [0, 0]: 1. Chính diện 0° (Vũ khí cầm thẳng)\n     - Ô [0, 1]: 2. Nghiêng 3/4 (45°) (Vũ khí xoay chéo thế thủ)\n     - Ô [0, 2]: 3. Nhìn ngang 90° (Cạnh mỏng lưỡi kiếm nhìn ngang)`;
      cellRow2Vi = `🔹 HÀNG 2 (Góc Máy Điện Ảnh Đột Phá):\n     - Ô [1, 0]: 4. 🦅 Trên cao nhìn xuống (Mũi kiếm hướng xuống từ trên cao)\n     - Ô [1, 1]: 5. 👑 Dưới hất lên (Kiếm khí phát sáng hướng lên trời)\n     - Ô [1, 2]: 6. 🌅 Sau lưng 180° (Bao kiếm đeo sau lưng)`;
    }

    const partDescEn =
      isBangs
        ? `pure front fringe bangs floating alone, ${hairColInfo.en} hair, ${hairTexInfo.en}`
        : isBackHair
        ? `pure flowing back hair mantle, ${hairColInfo.en} hair, ${hairLenInfo.en}`
        : isIris
        ? `isolated vibrant colored anime irises and dark pupils without sclera: ${eyeColInfo.en}`
        : isSclera
        ? `isolated smooth white anime sclera eye socket shape without iris`
        : isHighlight
        ? `isolated pure white sparkling glints and star highlights for anime eyes`
        : isEyes
        ? `isolated anime full eyes: ${eyeShapeInfo.en}, ${eyeColInfo.en}`
        : isEyelids
        ? `isolated anime eyelid blinking keyframes (open, half-closed, closed smile)`
        : isEyebrows
        ? `isolated anime eyebrows pair`
        : isNose
        ? `isolated anime small nose bridge: ${noseInfo.en}`
        : isEars
        ? `isolated anime ears pair`
        : isMouth
        ? `isolated anime lips and mouth: ${mouthInfo.en}`
        : isNoFace
        ? `blank anime face shape mannequin head base, pure clean skin, strictly NO facial features, NO eyes, NO nose, NO mouth`
        : isTorso
        ? `costume and armor torso chest only, strictly NO limbs: ${costumeInfo.en}, ${costumeColorVi}`
        : isUpperArmL || isUpperArmR
        ? `isolated upper arm bicep limb segment: ${costumeColorVi}`
        : isForearmL || isForearmR
        ? `isolated forearm limb segment: ${costumeColorVi}`
        : isHandL || isHandR
        ? `isolated anime hand and palm gesture`
        : isThighL || isThighR
        ? `isolated thigh leg limb segment: ${costumeColorVi}`
        : isShinL || isShinR
        ? `isolated shin and armored boot segment: ${costumeColorVi}`
        : isCloak
        ? `isolated flowing cape and back robes: ${costumeColorVi}`
        : `weapon and prop: ${propInfo.en}`;

    promptEnglish = [
      `masterpiece, best quality, ultra detailed 4k resolution, ${ar} aspect ratio modular 2D anime sprite sheet layout`,
      `consistent 6-angle cinematic model sheet of ONE SINGLE ISOLATED 2D anime component (${partDescEn}) on ${bgPromptColorEn}`,
      `strictly isolated 2D anime component only, headless floating asset, ${isNoFace ? 'strictly no eyes no nose no mouth' : 'strictly no extra body parts'}, zero background clutter`,
      `organized in a clean 2-row by 3-column grid with generous safety margins:`,
      cellRow1En,
      cellRow2En,
      `CRITICAL SCALE & HEIGHT RULE: Sprites in Col 1 (0°), Col 2 (45°), Col 3 (90°) in Row 1 AND Col 3 (180°) in Row 2 MUST share EXACT SAME VERTICAL HEIGHT, SAME SCALE and BASELINE ALIGNMENT`,
      `PERSPECTIVE EXCEPTION: Row 2 Col 1 (High Angle) is naturally foreshortened from top-down, Row 2 Col 2 (Low Angle) is naturally foreshortened tapering upward`,
      styleTextEn,
      bgTextEn,
      `crisp clean hard-edge sticker cutout, unlit flat matte 2D anime cel shading, zero background color bleeding, zero green fringe, --ar ${ar}`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE 6 GÓC QUAY ĐIỆN ẢNH CHO 1 CHI TIẾT (TỶ LỆ ${ar}) 】
• Chi tiết bóc tách: ${partNameVi} (${partDescEn})
• Phông nền: ${bgTextVi}
• Bố cục 6 ô chuẩn điện ảnh (2 Hàng × 3 Cột):
${cellRow1Vi}
${cellRow2Vi}

⚠️ QUY TẮC BẮT BUỘC VỀ ĐỘ CAO VẬT THỂ (SCALE & HEIGHT CONSISTENCY):
1. Chi tiết ở các góc tầm mắt (Chính diện 0°, Nghiêng 45°, Ngang 90°, Sau lưng 180°) BẮT BUỘC PHẢI CÓ CÙNG ĐỘ CAO VÀ KÍCH THƯỚC (Consistent Height & Baseline).
2. Ngoại lệ góc phối cảnh:
   - Góc 4 (🦅 Trên cao xuống): Chiều cao tự nhiên bị thu ngắn lại do phối cảnh nhìn dốc từ trên xuống (Foreshortening).
   - Góc 5 (👑 Dưới hất lên): Chiều cao tự nhiên bị kéo dốc lên do phối cảnh máy quay đặt dưới đất.
3. Nếu ở góc sau lưng 180° chi tiết bị khuất (như Mái tóc trước, Đôi mắt, Khẩu hình miệng, Sống mũi) $\to$ ĐỂ TRỐNG Ô RỖNG HOẶC ĐỂ NỀN XANH TRẦN ĐỂ KHÔNG BỊ DÍNH RÁC.`;


    const promptGemini = `Hãy tạo prompt sinh ảnh AI cho bảng bóc tách 6 góc quay điện ảnh tỉ lệ ${ar} của một linh kiện duy nhất (${partNameVi}):\n- Bố cục 2 Hàng × 3 Cột: Hàng 1 gồm Chính diện 0°, Nghiêng 45°, Ngang 90°. Hàng 2 gồm Trên cao nhìn xuống (High Angle), Dưới hất lên (Low Angle), Sau lưng 180°.\n- Yêu cầu kích thước: Tất cả các góc 0°, 45°, 90°, 180° phải CÙNG ĐỘ CAO VÀ TỶ LỆ KÍCH THƯỚC (ngoại trừ góc Trên cao xuống và Dưới hất lên có chiều cao thay đổi tự nhiên theo phối cảnh).\n- Lưu ý ô khuất: Nếu chi tiết không nhìn thấy được ở góc sau lưng 180° (như Mái trước), hãy để ô rỗng hoặc nền phẳng.\n- Nền: ${bgTextVi}, viền sắc nét không lem màu.`;

    gridStructureGuide = `📐 Khung Cắt ${ar}: Lưới 2 Hàng × 3 Cột chuẩn điện ảnh (6 ô rộng 341x512px), tự động đồng bộ chiều cao cho Tab 1 Cắt Lưới.`;

    const negativePrompt =
      'human face, skin, extra limbs, text, labels, watermark, blurry, inconsistent height on 0-45-90-180 angles, green halo, glowing outline';
    const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;

    return { promptEnglish, promptVietnamese, promptJSON: '', promptGemini, gridStructureGuide, negativePrompt, fullCopyText };
  }

  // 2. HAIR MULTI-ANGLE GRID (LEGACY BÓC TÁCH THEO ĐỘ SÂU Z-INDEX & 5 GÓC QUAY CINEMATIC)
  if (sheet === 'hair_multi_angle_grid') {
    const ar = config.aspect_ratio || '16:9';

    promptEnglish = [
      `masterpiece, best quality, ultra detailed 2D anime character hair turnaround model sheet`,
      `consistent modular hair layers sprite sheet organized in a clean 3-row by 5-column grid on ${bgPromptColorEn}`,
      `strictly headless pure hair component assets only, transparent invisible mannequin, no human face, no skin, no body, no human ears`,
      `Column 1: 0° Front View, Column 2: 45° Three-Quarter View, Column 3: 90° Side Profile View, Column 4: 135° Back Three-Quarter View, Column 5: 180° Rear Back View`,
      `[ROW 1 - FRONT BANGS FRINGE]: pure front fringe bangs floating alone across 5 angles, strictly no back hair`,
      `[ROW 2 - SIDEBURNS & CHEEK LOCKS]: floating sideburn cheek locks hugging the face contours across 5 angles, strictly no ears, no skin`,
      `[ROW 3 - BACK HAIR MANTLE & VOLUME]: complete back hair flowing down across 5 angles, pure rear hair layer with empty hollow face center`,
      `${hairColInfo.en} hair, ${hairLenInfo.en}, ${hairTexInfo.en}${hairAccInfo.en !== 'none' ? `, accessory: ${hairAccInfo.en}` : ''}`,
      styleTextEn,
      bgTextEn,
      `extremely crisp high-contrast edge separation, hard edge sticker cutout, unlit flat shading, absolutely ZERO background color bleeding, ZERO ambient green color spill onto hair strands, ZERO glowing halo around hair contours, 100% opaque solid matte color borders, modular puppet assembly ready`,
      `--ar ${ar} --no text typography letters font words labels captions numbers writing watermark signature logo characters subtitle calligraphy heading title annotations alphabet stamp frame border-text human face eyes mouth nose skin body ears neck ${config.bg_type === 'chroma_green' || !config.bg_type ? 'green reflection green glow green ambient light green fringe color spill neon rim light bounce light soft glowing outline translucent borders' : 'color spill rim light bounce light glow'}`,
    ].join(', ');

    const jsonSpec = {
      project: 'Flow-App 2D Motion Comic Engine',
      workflow_step: 'Step 2 - Cinematographic Modular Hair Turnaround Grid Decomposition',
      title: 'Bảng Bóc Tách Tóc Đa Tầng Theo Độ Sâu Z-Index & 5 Góc Quay Chuẩn Cinematic (3 Rows x 5 Columns)',
      art_style: styleLabelVi,
      resolution: '4K Ultra High Definition (3840x2160)',
      aspect_ratio: `${ar} Aspect Ratio (3 Rows x 5 Columns)`,
      background: bgTextVi,
      hair_specifications: {
        color: `${hairColInfo.vi} (${hairColInfo.en})`,
        length: `${hairLenInfo.vi} (${hairLenInfo.en})`,
        texture: `${hairTexInfo.vi} (${hairTexInfo.en})`,
        accessories: `${hairAccInfo.vi} (${hairAccInfo.en})`,
      },
      cinematographic_layer_breakdown: {
        total_rows: 3,
        total_columns: 5,
        camera_angles: [
          'Column 1: 0° Front View (Chính diện)',
          'Column 2: 45° Three-Quarter View (Nghiêng 3/4)',
          'Column 3: 90° Full Side Profile View (Nhìn ngang vành tai 90 độ)',
          'Column 4: 135° Back Three-Quarter View (Nghiêng sau 135 độ)',
          'Column 5: 180° Full Rear Back View (Sau lưng toàn cảnh 180 độ)'
        ],
        rows_definition: [
          {
            row: 'Row 1 (Z-Index Cao Nhất - Trên Cùng): Tóc Mái Trước Trán (Front Bangs Fringe)',
            description: '5 floating front fringe bangs pieces across 5 camera angles. Pure bangs only, strictly no back hair.',
          },
          {
            row: 'Row 2 (Z-Index Trung Gian - Ôm Mặt): Lọn Tóc Mai 2 Bên Má (Sideburns & Cheek Locks)',
            description: '5 floating sideburn cheek locks hugging the jawline and face contour across 5 angles. Strictly no ears, no skin.',
          },
          {
            row: 'Row 3 (Z-Index Thấp Nhất - Dưới Cùng): Suối Tóc Sau Đầu (Back Hair Mantle & Flowing Volume)',
            description: '5 complete back hair streams cascading down across 5 angles. Hollow face center to assemble behind head base.',
          },
        ],
      },
      anti_color_spill_rules: {
        zero_color_bleed: 'Strictly zero background color fringe or bleed onto fine hair edges.',
        solid_matte_edges: '100% opaque solid matte color borders without glow, rim light, or soft halos.',
        clean_cutout: 'Sharp crisp borders for instant 1-click transparent AI matting extraction.'
      },
      strict_constraints: {
        headless_pure_hair_only: 'Strictly pure hair components only. No human face, no skin, no ears, no body.',
        no_text_or_watermark: 'Zero text, zero labels, zero watermark.',
        ample_margins: 'Ample margins around each hair sprite to prevent clipping at borders.',
      },
      negative_prompt: 'human face, eyes, mouth, nose, human ears, skin, neck, body, text, watermark, blurry, 3D clay render, color spill, green halo, glowing outline',
    };
    promptJSON = JSON.stringify(jsonSpec, null, 2);

    promptVietnamese = `【 BẢNG SPRITE LINH KIỆN TÓC 3 DÃY × 5 GÓC QUAY CHUẨN ĐIỆN ẢNH (TỶ LỆ ${ar}) 】
• Mục tiêu: Bóc tách mái tóc thành 3 tầng độ sâu Z-Index đồng bộ xoay 360° (Mái trước $\to$ Tóc mai $\to$ Tóc sau lưng) để ghép khớp 100% vào nhân vật mà không bị phụ kiện thừa.
• Mái tóc: Tóc ${hairColInfo.vi}, ${hairLenInfo.vi}, ${hairTexInfo.vi}${hairAccInfo.vi !== 'Không có' ? `, Phụ kiện (${hairAccInfo.vi})` : ''}.
• Phông nền: ${bgTextVi}.
• ⚠️ YÊU CẦU ĐẶC BIỆT CHỐNG DÍNH MÀU NỀN VÀO VIỀN TÓC (ANTI-COLOR-SPILL):
  1. Đường viền sợi tóc phân tách sắc nét (Crisp Edge / Hard Cutout) dứt khoát với nền ${bgPromptColorEn}.
  2. Tuyệt đối KHÔNG ĐỂ MÀU NỀN HẮT ÁNH SÁNG/ÁM MÀU (Zero Color Spill / Zero Fringe) vào các sợi tóc con.
  3. Màu tóc là mảng màu đặc (Solid Opaque Matte Color), không bóng mờ phát sáng viền (No Glow/Rim Light) để khi bóc tách bằng AI trong suốt sạch 100%, không bị sạn viền.
  4. Thuần túy là tóc, KHÔNG CÓ DA MẶT, TAI, MẮT, MŨI HAY CỔ.

【 CHI TIẾT 3 TẦNG ĐỘ SÂU (Z-INDEX) × 5 GÓC QUAY CINEMATIC 】:
🔹 HÀNG 1 (Z-Index Cao Nhất - Đè Lên Mặt): TÓC MÁI TRƯỚC TRÁN (Thuần túy mái trước)
   - Cột 1: 0° Mái chính diện ôm 2 bên trán
   - Cột 2: 45° Mái nghiêng 3/4 ôm theo mặt xoay
   - Cột 3: 90° Mái nhìn ngang 90°
   - Cột 4: 135° Mép mái khuất dần phía sau chéo
   - Cột 5: 180° Ô rỗng (vì mặt quay lưng 180° nên không thấy mái trước)

🔹 HÀNG 2 (Z-Index Trung Gian - Ôm 2 Bên Mặt): LỌN TÓC MAI 2 BÊN MÁ (Thuần túy tóc mai, không dính tai)
   - Cột 1: 0° Hai lọn tóc mai ôm 2 bên má
   - Cột 2: 45° Lọn tóc mai nghiêng 3/4
   - Cột 3: 90° Lọn tóc mai nhìn ngang trước tai
   - Cột 4: 135° Lọn tóc mai nhìn từ phía sau chéo
   - Cột 5: 180° Lọn tóc mai 2 bên nhìn từ sau lưng

🔹 HÀNG 3 (Z-Index Dưới Cùng - Nằm Dưới Thân/Đầu): SUỐI TÓC SAU ĐẦU (Thuần túy tóc sau)
   - Cột 1: 0° Vòm đầu sau + suối tóc rủ 2 bên vai (khoảng giữa rỗng để ghép mặt/thân)
   - Cột 2: 45° Vòm đầu sau nghiêng 45° + suối tóc đổ dài chéo
   - Cột 3: 90° Nửa sau sọ đầu + suối tóc sau gáy (trán trước rỗng)
   - Cột 4: 135° Mặt sau vòm đầu + suối tóc sau chéo
   - Cột 5: 180° Trọn vẹn vòm đầu sau + suối tóc phủ kín lưng`;

    gridStructureGuide = `📐 Khung Cắt ${ar}: Lưới 3 Hàng × 5 Cột chuẩn 15 ô linh kiện độ sâu Z-Index, tự động khớp vào Tab 1 Cắt Lưới.`;
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
    // 8. FULL-BODY CHINESE DONGHUA PUPPET DECOMPOSITION GRID (4 ROWS X 5 COLUMNS)
    promptEnglish = [
      `masterpiece, best quality, ultra detailed 4k Chinese 2D Donghua modular puppet component sprite sheet`,
      `Xianxia cultivation manhua character anatomy parts decomposition on ${bgPromptColorEn}`,
      `organized in a 4-row by 5-column grid layout with clean spacing between parts:`,
      `[ROW 1 - HEAD & HAIR LAYERS]: headless head base, floating front bangs fringe, sideburn locks, flowing back hair mantle, jade hairpin top bun`,
      `[ROW 2 - FACIAL FEATURES & EXPRESSIONS]: isolated expressive phoenix eyes glowing azure, closed blinking eyelid, open speaking mouth, sharp nose bridge, neutral ear`,
      `[ROW 3 - DAOIST ROBES & COSTUME]: hollow daoist upper chest robe with gold trim, flowing lower skirt hem, inner pleated skirt, golden waist sash belt with dangling jade pendant, jade tassel sash`,
      `[ROW 4 - LIMBS, HAND SEALS & FLYING WEAPONS]: detached wide sleeves, arms, two-finger daoist sword seal gesture hand, martial arts boots, glowing azure flying spirit sword`,
      `strictly flat clean cutout sticker assets, crisp high-contrast outlines, unlit flat shading, zero green ambient color spill, zero glow on edges, 100% opaque solid colors, --ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG BÓC TÁCH TOÀN BỘ LINH KIỆN CƠ THỂ & ĐẠO BÀO (20 LINH KIỆN - CHUẨN HOẠT ẢNH TRUNG QUỐC) 】
• Mục tiêu: Bóc tách toàn bộ 20 vị trí giải phẫu cơ thể của nhân vật Hoạt ảnh / Hoạt hình Tu Tiên Trung Quốc (Đầu, Tóc, Ngũ quan, Đạo bào, Tứ chi, Bàn tay bắt quyết, Phi kiếm) theo lưới 4 Hàng × 5 Cột để cắt tự động và ghép xương 2D (Rigging).
• Phông nền: ${bgTextVi}.
• ⚠️ YÊU CẦU BẮT BUỘC: Các linh kiện nằm độc lập, có khoảng cách rộng rãi, không đè lấn nhau, viền dứt khoát không bóng mờ.

【 CHI TIẾT 4 HÀNG × 5 CỘT LINH KIỆN THEO VỊ TRÍ 】:
🔹 HÀNG 1: ĐẦU & CÁC LỚP TÓC
   - Cột 1: Đầu base trần + cổ
   - Cột 2: Tóc mái trước trán
   - Cột 3: Lọn tóc mai 2 bên má
   - Cột 4: Suối tóc sau lưng
   - Cột 5: Búi tóc củ tỏi đỉnh đầu cài trâm ngọc

🔹 HÀNG 2: NGŨ QUAN & BIỂU CẢM
   - Cột 1: Mắt phượng sắc sảo mở phát sáng kiếm ý
   - Cột 2: Mắt nhắm chớp mắt
   - Cột 3: Khẩu hình miệng mở nói (Lip-sync)
   - Cột 4: Sống mũi thẳng thanh tú
   - Cột 5: Đôi tai trần

🔹 HÀNG 3: TRANG PHỤC & ĐẠO BÀO
   - Cột 1: Thân áo đạo bào rỗng ruột
   - Cột 2: Vạt áo ngoài thướt tha
   - Cột 3: Váy lót trong xếp ly
   - Cột 4: Thắt lưng lụa viền vàng
   - Cột 5: Ngọc bội hộ thân đính dải lụa

🔹 HÀNG 4: TỨ CHI, BÀN TAY BẮT QUYẾT & VŨ KHÍ
   - Cột 1: Ống tay áo rộng tách rời
   - Cột 2: Cánh tay & Cẳng tay
   - Cột 3: Hai bàn tay bắt quyết kiếm ấn (Two-finger sword seal)
   - Cột 4: Đôi hài chiến đấu cổ trang
   - Cột 5: Phi kiếm ngọc bích phát sáng kiếm khí`;

    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Hàng × 5 Cột chuẩn 20 ô linh kiện cơ thể, tự động khớp vào Tab 1 Cắt Lưới.`;
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
        art_style: getStyleLabel(config.character_style, config.custom_character_style),
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
2. PHONG CÁCH ĐỒ HỌA: ${getStyleLabel(config.character_style, config.custom_character_style)}.
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
