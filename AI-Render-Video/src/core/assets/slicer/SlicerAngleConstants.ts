import { Character2DAngle, Character2DPartType } from '../../../types/scene2d';
import { GridCategoryDefinition, GridCellDefinition } from '../GridSliceRegistry';

export interface AngleDefinitionItem {
  id: string;              // e.g. "000_front", "045_three_quarter"
  angle: Character2DAngle;  // internal angle key
  label: string;           // Display name e.g. "0° Front (Chính diện)"
  shortLabel: string;      // e.g. "0° Front"
  deg: number;             // 0..360
  iconText: string;        // e.g. "👁️", "📐", "👈"
  mirrorAngle?: Character2DAngle;
  description: string;
}

export const STANDARD_ANGLE_DEFINITIONS: AngleDefinitionItem[] = [
  {
    id: '000_front',
    angle: 'front',
    label: '0° Front (Chính diện)',
    shortLabel: '0° Front',
    deg: 0,
    iconText: '👁️',
    description: 'Góc nhìn chính diện trực diện tầm mắt',
  },
  {
    id: '045_three_quarter',
    angle: 'three_quarter_left',
    mirrorAngle: 'three_quarter_right',
    label: '45° Three-Quarter (Nghiêng 3/4)',
    shortLabel: '45° Nghiêng',
    deg: 45,
    iconText: '📐',
    description: 'Góc quay 3/4 tạo độ sâu không gian',
  },
  {
    id: '090_side',
    angle: 'profile_left',
    mirrorAngle: 'profile_right',
    label: '90° Side Profile (Nhìn ngang)',
    shortLabel: '90° Ngang',
    deg: 90,
    iconText: '👈',
    description: 'Góc nhìn nghiêng ngang hoàn toàn',
  },
  {
    id: '135_rear',
    angle: 'back_three_quarter_left',
    mirrorAngle: 'back_three_quarter_right',
    label: '135° Rear Three-Quarter (Nghiêng sau)',
    shortLabel: '135° Sau Chéo',
    deg: 135,
    iconText: '↩️',
    description: 'Góc nhìn từ phía sau chéo 135 độ',
  },
  {
    id: '180_back',
    angle: 'back',
    label: '180° Back (Sau lưng)',
    shortLabel: '180° Sau Lưng',
    deg: 180,
    iconText: '🌅',
    description: 'Góc nhìn từ phía sau lưng hoàn toàn',
  },
  {
    id: 'high_angle_top',
    angle: 'top_down',
    label: 'High Angle / Top-Down (Trên cao nhìn xuống)',
    shortLabel: '🦅 Trên Cao',
    deg: 90,
    iconText: '🦅',
    description: 'Góc máy từ trên cao soi xuống hoặc đỉnh đầu',
  },
  {
    id: 'low_angle_bottom',
    angle: 'low_angle_front',
    label: 'Low Angle (Dưới hất lên)',
    shortLabel: '👑 Dưới Hất',
    deg: 90,
    iconText: '👑',
    description: 'Góc máy đặt thấp dưới đất hất ngược lên',
  },
];

/**
 * Maps an angle identifier (from Prompt JSON or filename) to a standard angle definition
 */
export function getAngleDefinitionById(angleIdOrName: string): AngleDefinitionItem {
  if (!angleIdOrName) return STANDARD_ANGLE_DEFINITIONS[0];
  const clean = angleIdOrName.toLowerCase();

  if (clean.includes('045') || clean.includes('three_quarter') || clean.includes('45')) {
    return STANDARD_ANGLE_DEFINITIONS[1];
  }
  if (clean.includes('090') || clean.includes('side') || clean.includes('profile') || clean.includes('90')) {
    return STANDARD_ANGLE_DEFINITIONS[2];
  }
  if (clean.includes('135') || clean.includes('rear')) {
    return STANDARD_ANGLE_DEFINITIONS[3];
  }
  if (clean.includes('180') || clean.includes('back')) {
    return STANDARD_ANGLE_DEFINITIONS[4];
  }
  if (clean.includes('high') || clean.includes('top') || clean.includes('dinh_dau')) {
    return STANDARD_ANGLE_DEFINITIONS[5];
  }
  if (clean.includes('low') || clean.includes('bottom') || clean.includes('duoi')) {
    return STANDARD_ANGLE_DEFINITIONS[6];
  }

  return STANDARD_ANGLE_DEFINITIONS[0]; // Default to Front 0°
}

/**
 * Creates a dynamic GridCategoryDefinition based on rows and columns selected by user
 */
export function createDynamicGridCategory(
  rows: number,
  cols: number,
  customLabel?: string
): GridCategoryDefinition {
  const r = Math.max(1, Math.min(12, rows));
  const c = Math.max(1, Math.min(12, cols));
  const isSingle = r === 1 && c === 1;

  const cells: GridCellDefinition[] = [];
  const defaultAngles = [
    STANDARD_ANGLE_DEFINITIONS[0], // 0°
    STANDARD_ANGLE_DEFINITIONS[1], // 45°
    STANDARD_ANGLE_DEFINITIONS[2], // 90°
    STANDARD_ANGLE_DEFINITIONS[5], // High Angle
    STANDARD_ANGLE_DEFINITIONS[6], // Low Angle
    STANDARD_ANGLE_DEFINITIONS[4], // 180°
    STANDARD_ANGLE_DEFINITIONS[3], // 135°
  ];

  let cellIndex = 0;
  for (let rowIndex = 0; rowIndex < r; rowIndex++) {
    for (let colIndex = 0; colIndex < c; colIndex++) {
      const angleDef = defaultAngles[cellIndex % defaultAngles.length];
      cells.push({
        row: rowIndex,
        col: colIndex,
        label: isSingle
          ? 'Toàn Bộ Ảnh Hoàn Chỉnh (Full Image)'
          : `Ô [${rowIndex + 1}, ${colIndex + 1}]: ${angleDef.shortLabel}`,
        partSlot: 'toc_truoc',
        angle: angleDef.angle,
        mirrorAngle: angleDef.mirrorAngle,
        description: `Ô lưới cắt dòng ${rowIndex + 1}, cột ${colIndex + 1} (${angleDef.label})`,
      });
      cellIndex++;
    }
  }

  const categoryId = `custom_grid_${r}x${c}`;
  const label =
    customLabel ||
    (isSingle
      ? '🖼️ 1 Ô Đơn (Toàn Bộ Ảnh)'
      : r === 1 && c === 2
      ? '✂️ 2 Ô Đơn Ngang Vừa Khít (1 Hàng × 2 Cột)'
      : r === 2 && c === 1
      ? '✂️ 2 Ô Đơn Dọc Vừa Khít (2 Hàng × 1 Cột)'
      : `🔲 Lưới Tùy Chọn (${r} Hàng × ${c} Cột)`);

  return {
    id: categoryId,
    label,
    icon: isSingle ? 'Maximize2' : 'Grid',
    rows: r,
    cols: c,
    defaultKeyColor: '#00ff00',
    description: `Khung lưới ma trận ${r} hàng × ${c} cột gồm ${r * c} ô cắt`,
    cells,
  };
}

/**
 * LocalStorage Cache Keys & Helper Functions
 */
export const SLICER_CACHE_KEYS = {
  CHECKER_THEME: 'flowmy_slicer_checker_theme',
  CUSTOM_GRID: 'flowmy_slicer_custom_grid_config',
  SELECTED_CAT_ID: 'flowmy_slicer_selected_cat_id',
  SINGLE_IMAGE_ANGLE: 'flowmy_slicer_single_image_angle',
  IS_SINGLE_MODE: 'flowmy_slicer_is_single_mode',
};

export type CheckerboardTheme = 'dark' | 'light';

export function loadCachedCheckerTheme(): CheckerboardTheme {
  try {
    const val = localStorage.getItem(SLICER_CACHE_KEYS.CHECKER_THEME);
    if (val === 'light' || val === 'dark') return val;
  } catch (e) {
    // Ignore storage errors
  }
  return 'dark';
}

export function saveCachedCheckerTheme(theme: CheckerboardTheme): void {
  try {
    localStorage.setItem(SLICER_CACHE_KEYS.CHECKER_THEME, theme);
  } catch (e) {
    // Ignore storage errors
  }
}

export function loadCachedSingleMode(): boolean {
  try {
    return localStorage.getItem(SLICER_CACHE_KEYS.IS_SINGLE_MODE) === 'true';
  } catch (e) {
    return false;
  }
}

export function saveCachedSingleMode(isSingle: boolean): void {
  try {
    localStorage.setItem(SLICER_CACHE_KEYS.IS_SINGLE_MODE, isSingle ? 'true' : 'false');
  } catch (e) {
    // Ignore storage errors
  }
}

export function loadCachedGridConfig(): { rows: number; cols: number } | null {
  try {
    const val = localStorage.getItem(SLICER_CACHE_KEYS.CUSTOM_GRID);
    if (val) {
      const parsed = JSON.parse(val);
      if (typeof parsed.rows === 'number' && typeof parsed.cols === 'number') {
        return { rows: Math.max(1, parsed.rows), cols: Math.max(1, parsed.cols) };
      }
    }
  } catch (e) {
    // Ignore storage errors
  }
  return null;
}

export function saveCachedGridConfig(rows: number, cols: number): void {
  try {
    localStorage.setItem(SLICER_CACHE_KEYS.CUSTOM_GRID, JSON.stringify({ rows, cols }));
  } catch (e) {
    // Ignore storage errors
  }
}
