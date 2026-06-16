# Data & Variables Nodes (Nhóm Xử lý Dữ liệu và Biến số)

Tài liệu này trình bày Logic thực thi và Cấu trúc JSON `Properties` cho các Node quản lý biến, bộ nhớ tạm.

---

## 1. Input Node
- **Type**: `"Input"`
- **Chức năng**: Khai báo biến đầu vào cho luồng.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "DataType": "String",
  "Value": "Hello World",
  "ArrayValues": "[\"Item 1\", \"Item 2\"]"
}
```

## 2. Output Node
- **Type**: `"Output"`
- **Chức năng**: Ghi nhận kết quả cuối cùng.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "DynIn_OutputValue_SrcNode": "123",
  "DynIn_OutputValue_SrcKey": "result_data"
}
```

## 3. Storage Node
- **Type**: `"Storage"`
- **Chức năng**: Lưu trữ dữ liệu hệ thống (Key-Value DB).
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "StorageKey": "global_token",
  "Action": "Set",
  "StorageValue": "abc_xyz",
  "DynIn_StorageValue_SrcNode": "123",
  "DynIn_StorageValue_SrcKey": "api_token"
}
```

## 4. List Out Node
- **Type**: `"ListOut"`
- **Chức năng**: Gom nhiều giá trị thành mảng.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "ListType": "String"
}
```

## 5. Assign Data Node
- **Type**: `"AssignData"`
- **Chức năng**: Đổi tên biến / Ánh xạ sang biến mới.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "SourceNodeId": "input_1",
  "SourceOutputKey": "value",
  "TargetKey": "username"
}
```

## 6. Key Value Bridge Node
- **Type**: `"KeyValueBridge"`
- **Chức năng**: Chuyển đổi dữ liệu thông qua Dictionary Map.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "DictionaryMap": "{\"True\":\"Thành công\", \"False\":\"Thất bại\"}",
  "DynIn_InputValue_SrcNode": "123",
  "DynIn_InputValue_SrcKey": "status"
}
```

## 7. String Split Node & Text Scan Node
- **Type**: `"StringSplit"`, `"TextScan"`
- **Chức năng**: Tách chuỗi và quét biểu thức chính quy.
- **Ví dụ JSON Properties**:
```json
// StringSplit
"Properties": {
  "Separator": ",",
  "DynIn_InputString_SrcNode": "123",
  "DynIn_InputString_SrcKey": "csv_row"
}

// TextScan
"Properties": {
  "RegexPattern": "[0-9]+",
  "DynIn_InputText_SrcNode": "123",
  "DynIn_InputText_SrcKey": "content"
}
```
