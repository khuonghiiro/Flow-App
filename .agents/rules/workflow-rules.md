# Universal AI Workflow & Execution Guidelines

> Áp dụng cho toàn bộ dự án Flow-App và các sub-project.

## 1. Tối Ưu Tốc Độ Thực Hiện (Speed Optimization)
- **Thực hiện trực tiếp, không tạo Plan thừa**:
  - Đối với các yêu cầu sửa bug, đổi giao diện, thêm tính năng cục bộ: **Sửa file trực tiếp ngay lập tức**.
  - **KHÔNG** kích hoạt Planning mode tạo `implementation_plan.md` làm chậm dòng tương tác, trừ khi người dùng yêu cầu lập kế hoạch trước hoặc tái cấu trúc quy mô lớn.
- **Phản hồi súc tích**:
  - Tập trung vào code thay đổi, giải thích ngắn gọn 1-2 dòng, không viết văn giải thích rườm rà.

## 2. Độ Chính Xác & Bảo Toàn Mã Nguồn
- Đọc nội dung file trước khi sửa để nắm đúng context và line number.
- Bảo toàn comment, type definitions, và logic đang hoạt động ổn định.
- Không để phát sinh lỗi cú pháp hay lỗi compile sau khi sửa.
