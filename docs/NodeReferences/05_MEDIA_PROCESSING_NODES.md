# Media Processing Nodes (Nhóm Xử lý Ảnh & Video)

Tài liệu này trình bày Logic thực thi và Cấu trúc JSON `Properties` cho các Node xử lý phương tiện đa phương tiện.

---

## 1. Image Processing Node
- **Type**: `"ImageProcessing"`
- **Chức năng**: Lọc màu, cắt ảnh, đóng dấu bản quyền.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "SourceImagePath": "C:\\images\\source.png",
  "ImageOperations": "[{\"Type\":\"Crop\", \"X\":0, \"Y\":0, \"W\":100, \"H\":100}, {\"Type\":\"Blur\"}]",
  "OutputPath": "C:\\images\\result.png",
  "DynIn_SourceImagePath_SrcNode": "capture_node",
  "DynIn_SourceImagePath_SrcKey": "base64_img"
}
```

## 2. Video Processing Node
- **Type**: `"VideoProcessing"`
- **Chức năng**: Cắt ghép video sử dụng cơ chế FFmpeg ngầm.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "Width": 450.0,
  "Height": 350.0,
  "SourceVideoPath": "C:\\videos\\input.mp4",
  "Operations": "[{\"Type\":\"ExtractAudio\"}]",
  "FFmpegArgs": "-c:v copy -c:a aac",
  "DynIn_SourceVideoPath_SrcNode": "123",
  "DynIn_SourceVideoPath_SrcKey": "video_file"
}
```

## 3. Media Gallery Node
- **Type**: `"MediaGallery"`
- **Chức năng**: Lưới hiển thị kết quả hình ảnh cho người dùng xem lại.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "Width": 500.0,
  "Height": 400.0,
  "InputPaths": "[\"C:\\img1.png\", \"C:\\img2.png\"]",
  "DisplayMode": "Grid",
  "DynIn_InputPaths_SrcNode": "list_out_node",
  "DynIn_InputPaths_SrcKey": "array_of_paths"
}
```
