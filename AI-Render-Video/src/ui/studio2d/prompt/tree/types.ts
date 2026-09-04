import type { GenerationMode, AspectRatio } from '../types';

export type SkillBranchCategory =
  | 'character'
  | 'walk'
  | 'actions'
  | 'face'
  | 'weapons';

export interface SkillTreeNode {
  id: string;
  promptId?: string; // Matching PromptItem id if clickable for prompt details
  title: string;
  shortLabel: string;
  subtitle?: string;
  icon: string;
  iconPath?: string; // SVG icon in /icons/
  category: SkillBranchCategory;
  color: string;
  tier: number; // 0: Root, 1: Major Pillar Hub, 2: Sub-hub / Category, 3: Leaf skill prompt
  x: number; // Canvas coordinate X
  y: number; // Canvas coordinate Y
  parentId?: string;
  badge?: string; // e.g. "0°", "45°", "8s Master", "Video", "Chibi"
  promptType?: 'image' | 'video' | 'attachment';
  generationMode?: GenerationMode;
  aspectRatio?: AspectRatio;
  refAngleImageId?: string; // ID ảnh góc cơ thể tham chiếu
  isHub?: boolean; // If true, acts as a branching nexus
}

export interface SkillTreeLink {
  fromId: string;
  toId: string;
  color: string;
  animated?: boolean;
}
