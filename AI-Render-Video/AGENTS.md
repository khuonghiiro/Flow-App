# Quy Tắc Bắt Buộc Dành Cho AI (AI Workspace Directives)

Tài liệu này được tự động nạp vào System Prompt của AI cho workspace **AI-Render-Video** và **Flow-App**. Mọi hành động của AI phải tuân thủ nghiêm ngặt các điều sau:

---

## ⚡ 1. Tốc Độ & Quy Trình Thực Hiện (Fast Execution)
- **Thực hiện trực tiếp, bỏ qua Planning rườm rà**: 
  - Đối với các yêu cầu sửa lỗi (bug fixes), chỉnh sửa UI, bổ sung hàm nhỏ hoặc tối ưu logic: **BẮT BUỘC thực hiện ngay bằng tool sửa file, KHÔNG tạo file `implementation_plan.md` hay chờ phê duyệt**.
  - Chỉ lập Plan khi: Người dùng yêu cầu rõ ràng, hoặc tái cấu trúc kiến trúc lớn ảnh hưởng toàn bộ project.
- **Phản hồi súc tích, đi thẳng vào vấn đề**: 
  - Tránh giải thích văn xuôi dài dòng, không nhắc lại những gì code đã thể hiện rõ.
  - Tóm tắt ngắn gọn các điểm chính đã sửa và hướng dẫn kiểm tra nhanh.

---

## 🎯 2. Độ Chính Xác & Bảo Toàn Code (Precision & Code Integrity)
- **Luôn đọc trước khi sửa**: Luôn view đúng file và các dòng code liên quan trước khi sửa để nắm chắc ngữ cảnh, tránh đoán code mò.
- **Bảo toàn tính tương thích**:
  - Giữ nguyên các comment, type definitions, và contract giao diện hiện hữu.
  - Không tự ý xóa code cũ không liên quan hoặc làm gãy luồng xử lý (breaking changes).
- **Kiểm tra sau khi sửa (Verify)**:
  - Nếu sửa file có thay đổi interface/types hoặc cấu trúc quan trọng, kiểm tra lại cú pháp hoặc chạy kiểm tra nhanh để đảm bảo 0 lỗi biên dịch.

---

## 🧱 3. Giới Hạn Modularity & Kích Thước File (Bắt Buộc)
- **File Size Limit**:
  - Kích thước lý tưởng: **150 – 500 dòng/file**.
  - Ngưỡng giới hạn cứng: **TUYỆT ĐỐI KHÔNG vượt quá 800 dòng**.
  - Nếu một file chuẩn bị vượt quá 800 dòng, AI **BẮT BUỘC** phải tách thành các sub-components, custom hooks, helper files hoặc service modules riêng trong thư mục con tương ứng.
- **Function/Method Limit**:
  - Mỗi hàm/phương thức tối đa **50 – 80 dòng**. Hàm phức tạp phải chia thành private helpers hoặc pure utilities.

---

## 💻 4. Tiêu Chuẩn Công Nghệ (Tech Stack Standards)
- **Frontend (React / TypeScript / Vite)**:
  - Strict TypeScript: Khai báo types rõ ràng, không lạm dụng `any`.
  - Tách biệt UI (JSX) và Logic (Custom Hooks / Stores).
  - Tối ưu hiệu năng Canvas/WebGL/SVG: Dùng `useCallback`, `useMemo`, tránh render lặp không cần thiết.
- **Backend (Python / FastAPI / AI Models)**:
  - Tách bạch `schemas/`, `api/endpoints/`, `core/` (AI Pipeline / GPU) và `utils/`.
  - Xử lý lỗi đầy đủ qua try-except và logging rõ ràng.

---

## 🎬 5. Quy Chuẩn Tạo Hoạt Ảnh 4s Loop Từ Tab 4 (Trợ Lý Prompt AI)
- **Cơ chế Start = End Frame (Seamless Loop)**:
  - Khi tạo video hoạt ảnh từ prompt Tab 4: Sau khi sinh ảnh gốc (`image_media_id`), **BẮT BUỘC gán `end_scene_media_id` = `image_media_id`**.
  - Gọi API `start_end_frame_2_video` (i2v_fl) với thời lượng **4s** để tạo chu kỳ chuyển động lặp vô tận (idle loop, breath, combat stance) hoàn hảo cho Sprite 2D.
  - Phông nền: Luôn giữ nguyên màu xanh đồng nhất `#00FF00` (Chroma Key) để nạp thẳng vào Tab 1.3 (Video Animation Slicer) tách nền tự động.

---

## 🛡️ 6. Quy Tắc Bất Khả Xâm Phạm Về Code Nguồn (Upstream Isolation)
- **TUYỆT ĐỐI KHÔNG sửa đổi bất kỳ file nào trong thư mục gốc `flowkit/`**:
  - Thư mục `flowkit/` là repo upstream của tác giả, dùng để cập nhật tính năng mới qua `git pull`. Mọi sửa đổi trực tiếp trong `flowkit/` sẽ gây xung đột (merge conflicts) khi nâng cấp.
  - Bắt buộc luôn giữ thư mục `flowkit/` ở trạng thái clean 100% (không có file modified hay untracked).
- **Toàn bộ logic mở rộng, vá lỗi, override BẮT BUỘC thực hiện trong `agent-veo3/`**:
  - Kế thừa và mở rộng động thông qua `agent-veo3/agent/flowkit_loader.py` và `agent-veo3/agent/extension_patcher.py` (runtime patching, hooks, wrappers, subclasses).
  - Thêm mới / ghi đè REST endpoints trong `agent-veo3/agent/api/veo3_routes.py`.
  - Mọi thay đổi về Chrome Extension (Manifest, background script, popup, content script) phải nằm độc quyền trong `agent-veo3/extension/`.

---

## 🏷️ 7. Quy Chuẩn Đổi Tên Ảnh & Video (Auto-Rename via mYWVGd)
- **Điều kiện kích hoạt đổi tên**:
  1. **Khi thực hiện theo quy trình Master Plan (`plans/plan_character_pipeline.md`)**:
     - **BẮT BUỘC** đổi tên toàn bộ ảnh mốc và video tạo ra theo đúng quy chuẩn:
       `[{động tác gì - góc bao nhiêu độ}] {tên nhân vật - số thứ tự tạo}`
       - `{động tác gì}`: Hành động cụ thể (ví dụ: `đi bộ`, `đứng thở`, `vung kiếm`, `chạy`, `chuẩn bị`, `đứng yên`).
       - `{góc bao nhiêu độ}`: Góc quay (ví dụ: `0`, `45`, `90`, `135`, `180`, `225`, `270`, `315`).
       - `{tên nhân vật}`: Tên nhân vật / chủ thể (ví dụ: `Liễu như viên`, `Hàn Lập`).
       - `{số thứ tự tạo}`: Đánh số (`01`, `02`...) là số video/ảnh thứ mấy tạo ở động tác và góc đó.
       - **Ví dụ Video**: `[đi bộ - 45] Liễu như viên - 01`, `[vung kiếm - 90] Tiêu Viêm - 02`, `[đứng thở - 0] Hàn Lập - 01`
       - **Ví dụ Ảnh**: `[đứng yên - 0] Liễu như viên - 01`, `[chuẩn bị - 45] Liễu như viên - 01`
  2. **Khi User có yêu cầu cụ thể về tên asset**:
     - **BẮT BUỘC nghe theo và đặt tên chính xác theo ý muốn của User** (bất kể định dạng User yêu cầu).
  3. **Khi User KHÔNG bảo đặt tên và KHÔNG theo quy trình `plan_character_pipeline.md`**:
     - **KHÔNG tự ý sửa tên ảnh và video**, giữ nguyên tên mặc định do Google Flow tạo ra.
- **Endpoint đổi tên**:
  - `POST /api/flow/image/rename`
  - `POST /api/flow/video/rename`
  - Payload: `{"asset_id": "<asset_id_hoặc_media_id>", "title": "<tên_mới>", "project_id": "<project_id>"}`

