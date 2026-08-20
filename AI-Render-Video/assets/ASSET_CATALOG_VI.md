# 📦 DANH MỤC TÀI NGUYÊN (ASSET CATALOG) — AI 3D Animation Studio

> **DÀNH CHO NGƯỜI DÙNG:** File tài liệu Tiếng Việt có dấu giúp bạn dễ dàng theo dõi toàn bộ tài nguyên và bản đồ đã lưu trong dự án.
> **Quy định AI:** AI chỉ đọc file `ASSET_CATALOG.md` (tiếng Anh). File `_VI.md` này chỉ phục vụ người dùng.
> **Thời gian quét:** 2026-08-20 14:50:50
> **Tổng tài nguyên:** 43 tệp tin (229.7 MB), 0 bản đồ lưu sẵn

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

*Chưa có bản đồ lưu sẵn.*


---

## 3. Bảng Danh Mục Tài Nguyên Chi Tiết

### 🧑 Nhân Vật — Nam (Male / Man)
| Mã ID | Đường Dẫn (Path) | Định Dạng | Dung Lượng | Ảnh Tham Chiếu (Preview) |
|:---|:---|:---|---:|:---|
| `precision_strike_manekin` | `characters/man/precision_strike_manekin.glb` | GLB | 1.15 MB | `assets/characters/man/precision_strike_manekin.png` |
| `sample_avatar` | `characters/male/sample_avatar.vrm` | VRM | 10.28 MB | `assets/characters/male/sample_avatar.png` |



### 👩 Nhân Vật — Nữ (Female / Woman)
| Mã ID | Đường Dẫn (Path) | Định Dạng | Dung Lượng | Ảnh Tham Chiếu (Preview) |
|:---|:---|:---|---:|:---|
| `tzitzimitl_female` | `characters/woman/tzitzimitl_female.glb` | GLB | 3.62 MB | `assets/characters/woman/tzitzimitl_female.png` |



### 👤 Nhân Vật — Thân Hình Cơ Bản (Base Bodies)
| Mã ID | Đường Dẫn (Path) | Định Dạng | Dung Lượng | Ảnh Tham Chiếu (Preview) |
|:---|:---|:---|---:|:---|
| `body_base_-_manekina` | `characters/base_bodies/male/body_base_-_manekina.glb` | GLB | 0.53 MB | `assets/characters/base_bodies/male/body_base_-_manekina.png` |
| `body_base_-_manekin` | `characters/base_bodies/man/body_base_-_manekin.glb` | GLB | 0.54 MB | `assets/characters/base_bodies/man/body_base_-_manekin.png` |
| `sample_avatar` | `characters/base_bodies/sample_avatar.vrm` | VRM | 10.28 MB | — |
| `sample_avatar` | `characters/sample_avatar.vrm` | VRM | 10.28 MB | — |



### 👤 Nhân Vật — Khuôn Mặt (Faces)
| Mã ID | Đường Dẫn (Path) | Định Dạng | Dung Lượng | Ảnh Tham Chiếu (Preview) |
|:---|:---|:---|---:|:---|
| `starlight_fragments_-_manekin` | `characters/faces/male/starlight_fragments_-_manekin.glb` | GLB | 0.19 MB | `assets/characters/faces/male/starlight_fragments_-_manekin.png` |
| `dawnbreaker_-_manekin` | `characters/faces/man/dawnbreaker_-_manekin.glb` | GLB | 0.20 MB | `assets/characters/faces/man/dawnbreaker_-_manekin.png` |



### 👤 Nhân Vật — Kiểu Tóc (Hairstyles)
*Chưa có kiểu tóc. Thả tệp .glb vào characters/hairstyles/*


### 👤 Nhân Vật — Kiểu Râu (Beards)
*Chưa có kiểu râu. Thả tệp .glb vào characters/beards/*


### 👤 Nhân Vật — Trang Phục (Costumes)
| Mã ID | Đường Dẫn (Path) | Định Dạng | Dung Lượng | Ảnh Tham Chiếu (Preview) |
|:---|:---|:---|---:|:---|
| `amber_nectar_-_manekina` | `characters/costumes/male/amber_nectar_-_manekina.glb` | GLB | 3.37 MB | `assets/characters/costumes/male/amber_nectar_-_manekina.png` |
| `precision_strike_-_manekina` | `characters/costumes/male/precision_strike_-_manekina.glb` | GLB | 0.99 MB | `assets/characters/costumes/male/precision_strike_-_manekina.png` |
| `scary_cat_-_manekina` | `characters/costumes/male/scary_cat_-_manekina.glb` | GLB | 1.37 MB | — |
| `amber_nectar_-_manekin` | `characters/costumes/man/amber_nectar_-_manekin.glb` | GLB | 3.43 MB | `assets/characters/costumes/man/amber_nectar_-_manekin.png` |
| `precision_strike_-_manekin` | `characters/costumes/man/precision_strike_-_manekin.glb` | GLB | 1.15 MB | `assets/characters/costumes/man/precision_strike_-_manekin.png` |
| `scary_cat_-_manekin` | `characters/costumes/man/scary_cat_-_manekin.glb` | GLB | 0.90 MB | `assets/characters/costumes/man/scary_cat_-_manekin.png` |
| `sleuths_verdict_-_manekin` | `characters/costumes/man/sleuths_verdict_-_manekin.glb` | GLB | 3.19 MB | — |



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
| `cathedral` | `maps/cathedral.glb` | GLB | 103.80 MB | — |
| `game_pirate_adventure_map` | `maps/game_pirate_adventure_map.glb` | GLB | 7.48 MB | — |
| `zone9_real_light` | `maps/zone9_real_light.glb` | GLB | 36.33 MB | — |



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
