import { SkillTreeNode } from '../types';

export const LOCOMOTION_NODES: SkillTreeNode[] = [
  // ─── TIER 1: TRỤ DI CHUYỂN ───
  {
    id: 'pillar_locomotion',
    promptId: 'walk_angle0',
    title: 'Trụ Di Chuyển (Locomotion)',
    shortLabel: 'Đi Bộ & Chạy',
    subtitle: 'Chu kỳ bước chân và chạy nước rút',
    icon: '🚶',
    iconPath: '/icons/pillar_locomotion.svg',
    category: 'walk',
    color: '#34d399',
    tier: 1,
    x: -3868,
    y: 608,
    parentId: 'root_master',
    isHub: true,
    badge: '10 prompts',
  },

  // ─── 1. HUB ĐI BỘ ───
  {
    id: 'hub_walk',
    promptId: 'walk_angle0',
    title: 'Nhánh Đi Bộ (Walk Cycles)',
    shortLabel: 'Đi Bộ',
    subtitle: 'Chu kỳ bước chân 5 góc độ cơ bản',
    icon: '🚶',
    iconPath: '/icons/skill_walk.svg',
    category: 'walk',
    color: '#34d399',
    tier: 2,
    x: -4437,
    y: 184,
    parentId: 'pillar_locomotion',
    isHub: true,
    badge: 'WALK 5 GÓC',
  },
  { id: 'node_walk_0', promptId: 'walk_angle0', title: 'Đi Bộ 0°', shortLabel: 'Đi 0°', icon: '⬇️', iconPath: '/icons/skill_walk.svg', category: 'walk', color: '#34d399', tier: 3, x: -4753, y: -245, parentId: 'hub_walk', badge: '0°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle0' },
  { id: 'node_walk_45', promptId: 'walk_angle45', title: 'Đi Bộ 45°', shortLabel: 'Đi 45°', icon: '↙️', iconPath: '/icons/skill_walk.svg', category: 'walk', color: '#34d399', tier: 3, x: -4862, y: -122, parentId: 'hub_walk', badge: '45°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle45' },
  { id: 'node_walk_90', promptId: 'walk_angle90', title: 'Đi Bộ 90°', shortLabel: 'Đi 90°', icon: '⬅️', iconPath: '/icons/skill_walk.svg', category: 'walk', color: '#34d399', tier: 3, x: -4943, y: 2, parentId: 'hub_walk', badge: '90°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle90' },
  { id: 'node_walk_135', promptId: 'walk_angle135', title: 'Đi Bộ 135°', shortLabel: 'Đi 135°', icon: '↘️', iconPath: '/icons/skill_walk.svg', category: 'walk', color: '#34d399', tier: 3, x: -5076, y: 133, parentId: 'hub_walk', badge: '135°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle135' },
  { id: 'node_walk_180', promptId: 'walk_angle180', title: 'Đi Bộ 180°', shortLabel: 'Đi 180°', icon: '⬆️', iconPath: '/icons/skill_walk.svg', category: 'walk', color: '#34d399', tier: 3, x: -5208, y: 306, parentId: 'hub_walk', badge: '180°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle180' },

  // ─── 2. HUB CHẠY ───
  {
    id: 'hub_run',
    promptId: 'run_angle0',
    title: 'Nhánh Chạy (Run Cycles)',
    shortLabel: 'Chạy Nhanh',
    subtitle: 'Chu kỳ chạy nước rút 5 góc độ cơ bản',
    icon: '🏃',
    iconPath: '/icons/skill_run.svg',
    category: 'walk',
    color: '#34d399',
    tier: 2,
    x: -3514,
    y: 135,
    parentId: 'pillar_locomotion',
    isHub: true,
    badge: 'RUN 5 GÓC',
  },
  { id: 'node_run_0', promptId: 'run_angle0', title: 'Chạy 0°', shortLabel: 'Chạy 0°', icon: '⬇️', iconPath: '/icons/skill_run.svg', category: 'walk', color: '#34d399', tier: 3, x: -3304, y: -164, parentId: 'hub_run', badge: '0°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle0' },
  { id: 'node_run_45', promptId: 'run_angle45', title: 'Chạy 45°', shortLabel: 'Chạy 45°', icon: '↙️', iconPath: '/icons/skill_run.svg', category: 'walk', color: '#34d399', tier: 3, x: -3114, y: -82, parentId: 'hub_run', badge: '45°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle45' },
  { id: 'node_run_90', promptId: 'run_angle90', title: 'Chạy 90°', shortLabel: 'Chạy 90°', icon: '⬅️', iconPath: '/icons/skill_run.svg', category: 'walk', color: '#34d399', tier: 3, x: -2982, y: 15, parentId: 'hub_run', badge: '90°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle90' },
  { id: 'node_run_135', promptId: 'run_angle135', title: 'Chạy 135°', shortLabel: 'Chạy 135°', icon: '↘️', iconPath: '/icons/skill_run.svg', category: 'walk', color: '#34d399', tier: 3, x: -2997, y: 156, parentId: 'hub_run', badge: '135°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle135' },
  { id: 'node_run_180', promptId: 'run_angle180', title: 'Chạy 180°', shortLabel: 'Chạy 180°', icon: '⬆️', iconPath: '/icons/skill_run.svg', category: 'walk', color: '#34d399', tier: 3, x: -3151, y: 379, parentId: 'hub_run', badge: '180°', promptType: 'video', generationMode: 'image_to_video', aspectRatio: '9:16', refAngleImageId: 'angle180' },
];


