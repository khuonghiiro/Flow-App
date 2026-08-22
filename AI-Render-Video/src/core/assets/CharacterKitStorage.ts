import {
  Character2DAssembly,
  Character2DPartType,
  Character2DPartConfig,
  CharacterResourceCategory,
  CharacterResourceKit,
} from '../../types/scene2d';

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

// Empty default kits - user will populate by slicing or creating kits
export const DEFAULT_RESOURCE_KITS: CharacterResourceKit[] = [];

const LOCAL_STORAGE_CUSTOM_KITS_KEY = 'flowmy_character_resource_kits_v1';

/**
 * Loads all available kits (User Custom Saved from LocalStorage)
 */
export const loadAllResourceKits = (): CharacterResourceKit[] => {
  try {
    const raw = localStorage.getItem(LOCAL_STORAGE_CUSTOM_KITS_KEY);
    if (!raw) return [];
    const customList: CharacterResourceKit[] = JSON.parse(raw);
    return customList;
  } catch (err) {
    console.warn('[CharacterKitStorage] Failed to load custom kits:', err);
    return [];
  }
};

/**
 * Saves a new custom kit to persistent LocalStorage
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
