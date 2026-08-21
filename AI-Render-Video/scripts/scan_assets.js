import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '../assets');

const outputMdEn = path.join(rootDir, 'ASSET_CATALOG.md');
const outputMdVi = path.join(rootDir, 'ASSET_CATALOG_VI.md');
const outputJson = path.join(rootDir, 'asset_manifest.json');
const structureJsonPath = path.join(rootDir, 'asset_structure.json');

const modelExts = ['.vrm', '.glb', '.gltf', '.fbx', '.obj'];
const audioExts = ['.mp3', '.wav', '.ogg'];
const animExts = ['.glb', '.bvh', '.fbx'];
const imageExts = ['.png', '.jpg', '.jpeg', '.webp'];

// ─── 1. Load Single Source of Truth: asset_structure.json ─────
let assetStructure = {
  character_structure: { categories: [] },
  world_and_props_structure: { categories: [] },
  gender_rules: { options: [] },
  dictionary: {}
};

if (fs.existsSync(structureJsonPath)) {
  try {
    assetStructure = JSON.parse(fs.readFileSync(structureJsonPath, 'utf-8'));
    console.log('✓ Loaded root asset_structure.json configuration.');
  } catch (err) {
    console.warn('Could not parse asset_structure.json:', err);
  }
}

// Build dynamic Vietnamese lookup map from asset_structure.json
const VIETNAMESE_LOOKUP = new Map();

// Load genders from structure
for (const g of assetStructure.gender_rules?.options || []) {
  VIETNAMESE_LOOKUP.set(g.key || g.id, { label: g.label, icon: g.icon });
  VIETNAMESE_LOOKUP.set(g.id, { label: g.label, icon: g.icon });
}

// Load character categories
for (const cat of assetStructure.character_structure?.categories || []) {
  VIETNAMESE_LOOKUP.set(cat.id, { label: cat.label, icon: cat.icon });
  VIETNAMESE_LOOKUP.set(cat.folder, { label: cat.label, icon: cat.icon });
  for (const a of cat.folder_aliases || []) {
    VIETNAMESE_LOOKUP.set(a, { label: cat.label, icon: cat.icon });
  }
}

// Load world & props categories & subcategories
for (const cat of assetStructure.world_and_props_structure?.categories || []) {
  VIETNAMESE_LOOKUP.set(cat.id, { label: cat.label, icon: cat.icon });
  VIETNAMESE_LOOKUP.set(cat.folder, { label: cat.label, icon: cat.icon });
  for (const a of cat.folder_aliases || []) {
    VIETNAMESE_LOOKUP.set(a, { label: cat.label, icon: cat.icon });
  }
  for (const sub of cat.subcategories || []) {
    VIETNAMESE_LOOKUP.set(sub.id, { label: sub.label, icon: sub.icon });
    VIETNAMESE_LOOKUP.set(sub.folder, { label: sub.label, icon: sub.icon });
    for (const sa of sub.folder_aliases || []) {
      VIETNAMESE_LOOKUP.set(sa, { label: sub.label, icon: sub.icon });
    }
  }
}

// Load custom dictionary entries
for (const [key, val] of Object.entries(assetStructure.dictionary || {})) {
  VIETNAMESE_LOOKUP.set(key, val);
}

/**
 * Resolve a human-friendly Vietnamese label & icon for any folder or item name
 */
function resolveVietnameseFolderInfo(folderName) {
  const cleanKey = folderName.toLowerCase().replace(/[^a-z0-9_]/g, '_');
  if (VIETNAMESE_LOOKUP.has(cleanKey)) {
    return VIETNAMESE_LOOKUP.get(cleanKey);
  }
  return {
    label: formatDisplayName(folderName),
    icon: '📦'
  };
}

/**
 * Clean human-readable display name formatter
 */
function formatDisplayName(filename) {
  if (!filename) return 'Tài Nguyên';
  const cleanKey = path.parse(filename).name.toLowerCase();
  if (VIETNAMESE_LOOKUP.has(cleanKey)) {
    return VIETNAMESE_LOOKUP.get(cleanKey).label;
  }
  return filename
    .replace(/\.[^/.]+$/, '')
    .replace(/_/g, ' ')
    .replace(/-/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/\b\w/g, c => c.toUpperCase());
}

/**
 * Detect gender from relative file path or filename
 */
function detectGender(relPath) {
  const p = relPath.toLowerCase();
  if (p.includes('/nam/') || p.includes('/male/') || p.includes('/man/') || p.includes('_nam') || p.includes('-man')) {
    return 'male';
  }
  if (p.includes('/nu/') || p.includes('/female/') || p.includes('/woman/') || p.includes('_nu') || p.includes('-manekina') || p.includes('-female')) {
    return 'female';
  }
  return 'unisex';
}

/**
 * Recursively find all files with allowed extensions and auto-match companion preview images
 */
function getFiles(folderPath, allowedExts) {
  if (!fs.existsSync(folderPath)) return [];
  const results = [];
  const entries = fs.readdirSync(folderPath, { withFileTypes: true });
  for (const entry of entries) {
    const full = path.join(folderPath, entry.name);
    if (entry.isDirectory()) {
      results.push(...getFiles(full, allowedExts));
    } else if (allowedExts.includes(path.extname(entry.name).toLowerCase())) {
      const stats = fs.statSync(full);
      const rel = path.relative(rootDir, full).replace(/\\/g, '/');
      const baseName = path.parse(entry.name).name;

      // Look for companion reference photo matching model name
      let previewUrl = '';
      for (const imgExt of ['.png', '.jpg', '.jpeg', '.webp', '.svg']) {
        const candidateImg = path.join(folderPath, `${baseName}${imgExt}`);
        const candidatePreview = path.join(folderPath, `${baseName}.preview${imgExt}`);
        if (fs.existsSync(candidateImg)) {
          previewUrl = path.relative(rootDir, candidateImg).replace(/\\/g, '/');
          break;
        } else if (fs.existsSync(candidatePreview)) {
          previewUrl = path.relative(rootDir, candidatePreview).replace(/\\/g, '/');
          break;
        }
      }
      if (!previewUrl) {
        for (const imgExt of ['.png', '.jpg', '.jpeg', '.webp', '.svg']) {
          const folderThumb = path.join(folderPath, `preview${imgExt}`);
          const folderThumb2 = path.join(folderPath, `thumbnail${imgExt}`);
          if (fs.existsSync(folderThumb)) {
            previewUrl = path.relative(rootDir, folderThumb).replace(/\\/g, '/');
            break;
          } else if (fs.existsSync(folderThumb2)) {
            previewUrl = path.relative(rootDir, folderThumb2).replace(/\\/g, '/');
            break;
          }
        }
      }

      results.push({
        id: baseName,
        name: formatDisplayName(entry.name),
        filename: entry.name,
        relPath: rel,
        format: path.extname(entry.name).replace('.', '').toUpperCase(),
        sizeMB: (stats.size / (1024 * 1024)).toFixed(2),
        gender: detectGender(rel),
        previewUrl: previewUrl ? `assets/${previewUrl}` : undefined
      });
    }
  }
  return results;
}

/**
 * Scan a list of folder aliases and combine results
 */
function scanFolderAliases(aliases, allowedExts) {
  const all = [];
  for (const a of aliases) {
    const full = path.join(rootDir, a);
    all.push(...getFiles(full, allowedExts));
  }
  // Deduplicate by relPath
  return Array.from(new Map(all.map(item => [item.relPath, item])).values());
}

console.log('Scanning assets directory:', rootDir);

// ─── 2. Scan Characters (Nam, Nữ, Modular parts) ────────────
const maleChars = scanFolderAliases(['nhan_vat/nam', 'characters/male', 'characters/man', 'characters/nam'], modelExts);
const femaleChars = scanFolderAliases(['nhan_vat/nu', 'characters/female', 'characters/woman', 'characters/nu'], modelExts);

const baseBodies = scanFolderAliases(['nhan_vat/than_co_ban', 'characters/base_bodies', 'characters/than_co_ban'], modelExts);
const faces = scanFolderAliases(['nhan_vat/khuon_mat', 'characters/faces', 'characters/khuon_mat'], modelExts);
const hairstyles = scanFolderAliases(['nhan_vat/kieu_toc', 'characters/hairstyles', 'characters/kieu_toc'], modelExts);
const beards = scanFolderAliases(['nhan_vat/kieu_rau', 'characters/beards', 'characters/kieu_rau'], modelExts);
const costumes = scanFolderAliases(['nhan_vat/trang_phuc', 'characters/costumes', 'characters/trang_phuc'], modelExts);
const accessories = scanFolderAliases(['nhan_vat/phu_kien', 'characters/accessories', 'characters/phu_kien'], modelExts);
const eyebrows = scanFolderAliases(['nhan_vat/long_may', 'characters/long_may'], modelExts);
const eyes = scanFolderAliases(['nhan_vat/mat', 'characters/mat'], modelExts);
const noses = scanFolderAliases(['nhan_vat/mui', 'characters/mui'], modelExts);
const mouths = scanFolderAliases(['nhan_vat/mieng', 'characters/mieng'], modelExts);
const hats = scanFolderAliases(['nhan_vat/mu_non', 'characters/mu_non'], modelExts);
const shoes = scanFolderAliases(['nhan_vat/giay_dep', 'characters/giay_dep'], modelExts);
const wings = scanFolderAliases(['nhan_vat/canh', 'characters/canh'], modelExts);
const tails = scanFolderAliases(['nhan_vat/duoi', 'characters/duoi'], modelExts);

// Legacy root character files
const rootCharFiles = [
  ...getFiles(path.join(rootDir, 'characters'), modelExts).filter(f => !f.relPath.includes('/')),
  ...getFiles(path.join(rootDir, 'nhan_vat'), modelExts).filter(f => !f.relPath.includes('/'))
];

// ─── 3. Scan Props & World Environment ────────────────────────
const maps = scanFolderAliases(['ban_do', 'maps'], modelExts);
const trees = scanFolderAliases(['dao_cu/cay_coi', 'props/cay_coi', 'props/nature/trees'], modelExts);
const rocks = scanFolderAliases(['dao_cu/da_dia_hinh', 'props/da_dia_hinh', 'props/nature/rocks'], modelExts);

const animalsLand = scanFolderAliases(['dao_cu/dong_vat/tren_can', 'props/dong_vat/tren_can', 'props/animals/terrestrial'], modelExts);
const animalsWater = scanFolderAliases(['dao_cu/dong_vat/duoi_nuoc', 'props/dong_vat/duoi_nuoc', 'props/animals/aquatic'], modelExts);
const animalsAir = scanFolderAliases(['dao_cu/dong_vat/tren_troi', 'props/dong_vat/tren_troi', 'props/animals/aerial'], modelExts);

const weapons = scanFolderAliases(['dao_cu/vu_khi', 'props/vu_khi', 'props/weapons'], modelExts);
const tools = scanFolderAliases(['dao_cu/dung_cu', 'props/dung_cu', 'props/tools'], modelExts);
const consumables = scanFolderAliases(['dao_cu/do_tieu_hao', 'props/do_tieu_hao', 'props/consumables'], modelExts);
const furniture = scanFolderAliases(['dao_cu/noi_that', 'props/noi_that', 'props/furniture'], modelExts);
const buildings = scanFolderAliases(['dao_cu/cong_trinh', 'props/cong_trinh', 'props/buildings'], modelExts);
const vehicles = scanFolderAliases(['dao_cu/phuong_tien', 'props/phuong_tien', 'props/vehicles'], modelExts);

// Skybox images
const skyboxDawn = scanFolderAliases(['bau_troi/binh_minh', 'SkyBoxs/binh_minh'], imageExts);
const skyboxMorning = scanFolderAliases(['bau_troi/buoi_sang', 'SkyBoxs/buoi_sang'], imageExts);
const skyboxNoon = scanFolderAliases(['bau_troi/buoi_trua', 'SkyBoxs/buoi_trua'], imageExts);
const skyboxAfternoon = scanFolderAliases(['bau_troi/buoi_chieu', 'SkyBoxs/buoi_chieu'], imageExts);
const skyboxNight = scanFolderAliases(['bau_troi/buoi_toi', 'SkyBoxs/buoi_toi'], imageExts);
const skyboxStorm = scanFolderAliases(['bau_troi/giong_bao', 'SkyBoxs/giong_bao'], imageExts);
const allSkyboxes = scanFolderAliases(['bau_troi', 'SkyBoxs'], imageExts);

// VFX
const vfxProps = scanFolderAliases(['hieu_ung', 'vfx', 'props/hieu_ung'], [...modelExts, ...imageExts]);

// Audio & Animations
const bgm = scanFolderAliases(['audio/bgm'], audioExts);
const sfxCombat = scanFolderAliases(['audio/sfx/combat'], audioExts);
const sfxInteract = scanFolderAliases(['audio/sfx/interactions'], audioExts);
const sfxAmbient = scanFolderAliases(['audio/sfx/ambient'], audioExts);

const animCombat = scanFolderAliases(['animations/combat'], animExts);
const animInteract = scanFolderAliases(['animations/interactions'], animExts);
const animXianxia = scanFolderAliases(['animations/xianxia'], animExts);
const animLocomotion = scanFolderAliases(['animations/locomotion'], animExts);

// Load Map Presets
const mapPresetDirs = [path.join(rootDir, 'maps/presets'), path.join(rootDir, 'ban_do/presets')];
const mapPresets = [];
for (const pDir of mapPresetDirs) {
  if (fs.existsSync(pDir)) {
    const pFiles = fs.readdirSync(pDir).filter(f => f.endsWith('.json'));
    for (const pf of pFiles) {
      try {
        const raw = fs.readFileSync(path.join(pDir, pf), 'utf-8');
        const parsed = JSON.parse(raw);
        mapPresets.push({
          ...parsed,
          relPath: path.relative(rootDir, path.join(pDir, pf)).replace(/\\/g, '/')
        });
      } catch (e) {
        console.warn(`Warning: Could not parse preset ${pf}:`, e.message);
      }
    }
  }
}

// Compute total assets
const allAssets = [
  ...maleChars, ...femaleChars, ...baseBodies, ...faces, ...hairstyles, ...beards,
  ...costumes, ...accessories, ...eyebrows, ...eyes, ...noses, ...mouths, ...hats, ...shoes, ...wings, ...tails,
  ...rootCharFiles, ...maps, ...trees, ...rocks, ...animalsLand, ...animalsWater, ...animalsAir,
  ...weapons, ...tools, ...consumables, ...furniture, ...buildings, ...vehicles,
  ...allSkyboxes, ...vfxProps, ...bgm, ...sfxCombat, ...sfxInteract, ...sfxAmbient,
  ...animCombat, ...animInteract, ...animXianxia, ...animLocomotion
];
const totalSize = allAssets.reduce((sum, item) => sum + parseFloat(item.sizeMB || 0), 0).toFixed(2);
const timestamp = new Date().toISOString();

// Build Complete Manifest
const manifest = {
  version: "2.0.0",
  last_scanned: timestamp,
  total_assets: allAssets.length,
  total_size_mb: parseFloat(totalSize),
  structure: assetStructure,
  map_presets: mapPresets,
  characters: {
    male: maleChars,
    female: femaleChars,
    base_bodies: [...baseBodies, ...rootCharFiles],
    faces,
    hairstyles,
    beards,
    costumes,
    accessories,
    eyebrows,
    eyes,
    noses,
    mouths,
    hats,
    shoes,
    wings,
    tails
  },
  props: {
    trees,
    rocks,
    animals: {
      all: [...animalsLand, ...animalsWater, ...animalsAir],
      terrestrial: animalsLand,
      aquatic: animalsWater,
      aerial: animalsAir
    },
    weapons,
    tools,
    consumables,
    furniture,
    buildings,
    vehicles,
    vfx: vfxProps
  },
  skyboxes: {
    all: allSkyboxes,
    binh_minh: skyboxDawn,
    buoi_sang: skyboxMorning,
    buoi_trua: skyboxNoon,
    buoi_chieu: skyboxAfternoon,
    buoi_toi: skyboxNight,
    giong_bao: skyboxStorm
  },
  maps,
  audio: {
    bgm,
    sfx_combat: sfxCombat,
    sfx_interaction: sfxInteract,
    sfx_ambient: sfxAmbient
  },
  animations: {
    combat: animCombat,
    interaction: animInteract,
    xianxia: animXianxia,
    locomotion: animLocomotion
  },
  vfx: vfxProps,
  available_actions: assetStructure.available_actions || [
    { id: "idle", label: "Đứng Chờ (Idle)" },
    { id: "walk", label: "Bước Đi (Walk)" },
    { id: "run", label: "Chạy Nhanh (Run)" },
    { id: "sit", label: "Ngồi Ghế / Ngồi Đất (Sit)" },
    { id: "meditate", label: "Ngồi Thiền Tĩnh Tọa (Meditate)" },
    { id: "fly_to", label: "Bay Đến (Fly)" },
    { id: "heavy_slash_combo", label: "Chém Kiếm Liên Hoàn (Heavy Slash)" },
    { id: "fast_slash", label: "Kiếm Pháp Nhanh (Fast Slash)" },
    { id: "magic_blast", label: "Bắn Chưởng / Phép Thuật (Magic Blast)" },
    { id: "punch_kick", label: "Quyền Cước (Punch Kick)" },
    { id: "block_defend", label: "Thủ Thế / Khiên Đỡ (Block)" },
    { id: "dodge", label: "Né Đòn (Dodge)" }
  ],
  available_expressions: assetStructure.available_expressions || [
    { id: "neutral", label: "Bình Thường (Neutral)" },
    { id: "smile", label: "Mỉm Cười (Smile)" },
    { id: "angry", label: "Tức Giận (Angry)" },
    { id: "pain", label: "Đau Đớn (Pain)" },
    { id: "serious", label: "Nghiêm Túc (Serious)" },
    { id: "wise", label: "Điềm Đạm (Wise)" },
    { id: "cold", label: "Lạnh Lùng (Cold)" },
    { id: "arrogant", label: "Kiêu Ngạo (Arrogant)" }
  ]
};

fs.writeFileSync(outputJson, JSON.stringify(manifest, null, 2), 'utf-8');
console.log('✓ Generated asset_manifest.json from single source of truth asset_structure.json');
