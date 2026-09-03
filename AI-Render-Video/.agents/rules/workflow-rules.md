# Workflow & Execution Guidelines for AI

> File này quy định cách AI tương tác và thực hiện task để đạt tốc độ cao nhất và độ chuẩn xác tối đa.

## 1. Tối Ưu Tốc Độ (Fast Path Execution)
- **Bỏ qua Planning cho task thông thường**: 
  - Khi người dùng yêu cầu sửa lỗi, đổi màu/icon, sửa logic tính toán, thêm component nhỏ, chỉnh CSS/layout: **LẬP TỨC thực hiện sửa code bằng tool**.
  - **KHÔNG** kích hoạt Planning Mode tạo `implementation_plan.md` rồi dừng lại chờ đợi, làm gián đoạn dòng công việc.
  - Chỉ lập Plan khi: Task yêu cầu thay đổi kiến trúc toàn diện hoặc người dùng bảo "lập kế hoạch trước".

## 2. Quy Trình Sửa Code Chuẩn Xác (Precision)
1. **Kiểm tra trước khi sửa**: Luôn đọc (view) đoạn code cần sửa để hiểu context xung quanh, không đoán mò line number hay cú pháp.
2. **Sửa tập trung**: Sử dụng `replace_file_content` hoặc `multi_replace_file_content` với khối thay đổi rõ ràng, chính xác.
3. **Giữ tính toàn vẹn**: Không xóa mất các import, type định nghĩa sẵn hoặc comment quan trọng.
4. **Xác minh**: Đảm bảo không làm gãy build hoặc phát sinh lỗi cú pháp mới.

## 3. Giao Tiếp Tinh Gọn (Concise Communication)
- Không viết giải thích dài dòng hay nhắc lại toàn bộ mã nguồn.
- Chỉ thông báo ngắn gọn:
  - File đã sửa và vị trí sửa.
  - Tóm tắt 1-2 câu về giải pháp đã áp dụng.
  - Hướng dẫn nhanh người dùng cách kiểm tra trên UI nếu cần.
