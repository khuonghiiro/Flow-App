/**
 * AssetBrowserPanel.tsx
 *
 * Full-featured Project Assets Browser with Vertical Tabs, Vietnamese Taxonomy,
 * Subcategory Pills, Gender Filters, Quick Actions, and Live 3D Preview with IndexedDB caching.
 * Designed to match the look-and-feel of Character Workbench & Map Designer.
 */
import React, { useState, useMemo, useEffect, useCallback, useRef } from 'react';
import {
  Folder,
  FolderOpen,
  Box,
  User,
  Map as MapIcon,
  Music,
  Sparkles,
  Film,
  Search,
  Plus,
  Check,
  ChevronRight,
  Upload,
  Play,
  Eye,
  Maximize2,
  Minimize2,
  Layers,
  RefreshCw,
  Sliders,
} from 'lucide-react';
import { PlacedProp } from '../types/map_preset';
import { CharacterAssembly } from '../types/scene';
import { Live3DThumbnail } from './Live3DThumbnail';

export interface AssetItem {
  id: string;
  name: string;
  path: string;
  category?: string;
  subCategory?: string;
  folder: string;
  type: 'prop' | 'character' | 'map' | 'audio' | 'vfx' | 'animation' | 'skybox';
  format: string;
  size?: string;
  sizeMB?: string;
  tags?: string[];
  description?: string;
  previewUrl?: string;
  previewColor?: string;
  gender?: 'male' | 'female' | 'unisex';
  assembly?: CharacterAssembly;
  // Specific data
  propData?: Partial<PlacedProp>;
  mapId?: string;
  vrmUrl?: string;
  audioUrl?: string;
  animName?: string;
}

export interface SubCategoryTab {
  id: string;
  label: string;
  icon?: string;
}

export interface CategoryTab {
  id: string;
  label: string;
  icon: string;
  group: 'character' | 'world' | 'media';
  subCategories?: SubCategoryTab[];
}

interface AssetBrowserPanelProps {
  onPlaceProp: (prop: AssetItem) => void;
  onSelectMap: (mapId: string) => void;
  onSelectAvatar: (actorId: string, vrmUrl: string, assembly?: CharacterAssembly) => void;
  onPlayAnimationPreview?: (animName: string) => void;
  onImportCustomFiles?: (files: FileList | File[]) => void;
  actorsList?: { id: string; name: string }[];
  isMaximized?: boolean;
  onToggleMaximize?: () => void;
}

// ─── Standard Category Tabs (Vietnamese taxonomy) ───────────────
const CATEGORY_TABS: CategoryTab[] = [
  // ─── Characters ───
  { id: 'than_co_ban', label: 'Thân Cơ Bản', icon: '🧍', group: 'character' },
  { id: 'trang_phuc', label: 'Trang Phục', icon: '👘', group: 'character' },
  { id: 'khuon_mat', label: 'Khuôn Mặt', icon: '🎭', group: 'character' },
  { id: 'kieu_toc', label: 'Kiểu Tóc', icon: '💇', group: 'character' },
  { id: 'mu_non', label: 'Mũ & Nón', icon: '🎩', group: 'character' },
  { id: 'giay_dep', label: 'Giày Dép', icon: '👟', group: 'character' },
  { id: 'phu_kien', label: 'Phụ Kiện', icon: '💍', group: 'character' },
  { id: 'kieu_rau', label: 'Râu', icon: '🧔', group: 'character' },
  { id: 'canh', label: 'Đôi Cánh', icon: '🪽', group: 'character' },
  { id: 'duoi', label: 'Đuôi Thú', icon: '🦊', group: 'character' },
  { id: '_lap_rap', label: 'Nhân Vật Đã Ráp', icon: '✨', group: 'character' },

  // ─── World & Maps ───
  { id: 'ban_do', label: 'Bản Đồ 3D', icon: '🗺️', group: 'world' },
  { id: '_custom_ban_do', label: 'Map Tự Thiết Kế', icon: '🏰', group: 'world' },
  { id: 'cay_coi', label: 'Cây Cối', icon: '🌳', group: 'world' },
  { id: 'da_dia_hinh', label: 'Đá & Địa Hình', icon: '🪨', group: 'world' },
  {
    id: 'dong_vat',
    label: 'Động Vật',
    icon: '🐾',
    group: 'world',
    subCategories: [
      { id: 'tren_can', label: 'Trên Cạn', icon: '🐾' },
      { id: 'duoi_nuoc', label: 'Dưới Nước', icon: '🐟' },
      { id: 'tren_troi', label: 'Trên Trời', icon: '🦅' },
    ],
  },
  { id: 'vu_khi', label: 'Vũ Khí', icon: '⚔️', group: 'world' },
  { id: 'dung_cu', label: 'Dụng Cụ', icon: '🛠️', group: 'world' },
  { id: 'do_tieu_hao', label: 'Đồ Tiêu Hao', icon: '🏺', group: 'world' },
  { id: 'noi_that', label: 'Nội Thất', icon: '🪑', group: 'world' },
  { id: 'cong_trinh', label: 'Công Trình', icon: '🏯', group: 'world' },
  { id: 'phuong_tien', label: 'Phương Tiện', icon: '🚗', group: 'world' },
  {
    id: 'bau_troi',
    label: 'Bầu Trời & Skybox',
    icon: '🌅',
    group: 'world',
    subCategories: [
      { id: 'binh_minh', label: 'Bình Minh', icon: '🌅' },
      { id: 'buoi_sang', label: 'Buổi Sáng', icon: '☀️' },
      { id: 'buoi_trua', label: 'Buổi Trưa', icon: '🌞' },
      { id: 'buoi_chieu', label: 'Buổi Chiều', icon: '🌇' },
      { id: 'buoi_toi', label: 'Buổi Tối', icon: '🌙' },
      { id: 'giong_bao', label: 'Giông Bão', icon: '⛈️' },
    ],
  },

  // ─── Media & VFX ───
  {
    id: 'audio',
    label: 'Âm Thanh',
    icon: '🎵',
    group: 'media',
    subCategories: [
      { id: 'bgm', label: 'Nhạc Nền (BGM)', icon: '🎼' },
      { id: 'sfx_combat', label: 'SFX Chiến Đấu', icon: '⚔️' },
      { id: 'sfx_ambient', label: 'SFX Môi Trường', icon: '🍃' },
      { id: 'sfx_interaction', label: 'SFX Tương Tác', icon: '💬' },
    ],
  },
  {
    id: 'animations',
    label: 'Hoạt Ảnh (Anim)',
    icon: '🎬',
    group: 'media',
    subCategories: [
      { id: 'combat', label: 'Chiến Đấu', icon: '⚔️' },
      { id: 'xianxia', label: 'Tiên Hiệp', icon: '🧘' },
      { id: 'locomotion', label: 'Di Chuyển', icon: '🚶' },
      { id: 'interactions', label: 'Tương Tác', icon: '🤝' },
    ],
  },
  {
    id: 'hieu_ung',
    label: 'Hiệu Ứng (VFX)',
    icon: '✨',
    group: 'media',
    subCategories: [
      { id: 'cam_xuc', label: 'Cảm Xúc', icon: '💭' },
      { id: 'bao_phu', label: 'Bao Phủ', icon: '🌪️' },
    ],
  },
];

export const AssetBrowserPanel: React.FC<AssetBrowserPanelProps> = ({
  onPlaceProp,
  onSelectMap,
  onSelectAvatar,
  onPlayAnimationPreview,
  onImportCustomFiles,
  actorsList = [],
  isMaximized = false,
  onToggleMaximize,
}) => {
  // ─── State ──────────────────────────────────────────────────
  const [mainSection, setMainSection] = useState<'character' | 'world' | 'media'>('character');
  const [allAssets, setAllAssets] = useState<AssetItem[]>([]);
  const [manifestStructure, setManifestStructure] = useState<any>(null);
  const [activeCategoryId, setActiveCategoryId] = useState<string>('than_co_ban');
  const [activeSubCategoryId, setActiveSubCategoryId] = useState<string>('all');
  const [genderFilter, setGenderFilter] = useState<'all' | 'male' | 'female'>('all');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [selectedAssetId, setSelectedAssetId] = useState<string | null>(null);
  const [isLoadingManifest, setIsLoadingManifest] = useState<boolean>(true);
  const [spawnNotification, setSpawnNotification] = useState<string | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);

  // ─── 1. Load Live Assets from asset_manifest.json ─────────────
  const loadManifest = useCallback(async () => {
    setIsLoadingManifest(true);
    try {
      const res = await fetch(`/assets/asset_manifest.json?t=${Date.now()}`);
      if (!res.ok) throw new Error('Could not fetch asset_manifest.json');
      const manifest = await res.json();
      if (manifest.structure) {
        setManifestStructure(manifest.structure);
      }

      const items: AssetItem[] = [];

      const parseAsset = (
        raw: any,
        category: string,
        type: AssetItem['type'],
        subCategory?: string
      ): AssetItem => {
        const pathLower = (raw.relPath || raw.path || '').toLowerCase();
        let gender: AssetItem['gender'] = raw.gender || 'unisex';
        if (pathLower.includes('/male/') || pathLower.includes('/man/') || pathLower.includes('/nam/')) gender = 'male';
        if (pathLower.includes('/female/') || pathLower.includes('/woman/') || pathLower.includes('/nu/')) gender = 'female';

        const assembly = raw.assembly || (raw.character_data ? {
          base_body: raw.character_data.base_body || raw.character_data.body,
          costume: raw.character_data.costume,
          face: raw.character_data.face,
          hairstyle: raw.character_data.hairstyle,
        } : undefined);

        let finalPath = raw.relPath ? (raw.relPath.startsWith('assets/') ? raw.relPath : `assets/${raw.relPath}`) : (raw.path || '');
        if (raw.format === 'JSON' && assembly?.base_body) {
          finalPath = assembly.base_body;
        }

        return {
          id: raw.id || raw.name || raw.relPath,
          name: raw.name || raw.filename || 'Tài nguyên',
          path: finalPath,
          category,
          subCategory,
          folder: raw.relPath ? raw.relPath.split('/').slice(0, -1).join('/') : '',
          type,
          format: raw.format || 'GLB',
          sizeMB: raw.sizeMB || '0.00',
          previewUrl: raw.previewUrl ? (raw.previewUrl.startsWith('/') ? raw.previewUrl : `/${raw.previewUrl}`) : undefined,
          gender,
          assembly,
          description: raw.description || `${raw.name || raw.filename} (${raw.format || 'GLB'})`,
        };
      };

      // 1. Characters (Dynamic 100% from manifest.characters)
      const chars = manifest.characters || {};
      for (const [catId, list] of Object.entries(chars)) {
        if (Array.isArray(list)) {
          list.forEach((c: any) => items.push(parseAsset(c, catId, 'character')));
        }
      }

      // ─── Assembled Characters from LocalStorage (_lap_rap) ───
      try {
        const savedPresetsRaw = localStorage.getItem('custom_character_presets');
        if (savedPresetsRaw) {
          const savedPresets = JSON.parse(savedPresetsRaw);
          if (Array.isArray(savedPresets)) {
            savedPresets.forEach((p: any, idx: number) => {
              const previewUrl = p.preview
                ? p.preview
                : p.profile?.preview_image
                ? p.profile.preview_image
                : p.costume
                ? (p.costume.endsWith('.glb') ? p.costume.replace('.glb', '.png') : p.costume)
                : p.body
                ? (p.body.endsWith('.glb') ? p.body.replace('.glb', '.png') : p.body)
                : undefined;

              const charBody = p.body || p.base_body || p.than_co_ban || '';

              items.push({
                id: p.id || `assembled_preset_${idx}`,
                name: p.name || `Nhân Vật Đã Ráp #${idx + 1}`,
                category: '_lap_rap',
                subCategory: undefined,
                path: charBody,
                folder: 'characters/assembled',
                type: 'character',
                format: 'JSON',
                sizeMB: '0.01',
                previewUrl: previewUrl && !previewUrl.startsWith('/') && !previewUrl.startsWith('data:') ? `/${previewUrl}` : previewUrl,
                gender: p.gender || (charBody.includes('manekina') ? 'female' : 'male'),
                description: `Đã ráp: Thân [${charBody.split('/').pop() || 'Gốc'}] + Trang phục [${p.costume?.split('/').pop() || 'Mặc định'}] + Mặt [${p.face?.split('/').pop() || 'Gốc'}]`,
                assembly: {
                  base_body: charBody,
                  costume: p.costume,
                  face: p.face,
                  hairstyle: p.hairstyle,
                },
              });
            });
          }
        }
      } catch (err) {
        console.warn('Lỗi đọc custom_character_presets:', err);
      }

      // 2. Maps
      const mapList = manifest.maps || manifest.structure?.map_presets || [];
      if (Array.isArray(mapList)) {
        mapList.forEach((m: any) => items.push(parseAsset(m, 'ban_do', 'map')));
      }

      // 3. Custom Maps
      const customMaps = manifest.props?._custom_ban_do || manifest.map_presets || [];
      if (Array.isArray(customMaps)) {
        customMaps.forEach((m: any) => items.push(parseAsset(m, '_custom_ban_do', 'map')));
      }

      // 4. World Props (Dynamic 100% from manifest.props)
      const props = manifest.props || {};
      for (const [catId, propVal] of Object.entries(props)) {
        if (catId === '_custom_ban_do' || catId === 'nhan_vat_da_rap') continue;
        if (Array.isArray(propVal)) {
          propVal.forEach((p: any) => items.push(parseAsset(p, catId, 'prop')));
        } else if (typeof propVal === 'object' && propVal !== null) {
          for (const [subCatId, subList] of Object.entries(propVal as Record<string, any>)) {
            if (subCatId === 'all') continue;
            if (Array.isArray(subList)) {
              subList.forEach((p: any) => items.push(parseAsset(p, catId, 'prop', subCatId)));
            }
          }
        }
      }

      // 5. Skyboxes
      const skies = manifest.skyboxes || {};
      for (const [timeKey, list] of Object.entries(skies)) {
        if (timeKey === 'all') continue;
        if (Array.isArray(list)) {
          list.forEach((s: any) => items.push(parseAsset(s, 'bau_troi', 'skybox', timeKey)));
        }
      }

      // 6. VFX
      const vfx = manifest.vfx || manifest.props?.vfx || {};
      if (Array.isArray(vfx)) {
        vfx.forEach((v: any) => items.push(parseAsset(v, 'hieu_ung', 'prop')));
      } else if (typeof vfx === 'object') {
        for (const [vfxKey, list] of Object.entries(vfx)) {
          if (vfxKey === 'all') continue;
          if (Array.isArray(list)) {
            list.forEach((v: any) => items.push(parseAsset(v, 'hieu_ung', 'prop', vfxKey)));
          }
        }
      }

      // 7. Audio
      const audio = manifest.audio || {};
      (audio.bgm || []).forEach((a: any) => items.push(parseAsset(a, 'audio', 'audio', 'bgm')));
      (audio.sfx_combat || audio.sfx?.combat || []).forEach((a: any) => items.push(parseAsset(a, 'audio', 'audio', 'sfx_combat')));
      (audio.sfx_ambient || audio.sfx?.ambient || []).forEach((a: any) => items.push(parseAsset(a, 'audio', 'audio', 'sfx_ambient')));
      (audio.sfx_interaction || audio.sfx_interactions || audio.sfx?.interactions || []).forEach((a: any) =>
        items.push(parseAsset(a, 'audio', 'audio', 'sfx_interaction'))
      );

      // 8. Animations
      const anims = manifest.animations || {};
      (anims.combat || []).forEach((a: any) => items.push(parseAsset(a, 'animations', 'animation', 'combat')));
      (anims.xianxia || []).forEach((a: any) => items.push(parseAsset(a, 'animations', 'animation', 'xianxia')));
      (anims.locomotion || []).forEach((a: any) => items.push(parseAsset(a, 'animations', 'animation', 'locomotion')));
      (anims.interactions || anims.interaction || []).forEach((a: any) =>
        items.push(parseAsset(a, 'animations', 'animation', 'interactions'))
      );

      // Deduplicate by ID (or path fallback)
      const uniqueItems = Array.from(new Map(items.map((i) => [i.id || i.path, i])).values());
      setAllAssets(uniqueItems);
    } catch (err) {
      console.warn('Lỗi đọc asset_manifest.json trong Project Assets:', err);
    } finally {
      setIsLoadingManifest(false);
    }
  }, []);

  useEffect(() => {
    loadManifest();
    const handleAssetsUpdated = () => loadManifest();
    window.addEventListener('flow_assets_updated', handleAssetsUpdated);
    window.addEventListener('storage', handleAssetsUpdated);
    return () => {
      window.removeEventListener('flow_assets_updated', handleAssetsUpdated);
      window.removeEventListener('storage', handleAssetsUpdated);
    };
  }, [loadManifest]);

  // ─── Dynamic Category Tabs derived from Manifest Structure ────
  const dynamicCategoryTabs = useMemo<CategoryTab[]>(() => {
    if (!manifestStructure) return CATEGORY_TABS;

    const tabs: CategoryTab[] = [];

    // 1. Character categories from structure
    const charCats = manifestStructure.character_structure?.categories || [];
    for (const c of charCats) {
      tabs.push({
        id: c.id,
        label: c.label || c.id,
        icon: c.icon || '🧍',
        group: 'character',
        subCategories: c.subcategories?.map((s: any) => ({
          id: s.id,
          label: s.label || s.id,
          icon: s.icon
        }))
      });
    }

    // 2. World & Props categories from structure
    const propCats = manifestStructure.world_and_props_structure?.categories || [];
    for (const p of propCats) {
      if (p.id === 'audio' || p.id === 'animations') continue;
      tabs.push({
        id: p.id,
        label: p.label || p.id,
        icon: p.icon || '📦',
        group: p.id === 'hieu_ung' ? 'media' : 'world',
        subCategories: p.subcategories?.map((s: any) => ({
          id: s.id,
          label: s.label || s.id,
          icon: s.icon
        }))
      });
    }

    // 3. Media Tabs
    tabs.push(
      {
        id: 'audio',
        label: 'Âm Thanh',
        icon: '🎵',
        group: 'media',
        subCategories: [
          { id: 'bgm', label: 'Nhạc Nền (BGM)', icon: '🎼' },
          { id: 'sfx_combat', label: 'SFX Chiến Đấu', icon: '⚔️' },
          { id: 'sfx_ambient', label: 'SFX Môi Trường', icon: '🍃' },
          { id: 'sfx_interaction', label: 'SFX Tương Tác', icon: '💬' },
        ],
      },
      {
        id: 'animations',
        label: 'Hoạt Ảnh (Anim)',
        icon: '🎬',
        group: 'media',
        subCategories: [
          { id: 'combat', label: 'Chiến Đấu', icon: '⚔️' },
          { id: 'xianxia', label: 'Tiên Hiệp', icon: '🧘' },
          { id: 'locomotion', label: 'Di Chuyển', icon: '🚶' },
          { id: 'interactions', label: 'Tương Tác', icon: '🤝' },
        ],
      }
    );

    return tabs;
  }, [manifestStructure]);

  // Switch active category when main section changes
  const handleSwitchSection = (section: 'character' | 'world' | 'media') => {
    setMainSection(section);
    setActiveSubCategoryId('all');
    const firstCat = dynamicCategoryTabs.find((t) => t.group === section);
    if (firstCat) {
      setActiveCategoryId(firstCat.id);
    }
  };

  // ─── Active Category & Subcategories ──────────────────────────
  const currentCategoryTab = dynamicCategoryTabs.find((t) => t.id === activeCategoryId) || dynamicCategoryTabs.find((t) => t.group === mainSection) || dynamicCategoryTabs[0];
  const subCategories = currentCategoryTab?.subCategories || [];
  const hasSubCategories = subCategories.length > 0;

  // ─── Count Items Per Category ─────────────────────────────────
  const categoryCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    dynamicCategoryTabs.forEach((cat) => {
      counts[cat.id] = allAssets.filter((a) => a.category === cat.id).length;
    });
    return counts;
  }, [allAssets, dynamicCategoryTabs]);

  // ─── Filtered Items for Display ───────────────────────────────
  const filteredAssets = useMemo(() => {
    return allAssets.filter((asset) => {
      // Category Match
      if (asset.category !== activeCategoryId) return false;

      // SubCategory Match
      if (hasSubCategories && activeSubCategoryId !== 'all') {
        if (asset.subCategory !== activeSubCategoryId) return false;
      }

      // Gender Match
      if (genderFilter !== 'all' && asset.gender && asset.gender !== 'unisex') {
        if (asset.gender !== genderFilter) return false;
      }

      // Search Query
      if (searchQuery.trim() !== '') {
        const q = searchQuery.toLowerCase();
        const matchName = asset.name.toLowerCase().includes(q);
        const matchPath = asset.path.toLowerCase().includes(q);
        if (!matchName && !matchPath) return false;
      }

      return true;
    });
  }, [allAssets, activeCategoryId, activeSubCategoryId, genderFilter, searchQuery, hasSubCategories]);

  const selectedAsset = useMemo(() => {
    return allAssets.find((a) => a.id === selectedAssetId) || filteredAssets[0] || null;
  }, [allAssets, selectedAssetId, filteredAssets]);

  // ─── Actions Handler ──────────────────────────────────────────
  const handleAction = (asset: AssetItem) => {
    if (asset.type === 'prop') {
      onPlaceProp(asset);
      setSpawnNotification(`Đã đặt "${asset.name}" vào Scene!`);
      setTimeout(() => setSpawnNotification(null), 2500);
    } else if (asset.type === 'map') {
      onSelectMap(asset.path || asset.id);
      setSpawnNotification(`Đang áp dụng Bản Đồ: ${asset.name}...`);
      setTimeout(() => setSpawnNotification(null), 2500);
    } else if (asset.type === 'character') {
      const targetActor = actorsList[0]?.id || 'actor_warrior';
      if (asset.category === '_lap_rap' && asset.assembly) {
        onSelectAvatar(targetActor, asset.path, asset.assembly);
      } else {
        onSelectAvatar(targetActor, asset.path);
      }
      setSpawnNotification(`Đã gán "${asset.name}" cho nhân vật!`);
      setTimeout(() => setSpawnNotification(null), 2500);
    } else if (asset.type === 'animation' && onPlayAnimationPreview) {
      onPlayAnimationPreview(asset.name || asset.id);
      setSpawnNotification(`Đang phát hoạt ảnh: ${asset.name}`);
      setTimeout(() => setSpawnNotification(null), 2500);
    } else if (asset.type === 'skybox') {
      setSpawnNotification(`Đã chọn Skybox: ${asset.name}`);
      setTimeout(() => setSpawnNotification(null), 2500);
    }
  };

  const handleDeleteCustomPreset = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      const savedRaw = localStorage.getItem('custom_character_presets');
      if (savedRaw) {
        const list = JSON.parse(savedRaw);
        const updated = list.filter((p: any) => p.id !== id && `assembled_preset_${p.id}` !== id && p.name !== id);
        localStorage.setItem('custom_character_presets', JSON.stringify(updated));
        window.dispatchEvent(new Event('flow_assets_updated'));
      }
    } catch {}
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files || e.target.files.length === 0) return;
    const files = Array.from(e.target.files);

    // Check if user uploaded JSON character profiles
    const jsonFiles = files.filter((f) => f.name.toLowerCase().endsWith('.json'));
    const otherFiles = files.filter((f) => !f.name.toLowerCase().endsWith('.json'));

    if (jsonFiles.length > 0) {
      let importedCount = 0;
      for (const jf of jsonFiles) {
        try {
          const text = await jf.text();
          const profile = JSON.parse(text);
          const ass = profile.assembly || profile.character_assembly || {};
          const body = profile.than_co_ban || profile.base_body || ass.than_co_ban || ass.base_body || profile.model || '';
          const preset: any = {
            id: profile.id || `preset_${Date.now()}_${Math.random().toString(36).substring(2, 6)}`,
            name: profile.name || jf.name.replace('.json', ''),
            body,
            assembly: { ...ass, than_co_ban: body, base_body: body },
            gender: profile.gender || (body.includes('nu') || body.includes('female') || body.includes('manekina') ? 'female' : 'male'),
            preview: profile.preview_image || undefined,
          };

          const savedRaw = localStorage.getItem('custom_character_presets');
          const savedList = savedRaw ? JSON.parse(savedRaw) : [];
          const updated = [preset, ...savedList.filter((p: any) => p.name !== preset.name && p.id !== preset.id)];
          localStorage.setItem('custom_character_presets', JSON.stringify(updated));
          importedCount++;
        } catch (err) {
          console.warn('Lỗi đọc file JSON nhân vật:', err);
        }
      }

      if (importedCount > 0) {
        window.dispatchEvent(new Event('flow_assets_updated'));
        setSpawnNotification(`✅ Đã nạp ${importedCount} nhân vật vào tab "Nhân Vật Đã Ráp"!`);
        setActiveCategoryId('_lap_rap');
        setTimeout(() => setSpawnNotification(null), 4000);
      }
    }

    if (otherFiles.length > 0 && onImportCustomFiles) {
      onImportCustomFiles(otherFiles);
    }
    e.target.value = '';
  };

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        width: '100%',
        background: '#090d16',
        color: '#f8fafc',
        fontFamily: 'Inter, system-ui, sans-serif',
        overflow: 'hidden',
      }}
    >
      {/* ─── Top Header Toolbar ───────────────────────────────── */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '8px 14px',
          background: 'rgba(15, 23, 42, 0.95)',
          borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
          gap: 12,
          flexShrink: 0,
        }}
      >
        {/* Left Title & Total Count */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <Layers size={18} color="#38bdf8" />
          <span style={{ fontSize: 13, fontWeight: 700, color: '#f8fafc' }}>
            Kho Tài Nguyên Dự Án (Project Assets)
          </span>
          <span
            style={{
              fontSize: 10,
              fontWeight: 700,
              color: '#38bdf8',
              background: 'rgba(56, 189, 248, 0.12)',
              padding: '2px 8px',
              borderRadius: 12,
              border: '1px solid rgba(56, 189, 248, 0.25)',
            }}
          >
            {allAssets.length} Tổng tài nguyên
          </span>
        </div>

        {/* Center Search Bar */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            background: 'rgba(0, 0, 0, 0.4)',
            border: '1px solid rgba(255, 255, 255, 0.1)',
            borderRadius: 6,
            padding: '4px 10px',
            width: 260,
            gap: 6,
          }}
        >
          <Search size={13} color="#94a3b8" />
          <input
            type="text"
            placeholder="Tìm theo tên Tiếng Việt, mã tài nguyên..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{
              background: 'none',
              border: 'none',
              color: '#f8fafc',
              fontSize: 11,
              outline: 'none',
              width: '100%',
            }}
          />
          {searchQuery && (
            <button
              onClick={() => setSearchQuery('')}
              style={{ background: 'none', border: 'none', color: '#94a3b8', cursor: 'pointer', padding: 0 }}
            >
              ✕
            </button>
          )}
        </div>

        {/* Right Action Buttons */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {spawnNotification && (
            <div
              style={{
                fontSize: 11,
                fontWeight: 600,
                color: '#38bdf8',
                background: 'rgba(56, 189, 248, 0.15)',
                border: '1px solid rgba(56, 189, 248, 0.3)',
                borderRadius: 4,
                padding: '3px 8px',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
              }}
            >
              <Check size={12} /> {spawnNotification}
            </div>
          )}

          <input
            type="file"
            ref={fileInputRef}
            style={{ display: 'none' }}
            multiple
            accept=".glb,.gltf,.vrm,.mp3,.json,.png,.jpg"
            onChange={handleFileUpload}
          />

          {activeCategoryId === '_lap_rap' && (
            <button
              onClick={() => fileInputRef.current?.click()}
              title="Nhập file JSON nhân vật từ máy tính"
              style={{
                background: 'linear-gradient(135deg, #0284c7, #0369a1)',
                border: '1px solid #38bdf8',
                color: '#fff',
                borderRadius: 6,
                padding: '4px 10px',
                fontSize: 11,
                fontWeight: 700,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 5,
                boxShadow: '0 2px 6px rgba(2, 132, 199, 0.3)',
              }}
            >
              <Upload size={12} />
              Nhập JSON Nhân Vật
            </button>
          )}

          <button
            onClick={loadManifest}
            title="Quét lại toàn bộ tài nguyên từ asset_manifest.json"
            style={{
              background: 'rgba(56, 189, 248, 0.12)',
              border: '1px solid rgba(56, 189, 248, 0.3)',
              color: '#38bdf8',
              borderRadius: 6,
              padding: '4px 10px',
              fontSize: 11,
              fontWeight: 600,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 5,
            }}
          >
            <RefreshCw size={12} style={{ animation: isLoadingManifest ? 'spin 1s linear infinite' : 'none' }} />
            Quét lại
          </button>

          <button
            onClick={() => fileInputRef.current?.click()}
            title="Import file 3D hoặc audio từ máy tính"
            style={{
              background: 'rgba(255, 255, 255, 0.05)',
              border: '1px solid rgba(255, 255, 255, 0.12)',
              color: '#cbd5e1',
              borderRadius: 6,
              padding: '4px 10px',
              fontSize: 11,
              fontWeight: 600,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 5,
            }}
          >
            <Upload size={12} />
            Import
          </button>

          {onToggleMaximize && (
            <button
              onClick={onToggleMaximize}
              title={isMaximized ? 'Thu nhỏ' : 'Mở rộng toàn màn hình'}
              style={{
                background: 'rgba(255, 255, 255, 0.05)',
                border: '1px solid rgba(255, 255, 255, 0.12)',
                color: '#cbd5e1',
                borderRadius: 6,
                padding: '4px 8px',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
              }}
            >
              {isMaximized ? <Minimize2 size={13} /> : <Maximize2 size={13} />}
            </button>
          )}
        </div>
      </div>

      {/* ─── Main Body: Left Vertical Tabs + Center Grid + Right Inspector ── */}
      <div style={{ display: 'flex', flex: 1, overflow: 'hidden' }}>
        {/* ─── 1. Left Sidebar: Section Tabs + Multi-Column Wrap Grid ─ */}
        <div
          style={{
            maxHeight: '100%',
            height: '100%',
            background: 'rgba(15, 23, 42, 0.98)',
            borderRight: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            flexDirection: 'column',
            flexShrink: 0,
            overflow: 'hidden',
          }}
        >
          {/* Top 3 Main Section Selector Tabs */}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
              background: 'rgba(0, 0, 0, 0.3)',
              padding: '4px',
              gap: 4,
            }}
          >
            {[
              { id: 'character', label: 'Nhân Vật', icon: '👤' },
              { id: 'world', label: 'Bối Cảnh', icon: '🗺️' },
              { id: 'media', label: 'Hiệu Ứng & Âm Thanh', icon: '✨' },
            ].map((sec) => {
              const isSecActive = mainSection === sec.id;
              return (
                <button
                  key={sec.id}
                  onClick={() => handleSwitchSection(sec.id as any)}
                  style={{
                    flex: 1,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 4,
                    padding: '6px 8px',
                    borderRadius: 6,
                    border: 'none',
                    cursor: 'pointer',
                    fontSize: 10,
                    fontWeight: 700,
                    background: isSecActive ? '#38bdf8' : 'rgba(255, 255, 255, 0.04)',
                    color: isSecActive ? '#090d16' : '#94a3b8',
                    transition: 'all 0.15s ease',
                    whiteSpace: 'nowrap',
                  }}
                >
                  <span style={{ fontSize: 12 }}>{sec.icon}</span>
                  <span>{sec.label}</span>
                </button>
              );
            })}
          </div>

          {/* Multi-Column Overflow Grid of Category Tabs (Col 1 -> Col 2 -> Col 3) with Horizontal Scrolling */}
          <div
            onWheel={(e) => {
              if (e.deltaY !== 0) {
                e.currentTarget.scrollLeft += e.deltaY;
              }
            }}
            style={{
              flex: 1,
              display: 'flex',
              flexDirection: 'column',
              flexWrap: 'wrap',
              alignContent: 'flex-start',
              gap: 4,
              padding: '6px',
              overflowX: 'auto',
              overflowY: 'hidden',
              scrollBehavior: 'smooth',
            }}
          >
            {dynamicCategoryTabs.filter((cat) => cat.group === mainSection).map((cat) => {
              const isActive = activeCategoryId === cat.id;
              const count = categoryCounts[cat.id] || 0;

              return (
                <button
                  key={cat.id}
                  onClick={() => {
                    setActiveCategoryId(cat.id);
                    setActiveSubCategoryId('all');
                  }}
                  title={cat.label}
                  style={{
                    width: 116,
                    height: 42,
                    display: 'flex',
                    flexDirection: 'row',
                    alignItems: 'center',
                    gap: 6,
                    padding: '4px 6px',
                    borderRadius: 6,
                    border: 'none',
                    background: isActive ? 'rgba(56, 189, 248, 0.18)' : 'rgba(255, 255, 255, 0.03)',
                    color: isActive ? '#38bdf8' : '#94a3b8',
                    fontWeight: isActive ? 700 : 500,
                    fontSize: 10,
                    cursor: 'pointer',
                    textAlign: 'left',
                    transition: 'all 0.15s ease',
                    borderLeft: isActive ? '3px solid #38bdf8' : '3px solid transparent',
                    boxSizing: 'border-box',
                  }}
                >
                  <span style={{ fontSize: 16 }}>{cat.icon}</span>
                  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', overflow: 'hidden', flex: 1 }}>
                    <span
                      style={{
                        fontSize: 10,
                        fontWeight: 700,
                        whiteSpace: 'nowrap',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        width: '100%',
                      }}
                    >
                      {cat.label}
                    </span>
                    <span style={{ fontSize: 8, color: isActive ? '#38bdf8' : '#64748b', fontWeight: 600 }}>
                      {count} mục
                    </span>
                  </div>
                  {/* Badge */}
                  <span
                    style={{
                      fontSize: 8,
                      fontWeight: 700,
                      padding: '1px 4px',
                      borderRadius: 8,
                      background: count > 0 ? (isActive ? '#38bdf8' : 'rgba(148, 163, 184, 0.25)') : 'rgba(255,255,255,0.06)',
                      color: count > 0 ? (isActive ? '#090d16' : '#cbd5e1') : '#475569',
                    }}
                  >
                    {count}
                  </span>
                </button>
              );
            })}
          </div>
        </div>

        {/* ─── 2. Center Content: Sub-tabs Bar + Asset Grid ─────── */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          {/* Sub-tabs & Gender Filter Header Bar */}
          <div
            style={{
              padding: '6px 12px',
              background: 'rgba(0, 0, 0, 0.25)',
              borderBottom: '1px solid rgba(255, 255, 255, 0.06)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: 8,
              flexShrink: 0,
            }}
          >
            {/* Sub-categories Horizontal Pills */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 4, overflowX: 'auto' }}>
              {hasSubCategories && (
                <>
                  <button
                    onClick={() => setActiveSubCategoryId('all')}
                    style={{
                      padding: '3px 8px',
                      borderRadius: 4,
                      fontSize: 10,
                      fontWeight: 600,
                      cursor: 'pointer',
                      border: 'none',
                      background: activeSubCategoryId === 'all' ? '#38bdf8' : 'rgba(255,255,255,0.06)',
                      color: activeSubCategoryId === 'all' ? '#090d16' : '#cbd5e1',
                    }}
                  >
                    Tất Cả ({filteredAssets.length})
                  </button>
                  {subCategories.map((sub) => {
                    const isActiveSub = activeSubCategoryId === sub.id;
                    return (
                      <button
                        key={sub.id}
                        onClick={() => setActiveSubCategoryId(sub.id)}
                        style={{
                          padding: '3px 8px',
                          borderRadius: 4,
                          fontSize: 10,
                          fontWeight: 600,
                          cursor: 'pointer',
                          border: 'none',
                          background: isActiveSub ? '#38bdf8' : 'rgba(255,255,255,0.06)',
                          color: isActiveSub ? '#090d16' : '#cbd5e1',
                          display: 'flex',
                          alignItems: 'center',
                          gap: 4,
                        }}
                      >
                        {sub.icon && <span>{sub.icon}</span>}
                        <span>{sub.label}</span>
                      </button>
                    );
                  })}
                </>
              )}
            </div>

            {/* Gender Filter Toggle for Character items */}
            {currentCategoryTab.group === 'character' && (
              <div style={{ display: 'flex', alignItems: 'center', gap: 3 }}>
                <span style={{ fontSize: 10, color: '#94a3b8' }}>Giới tính:</span>
                {(['all', 'male', 'female'] as const).map((g) => (
                  <button
                    key={g}
                    onClick={() => setGenderFilter(g)}
                    style={{
                      padding: '2px 7px',
                      borderRadius: 4,
                      fontSize: 9,
                      fontWeight: 700,
                      cursor: 'pointer',
                      border: 'none',
                      background: genderFilter === g ? '#38bdf8' : 'rgba(255,255,255,0.06)',
                      color: genderFilter === g ? '#090d16' : '#cbd5e1',
                    }}
                  >
                    {g === 'all' ? 'Tất cả' : g === 'male' ? '👦 Nam' : '👧 Nữ'}
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Asset Grid Cards */}
          <div
            style={{
              flex: 1,
              overflowY: 'auto',
              padding: 12,
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(130px, 1fr))',
              gap: 10,
              alignContent: 'start',
            }}
          >
            {filteredAssets.length === 0 ? (
              <div
                style={{
                  gridColumn: '1 / -1',
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  justifyContent: 'center',
                  padding: 40,
                  gap: 12,
                  color: '#94a3b8',
                  background: 'rgba(255, 255, 255, 0.02)',
                  borderRadius: 10,
                  border: '1px dashed rgba(255, 255, 255, 0.1)',
                  margin: '20px 10px',
                }}
              >
                <div style={{ fontSize: 36 }}>✨</div>
                <span style={{ fontSize: 13, fontWeight: 700, color: '#f1f5f9' }}>
                  {activeCategoryId === '_lap_rap'
                    ? 'Chưa có nhân vật nào trong mục "Nhân Vật Đã Ráp"'
                    : 'Chưa có tài nguyên nào trong mục này'}
                </span>
                <span style={{ fontSize: 11, color: '#64748b', maxWidth: 420, textAlign: 'center' }}>
                  {activeCategoryId === '_lap_rap'
                    ? 'Bạn có thể vào tab "Xưởng Nhân Vật" phối đồ rồi bấm "Lưu Mẫu", hoặc bấm nút bên dưới để nạp file .JSON nhân vật có sẵn từ máy tính!'
                    : 'Thả file vào thư mục assets/ và chạy _scan_assets.bat'}
                </span>
                {activeCategoryId === '_lap_rap' && (
                  <button
                    onClick={() => fileInputRef.current?.click()}
                    style={{
                      marginTop: 6,
                      background: 'linear-gradient(135deg, #0284c7, #0369a1)',
                      border: '1px solid #38bdf8',
                      color: '#fff',
                      borderRadius: 6,
                      padding: '8px 16px',
                      fontSize: 12,
                      fontWeight: 700,
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 6,
                      boxShadow: '0 2px 8px rgba(2, 132, 199, 0.4)',
                    }}
                  >
                    <Upload size={14} /> Chọn File .JSON Nhân Vật Từ Máy Tính
                  </button>
                )}
              </div>
            ) : (
              filteredAssets.map((asset) => {
                const isSelected = selectedAsset?.id === asset.id;

                return (
                  <div
                    key={asset.id}
                    onClick={() => setSelectedAssetId(asset.id)}
                    onDoubleClick={() => handleAction(asset)}
                    style={{
                      background: isSelected ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255, 255, 255, 0.03)',
                      border: `1px solid ${isSelected ? '#38bdf8' : 'rgba(255, 255, 255, 0.08)'}`,
                      borderRadius: 8,
                      padding: 6,
                      display: 'flex',
                      flexDirection: 'column',
                      gap: 6,
                      cursor: 'pointer',
                      transition: 'all 0.15s ease',
                      position: 'relative',
                    }}
                  >
                    {/* Delete button for assembled characters */}
                    {asset.category === '_lap_rap' && (
                      <button
                        onClick={(e) => handleDeleteCustomPreset(asset.id, e)}
                        title="Xóa mẫu nhân vật này khỏi danh sách"
                        style={{
                          position: 'absolute',
                          top: 8,
                          right: 8,
                          zIndex: 5,
                          background: 'rgba(0, 0, 0, 0.75)',
                          border: '1px solid rgba(239, 68, 68, 0.5)',
                          color: '#ef4444',
                          borderRadius: 4,
                          padding: '2px 5px',
                          fontSize: 10,
                          fontWeight: 700,
                          cursor: 'pointer',
                        }}
                      >
                        ✕
                      </button>
                    )}
                    {/* Live 3D Preview / Companion Photo */}
                    <Live3DThumbnail
                      assetPath={asset.path}
                      previewUrl={asset.previewUrl}
                      altText={asset.name}
                      fallbackIcon={currentCategoryTab.icon}
                      format={asset.format}
                      height={90}
                    />

                    {/* Asset Name & Meta Info */}
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
                      <span
                        title={asset.name}
                        style={{
                          fontSize: 11,
                          fontWeight: 600,
                          color: isSelected ? '#38bdf8' : '#e2e8f0',
                          whiteSpace: 'nowrap',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                        }}
                      >
                        {asset.name}
                      </span>
                      <span style={{ fontSize: 9, color: '#64748b' }}>
                        {asset.sizeMB ? `${asset.sizeMB} MB` : asset.format}
                        {asset.gender && asset.gender !== 'unisex' && ` • ${asset.gender === 'male' ? 'Nam' : 'Nữ'}`}
                      </span>
                    </div>

                    {/* Quick Action Button */}
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        handleAction(asset);
                      }}
                      style={{
                        width: '100%',
                        padding: '4px 0',
                        borderRadius: 4,
                        fontSize: 10,
                        fontWeight: 700,
                        cursor: 'pointer',
                        border: 'none',
                        background: isSelected ? '#38bdf8' : 'rgba(255, 255, 255, 0.08)',
                        color: isSelected ? '#090d16' : '#cbd5e1',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        gap: 4,
                        transition: 'all 0.15s ease',
                      }}
                    >
                      {asset.type === 'prop' ? (
                        <>
                          <Plus size={11} /> Đặt vào
                        </>
                      ) : asset.type === 'map' ? (
                        <>
                          <MapIcon size={11} /> Tải Map
                        </>
                      ) : asset.type === 'character' ? (
                        <>
                          <User size={11} /> Gán Avatar
                        </>
                      ) : (
                        <>
                          <Play size={11} /> Phát
                        </>
                      )}
                    </button>
                  </div>
                );
              })
            )}
          </div>
        </div>

        {/* ─── 3. Right Quick Inspector Preview ─────────────────── */}
        {selectedAsset && (
          <div
            style={{
              width: 220,
              background: 'rgba(15, 23, 42, 0.98)',
              borderLeft: '1px solid rgba(255, 255, 255, 0.08)',
              display: 'flex',
              flexDirection: 'column',
              padding: 10,
              gap: 10,
              flexShrink: 0,
              overflowY: 'auto',
            }}
          >
            <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', borderBottom: '1px solid rgba(255,255,255,0.08)', paddingBottom: 4 }}>
              THÔNG TIN CHI TIẾT
            </div>

            {/* Large 3D Preview Box */}
            <Live3DThumbnail
              assetPath={selectedAsset.path}
              previewUrl={selectedAsset.previewUrl}
              altText={selectedAsset.name}
              fallbackIcon={currentCategoryTab.icon}
              format={selectedAsset.format}
              height={140}
            />

            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#f8fafc' }}>{selectedAsset.name}</span>
              <span style={{ fontSize: 10, color: '#38bdf8', fontWeight: 600 }}>
                {selectedAsset.format} • {(selectedAsset.category || selectedAsset.type).toUpperCase()}
                {selectedAsset.gender && ` • ${selectedAsset.gender === 'male' ? 'NAM' : selectedAsset.gender === 'female' ? 'NỮ' : 'CHUNG'}`}
              </span>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 10, color: '#94a3b8' }}>
              <div>
                <span style={{ color: '#64748b' }}>Đường dẫn: </span>
                <span style={{ color: '#cbd5e1', wordBreak: 'break-all' }}>{selectedAsset.path}</span>
              </div>
              {selectedAsset.sizeMB && (
                <div>
                  <span style={{ color: '#64748b' }}>Dung lượng: </span>
                  <span style={{ color: '#cbd5e1' }}>{selectedAsset.sizeMB} MB</span>
                </div>
              )}
            </div>

            {/* Primary Action Button */}
            <button
              onClick={() => handleAction(selectedAsset)}
              style={{
                marginTop: 'auto',
                width: '100%',
                padding: '8px 0',
                borderRadius: 6,
                background: '#38bdf8',
                color: '#090d16',
                fontWeight: 700,
                fontSize: 11,
                border: 'none',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 5,
              }}
            >
              {selectedAsset.type === 'prop' && <><Plus size={13} /> Chèn vào Scene</>}
              {selectedAsset.type === 'map' && <><MapIcon size={13} /> Kích Hoạt Bản Đồ</>}
              {selectedAsset.type === 'character' && <><User size={13} /> Gán Cho Nhân Vật</>}
              {selectedAsset.type === 'animation' && <><Play size={13} /> Chạy Hoạt Ảnh</>}
              {selectedAsset.type === 'skybox' && <><Sparkles size={13} /> Đổi Bầu Trời</>}
              {selectedAsset.type === 'audio' && <><Music size={13} /> Nghe Thử</>}
              {selectedAsset.type === 'vfx' && <><Sparkles size={13} /> Kích Hoạt VFX</>}
            </button>
          </div>
        )}
      </div>
    </div>
  );
};
