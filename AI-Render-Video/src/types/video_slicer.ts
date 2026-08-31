// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Animation Slicer Type Definitions
// =========================================================================================

export interface VideoCropBBox {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface VideoSliceFrame {
  id: string;
  index: number;
  timestamp: number;
  originalDataUrl: string;
  transparentDataUrl: string;
  cropRect: VideoCropBBox;
  offsetX: number;
  offsetY: number;
  scale: number;
  rotation: number;
  flipX: boolean;
  durationMs: number;
}

export interface VideoMetadata {
  name: string;
  duration: number;
  width: number;
  height: number;
  fps: number;
  sizeBytes: number;
  dataUrl: string;
}

export interface VideoExtractOptions {
  fps: number;
  startTime: number;
  endTime: number;
  maxFrames: number;
  crop?: VideoCropBBox;
}

export interface VideoAIMattingBatchResult {
  success: boolean;
  results: string[];
  count: number;
  durationSec: number;
  modelUsed: string;
}
