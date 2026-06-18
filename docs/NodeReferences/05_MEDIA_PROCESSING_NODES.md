# Media Processing Nodes (Nhóm Xử lý Ảnh & Video)

Tài liệu này trình bày Logic thực thi và Cấu trúc JSON `Properties` cho các Node xử lý đa phương tiện.

> **LƯU Ý**: Ngoài các Properties đặc thù dưới đây, MỌI node đều phải có thêm bộ **Shared Properties** (`RunMode`, `AutoRunIntervalValue`, `EndBehavior`, `TitleDisplayMode`...). Xem `_NODE_RENDER_LOGIC_SYNTHESIS.md` mục 2.

---

## 1. Image Processing Node
- **Type**: `"ImageProcessing"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right). Kích thước mặc định 360x280.
- **Chức năng**: Hiển thị và xử lý ảnh trên canvas. Nhận ảnh từ URL, Base64, hoặc output node khác. Hỗ trợ crop, FFmpeg filter, và AI prompt.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `Width` | double | Chiều rộng hiển thị trên canvas (pixel). |
| `Height` | double | Chiều cao hiển thị trên canvas (pixel). |
| `InputMode` | string | Nguồn ảnh: `"FromUrl"` (từ URL), `"FromBase64"` (từ base64 string), `"FromNode"` (từ output node khác). |
| `CropMode` | string | Chế độ cắt: `"None"` (không cắt), `"Manual"` (cắt thủ công), `"Auto"` (cắt tự động). |
| `ImageUrl` | string | *(Khi InputMode = FromUrl)* URL ảnh cần hiển thị. |
| `ImageUrlSourceNodeId` | string | *(Tùy chọn)* ID node cung cấp URL ảnh động (thay vì URL cố định). |
| `ImageUrlSourceOutputKey` | string | *(Tùy chọn)* Output key chứa URL ảnh. |
| `ImageBase64` | string | *(Khi InputMode = FromBase64)* Chuỗi base64 của ảnh. |
| `ImageBase64SourceNodeId` | string | *(Tùy chọn)* ID node cung cấp base64 ảnh động. |
| `ImageBase64SourceOutputKey` | string | *(Tùy chọn)* Output key chứa base64. |
| `PreferGpu` | bool | `true` = ưu tiên GPU cho xử lý ảnh (nhanh hơn). |
| `FfmpegFilter` | string | FFmpeg filter string để xử lý ảnh (ví dụ `"scale=640:480"`). |
| `CroppedFolderPath` | string | Thư mục lưu ảnh đã crop. |
| `IsVerticalMode` | bool | `true` = hiển thị ảnh dọc trên canvas. |
| `PromptSize` | int | Kích thước vùng nhập prompt AI (pixel). |
| `ProcessorPrompt` | string | Prompt AI để xử lý ảnh (mô tả xử lý mong muốn). |

- **Ví dụ**: Hiển thị ảnh từ URL:
```json
"Properties": {
  "Width": 360,
  "Height": 280,
  "InputMode": "FromUrl",
  "CropMode": "None",
  "ImageUrl": "https://example.com/photo.jpg",
  "PreferGpu": false,
  "IsVerticalMode": false
}
```

- **Ví dụ**: Hiển thị ảnh từ ScreenCapture node:
```json
"Properties": {
  "Width": 360,
  "Height": 280,
  "InputMode": "FromBase64",
  "ImageBase64SourceNodeId": "Node_ScreenCapture_abc...",
  "ImageBase64SourceOutputKey": "capturedImage"
}
```

---

## 2. Video Processing Node
- **Type**: `"VideoProcessing"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right). Kích thước mặc định 1360x768.
- **Chức năng**: Nhúng trình phát video trực tiếp trên canvas. Phát video từ file hoặc từ output node khác.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `Width` | double | Chiều rộng player trên canvas. Mặc định `1360`. |
| `Height` | double | Chiều cao player. Mặc định `768`. |
| `VideoPath` | string | Đường dẫn file video (ví dụ `"C:\\videos\\clip.mp4"`). |
| `VideoSourceNodeId` | string | *(Tùy chọn)* ID node cung cấp đường dẫn/URL video động. |
| `VideoSourceOutputKey` | string | *(Tùy chọn)* Output key chứa đường dẫn video. |
| `OutputFolderSourceNodeId` | string | *(Tùy chọn)* ID node cung cấp thư mục output. |
| `OutputFolderSourceOutputKey` | string | *(Tùy chọn)* Output key chứa đường dẫn thư mục. |

- **Ví dụ**: Phát video cố định:
```json
"Properties": {
  "Width": 1360,
  "Height": 768,
  "VideoPath": "C:\\Videos\\demo.mp4"
}
```

---

## 3. Media Gallery Node
- **Type**: `"MediaGallery"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right). Kích thước mặc định 500x400.
- **Chức năng**: Lưới hiển thị ảnh/video gallery trên canvas. Nhận dữ liệu JSON từ node khác, tự render thành grid ảnh. Hỗ trợ popup preview và lưu file.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `Width` | double | Chiều rộng gallery (pixel). |
| `Height` | double | Chiều cao gallery (pixel). |
| `FrameDisplayWidth` | double | Chiều rộng mỗi thumbnail (pixel). |
| `FrameDisplayHeight` | double | Chiều cao mỗi thumbnail (pixel). |
| `DisplayMode` | string | Bố cục: `"Grid"` (lưới) / `"List"` (danh sách). |
| `ItemClickPreviewMode` | string | Hành vi khi click item: `"Popup"` (mở popup lớn) / `"Inline"` (mở tại chỗ). |
| `TitleKeyTemplate` | string | Key trong JSON data chứa tiêu đề item (ví dụ `"title"`). |
| `ImageUrlKeyTemplate` | string | Key trong JSON data chứa URL ảnh (ví dụ `"imageUrl"`). |
| `VideoUrlKeyTemplate` | string | Key chứa URL video. |
| `GroupArrayKey` | string | *(Tùy chọn)* Key chứa array group. |
| `GroupTitleKey` | string | *(Tùy chọn)* Key chứa tên nhóm. |
| `GroupItemsKey` | string | *(Tùy chọn)* Key chứa items trong nhóm. |
| `JsonSourceNodeId` | string | **ID node cung cấp JSON data** (bắt buộc). Gallery đọc data từ output của node này. |
| `JsonSourceOutputKey` | string | Output key chứa JSON array data. |
| `FolderSaveImages` | string | Thư mục lưu ảnh khi người dùng download. |
| `FolderSourceNodeId` | string | ID node cung cấp thư mục lưu ảnh động. |
| `CanReexecuteSourceNode` | bool | `true` = cho phép người dùng chạy lại node nguồn từ giao diện gallery. |

- **Ví dụ**: Gallery ảnh từ API response:
```json
"Properties": {
  "Width": 500,
  "Height": 400,
  "FrameDisplayWidth": 200,
  "FrameDisplayHeight": 200,
  "DisplayMode": "Grid",
  "ItemClickPreviewMode": "Popup",
  "TitleKeyTemplate": "title",
  "ImageUrlKeyTemplate": "thumbnailUrl",
  "JsonSourceNodeId": "Node_HttpRequest_abc...",
  "JsonSourceOutputKey": "responseBody",
  "CanReexecuteSourceNode": false
}
```
