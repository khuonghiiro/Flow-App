# Browser & Web Nodes (Nhóm Xử lý Web & Mạng)

Tài liệu này trình bày Logic thực thi và Cấu trúc JSON `Properties` cho các Node web.

---

## 1. Web Node
- **Type**: `"Web"`
- **Chức năng**: Khởi tạo trình duyệt ảo (WebView2), nhúng JS, chặn request, trích xuất dữ liệu.
- **Ví dụ JSON Properties**:
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
  "RequestInterceptRules": "[{\"MatchUrlPattern\":\"*api*\",\"ReplaceUrlValue\":\"http://localhost\"}]",
  "JsSources": "[{\"SourceNodeId\":\"123\",\"SourceOutputKey\":\"script_text\"}]",
  "ResponseOutputs": "[{\"Key\":\"result\",\"Url\":\"*\",\"RequestMethod\":\"GET\",\"ExtractType\":\"Response\"}]",
  "AutoReloadEnabled": false,
  "AutoReloadIntervalValue": 10.0,
  "AutoReloadIntervalUnit": "Seconds",
  "BlockAllRequestsAfterFirstMatch": true,
  "EnableSleepMode": false
}
```

## 2. HTML UI Node
- **Type**: `"HtmlUi"`
- **Chức năng**: Tải trang Web từ bộ mã HTML/CSS/JS cục bộ để tạo giao diện tuỳ chỉnh.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "Width": 400.0,
  "Height": 300.0,
  "HtmlTemplate": "<div>Hello World</div>",
  "CssTemplate": "body { color: red; }",
  "JsTemplate": "console.log('UI loaded');"
}
```

## 3. Embed Application Node
- **Type**: `"EmbedApplicationNode"`
- **Chức năng**: Nhúng cửa sổ ứng dụng Windows (.exe) vào Canvas.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "Width": 800.0,
  "Height": 600.0,
  "TargetProcessName": "notepad",
  "AppPath": "C:\\Windows\\notepad.exe"
}
```

## 4. HTTP Request Node
- **Type**: `"HttpRequest"`
- **Chức năng**: Gọi REST API ngầm (cURL).
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "Url": "https://api.example.com/data",
  "Method": "POST",
  "TimeoutMs": 10000,
  "Headers": "[{\"Key\":\"Authorization\",\"Value\":\"Bearer token\"}]",
  "Body": "{\"name\": \"test\"}",
  "BodyFormat": "Json"
}
```

## 5. File Download Node
- **Type**: `"FileDownload"`
- **Chức năng**: Tải file từ internet về máy tính.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "DownloadUrl": "https://example.com/file.zip",
  "SaveDirectory": "C:\\Downloads",
  "AutoRenameIfExists": true,
  "DynIn_DownloadUrl_SrcNode": "url_node",
  "DynIn_DownloadUrl_SrcKey": "out_url"
}
```
