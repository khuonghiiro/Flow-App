// Types for AI 3D Animation Studio - Master Scene Schema

import { CombatMasterTrack } from './combat';
import {
  ObjectInteractionTrack,
  TransformConfig,
  UpgradeEvent,
  SpawnObjectEvent,
  DestroyObjectEvent,
  InventoryActionTrack,
  ActorInventory,
} from './interactions';

export interface Vector3D {
  x: number;
  y: number;
  z: number;
}

export type Vec3Tuple = [number, number, number];

export interface EnvironmentConfig {
  map: string;
  sky_time: 'sunrise' | 'noon' | 'sunset' | 'night';
  weather: {
    fog: number;
    wind: number;
    rain?: number;
  };
}

export interface EnvironmentOverride {
  enabled: boolean;
  sky_time: 'sunrise' | 'noon' | 'sunset' | 'night' | 'manual';
  sun_position?: number;
  fog_density?: number;
  wind_intensity?: number;
}

export interface SubtitlesConfig {
  enable_overlay: boolean;
  burn_in_export: boolean;
  font_size: number;
  show_speaker_name: boolean;
  position?: 'bottom' | 'top';
  text_color?: string;
  background_opacity?: number;
}

export interface VoiceConfig {
  voice_id: string;
  speed?: number;
  pitch?: number;
  emotion?: string;
}

export interface DialogueManifestItem {
  line_id: string;
  speaker_id: string;
  speaker_name: string;
  speaker_color: string;
  text: string;
  voice_config: VoiceConfig;
  audio_path: string | null;
  audio_naming_rule: string;
  status: 'pending_tts' | 'ready' | 'rendering';
  start_time: number;
  estimated_duration: number;
  actual_duration?: number;
}

export type ShotType =
  | 'cinematic_dolly'
  | 'combat_action_cam'
  | 'face_close_up'
  | 'wide_overview'
  | 'free_orbit';

export interface CameraTrack {
  start: number;
  end: number;
  shot_type: ShotType;
  from?: Vec3Tuple;
  to?: Vec3Tuple;
  look_at?: string | Vec3Tuple;
  follow_target?: string;
  distance?: number;
  height?: number;
  fov?: number;
}

export interface MovementTrackItem {
  start: number;
  end: number;
  action:
    | 'idle' | 'walk' | 'run' | 'talk_gesture' | 'sit' | 'climb'
    | 'fly_to' | 'dash_to' | 'teleport'
    | 'kneel' | 'bow' | 'meditate'
    | 'arms_crossed' | 'hands_behind_back';
  destination?: Vec3Tuple;
  target_object?: string;
  avoid_obstacles?: boolean;
  look_at?: string;
  fly_height?: number;        // Chiều cao bay (cho fly_to)
  speed_multiplier?: number;  // Tốc độ di chuyển (1.0 = bình thường)
}

export interface ExpressionKeyframe {
  time_offset: number;
  type:
    | 'angry' | 'pain' | 'smile' | 'smirk' | 'sad' | 'serious'
    | 'surprised' | 'neutral' | 'shock'
    // Xianxia / Tiên hiệp biểu cảm
    | 'cold' | 'arrogant' | 'contempt' | 'wise' | 'fierce'
    | 'meditative' | 'menacing' | 'compassionate' | 'determined';
  weight: number;
}

export interface SpeechTrackItem {
  line_ref: string;
  expressions?: ExpressionKeyframe[];
}

export interface ScreenShakeConfig {
  intensity: number;
  duration: number;
}

export interface CombatTargetConfig {
  actor_id: string;
  reaction_anim: 'fly_back_knockdown' | 'stagger_back' | 'block_defend' | 'dodge';
  knockback_distance: number;
  facial_expression: 'pain' | 'shock' | 'angry';
  impact_vfx: 'impact_hit_sparks' | 'blood_splash' | 'energy_burst';
  screen_shake: ScreenShakeConfig;
}

export interface CombatActionItem {
  start_time: number;
  impact_time: number;
  anim: 'heavy_slash_combo' | 'fast_slash' | 'magic_blast' | 'punch_kick';
  weapon_vfx?: {
    type: 'sword_slash_fire' | 'magic_trail' | 'lightning_edge';
    start: number;
    end: number;
  };
  target: CombatTargetConfig;
}

export interface VFXTrackItem {
  start: number;
  end: number;
  type: 'magic_shield_barrier' | 'aura_flame' | 'ground_dust';
  attach_to: 'root' | 'weapon_r' | 'weapon_l' | 'head';
}

export interface ActorTracks {
  movement?: MovementTrackItem[];
  speech?: SpeechTrackItem[];
  combat_actions?: CombatActionItem[];
  vfx?: VFXTrackItem[];
  // Hệ thống mới
  object_interactions?: ObjectInteractionTrack[];
  transformations?: TransformConfig[];
  combat_master?: CombatMasterTrack;
  inventory_actions?: InventoryActionTrack[];
}

/** Lắp ráp nhân vật từ các parts modular */
export interface CharacterAssembly {
  base_body: string;           // "characters/base_bodies/male_warrior.vrm"
  face?: string;               // "characters/faces/face_male_young.glb"
  hairstyle?: string;          // "characters/hairstyles/hair_long_flowing.glb"
  beard?: string;              // "characters/beards/beard_long_sage.glb"
  costume?: string;            // "characters/costumes/costume_xianxia_white.glb"
  accessories?: string[];      // ["characters/accessories/acc_crown_gold.glb"]
  skin_color?: string;         // "#ffd1b3"
  hair_color?: string;         // "#1a1a2e"
  eye_color?: string;          // "#4a90d9"
}

export interface ActorConfig {
  id: string;
  name: string;
  model: string;                      // Backward compat: "characters/hero_knight.vrm"
  assembly?: CharacterAssembly;       // Modular character assembly
  costume?: string;
  spawn_point: Vec3Tuple;
  rotation_y?: number;
  tracks: ActorTracks;
  // Hệ thống mới
  inventory?: ActorInventory;
  can_fly?: boolean;            // Nhân vật có thể bay
  power_level?: number;         // Cấp lực (dùng cho xianxia)
  faction?: string;             // Phe phái
}


export interface CropGrowthStage {
  time: number;
  stage: 'seed' | 'sprout' | 'growing' | 'mature_crop';
  scale: number;
}

export interface DynamicWorldEvent {
  target: string;
  growth_timeline?: CropGrowthStage[];
  custom_event?: string;
  // Hệ thống mới
  upgrade?: UpgradeEvent;
  spawn_object?: SpawnObjectEvent;
  destroy_object?: DestroyObjectEvent;
}

export interface MasterSceneConfig {
  scene_id: string;
  title?: string;
  fps: number;
  duration: number;
  environment: EnvironmentConfig;
  subtitles_config: SubtitlesConfig;
  dialogues_manifest: DialogueManifestItem[];
  camera_tracks: CameraTrack[];
  actors: ActorConfig[];
  dynamic_world_events?: DynamicWorldEvent[];
}

export interface AssetCatalogItem {
  id: string;
  name: string;
  category: 'character' | 'costume' | 'weapon' | 'prop' | 'animation' | 'vfx' | 'audio';
  path: string;
  thumbnail?: string;
  tags?: string[];
  metadata?: Record<string, unknown>;
}

export interface SpatialObstacle {
  id: string;
  name: string;
  type: 'tree' | 'rock' | 'wall' | 'chair' | 'farm_plot';
  position: Vec3Tuple;
  radius: number;
  height: number;
}
