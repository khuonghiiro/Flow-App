# Core Flow Nodes (Nhóm Điều khiển Luồng Cơ bản)

Tài liệu này chứa cấu trúc JSON `Properties` và Logic thực thi của các Node liên quan đến rẽ nhánh, lặp, khởi tạo và kết thúc.

---

## 1. Start Node
- **Type**: `"Start"`
- **Chức năng**: Điểm mốc bắt đầu của Workflow. Không có input port.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "RunMode": "MainFlow",
  "EndBehavior": "StopCurrentFlow"
}
```

## 2. End Node
- **Type**: `"End"`
- **Chức năng**: Kết thúc nhánh hoặc toàn bộ luồng.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "EndBehavior": "StopCurrentFlow"
}
```

## 3. Delay Node
- **Type**: `"Delay"`
- **Chức năng**: Tạm dừng luồng thực thi.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "DelayValue": 5,
  "DelayUnit": "Seconds",
  "TimingMode": "None",
  "RandomMinValue": 0,
  "RandomMaxValue": 0,
  "DynIn_DelayValue_SrcNode": "123-abc",
  "DynIn_DelayValue_SrcKey": "delay_time"
}
```

## 4. Loop Node
- **Type**: `"Loop"`
- **Chức năng**: Vòng lặp. Truyền tín hiệu lặp xuống `LoopBodyNode`.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "LoopType": "RepeatN",
  "RepeatCount": 10,
  "StartIndex": 0,
  "EndIndex": 9,
  "ArrayInputKey": "array",
  "InputType": "Integer"
}
```

## 5. Loop Body (Generic Container)
- **Type**: `"BodyContainer"`
- **Chức năng**: Vùng chứa UI bao bọc các Node con bên trong vòng lặp. 
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "Width": 800.0,
  "Height": 600.0
}
```

## 6. Conditional Node
- **Type**: `"IfElse"`
- **Chức năng**: Rẽ nhánh `If / Else`.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "ConditionGroups": "[{\"Rules\":[{\"LeftValue\":\"A\",\"Operator\":\"Equals\",\"RightValue\":\"B\"}]}]"
}
```

## 7. AsyncTask Node
- **Type**: `"AsyncTask"`
- **Chức năng**: Khởi chạy đa luồng (Parallel).
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "RunInParallel": true,
  "UiPresentationMode": "ManualBranches",
  "RepeatCount": 0,
  "AsyncTaskBranches": "[{\"Id\":\"branch_1\",\"Label\":\"Task 1\",\"CanRemove\":true}]"
}
```

## 8. Break & Continue Node
- **Type**: `"Break"`, `"Continue"`
- **Chức năng**: Điều khiển ngắt vòng lặp.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "TargetLoopId": "node_loop_1" 
}
```
