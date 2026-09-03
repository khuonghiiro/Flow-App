import { SkillTreeNode } from '../types';

export const ACTION_NODES: SkillTreeNode[] = [
  // ─── TIER 1: TRỤ ĐỘNG TÁC & CHIẾN ĐẤU ───
  {
    id: 'pillar_actions',
    promptId: 'idle_angle0',
    title: 'Trụ Động Tác & Chiến Đấu',
    shortLabel: 'Tư Thế & Đòn Đánh',
    subtitle: 'Đứng, ngồi, nằm, nhảy, đánh, ngã, trúng đòn và thủ',
    icon: '⚡',
    iconPath: '/icons/pillar_actions.svg',
    category: 'actions',
    color: '#f59e0b',
    tier: 1,
    x: 473,
    y: 509,
    parentId: 'root_master',
    isHub: true,
    badge: '43 PROMPTS',
  },

  // ─── 1. ĐỨNG YÊN ───
  { id: 'hub_idle', promptId: 'idle_angle0', title: 'Đứng Yên (Idle)', shortLabel: 'Đứng Yên', icon: '🧍', iconPath: '/icons/skill_idle.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 918, y: 278, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_idle_0', promptId: 'idle_angle0', title: 'Đứng Yên 0°', shortLabel: 'Idle 0°', icon: '⬇️', iconPath: '/icons/skill_idle.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1197, y: -35, parentId: 'hub_idle', badge: '0°', promptType: 'video' },
  { id: 'node_idle_45', promptId: 'idle_angle45', title: 'Đứng Yên 45°', shortLabel: 'Idle 45°', icon: '↙️', iconPath: '/icons/skill_idle.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1328, y: -13, parentId: 'hub_idle', badge: '45°', promptType: 'video' },
  { id: 'node_idle_90', promptId: 'idle_angle90', title: 'Đứng Yên 90°', shortLabel: 'Idle 90°', icon: '⬅️', iconPath: '/icons/skill_idle.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1496, y: 10, parentId: 'hub_idle', badge: '90°', promptType: 'video' },
  { id: 'node_idle_135', promptId: 'idle_angle135', title: 'Đứng Yên 135°', shortLabel: 'Idle 135°', icon: '↘️', iconPath: '/icons/skill_idle.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1696, y: 46, parentId: 'hub_idle', badge: '135°', promptType: 'video' },
  { id: 'node_idle_180', promptId: 'idle_angle180', title: 'Đứng Yên 180°', shortLabel: 'Idle 180°', icon: '⬆️', iconPath: '/icons/skill_idle.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1587, y: 144, parentId: 'hub_idle', badge: '180°', promptType: 'video' },

  // ─── 2. PHÒNG THỦ / ĐỠ ĐÒN (DEFEND) ───
  { id: 'hub_defend', promptId: 'defend_angle0', title: 'Phòng Thủ / Đỡ Đòn (Guard)', shortLabel: 'Thủ & Đỡ', icon: '🛡️', iconPath: '/icons/skill_shield.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 176, y: 763, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_defend_0', promptId: 'defend_angle0', title: 'Thủ 0°', shortLabel: 'Thủ 0°', icon: '⬇️', iconPath: '/icons/skill_shield.svg', category: 'actions', color: '#f59e0b', tier: 3, x: -205, y: 945, parentId: 'hub_defend', badge: '0°', promptType: 'video' },
  { id: 'node_defend_45', promptId: 'defend_angle45', title: 'Thủ 45°', shortLabel: 'Thủ 45°', icon: '↙️', iconPath: '/icons/skill_shield.svg', category: 'actions', color: '#f59e0b', tier: 3, x: -193, y: 1056, parentId: 'hub_defend', badge: '45°', promptType: 'video' },
  { id: 'node_defend_90', promptId: 'defend_angle90', title: 'Thủ 90°', shortLabel: 'Thủ 90°', icon: '⬅️', iconPath: '/icons/skill_shield.svg', category: 'actions', color: '#f59e0b', tier: 3, x: -73, y: 1146, parentId: 'hub_defend', badge: '90°', promptType: 'video' },
  { id: 'node_defend_135', promptId: 'defend_angle135', title: 'Thủ 135°', shortLabel: 'Thủ 135°', icon: '↘️', iconPath: '/icons/skill_shield.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 58, y: 1156, parentId: 'hub_defend', badge: '135°', promptType: 'video' },
  { id: 'node_defend_180', promptId: 'defend_angle180', title: 'Thủ 180°', shortLabel: 'Thủ 180°', icon: '⬆️', iconPath: '/icons/skill_shield.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 177, y: 1092, parentId: 'hub_defend', badge: '180°', promptType: 'video' },

  // ─── 3. NGỒI ───
  { id: 'hub_sit', promptId: 'sit_angle0', title: 'Ngồi Không Ghế', shortLabel: 'Ngồi', icon: '🪑', iconPath: '/icons/skill_sit.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 1180, y: 488, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_sit_0', promptId: 'sit_angle0', title: 'Ngồi 0°', shortLabel: 'Sit 0°', icon: '⬇️', iconPath: '/icons/skill_sit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1371, y: 252, parentId: 'hub_sit', badge: '0°', promptType: 'video' },
  { id: 'node_sit_45', promptId: 'sit_angle45', title: 'Ngồi 45°', shortLabel: 'Sit 45°', icon: '↙️', iconPath: '/icons/skill_sit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1511, y: 260, parentId: 'hub_sit', badge: '45°', promptType: 'video' },
  { id: 'node_sit_90', promptId: 'sit_angle90', title: 'Ngồi 90°', shortLabel: 'Sit 90°', icon: '⬅️', iconPath: '/icons/skill_sit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1715, y: 326, parentId: 'hub_sit', badge: '90°', promptType: 'video' },
  { id: 'node_sit_135', promptId: 'sit_angle135', title: 'Ngồi 135°', shortLabel: 'Sit 135°', icon: '↘️', iconPath: '/icons/skill_sit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1812, y: 422, parentId: 'hub_sit', badge: '135°', promptType: 'video' },
  { id: 'node_sit_180', promptId: 'sit_angle180', title: 'Ngồi 180°', shortLabel: 'Sit 180°', icon: '⬆️', iconPath: '/icons/skill_sit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1670, y: 520, parentId: 'hub_sit', badge: '180°', promptType: 'video' },

  // ─── 4. NẰM ───
  { id: 'hub_lie', promptId: 'lie_angle0', title: 'Nằm Không Giường', shortLabel: 'Nằm', icon: '🛌', iconPath: '/icons/skill_lie.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 1204, y: 695, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_lie_0', promptId: 'lie_angle0', title: 'Nằm 0°', shortLabel: 'Lie 0°', icon: '⬇️', iconPath: '/icons/skill_lie.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1881, y: 616, parentId: 'hub_lie', badge: '0°', promptType: 'video' },
  { id: 'node_lie_45', promptId: 'lie_angle45', title: 'Nằm 45°', shortLabel: 'Lie 45°', icon: '↙️', iconPath: '/icons/skill_lie.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2039, y: 710, parentId: 'hub_lie', badge: '45°', promptType: 'video' },
  { id: 'node_lie_90', promptId: 'lie_angle90', title: 'Nằm 90°', shortLabel: 'Lie 90°', icon: '⬅️', iconPath: '/icons/skill_lie.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2192, y: 842, parentId: 'hub_lie', badge: '90°', promptType: 'video' },
  { id: 'node_lie_135', promptId: 'lie_angle135', title: 'Nằm 135°', shortLabel: 'Lie 135°', icon: '↘️', iconPath: '/icons/skill_lie.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2066, y: 952, parentId: 'hub_lie', badge: '135°', promptType: 'video' },
  { id: 'node_lie_180', promptId: 'lie_angle180', title: 'Nằm 180°', shortLabel: 'Lie 180°', icon: '⬆️', iconPath: '/icons/skill_lie.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1901, y: 1038, parentId: 'hub_lie', badge: '180°', promptType: 'video' },

  // ─── 5. BẬT NHẢY ───
  { id: 'hub_jump', promptId: 'jump_angle0', title: 'Nhảy (Jump)', shortLabel: 'Bật Nhảy', icon: '⬆️', iconPath: '/icons/skill_jump.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 1194, y: 903, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_jump_0', promptId: 'jump_angle0', title: 'Nhảy 0°', shortLabel: 'Jump 0°', icon: '⬇️', iconPath: '/icons/skill_jump.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2066, y: 1162, parentId: 'hub_jump', badge: '0°', promptType: 'video' },
  { id: 'node_jump_45', promptId: 'jump_angle45', title: 'Nhảy 45°', shortLabel: 'Jump 45°', icon: '↙️', iconPath: '/icons/skill_jump.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2130, y: 1259, parentId: 'hub_jump', badge: '45°', promptType: 'video' },
  { id: 'node_jump_90', promptId: 'jump_angle90', title: 'Nhảy 90°', shortLabel: 'Jump 90°', icon: '⬅️', iconPath: '/icons/skill_jump.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2203, y: 1348, parentId: 'hub_jump', badge: '90°', promptType: 'video' },
  { id: 'node_jump_135', promptId: 'jump_angle135', title: 'Nhảy 135°', shortLabel: 'Jump 135°', icon: '↘️', iconPath: '/icons/skill_jump.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2307, y: 1451, parentId: 'hub_jump', badge: '135°', promptType: 'video' },
  { id: 'node_jump_180', promptId: 'jump_angle180', title: 'Nhảy 180°', shortLabel: 'Jump 180°', icon: '⬆️', iconPath: '/icons/skill_jump.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2183, y: 1525, parentId: 'hub_jump', badge: '180°', promptType: 'video' },

  // ─── 6. ĐÁNH CÔNG ───
  { id: 'hub_attack', promptId: 'attack_angle0', title: 'Đánh Công (Attack)', shortLabel: 'Đánh Công', icon: '⚔️', iconPath: '/icons/skill_attack.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 1215, y: 1121, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_atk_0', promptId: 'attack_angle0', title: 'Đánh 0°', shortLabel: 'Atk 0°', icon: '⬇️', iconPath: '/icons/skill_attack.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1664, y: 1324, parentId: 'hub_attack', badge: '0°', promptType: 'video' },
  { id: 'node_atk_45', promptId: 'attack_angle45', title: 'Đánh 45°', shortLabel: 'Atk 45°', icon: '↙️', iconPath: '/icons/skill_attack.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1804, y: 1455, parentId: 'hub_attack', badge: '45°', promptType: 'video' },
  { id: 'node_atk_90', promptId: 'attack_angle90', title: 'Đánh 90°', shortLabel: 'Atk 90°', icon: '⬅️', iconPath: '/icons/skill_attack.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1889, y: 1617, parentId: 'hub_attack', badge: '90°', promptType: 'video' },
  { id: 'node_atk_135', promptId: 'attack_angle135', title: 'Đánh 135°', shortLabel: 'Atk 135°', icon: '↘️', iconPath: '/icons/skill_attack.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1724, y: 1646, parentId: 'hub_attack', badge: '135°', promptType: 'video' },
  { id: 'node_atk_180', promptId: 'attack_angle180', title: 'Đánh 180°', shortLabel: 'Atk 180°', icon: '⬆️', iconPath: '/icons/skill_attack.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1555, y: 1621, parentId: 'hub_attack', badge: '180°', promptType: 'video' },

  // ─── 7. NGÃ / ĐỔ SỤP (FALL / KNOCKDOWN) ───
  { id: 'hub_fall', promptId: 'fall_angle0', title: 'Ngã & Đổ Sụp (Fall / Knockdown)', shortLabel: 'Ngã & Đổ Sụp', icon: '💥', iconPath: '/icons/skill_fall.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 1212, y: 1402, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_fall_0', promptId: 'fall_angle0', title: 'Ngã 0°', shortLabel: 'Ngã 0°', icon: '⬇️', iconPath: '/icons/skill_fall.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1631, y: 1744, parentId: 'hub_fall', badge: '0°', promptType: 'video' },
  { id: 'node_fall_45', promptId: 'fall_angle45', title: 'Ngã 45°', shortLabel: 'Ngã 45°', icon: '↙️', iconPath: '/icons/skill_fall.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1652, y: 1850, parentId: 'hub_fall', badge: '45°', promptType: 'video' },
  { id: 'node_fall_90', promptId: 'fall_angle90', title: 'Ngã 90°', shortLabel: 'Ngã 90°', icon: '⬅️', iconPath: '/icons/skill_fall.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1673, y: 1958, parentId: 'hub_fall', badge: '90°', promptType: 'video' },
  { id: 'node_fall_135', promptId: 'fall_angle135', title: 'Ngã 135°', shortLabel: 'Ngã 135°', icon: '↘️', iconPath: '/icons/skill_fall.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1488, y: 1940, parentId: 'hub_fall', badge: '135°', promptType: 'video' },
  { id: 'node_fall_180', promptId: 'fall_angle180', title: 'Ngã 180°', shortLabel: 'Ngã 180°', icon: '⬆️', iconPath: '/icons/skill_fall.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1347, y: 1927, parentId: 'hub_fall', badge: '180°', promptType: 'video' },

  // ─── 8. BỊ TRÚNG ĐÒN (HIT REACTION) ───
  { id: 'hub_hit', promptId: 'hit_angle0', title: 'Bị Trúng Đòn (Hit Reaction)', shortLabel: 'Bị Trúng Đòn', icon: '⚡', iconPath: '/icons/skill_hit.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 891, y: 1435, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_hit_0', promptId: 'hit_angle0', title: 'Trúng Đòn 0°', shortLabel: 'Trúng 0°', icon: '⬇️', iconPath: '/icons/skill_hit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1174, y: 1711, parentId: 'hub_hit', badge: '0°', promptType: 'video' },
  { id: 'node_hit_45', promptId: 'hit_angle45', title: 'Trúng Đòn 45°', shortLabel: 'Trúng 45°', icon: '↙️', iconPath: '/icons/skill_hit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1185, y: 1889, parentId: 'hub_hit', badge: '45°', promptType: 'video' },
  { id: 'node_hit_90', promptId: 'hit_angle90', title: 'Trúng Đòn 90°', shortLabel: 'Trúng 90°', icon: '⬅️', iconPath: '/icons/skill_hit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1065, y: 1968, parentId: 'hub_hit', badge: '90°', promptType: 'video' },
  { id: 'node_hit_135', promptId: 'hit_angle135', title: 'Trúng Đòn 135°', shortLabel: 'Trúng 135°', icon: '↘️', iconPath: '/icons/skill_hit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 893, y: 1971, parentId: 'hub_hit', badge: '135°', promptType: 'video' },
  { id: 'node_hit_180', promptId: 'hit_angle180', title: 'Trúng Đòn 180°', shortLabel: 'Trúng 180°', icon: '⬆️', iconPath: '/icons/skill_hit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 721, y: 1891, parentId: 'hub_hit', badge: '180°', promptType: 'video' },

  // ─── 9. CỬ ĐỘNG ĐẦU ───
  { id: 'hub_head', promptId: 'head_shake', title: 'Cử Động Đầu', shortLabel: 'Cử Động Đầu', icon: '🤨', iconPath: '/icons/skill_head.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 390, y: 895, parentId: 'pillar_actions', isHub: true, badge: '3 MẪU' },
  { id: 'node_head_shake', promptId: 'head_shake', title: 'Lắc Đầu', shortLabel: 'Lắc Đầu', icon: '🤨', iconPath: '/icons/skill_head.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 520, y: 1234, parentId: 'hub_head', badge: 'No', promptType: 'video' },
  { id: 'node_head_nod', promptId: 'head_nod', title: 'Gật Đầu', shortLabel: 'Gật Đầu', icon: '✅', iconPath: '/icons/skill_head.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 621, y: 1106, parentId: 'hub_head', badge: 'Yes', promptType: 'video' },
  { id: 'node_head_look', promptId: 'look_aside', title: 'Ngó Sang', shortLabel: 'Ngó Sang', icon: '👀', iconPath: '/icons/skill_head.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 340, y: 1303, parentId: 'hub_head', badge: 'Look', promptType: 'video' },
];
