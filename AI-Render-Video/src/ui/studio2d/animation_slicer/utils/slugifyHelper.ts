// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================

/**
 * Converts Vietnamese unicode string with accents, spaces and special characters
 * into a safe, filesystem-friendly slug for folder names (default: underscore).
 * Example: "Tôn Ngộ Không" -> "ton_ngo_khong", "Chém Kiếm Lôi Điện" -> "chem_kiem_loi_dien"
 */
export function slugifyVietnamese(text: string, separator: '_' | '-' = '_'): string {
  if (!text) return '';

  let str = text.toLowerCase();

  // Normalize and replace accented characters
  str = str.replace(/à|á|ạ|ả|ã|â|ầ|ấ|ậ|ẩ|ẫ|ă|ằ|ắ|ặ|ẳ|ẵ/g, 'a');
  str = str.replace(/è|é|ẹ|ẻ|ẽ|ê|ề|ế|ệ|ể|ễ/g, 'e');
  str = str.replace(/ì|í|ị|ỉ|ĩ/g, 'i');
  str = str.replace(/ò|ó|ọ|ỏ|õ|ô|ồ|ố|ộ|ổ|ỗ|ơ|ờ|ớ|ợ|ở|ỡ/g, 'o');
  str = str.replace(/ù|ú|ụ|ủ|ũ|ư|ừ|ứ|ự|ử|ữ/g, 'u');
  str = str.replace(/ỳ|ý|ỵ|ỷ|ỹ/g, 'y');
  str = str.replace(/đ/g, 'd');

  // Remove combining diacritical marks
  str = str.normalize('NFD').replace(/[\u0300-\u036f]/g, '');

  // Replace special characters, spaces, and punctuation with separator
  str = str.replace(/[^a-z0-9\s_-]/g, '');
  str = str.replace(/[\s_-]+/g, separator);

  // Remove leading/trailing separator
  str = str.replace(new RegExp(`^\\${separator}+|\\${separator}+$`, 'g'), '');

  return str || 'custom';
}

/**
 * Returns a standardized folder path for character action pose and angle:
 * Example: ("Tôn Ngộ Không", "Chém Kiếm", 0, "chinh_dien")
 *       -> "asset_2ds/nhan_vat/ton_ngo_khong/hanh_dong/chem_kiem/chinh_dien"
 */
export function getActionFolderPath(
  characterName: string,
  poseName: string,
  angleDeg: number,
  angleSlugName?: string
): {
  characterSlug: string;
  poseSlug: string;
  angleSlug: string;
  fullPath: string;
} {
  const characterSlug = slugifyVietnamese(characterName || 'nhan_vat_chinh');
  const poseSlug = slugifyVietnamese(poseName || 'dong_tac_moi');
  const angleSlug = angleSlugName || `goc_${angleDeg}`;
  const fullPath = `asset_2ds/nhan_vat/${characterSlug}/hanh_dong/${poseSlug}/${angleSlug}`;
  return { characterSlug, poseSlug, angleSlug, fullPath };
}
