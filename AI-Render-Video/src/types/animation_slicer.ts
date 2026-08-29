// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { StandardHorizontalAngle } from './studio2d_director';

/** Sliced Animation Frame metadata and per-frame alignment */
export interface AnimationSliceFrame {
  id: string;
  index: number;
  originalDataUrl: string;
  transparentDataUrl: string;
  cropRect: { x: number; y: number; width: number; height: number };
  offsetX: number; // Fine-tuning position X offset in pixels (-200..200)
  offsetY: number; // Fine-tuning position Y offset in pixels (-200..200)
  scale: number;   // Fine-tuning scale multiplier (0.5..2.5)
  rotation: number;// Fine-tuning rotation in degrees (-180..180)
  flipX: boolean;
  durationMs?: number; // Optional per-frame timing override
}

/** Definition for a Custom or Preset Action Pose */
export interface CustomPoseDefinition {
  id: string;           // Safe slug ID (e.g. "chem-kiem-loi-dien", "phi-than-luot-gio")
  name: string;         // Human readable display name (e.g. "Chém Kiếm Lôi Điện")
  category: 'combat' | 'movement' | 'dialogue' | 'magic' | 'custom';
  icon: string;         // Emoji or symbol (e.g. "🗡️", "⚡")
  folderPath: string;   // Sanitized relative folder path (e.g. "actions/chem-kiem-loi-dien")
  description?: string;
  createdAt: string;
  isCustom?: boolean;
}

/** Full Animated Sequence Configuration for a specific pose & angle */
export interface AnimationSequenceConfig {
  id: string;
  poseId: string;
  poseName: string;
  folderSlug: string;
  angleDeg: number;
  angleId: StandardHorizontalAngle;
  fps: number;
  loopMode: 'loop' | 'ping_pong' | 'once';
  frames: AnimationSliceFrame[];
  frameOrder: number[]; // Ordered list of frame indices (e.g. [0, 1, 2, 3, 2, 1])
  sourceImageWidth: number;
  sourceImageHeight: number;
  columnsCount: number;
  rowsCount: number;
  createdAt: string;
  updatedAt: string;
}

/** Payload when user saves an animation sequence */
export interface AnimationPoseSavePayload {
  poseId: string;
  poseName: string;
  folderSlug: string;
  angleDeg: number;
  angleId: StandardHorizontalAngle;
  fps: number;
  loopMode: 'loop' | 'ping_pong' | 'once';
  frames: AnimationSliceFrame[];
  frameOrder: number[];
  spriteSheetDataUrl?: string;
}
