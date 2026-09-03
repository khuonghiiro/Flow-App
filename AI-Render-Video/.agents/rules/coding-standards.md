# Coding & Modularity Standards for AI Studio & Flow-App

> Quy tắc này được tự động áp dụng trong mọi phiên làm việc tại thư mục này.

## 1. Nguyên Tắc Giới Hạn Kích Thước File (Bắt Buộc)
- **Mục tiêu**: 150 – 500 dòng/file.
- **Giới hạn cứng**: **TUYỆT ĐỐI KHÔNG vượt quá 800 dòng/file**.
- **Quy trình khi file vượt giới hạn**:
  - Không nhồi nhét code vào file đã lớn.
  - Tách sub-components vào thư mục con (ví dụ: `catalog/`, `panels/`, `tree/nodes/`, `hooks/`).
  - Tách các modal hoặc tab giao diện phức tạp thành các component con riêng biệt.

## 2. Giới Hạn Hàm & Phương Thức
- Giữ hàm/method dưới **50 – 80 dòng**.
- Tách các thuật toán tính toán tọa độ, SVG path generation, transform matrix thành các helper riêng biệt.

## 3. Tiêu Chuẩn Giao Diện (UI Layout & Theme)
- **Dark Mode & Glassmorphism**: Giữ phong cách hiện đại, tương phản sắc nét, viền mờ cao cấp.
- **Canvas & Tree Viewport**: 
  - Tách biệt rõ giữa Link layer (SVG), Node layer (HTML/Canvas), và Interaction controller (Zoom/Pan/Drag).
- **Layout RPG / Customizer**:
  - Cột 1: Viewport chính (3D/2D Viewport với overlay HUD, slot trang bị).
  - Cột 2: Catalog tài nguyên & Sliders tinh chỉnh thuộc tính.

## 4. Kiểm Soát Trạng Thái & Render
- Dùng `useMemo` và `useCallback` cho các hàm tính toán vị trí node, path SVG, link curves để tránh lag giật khi kéo thả canvas.
- Không mutate state trực tiếp.
- Giữ type safety: Đầy đủ interface/type cho Node, Link, TreeState.
