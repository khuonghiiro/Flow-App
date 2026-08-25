# AI Image Animation Studio & API Engine

> **Port**: `3979`  
> **Target Hardware**: NVIDIA GeForce RTX 3060 12GB VRAM (or any CUDA / CPU fallback)  
> **API Docs**: `http://localhost:3979/docs` (Swagger UI) & `http://localhost:3979/redoc`

Hệ thống AI Image Animation chuyên dụng tạo chuyển động mềm mại, tự nhiên từ ảnh tĩnh (tóc bay theo gió, tà áo phất phơ, vải lụa, sóng nước, chuyển động môi trường), cung cấp REST API + WebSocket để các website hoặc ứng dụng bên ngoài có thể tích hợp và xây dựng giao diện tùy biến.

---

## 🌟 Tính Năng Nổi Bật

1. **Chuyên Biệt Tóc & Quần Áo (Hair & Cloth Physics)**:
   - **Brush Masking**: Cọ quét vùng tóc, tà áo với tính năng Feathering (viền mềm) tự nhiên.
   - **Motion Flow Vectors**: Kéo thả trực tiếp mũi tên xác định hướng gió và lực gió trên từng vùng tóc/áo.
   - **Anchor Pin Points**: Đặt ghim cố định khuôn mặt, cơ thể để không bị méo hình khi gió thổi.
   - **Multi-Harmonic Wind Turbulence**: Mô phỏng dao động đa tần số (lọn tóc bay nhấp nhô, tà áo lượn sóng).

2. **Dual-Engine Architecture**:
   - **Engine 1: Neural Vector Mesh Flow Engine**: Render siêu tốc 60 FPS, tiêu thụ < 1GB VRAM, tạo vòng lặp vô tận (Seamless Loop) chất lượng 4K.
   - **Engine 2: Generative Diffusion Motion Engine**: Tối ưu cho RTX 3060 12GB VRAM với FP16, VAE Slicing và Sequential CPU Offloading.

3. **Mở Rộng API Cho Ứng Dụng Khác**:
   - Bật CORS toàn diện (`*`), hỗ trợ JSON REST API + WebSocket tiến trình thực.
   - Xuất đa định dạng: `MP4` (H.264), `WebM` (VP9), `GIF` (High-Quality Palettegen), `APNG`, `PNG Sequence`.

---

## 🚀 Hướng Dẫn Cài Đặt & Chạy

### 1. Cài đặt môi trường (chỉ chạy lần đầu):
Nhấp đúp chuột vào file:
```cmd
setup_env.bat
```
*(Script sẽ tự động tạo thư mục `.venv` cục bộ, cài đặt PyTorch với CUDA 12.1 và các thư viện cần thiết)*

### 2. Khởi chạy Server:
Nhấp đúp chuột vào file:
```cmd
run_server.bat
```
Truy cập:
- Studio Web UI: `http://localhost:3979`
- API Documentation (Swagger): `http://localhost:3979/docs`
- ReDoc: `http://localhost:3979/redoc`

---

## 📡 Tài Liệu API Dành Cho Website Khác Tích Hợp

### 1. Kiểm tra trạng thái GPU & VRAM (`GET /api/health`):
```bash
curl -X GET "http://localhost:3979/api/health"
```

### 2. Lấy danh sách Preset gió (`GET /api/presets`):
```bash
curl -X GET "http://localhost:3979/api/presets"
```

### 3. Tạo chuyển động ảnh tĩnh (`POST /api/animate/flow`):
```json
{
  "image": "data:image/png;base64,...",
  "mask": "data:image/png;base64,...",
  "vectors": [
    { "start_x": 0.4, "start_y": 0.3, "end_x": 0.6, "end_y": 0.28, "strength": 1.2 }
  ],
  "pins": [
    { "x": 0.5, "y": 0.5 }
  ],
  "preset": "gentle_breeze",
  "wind_strength": 1.0,
  "turbulence": 0.6,
  "duration_seconds": 3.0,
  "fps": 30,
  "format": "mp4",
  "loop_mode": "seamless_phase"
}
```

---

## 📐 Tiêu Chuẩn Phân Tách Code (Modularity Rules)
- Mọi file code trong dự án tuân thủ nghiêm ngặt quy tắc **không vượt quá 800 - 1000 dòng**.
- Tách biệt rõ ràng tầng `schemas`, `core`, `api/endpoints`, `utils`, `static/css`, `static/js`.
