// Types cho hệ thống tương tác đồ vật, nâng cấp vật thể, và inventory
// Tách riêng khỏi scene.ts để giữ file gọn

import { Vec3Tuple } from './scene';

// ============================================================
// OBJECT INTERACTION (Thao tác đồ vật)
// ============================================================

/** Loại tương tác với đồ vật */
export type InteractionType =
  | 'pickup'         // Nhặt lên bằng 1 tay
  | 'carry'          // Bưng bê bằng 2 tay
  | 'drink'          // Nâng ly uống
  | 'eat'            // Ăn
  | 'place'          // Đặt xuống
  | 'throw'          // Ném đi
  | 'give'           // Đưa cho người khác
  | 'pour'           // Rót (nước, hạt giống)
  | 'dig'            // Đào đất
  | 'water'          // Tưới cây
  | 'plant_seed'     // Gieo hạt
  | 'harvest'        // Thu hoạch
  | 'read'           // Đọc sách/cuộn giấy
  | 'open_door'      // Mở cửa
  | 'close_door'     // Đóng cửa
  | 'push'           // Đẩy
  | 'pull';          // Kéo

/** Socket gắn đồ vật trên nhân vật */
export type AttachSocket =
  | 'weapon_r'       // Tay phải
  | 'weapon_l'       // Tay trái
  | 'both_hands'     // Hai tay
  | 'back'           // Lưng
  | 'hip_r'          // Hông phải
  | 'hip_l'          // Hông trái
  | 'head';          // Đầu (mũ, nón)

/** Các pha của hành động tương tác */
export interface InteractionPhases {
  reach: number;       // 0.0 - 0.25 (duỗi tay ra)
  grab: number;        // 0.25 - 0.35 (nắm chặt)
  action: number;      // 0.35 - 0.75 (thực hiện hành động)
  release: number;     // 0.75 - 1.0 (thả/đặt xuống)
}

/** Track tương tác đồ vật trên timeline */
export interface ObjectInteractionTrack {
  start: number;
  end: number;
  actor_id: string;
  interaction: InteractionType;
  target_object: string;          // "props.water_cup_01"
  attach_socket: AttachSocket;
  destination?: Vec3Tuple;        // Vị trí đặt xuống (cho place/throw)
  give_to_actor?: string;         // Actor nhận (cho give)
  phases?: InteractionPhases;     // Override phase timing
  facial_during?: string;         // Biểu cảm trong lúc tương tác
  sound_effect?: string;          // Âm thanh
}

// ============================================================
// SMART SOCKET (Mở rộng)
// ============================================================

/** Loại socket thông minh mở rộng */
export type SmartSocketType =
  | 'chair'          // Ngồi ghế
  | 'tree'           // Trèo cây
  | 'farm_plot'      // Ruộng
  | 'table'          // Bàn (đặt đồ, ăn cơm)
  | 'bed'            // Giường (nằm, ngủ)
  | 'door'           // Cửa (mở/đóng)
  | 'ladder'         // Thang (leo)
  | 'well'           // Giếng nước (kéo gàu)
  | 'anvil'          // Đe rèn (rèn vũ khí)
  | 'cauldron'       // Nồi nấu (pha thuốc)
  | 'bookshelf'      // Kệ sách (đọc sách)
  | 'weapon_rack'    // Giá vũ khí (chọn vũ khí)
  | 'chest'          // Rương (mở/đóng, lấy đồ)
  | 'altar'          // Bàn thờ/tế đàn
  | 'training_dummy'; // Hình nộm luyện tập

/** Cấu hình socket mở rộng */
export interface SmartSocketConfig {
  id: string;
  type: SmartSocketType;
  entryPosition: Vec3Tuple;
  targetPosition: Vec3Tuple;
  targetRotationY?: number;
  interactable_items?: string[];    // Danh sách đồ vật có thể tương tác
  required_animation?: string;      // Animation bắt buộc khi tương tác
  occupied_by?: string | null;      // Actor đang chiếm slot
  capacity?: number;                // Số slot (ví dụ: bàn 4 ghế = 4)
  interaction_radius?: number;      // Bán kính tương tác (mét)
}

// ============================================================
// OBJECT UPGRADE (Nâng cấp vật thể)
// ============================================================

/** Loại VFX nâng cấp */
export type UpgradeVFXType =
  | 'construction_dust'   // Bụi xây dựng
  | 'magic_sparkle'       // Lấp lánh phép thuật
  | 'growth_burst'        // Bùng nổ sinh trưởng (cây)
  | 'light_pillar'        // Cột sáng trời chiếu xuống
  | 'fire_forge'          // Lửa rèn
  | 'ice_crystallize'     // Đóng băng kết tinh
  | 'energy_absorb';      // Hấp thụ năng lượng

/** Cấu hình thay đổi hình ảnh khi nâng cấp */
export interface UpgradeVisualChanges {
  swap_model?: string;                  // Đổi mesh sang model mới
  scale_change?: Vec3Tuple;             // Thay đổi kích thước
  color_change?: string;                // Đổi màu (hex)
  emissive_color?: string;              // Màu phát sáng
  emissive_intensity?: number;          // Cường độ phát sáng
  add_decorations?: string[];           // Thêm phụ kiện
  remove_parts?: string[];              // Bỏ bớt parts
  material_change?: 'wood' | 'stone' | 'metal' | 'crystal' | 'gold';
}

/** Cấu hình sự kiện nâng cấp */
export interface UpgradeEvent {
  target: string;                       // "props.house_01"
  trigger_time: number;                 // Thời điểm kích hoạt
  upgrade_duration: number;             // Thời gian hiệu ứng (3-5s)
  from_level: number;
  to_level: number;
  visual_changes: UpgradeVisualChanges;
  vfx: {
    type: UpgradeVFXType;
    color: string;
    intensity: number;
    duration?: number;
  };
  camera_focus?: boolean;               // Camera tự zoom vào
  sound_effect?: string;
}

/** Sự kiện spawn vật thể mới */
export interface SpawnObjectEvent {
  object_id: string;
  model: string;
  position: Vec3Tuple;
  rotation_y?: number;
  scale?: Vec3Tuple;
  spawn_time: number;
  spawn_vfx?: string;
  spawn_anim?: 'fade_in' | 'grow' | 'drop' | 'materialize';
  spawn_duration?: number;
}

/** Sự kiện phá hủy vật thể */
export interface DestroyObjectEvent {
  object_id: string;
  destroy_time: number;
  destroy_vfx?: string;
  destroy_anim?: 'fade_out' | 'explode' | 'collapse' | 'dissolve';
  destroy_duration?: number;
  debris?: boolean;                     // Có mảnh vỡ bay ra không
}

// ============================================================
// INVENTORY (Túi đồ)
// ============================================================

/** Loại vật phẩm */
export type ItemCategory =
  | 'weapon'         // Vũ khí
  | 'armor'          // Giáp
  | 'consumable'     // Đồ tiêu hao (thuốc, thức ăn)
  | 'material'       // Nguyên liệu
  | 'tool'           // Dụng cụ (cuốc, xẻng, bình tưới)
  | 'quest'          // Đồ quest
  | 'key';           // Chìa khóa / vật phẩm đặc biệt

/** Vật phẩm trong inventory */
export interface InventoryItem {
  item_id: string;
  name: string;
  category: ItemCategory;
  model_id?: string;             // Model 3D khi cầm trên tay
  icon?: string;                 // Icon UI
  stackable?: boolean;           // Có xếp chồng được không
  quantity: number;
  equip_socket?: AttachSocket;   // Socket khi trang bị
  effects?: Record<string, number>;  // Hiệu ứng (damage: 10, heal: 50)
}

/** Inventory của actor */
export interface ActorInventory {
  actor_id: string;
  max_slots: number;
  items: InventoryItem[];
  equipped: {
    weapon_r?: string;           // item_id
    weapon_l?: string;
    armor?: string;
    accessory?: string;
  };
}

/** Track hành động inventory trên timeline */
export interface InventoryActionTrack {
  start: number;
  end: number;
  actor_id: string;
  action: 'equip' | 'unequip' | 'use' | 'drop' | 'give';
  item_id: string;
  target_actor?: string;          // Cho give
  anim_override?: string;        // Animation tùy chỉnh
}

// ============================================================
// TRANSFORMATION (Biến thân nhân vật)
// ============================================================

/** Thay đổi hình dạng cơ thể khi biến thân */
export interface BodyMorphConfig {
  scale_multiplier: number;       // 1.0 -> 1.5 (phóng to)
  muscle_weight?: number;         // 0 -> 1
  aura_color: string;             // "#ffdd00"
  aura_intensity?: number;        // 0 -> 2
  skin_color_change?: string;     // Đổi màu da
  eye_color_change?: string;      // Đổi màu mắt
  hair_color_change?: string;     // Đổi màu tóc
}

/** VFX cho quá trình biến thân */
export interface TransformVFXConfig {
  pre_transform: string;          // "energy_charge_aura"
  during: string;                 // "light_explosion"
  post_transform: string;         // "power_aura_loop"
}

/** Cấu hình biến thân nhân vật */
export interface TransformConfig {
  actor_id: string;
  trigger_time: number;           // Thời điểm kích hoạt
  duration: number;               // Thời gian biến thân (2-5s)
  from_costume: string;           // "default_armor"
  to_costume: string;             // "super_saiyan_armor"
  body_morph?: BodyMorphConfig;
  vfx: TransformVFXConfig;
  camera_action?: 'zoom_in' | 'orbit' | 'dramatic_zoom';
  anim_during?: string;           // Animation trong lúc biến thân
  screen_flash?: boolean;         // Flash trắng toàn màn hình
  slow_motion?: boolean;          // Slow motion trong lúc biến thân
  power_level_text?: string;      // Text hiển thị "Level Up!" / "Siêu Cấp!"
}
