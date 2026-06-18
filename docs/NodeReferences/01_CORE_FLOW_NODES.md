# Core Flow Nodes (Nhóm Điều khiển Luồng Cơ bản)

Tài liệu này chứa cấu trúc JSON `Properties` và Logic thực thi của các Node liên quan đến rẽ nhánh, lặp, khởi tạo và kết thúc.

> **LƯU Ý**: Ngoài các Properties đặc thù dưới đây, MỌI node đều phải có thêm bộ **Shared Properties** (`RunMode`, `AutoRunIntervalValue`, `EndBehavior`, `TitleDisplayMode`...). Xem `_NODE_RENDER_LOGIC_SYNTHESIS.md` mục 2.

---

## 1. Start Node
- **Type**: `"Start"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Điểm mốc bắt đầu của Workflow. Khi người dùng nhấn "Chạy", hệ thống bắt đầu từ node này.
- **Specific Properties**: Không có property riêng. Chỉ cần Shared Properties.
- **Ví dụ JSON hoàn chỉnh**:
```json
{
  "Id": "Node_Start_a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
  "Title": "Start",
  "X": 9850, "Y": 9950,
  "Type": "Start",
  "ColorKey": "SkyAzure",
  "Properties": {
    "RunMode": "MainFlow",
    "AutoRunIntervalValue": 5, "AutoRunIntervalUnit": "Seconds",
    "AutoScopeVisualPadding": "40",
    "AutoScopeFrameX": "0", "AutoScopeFrameY": "0",
    "AutoScopeFrameWidth": "0", "AutoScopeFrameHeight": "0",
    "EndBehavior": "StopCurrentFlow",
    "DiamondSharpness": "Medium",
    "TitleDisplayMode": "Always", "TitleColorMode": "NodeColor"
  },
  "Ports": [
    { "Id": "guid-1", "IsInput": true, "Position": "Left", "Index": 0, "BranchIndex": null },
    { "Id": "guid-2", "IsInput": false, "Position": "Right", "Index": 0, "BranchIndex": null }
  ],
  "OutputValues": null
}
```

---

## 2. End Node
- **Type**: `"End"`
- **ColorKey**: `"Danger"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Kết thúc nhánh hoặc toàn bộ luồng. Khi luồng chạy đến node này, workflow dừng lại.
- **Specific Properties**: Không có property riêng. Chỉ cần Shared Properties.

---

## 3. Delay Node
- **Type**: `"Delay"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Tạm dừng luồng thực thi trong một khoảng thời gian trước khi chạy node tiếp theo.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `DelayMilliseconds` | int | Giá trị delay đã quy đổi ra milliseconds (hệ thống tự tính). |
| `DelayValue` | int | Giá trị thời gian chờ do người dùng đặt (ví dụ: `2`). |
| `DelayUnit` | string | Đơn vị thời gian: `"Milliseconds"` / `"Seconds"` / `"Minutes"`. |
| `TimingMode` | string | `"Fixed"` = chờ cố định. `"Random"` = chờ ngẫu nhiên trong khoảng Min-Max. |
| `RandomMinValue` | int | Giá trị tối thiểu khi `TimingMode` là `"Random"`. |
| `RandomMaxValue` | int | Giá trị tối đa khi `TimingMode` là `"Random"`. |
| `DelaySourceNodeId` | string | *(Tùy chọn)* ID của node cung cấp giá trị delay động thay vì dùng `DelayValue` cố định. |
| `DelaySourceOutputKey` | string | *(Tùy chọn)* Output key của node nguồn chứa giá trị delay. |

- **Ví dụ**: Chờ cố định 2 giây:
```json
"Properties": {
  "DelayMilliseconds": 2000,
  "DelayValue": 2,
  "DelayUnit": "Seconds",
  "TimingMode": "Fixed",
  "RandomMinValue": 0,
  "RandomMaxValue": 0
}
```

---

## 4. Loop Node
- **Type**: `"Loop"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right). Tự tạo `LoopBodyNode` bên trong.
- **Chức năng**: Vòng lặp — lặp N lần hoặc duyệt từng phần tử trong mảng. Bên trong Loop tự sinh 1 LoopBodyNode chứa các node con.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `LoopType` | string | Loại vòng lặp: `"RepeatN"` (lặp N lần), `"ForEach"` (duyệt mảng), `"WhileCondition"` (lặp có điều kiện). |
| `RepeatCount` | int | Số lần lặp khi dùng `"RepeatN"`. |
| `StartIndex` | int | Index bắt đầu (thường `0`). |
| `EndIndex` | int | Index kết thúc (thường `RepeatCount - 1`). |
| `ArrayInputKey` | string | Key output từ node khác chứa mảng cần duyệt (dùng với `"ForEach"`). |
| `InputType` | string | Kiểu dữ liệu: `"Integer"` / `"String"`. |
| `CustomOutputMappings` | string | *(Tùy chọn)* JSON array mapping output từ loop body ra ngoài. |
| `DataAssignments` | string | *(Tùy chọn)* JSON array gán dữ liệu từ bên ngoài vào loop body. |

- **Ví dụ**: Lặp 10 lần:
```json
"Properties": {
  "LoopType": "RepeatN",
  "RepeatCount": 10,
  "StartIndex": 0,
  "EndIndex": 9,
  "ArrayInputKey": "",
  "InputType": "Integer"
}
```

---

## 5. BodyContainer Node
- **Type**: `"BodyContainer"`
- **ColorKey**: `"Info"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Vùng chứa hình chữ nhật lớn trên canvas, bao bọc các node con bên trong. Dùng để nhóm và tổ chức layout.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `BodyWidth` | double | Chiều rộng vùng chứa (pixel). Mặc định `800`. |
| `BodyHeight` | double | Chiều cao vùng chứa (pixel). Mặc định `600`. |
| `BodyBackgroundColorHex` | string | Màu nền dạng hex, ví dụ `"#1E1E2E"`. |
| `BodyBorderColorHex` | string | Màu viền dạng hex, ví dụ `"#3B3B5C"`. |
| `UseUnifiedColors` | bool | `true` = viền và nền cùng màu với ColorKey của node. |
| `BackgroundOpacityPercent` | double | Độ trong suốt nền (0-100). |
| `LockInnerNodes` | bool | `true` = khóa các node bên trong không cho di chuyển. |
| `BorderThickness` | double | Độ dày viền (pixel). |
| `BorderDashStyle` | string | Kiểu viền: `"Solid"` / `"Dashed"` / `"Dotted"`. |

- **Ví dụ**:
```json
"Properties": {
  "BodyWidth": 800.0,
  "BodyHeight": 600.0,
  "BodyBackgroundColorHex": "#1E1E2E",
  "BodyBorderColorHex": "#3B3B5C",
  "UseUnifiedColors": false,
  "BackgroundOpacityPercent": 80,
  "LockInnerNodes": false,
  "BorderThickness": 1.0,
  "BorderDashStyle": "Solid"
}
```

---

## 6. Conditional Node (IfElse)
- **Type**: `"IfElse"`
- **ColorKey**: `"Lavender"`
- **Ports**: 1 Input Left + N Output Right (mỗi branch 1 port, dùng `BranchIndex`: 0 = if, 1 = else, 2+ = else if)
- **Chức năng**: Rẽ nhánh theo điều kiện. Kiểm tra biểu thức logic → nếu đúng đi nhánh "if", sai đi nhánh "else".
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `ConditionalVisualMode` | string | Hình dáng node: `"Diamond"` (hình thoi) / `"Rectangle"` (hình chữ nhật). |
| `ConditionGroups` | string | JSON array chứa nhóm điều kiện. Mỗi nhóm có array `Rules`. |

- **Cấu trúc ConditionGroups**: Mỗi Rule gồm:
  - `LeftValue`: Giá trị bên trái (có thể dùng `DynIn` để lấy từ node khác)
  - `Operator`: `"Equals"` / `"NotEquals"` / `"Contains"` / `"StartsWith"` / `"EndsWith"` / `"GreaterThan"` / `"LessThan"` / `"IsEmpty"` / `"IsNotEmpty"` / `"MatchesRegex"`
  - `RightValue`: Giá trị bên phải để so sánh

- **Ví dụ**: Kiểm tra nếu A = B:
```json
"Properties": {
  "ConditionalVisualMode": "Diamond",
  "ConditionGroups": "[{\"Rules\":[{\"LeftValue\":\"A\",\"Operator\":\"Equals\",\"RightValue\":\"B\"}]}]"
}
```

---

## 7. AsyncTask Node
- **Type**: `"AsyncTask"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 1 Input Left + N Output Right (mỗi branch 1 port)
- **Chức năng**: Khởi chạy đa luồng (Parallel). Mỗi branch là 1 luồng chạy song song. Hữu ích khi cần gọi nhiều API cùng lúc.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `RunInParallel` | bool | `true` = chạy tất cả branch song song. `false` = chạy tuần tự. |
| `UiPresentationMode` | string | `"ManualBranches"` = người dùng tự tạo branch. `"DispatchLoop"` = tự chia lặp. |
| `DispatchLoopType` | string | Loại lặp khi mode là DispatchLoop: `"None"` / `"RepeatN"` / `"ForEach"`. |
| `RepeatCount` | int | Số lần lặp (cho DispatchLoop). |
| `StartIndex` | int | Index bắt đầu. |
| `EndIndex` | int | Index kết thúc. |
| `ReadResultsInBody` | bool | `true` = cho phép đọc kết quả của branch khác từ bên trong body. |
| `AsyncTaskBranches` | string | JSON array khai báo các branch: `[{"Id":"guid","Label":"Task 1","CanRemove":true}]`. |

- **Ví dụ**: 2 branch chạy song song:
```json
"Properties": {
  "RunInParallel": true,
  "UiPresentationMode": "ManualBranches",
  "DispatchLoopType": "None",
  "RepeatCount": 0,
  "ReadResultsInBody": false,
  "AsyncTaskBranches": "[{\"Id\":\"branch_1\",\"Label\":\"API Call 1\",\"CanRemove\":true},{\"Id\":\"branch_2\",\"Label\":\"API Call 2\",\"CanRemove\":true}]"
}
```

---

## 8. Break & Continue Node
- **Type**: `"Break"` / `"Continue"`
- **ColorKey**: Break=`"Danger"`, Continue=`"Info"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Đặt bên trong Loop/Async body. `Break` = dừng vòng lặp ngay lập tức. `Continue` = bỏ qua phần còn lại, nhảy sang lần lặp tiếp.
- **Specific Properties**: Không có property riêng.

---

## 9. Callback Node
- **Type**: `"Callback"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Nhảy ngược (jump back) về một node đã chạy trước đó, tạo vòng lặp retry. Có bộ đếm giới hạn để tránh vòng lặp vô hạn.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `TargetNodeId` | string | ID của node muốn nhảy về (ví dụ: `"Node_Delay_abc123..."`). |
| `MaxCallbackCount` | int | Số lần nhảy tối đa. Vượt quá sẽ đi tiếp thay vì nhảy lại. |
| `FlowBehavior` | string | `"JumpThenContinue"` = nhảy về rồi tiếp tục flow. `"StopCurrentFlow"` = nhảy về rồi dừng. |

- **Ví dụ**: Retry tối đa 3 lần:
```json
"Properties": {
  "TargetNodeId": "Node_HttpRequest_abc123...",
  "MaxCallbackCount": 3,
  "FlowBehavior": "JumpThenContinue"
}
```

---

## 10. AsyncTaskDispatchCollect Node
- **Type**: `"AsyncTaskDispatchCollect"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Gom kết quả từ tất cả các luồng AsyncTask thành 1 JSON duy nhất `{ "0": "val1", "1": "val2" }`. Đặt SAU AsyncTask node để thu thập output.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `SourceBodyNodeId` | string | ID của node bên trong AsyncTask body mà bạn muốn lấy kết quả. |
| `SourceOutputKey` | string | Output key của node nguồn chứa kết quả cần gom. |

- **Ví dụ**: Gom kết quả từ node API bên trong AsyncTask:
```json
"Properties": {
  "SourceBodyNodeId": "Node_HttpRequest_trong_asynctask_body",
  "SourceOutputKey": "responseBody"
}
```
