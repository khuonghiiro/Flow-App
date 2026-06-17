# User Interaction Nodes (Nhóm Tương tác Màn hình, Chuột, Phím)

Tài liệu này trình bày Logic thực thi và Cấu trúc JSON `Properties` cho các Node mô phỏng thao tác của con người.

---

## 1. Screen Position Picker Node
- **Type**: `"ScreenPosition"`
- **Chức năng**: Chọn tọa độ X, Y trên màn hình và click/cuộn chuột.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "X_Pos": 1920.0,
  "Y_Pos": 1080.0,
  "MouseAction": "LeftClick",
  "ClickCount": 1,
  "HoldDurationMs": 0,
  "ScrollCount": 0,
  "ScrollIntervalMs": 100,
  "DynIn_X_Pos_SrcNode": "123",
  "DynIn_X_Pos_SrcKey": "target_x"
}
```

## 2. Key Press Event Node
- **Type**: `"KeyPressEvent"`
- **Chức năng**: Giả lập gõ phím.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "Key": "Enter",
  "PressDelayMs": 50,
  "RepeatCount": 1
}
```

## 3. Hotkey Press Event Node
- **Type**: `"HotkeyPressEvent"`
- **Chức năng**: Gửi tổ hợp phím (vd Ctrl + C).
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "ModifierKey": "Control",
  "MainKey": "C"
}
```

## 4. Mouse Event Node
- **Type**: `"MouseEvent"`
- **Chức năng**: Gửi sự kiện chuột tĩnh tại vị trí hiện tại của con trỏ.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "MouseEvent": "MiddleClick"
}
```

## 5. Screen Capture Node
- **Type**: `"ScreenCapture"`
- **Chức năng**: Chụp ảnh màn hình trong toạ độ chỉ định.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "X": 100.0,
  "Y": 200.0,
  "Width": 500.0,
  "Height": 400.0,
  "SaveFormat": "Png"
}
```

## 6. Macro Recorder Node
- **Type**: `"MacroRecorder"`
- **Chức năng**: Ghi lại chuỗi thao tác và phát lại.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "PlaybackSpeed": 1.0,
  "RecordedActions": "[{\"Type\":\"MouseMove\",\"X\":10,\"Y\":20,\"TimeOffsetMs\":100}]"
}
```
