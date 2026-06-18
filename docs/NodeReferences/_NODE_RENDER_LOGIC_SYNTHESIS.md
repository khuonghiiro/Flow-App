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
  "ZoomLevel": 0.65,
  "PanX": -5700.0,
  "PanY": -6100.0,
  "SavedScreenWidth": 1920.0,
  "SavedScreenHeight": 1080.0,
  "SavedViewportCenterX": 10200.0,
  "SavedViewportCenterY": 9950.0,
  "ConnectionLineStyle": "Orthogonal"
}
```

### ⚠️ QUAN TRỌNG VỀ VIEWPORT CAMERA
- `SavedViewportCenterX` và `SavedViewportCenterY` **BẮT BUỘC** phải trỏ đúng vào trọng tâm của cụm Node.
  - Ví dụ: Nếu các node nằm quanh `X: 9850-11450`, `Y: 9950`, thì `SavedViewportCenterX` ~ `10600`, `SavedViewportCenterY` ~ `9950`.
  - Nếu Viewport ở `960`/`540` mà node ở `10000`, người dùng mở lên sẽ thấy **màn hình trống trơn**.
- `PanX` và `PanY` thường là **số âm lớn**, tương ứng với offset camera đến khu vực node. Cách tính gần đúng: `PanX ≈ -(SavedViewportCenterX * ZoomLevel)`, `PanY ≈ -(SavedViewportCenterY * ZoomLevel)`.
- `ZoomLevel` nên dùng giá trị `0.5 - 0.9` cho workflow có nhiều node, `1.0` nếu ít node.

---

## 2. Cấu Trúc Object Cơ Bản Của 1 Node
Bên trong mảng `Nodes`, mỗi node đều phải có bộ khung cố định. Lưu ý phần `Ports` phải chứa `Index` và `BranchIndex`.

### Quy Tắc BẮT BUỘC:
- **Id format**: Phải theo dạng `"Node_{Type}_{GUID}"`. Ví dụ: `"Node_Start_a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"`. GUID phải là chuỗi hex hợp lệ format `xxxxxxxx-xxxx-4xxx-8xxx-xxxxxxxxxxxx`.
- **Port IDs**: Phải là **GUID hợp lệ** (ví dụ: `"aa000001-0001-4001-8001-000000000001"`). **KHÔNG ĐƯỢC** dùng tên tự đặt như `"Start_Out"`, `"Folder_In"`.
- **Vị trí (X, Y)**: Giá trị tọa độ thường nằm trong khoảng `9000-11000` để nằm giữa canvas vô tận.
- **Ports**: Hầu hết node đều có **2 ports** (1 Input Left + 1 Output Right), kể cả Start và End. Trường hợp ngoại lệ: **InputNode chỉ có 1 port Output Right** (không có port Input).
- **Chuỗi Flow hợp lệ**: `Start → [Node có cả 2 ports] → ... → End`. InputNode **có thể nối RA** (từ output port của nó → input port của node khác), nhưng **KHÔNG THỂ nhận nối VÀO** (vì không có port Input). Vì vậy InputNode luôn là điểm nguồn dữ liệu.
- **BranchIndex**: Luôn là `null` cho node thường. Chỉ có giá trị số khi là node ConditionalBranch hoặc AsyncTaskBranch.
- **OutputValues**: Luôn là `null` khi tạo workflow mới.

### Khoảng cách giữa các Node:
- Node thường: `Width` ~ 200px, `Height` ~ 150px (Start/End là 60x60). Khoảng cách X giữa 2 node liên tiếp ≥ **400px**.
- Node lớn: `VideoProcessing` (1360x768), `BodyContainer` (800x600), `HtmlUi` (420x320), `ImageProcessing` (360x280) → cần `X_Spacing` > 1500px.

### Ví dụ Node Cơ Bản Hoàn Chỉnh:
```json
{
  "Id": "Node_Start_a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
  "Title": "Start",
  "X": 9850,
  "Y": 9950,
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
    "TitleColorMode": "NodeColor"
  },
  "Ports": [
    {
      "Id": "aa000001-0001-4001-8001-000000000001",
      "IsInput": true,
      "Position": "Left",
      "Index": 0,
      "BranchIndex": null
    },
    {
      "Id": "aa000001-0001-4001-8001-000000000002",
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

#### 1. Nhóm Shared Properties (BẮT BUỘC cho MỌI node)
Các trường sau **phải có** trong Properties của mọi node, nếu thiếu sẽ gây lỗi UX khi load:
```json
{
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
  "TitleColorMode": "NodeColor"
}
```

#### 2. Nhóm Specific Properties (Tĩnh)
Các giá trị cố định đặc thù của từng loại Node. (Xem bảng chỉ mục mục 4).

#### 3. Nhóm Dynamic Input Overrides (Mapping Động)
Lấy giá trị đè từ node khác:
- `"DynIn_[TargetProperty]_SrcNode": "id_node_nguồn"`
- `"DynIn_[TargetProperty]_SrcKey": "key_output_nguồn"`
- `"DynIn_[TargetProperty]_ConvType": "String"`

---

## 3. Cấu Trúc Object Của Connections
Nối dây dữ liệu giữa các Node. **Port IDs phải khớp chính xác** với các GUID đã khai báo trong Ports của node:

```json
{
  "FromNodeId": "Node_Start_a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
  "FromPortId": "aa000001-0001-4001-8001-000000000002",
  "ToNodeId": "Node_Delay_c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f",
  "ToPortId": "cc000003-0003-4003-8003-000000000001"
}
```

### Lưu ý:
- `FromPortId` luôn là port có `"IsInput": false` (Output port).
- `ToPortId` luôn là port có `"IsInput": true` (Input port).

### ⚠️ QUY TẮC KẾT NỐI BẮT BUỘC (QUAN TRỌNG NHẤT)

**Quy tắc 1: MỌI node phải có ít nhất 1 connection visual trong mảng `Connections`.**
- Nếu 1 node không có connection nào → nó sẽ hiển thị cô lập, người dùng sẽ nghĩ workflow bị lỗi.
- Kể cả InputNode (chỉ có port Output) cũng **PHẢI** có connection nối RA đến 1 node khác.

**Quy tắc 2: Hệ thống kết nối có 2 TẦNG — cả 2 đều BẮT BUỘC:**

| Tầng | Mục đích | Cách khai báo | Ví dụ |
|------|----------|--------------|-------|
| **Tầng 1: Connection Visual** (dây nối trên canvas) | Hiển thị đường nối giữa 2 node cho người dùng thấy mối quan hệ | Khai báo trong mảng `"Connections"` | `{"FromNodeId":"Input","ToNodeId":"HttpRequest","FromPortId":"...","ToPortId":"..."}` |
| **Tầng 2: Property Binding** (data binding trong Properties) | Chỉ định NODE NÀO cung cấp dữ liệu cho PROPERTY NÀO | Khai báo trong `"Properties"` của node đích | `"UrlSourceNodeId":"Node_Input_..."`, `"UrlSourceOutputKey":"apiUrl"` |

**SAI** (chỉ có Tầng 2, thiếu Tầng 1):
```json
// HttpRequest chỉ có UrlSourceNodeId nhưng KHÔNG có Connection từ Input → HttpRequest
// → Input node hiển thị cô lập, không có dây nối
"Properties": { "UrlSourceNodeId": "Node_Input_abc..." }
"Connections": []  // ← THIẾU!
```

**ĐÚNG** (có CẢ HAI tầng):
```json
// Tầng 1: Dây nối visual
"Connections": [
  { "FromNodeId": "Node_Input_abc...", "ToNodeId": "Node_HttpRequest_def...", "FromPortId": "...", "ToPortId": "..." }
]
// Tầng 2: Property binding
"Properties": { "UrlSourceNodeId": "Node_Input_abc...", "UrlSourceOutputKey": "apiUrl" }
```

**Quy tắc 3: Chuỗi Flow chính — Start phải nối liên tục đến End.**
- `Start → [Node A] → [Node B] → ... → End` — mỗi cặp node liên tiếp phải có 1 Connection.
- InputNode không nằm trong chuỗi flow chính, nhưng phải nối RA đến 1 node nó cung cấp dữ liệu.

---

## 4. VÍ DỤ WORKFLOW HOÀN CHỈNH (ĐÃ TEST THÀNH CÔNG)

Workflow "Gọi API và hiển thị kết quả": `Input (URL) + Start → Delay → HttpRequest → Notification → Output → End`

```
  ┌──────────────────────────────────────────────┐
  │  Input - API URL (chỉ có 1 port Output)       │
  │  Key: "apiUrl", Value: "https://..."          │
  └──────────────────┬───────────────────────────┘
                     │ Connection visual (Tầng 1)
                     │ + UrlSourceNodeId (Tầng 2)
                     ▼
┌───────┐  ┌──────────┐  ┌───────────┐  ┌───────────┐  ┌──────────┐  ┌─────┐
│ Start │─▶│ Delay 1s │─▶│ HTTP GET  │─▶│Notification│─▶│  Output  │─▶│ End │
└───────┘  └──────────┘  └───────────┘  └───────────┘  └──────────┘  └─────┘
```

**Connections cần tạo (6 dây nối):**
1. `Start` → `Delay` (flow)
2. `Input` → `HttpRequest` (data — InputNode nối RA)
3. `Delay` → `HttpRequest` (flow)
4. `HttpRequest` → `Notification` (flow)
5. `Notification` → `Output` (flow)
6. `Output` → `End` (flow)

---

## 5. BẢNG CHỈ MỤC TỪ ĐIỂN NODE (NODE REFERENCES)
Để biết phần "Specific Properties" của từng loại Node phải gõ như thế nào, hãy tham chiếu các file sau:

### 5.1 Nhóm Điều khiển Luồng (Core Flow)
Tham chiếu file: `docs/NodeReferences/01_CORE_FLOW_NODES.md`
- Chứa các node: **Start, End, Delay, Loop, IfElse, AsyncTask, BodyContainer, Break, Continue, Callback, AsyncTaskDispatchCollect**.

### 5.2 Nhóm Web & Mạng (Browser & Web)
Tham chiếu file: `docs/NodeReferences/02_BROWSER_WEB_NODES.md`
- Chứa các node: **Web, HtmlUi, EmbedApplicationNode, HttpRequest, FileDownload**.

### 5.3 Nhóm Tương tác Chuột/Phím (User Interaction)
Tham chiếu file: `docs/NodeReferences/03_USER_INTERACTION_NODES.md`
- Chứa các node: **ScreenPosition, KeyPressEvent, HotkeyPressEvent, MouseEvent, ScreenCapture, MacroRecorder**.

### 5.4 Nhóm Dữ liệu & Biến (Data & Variables)
Tham chiếu file: `docs/NodeReferences/04_DATA_VARIABLES_NODES.md`
- Chứa các node: **Input, Output, Storage, ListOut, AssignData, KeyValueBridge, StringSplit, TextScan, KeyScopedStore**.

### 5.5 Nhóm Xử lý Đa phương tiện (Media)
Tham chiếu file: `docs/NodeReferences/05_MEDIA_PROCESSING_NODES.md`
- Chứa các node: **ImageProcessing, VideoProcessing, MediaGallery**.

### 5.6 Nhóm Tiện ích Mở rộng (Utilities)
Tham chiếu file: `docs/NodeReferences/06_UTILITIES_NODES.md`
- Chứa các node: **Code (C#), FolderFilePaths, DataFetcher, GitSource, FlowOverwrite, Notification, BorderHighlight, Folder**.

