/**
 * create_asset_folders.js
 *
 * Automatically creates the entire standard Vietnamese asset folder hierarchy on disk
 * based on assets/asset_structure.json and adds .gitkeep files so Git tracks the structure
 * without committing large 3D/audio binary files.
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '../assets');
const structureJsonPath = path.join(rootDir, 'asset_structure.json');

console.log('====================================================');
console.log(' 📁 FLOWMY - TỰ ĐỘNG KHỞI TẠO CÂY THƯ MỤC ASSETS');
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

function ensureDir(relPath) {
  const fullPath = path.join(rootDir, relPath);
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

// 1. Character Categories & Gender Folders
const charCats = structure?.character_structure?.categories || [];
for (const cat of charCats) {
  const catFolder = `nhan_vat/${cat.folder || cat.id}`;
  ensureDir(catFolder);
  if (cat.supports_gender) {
    ensureDir(`${catFolder}/nam`);
    ensureDir(`${catFolder}/nu`);
    ensureDir(`${catFolder}/chung`);
  }
}

// 2. Maps & Presets & Custom Maps
ensureDir('ban_do');
ensureDir('ban_do/presets');
ensureDir('ban_do/_custom_ban_do');
ensureDir('nhan_vat/_lap_rap');

// 3. World & Props
const propCats = structure?.world_and_props_structure?.categories || [];
for (const cat of propCats) {
  const catFolder = cat.id === 'ban_do' ? 'ban_do' : (cat.id === 'bau_troi' ? 'bau_troi' : (cat.id === 'hieu_ung' ? 'hieu_ung' : `dao_cu/${cat.folder || cat.id}`));
  ensureDir(catFolder);
  for (const sub of cat.subcategories || []) {
    ensureDir(`${catFolder}/${sub.folder || sub.id}`);
  }
}

// 4. Audio & Animations
ensureDir('audio/bgm');
ensureDir('audio/sfx/combat');
ensureDir('audio/sfx/interactions');
ensureDir('audio/sfx/ambient');
ensureDir('animations/combat');
ensureDir('animations/interactions');
ensureDir('animations/xianxia');
ensureDir('animations/locomotion');

console.log(`✅ Đã kiểm tra và khởi tạo ${createdFolders.length} thư mục mới trên ổ đĩa.`);
console.log('✅ Đã tạo tệp .gitkeep để Git luôn giữ lại cấu trúc thư mục khi pull về.');
console.log('\nDanh sách các thư mục chuẩn:');
console.log(' - assets/nhan_vat/ (than_co_ban, trang_phuc, khuon_mat, kieu_toc, long_may, mat, mui, mieng, mu_non, giay_dep, phu_kien, kieu_rau, canh, duoi)');
console.log(' - assets/ban_do/ (và ban_do/presets/)');
console.log(' - assets/dao_cu/ (cay_coi, da_dia_hinh, dong_vat/tren_can, duoi_nuoc, tren_troi, cong_trinh, noi_that, dung_cu, do_tieu_hao, vu_khi, phuong_tien)');
console.log(' - assets/bau_troi/ (binh_minh, buoi_sang, buoi_trua, buoi_chieu, buoi_toi, giong_bao)');
console.log(' - assets/hieu_ung/ (cam_xuc, bao_phu)');
console.log(' - assets/audio/ & assets/animations/');
console.log('\n====================================================\n');
