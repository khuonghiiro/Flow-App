// Types for AI 3D Animation Studio - Master Scene Schema

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
  action: 'idle' | 'walk' | 'run' | 'talk_gesture' | 'sit' | 'climb';
  destination?: Vec3Tuple;
  target_object?: string;
  avoid_obstacles?: boolean;
  look_at?: string;
}

export interface ExpressionKeyframe {
  time_offset: number;
  type: 'angry' | 'pain' | 'smile' | 'smirk' | 'sad' | 'serious' | 'surprised' | 'neutral';
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
}

export interface ActorConfig {
  id: string;
  name: string;
  model: string;
  costume?: string;
  spawn_point: Vec3Tuple;
  rotation_y?: number;
  tracks: ActorTracks;
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
