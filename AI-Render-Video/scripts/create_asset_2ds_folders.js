/**
 * create_asset_2ds_folders.js
 *
 * Automatically scaffolds the standard 2D asset hierarchy under asset_2ds/
 * for 2D Cutout Animation, Puppet Rigging, Parallax Maps, Props, and VFX.
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const root2dDir = path.resolve(__dirname, '../asset_2ds');

const FOLDER_STRUCTURE = [
  // Character Cutout Detail Parts
  'chi_tiet_nhan_vat/dau',
  'chi_tiet_nhan_vat/khuon_mat',
  'chi_tiet_nhan_vat/mat',
  'chi_tiet_nhan_vat/mat/trong_trang',
  'chi_tiet_nhan_vat/mat/trong_den_iris',
  'chi_tiet_nhan_vat/mat/diem_sang_mat',
  'chi_tiet_nhan_vat/mat/mi_mat',
  'chi_tiet_nhan_vat/mieng',
  'chi_tiet_nhan_vat/mui',
  'chi_tiet_nhan_vat/toc_truoc',
  'chi_tiet_nhan_vat/toc_sau',
  'chi_tiet_nhan_vat/than_co_ban',
  'chi_tiet_nhan_vat/canh_tay',
  'chi_tiet_nhan_vat/cang_tay',
  'chi_tiet_nhan_vat/ban_tay',
  'chi_tiet_nhan_vat/dui',
  'chi_tiet_nhan_vat/cang_chan',
  'chi_tiet_nhan_vat/trang_phuc',
  'chi_tiet_nhan_vat/vu_khi',
  'chi_tiet_nhan_vat/long_may',

  // 2D Characters & Animation Sequences (Tab 1.2 & Tab 1.3)
  'nhan_vat/_lap_rap',

  // Parallax Maps & Backgrounds
  'ban_do/bau_troi',
  'ban_do/hau_canh',
  'ban_do/trung_canh',
  'ban_do/tien_canh',
  'ban_do/vat_can',
  'ban_do/_custom_ban_do',

  // Props & Items
  'dao_cu/vu_khi',
  'dao_cu/thuc_an',
  'dao_cu/do_dac',
  'dao_cu/huyen_huyen',

  // VFX & Effects (GIF / PNG sequence)
  'hieu_ung/chem_kiem',
  'hieu_ung/chuong_khi',
  'hieu_ung/set_lua',
  'hieu_ung/thoi_tiet',

  // Audio & SFX
  'am_thanh/sfx_combat',
  'am_thanh/sfx_moi_truong',
  'am_thanh/sfx_hanh_dong',
];

console.log('====================================================');
console.log(' 🎨 FLOWMY - TỰ ĐỘNG KHỞI TẠO CÂY THƯ MỤC ASSET_2DS');
console.log('====================================================\n');

if (!fs.existsSync(root2dDir)) {
  fs.mkdirSync(root2dDir, { recursive: true });
}

let createdCount = 0;
for (const rel of FOLDER_STRUCTURE) {
  const fullPath = path.join(root2dDir, rel);
  if (!fs.existsSync(fullPath)) {
    fs.mkdirSync(fullPath, { recursive: true });
    createdCount++;
  }
  const gitkeep = path.join(fullPath, '.gitkeep');
  if (!fs.existsSync(gitkeep)) {
    fs.writeFileSync(gitkeep, `# Keep empty 2D folder in Git: ${rel}\n`, 'utf-8');
  }
}

// Create README.md in asset_2ds root
const readmePath = path.join(root2dDir, 'README.md');
const readmeContent = `# Thư Mục Tài Nguyên 2D - FlowMy 2D Studio

Thư mục này chứa toàn bộ tài nguyên ảnh 2D, sprite sheets, GIF và linh kiện cắt dán (2D cutout puppet parts) dùng cho:
- Lắp ráp nhân vật 2D nhiều lớp (Head, Face, Eyes, Mouth, Hair, Body, Limbs, Outfits, Weapons).
- Lắp ráp bản đồ 2D phân tầng Parallax (Bầu trời, Hậu cảnh, Trung cảnh, Tiền cảnh).
- Diễn hoạt hoạt ảnh phong cách Động Thái Mạn (Motion Comic / Cutout Animation).

## Cấu trúc thư mục:
- \`nhan_vat/\`: Các bộ phận cơ thể, tóc, mắt, miệng, trang phục, vũ khí và thư mục lưu nhân vật đã lắp ráp (\`_lap_rap/\`).
- \`ban_do/\`: Các lớp bản đồ parallax và thư mục lưu cấu hình bản đồ tùy chỉnh (\`_custom_ban_do/\`).
- \`dao_cu/\`: Đạo cụ cầm nắm, đồ đạc, thức ăn, vật phẩm tu tiên.
- \`hieu_ung/\`: Hiệu ứng kỹ năng, vệt chém kiếm (Slash VFX), chưởng khí, mưa gió sấm chớp.
- \`am_thanh/\`: Hiệu ứng âm thanh SFX chiến đấu, môi trường, hành động.
`;

fs.writeFileSync(readmePath, readmeContent, 'utf-8');

console.log(`✓ Đã tạo/kiểm tra ${FOLDER_STRUCTURE.length} thư mục chuẩn trong asset_2ds/ (Mới tạo: ${createdCount})`);
console.log('✓ Đã cập nhật README.md cho asset_2ds/\n');
