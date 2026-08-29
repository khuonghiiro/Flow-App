// Types for FlowMy 2.5D Multi-Angle & Motion Comic Studio
import { Character2DAngle } from './scene2d';

/** Standard Horizontal & Top-Down Camera / Actor Angles */
export type StandardHorizontalAngle =
  | 'front'                    // 0 deg
  | 'three_quarter_left'      // 45 deg
  | 'profile_left'            // 90 deg
  | 'back_three_quarter_left' // 135 deg
  | 'back'                    // 180 deg
  | 'back_three_quarter_right'// 225 deg (auto-flip 135)
  | 'profile_right'           // 270 deg (auto-flip 90)
  | 'three_quarter_right'     // 315 deg (auto-flip 45)
  | 'top_down'                // High angle 0 deg
  | 'top_down_three_quarter_left' // High angle 45 deg
  | 'top_down_profile_left'       // High angle 90 deg
  | 'top_down_back'               // High angle 180 deg
  | 'top_down_profile_right'      // High angle 270 deg
  | 'top_down_three_quarter_right';// High angle 315 deg

export interface AngleInfo {
  id: StandardHorizontalAngle;
  deg: number;
  labelVi: string;
  compass: string;
  isTopDown?: boolean;
  mirroredFrom?: StandardHorizontalAngle;
}

export const STANDARD_8_ANGLES: AngleInfo[] = [
  { id: 'front', deg: 0, labelVi: 'Chính Diện (0°)', compass: 'S' },
  { id: 'three_quarter_left', deg: 45, labelVi: 'Chéo Trước Trái (45°)', compass: 'SE' },
  { id: 'profile_left', deg: 90, labelVi: 'Ngang Trái (90°)', compass: 'E' },
  { id: 'back_three_quarter_left', deg: 135, labelVi: 'Chéo Sau Trái (135°)', compass: 'NE' },
  { id: 'back', deg: 180, labelVi: 'Sau Lưng (180°)', compass: 'N' },
  { id: 'back_three_quarter_right', deg: 225, labelVi: 'Chéo Sau Phải (225°)', compass: 'NW', mirroredFrom: 'back_three_quarter_left' },
  { id: 'profile_right', deg: 270, labelVi: 'Ngang Phải (270°)', compass: 'W', mirroredFrom: 'profile_left' },
  { id: 'three_quarter_right', deg: 315, labelVi: 'Chéo Trước Phải (315°)', compass: 'SW', mirroredFrom: 'three_quarter_left' },
];

export const TOP_DOWN_ANGLES: AngleInfo[] = [
  { id: 'top_down', deg: 0, labelVi: 'Đỉnh Đầu Chính Diện (0°)', compass: '👑 0°', isTopDown: true },
  { id: 'top_down_three_quarter_left', deg: 45, labelVi: 'Đỉnh Đầu Chéo Trái (45°)', compass: '👑 45°', isTopDown: true },
  { id: 'top_down_profile_left', deg: 90, labelVi: 'Đỉnh Đầu Ngang Trái (90°)', compass: '👑 90°', isTopDown: true },
  { id: 'top_down_back', deg: 180, labelVi: 'Đỉnh Đầu Sau Lưng (180°)', compass: '👑 180°', isTopDown: true },
  { id: 'top_down_profile_right', deg: 270, labelVi: 'Đỉnh Đầu Ngang Phải (270°)', compass: '👑 270°', isTopDown: true, mirroredFrom: 'top_down_profile_left' },
  { id: 'top_down_three_quarter_right', deg: 315, labelVi: 'Đỉnh Đầu Chéo Phải (315°)', compass: '👑 315°', isTopDown: true, mirroredFrom: 'top_down_three_quarter_left' },
];

/** Angle sprite texture slot definitions */
export type MultiAngleSpriteMap = Partial<Record<StandardHorizontalAngle, string>>;

export interface LayerPartConfig {
  id: string;
  name: string;
  category: 'toc_sau' | 'than' | 'khuon_mat' | 'mat' | 'mieng' | 'toc_truoc' | 'trang_phuc' | 'tay' | 'vu_khi' | 'vfx' | 'custom';
  path: string;
  offset: [number, number]; // [x, y] in pixels
  scale: [number, number]; // [scaleX, scaleY]
  rotation: number; // degrees
  zIndex: number;
  opacity: number;
  visible: boolean;
  angles?: MultiAngleSpriteMap;
}

export interface Actor2DProfile {
  id: string;
  name: string;
  avatarIcon?: string;
  baseScale: number;
  sprites: MultiAngleSpriteMap;
  parts?: LayerPartConfig[];
  autoMirrorSymmetry: boolean;
}

export type ActionPoseType =
  | 'idle_breathe'
  | 'talk_dialogue'
  | 'combat_slash'
  | 'combat_cast'
  | 'shocked_back'
  | 'fly_dash'
  | 'walk_cycle';

export interface ActorStateInKeyframe {
  actorId: string;
  worldFacingAngle: number; // 0..360 (Which direction character faces in world)
  positionStart: [number, number]; // [x, y] in canvas space (-500..500)
  positionEnd: [number, number];
  scale: number;
  zIndex: number;
  actionPose: ActionPoseType;
  flipX?: boolean;
  opacity?: number;
}

export interface CameraTrajectoryConfig {
  angleStart: number; // 0..360 (Yaw)
  angleEnd: number;
  pitchStart?: number; // 0 = Eye level, 60/90 = Top-down (Pitch)
  pitchEnd?: number;
  zoomStart: number; // 1.0 = normal, 1.8 = closeup
  zoomEnd: number;
  panStart: [number, number]; // [dx, dy]
  panEnd: [number, number];
  shakeIntensity?: number; // 0..1 (default 0.0)
}

export interface MultiAngleDirectorShot {
  id: string;
  title: string;
  durationSeconds: number;
  camera: CameraTrajectoryConfig;
  actors: Record<string, ActorStateInKeyframe>;
  speakerActorId?: string;
  dialogueText?: string;
  sfxSoundUrl?: string;
  vfxOverlayUrl?: string;
  transitionIn?: 'none' | 'jump_cut' | 'whip_pan' | 'fade_black' | 'flash_white';
}

export interface BackgroundLayer2D {
  id: string;
  name: string;
  path: string;
  parallaxFactor: number;
  angles?: MultiAngleSpriteMap;
  opacity: number;
  offset: [number, number];
}

export interface Director2DProject {
  version: string;
  projectId: string;
  title: string;
  description: string;
  createdAt: string;
  updatedAt: string;
  stageWidth: number;
  stageHeight: number;
  backgroundLayers: BackgroundLayer2D[];
  actors: Actor2DProfile[];
  shots: MultiAngleDirectorShot[];
}
