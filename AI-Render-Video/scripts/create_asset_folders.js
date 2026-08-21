/**
 * create_asset_folders.js
 *
 * Automatically creates the clean standard asset folder hierarchy on disk
 * based on assets/asset_structure.json.
 * 
 * Usage:
 *   node create_asset_folders.js [vi | en | both | --clean]
 * 
 * - 'vi' (default): Creates ONLY 1 single canonical Vietnamese folder per category (e.g. assets/nhan_vat/than_co_ban).
 * - 'en': Creates ONLY 1 single canonical English folder per category (e.g. assets/characters/base_bodies).
 * - 'both': Creates both canonical Vietnamese and English root trees.
 * - '--clean': Removes empty duplicate alias folders that only contain .gitkeep.
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '../assets');
const structureJsonPath = path.join(rootDir, 'asset_structure.json');

// Parse CLI arguments
const args = process.argv.slice(2);
let mode = 'vi'; // default: Vietnamese canonical folders
let cleanAliases = false;

for (const arg of args) {
  const lower = arg.toLowerCase().replace(/^--?/, '');
  if (lower === 'vi' || lower === 'vietnamese' || lower === 'lang=vi') mode = 'vi';
  else if (lower === 'en' || lower === 'english' || lower === 'lang=en') mode = 'en';
  else if (lower === 'both' || lower === 'all' || lower === 'lang=both') mode = 'both';
  else if (lower === 'clean' || lower === 'clean-empty' || lower === 'clean_empty') cleanAliases = true;
}

console.log('====================================================');
console.log(' 📁 FLOWMY - TỰ ĐỘNG KHỞI TẠO CÂY THƯ MỤC ASSETS');
console.log(` ⚙️ Chế độ tạo thư mục: [ ${mode.toUpperCase()} ] (1 thư mục chuẩn duy nhất/mục)`);
console.log('====================================================\n');

if (!fs.existsSync(rootDir)) {
  fs.mkdirSync(rootDir, { recursive: true });
}

let structure = null;
if (fs.existsSync(structureJsonPath)) {
  try {
    structure = JSON.parse(fs.readFileSync(structureJsonPath, 'utf-8'));
    console.log('✓ Đã nạp cấu hình từ asset_structure.json\n');
  } catch (err) {
    console.warn('Lỗi đọc asset_structure.json:', err.message);
  }
}

const createdFolders = [];
const allValidCanonicalFolders = new Set();

function ensureDir(relPath) {
  const fullPath = path.join(rootDir, relPath);
  allValidCanonicalFolders.add(path.normalize(fullPath).toLowerCase());
  if (!fs.existsSync(fullPath)) {
    fs.mkdirSync(fullPath, { recursive: true });
    createdFolders.push(relPath);
  }
  // Create .gitkeep so git tracks empty folders
  const gitkeepPath = path.join(fullPath, '.gitkeep');
  if (!fs.existsSync(gitkeepPath)) {
    fs.writeFileSync(gitkeepPath, '# Keep empty asset folder in Git\n', 'utf-8');
  }
}

// ─── 1. CHẾ ĐỘ TIẾNG VIỆT (assets/nhan_vat, ban_do, dao_cu, bau_troi, hieu_ung) ───
if (mode === 'vi' || mode === 'both') {
  console.log('📂 Đang tạo cây thư mục Tiếng Việt chuẩn...');

  // Characters (nhan_vat)
  ensureDir('nhan_vat/_lap_rap');
  const charCats = structure?.character_structure?.categories || [];
  for (const cat of charCats) {
    if (cat.id?.startsWith('_')) continue;
    // Canonical folder name (default: cat.folder or cat.id)
    const folderName = cat.folder || cat.id;
    const catFolder = `nhan_vat/${folderName}`;
    ensureDir(catFolder);
    if (cat.supports_gender) {
      ensureDir(`${catFolder}/nam`);
      ensureDir(`${catFolder}/nu`);
      ensureDir(`${catFolder}/chung`);
    }
  }

  // Maps (ban_do)
  ensureDir('ban_do/presets');
  ensureDir('ban_do/_custom_ban_do');

  // Props & World (dao_cu)
  const propCats = structure?.world_and_props_structure?.categories || [];
  for (const cat of propCats) {
    if (cat.id === 'ban_do' || cat.id === 'bau_troi' || cat.id === 'hieu_ung') continue;
    const folderName = cat.folder || cat.id;
    const catFolder = `dao_cu/${folderName}`;
    ensureDir(catFolder);
    for (const sub of cat.subcategories || []) {
      const subName = sub.folder || sub.id;
      ensureDir(`${catFolder}/${subName}`);
    }
  }

  // Skyboxes (bau_troi)
  const skyTimes = ['binh_minh', 'buoi_sang', 'buoi_trua', 'buoi_chieu', 'buoi_toi', 'giong_bao'];
  for (const time of skyTimes) {
    ensureDir(`bau_troi/${time}`);
  }

  // VFX (hieu_ung)
  ensureDir('hieu_ung/cam_xuc');
  ensureDir('hieu_ung/bao_phu');
}

// ─── 2. CHẾ ĐỘ TIẾNG ANH (assets/characters, maps, props, SkyBoxs, vfx) ───
if (mode === 'en' || mode === 'both') {
  console.log('📂 Đang tạo cây thư mục Tiếng Anh chuẩn...');

  const enCharMap = {
    than_co_ban: 'base_bodies',
    trang_phuc: 'costumes',
    khuon_mat: 'faces',
    kieu_toc: 'hairstyles',
    long_may: 'eyebrows',
    mat: 'eyes',
    mui: 'noses',
    mieng: 'mouths',
    mu_non: 'hats',
    giay_dep: 'shoes',
    phu_kien: 'accessories',
    kieu_rau: 'beards',
    canh: 'wings',
    duoi: 'tails',
  };

  ensureDir('characters/_lap_rap');
  const charCats = structure?.character_structure?.categories || [];
  for (const cat of charCats) {
    if (cat.id?.startsWith('_')) continue;
    const enFolder = enCharMap[cat.id] || cat.folder || cat.id;
    const catFolder = `characters/${enFolder}`;
    ensureDir(catFolder);
    if (cat.supports_gender) {
      ensureDir(`${catFolder}/nam`);
      ensureDir(`${catFolder}/nu`);
      ensureDir(`${catFolder}/chung`);
    }
  }

  // Maps
  ensureDir('maps/presets');
  ensureDir('maps/_custom_ban_do');

  // Props
  const enPropMap = {
    cay_coi: 'trees',
    da_dia_hinh: 'rocks',
    dong_vat: 'animals',
    cong_trinh: 'buildings',
    noi_that: 'furniture',
    dung_cu: 'tools',
    do_tieu_hao: 'consumables',
    vu_khi: 'weapons',
    phuong_tien: 'vehicles',
  };

  const propCats = structure?.world_and_props_structure?.categories || [];
  for (const cat of propCats) {
    if (cat.id === 'ban_do' || cat.id === 'bau_troi' || cat.id === 'hieu_ung') continue;
    const enFolder = enPropMap[cat.id] || cat.folder || cat.id;
    const catFolder = `props/${enFolder}`;
    ensureDir(catFolder);
    for (const sub of cat.subcategories || []) {
      const subName = sub.folder || sub.id;
      ensureDir(`${catFolder}/${subName}`);
    }
  }

  // SkyBoxs
  const skyTimes = ['binh_minh', 'buoi_sang', 'buoi_trua', 'buoi_chieu', 'buoi_toi', 'giong_bao'];
  for (const time of skyTimes) {
    ensureDir(`SkyBoxs/${time}`);
  }

  // VFX
  ensureDir('vfx/cam_xuc');
  ensureDir('vfx/bao_phu');
}

// ─── 3. COMMON AUDIO & ANIMATIONS ───
ensureDir('audio/bgm');
ensureDir('audio/sfx/combat');
ensureDir('audio/sfx/interactions');
ensureDir('audio/sfx/ambient');
ensureDir('animations/combat');
ensureDir('animations/interactions');
ensureDir('animations/xianxia');
ensureDir('animations/locomotion');

// ─── 4. DỌN DẸP CÁC THƯ MỤC BÍ DANH BỊ TRỐNG (NẾU ĐƯỢC YÊU CẦU) ───
let cleanedCount = 0;
function cleanEmptyAliasDirs(dir) {
  if (!fs.existsSync(dir)) return;
  const entries = fs.readdirSync(dir, { withFileTypes: true });

  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      cleanEmptyAliasDirs(fullPath);

      // Check if this directory is empty or only has .gitkeep and is NOT a valid canonical folder
      const subEntries = fs.readdirSync(fullPath);
      const isRedundantAlias = !allValidCanonicalFolders.has(path.normalize(fullPath).toLowerCase());
      const hasOnlyGitKeep = subEntries.length === 0 || (subEntries.length === 1 && subEntries[0] === '.gitkeep');

      if (isRedundantAlias && hasOnlyGitKeep) {
        try {
          if (subEntries.includes('.gitkeep')) {
            fs.unlinkSync(path.join(fullPath, '.gitkeep'));
          }
          fs.rmdirSync(fullPath);
          cleanedCount++;
        } catch (e) {
          // Ignore removal errors
        }
      }
    }
  }
}

if (cleanAliases || args.includes('--clean')) {
  console.log('\n🧹 Đang dọn dẹp các thư mục bí danh trùng lặp bị trống...');
  cleanEmptyAliasDirs(path.join(rootDir, 'nhan_vat'));
  cleanEmptyAliasDirs(path.join(rootDir, 'dao_cu'));
  cleanEmptyAliasDirs(path.join(rootDir, 'ban_do'));
  if (mode === 'vi') {
    cleanEmptyAliasDirs(path.join(rootDir, 'characters'));
    cleanEmptyAliasDirs(path.join(rootDir, 'props'));
    cleanEmptyAliasDirs(path.join(rootDir, 'maps'));
  }
  if (cleanedCount > 0) {
    console.log(`✓ Đã dọn dẹp ${cleanedCount} thư mục trùng lặp không sử dụng.`);
  }
}

console.log(`\n✅ Hoàn tất! Đã khởi tạo ${createdFolders.length} thư mục mới trên ổ đĩa.`);
console.log('✅ Mỗi danh mục chỉ tạo đúng 1 thư mục chuẩn duy nhất, không tạo thừa bí danh.');
console.log('====================================================\n');
