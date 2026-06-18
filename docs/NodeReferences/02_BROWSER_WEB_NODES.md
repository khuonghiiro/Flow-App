# Browser & Web Nodes (Nhóm Xử lý Web & Mạng)

Tài liệu này trình bày Logic thực thi và Cấu trúc JSON `Properties` cho các Node web.

> **LƯU Ý**: Ngoài các Properties đặc thù dưới đây, MỌI node đều phải có thêm bộ **Shared Properties** (`RunMode`, `AutoRunIntervalValue`, `EndBehavior`, `TitleDisplayMode`...). Xem `_NODE_RENDER_LOGIC_SYNTHESIS.md` mục 2.

---

## 1. Web Node
- **Type**: `"Web"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right). Kích thước mặc định 800x600.
- **Chức năng**: Nhúng trình duyệt WebView2 vào canvas. Có thể: mở URL, chặn request, inject JavaScript, trích xuất response API. Rất mạnh cho web scraping và automation.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `Width` | double | Chiều rộng cửa sổ trình duyệt trên canvas (pixel). |
| `Height` | double | Chiều cao cửa sổ trình duyệt trên canvas (pixel). |
| `ExtractUrl` | string | URL mà trình duyệt sẽ mở khi node được kích hoạt. |
| `ExtractRequestMethod` | string | Phương thức HTTP cần bắt: `"GET"` / `"POST"` / `"All"`. |
| `ExtractStatusCode` | string | Mã status cần bắt (ví dụ `"200"`). Để trống = bắt tất cả. |
| `ResponseOutputsWaitTimeoutMs` | int | Thời gian chờ tối đa (ms) để bắt response trước khi timeout. |
| `ResponseOutputsWaitMode` | string | `"All"` = chờ tất cả output match. `"Any"` = chờ 1 cái match là đủ. |
| `BlockingRules` | string | JSON array các rule chặn request. Mỗi rule: `{"UrlPattern":"*ads*","Method":"All"}`. Dùng để chặn quảng cáo, tracking. |
| `RequestInterceptRules` | string | JSON array rule đổi hướng request. Mỗi rule: `{"MatchUrlPattern":"*api*","ReplaceUrlValue":"http://localhost"}`. |
| `JsSources` | string | JSON array nguồn JavaScript inject. Mỗi item: `{"SourceNodeId":"node_id","SourceOutputKey":"script_text"}`. Lấy code JS từ output của node khác rồi chạy trên trang web. |
| `ResponseOutputs` | string | JSON array khai báo output cần trích xuất. Mỗi item: `{"Key":"result","Url":"*api*","RequestMethod":"GET","ExtractType":"Response"}`. Khi trình duyệt bắt được request khớp pattern → trích response body ra thành output. |
| `AutoReloadEnabled` | bool | `true` = tự động reload trang theo interval. |
| `AutoReloadIntervalValue` | double | Giá trị interval reload. |
| `AutoReloadIntervalUnit` | string | Đơn vị: `"Seconds"` / `"Minutes"`. |
| `BlockAllRequestsAfterFirstMatch` | bool | `true` = sau khi bắt được response đầu tiên, chặn tất cả request còn lại. |
| `EnableSleepMode` | bool | `true` = cho trình duyệt ngủ khi không sử dụng để tiết kiệm tài nguyên. |

- **Ví dụ**: Mở Google và bắt response:
```json
"Properties": {
  "Width": 800.0,
  "Height": 600.0,
  "ExtractUrl": "https://google.com",
  "ExtractRequestMethod": "GET",
  "ExtractStatusCode": "200",
  "ResponseOutputsWaitTimeoutMs": 5000,
  "ResponseOutputsWaitMode": "All",
  "BlockingRules": "[{\"UrlPattern\":\"*ads*\",\"Method\":\"All\"}]",
  "ResponseOutputs": "[{\"Key\":\"html\",\"Url\":\"*\",\"RequestMethod\":\"GET\",\"ExtractType\":\"Response\"}]",
  "AutoReloadEnabled": false,
  "EnableSleepMode": false
}
```

---

## 2. HTML UI Node
- **Type**: `"HtmlUi"`
- **ColorKey**: `"EspressoBrown"`
- **Ports**: 2 ports (Input Left + Output Right). Kích thước mặc định 420x320.
- **Chức năng**: Render giao diện HTML/CSS/JS tùy chỉnh trực tiếp trên canvas. Có thể nhận data từ node khác qua `InputMappings` và gửi data ra ngoài qua `OutputKeys`. Dùng để tạo UI dashboard, form nhập liệu.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `HtmlCode` | string | Mã HTML hiển thị (ví dụ `"<div>Hello</div>"`). |
| `JsCode` | string | Mã JavaScript chạy trong webview. Dùng `SetOutput("key", value)` để gửi data ra ngoài. |
| `CssCode` | string | Mã CSS styling cho giao diện. |
| `ParamsCode` | string | Mã tham số bổ sung. |
| `InputMappings` | string | JSON array mapping data từ node khác vào giao diện. Mỗi item: `{"SourceNodeId":"id","SourceOutputKey":"key","InputKeyOverride":"dataKey","ShouldReExecute":false,"AutoRefreshEnabled":false,"AutoRefreshInterval":1000,"AutoRefreshUnit":"ms"}`. |
| `OutputKeys` | string | JSON array khai báo các key output mà JS có thể gửi ra: `["result","status"]`. |
| `Width` | double | Chiều rộng (pixel). |
| `Height` | double | Chiều cao (pixel). |
| `EnableSleepMode` | bool | `true` = ngủ khi không sử dụng. |
| `UseWebTab` | bool | `true` = mở URL thật thay vì render HTML cục bộ. |
| `WebTabUrl` | string | URL khi `UseWebTab` = true. |

- **Ví dụ**: Form nhập liệu đơn giản:
```json
"Properties": {
  "HtmlCode": "<input id='name' placeholder='Nhập tên'><button onclick='send()'>Gửi</button>",
  "JsCode": "function send() { SetOutput('userName', document.getElementById('name').value); }",
  "CssCode": "body { font-family: Arial; padding: 10px; }",
  "OutputKeys": "[\"userName\"]",
  "Width": 420,
  "Height": 320
}
```

---

## 3. Embed Application Node
- **Type**: `"EmbedApplicationNode"`
- **ColorKey**: `"CharcoalDark"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Nhúng cửa sổ ứng dụng Windows (.exe) vào canvas. Chụp hình ảnh cửa sổ app và hiển thị real-time.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `ProcessName` | string | Tên process (ví dụ `"notepad"`, `"chrome"`). |
| `ProcessId` | int | PID của process (0 = tự tìm theo ProcessName). |
| `WindowHandle` | string | Handle cửa sổ Windows (dạng số, `"0"` = tự tìm). |
| `WindowTitle` | string | Tiêu đề cửa sổ để tìm (ví dụ `"Untitled - Notepad"`). |
| `EmbeddedWidth` | int | Chiều rộng hiển thị (pixel). |
| `EmbeddedHeight` | int | Chiều cao hiển thị (pixel). |
| `IsActive` | bool | `true` = đang nhúng hoạt động. |
| `ShowBorder` | bool | `true` = hiện viền xung quanh cửa sổ nhúng. |
| `AllowInteraction` | bool | `true` = cho phép click/nhập trực tiếp vào app nhúng. |
| `AutoRefresh` | bool | `true` = tự cập nhật hình ảnh theo RefreshRate. |
| `RefreshRate` | int | Số lần cập nhật/giây (FPS). |
| `CaptureMode` | string | Phương thức chụp: `"BitBlt"` (nhanh) / `"PrintWindow"` (chính xác hơn). |

- **Ví dụ**: Nhúng Notepad:
```json
"Properties": {
  "ProcessName": "notepad",
  "ProcessId": 0,
  "WindowHandle": "0",
  "WindowTitle": "",
  "EmbeddedWidth": 800,
  "EmbeddedHeight": 600,
  "ShowBorder": true,
  "AllowInteraction": true,
  "AutoRefresh": false,
  "RefreshRate": 30,
  "CaptureMode": "BitBlt"
}
```

---

## 4. HTTP Request Node
- **Type**: `"HttpRequest"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Gọi REST API (GET/POST/PUT/DELETE). Trả về response body, status code, headers. Hỗ trợ authentication, body JSON, form data, và cURL.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `Url` | string | URL endpoint cần gọi. Có thể để trống nếu dùng `UrlSourceNodeId`. |
| `HttpMethod` | string | Phương thức HTTP: `"GET"` / `"POST"` / `"PUT"` / `"DELETE"` / `"PATCH"`. ⚠️ KHÔNG dùng `"Method"`. |
| `AuthType` | string | Kiểu xác thực: `"None"` / `"Bearer"` / `"Basic"` / `"ApiKey"`. |
| `BodyType` | string | Kiểu body: `"None"` / `"Json"` / `"FormData"` / `"Raw"`. ⚠️ KHÔNG dùng `"BodyFormat"`. |
| `TimeoutSeconds` | int | Thời gian chờ tối đa (giây). ⚠️ KHÔNG dùng `"TimeoutMs"`. |
| `RawBody` | string | Nội dung body (JSON string, raw text...). ⚠️ KHÔNG dùng `"Body"`. |
| `Headers` | string | JSON array headers: `[{"Key":"Content-Type","Value":"application/json"}]`. |
| `QueryParams` | string | JSON array query parameters: `[{"Key":"page","Value":"1"}]`. |
| `FormData` | string | JSON array form data (khi BodyType = FormData). |
| `UrlSourceNodeId` | string | *(Tùy chọn)* ID node cung cấp URL động. Khi có giá trị → bỏ qua `Url` cố định, lấy URL từ output của node này. |
| `UrlSourceOutputKey` | string | *(Tùy chọn)* Output key của node chứa URL động. |
| `BodySourceNodeId` | string | *(Tùy chọn)* ID node cung cấp body động. |
| `BodySourceOutputKey` | string | *(Tùy chọn)* Output key chứa body. |
| `AuthToken` | string | Token xác thực (cho Bearer auth). |
| `TokenSourceNodeId` | string | *(Tùy chọn)* ID node cung cấp token động. |
| `TokenSourceOutputKey` | string | *(Tùy chọn)* Output key chứa token. |
| `UseCurl` | bool | `true` = sử dụng cURL thay vì HttpClient. Bypass anti-bot tốt hơn. |
| `ImpersonateBrowser` | string | Mô phỏng trình duyệt khi dùng cURL (ví dụ `"chrome"`). |

- **Ví dụ**: Gọi API POST với URL lấy từ Input node:
```json
"Properties": {
  "Url": "",
  "HttpMethod": "POST",
  "AuthType": "Bearer",
  "BodyType": "Json",
  "TimeoutSeconds": 30,
  "RawBody": "{\"message\": \"hello\"}",
  "Headers": "[{\"Key\":\"Content-Type\",\"Value\":\"application/json\"}]",
  "UrlSourceNodeId": "Node_Input_abc123...",
  "UrlSourceOutputKey": "apiUrl",
  "AuthToken": "my_token_here",
  "UseCurl": false
}
```

---

## 5. File Download Node
- **Type**: `"FileDownload"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Tải file từ URL về thư mục trên máy tính. Hỗ trợ đặt tên file tùy chỉnh và auto-increment.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `DownloadUrl` | string | URL file cần tải. Có thể để trống nếu dùng `UrlSourceNodeId`. |
| `DownloadFolderPath` | string | Thư mục lưu file tải về. |
| `FileNameTemplate` | string | Template tên file. `{OriginalName}` = giữ tên gốc. |
| `MaxFileNameLength` | int | Giới hạn độ dài tên file (ký tự). |
| `AutoIncrementIfExists` | bool | `true` = thêm số thứ tự nếu file trùng tên. |
| `RemoveDiacriticsFromFileName` | bool | `true` = bỏ dấu tiếng Việt trong tên file. |
| `UrlSourceNodeId` | string | *(Tùy chọn)* ID node cung cấp URL download động. |
| `UrlSourceOutputKey` | string | *(Tùy chọn)* Output key chứa URL. |
| `FolderSourceNodeId` | string | *(Tùy chọn)* ID node cung cấp đường dẫn thư mục động. |
| `FolderSourceOutputKey` | string | *(Tùy chọn)* Output key chứa đường dẫn thư mục. |
| `SaveAdditionalOutputFiles` | bool | `true` = lưu thêm file output phụ. |

- **Ví dụ**: Tải file với URL từ node khác:
```json
"Properties": {
  "DownloadUrl": "",
  "DownloadFolderPath": "C:\\Downloads",
  "FileNameTemplate": "{OriginalName}",
  "MaxFileNameLength": 100,
  "AutoIncrementIfExists": true,
  "UrlSourceNodeId": "Node_Input_url_id",
  "UrlSourceOutputKey": "fileUrl"
}
```
