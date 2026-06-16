# TÀI LIỆU TỔNG HỢP LOGIC XỬ LÝ VÀ RENDER NODE

> **Dành cho AI & Developer**: Tài liệu này phân tích cấu trúc object (JSON Schema) thực tế sinh ra bởi FlowMy, cơ chế tiêm dữ liệu động (Dynamic Inputs).
> **QUAN TRỌNG:** Bản thân hệ thống có gần 40 Node. Tài liệu này đóng vai trò là "Bản lề" kiến trúc cốt lõi. Để biết chính xác cấu trúc JSON và Logic của từng loại Node riêng biệt, hãy tham chiếu các file trong thư mục `docs/NodeReferences/`.

---

## 1. Cấu Trúc Object JSON Của Workflow (Global Scope)
Một Workflow hoàn chỉnh xuất ra file JSON luôn có dạng gốc như sau. **Mọi AI render workflow bắt buộc phải tuân thủ form này**, không được thiếu các key về hiển thị (Zoom/Pan):

```json
{
  "Name": "Tên luồng của bạn",
  "Nodes": [
    // Danh sách các khối Node (Xem mục 2)
  ],
  "Connections": [
    // Danh sách các mảng liên kết (Xem mục 3)
  ],
  "ZoomLevel": 1.0,
  "PanX": 0.0,
  "PanY": 0.0,
  "SavedScreenWidth": 1920.0,
  "SavedScreenHeight": 1080.0,
  "SavedViewportCenterX": 960.0,
  "SavedViewportCenterY": 540.0,
  "ConnectionLineStyle": "Orthogonal"
}
```

---

## 2. Cấu Trúc Object Cơ Bản Của 1 Node
Bên trong mảng `Nodes`, mỗi node đều phải có bộ khung cố định. Lưu ý phần `Ports` phải chứa `Index` và `BranchIndex`. Giá trị tọa độ `X` và `Y` thường nằm trong khoảng lớn (vd: 9000-11000) để nằm giữa không gian vô tận của Canvas.

```json
{
  "Id": "Node_Start_12345",
  "Title": "Start",
  "X": 10000.0,
  "Y": 10000.0,
  "Type": "Start",
  "ColorKey": "SkyAzure",
  "Properties": {
    "RunMode": "MainFlow",
    "AutoRunIntervalValue": 5,
    "AutoRunIntervalUnit": "Seconds",
    "AutoScopeVisualPadding": "40",
    "AutoScopeFrameX": "0",
    "AutoScopeFrameY": "0",
    "AutoScopeFrameWidth": "0",
    "AutoScopeFrameHeight": "0",
    "EndBehavior": "StopCurrentFlow",
    "DiamondSharpness": "Medium",
    "TitleDisplayMode": "Always",
    "TitleColorMode": "NodeColor",
    "TitleColorKey": "NodeColor"
    // ---> CHÈN THÊM CÁC THUỘC TÍNH RIÊNG (SPECIFIC PROPERTIES) CỦA TỪNG NODE TẠI ĐÂY <---
  },
  "Ports": [
    {
      "Id": "port_in_123",
      "IsInput": true,
      "Position": "Left",
      "Index": 0,
      "BranchIndex": null
    },
    {
      "Id": "port_out_456",
      "IsInput": false,
      "Position": "Right",
      "Index": 0,
      "BranchIndex": null
    }
  ],
  "OutputValues": null
}
```

### Phân Tích Object `Properties`
Object `Properties` lưu trữ dưới dạng key-value, phân thành 3 loại biến số:
1. **Nhóm Shared Properties**: Các trường như `RunMode`, `EndBehavior`, `TitleDisplayMode`, `AutoRun...` (như ví dụ trên). Các trường này node nào cũng nên có để deserialize không bị lỗi UX.
2. **Nhóm Specific Properties (Tĩnh)**: Các giá trị cố định đặc thù của từng loại Node. (Xem bảng chỉ mục bên dưới).
3. **Nhóm Dynamic Input Overrides (Mapping Động)**: Lấy giá trị đè từ node khác.
   - `"DynIn_[TargetProperty]_SrcNode": "id_node_nguồn"`
   - `"DynIn_[TargetProperty]_SrcKey": "key_output_nguồn"`
   - `"DynIn_[TargetProperty]_ConvType": "String"`

---

## 3. Cấu Trúc Object Của Connections
Nối dây dữ liệu giữa các Node:

```json
{
  "FromNodeId": "Node_Start_12345",
  "FromPortId": "port_out_456",
  "ToNodeId": "Node_Delay_789",
  "ToPortId": "port_in_789"
}
```

---

## 4. BẢNG CHỈ MỤC TỪ ĐIỂN NODE (NODE REFERENCES)
Để biết phần "Specific Properties" của từng loại Node phải gõ như thế nào, hãy tham chiếu các file sau:

### 4.1 Nhóm Điều khiển Luồng (Core Flow)
Tham chiếu file: `docs/NodeReferences/01_CORE_FLOW_NODES.md`
- Chứa các node: **Start, End, Delay, Loop, IfElse, AsyncTask, BodyContainer**.

### 4.2 Nhóm Web & Mạng (Browser & Web)
Tham chiếu file: `docs/NodeReferences/02_BROWSER_WEB_NODES.md`
- Chứa các node: **WebNode, HtmlUi, EmbedApplication, HttpRequest, FileDownload**.

### 4.3 Nhóm Tương tác Chuột/Phím (User Interaction)
Tham chiếu file: `docs/NodeReferences/03_USER_INTERACTION_NODES.md`
- Chứa các node: **ScreenPosition, KeyPressEvent, HotkeyPressEvent, MouseEvent, ScreenCapture, MacroRecorder**.

### 4.4 Nhóm Dữ liệu & Biến (Data & Variables)
Tham chiếu file: `docs/NodeReferences/04_DATA_VARIABLES_NODES.md`
- Chứa các node: **Input, Output, Storage, ListOut, AssignData, KeyValueBridge, StringSplit**.

### 4.5 Nhóm Xử lý Đa phương tiện (Media)
Tham chiếu file: `docs/NodeReferences/05_MEDIA_PROCESSING_NODES.md`
- Chứa các node: **ImageProcessing, VideoProcessing, MediaGallery**.

### 4.6 Nhóm Tiện ích Mở rộng (Utilities)
Tham chiếu file: `docs/NodeReferences/06_UTILITIES_NODES.md`
- Chứa các node: **Code (C#), FolderFilePaths, DataFetcher, GitSource, FlowOverwrite, Notification**.
