// Types cho hệ thống Combat nâng cao
// Tách riêng khỏi scene.ts để giữ file gọn

import { Vec3Tuple } from './scene';

// ============================================================
// COMBO SYSTEM
// ============================================================

/** Loại sát thương của từng đòn trong combo */
export type DamageType = 'light' | 'medium' | 'heavy' | 'finisher' | 'special';

/** Phản ứng khi bị trúng đòn */
export type HitReaction =
  | 'flinch'           // Giật nhẹ
  | 'stagger'          // Loạng choạng
  | 'stagger_back'     // Loạng choạng lùi
  | 'fly_back'         // Văng lùi
  | 'launch_air'       // Bắn lên trời
  | 'ground_bounce'    // Đập đất nảy lên
  | 'spin_fall'        // Xoay rơi
  | 'block_impact'     // Đỡ chịu lực
  | 'dodge_evade';     // Né tránh

/** Hành động camera đặc biệt khi đánh */
export type CombatCameraAction =
  | 'zoom_in'          // Zoom vào điểm va chạm
  | 'slow_motion'      // Slow motion
  | 'angle_switch'     // Đổi góc camera
  | 'orbit_around'     // Camera xoay quanh
  | 'dramatic_zoom';   // Zoom dramatic (zoom in + ra nhanh)

/** Một đòn trong chuỗi combo */
export interface ComboHit {
  relative_start: number;       // Offset từ combo start (giây)
  anim: string;                 // "punch_1" | "kick_2" | "uppercut_finish"
  impact_delay: number;         // Delay từ start đến va chạm (giây)
  damage_type: DamageType;
  target_reaction: HitReaction;
  knockback: number;            // Khoảng cách văng lùi (mét)
  knockback_direction?: Vec3Tuple;  // Hướng knockback tùy chỉnh
  hit_vfx?: string;             // VFX tại điểm va chạm
  camera_action?: CombatCameraAction;
  screen_shake_intensity?: number;
  sound_effect?: string;
}

/** Chuỗi đòn combo liên hoàn */
export interface ComboChain {
  combo_id: string;
  attacker_id: string;
  target_id: string;
  start_time: number;           // Thời điểm bắt đầu combo trên timeline
  hits: ComboHit[];             // Mảng nhiều đòn liên tiếp
  total_duration: number;       // Tổng thời gian combo
  finisher_vfx?: string;        // VFX đặc biệt cho đòn cuối
  finisher_slow_motion?: SlowMotionConfig;
}

// ============================================================
// RANGED COMBAT (Combat từ xa)
// ============================================================

/** Loại đạn/phép từ xa */
export type ProjectileType =
  | 'energy_ball'       // Cầu năng lượng
  | 'fire_bolt'         // Tia lửa
  | 'ice_shard'         // Mảnh băng
  | 'lightning_bolt'    // Sét đánh
  | 'dark_beam'         // Tia tối
  | 'arrow'             // Mũi tên
  | 'shuriken';         // Phi tiêu

/** Cấu hình đạn/phép từ xa */
export interface ProjectileConfig {
  type: ProjectileType;
  speed: number;              // Tốc độ bay (m/s)
  size: number;               // Kích thước (scale)
  color: string;              // Màu sắc hex
  trail_vfx?: string;         // VFX vệt kéo theo
  impact_vfx: string;         // VFX khi va chạm
  homing?: boolean;           // Tự tìm mục tiêu
  arc_height?: number;        // Độ cong quỹ đạo (0 = thẳng)
}

/** Hành động combat từ xa */
export interface RangedCombatAction {
  start_time: number;
  cast_time: number;           // Thời gian thi triển (giây)
  release_time: number;        // Thời điểm phóng đạn
  impact_time: number;         // Thời điểm đạn chạm mục tiêu
  caster_id: string;
  target_id: string;
  cast_anim: string;           // "magic_charge" | "aim_bow"
  release_anim: string;        // "magic_release" | "shoot_arrow"
  projectile: ProjectileConfig;
  target_reaction: HitReaction;
  target_knockback: number;
  target_facial: string;
  screen_shake?: { intensity: number; duration: number };
  charge_vfx?: string;         // VFX khi đang charge
}

// ============================================================
// AERIAL COMBAT (Combat trên không)
// ============================================================

/** Giai đoạn combat trên không */
export type AerialPhase =
  | 'launch'           // Bay lên / đánh bắn lên
  | 'air_combo'        // Combo trên không
  | 'air_dash'         // Lao nhanh trên không
  | 'slam_down'        // Đập xuống đất
  | 'landing';         // Tiếp đất

/** Cấu hình combat trên không */
export interface AerialCombatAction {
  start_time: number;
  attacker_id: string;
  target_id: string;
  phases: AerialPhaseConfig[];
  total_duration: number;
  gravity_override?: number;    // Trọng lực tùy chỉnh (0 = lơ lửng)
}

/** Cấu hình từng giai đoạn aerial */
export interface AerialPhaseConfig {
  phase: AerialPhase;
  relative_start: number;
  duration: number;
  height: number;               // Chiều cao (mét)
  anim: string;
  target_anim?: string;         // Animation đối thủ
  hits?: ComboHit[];            // Đòn đánh trong giai đoạn này
  movement_curve?: 'linear' | 'ease_in' | 'ease_out' | 'arc';
}

// ============================================================
// SLOW MOTION
// ============================================================

/** Cấu hình slow motion */
export interface SlowMotionConfig {
  trigger_time: number;         // Thời điểm kích hoạt
  duration: number;             // Thời gian slow (giây thực)
  time_scale: number;           // Tốc độ (0.1 = 10% speed)
  camera_zoom?: number;         // Zoom camera (1.0 = no zoom)
  camera_orbit_speed?: number;  // Tốc độ xoay camera quanh
  blur_intensity?: number;      // Radial blur
  desaturation?: number;        // Giảm bão hòa màu (0-1)
  focus_actor?: string;         // Focus vào actor cụ thể
}

// ============================================================
// COMBAT MASTER TRACK (Tổng hợp)
// ============================================================

/** Track combat tổng hợp cho 1 actor */
export interface CombatMasterTrack {
  melee_combos?: ComboChain[];
  ranged_actions?: RangedCombatAction[];
  aerial_actions?: AerialCombatAction[];
  slow_motion_events?: SlowMotionConfig[];
  counter_attacks?: CounterAttackConfig[];
}

/** Cấu hình phản đòn */
export interface CounterAttackConfig {
  trigger_on_hit_from: string;   // Actor tấn công
  window_start: number;          // Thời điểm mở cửa sổ phản đòn
  window_duration: number;       // Thời gian cửa sổ (giây)
  counter_anim: string;          // Animation phản đòn
  counter_vfx?: string;
  counter_damage_type: DamageType;
  counter_reaction: HitReaction;
}
