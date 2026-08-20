import { Vec3Tuple } from './scene';

/**
 * 3D Light Source Socket Attachment for Props (Unity/Unreal style PointLight / SpotLight)
 */
export interface PropLightConfig {
  type?: 'point' | 'spot' | 'area_glow' | 'directional';
  preset?: 'custom' | 'flashlight' | 'street_lamp' | 'tree_aura' | 'lantern' | 'candle' | 'magic_crystal' | 'stage_spotlight' | string;
  color?: string;                      // Hex color, e.g. "#ff9933" (warm flame), "#38bdf8" (magic)
  intensity?: number;                  // Luminous intensity, e.g. 2.0 to 10.0
  distance?: number;                   // Illumination range in meters, e.g. 10.0 to 50.0
  decay?: number;                      // Physical falloff exponent (default 2.0)
  cast_shadow?: boolean;               // Whether this point light casts 3D shadows
  flicker?: boolean;                   // Natural organic flame flicker effect
  offset?: Vec3Tuple;                  // Relative local offset from prop center [x, y, z]
  target_direction?: Vec3Tuple;        // Direction vector for SpotLight [dx, dy, dz]
  spot_angle?: number;                 // Angle in radians for SpotLight (default Math.PI / 3)
  spot_penumbra?: number;              // Soft edge factor for SpotLight (0.0 to 1.0)
}

/**
 * Placed Object / Prop in a customized map preset
 */
export interface PlacedProp {
  id: string;                          // e.g. "tree_sakura_01", "bench_stone_01", "pond_lake_01"
  asset_path: string;                  // e.g. "props/nature/tree_sakura.glb" or legacy name "village_tree"
  position: Vec3Tuple;                 // [x, y, z]
  rotation?: Vec3Tuple;                // [rx, ry, rz] in radians or degrees
  scale?: Vec3Tuple | number;          // Uniform scale or [sx, sy, sz]
  type?: 'nature' | 'building' | 'furniture' | 'tool' | 'animal' | 'water' | 'obstacle';
  is_obstacle?: boolean;               // Should NavMesh treat this as an obstacle?
  obstacle_radius?: number;            // Collision radius for character pathfinding
  smart_socket?: {
    socket_type: 'sit' | 'climb' | 'stand' | 'harvest' | 'look_at';
    entry_offset?: Vec3Tuple;          // Position to approach before interacting
    target_offset?: Vec3Tuple;         // Final resting position
    target_rotation_y?: number;        // Facing rotation
  };
  light?: PropLightConfig;             // Dynamic 3D Light Socket (Lantern, Torch, Candle, Crystal, etc.)
}

/**
 * Saved Map Preset Configuration
 * Can be exported, shared, and reused by AI directors
 */
export interface MapPresetConfig {
  map_id: string;                      // Unique ID, e.g. "sakura_lake_village"
  name: string;                        // Human readable name: "Làng Hoa Anh Đào Bên Hồ"
  description?: string;                // Detailed spatial description for AI prompt understanding
  base_map?: string;                   // Base terrain .glb, e.g. "maps/medieval_fantasy_book.glb"
  sky_time?: 'sunrise' | 'noon' | 'sunset' | 'night' | 'overcast';
  weather?: {
    fog: number;
    wind: number;
    rain?: number;
    wind_direction?: number;
    cloud_coverage?: number;
    cloud_type?: 'giant_cumulus' | 'cumulus' | 'multi_layered' | 'stratus' | 'cirrus' | 'storm' | 'sunset_glow' | 'cumulonimbus';
    cloud_layers?: number;
    cloud_altitude?: number;
  };
  lighting?: {
    ambient_color?: string;
    sun_color?: string;
    sun_intensity?: number;
  };
  default_spawn_points?: Record<string, Vec3Tuple>; // Named spawn points, e.g. { "hero": [-2, 0, 1], "villain": [3, 0, 2], "center": [0, 0, 0] }
  placed_props: PlacedProp[];          // List of placed static/interactive objects
  tags?: string[];                     // Categorization tags, e.g. ["xianxia", "peaceful", "lake", "village"]
  created_at?: string;
  author?: string;
}
