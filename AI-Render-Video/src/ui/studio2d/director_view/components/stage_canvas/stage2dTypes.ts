// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import {
  Director2DProject,
  MultiAngleDirectorShot,
} from '../../../../../types/studio2d_director';

export interface Stage2DCanvasProps {
  project: Director2DProject;
  activeShot: MultiAngleDirectorShot;
  shotProgress: number; // 0..1 (progress inside current shot)
  currentTime?: number;
  isPlaying: boolean;
  selectedActorId: string | null;
  selectedPartId?: string | null;
  selectedPropId?: string | null;
  onSelectActor: (id: string) => void;
  onSelectPart?: (partId: string) => void;
  onSelectProp?: (propId: string) => void;
  onUpdateCameraAngle: (yawDeg: number, pitchDeg?: number) => void;
  onUpdateActorPosition?: (actorId: string, pos: [number, number]) => void;
  onUpdateActorScale?: (actorId: string, scale: number) => void;
  onUpdateActorRotation?: (actorId: string, rotationDeg: number) => void;
  onUpdateActorFacingAngle?: (actorId: string, angleDeg: number, flipX?: boolean) => void;
  onUpdateActorFlipX?: (actorId: string, flipX: boolean) => void;
  onUpdateActorZIndex?: (actorId: string, delta: number) => void;
  onUpdatePropPosition?: (propId: string, pos: [number, number]) => void;
  onUpdatePropScale?: (propId: string, scale: number) => void;
  onUpdatePropRotation?: (propId: string, rotationDeg: number) => void;
  onUpdatePropFlipX?: (propId: string, flipX: boolean) => void;
  onUpdatePropZIndex?: (propId: string, delta: number) => void;
  onUpdateCameraFrame?: (width: number, height: number, panX?: number, panY?: number) => void;
  isCameraSelected?: boolean;
  onSelectCamera?: () => void;
  showTrajectoryLine?: boolean;
}

export function easeInOutCubic(t: number): number {
  return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
}

export interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  size: number;
  rot: number;
  vRot: number;
  opacity: number;
  type: 'leaf' | 'sparkle' | 'mist';
}

export interface BBoxCorner {
  x: number;
  y: number;
  cursor: 'nwse-resize' | 'nesw-resize';
}

export interface ActiveBBoxInfo {
  type: 'actor' | 'prop' | 'camera';
  id: string;
  centerX: number;
  centerY: number;
  initDist: number;
  initScale: number;
  initRotation: number;
  corners: BBoxCorner[];
  rotateHandle: { x: number; y: number };
  camWidth?: number;
  camHeight?: number;
}
