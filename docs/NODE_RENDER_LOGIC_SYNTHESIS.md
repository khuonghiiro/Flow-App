# TÀI LIỆU TỔNG HỢP LOGIC XỬ LÝ VÀ RENDER NODE
> **Dành cho AI & Developer**: Tài liệu này phân tích cấu trúc object (JSON Schema) thực tế sinh ra bởi FlowMy, cơ chế tiêm dữ liệu động (Dynamic Inputs). 
> **QUAN TRỌNG:** Để sinh ra đúng file JSON, AI cần nắm vững cấu trúc Object cốt lõi của Workflow và Node được trình bày ở đây. Các thuộc tính chi tiết của từng Node cụ thể nằm trong `docs/NodeReferences/`.

---

## 1. Cấu Trúc Object JSON Của Workflow
Một Workflow hoàn chỉnh xuất ra file JSON sẽ có cấu trúc Object như sau:

```json
{
  "Name": "Tên Workflow",
  "ZoomLevel": 1.0,
  "PanX": 0.0,
  "PanY": 0.0,
  "ConnectionLineStyle": "Orthogonal",
  "Nodes": [
    // ... chứa các Object Node
  ],
  "Connections": [
    {
      "FromNodeId": "Node_1",
      "FromPortId": "Port_Out_1",
      "ToNodeId": "Node_2",
      "ToPortId": "Port_In_1"
    }
  ]
}
```

---

## 2. Cấu Trúc Object Cơ Bản Của 1 Node
Bất kỳ Node nào (dù là Start, Web, hay ImageProcessing) đều phải tuân thủ chuẩn Object sau đây:

```json
{
  "Id": "Unique_Node_Id",
  "Title": "Tên hiển thị trên UI",
  "X": 9050.0,
  "Y": 9456.0,
  "Type": "Start", 
  "ColorKey": "SkyAzure",
  "Properties": {
    "RunMode": "MainFlow",
    "EndBehavior": "StopCurrentFlow",
    "TitleDisplayMode": "Always",
    "TitleColorMode": "NodeColor"
    // ---> CÁC THUỘC TÍNH ĐẶC THÙ (SPECIFIC PROPERTIES) SẼ NẰM Ở ĐÂY
  },
  "Ports": [
    {
      "Id": "Unique_Port_Id",
      "IsInput": true,
      "Position": "Left",
      "IsVisible": true
    },
    {
      "Id": "Unique_Port_Id_2",
      "IsInput": false,
      "Position": "Right",
      "IsVisible": true
    }
  ]
}
```

### Giải thích các trường trong Object Node:
- **`Type`**: String xác định loại Node (VD: `"Delay"`, `"WebNode"`). Đây là trường then chốt để Executor biết cách chạy.
- **`ColorKey`**: Màu hiển thị (VD: `"SkyAzure"`, `"EmeraldGreen"`, `"SunsetOrange"`).
- **`Ports`**: Mảng định nghĩa các điểm nối dây. `IsInput=true` (Cổng nhận), `false` (Cổng xuất). `Position` có thể là `"Left"`, `"Right"`, `"Top"`, `"Bottom"`.
- **`Properties`**: Object dạng `Dictionary<string, object>`. Đây là nơi chứa toàn bộ Data thực thi (như DelayValue, ExtractUrl, ...).

---

## 3. Cơ Chế Tiêm Dữ Liệu Động (Dynamic Input Overrides)
Bên trong Object `Properties`, ngoài các key tĩnh, người dùng có thể mapping dữ liệu từ Node khác. Hệ thống dùng tiền tố `DynIn_`:
```json
"Properties": {
  "DelayValue": 5,
  "DynIn_DelayValue_SrcNode": "Node_Id_Cua_Storage",
  "DynIn_DelayValue_SrcKey": "Output_Key_Cua_Storage",
  "DynIn_DelayValue_ConvType": "Integer"
}
```

---

## 4. BẢNG CHỈ MỤC CHI TIẾT TỪNG NODE (NODE REFERENCES)
Để biết chính xác cấu trúc của Object `Properties` cho từng Node `Type`, hãy tham chiếu các file sau (mỗi file đều có mẫu JSON Properties cụ thể):

### 4.1 Nhóm Điều khiển Luồng (Core Flow)
Tham chiếu file: `docs/NodeReferences/01_CORE_FLOW_NODES.md`
- Chứa các node: **Start, End, Delay, Loop, Conditional, AsyncTask, Generic/Body**.

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
