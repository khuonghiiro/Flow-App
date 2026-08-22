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

const modelExts = ['.vrm', '.glb', '.gltf', '.fbx', '.obj', '.dae'];
const audioExts = ['.mp3', '.wav', '.ogg', '.m4a'];
const animExts = ['.glb', '.bvh', '.fbx'];
const imageExts = ['.png', '.jpg', '.jpeg', '.webp', '.gif', '.bmp', '.svg'];

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
 * Recursively collect all files inside a directory (helper for bundle scanning)
 */
function getAllFilesInDir(dirPath) {
  if (!fs.existsSync(dirPath)) return [];
  const results = [];
  try {
    const list = fs.readdirSync(dirPath, { withFileTypes: true });
    for (const item of list) {
      if (item.name.startsWith('.')) continue;
      const fullPath = path.join(dirPath, item.name);
      if (item.isDirectory()) {
        results.push(...getAllFilesInDir(fullPath));
      } else {
        const stats = fs.statSync(fullPath);
        results.push({
          name: item.name,
          fullPath: fullPath,
          size: stats.size
        });
      }
    }
  } catch (err) {
    console.warn(`Lỗi duyệt thư mục ${dirPath}:`, err.message);
  }
  return results;
}

/**
 * ─── HIERARCHICAL FOLDER SCANNER ──────────────────────────────
 * Quy tắc xác nhận nạp tài nguyên theo cấp bậc thư mục:
 * 1. Cấp gốc danh mục (relativeDepth === 0, ví dụ folder C khi C là gốc):
 *    - Các file model 3D (.glb, .gltf, .vrm, .fbx, .obj, .dae) -> Nạp thành tài nguyên Model 3D.
 *      + Nếu có ảnh cùng tên (cùng basename) trong folder C -> Lấy làm ảnh tham chiếu (previewUrl) của file đó.
 *    - Các file ảnh trong folder C mà KHÔNG trùng tên với file 3D nào -> Là tài nguyên Hình ảnh/Texture độc lập -> Nạp lên sử dụng!
 *    - Các file Audio/JSON -> Nạp bình thường.
 * 2. Cấp folder con (relativeDepth > 0, ví dụ các folder con 1, 2, 3, 4, 5... bên trong C):
 *    - Đi sâu vào kiểm tra xem trong folder con có chứa file model 3D không:
 *      + Nếu có model 3D: Nạp model 3D đó. Nếu trong folder con có ảnh trùng tên với model 3D -> Lấy làm preview của model.
 *      + CÁC ẢNH KHÁC KHÁC TÊN TRONG FOLDER CON -> KHÔNG ĐƯỢC LOAD LÊN làm tài nguyên riêng!
 *      + Nếu là 1 Folder Bundle (chứa scene.gltf + textures/): Nạp thành 1 Model Bundle duy nhất, ảnh preview đại diện, bỏ qua các ảnh texture con.
 *    - Nếu trong folder con KHÔNG có model 3D: Tuyệt đối KHÔNG nạp bất kỳ ảnh nào trong folder con đó (vì không nằm ở folder gốc C).
 */
function scanFolderHierarchy(currentDir, relativeDepth = 0, rootCategoryDir = currentDir, allowedModelExts = modelExts, options = {}) {
  if (!fs.existsSync(currentDir)) return [];
  const results = [];
  let entries = [];
  try {
    entries = fs.readdirSync(currentDir, { withFileTypes: true });
  } catch (err) {
    return [];
  }

  const files = entries.filter(e => !e.isDirectory());
  const dirs = entries.filter(e => e.isDirectory());

  // Track companion preview images in this folder to avoid duplicate listing
  const consumedCompanionImages = new Set();

  const isPrimaryImageCategory = options.isImageCategory || false;

  // ─── 1. Scan Direct Model Files in this directory ──────────────────────────────
  for (const file of files) {
    const ext = path.extname(file.name).toLowerCase();
    if (allowedModelExts.includes(ext)) {
      const fullPath = path.join(currentDir, file.name);
      const stats = fs.statSync(fullPath);
      const baseName = path.parse(file.name).name;
      const relPath = path.relative(rootDir, fullPath).replace(/\\/g, '/');

      // Look for companion reference image matching this model name in currentDir
      let previewUrl = '';
      for (const imgExt of imageExts) {
        const candidate1 = `${baseName}${imgExt}`;
        const candidate2 = `${baseName}.preview${imgExt}`;
        const candidate3 = `${baseName}.thumbnail${imgExt}`;

        if (files.some(f => f.name.toLowerCase() === candidate1.toLowerCase())) {
          previewUrl = path.relative(rootDir, path.join(currentDir, candidate1)).replace(/\\/g, '/');
          consumedCompanionImages.add(candidate1);
          break;
        } else if (files.some(f => f.name.toLowerCase() === candidate2.toLowerCase())) {
          previewUrl = path.relative(rootDir, path.join(currentDir, candidate2)).replace(/\\/g, '/');
          consumedCompanionImages.add(candidate2);
          break;
        } else if (files.some(f => f.name.toLowerCase() === candidate3.toLowerCase())) {
          previewUrl = path.relative(rootDir, path.join(currentDir, candidate3)).replace(/\\/g, '/');
          consumedCompanionImages.add(candidate3);
          break;
        }
      }

      const uniqueId = relPath.replace(/\.[^/.]+$/, '').replace(/[/\\ \-_]/g, '_').toLowerCase();
      results.push({
        id: uniqueId,
        name: formatDisplayName(file.name),
        filename: file.name,
        relPath: relPath,
        path: `assets/${relPath}`,
        format: ext.replace('.', '').toUpperCase(),
        sizeMB: (stats.size / (1024 * 1024)).toFixed(2),
        gender: detectGender(relPath),
        previewUrl: previewUrl ? (previewUrl.startsWith('assets/') ? previewUrl : `assets/${previewUrl}`) : undefined,
        description: `${formatDisplayName(file.name)} (${ext.replace('.', '').toUpperCase()})`
      });
    }
  }

  // ─── 2. Handle Standalone Images & Audio ──────────────────────────────
  for (const file of files) {
    const ext = path.extname(file.name).toLowerCase();

    // Standalone Images & GIFs
    if (imageExts.includes(ext)) {
      if (consumedCompanionImages.has(file.name)) continue;
      if (file.name.startsWith('.') || file.name.toLowerCase().startsWith('preview.') || file.name.toLowerCase().startsWith('thumbnail.')) continue;

      // RULE: Only load standalone images if at ROOT level (relativeDepth === 0) or if this is an explicit image category
      if (relativeDepth === 0 || isPrimaryImageCategory) {
        const fullPath = path.join(currentDir, file.name);
        const stats = fs.statSync(fullPath);
        const relPath = path.relative(rootDir, fullPath).replace(/\\/g, '/');
        const uniqueId = relPath.replace(/\.[^/.]+$/, '').replace(/[/\\ \-_]/g, '_').toLowerCase();

        results.push({
          id: uniqueId,
          name: formatDisplayName(file.name),
          filename: file.name,
          relPath: relPath,
          path: `assets/${relPath}`,
          format: ext.replace('.', '').toUpperCase(),
          sizeMB: (stats.size / (1024 * 1024)).toFixed(2),
          gender: detectGender(relPath),
          previewUrl: `assets/${relPath}`,
          isStandaloneImage: true,
          description: `Tài nguyên Hình Ảnh/Texture: ${formatDisplayName(file.name)} (${ext.replace('.', '').toUpperCase()})`
        });
      }
      // If relativeDepth > 0 and NOT isPrimaryImageCategory: SKIP image (textures/subfolder images are NOT loaded as standalone assets)
    }

    // Audio Files
    if (audioExts.includes(ext)) {
      const fullPath = path.join(currentDir, file.name);
      const stats = fs.statSync(fullPath);
      const relPath = path.relative(rootDir, fullPath).replace(/\\/g, '/');
      const uniqueId = relPath.replace(/\.[^/.]+$/, '').replace(/[/\\ \-_]/g, '_').toLowerCase();

      results.push({
        id: uniqueId,
        name: formatDisplayName(file.name),
        filename: file.name,
        relPath: relPath,
        path: `assets/${relPath}`,
        format: ext.replace('.', '').toUpperCase(),
        sizeMB: (stats.size / (1024 * 1024)).toFixed(2),
        description: `Âm Thanh: ${formatDisplayName(file.name)} (${ext.replace('.', '').toUpperCase()})`
      });
    }
  }

  // ─── 3. Scan Subdirectories ──────────────────────────────────
  for (const dir of dirs) {
    if (dir.name.startsWith('.') || dir.name === 'node_modules' || dir.name === 'presets') continue;
    if (dir.name === '_lap_rap' || dir.name === '_custom_ban_do') continue;

    const subDirPath = path.join(currentDir, dir.name);
    const allSubFiles = getAllFilesInDir(subDirPath);
    const subModelFiles = allSubFiles.filter(f => allowedModelExts.includes(path.extname(f.name).toLowerCase()));

    // Check if this subfolder is a single GLTF/GLB bundle (e.g. scene.gltf + textures/)
    const hasSceneEntry = subModelFiles.some(f => {
      const lower = f.name.toLowerCase();
      return lower === 'scene.gltf' || lower === 'scene.glb' || lower === 'main.gltf' || lower === 'main.glb' || lower === 'index.gltf';
    });

    const isTrueBundle = hasSceneEntry || (subModelFiles.length === 1 && allSubFiles.length > 1);

    if (isTrueBundle && subModelFiles.length > 0) {
      let mainModel = subModelFiles.find(f => f.name.toLowerCase() === 'scene.gltf' || f.name.toLowerCase() === 'scene.glb');
      if (!mainModel) mainModel = subModelFiles.find(f => f.name.toLowerCase() === 'main.gltf' || f.name.toLowerCase() === 'main.glb');
      if (!mainModel) mainModel = subModelFiles.find(f => f.name.toLowerCase() === `${dir.name.toLowerCase()}.gltf` || f.name.toLowerCase() === `${dir.name.toLowerCase()}.glb`);
      if (!mainModel) mainModel = subModelFiles[0];

      const totalBundleSize = allSubFiles.reduce((acc, f) => acc + (f.size || 0), 0);

      let bundlePreviewUrl = '';
      const subImages = allSubFiles.filter(f => imageExts.includes(path.extname(f.name).toLowerCase()));
      const previewImg = subImages.find(f => {
        const lower = f.name.toLowerCase();
        return lower.startsWith('preview') || lower.startsWith('thumbnail') || lower.startsWith('cover') || lower.startsWith(dir.name.toLowerCase());
      });

      if (previewImg) {
        bundlePreviewUrl = path.relative(rootDir, previewImg.fullPath).replace(/\\/g, '/');
      } else {
        for (const imgExt of imageExts) {
          const candidate = `${dir.name}${imgExt}`;
          const candidateFull = path.join(currentDir, candidate);
          if (fs.existsSync(candidateFull)) {
            bundlePreviewUrl = path.relative(rootDir, candidateFull).replace(/\\/g, '/');
            consumedCompanionImages.add(candidate);
            break;
          }
        }
      }

      const relModelPath = path.relative(rootDir, mainModel.fullPath).replace(/\\/g, '/');
      const uniqueId = relModelPath.replace(/\.[^/.]+$/, '').replace(/[/\\ \-_]/g, '_').toLowerCase();
      results.push({
        id: uniqueId,
        name: formatDisplayName(dir.name),
        filename: dir.name,
        relPath: relModelPath,
        path: `assets/${relModelPath}`,
        bundleDir: path.relative(rootDir, subDirPath).replace(/\\/g, '/'),
        isFolderBundle: true,
        format: path.extname(mainModel.name).replace('.', '').toUpperCase(),
        sizeMB: (totalBundleSize / (1024 * 1024)).toFixed(2),
        gender: detectGender(relModelPath),
        previewUrl: bundlePreviewUrl ? (bundlePreviewUrl.startsWith('assets/') ? bundlePreviewUrl : `assets/${bundlePreviewUrl}`) : undefined,
        description: `${formatDisplayName(dir.name)} (Model Bundle: ${path.extname(mainModel.name).toUpperCase()})`
      });
      // All other texture images inside the bundle folder are ignored
    } else {
      // Recurse into subfolder with relativeDepth + 1
      results.push(...scanFolderHierarchy(subDirPath, relativeDepth + 1, rootCategoryDir, allowedModelExts, options));
    }
  }

  return results;
}

/**
 * Scan a list of folder aliases and combine results using Hierarchical Scanner
 */
function scanFolderAliases(aliases, allowedModelExts = modelExts, options = {}) {
  const all = [];
  for (const a of aliases) {
    const full = path.join(rootDir, a);
    if (fs.existsSync(full)) {
      all.push(...scanFolderHierarchy(full, 0, full, allowedModelExts, options));
    }
  }
  // Deduplicate by relPath
  return Array.from(new Map(all.map(item => [item.relPath, item])).values());
}

console.log('Scanning assets directory:', rootDir);

// 2.1 Assembled Characters (_lap_rap)
const assembledChars = [];
const possibleLapRapDirs = [
  path.join(rootDir, 'characters/_lap_rap'),
  path.join(rootDir, 'characters/lap_rap'),
  path.join(rootDir, 'nhan_vat/_lap_rap'),
  path.join(rootDir, 'nhan_vat/lap_rap'),
];

for (const lapRapDir of possibleLapRapDirs) {
  if (fs.existsSync(lapRapDir)) {
    const files = fs.readdirSync(lapRapDir);
    for (const file of files) {
      const full = path.join(lapRapDir, file);
      const baseName = path.parse(file).name;
      const relFolder = path.relative(rootDir, lapRapDir).replace(/\\/g, '/');
      if (file.endsWith('.json')) {
        try {
          const json = JSON.parse(fs.readFileSync(full, 'utf-8'));
          const pngCandidate = path.join(lapRapDir, `${baseName}.png`);
          let previewUrl = fs.existsSync(pngCandidate)
            ? `assets/${relFolder}/${baseName}.png`
            : (json.preview_image || undefined);

          assembledChars.push({
            id: json.id || baseName,
            name: json.name || formatDisplayName(baseName),
            filename: file,
            relPath: `${relFolder}/${file}`,
            path: `assets/${relFolder}/${file}`,
            format: 'JSON',
            sizeMB: '0.01',
            gender: json.gender || 'unisex',
            previewUrl,
            character_data: json,
            assembly: json.assembly || {
              base_body: json.base_body,
              costume: json.costume,
              face: json.face,
              hairstyle: json.hairstyle,
            }
          });
        } catch (err) {
          console.warn(`Lỗi đọc nhân vật lắp ráp ${file}:`, err.message);
        }
      } else if (modelExts.includes(path.extname(file).toLowerCase())) {
        const stats = fs.statSync(full);
        const pngCandidate = path.join(lapRapDir, `${baseName}.png`);
        assembledChars.push({
          id: baseName,
          name: formatDisplayName(file),
          filename: file,
          relPath: `${relFolder}/${file}`,
          path: `assets/${relFolder}/${file}`,
          format: path.extname(file).replace('.', '').toUpperCase(),
          sizeMB: (stats.size / (1024 * 1024)).toFixed(2),
          gender: detectGender(file),
          previewUrl: fs.existsSync(pngCandidate) ? `assets/${relFolder}/${baseName}.png` : undefined
        });
      }
    }
  }
}

// 2.2 Custom Maps (_custom_ban_do)
const customMaps = [];
const customMapDir = path.join(rootDir, 'ban_do/_custom_ban_do');
if (fs.existsSync(customMapDir)) {
  const files = fs.readdirSync(customMapDir);
  for (const file of files) {
    const full = path.join(customMapDir, file);
    const baseName = path.parse(file).name;
    if (file.endsWith('.json')) {
      try {
        const json = JSON.parse(fs.readFileSync(full, 'utf-8'));
        const pngCandidate = path.join(customMapDir, `${baseName}.png`);
        let previewUrl = fs.existsSync(pngCandidate)
          ? `assets/ban_do/_custom_ban_do/${baseName}.png`
          : undefined;

        customMaps.push({
          id: json.id || baseName,
          name: json.name || formatDisplayName(baseName),
          filename: file,
          relPath: `ban_do/_custom_ban_do/${file}`,
          path: `assets/ban_do/_custom_ban_do/${file}`,
          format: 'JSON',
          sizeMB: '0.02',
          previewUrl,
          map_data: json
        });
      } catch (err) {
        console.warn(`Lỗi đọc map tùy chỉnh ${file}:`, err.message);
      }
    }
  }
}

// ─── 2. DYNAMICALLY SCAN CHARACTERS ────────────────────────────
const charactersManifest = {};
const charCategories = assetStructure?.character_structure?.categories || [];

for (const cat of charCategories) {
  if (cat.id === '_lap_rap') {
    charactersManifest['_lap_rap'] = assembledChars;
    continue;
  }
  const folder = cat.folder || cat.id;
  const aliases = [
    `nhan_vat/${folder}/nam`, `nhan_vat/${folder}/nu`, `nhan_vat/${folder}/chung`, `nhan_vat/${folder}`,
    `characters/${folder}/nam`, `characters/${folder}/nu`, `characters/${folder}/chung`, `characters/${folder}`,
    `${folder}/nam`, `${folder}/nu`, `${folder}/chung`, folder
  ];
  if (cat.id !== folder) {
    aliases.push(`nhan_vat/${cat.id}/nam`, `nhan_vat/${cat.id}/nu`, `nhan_vat/${cat.id}/chung`, `nhan_vat/${cat.id}`);
  }
  charactersManifest[cat.id] = scanFolderAliases(aliases, modelExts);
}

// ─── 3. DYNAMICALLY SCAN WORLD & PROPS ──────────────────────────
const propsManifest = {};
const propCategories = [...(assetStructure?.world_and_props_structure?.categories || [])];
let mapsList = [];
let skyboxManifest = {};
let vfxManifest = {};

for (const cat of propCategories) {
  if (cat.id === 'ban_do') {
    mapsList = scanFolderAliases(['ban_do', 'maps', cat.folder || 'ban_do'], modelExts);
  } else if (cat.id === '_custom_ban_do') {
    propsManifest['_custom_ban_do'] = customMaps;
  } else if (cat.id === 'nhan_vat_da_rap') {
    propsManifest['nhan_vat_da_rap'] = assembledChars;
  } else if (cat.id === 'bau_troi') {
    skyboxManifest = {};
    const allSky = [];
    for (const sub of (cat.subcategories || [])) {
      const aliases = [`bau_troi/${sub.folder}`, `SkyBoxs/${sub.folder}`, sub.folder];
      const scannedSub = scanFolderAliases(aliases, imageExts, { isImageCategory: true });
      skyboxManifest[sub.id] = scannedSub;
      allSky.push(...scannedSub);
    }
    skyboxManifest.all = allSky;
  } else if (cat.id === 'hieu_ung') {
    vfxManifest = {};
    const allVfx = [];
    for (const sub of (cat.subcategories || [])) {
      const aliases = [`hieu_ung/${sub.folder}`, `vfx/${sub.folder}`, sub.folder];
      const scannedSub = scanFolderAliases(aliases, [...modelExts, ...imageExts], { isImageCategory: true });
      vfxManifest[sub.id] = scannedSub;
      allVfx.push(...scannedSub);
    }
    vfxManifest.all = allVfx;
  } else if (cat.subcategories && cat.subcategories.length > 0) {
    propsManifest[cat.id] = {};
    const allInCat = [];
    for (const sub of cat.subcategories) {
      const aliases = [
        `dao_cu/${cat.folder}/${sub.folder}`, `props/${cat.folder}/${sub.folder}`,
        `${cat.folder}/${sub.folder}`, `dao_cu/${cat.id}/${sub.id}`, `${cat.id}/${sub.id}`
      ];
      const scannedSub = scanFolderAliases(aliases, modelExts);
      propsManifest[cat.id][sub.id] = scannedSub;
      allInCat.push(...scannedSub);
    }
    propsManifest[cat.id].all = allInCat;
  } else {
    const aliases = [`dao_cu/${cat.folder}`, `props/${cat.folder}`, cat.folder];
    if (cat.id !== cat.folder) {
      aliases.push(`dao_cu/${cat.id}`, `props/${cat.id}`, cat.id);
    }
    propsManifest[cat.id] = scanFolderAliases(aliases, modelExts);
  }
}

// ─── 4. AUTO-DISCOVERY OF UNCONFIGURED FOLDERS & ROOT ASSETS ON DISK ─────────
const knownTopFolders = new Set(['node_modules', '.git', 'animations', 'audio', 'ban_do', 'bau_troi', 'dao_cu', 'hieu_ung', 'nhan_vat', 'characters', 'props', 'maps', 'SkyBoxs', 'vfx', 'presets']);
try {
  const topEntries = fs.readdirSync(rootDir, { withFileTypes: true });

  // 4.1 Root-level Model Files (e.g. assets/doan_tau_sat.glb, assets/xe_bus.glb, assets/goc_cay_co_reu.glb)
  const rootModelFiles = topEntries.filter(e => !e.isDirectory() && modelExts.includes(path.extname(e.name).toLowerCase()));
  for (const file of rootModelFiles) {
    const fullPath = path.join(rootDir, file.name);
    const stats = fs.statSync(fullPath);
    const ext = path.extname(file.name).toLowerCase();
    const relPath = file.name;
    const lowerName = file.name.toLowerCase();
    const uniqueId = `root_${file.name.replace(/\.[^/.]+$/, '').replace(/[/\\ \-_]/g, '_').toLowerCase()}`;

    // Look for companion image
    let previewUrl = '';
    const baseName = path.parse(file.name).name;
    for (const imgExt of imageExts) {
      const candidate = `${baseName}${imgExt}`;
      if (fs.existsSync(path.join(rootDir, candidate))) {
        previewUrl = `assets/${candidate}`;
        break;
      }
    }

    const item = {
      id: uniqueId,
      name: formatDisplayName(file.name),
      filename: file.name,
      relPath: relPath,
      path: `assets/${relPath}`,
      format: ext.replace('.', '').toUpperCase(),
      sizeMB: (stats.size / (1024 * 1024)).toFixed(2),
      gender: detectGender(relPath),
      previewUrl: previewUrl || undefined,
      description: `${formatDisplayName(file.name)} (${ext.replace('.', '').toUpperCase()})`
    };

    // Classify into best category
    if (lowerName.includes('xe') || lowerName.includes('tau') || lowerName.includes('bus') || lowerName.includes('thuyen') || lowerName.includes('car')) {
      if (!propsManifest['phuong_tien']) propsManifest['phuong_tien'] = [];
      if (Array.isArray(propsManifest['phuong_tien'])) propsManifest['phuong_tien'].push(item);
    } else if (lowerName.includes('cay') || lowerName.includes('goc') || lowerName.includes('hoa') || lowerName.includes('la') || lowerName.includes('tree')) {
      if (!propsManifest['cay_coi']) propsManifest['cay_coi'] = [];
      if (Array.isArray(propsManifest['cay_coi'])) propsManifest['cay_coi'].push(item);
    } else if (lowerName.includes('nha') || lowerName.includes('house') || lowerName.includes('cabin') || lowerName.includes('building')) {
      if (!propsManifest['cong_trinh']) propsManifest['cong_trinh'] = [];
      if (Array.isArray(propsManifest['cong_trinh'])) propsManifest['cong_trinh'].push(item);
    } else if (lowerName.includes('map') || lowerName.includes('island') || lowerName.includes('cathedral')) {
      mapsList.push(item);
    } else {
      if (!propsManifest['noi_that']) propsManifest['noi_that'] = [];
      if (Array.isArray(propsManifest['noi_that'])) propsManifest['noi_that'].push(item);
    }
  }

  // 4.2 Discovered Unconfigured Subdirectories
  for (const entry of topEntries) {
    if (entry.isDirectory() && !knownTopFolders.has(entry.name) && !entry.name.startsWith('.')) {
      const discoveredItems = scanFolderHierarchy(path.join(rootDir, entry.name), 0, path.join(rootDir, entry.name), modelExts);
      if (discoveredItems.length > 0) {
        propsManifest[entry.name] = discoveredItems;
        if (!propCategories.some(c => c.id === entry.name || c.folder === entry.name)) {
          propCategories.push({
            id: entry.name,
            folder: entry.name,
            label: formatDisplayName(entry.name),
            icon: '📦'
          });
        }
      }
    }
  }
} catch (e) {
  console.warn('Auto-discovery error:', e.message);
}

// ─── 5. Audio & Animations ─────────────────────────────────────
const bgm = scanFolderAliases(['audio/bgm'], audioExts);
const sfxCombat = scanFolderAliases(['audio/sfx/combat'], audioExts);
const sfxInteract = scanFolderAliases(['audio/sfx/interactions', 'audio/sfx/interaction'], audioExts);
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

// Collect all unique assets
const allAssetsMap = new Map();
function collectAssets(obj) {
  if (!obj) return;
  if (Array.isArray(obj)) {
    obj.forEach(item => {
      if (item && item.relPath) allAssetsMap.set(item.relPath, item);
    });
  } else if (typeof obj === 'object') {
    Object.values(obj).forEach(val => collectAssets(val));
  }
}

collectAssets(charactersManifest);
collectAssets(propsManifest);
collectAssets(skyboxManifest);
collectAssets(vfxManifest);
collectAssets(mapsList);
collectAssets(bgm);
collectAssets(sfxCombat);
collectAssets(sfxInteract);
collectAssets(sfxAmbient);
collectAssets(animCombat);
collectAssets(animInteract);
collectAssets(animXianxia);
collectAssets(animLocomotion);

const allAssets = Array.from(allAssetsMap.values());
const totalSize = allAssets.reduce((sum, item) => sum + parseFloat(item.sizeMB || 0), 0).toFixed(2);
const timestamp = new Date().toISOString();

// Build Clean, Dynamic Manifest
const manifest = {
  version: "2.0.0",
  last_scanned: timestamp,
  total_assets: allAssets.length,
  total_size_mb: parseFloat(totalSize),
  structure: {
    character_structure: {
      ...assetStructure.character_structure,
      categories: charCategories
    },
    world_and_props_structure: {
      ...assetStructure.world_and_props_structure,
      categories: propCategories
    },
    gender_rules: assetStructure.gender_rules,
    available_actions: assetStructure.available_actions
  },
  map_presets: mapPresets,
  characters: charactersManifest,
  props: propsManifest,
  skyboxes: skyboxManifest,
  vfx: vfxManifest,
  maps: mapsList,
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
console.log('✓ Generated asset_manifest.json with leaf-folder max-depth model bundle & companion image scanner.');
