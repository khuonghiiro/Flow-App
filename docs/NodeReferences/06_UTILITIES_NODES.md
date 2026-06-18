# Utilities Nodes (Nhóm Tiện ích Mở rộng)

Tài liệu này trình bày Logic thực thi và Cấu trúc JSON `Properties` cho các Node tiện ích.

> **LƯU Ý**: Ngoài các Properties đặc thù dưới đây, MỌI node đều phải có thêm bộ **Shared Properties** (`RunMode`, `AutoRunIntervalValue`, `EndBehavior`, `TitleDisplayMode`...). Xem `_NODE_RENDER_LOGIC_SYNTHESIS.md` mục 2.

---

## 1. Code Node (C# Script)
- **Type**: `"Code"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Chạy mã C# tùy chỉnh bên trong workflow. Nhận input từ node khác qua `InputMappings`, xử lý logic, rồi xuất kết quả qua `SetOutput("key", value)`.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `ScriptCode` | string | Mã C# thực thi. Có sẵn biến `inputs` (Dictionary) và hàm `SetOutput(key, value)` để trả kết quả. |
| `InputMappings` | string | JSON array mapping data từ node khác vào biến `inputs`. Mỗi item: `{"SourceNodeId":"id","SourceOutputKey":"key","InputKeyOverride":"tên_biến_trong_code"}`. |
| `OutputKeys` | string | JSON array khai báo key output: `["result","count"]`. Phải khớp với `SetOutput("key",...)` trong code. |

- **Pattern InputMappings** (quan trọng — dùng chung cho Code, HtmlUi):
```json
"InputMappings": "[{\"SourceNodeId\":\"Node_Input_abc...\",\"SourceOutputKey\":\"userName\",\"InputKeyOverride\":\"name\",\"ShouldReExecute\":false,\"AutoRefreshEnabled\":false,\"AutoRefreshInterval\":1000,\"AutoRefreshUnit\":\"ms\"}]"
```
  - `SourceNodeId`: ID node cung cấp dữ liệu
  - `SourceOutputKey`: Output key của node nguồn
  - `InputKeyOverride`: Tên biến trong code (truy cập qua `inputs["name"]`)
  - `ShouldReExecute`: `true` = chạy lại khi data thay đổi
  - `AutoRefreshEnabled`: `true` = tự động refresh data theo interval
  - `AutoRefreshInterval`: Interval (ms)

- **Ví dụ**: Nối chuỗi từ Input node:
```json
"Properties": {
  "ScriptCode": "var name = inputs[\"name\"];\nSetOutput(\"greeting\", \"Xin chào \" + name + \"!\");",
  "InputMappings": "[{\"SourceNodeId\":\"Node_Input_abc...\",\"SourceOutputKey\":\"userName\",\"InputKeyOverride\":\"name\",\"ShouldReExecute\":false,\"AutoRefreshEnabled\":false,\"AutoRefreshInterval\":1000,\"AutoRefreshUnit\":\"ms\"}]",
  "OutputKeys": "[\"greeting\"]"
}
```

---

## 2. Folder File Paths Node
- **Type**: `"FolderFilePaths"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Quét thư mục lấy danh sách đường dẫn file. Có thể lọc theo phần mở rộng, quét thư mục con, và đọc nội dung file.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `FolderPath` | string | Đường dẫn thư mục cần quét (ví dụ `"C:\\MyFolder"`). |
| `FolderSourceNodeId` | string | *(Tùy chọn)* ID node cung cấp đường dẫn thư mục động. |
| `FolderSourceOutputKey` | string | *(Tùy chọn)* Output key chứa đường dẫn. |
| `RefreshFolderSourceNodeBeforeUse` | bool | `true` = chạy lại node nguồn trước khi quét. |
| `IncludeSubfolders` | bool | `true` = quét cả thư mục con (recursive). |
| `ExtensionFilterText` | string | Lọc phần mở rộng file (ví dụ `".jpg,.png,.gif"`). |
| `ExtensionTags` | string | JSON array phần mở rộng: `"[\".jpg\",\".png\"]"`. |
| `ReadFileContents` | bool | `true` = đọc nội dung file text ra output. |
| `ReadContentExtensionsText` | string | Loại file nào được đọc nội dung (ví dụ `".txt,.json"`). |

- **Ví dụ**: Quét tất cả ảnh trong thư mục:
```json
"Properties": {
  "FolderPath": "C:\\Pictures",
  "IncludeSubfolders": true,
  "ExtensionFilterText": ".jpg,.png,.gif",
  "ExtensionTags": "[\".jpg\",\".png\",\".gif\"]",
  "ReadFileContents": false
}
```

---

## 3. Data Fetcher Node
- **Type**: `"DataFetcher"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Lấy output từ node khác theo polling. Hữu ích khi cần đợi node Web/API hoàn thành rồi mới lấy kết quả.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `SourceNodeId` | string | ID node muốn lấy output (thường là Web node hoặc HtmlUi node). |
| `SourceOutputKey` | string | Output key cần lấy từ node nguồn. |
| `WaitForWebNodeLoad` | bool | `true` = chờ Web node load xong trang trước khi lấy data. |
| `EnableTimer` | bool | `true` = lấy data liên tục theo interval (auto-refresh). |
| `TimerIntervalValue` | int | Giá trị interval. |
| `TimerUnit` | string | Đơn vị: `"Seconds"` / `"Minutes"`. |
| `EnableRealtime` | bool | `true` = mode realtime, cập nhật data ngay khi node nguồn thay đổi. |
| `EnableDataReadyScan` | bool | `true` = kiểm tra xem data đã sẵn sàng chưa trước khi lấy. |
| `DataReadyScanIntervalValue` | int | Interval kiểm tra data ready. |
| `DataReadyScanUnit` | string | Đơn vị. |
| `RunSourceNodeFirst` | bool | `true` = chạy lại node nguồn trước khi lấy output. |

- **Ví dụ**: Lấy response từ Web node, chờ load xong:
```json
"Properties": {
  "SourceNodeId": "Node_Web_abc...",
  "SourceOutputKey": "html",
  "WaitForWebNodeLoad": true,
  "EnableTimer": false,
  "RunSourceNodeFirst": false
}
```

---

## 4. Git Source Node
- **Type**: `"GitSource"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Quản lý repository Git. Clone, pull, mở trong editor (VSCodium). Hiển thị trạng thái commit.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `RepoUrl` | string | URL repository (ví dụ `"https://github.com/user/repo.git"`). |
| `LocalPath` | string | Đường dẫn thư mục local clone. |
| `Branch` | string | Branch cần checkout (mặc định `"main"`). |
| `DisplayName` | string | Tên hiển thị trên canvas. |
| `AutoOpenOnExecute` | bool | `true` = tự mở trong editor khi chạy node. |
| `CommandText` | string | Lệnh terminal tùy chỉnh chạy sau khi pull. |

- **Ví dụ**:
```json
"Properties": {
  "RepoUrl": "https://github.com/user/repo.git",
  "LocalPath": "D:\\Projects\\repo",
  "Branch": "main",
  "DisplayName": "My Project",
  "AutoOpenOnExecute": false
}
```

---

## 5. Flow Overwrite Node
- **Type**: `"FlowOverwrite"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Gộp/đè output từ nhiều node thành 1 output thống nhất. Dùng khi cần merge kết quả.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `OutputKey` | string | Tên key output kết quả (ví dụ `"mergedData"`). |
| `AppendMode` | bool | `true` = nối thêm (append). `false` = ghi đè (overwrite). |
| `IncludeIndirectSources` | bool | `true` = bao gồm cả output gián tiếp (từ node → node → node). |
| `Mappings` | string | JSON array mapping nguồn: `[{"SourceNodeId":"id","SourceOutputKey":"key","TargetKey":"field_name"}]`. |

- **Ví dụ**: Gộp 2 API response:
```json
"Properties": {
  "OutputKey": "mergedResult",
  "AppendMode": false,
  "IncludeIndirectSources": false,
  "Mappings": "[{\"SourceNodeId\":\"Node_Http_1\",\"SourceOutputKey\":\"responseBody\",\"TargetKey\":\"api1\"},{\"SourceNodeId\":\"Node_Http_2\",\"SourceOutputKey\":\"responseBody\",\"TargetKey\":\"api2\"}]"
}
```

---

## 6. Notification Node
- **Type**: `"Notification"`
- **ColorKey**: `"CantaloupeOrange"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Hiện popup thông báo Toast trên màn hình. Có thể dùng text tĩnh hoặc lấy nội dung động từ node khác.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `DefaultDurationSeconds` | int | Thời gian hiển thị popup (giây). |
| `StaticTitle` | string | Tiêu đề cố định (dùng khi không có TitleInput binding). |
| `StaticContent` | string | Nội dung cố định (dùng khi không có ContentInput binding). |
| `TitleInput` | string | *(Tùy chọn)* JSON object binding tiêu đề từ node khác: `{"VariableKey":"title","SourceNodeId":"id","SourceOutputKey":"key"}`. Nếu `SourceNodeId` trống → dùng `StaticTitle`. |
| `ContentInput` | string | *(Tùy chọn)* JSON object binding nội dung từ node khác. Tương tự TitleInput. |
| `DurationInput` | string | *(Tùy chọn)* JSON object binding thời gian hiển thị từ node khác. |
| `ToastBackgroundOpacity` | double | Độ trong suốt nền popup (0.0 → 1.0). |

- **Ví dụ**: Thông báo tĩnh:
```json
"Properties": {
  "DefaultDurationSeconds": 5,
  "StaticTitle": "✅ Thành công",
  "StaticContent": "Workflow đã hoàn thành!"
}
```

- **Ví dụ**: Thông báo động (nội dung từ node khác):
```json
"Properties": {
  "DefaultDurationSeconds": 5,
  "StaticTitle": "",
  "StaticContent": "",
  "TitleInput": "{\"VariableKey\":\"title\",\"SourceNodeId\":\"\",\"SourceOutputKey\":\"\"}",
  "ContentInput": "{\"VariableKey\":\"content\",\"SourceNodeId\":\"Node_Output_abc...\",\"SourceOutputKey\":\"result\"}"
}
```

---

## 7. Border Highlight Node
- **Type**: `"BorderHighlight"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Highlight viền cửa sổ ứng dụng Windows. Dùng để đánh dấu visual khi automation đang thao tác trên cửa sổ nào.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `BorderColorHex` | string | Màu viền highlight (hex, ví dụ `"#00D2FF"`). |
| `BorderThickness` | int | Độ dày viền (1-10 pixel). |
| `GradientSize` | int | Kích thước gradient glow (5-50 pixel). |
| `Opacity` | double | Độ trong suốt (0.1 → 1.0). |
| `EffectType` | string | Kiểu hiệu ứng: `"SolidBorder"` (viền đặc) / `"GradientGlow"` (phát sáng). |
| `HighlightMode` | string | `"SelectedWindow"` (1 cửa sổ) / `"AllWindows"` (tất cả). |
| `TargetProcessName` | string | Tên process cần highlight. |
| `DurationMs` | int | Thời gian hiệu ứng (ms). |
| `WaitForCompletion` | bool | `true` = chờ hiệu ứng xong rồi mới chạy node tiếp. |

- **Ví dụ**: Highlight Notepad 3 giây:
```json
"Properties": {
  "BorderColorHex": "#00D2FF",
  "BorderThickness": 3,
  "Opacity": 0.8,
  "EffectType": "SolidBorder",
  "HighlightMode": "SelectedWindow",
  "TargetProcessName": "notepad",
  "DurationMs": 3000,
  "WaitForCompletion": true
}
```

---

## 8. Folder Node
- **Type**: `"Folder"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Quản lý thư mục output với template đường dẫn động. Tự tạo thư mục con dựa trên biến.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `RootFolderPath` | string | Đường dẫn thư mục gốc (ví dụ `"D:\\Output"`). |
| `RootFolderPresetKey` | string | Key preset thư mục (Desktop, Downloads...). |
| `SubPathTemplate` | string | Template thư mục con dùng biến: `"{date}/{category}"`. Biến lấy từ `KeyValueInputs`. |
| `KeyValueInputs` | string | JSON array biến cho template: `[{"Key":"category","SourceNodeId":"id","SourceOutputKey":"cat"}]`. |

- **Ví dụ**: Thư mục output theo ngày và loại:
```json
"Properties": {
  "RootFolderPath": "D:\\Output",
  "SubPathTemplate": "{date}/{type}",
  "KeyValueInputs": "[{\"Key\":\"type\",\"SourceNodeId\":\"Node_Input_abc...\",\"SourceOutputKey\":\"fileType\"}]"
}
```
