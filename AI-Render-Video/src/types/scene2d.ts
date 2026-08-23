// Types for FlowMy 2D Cutout & Motion Comic Studio

export type Character2DPartType =
  | 'dau'
  | 'khuon_mat'
  | 'mat'
  | 'mieng'
  | 'mui'
  | 'toc_truoc'
  | 'toc_sau'
  | 'than_co_ban'
  | 'canh_tay_trai'
  | 'canh_tay_phai'
  | 'cang_tay_trai'
  | 'cang_tay_phai'
  | 'ban_tay_trai'
  | 'ban_tay_phai'
  | 'dui_trai'
  | 'dui_phai'
  | 'cang_chan_trai'
  | 'cang_chan_phai'
  | 'trang_phuc'
  | 'vu_khi';

export type Character2DAngle =
  | 'front'                   // 0 deg
  | 'three_quarter_left'     // 45 deg
  | 'profile_left'           // 90 deg (Nhìn thẳng tai trái / Profile)
  | 'back_three_quarter_left'// 135 deg
  | 'back'                   // 180 deg (Sau lưng)
  | 'back_three_quarter_right'// 225 deg
  | 'profile_right'          // 270 deg (Nhìn thẳng tai phải)
  | 'three_quarter_right'    // 315 deg
  | 'top_down'                // Đỉnh đầu 0°
  | 'top_down_three_quarter_left' // Đỉnh đầu 45°
  | 'top_down_profile_left'       // Đỉnh đầu 90° (Ngang tai)
  | 'top_down_back_three_quarter_left' // Đỉnh đầu 135°
  | 'top_down_back'               // Đỉnh đầu 180°
  | 'top_down_back_three_quarter_right' // Đỉnh đầu 225°
  | 'top_down_profile_right'      // Đỉnh đầu 270°
  | 'top_down_three_quarter_right'; // Đỉnh đầu 315°

export type Character2DAngleSet = Partial<Record<Character2DAngle, string>>;

export interface PartTransform {
  offset: [number, number];       // [x, y] in pixels relative to character anchor
  scale: [number, number];        // [scaleX, scaleY]
  rotation: number;               // Rotation angle in degrees
  pivot: [number, number];        // Pivot point [0..1, 0..1], default [0.5, 0.5]
  flipX: boolean;
  flipY: boolean;
  z_index: number;
  z_depth_3d?: number;            // 3D physical depth spacing in meters (e.g. -0.05 to +0.08)
  opacity: number;
}

export interface PartAngleOverride {
  offset?: [number, number];
  scale?: [number, number];
  rotation?: number;
  flipX?: boolean;
  flipY?: boolean;
  z_index?: number;
  z_depth_3d?: number;
  visible?: boolean;
}

export interface Character2DPartConfig extends PartTransform {
  path: string;
  name?: string;
  // Multi-angle images for 3D camera auto-switching
  angles?: Character2DAngleSet;
  // Per-angle transform & visibility fine-tuning overrides
  angle_overrides?: Partial<Record<Character2DAngle, PartAngleOverride>>;
  // Multi-state sprite paths for dynamic switching (e.g. eyes blink, mouth talk)
  states?: {
    idle?: string;
    open?: string;
    blink?: string;
    angry?: string;
    happy?: string;
    talk?: string;
    shout?: string;
    hold?: string;
    slash?: string;
  };
}

export interface Character2DAssembly {
  id: string;
  name: string;
  gender: 'nam' | 'nu' | 'chung';
  style: 'anime' | 'tu_tien' | 'tu_tien_manhua' | 'anime_action' | 'kiem_hiep' | 'chibi' | 'manhua' | 'realistic' | 'cyberpunk_anime';
  preview_image?: string;
  base_scale: number;
  layer_depth_spacing?: number;   // Multiplier for 3D Z-depth between layers (default: 1.0)
  billboard_mode?: 'y_axis' | 'full' | 'static_plane'; // How 2.5D layers face camera in 3D
  parts: Partial<Record<Character2DPartType, Character2DPartConfig>>;
  created_at?: string;
  updated_at?: string;
}

export type CharacterResourceCategory =
  | 'toc'
  | 'mat'
  | 'mieng'
  | 'khuon_mat'
  | 'trang_phuc'
  | 'vu_khi'
  | 'custom_slices'
  | 'combo_nhan_vat';

export interface CharacterResourceKit {
  id: string;
  name: string;
  category: CharacterResourceCategory;
  categoryLabel: string;
  previewImage?: string;
  angleCount?: number;
  tags?: string[];
  gender?: 'nam' | 'nu' | 'chung';
  style?: string;
  description?: string;
  parts: Partial<Record<Character2DPartType, Character2DPartConfig>>;
  createdAt: string;
}

export type Map2DLayerType = 'sky' | 'background' | 'midground' | 'floor' | 'foreground' | 'prop' | 'vfx';

export interface Map2DLayerConfig {
  id: string;
  name: string;
  type: Map2DLayerType;
  path: string;
  parallax_factor: number;        // e.g. sky: 0.1, bg: 0.3, midground: 1.0, foreground: 1.8
  offset: [number, number];       // [x, y]
  scale: [number, number];
  opacity: number;
  z_index: number;
  scroll_speed_x?: number;        // Auto-scrolling clouds / wind (px/s)
  scroll_speed_y?: number;
  blend_mode?: 'normal' | 'screen' | 'multiply' | 'overlay';
}

export interface Map2DPreset {
  id: string;
  name: string;
  description?: string;
  preview_image?: string;
  layers: Map2DLayerConfig[];
  atmosphere: {
    weather: 'none' | 'falling_leaves' | 'rain' | 'snow' | 'petals' | 'dust_embers' | 'thunderstorm';
    fog_opacity: number;
    lighting_tint: string;        // Hex color tint, e.g. "#ffe8d6" or "#2a3b5c"
    ambient_audio?: string;
  };
}

export type Camera2DShotType =
  | 'closeup'
  | 'medium_shot'
  | 'wide_shot'
  | 'jump_cut_closeup'
  | 'dramatic_low_angle'
  | 'dramatic_high_angle'
  | 'split_screen_dialogue';

export interface Actor2DStateInShot {
  actor_id: string;
  position: [number, number];     // [x, y] on 2D stage
  flipX?: boolean;
  scale?: number;
  animation: 'idle' | 'breathe' | 'talk' | 'walk' | 'run' | 'combat_slash' | 'combat_cast' | 'shocked' | 'eat' | 'hurt';
  expression: 'neutral' | 'angry' | 'smile' | 'shocked' | 'crying' | 'smirk';
  mouth_talk_cycle?: boolean;
  weapon_visible?: boolean;
}

export interface Scene2DShot {
  id: string;
  time_start: number;
  time_end: number;
  shot_type: Camera2DShotType;
  camera_focus_actor?: string;
  camera_zoom: number;            // 1.0 = normal, 1.5 = closeup zoom
  camera_offset: [number, number];
  camera_shake?: {
    intensity: number;            // 0.0 to 1.0
    duration: number;             // seconds
  };
  transition_in?: 'none' | 'jump_cut' | 'whip_pan' | 'fade_black' | 'flash_white';
  vfx_overlay?: string;
  actors: Record<string, Actor2DStateInShot>;
  subtitle_text?: string;
  speaker_name?: string;
  sfx_sound?: string;
}

export interface Scene2DConfig {
  scene_id: string;
  title: string;
  map_id: string;
  duration_seconds: number;
  shots: Scene2DShot[];
}

export interface StandardCropPreset {
  id: string;
  label: string;
  category: 'nhan_vat' | 'ban_do' | 'dao_cu' | 'hieu_ung';
  slot: Character2DPartType | 'map_layer' | 'prop' | 'vfx';
  width: number;
  height: number;
  aspectRatio: number;
  suggestedPivot: [number, number];
  description: string;
}

export interface AIPartPromptConfig {
  sheet_type?:
    | 'hair_multi_angle_grid'
    | 'eyes_grid'
    | 'mouth_grid'
    | 'nose_chin_grid'
    | 'costume_grid'
    | 'weapons_grid'
    | 'limbs_hands_grid'
    | 'body_turnaround_grid'
    | 'single_part';
  part_type: Character2DPartType | 'map_layer' | 'combat_scene';
  character_style: 'tu_tien_manhua' | 'anime_action' | 'kiem_hiep' | 'cyberpunk_anime' | 'chibi' | 'hoat_hinh_3d_trung_quoc' | 'custom';
  custom_character_style?: string;
  gender: 'nam' | 'nu' | 'neutral';
  view_angle: 'front' | 'three_quarter' | 'profile_side' | 'back' | 'all_angles_16_9';
  action_or_expression: string;
  color_theme: string;
  special_features: string;
  clean_background: boolean;
  aspect_ratio?: '1:1' | '3:4' | '16:9' | '9:16';
  bg_type?: 'chroma_green' | 'pure_white' | 'chroma_gray' | 'pure_black';
  // Hair specific
  hair_length?: 'short' | 'medium_shoulder' | 'long_waist' | 'very_long_flowing' | 'top_knot_daoist' | 'custom';
  custom_hair_length?: string;
  hair_texture?: 'straight_silky' | 'wavy_curls' | 'wild_spiky' | 'braided_traditional' | 'custom';
  custom_hair_texture?: string;
  hair_color?: 'jet_black' | 'silver_white' | 'crimson_red' | 'azure_blue' | 'chestnut_brown' | 'golden_blonde' | 'mystic_purple' | 'custom';
  custom_hair_color?: string;
  hair_accessories?: 'none' | 'jade_hairpin' | 'flowing_ribbons' | 'golden_crown' | 'custom';
  custom_hair_accessories?: string;
  // Workflow Step Mode
  workflow_step?: 'step1_master_character' | 'step2_decomposed_parts' | 'step2_decomposed_hair' | 'step3_action_sequence';
  // Character Details (Step 1)
  costume_style?: 'dao_bao_tien_hiep' | 'kiem_khach_ao_vai' | 'hac_y_ma_dao' | 'hoang_toc_kim_bao' | 'bach_y_tien_tu' | 'custom';
  custom_costume_style?: string;
  costume_color?: string;
  facial_features?: string;
  prop_item?: 'flying_sword' | 'jade_hairpin' | 'feather_fan' | 'talisman_scrolls' | 'gourd_wine' | 'custom';
  custom_prop_item?: string;
  nose_shape?: 'straight_high_bridge' | 'small_delicate' | 'sharp_defined' | 'chibi_tiny_dot' | 'chibi_no_nose' | 'custom';
  custom_nose_shape?: string;
  ear_style?: 'human_natural' | 'elf_fairy_pointed';
  // Eyes specific
  eye_color?: 'azure_blue' | 'emerald_green' | 'crimson_red' | 'golden_amber' | 'mystic_purple' | 'obsidian_black' | 'chibi_sweet_pink' | 'custom';
  custom_eye_color?: string;
  eye_shape?: 'sharp_phoenix' | 'large_clear' | 'fox_alluring' | 'cold_swordsman' | 'chibi_sparkling_starry' | 'chibi_happy_crescent' | 'chibi_pouty_teary' | 'custom';
  custom_eye_shape?: string;
  // Mouth specific
  mouth_style?: 'gentle_smile' | 'speaking_cycle' | 'battle_roar' | 'confident_smirk' | 'chibi_cat_mouth' | 'chibi_surprised_o' | 'chibi_puffed_cheek' | 'chibi_big_smile' | 'custom';
  custom_mouth_style?: string;
  // Weapon specific
  weapon_type?: 'flying_sword' | 'broadsword' | 'staff' | 'feather_fan' | 'celestial_bow';
  weapon_element?: 'azure_lightning' | 'crimson_flame' | 'frost_ice' | 'golden_radiance';
}


