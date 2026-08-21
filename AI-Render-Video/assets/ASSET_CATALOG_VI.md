# 📦 DANH MỤC TÀI NGUYÊN (ASSET CATALOG) — AI 3D Animation Studio

> **DÀNH CHO NGƯỜI DÙNG:** File tài liệu Tiếng Việt có dấu giúp bạn dễ dàng theo dõi toàn bộ tài nguyên và bản đồ đã lưu trong dự án.
> **Quy định AI:** AI chỉ đọc file `ASSET_CATALOG.md` (tiếng Anh). File `_VI.md` này chỉ phục vụ người dùng.
> **Thời gian quét:** 2026-08-21 04:35:13
> **Tổng tài nguyên:** 29 tệp tin (47.9 MB), 2 bản đồ lưu sẵn

---

## 1. Hướng Dẫn Soạn Kịch Bản Scene JSON Cho Người Dùng

### Bước 1: Chọn Bản Đồ Hoặc Tái Sử Dụng Bản Đồ Đã Lưu (Map Preset)
Bạn có thể trỏ trực tiếp tới bản đồ đã lưu để tận dụng ngay vị trí đồ vật, cây cối, ao hồ và điểm xuất hiện:
```json
"environment": {
  "map": "farming_village",
  "map_preset": "sakura_lake_village",
  "sky_time": "sunset",
  "weather": { "fog": 0.012, "wind": 0.35 }
}
```

### Bước 2: Chọn Nhân Vật & Lắp Ráp Ngoại Hình (Modular Assembly)
Bạn có thể chọn model có sẵn trong thư mục `characters/male/`, `characters/female/` hoặc lắp ráp bằng khối `assembly`:
```json
{
  "id": "actor_cultivator",
  "name": "Lý Tiên Sinh",
  "model": "characters/male/sample_avatar.vrm",
  "spawn_point": [-3.5, 0, -1.8]
}
```

### Bước 3: Diễn Hoạt Hoạt Ảnh & Biểu Cảm
- **Hành động cơ thể:** `idle` (đứng thở), `walk` (đi bộ), `run` (chạy), `arms_crossed` (khoanh tay), `hands_behind_back` (chắp tay sau lưng), `meditate` (ngồi thiền), `fly_to` (ngự kiếm bay)...
- **Biểu cảm khuôn mặt:** `cold` (lạnh lùng), `arrogant` (kiêu ngạo), `contempt` (khinh thường), `wise` (uyên bác), `fierce` (hung dữ sát khí), `meditative` (thiền định thanh tịnh)...

---

## 2. Danh Sách Bản Đồ Đã Lưu (Map Presets)

#### Mã Map: `sakura_lake_village` — Làng Hoa Anh Đào Ven Hồ
- **Tệp cấu hình**: `maps/presets/sakura_lake_village.json`
- **Mô tả bối cảnh**: Ngôi làng thanh tịnh ven hồ nước, có 2 hàng cây hoa anh đào lớn, một ghế dài đá ngồi ngắm cảnh hướng ra hồ, và vườn thảo dược phía đông.
- **Map nền**: `farming_village` | **Bầu trời & Thời tiết**: sunset, Sương mù: 0.012
- **Các điểm xuất hiện (Spawn Points)**:
  - Điểm xuất hiện `"lakeside_bench"`: [-3.5, 0, -1.8]
  - Điểm xuất hiện `"village_entrance"`: [0, 0, 4]
  - Điểm xuất hiện `"sakura_tree_north"`: [4, 0, -3]
  - Điểm xuất hiện `"herb_garden"`: [0, 0, -5]
- **Danh sách đồ vật & điểm tương tác**:
  - `sakura_tree_01` (nature) tại [4, 0, -3] — Model: `props/nature/tree_sakura.glb` (Tương tác: climb)
  - `stone_bench_01` (furniture) tại [-3.5, 0, -1.8] — Model: `props/furniture/chair_wooden.glb` (Tương tác: sit)
  - `herb_farm_plot` (nature) tại [0, 0, -5] — Model: `props/tools/farm_plot.glb` (Tương tác: harvest)
  - `night_lantern_stand` (furniture) tại [-2.8, 0, -1.5] — Model: `props/furniture/lantern_prop.glb`

#### Mã Map: `xianxia_mountain_arena` — Vấn Đỉnh Phong — Đấu Trường Tiên Giới
- **Tệp cấu hình**: `maps/presets/xianxia_mountain_arena.json`
- **Mô tả bối cảnh**: Đỉnh núi mây mù bao phủ, có các cột đá khắc phù văn cổ xưa xung quanh đài tỷ võ, hướng bắc có tảng đá linh khí tọa thiền.
- **Map nền**: `medieval_fantasy_book` | **Bầu trời & Thời tiết**: sunrise, Sương mù: 0.02
- **Các điểm xuất hiện (Spawn Points)**:
  - Điểm xuất hiện `"challenger_1_west"`: [-4, 0, 0]
  - Điểm xuất hiện `"challenger_2_east"`: [4, 0, 0]
  - Điểm xuất hiện `"meditation_stone_north"`: [0, 0, -4.5]
  - Điểm xuất hiện `"arena_center"`: [0, 0, 0]
- **Danh sách đồ vật & điểm tương tác**:
  - `meditation_stone_01` (nature) tại [0, 0, -4.5] — Model: `props/nature/rock_large.glb` (Tương tác: stand)
  - `ancient_pillar_west` (building) tại [-5.5, 0, -2] — Model: `props/buildings/tower_mage.glb`
  - `ancient_pillar_east` (building) tại [5.5, 0, -2] — Model: `props/buildings/tower_mage.glb`



---

## 3. Bảng Danh Mục Tài Nguyên Chi Tiết

### 🧑 Nhân Vật — Nam (Male / Man)
*Chưa có model nhân vật nam. Thả tệp .glb/.vrm kèm ảnh .png vào characters/male/ hoặc characters/man/*


### 👩 Nhân Vật — Nữ (Female / Woman)
*Chưa có model nhân vật nữ. Thả tệp .glb/.vrm kèm ảnh .png vào characters/female/ hoặc characters/woman/*


### 👤 Nhân Vật — Thân Hình Cơ Bản (Base Bodies)
| Mã ID | Đường Dẫn (Path) | Định Dạng | Dung Lượng | Ảnh Tham Chiếu (Preview) |
|:---|:---|:---|---:|:---|
| `sample_avatar` | `characters/sample_avatar.vrm` | VRM | 10.28 MB | — |



### 👤 Nhân Vật — Khuôn Mặt (Faces)
*Chưa có khuôn mặt rời. Thả tệp .glb vào characters/faces/*


### 👤 Nhân Vật — Kiểu Tóc (Hairstyles)
*Chưa có kiểu tóc. Thả tệp .glb vào characters/hairstyles/*


### 👤 Nhân Vật — Kiểu Râu (Beards)
*Chưa có kiểu râu. Thả tệp .glb vào characters/beards/*


### 👤 Nhân Vật — Trang Phục (Costumes)
*Chưa có trang phục. Thả tệp .glb vào characters/costumes/*


### 👤 Nhân Vật — Phụ Kiện (Accessories)
*Chưa có phụ kiện. Thả tệp .glb vào characters/accessories/*


### 🌌 Bầu Trời & Môi Trường (SkyBoxs 360°)
| Mã ID | Đường Dẫn (Path) | Định Dạng | Dung Lượng | Ảnh Tham Chiếu (Preview) |
|:---|:---|:---|---:|:---|
| `binh_minh_it_may_1` | `SkyBoxs/binh_minh/it_may/binh_minh_it_may_1.png` | PNG | 0.86 MB | `assets/SkyBoxs/binh_minh/it_may/binh_minh_it_may_1.png` |
| `binh_minh_khong_may_1` | `SkyBoxs/binh_minh/khong_may/binh_minh_khong_may_1.png` | PNG | 0.86 MB | `assets/SkyBoxs/binh_minh/khong_may/binh_minh_khong_may_1.png` |
| `binh_minh_nhieu_may_1` | `SkyBoxs/binh_minh/nhieu_may/binh_minh_nhieu_may_1.png` | PNG | 0.86 MB | `assets/SkyBoxs/binh_minh/nhieu_may/binh_minh_nhieu_may_1.png` |
| `buoi_chieu_it_may_1` | `SkyBoxs/buoi_chieu/it_may/buoi_chieu_it_may_1.png` | PNG | 0.86 MB | `assets/SkyBoxs/buoi_chieu/it_may/buoi_chieu_it_may_1.png` |
| `buoi_chieu_khong_may_1` | `SkyBoxs/buoi_chieu/khong_may/buoi_chieu_khong_may_1.png` | PNG | 0.86 MB | `assets/SkyBoxs/buoi_chieu/khong_may/buoi_chieu_khong_may_1.png` |
| `buoi_chieu_nhieu_may_1` | `SkyBoxs/buoi_chieu/nhieu_may/buoi_chieu_nhieu_may_1.png` | PNG | 1.35 MB | `assets/SkyBoxs/buoi_chieu/nhieu_may/buoi_chieu_nhieu_may_1.png` |
| `buoi_sang_it_may_1` | `SkyBoxs/buoi_sang/it_may/buoi_sang_it_may_1.png` | PNG | 0.86 MB | `assets/SkyBoxs/buoi_sang/it_may/buoi_sang_it_may_1.png` |
| `buoi_sang_khong_may_1` | `SkyBoxs/buoi_sang/khong_may/buoi_sang_khong_may_1.png` | PNG | 1.00 MB | `assets/SkyBoxs/buoi_sang/khong_may/buoi_sang_khong_may_1.png` |
| `3` | `SkyBoxs/buoi_sang/nhieu_may/3.png` | PNG | 1.63 MB | `assets/SkyBoxs/buoi_sang/nhieu_may/3.png` |
| `buoi_sang_2` | `SkyBoxs/buoi_sang/nhieu_may/buoi_sang_2.png` | PNG | 1.59 MB | `assets/SkyBoxs/buoi_sang/nhieu_may/buoi_sang_2.png` |
| `buoi_sang_nhieu_may_1` | `SkyBoxs/buoi_sang/nhieu_may/buoi_sang_nhieu_may_1.png` | PNG | 1.00 MB | `assets/SkyBoxs/buoi_sang/nhieu_may/buoi_sang_nhieu_may_1.png` |
| `buoi_toi_it_may_1` | `SkyBoxs/buoi_toi/it_may/buoi_toi_it_may_1.png` | PNG | 0.27 MB | `assets/SkyBoxs/buoi_toi/it_may/buoi_toi_it_may_1.png` |
| `buoi_toi_khong_may_1` | `SkyBoxs/buoi_toi/khong_may/buoi_toi_khong_may_1.png` | PNG | 1.05 MB | `assets/SkyBoxs/buoi_toi/khong_may/buoi_toi_khong_may_1.png` |
| `buoi_toi_nhieu_may_1` | `SkyBoxs/buoi_toi/nhieu_may/buoi_toi_nhieu_may_1.png` | PNG | 1.05 MB | `assets/SkyBoxs/buoi_toi/nhieu_may/buoi_toi_nhieu_may_1.png` |
| `buoi_trua_it_may_1` | `SkyBoxs/buoi_trua/it_may/buoi_trua_it_may_1.png` | PNG | 1.00 MB | `assets/SkyBoxs/buoi_trua/it_may/buoi_trua_it_may_1.png` |
| `buoi_trua_khong_may_1` | `SkyBoxs/buoi_trua/khong_may/buoi_trua_khong_may_1.png` | PNG | 1.00 MB | `assets/SkyBoxs/buoi_trua/khong_may/buoi_trua_khong_may_1.png` |
| `buoi_trua_nhieu_may_1` | `SkyBoxs/buoi_trua/nhieu_may/buoi_trua_nhieu_may_1.png` | PNG | 1.00 MB | `assets/SkyBoxs/buoi_trua/nhieu_may/buoi_trua_nhieu_may_1.png` |
| `giong_bao_it_may_1` | `SkyBoxs/giong_bao/it_may/giong_bao_it_may_1.png` | PNG | 1.35 MB | `assets/SkyBoxs/giong_bao/it_may/giong_bao_it_may_1.png` |
| `giong_bao_nhieu_may_1` | `SkyBoxs/giong_bao/nhieu_may/giong_bao_nhieu_may_1.png` | PNG | 1.35 MB | `assets/SkyBoxs/giong_bao/nhieu_may/giong_bao_nhieu_may_1.png` |
| `skybox-alien` | `SkyBoxs/skybox-alien.png` | PNG | 1.35 MB | `assets/SkyBoxs/skybox-alien.png` |
| `skybox-day` | `SkyBoxs/skybox-day.png` | PNG | 1.00 MB | `assets/SkyBoxs/skybox-day.png` |
| `skybox-morning` | `SkyBoxs/skybox-morning.png` | PNG | 0.86 MB | `assets/SkyBoxs/skybox-morning.png` |
| `skybox-night` | `SkyBoxs/skybox-night.png` | PNG | 1.05 MB | `assets/SkyBoxs/skybox-night.png` |
| `skybox-space` | `SkyBoxs/skybox-space.png` | PNG | 0.27 MB | `assets/SkyBoxs/skybox-space.png` |



### ⚔️ Đạo Cụ — Vũ Khí (Weapons)
*Chưa có vũ khí. Thả tệp .glb vào props/weapons/*


### 🔧 Đạo Cụ — Dụng Cụ (Tools)
*Chưa có dụng cụ tương tác. Thả tệp .glb vào props/tools/*


### 🍵 Đạo Cụ — Đồ Tiêu Hao (Consumables)
*Chưa có đồ tiêu hao. Thả tệp .glb vào props/consumables/*


### 🪑 Đạo Cụ — Nội Thất (Furniture)
*Chưa có đồ nội thất. Thả tệp .glb vào props/furniture/*


### 🏠 Đạo Cụ — Công Trình (Buildings)
*Chưa có công trình xây dựng. Thả tệp .glb vào props/buildings/*


### 🌳 Đạo Cụ — Thiên Nhiên (Nature)
*Chưa có cây cối, đá cảnh. Thả tệp .glb vào props/nature/*


### 🐴 Đạo Cụ — Phương Tiện & Thú Cưỡi (Vehicles)
*Chưa có thú cưỡi/kiếm bay. Thả tệp .glb vào props/vehicles/*


### 🪑 Đạo Cụ — Thư Mục Gốc Cũ (Legacy Props)
| Mã ID | Đường Dẫn (Path) | Định Dạng | Dung Lượng | Ảnh Tham Chiếu (Preview) |
|:---|:---|:---|---:|:---|
| `duck_prop` | `props/duck_prop.glb` | GLB | 0.11 MB | — |
| `lantern_prop` | `props/lantern_prop.glb` | GLB | 9.42 MB | — |



### 🗺️ Bản Đồ Bối Cảnh (Maps)
| Mã ID | Đường Dẫn (Path) | Định Dạng | Dung Lượng | Ảnh Tham Chiếu (Preview) |
|:---|:---|:---|---:|:---|
| `scene` | `maps/medieval_fantasy_book/scene.gltf` | GLTF | 0.03 MB | — |
| `medieval_fantasy_book` | `maps/medieval_fantasy_book.glb` | GLB | 3.70 MB | — |



### 🎵 Âm Thanh — Nhạc Nền (BGM)
*Chưa có bản nhạc nền nào.*


### ⚔️ Âm Thanh — Hiệu Ứng Chiến Đấu (Combat SFX)
*Chưa có âm thanh chiến đấu.*


### 🔔 Âm Thanh — Hiệu Ứng Tương Tác (Interaction SFX)
*Chưa có âm thanh tương tác.*


### 🌧️ Âm Thanh — Hiệu Ứng Môi Trường (Ambient SFX)
*Chưa có âm thanh môi trường.*


### 🎬 Hoạt Ảnh — Chiến Đấu (Combat Animations)
*Đang sử dụng hệ thống diễn hoạt procedural nội tại.*


### 🎬 Hoạt Ảnh — Tương Tác (Interaction Animations)
*Đang sử dụng hệ thống diễn hoạt procedural nội tại.*


### 🎬 Hoạt Ảnh — Tiên Hiệp (Xianxia Poses)
*Hệ thống XianxiaPoseLibrary 13 tư thế đang kích hoạt.*


### 🎬 Hoạt Ảnh — Di Chuyển (Locomotion)
*Đang sử dụng hệ thống di chuyển nội tại.*


### ✨ Hiệu Ứng Hình Ảnh (VFX Textures)
*Shader hiệu ứng hạt nội tại đang kích hoạt.*


---

## 4. Bảng Tra Cứu Hành Động & Biểu Cảm Hỗ Trợ

### 🏃 Hành Động Cơ Thể (40 Hành động)
- **Cơ bản:** `idle` (đứng thở), `walk` (đi bộ), `run` (chạy), `sit` (ngồi), `climb` (trèo)
- **Nâng cao:** `fly_to` (bay lượn), `dash_to` (lướt nhanh), `teleport` (dịch chuyển), `kneel` (quỳ), `bow` (cúi chào), `meditate` (ngồi thiền)
- **Chiến đấu:** `heavy_slash_combo` (chém combo), `fast_slash` (chém nhanh), `magic_blast` (chưởng phép), `punch_kick` (đấm đá), `fly_back_knockdown` (bị đánh văng ngã), `stagger_back` (loạng choạng), `block_defend` (đỡ đòn), `dodge` (né tránh)
- **Tư thế Tiên Hiệp:** `arms_crossed` (khoanh tay), `hands_behind_back` (chắp tay sau lưng), `fist_salute` (bao quyền bái lễ), `finger_spell` (bắt ấn quyết), `power_charge` (vận công tụ khí), `flying_stance` (tư thế ngự không)
- **Tương tác Đời Sống:** `pickup_right` (nhặt đồ), `carry_two_hands` (bưng bê 2 tay), `drink` (uống nước/rượu), `pour` (rót nước), `dig` (cuốc đất), `water_plants` (tưới cây), `plant_seed` (gieo hạt), `harvest` (thu hoạch), `wave` (vẫy tay), `dance` (nhảy múa), `throw` (ném đồ)

### 🎭 Biểu Cảm Khuôn Mặt (21 Biểu cảm)
- **Cơ bản:** `neutral` (bình thường), `angry` (tức giận), `pain` (đau đớn), `smile` (mỉm cười), `smirk` (cười nhếch mép), `sad` (buồn bã), `serious` (nghiêm túc), `surprised` (ngạc nhiên), `shock` (sốc/sửng sốt)
- **Tiên Hiệp & Truyền Kỳ:** `cold` (lạnh lùng sắc bén), `arrogant` (kiêu ngạo ngút trời), `contempt` (khinh thường coi rẻ), `wise` (uyên bác thấu hiểu), `fierce` (hung bạo sát khí), `meditative` (thiền định an yên), `menacing` (nham hiểm hiểm độc), `compassionate` (từ bi nhân hậu), `determined` (kiên định quyết tâm)
