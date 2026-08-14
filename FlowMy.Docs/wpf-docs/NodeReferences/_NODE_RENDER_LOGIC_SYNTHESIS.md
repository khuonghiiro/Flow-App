# TÀI LIỆU TỔNG HỢP LOGIC XỬ LÝ VÀ RENDER NODE

> **Dành cho AI & Developer**: Tài liệu này phân tích cấu trúc object (JSON Schema) thực tế sinh ra bởi FlowMy, cơ chế tiêm dữ liệu động (Dynamic Inputs).
> **QUAN TRỌNG:** Bản thân hệ thống có gần 40 Node. Tài liệu này đóng vai trò là "Bản lề" kiến trúc cốt lõi. Để biết chính xác cấu trúc JSON và Logic của từng loại Node riêng biệt, hãy tham chiếu các file trong thư mục `docs/NodeReferences/`.

---

## ⚡ FORMAT v2 — AI SIMPLIFIED (ƯU TIÊN ĐỌC TRƯỚC)

> **Từ version 2**, hệ thống hỗ trợ format JSON đơn giản hóa dành cho AI tạo sinh workflow. AI chỉ cần viết các fields cốt lõi — hệ thống **tự bổ sung** mọi thứ còn thiếu khi load.

### Nguyên tắc v2
1. **Convention over Configuration**: Shared properties có default → **không cần ghi nếu = default**
2. **Auto-generate**: Port IDs, Node IDs tự sinh nếu thiếu
3. **Viewport auto-calculate**: Nếu thiếu PanX/PanY/Zoom → tự tính từ bounding box nodes
4. **Connection by Index**: `{"FromNodeId": "0", "ToNodeId": "1"}` thay vì GUID
5. **Omit standard ports**: Node thường (2 ports: Input Left + Output Right) → bỏ hẳn `Ports`

### Ví dụ v2: Start → Delay 2s → End (3 nodes, 25 dòng)
```json
{
  "Version": 2,
  "Name": "Simple Delay Workflow",
  "Nodes": [
    { "Type": "Start", "Title": "Start", "X": 9850, "Y": 9950 },
    { 
      "Type": "Delay", "Title": "Wait 2s", "X": 10250, "Y": 9950,
      "Properties": { "DelayValue": 2, "DelayUnit": "Seconds" }
    },
    { "Type": "End", "Title": "End", "X": 10650, "Y": 9950 }
  ],
  "Connections": [
    { "FromNodeId": "0", "ToNodeId": "1" },
    { "FromNodeId": "1", "ToNodeId": "2" }
  ]
}
```

### Các fields được auto-fill khi load v2

| Field | Auto-fill rule | Giá trị default |
|-------|---------------|-----------------|
| `Id` (node) | `"Node_{Type}_{GUID}"` | Tự sinh |
| `Ports` | 1 Input Left + 1 Output Right (trừ InputNode: chỉ Output Right) | Tự sinh |
| `Port.Id` | GUID mới | Tự sinh |
| `ColorKey` | Theo TemplateFactory | Null OK |
| `Properties` | Null = không có specific props | `{}` |
| `OutputValues` | Luôn null khi tạo mới | Bỏ qua |
| `PanX/PanY` | Tính từ bounding box: `Pan = -(Center * Zoom) + ScreenSize/2` | Auto |
| `SavedViewportCenterX/Y` | Trung tâm bounding box nodes | Auto |
| `ZoomLevel` | Auto-fit theo spread nodes | `0.3 - 1.5` |
| `ConnectionLineStyle` | Default | `"Bezier"` |

### Connection by Index (Option C)
- `"FromNodeId": "0"` → Node đầu tiên trong `Nodes[]`
- `"FromNodeId": "1"` → Node thứ 2
- `"FromNodeId": "Node_Start_abc..."` → Match theo GUID (user-created)
- System tự detect: nếu parse được int → dùng index, ngược lại → dùng string ID
- **Khi save**: luôn dùng GUID ID (user-created nodes giữ nguyên GUID)

### Shared Properties — chỉ ghi khi ≠ default

| Property | Default | Chỉ ghi khi |
|----------|---------|-------------|
| `RunMode` | `"MainFlow"` | ≠ MainFlow |
| `AutoRunIntervalValue` | `5` | ≠ 5 |
| `AutoRunIntervalUnit` | `"Seconds"` | ≠ Seconds |
| `EndBehavior` | `"StopCurrentFlow"` | ≠ StopCurrentFlow |
| `DiamondSharpness` | `"Medium"` | ≠ Medium |
| `TitleDisplayMode` | `"Always"` | ≠ Always |
| `TitleColorMode` | `"NodeColor"` | ≠ NodeColor |
| `IconSize` | `32` | ≠ 32 |
| `AutoScopeVisualPadding` | `40` | ≠ 40 |
| `AutoScopeFrame*` | `0` | ≠ 0 |

### ⚠️ Node thường vs Node đặc biệt (khi nào CẦN Ports)
- **KHÔNG cần Ports** (tự sinh): Start, End, Delay, Input, Output, Notification, Code, Folder, HttpRequest, Storage, AssignData, StringSplit, TextScan, ...
- **CẦN Ports** (phức tạp, nhiều output): IfElse (ConditionalNode), Loop, AsyncTask, BodyContainer, LoopBody, AsyncTaskBody

---

## 🗺️ AI LAYOUT GUIDE — Toạ Độ & Kết Nối

> **Canvas**: 20000 x 20000 pixels. **Tâm** ở `(10000, 10000)`. Mọi workflow nên bắt đầu gần tâm để luôn hiển thị đúng khi mở.

### 1. Điểm bắt đầu (Origin Point)
- **Start node** luôn đặt tại: `X = 9850, Y = 9950` (gần tâm, lệch trái một chút)
- Các node tiếp theo nối **ngang sang phải** (flow pattern chuẩn)

### 2. Kích thước Node (Visual Width × Height)

| Nhóm | Node Types | Kích thước Visual | X Spacing |
|------|-----------|------------------|-----------|
| **Nhỏ** (icon) | Start, End, Input, Output, Break, Continue, AssignData | 60 × 60 | **250px** |
| **Trung bình** | Delay, KeyPress, HotkeyPress, MouseEvent, Notification, Storage, Callback, StringSplit, TextScan, HttpRequest, FileDownload, FolderFilePaths, KeyValueBridge, FlowOverwrite, GitSource, MacroRecorder, BorderHighlight, ScreenPosition, ScreenCapture, ListOut, DataFetcher, ShowInputMsg | ~150 × 80 | **350px** |
| **Lớn** | Code, Folder, ConditionalNode (IfElse), AsyncTask, EmbedApplication | ~200 × 120 | **450px** |
| **Rất lớn** | Web (WebView2), HtmlUi, DynamicUi | 420 × 320 | **700px** |
| **Khổng lồ** | ImageProcessing | 360 × 280 | **800px** |
| **Cực lớn** | VideoProcessing | 1360 × 768 | **1600px** |
| **Container** | BodyContainer, ActionCanVas | 800 × 400 | **1200px** |
| **Loop** | Loop (+ LoopBody bên trong) | 200 × 80 + body 400 × 300 | **900px** |

### 3. Quy tắc toạ độ

#### Layout ngang (chuẩn — dùng cho hầu hết workflow):
```
Flow direction: → (trái sang phải)

Start(9850, 9950) → NodeA(10200, 9950) → NodeB(10550, 9950) → End(10900, 9950)
                     +350px                 +350px                +350px
```

**Công thức đơn giản:**
```
Node[0]: X = 9850,                  Y = 9950
Node[i]: X = Node[i-1].X + SPACING, Y = 9950  (cùng hàng Y)
```

Trong đó `SPACING` lấy theo bảng kích thước ở trên.

#### InputNode (data source — không nằm trong flow chính):
```
InputNode thường đặt PHÍA TRÊN flow chính, lệch trái so với node nó cung cấp data:

   Input(10100, 9650)    ← Y - 300
          │ (connection data)
          ▼
Start ──→ Delay ──→ HttpRequest ──→ End
(9850)    (10200)   (10550)          (10900)
                    ↑ nhận data từ Input
```

#### Layout rẽ nhánh (IfElse / ConditionalNode):
```
                    ┌→ NodeTrue  (X+450, Y-200)
Start → IfElse ────┤
                    └→ NodeFalse (X+450, Y+200)
                         └→ End
```
**Mỗi nhánh cách nhau ΔY = 300-400px.**

#### Layout song song (nhiều Input + 1 flow):
```
Input1(9850, 9500)  ──┐
Input2(9850, 9750)  ──┼→ Start(10200, 9950) → ... → End
Input3(9850, 10200) ──┘

InputNodes xếp dọc, cách nhau ΔY = 250px
```

### 4. Bảng toạ độ nhanh (Copy-paste cho AI)

| Vị trí | X | Y | Ghi chú |
|--------|---|---|---------|
| Start | 9850 | 9950 | Luôn bắt đầu tại đây |
| Node 1 | 10200 | 9950 | +350 từ Start |
| Node 2 | 10550 | 9950 | +350 từ Node 1 |
| Node 3 | 10900 | 9950 | +350 từ Node 2 |
| Node 4 | 11250 | 9950 | +350 từ Node 3 |
| Node 5 | 11600 | 9950 | +350 từ Node 4 |
| End | X cuối + 350 | 9950 | Luôn ở cuối flow |
| Input (data) | X target - 100 | 9650 | Phía trên node đích |
| Branch trên | X parent + 450 | Y - 250 | Nhánh true |
| Branch dưới | X parent + 450 | Y + 250 | Nhánh false |

### 5. Viewport — Không cần lo!
> **v2 auto-calculate**: Khi thiếu `PanX`, `PanY`, `SavedViewportCenterX/Y` → hệ thống tự tính từ bounding box các node. AI **KHÔNG CẦN** tính viewport nữa.

### 6. Bảng Tra Cứu Combobox Output Keys (Dùng cho Dynamic Binding)

Khi một Node cần lấy dữ liệu từ Output của Node khác (qua Property Binding như `UrlSourceNodeId` + `UrlSourceOutputKey`, hoặc `DynIn_*_SrcNode` + `DynIn_*_SrcKey`, hoặc `SourceNodeId` + `SourceOutputKey`), hãy sử dụng đúng Key theo bảng dưới đây:

| Node Nguồn (Type) | Output Key khả dụng (Combobox Key) | Mô tả dữ liệu |
|-------------------|-----------------------------------|---------------|
| **Input** | `"value"` (hoặc theo `InputKey`) | Giá trị nhập từ InputNode |
| **HttpRequest** | `"statusCode"`, `"responseBody"`, `"headers"`, `"isSuccess"`, `"error"` | Kết quả HTTP Request |
| **StringSplit** | `"items"`, `"count"` | Mảng chuỗi sau khi tách và số lượng |
| **FolderFilePaths** | `"filePaths"`, `"folderPath"`, `"fileCount"` | Danh sách đường dẫn file trong folder |
| **TextScan** | `"scannedText"`, `"confidence"` | Kết quả OCR quét văn bản |
| **Storage** | Các key người dùng đã lưu trong Storage | Dữ liệu lưu đệm |
| **Code** | Key trả về từ C# code (`dict["key"]`) | Dữ liệu tính toán từ Code |

### 7. Quy Tắc Nối Cổng (Port Connection Rules — Rất Quan Trọng)

> ⚠️ **Mỗi Cổng Input (Left Port) của một Node chỉ nhận TỐI ĐA 1 DÂY NỐI (1 Incoming Connection)**.

1. **Dây nối Flow (Main Flow)**:
   - Nối tuần tự: `Start` → `Delay` → `HttpRequest` → `IfElse` → `End`.
   - Cổng Input của `HttpRequest` **chỉ dành cho dây nối Flow từ Start/Delay**.

2. **Truyền Dữ Liệu từ InputNode (Data Binding)**:
   - Node `Input` cung cấp dữ liệu cho `HttpRequest` bằng cách set trong `Properties`:
     `"UrlSourceNodeId": "0"` (với 0 là index của InputNode trong `Nodes[]`)
     `"UrlSourceOutputKey": "value"`
   - **KHÔNG CẦN** cắm thêm 1 dây nối visual từ `InputNode` chụm vào cổng Input của `HttpRequest` (tránh bị 2 dây chụm vào 1 cổng).

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

