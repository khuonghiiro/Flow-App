# TÀI LIỆU TỔNG HỢP LOGIC XỬ LÝ VÀ RENDER NODE
> **Dành cho AI & Developer**: Tài liệu này phân tích cấu trúc object (JSON Schema) thực tế sinh ra bởi FlowMy, cơ chế tiêm dữ liệu động (Dynamic Inputs). 
> **QUAN TRỌNG:** Bản thân hệ thống có gần 40 Node. Tài liệu này đóng vai trò là "Bản lề" kiến trúc cốt lõi. Để biết chính xác cấu trúc JSON và Logic của từng loại Node riêng biệt, hãy tham chiếu các file trong thư mục `docs/NodeReferences/`.

---

## 1. Phân Tích Cấu Trúc JSON Workflow
Một Workflow hoàn chỉnh xuất ra file JSON sẽ có 3 thành phần chính:

### 1.1 Global Scope (Môi trường Canvas)
- **`ZoomLevel`**: Mức độ thu phóng của canvas.
- **`PanX`, `PanY`**: Tọa độ dịch chuyển khung nhìn (Viewport).
- **`ConnectionLineStyle`**: Kiểu dáng dây kết nối (VD: `"Orthogonal"` cho dây vuông góc, `"Bezier"` cho dây cong).

### 1.2 Mảng `Connections` (Liên kết các Node)
Định nghĩa dòng chảy dữ liệu và trình tự thực thi: `FromNodeId` -> `FromPortId` -> `ToNodeId` -> `ToPortId`.

### 1.3 Mảng `Nodes` (Các khối xử lý)
Mỗi node chứa: `Id`, `Title`, `X`, `Y`, `Type`, `ColorKey`, mảng `Ports`, và quan trọng nhất là object `Properties`.

---

## 2. Bóc tách Object `Properties` của Node
Object `Properties` lưu trữ dưới dạng key-value, phân thành 3 loại biến số:

### 2.1 Nhóm Shared Properties
Dùng chung mọi node: `RunMode`, `EndBehavior`, `TitleDisplayMode`, `TitleColorMode`.

### 2.2 Nhóm Specific Properties (Tĩnh)
Các giá trị cố định nhập tay. Ví dụ: `DelayValue: 3`, `MouseAction: "LeftClick"`.

### 2.3 Nhóm Dynamic Input Overrides (Mapping Động)
Hệ thống hỗ trợ lấy giá trị đầu vào từ output của node trước đó, đè lên giá trị tĩnh. Biểu diễn qua tiền tố `DynIn_`:
- **`DynIn_[TargetProperty]_SrcNode`**: ID của node nguồn (Source Node) cấp dữ liệu.
- **`DynIn_[TargetProperty]_SrcKey`**: Key của biến Output bên trong node nguồn.
- **`DynIn_[TargetProperty]_ConvType`**: Kiểu ép dữ liệu (VD: `"Integer"`).

---

## 3. BẢNG CHỈ MỤC TỪ ĐIỂN NODE (NODE REFERENCES)
Hệ thống có rất nhiều Node với UI và JSON phức tạp. Tuỳ thuộc vào Node `Type` mà bạn đọc được trong file JSON, hãy tham chiếu file logic tương ứng dưới đây để hiểu cách parse data và mô phỏng logic.

### 3.1 Nhóm Điều khiển Luồng (Core Flow)
Tham chiếu file: `docs/NodeReferences/01_CORE_FLOW_NODES.md`
- Chứa các node: **Start, End, Delay, Loop, Conditional, AsyncTask, Generic/Body**.

### 3.2 Nhóm Web & Mạng (Browser & Web)
Tham chiếu file: `docs/NodeReferences/02_BROWSER_WEB_NODES.md`
- Chứa các node: **WebNode, HtmlUi, EmbedApplication, HttpRequest, FileDownload**.

### 3.3 Nhóm Tương tác Chuột/Phím (User Interaction)
Tham chiếu file: `docs/NodeReferences/03_USER_INTERACTION_NODES.md`
- Chứa các node: **ScreenPosition, KeyPressEvent, HotkeyPressEvent, MouseEvent, ScreenCapture, MacroRecorder**.

### 3.4 Nhóm Dữ liệu & Biến (Data & Variables)
Tham chiếu file: `docs/NodeReferences/04_DATA_VARIABLES_NODES.md`
- Chứa các node: **Input, Output, Storage, ListOut, AssignData, KeyValueBridge, StringSplit**.

### 3.5 Nhóm Xử lý Đa phương tiện (Media)
Tham chiếu file: `docs/NodeReferences/05_MEDIA_PROCESSING_NODES.md`
- Chứa các node: **ImageProcessing, VideoProcessing, MediaGallery**.

### 3.6 Nhóm Tiện ích Mở rộng (Utilities)
Tham chiếu file: `docs/NodeReferences/06_UTILITIES_NODES.md`
- Chứa các node: **Code (C#), FolderFilePaths, DataFetcher, GitSource, FlowOverwrite, Notification**.
