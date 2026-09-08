---
name: auto-rename-assets
description: Automatically or manually standardize display names for generated images and videos on Google Flow via mYWVGd RPC.
---

# Tự Động Đổi Tên Ảnh & Video Chuẩn Hóa Trên Google Flow (mYWVGd)

Kỹ năng này hướng dẫn AI tự động đổi tên hiển thị (display name) cho mọi asset ảnh và video được sinh ra trên Google Flow thông qua RPC `mYWVGd`.

---

## 🎯 1. Quy Chuẩn Đặt Tên Bắt Buộc

Khi người dùng yêu cầu tạo ảnh hoặc video (hoạt ảnh 4s loop Sprite 2D, clip hành động, bối cảnh), **AI BẮT BUỘC** áp dụng quy chuẩn đặt tên:

1. **Cho Ảnh Tĩnh (Images)**:
   ```
   [IMG] {Tên Nhân Vật / Chủ Thể} - {Tư Thế / Góc Nhìn} - {Tag}
   ```
   *Ví dụ:* `[IMG] Han Lang Phong - Idle Stance - 01`

2. **Cho Video (Videos)**:
   ```
   [VID] {Tên Nhân Vật / Chủ Thể} - {Hành Động} - {Thời lượng}s Loop - {Tag}
   ```
   *Ví dụ:* `[VID] Han Lang Phong - Breathing Idle - 4s Loop - 01`

---

## ⚡ 2. Các Cách Đổi Tên

### Cách 1: Truyền trực tiếp khi Generate (Khuyên Dùng)
Khi gọi `/api/flow/generate-image` hoặc `/api/flow/generate-video`, truyền thêm trường `"title"` (hoặc `"display_name"`):
```bash
curl -X POST http://127.0.0.1:8100/api/flow/generate-video \
  -H "Content-Type: application/json" \
  -d '{
    "start_image_media_id": "<IMAGE_MEDIA_ID>",
    "end_image_media_id": "<IMAGE_MEDIA_ID>",
    "prompt": "...",
    "duration": 4.0,
    "duration_s": 4,
    "project_id": "<PID>",
    "title": "[VID] Han Lang Phong - Combat Stance - 4s Loop - 01"
  }'
```

### Cách 2: Gọi Endpoint Đổi Tên Sau Khi Tạo
Nếu ảnh/video đã được tạo (qua batch hoặc luồng khác), gọi endpoint:
```bash
# Đổi tên Video:
curl -X POST http://127.0.0.1:8100/api/flow/video/rename \
  -H "Content-Type: application/json" \
  -d '{
    "asset_id": "<VIDEO_OPERATION_ID_HOẶC_MEDIA_ID>",
    "title": "[VID] Han Lang Phong - Breathing Idle - 4s Loop - 01",
    "project_id": "<PID>"
  }'

# Đổi tên Ảnh:
curl -X POST http://127.0.0.1:8100/api/flow/image/rename \
  -H "Content-Type: application/json" \
  -d '{
    "asset_id": "<IMAGE_ASSET_ID_HOẶC_MEDIA_ID>",
    "title": "[IMG] Han Lang Phong - Idle Stance - 01",
    "project_id": "<PID>"
  }'
```
*Hệ thống tự động nhận diện cả UUID Media ID lẫn Node/Operation ID.*
