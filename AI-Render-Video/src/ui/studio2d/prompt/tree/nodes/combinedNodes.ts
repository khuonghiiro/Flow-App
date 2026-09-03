import { SkillTreeNode } from '../types';

const ANGLES = [
  { deg: '0', label: '0° Chính Diện', short: '0°', icon: '⬇️', iconPath: '/icons/skill_angle0.svg' },
  { deg: '45', label: '45° Nghiêng Trái', short: '45°', icon: '↙️', iconPath: '/icons/skill_angle45.svg' },
  { deg: '90', label: '90° Nhìn Ngang', short: '90°', icon: '⬅️', iconPath: '/icons/skill_angle90.svg' },
  { deg: '135', label: '135° Lưng Phải', short: '135°', icon: '↘️', iconPath: '/icons/skill_angle135.svg' },
  { deg: '180', label: '180° Sau Lưng', short: '180°', icon: '⬆️', iconPath: '/icons/skill_angle180.svg' },
];

function createCombinedActionFamily(
  actionId: string,
  promptPrefix: string,
  title: string,
  shortName: string,
  icon: string,
  iconPath: string,
  hubX: number,
  hubY: number,
  coords: Record<string, { x: number; y: number }>
): SkillTreeNode[] {
  const hubNode: SkillTreeNode = {
    id: `hub_${actionId}`,
    promptId: `${promptPrefix}_angle0`,
    title: `Kết Hợp: ${title}`,
    shortLabel: shortName,
    subtitle: `${title} 5 góc độ cơ bản`,
    icon,
    iconPath,
    category: 'actions',
    color: '#f59e0b',
    tier: 3,
    x: hubX,
    y: hubY,
    parentId: 'hub_combined',
    isHub: true,
    badge: '5 GÓC',
  };

  const angleNodes: SkillTreeNode[] = ANGLES.map((a) => {
    const pos = coords[a.deg] || { x: hubX, y: hubY };
    return {
      id: `node_${actionId}_${a.deg}`,
      promptId: `${promptPrefix}_angle${a.deg}`,
      title: `${title} ${a.deg}°`,
      shortLabel: `${shortName} ${a.short}`,
      icon,
      iconPath,
      category: 'actions',
      color: '#f59e0b',
      tier: 4,
      x: pos.x,
      y: pos.y,
      parentId: `hub_${actionId}`,
      badge: `${a.deg}°`,
      promptType: 'video',
    };
  });

  return [hubNode, ...angleNodes];
}

export const COMBINED_NODES: SkillTreeNode[] = [
  // Master Hub Kết Hợp
  {
    id: 'hub_combined',
    promptId: 'bow_angle0',
    title: 'Động Tác Kết Hợp (Combined Actions)',
    shortLabel: 'Tay Chân Kết Hợp',
    subtitle: 'Vái chào, cuốc đất, chẻ củi, đả tọa vận công đủ 5 góc',
    icon: '🙇',
    iconPath: '/icons/skill_bow_salute.svg',
    category: 'actions',
    color: '#f59e0b',
    tier: 2,
    x: 303,
    y: 2442,
    parentId: 'pillar_actions',
    isHub: true,
    badge: '4 ĐỘNG TÁC',
  },
  // 4 Action families with exact user coordinates
  ...createCombinedActionFamily('c_bow', 'bow', 'Vái Chào', 'Vái Chào', '🙇', '/icons/skill_bow_salute.svg', 20, 2471, {
    '0': { x: -184, y: 2236 },
    '45': { x: -219, y: 2370 },
    '90': { x: -319, y: 2496 },
    '135': { x: -217, y: 2619 },
    '180': { x: -112, y: 2735 },
  }),
  ...createCombinedActionFamily('c_hoe', 'hoe', 'Cuốc Đất', 'Cuốc Đất', '⛏️', '/icons/skill_attack.svg', 363, 3196, {
    '0': { x: 759, y: 3183 },
    '45': { x: 810, y: 3295 },
    '90': { x: 808, y: 3437 },
    '135': { x: 765, y: 3570 },
    '180': { x: 617, y: 3808 },
  }),
  ...createCombinedActionFamily('c_chop', 'chop', 'Chẻ Củi', 'Chẻ Củi', '🪵', '/icons/skill_attack.svg', 851, 3053, {
    '0': { x: 1186, y: 3051 },
    '45': { x: 1290, y: 3170 },
    '90': { x: 1371, y: 3324 },
    '135': { x: 1326, y: 3448 },
    '180': { x: 1181, y: 3612 },
  }),
  ...createCombinedActionFamily('c_meditate', 'meditate', 'Vận Công', 'Vận Công', '✨', '/icons/skill_spell.svg', 968, 2548, {
    '0': { x: 1158, y: 2398 },
    '45': { x: 1306, y: 2443 },
    '90': { x: 1320, y: 2598 },
    '135': { x: 1311, y: 2759 },
    '180': { x: 1148, y: 2897 },
  }),
];
