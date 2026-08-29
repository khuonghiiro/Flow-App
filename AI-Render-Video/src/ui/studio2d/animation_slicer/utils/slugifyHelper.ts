// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================

/**
 * Converts Vietnamese unicode string with accents, spaces and special characters
 * into a safe, filesystem-friendly kebab-case slug for folder names.
 * Example: "Chém Kiếm Lôi Điện" -> "chem-kiem-loi-dien"
 */
export function slugifyVietnamese(text: string): string {
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

  // Replace special characters, spaces, and punctuation with hyphens
  str = str.replace(/[^a-z0-9\s_-]/g, '');
  str = str.replace(/[\s_]+/g, '-');

  // Remove leading/trailing hyphens and duplicate hyphens
  str = str.replace(/-+/g, '-');
  str = str.replace(/^-+|-+$/g, '');

  return str || 'action-pose';
}

/**
 * Returns a standardized folder path for an action pose and angle
 * Example: ("Chém Kiếm Lôi Điện", 45) -> "actions/chem-kiem-loi-dien/angle-45"
 */
export function getActionFolderPath(poseName: string, angleDeg: number): {
  poseSlug: string;
  angleSlug: string;
  fullPath: string;
} {
  const poseSlug = slugifyVietnamese(poseName);
  const angleSlug = `angle-${angleDeg}`;
  const fullPath = `assets_2d/actions/${poseSlug}/${angleSlug}`;
  return { poseSlug, angleSlug, fullPath };
}
