import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '../asset_2ds');

const outputJson = path.join(rootDir, 'asset_2d_manifest.json');
const structureJsonPath = path.join(rootDir, 'asset_2d_structure.json');

const imageExts = ['.png', '.jpg', '.jpeg', '.webp', '.gif', '.svg'];
const audioExts = ['.mp3', '.wav', '.ogg', '.m4a'];

// ─── 1. Load asset_2d_structure.json ─────────────────────────────
let assetStructure = { categories: [], dictionary: {} };

if (fs.existsSync(structureJsonPath)) {
  try {
    assetStructure = JSON.parse(fs.readFileSync(structureJsonPath, 'utf-8'));
    console.log('✓ Loaded asset_2d_structure.json configuration.');
  } catch (err) {
    console.warn('Could not parse asset_2d_structure.json:', err);
  }
}

// Build Vietnamese lookup map
const VIETNAMESE_LOOKUP = new Map();

for (const cat of assetStructure.categories || []) {
  VIETNAMESE_LOOKUP.set(cat.id, { label: cat.label, icon: cat.icon });
  VIETNAMESE_LOOKUP.set(cat.folder, { label: cat.label, icon: cat.icon });
  for (const sub of cat.subcategories || []) {
    VIETNAMESE_LOOKUP.set(sub.id, { label: sub.label, icon: sub.icon });
    VIETNAMESE_LOOKUP.set(sub.folder, { label: sub.label, icon: sub.icon });
  }
}

for (const [key, val] of Object.entries(assetStructure.dictionary || {})) {
  VIETNAMESE_LOOKUP.set(key, val);
}

// ─── Helpers ─────────────────────────────────────────────────────

function formatDisplayName(filename) {
  if (!filename) return 'Tài Nguyên 2D';
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

function getFileSizeMB(fullPath) {
  try {
    const stats = fs.statSync(fullPath);
    return (stats.size / (1024 * 1024)).toFixed(2);
  } catch {
    return '0.00';
  }
}

/**
 * Scan all supported files in a directory (non-recursive for leaf, recursive for parent)
 */
function scanDirectory(dirPath, categoryId, subCategoryId, recursive = true) {
  if (!fs.existsSync(dirPath)) return [];
  const results = [];

  let entries;
  try {
    entries = fs.readdirSync(dirPath, { withFileTypes: true });
  } catch {
    return [];
  }

  for (const entry of entries) {
    if (entry.name.startsWith('.')) continue;
    const fullPath = path.join(dirPath, entry.name);

    if (entry.isFile()) {
      const ext = path.extname(entry.name).toLowerCase();
      const isImage = imageExts.includes(ext);
      const isAudio = audioExts.includes(ext);
      const isJson = ext === '.json' && !entry.name.includes('structure') && !entry.name.includes('manifest');

      if (!isImage && !isAudio && !isJson) continue;

      const relPath = path.relative(rootDir, fullPath).replace(/\\/g, '/');
      const baseName = path.parse(entry.name).name;
      const uniqueId = relPath.replace(/\.[^/.]+$/, '').replace(/[/\\ \-_]/g, '_').toLowerCase();

      // For images, check if there's a companion with same name (skip thumbnail/preview)
      if (entry.name.toLowerCase().startsWith('preview.') || entry.name.toLowerCase().startsWith('thumbnail.')) {
        continue;
      }

      const item = {
        id: uniqueId,
        name: formatDisplayName(entry.name),
        filename: entry.name,
        relPath: relPath,
        path: `asset_2ds/${relPath}`,
        format: ext.replace('.', '').toUpperCase(),
        sizeMB: getFileSizeMB(fullPath),
        categoryId: categoryId,
        subCategoryId: subCategoryId || undefined,
        type: isImage ? 'image' : isAudio ? 'audio' : 'data',
      };

      // For images, set previewUrl to self
      if (isImage) {
        item.previewUrl = `asset_2ds/${relPath}`;
      }

      // For JSON (assembled characters/maps), try to read metadata
      if (isJson) {
        try {
          const jsonData = JSON.parse(fs.readFileSync(fullPath, 'utf-8'));
          item.name = jsonData.name || formatDisplayName(entry.name);

          // Look for companion .png preview
          const pngCandidate = path.join(dirPath, `${baseName}.png`);
          if (fs.existsSync(pngCandidate)) {
            item.previewUrl = `asset_2ds/${path.relative(rootDir, pngCandidate).replace(/\\/g, '/')}`;
          } else if (jsonData.preview_image) {
            item.previewUrl = jsonData.preview_image;
          }

          item.metadata = {
            gender: jsonData.gender || undefined,
            characterName: jsonData.name || undefined,
          };
        } catch { /* ignore parse errors */ }
      }

      results.push(item);
    } else if (entry.isDirectory() && recursive) {
      // Skip known special folders at this level
      if (entry.name === 'node_modules') continue;
      results.push(...scanDirectory(fullPath, categoryId, subCategoryId, true));
    }
  }

  return results;
}

// ─── 2. Scan all categories ──────────────────────────────────────

console.log('Scanning asset_2ds directory:', rootDir);

const categoriesManifest = {};
const configuredCategories = assetStructure.categories || [];

for (const cat of configuredCategories) {
  const catFolder = path.join(rootDir, cat.folder);

  if (cat.subcategories && cat.subcategories.length > 0) {
    // Category with subcategories
    categoriesManifest[cat.id] = {};
    const allInCat = [];

    for (const sub of cat.subcategories) {
      const subFolder = path.join(catFolder, sub.folder);
      const items = scanDirectory(subFolder, cat.id, sub.id, true);
      categoriesManifest[cat.id][sub.id] = items;
      allInCat.push(...items);
    }

    // Also scan any files directly in category root (not in any subcategory)
    const rootFiles = scanDirectory(catFolder, cat.id, undefined, false);
    if (rootFiles.length > 0) {
      categoriesManifest[cat.id]['_root'] = rootFiles;
      allInCat.push(...rootFiles);
    }

    categoriesManifest[cat.id]['_all'] = allInCat;
  } else {
    // Category without subcategories — flat scan
    categoriesManifest[cat.id] = scanDirectory(catFolder, cat.id, undefined, true);
  }
}

// ─── 3. Auto-discover unconfigured folders ───────────────────────

const knownFolders = new Set(configuredCategories.map(c => c.folder));
knownFolders.add('.git');
knownFolders.add('node_modules');

try {
  const topEntries = fs.readdirSync(rootDir, { withFileTypes: true });
  for (const entry of topEntries) {
    if (!entry.isDirectory()) continue;
    if (knownFolders.has(entry.name) || entry.name.startsWith('.')) continue;

    const discoveredItems = scanDirectory(path.join(rootDir, entry.name), entry.name, undefined, true);
    if (discoveredItems.length > 0) {
      categoriesManifest[entry.name] = discoveredItems;
      const dictEntry = assetStructure.dictionary?.[entry.name];
      console.log(`  🆕 Auto-discovered: ${entry.name} (${discoveredItems.length} items) → ${dictEntry?.label || formatDisplayName(entry.name)}`);
    }
  }
} catch (e) {
  console.warn('Auto-discovery error:', e.message);
}

// ─── 4. Collect stats ────────────────────────────────────────────

const allItems = [];
function collectItems(obj) {
  if (!obj) return;
  if (Array.isArray(obj)) {
    for (const item of obj) {
      if (item && item.relPath) allItems.push(item);
    }
  } else if (typeof obj === 'object') {
    for (const val of Object.values(obj)) {
      collectItems(val);
    }
  }
}
collectItems(categoriesManifest);

// Deduplicate by relPath
const uniqueMap = new Map();
for (const item of allItems) {
  uniqueMap.set(item.relPath, item);
}
const uniqueItems = Array.from(uniqueMap.values());

const totalSize = uniqueItems.reduce((sum, item) => sum + parseFloat(item.sizeMB || 0), 0).toFixed(2);
const timestamp = new Date().toISOString();

const imageCount = uniqueItems.filter(i => i.type === 'image').length;
const audioCount = uniqueItems.filter(i => i.type === 'audio').length;
const dataCount = uniqueItems.filter(i => i.type === 'data').length;

// ─── 5. Build manifest ──────────────────────────────────────────

const manifest = {
  version: '1.0.0',
  last_scanned: timestamp,
  total_assets: uniqueItems.length,
  total_size_mb: parseFloat(totalSize),
  stats: {
    images: imageCount,
    audio: audioCount,
    data_json: dataCount,
  },
  structure: assetStructure,
  categories: categoriesManifest,
};

fs.writeFileSync(outputJson, JSON.stringify(manifest, null, 2), 'utf-8');

// ─── 6. Summary ─────────────────────────────────────────────────

console.log('');
console.log('╔══════════════════════════════════════════════╗');
console.log('║   🎨 2D ASSET SCAN COMPLETE                  ║');
console.log('╠══════════════════════════════════════════════╣');
console.log(`║  Tổng tài nguyên: ${String(uniqueItems.length).padEnd(6)} (${totalSize} MB)     ║`);
console.log(`║  ┣ Hình ảnh:      ${String(imageCount).padEnd(6)}                    ║`);
console.log(`║  ┣ Âm thanh:      ${String(audioCount).padEnd(6)}                    ║`);
console.log(`║  ┗ Dữ liệu JSON:  ${String(dataCount).padEnd(6)}                    ║`);
console.log('╠══════════════════════════════════════════════╣');

for (const cat of configuredCategories) {
  const catData = categoriesManifest[cat.id];
  let count = 0;
  if (Array.isArray(catData)) {
    count = catData.length;
  } else if (catData && catData._all) {
    count = catData._all.length;
  }
  console.log(`║  ${cat.icon} ${cat.label.padEnd(22)} ${String(count).padStart(4)} items    ║`);
}

// Show auto-discovered
for (const [key, val] of Object.entries(categoriesManifest)) {
  if (configuredCategories.some(c => c.id === key)) continue;
  const count = Array.isArray(val) ? val.length : 0;
  if (count > 0) {
    const dictEntry = assetStructure.dictionary?.[key];
    const label = dictEntry?.label || formatDisplayName(key);
    const icon = dictEntry?.icon || '📦';
    console.log(`║  ${icon} ${label.padEnd(22)} ${String(count).padStart(4)} items 🆕 ║`);
  }
}

console.log('╚══════════════════════════════════════════════╝');
console.log(`\n✓ Output: ${outputJson}`);
