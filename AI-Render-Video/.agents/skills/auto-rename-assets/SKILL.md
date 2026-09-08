---
name: auto-rename-assets
description: Automatically or manually standardize display names for generated images and videos on Google Flow via mYWVGd RPC.
---

# Tự Động Đổi Tên Ảnh & Video Chuẩn Hóa Trên Google Flow (mYWVGd)

Kỹ năng này hướng dẫn AI tự động đổi tên hiển thị (display name) cho mọi asset ảnh và video được sinh ra trên Google Flow thông qua RPC `mYWVGd`.

---

## 🎯 1. Quy Chuẩn Đặt Tên & Điều Kiện Kích Hoạt

### Điều kiện áp dụng:
1. **Khi thực hiện tạo nhân vật & video theo Master Plan (`plans/plan_character_pipeline.md`)**:
   - **BẮT BUỘC** áp dụng chuẩn: `[{động tác gì - góc bao nhiêu độ}] {tên nhân vật - số thứ tự tạo}`.
2. **Khi User có yêu cầu đặt tên cụ thể**:
   - **Luôn lắng nghe và tuân thủ 100%** theo cách đặt tên User mong muốn (kể cả định dạng khác).
3. **Khi User KHÔNG yêu cầu đặt tên và KHÔNG theo quy trình `plan_character_pipeline.md`**:
   - **KHÔNG tự ý sửa tên**, giữ nguyên tên mặc định do hệ thống Flow sinh ra.

### Cấu trúc chuẩn khi chạy Master Plan:
```
[{động tác gì - góc bao nhiêu độ}] {tên nhân vật - số thứ tự tạo}
```

- Trong đó:
  - `{động tác gì}`: Hành động cụ thể (ví dụ: `đi bộ`, `đứng thở`, `vung kiếm`, `chạy`, `chuẩn bị`, `đứng yên`).
  - `{góc bao nhiêu độ}`: Góc quay camera (ví dụ: `0`, `45`, `90`, `135`, `180`, `225`, `270`, `315`).
  - `{tên nhân vật}`: Tên nhân vật hoặc chủ thể (ví dụ: `Liễu như viên`, `Hàn Lập`, `Tiêu Viêm`).
  - `{số thứ tự tạo}`: Đánh số 2 chữ số (`01`, `02`, `03`...) là số asset thứ mấy được tạo ra cho động tác và góc quay đó.

### 🎬 Ví dụ cho Video:
- `[đi bộ - 45] Liễu như viên - 01` *(video đi bộ góc 45 độ lần 1)*
- `[đi bộ - 45] Liễu như viên - 02` *(video đi bộ góc 45 độ lần 2 nếu render lại)*
- `[đứng thở - 0] Liễu như viên - 01` *(video idle loop 4s góc chính diện)*
- `[vung kiếm - 90] Liễu như viên - 01` *(video chém kiếm góc nhìn ngang)*

### 📸 Ví dụ cho Ảnh tĩnh:
- `[đứng yên - 0] Liễu như viên - 01`
- `[chuẩn bị - 45] Liễu như viên - 01`

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
    "title": "[đi bộ - 45] Liễu như viên - 01"
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
    "title": "[đi bộ - 45] Liễu như viên - 01",
    "project_id": "<PID>"
  }'

# Đổi tên Ảnh:
curl -X POST http://127.0.0.1:8100/api/flow/image/rename \
  -H "Content-Type: application/json" \
  -d '{
    "asset_id": "<IMAGE_ASSET_ID_HOẶC_MEDIA_ID>",
    "title": "[đứng yên - 0] Liễu như viên - 01",
    "project_id": "<PID>"
  }'
```
*Hệ thống tự động nhận diện cả UUID Media ID lẫn Node/Operation ID.*
