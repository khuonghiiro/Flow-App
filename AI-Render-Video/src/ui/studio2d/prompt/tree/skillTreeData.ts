import { SkillBranchCategory, SkillTreeNode, SkillTreeLink } from './types';
import { CHARACTER_NODES } from './nodes/characterNodes';
import { LOCOMOTION_NODES } from './nodes/locomotionNodes';
import { ACTION_NODES } from './nodes/actionNodes';
import { FACE_NODES } from './nodes/faceNodes';
import { WEAPON_NODES } from './nodes/weaponNodes';
import { ALL_TREE_LINKS } from './nodes/treeLinks';

export const SKILL_BRANCHES: Record<
  SkillBranchCategory,
  { label: string; icon: string; color: string; bgGlow: string }
> = {
  character: {
    label: 'Nhân Vật & Góc Nhìn',
    icon: '👤',
    color: '#38bdf8',
    bgGlow: 'rgba(56, 189, 248, 0.25)',
  },
  walk: {
    label: 'Di Chuyển (Walk/Run)',
    icon: '🚶',
    color: '#34d399',
    bgGlow: 'rgba(52, 211, 153, 0.25)',
  },
  actions: {
    label: 'Tư Thế & Đòn Đánh',
    icon: '⚡',
    color: '#f59e0b',
    bgGlow: 'rgba(245, 158, 11, 0.25)',
  },
  face: {
    label: 'Ngũ Quan & Biểu Cảm',
    icon: '😊',
    color: '#ec4899',
    bgGlow: 'rgba(236, 72, 153, 0.25)',
  },
  weapons: {
    label: 'Vũ Khí & Phép Thuật',
    icon: '⚔️',
    color: '#c084fc',
    bgGlow: 'rgba(192, 132, 252, 0.25)',
  },
};

export const BRANCH_CONFIGS = SKILL_BRANCHES;

/**
 * Danh sách toàn bộ Node Cây Kỹ Năng (Skill Tree Nodes).
 * Được cấu trúc dạng mô-đun hóa sạch sẽ theo từng nhánh.
 */
export const SKILL_TREE_NODES: SkillTreeNode[] = [
  ...CHARACTER_NODES,
  ...LOCOMOTION_NODES,
  ...ACTION_NODES,
  ...FACE_NODES,
  ...WEAPON_NODES,
];

/**
 * Toàn bộ đường nối liên kết (Bezier Curves) giữa các Node.
 */
export const SKILL_TREE_LINKS: SkillTreeLink[] = ALL_TREE_LINKS;
