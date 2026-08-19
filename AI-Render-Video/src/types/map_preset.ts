import { Vec3Tuple } from './scene';

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
  sky_time?: 'sunrise' | 'noon' | 'sunset' | 'night';
  weather?: {
    fog: number;
    wind: number;
    rain?: number;
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
