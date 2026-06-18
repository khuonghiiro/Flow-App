# User Interaction Nodes (Nhóm Tương tác Chuột/Phím)

Tài liệu này trình bày Logic thực thi và Cấu trúc JSON `Properties` cho các Node điều khiển chuột, bàn phím, màn hình.

> **LƯU Ý**: Ngoài các Properties đặc thù dưới đây, MỌI node đều phải có thêm bộ **Shared Properties** (`RunMode`, `AutoRunIntervalValue`, `EndBehavior`, `TitleDisplayMode`...). Xem `_NODE_RENDER_LOGIC_SYNTHESIS.md` mục 2.

---

## 1. Screen Position Picker Node
- **Type**: `"ScreenPosition"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Lưu tọa độ 1 điểm trên màn hình. Khi chạy, xuất tọa độ X/Y ra output cho các node khác dùng (ví dụ MouseEvent, ScreenCapture).
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `SavedX` | int | Tọa độ X đã lưu (pixel từ góc trái trên màn hình). |
| `SavedY` | int | Tọa độ Y đã lưu. |
| `RelativeMode` | string | Chế độ tọa độ: `"Absolute"` (toàn màn hình) / `"RelativeToWindow"` (tương đối cửa sổ). |
| `OutputKey` | string | Tên key output (mặc định `"position"`). |
| `UseMonitorOffset` | bool | `true` = tính offset khi dùng nhiều màn hình. |
| `TargetProcessName` | string | *(Khi RelativeMode = RelativeToWindow)* Tên process làm gốc tọa độ. |
| `TargetWindowTitle` | string | Tiêu đề cửa sổ làm gốc tọa độ. |

- **Ví dụ**: Lưu vị trí nút "Đăng nhập":
```json
"Properties": {
  "SavedX": 500,
  "SavedY": 300,
  "RelativeMode": "Absolute",
  "OutputKey": "loginBtnPos"
}
```

---

## 2. KeyPress Event Node
- **Type**: `"KeyPressEvent"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Mô phỏng nhấn phím bàn phím. Hỗ trợ nhấn nhiều lần, giữ phím, và gửi phím vào cửa sổ nền (background mode).
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `RepeatCount` | int | Số lần nhấn lặp lại. |
| `PressDelay` | int | Khoảng cách giữa mỗi lần nhấn. |
| `DelayUnit` | string | Đơn vị: `"Milliseconds"` / `"Seconds"`. |
| `IsAsync` | bool | `true` = nhấn phím không chờ hoàn thành (chạy tiếp ngay). |
| `HoldDuration` | int | Thời gian giữ phím (dùng cho game, kéo thả). |
| `HoldDurationUnit` | string | Đơn vị thời gian giữ phím. |
| `InputMode` | string | `"ManualKey"` (phím cố định) / `"SourceNode"` (lấy phím từ node khác). |
| `TargetProcessName` | string | *(Tùy chọn)* Gửi phím vào process cụ thể thay vì cửa sổ active. |
| `TargetWindowTitle` | string | Tiêu đề cửa sổ đích. |
| `UseBackgroundMode` | bool | `true` = gửi phím vào cửa sổ nền (không cần focus). |
| `BackgroundInputMode` | string | Phương thức gửi: `"PostMessage"` / `"SendInput"`. |

- **Ví dụ**: Nhấn Enter 1 lần:
```json
"Properties": {
  "RepeatCount": 1,
  "PressDelay": 50,
  "DelayUnit": "Milliseconds",
  "IsAsync": false,
  "InputMode": "ManualKey",
  "UseBackgroundMode": false
}
```

---

## 3. Hotkey Press Event Node
- **Type**: `"HotkeyPressEvent"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Mô phỏng tổ hợp phím (Ctrl+C, Alt+Tab, Ctrl+Shift+V...). Nhấn đồng thời nhiều phím.
- **Mô tả các Properties**: Giống KeyPress, thêm:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `HotkeyKeys` | string | JSON array các phím: `"[\"Control\",\"C\"]"` = Ctrl+C. |

- **Ví dụ**: Ctrl+V (paste):
```json
"Properties": {
  "HotkeyKeys": "[\"Control\",\"V\"]",
  "RepeatCount": 1,
  "PressDelay": 50,
  "DelayUnit": "Milliseconds",
  "UseBackgroundMode": false
}
```

---

## 4. Mouse Event Node
- **Type**: `"MouseEvent"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Mô phỏng thao tác chuột — click, double-click, kéo thả. Có thể di chuyển chuột đến tọa độ và click.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `MouseButton` | string | Nút chuột: `"Left"` / `"Right"` / `"Middle"`. |
| `ClickType` | string | Loại click: `"Single"` / `"Double"` / `"Down"` (nhấn giữ) / `"Up"` (thả). |
| `TargetX` | int | Tọa độ X cần click. Có thể lấy từ ScreenPosition node. |
| `TargetY` | int | Tọa độ Y cần click. |
| `MoveSpeed` | int | Tốc độ di chuyển chuột (0 = teleport ngay lập tức). |
| `RepeatCount` | int | Số lần click lặp lại. |
| `RepeatDelay` | int | Khoảng cách giữa mỗi lần click (ms). |
| `IsAsync` | bool | `true` = click xong không chờ, chạy node tiếp ngay. |
| `TargetProcessName` | string | *(Tùy chọn)* Process đích. |
| `UseBackgroundMode` | bool | `true` = click vào cửa sổ nền. |
| `BackgroundInputMode` | string | `"PostMessage"` / `"SendInput"`. |

- **Ví dụ**: Click chuột trái tại (500, 300):
```json
"Properties": {
  "MouseButton": "Left",
  "ClickType": "Single",
  "TargetX": 500,
  "TargetY": 300,
  "MoveSpeed": 0,
  "RepeatCount": 1,
  "UseBackgroundMode": false
}
```

---

## 5. Screen Capture Node
- **Type**: `"ScreenCapture"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Chụp ảnh vùng màn hình hoặc cửa sổ. Xuất base64 image ra output cho node khác dùng (OCR, ImageProcessing...).
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `CaptureX` | int | Tọa độ X góc trái trên vùng chụp. |
| `CaptureY` | int | Tọa độ Y góc trái trên vùng chụp. |
| `CaptureWidth` | int | Chiều rộng vùng chụp (pixel). |
| `CaptureHeight` | int | Chiều cao vùng chụp (pixel). |
| `CaptureMode` | string | Chế độ: `"FullScreen"` (toàn màn hình), `"Region"` (vùng tùy chọn), `"Window"` (chụp cửa sổ), `"FromFile"` (đọc ảnh từ file). |
| `UseNativeWidth` | bool | `true` = giữ kích thước gốc trên canvas. |
| `MaxNodeWidth` | string | Chiều rộng tối đa hiển thị trên canvas (pixel). |
| `ImagePath` | string | *(Khi mode = FromFile)* Đường dẫn file ảnh. |
| `TargetProcessName` | string | *(Khi mode = Window)* Tên process cần chụp. |
| `TargetWindowTitle` | string | Tiêu đề cửa sổ cần chụp. |
| `UseBackgroundMode` | bool | `true` = chụp cửa sổ ẩn (không cần hiện trên màn hình). |
| `CoordSourceNodeId` | string | *(Tùy chọn)* ID node cung cấp tọa độ vùng chụp động. |
| `CoordSourceOutputKey` | string | *(Tùy chọn)* Output key chứa tọa độ. |

- **Ví dụ**: Chụp vùng màn hình:
```json
"Properties": {
  "CaptureX": 0,
  "CaptureY": 0,
  "CaptureWidth": 1920,
  "CaptureHeight": 1080,
  "CaptureMode": "FullScreen",
  "UseNativeWidth": true,
  "MaxNodeWidth": "360"
}
```

---

## 6. Macro Recorder Node
- **Type**: `"MacroRecorder"`
- **ColorKey**: `"SkyAzure"`
- **Ports**: 2 ports (Input Left + Output Right)
- **Chức năng**: Ghi lại chuỗi thao tác chuột/phím rồi phát lại (replay). Hỗ trợ lặp, đếm ngược, và chạy trên cửa sổ nền.
- **Mô tả các Properties**:

| Property | Kiểu | Mô tả chức năng |
|----------|------|-----------------|
| `OutputKey` | string | Key output chứa macro data (mặc định `"macroData"`). |
| `MacroDataJson` | string | JSON chứa dữ liệu macro đã ghi (chuỗi thao tác). |
| `PlaybackMode` | string | Chế độ phát: `"PlayOnce"` (1 lần), `"RepeatN"` (lặp N lần), `"RepeatForever"` (lặp mãi). |
| `RepeatIntervalMs` | int | Khoảng cách giữa các lần lặp (ms). |
| `RepeatCount` | int | Số lần lặp (khi mode = RepeatN). |
| `VisualPlaybackMode` | string | Hiệu ứng visual: `"Normal"` / `"SlowMotion"`. |
| `ShowMouseTrail` | bool | `true` = hiện đường di chuột khi phát lại. |
| `CountdownSeconds` | int | Số giây đếm ngược trước khi bắt đầu (0-10). |
| `ExecutionMode` | string | `"Foreground"` (cửa sổ trước) / `"Background"` (cửa sổ nền). |
| `TargetProcessName` | string | Process đích khi mode = Background. |

- **Ví dụ**: Phát macro 3 lần:
```json
"Properties": {
  "OutputKey": "macroData",
  "PlaybackMode": "RepeatN",
  "RepeatCount": 3,
  "RepeatIntervalMs": 500,
  "ShowMouseTrail": true,
  "CountdownSeconds": 3,
  "ExecutionMode": "Foreground"
}
```
