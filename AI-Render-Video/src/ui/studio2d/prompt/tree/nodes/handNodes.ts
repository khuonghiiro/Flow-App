import { SkillTreeNode } from '../types';

const ANGLES = [
  { deg: '0', label: '0° Chính Diện', short: '0°', icon: '⬇️', iconPath: '/icons/skill_angle0.svg' },
  { deg: '45', label: '45° Nghiêng Trái', short: '45°', icon: '↙️', iconPath: '/icons/skill_angle45.svg' },
  { deg: '90', label: '90° Nhìn Ngang', short: '90°', icon: '⬅️', iconPath: '/icons/skill_angle90.svg' },
  { deg: '135', label: '135° Lưng Phải', short: '135°', icon: '↘️', iconPath: '/icons/skill_angle135.svg' },
  { deg: '180', label: '180° Sau Lưng', short: '180°', icon: '⬆️', iconPath: '/icons/skill_angle180.svg' },
];

function createHandActionFamily(
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
    title: `Tay: ${title}`,
    shortLabel: shortName,
    subtitle: `${title} 5 góc độ cơ bản`,
    icon,
    iconPath,
    category: 'actions',
    color: '#f59e0b',
    tier: 3,
    x: hubX,
    y: hubY,
    parentId: 'hub_hands',
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
      icon: a.icon,
      iconPath: a.iconPath,
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

export const HAND_NODES: SkillTreeNode[] = [
  // Master Hub Tay
  {
    id: 'hub_hands',
    promptId: 'clap_angle0',
    title: 'Cử Động Tay (Hand Gestures)',
    shortLabel: 'Động Tác Tay',
    subtitle: 'Vỗ tay, chắp tay sau lưng, vuốt cằm, nắm đấm, xòe tay, vẫy tay đủ 5 góc',
    icon: '👏',
    iconPath: '/icons/skill_hand_clap.svg',
    category: 'actions',
    color: '#f59e0b',
    tier: 2,
    x: 300,
    y: -221,
    parentId: 'pillar_actions',
    isHub: true,
    badge: '6 ĐỘNG TÁC',
  },
  // 6 Action families with exact user coordinates
  ...createHandActionFamily('h_clap', 'clap', 'Vỗ Tay', 'Vỗ Tay', '👏', '/icons/skill_hand_clap.svg', -1371, -858, {
    '0': { x: -1532, y: -1089 },
    '45': { x: -1604, y: -974 },
    '90': { x: -1694, y: -837 },
    '135': { x: -1630, y: -732 },
    '180': { x: -1598, y: -586 },
  }),
  ...createHandActionFamily('h_back', 'back', 'Chắp Sau Lưng', 'Sau Lưng', '🧘', '/icons/skill_hand_back.svg', -112, -1002, {
    '0': { x: -331, y: -1157 },
    '45': { x: -406, y: -1062 },
    '90': { x: -463, y: -959 },
    '135': { x: -378, y: -863 },
    '180': { x: -300, y: -790 },
  }),
  ...createHandActionFamily('h_chin', 'chin', 'Vuốt Cằm', 'Vuốt Cằm', '🤔', '/icons/skill_head.svg', 506, -829, {
    '0': { x: 752, y: -1123 },
    '45': { x: 843, y: -1014 },
    '90': { x: 962, y: -920 },
    '135': { x: 874, y: -814 },
    '180': { x: 783, y: -705 },
  }),
  ...createHandActionFamily('h_fist', 'fist', 'Nắm Đấm', 'Nắm Đấm', '✊', '/icons/skill_attack.svg', -951, -488, {
    '0': { x: -1107, y: -707 },
    '45': { x: -1321, y: -577 },
    '90': { x: -1404, y: -418 },
    '135': { x: -1297, y: -336 },
    '180': { x: -1282, y: -196 },
  }),
  ...createHandActionFamily('h_palm', 'palm', 'Xòe Tay', 'Xòe Tay', '🖐️', '/icons/skill_spell.svg', 843, -451, {
    '0': { x: 1309, y: -663 },
    '45': { x: 1403, y: -558 },
    '90': { x: 1491, y: -472 },
    '135': { x: 1273, y: -365 },
    '180': { x: 1404, y: -293 },
  }),
  ...createHandActionFamily('h_wave', 'wave', 'Vẫy Tay', 'Vẫy Tay', '👋', '/icons/skill_hand_clap.svg', 1004, -52, {
    '0': { x: 1626, y: -204 },
    '45': { x: 1703, y: -100 },
    '90': { x: 1813, y: 3 },
    '135': { x: 1657, y: 91 },
    '180': { x: 1749, y: 197 },
  }),
];
