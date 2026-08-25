---
name: code-standards
description: Enforces file length limits (< 800-1000 lines), function limits (< 50-80 lines), modular separation of logic, and clean architecture across Python, JS, TS, HTML, CSS.
---

# Quy Chuẩn Kiến Trúc & Giới Hạn Mã Nguồn (Code Standards)

Quy tắc này áp dụng bắt buộc cho toàn bộ các file mã nguồn trong dự án (Python, JavaScript, TypeScript, CSS, HTML, ...):

## 1. Giới Hạn Kích Thước File (Bắt Buộc)
- **Kích thước lý tưởng**: 150 – 500 dòng/file.
- **Ngưỡng tối đa tuyệt đối**: **KHÔNG ĐƯỢC VƯỢT QUÁ 800 – 1.000 DÒNG** trên bất kỳ file nào.
- **Quy tắc phân tách khi vượt ngưỡng**:
  - Khi một file tiệm cận hoặc vượt quá giới hạn, BẮT BUỘC phải phân rã và tách nhỏ thành các module/sub-components chuyên biệt theo từng chức năng hoặc tầng logic.
  - Không nhồi nhét nhiều trách nhiệm (responsibilities) vào cùng một file.

## 2. Giới Hạn Kích Thước Hàm & Phương Thức
- Mỗi hàm (function/method) chỉ nên từ **20 – 50 dòng**, tối đa không vượt quá **80 dòng**.
- Các quy trình phức tạp phải được bẻ nhỏ thành các hàm phụ (helper functions), utility thuần túy hoặc service riêng.

## 3. Tách Biệt Rõ Ràng Các Tầng Logic (Separation of Concerns)
- **Python Backend**:
  - Tách riêng Cấu hình (`config.py`), Mô hình dữ liệu (`schemas/`), Quản lý tài nguyên phần cứng (`device_manager.py`), Tiền xử lý (`preprocessor.py`), Mô hình AI suy luận (`pipeline.py`), Hậu xử lý (`postprocessor.py`), và các API router độc lập (`endpoints/`).
- **Frontend (Web UI)**:
  - **CSS**: Tách thành các file chuyên trách: `variables.css` (tokens, theme), `layout.css` (bố cục khung), `components.css` (nút bấm, thanh trượt, thẻ), `turntable.css` (khung xoay 360 & con quay).
  - **JavaScript**: Tách thành các module riêng: `api.js` (giao tiếp mạng), `orbit_gizmo.js` (la bàn 3D), `turntable_viewer.js` (trình xoay 360), `gallery.js` (quản lý album kết quả), `app.js` (bộ điều phối chính).

## 4. Bảo Vệ Môi Trường & Mã Nguồn
- Luôn sử dụng môi trường ảo độc lập trong dự án (`venv` / `.venv`).
- Duy trì `.gitignore` chặt chẽ, ngăn ngừa checkpoint/trọng số AI lớn, cache, và thư mục môi trường bị đẩy lên git.
