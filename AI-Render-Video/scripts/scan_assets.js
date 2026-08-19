import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '../assets');

const outputMdEn = path.join(rootDir, 'ASSET_CATALOG.md');
const outputMdVi = path.join(rootDir, 'ASSET_CATALOG_VI.md');
const outputJson = path.join(rootDir, 'asset_manifest.json');

const modelExts = ['.vrm', '.glb', '.gltf'];
const audioExts = ['.mp3', '.wav', '.ogg'];
const animExts = ['.glb', '.bvh', '.fbx'];
const imageExts = ['.png', '.jpg', '.jpeg', '.webp'];

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
      results.push({
        id: path.parse(entry.name).name,
        name: entry.name,
        relPath: rel,
        format: path.extname(entry.name).replace('.', '').toUpperCase(),
        sizeMB: (stats.size / (1024 * 1024)).toFixed(2)
      });
    }
  }
  return results;
}

console.log('Scanning assets directory:', rootDir);

const baseBodies = getFiles(path.join(rootDir, 'characters/base_bodies'), modelExts);
const faces = getFiles(path.join(rootDir, 'characters/faces'), modelExts);
const hairstyles = getFiles(path.join(rootDir, 'characters/hairstyles'), modelExts);
const beards = getFiles(path.join(rootDir, 'characters/beards'), modelExts);
const costumes = getFiles(path.join(rootDir, 'characters/costumes'), modelExts);
const accessories = getFiles(path.join(rootDir, 'characters/accessories'), modelExts);

// Legacy root characters
const rootCharFiles = fs.existsSync(path.join(rootDir, 'characters'))
  ? fs.readdirSync(path.join(rootDir, 'characters'), { withFileTypes: true })
      .filter(e => !e.isDirectory() && modelExts.includes(path.extname(e.name).toLowerCase()))
      .map(e => {
        const full = path.join(rootDir, 'characters', e.name);
        return {
          id: path.parse(e.name).name,
          name: e.name,
          relPath: `characters/${e.name}`,
          format: path.extname(e.name).replace('.', '').toUpperCase(),
          sizeMB: (fs.statSync(full).size / (1024 * 1024)).toFixed(2)
        };
      })
  : [];

const weapons = getFiles(path.join(rootDir, 'props/weapons'), modelExts);
const tools = getFiles(path.join(rootDir, 'props/tools'), modelExts);
const consumables = getFiles(path.join(rootDir, 'props/consumables'), modelExts);
const furniture = getFiles(path.join(rootDir, 'props/furniture'), modelExts);
const buildings = getFiles(path.join(rootDir, 'props/buildings'), modelExts);
const nature = getFiles(path.join(rootDir, 'props/nature'), modelExts);
const vehicles = getFiles(path.join(rootDir, 'props/vehicles'), modelExts);

const rootProps = fs.existsSync(path.join(rootDir, 'props'))
  ? fs.readdirSync(path.join(rootDir, 'props'), { withFileTypes: true })
      .filter(e => !e.isDirectory() && modelExts.includes(path.extname(e.name).toLowerCase()))
      .map(e => {
        const full = path.join(rootDir, 'props', e.name);
        return {
          id: path.parse(e.name).name,
          name: e.name,
          relPath: `props/${e.name}`,
          format: path.extname(e.name).replace('.', '').toUpperCase(),
          sizeMB: (fs.statSync(full).size / (1024 * 1024)).toFixed(2)
        };
      })
  : [];

const maps = getFiles(path.join(rootDir, 'maps'), modelExts);

const bgm = getFiles(path.join(rootDir, 'audio/bgm'), audioExts);
const sfxCombat = getFiles(path.join(rootDir, 'audio/sfx/combat'), audioExts);
const sfxInteract = getFiles(path.join(rootDir, 'audio/sfx/interaction'), audioExts);
const sfxAmbient = getFiles(path.join(rootDir, 'audio/sfx/ambient'), audioExts);

const animCombat = getFiles(path.join(rootDir, 'animations/combat'), animExts);
const animInteract = getFiles(path.join(rootDir, 'animations/interaction'), animExts);
const animXianxia = getFiles(path.join(rootDir, 'animations/xianxia'), animExts);
const animLocomotion = getFiles(path.join(rootDir, 'animations/locomotion'), animExts);

const vfx = getFiles(path.join(rootDir, 'vfx'), imageExts);

const allAssets = [
  ...baseBodies, ...faces, ...hairstyles, ...beards, ...costumes, ...accessories, ...rootCharFiles,
  ...weapons, ...tools, ...consumables, ...furniture, ...buildings, ...nature, ...vehicles, ...rootProps,
  ...maps, ...bgm, ...sfxCombat, ...sfxInteract, ...sfxAmbient,
  ...animCombat, ...animInteract, ...animXianxia, ...animLocomotion, ...vfx
];

const totalSize = allAssets.reduce((sum, a) => sum + parseFloat(a.sizeMB), 0).toFixed(1);
const timestamp = new Date().toISOString().replace('T', ' ').slice(0, 19);

function makeTable(items, fallback) {
  if (!items || items.length === 0) return `*${fallback}*\n`;
  let table = '| ID | Path | Format | Size |\n|:---|:---|:---|---:|\n';
  for (const item of items) {
    table += `| \`${item.id}\` | \`${item.relPath}\` | ${item.format} | ${item.sizeMB} MB |\n`;
  }
  return table;
}

// ----------------------------------------------------
// 1. GENERATE ASSET_CATALOG.md (English for AI)
// ----------------------------------------------------
const mdEn = `# ASSET CATALOG — AI 3D Animation Studio

> **FOR AI AGENTS:** This file is the single source of truth for all available scene resources. Read this file carefully before generating JSON \`MasterSceneConfig\`.
> **Language Rule:** AI agents must read \`ASSET_CATALOG.md\` (English). Do not rely on \`_VI.md\` files which are formatted for human users.
> **Auto-generated:** ${timestamp}
> **Total assets:** ${allAssets.length} files, ${totalSize} MB

---

## 1. AI Guidelines for Scene JSON Generation

### Step 1: Map Selection
Reference an available environment model via \`environment.map\`:
\`\`\`json
"environment": {
  "map": "medieval_fantasy_book",
  "sky_time": "sunset",
  "weather": { "fog": 0.015, "wind": 0.3 }
}
\`\`\`

### Step 2: Modular Character Assembly
Characters can be composed from modular parts using the \`assembly\` object:
\`\`\`json
{
  "id": "actor_cultivator",
  "name": "Master Li",
  "model": "characters/sample_avatar.vrm",
  "assembly": {
    "base_body": "characters/base_bodies/male_warrior.vrm",
    "face": "characters/faces/face_male_young.glb",
    "hairstyle": "characters/hairstyles/hair_topknot.glb",
    "costume": "characters/costumes/costume_xianxia_white.glb",
    "accessories": ["characters/accessories/acc_headband.glb"],
    "skin_color": "#ffd1b3",
    "hair_color": "#1a1a2e"
  },
  "spawn_point": [0, 0, 0]
}
\`\`\`
*Backward Compatibility:* If modular parts are not available, specify \`model: "characters/sample_avatar.vrm"\` directly.

### Step 3: Animation & Expression Selection
Use exact IDs from the tables below:
- **Movement Action:** \`idle\`, \`walk\`, \`run\`, \`fly_to\`, \`arms_crossed\`, \`hands_behind_back\`, \`meditate\`
- **Facial Expression:** \`neutral\`, \`angry\`, \`pain\`, \`smile\`, \`cold\`, \`arrogant\`, \`contempt\`, \`wise\`, \`fierce\`, \`meditative\`
- **Combat & Interactions:** \`combat_actions\`, \`combat_master\`, \`object_interactions\`, \`transformations\`

---

## 2. Available Asset Catalog

### Characters — Base Bodies
${makeTable([...baseBodies, ...rootCharFiles], 'No base body assets found. Add .vrm/.glb to characters/base_bodies/')}

### Characters — Faces
${makeTable(faces, 'No face assets found. Add .glb to characters/faces/')}

### Characters — Hairstyles
${makeTable(hairstyles, 'No hairstyle assets found. Add .glb to characters/hairstyles/')}

### Characters — Beards
${makeTable(beards, 'No beard assets found. Add .glb to characters/beards/')}

### Characters — Costumes
${makeTable(costumes, 'No costume assets found. Add .glb to characters/costumes/')}

### Characters — Accessories
${makeTable(accessories, 'No accessory assets found. Add .glb to characters/accessories/')}

### Props — Weapons
${makeTable(weapons, 'No weapon assets found. Add .glb to props/weapons/')}

### Props — Tools
${makeTable(tools, 'No tool assets found. Add .glb to props/tools/')}

### Props — Consumables
${makeTable(consumables, 'No consumable assets found. Add .glb to props/consumables/')}

### Props — Furniture
${makeTable(furniture, 'No furniture assets found. Add .glb to props/furniture/')}

### Props — Buildings
${makeTable(buildings, 'No building assets found. Add .glb to props/buildings/')}

### Props — Nature
${makeTable(nature, 'No nature assets found. Add .glb to props/nature/')}

### Props — Vehicles
${makeTable(vehicles, 'No vehicle assets found. Add .glb to props/vehicles/')}

### Props — Legacy Root
${makeTable(rootProps, 'No legacy root props.')}

### Maps & Environments
${makeTable(maps, 'No map assets found. Add .glb/.gltf to maps/')}

### Audio — Background Music (BGM)
${makeTable(bgm, 'No BGM tracks found.')}

### Audio — Combat Sound Effects
${makeTable(sfxCombat, 'No combat sound effects found.')}

### Audio — Interaction Sound Effects
${makeTable(sfxInteract, 'No interaction sound effects found.')}

### Audio — Ambient Sounds
${makeTable(sfxAmbient, 'No ambient sounds found.')}

### Animations — Combat
${makeTable(animCombat, 'Procedural animations active. Optional mocap clips in animations/combat/')}

### Animations — Interactions
${makeTable(animInteract, 'Procedural animations active. Optional mocap clips in animations/interaction/')}

### Animations — Xianxia Cultivation
${makeTable(animXianxia, 'Procedural poses active via XianxiaPoseLibrary. Optional clips in animations/xianxia/')}

### Animations — Locomotion
${makeTable(animLocomotion, 'Procedural locomotion active. Optional clips in animations/locomotion/')}

### VFX Textures
${makeTable(vfx, 'Procedural VFX shaders active.')}

---

## 3. Supported Actions & Expressions Reference

### Locomotion & Body Actions (40 Actions)
- **Basic:** \`idle\`, \`walk\`, \`run\`, \`sit\`, \`climb\`
- **Advanced Locomotion:** \`fly_to\`, \`dash_to\`, \`teleport\`, \`kneel\`, \`bow\`, \`meditate\`
- **Combat:** \`heavy_slash_combo\`, \`fast_slash\`, \`magic_blast\`, \`punch_kick\`, \`fly_back_knockdown\`, \`stagger_back\`, \`block_defend\`, \`dodge\`
- **Xianxia Poses:** \`arms_crossed\`, \`hands_behind_back\`, \`fist_salute\`, \`finger_spell\`, \`power_charge\`, \`flying_stance\`
- **Object Interactions:** \`pickup_right\`, \`carry_two_hands\`, \`drink\`, \`pour\`, \`dig\`, \`water_plants\`, \`plant_seed\`, \`harvest\`, \`wave\`, \`dance\`, \`throw\`

### Facial Expressions (21 Expressions)
- **Standard:** \`neutral\`, \`angry\`, \`pain\`, \`smile\`, \`smirk\`, \`sad\`, \`serious\`, \`surprised\`, \`shock\`
- **Xianxia Dramatic:** \`cold\`, \`arrogant\`, \`contempt\`, \`wise\`, \`fierce\`, \`meditative\`, \`menacing\`, \`compassionate\`, \`determined\`
`;

// ----------------------------------------------------
// 2. GENERATE ASSET_CATALOG_VI.md (Tiếng Việt có dấu cho User)
// ----------------------------------------------------
const mdVi = `# 📦 DANH MỤC TÀI NGUYÊN (ASSET CATALOG) — AI 3D Animation Studio

> **DÀNH CHO NGƯỜI DÙNG:** File tài liệu Tiếng Việt có dấu giúp bạn dễ dàng theo dõi toàn bộ tài nguyên hiện có trong dự án.
> **Quy định AI:** AI chỉ đọc file \`ASSET_CATALOG.md\` (tiếng Anh). File \`_VI.md\` này chỉ phục vụ người dùng.
> **Thời gian quét:** ${timestamp}
> **Tổng tài nguyên:** ${allAssets.length} tệp tin, ${totalSize} MB

---

## 1. Hướng Dẫn Soạn Kịch Bản Scene JSON Cho Người Dùng

### Bước 1: Chọn Bản Đồ Bối Cảnh (Map)
Khai báo trường \`environment.map\` trỏ tới model trong thư mục \`maps/\`.

### Bước 2: Lắp Ráp Ngoại Hình Nhân Vật (Modular Assembly)
Bạn có thể tự do kết hợp khuôn mặt, mái tóc, trang phục, râu và phụ kiện cho từng nhân vật bằng khối \`assembly\`:
\`\`\`json
{
  "id": "actor_cultivator",
  "name": "Lý Tiên Sinh",
  "model": "characters/sample_avatar.vrm",
  "assembly": {
    "base_body": "characters/base_bodies/male_warrior.vrm",
    "face": "characters/faces/face_male_young.glb",
    "hairstyle": "characters/hairstyles/hair_topknot.glb",
    "costume": "characters/costumes/costume_xianxia_white.glb",
    "accessories": ["characters/accessories/acc_headband.glb"],
    "skin_color": "#ffd1b3",
    "hair_color": "#1a1a2e"
  },
  "spawn_point": [0, 0, 0]
}
\`\`\`

### Bước 3: Diễn Hoạt Hoạt Ảnh & Biểu Cảm
- **Hành động cơ thể:** \`idle\` (đứng thở), \`walk\` (đi bộ), \`run\` (chạy), \`arms_crossed\` (khoanh tay), \`hands_behind_back\` (chắp tay sau lưng), \`meditate\` (ngồi thiền), \`fly_to\` (ngự kiếm bay)...
- **Biểu cảm khuôn mặt:** \`cold\` (lạnh lùng), \`arrogant\` (kiêu ngạo), \`contempt\` (khinh thường), \`wise\` (uyên bác), \`fierce\` (hung dữ sát khí), \`meditative\` (thiền định thanh tịnh)...

---

## 2. Bảng Danh Mục Tài Nguyên Chi Tiết

### 👤 Nhân Vật — Thân Hình Cơ Bản (Base Bodies)
${makeTable([...baseBodies, ...rootCharFiles], 'Chưa có thân hình cơ bản. Thả tệp .vrm/.glb vào characters/base_bodies/')}

### 👤 Nhân Vật — Khuôn Mặt (Faces)
${makeTable(faces, 'Chưa có khuôn mặt rời. Thả tệp .glb vào characters/faces/')}

### 👤 Nhân Vật — Kiểu Tóc (Hairstyles)
${makeTable(hairstyles, 'Chưa có kiểu tóc. Thả tệp .glb vào characters/hairstyles/')}

### 👤 Nhân Vật — Kiểu Râu (Beards)
${makeTable(beards, 'Chưa có kiểu râu. Thả tệp .glb vào characters/beards/')}

### 👤 Nhân Vật — Trang Phục (Costumes)
${makeTable(costumes, 'Chưa có trang phục. Thả tệp .glb vào characters/costumes/')}

### 👤 Nhân Vật — Phụ Kiện (Accessories)
${makeTable(accessories, 'Chưa có phụ kiện. Thả tệp .glb vào characters/accessories/')}

### ⚔️ Đạo Cụ — Vũ Khí (Weapons)
${makeTable(weapons, 'Chưa có vũ khí. Thả tệp .glb vào props/weapons/')}

### 🔧 Đạo Cụ — Dụng Cụ (Tools)
${makeTable(tools, 'Chưa có dụng cụ tương tác. Thả tệp .glb vào props/tools/')}

### 🍵 Đạo Cụ — Đồ Tiêu Hao (Consumables)
${makeTable(consumables, 'Chưa có đồ tiêu hao. Thả tệp .glb vào props/consumables/')}

### 🪑 Đạo Cụ — Nội Thất (Furniture)
${makeTable(furniture, 'Chưa có đồ nội thất. Thả tệp .glb vào props/furniture/')}

### 🏠 Đạo Cụ — Công Trình (Buildings)
${makeTable(buildings, 'Chưa có công trình xây dựng. Thả tệp .glb vào props/buildings/')}

### 🌳 Đạo Cụ — Thiên Nhiên (Nature)
${makeTable(nature, 'Chưa có cây cối, đá cảnh. Thả tệp .glb vào props/nature/')}

### 🐴 Đạo Cụ — Phương Tiện & Thú Cưỡi (Vehicles)
${makeTable(vehicles, 'Chưa có thú cưỡi/kiếm bay. Thả tệp .glb vào props/vehicles/')}

### 🪑 Đạo Cụ — Thư Mục Gốc Cũ (Legacy Props)
${makeTable(rootProps, 'Không có đạo cụ ở thư mục gốc.')}

### 🗺️ Bản Đồ Bối Cảnh (Maps)
${makeTable(maps, 'Chưa có bản đồ. Thả tệp .glb/.gltf vào maps/')}

### 🎵 Âm Thanh — Nhạc Nền (BGM)
${makeTable(bgm, 'Chưa có bản nhạc nền nào.')}

### ⚔️ Âm Thanh — Hiệu Ứng Chiến Đấu (Combat SFX)
${makeTable(sfxCombat, 'Chưa có âm thanh chiến đấu.')}

### 🔔 Âm Thanh — Hiệu Ứng Tương Tác (Interaction SFX)
${makeTable(sfxInteract, 'Chưa có âm thanh tương tác.')}

### 🌧️ Âm Thanh — Hiệu Ứng Môi Trường (Ambient SFX)
${makeTable(sfxAmbient, 'Chưa có âm thanh môi trường.')}

### 🎬 Hoạt Ảnh — Chiến Đấu (Combat Animations)
${makeTable(animCombat, 'Đang sử dụng hệ thống diễn hoạt procedural nội tại.')}

### 🎬 Hoạt Ảnh — Tương Tác (Interaction Animations)
${makeTable(animInteract, 'Đang sử dụng hệ thống diễn hoạt procedural nội tại.')}

### 🎬 Hoạt Ảnh — Tiên Hiệp (Xianxia Poses)
${makeTable(animXianxia, 'Hệ thống XianxiaPoseLibrary 13 tư thế đang kích hoạt.')}

### 🎬 Hoạt Ảnh — Di Chuyển (Locomotion)
${makeTable(animLocomotion, 'Đang sử dụng hệ thống di chuyển nội tại.')}

### ✨ Hiệu Ứng Hình Ảnh (VFX Textures)
${makeTable(vfx, 'Shader hiệu ứng hạt nội tại đang kích hoạt.')}

---

## 3. Bảng Tra Cứu Hành Động & Biểu Cảm Hỗ Trợ

### 🏃 Hành Động Cơ Thể (40 Hành động)
- **Cơ bản:** \`idle\` (đứng thở), \`walk\` (đi bộ), \`run\` (chạy), \`sit\` (ngồi), \`climb\` (trèo)
- **Nâng cao:** \`fly_to\` (bay lượn), \`dash_to\` (lướt nhanh), \`teleport\` (dịch chuyển), \`kneel\` (quỳ), \`bow\` (cúi chào), \`meditate\` (ngồi thiền)
- **Chiến đấu:** \`heavy_slash_combo\` (chém combo), \`fast_slash\` (chém nhanh), \`magic_blast\` (chưởng phép), \`punch_kick\` (đấm đá), \`fly_back_knockdown\` (bị đánh văng ngã), \`stagger_back\` (loạng choạng), \`block_defend\` (đỡ đòn), \`dodge\` (né tránh)
- **Tư thế Tiên Hiệp:** \`arms_crossed\` (khoanh tay), \`hands_behind_back\` (chắp tay sau lưng), \`fist_salute\` (bao quyền bái lễ), \`finger_spell\` (bắt ấn quyết), \`power_charge\` (vận công tụ khí), \`flying_stance\` (tư thế ngự không)
- **Tương tác Đời Sống:** \`pickup_right\` (nhặt đồ), \`carry_two_hands\` (bưng bê 2 tay), \`drink\` (uống nước/rượu), \`pour\` (rót nước), \`dig\` (cuốc đất), \`water_plants\` (tưới cây), \`plant_seed\` (gieo hạt), \`harvest\` (thu hoạch), \`wave\` (vẫy tay), \`dance\` (nhảy múa), \`throw\` (ném đồ)

### 🎭 Biểu Cảm Khuôn Mặt (21 Biểu cảm)
- **Cơ bản:** \`neutral\` (bình thường), \`angry\` (tức giận), \`pain\` (đau đớn), \`smile\` (mỉm cười), \`smirk\` (cười nhếch mép), \`sad\` (buồn bã), \`serious\` (nghiêm túc), \`surprised\` (ngạc nhiên), \`shock\` (sốc/sửng sốt)
- **Tiên Hiệp & Truyền Kỳ:** \`cold\` (lạnh lùng sắc bén), \`arrogant\` (kiêu ngạo ngút trời), \`contempt\` (khinh thường coi rẻ), \`wise\` (uyên bác thấu hiểu), \`fierce\` (hung bạo sát khí), \`meditative\` (thiền định an yên), \`menacing\` (nham hiểm hiểm độc), \`compassionate\` (từ bi nhân hậu), \`determined\` (kiên định quyết tâm)
`;

// ----------------------------------------------------
// 3. GENERATE asset_manifest.json
// ----------------------------------------------------
const manifest = {
  generated_at: timestamp,
  total_files: allAssets.length,
  total_size_mb: parseFloat(totalSize),
  characters: {
    base_bodies: [...baseBodies, ...rootCharFiles],
    faces,
    hairstyles,
    beards,
    costumes,
    accessories
  },
  props: {
    weapons,
    tools,
    consumables,
    furniture,
    buildings,
    nature,
    vehicles,
    legacy: rootProps
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
  vfx,
  available_actions: [
    "idle","walk","run","sit","climb",
    "fly_to","dash_to","teleport","kneel","bow","meditate",
    "heavy_slash_combo","fast_slash","magic_blast","punch_kick",
    "fly_back_knockdown","stagger_back","block_defend","dodge",
    "arms_crossed","hands_behind_back","fist_salute","finger_spell",
    "power_charge","flying_stance",
    "pickup_right","carry_two_hands","drink","pour","dig",
    "water_plants","plant_seed","harvest","wave","dance","throw"
  ],
  available_expressions: [
    "neutral","angry","pain","smile","smirk","sad","serious","surprised","shock",
    "cold","arrogant","contempt","wise","fierce",
    "meditative","menacing","compassionate","determined"
  ]
};

fs.writeFileSync(outputMdEn, mdEn, 'utf-8');
console.log('✓ Generated ASSET_CATALOG.md (English for AI)');

fs.writeFileSync(outputMdVi, mdVi, 'utf-8');
console.log('✓ Generated ASSET_CATALOG_VI.md (Tiếng Việt cho User)');

fs.writeFileSync(outputJson, JSON.stringify(manifest, null, 2), 'utf-8');
console.log('✓ Generated asset_manifest.json');
