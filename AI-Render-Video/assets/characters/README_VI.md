# 👤 THƯ MỤC NHÂN VẬT (CHARACTERS)

## 📂 1. Cấu Trúc Phân Loại Nam / Nữ & Lắp Ráp Modular

```text
assets/characters/
├── male/ (hoặc man/)          # 🧑 Model nhân vật Nam hoàn chỉnh (.glb, .vrm) + ảnh tham chiếu (.png, .jpg)
├── female/ (hoặc woman/)      # 👩 Model nhân vật Nữ hoàn chỉnh (.glb, .vrm) + ảnh tham chiếu (.png, .jpg)
├── base_bodies/               # 👤 Thân hình cơ bản / manekin có khung xương Humanoid (.vrm, .glb)
├── faces/                     # 🎭 Khuôn mặt thay thế rời (.glb)
├── hairstyles/                # 💇 Mái tóc rời (.glb)
├── beards/                    # 🧔 Các kiểu râu (.glb)
├── costumes/                  # 👘 Trang phục, áo bào (.glb)
└── accessories/               # 👑 Phụ kiện đeo người (mũ, vương miện, khăn quàng,...) (.glb)
```

---

## 🖼️ 2. Quy Tắc Ghép Ảnh Tham Chiếu (Reference Preview) & Ảnh Độc Lập

1. **Khi có cả Model 3D (`.glb` / `.vrm`) và Ảnh cùng tên (`.png` / `.jpg` / `.webp`):**
   - Hệ thống **chỉ nạp 1 Model 3D duy nhất**, và **tự động gắn ảnh đó làm ảnh hiển thị Preview/Thumbnail** để bạn nhìn thấy chân dung nhân vật trong Asset Browser & Inspector.
   - Không bị trùng lặp tài nguyên (ảnh không bị tách thành một file riêng).
   - Ví dụ:
     - `male/kiem_khach_nam.glb` + `male/kiem_khach_nam.png` ➔ Load 1 model `kiem_khach_nam.glb` có thumbnail `kiem_khach_nam.png`.
     - `female/phap_su_nu.vrm` + `female/phap_su_nu.png` ➔ Load 1 model `phap_su_nu.vrm` có thumbnail `phap_su_nu.png`.

2. **Khi chỉ có file Ảnh (không có model 3D cùng tên):**
   - Hệ thống sẽ nhận diện ảnh đó là tài nguyên hình ảnh độc lập (Texture / VFX / Banner 2D).

---

## 🤖 3. Cách Dùng Trong Kịch Bản JSON

### Cách 1: Sử dụng Model có sẵn trong thư mục Nam/Nữ
```json
{
  "id": "actor_main",
  "name": "Lý Tiên Sinh",
  "model": "characters/male/kiem_khach_nam.glb",
  "spawn_point": [0, 0, 0]
}
```

### Cách 2: Lắp ráp Modular Appearance
```json
{
  "id": "actor_custom",
  "name": "Nữ Pháp Sư",
  "model": "characters/female/sample_avatar.vrm",
  "assembly": {
    "base_body": "characters/base_bodies/female_body.vrm",
    "face": "characters/faces/face_female_01.glb",
    "hairstyle": "characters/hairstyles/hair_long.glb",
    "costume": "characters/costumes/costume_robe_blue.glb"
  }
}
```
