# FlowMy & AI Workspace Directives

Quy chuẩn cấp cao áp dụng cho toàn bộ workspace **Flow-App** (WPF C#, AI Studio React/Vite, Python Server).

---

## ⚡ 1. Tốc Độ & Quy Trình Làm Việc (Speed & Workflow)
- **Sửa trực tiếp cho các tác vụ thường ngày**: Đối với fix bug, chỉnh sửa UI, bổ sung hàm nhỏ: Thực hiện ngay bằng tool sửa file, **KHÔNG** dừng lại để tạo `implementation_plan.md`.
- **Giao tiếp ngắn gọn**: Trả lời súc tích, đi thẳng vào file và đoạn code đã sửa, không viết giải thích lan man.

---

## 🎯 2. Độ Chuẩn Xác & Giữ Toàn Vẹn Code
- Luôn kiểm tra nội dung file trước khi sửa để nắm đúng ngữ cảnh.
- Bảo toàn comment, contracts và types hiện hữu; không tạo breaking changes.
- Đảm bảo 0 lỗi biên dịch sau khi hoàn tất.

---

## 🧱 3. Giới Hạn Modularity (Hard Limits)
- **Kích thước file**: Mục tiêu 150 – 500 dòng/file. **TUYỆT ĐỐI KHÔNG vượt quá 800 dòng/file**.
- **Kích thước hàm**: Dưới 50 – 80 dòng/hàm.
- Bắt buộc tách modular khi file phình to: tách sub-components, custom hooks, services hoặc partial classes.

---

## 🎨 4. Chuẩn Giao Diện & Framework
- **WPF / C#**: Tuân thủ theme token trong `FlowMy.Docs`, không hardcode màu raw, `Padding="0"` cho Button có kích thước cố định, thread-safe (không gọi `Dispatcher.Invoke` chặn luồng background).
- **React / TS**: Strict types, tách rời UI và State Logic, tối ưu memoization cho canvas/animation.
