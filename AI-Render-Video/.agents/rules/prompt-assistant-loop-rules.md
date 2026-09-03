# Quy Tắc Tạo Hoạt Ảnh 4s Loop Từ Tab 4 (Trợ Lý Prompt AI)

> Áp dụng khi người dùng yêu cầu AI tạo ảnh và chuyển ảnh thành video animation từ Tab 4 ("4. Trợ Lý Prompt AI") hoặc pipeline `agent-veo3`.

---

## 🎯 1. Quy Trình Cốt Lõi: Start Frame = End Frame (Seamless Loop)
Để tạo chuyển động mượt mà lặp vô tận (cho nhân vật 2D, sprite game, VFX):
1. **Bước 1 (Tạo ảnh tĩnh)**: 
   - Sử dụng prompt từ Tab 4 (Cơ sở nhân vật, góc xoay 0/45/90/135/180°, vũ khí, tư thế).
   - Bắt buộc giữ phông nền xanh đồng nhất: `chroma key green screen background #00FF00`.
   - Sinh ảnh qua API `GENERATE_IMAGE` -> Thu được `image_media_id`.

2. **Bước 2 (Gán Khung Đầu = Khung Cuối)**:
   - **BẮT BUỘC gán `end_scene_media_id` = `image_media_id`**.
   - Cùng một ảnh vừa làm `startImage` vừa làm `endImage`.

3. **Bước 3 (Thời Lượng 4 Giây & Prompt Animation)**:
   - Thiết lập thời lượng hoạt ảnh là **4s** (chuẩn loop ngắn).
   - Sử dụng cấu trúc `transition_prompt` lặp khép kín:
     ```text
     4-second seamless looping animation, starting and ending at the exact same pose. Smooth natural subtle movement that returns perfectly to initial frame. Centered subject, fixed camera, solid chroma green background #00FF00 preserved with zero artifacts.
     ```

4. **Bước 4 (Khởi tạo Video Loop qua Veo3)**:
   - Gửi yêu cầu `GENERATE_VIDEO` (kích hoạt chế độ `start_end_frame_2_video`).
   - Video hoàn thành sẽ chuyển tiếp trực tiếp vào **Tab 1.3: Video Animation Slicer & AI Matting** để tách nền xanh và cắt frame sprite sheet.

---

## ⚠️ 2. Các Lưu Ý Bắt Buộc
- **Không dùng ảnh khác cho end_frame** khi mục tiêu là tạo sprite animation loop; chỉ dùng ảnh khác khi làm hiệu ứng chuyển cảnh (scene transition).
- **Phông nền Chroma**: Giữ nguyên mã màu `#00FF00` để các module tách nền tự động (Rembg/AI Matting) hoạt động chuẩn xác 100%.
