/**
 * MapAssetRegistry.ts
 *
 * Dynamic asset registry for 3D Maps, Props, Environment, Animals, and Materials.
 * Automatically loads category schema from asset_structure.json / asset_manifest.json.
 * Supports Vietnamese taxonomy, automatic sub-tab discovery, and companion preview photos.
 */

export interface MapAssetItem {
  id: string;
  name: string;
  path: string;
  format: string;
  sizeMB: string;
  previewUrl?: string;
  category: string;
  subCategory?: string;
  description?: string;
}

export interface MapSubCategory {
  id: string;
  label: string;
  icon?: string;
  items: MapAssetItem[];
}

export interface MapCategory {
  id: string;
  label: string;
  icon: string;
  subCategories: MapSubCategory[];
  items: MapAssetItem[]; // All items in this category
}

// ─── Default Fallback Assets (When manifest is loading or empty) ────────────────

const DEFAULT_MAPS: MapAssetItem[] = [
  {
    id: 'cathedral',
    name: 'Thánh Đường (Cathedral)',
    path: 'assets/ban_do/cathedral.glb',
    format: 'GLB',
    sizeMB: '103.80',
    category: 'ban_do',
    description: 'Thánh đường cổ kính chi tiết cao',
  },
  {
    id: 'game_pirate_adventure_map',
    name: 'Đảo Hải Tặc (Pirate Island)',
    path: 'assets/ban_do/game_pirate_adventure_map.glb',
    format: 'GLB',
    sizeMB: '7.48',
    category: 'ban_do',
    description: 'Bản đồ đảo biển hải tặc nhiệt đới',
  },
  {
    id: 'zone9_real_light',
    name: 'Zone 9 Khu Đô Thị (Zone9 Real Light)',
    path: 'assets/ban_do/zone9_real_light.glb',
    format: 'GLB',
    sizeMB: '36.33',
    category: 'ban_do',
    description: 'Khu bối cảnh đô thị chân thực',
  },
];

const DEFAULT_SKYBOXES: MapAssetItem[] = [
  {
    id: 'buoi_sang_khong_may_1',
    name: 'Buổi Sáng Trong Xanh',
    path: 'assets/SkyBoxs/buoi_sang/khong_may/buoi_sang_khong_may_1.png',
    format: 'PNG',
    sizeMB: '1.00',
    previewUrl: '/assets/SkyBoxs/buoi_sang/khong_may/buoi_sang_khong_may_1.png',
    category: 'bau_troi',
    subCategory: 'buoi_sang',
  },
  {
    id: 'buoi_trua_it_may_1',
    name: 'Buổi Trưa Nắng Gắt',
    path: 'assets/SkyBoxs/buoi_trua/it_may/buoi_trua_it_may_1.png',
    format: 'PNG',
    sizeMB: '1.00',
    previewUrl: '/assets/SkyBoxs/buoi_trua/it_may/buoi_trua_it_may_1.png',
    category: 'bau_troi',
    subCategory: 'buoi_trua',
  },
  {
    id: 'buoi_chieu_it_may_1',
    name: 'Buổi Chiều Hoàng Hôn',
    path: 'assets/SkyBoxs/buoi_chieu/it_may/buoi_chieu_it_may_1.png',
    format: 'PNG',
    sizeMB: '0.86',
    previewUrl: '/assets/SkyBoxs/buoi_chieu/it_may/buoi_chieu_it_may_1.png',
    category: 'bau_troi',
    subCategory: 'buoi_chieu',
  },
  {
    id: 'buoi_toi_it_may_1',
    name: 'Ban Đêm Trăng Sao',
    path: 'assets/SkyBoxs/buoi_toi/it_may/buoi_toi_it_may_1.png',
    format: 'PNG',
    sizeMB: '0.27',
    previewUrl: '/assets/SkyBoxs/buoi_toi/it_may/buoi_toi_it_may_1.png',
    category: 'bau_troi',
    subCategory: 'buoi_toi',
  },
];

const DEFAULT_PROPS: MapAssetItem[] = [
  {
    id: 'lantern_prop',
    name: 'Đèn Lồng Cổ Trang (Lantern)',
    path: 'assets/props/lantern_prop.glb',
    format: 'GLB',
    sizeMB: '9.42',
    category: 'noi_that',
  },
  {
    id: 'duck_prop',
    name: 'Vịt Vàng (Duck)',
    path: 'assets/props/duck_prop.glb',
    format: 'GLB',
    sizeMB: '0.11',
    category: 'dong_vat',
    subCategory: 'duoi_nuoc',
  },
];

// ─── Default Fallback Categories ──────────────────────────────────────

export const MAP_CATEGORY_DEFINITIONS: Array<{
  id: string;
  label: string;
  icon: string;
  subCategories?: Array<{ id: string; label: string; icon?: string }>;
}> = [
  { id: 'ban_do', label: 'Bản Đồ 3D', icon: '🗺️' },
  { id: 'cay_coi', label: 'Cây Cối', icon: '🌳' },
  { id: 'da_dia_hinh', label: 'Đá & Địa Hình', icon: '🪨' },
  {
    id: 'dong_vat',
    label: 'Động Vật',
    icon: '🦁',
    subCategories: [
      { id: 'tren_can', label: 'Trên Cạn', icon: '🦁' },
      { id: 'duoi_nuoc', label: 'Dưới Nước', icon: '🐬' },
      { id: 'tren_troi', label: 'Trên Trời', icon: '🦅' },
    ],
  },
  { id: 'cong_trinh', label: 'Công Trình', icon: '🏠' },
  { id: 'noi_that', label: 'Nội Thất', icon: '🪑' },
  { id: 'dung_cu', label: 'Dụng Cụ', icon: '🔧' },
  { id: 'do_tieu_hao', label: 'Đồ Tiêu Hao', icon: '🍵' },
  { id: 'vu_khi', label: 'Vũ Khí', icon: '⚔️' },
  { id: 'phuong_tien', label: 'Phương Tiện', icon: '🐴' },
  {
    id: 'bau_troi',
    label: 'Bầu Trời',
    icon: '🌌',
    subCategories: [
      { id: 'binh_minh', label: 'Bình Minh', icon: '🌅' },
      { id: 'buoi_sang', label: 'Buổi Sáng', icon: '☀️' },
      { id: 'buoi_trua', label: 'Buổi Trưa', icon: '🌞' },
      { id: 'buoi_chieu', label: 'Buổi Chiều', icon: '🌇' },
      { id: 'buoi_toi', label: 'Buổi Tối', icon: '🌙' },
      { id: 'giong_bao', label: 'Giông Bão', icon: '⛈️' },
    ],
  },
  {
    id: 'hieu_ung',
    label: 'Hiệu Ứng VFX',
    icon: '✨',
    subCategories: [
      { id: 'cam_xuc', label: 'Biểu Cảm', icon: '😃' },
      { id: 'bao_phu', label: 'Lớp Phủ', icon: '🌫️' },
    ],
  },
];

/**
 * Fetch and build dynamic Map and Prop categories from live manifest & structure JSON.
 */
export async function fetchLiveMapCategories(): Promise<MapCategory[]> {
  try {
    const res = await fetch(`/assets/asset_manifest.json?t=${Date.now()}`);
    if (!res.ok) return buildFallbackCategories();

    const manifestData = await res.json();
    const structure = manifestData.structure?.world_and_props_structure;

    const deduplicateItems = (list: MapAssetItem[]): MapAssetItem[] => {
      const map = new Map<string, MapAssetItem>();
      for (const item of list) {
        const key = item.path || item.id;
        if (!map.has(key)) {
          map.set(key, item);
        }
      }
      return Array.from(map.values());
    };

    const parseItem = (p: any, cat: string, sub?: string): MapAssetItem => {
      const rel = p.relPath || p.path || '';
      const cleanPath = rel ? (rel.startsWith('assets/') ? rel : `assets/${rel}`) : '';
      const uniqueId = p.id
        ? `${cat}_${sub ? sub + '_' : ''}${p.id}`
        : cleanPath
        ? cleanPath.replace(/[/\\ \-_.]/g, '_')
        : `${cat}_${sub ? sub + '_' : ''}${p.name || Math.random().toString(36).substr(2, 6)}`;

      const isImg = cleanPath.endsWith('.png') || cleanPath.endsWith('.jpg') || cleanPath.endsWith('.webp');
      const resolvedPreview = p.previewUrl
        ? (p.previewUrl.startsWith('/') ? p.previewUrl : `/${p.previewUrl}`)
        : isImg
        ? (cleanPath.startsWith('/') ? cleanPath : `/${cleanPath}`)
        : undefined;

      return {
        id: uniqueId,
        name: p.name || formatDisplayName(p.filename || p.relPath),
        path: cleanPath,
        format: p.format || (cleanPath.endsWith('.png') ? 'PNG' : 'GLB'),
        sizeMB: p.sizeMB || '0.5',
        previewUrl: resolvedPreview,
        category: cat,
        subCategory: sub,
        description: p.description,
      };
    };

    const parseItemList = (list: any[], cat: string, sub?: string): MapAssetItem[] => {
      if (!Array.isArray(list)) return [];
      return deduplicateItems(list.map(item => parseItem(item, cat, sub)));
    };

    // If structure is defined in root JSON, build dynamically from schema
    if (structure && Array.isArray(structure.categories)) {
      const categoriesList: MapCategory[] = structure.categories.map((catConfig: any) => {
        const catId = catConfig.id;
        const aliases = [catId, catConfig.folder, ...(catConfig.folder_aliases || [])];
        let rawItems: any[] = [];

        // Maps category
        if (catId === 'ban_do' || catConfig.folder === 'ban_do' || catConfig.folder_aliases?.includes('maps')) {
          rawItems = manifestData.maps || [];
        } else if (catId === 'bau_troi' || catConfig.folder === 'bau_troi' || catConfig.folder_aliases?.includes('SkyBoxs')) {
          rawItems = manifestData.skyboxes?.all || [];
        } else if (catId === 'hieu_ung' || catConfig.folder === 'hieu_ung' || catConfig.folder_aliases?.includes('vfx')) {
          rawItems = manifestData.props?.vfx || manifestData.vfx || [];
        } else {
          // Check manifest.props
          for (const a of aliases) {
            if (manifestData.props && manifestData.props[a]) {
              const val = manifestData.props[a];
              if (Array.isArray(val)) {
                rawItems = val;
                break;
              } else if (val.all && Array.isArray(val.all)) {
                rawItems = val.all;
                break;
              }
            }
          }
        }

        // Subcategories
        const subCategoriesConfig: any[] = catConfig.subcategories || [];
        let items: MapAssetItem[] = [];

        if (subCategoriesConfig.length > 0) {
          const subCategories: MapSubCategory[] = subCategoriesConfig.map((subConf) => {
            const subAliases = [subConf.id, subConf.folder, ...(subConf.folder_aliases || [])];
            let subRaw: any[] = [];

            // Check if nested in props object
            if (catId === 'dong_vat' && manifestData.props?.animals) {
              if (subConf.id === 'tren_can') subRaw = manifestData.props.animals.terrestrial || [];
              else if (subConf.id === 'duoi_nuoc') subRaw = manifestData.props.animals.aquatic || [];
              else if (subConf.id === 'tren_troi') subRaw = manifestData.props.animals.aerial || [];
            } else if (catId === 'bau_troi' && manifestData.skyboxes) {
              subRaw = manifestData.skyboxes[subConf.id] || [];
            }

            if (subRaw.length === 0) {
              subRaw = rawItems.filter((it: any) => {
                const p = (it.relPath || it.path || '').toLowerCase();
                return subAliases.some(alias => p.includes(`/${alias}/`) || p.includes(`_${alias}`));
              });
            }

            const parsedSubItems = parseItemList(subRaw, catId, subConf.id);
            return {
              id: subConf.id,
              label: subConf.label,
              icon: subConf.icon,
              items: parsedSubItems,
            };
          });

          // All items in category is combination of sub-items or raw items
          const allSubItems = deduplicateItems(subCategories.flatMap(s => s.items));
          items = allSubItems.length > 0 ? allSubItems : deduplicateItems(parseItemList(rawItems, catId));

          // Add default fallback if maps or skyboxes empty
          if (items.length === 0) {
            if (catId === 'ban_do') items = [...DEFAULT_MAPS];
            else if (catId === 'bau_troi') items = [...DEFAULT_SKYBOXES];
          }

          return {
            id: catId,
            label: catConfig.label,
            icon: catConfig.icon || '📦',
            subCategories,
            items,
          };
        }

        // Category without subcategories
        items = deduplicateItems(parseItemList(rawItems, catId));
        if (items.length === 0) {
          if (catId === 'ban_do') items = [...DEFAULT_MAPS];
          else if (catId === 'noi_that') items = DEFAULT_PROPS.filter(p => p.category === 'noi_that');
        }

        return {
          id: catId,
          label: catConfig.label,
          icon: catConfig.icon || '📦',
          subCategories: [],
          items,
        };
      });

      return enrichWithLocalPresets(categoriesList);
    }

    return enrichWithLocalPresets(buildFallbackCategories(manifestData));
  } catch (err) {
    console.warn('Using default map category fallback:', err);
    return enrichWithLocalPresets(buildFallbackCategories());
  }
}

function enrichWithLocalPresets(cats: MapCategory[]): MapCategory[] {
  try {
    const deduplicate = (list: MapAssetItem[]): MapAssetItem[] => {
      const map = new Map<string, MapAssetItem>();
      for (const item of list) {
        const key = item.path || item.id;
        if (!map.has(key)) map.set(key, item);
      }
      return Array.from(map.values());
    };

    // 1. Custom Maps (_custom_ban_do)
    const customMapsRaw = typeof localStorage !== 'undefined' ? localStorage.getItem('custom_map_designer_presets') : null;
    if (customMapsRaw) {
      const customMaps = JSON.parse(customMapsRaw);
      if (Array.isArray(customMaps) && customMaps.length > 0) {
        const customMapItems: MapAssetItem[] = customMaps.map((cm: any) => {
          const rawName = cm.name || 'custom_map';
          const cleanName = rawName.replace(/[\uD800-\uDBFF][\uDC00-\uDFFF]|[\u2600-\u27BF]|\p{Emoji}/gu, '').trim();
          const safeName = (cleanName || rawName)
            .replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF\-]/g, '_')
            .toLowerCase()
            .replace(/^_+/, '') || 'custom_map';
          return {
            id: `local_map_${cm.name}`,
            name: cm.name,
            path: `assets/ban_do/_custom_ban_do/${safeName}.json`,
            format: 'JSON',
            sizeMB: '0.1',
            previewUrl: cm.preview_image || undefined,
            category: '_custom_ban_do',
            description: `Bản đồ tự thiết kế (${cm.placed_objects?.length || 0} vật thể)`,
          };
        });

        let customCat = cats.find((c) => c.id === '_custom_ban_do');
        if (!customCat) {
          customCat = {
            id: '_custom_ban_do',
            label: 'Bản Đồ Tự Thiết Kế',
            icon: '🏰',
            subCategories: [],
            items: [],
          };
          cats.unshift(customCat);
        }
        customCat.items = deduplicate([...customMapItems, ...customCat.items]);
      }
    }

    // 2. Assembled Characters (nhan_vat_da_rap)
    const customCharsRaw = typeof localStorage !== 'undefined' ? localStorage.getItem('custom_character_presets') : null;
    if (customCharsRaw) {
      const customChars = JSON.parse(customCharsRaw);
      if (Array.isArray(customChars) && customChars.length > 0) {
        const customCharItems: MapAssetItem[] = customChars.map((cc: any) => {
          const rawName = cc.name || 'char';
          const cleanName = rawName.replace(/[\uD800-\uDBFF][\uDC00-\uDFFF]|[\u2600-\u27BF]|\p{Emoji}/gu, '').trim();
          const safeName = (cleanName || rawName)
            .replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF\-]/g, '_')
            .toLowerCase()
            .replace(/^_+/, '') || 'nhan_vat_lap_rap';
          return {
            id: `local_char_${cc.id || cc.name}`,
            name: cc.name,
            path: `assets/nhan_vat/_lap_rap/${safeName}.json`,
            format: 'JSON',
            sizeMB: '0.1',
            previewUrl: cc.preview || cc.profile?.preview_image || undefined,
            category: 'nhan_vat_da_rap',
            description: `${cc.gender === 'female' ? 'Nữ' : 'Nam'} • Đã phối đồ modular`,
          };
        });

        let charCat = cats.find((c) => c.id === 'nhan_vat_da_rap');
        if (!charCat) {
          charCat = {
            id: 'nhan_vat_da_rap',
            label: 'Nhân Vật Đã Lắp Ráp',
            icon: '🧑',
            subCategories: [],
            items: [],
          };
          cats.push(charCat);
        }
        charCat.items = deduplicate([...customCharItems, ...charCat.items]);
      }
    }
  } catch (e) {
    console.warn('Enrich local presets error:', e);
  }
  return cats;
}

function buildFallbackCategories(manifestData?: any): MapCategory[] {
  const mapItems = manifestData?.maps?.map((m: any) => ({
    id: m.id || m.name,
    name: m.name || formatDisplayName(m.filename || m.relPath),
    path: m.relPath ? `assets/${m.relPath}` : 'assets/maps/cathedral.glb',
    format: m.format || 'GLB',
    sizeMB: m.sizeMB || '1.0',
    previewUrl: m.previewUrl ? `/${m.previewUrl}` : undefined,
    category: 'ban_do',
  })) || DEFAULT_MAPS;

  return MAP_CATEGORY_DEFINITIONS.map((def) => ({
    id: def.id,
    label: def.label,
    icon: def.icon,
    subCategories: (def.subCategories || []).map(s => ({
      id: s.id,
      label: s.label,
      icon: s.icon,
      items: [],
    })),
    items: def.id === 'ban_do' ? mapItems : [],
  }));
}

function formatDisplayName(filename: string): string {
  if (!filename) return 'Tài Nguyên';
  return filename
    .replace(/\.[^/.]+$/, '')
    .replace(/_/g, ' ')
    .replace(/-/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase());
}
