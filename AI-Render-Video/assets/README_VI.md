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
