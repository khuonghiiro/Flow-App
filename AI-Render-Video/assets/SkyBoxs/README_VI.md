# 🌌 Hướng Dẫn Quản Lý Tài Nguyên SkyBox 360°

Thư mục này chứa toàn bộ các ảnh toàn cảnh **Skybox 360° (Equirectangular panorama)** được phân chia khoa học theo **Khoảng thời gian trong ngày** và **Mức độ mây / Thời tiết**.

---

## 📂 Cấu Trúc Thư Mục SkyBoxs

```text
assets/SkyBoxs/
├── binh_minh/                  # 🌅 Bình minh (Rạng đông / Dawn)
│   ├── khong_may/              # Trời quang đãng, không mây (Độ phủ mây 0% - 25%)
│   ├── it_may/                 # Mây rải rác nhẹ (Độ phủ mây 25% - 65%)
│   └── nhieu_may/              # Mây dày phủ kín (Độ phủ mây 65% - 100%)
├── buoi_sang/                  # 🌤️ Buổi sáng (Morning)
│   ├── khong_may/
│   ├── it_may/
│   └── nhieu_may/
├── buoi_trua/                  # ☀️ Buổi trưa (Noon / Nắng gắt đỉnh đầu)
│   ├── khong_may/
│   ├── it_may/
│   └── nhieu_may/
├── buoi_chieu/                 # 🌇 Buổi chiều / Hoàng hôn (Afternoon / Sunset / Dusk)
│   ├── khong_may/
│   ├── it_may/
│   └── nhieu_may/
├── buoi_toi/                   # 🌙 Buổi tối / Đêm sao (Night / Midnight)
│   ├── khong_may/
│   ├── it_may/
│   └── nhieu_may/
└── giong_bao/                  # ⛈️ Giông bão / Mưa lớn (Thunderstorm / Heavy Overcast)
    ├── it_may/
    └── nhieu_may/
```

---

## ⚙️ Cơ Chế Tự Động Load Của Studio & AI
1. **Theo thời gian bầu trời (`sky_time`)**:
   - `sunrise` (Bình minh) ➔ Quét thư mục `binh_minh/`
   - `morning` (Buổi sáng) ➔ Quét thư mục `buoi_sang/`
   - `noon` (Buổi trưa) ➔ Quét thư mục `buoi_trua/`
   - `sunset` / `afternoon` (Buổi chiều / Hoàng hôn) ➔ Quét thư mục `buoi_chieu/`
   - `night` (Buổi tối / Đêm) ➔ Quét thư mục `buoi_toi/`
   - Khi có mưa lớn / giông bão (`rain_intensity > 0.6` hoặc bão) ➔ Quét thư mục `giong_bao/`

2. **Theo độ phủ mây (`cloud_coverage`)**:
   - `0% - 25%`: Lấy ảnh từ folder `khong_may/`
   - `25% - 65%`: Lấy ảnh từ folder `it_may/`
   - `65% - 100%`: Lấy ảnh từ folder `nhieu_may/`

3. **Chọn ngẫu nhiên khi có nhiều ảnh**:
   - Bạn có thể thả bao nhiêu ảnh tùy thích vào mỗi thư mục (ví dụ: `sky_01.png`, `sky_02.hdr`, `sky_03.jpg`,...). Studio sẽ tự động lấy ngẫu nhiên một ảnh trong thư mục tương ứng!

---

## 🎨 Định Dạng Hỗ Trợ
- **Ảnh 360° Panorama**: `.hdr`, `.exr`, `.png`, `.jpg`, `.jpeg`, `.webp`
- **Tỉ lệ khuyến nghị**: `2:1` (ví dụ: `2048x1024`, `4096x2048`, `8192x4096`)
