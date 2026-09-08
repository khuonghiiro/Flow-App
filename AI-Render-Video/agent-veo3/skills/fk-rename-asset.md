# /fk-rename-asset — Tự Động Đổi Tên Ảnh & Video Chuẩn Hóa Trên Google Flow (mYWVGd)

Kỹ năng tự động hoặc thủ công đổi tên hiển thị (display name) cho ảnh tĩnh và video clip trên Google Flow Web UI thông qua batch RPC `mYWVGd`.

Usage: 
- Đổi tên trực tiếp: `/fk-rename-asset <asset_id_hoặc_media_id> "<tên_mới>" [project_id]`
- Gọi REST: `POST http://127.0.0.1:8100/api/flow/video/rename` hoặc `POST http://127.0.0.1:8100/api/flow/image/rename`

---

## 🎯 1. Quy Chuẩn Đặt Tên Bắt Buộc (Naming Conventions)

Khi AI tạo bất kỳ ảnh hoặc video nào (video hoạt ảnh 4s loop, video hành động, clip câu chuyện, cảnh nối tiếp), **BẮT BUỘC** gán hoặc đổi tên asset ngay trên Google Flow theo cấu trúc:

### 📸 Cho Ảnh Tĩnh (Images):
```
[IMG] {Tên Nhân Vật / Chủ Thể} - {Tư Thế / Góc Nhìn} - {Tag/Số thứ tự}
```
*Ví dụ:*
- `[IMG] Han Lang Phong - Idle Stance - 01`
- `[IMG] Cyber Samurai - Front View - 02`
- `[IMG] Magic Staff Weapon - Item Icon - 01`

### 🎬 Cho Video Hoạt Ảnh (Videos):
```
[VID] {Tên Nhân Vật / Chủ Thể} - {Hành Động} - {Thời lượng}s Loop - {Tag/Số thứ tự}
```
*(Nếu video thường không loop thì bỏ chữ Loop, ví dụ `{Thời lượng}s`)*
*Ví dụ:*
- `[VID] Han Lang Phong - Breathing Idle - 4s Loop - 01`
- `[VID] Cyber Samurai - Sword Slash - 4s Loop - 02`
- `[VID] Flying Dragon - Wing Flap - 4s Loop - 01`
- `[VID] Battlefield Intro - Camera Pan - 8s - 01`

---

## ⚡ 2. Hai Cách Thực Hiện Đổi Tên

### Cách 1: Tự Động Đổi Tên Ngay Khi Gọi Sinh (Khuyên Dùng)
Khi gọi API `/api/flow/generate-image` hoặc `/api/flow/generate-video`, truyền trực tiếp trường `"title"` (hoặc `"display_name"`). Backend `agent-veo3` sẽ tự động kích hoạt RPC `mYWVGd` để đổi tên ngay sau khi tác vụ được gửi lên Google Cloud:

```bash
# Tạo video kèm tự động đặt tên:
curl -X POST http://127.0.0.1:8100/api/flow/generate-video \
  -H "Content-Type: application/json" \
  -d '{
    "start_image_media_id": "<IMAGE_MEDIA_ID>",
    "end_image_media_id": "<IMAGE_MEDIA_ID>",
    "prompt": "seamless idle loop, martial arts combat stance, green background #00FF00",
    "duration": 4.0,
    "duration_s": 4,
    "project_id": "<PID>",
    "title": "[VID] Han Lang Phong - Combat Stance - 4s Loop - 01"
  }'
```

### Cách 2: Đổi Tên Sau Khi Đã Tạo (Bằng Endpoint Rename)
Sau khi tạo ảnh hoặc video từ bất kỳ nguồn nào (kể cả tạo qua batch `/api/requests/batch` hoặc script khác):

1. **Đổi tên Video**:
```bash
curl -X POST http://127.0.0.1:8100/api/flow/video/rename \
  -H "Content-Type: application/json" \
  -d '{
    "asset_id": "<VIDEO_OPERATION_ID_HOẶC_MEDIA_ID>",
    "title": "[VID] Han Lang Phong - Breathing Idle - 4s Loop - 01",
    "project_id": "<PID>"
  }'
```

2. **Đổi tên Ảnh**:
```bash
curl -X POST http://127.0.0.1:8100/api/flow/image/rename \
  -H "Content-Type: application/json" \
  -d '{
    "asset_id": "<IMAGE_ASSET_ID_HOẶC_MEDIA_ID>",
    "title": "[IMG] Han Lang Phong - Idle Stance - 01",
    "project_id": "<PID>"
  }'
```

> **Ghi chú thông minh**: Backend đã có cơ chế tự động phân giải (`resolve_asset_id`): Bạn có thể truyền thẳng `media_id` (UUID ảnh hoặc UUID video) mà không cần phải tìm thủ công Node ID, hệ thống sẽ tự động tra cứu project graph và đổi tên chính xác 100%.

---

## 🔄 3. Tích Hợp Vào Quy Trình Khi Người Dùng Yêu Cầu Tạo Video Khác

Bất cứ khi nào người dùng yêu cầu:
- *"Tạo cho tôi video nhân vật X đang làm hành động Y"*
- *"Tạo video loop 4s từ ảnh này"*
- *"Tạo một loạt hoạt ảnh combat"*

**AI phải thực hiện tuần tự:**
1. Sinh ảnh (hoặc lấy ảnh có sẵn) -> Đổi tên thành `[IMG] ...`.
2. Tạo video với prompt hành động phù hợp.
3. **Ngay sau khi có kết quả video** (hoặc ngay lúc submit): Gọi API đổi tên video thành `[VID] {Nhân Vật} - {Hành Động} - {Thời lượng}s Loop - {Tag}`.
4. Báo cáo lại cho người dùng ID và Tên hiển thị rõ ràng trên Google Flow.
