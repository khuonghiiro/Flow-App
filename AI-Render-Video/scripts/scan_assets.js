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
      const baseName = path.parse(entry.name).name;

      // Look for companion reference photo
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
        name: entry.name,
        relPath: rel,
        format: path.extname(entry.name).replace('.', '').toUpperCase(),
        sizeMB: (stats.size / (1024 * 1024)).toFixed(2),
        previewUrl: previewUrl ? `assets/${previewUrl}` : undefined
      });
    }
  }
  return results;
}

console.log('Scanning assets directory:', rootDir);

// Characters (Male, Female, Man, Woman, Base bodies, Parts)
const maleChars = [
  ...getFiles(path.join(rootDir, 'characters/male'), modelExts),
  ...getFiles(path.join(rootDir, 'characters/man'), modelExts),
];
// De-duplicate by id
const uniqueMaleChars = Array.from(new Map(maleChars.map(c => [c.id, c])).values());

const femaleChars = [
  ...getFiles(path.join(rootDir, 'characters/female'), modelExts),
  ...getFiles(path.join(rootDir, 'characters/woman'), modelExts),
];
const uniqueFemaleChars = Array.from(new Map(femaleChars.map(c => [c.id, c])).values());

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
        const baseName = path.parse(e.name).name;
        let previewUrl = '';
        for (const imgExt of ['.png', '.jpg', '.jpeg', '.webp']) {
          const candidateImg = path.join(rootDir, 'characters', `${baseName}${imgExt}`);
          if (fs.existsSync(candidateImg)) {
            previewUrl = `assets/characters/${baseName}${imgExt}`;
            break;
          }
        }
        return {
          id: baseName,
          name: e.name,
          relPath: `characters/${e.name}`,
          format: path.extname(e.name).replace('.', '').toUpperCase(),
          sizeMB: (fs.statSync(full).size / (1024 * 1024)).toFixed(2),
          previewUrl: previewUrl || undefined
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
const skyboxes = getFiles(path.join(rootDir, 'SkyBoxs'), imageExts);

// Scan Saved Map Presets (.json)
const mapPresetsFolder = path.join(rootDir, 'maps/presets');
const mapPresetFiles = fs.existsSync(mapPresetsFolder)
  ? fs.readdirSync(mapPresetsFolder).filter(f => f.endsWith('.json'))
  : [];

const mapPresets = mapPresetFiles.map(f => {
  const full = path.join(mapPresetsFolder, f);
  try {
    const data = JSON.parse(fs.readFileSync(full, 'utf-8'));
    return {
      map_id: data.map_id || path.parse(f).name,
      name: data.name || data.map_id,
      description: data.description || '',
      base_map: data.base_map || 'farming_village',
      sky_time: data.sky_time || 'sunset',
      weather: data.weather || { fog: 0.01, wind: 0.3 },
      default_spawn_points: data.default_spawn_points || {},
      placed_props: data.placed_props || [],
      tags: data.tags || [],
      relPath: `maps/presets/${f}`
    };
  } catch {
    return null;
  }
}).filter(Boolean);

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
  ...uniqueMaleChars, ...uniqueFemaleChars, ...baseBodies, ...faces, ...hairstyles, ...beards, ...costumes, ...accessories, ...rootCharFiles,
  ...weapons, ...tools, ...consumables, ...furniture, ...buildings, ...nature, ...vehicles, ...rootProps,
  ...maps, ...skyboxes, ...bgm, ...sfxCombat, ...sfxInteract, ...sfxAmbient,
  ...animCombat, ...animInteract, ...animXianxia, ...animLocomotion, ...vfx
];

const totalSize = allAssets.reduce((sum, a) => sum + parseFloat(a.sizeMB), 0).toFixed(1);
const timestamp = new Date().toISOString().replace('T', ' ').slice(0, 19);

function makeTable(items, fallback) {
  if (!items || items.length === 0) return `*${fallback}*\n`;
  let table = '| ID | Path | Format | Size | Ref Image |\n|:---|:---|:---|---:|:---|\n';
  for (const item of items) {
    const preview = item.previewUrl ? `\`${item.previewUrl}\`` : '—';
    table += `| \`${item.id}\` | \`${item.relPath}\` | ${item.format} | ${item.sizeMB} MB | ${preview} |\n`;
  }
  return table + '\n';
}

function makeTableVi(items, fallback) {
  if (!items || items.length === 0) return `*${fallback}*\n`;
  let table = '| Mã ID | Đường Dẫn (Path) | Định Dạng | Dung Lượng | Ảnh Tham Chiếu (Preview) |\n|:---|:---|:---|---:|:---|\n';
  for (const item of items) {
    const preview = item.previewUrl ? `\`${item.previewUrl}\`` : '—';
    table += `| \`${item.id}\` | \`${item.relPath}\` | ${item.format} | ${item.sizeMB} MB | ${preview} |\n`;
  }
  return table + '\n';
}

function makePresetsEn(presets) {
  if (!presets || presets.length === 0) return '*No saved map presets found.*\n';
  let md = '';
  for (const p of presets) {
    const spawns = Object.entries(p.default_spawn_points || {})
      .map(([name, pos]) => `  - \`"${name}"\`: [${pos.join(', ')}]`)
      .join('\n');
    const props = (p.placed_props || [])
      .map(pr => `  - \`${pr.id}\` (${pr.type || 'prop'}) at [${pr.position.join(', ')}] — Asset: \`${pr.asset_path}\`${pr.smart_socket ? ` (Socket: ${pr.smart_socket.socket_type})` : ''}`)
      .join('\n');

    md += `#### Preset ID: \`${p.map_id}\` — ${p.name}
- **File**: \`${p.relPath}\`
- **Description**: ${p.description || 'Custom map configuration.'}
- **Base Map**: \`${p.base_map}\` | **Default Sky/Weather**: ${p.sky_time}, Fog: ${p.weather?.fog ?? 0.01}
- **Named Spawn Points**:
${spawns || '  - None'}
- **Placed Objects & Interactables**:
${props || '  - None'}

`;
  }
  return md;
}

function makePresetsVi(presets) {
  if (!presets || presets.length === 0) return '*Chưa có bản đồ lưu sẵn.*\n';
  let md = '';
  for (const p of presets) {
    const spawns = Object.entries(p.default_spawn_points || {})
      .map(([name, pos]) => `  - Điểm xuất hiện \`"${name}"\`: [${pos.join(', ')}]`)
      .join('\n');
    const props = (p.placed_props || [])
      .map(pr => `  - \`${pr.id}\` (${pr.type || 'vật thể'}) tại [${pr.position.join(', ')}] — Model: \`${pr.asset_path}\`${pr.smart_socket ? ` (Tương tác: ${pr.smart_socket.socket_type})` : ''}`)
      .join('\n');

    md += `#### Mã Map: \`${p.map_id}\` — ${p.name}
- **Tệp cấu hình**: \`${p.relPath}\`
- **Mô tả bối cảnh**: ${p.description || 'Bản đồ tùy chỉnh.'}
- **Map nền**: \`${p.base_map}\` | **Bầu trời & Thời tiết**: ${p.sky_time}, Sương mù: ${p.weather?.fog ?? 0.01}
- **Các điểm xuất hiện (Spawn Points)**:
${spawns || '  - Chưa có điểm xuất hiện'}
- **Danh sách đồ vật & điểm tương tác**:
${props || '  - Không có đồ vật'}

`;
  }
  return md;
}

// ----------------------------------------------------
// 1. GENERATE ASSET_CATALOG.md (English for AI)
// ----------------------------------------------------
const mdEn = `# ASSET CATALOG — AI 3D Animation Studio

> **FOR AI AGENTS:** This file is the single source of truth for all available scene resources. Read this file carefully before generating JSON \`MasterSceneConfig\`.
> **Language Rule:** AI agents must read \`ASSET_CATALOG.md\` (English). Do not rely on \`_VI.md\` files which are formatted for human users.
> **Auto-generated:** ${timestamp}
> **Total assets:** ${allAssets.length} asset files (${totalSize} MB), ${mapPresets.length} saved map presets

---

## 1. AI Guidelines for Scene JSON Generation

### Step 1: Map Selection & Saved Map Presets
You can use a raw map file OR reference a **Saved Map Preset** directly:

**Option A: Using a Saved Map Preset (Recommended when user asks for a saved map):**
\`\`\`json
"environment": {
  "map": "farming_village",
  "map_preset": "sakura_lake_village",
  "sky_time": "sunset",
  "weather": { "fog": 0.012, "wind": 0.35 }
}
\`\`\`
*Benefit:* When using a map preset, you can place actors at named spawn points (e.g. \`[-3.5, 0, -1.8]\` at lakeside bench) and interact with preset props (e.g. \`"props.stone_bench_01"\` or \`"props.sakura_tree_01"\`).

**Option B: Standard Raw Map:**
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
  "spawn_point": [-3.5, 0, -1.8]
}
\`\`\`
*Backward Compatibility:* If modular parts are not available, specify \`model: "characters/sample_avatar.vrm"\` directly.

### Step 3: Animation & Expression Selection
Use exact IDs from the tables below:
- **Movement Action:** \`idle\`, \`walk\`, \`run\`, \`fly_to\`, \`arms_crossed\`, \`hands_behind_back\`, \`meditate\`
- **Facial Expression:** \`neutral\`, \`angry\`, \`pain\`, \`smile\`, \`cold\`, \`arrogant\`, \`contempt\`, \`wise\`, \`fierce\`, \`meditative\`
- **Combat & Interactions:** \`combat_actions\`, \`combat_master\`, \`object_interactions\`, \`transformations\`

---

## 2. Saved Map Presets (Configured Environments)

${makePresetsEn(mapPresets)}

---

## 3. Available Raw Asset Catalog

### Characters — Male (Nam)
${makeTable(uniqueMaleChars, 'No male character models found. Add .glb/.vrm with companion .png to characters/male/ or characters/man/')}

### Characters — Female (Nữ)
${makeTable(uniqueFemaleChars, 'No female character models found. Add .glb/.vrm with companion .png to characters/female/ or characters/woman/')}

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

### SkyBoxs 360° Panoramas
${makeTable(skyboxes, 'No skybox textures found. Add equirectangular 360 images to SkyBoxs/')}

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

### Maps — Environments
${makeTable(maps, 'No raw map models found. Add .glb to maps/')}

### Audio — Background Music (BGM)
${makeTable(bgm, 'No BGM audio found. Add .mp3/.wav to audio/bgm/')}

### Audio — Combat SFX
${makeTable(sfxCombat, 'No combat SFX found. Add .mp3 to audio/sfx/combat/')}

### Audio — Interaction SFX
${makeTable(sfxInteract, 'No interaction SFX found. Add .mp3 to audio/sfx/interaction/')}

### Audio — Ambient SFX
${makeTable(sfxAmbient, 'No ambient SFX found. Add .mp3 to audio/sfx/ambient/')}

### Animations — Combat
${makeTable(animCombat, 'Procedural combat animation system active.')}

### Animations — Interaction
${makeTable(animInteract, 'Procedural interaction animation system active.')}

### Animations — Xianxia
${makeTable(animXianxia, 'XianxiaPoseLibrary 13 poses active.')}

### Animations — Locomotion
${makeTable(animLocomotion, 'Procedural locomotion active.')}

### VFX — Visual Effects
${makeTable(vfx, 'Internal particle shader VFX active.')}

---

## 4. Supported Actions & Expressions Reference

### Body Actions (40 Actions)
- **Locomotion:** \`idle\`, \`walk\`, \`run\`, \`sit\`, \`climb\`
- **Special:** \`fly_to\`, \`dash_to\`, \`teleport\`, \`kneel\`, \`bow\`, \`meditate\`
- **Combat:** \`heavy_slash_combo\`, \`fast_slash\`, \`magic_blast\`, \`punch_kick\`, \`fly_back_knockdown\`, \`stagger_back\`, \`block_defend\`, \`dodge\`
- **Xianxia Poses:** \`arms_crossed\`, \`hands_behind_back\`, \`fist_salute\`, \`finger_spell\`, \`power_charge\`, \`flying_stance\`
- **Life Interactions:** \`pickup_right\`, \`carry_two_hands\`, \`drink\`, \`pour\`, \`dig\`, \`water_plants\`, \`plant_seed\`, \`harvest\`, \`wave\`, \`dance\`, \`throw\`

### Facial Expressions (21 Expressions)
- **Standard:** \`neutral\`, \`angry\`, \`pain\`, \`smile\`, \`smirk\`, \`sad\`, \`serious\`, \`surprised\`, \`shock\`
- **Xianxia Dramatic:** \`cold\`, \`arrogant\`, \`contempt\`, \`wise\`, \`fierce\`, \`meditative\`, \`menacing\`, \`compassionate\`, \`determined\`
`;

// ----------------------------------------------------
// 2. GENERATE ASSET_CATALOG_VI.md (Tiếng Việt cho User)
// ----------------------------------------------------
const mdVi = `# 📦 DANH MỤC TÀI NGUYÊN (ASSET CATALOG) — AI 3D Animation Studio

> **DÀNH CHO NGƯỜI DÙNG:** File tài liệu Tiếng Việt có dấu giúp bạn dễ dàng theo dõi toàn bộ tài nguyên và bản đồ đã lưu trong dự án.
> **Quy định AI:** AI chỉ đọc file \`ASSET_CATALOG.md\` (tiếng Anh). File \`_VI.md\` này chỉ phục vụ người dùng.
> **Thời gian quét:** ${timestamp}
> **Tổng tài nguyên:** ${allAssets.length} tệp tin (${totalSize} MB), ${mapPresets.length} bản đồ lưu sẵn

---

## 1. Hướng Dẫn Soạn Kịch Bản Scene JSON Cho Người Dùng

### Bước 1: Chọn Bản Đồ Hoặc Tái Sử Dụng Bản Đồ Đã Lưu (Map Preset)
Bạn có thể trỏ trực tiếp tới bản đồ đã lưu để tận dụng ngay vị trí đồ vật, cây cối, ao hồ và điểm xuất hiện:
\`\`\`json
"environment": {
  "map": "farming_village",
  "map_preset": "sakura_lake_village",
  "sky_time": "sunset",
  "weather": { "fog": 0.012, "wind": 0.35 }
}
\`\`\`

### Bước 2: Chọn Nhân Vật & Lắp Ráp Ngoại Hình (Modular Assembly)
Bạn có thể chọn model có sẵn trong thư mục \`characters/male/\`, \`characters/female/\` hoặc lắp ráp bằng khối \`assembly\`:
\`\`\`json
{
  "id": "actor_cultivator",
  "name": "Lý Tiên Sinh",
  "model": "characters/male/sample_avatar.vrm",
  "spawn_point": [-3.5, 0, -1.8]
}
\`\`\`

### Bước 3: Diễn Hoạt Hoạt Ảnh & Biểu Cảm
- **Hành động cơ thể:** \`idle\` (đứng thở), \`walk\` (đi bộ), \`run\` (chạy), \`arms_crossed\` (khoanh tay), \`hands_behind_back\` (chắp tay sau lưng), \`meditate\` (ngồi thiền), \`fly_to\` (ngự kiếm bay)...
- **Biểu cảm khuôn mặt:** \`cold\` (lạnh lùng), \`arrogant\` (kiêu ngạo), \`contempt\` (khinh thường), \`wise\` (uyên bác), \`fierce\` (hung dữ sát khí), \`meditative\` (thiền định thanh tịnh)...

---

## 2. Danh Sách Bản Đồ Đã Lưu (Map Presets)

${makePresetsVi(mapPresets)}

---

## 3. Bảng Danh Mục Tài Nguyên Chi Tiết

### 🧑 Nhân Vật — Nam (Male / Man)
${makeTableVi(uniqueMaleChars, 'Chưa có model nhân vật nam. Thả tệp .glb/.vrm kèm ảnh .png vào characters/male/ hoặc characters/man/')}

### 👩 Nhân Vật — Nữ (Female / Woman)
${makeTableVi(uniqueFemaleChars, 'Chưa có model nhân vật nữ. Thả tệp .glb/.vrm kèm ảnh .png vào characters/female/ hoặc characters/woman/')}

### 👤 Nhân Vật — Thân Hình Cơ Bản (Base Bodies)
${makeTableVi([...baseBodies, ...rootCharFiles], 'Chưa có thân hình cơ bản. Thả tệp .vrm/.glb vào characters/base_bodies/')}

### 👤 Nhân Vật — Khuôn Mặt (Faces)
${makeTableVi(faces, 'Chưa có khuôn mặt rời. Thả tệp .glb vào characters/faces/')}

### 👤 Nhân Vật — Kiểu Tóc (Hairstyles)
${makeTableVi(hairstyles, 'Chưa có kiểu tóc. Thả tệp .glb vào characters/hairstyles/')}

### 👤 Nhân Vật — Kiểu Râu (Beards)
${makeTableVi(beards, 'Chưa có kiểu râu. Thả tệp .glb vào characters/beards/')}

### 👤 Nhân Vật — Trang Phục (Costumes)
${makeTableVi(costumes, 'Chưa có trang phục. Thả tệp .glb vào characters/costumes/')}

### 👤 Nhân Vật — Phụ Kiện (Accessories)
${makeTableVi(accessories, 'Chưa có phụ kiện. Thả tệp .glb vào characters/accessories/')}

### 🌌 Bầu Trời & Môi Trường (SkyBoxs 360°)
${makeTableVi(skyboxes, 'Chưa có ảnh Skybox. Thả ảnh 360 độ vào SkyBoxs/')}

### ⚔️ Đạo Cụ — Vũ Khí (Weapons)
${makeTableVi(weapons, 'Chưa có vũ khí. Thả tệp .glb vào props/weapons/')}

### 🔧 Đạo Cụ — Dụng Cụ (Tools)
${makeTableVi(tools, 'Chưa có dụng cụ tương tác. Thả tệp .glb vào props/tools/')}

### 🍵 Đạo Cụ — Đồ Tiêu Hao (Consumables)
${makeTableVi(consumables, 'Chưa có đồ tiêu hao. Thả tệp .glb vào props/consumables/')}

### 🪑 Đạo Cụ — Nội Thất (Furniture)
${makeTableVi(furniture, 'Chưa có đồ nội thất. Thả tệp .glb vào props/furniture/')}

### 🏠 Đạo Cụ — Công Trình (Buildings)
${makeTableVi(buildings, 'Chưa có công trình xây dựng. Thả tệp .glb vào props/buildings/')}

### 🌳 Đạo Cụ — Thiên Nhiên (Nature)
${makeTableVi(nature, 'Chưa có cây cối, đá cảnh. Thả tệp .glb vào props/nature/')}

### 🐴 Đạo Cụ — Phương Tiện & Thú Cưỡi (Vehicles)
${makeTableVi(vehicles, 'Chưa có thú cưỡi/kiếm bay. Thả tệp .glb vào props/vehicles/')}

### 🪑 Đạo Cụ — Thư Mục Gốc Cũ (Legacy Props)
${makeTableVi(rootProps, 'Không có đạo cụ ở thư mục gốc.')}

### 🗺️ Bản Đồ Bối Cảnh (Maps)
${makeTableVi(maps, 'Chưa có bản đồ. Thả tệp .glb/.gltf vào maps/')}

### 🎵 Âm Thanh — Nhạc Nền (BGM)
${makeTableVi(bgm, 'Chưa có bản nhạc nền nào.')}

### ⚔️ Âm Thanh — Hiệu Ứng Chiến Đấu (Combat SFX)
${makeTableVi(sfxCombat, 'Chưa có âm thanh chiến đấu.')}

### 🔔 Âm Thanh — Hiệu Ứng Tương Tác (Interaction SFX)
${makeTableVi(sfxInteract, 'Chưa có âm thanh tương tác.')}

### 🌧️ Âm Thanh — Hiệu Ứng Môi Trường (Ambient SFX)
${makeTableVi(sfxAmbient, 'Chưa có âm thanh môi trường.')}

### 🎬 Hoạt Ảnh — Chiến Đấu (Combat Animations)
${makeTableVi(animCombat, 'Đang sử dụng hệ thống diễn hoạt procedural nội tại.')}

### 🎬 Hoạt Ảnh — Tương Tác (Interaction Animations)
${makeTableVi(animInteract, 'Đang sử dụng hệ thống diễn hoạt procedural nội tại.')}

### 🎬 Hoạt Ảnh — Tiên Hiệp (Xianxia Poses)
${makeTableVi(animXianxia, 'Hệ thống XianxiaPoseLibrary 13 tư thế đang kích hoạt.')}

### 🎬 Hoạt Ảnh — Di Chuyển (Locomotion)
${makeTableVi(animLocomotion, 'Đang sử dụng hệ thống di chuyển nội tại.')}

### ✨ Hiệu Ứng Hình Ảnh (VFX Textures)
${makeTableVi(vfx, 'Shader hiệu ứng hạt nội tại đang kích hoạt.')}

---

## 4. Bảng Tra Cứu Hành Động & Biểu Cảm Hỗ Trợ

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
  map_presets: mapPresets,
  characters: {
    male: uniqueMaleChars,
    female: uniqueFemaleChars,
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
console.log('✓ Generated ASSET_CATALOG.md (English for AI, including Map Presets)');

fs.writeFileSync(outputMdVi, mdVi, 'utf-8');
console.log('✓ Generated ASSET_CATALOG_VI.md (Tiếng Việt cho User)');

fs.writeFileSync(outputJson, JSON.stringify(manifest, null, 2), 'utf-8');
console.log('✓ Generated asset_manifest.json');
