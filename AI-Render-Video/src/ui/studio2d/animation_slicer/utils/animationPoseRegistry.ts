// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { CustomPoseDefinition, AnimationSequenceConfig } from '../../../../types/animation_slicer';
import { slugifyVietnamese } from './slugifyHelper';

const STORAGE_KEY_POSES = 'flowmy_animation_poses_registry_v1';
const STORAGE_KEY_SEQUENCES = 'flowmy_saved_animation_sequences_v1';

export const DEFAULT_BUILTIN_POSES: CustomPoseDefinition[] = [
  {
    id: 'chem-kiem-loi-dien',
    name: 'Chém Kiếm Lôi Điện',
    category: 'combat',
    icon: '🗡️',
    folderPath: 'assets_2d/actions/chem-kiem-loi-dien',
    description: 'Động tác vung kiếm bộc phát kiếm khí lôi quang chém ngang',
    createdAt: new Date().toISOString(),
  },
  {
    id: 'xuat-chuong-linh-luc',
    name: 'Xuất Chưởng Linh Lực',
    category: 'magic',
    icon: '⚡',
    folderPath: 'assets_2d/actions/xuat-chuong-linh-luc',
    description: 'Tụ khí đan điền đẩy chưởng lực bão táp về phía trước',
    createdAt: new Date().toISOString(),
  },
  {
    id: 'phi-than-luot-gio',
    name: 'Phi Thân Lướt Gió',
    category: 'movement',
    icon: '💨',
    folderPath: 'assets_2d/actions/phi-than-luot-gio',
    description: 'Thân pháp lướt nhanh trên không trung hoặc ngự kiếm',
    createdAt: new Date().toISOString(),
  },
  {
    id: 'luan-dao-doi-thoai',
    name: 'Luận Đạo Đối Thoại',
    category: 'dialogue',
    icon: '🗣️',
    folderPath: 'assets_2d/actions/luan-dao-doi-thoai',
    description: 'Khẩu hình cử động đóng mở môi và biểu cảm nói chuyện',
    createdAt: new Date().toISOString(),
  },
  {
    id: 'trung-don-chan-thuong',
    name: 'Trúng Đòn Chấn Thương',
    category: 'combat',
    icon: '😱',
    folderPath: 'assets_2d/actions/trung-don-chan-thuong',
    description: 'Bị kiếm khí đối phương chấn thương văng lùi về sau',
    createdAt: new Date().toISOString(),
  },
  {
    id: 'da-toa-tu-khi',
    name: 'Đả Tọa Tụ Khí',
    category: 'magic',
    icon: '🧘',
    folderPath: 'assets_2d/actions/da-toa-tu-khi',
    description: 'Ngồi kiết già tĩnh tâm hấp thụ linh khí đất trời',
    createdAt: new Date().toISOString(),
  },
  {
    id: 'bo-hanh-co-phong',
    name: 'Bộ Hành Cổ Phong',
    category: 'movement',
    icon: '🚶',
    folderPath: 'assets_2d/actions/bo-hanh-co-phong',
    description: 'Dáng đi tao nhã thanh thoát phong thái kiếm hiệp',
    createdAt: new Date().toISOString(),
  },
];

/**
 * Loads all available action poses (built-in + user-created from localStorage)
 */
export function loadAllActionPoses(): CustomPoseDefinition[] {
  try {
    const saved = localStorage.getItem(STORAGE_KEY_POSES);
    if (saved) {
      const customList: CustomPoseDefinition[] = JSON.parse(saved);
      const combinedMap = new Map<string, CustomPoseDefinition>();
      DEFAULT_BUILTIN_POSES.forEach((p) => combinedMap.set(p.id, p));
      customList.forEach((p) => combinedMap.set(p.id, p));
      return Array.from(combinedMap.values());
    }
  } catch (e) {
    console.error('Failed to load action poses from localStorage', e);
  }
  return DEFAULT_BUILTIN_POSES;
}

/**
 * Saves a new custom action pose to the registry
 */
export function registerNewCustomPose(name: string, category: CustomPoseDefinition['category'] = 'custom', icon = '✨'): CustomPoseDefinition {
  const id = slugifyVietnamese(name);
  const newPose: CustomPoseDefinition = {
    id,
    name: name.trim(),
    category,
    icon,
    folderPath: `assets_2d/actions/${id}`,
    createdAt: new Date().toISOString(),
    isCustom: true,
  };

  const existing = loadAllActionPoses();
  const filtered = existing.filter((p) => p.id !== id);
  const updated = [...filtered, newPose];

  try {
    localStorage.setItem(STORAGE_KEY_POSES, JSON.stringify(updated.filter((p) => p.isCustom)));
  } catch (e) {
    console.error('Failed to save custom pose to localStorage', e);
  }

  return newPose;
}

/**
 * Saves an animation sequence configuration into localStorage
 */
export function saveAnimationSequence(sequence: AnimationSequenceConfig): void {
  try {
    const saved = localStorage.getItem(STORAGE_KEY_SEQUENCES);
    const list: AnimationSequenceConfig[] = saved ? JSON.parse(saved) : [];
    const filtered = list.filter((s) => s.id !== sequence.id);
    filtered.push(sequence);
    localStorage.setItem(STORAGE_KEY_SEQUENCES, JSON.stringify(filtered));
  } catch (e) {
    console.error('Failed to save animation sequence', e);
  }
}

/**
 * Generates Windows Batch script content (.bat) to create folder hierarchy
 */
export function generateBatchScriptForPoses(poses: CustomPoseDefinition[]): string {
  const angles = [0, 45, 90, 135, 180, 225, 270, 315];
  let batContent = '@echo off\n:: Tao cau truc thu muc dong tac hoat anh 2D\n\n';

  poses.forEach((pose) => {
    batContent += `:: Dong tac: ${pose.name}\n`;
    angles.forEach((deg) => {
      batContent += `if not exist "assets_2d\\actions\\${pose.id}\\angle-${deg}" mkdir "assets_2d\\actions\\${pose.id}\\angle-${deg}"\n`;
    });
    batContent += '\n';
  });

  batContent += 'echo Da tao xong toan bo thu muc dong tac!\npause\n';
  return batContent;
}

/**
 * Downloads a .bat file to the user machine
 */
export function downloadBatchScript(poses: CustomPoseDefinition[]): void {
  const content = generateBatchScriptForPoses(poses);
  const blob = new Blob([content], { type: 'application/bat' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `tao_thu_muc_dong_tac_${Date.now()}.bat`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
