import { SkillTreeNode } from '../types';

export const CHARACTER_NODES: SkillTreeNode[] = [
  // ─── TIER 0: ROOT MASTER NODE (ƯU TIÊN SỐ 1) ───
  {
    id: 'root_master',
    promptId: 'character_base',
    title: 'Nhân Vật Gốc - 0° Chính Diện (Master Front Base)',
    shortLabel: '0° Chính Diện',
    subtitle: 'Khởi nguồn tạo hình anime chibi 0° trực diện (Mốc 10/10) - BẮT BUỘC tạo đầu tiên',
    icon: '🌟',
    iconPath: '/icons/skill_root.svg',
    category: 'character',
    color: '#38bdf8',
    tier: 0,
    x: -1544,
    y: 1261,
    badge: 'ƯU TIÊN 1: 0° GỐC',
    promptType: 'image',
    generationMode: 'text_to_image',
    aspectRatio: '9:16',
    isHub: true,
  },

  // ─── TIER 1: CÁC GÓC DẪN XUẤT TỪ 0° (ƯU TIÊN SỐ 2) ───
  { id: 'node_char_0', promptId: 'angle0', title: 'Ảnh Mốc 0° Chính Diện', shortLabel: '0° Front', subtitle: 'Khóa nhận diện nhân vật (Identity Lock 10/10)', icon: '👤', iconPath: '/icons/skill_char_0.svg', category: 'character', color: '#38bdf8', tier: 1, x: -2558, y: 245, parentId: 'root_master', badge: '0° Master', promptType: 'image', generationMode: 'image_to_image', aspectRatio: '9:16', refAngleImageId: 'character_base' },
  { id: 'node_char_45', promptId: 'angle45', title: 'Nhân Vật - 45° Nghiêng Trái', shortLabel: '45° Nghiêng Trái', subtitle: 'Dẫn xuất từ ảnh 0° + Mannequin 45°', icon: '👤', iconPath: '/icons/skill_char_45.svg', category: 'character', color: '#38bdf8', tier: 1, x: -2670, y: 413, parentId: 'root_master', badge: 'Ưu Tiên 2: 45°', promptType: 'image', generationMode: 'image_to_image', aspectRatio: '9:16', refAngleImageId: 'character_base' },
  { id: 'node_char_90', promptId: 'angle90', title: 'Nhân Vật - 90° Nhìn Ngang', shortLabel: '90° Nhìn Ngang', subtitle: 'Dẫn xuất từ ảnh 0° + Mannequin 90°', icon: '👤', iconPath: '/icons/skill_char_90.svg', category: 'character', color: '#38bdf8', tier: 1, x: -2213, y: 145, parentId: 'root_master', badge: 'Ưu Tiên 2: 90°', promptType: 'image', generationMode: 'image_to_image', aspectRatio: '9:16', refAngleImageId: 'character_base' },
  { id: 'node_char_180', promptId: 'angle180', title: 'Nhân Vật - 180° Sau Lưng', shortLabel: '180° Sau Lưng', subtitle: 'Dẫn xuất từ ảnh 0° + Mannequin 180° (đai phẳng cấm nơ)', icon: '👤', iconPath: '/icons/skill_char_180.svg', category: 'character', color: '#38bdf8', tier: 1, x: -1926, y: 118, parentId: 'root_master', badge: 'Ưu Tiên 2: 180°', promptType: 'image', generationMode: 'image_to_image', aspectRatio: '9:16', refAngleImageId: 'character_base' },
  { id: 'node_char_135', promptId: 'angle135', title: 'Nhân Vật - 135° Lưng Nghiêng', shortLabel: '135° Lưng Nghiêng', subtitle: 'Dẫn xuất từ Mannequin 135° + Mặt sau 180°', icon: '👤', iconPath: '/icons/skill_char_135.svg', category: 'character', color: '#38bdf8', tier: 2, x: -1990, y: 367, parentId: 'node_char_180', badge: 'Ưu Tiên 2: 135°', promptType: 'image', generationMode: 'image_to_image', aspectRatio: '9:16', refAngleImageId: 'angle180' },

  // ─── TIER 2: TRỤ 5 GÓC HOÀN CHỈNH (GATEWAY MỞ KHÓA HÀNH ĐỘNG) ───
  {
    id: 'pillar_character',
    promptId: 'angle0',
    title: 'Bộ Mốc 5 Góc Hoàn Chỉnh (5-Angle Rig)',
    shortLabel: 'Bộ Mốc 5 Góc',
    subtitle: 'Hội tụ đủ 5 góc mốc 10/10 — Điều kiện tiên quyết mở khóa tạo mọi hành động',
    icon: '👥',
    iconPath: '/icons/pillar_character.svg',
    category: 'character',
    color: '#38bdf8',
    tier: 2,
    x: -2181,
    y: 632,
    parentId: 'root_master',
    isHub: true,
    badge: 'ĐỦ 5 GÓC OK',
  },
];



