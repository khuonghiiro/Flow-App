# 🎨 THƯ MỤC TÀI NGUYÊN TỔNG HỢP (ASSETS ROOT)

> **Dành cho User đọc.** File README.md (không có _VI) dành cho AI đọc.

---

## 📌 1. Mục Đích & Vai Trò

Thư mục `assets/` là kho chứa toàn bộ tài nguyên đầu vào (mô hình 3D, textures, âm thanh, hoạt ảnh, bản đồ) phục vụ cho hệ thống AI và Studio dựng phim 3D.

- **Bạn chỉ cần:** Thả các file nhân vật, đạo cụ, âm thanh, hoạt ảnh vào đúng thư mục con tương ứng.
- **AI sẽ tự động:** Quét danh mục tài nguyên để biết studio có những gì, từ đó lên kịch bản dựng phim.

---

## 📂 2. Cấu Trúc Thư Mục

```text
assets/
├── ASSET_CATALOG.md             # ⭐ Tự sinh bởi _scan_assets.bat (cho AI đọc)
├── ASSET_CATALOG_VI.md          # ⭐ Bản tiếng Việt (cho bạn đọc)
├── asset_manifest.json          # JSON cho code runtime load
├── characters/
│   ├── male/                    # 🧑 Nhân vật Nam (.glb, .vrm + ảnh tham chiếu .png)
│   ├── female/                  # 👩 Nhân vật Nữ (.glb, .vrm + ảnh tham chiếu .png)
│   ├── base_bodies/             # Thân hình cơ bản / manekin (.vrm, .glb)
│   ├── faces/                   # Khuôn mặt thay đổi được (.glb)
│   ├── hairstyles/              # Kiểu tóc (.glb)
│   ├── beards/                  # Râu (.glb)
│   ├── costumes/                # Trang phục (.glb)
│   └── accessories/             # Phụ kiện (vương miện, khăn quàng,...) (.glb)
├── props/
│   ├── weapons/                 # Vũ khí (kiếm, trượng phép, cung,...) (.glb)
│   ├── tools/                   # Dụng cụ (cuốc, bình tưới,...) (.glb)
│   ├── consumables/             # Đồ tiêu hao (ly trà, bình rượu,...) (.glb)
│   ├── furniture/               # Nội thất (ghế, bàn, giường,...) (.glb)
│   ├── buildings/               # Công trình (nhà gỗ lv1, nhà đá lv2,...) (.glb)
│   ├── nature/                  # Cây cối, đá, bụi hoa (.glb)
│   └── vehicles/                # Phương tiện (kiếm bay, cưỡi mây,...) (.glb)
├── maps/                        # Bản đồ môi trường (.glb, .gltf)
├── SkyBoxs/                     # 🌌 Ảnh toàn cảnh 360° theo thời gian & độ phủ mây
│   ├── binh_minh/               # Bình minh (khong_may, it_may, nhieu_may)
│   ├── buoi_sang/               # Buổi sáng (khong_may, it_may, nhieu_may)
│   ├── buoi_trua/               # Buổi trưa (khong_may, it_may, nhieu_may)
│   ├── buoi_chieu/              # Buổi chiều/hoàng hôn (khong_may, it_may, nhieu_may)
│   ├── buoi_toi/                # Buổi tối/đêm (khong_may, it_may, nhieu_may)
│   └── giong_bao/               # Giông bão/mưa lớn (it_may, nhieu_may)
├── audio/
│   ├── bgm/                     # Nhạc nền (.mp3, .wav)
│   ├── sfx/
│   │   ├── combat/              # Âm thanh chiến đấu
│   │   ├── interaction/         # Âm thanh tương tác
│   │   └── ambient/             # Âm thanh môi trường (gió, mưa)
│   └── dialogues/               # Giọng đọc lồng tiếng TTS
├── animations/
│   ├── combat/                  # Hoạt ảnh chiến đấu (.glb, .bvh)
│   ├── interaction/             # Hoạt ảnh tương tác (ngồi, uống,...)
│   ├── xianxia/                 # Hoạt ảnh tiên hiệp (thiền, vận công,...)
│   └── locomotion/              # Hoạt ảnh di chuyển (đi, chạy, bay)
└── vfx/                         # Texture hiệu ứng hình ảnh (.png)
```

---

## 🔧 3. Cách Sử Dụng

### Bước 1: Thả file vào thư mục
Ví dụ: thả `face_male_young.glb` vào `characters/faces/`

### Bước 2: Chạy `_scan_assets.bat`
Script sẽ quét tất cả thư mục và tự động tạo:
- `ASSET_CATALOG.md` — danh mục cho AI đọc
- `ASSET_CATALOG_VI.md` — danh mục tiếng Việt cho bạn đọc
- `asset_manifest.json` — JSON cho code

### Bước 3: Gửi cho AI
Copy nội dung `ASSET_CATALOG.md` → paste cho ChatGPT/Claude/Gemini và yêu cầu: "Dựa vào catalog này, tạo scene JSON phim tiên hiệp"

### Quy tắc đặt tên file
- Mô hình 3D: `{loại}_{tên}.glb` hoặc `{tên}.vrm`
- Âm thanh: `sfx_{hành_động}.mp3`, `bgm_{tâm_trạng}.mp3`
- Hoạt ảnh: `anim_{hành_động}.glb`

### Lắp ráp nhân vật modular
Nhân vật có thể được lắp ráp từ các bộ phận riêng lẻ — chỉ cần thay đổi path trong JSON:
- Đổi **mặt** → thay `face` field
- Đổi **tóc** → thay `hairstyle` field
- Đổi **trang phục** → thay `costume` field
- Thêm **phụ kiện** → thêm vào `accessories` array

---

## 🖼️ 4. Quy Tắc Load Model & Hiển Thị Preview

### Thứ tự chọn ảnh preview
1. **Ảnh cùng tên (ưu tiên cao nhất):** Nếu có file `.png`/`.jpg`/`.gif` trùng tên với model trong cùng thư mục → dùng làm preview.
   - VD: `trang_phuc_nu.glb` + `trang_phuc_nu.png` → preview = `trang_phuc_nu.png`
2. **`previewUrl` từ manifest:** Nếu `asset_manifest.json` chỉ định URL preview → dùng URL đó.
3. **Snapshot 3D tự động:** Nếu không có ảnh nào → engine render ảnh 3D headless và cache:
   - Cache RAM (trong phiên), IndexedDB (lưu qua reload), WebGL render (chỉ 1 lần)

### Cấu trúc thư mục model 3D

#### File trực tiếp (root children):
```
nhan_vat/trang_phuc/nu/
├── costume_female_warrior.glb        ← File model
├── costume_female_warrior.png        ← Ảnh preview (cùng tên = tự link)
├── another_dress.glb                 ← Model không có preview
└── random_preview.gif                ← Ảnh/GIF hiển thị trực tiếp
```

#### Thư mục con (FBX + textures):
```
nhan_vat/trang_phuc/nu/
└── precision-strike-manekina/        ← Thư mục con (root child)
    ├── source/                       ← Chứa model + textures
    │   ├── Manekina Precision Strike.fbx
    │   ├── Beyd_Avatar_Girl_Top_Tex_Diffuse.png
    │   └── ...
    └── textures/                     ← Vị trí texture thay thế
        └── ...
```

**Quy tắc cho thư mục con:**
- Engine quét **đệ quy** bên trong thư mục con
- Model 3D (`.fbx`, `.glb`, `.gltf`) trong thư mục con sẽ được đăng ký
- Textures **phải nằm cùng thư mục** với file model
- **Tên thư mục con** dùng làm tên hiển thị

### Nhận diện giới tính
Giới tính tự detect từ **đường dẫn thư mục**:
- Chứa `/nu/` hoặc `/female/` → **nữ**
- Chứa `/nam/` hoặc `/male/` → **nam**
- Còn lại → **unisex**

> ⚠️ Model đặt trong `nhan_vat/trang_phuc/nu/` sẽ phân loại là **nữ**.
> Không đặt model nam vào folder nữ và ngược lại.

### Quy tắc riêng cho FBX
- **Đơn vị:** FBX thường dùng **cm** (lớn gấp 100x so với GLB dùng m). Engine tự phát hiện và thu nhỏ về ~1.8m.
- **Textures:** FBX embed đường dẫn tuyệt đối từ máy export. Engine tự remap về filename → tìm cùng thư mục FBX.
- **Import qua UI:** Dùng nút **📂 Folder** để chọn cả thư mục chứa `.fbx` + textures.

---

## 📋 5. Giải Thích Các Trường Trạng Thái — `asset_structure.json` & `asset_manifest.json`

### `asset_structure.json` — Cấu Trúc Hệ Thống

File này định nghĩa **cấu trúc thư mục** và **cách hiển thị UI** cho hệ thống tài nguyên.

#### Trường trong Category (`character_structure.categories[]`)

| Trường | Kiểu | Giải thích |
|---|---|---|
| `id` | string | ID duy nhất, trùng với tên folder (VD: `trang_phuc`, `khuon_mat`) |
| `folder` | string | Tên folder vật lý trong `nhan_vat/` |
| `folder_aliases` | string[] | Tên folder thay thế cũng map vào category này (VD: `["costumes"]`) |
| `label` | string | Tên hiển thị trên UI (VD: `"Trang Phục"`) |
| `icon` | string | Emoji icon cho tab dọc |
| **`supports_gender`** | **boolean** | **`true`:** Category có chia folder `nam/`/`nu/` — engine quét riêng theo giới tính, UI hiện nút lọc ♂/♀. **`false`:** Không chia giới tính — tất cả item là `unisex`, ẩn nút lọc. |
| `default_gender` | string | Giới tính mặc định khi không detect được (`"male"`, `"female"`, hoặc `"unisex"`) |

#### Quy tắc giới tính (`gender_rules`)

| Trường | Kiểu | Giải thích |
|---|---|---|
| **`filter_enabled`** | **boolean** | **`true`:** Hiện nút lọc giới tính (♂/♀) trong Xưởng Nhân Vật. User có thể lọc theo giới tính. **`false`:** Ẩn nút lọc, hiện tất cả tài nguyên. |
| `options[]` | array | Danh sách lựa chọn giới tính với `id`, `key`, `label`, `icon`, `folder_aliases` |

### `asset_manifest.json` — Danh Mục Tài Nguyên

File này được **tạo tự động** bởi `scan_assets.js`, chứa toàn bộ danh sách tài nguyên đã quét.

#### Trường của mỗi item

| Trường | Kiểu | Giải thích |
|---|---|---|
| `id` | string | ID duy nhất tạo từ đường dẫn file |
| `name` | string | Tên hiển thị trên UI, tự format từ tên file |
| `filename` | string | Tên file gốc (VD: `"Manekina Precision Strike.fbx"`) |
| `relPath` | string | Đường dẫn tương đối từ `assets/` (VD: `"nhan_vat/trang_phuc/nu/model.glb"`) |
| `path` | string | Đường dẫn đầy đủ với prefix `assets/` để load URL |
| `format` | string | Định dạng file viết hoa (VD: `"GLB"`, `"FBX"`, `"VRM"`, `"PNG"`) |
| `sizeMB` | string | Dung lượng file tính bằng MB |
| `gender` | string | Giới tính detect được: `"male"`, `"female"`, hoặc `"unisex"` |
| `previewUrl` | string | URL ảnh preview (nếu có file `.png`/`.jpg` cùng tên) |
| `description` | string | Mô tả tài nguyên cho người đọc |
| **`isStandaloneImage`** | **boolean** | **`true`:** Đây là file ảnh độc lập (`.png`/`.jpg`/`.gif`) — KHÔNG phải texture của model 3D. Engine hiển thị trực tiếp ảnh này trong browser mà không load 3D. **`false`/vắng:** Đây là file model 3D có thể load vào scene. |
| **`isFolderBundle`** | **boolean** | **`true`:** Model 3D này nằm trong thư mục con cùng với các file texture. Engine dùng `LoadingManager` để resolve đường dẫn texture tương đối. Trường `bundleDir` chỉ ra thư mục cha. **`false`/vắng:** Model đơn lẻ (VD: `.glb` với texture nhúng sẵn). |
| `bundleDir` | string | Chỉ có khi `isFolderBundle: true`. Đường dẫn thư mục cha chứa bundle (VD: `"nhan_vat/trang_phuc/nu"`) |
