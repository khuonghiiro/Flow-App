import { SkillTreeNode } from '../types';

export const WEAPON_NODES: SkillTreeNode[] = [
  // ─── TIER 1: TRỤ VŨ KHÍ & PHÉP THUẬT ───
  {
    id: 'pillar_weapons',
    promptId: 'weapon_sword',
    title: 'Trụ Vũ Khí & Phép Thuật',
    shortLabel: 'Trang Bị & Kỹ Năng',
    subtitle: 'Kiếm, trượng, cung và hào quang phép',
    icon: '⚔️',
    iconPath: '/icons/pillar_weapons.svg',
    category: 'weapons',
    color: '#c084fc',
    tier: 1,
    x: -865,
    y: 1680,
    parentId: 'root_master',
    isHub: true,
    badge: '4 MẪU',
  },

  // ─── 4 VŨ KHÍ ───
  { id: 'node_w_sword', promptId: 'weapon_sword', title: 'Vũ Khí: Kiếm', shortLabel: 'Kiếm Tiên', icon: '🗡️', iconPath: '/icons/skill_sword.svg', category: 'weapons', color: '#c084fc', tier: 2, x: -250, y: 1530, parentId: 'pillar_weapons', badge: 'Sword', promptType: 'attachment', generationMode: 'text_to_image', aspectRatio: '1:1' },
  { id: 'node_w_staff', promptId: 'weapon_staff', title: 'Vũ Khí: Gậy Phép', shortLabel: 'Gậy Phép', icon: '🔱', iconPath: '/icons/skill_staff.svg', category: 'weapons', color: '#c084fc', tier: 2, x: -410, y: 1438, parentId: 'pillar_weapons', badge: 'Staff', promptType: 'attachment', generationMode: 'text_to_image', aspectRatio: '1:1' },
  { id: 'node_w_bow', promptId: 'weapon_bow', title: 'Vũ Khí: Cung Tên', shortLabel: 'Cung Tên', icon: '🏹', iconPath: '/icons/skill_bow.svg', category: 'weapons', color: '#c084fc', tier: 2, x: -276, y: 1941, parentId: 'pillar_weapons', badge: 'Bow', promptType: 'attachment', generationMode: 'text_to_image', aspectRatio: '1:1' },
  { id: 'node_w_spell', promptId: 'weapon_spell', title: 'Phát Động Phép', shortLabel: 'Chưởng Phép', icon: '✨', iconPath: '/icons/skill_spell.svg', category: 'weapons', color: '#c084fc', tier: 2, x: -140, y: 1742, parentId: 'pillar_weapons', badge: 'Magic', promptType: 'attachment', generationMode: 'text_to_image', aspectRatio: '1:1' },
];
