import { SkillTreeNode } from '../types';
import { HAND_NODES } from './handNodes';
import { LEG_NODES } from './legNodes';
import { COMBINED_NODES } from './combinedNodes';

const BASE_ACTION_NODES: SkillTreeNode[] = [
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
    x: -41,
    y: 714,
    parentId: 'root_master',
    isHub: true,
    badge: '43 PROMPTS',
  },

  // ─── 1. ĐỨNG YÊN ───
  { id: 'hub_idle', promptId: 'idle_angle0', title: 'Đứng Yên (Idle)', shortLabel: 'Đứng Yên', icon: '🧍', iconPath: '/icons/skill_idle.svg', category: 'actions', color: '#f59e0b', tier: 2, x: -749, y: 263, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_idle_0', promptId: 'idle_angle0', title: 'Đứng Yên 0°', shortLabel: 'Đứng 0°', icon: '⬇️', iconPath: '/icons/skill_idle.svg', category: 'actions', color: '#f59e0b', tier: 3, x: -985, y: -31, parentId: 'hub_idle', badge: '0°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle0' },
  { id: 'node_idle_45', promptId: 'idle_angle45', title: 'Đứng Yên 45°', shortLabel: 'Đứng 45°', icon: '↙️', iconPath: '/icons/skill_idle.svg', category: 'actions', color: '#f59e0b', tier: 3, x: -1142, y: 79, parentId: 'hub_idle', badge: '45°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle45' },
  { id: 'node_idle_90', promptId: 'idle_angle90', title: 'Đứng Yên 90°', shortLabel: 'Đứng 90°', icon: '⬅️', iconPath: '/icons/skill_idle.svg', category: 'actions', color: '#f59e0b', tier: 3, x: -1254, y: 216, parentId: 'hub_idle', badge: '90°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle90' },
  { id: 'node_idle_135', promptId: 'idle_angle135', title: 'Đứng Yên 135°', shortLabel: 'Đứng 135°', icon: '↘️', iconPath: '/icons/skill_idle.svg', category: 'actions', color: '#f59e0b', tier: 3, x: -1140, y: 330, parentId: 'hub_idle', badge: '135°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle135' },
  { id: 'node_idle_180', promptId: 'idle_angle180', title: 'Đứng Yên 180°', shortLabel: 'Đứng 180°', icon: '⬆️', iconPath: '/icons/skill_idle.svg', category: 'actions', color: '#f59e0b', tier: 3, x: -1057, y: 443, parentId: 'hub_idle', badge: '180°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle180' },

  // ─── 2. PHÒNG THỦ / ĐỠ ĐÒN (DEFEND) ───
  { id: 'hub_defend', promptId: 'defend_angle0', title: 'Phòng Thủ / Đỡ Đòn (Guard)', shortLabel: 'Thủ & Đỡ', icon: '🛡️', iconPath: '/icons/skill_shield.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 262, y: 1214, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_defend_0', promptId: 'defend_angle0', title: 'Thủ 0°', shortLabel: 'Thủ 0°', icon: '⬇️', iconPath: '/icons/skill_shield.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 469, y: 953, parentId: 'hub_defend', badge: '0°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle0' },
  { id: 'node_defend_45', promptId: 'defend_angle45', title: 'Thủ 45°', shortLabel: 'Thủ 45°', icon: '↙️', iconPath: '/icons/skill_shield.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 473, y: 1141, parentId: 'hub_defend', badge: '45°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle45' },
  { id: 'node_defend_90', promptId: 'defend_angle90', title: 'Thủ 90°', shortLabel: 'Thủ 90°', icon: '⬅️', iconPath: '/icons/skill_shield.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 510, y: 1325, parentId: 'hub_defend', badge: '90°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle90' },
  { id: 'node_defend_135', promptId: 'defend_angle135', title: 'Thủ 135°', shortLabel: 'Thủ 135°', icon: '↘️', iconPath: '/icons/skill_shield.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 512, y: 1524, parentId: 'hub_defend', badge: '135°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle135' },
  { id: 'node_defend_180', promptId: 'defend_angle180', title: 'Thủ 180°', shortLabel: 'Thủ 180°', icon: '⬆️', iconPath: '/icons/skill_shield.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 406, y: 1763, parentId: 'hub_defend', badge: '180°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle180' },

  // ─── 3. NGỒI ───
  { id: 'hub_sit', promptId: 'sit_angle0', title: 'Ngồi Không Ghế', shortLabel: 'Ngồi', icon: '🪑', iconPath: '/icons/skill_sit.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 469, y: 422, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_sit_0', promptId: 'sit_angle0', title: 'Ngồi 0°', shortLabel: 'Ngồi 0°', icon: '⬇️', iconPath: '/icons/skill_sit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 626, y: 63, parentId: 'hub_sit', badge: '0°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle0' },
  { id: 'node_sit_45', promptId: 'sit_angle45', title: 'Ngồi 45°', shortLabel: 'Ngồi 45°', icon: '↙️', iconPath: '/icons/skill_sit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 800, y: 194, parentId: 'hub_sit', badge: '45°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle45' },
  { id: 'node_sit_90', promptId: 'sit_angle90', title: 'Ngồi 90°', shortLabel: 'Ngồi 90°', icon: '⬅️', iconPath: '/icons/skill_sit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1004, y: 260, parentId: 'hub_sit', badge: '90°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle90' },
  { id: 'node_sit_135', promptId: 'sit_angle135', title: 'Ngồi 135°', shortLabel: 'Ngồi 135°', icon: '↘️', iconPath: '/icons/skill_sit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1101, y: 356, parentId: 'hub_sit', badge: '135°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle135' },
  { id: 'node_sit_180', promptId: 'sit_angle180', title: 'Ngồi 180°', shortLabel: 'Ngồi 180°', icon: '⬆️', iconPath: '/icons/skill_sit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 959, y: 454, parentId: 'hub_sit', badge: '180°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle180' },

  // ─── 4. NẰM ───
  { id: 'hub_lie', promptId: 'lie_angle0', title: 'Nằm Không Giường', shortLabel: 'Nằm', icon: '🛌', iconPath: '/icons/skill_lie.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 2498, y: 389, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_lie_0', promptId: 'lie_angle0', title: 'Nằm 0°', shortLabel: 'Nằm 0°', icon: '⬇️', iconPath: '/icons/skill_lie.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 3175, y: 310, parentId: 'hub_lie', badge: '0°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle0' },
  { id: 'node_lie_45', promptId: 'lie_angle45', title: 'Nằm 45°', shortLabel: 'Nằm 45°', icon: '↙️', iconPath: '/icons/skill_lie.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 3333, y: 404, parentId: 'hub_lie', badge: '45°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle45' },
  { id: 'node_lie_90', promptId: 'lie_angle90', title: 'Nằm 90°', shortLabel: 'Nằm 90°', icon: '⬅️', iconPath: '/icons/skill_lie.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 3486, y: 536, parentId: 'hub_lie', badge: '90°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle90' },
  { id: 'node_lie_135', promptId: 'lie_angle135', title: 'Nằm 135°', shortLabel: 'Nằm 135°', icon: '↘️', iconPath: '/icons/skill_lie.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 3360, y: 646, parentId: 'hub_lie', badge: '135°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle135' },
  { id: 'node_lie_180', promptId: 'lie_angle180', title: 'Nằm 180°', shortLabel: 'Nằm 180°', icon: '⬆️', iconPath: '/icons/skill_lie.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 3195, y: 732, parentId: 'hub_lie', badge: '180°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle180' },

  // ─── 5. BẬT NHẢY ───
  { id: 'hub_jump', promptId: 'jump_angle0', title: 'Nhảy (Jump)', shortLabel: 'Bật Nhảy', icon: '⬆️', iconPath: '/icons/skill_jump.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 1989, y: 623, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_jump_0', promptId: 'jump_angle0', title: 'Nhảy 0°', shortLabel: 'Nhảy 0°', icon: '⬇️', iconPath: '/icons/skill_jump.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2341, y: 516, parentId: 'hub_jump', badge: '0°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle0' },
  { id: 'node_jump_45', promptId: 'jump_angle45', title: 'Nhảy 45°', shortLabel: 'Nhảy 45°', icon: '↙️', iconPath: '/icons/skill_jump.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2556, y: 587, parentId: 'hub_jump', badge: '45°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle45' },
  { id: 'node_jump_90', promptId: 'jump_angle90', title: 'Nhảy 90°', shortLabel: 'Nhảy 90°', icon: '⬅️', iconPath: '/icons/skill_jump.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2661, y: 745, parentId: 'hub_jump', badge: '90°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle90' },
  { id: 'node_jump_135', promptId: 'jump_angle135', title: 'Nhảy 135°', shortLabel: 'Nhảy 135°', icon: '↘️', iconPath: '/icons/skill_jump.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2402, y: 871, parentId: 'hub_jump', badge: '135°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle135' },
  { id: 'node_jump_180', promptId: 'jump_angle180', title: 'Nhảy 180°', shortLabel: 'Nhảy 180°', icon: '⬆️', iconPath: '/icons/skill_jump.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2592, y: 991, parentId: 'hub_jump', badge: '180°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle180' },

  // ─── 6. ĐÁNH CÔNG ───
  { id: 'hub_attack', promptId: 'attack_angle0', title: 'Đánh Công (Attack)', shortLabel: 'Đánh Công', icon: '⚔️', iconPath: '/icons/skill_attack.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 2449, y: 1712, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_atk_0', promptId: 'attack_angle0', title: 'Đánh 0°', shortLabel: 'Đánh 0°', icon: '⬇️', iconPath: '/icons/skill_attack.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2912, y: 1661, parentId: 'hub_attack', badge: '0°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle0' },
  { id: 'node_atk_45', promptId: 'attack_angle45', title: 'Đánh 45°', shortLabel: 'Đánh 45°', icon: '↙️', iconPath: '/icons/skill_attack.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 3002, y: 1742, parentId: 'hub_attack', badge: '45°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle45' },
  { id: 'node_atk_90', promptId: 'attack_angle90', title: 'Đánh 90°', shortLabel: 'Đánh 90°', icon: '⬅️', iconPath: '/icons/skill_attack.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 3067, y: 1871, parentId: 'hub_attack', badge: '90°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle90' },
  { id: 'node_atk_135', promptId: 'attack_angle135', title: 'Đánh 135°', shortLabel: 'Đánh 135°', icon: '↘️', iconPath: '/icons/skill_attack.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2782, y: 1987, parentId: 'hub_attack', badge: '135°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle135' },
  { id: 'node_atk_180', promptId: 'attack_angle180', title: 'Đánh 180°', shortLabel: 'Đánh 180°', icon: '⬆️', iconPath: '/icons/skill_attack.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2896, y: 2087, parentId: 'hub_attack', badge: '180°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle180' },

  // ─── 7. NGÃ / ĐỔ SỤP (FALL / KNOCKDOWN) ───
  { id: 'hub_fall', promptId: 'fall_angle0', title: 'Ngã & Đổ Sụp (Fall / Knockdown)', shortLabel: 'Ngã & Đổ Sụp', icon: '💥', iconPath: '/icons/skill_fall.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 2023, y: 1990, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_fall_0', promptId: 'fall_angle0', title: 'Ngã 0°', shortLabel: 'Ngã 0°', icon: '⬇️', iconPath: '/icons/skill_fall.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2336, y: 1858, parentId: 'hub_fall', badge: '0°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle0' },
  { id: 'node_fall_45', promptId: 'fall_angle45', title: 'Ngã 45°', shortLabel: 'Ngã 45°', icon: '↙️', iconPath: '/icons/skill_fall.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2466, y: 1978, parentId: 'hub_fall', badge: '45°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle45' },
  { id: 'node_fall_90', promptId: 'fall_angle90', title: 'Ngã 90°', shortLabel: 'Ngã 90°', icon: '⬅️', iconPath: '/icons/skill_fall.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2581, y: 2149, parentId: 'hub_fall', badge: '90°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle90' },
  { id: 'node_fall_135', promptId: 'fall_angle135', title: 'Ngã 135°', shortLabel: 'Ngã 135°', icon: '↘️', iconPath: '/icons/skill_fall.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2690, y: 2302, parentId: 'hub_fall', badge: '135°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle135' },
  { id: 'node_fall_180', promptId: 'fall_angle180', title: 'Ngã 180°', shortLabel: 'Ngã 180°', icon: '⬆️', iconPath: '/icons/skill_fall.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2498, y: 2478, parentId: 'hub_fall', badge: '180°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle180' },

  // ─── 8. BỊ TRÚNG ĐÒN (HIT REACTION) ───
  { id: 'hub_hit', promptId: 'hit_angle0', title: 'Bị Trúng Đòn (Hit Reaction)', shortLabel: 'Bị Trúng Đòn', icon: '⚡', iconPath: '/icons/skill_hit.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 1300, y: 2272, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_hit_0', promptId: 'hit_angle0', title: 'Trúng Đòn 0°', shortLabel: 'Trúng 0°', icon: '⬇️', iconPath: '/icons/skill_hit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1737, y: 2103, parentId: 'hub_hit', badge: '0°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle0' },
  { id: 'node_hit_45', promptId: 'hit_angle45', title: 'Trúng Đòn 45°', shortLabel: 'Trúng 45°', icon: '↙️', iconPath: '/icons/skill_hit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1985, y: 2306, parentId: 'hub_hit', badge: '45°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle45' },
  { id: 'node_hit_90', promptId: 'hit_angle90', title: 'Trúng Đòn 90°', shortLabel: 'Trúng 90°', icon: '⬅️', iconPath: '/icons/skill_hit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1882, y: 2462, parentId: 'hub_hit', badge: '90°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle90' },
  { id: 'node_hit_135', promptId: 'hit_angle135', title: 'Trúng Đòn 135°', shortLabel: 'Trúng 135°', icon: '↘️', iconPath: '/icons/skill_hit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1762, y: 2596, parentId: 'hub_hit', badge: '135°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle135' },
  { id: 'node_hit_180', promptId: 'hit_angle180', title: 'Trúng Đòn 180°', shortLabel: 'Trúng 180°', icon: '⬆️', iconPath: '/icons/skill_hit.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 1696, y: 2802, parentId: 'hub_hit', badge: '180°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle180' },

  // ─── 9. CỬ ĐỘNG ĐẦU ───
  { id: 'hub_head', promptId: 'head_angle0', title: 'Cử Động Đầu (Head Motion)', shortLabel: 'Cử Động Đầu', icon: '🤨', iconPath: '/icons/skill_head.svg', category: 'actions', color: '#f59e0b', tier: 2, x: 1928, y: 1021, parentId: 'pillar_actions', isHub: true, badge: '5 GÓC' },
  { id: 'node_head_0', promptId: 'head_angle0', title: 'Đầu 0°', shortLabel: 'Đầu 0°', icon: '⬇️', iconPath: '/icons/skill_angle0.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2408, y: 1125, parentId: 'hub_head', badge: '0°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle0' },
  { id: 'node_head_45', promptId: 'head_angle45', title: 'Đầu 45°', shortLabel: 'Đầu 45°', icon: '↙️', iconPath: '/icons/skill_angle45.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2557, y: 1197, parentId: 'hub_head', badge: '45°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle45' },
  { id: 'node_head_90', promptId: 'head_angle90', title: 'Đầu 90°', shortLabel: 'Đầu 90°', icon: '⬅️', iconPath: '/icons/skill_angle90.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2416, y: 1283, parentId: 'hub_head', badge: '90°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle90' },
  { id: 'node_head_135', promptId: 'head_angle135', title: 'Đầu 135°', shortLabel: 'Đầu 135°', icon: '↘️', iconPath: '/icons/skill_angle135.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2571, y: 1345, parentId: 'hub_head', badge: '135°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle135' },
  { id: 'node_head_180', promptId: 'head_angle180', title: 'Đầu 180°', shortLabel: 'Đầu 180°', icon: '⬆️', iconPath: '/icons/skill_angle180.svg', category: 'actions', color: '#f59e0b', tier: 3, x: 2357, y: 1435, parentId: 'hub_head', badge: '180°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle180' },
];

export const ACTION_NODES: SkillTreeNode[] = [
  ...BASE_ACTION_NODES,
  ...HAND_NODES,
  ...LEG_NODES,
  ...COMBINED_NODES,
];
