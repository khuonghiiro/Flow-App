import { AssetCatalogItem } from '../../types/scene';

export class AssetScanner {
  private static defaultCatalog: AssetCatalogItem[] = [
    {
      id: 'actor_warrior',
      name: 'Chiến Binh Áo Giáp (Hero Knight)',
      category: 'character',
      path: 'characters/hero_knight.vrm',
      tags: ['hero', 'knight', 'armor', 'sword'],
      metadata: { height: 1.8, sockets: ['weapon_r', 'head', 'root'] },
    },
    {
      id: 'actor_dark_mage',
      name: 'Phù Thủy Tối Thượng (Dark Mage)',
      category: 'character',
      path: 'characters/dark_mage.vrm',
      tags: ['mage', 'villain', 'magic', 'robe'],
      metadata: { height: 1.75, sockets: ['weapon_r', 'weapon_l', 'head', 'root'] },
    },
    {
      id: 'weapon_fire_sword',
      name: 'Thanh Kiếm Lửa',
      category: 'weapon',
      path: 'weapons/fire_sword.glb',
      tags: ['sword', 'fire', 'melee'],
      metadata: { length: 1.1, socket_target: 'weapon_r' },
    },
    {
      id: 'weapon_magic_staff',
      name: 'Trượng Hắc Thuật',
      category: 'weapon',
      path: 'weapons/magic_staff.glb',
      tags: ['staff', 'magic', 'ranged'],
      metadata: { length: 1.4, socket_target: 'weapon_r' },
    },
    {
      id: 'prop_wooden_chair',
      name: 'Ghế Gỗ Làng Quê',
      category: 'prop',
      path: 'props/wooden_chair.glb',
      tags: ['chair', 'furniture', 'smart_socket'],
      metadata: { seat_height: 0.5, entry_offset: [0, 0, 0.8] },
    },
    {
      id: 'prop_village_tree',
      name: 'Cây Cổ Thụ Làng',
      category: 'prop',
      path: 'props/village_tree.glb',
      tags: ['tree', 'nature', 'climbable'],
      metadata: { trunk_radius: 0.6, branch_height: 2.8 },
    },
    {
      id: 'prop_farm_plot',
      name: 'Mảnh Ruộng Đất Màu',
      category: 'prop',
      path: 'props/farm_plot.glb',
      tags: ['farm', 'ground', 'farming_system'],
      metadata: { stages: ['seed', 'sprout', 'growing', 'mature_crop'] },
    },
    {
      id: 'vfx_sword_slash_fire',
      name: 'Vệt Chém Lửa Bùng Cháy',
      category: 'vfx',
      path: 'vfx/sword_slash_fire.json',
      tags: ['slash', 'fire', 'trail'],
    },
    {
      id: 'vfx_impact_hit_sparks',
      name: 'Tia Lửa Va Chạm Kim Loại',
      category: 'vfx',
      path: 'vfx/impact_hit_sparks.json',
      tags: ['sparks', 'impact', 'combat'],
    },
    {
      id: 'vfx_magic_shield_barrier',
      name: 'Khiên Năng Lượng Ma Pháp',
      category: 'vfx',
      path: 'vfx/magic_shield_barrier.json',
      tags: ['shield', 'barrier', 'defense'],
    },
  ];

  public static getCatalog(): AssetCatalogItem[] {
    return [...this.defaultCatalog];
  }

  public static getByCategory(category: AssetCatalogItem['category']): AssetCatalogItem[] {
    return this.defaultCatalog.filter((item) => item.category === category);
  }

  public static findById(id: string): AssetCatalogItem | undefined {
    return this.defaultCatalog.find((item) => item.id === id);
  }
}
