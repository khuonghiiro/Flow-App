import React, { useState, useMemo } from 'react';
import { 
  Folder, FolderOpen, Box, User, Map, Music, Sparkles, Film, 
  Search, Plus, Check, ChevronRight, Upload, Play, Eye, Maximize2, Minimize2,
  Layers, RefreshCw
} from 'lucide-react';
import { PlacedProp } from '../types/map_preset';

export interface AssetItem {
  id: string;
  name: string;
  path: string;
  folder: string;
  type: 'prop' | 'character' | 'map' | 'audio' | 'vfx' | 'animation';
  format: string;
  size?: string;
  tags?: string[];
  description?: string;
  previewColor?: string;
  // Specific data
  propData?: Partial<PlacedProp>;
  mapId?: string;
  vrmUrl?: string;
  audioUrl?: string;
  animName?: string;
}

interface AssetBrowserPanelProps {
  onPlaceProp: (prop: AssetItem) => void;
  onSelectMap: (mapId: string) => void;
  onSelectAvatar: (actorId: string, vrmUrl: string) => void;
  onPlayAnimationPreview?: (animName: string) => void;
  onImportCustomFiles?: (files: FileList | File[]) => void;
  actorsList?: { id: string; name: string }[];
  isMaximized?: boolean;
  onToggleMaximize?: () => void;
}

const DEFAULT_ASSET_DATABASE: AssetItem[] = [
  // --- PROPS: NATURE ---
  {
    id: 'prop_tree_sakura',
    name: 'Cây Hoa Anh Đào',
    path: 'Assets/Props/Nature/tree_sakura.glb',
    folder: 'Assets/Props/Nature',
    type: 'prop',
    format: 'GLB',
    size: '1.4 MB',
    tags: ['cây', 'hoa anh đào', 'thiên nhiên', 'leo trèo'],
    description: 'Cây anh đào tán rộng nở hoa rực rỡ, có socket leo trèo',
    previewColor: '#ec4899',
    propData: {
      type: 'nature',
      scale: 1.2,
      is_obstacle: true,
      obstacle_radius: 0.8,
      smart_socket: {
        socket_type: 'climb',
        entry_offset: [0, 0, 1.2],
        target_offset: [0, 2.2, 0],
        target_rotation_y: 0
      }
    }
  },
  {
    id: 'prop_tree_oak',
    name: 'Cây Sồi Cổ Thụ',
    path: 'Assets/Props/Nature/tree_oak.glb',
    folder: 'Assets/Props/Nature',
    type: 'prop',
    format: 'Procedural',
    size: '12 KB',
    tags: ['cây', 'sồi', 'bóng mát'],
    description: 'Cây xanh tán tròn thân gỗ sồi tự nhiên',
    previewColor: '#16a34a',
    propData: { type: 'nature', scale: 1.0, is_obstacle: true, obstacle_radius: 0.6 }
  },
  {
    id: 'prop_farm_plot',
    name: 'Vườn Nông Trại & Thảo Dược',
    path: 'Assets/Props/Nature/farm_plot.glb',
    folder: 'Assets/Props/Nature',
    type: 'prop',
    format: 'Procedural',
    size: '18 KB',
    tags: ['nông trại', 'vườn', 'cây trồng'],
    description: 'Luống đất cày xới trồng rau củ xanh tươi',
    previewColor: '#854d0e',
    propData: { type: 'nature', scale: 1.0, is_obstacle: false }
  },
  {
    id: 'prop_duck',
    name: 'Chú Vịt Đồng Quê',
    path: 'Assets/Props/Creatures/duck_prop.glb',
    folder: 'Assets/Props/Creatures',
    type: 'prop',
    format: 'GLB',
    size: '120 KB',
    tags: ['vịt', 'động vật', 'vui nhộn'],
    description: 'Chú vịt vàng bơi lội sinh động',
    previewColor: '#eab308',
    propData: { type: 'animal', scale: 1.0, is_obstacle: false }
  },

  // --- PROPS: FURNITURE ---
  {
    id: 'prop_chair_wood',
    name: 'Ghế Gỗ Nghỉ Ngơi',
    path: 'Assets/Props/Furniture/chair_wooden.glb',
    folder: 'Assets/Props/Furniture',
    type: 'prop',
    format: 'Procedural',
    size: '8 KB',
    tags: ['ghế', 'nội thất', 'ngồi'],
    description: 'Ghế tựa bằng gỗ có socket ngồi nghỉ',
    previewColor: '#d97706',
    propData: {
      type: 'furniture',
      scale: 1.0,
      is_obstacle: true,
      obstacle_radius: 0.5,
      smart_socket: {
        socket_type: 'sit',
        entry_offset: [0, 0, 0.8],
        target_offset: [0, 0.45, 0],
        target_rotation_y: 0
      }
    }
  },
  {
    id: 'prop_lantern_stand',
    name: 'Trụ Đèn Lồng Cổ Trang',
    path: 'Assets/Props/Furniture/lantern_stand.glb',
    folder: 'Assets/Props/Furniture',
    type: 'prop',
    format: 'GLB',
    size: '9.8 MB',
    tags: ['đèn', 'ánh sáng', 'lồng đèn'],
    description: 'Trụ đèn lồng phát ánh sáng vàng ấm cúng ban đêm',
    previewColor: '#f97316',
    propData: { type: 'furniture', scale: 1.0, is_obstacle: true, obstacle_radius: 0.4 }
  },
  {
    id: 'prop_stone_bench',
    name: 'Ghế Đá Ven Hồ',
    path: 'Assets/Props/Furniture/bench_stone.glb',
    folder: 'Assets/Props/Furniture',
    type: 'prop',
    format: 'Procedural',
    size: '14 KB',
    tags: ['ghế đá', 'ven hồ', 'ngồi'],
    description: 'Ghế dài phiến đá tự nhiên',
    previewColor: '#64748b',
    propData: {
      type: 'furniture',
      scale: 1.1,
      is_obstacle: true,
      obstacle_radius: 0.6,
      smart_socket: {
        socket_type: 'sit',
        entry_offset: [0, 0, 0.9],
        target_offset: [0, 0.5, 0],
        target_rotation_y: 0
      }
    }
  },

  // --- MAPS & PRESETS ---
  {
    id: 'map_farming_village',
    name: 'Làng Quê Yên Bình',
    path: 'Assets/Maps/farming_village',
    folder: 'Assets/Maps',
    type: 'map',
    format: 'Map Preset',
    size: 'Standard',
    tags: ['làng quê', 'đồng cỏ', 'yên bình'],
    description: 'Bản đồ đồng quê cỏ xanh với cây cối, luống cày và vịt đồng',
    previewColor: '#22c55e',
    mapId: 'farming_village'
  },
  {
    id: 'map_sakura_lake',
    name: 'Làng Hoa Anh Đào Ven Hồ',
    path: 'Assets/Maps/sakura_lake_village.json',
    folder: 'Assets/Maps',
    type: 'map',
    format: 'Preset JSON',
    size: '8 KB',
    tags: ['anh đào', 'ven hồ', 'hoàng hôn'],
    description: 'Cảnh sắc hồ nước lãng mạn phủ cánh hoa anh đào',
    previewColor: '#f43f5e',
    mapId: 'sakura_lake_village'
  },
  {
    id: 'map_cathedral',
    name: 'Thánh Đường Cổ Kính (3D)',
    path: 'Assets/Maps/cathedral.glb',
    folder: 'Assets/Maps',
    type: 'map',
    format: 'GLB 3D',
    size: '108 MB',
    tags: ['thánh đường', 'cổ kính', 'kiến trúc gothic', '3d'],
    description: 'Kiến trúc thánh đường châu Âu cổ nguy nga tráng lệ',
    previewColor: '#8b5cf6',
    mapId: 'cathedral.glb'
  },
  {
    id: 'map_pirate_island',
    name: 'Đảo Hải Tặc Phiêu Lưu (3D)',
    path: 'Assets/Maps/game_pirate_adventure_map.glb',
    folder: 'Assets/Maps',
    type: 'map',
    format: 'GLB 3D',
    size: '7.8 MB',
    tags: ['hải tặc', 'biển', 'đảo', 'phiêu lưu'],
    description: 'Quần đảo nhiệt đới với boong tàu và pháo đài biển',
    previewColor: '#06b6d4',
    mapId: 'game_pirate_adventure_map.glb'
  },

  // --- CHARACTERS ---
  {
    id: 'char_warrior',
    name: 'Chiến Binh Kiếm Khách',
    path: 'Assets/Characters/sample_avatar.vrm',
    folder: 'Assets/Characters',
    type: 'character',
    format: 'VRM 1.0',
    size: '10.7 MB',
    tags: ['chiến binh', 'kiếm khách', 'nam', 'vrm'],
    description: 'Avatar chiến binh giáp da tóc bạc đầy dũng mãnh',
    previewColor: '#38bdf8',
    vrmUrl: '/assets/characters/sample_avatar.vrm'
  },
  {
    id: 'char_villager',
    name: 'Nữ Thần Dân Làng',
    path: 'Assets/Characters/female_villager.vrm',
    folder: 'Assets/Characters',
    type: 'character',
    format: 'VRM',
    size: '11.2 MB',
    tags: ['dân làng', 'nữ', 'trang phục truyền thống'],
    description: 'Avatar thiếu nữ thôn quê hiền hòa đáng yêu',
    previewColor: '#a855f7',
    vrmUrl: '/assets/characters/sample_avatar.vrm'
  },

  // --- ANIMATIONS ---
  {
    id: 'anim_heavy_slash',
    name: 'Combo Trảm Kích (Heavy Slash)',
    path: 'Assets/Animations/heavy_slash_combo',
    folder: 'Assets/Animations',
    type: 'animation',
    format: 'Motion',
    tags: ['chém kiếm', 'combat', 'tấn công'],
    description: 'Động tác chém kiếm xoay vòng 3 nhát liên hoàn tốc biến',
    previewColor: '#ef4444',
    animName: 'heavy_slash_combo'
  },
  {
    id: 'anim_talk',
    name: 'Cử Chỉ Thoại (Talk Gesture)',
    path: 'Assets/Animations/talk_gesture',
    folder: 'Assets/Animations',
    type: 'animation',
    format: 'Motion',
    tags: ['nói chuyện', 'hội thoại', 'tay'],
    description: 'Cử động vẫy tay giao tiếp tự nhiên',
    previewColor: '#10b981',
    animName: 'talk_gesture'
  },
  {
    id: 'anim_block',
    name: 'Đỡ Đòn Phòng Thủ (Block Defend)',
    path: 'Assets/Animations/block_defend',
    folder: 'Assets/Animations',
    type: 'animation',
    format: 'Motion',
    tags: ['đỡ', 'thủ', 'khiên'],
    description: 'Động tác giơ khiên/kiếm chống đỡ chấn động',
    previewColor: '#eab308',
    animName: 'block_defend'
  },
  {
    id: 'anim_sit',
    name: 'Ngồi Nghỉ (Sit Rest)',
    path: 'Assets/Animations/sit',
    folder: 'Assets/Animations',
    type: 'animation',
    format: 'Motion',
    tags: ['ngồi', 'ghế', 'thư giãn'],
    description: 'Tư thế ngồi trên ghế hoặc bậc thềm',
    previewColor: '#6366f1',
    animName: 'sit'
  },

  // --- VFX & AUDIO ---
  {
    id: 'vfx_slash_flame',
    name: 'Vệt Chém Lửa Đỏ (Fire Slash)',
    path: 'Assets/VFX/vfx_heavy_slash',
    folder: 'Assets/VFX',
    type: 'vfx',
    format: 'Particle Ring',
    tags: ['vfx', 'lửa', 'vệt chém'],
    description: 'Vòng sáng rực đỏ quét cung 160 độ khi chém kiếm',
    previewColor: '#dc2626'
  },
  {
    id: 'audio_slash_sfx',
    name: 'SFX Kiếm Chém Xé Gió',
    path: 'Assets/Audio/SFX/heavy_slash.mp3',
    folder: 'Assets/Audio',
    type: 'audio',
    format: 'MP3',
    size: '45 KB',
    tags: ['sfx', 'kiếm', 'va chạm'],
    description: 'Âm thanh vũ khí vung xé gió và chém trúng mục tiêu',
    previewColor: '#38bdf8'
  }
];

const FOLDER_TREE = [
  {
    name: 'Assets',
    path: 'Assets',
    children: [
      {
        name: 'Props',
        path: 'Assets/Props',
        children: [
          { name: 'Nature', path: 'Assets/Props/Nature' },
          { name: 'Furniture', path: 'Assets/Props/Furniture' },
          { name: 'Creatures', path: 'Assets/Props/Creatures' },
        ]
      },
      { name: 'Maps & Presets', path: 'Assets/Maps' },
      { name: 'Characters', path: 'Assets/Characters' },
      { name: 'Animations', path: 'Assets/Animations' },
      { name: 'VFX', path: 'Assets/VFX' },
      { name: 'Audio', path: 'Assets/Audio' },
    ]
  }
];

export const AssetBrowserPanel: React.FC<AssetBrowserPanelProps> = ({
  onPlaceProp,
  onSelectMap,
  onSelectAvatar,
  onPlayAnimationPreview,
  onImportCustomFiles,
  actorsList = [{ id: 'actor_warrior', name: 'Chiến Binh (Warrior)' }],
  isMaximized = false,
  onToggleMaximize
}) => {
  const [selectedFolder, setSelectedFolder] = useState<string>('Assets');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [filterType, setFilterType] = useState<string>('all');
  const [selectedAssetId, setSelectedAssetId] = useState<string | null>(null);
  const [spawnNotification, setSpawnNotification] = useState<string | null>(null);
  const fileInputRef = React.useRef<HTMLInputElement>(null);

  // Filtered Assets
  const filteredAssets = useMemo(() => {
    return DEFAULT_ASSET_DATABASE.filter((asset) => {
      // Folder filter (if not Assets root, match prefix)
      if (selectedFolder !== 'Assets' && !asset.folder.startsWith(selectedFolder)) {
        return false;
      }
      // Type filter
      if (filterType !== 'all' && asset.type !== filterType) {
        return false;
      }
      // Search query
      if (searchQuery.trim() !== '') {
        const q = searchQuery.toLowerCase();
        const matchName = asset.name.toLowerCase().includes(q);
        const matchPath = asset.path.toLowerCase().includes(q);
        const matchTag = asset.tags?.some((t) => t.toLowerCase().includes(q));
        if (!matchName && !matchPath && !matchTag) return false;
      }
      return true;
    });
  }, [selectedFolder, filterType, searchQuery]);

  const selectedAsset = useMemo(() => {
    return DEFAULT_ASSET_DATABASE.find((a) => a.id === selectedAssetId) || null;
  }, [selectedAssetId]);

  const handleAction = (asset: AssetItem) => {
    if (asset.type === 'prop') {
      onPlaceProp(asset);
      setSpawnNotification(`Đã đặt "${asset.name}" vào Scene!`);
      setTimeout(() => setSpawnNotification(null), 2500);
    } else if (asset.type === 'map' && asset.mapId) {
      onSelectMap(asset.mapId);
      setSpawnNotification(`Đang tải Map: ${asset.name}...`);
      setTimeout(() => setSpawnNotification(null), 2500);
    } else if (asset.type === 'character' && asset.vrmUrl) {
      const targetActor = actorsList[0]?.id || 'actor_warrior';
      onSelectAvatar(targetActor, asset.vrmUrl);
      setSpawnNotification(`Đã gán Avatar "${asset.name}"!`);
      setTimeout(() => setSpawnNotification(null), 2500);
    } else if (asset.type === 'animation' && asset.animName && onPlayAnimationPreview) {
      onPlayAnimationPreview(asset.animName);
      setSpawnNotification(`Đang phát Animation: ${asset.name}`);
      setTimeout(() => setSpawnNotification(null), 2500);
    }
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0 && onImportCustomFiles) {
      onImportCustomFiles(e.target.files);
    }
  };

  // Render Folder Tree item recursively
  const renderTreeItem = (node: any, level: number = 0) => {
    const isSelected = selectedFolder === node.path;
    const hasChildren = node.children && node.children.length > 0;

    return (
      <div key={node.path} style={{ marginLeft: level * 10 }}>
        <div
          className={`unity-folder-item ${isSelected ? 'active' : ''}`}
          onClick={() => setSelectedFolder(node.path)}
        >
          {isSelected ? <FolderOpen size={13} color="#38bdf8" /> : <Folder size={13} color="#94a3b8" />}
          <span className="folder-name">{node.name}</span>
        </div>
        {hasChildren && (
          <div>
            {node.children.map((child: any) => renderTreeItem(child, level + 1))}
          </div>
        )}
      </div>
    );
  };

  return (
    <div className={`unity-asset-browser ${isMaximized ? 'maximized' : ''}`}>
      {/* Top Toolbar */}
      <div className="unity-browser-toolbar">
        {/* Left: Breadcrumbs */}
        <div className="unity-breadcrumbs">
          <Layers size={13} color="#38bdf8" />
          {selectedFolder.split('/').map((crumb, idx, arr) => (
            <React.Fragment key={idx}>
              <span
                className={`crumb ${idx === arr.length - 1 ? 'active' : ''}`}
                onClick={() => setSelectedFolder(arr.slice(0, idx + 1).join('/'))}
              >
                {crumb}
              </span>
              {idx < arr.length - 1 && <ChevronRight size={10} color="#64748b" />}
            </React.Fragment>
          ))}
        </div>

        {/* Center: Search & Filter Tabs */}
        <div className="unity-search-filter">
          <div className="unity-search-box">
            <Search size={12} color="#64748b" />
            <input
              type="text"
              placeholder="Tìm props, maps, avatars, vfx..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>

          <div className="unity-type-filters">
            {[
              { id: 'all', label: 'Tất cả' },
              { id: 'prop', label: 'Props (3D)' },
              { id: 'map', label: 'Maps' },
              { id: 'character', label: 'Avatars' },
              { id: 'animation', label: 'Động tác' },
              { id: 'vfx', label: 'VFX' },
            ].map((f) => (
              <button
                key={f.id}
                className={`type-pill ${filterType === f.id ? 'active' : ''}`}
                onClick={() => setFilterType(f.id)}
              >
                {f.label}
              </button>
            ))}
          </div>
        </div>

        {/* Right: Actions */}
        <div className="unity-toolbar-actions">
          {spawnNotification && (
            <div className="unity-notification">
              <Check size={12} /> {spawnNotification}
            </div>
          )}

          <input
            type="file"
            ref={fileInputRef}
            style={{ display: 'none' }}
            multiple
            accept=".glb,.gltf,.vrm,.mp3,.json"
            onChange={handleFileUpload}
          />

          <button
            className="unity-btn"
            title="Import file .glb / .vrm từ máy"
            onClick={() => fileInputRef.current?.click()}
          >
            <Upload size={12} /> Import
          </button>

          {onToggleMaximize && (
            <button
              className="unity-btn icon-only"
              onClick={onToggleMaximize}
              title={isMaximized ? 'Thu nhỏ' : 'Mở rộng cửa sổ'}
            >
              {isMaximized ? <Minimize2 size={13} /> : <Maximize2 size={13} />}
            </button>
          )}
        </div>
      </div>

      {/* Main Browser Layout: Left Folder Tree, Center Grid, Right Quick Inspector */}
      <div className="unity-browser-body">
        {/* Left: Folder Hierarchy Tree */}
        <div className="unity-folder-tree">
          <div className="folder-tree-header">THƯ MỤC DỰ ÁN</div>
          <div className="folder-tree-list">
            {FOLDER_TREE.map((root) => renderTreeItem(root, 0))}
          </div>
        </div>

        {/* Center: Asset Grid */}
        <div className="unity-asset-grid-container">
          <div className="unity-asset-grid">
            {filteredAssets.length === 0 ? (
              <div className="empty-assets">
                <Box size={24} color="#64748b" />
                <span>Không tìm thấy tài nguyên nào phù hợp</span>
              </div>
            ) : (
              filteredAssets.map((asset) => {
                const isSelected = selectedAssetId === asset.id;
                let Icon = Box;
                if (asset.type === 'character') Icon = User;
                if (asset.type === 'map') Icon = Map;
                if (asset.type === 'audio') Icon = Music;
                if (asset.type === 'vfx') Icon = Sparkles;
                if (asset.type === 'animation') Icon = Film;

                return (
                  <div
                    key={asset.id}
                    className={`unity-asset-card ${isSelected ? 'selected' : ''}`}
                    onClick={() => setSelectedAssetId(asset.id)}
                    onDoubleClick={() => handleAction(asset)}
                  >
                    <div
                      className="asset-icon-box"
                      style={{
                        backgroundColor: asset.previewColor ? `${asset.previewColor}18` : 'rgba(255,255,255,0.05)',
                        borderColor: isSelected ? '#38bdf8' : 'rgba(255,255,255,0.08)'
                      }}
                    >
                      <Icon size={24} color={asset.previewColor || '#94a3b8'} />
                      <span className="asset-format-badge">{asset.format}</span>
                    </div>

                    <div className="asset-info">
                      <div className="asset-name" title={asset.name}>
                        {asset.name}
                      </div>
                      <div className="asset-meta">
                        {asset.size || asset.type.toUpperCase()}
                      </div>
                    </div>

                    <button
                      className="card-quick-btn"
                      title={
                        asset.type === 'prop'
                          ? 'Đặt vào Scene'
                          : asset.type === 'map'
                          ? 'Tải Map này'
                          : asset.type === 'character'
                          ? 'Đổi Avatar'
                          : 'Phát Thử'
                      }
                      onClick={(e) => {
                        e.stopPropagation();
                        handleAction(asset);
                      }}
                    >
                      {asset.type === 'prop' ? (
                        <>
                          <Plus size={11} /> Đặt vào
                        </>
                      ) : asset.type === 'map' ? (
                        <>
                          <Map size={11} /> Tải Map
                        </>
                      ) : asset.type === 'character' ? (
                        <>
                          <User size={11} /> Đổi Avatar
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

        {/* Right: Quick Inspector Preview for Selected Asset */}
        {selectedAsset && (
          <div className="unity-asset-inspector">
            <div className="inspector-header">THÔNG TIN ASSET</div>
            
            <div className="inspector-preview-box">
              <div
                className="preview-icon-large"
                style={{
                  backgroundColor: selectedAsset.previewColor ? `${selectedAsset.previewColor}22` : '#1e293b',
                  borderColor: selectedAsset.previewColor || '#38bdf8'
                }}
              >
                {selectedAsset.type === 'character' && <User size={36} color={selectedAsset.previewColor || '#38bdf8'} />}
                {selectedAsset.type === 'map' && <Map size={36} color={selectedAsset.previewColor || '#38bdf8'} />}
                {selectedAsset.type === 'prop' && <Box size={36} color={selectedAsset.previewColor || '#38bdf8'} />}
                {selectedAsset.type === 'audio' && <Music size={36} color={selectedAsset.previewColor || '#38bdf8'} />}
                {selectedAsset.type === 'vfx' && <Sparkles size={36} color={selectedAsset.previewColor || '#38bdf8'} />}
                {selectedAsset.type === 'animation' && <Film size={36} color={selectedAsset.previewColor || '#38bdf8'} />}
              </div>
              <div className="preview-title">{selectedAsset.name}</div>
              <div className="preview-type-badge">{selectedAsset.format} • {selectedAsset.type.toUpperCase()}</div>
            </div>

            <div className="inspector-details">
              <div className="detail-row">
                <span className="label">Đường dẫn:</span>
                <span className="val" title={selectedAsset.path}>{selectedAsset.path}</span>
              </div>
              {selectedAsset.size && (
                <div className="detail-row">
                  <span className="label">Dung lượng:</span>
                  <span className="val">{selectedAsset.size}</span>
                </div>
              )}
              {selectedAsset.description && (
                <div className="detail-desc">
                  {selectedAsset.description}
                </div>
              )}
              {selectedAsset.tags && selectedAsset.tags.length > 0 && (
                <div className="tag-list">
                  {selectedAsset.tags.map((t, i) => (
                    <span key={i} className="tag">{t}</span>
                  ))}
                </div>
              )}
            </div>

            <div className="inspector-actions">
              <button
                className="primary-action-btn"
                onClick={() => handleAction(selectedAsset)}
              >
                {selectedAsset.type === 'prop' && <><Plus size={14} /> Chèn vào Scene Ngay</>}
                {selectedAsset.type === 'map' && <><Map size={14} /> Kích Hoạt Bản Đồ Này</>}
                {selectedAsset.type === 'character' && <><User size={14} /> Gán Cho Nhân Vật</>}
                {selectedAsset.type === 'animation' && <><Play size={14} /> Chạy Thử Động Tác</>}
                {selectedAsset.type === 'vfx' && <><Sparkles size={14} /> Xem Thử Hiệu Ứng</>}
                {selectedAsset.type === 'audio' && <><Play size={14} /> Nghe Thử SFX</>}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
