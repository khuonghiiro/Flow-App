---
name: sprite-loop-4s
description: Generate seamless 4-second looping sprite animations by using the same image as start frame and end frame via agent-veo3.
---

# Quy Trình Tạo Hoạt Ảnh 4s Lặp Vô Tận (Seamless Loop) Cho Sprite 2D

Kỹ năng này kết hợp giữa **Tab 4: Trợ Lý Prompt AI** và pipeline **agent-veo3** (`http://127.0.0.1:8100`).

## 1. Mục Tiêu
Biến ảnh tĩnh nhân vật 2D / vũ khí / VFX thành hoạt ảnh lặp vô tận (looping video 4s) bằng cách:
- Frame 0s (bắt đầu): `start_image`
- Frame 4s (kết thúc): `end_image` = `start_image` (cùng 1 ảnh)
- Giữ nguyên màu nền Chroma Key xanh `#00FF00`

## 2. Quy Trình Gọi API agent-veo3

1. **Tạo ảnh tĩnh từ prompt Tab 4**:
   ```bash
   curl -X POST http://127.0.0.1:8100/api/requests/batch \
     -H "Content-Type: application/json" \
     -d '{"requests": [{"type": "GENERATE_IMAGE", "scene_id": "<SID>", "project_id": "<PID>", "video_id": "<VID>", "orientation": "HORIZONTAL"}]}'
   ```
2. **Gán Start Frame = End Frame**:
   ```bash
   curl -X PATCH http://127.0.0.1:8100/api/scenes/<SID> \
     -H "Content-Type: application/json" \
     -d '{"horizontal_end_scene_media_id": "<image_media_id>"}'
   ```
3. **Cập nhật Prompt Chuyển Động Lặp 4s**:
   ```bash
   curl -X PATCH http://127.0.0.1:8100/api/scenes/<SID> \
     -H "Content-Type: application/json" \
     -d '{"transition_prompt": "4-second seamless looping animation, starting and ending at the exact same pose, smooth idle/breathing loop, static camera, solid chroma green #00FF00 background."}'
   ```
4. **Tạo Video Loop**:
   ```bash
   curl -X POST http://127.0.0.1:8100/api/requests/batch \
     -H "Content-Type: application/json" \
     -d '{"requests": [{"type": "GENERATE_VIDEO", "scene_id": "<SID>", "project_id": "<PID>", "video_id": "<VID>", "orientation": "HORIZONTAL"}]}'
   ```
5. **Chuyển tiếp vào Studio 2D**:
   - Mở **Tab 1.3: Video Animation Slicer & AI Matting** để tách nền xanh và cắt ra sprite sheet PNG.
