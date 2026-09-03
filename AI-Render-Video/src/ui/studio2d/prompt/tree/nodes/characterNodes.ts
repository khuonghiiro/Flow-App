import { SkillTreeNode } from '../types';

export const CHARACTER_NODES: SkillTreeNode[] = [
  // ─── TIER 0: ROOT MASTER NODE ───
  {
    id: 'root_master',
    promptId: 'character_base',
    title: 'Nhân Vật Gốc (Master Base)',
    shortLabel: 'Mannequin 0°',
    subtitle: 'Khởi nguồn tạo hình anime chibi mặt trơn không vẽ ngũ quan',
    icon: '🌟',
    iconPath: '/icons/skill_root.svg',
    category: 'character',
    color: '#38bdf8',
    tier: 0,
    x: -1544,
    y: 1261,
    badge: 'ROOT MASTER',
    promptType: 'image',
    isHub: true,
  },

  // ─── TIER 1: TRỤ THÂN THỂ ───
  {
    id: 'pillar_character',
    promptId: 'angle0',
    title: 'Trụ Thân Thể & Góc Nhìn',
    shortLabel: '5 Góc Cơ Thể',
    subtitle: '5 góc xoay cơ bản của mannequin',
    icon: '👤',
    iconPath: '/icons/pillar_character.svg',
    category: 'character',
    color: '#38bdf8',
    tier: 1,
    x: -2181,
    y: 632,
    parentId: 'root_master',
    isHub: true,
    badge: '5 GÓC',
  },

  // ─── BRANCH 1: 5 GÓC CƠ THỂ ───
  { id: 'node_char_0', promptId: 'angle0', title: 'Nhân Vật - 0° Chính Diện', shortLabel: '0° Chính Diện', icon: '👤', iconPath: '/icons/skill_char_0.svg', category: 'character', color: '#38bdf8', tier: 2, x: -2558, y: 245, parentId: 'pillar_character', badge: '0° Front', promptType: 'image' },
  { id: 'node_char_45', promptId: 'angle45', title: 'Nhân Vật - 45° Xoay Trái', shortLabel: '45° Nghiêng Trái', icon: '👤', iconPath: '/icons/skill_char_45.svg', category: 'character', color: '#38bdf8', tier: 2, x: -2670, y: 413, parentId: 'pillar_character', badge: '45° Left', promptType: 'image' },
  { id: 'node_char_90', promptId: 'angle90', title: 'Nhân Vật - 90° Side Profile', shortLabel: '90° Nhìn Ngang', icon: '👤', iconPath: '/icons/skill_char_90.svg', category: 'character', color: '#38bdf8', tier: 2, x: -2213, y: 145, parentId: 'pillar_character', badge: '90° Side', promptType: 'image' },
  { id: 'node_char_135', promptId: 'angle135', title: 'Nhân Vật - 135° Lưng Phải', shortLabel: '135° Lưng Phải', icon: '👤', iconPath: '/icons/skill_char_135.svg', category: 'character', color: '#38bdf8', tier: 2, x: -1990, y: 367, parentId: 'pillar_character', badge: '135° Right', promptType: 'image' },
  { id: 'node_char_180', promptId: 'angle180', title: 'Nhân Vật - 180° Sau Lưng', shortLabel: '180° Sau Lưng', icon: '👤', iconPath: '/icons/skill_char_180.svg', category: 'character', color: '#38bdf8', tier: 2, x: -1926, y: 118, parentId: 'pillar_character', badge: '180° Back', promptType: 'image' },
];
