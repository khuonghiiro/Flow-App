# Utilities Nodes (Nhóm Tiện ích Mở rộng)

Tài liệu này trình bày Logic thực thi và Cấu trúc JSON `Properties` cho các Node tiện ích lập trình.

---

## 1. Code Node
- **Type**: `"Code"`
- **Chức năng**: Biên dịch (compile) mã C# trực tiếp lúc chạy thông qua Roslyn.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "Width": 600.0,
  "Height": 400.0,
  "CodeContent": "using System;\n\npublic class NodeScript\n{\n    public object Execute(object[] args)\n    {\n        return \"Hello \" + args[0];\n    }\n}",
  "IsAsync": false,
  "InputMappings": "[{\"SourceNodeId\":\"123\", \"SourceOutputKey\":\"name\"}]"
}
```

## 2. Folder & File Paths Node
- **Type**: `"FolderFilePaths"`
- **Chức năng**: Quét thư mục và lấy danh sách file.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "DirectoryPath": "C:\\Data",
  "FileExtensionFilter": "*.png|*.jpg",
  "SearchOption": "TopDirectoryOnly"
}
```

## 3. Data Fetcher Node
- **Type**: `"DataFetcher"`
- **Chức năng**: Đọc dữ liệu từ SQLite hoặc local file.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "FetchTarget": "Database",
  "FetchQuery": "SELECT * FROM Users",
  "DynIn_FetchQuery_SrcNode": "123",
  "DynIn_FetchQuery_SrcKey": "sql_string"
}
```

## 4. Git Source Node
- **Type**: `"GitSource"`
- **Chức năng**: Clone source code từ Git.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "RepoUrl": "https://github.com/user/repo.git",
  "Branch": "main",
  "TargetFolder": "C:\\SourceCode"
}
```

## 5. Flow Overwrite Node
- **Type**: `"FlowOverwrite"`
- **Chức năng**: Ghi đè file JSON của Workflow (Self-modifying flow).
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "TargetWorkflowId": "wf_12345",
  "NewJsonPayload": "{\"Name\":\"Updated\"}",
  "DynIn_NewJsonPayload_SrcNode": "code_node",
  "DynIn_NewJsonPayload_SrcKey": "new_json_str"
}
```

## 6. Notification Node
- **Type**: `"Notification"`
- **Chức năng**: Hiện popup Toast Notification ở Windows.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "Title": "Hoàn tất",
  "Message": "Công việc đã xong",
  "IconType": "Info",
  "DynIn_Message_SrcNode": "123",
  "DynIn_Message_SrcKey": "log_text"
}
```

## 7. Border Highlight Node
- **Type**: `"BorderHighlight"`
- **Chức năng**: Nháy viền UI của 1 node đang có trên màn hình Canvas.
- **Ví dụ JSON Properties**:
```json
"Properties": {
  "TargetNodeId": "Node_Web_1",
  "BorderColor": "#FF0000"
}
```
