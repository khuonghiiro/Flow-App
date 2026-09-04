import { SkillTreeNode } from '../types';

const ANGLES = [
  { deg: '0', label: '0° Chính Diện', short: '0°', icon: '⬇️', iconPath: '/icons/skill_angle0.svg' },
  { deg: '45', label: '45° Nghiêng Trái', short: '45°', icon: '↙️', iconPath: '/icons/skill_angle45.svg' },
  { deg: '90', label: '90° Nhìn Ngang', short: '90°', icon: '⬅️', iconPath: '/icons/skill_angle90.svg' },
  { deg: '135', label: '135° Lưng Phải', short: '135°', icon: '↘️', iconPath: '/icons/skill_angle135.svg' },
  { deg: '180', label: '180° Sau Lưng', short: '180°', icon: '⬆️', iconPath: '/icons/skill_angle180.svg' },
];

function createLegActionFamily(
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
    title: `Chân: ${title}`,
    shortLabel: shortName,
    subtitle: `${title} 5 góc độ cơ bản`,
    icon,
    iconPath,
    category: 'actions',
    color: '#f59e0b',
    tier: 3,
    x: hubX,
    y: hubY,
    parentId: 'hub_legs',
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
      generationMode: 'image_to_video',
      aspectRatio: '9:16',
      refAngleImageId: `angle${a.deg}`,
    };
  });

  return [hubNode, ...angleNodes];
}

export const LEG_NODES: SkillTreeNode[] = [
  // Master Hub Chân
  {
    id: 'hub_legs',
    promptId: 'kick_angle0',
    title: 'Cử Động Chân (Leg & Kick Actions)',
    shortLabel: 'Động Tác Chân',
    subtitle: 'Đá cao, đá xoay, quỳ một gối, quỳ seiza, dậm chân đủ 5 góc',
    icon: '🦵',
    iconPath: '/icons/skill_leg_kick.svg',
    category: 'actions',
    color: '#f59e0b',
    tier: 2,
    x: 3117,
    y: 1565,
    parentId: 'pillar_actions',
    isHub: true,
    badge: '5 ĐỘNG TÁC',
  },
  // 5 Action families with exact user coordinates
  ...createLegActionFamily('l_kick', 'kick', 'Đá Cao', 'Đá Cao', '🦵', '/icons/skill_leg_kick.svg', 3420, 957, {
    '0': { x: 3604, y: 692 },
    '45': { x: 3797, y: 769 },
    '90': { x: 3928, y: 881 },
    '135': { x: 3780, y: 997 },
    '180': { x: 3585, y: 1069 },
  }),
  ...createLegActionFamily('l_round', 'roundhouse', 'Đá Xoay', 'Đá Xoay', '⚡', '/icons/skill_leg_kick.svg', 3752, 1247, {
    '0': { x: 4077, y: 1113 },
    '45': { x: 4193, y: 1195 },
    '90': { x: 4332, y: 1289 },
    '135': { x: 4460, y: 1391 },
    '180': { x: 3993, y: 1349 },
  }),
  ...createLegActionFamily('l_kneel', 'kneel', 'Quỳ Gối', 'Quỳ Gối', '🧎', '/icons/skill_sit.svg', 3962, 1624, {
    '0': { x: 4255, y: 1495 },
    '45': { x: 4606, y: 1562 },
    '90': { x: 4458, y: 1664 },
    '135': { x: 4655, y: 1773 },
    '180': { x: 4188, y: 1746 },
  }),
  ...createLegActionFamily('l_seiza', 'seiza', 'Tọa Thiền', 'Tọa Thiền', '🧘', '/icons/skill_sit.svg', 3924, 1974, {
    '0': { x: 4242, y: 1887 },
    '45': { x: 4388, y: 1949 },
    '90': { x: 4543, y: 2085 },
    '135': { x: 4383, y: 2192 },
    '180': { x: 4079, y: 2106 },
  }),
  ...createLegActionFamily('l_stomp', 'stomp', 'Dậm Chân', 'Dậm Chân', '💥', '/icons/skill_fall.svg', 3489, 2244, {
    '0': { x: 3708, y: 2135 },
    '45': { x: 3857, y: 2203 },
    '90': { x: 3952, y: 2335 },
    '135': { x: 4052, y: 2430 },
    '180': { x: 3837, y: 2529 },
  }),
];


