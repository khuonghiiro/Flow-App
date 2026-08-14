# Data & Variables Nodes (Nhóm Xử lý Dữ liệu và Biến số)

Tài liệu này trình bày Logic thực thi và Cấu trúc JSON `Properties` cho các Node quản lý biến, bộ nhớ tạm.

> **LƯU Ý**: Ngoài các Properties đặc thù dưới đây, MỌI node đều phải có thêm bộ **Shared Properties** (`RunMode`, `AutoRunIntervalValue`, `EndBehavior`, `TitleDisplayMode`...). Xem `_NODE_RENDER_LOGIC_SYNTHESIS.md` mục 2.

---

## 1. Input Node
- **Type**: `"Input"`
- **ColorKey**: `"Ocean"`
- **Ports**: ⚠️ Chỉ có **1 port Output Right** (KHÔNG có port Input). InputNode là nguồn dữ liệu, có thể nối RA nhưng KHÔNG THỂ nhận nối VÀO.
- **Chức năng**: Khai báo biến đầu vào cho workflow. Giá trị được gán sẵn hoặc nhập trước khi chạy. Các node khác tham chiếu output của InputNode để lấy dữ liệu.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `InputKey` | string | Tên biến / key output (ví dụ `"userName"`, `"apiUrl"`). ⚠️ KHÔNG dùng `"Key"`. |
| `InputValue` | string | Giá trị mặc định của biến (ví dụ `"Hello World"`, `"https://api.com"`). ⚠️ KHÔNG dùng `"Value"`. |
| `InputDataType` | string | Kiểu dữ liệu: `"String"` / `"Integer"` / `"Double"` / `"Boolean"` / `"Array"`. ⚠️ KHÔNG dùng `"DataType"`. |
| `InputArrayValues` | string | *(Chỉ dùng khi DataType = Array)* JSON array giá trị: `"[\"item1\",\"item2\"]"`. |

- **Ví dụ**: Khai báo URL API:
```json
"Properties": {
  "InputKey": "apiUrl",
  "InputValue": "https://jsonplaceholder.typicode.com/posts/1",
  "InputDataType": "String"
}
```

- **Cách node khác sử dụng dữ liệu từ Input** — cần **CẢ 2 tầng**:

**Tầng 1 — Connection visual (mảng Connections):** Phải tạo dây nối từ Input → node đích:
```json
// Trong mảng "Connections":
{ "FromNodeId": "Node_Input_abc...", "ToNodeId": "Node_HttpRequest_def...", "FromPortId": "input_output_port_guid", "ToPortId": "http_input_port_guid" }
```

**Tầng 2 — Property binding (trong Properties của node đích):** Chỉ định dữ liệu lấy từ đâu:
```json
// Trong HttpRequest Properties:
"UrlSourceNodeId": "Node_Input_abc...",
"UrlSourceOutputKey": "apiUrl"
```

> ⚠️ **PHẢI CÓ CẢ 2**: Nếu chỉ có Tầng 2 (UrlSourceNodeId) mà không có Tầng 1 (Connection) → Input node sẽ hiển thị cô lập, không có dây nối trên canvas!

---

## 2. Output Node
- **Type**: `"Output"`
- **ColorKey**: `"Emerald"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Thu thập và hiển thị kết quả. Có thể format string với biến từ nhiều node khác. Copy kết quả vào clipboard.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `OutputKey` | string | Tên key output (ví dụ `"result"`, `"apiResult"`). |
| `FormatString` | string | Template chuỗi kết quả. Dùng `{tên_biến}` để chèn giá trị. Ví dụ: `"Kết quả: {data}"`. |
| `SaveToClipboard` | string | `"True"` / `"False"` — tự động copy kết quả vào clipboard. |
| `InputVariables` | string | JSON array mapping biến từ node khác. **Đây là cách Output node lấy dữ liệu**. |

- **Cấu trúc InputVariables** (quan trọng):
```json
"InputVariables": "[{\"VariableKey\":\"data\",\"SourceNodeId\":\"Node_HttpRequest_abc...\",\"SourceOutputKey\":\"responseBody\"}]"
```
  - `VariableKey`: Tên biến dùng trong `FormatString` (ví dụ `{data}`)
  - `SourceNodeId`: ID node cung cấp dữ liệu
  - `SourceOutputKey`: Output key của node nguồn

- **Ví dụ hoàn chỉnh**: Xuất response API:
```json
"Properties": {
  "OutputKey": "apiResult",
  "FormatString": "API trả về: {responseBody}",
  "SaveToClipboard": "False",
  "InputVariables": "[{\"VariableKey\":\"responseBody\",\"SourceNodeId\":\"Node_HttpRequest_d4e5f6a7-...\",\"SourceOutputKey\":\"responseBody\"}]"
}
```

---

## 3. Storage Node
- **Type**: `"Storage"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right). Port visibility thay đổi theo mode.
- **Chức năng**: Lưu trữ Key-Value vĩnh viễn (persist qua các lần chạy). Toggle giữa 2 mode: Ghi (lưu data từ node khác) và Đọc (lấy data đã lưu ra).
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `IsInputMode` | bool | `true` = chế độ Ghi (nhận data vào, ẩn output). `false` = chế độ Đọc (xuất data ra). |
| `SourceNodeId` | string | *(Chế độ Ghi)* ID node cung cấp data cần lưu. |
| `SourceOutputKey` | string | *(Chế độ Ghi)* Output key của node nguồn. |
| `OutputKeys` | string | *(Chế độ Đọc)* JSON array các key cần đọc: `"[\"token\",\"userId\"]"`. |

- **Ví dụ**: Lưu token API:
```json
"Properties": {
  "IsInputMode": true,
  "SourceNodeId": "Node_HttpRequest_abc...",
  "SourceOutputKey": "authToken"
}
```

---

## 4. List Out Node
- **Type**: `"ListOut"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right) + Dynamic output ports
- **Chức năng**: Gom output từ nhiều node thành 1 mảng. Hữu ích khi cần tổng hợp kết quả từ nhiều nguồn.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `OutputMappings` | string | JSON array mapping nguồn. Mỗi item: `{"SourceNodeId":"id","SourceOutputKey":"key","OutputKey":"item_name"}`. |

- **Ví dụ**: Gom 2 kết quả:
```json
"Properties": {
  "OutputMappings": "[{\"SourceNodeId\":\"Node_A\",\"SourceOutputKey\":\"value\",\"OutputKey\":\"item1\"},{\"SourceNodeId\":\"Node_B\",\"SourceOutputKey\":\"value\",\"OutputKey\":\"item2\"}]"
}
```

---

## 5. Assign Data Node
- **Type**: `"AssignData"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Gán/đổi tên biến. Lấy output từ node khác và tạo output mới với tên khác. Dùng khi cần chuẩn hóa tên biến.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `Assignments` | string | JSON array các phép gán. Mỗi item: `{"SourceNodeId":"id","SourceOutputKey":"old_key","TargetKey":"new_key"}`. |

- **Ví dụ**: Đổi tên "responseBody" → "userData":
```json
"Properties": {
  "Assignments": "[{\"SourceNodeId\":\"Node_HttpRequest_abc...\",\"SourceOutputKey\":\"responseBody\",\"TargetKey\":\"userData\"}]"
}
```

---

## 6. Key Value Bridge Node
- **Type**: `"KeyValueBridge"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right) + Dynamic data ports
- **Chức năng**: Chia sẻ dữ liệu giữa các luồng/workflow khác nhau qua kênh tên (channel). Mode Pass = ghi data vào kênh, mode Get = đọc data từ kênh (có polling).
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `IsPassKeyMode` | bool | `true` = mode Ghi (Pass data vào kênh). `false` = mode Đọc (Get data từ kênh). |
| `KvChannelKey` | string | Tên kênh chia sẻ (ví dụ `"shared_data"`). 2 node cùng kênh sẽ chia sẻ data. |
| `SelectedSourceBridgeNodeId` | string | *(Mode Đọc)* ID của node Bridge nguồn cần đọc. |
| `PollIntervalValue` | int | *(Mode Đọc)* Interval kiểm tra data mới. |
| `PollIntervalUnit` | string | Đơn vị: `"Milliseconds"` / `"Seconds"` / `"Minutes"`. |
| `EnableDataCleanup` | bool | `true` = tự động xóa data cũ sau khi đọc. |
| `CleanupTargetBridgeNodeId` | string | ID node Bridge cần xóa data. |
| `CleanupClearAllNodeData` | bool | `true` = xóa toàn bộ data trên node đích. |

- **Ví dụ**: Ghi data vào kênh "auth":
```json
"Properties": {
  "IsPassKeyMode": true,
  "KvChannelKey": "auth_channel"
}
```

---

## 7. String Split Node
- **Type**: `"StringSplit"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Tách chuỗi bằng regex pattern thành mảng. Mặc định tách theo dòng mới.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `RegexPattern` | string | Pattern regex dùng để tách chuỗi. ⚠️ KHÔNG dùng `"Separator"`. Ví dụ: `"\\r?\\n"` (tách dòng), `","` (tách dấu phẩy). |
| `OutputKey` | string | Tên key output chứa mảng kết quả (mặc định `"ListItems"`). |

- **Ví dụ**: Tách CSV theo dấu phẩy:
```json
"Properties": {
  "RegexPattern": ",",
  "OutputKey": "ListItems"
}
```

---

## 8. Text Scan Node
- **Type**: `"TextScan"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: OCR — Quét ảnh/vùng màn hình thành văn bản. Hỗ trợ Tesseract (offline) và Windows OCR (nhanh).
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `OcrEngineMode` | string | Engine OCR: `"WindowsOcr"` (nhanh, cần Windows 10+) / `"Tesseract"` (chính xác, cần tessdata). |
| `ImageSourceMode` | string | Nguồn ảnh: `"ScreenRegion"` (chụp vùng màn hình) / `"FromNode"` (lấy từ node khác) / `"FromFile"` (từ file). |
| `CaptureX` | int | Tọa độ X vùng chụp (pixel). |
| `CaptureY` | int | Tọa độ Y vùng chụp (pixel). |
| `CaptureWidth` | int | Chiều rộng vùng chụp. |
| `CaptureHeight` | int | Chiều cao vùng chụp. |
| `OcrLanguage` | string | Mã ngôn ngữ OCR (ví dụ `"en"`, `"vi"`, `"ja"`). |
| `AutoDetectLanguage` | string | `"True"` = tự nhận diện ngôn ngữ. |
| `ImageSourceNodeId` | string | *(Khi mode = FromNode)* ID node cung cấp ảnh. |
| `ImageSourceOutputKey` | string | *(Khi mode = FromNode)* Output key chứa ảnh (base64 hoặc path). |
| `ImagePath` | string | *(Khi mode = FromFile)* Đường dẫn file ảnh. |

- **Ví dụ**: Quét chữ vùng màn hình:
```json
"Properties": {
  "OcrEngineMode": "WindowsOcr",
  "ImageSourceMode": "ScreenRegion",
  "CaptureX": 100, "CaptureY": 200,
  "CaptureWidth": 500, "CaptureHeight": 100,
  "OcrLanguage": "vi",
  "AutoDetectLanguage": "False"
}
```

---

## 9. Key Scoped Store Node
- **Type**: `"KeyScopedStore"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Bộ nhớ đệm theo phiên (Dictionary Bucket). Gom dữ liệu từ nhiều luồng vào 1 bucket trong cùng ExecutionId. Mode Write = ghi vào, mode Read = đọc ra JSON.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `IsWriteMode` | bool | `true` = ghi dữ liệu vào bucket. `false` = đọc toàn bộ bucket ra JSON. |
| `StaticKey` | string | Tên bucket (ví dụ `"collected_results"`). |
| `PollTimeValue` | int | *(Mode Đọc)* Interval kiểm tra bucket có data mới. |
| `PollUnit` | string | Đơn vị: `"Milliseconds"` / `"Seconds"` / `"Minutes"`. |

- **Ví dụ**: Ghi vào bucket:
```json
"Properties": {
  "IsWriteMode": true,
  "StaticKey": "api_results",
  "PollTimeValue": 2,
  "PollUnit": "Seconds"
}
```
