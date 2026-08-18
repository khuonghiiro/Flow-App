---
name: code-modularization
description: >-
  Enforces strict file length limits (Hard Max: 1000 lines, Proactive Refactor: >= 500 lines),
  method limits (max 80 lines), and proactive separation of concerns across Python (.py),
  JavaScript (.js), CSS (.css), and HTML (.html). Use when creating, modifying, or refactoring
  code to keep architecture decoupled, maintainable, and prevent monolithic file bloat.
---

# Quy Chuẩn Tách Logic & Module Hóa Code (Code Modularization Standard)

Quy chuẩn này được thiết lập để đảm bảo mã nguồn luôn tinh gọn, dễ bảo trì (`maintainable`), có tính module hóa cao (`modular`), và tuân thủ nguyên lý Đơn Trách Nhiệm (**Single Responsibility Principle - SRP**).

---

## 1. Các Ngưỡng Giới Hạn Kích Thước File & Hàm

| Tiêu Chí | Ngưỡng Tối Đa (Hard Limit) | Ngưỡng Cảnh Báo & Tách Sẵn (Soft Limit) | Hành Động Bắt Buộc |
| :--- | :--- | :--- | :--- |
| **Độ dài File (.py, .js, .css, .html)** | **1000 dòng** | **500 - 600 dòng** | Khi file đạt từ 500 dòng trở lên, bắt buộc phải xem xét tách nhỏ logic sang các module vệ tinh riêng biệt. Tuyệt đối không để file vượt quá 1000 dòng. |
| **Độ dài Hàm / Phương thức** | **80 dòng** | **40 - 50 dòng** | Chia nhỏ hàm thành các helper functions / sub-routines có tên tường minh. |
| **Mức độ phụ thuộc (Coupling)** | Thấp (Loose Coupling) | Cao (High Cohesion) | Mỗi file chỉ chịu trách nhiệm cho 1 phạm vi nghiệp vụ cụ thể. |

---

## 2. Hướng Dẫn Tách Logic Theo Từng Ngôn Ngữ

### 2.1. Đối Với Python (`.py`)
Khi một file Python bắt đầu phình to, tách nhỏ theo kiến trúc phân tầng:
1. **Schemas & Models**: Tách các Pydantic models, Enums, DTOs vào thư mục `models/` hoặc file `schemas.py`.
2. **Business / Engine Logic**: Tách các thuật toán xử lý dữ liệu nặng, AI engine, Audio processing vào thư mục `engines/`, `services/`, hoặc `processors/`.
3. **API Routing / Controllers**: Trong FastAPI/Flask, không nhồi nhét tất cả endpoint vào `server.py` hay `app.py`. Sử dụng `APIRouter` để tách thành các router riêng:
   - `routers/transcribe.py` (Xử lý speech-to-text)
   - `routers/diarization.py` (Xử lý phân tách nhân vật)
   - `routers/translation.py` (Xử lý dịch thuật)
   - `routers/models.py` (Xử lý quản lý model & VRAM)
4. **Utilities & Helpers**: Các hàm dùng chung (format string, đọc ghi file, detect hardware) chuyển vào thư mục `utils/`.

---

### 2.2. Đối Với JavaScript (`.js`)
Tránh tạo file JavaScript nguyên khối (`app.js` > 1000 dòng). Bóc tách thành các module ES6 hoặc các file chức năng riêng biệt:
1. **State Management (`state.js`)**: Quản lý biến toàn cục, cache timeline, danh sách nhân vật (`characterProfiles`), audio metadata.
2. **API Client (`api_client.js` / `services.js`)**: Chứa toàn bộ các hàm gọi `fetch()`, gọi API server, xử lý upload `FormData` và bắt lỗi mạng.
3. **UI Renderers (`ui_renderer.js` / `components/`)**:
   - `renderTimeline()`: Vẽ danh sách timeline phụ đề.
   - `renderCharacterProfiles()`: Vẽ danh sách mẫu giọng nhân vật.
   - `renderHardwareStatus()`: Vẽ thông tin phần cứng CPU/GPU.
4. **Event Handlers (`event_handlers.js`)**: Bắt sự kiện click chuột, phím tắt, kéo thả file (`drag-and-drop`), thay đổi dropdown select.
5. **Formatters & Helpers (`formatters.js`)**: Các hàm định dạng thời gian (`formatTimeSrt`, `formatTimeAss`), download file, show toast thông báo.

---

### 2.3. Đối Với CSS (`.css`)
Không dồn toàn bộ giao diện vào một file `style.css` vài nghìn dòng. Sử dụng cấu trúc module hóa:
1. **`base/variables.css`**: Khai báo CSS Variables (Design Tokens), màu sắc HSL, font chữ, Dark/Light theme.
2. **`layout/layout.css`**: Khung sườn header cố định (`.app-header-shell`), navigation tabs, viewport cuộn (`.app-content-shell`), sidebar.
3. **`components/`**:
   - `cards.css`: Thẻ hiển thị Swagger, Model Card, Stats Card.
   - `buttons.css`: Nút bấm, Icon Buttons, Glow Effects.
   - `timeline.css`: Giao diện danh sách mốc phụ đề, thẻ nhân vật.
   - `modal_toast.css`: Hộp thoại modal, thông báo Toast.

---

### 2.4. Đối Với HTML (`.html`)
1. **Tránh Inline Scripts & Styles**: 100% mã JavaScript và CSS phải được đặt ở file riêng, không viết inline trong thẻ `<style>` hoặc `<script>` trong HTML.
2. **Component Sectioning**: Chia rõ ràng các `<section id="...">` với comment phân tách cụ thể.
3. **SVG Icons**: Tránh lặp lại mã SVG raw hàng trăm dòng. Sử dụng `<svg class="icon"><use href="#icon-id"/></svg>` hoặc class icon chuẩn.

---

## 3. Quy Trình Refactor Tách File An Toàn (Safe Refactoring Workflow)

Khi phát hiện file vượt quá 500-1000 dòng hoặc khi xây dựng tính năng mới:
1. **Phân tích Trách Nhiệm (Audit)**: Liệt kê các nhóm chức năng trong file hiện tại.
2. **Tạo Module Mới**: Tạo file module mới trong thư mục tương ứng (`services/`, `routers/`, `utils/`, `components/`).
3. **Di chuyển & Export**: Chuyển các hàm/class liên quan sang file mới và export rõ ràng.
4. **Import & Backward Compatibility**:
   - Import module mới vào file gốc.
   - Giữ nguyên tên hàm/biến hoặc re-export (ví dụ `window.functionName = functionName` trong JS) để đảm bảo các thành phần khác gọi tới không bị gãy (No Breaking Changes).
5. **Kiểm Tra & Xác Minh (Verify)**:
   - Chạy lệnh compile / linter / test để xác nhận không có lỗi cú pháp hoặc thiếu import.
   - Xác nhận số dòng của tất cả các file sau khi tách đều nằm trong giới hạn $\le 1000$ dòng.

---

## 4. Tóm Tắt Nguyên Tắc Bất Di Bất Dịch (Golden Rules)
1. 🛑 **Tuyệt đối không tạo file vượt quá 1000 dòng.**
2. ⚡ **Chủ động tách logic ngay từ đầu** (Proactive Splitting) thay vì dồn hết vào 1 file rồi mới sửa sau.
3. 📦 **1 File = 1 Trách Nhiệm Rõ Ràng** (ASR riêng, Diarization riêng, Translation riêng, UI riêng, API riêng).
