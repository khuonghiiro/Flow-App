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
  switch (s) {
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
  switch (s) {
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

export const getBodyProportionLabels = (prop?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!prop) return { vi: 'Chibi đáng yêu 2.5 đầu, đầu to thân nhỏ gọn gàng', en: 'Cute chibi proportions, large head, short compact body, 2.5 heads tall, small hands and feet' };
  switch (prop) {
    case 'chibi_2_5': return { vi: 'Chibi đáng yêu 2.5 đầu, đầu to thân nhỏ gọn gàng', en: 'Cute chibi proportions, large head, short compact body, 2.5 heads tall, small hands and feet' };
    case 'chibi_2_heads': return { vi: 'Super Chibi 2 đầu kawaii', en: 'Super kawaii chibi 2 heads tall, tiny body, giant head' };
    case 'anime_standard': return { vi: 'Anime tiêu chuẩn 6-7 đầu, thon thả thanh thoát', en: 'Standard 2D anime proportions, 6 to 7 heads tall, slender elegant body, long limbs' };
    case 'heroic_martial': return { vi: 'Hiệp khách oai phong 7.5 đầu', en: 'Heroic martial cultivator proportions, 7.5 heads tall, athletic powerful build' };
    default: return { vi: prop, en: prop };
  }
};

export const buildAIPromptForPart = (config: AIPartPromptConfig): AIPromptResult => {
  const sheet = config.sheet_type || 'hair_multi_angle_grid';
  let bgTextEn = 'isolated on solid pure flat white background #FFFFFF, clean flat cutout, zero drop shadows, no ambient occlusion, strictly neutral lighting';
  let bgTextVi = 'Nền trắng tinh khiết (#FFFFFF) phẳng 1 màu, viền tương phản cao dễ cắt';
  let bgPromptColorEn = 'pure Chroma Green #00FF00';
  let bgPromptColorHex = '#00FF00';

  if (config.bg_type === 'chroma_green' || !config.bg_type) {
    bgTextEn = 'isolated on solid flat pure chroma green background #00FF00, uniform flat single color, high contrast edge, strictly flat neutral unlit shading, absolutely zero ambient color spill, zero green fringe on edges, no bounce light, no rim lighting, no global illumination, pure matte colors';
    bgTextVi = 'Nền xanh lá Chroma Green (#00FF00) phẳng 1 màu dứt khoát để cắt phông tức thì';
    bgPromptColorEn = 'pure Chroma Green #00FF00';
    bgPromptColorHex = '#00FF00';
  } else if (config.bg_type === 'pure_white') {
    bgTextEn = 'isolated on solid pure flat white background #FFFFFF, clean flat cutout, zero drop shadows, no ambient occlusion, strictly neutral lighting';
    bgTextVi = 'Nền trắng tinh khiết (#FFFFFF) phẳng 1 màu';
    bgPromptColorEn = 'pure White #FFFFFF';
    bgPromptColorHex = '#FFFFFF';
  } else if (config.bg_type === 'chroma_gray') {
    bgTextEn = 'isolated on solid flat neutral dark gray background #333333, uniform flat single color, high contrast edge, zero shadows, no color spill, perfect for white hair extraction';
    bgTextVi = 'Nền xám đậm trung tính (#333333) không bóng đổ, chuẩn bóc tách tóc trắng/bạc';
    bgPromptColorEn = 'neutral Dark Gray #333333';
    bgPromptColorHex = '#333333';
  } else if (config.bg_type === 'pure_black') {
    bgTextEn = 'isolated on solid flat pure black background #000000, uniform flat single color, high contrast edge, zero shadows';
    bgTextVi = 'Nền đen tuyền (#000000) không bóng đổ, dùng cho chi tiết phát sáng';
    bgPromptColorEn = 'pure Black #000000';
    bgPromptColorHex = '#000000';
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
  const costumeColorVi = config.costume_color?.trim() || 'Trắng bạc phối tím nhạt viền ngọc bích';
  const propInfo = getPropLabels(config.prop_item, config.custom_prop_item);
  const hairLenInfo = getHairLengthLabels(config.hair_length, config.custom_hair_length);
  const hairColInfo = getHairColorLabels(config.hair_color, config.custom_hair_color);
  const hairTexInfo = getHairTextureLabels(config.hair_texture, config.custom_hair_texture);
  const hairAccInfo = getHairAccessoryLabels(config.hair_accessories, config.custom_hair_accessories);
  const bodyPropInfo = getBodyProportionLabels(config.body_proportion, config.custom_body_proportion);

  let promptEnglish = '';
  let promptVietnamese = '';
  let promptJSON = '';
  let gridStructureGuide = '';

  // 0. STEP 1: MASTER CHARACTER TURNAROUND SHEET (MODULAR 2-PART ARCHITECTURE)
  if (config.workflow_step === 'step1_master_character') {
    const isMale = config.gender === 'nam';
    const genderLabelEn = isMale ? 'Male' : 'Female';
    const genderLabelVi = isMale ? 'Nam' : 'Nữ';
    const artStyleEn = config.custom_character_style?.trim() || config.character_style?.trim() || 'Chinese Guoman / 国漫 Xianxia Chibi';
    const artStyleVi = styleLabelVi;

    const baseDescEn = `2D ${artStyleEn} character turnaround sheet, ${genderLabelEn}, ${bodyPropInfo.en}, face (${eyeShapeInfo.en}, ${eyeColInfo.en}, ${noseInfo.en}, ${mouthInfo.en}), hair (${hairColInfo.en}, ${hairTexInfo.en}, ${hairLenInfo.en}${hairAccInfo.en !== 'none' ? `, ${hairAccInfo.en}` : ''}), costume (${costumeInfo.en}, color: ${costumeColorVi}), weapon: ${propInfo.en}, flat ${bgPromptColorEn} background, clean anime lineart, cel shading, zero shadows, no text, no borders --ar 16:9`;

    promptEnglish = `masterpiece, best quality, ultra detailed, 2D ${artStyleEn} character turnaround sheet, ONE SINGLE IDENTICAL ${genderLabelEn.toUpperCase()} CHARACTER.

CHARACTER SPECIFICATIONS:
- Gender: ${genderLabelEn}
- Art Style: ${artStyleEn}
- Proportion: ${bodyPropInfo.en}
- Eyes: ${eyeShapeInfo.en}, ${eyeColInfo.en}
- Nose & Mouth: ${noseInfo.en}, ${mouthInfo.en}
- Hairstyle: ${hairTexInfo.en}, ${hairColInfo.en}, ${hairLenInfo.en}${hairAccInfo.en !== 'none' ? `, ${hairAccInfo.en}` : ''}
- Costume: ${costumeInfo.en} (Color: ${costumeColorVi})
- Weapon / Props: ${propInfo.en}

TURNAROUND 5-VIEW SEQUENCE (16:9 Canvas):
1. VIEW 1 — FRONT (0°): Direct frontal view, full body from head to feet.
2. VIEW 2 — THREE-QUARTER (45°): 45-degree angle showing face depth and shoulder.
3. VIEW 3 — SIDE PROFILE (90°): 90-degree clean side silhouette and nose profile.
4. VIEW 4 — REAR THREE-QUARTER (135°): 135-degree angle showing back sash and rear hair.
5. VIEW 5 — BACK (180°): Full rear view showing back hair mantle and robe spine.
+ TOP-DOWN REFERENCE: One smaller top-down view showing head crown and parting.

CONSISTENCY & RESTRICTIONS:
- 100% identical character rotated around vertical axis.
- Clean 2D anime lineart, flat cel shading, readable silhouettes for 2D rigging.
- Flat uniform ${bgPromptColorEn} (${bgPromptColorHex}) background with zero shadows.
- STRICTLY NO text, NO letters, NO numbers, NO labels, NO watermark, NO grid borders.

--ar 16:9`;

    promptVietnamese = `【 BẢNG THIẾT KẾ NHÂN VẬT GỐC ĐA GÓC QUAY (CHARACTER TURNAROUND SHEET - 16:9) 】

════════════════════════════════════════════════════════════
1. THUỘC TÍNH NHÂN VẬT:
════════════════════════════════════════════════════════════
• Giới tính: ${genderLabelVi} | Phong cách: ${artStyleVi} | Tỷ lệ: ${bodyPropInfo.vi}
• Khuôn mặt: Mắt ${eyeShapeInfo.vi} (${eyeColInfo.vi}), mũi ${noseInfo.vi}, miệng ${mouthInfo.vi}
• Mái tóc: ${hairColInfo.vi}, ${hairTexInfo.vi}, ${hairLenInfo.vi} (${hairAccInfo.vi})
• Trang phục: ${costumeInfo.vi} (Màu sắc: ${costumeColorVi})
• Vũ khí / Pháp bảo: ${propInfo.vi}

════════════════════════════════════════════════════════════
2. BỐ CỤC 5 GÓC XOAY TOÀN THÂN (16:9):
════════════════════════════════════════════════════════════
1. Front (0° Chính diện): Mẫu tham chiếu chính, toàn thân thẳng góc máy.
2. 45° (Nghiêng 3/4): Thấy độ sâu khuôn mặt, tóc mai và vai.
3. Side (90° Nhìn ngang): Thấy sống mũi, cằm, vành tai và dáng suối tóc.
4. 135° (Nghiêng sau): Thấy tà áo choàng, thắt lưng và búi tóc sau gáy.
5. Back (180° Sau lưng): Thấy toàn bộ suối tóc và lưng áo.
+ Góc phụ Đỉnh Đầu (Top-Down): Soi đỉnh đầu từ trên xuống thấy đường rẽ ngôi và trâm cài.

• Nền: ${bgTextVi}.
• CẤM: KHÔNG CHỮ, KHÔNG SỐ, KHÔNG NHÃN DÁN, KHÔNG ĐƯỜNG KẺ KHUNG ĐEN, KHÔNG WATERMARK.`;

    const step1Prompts = [
      {
        name: 'master_000_front',
        part_id: 'master_character',
        part_name: 'Nhân Vật Gốc Toàn Thân',
        group_id: 'step1_master',
        group_name: 'Bảng Xoay Nhân Vật Gốc',
        angle: '0° Front (Chính diện)',
        angle_id: '000_front',
        angle_deg: 0,
        z_index: 0,
        save_filename: 'master_000_front.png',
        view_desc: 'Góc nhìn chính diện 0° toàn thân làm chuẩn tham chiếu chính',
        prompt: `0° front view of the character, facing camera, full body, head to feet, clear eyes and bangs, uniform solid ${bgPromptColorEn} background --ar 16:9`,
        count: 4,
      },
      {
        name: 'master_045_three_quarter',
        part_id: 'master_character',
        part_name: 'Nhân Vật Gốc Toàn Thân',
        group_id: 'step1_master',
        group_name: 'Bảng Xoay Nhân Vật Gốc',
        angle: '45° Three-Quarter (Nghiêng 3/4)',
        angle_id: '045_three_quarter',
        angle_deg: 45,
        z_index: 0,
        save_filename: 'master_045_three_quarter.png',
        view_desc: 'Góc xoay 45° hiển thị độ sâu ngũ quan, tóc mai và vai',
        prompt: `45° three-quarter angle view of the exact same character, depth of face and hair, uniform solid ${bgPromptColorEn} background --ar 16:9`,
        count: 2,
      },
      {
        name: 'master_090_side',
        part_id: 'master_character',
        part_name: 'Nhân Vật Gốc Toàn Thân',
        group_id: 'step1_master',
        group_name: 'Bảng Xoay Nhân Vật Gốc',
        angle: '90° Side Profile (Nhìn ngang)',
        angle_id: '090_side',
        angle_deg: 90,
        z_index: 0,
        save_filename: 'master_090_side.png',
        view_desc: 'Góc nhìn ngang 90° thấy sống mũi, cằm, tai và dáng suối tóc',
        prompt: `90° side profile view of the exact same character, clean facial silhouette and spine posture, uniform solid ${bgPromptColorEn} background --ar 16:9`,
        count: 2,
      },
      {
        name: 'master_135_rear',
        part_id: 'master_character',
        part_name: 'Nhân Vật Gốc Toàn Thân',
        group_id: 'step1_master',
        group_name: 'Bảng Xoay Nhân Vật Gốc',
        angle: '135° Rear Three-Quarter (Nghiêng sau)',
        angle_id: '135_rear',
        angle_deg: 135,
        z_index: 0,
        save_filename: 'master_135_rear.png',
        view_desc: 'Góc xoay 135° thấy tà áo sau, thắt lưng và búi tóc',
        prompt: `135° rear three-quarter view of the exact same character, back of hair bun and sash, uniform solid ${bgPromptColorEn} background --ar 16:9`,
        count: 1,
      },
      {
        name: 'master_180_back',
        part_id: 'master_character',
        part_name: 'Nhân Vật Gốc Toàn Thân',
        group_id: 'step1_master',
        group_name: 'Bảng Xoay Nhân Vật Gốc',
        angle: '180° Back (Sau lưng)',
        angle_id: '180_back',
        angle_deg: 180,
        z_index: 0,
        save_filename: 'master_180_back.png',
        view_desc: 'Góc sau lưng 180° thấy trọn vẹn suối tóc và lưng áo',
        prompt: `180° rear back view of the exact same character, complete back hair mantle and costume spine, uniform solid ${bgPromptColorEn} background --ar 16:9`,
        count: 1,
      },
      {
        name: 'master_top_down',
        part_id: 'master_character',
        part_name: 'Nhân Vật Gốc Toàn Thân',
        group_id: 'step1_master',
        group_name: 'Bảng Xoay Nhân Vật Gốc',
        angle: 'Top-Down (Đỉnh đầu)',
        angle_id: 'top_down',
        angle_deg: 90,
        z_index: 0,
        save_filename: 'master_top_down.png',
        view_desc: 'Góc phụ soi đỉnh đầu từ trên xuống thấy đường rẽ ngôi và trâm cài',
        prompt: `Top-down view looking downward at the character head crown, hair parting and hair accessories, uniform solid ${bgPromptColorEn} background --ar 16:9`,
        count: 1,
      },
    ];

    promptJSON = JSON.stringify(
      config.include_base_prompt === false
        ? { prompts: step1Prompts }
        : { base_prompt: baseDescEn, prompts: step1Prompts },
      null,
      2
    );

    const negativePrompt = 'realistic human face, small realistic eyes, 3D CGI render, photorealism, live-action, western comic style, ugly anatomy, deformed face, muddy colors, bad eyes, realistic skin texture, realistic wrinkles, dull eyes, pores, text, letters, words, labels, watermark, signature, bad proportions, divider lines, grid frames';
    const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;
    const promptGemini = promptVietnamese;
    return { promptEnglish, promptVietnamese, promptJSON, promptGemini, gridStructureGuide, negativePrompt, fullCopyText };
  }

  // 1. MASTER MODULAR 2D SPRITE SHEET 2×3, 1×4 & SINGLE 1:1 ARCHITECTURE
  if (
    sheet === 'single_isolated_1x1' ||
    sheet === 'single_part' ||
    sheet === 'seamless_turnaround_1x4' ||
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
    const artStyleEn =
      config.custom_character_style?.trim() ||
      config.character_style?.trim() ||
      '2D Chinese Guoman / Xianxia Anime Artstyle';

    interface Asset2DComponentDef {
      id: string;
      nameVi: string;
      titleEn: string;
      summaryEn: string;
      includedGeometry: string[];
      excludedGeometry: string[];
      rearVisibility: 'visible' | 'hidden' | 'conditional';
      groupId: string;
      groupNameVi: string;
      zIndex: number;
      filePrefix: string;
    }

    let comp: Asset2DComponentDef;

    switch (config.part_type) {
      case 'toc_truoc':
        comp = {
          id: 'toc_truoc',
          nameVi: 'Mái Tóc Trước (Front Bangs Fringe)',
          titleEn: 'EXCLUSIVELY THE FLOATING FRONT BANGS / FRONT FRINGE HAIR LAYER.',
          summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:
This component is EXCLUSIVELY the front fringe bangs hair layer physically hovering in front of the forehead and face (${hairColInfo.en}, ${hairTexInfo.en}, ${hairLenInfo.en}${hairAccInfo.en !== 'none' ? `, ${hairAccInfo.en}` : ''}).
The front bangs must float as an independent 2D hair cluster, completely separated and severed from the rest of the head, face, and back hair.
DO NOT attach any back hair, rear hair mantle, ponytail, hair bun, top scalp, or facial skin!
In Cell [1,2] (180° Rear Back), because the front bangs are physically located on the front of the head and 100% occluded from behind, THIS CELL MUST REMAIN COMPLETELY EMPTY CHROMA GREEN (#00FF00).`,
          includedGeometry: [
            'floating front fringe bangs locks',
            'hair strands crossing in front of the forehead',
            'front fringe tips and middle locks belonging strictly to the front-bangs layer',
          ],
          excludedGeometry: [
            'back hair',
            'rear hair mantle',
            'hair falling behind the neck or shoulders',
            'hair bun on the back of the head',
            'top scalp hair mass',
            'head silhouette',
            'face',
            'forehead skin',
            'scalp',
            'ears',
            'eyebrows',
            'eyes',
            'eyelashes',
            'nose',
            'mouth',
            'neck',
            'body',
          ],
          rearVisibility: 'hidden',
          groupId: '01_head_face',
          groupNameVi: 'Khuôn Mặt & Ngũ Quan',
          zIndex: 50,
          filePrefix: '05_toc_truoc',
        };
        break;

      case 'toc_sau':
        comp = {
          id: 'toc_sau',
          nameVi: 'Suối Tóc Sau Lưng (Back Hair Mantle)',
          titleEn: 'EXCLUSIVELY THE REAR BACK HAIR MANTLE / BACK HAIR VOLUME LAYER.',
          summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:
The front bangs (mái tóc trước) and facial features have ALREADY been separated into independent layers!
Therefore, across ALL 6 views (including Front 0°, 45°, 90°, High Angle, Low Angle, and 180° Back), this asset contains ONLY the back hair mass, rear hair bun/crown, and long flowing hair streams behind the back (${hairColInfo.en}, ${hairTexInfo.en}, ${hairLenInfo.en}).
In FRONT (0°) and THREE-QUARTER (45°) views, the front-center area where the face and front bangs belong MUST REMAIN A COMPLETELY HOLLOW / EMPTY GAP for later puppet assembly.
DO NOT include any front bangs, front fringe, forehead locks, forehead skin, or facial features!`,
          includedGeometry: [
            'rear back hair mass',
            'flowing back hair mantle cascading behind the shoulders and spine',
            'rear hair bun / hair crown ornaments on the rear of the head',
            'hollow empty front-center space in front views where face and bangs assemble',
          ],
          excludedGeometry: [
            'front bangs',
            'front fringe',
            'forehead hair',
            'front facial hair framing the forehead',
            'forehead skin',
            'face silhouette',
            'eyes',
            'eyebrows',
            'nose',
            'mouth',
            'cheeks',
            'chin',
            'mannequin head base',
          ],
          rearVisibility: 'visible',
          groupId: '01_head_face',
          groupNameVi: 'Khuôn Mặt & Ngũ Quan',
          zIndex: 10,
          filePrefix: '01_toc_sau',
        };
        break;

      case 'khuon_mat_no_face':
      case 'khuon_mat':
        comp = {
          id: 'khuon_mat_no_face',
          nameVi: 'Khuôn Mặt Trần Không Ngũ Quan (Blank Face Base)',
          titleEn: 'EXCLUSIVELY THE BLANK PORCELAIN FACE SKIN / HEAD BASE (NO HAIR, NO FEATURES).',
          summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:
A completely featureless, blank anime head and facial skin silhouette.
ABSOLUTELY NO hair of any kind (NO front bangs, NO back hair, NO side hair).
ABSOLUTELY NO facial features (NO eyes, NO eyebrows, NO nose, NO mouth).
Pure clean porcelain skin mannequin base for assembling modular eyes, nose, mouth and hair layers.`,
          includedGeometry: [
            'blank facial skin silhouette',
            'forehead skin surface',
            'cheeks',
            'jawline',
            'chin',
            'neck connection base',
          ],
          excludedGeometry: [
            'hair of any kind',
            'front bangs',
            'front fringe',
            'side hair',
            'back hair',
            'hair accessories',
            'eyebrows',
            'eyes',
            'eyelashes',
            'iris',
            'pupil',
            'sclera',
            'nose',
            'mouth',
            'ears',
            'clothing',
            'body',
          ],
          rearVisibility: 'visible',
          groupId: '01_head_face',
          groupNameVi: 'Khuôn Mặt & Ngũ Quan',
          zIndex: 30,
          filePrefix: '03_khuon_mat',
        };
        break;

      case 'trong_den_iris':
        comp = {
          id: 'trong_den_iris',
          nameVi: 'Mống Mắt & Con Ngươi Màu (Iris & Pupil Layer)',
          titleEn: 'EXCLUSIVELY THE PAIR OF ANIME IRISES AND PUPILS.',
          summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:
Isolated pair of floating circular anime iris discs and pupils (${eyeColInfo.en}) with internal color gradient reflections.
DO NOT include sclera, eyelids, eyelashes, skin, or head!
In Cell [1,2] (180° Rear Back), the eyes are 100% occluded, so Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
          includedGeometry: [
            'left circular iris disc and pupil',
            'right circular iris disc and pupil',
            'internal iris color gradient and luster',
          ],
          excludedGeometry: [
            'sclera',
            'white of eyes',
            'eyelids',
            'eyelashes',
            'eyebrows',
            'face skin',
            'head',
            'hair',
          ],
          rearVisibility: 'hidden',
          groupId: '01_head_face',
          groupNameVi: 'Khuôn Mặt & Ngũ Quan',
          zIndex: 42,
          filePrefix: '04a_trong_den_iris',
        };
        break;

      case 'trong_trang':
        comp = {
          id: 'trong_trang',
          nameVi: 'Tròng Trắng / Hốc Mắt (Sclera Base Layer)',
          titleEn: 'EXCLUSIVELY THE PAIR OF ANIME SCLERA (EYE SOCKET WHITES).',
          summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:
Isolated pair of smooth pure white anime sclera base shapes with subtle upper socket shadow.
DO NOT include iris, pupil, highlights, eyelids, face skin, or head!
In Cell [1,2] (180° Rear Back), Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
          includedGeometry: [
            'left white sclera shape',
            'right white sclera shape',
            'subtle upper eye-socket shadow gradient',
          ],
          excludedGeometry: [
            'iris',
            'pupil',
            'highlights',
            'eyelids',
            'eyelashes',
            'eyebrows',
            'face skin',
            'head',
            'hair',
          ],
          rearVisibility: 'hidden',
          groupId: '01_head_face',
          groupNameVi: 'Khuôn Mặt & Ngũ Quan',
          zIndex: 41,
          filePrefix: '04b_trong_trang',
        };
        break;

      case 'diem_sang_mat':
        comp = {
          id: 'diem_sang_mat',
          nameVi: 'Điểm Sáng Mắt (Eye Sparkles & Highlights)',
          titleEn: 'EXCLUSIVELY THE EYE SPARKLES AND HIGHLIGHT GLINTS.',
          summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:
Isolated crisp pure white reflection dots and star glints for anime eyes.
DO NOT include iris, pupil, sclera, eyelids, face skin, or head!
In Cell [1,2] (180° Rear Back), Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
          includedGeometry: [
            'crisp circular white glint spots',
            'starburst highlight glints',
            'reflection sparkle shapes',
          ],
          excludedGeometry: [
            'iris',
            'pupil',
            'sclera',
            'eyelids',
            'face skin',
            'head',
            'hair',
          ],
          rearVisibility: 'hidden',
          groupId: '01_head_face',
          groupNameVi: 'Khuôn Mặt & Ngũ Quan',
          zIndex: 43,
          filePrefix: '04c_diem_sang_mat',
        };
        break;

      case 'mi_mat':
        comp = {
          id: 'mi_mat',
          nameVi: 'Mi Mắt & Chớp Mắt (Eyelids & Blink Keyframes)',
          titleEn: 'EXCLUSIVELY THE EYELIDS AND BLINK KEYFRAME CONTOURS.',
          summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:
Isolated crisp anime upper/lower eyelid lineart and blinking stages.
DO NOT include iris, pupil, sclera, eyebrows, nose, face skin, or head!
In Cell [1,2] (180° Rear Back), Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
          includedGeometry: [
            'upper lash line',
            'lower lash line',
            'eyelid crease line',
            'blink keyframe contours (open, half-closed, closed)',
          ],
          excludedGeometry: [
            'iris',
            'pupil',
            'sclera',
            'eyebrows',
            'nose',
            'face skin',
            'head',
            'hair',
          ],
          rearVisibility: 'hidden',
          groupId: '01_head_face',
          groupNameVi: 'Khuôn Mặt & Ngũ Quan',
          zIndex: 44,
          filePrefix: '04d_mi_mat',
        };
        break;

      case 'long_may':
        comp = {
          id: 'long_may',
          nameVi: 'Cặp Lông Mày (Eyebrows Only)',
          titleEn: 'EXCLUSIVELY THE PAIR OF ANIME EYEBROWS.',
          summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:
Two isolated eyebrow hair strokes floating independently in space.
DO NOT include forehead skin, DO NOT include eyes, DO NOT include hair, DO NOT include head!
In Cell [1,2] (180° Rear Back), the eyebrows are 100% occluded, so Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
          includedGeometry: [
            'left eyebrow stroke',
            'right eyebrow stroke',
          ],
          excludedGeometry: [
            'forehead skin',
            'face skin',
            'eyes',
            'eyelashes',
            'hair',
            'nose',
            'mouth',
            'head',
          ],
          rearVisibility: 'hidden',
          groupId: '01_head_face',
          groupNameVi: 'Khuôn Mặt & Ngũ Quan',
          zIndex: 45,
          filePrefix: '04e_long_may',
        };
        break;

      case 'mui':
        comp = {
          id: 'mui',
          nameVi: 'Sống Mũi (Nose Only)',
          titleEn: 'EXCLUSIVELY THE ANIME NOSE BRIDGE AND NOSE TIP.',
          summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:
Include ONLY the delicate anime nose bridge contour and tip (${noseInfo.en}).
DO NOT include eyes, DO NOT include mouth, DO NOT include chin, DO NOT include cheeks, DO NOT include facial skin outside the nose!
In Cell [1,2] (180° Rear Back), the nose is 100% occluded, so Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
          includedGeometry: [
            'nose bridge contour line',
            'nose tip outline and subtle minimalist shading dot',
          ],
          excludedGeometry: [
            'eyes',
            'eyebrows',
            'mouth',
            'chin',
            'cheeks',
            'forehead',
            'facial skin outside the nose',
            'hair',
            'head',
          ],
          rearVisibility: 'hidden',
          groupId: '01_head_face',
          groupNameVi: 'Khuôn Mặt & Ngũ Quan',
          zIndex: 35,
          filePrefix: '04f_mui',
        };
        break;

      case 'doi_tai':
      case 'mui_tai':
        comp = {
          id: 'doi_tai',
          nameVi: 'Đôi Tai (Ears Only)',
          titleEn: 'EXCLUSIVELY THE PAIR OF ANIME EARS.',
          summaryEn: 'Isolated pair of anime ears with earlobe contour and inner ear cartilage lines.\nDO NOT include face skin, hair, head, neck, or body!',
          includedGeometry: [
            'left ear outer and inner cartilage',
            'right ear outer and inner cartilage',
            'earlobes',
          ],
          excludedGeometry: [
            'face skin',
            'hair',
            'head',
            'neck',
            'body',
          ],
          rearVisibility: 'visible',
          groupId: '01_head_face',
          groupNameVi: 'Khuôn Mặt & Ngũ Quan',
          zIndex: 26,
          filePrefix: '04g_doi_tai',
        };
        break;

      case 'mieng':
        comp = {
          id: 'mieng',
          nameVi: 'Khẩu Hình Miệng (Mouth & Lips)',
          titleEn: 'EXCLUSIVELY THE ANIME MOUTH AND LIP CONTOURS.',
          summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:
Include ONLY the lips and mouth opening contour (${mouthInfo.en}).
The mouth is an independent floating 2D sticker layer.
DO NOT include nose, DO NOT include chin, DO NOT include cheeks, DO NOT include surrounding facial skin, DO NOT include head!
In Cell [1,2] (180° Rear Back), the mouth is 100% occluded, so Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
          includedGeometry: [
            'upper lip line and color',
            'lower lip line and color',
            'mouth expression contour',
          ],
          excludedGeometry: [
            'nose',
            'chin',
            'cheeks',
            'facial skin surrounding the mouth',
            'eyes',
            'eyebrows',
            'hair',
            'head',
          ],
          rearVisibility: 'hidden',
          groupId: '01_head_face',
          groupNameVi: 'Khuôn Mặt & Ngũ Quan',
          zIndex: 36,
          filePrefix: '04h_mieng',
        };
        break;

      case 'mat':
        comp = {
          id: 'mat',
          nameVi: 'Đôi Mắt Tổng Hợp (Full Anime Eyes)',
          titleEn: 'EXCLUSIVELY THE COMPLETE PAIR OF ANIME EYES.',
          summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:
Include complete pair of anime eyes (${eyeShapeInfo.en}, ${eyeColInfo.en}).
The eyes must float as an isolated independent 2D sticker layer.
DO NOT include face skin, forehead, eyebrows, nose, mouth, hair, or head!
In Cell [1,2] (180° Rear Back), the eyes are 100% occluded, so Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
          includedGeometry: [
            'left eye complete structure (sclera, iris, pupil, lash line)',
            'right eye complete structure (sclera, iris, pupil, lash line)',
            'internal eye glints and reflections',
          ],
          excludedGeometry: [
            'face skin',
            'forehead',
            'eyebrows',
            'nose',
            'mouth',
            'cheeks',
            'hair',
            'head',
          ],
          rearVisibility: 'hidden',
          groupId: '01_head_face',
          groupNameVi: 'Khuôn Mặt & Ngũ Quan',
          zIndex: 40,
          filePrefix: '04_ngu_quan_mat',
        };
        break;

      case 'than_co_ban':
        comp = {
          id: 'than_co_ban',
          nameVi: 'Thân Ngực & Eo Áo Giáp (Torso & Chest Armor)',
          titleEn: 'EXCLUSIVELY THE TORSO AND CHEST OUTFIT SEGMENT.',
          summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:
Costume chest tunic, waist sash, and collar garment (${costumeInfo.en}, ${costumeColorVi}).
DO NOT include head, neck, arms, sleeves, hands, legs, feet, or flowing cape!`,
          includedGeometry: [
            'chest tunic / armor plate',
            'waistband / sash',
            'upper torso garment body',
          ],
          excludedGeometry: [
            'head',
            'neck',
            'shoulders / arm sleeves',
            'arms',
            'hands',
            'legs',
            'feet',
            'flowing cape',
          ],
          rearVisibility: 'visible',
          groupId: '02_torso_arms',
          groupNameVi: 'Khớp Xương Thân & Cánh Tay',
          zIndex: 20,
          filePrefix: '02_than_co_ban',
        };
        break;

      case 'canh_tay_trai':
        comp = {
          id: 'canh_tay_trai',
          nameVi: 'Cánh Tay Trái - Bắp Tay (Left Upper Arm: Vai → Khuỷu)',
          titleEn: 'EXCLUSIVELY THE LEFT UPPER ARM SEGMENT FROM SHOULDER TO ELBOW.',
          summaryEn: `Left upper bicep arm sleeve segment (${costumeColorVi}).
DO NOT include torso, chest, head, forearm, wrist, hand, or weapon!`,
          includedGeometry: [
            'left upper arm bicep',
            'sleeve fabric covering the left upper arm',
          ],
          excludedGeometry: [
            'torso',
            'chest',
            'neck',
            'head',
            'forearm',
            'wrist',
            'hand',
            'weapon',
          ],
          rearVisibility: 'visible',
          groupId: '02_torso_arms',
          groupNameVi: 'Khớp Xương Thân & Cánh Tay',
          zIndex: 21,
          filePrefix: '02a_canh_tay_trai',
        };
        break;

      case 'cang_tay_trai':
        comp = {
          id: 'cang_tay_trai',
          nameVi: 'Cẳng Tay Trái (Left Forearm: Khuỷu → Cổ tay)',
          titleEn: 'EXCLUSIVELY THE LEFT FOREARM SEGMENT FROM ELBOW TO WRIST.',
          summaryEn: `Left forearm sleeve and bracer segment (${costumeColorVi}).
DO NOT include upper arm, shoulder, torso, hand, fingers, or weapon!`,
          includedGeometry: [
            'left forearm',
            'forearm bracer / cuff / sleeve fabric',
          ],
          excludedGeometry: [
            'upper arm',
            'shoulder',
            'torso',
            'hand',
            'fingers',
            'weapon',
          ],
          rearVisibility: 'visible',
          groupId: '02_torso_arms',
          groupNameVi: 'Khớp Xương Thân & Cánh Tay',
          zIndex: 22,
          filePrefix: '02b_cang_tay_trai',
        };
        break;

      case 'ban_tay_trai':
        comp = {
          id: 'ban_tay_trai',
          nameVi: 'Bàn Tay Trái (Left Hand & Palm)',
          titleEn: 'EXCLUSIVELY THE LEFT HAND FROM WRIST TO FINGERTIPS.',
          summaryEn: 'Left hand, palm, and fingers in specified pose.\nDO NOT include forearm, elbow, arm, torso, or weapon!',
          includedGeometry: [
            'left palm',
            'left fingers',
            'wrist joint connection line',
          ],
          excludedGeometry: [
            'forearm',
            'elbow',
            'upper arm',
            'torso',
            'weapon',
          ],
          rearVisibility: 'visible',
          groupId: '02_torso_arms',
          groupNameVi: 'Khớp Xương Thân & Cánh Tay',
          zIndex: 23,
          filePrefix: '02c_ban_tay_trai',
        };
        break;

      case 'canh_tay_phai':
        comp = {
          id: 'canh_tay_phai',
          nameVi: 'Cánh Tay Phải - Bắp Tay (Right Upper Arm: Vai → Khuỷu)',
          titleEn: 'EXCLUSIVELY THE RIGHT UPPER ARM SEGMENT FROM SHOULDER TO ELBOW.',
          summaryEn: `Right upper bicep arm sleeve segment (${costumeColorVi}).
DO NOT include torso, chest, head, forearm, wrist, hand, or weapon!`,
          includedGeometry: [
            'right upper arm bicep',
            'sleeve fabric covering the right upper arm',
          ],
          excludedGeometry: [
            'torso',
            'chest',
            'neck',
            'head',
            'forearm',
            'wrist',
            'hand',
            'weapon',
          ],
          rearVisibility: 'visible',
          groupId: '02_torso_arms',
          groupNameVi: 'Khớp Xương Thân & Cánh Tay',
          zIndex: 19,
          filePrefix: '02d_canh_tay_phai',
        };
        break;

      case 'cang_tay_phai':
        comp = {
          id: 'cang_tay_phai',
          nameVi: 'Cẳng Tay Phải (Right Forearm: Khuỷu → Cổ tay)',
          titleEn: 'EXCLUSIVELY THE RIGHT FOREARM SEGMENT FROM ELBOW TO WRIST.',
          summaryEn: `Right forearm sleeve and bracer segment (${costumeColorVi}).
DO NOT include upper arm, shoulder, torso, hand, fingers, or weapon!`,
          includedGeometry: [
            'right forearm',
            'forearm bracer / cuff / sleeve fabric',
          ],
          excludedGeometry: [
            'upper arm',
            'shoulder',
            'torso',
            'hand',
            'fingers',
            'weapon',
          ],
          rearVisibility: 'visible',
          groupId: '02_torso_arms',
          groupNameVi: 'Khớp Xương Thân & Cánh Tay',
          zIndex: 18,
          filePrefix: '02e_cang_tay_phai',
        };
        break;

      case 'ban_tay_phai':
        comp = {
          id: 'ban_tay_phai',
          nameVi: 'Bàn Tay Phải (Right Hand & Palm)',
          titleEn: 'EXCLUSIVELY THE RIGHT HAND FROM WRIST TO FINGERTIPS.',
          summaryEn: 'Right hand, palm, and fingers in specified pose.\nDO NOT include forearm, elbow, arm, torso, or weapon!',
          includedGeometry: [
            'right palm',
            'right fingers',
            'wrist joint connection line',
          ],
          excludedGeometry: [
            'forearm',
            'elbow',
            'upper arm',
            'torso',
            'weapon',
          ],
          rearVisibility: 'visible',
          groupId: '02_torso_arms',
          groupNameVi: 'Khớp Xương Thân & Cánh Tay',
          zIndex: 17,
          filePrefix: '02f_ban_tay_phai',
        };
        break;

      case 'dui_trai':
        comp = {
          id: 'dui_trai',
          nameVi: 'Đùi Trái (Left Thigh: Hông → Gối)',
          titleEn: 'EXCLUSIVELY THE LEFT THIGH SEGMENT FROM HIP TO KNEE.',
          summaryEn: `Left thigh garment/pants limb segment (${costumeColorVi}).
DO NOT include torso, pelvis, shin, boot, or foot!`,
          includedGeometry: [
            'left thigh',
            'fabric/pants covering the left thigh',
            'hip joint connection line',
          ],
          excludedGeometry: [
            'torso',
            'pelvis',
            'shin',
            'boot',
            'foot',
          ],
          rearVisibility: 'visible',
          groupId: '03_legs_feet',
          groupNameVi: 'Khớp Xương Chân & Giày',
          zIndex: 15,
          filePrefix: '03a_dui_trai',
        };
        break;

      case 'cang_chan_trai':
        comp = {
          id: 'cang_chan_trai',
          nameVi: 'Cẳng Chân & Giày Ủng Trái (Left Shin & Boot: Gối → Gót)',
          titleEn: 'EXCLUSIVELY THE LEFT SHIN AND BOOT SEGMENT FROM KNEE TO FOOT.',
          summaryEn: `Left lower leg and boot (${costumeColorVi}).
DO NOT include thigh, hip, torso, or right leg!`,
          includedGeometry: [
            'left shin',
            'left boot / footwear',
            'knee cap guard',
          ],
          excludedGeometry: [
            'thigh',
            'hip',
            'torso',
            'right leg',
          ],
          rearVisibility: 'visible',
          groupId: '03_legs_feet',
          groupNameVi: 'Khớp Xương Chân & Giày',
          zIndex: 16,
          filePrefix: '03b_cang_chan_trai',
        };
        break;

      case 'dui_phai':
        comp = {
          id: 'dui_phai',
          nameVi: 'Đùi Phải (Right Thigh: Hông → Gối)',
          titleEn: 'EXCLUSIVELY THE RIGHT THIGH SEGMENT FROM HIP TO KNEE.',
          summaryEn: `Right thigh garment/pants limb segment (${costumeColorVi}).
DO NOT include torso, pelvis, shin, boot, or foot!`,
          includedGeometry: [
            'right thigh',
            'fabric/pants covering the right thigh',
            'hip joint connection line',
          ],
          excludedGeometry: [
            'torso',
            'pelvis',
            'shin',
            'boot',
            'foot',
          ],
          rearVisibility: 'visible',
          groupId: '03_legs_feet',
          groupNameVi: 'Khớp Xương Chân & Giày',
          zIndex: 13,
          filePrefix: '03c_dui_phai',
        };
        break;

      case 'cang_chan_phai':
        comp = {
          id: 'cang_chan_phai',
          nameVi: 'Cẳng Chân & Giày Ủng Phải (Right Shin & Boot: Gối → Gót)',
          titleEn: 'EXCLUSIVELY THE RIGHT SHIN AND BOOT SEGMENT FROM KNEE TO FOOT.',
          summaryEn: `Right lower leg and boot (${costumeColorVi}).
DO NOT include thigh, hip, torso, or left leg!`,
          includedGeometry: [
            'right shin',
            'right boot / footwear',
            'knee cap guard',
          ],
          excludedGeometry: [
            'thigh',
            'hip',
            'torso',
            'left leg',
          ],
          rearVisibility: 'visible',
          groupId: '03_legs_feet',
          groupNameVi: 'Khớp Xương Chân & Giày',
          zIndex: 14,
          filePrefix: '03d_cang_chan_phai',
        };
        break;

      case 'ao_choang':
      case 'trang_phuc':
        comp = {
          id: 'ao_choang',
          nameVi: 'Áo Choàng / Tà Áo Bay (Cape & Robe Flow)',
          titleEn: 'EXCLUSIVELY THE FLOWING CAPE / MANTLE FABRIC LAYER.',
          summaryEn: `Flowing cape and fabric ribbons (${costumeColorVi}).
DO NOT include character body, chest, arms, hands, legs, or head!`,
          includedGeometry: [
            'back cape drape',
            'flowing ribbon tails',
            'shoulder clasp attachments',
          ],
          excludedGeometry: [
            'torso',
            'chest',
            'arms',
            'hands',
            'legs',
            'head',
            'character body',
          ],
          rearVisibility: 'visible',
          groupId: '04_props_costumes',
          groupNameVi: 'Trang Phục Bay & Vũ Khí',
          zIndex: 8,
          filePrefix: '06a_ao_choang',
        };
        break;

      case 'vu_khi':
      default:
        comp = {
          id: 'vu_khi',
          nameVi: 'Vũ Khí & Pháp Bảo (Weapons & Props)',
          titleEn: 'EXCLUSIVELY THE WEAPON / PROP ARTIFACT.',
          summaryEn: `Isolated weapon artifact (${propInfo.en}).
DO NOT include character, hands, arms, body, or scenery!`,
          includedGeometry: [
            'blade / weapon body',
            'hilt / handle',
            'magical glow / aura directly emanating from weapon',
          ],
          excludedGeometry: [
            'character',
            'hands',
            'arms',
            'body',
            'background scenery',
          ],
          rearVisibility: 'visible',
          groupId: '04_props_costumes',
          groupNameVi: 'Trang Phục Bay & Vũ Khí',
          zIndex: 60,
          filePrefix: '06_vu_khi',
        };
        break;
    }

    const partDescriptionFormatted = [
      comp.titleEn,
      comp.summaryEn ? `\n${comp.summaryEn}` : '',
      '\nInclude ONLY:',
      ...comp.includedGeometry.map((item) => `- ${item}`),
      '\nDO NOT include:',
      ...comp.excludedGeometry.map((item) => `- ${item}`),
    ]
      .filter(Boolean)
      .join('\n');

    const baseRefPrompt = `2D ${artStyleEn} character asset: ${config.gender === 'nam' ? 'Male' : 'Female'}, ${bodyPropInfo.en}, hair (${hairColInfo.en}, ${hairTexInfo.en}), costume (${costumeInfo.en}, color: ${costumeColorVi}), solid ${bgPromptColorEn} background, crisp 2D anime lineart, cel shading, zero shadows`;

    // ──────────────────────────────────────────────────────────────────────────
    // A. DẠNG 1:1 ẢNH ĐƠN SIÊU NÉT (SINGLE ISOLATED 1:1 ASSET — NO GRID, MAX DETAIL)
    // ──────────────────────────────────────────────────────────────────────────
    if (sheet === 'single_isolated_1x1' || sheet === 'single_part' || config.aspect_ratio === '1:1') {
      const angle = config.view_angle || 'front';
      let angleLabelVi = '0° Chính diện (Front 0°)';
      let angleLabelEn = 'Orthographic Front 0° view';
      let angleDescEn = 'Camera directly facing the front of the isolated component';

      if (angle === 'three_quarter' || angle === '45' || angle === 'three_quarter_45') {
        angleLabelVi = '45° Nghiêng 3/4 (Three-Quarter 45°)';
        angleLabelEn = 'Orthographic Three-Quarter 45° view';
        angleDescEn = 'Camera rotated 45 degrees showing three-quarter depth';
      } else if (angle === 'profile_side' || angle === '90' || angle === 'side_90') {
        angleLabelVi = '90° Nhìn ngang (Side Profile 90°)';
        angleLabelEn = 'Orthographic Side Profile 90° view';
        angleDescEn = 'Camera rotated 90 degrees showing clean side silhouette';
      } else if (angle === 'back' || angle === '180' || angle === 'rear_180') {
        angleLabelVi = '180° Sau lưng (Rear Back 180°)';
        angleLabelEn = 'Orthographic Rear Back 180° view';
        angleDescEn = comp.rearVisibility === 'hidden'
          ? 'Pure empty space (hidden from behind)'
          : 'Camera directly facing the rear back of the component';
      } else if (angle === 'high_angle' || angle === 'top_down') {
        angleLabelVi = 'Trên cao nhìn xuống (High Angle)';
        angleLabelEn = 'Cinematic High-Angle top-down view';
        angleDescEn = 'Camera positioned elevated above looking downward at the component';
      } else if (angle === 'low_angle' || angle === 'bottom_up') {
        angleLabelVi = 'Dưới hất lên (Low Angle)';
        angleLabelEn = 'Cinematic Low-Angle bottom-up view';
        angleDescEn = 'Camera positioned below looking upward at the component';
      }

      if (comp.rearVisibility === 'hidden' && (angle === 'back' || angle === '180' || angle === 'rear_180')) {
        promptEnglish = `masterpiece, 4k resolution, 1:1 aspect ratio, pure solid chroma green background #00FF00, blank empty canvas, no objects, no characters --ar 1:1`;
        promptVietnamese = `【 ẢNH ĐƠN 1:1 — GÓC SAU LƯNG 180°: CHI TIẾT BỊ KHUẤT HOÀN TOÀN 】\n• Linh kiện: ${comp.nameVi}\n• Góc quay: Sau lưng 180°\n• Trạng thái: Bị khuất 100% khi nhìn từ sau lưng ➔ Không cần tạo ảnh cho góc này (hoặc để nền xanh trần #00FF00).`;
      } else {
        promptEnglish = `masterpiece, best quality, ultra detailed, 4k resolution, 1:1 square aspect ratio,

ONE SINGLE ISOLATED 2D ANIME COMPONENT — SINGLE VIEW (${angleLabelEn.toUpperCase()}):
${comp.titleEn}

CAMERA ANGLE:
${angleLabelEn} — ${angleDescEn}

Include ONLY:
${comp.includedGeometry.map((item) => `- ${item}`).join('\n')}

DO NOT include:
${comp.excludedGeometry.slice(0, 10).map((item) => `- ${item}`).join('\n')}

STRICT ISOLATION RULES:
- Exactly ONE isolated 2D component centered cleanly on screen.
- Absolutely NO full character, NO full body, NO face, NO head silhouette unless specified.
- Absolutely NO multiple views, NO comic panels, NO box frames, NO grid lines, NO borders.
- Art Style: ${artStyleEn}. Clean crisp anime lineart, flat matte cel shading.
- Background: ${bgPromptColorEn} (${bgPromptColorHex}) solid flat uniform color, zero shadows.

NEGATIVE PROMPT:
full character, full body, head, face, extra limbs, multiple views, turnaround, comic panels, grid lines, borders, frames, divider lines, text, letters, numbers, watermark, blurry, 3D CGI render, glow

--ar 1:1`;

        promptVietnamese = `【 ẢNH ĐƠN 1:1 SIÊU NÉT — 1 GÓC QUAY DUY NHẤT (TỶ LỆ 1:1 KHÔNG DÍNH LƯỚI) 】

════════════════════════════════════════════════════════════
1. LINH KIỆN & GÓC QUAY:
════════════════════════════════════════════════════════════
• Linh kiện: ${comp.nameVi}
• Góc quay lựa chọn: ${angleLabelVi}
• Tiêu đề định danh: ${comp.titleEn}
• Thuộc tính bao gồm:
${comp.includedGeometry.map((g) => `   + ${g}`).join('\n')}
• Thành phần LOẠI TRỪ TUYỆT ĐỐI:
${comp.excludedGeometry.slice(0, 8).map((g) => `   - KHÔNG CÓ ${g}`).join('\n')}

════════════════════════════════════════════════════════════
2. ƯU ĐIỂM DẠNG 1:1:
════════════════════════════════════════════════════════════
✅ 100% Canvas tập trung vào đúng 1 chi tiết ➔ Độ phân giải 4K sắc nét nhất.
✅ TUYỆT ĐỐI KHÔNG BỊ DÍNH ĐƯỜNG KẺ LƯỚI HAY KHUNG CHIA Ô ĐEN.`;
      }

      const allComponentAnglesPrompts = [
        {
          name: `${comp.filePrefix}_000_front`,
          part_id: comp.id,
          part_name: comp.nameVi,
          group_id: comp.groupId,
          group_name: comp.groupNameVi,
          angle: '0° Front (Chính diện)',
          angle_id: '000_front',
          angle_deg: 0,
          z_index: comp.zIndex,
          save_filename: `${comp.filePrefix}_000_front.png`,
          view_desc: 'Góc chính diện 0° lơ lửng độc lập',
          prompt: `0° front view of isolated ${comp.titleEn}. Include ONLY: ${comp.includedGeometry.join(', ')}. Zero face, zero body, solid ${bgPromptColorEn} background --ar 1:1`,
          count: 4,
        },
        {
          name: `${comp.filePrefix}_045_three_quarter`,
          part_id: comp.id,
          part_name: comp.nameVi,
          group_id: comp.groupId,
          group_name: comp.groupNameVi,
          angle: '45° Three-Quarter (Nghiêng 3/4)',
          angle_id: '045_three_quarter',
          angle_deg: 45,
          z_index: comp.zIndex,
          save_filename: `${comp.filePrefix}_045_three_quarter.png`,
          view_desc: 'Góc nghiêng 3/4 45 độ',
          prompt: `45° three-quarter view of isolated ${comp.titleEn}, depth and curve silhouette, solid ${bgPromptColorEn} background --ar 1:1`,
          count: 2,
        },
        {
          name: `${comp.filePrefix}_090_side`,
          part_id: comp.id,
          part_name: comp.nameVi,
          group_id: comp.groupId,
          group_name: comp.groupNameVi,
          angle: '90° Side Profile (Nhìn ngang)',
          angle_id: '090_side',
          angle_deg: 90,
          z_index: comp.zIndex,
          save_filename: `${comp.filePrefix}_090_side.png`,
          view_desc: 'Góc nhìn ngang 90 độ',
          prompt: `90° side profile view of isolated ${comp.titleEn}, clean side silhouette, solid ${bgPromptColorEn} background --ar 1:1`,
          count: 2,
        },
        {
          name: `${comp.filePrefix}_high_angle`,
          part_id: comp.id,
          part_name: comp.nameVi,
          group_id: comp.groupId,
          group_name: comp.groupNameVi,
          angle: 'High Angle (Trên cao nhìn xuống)',
          angle_id: 'high_angle_top',
          angle_deg: 90,
          z_index: comp.zIndex,
          save_filename: `${comp.filePrefix}_high_angle_top.png`,
          view_desc: 'Góc quay trên cao nhìn chúc xuống',
          prompt: `High-angle top-down perspective view of isolated ${comp.titleEn}, solid ${bgPromptColorEn} background --ar 1:1`,
          count: 1,
        },
        {
          name: `${comp.filePrefix}_low_angle`,
          part_id: comp.id,
          part_name: comp.nameVi,
          group_id: comp.groupId,
          group_name: comp.groupNameVi,
          angle: 'Low Angle (Dưới hất lên)',
          angle_id: 'low_angle_bottom',
          angle_deg: 90,
          z_index: comp.zIndex,
          save_filename: `${comp.filePrefix}_low_angle_bottom.png`,
          view_desc: 'Góc quay dưới hất ngược lên',
          prompt: `Low-angle bottom-up perspective view of isolated ${comp.titleEn}, solid ${bgPromptColorEn} background --ar 1:1`,
          count: 1,
        },
        {
          name: `${comp.filePrefix}_180_back`,
          part_id: comp.id,
          part_name: comp.nameVi,
          group_id: comp.groupId,
          group_name: comp.groupNameVi,
          angle: '180° Back (Sau lưng)',
          angle_id: '180_back',
          angle_deg: 180,
          z_index: comp.zIndex,
          save_filename: `${comp.filePrefix}_180_back.png`,
          view_desc: comp.rearVisibility === 'hidden' ? 'Bị khuất từ sau lưng (ô rỗng)' : 'Góc nhìn từ sau lưng',
          prompt: comp.rearVisibility === 'hidden'
            ? `pure empty solid ${bgPromptColorEn} background #00FF00, blank empty canvas --ar 1:1`
            : `180° rear back view of isolated ${comp.titleEn}, rear back texture, solid ${bgPromptColorEn} background --ar 1:1`,
          count: 1,
        },
      ];

      const step2Prompts = config.json_scope === 'single_angle'
        ? [
            {
              name: `${comp.filePrefix}_${angle}`,
              part_id: comp.id,
              part_name: comp.nameVi,
              group_id: comp.groupId,
              group_name: comp.groupNameVi,
              angle: angleLabelVi,
              angle_id: angle,
              angle_deg: angle === 'three_quarter' ? 45 : angle === 'profile_side' ? 90 : angle === 'back' ? 180 : 0,
              z_index: comp.zIndex,
              save_filename: `${comp.filePrefix}_${angle}.png`,
              view_desc: angleDescEn,
              prompt: `masterpiece, 4k, 1:1 square, isolated ${comp.titleEn}, angle: ${angleLabelEn}. Include ONLY: ${comp.includedGeometry.join(', ')}. DO NOT include: ${comp.excludedGeometry.slice(0, 8).join(', ')}. Solid uniform ${bgPromptColorEn} background, clean lineart, flat cel shading, zero shadows, no text, no borders --ar 1:1`,
              count: 4,
            },
          ]
        : allComponentAnglesPrompts;

      promptJSON = JSON.stringify(
        config.include_base_prompt === false
          ? { prompts: step2Prompts }
          : { base_prompt: baseRefPrompt, prompts: step2Prompts },
        null,
        2
      );

      const promptGemini = promptVietnamese;
      gridStructureGuide = `📐 Khung 1:1 vuông: 1 Ảnh đơn siêu nét, tải về bấm Tab 1 Cắt Ảnh Đơn để xóa nền tức thì.`;
      const negativePrompt =
        'full character, full body, head, face, extra limbs, multiple views, turnaround, comic panels, grid lines, borders, frames, divider lines, text, letters, numbers, watermark, signature, blurry, 3D CGI render, glow, rim light, color spill';
      const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;

      return {
        promptEnglish,
        promptVietnamese,
        promptJSON,
        promptGemini,
        gridStructureGuide,
        negativePrompt,
        fullCopyText,
      };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // B. DẠNG CHUỖI XOAY NGANG 4 GÓC 16:9 (SEAMLESS 1×4 HORIZONTAL TURNAROUND)
    // ──────────────────────────────────────────────────────────────────────────
    if (sheet === 'seamless_turnaround_1x4' || sheet === 'modular_bangs_3x1' || sheet === 'modular_backhair_3x1' || sheet === 'modular_torso_armor_3x1') {
      promptEnglish = `masterpiece, best quality, ultra detailed, 4k resolution, 16:9 aspect ratio,

SEAMLESS 4-VIEW HORIZONTAL ROTATION SEQUENCE OF ONE SINGLE ISOLATED 2D COMPONENT:
${comp.titleEn}

Four continuous turnaround views arranged side-by-side in one horizontal row on an open seamless background:
1. Front 0° View (Orthographic frontal view)
2. Three-Quarter 45° View (Orthographic 45-degree angle)
3. Side Profile 90° View (Orthographic 90-degree side silhouette)
4. Rear Back 180° View (${
        comp.rearVisibility === 'hidden'
          ? 'EMPTY SPACE: Pure empty chroma green since front-only component'
          : 'Exact rear view showing back details'
      })

Include ONLY:
${comp.includedGeometry.map((item) => `- ${item}`).join('\n')}

DO NOT include:
${comp.excludedGeometry.slice(0, 10).map((item) => `- ${item}`).join('\n')}

LAYOUT RESTRICTIONS:
- All 4 views share the same height and baseline on open seamless background.
- ABSOLUTELY NO GRID LINES, NO BOX FRAMES, NO COMIC PANELS, NO DIVIDER BORDERS.
- Pure isolated component only, NO character body, NO face, NO head.
- Art Style: ${artStyleEn}. Clean anime lineart, flat cel shading.
- Background: ${bgPromptColorEn} (${bgPromptColorHex}) solid flat uniform color.

NEGATIVE PROMPT:
grid lines, divider lines, panel borders, box frames, comic panels, full character, full body, extra limbs, text, labels, watermark, blurry, 3D CGI render, glow

--ar 16:9`;

      promptVietnamese = `【 CHUỖI XOAY NGANG 4 GÓC LIỀN MẠCH (1 HÀNG 16:9 — KHÔNG DÙNG LƯỚI / KHÔNG KHUNG ĐEN) 】

════════════════════════════════════════════════════════════
1. LINH KIỆN BÓC TÁCH:
════════════════════════════════════════════════════════════
• Linh kiện: ${comp.nameVi}
• Bố cục: 4 góc dàn ngang trên 1 hàng (0° Front ➔ 45° Nghiêng ➔ 90° Ngang ➔ 180° Sau lưng)
• Tiêu đề định danh: ${comp.titleEn}
• Nền: Phẳng 1 màu ${bgTextVi}, không có khung hay đường kẻ ngăn cách!`;

      const horizontal1x4Prompts = [
        {
          name: `${comp.filePrefix}_000_front`,
          part_id: comp.id,
          part_name: comp.nameVi,
          group_id: comp.groupId,
          group_name: comp.groupNameVi,
          angle: '0° Front (Chính diện)',
          angle_id: '000_front',
          angle_deg: 0,
          z_index: comp.zIndex,
          save_filename: `${comp.filePrefix}_000_front.png`,
          view_desc: 'Góc chính diện 0° lơ lửng độc lập',
          prompt: `0° front view of isolated ${comp.titleEn}. Include ONLY: ${comp.includedGeometry.join(', ')}. Zero face, zero body, solid ${bgPromptColorEn} background --ar 1:1`,
          count: 4,
        },
        {
          name: `${comp.filePrefix}_045_three_quarter`,
          part_id: comp.id,
          part_name: comp.nameVi,
          group_id: comp.groupId,
          group_name: comp.groupNameVi,
          angle: '45° Three-Quarter (Nghiêng 3/4)',
          angle_id: '045_three_quarter',
          angle_deg: 45,
          z_index: comp.zIndex,
          save_filename: `${comp.filePrefix}_045_three_quarter.png`,
          view_desc: 'Góc nghiêng 3/4 45 độ',
          prompt: `45° three-quarter view of isolated ${comp.titleEn}, depth and curve silhouette, solid ${bgPromptColorEn} background --ar 1:1`,
          count: 2,
        },
        {
          name: `${comp.filePrefix}_090_side`,
          part_id: comp.id,
          part_name: comp.nameVi,
          group_id: comp.groupId,
          group_name: comp.groupNameVi,
          angle: '90° Side Profile (Nhìn ngang)',
          angle_id: '090_side',
          angle_deg: 90,
          z_index: comp.zIndex,
          save_filename: `${comp.filePrefix}_090_side.png`,
          view_desc: 'Góc nhìn ngang 90 độ',
          prompt: `90° side profile view of isolated ${comp.titleEn}, clean side silhouette, solid ${bgPromptColorEn} background --ar 1:1`,
          count: 2,
        },
        {
          name: `${comp.filePrefix}_180_back`,
          part_id: comp.id,
          part_name: comp.nameVi,
          group_id: comp.groupId,
          group_name: comp.groupNameVi,
          angle: '180° Back (Sau lưng)',
          angle_id: '180_back',
          angle_deg: 180,
          z_index: comp.zIndex,
          save_filename: `${comp.filePrefix}_180_back.png`,
          view_desc: comp.rearVisibility === 'hidden' ? 'Bị khuất từ sau lưng (ô rỗng)' : 'Góc nhìn từ sau lưng',
          prompt: comp.rearVisibility === 'hidden'
            ? `pure empty solid ${bgPromptColorEn} background #00FF00, blank empty canvas --ar 1:1`
            : `180° rear back view of isolated ${comp.titleEn}, rear back texture, solid ${bgPromptColorEn} background --ar 1:1`,
          count: 1,
        },
      ];

      promptJSON = JSON.stringify(
        config.include_base_prompt === false
          ? { prompts: horizontal1x4Prompts }
          : { base_prompt: baseRefPrompt, prompts: horizontal1x4Prompts },
        null,
        2
      );

      const promptGemini = promptVietnamese;
      gridStructureGuide = `📐 Khung 16:9 1 Hàng: 4 góc dàn ngang tự nhiên, không dính khung lưới.`;
      const negativePrompt =
        'grid lines, divider lines, panel borders, box frames, comic panels, full character, full body, extra limbs, text, labels, watermark, blurry, 3D CGI render, glow';
      const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;

      return {
        promptEnglish,
        promptVietnamese,
        promptJSON,
        promptGemini,
        gridStructureGuide,
        negativePrompt,
        fullCopyText,
      };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // C. DẠNG BẢNG ĐA GÓC 2×3 CẢI TIẾN (CLEAN MULTI-ANGLE 2×3 SHEET)
    // ──────────────────────────────────────────────────────────────────────────
    promptEnglish = `masterpiece, best quality, ultra detailed, 4k resolution, 16:9 aspect ratio,

MODULAR 2D ANIME SPRITE SHEET — 6 ORTHOGRAPHIC VIEWS ON OPEN SEAMLESS BACKDROP:
${partDescriptionFormatted}

The image contains exactly 6 views of the SAME isolated component arranged neatly in 2 rows of 3 views across an open seamless canvas:

TOP ROW:
1. FRONT 0° (Eye-level orthographic front view)
2. THREE-QUARTER 45° (Eye-level orthographic 45° view)
3. SIDE PROFILE 90° (Eye-level orthographic 90° side profile)

BOTTOM ROW:
4. HIGH ANGLE (Top-down view looking down at the component)
5. LOW ANGLE (Bottom-up view looking up at the component)
6. REAR BACK 180° (${
  comp.rearVisibility === 'hidden'
    ? 'EMPTY SPACE: Pure empty chroma green since front-only component'
    : 'Exact rear view of the SAME component'
})

CRITICAL SEAMLESS LAYOUT RULES:
- Arranged on ONE continuous seamless open solid green background.
- ABSOLUTELY NO GRID LINES, NO BOX FRAMES, NO COMIC PANELS, NO CELL BORDERS, NO BLACK LINES.
- Generous open spacing between sprites.
- All turnaround views share the same height and baseline.
- Pure isolated component only. DO NOT generate full character.
- Art Style: ${artStyleEn}. Clean professional lineart, hard-edge cel shading, flat matte colors.
- Background: ${bgPromptColorEn} (${bgPromptColorHex}) solid flat uniform single color, zero shadows.

NEGATIVE PROMPT:
grid lines, divider lines, cell borders, panel frames, black outlines around cells, comic panels, full character, full body, head, face, extra limbs, text, labels, watermark, blurry, 3D CGI render, glow, rim light

--ar 16:9`;

    promptVietnamese = `【 BẢNG SPRITE 6 GÓC QUAY ĐIỆN ẢNH CHO 1 CHI TIẾT (LƯỚI 2 HÀNG × 3 CỘT — TỶ LỆ 16:9) 】

════════════════════════════════════════════════════════════
1. CHI TIẾT BÓC TÁCH KHỚP XƯƠNG (ISOLATED COMPONENT LAYER):
════════════════════════════════════════════════════════════
• Tên linh kiện: ${comp.nameVi}
• Tiêu đề định danh: ${comp.titleEn}
• Thuộc tính bao gồm (Included Geometry):
${comp.includedGeometry.map((g) => `   + ${g}`).join('\n')}
• Thành phần LOẠI TRỪ TUYỆT ĐỐI (Strictly Excluded):
${comp.excludedGeometry.map((g) => `   - KHÔNG CÓ ${g}`).join('\n')}
• Phong cách vẽ: ${styleLabelVi} (${artStyleEn})
• Trạng thái góc 180° Sau lưng: ${comp.rearVisibility === 'hidden' ? '🟩 BỊ KHUẤT HOÀN TOÀN — ĐỂ Ô RỖNG NỀN XANH LÁ TRẦN (#00FF00)' : '✅ CÓ THỂ NHÌN THẤY TỪ PHÍA SAU — VẼ MẶT SAU CHI TIẾT'}

════════════════════════════════════════════════════════════
2. BỐ CỤC 6 GÓC QUAY (2 HÀNG × 3 CỘT LIỀN MẠCH — KHÔNG VẼ KHUNG ĐEN):
════════════════════════════════════════════════════════════
🔹 HÀNG TRÊN:
   - 1. Chính diện 0° (FRONT 0°)
   - 2. Nghiêng 3/4 45° (THREE-QUARTER 45°)
   - 3. Nhìn ngang 90° (SIDE PROFILE 90°)

🔹 HÀNG DƯỚI:
   - 4. Trên cao nhìn xuống (HIGH ANGLE / TOP-DOWN)
   - 5. Dưới hất lên (LOW ANGLE / BOTTOM-UP)
   - 6. Sau lưng 180° (REAR 180°: ${comp.rearVisibility === 'hidden' ? 'Ô RỖNG HOÀN TOÀN' : 'Mặt sau chi tiết'})

════════════════════════════════════════════════════════════
3. QUY TẮC BẢO ĐẢM KHÔNG DÍNH LƯỚI:
════════════════════════════════════════════════════════════
• Đã xóa toàn bộ từ khóa kích hoạt đường kẻ ô của AI.
• Phông nền: ${bgTextVi}.`;

    const multiAngle2x3Prompts = [
      {
        name: `${comp.filePrefix}_000_front`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: '0° Front (Chính diện)',
        angle_id: '000_front',
        angle_deg: 0,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_000_front.png`,
        view_desc: 'Hàng trên Ô 1: Góc chính diện 0° lơ lửng độc lập',
        prompt: `0° front view of isolated ${comp.titleEn}. Include ONLY: ${comp.includedGeometry.join(', ')}. Zero face, zero body, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 4,
      },
      {
        name: `${comp.filePrefix}_045_three_quarter`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: '45° Three-Quarter (Nghiêng 3/4)',
        angle_id: '045_three_quarter',
        angle_deg: 45,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_045_three_quarter.png`,
        view_desc: 'Hàng trên Ô 2: Góc nghiêng 3/4 45 độ',
        prompt: `45° three-quarter view of isolated ${comp.titleEn}, depth and curve silhouette, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 2,
      },
      {
        name: `${comp.filePrefix}_090_side`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: '90° Side Profile (Nhìn ngang)',
        angle_id: '090_side',
        angle_deg: 90,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_090_side.png`,
        view_desc: 'Hàng trên Ô 3: Góc nhìn ngang 90 độ',
        prompt: `90° side profile view of isolated ${comp.titleEn}, side silhouette, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 2,
      },
      {
        name: `${comp.filePrefix}_high_angle`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: 'High Angle (Trên cao nhìn xuống)',
        angle_id: 'high_angle_top',
        angle_deg: 90,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_high_angle_top.png`,
        view_desc: 'Hàng dưới Ô 4: Góc quay trên cao nhìn chúc xuống',
        prompt: `High-angle top-down perspective view of isolated ${comp.titleEn}, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 1,
      },
      {
        name: `${comp.filePrefix}_low_angle`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: 'Low Angle (Dưới hất lên)',
        angle_id: 'low_angle_bottom',
        angle_deg: 90,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_low_angle_bottom.png`,
        view_desc: 'Hàng dưới Ô 5: Góc quay dưới hất ngược lên',
        prompt: `Low-angle bottom-up perspective view of isolated ${comp.titleEn}, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 1,
      },
      {
        name: `${comp.filePrefix}_180_back`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: '180° Rear Back (Sau lưng)',
        angle_id: '180_back',
        angle_deg: 180,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_180_back.png`,
        view_desc: comp.rearVisibility === 'hidden' ? 'Hàng dưới Ô 6: Bị khuất hoàn toàn (ô rỗng)' : 'Hàng dưới Ô 6: Mặt sau chi tiết',
        prompt: comp.rearVisibility === 'hidden'
          ? `pure empty solid ${bgPromptColorEn} background #00FF00, blank empty canvas --ar 1:1`
          : `180° rear back view of isolated ${comp.titleEn}, rear back texture, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 1,
      },
    ];

    promptJSON = JSON.stringify(
      config.include_base_prompt === false
        ? { prompts: multiAngle2x3Prompts }
        : { base_prompt: baseRefPrompt, prompts: multiAngle2x3Prompts },
      null,
      2
    );

    const promptGemini = promptVietnamese;
    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 2 Hàng × 3 Cột điện ảnh, xếp tự nhiên trên nền xanh.`;

    const negativePrompt =
      'grid lines, divider lines, cell borders, panel frames, black outlines around cells, comic panels, full character, full body, head, face, extra limbs, text, labels, watermark, blurry, 3D CGI render, glow, rim light';
    const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;

    return {
      promptEnglish,
      promptVietnamese,
      promptJSON,
      promptGemini,
      gridStructureGuide,
      negativePrompt,
      fullCopyText,
    };
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
• Mục tiêu: Bóc tách mái tóc thành 3 tầng độ sâu Z-Index đồng bộ xoay 360° (Mái trước → Tóc mai → Tóc sau lưng) để ghép khớp 100% vào nhân vật mà không bị phụ kiện thừa.
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

/**
 * Metadata info extracted when parsing an asset image filename
 */
export interface ParsedPartFilenameInfo {
  part_id: string;
  part_name: string;
  group_id: string;
  group_name: string;
  angle_id: string;
  angle_name: string;
  angle_deg: number;
  z_index: number;
  is_master_character: boolean;
  save_filename: string;
}

/**
 * Parses an asset image filename to auto-detect its character part, angle, z_index, and group
 * @param filename - e.g. "05_toc_truoc_000_front.png", "toc_sau_180_back.jpg", "master_045_three_quarter.png"
 */
export function parsePartFilename(filename: string): ParsedPartFilenameInfo | null {
  if (!filename) return null;
  const cleanName = filename.toLowerCase().replace(/\.[a-zA-Z0-9]+$/, '');

  // Check master character turnaround
  if (cleanName.includes('master') || cleanName.includes('character_turnaround')) {
    let angle_deg = 0;
    let angle_id = '000_front';
    let angle_name = '0° Front (Chính diện)';

    if (cleanName.includes('045') || cleanName.includes('three_quarter') || cleanName.includes('45')) {
      angle_deg = 45;
      angle_id = '045_three_quarter';
      angle_name = '45° Three-Quarter (Nghiêng 3/4)';
    } else if (cleanName.includes('090') || cleanName.includes('side') || cleanName.includes('90')) {
      angle_deg = 90;
      angle_id = '090_side';
      angle_name = '90° Side Profile (Nhìn ngang)';
    } else if (cleanName.includes('135') || cleanName.includes('rear_three_quarter')) {
      angle_deg = 135;
      angle_id = '135_rear';
      angle_name = '135° Rear Three-Quarter (Nghiêng sau)';
    } else if (cleanName.includes('180') || cleanName.includes('back')) {
      angle_deg = 180;
      angle_id = '180_back';
      angle_name = '180° Back (Sau lưng)';
    } else if (cleanName.includes('top') || cleanName.includes('dinh_dau')) {
      angle_deg = 90;
      angle_id = 'top_down';
      angle_name = 'Top-Down (Đỉnh đầu)';
    }

    return {
      part_id: 'master_character',
      part_name: 'Nhân Vật Gốc Toàn Thân',
      group_id: 'step1_master',
      group_name: 'Bảng Xoay Nhân Vật Gốc',
      angle_id,
      angle_name,
      angle_deg,
      z_index: 0,
      is_master_character: true,
      save_filename: `master_${angle_id}.png`,
    };
  }

  const partMap: Record<
    string,
    { part_id: string; part_name: string; group_id: string; group_name: string; z_index: number; filePrefix: string }
  > = {
    toc_truoc: { part_id: 'toc_truoc', part_name: 'Mái Tóc Trước', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 50, filePrefix: '05_toc_truoc' },
    toc_sau: { part_id: 'toc_sau', part_name: 'Suối Tóc Sau Lưng', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 10, filePrefix: '01_toc_sau' },
    khuon_mat: { part_id: 'khuon_mat_no_face', part_name: 'Khuôn Mặt Trần', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 30, filePrefix: '03_khuon_mat' },
    trong_den: { part_id: 'trong_den_iris', part_name: 'Mống Mắt (Iris)', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 42, filePrefix: '04a_trong_den_iris' },
    trong_trang: { part_id: 'trong_trang', part_name: 'Tròng Trắng (Sclera)', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 41, filePrefix: '04b_trong_trang' },
    diem_sang: { part_id: 'diem_sang_mat', part_name: 'Điểm Sáng Mắt', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 43, filePrefix: '04c_diem_sang_mat' },
    mi_mat: { part_id: 'mi_mat', part_name: 'Mi Mắt & Chớp Mắt', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 44, filePrefix: '04d_mi_mat' },
    long_may: { part_id: 'long_may', part_name: 'Cặp Lông Mày', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 45, filePrefix: '04e_long_may' },
    mui: { part_id: 'mui', part_name: 'Sống Mũi', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 35, filePrefix: '04f_mui' },
    doi_tai: { part_id: 'doi_tai', part_name: 'Đôi Tai', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 26, filePrefix: '04g_doi_tai' },
    mieng: { part_id: 'mieng', part_name: 'Khẩu Hình Miệng', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 36, filePrefix: '04h_mieng' },
    ngu_quan: { part_id: 'mat', part_name: 'Đôi Mắt & Ngũ Quan', group_id: '01_head_face', group_name: 'Khuôn Mặt & Ngũ Quan', z_index: 40, filePrefix: '04_ngu_quan_mat' },
    than_co_ban: { part_id: 'than_co_ban', part_name: 'Thân Đạo Bào Hanfu', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 20, filePrefix: '02_than_co_ban' },
    canh_tay_trai: { part_id: 'canh_tay_trai', part_name: 'Cánh Tay Trái', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 21, filePrefix: '02a_canh_tay_trai' },
    cang_tay_trai: { part_id: 'cang_tay_trai', part_name: 'Cẳng Tay Trái', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 22, filePrefix: '02b_cang_tay_trai' },
    ban_tay_trai: { part_id: 'ban_tay_trai', part_name: 'Bàn Tay Trái', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 23, filePrefix: '02c_ban_tay_trai' },
    canh_tay_phai: { part_id: 'canh_tay_phai', part_name: 'Cánh Tay Phải', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 19, filePrefix: '02d_canh_tay_phai' },
    cang_tay_phai: { part_id: 'cang_tay_phai', part_name: 'Cẳng Tay Phải', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 18, filePrefix: '02e_cang_tay_phai' },
    ban_tay_phai: { part_id: 'ban_tay_phai', part_name: 'Bàn Tay Phải', group_id: '02_torso_arms', group_name: 'Khớp Xương Thân & Cánh Tay', z_index: 17, filePrefix: '02f_ban_tay_phai' },
    dui_trai: { part_id: 'dui_trai', part_name: 'Đùi Trái', group_id: '03_legs_feet', group_name: 'Khớp Xương Chân & Giày', z_index: 15, filePrefix: '03a_dui_trai' },
    cang_chan_trai: { part_id: 'cang_chan_trai', part_name: 'Cẳng Chân & Ủng Trái', group_id: '03_legs_feet', group_name: 'Khớp Xương Chân & Giày', z_index: 16, filePrefix: '03b_cang_chan_trai' },
    dui_phai: { part_id: 'dui_phai', part_name: 'Đùi Phải', group_id: '03_legs_feet', group_name: 'Khớp Xương Chân & Giày', z_index: 13, filePrefix: '03c_dui_phai' },
    cang_chan_phai: { part_id: 'cang_chan_phai', part_name: 'Cẳng Chân & Ủng Phải', group_id: '03_legs_feet', group_name: 'Khớp Xương Chân & Giày', z_index: 14, filePrefix: '03d_cang_chan_phai' },
    ao_choang: { part_id: 'ao_choang', part_name: 'Áo Choàng / Tà Áo Bay', group_id: '04_props_costumes', group_name: 'Trang Phục Bay & Vũ Khí', z_index: 8, filePrefix: '06a_ao_choang' },
    vu_khi: { part_id: 'vu_khi', part_name: 'Phi Kiếm / Vũ Khí', group_id: '04_props_costumes', group_name: 'Trang Phục Bay & Vũ Khí', z_index: 60, filePrefix: '06_vu_khi' },
  };

  for (const [key, meta] of Object.entries(partMap)) {
    if (cleanName.includes(key)) {
      let angle_deg = 0;
      let angle_id = '000_front';
      let angle_name = '0° Front (Chính diện)';

      if (cleanName.includes('045') || cleanName.includes('three_quarter') || cleanName.includes('45')) {
        angle_deg = 45;
        angle_id = '045_three_quarter';
        angle_name = '45° Three-Quarter (Nghiêng 3/4)';
      } else if (cleanName.includes('090') || cleanName.includes('side') || cleanName.includes('90')) {
        angle_deg = 90;
        angle_id = '090_side';
        angle_name = '90° Side Profile (Nhìn ngang)';
      } else if (cleanName.includes('135') || cleanName.includes('rear_three_quarter')) {
        angle_deg = 135;
        angle_id = '135_rear';
        angle_name = '135° Rear Three-Quarter (Nghiêng sau)';
      } else if (cleanName.includes('180') || cleanName.includes('back')) {
        angle_deg = 180;
        angle_id = '180_back';
        angle_name = '180° Back (Sau lưng)';
      } else if (cleanName.includes('high_angle') || cleanName.includes('top')) {
        angle_deg = 90;
        angle_id = 'high_angle_top';
        angle_name = 'High Angle (Trên cao nhìn xuống)';
      } else if (cleanName.includes('low_angle') || cleanName.includes('bottom')) {
        angle_deg = 90;
        angle_id = 'low_angle_bottom';
        angle_name = 'Low Angle (Dưới hất lên)';
      }

      return {
        part_id: meta.part_id,
        part_name: meta.part_name,
        group_id: meta.group_id,
        group_name: meta.group_name,
        z_index: meta.z_index,
        angle_id,
        angle_name,
        angle_deg,
        is_master_character: false,
        save_filename: `${meta.filePrefix}_${angle_id}.png`,
      };
    }
  }

  return null;
}
