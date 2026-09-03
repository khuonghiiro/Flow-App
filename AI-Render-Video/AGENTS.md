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
