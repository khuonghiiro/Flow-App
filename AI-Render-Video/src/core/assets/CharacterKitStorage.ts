import {
  Character2DAssembly,
  Character2DPartType,
  Character2DPartConfig,
  CharacterResourceCategory,
  CharacterResourceKit,
} from '../../types/scene2d';
import { generateDemoPartSvg } from './Asset2DRegistry';

export const RESOURCE_CATEGORIES: { id: CharacterResourceCategory; label: string; icon: string; description: string }[] = [
  {
    id: 'toc',
    label: 'Bộ Tóc 3D',
    icon: '💇',
    description: 'Tóc mái trước, tóc dài sau lưng và các góc quay 3D',
  },
  {
    id: 'mat',
    label: 'Mắt & Biểu Cảm',
    icon: '👀',
    description: 'Cặp mắt, trạng thái mở, nhắm, chớp mắt và biểu cảm',
  },
  {
    id: 'mieng',
    label: 'Khẩu Hình Miệng',
    icon: '👄',
    description: 'Khẩu hình nói chuyện A/I/U/E/O, cười, nghiến răng chiến đấu',
  },
  {
    id: 'khuon_mat',
    label: 'Khuôn Mặt & Đầu',
    icon: '👤',
    description: 'Khung đầu, cằm nhọn 90°, quai hàm và sống mũi',
  },
  {
    id: 'trang_phuc',
    label: 'Trang Phục & Quần Áo',
    icon: '👕',
    description: 'Đạo bào tu tiên, áo giáp, tà áo bay và thắt lưng',
  },
  {
    id: 'vu_khi',
    label: 'Vũ Khí & Đạo Cụ',
    icon: '⚔️',
    description: 'Phi kiếm phát sáng, bao kiếm, quạt ngọc, trượng tiên',
  },
  {
    id: 'custom_slices',
    label: 'Linh Kiện Của Tôi',
    icon: '💾',
    description: 'Các bộ linh kiện bạn đã bóc tách từ ảnh và lưu lại',
  },
  {
    id: 'combo_nhan_vat',
    label: 'Combo Nhân Vật',
    icon: '✨',
    description: 'Trọn bộ nhân vật hoàn chỉnh đa góc',
  },
];

// Helper: Generates SVG data URL for preview cards
const createPreviewSvg = (type: string, color: string, label: string): string => {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200">
    <rect width="200" height="200" rx="16" fill="#0f172a" />
    <circle cx="100" cy="100" r="70" fill="${color}" opacity="0.25" />
    <path d="M 50 140 Q 100 40 150 140" stroke="${color}" stroke-width="8" stroke-linecap="round" fill="none" />
    <text x="100" y="165" font-family="sans-serif" font-size="12" font-weight="bold" fill="#f8fafc" text-anchor="middle">${label}</text>
  </svg>`;
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
};

/**
 * Built-in Preset Kits
 */
export const DEFAULT_RESOURCE_KITS: CharacterResourceKit[] = [
  // ─── BỘ TÓC ───────────────────────────────────────────────────
  {
    id: 'kit_toc_co_trang_nu',
    name: 'Tóc Cổ Trang Tiên Nữ (Dài & Mái Bay)',
    category: 'toc',
    categoryLabel: 'Bộ Tóc 3D',
    gender: 'nu',
    style: 'tu_tien',
    angleCount: 8,
    tags: ['Cổ Trang', 'Dài', 'Nữ', '8 Góc'],
    description: 'Bộ tóc dài xõa sau lưng kết hợp tóc mái rẽ ngôi thanh tú phong cách tiên hiệp.',
    previewImage: createPreviewSvg('hair', '#38bdf8', 'Tóc Cổ Trang Nữ'),
    parts: {
      toc_truoc: {
        path: generateDemoPartSvg('toc_truoc', 'nu'),
        offset: [0, -115],
        scale: [1.05, 1.05],
        rotation: 0,
        pivot: [0.5, 0.2],
        flipX: false,
        flipY: false,
        z_index: 7,
        z_depth_3d: 0.03,
        opacity: 1,
      },
      toc_sau: {
        path: generateDemoPartSvg('toc_sau', 'nu'),
        offset: [0, -70],
        scale: [1.1, 1.15],
        rotation: 0,
        pivot: [0.5, 0.2],
        flipX: false,
        flipY: false,
        z_index: 1,
        z_depth_3d: -0.045,
        opacity: 1,
      },
    },
    createdAt: '2026-08-22T00:00:00Z',
  },
  {
    id: 'kit_toc_kiem_hiep_nam',
    name: 'Tóc Búi Kiếm Hiệp Nam (Cột Cao Phất Phơ)',
    category: 'toc',
    categoryLabel: 'Bộ Tóc 3D',
    gender: 'nam',
    style: 'kiem_hiep',
    angleCount: 8,
    tags: ['Kiếm Hiệp', 'Búi Cao', 'Nam'],
    description: 'Tóc nam kiếm khách búi cao cài trâm ngọc, hai lọn tóc mai rủ xuống phóng khoáng.',
    previewImage: createPreviewSvg('hair', '#a855f7', 'Tóc Kiếm Hiệp Nam'),
    parts: {
      toc_truoc: {
        path: generateDemoPartSvg('toc_truoc', 'nam'),
        offset: [0, -120],
        scale: [1.02, 1.02],
        rotation: 0,
        pivot: [0.5, 0.2],
        flipX: false,
        flipY: false,
        z_index: 7,
        z_depth_3d: 0.03,
        opacity: 1,
      },
      toc_sau: {
        path: generateDemoPartSvg('toc_sau', 'nam'),
        offset: [0, -80],
        scale: [1.05, 1.05],
        rotation: 0,
        pivot: [0.5, 0.2],
        flipX: false,
        flipY: false,
        z_index: 1,
        z_depth_3d: -0.04,
        opacity: 1,
      },
    },
    createdAt: '2026-08-22T00:00:00Z',
  },
  {
    id: 'kit_toc_anime_ngan',
    name: 'Tóc Anime Ngắn Năng Động (Shonen)',
    category: 'toc',
    categoryLabel: 'Bộ Tóc 3D',
    gender: 'chung',
    style: 'anime',
    angleCount: 4,
    tags: ['Anime', 'Ngắn', 'Gai Nhọn'],
    description: 'Kiểu tóc anime ngắn nhiều lớp gai nhọn cá tính, phù hợp hành động.',
    previewImage: createPreviewSvg('hair', '#f59e0b', 'Tóc Anime Shonen'),
    parts: {
      toc_truoc: {
        path: generateDemoPartSvg('toc_truoc', 'nam'),
        offset: [0, -125],
        scale: [1, 1],
        rotation: 0,
        pivot: [0.5, 0.3],
        flipX: false,
        flipY: false,
        z_index: 7,
        z_depth_3d: 0.025,
        opacity: 1,
      },
    },
    createdAt: '2026-08-22T00:00:00Z',
  },

  // ─── MẮT & BIỂU CẢM ──────────────────────────────────────────
  {
    id: 'kit_mat_phuong_tu_tien',
    name: 'Mắt Phượng Sắc Sảo (Có Trạng Thái Chớp Mắt)',
    category: 'mat',
    categoryLabel: 'Mắt & Biểu Cảm',
    gender: 'chung',
    style: 'tu_tien',
    tags: ['Mắt Phượng', 'Chớp Mắt', 'Chiến Đấu'],
    description: 'Cặp mắt phượng sắc bén có sẵn chu kỳ mở mắt, chớp mắt và nhắm mắt.',
    previewImage: createPreviewSvg('eyes', '#38bdf8', 'Mắt Phượng Tu Tiên'),
    parts: {
      mat: {
        path: generateDemoPartSvg('mat', 'nam'),
        offset: [0, -100],
        scale: [1, 1],
        rotation: 0,
        pivot: [0.5, 0.5],
        flipX: false,
        flipY: false,
        z_index: 6,
        z_depth_3d: 0.015,
        opacity: 1,
        states: {
          idle: generateDemoPartSvg('mat', 'nam'),
          open: generateDemoPartSvg('mat', 'nam'),
        },
      },
    },
    createdAt: '2026-08-22T00:00:00Z',
  },
  {
    id: 'kit_mat_anime_to_tron',
    name: 'Mắt Anime Long Lanh (Nữ Tính)',
    category: 'mat',
    categoryLabel: 'Mắt & Biểu Cảm',
    gender: 'nu',
    style: 'anime',
    tags: ['Anime', 'Mắt To', 'Long Lanh'],
    description: 'Đôi mắt to tròn với ánh sao phản quang long lanh phong cách anime.',
    previewImage: createPreviewSvg('eyes', '#ec4899', 'Mắt Anime Long Lanh'),
    parts: {
      mat: {
        path: generateDemoPartSvg('mat', 'nu'),
        offset: [0, -98],
        scale: [1.05, 1.05],
        rotation: 0,
        pivot: [0.5, 0.5],
        flipX: false,
        flipY: false,
        z_index: 6,
        z_depth_3d: 0.015,
        opacity: 1,
      },
    },
    createdAt: '2026-08-22T00:00:00Z',
  },

  // ─── KHẨU HÌNH MIỆNG ─────────────────────────────────────────
  {
    id: 'kit_mieng_lip_sync',
    name: 'Bộ Khẩu Hình Lip-Sync (Nói Chuyện & Cười)',
    category: 'mieng',
    categoryLabel: 'Khẩu Hình Miệng',
    gender: 'chung',
    style: 'tu_tien',
    tags: ['Lip-Sync', 'Thoại', 'Cười'],
    description: 'Khẩu hình đầy đủ trạng thái ngậm miệng, nói chuyện và thét chiến đấu.',
    previewImage: createPreviewSvg('mouth', '#ef4444', 'Khẩu Hình Lip-Sync'),
    parts: {
      mieng: {
        path: generateDemoPartSvg('mieng', 'nam'),
        offset: [0, -78],
        scale: [1, 1],
        rotation: 0,
        pivot: [0.5, 0.5],
        flipX: false,
        flipY: false,
        z_index: 6,
        z_depth_3d: 0.012,
        opacity: 1,
        states: {
          idle: generateDemoPartSvg('mieng', 'nam'),
          talk: generateDemoPartSvg('mieng', 'nam'),
        },
      },
    },
    createdAt: '2026-08-22T00:00:00Z',
  },

  // ─── TRANG PHỤC & QUẦN ÁO ────────────────────────────────────
  {
    id: 'kit_trang_phuc_dao_bao_xanh',
    name: 'Đạo Bào Thanh Vân Tông (Xanh Lam & Kim Tuyến)',
    category: 'trang_phuc',
    categoryLabel: 'Trang Phục & Quần Áo',
    gender: 'chung',
    style: 'tu_tien',
    tags: ['Đạo Bào', 'Thanh Vân', 'Xanh Lam'],
    description: 'Áo dài đạo môn thêu chỉ vàng, có dải thắt lưng ngọc bích và tà áo bay.',
    previewImage: createPreviewSvg('robe', '#0284c7', 'Đạo Bào Thanh Vân'),
    parts: {
      trang_phuc: {
        path: generateDemoPartSvg('trang_phuc', 'nam'),
        offset: [0, 20],
        scale: [1, 1],
        rotation: 0,
        pivot: [0.5, 0.5],
        flipX: false,
        flipY: false,
        z_index: 4,
        z_depth_3d: 0.01,
        opacity: 1,
      },
    },
    createdAt: '2026-08-22T00:00:00Z',
  },

  // ─── VŨ KHÍ & ĐẠO CỤ ─────────────────────────────────────────
  {
    id: 'kit_vu_khi_phi_kiem_thanh_quang',
    name: 'Thanh Quang Phi Kiếm (Phát Sáng Lam Linh)',
    category: 'vu_khi',
    categoryLabel: 'Vũ Khí & Đạo Cụ',
    gender: 'chung',
    style: 'tu_tien',
    tags: ['Phi Kiếm', 'Kiếm Khí', 'Linh Khí'],
    description: 'Thanh kiếm tu tiên tỏa hào quang xanh lam, có thể xoay và phóng kiếm khí.',
    previewImage: createPreviewSvg('sword', '#38bdf8', 'Phi Kiếm Thanh Quang'),
    parts: {
      vu_khi: {
        path: generateDemoPartSvg('vu_khi', 'nam'),
        offset: [75, 40],
        scale: [1, 1],
        rotation: -25,
        pivot: [0.2, 0.8],
        flipX: false,
        flipY: false,
        z_index: 8,
        z_depth_3d: 0.04,
        opacity: 1,
      },
    },
    createdAt: '2026-08-22T00:00:00Z',
  },
];

const LOCAL_STORAGE_CUSTOM_KITS_KEY = 'flowmy_character_resource_kits_v1';

/**
 * Loads all available kits (Default built-in + User Custom Saved)
 */
export const loadAllResourceKits = (): CharacterResourceKit[] => {
  try {
    const raw = localStorage.getItem(LOCAL_STORAGE_CUSTOM_KITS_KEY);
    if (!raw) return DEFAULT_RESOURCE_KITS;
    const customList: CharacterResourceKit[] = JSON.parse(raw);
    return [...customList, ...DEFAULT_RESOURCE_KITS];
  } catch (err) {
    console.warn('[CharacterKitStorage] Failed to load custom kits:', err);
    return DEFAULT_RESOURCE_KITS;
  }
};

/**
 * Saves a new custom kit (e.g. freshly sliced hair kit) to persistent LocalStorage
 */
export const saveCustomResourceKit = (kit: CharacterResourceKit): void => {
  try {
    const raw = localStorage.getItem(LOCAL_STORAGE_CUSTOM_KITS_KEY);
    const existing: CharacterResourceKit[] = raw ? JSON.parse(raw) : [];
    // Remove if duplicate ID
    const filtered = existing.filter((k) => k.id !== kit.id);
    filtered.unshift(kit);
    localStorage.setItem(LOCAL_STORAGE_CUSTOM_KITS_KEY, JSON.stringify(filtered));
  } catch (err) {
    console.error('[CharacterKitStorage] Failed to save custom kit:', err);
  }
};

/**
 * Deletes a custom resource kit by ID
 */
export const deleteCustomResourceKit = (kitId: string): void => {
  try {
    const raw = localStorage.getItem(LOCAL_STORAGE_CUSTOM_KITS_KEY);
    if (!raw) return;
    const existing: CharacterResourceKit[] = JSON.parse(raw);
    const updated = existing.filter((k) => k.id !== kitId);
    localStorage.setItem(LOCAL_STORAGE_CUSTOM_KITS_KEY, JSON.stringify(updated));
  } catch (err) {
    console.error('[CharacterKitStorage] Failed to delete kit:', err);
  }
};

/**
 * Applies a Resource Kit into a Character Assembly seamlessly,
 * merging parts and maintaining existing unaffected slots.
 */
export const applyKitToAssembly = (
  currentAssembly: Character2DAssembly,
  kit: CharacterResourceKit
): Character2DAssembly => {
  const updatedParts = { ...currentAssembly.parts };

  Object.entries(kit.parts).forEach(([slotKey, partConfig]) => {
    if (partConfig) {
      const slot = slotKey as Character2DPartType;
      const existing = updatedParts[slot];
      updatedParts[slot] = {
        ...(existing || {
          offset: [0, 0],
          scale: [1, 1],
          rotation: 0,
          pivot: [0.5, 0.5],
          flipX: false,
          flipY: false,
          z_index: 5,
          z_depth_3d: 0,
          opacity: 1,
        }),
        ...partConfig,
        angles: {
          ...(existing?.angles || {}),
          ...(partConfig.angles || {}),
        },
      };
    }
  });

  return {
    ...currentAssembly,
    parts: updatedParts,
    updated_at: new Date().toISOString(),
  };
};
