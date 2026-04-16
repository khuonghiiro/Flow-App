# Ví dụ workflow — ExecutionId / scoped outputs

File: `workflow_scoped_demo.json`

## Logic minh họa

1. **`n_in` (Input)**  
   - Không nằm trên dây flow (không có port IN) — giá trị lấy từ `InputValue` đã lưu trong file.  
   - Output key = `message` (theo `InputKey`).

2. **`n_assign` (AssignData)**  
   - Chạy sau **Start** → copy `n_in.message` vào **`n_store.bucket`** (`StoredOutputs`).  
   - Runtime gọi `PublishStorageOutputsToScoped` → downstream trong **cùng một lần chạy** đọc đúng snapshot theo `ExecutionId`.

3. **`n_out` (Output)**  
   - `InputVariables`: biến `show` lấy từ node `n_store`, key `bucket`.  
   - Executor dùng `ResolveDynamicValueForExecution` → ưu tiên scoped, đúng khi nhiều run song song.

4. **`n_store` (Storage)**  
   - Chỉ xuất hiện trên graph để Assign ghi và Output đọc; **không** bắt buộc nối dây flow vào Storage cho ví dụ này.

## Cách dùng trong app

1. Copy file vào thư mục workflow của app (thường `Workflows` cạnh executable), **hoặc** dùng chức năng **Import** nếu có.  
2. Đặt tên file trùng `Name` trong JSON (ví dụ `ScopedDemo_InputAssignStorageOutput.json`) nếu app load theo tên file.  
3. Chạy workflow: Output nên hiển thị dạng `Scoped: Hello from scoped run`.  
4. Sửa `InputValue` trong JSON rồi chạy lại để thấy giá trị đổi theo từng lần (vẫn qua scoped khi có nhiều run).

## Lưu ý

- **`ExecutionId` không nằm trong JSON** — app sinh mỗi lần bấm chạy; scoped là **runtime**, không phải trường lưu file.  
- JSON này đúng cấu trúc `WorkflowDto` / `NodeDto` / `ConnectionDto` (xem `Models/Persistence/WorkflowPersistenceModels.cs` và `FileWorkflowPersistenceService`).

## Tài liệu liên quan

- `docs/AI_NODE_FLOW_GUIDE.md` §4.1 — ExecutionId & scoped outputs.
