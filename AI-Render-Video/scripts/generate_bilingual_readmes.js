import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const root = path.resolve(__dirname, '../assets');

const folders = [
  // Characters
  {
    dir: 'characters',
    en: `# CHARACTERS DIRECTORY

## Purpose
Root directory for all character 3D models and modular parts.

## Subdirectories
- \`base_bodies/\`: Base humanoid body rigs (.vrm, .glb)
- \`faces/\`: Modular face meshes (.glb)
- \`hairstyles/\`: Hair meshes (.glb)
- \`beards/\`: Beard and mustache meshes (.glb)
- \`costumes/\`: Clothing and robes (.glb)
- \`accessories/\`: Wearable accessories (.glb)

## AI Usage
Assemble actors in scene configs via the \`assembly\` object.
`,
    vi: `# 👤 THƯ MỤC NHÂN VẬT (CHARACTERS)

## Mục Đích
Thư mục chứa toàn bộ mô hình 3D nhân vật và các bộ phận lắp ráp modular.

## Các Thư Mục Con
- \`base_bodies/\`: Thân hình cơ bản có khung xương Humanoid (.vrm, .glb)
- \`faces/\`: Khuôn mặt thay thế (.glb)
- \`hairstyles/\`: Mái tóc (.glb)
- \`beards/\`: Các kiểu râu (.glb)
- \`costumes/\`: Trang phục, áo bào (.glb)
- \`accessories/\`: Phụ kiện đeo người (.glb)

## Dành Cho Người Dùng
Bạn có thể thay đổi diện mạo nhân vật linh hoạt bằng cách kết hợp các bộ phận trong từng thư mục con vào kịch bản JSON.
`
  },
  {
    dir: 'characters/hairstyles',
    en: `# HAIRSTYLES — Character Hair Meshes

## Naming Convention
\`\`\`
hair_{style}_{trait}.glb
\`\`\`
Examples: \`hair_short_spiky.glb\`, \`hair_long_flowing.glb\`, \`hair_topknot.glb\`, \`hair_twin_tails.glb\`

## Technical Requirements
- Attach point: \`head\` bone.
- Supports runtime tinting via \`hair_color\` in the assembly config.

## AI Usage
\`\`\`json
{ "assembly": { "hairstyle": "characters/hairstyles/hair_topknot.glb", "hair_color": "#1a1a2e" } }
\`\`\`
`,
    vi: `# 💇 KIỂU TÓC (HAIRSTYLES)

## Quy Tắc Đặt Tên
\`\`\`
hair_{kiểu}_{đặc_điểm}.glb
\`\`\`
Ví dụ: \`hair_short_spiky.glb\` (tóc ngắn nhọn), \`hair_long_flowing.glb\` (tóc dài bay), \`hair_topknot.glb\` (búi tóc tiên hiệp).

## Yêu Cầu Kỹ Thuật
- Điểm gắn: Xương đầu (\`head\`).
- Hỗ trợ đổi màu tóc qua trường \`hair_color\` trong cấu hình lắp ráp.
`
  },
  {
    dir: 'characters/beards',
    en: `# BEARDS — Character Facial Hair

## Naming Convention
\`\`\`
beard_{style}.glb
\`\`\`
Examples: \`beard_goatee.glb\`, \`beard_long_sage.glb\`, \`beard_stubble.glb\`

## Technical Requirements
- Attach point: \`head\` bone (chin region).

## AI Usage
\`\`\`json
{ "assembly": { "beard": "characters/beards/beard_long_sage.glb" } }
\`\`\`
`,
    vi: `# 🧔 RÂU NHÂN VẬT (BEARDS)

## Quy Tắc Đặt Tên
\`\`\`
beard_{kiểu}.glb
\`\`\`
Ví dụ: \`beard_goatee.glb\` (râu dê), \`beard_long_sage.glb\` (râu dài tiên nhân), \`beard_stubble.glb\` (râu quai nón ngắn).

## Yêu Cầu Kỹ Thuật
- Điểm gắn: Xương đầu (\`head\`), khu vực cằm.
`
  },
  {
    dir: 'characters/costumes',
    en: `# COSTUMES — Clothing and Robes

## Naming Convention
\`\`\`
costume_{style}_{variant}.glb
\`\`\`
Examples: \`costume_knight_armor.glb\`, \`costume_xianxia_white.glb\`, \`costume_mage_robe.glb\`, \`costume_super_armor.glb\`

## Technical Requirements
- Replaces or overlays torso and limb meshes on the base body.
- Supports runtime transformations via the \`transformations\` timeline track.

## AI Usage
\`\`\`json
{ "assembly": { "costume": "characters/costumes/costume_xianxia_white.glb" } }
\`\`\`
`,
    vi: `# 👔 TRANG PHỤC (COSTUMES)

## Quy Tắc Đặt Tên
\`\`\`
costume_{phong_cách}_{biến_thể}.glb
\`\`\`
Ví dụ: \`costume_knight_armor.glb\` (giáp hiệp sĩ), \`costume_xianxia_white.glb\` (bạch y tu tiên), \`costume_mage_robe.glb\` (áo choàng pháp sư).

## Tính Năng Biến Thân
Trang phục có thể được hoán đổi mượt mà giữa cảnh quay qua timeline \`transformations\`.
`
  },
  {
    dir: 'characters/accessories',
    en: `# ACCESSORIES — Wearable Items

## Naming Convention
\`\`\`
acc_{name}.glb
\`\`\`
Examples: \`acc_crown_gold.glb\`, \`acc_mask_fox.glb\`, \`acc_headband.glb\`, \`acc_scarf.glb\`

## Technical Requirements
- Attach points: \`head\`, \`neck\`, \`chest\`, or \`hip\`.

## AI Usage
\`\`\`json
{ "assembly": { "accessories": ["characters/accessories/acc_headband.glb"] } }
\`\`\`
`,
    vi: `# 💍 PHỤ KIỆN (ACCESSORIES)

## Quy Tắc Đặt Tên
\`\`\`
acc_{tên_phụ_kiện}.glb
\`\`\`
Ví dụ: \`acc_crown_gold.glb\` (vương miện vàng), \`acc_mask_fox.glb\` (mặt nạ hồ ly), \`acc_headband.glb\` (băng đô trán).
`
  },

  // Props
  {
    dir: 'props',
    en: `# PROPS & OBJECTS DIRECTORY

## Purpose
Root directory for all 3D props, weapons, tools, furniture, and interactables.

## Subdirectories
- \`weapons/\`: Swords, staves, bows, shields (.glb)
- \`tools/\`: Hoes, watering cans, pickaxes (.glb)
- \`consumables/\`: Cups, wine bottles, food (.glb)
- \`furniture/\`: Chairs, tables, beds, shelves (.glb)
- \`buildings/\`: Houses, towers with upgrade variants (.glb)
- \`nature/\`: Trees, rocks, bushes (.glb)
- \`vehicles/\`: Flying swords, clouds, mounts (.glb)
`,
    vi: `# 🪑 ĐẠO CỤ & VẬT THỂ (PROPS)

## Mục Đích
Thư mục chứa toàn bộ đạo cụ, vũ khí, nông cụ, nội thất và vật thể tương tác trong bối cảnh.

## Các Thư Mục Con
- \`weapons/\`: Vũ khí (kiếm, gậy phép, cung tên)
- \`tools/\`: Nông cụ và công cụ (cuốc, bình tưới nước)
- \`consumables/\`: Đồ ăn uống, ly chén, bình rượu
- \`furniture/\`: Bàn ghế, giường tủ nội thất
- \`buildings/\`: Nhà cửa, tháp phép thuật (hỗ trợ cấp độ nâng cấp)
- \`nature/\`: Cây cỏ, hoa lá, tảng đá tự nhiên
- \`vehicles/\`: Kiếm bay ngự kiếm, thú cưỡi, thuyền bè
`
  },
  {
    dir: 'props/weapons',
    en: `# WEAPONS — Combat Equipments

## Naming Convention
\`\`\`
{type}_{name}_{variant}.glb
\`\`\`
Examples: \`sword_fire.glb\`, \`sword_ice.glb\`, \`staff_magic.glb\`, \`bow_longbow.glb\`, \`sword_xianxia_jade.glb\`

## Attach Points
- \`weapon_r\` (Right hand socket)
- \`weapon_l\` (Left hand socket)
`,
    vi: `# ⚔️ VŨ KHÍ (WEAPONS)

## Quy Tắc Đặt Tên
\`\`\`
{loại}_{tên}_{biến_thể}.glb
\`\`\`
Ví dụ: \`sword_fire.glb\` (hỏa kiếm), \`staff_magic.glb\` (trượng phép), \`sword_xianxia_jade.glb\` (bích ngọc kiếm).

## Điểm Neo Gắn Tay
- \`weapon_r\`: Tay phải
- \`weapon_l\`: Tay trái
`
  },
  {
    dir: 'props/tools',
    en: `# TOOLS — Interaction & Farming Implements

## Naming Convention
\`\`\`
tool_{name}.glb
\`\`\`
Examples: \`tool_hoe.glb\`, \`tool_watering_can.glb\`, \`tool_pickaxe.glb\`, \`tool_fishing_rod.glb\`

## AI Usage
Used with \`object_interactions\` and \`inventory_actions\` tracks.
`,
    vi: `# 🔧 DỤNG CỤ & NÔNG CỤ (TOOLS)

## Quy Tắc Đặt Tên
\`\`\`
tool_{tên_dụng_cụ}.glb
\`\`\`
Ví dụ: \`tool_hoe.glb\` (cuốc đất), \`tool_watering_can.glb\` (bình tưới), \`tool_pickaxe.glb\` (cuốc chim đào khoáng).
`
  },
  {
    dir: 'props/consumables',
    en: `# CONSUMABLES — Food, Drinks & Items

## Naming Convention
\`\`\`
{type}_{name}.glb
\`\`\`
Examples: \`cup_tea.glb\`, \`bottle_wine.glb\`, \`food_bread.glb\`, \`scroll_paper.glb\`

## AI Usage
Used in actions: \`drink\`, \`eat\`, \`pour\`, \`read\`.
`,
    vi: `# 🍵 ĐỒ TIÊU HAO (CONSUMABLES)

## Quy Tắc Đặt Tên
\`\`\`
{loại}_{tên}.glb
\`\`\`
Ví dụ: \`cup_tea.glb\` (ly trà), \`bottle_wine.glb\` (bình hồ lô rượu), \`scroll_paper.glb\` (cuộn mật thư).
`
  },
  {
    dir: 'props/furniture',
    en: `# FURNITURE — Interactive Interior Items

## Naming Convention
\`\`\`
{type}_{style}.glb
\`\`\`
Examples: \`chair_wooden.glb\`, \`table_round.glb\`, \`bed_simple.glb\`, \`bookshelf.glb\`

## Smart Sockets
Registered with interaction anchor positions for seamless sit, sleep, or read animations.
`,
    vi: `# 🪑 NỘI THẤT (FURNITURE)

## Quy Tắc Đặt Tên
\`\`\`
{loại}_{kiểu_dáng}.glb
\`\`\`
Ví dụ: \`chair_wooden.glb\` (ghế gỗ), \`table_round.glb\` (bàn tròn), \`bookshelf.glb\` (giá sách).
`
  },
  {
    dir: 'props/buildings',
    en: `# BUILDINGS — Structures & Upgrade Stages

## Naming Convention
\`\`\`
{type}_{style}_lv{N}.glb
\`\`\`
Examples: \`house_wood_lv1.glb\`, \`house_stone_lv2.glb\`, \`house_castle_lv3.glb\`, \`tower_mage.glb\`

## Upgrade Integration
Compatible with \`ObjectUpgradeSystem\` for evolving building models with VFX animations.
`,
    vi: `# 🏠 CÔNG TRÌNH & NHÀ CỬA (BUILDINGS)

## Quy Tắc Đặt Tên
\`\`\`
{loại}_{phong_cách}_lv{cấp_độ}.glb
\`\`\`
Ví dụ: \`house_wood_lv1.glb\` (nhà tranh cấp 1), \`house_stone_lv2.glb\` (nhà đá cấp 2), \`house_castle_lv3.glb\` (lâu đài cấp 3).
`
  },
  {
    dir: 'props/nature',
    en: `# NATURE — Vegetation, Trees & Rocks

## Naming Convention
\`\`\`
{type}_{species}.glb
\`\`\`
Examples: \`tree_oak.glb\`, \`tree_sakura.glb\`, \`rock_large.glb\`, \`bush_flower.glb\`
`,
    vi: `# 🌳 CẢNH QUAN THIÊN NHIÊN (NATURE)

## Quy Tắc Đặt Tên
\`\`\`
{loại}_{loài}.glb
\`\`\`
Ví dụ: \`tree_oak.glb\` (cây sồi), \`tree_sakura.glb\` (cây hoa anh đào), \`rock_large.glb\` (tảng đá lớn).
`
  },
  {
    dir: 'props/vehicles',
    en: `# VEHICLES & MOUNTS — Travel Mounts

## Naming Convention
\`\`\`
{type}_{name}.glb
\`\`\`
Examples: \`flying_sword.glb\`, \`cloud_mount.glb\`, \`horse.glb\`, \`boat_small.glb\`

## Xianxia Flight
Enables sword-flying (\`flying_stance\`) and cloud riding.
`,
    vi: `# 🐴 PHƯƠNG TIỆN & THÚ CƯỠI (VEHICLES)

## Quy Tắc Đặt Tên
\`\`\`
{loại}_{tên}.glb
\`\`\`
Ví dụ: \`flying_sword.glb\` (phi kiếm ngự không), \`cloud_mount.glb\` (cân đẩu vân), \`horse.glb\` (ngựa chiến).
`
  },

  // Maps
  {
    dir: 'maps',
    en: `# MAPS & SCENERY DIRECTORY

## Purpose
Environment scenes and level terrains (.glb, .gltf).

## AI Usage
Referenced directly in \`environment.map\`:
\`\`\`json
"environment": {
  "map": "medieval_fantasy_book"
}
\`\`\`
`,
    vi: `# 🗺️ BẢN ĐỒ & MÔI TRƯỜNG (MAPS)

## Mục Đích
Chứa các bản đồ không gian 3D, bối cảnh đền đài, làng mạc, chiến trường (.glb, .gltf).

## Cách Khai Báo Trong Kịch Bản
Trỏ trực tiếp tên file vào trường \`environment.map\`.
`
  },

  // Audio
  {
    dir: 'audio',
    en: `# AUDIO ROOT DIRECTORY

## Subdirectories
- \`bgm/\`: Background musical scores (.mp3, .wav)
- \`sfx/combat/\`: Weapon clashes, magic impacts, explosions (.mp3)
- \`sfx/interaction/\`: Doors, water pouring, footsteps (.mp3)
- \`sfx/ambient/\`: Wind, rain, bird ambient sounds (.mp3)
- \`dialogues/\`: Generated TTS voice audio files (.mp3)
`,
    vi: `# 🎵 THƯ MỤC ÂM THANH (AUDIO)

## Các Thư Mục Con
- \`bgm/\`: Nhạc nền cảm xúc cho từng phân cảnh (.mp3, .wav)
- \`sfx/combat/\`: Tiếng va chạm vũ khí, nổ chưởng, đao kiếm (.mp3)
- \`sfx/interaction/\`: Tiếng mở cửa, rót nước, bước chân (.mp3)
- \`sfx/ambient/\`: Tiếng gió thổi, mưa rơi, chim hót (.mp3)
- \`dialogues/\`: File giọng lồng tiếng nhân vật tự động tạo từ TTS (.mp3)
`
  },
  {
    dir: 'audio/bgm',
    en: `# BGM — Background Music Tracks
## Naming Convention: \`bgm_{mood}_{name}.mp3\`
Examples: \`bgm_epic_battle.mp3\`, \`bgm_peaceful_village.mp3\`, \`bgm_mystic_cultivation.mp3\`
`,
    vi: `# 🎵 NHẠC NỀN (BGM)
## Quy Tắc Đặt Tên: \`bgm_{tâm_trạng}_{tên}.mp3\`
Ví dụ: \`bgm_epic_battle.mp3\` (chiến đấu hào hùng), \`bgm_peaceful_village.mp3\` (làng quê thanh bình).
`
  },
  {
    dir: 'audio/dialogues',
    en: `# DIALOGUES — Generated TTS Voice Clips
Contains generated dialogue audio files mapped to \`dialogues_manifest\` in scene configs.
Naming rule: \`{scene_id}_{speaker_id}_{line_id}.mp3\`
`,
    vi: `# 🎙️ GIỌNG THOẠI LỒNG TIẾNG (DIALOGUES)
Chứa các tệp âm thanh giọng đọc TTS được tạo tự động khớp theo kịch bản thoại \`dialogues_manifest\`.
`
  },
  {
    dir: 'audio/sfx',
    en: `# SFX — Sound Effects
Subdivided into \`combat/\`, \`interaction/\`, and \`ambient/\`.
`,
    vi: `# 🔊 HIỆU ỨNG ÂM THANH (SFX)
Được chia thành 3 nhóm: chiến đấu (\`combat/\`), tương tác (\`interaction/\`) và môi trường (\`ambient/\`).
`
  },
  {
    dir: 'audio/sfx/combat',
    en: `# COMBAT SFX — Battle Sound Effects
## Naming Convention: \`sfx_{action}.mp3\`
Examples: \`sfx_sword_hit.mp3\`, \`sfx_magic_blast.mp3\`, \`sfx_explosion.mp3\`, \`sfx_block.mp3\`
`,
    vi: `# ⚔️ ÂM THANH CHIẾN ĐẤU (COMBAT SFX)
## Quy Tắc Đặt Tên: \`sfx_{hành_động}.mp3\`
Ví dụ: \`sfx_sword_hit.mp3\` (kiếm chém trúng), \`sfx_magic_blast.mp3\` (phóng chưởng phép), \`sfx_explosion.mp3\` (tiếng nổ).
`
  },
  {
    dir: 'audio/sfx/interaction',
    en: `# INTERACTION SFX — Object Action Sounds
## Naming Convention: \`sfx_{action}.mp3\`
Examples: \`sfx_door_open.mp3\`, \`sfx_water_pour.mp3\`, \`sfx_footstep.mp3\`, \`sfx_pickup.mp3\`
`,
    vi: `# 🔔 ÂM THANH TƯƠNG TÁC (INTERACTION SFX)
## Quy Tắc Đặt Tên: \`sfx_{hành_động}.mp3\`
Ví dụ: \`sfx_door_open.mp3\` (mở cửa), \`sfx_water_pour.mp3\` (rót nước), \`sfx_footstep.mp3\` (bước chân).
`
  },
  {
    dir: 'audio/sfx/ambient',
    en: `# AMBIENT SFX — Environment Atmospheres
## Naming Convention: \`sfx_{type}.mp3\`
Examples: \`sfx_wind.mp3\`, \`sfx_rain.mp3\`, \`sfx_birds.mp3\`, \`sfx_crickets.mp3\`
`,
    vi: `# 🌧️ ÂM THANH MÔI TRƯỜNG (AMBIENT SFX)
## Quy Tắc Đặt Tên: \`sfx_{loại}.mp3\`
Ví dụ: \`sfx_wind.mp3\` (gió rít), \`sfx_rain.mp3\` (mưa rơi), \`sfx_birds.mp3\` (tiếng chim rừng).
`
  },

  // Animations
  {
    dir: 'animations',
    en: `# ANIMATIONS & MOCAP DIRECTORY

## Subdirectories
- \`combat/\`: Attack combos, spells, dodges (.glb, .bvh)
- \`interaction/\`: Sitting, drinking, farming (.glb)
- \`xianxia/\`: Meditation, flying, hand seals (.glb)
- \`locomotion/\`: Walking, running, jumping (.glb)
`,
    vi: `# 🏃 THƯ MỤC HOẠT ẢNH (ANIMATIONS)

## Các Thư Mục Con
- \`combat/\`: Hoạt ảnh võ thuật, chém kiếm, đỡ đòn (.glb, .bvh)
- \`interaction/\`: Hoạt ảnh sinh hoạt (ngồi ghế, uống trà, cuốc đất) (.glb)
- \`xianxia/\`: Hoạt ảnh tiên hiệp (ngồi thiền, bắt ấn, tụ khí) (.glb)
- \`locomotion/\`: Hoạt ảnh di chuyển (đi bộ, chạy nhanh, nhảy) (.glb)
`
  },
  {
    dir: 'animations/combat',
    en: `# COMBAT ANIMATIONS
## Naming Convention: \`anim_{action_name}.glb\`
Examples: \`anim_heavy_slash.glb\`, \`anim_magic_blast.glb\`, \`anim_aerial_combo.glb\`
`,
    vi: `# ⚔️ HOẠT ẢNH CHIẾN ĐẤU (COMBAT ANIMATIONS)
## Quy Tắc Đặt Tên: \`anim_{tên_hành_động}.glb\`
Ví dụ: \`anim_heavy_slash.glb\` (chém mạnh), \`anim_magic_blast.glb\` (bắn phép).
`
  },
  {
    dir: 'animations/interaction',
    en: `# INTERACTION ANIMATIONS
## Naming Convention: \`anim_{action}.glb\`
Examples: \`anim_sit.glb\`, \`anim_drink.glb\`, \`anim_dig.glb\`, \`anim_carry.glb\`
`,
    vi: `# 🤝 HOẠT ẢNH TƯƠNG TÁC (INTERACTION ANIMATIONS)
## Quy Tắc Đặt Tên: \`anim_{hành_động}.glb\`
Ví dụ: \`anim_sit.glb\` (ngồi ghế), \`anim_drink.glb\` (nâng chén uống), \`anim_dig.glb\` (cuốc đất).
`
  },
  {
    dir: 'animations/xianxia',
    en: `# XIANXIA ANIMATIONS — Cultivation & Martial Arts
## Naming Convention: \`anim_{pose_name}.glb\`
Examples: \`anim_meditation.glb\`, \`anim_fist_salute.glb\`, \`anim_power_charge.glb\`, \`anim_finger_spell.glb\`
`,
    vi: `# 🧘 HOẠT ẢNH TIÊN HIỆP (XIANXIA ANIMATIONS)
## Quy Tắc Đặt Tên: \`anim_{tên_tư_thế}.glb\`
Ví dụ: \`anim_meditation.glb\` (ngồi thiền), \`anim_fist_salute.glb\` (bao quyền bái lễ), \`anim_power_charge.glb\` (vận khí).
`
  },
  {
    dir: 'animations/locomotion',
    en: `# LOCOMOTION ANIMATIONS
## Naming Convention: \`anim_{action}.glb\`
Examples: \`anim_walk.glb\`, \`anim_run.glb\`, \`anim_fly.glb\`, \`anim_jump.glb\`
`,
    vi: `# 🏃 HOẠT ẢNH DI CHUYỂN (LOCOMOTION)
## Quy Tắc Đặt Tên: \`anim_{hành_động}.glb\`
Ví dụ: \`anim_walk.glb\` (đi bộ), \`anim_run.glb\` (chạy nước rút), \`anim_fly.glb\` (ngự không bay).
`
  },

  // VFX
  {
    dir: 'vfx',
    en: `# VFX — Visual Effect Textures & Sprites
## Naming Convention: \`{type}_{name}.png\`
Examples: \`particle_fire.png\`, \`particle_spark.png\`, \`particle_magic.png\`, \`aura_glow.png\`
`,
    vi: `# ✨ HIỆU ỨNG HÌNH ẢNH (VFX TEXTURES)
## Quy Tắc Đặt Tên: \`{loại}_{tên}.png\`
Ví dụ: \`particle_fire.png\` (hạt lửa), \`particle_spark.png\` (tia lửa lóe), \`aura_glow.png\` (vòng hào quang).
`
  }
];

let created = 0;
for (const item of folders) {
  const targetDir = path.join(root, item.dir);
  if (!fs.existsSync(targetDir)) {
    fs.mkdirSync(targetDir, { recursive: true });
  }
  fs.writeFileSync(path.join(targetDir, 'README.md'), item.en, 'utf-8');
  fs.writeFileSync(path.join(targetDir, 'README_VI.md'), item.vi, 'utf-8');
  created += 2;
}

console.log(`Successfully generated ${created} bilingual documentation files.`);
