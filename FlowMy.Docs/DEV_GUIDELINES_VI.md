# HƯỚNG DẪN QUY CHUẨN PHÂN TÁCH MÃ NGUỒN (DÀNH CHO LẬP TRÌNH VIÊN)

> **Mục tiêu**: Hướng dẫn đội ngũ lập trình viên cách tổ chức file, hàm và phân rã module để mã nguồn dự án `FlowMy` luôn sạch sẽ, dễ bảo trì, biên dịch nhanh và hỗ trợ AI làm việc hiệu quả nhất.

---

## 1. QUY ĐỊNH VỀ KÍCH THƯỚC FILE VÀ PHÂN TÁCH LOGIC

1. **Giới hạn số dòng**:
   - Khuyến nghị: **200 – 600 dòng / file**.
   - Cảnh báo: File vượt quá **1.000 dòng**.
   - Giới hạn đỏ: **Tuyệt đối không để file vượt quá 1.200 – 1.500 dòng**.
2. **Cách phân tách file khi đạt giới hạn**:
   - **Giao diện / Controls / ViewModels**: Dùng `partial class` và đặt tên file theo cụm tính năng:
     - `[TênClass].[NhómChứcNăng].cs` (Ví dụ: `WorkflowEditorWindow.CanvasEvents.cs`, `WebNode.DataExtraction.cs`).
   - **Logic xử lý / Nghiệp vụ**: Tách thành các lớp `Service` độc lập trong thư mục `Services/`.
   - **Hàm tiện ích**: Đưa vào `FlowMy.Core/Helpers/` hoặc `FlowMy.Core/Extensions/`.

---

## 2. QUY ĐỊNH VỀ THIẾT KẾ HÀM (METHODS)

1. **Độ dài hàm**:
   - Tối đa **50 – 80 dòng / hàm**.
   - Nếu hàm phải làm nhiều công đoạn, hãy chia nhỏ thành các hàm `private` phụ trợ:
     ```csharp
     public async Task ExecuteNodeAsync(...)
     {
         if (!ValidateInputs(...)) return;
         var data = await FetchDataAsync(...);
         ProcessData(data);
         UpdateUiState(...);
     }
     ```
2. **Nguyên tắc đơn nhiệm (Single Responsibility)**:
   - Mỗi hàm chỉ giải quyết duy nhất 1 nhiệm vụ.

---

## 3. KHỐI COMMENT CHUẨN Ở ĐẦU MỖI FILE MÃ NGUỒN

Tất cả các file mã nguồn C# (.cs) mới hoặc file con được phân tách cần đặt đoạn comment ngắn gọn sau ở ngay đầu file để điều hướng AI đọc tài liệu chuẩn:

```csharp
// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
```

---

## 4. AN TOÀN ĐA LUỒNG TRONG WPF

1. **Không gọi `Dispatcher.Invoke` đồng bộ từ Background Thread**:
   - Khi chạy trong các luồng nền (Task, CefSharp callbacks, Timer), không được gọi `Dispatcher.Invoke` chặn luồng vì sẽ gây đơ UI và nguy cơ Deadlock.
   - Sử dụng bộ nhớ đệm (Cached ViewModel/State) hoặc truyền Service Interface trực tiếp (`IScopedOutputSync`).
2. **Kích thước màn hình (Screen Metrics)**:
   - Dùng Win32 Native `GetSystemMetrics` thay vì gọi `SystemParameters.WorkArea` trong các constructor khởi tạo trên luồng nền.

---

## 5. QUY TẮC GIAO DIỆN XAML & HỆ THỐNG THEME

1. **Phân tách giao diện XAML (XAML Modularity)**:
   - File `.xaml` không được vượt quá **800 – 1.000 dòng**.
   - Đối với các Dialog phức tạp, chia nhỏ thành các `UserControl` độc lập hoặc tách nhỏ `ResourceDictionary`.
2. **Tuân thủ Theme Tokens**:
   - Tuyệt đối **không hardcode** màu (ví dụ `Background="#FF1A1B1E"`).
   - Luôn sử dụng `{DynamicResource TokenKey}` theo danh mục [THEME_TOKEN_REFERENCE.md](wpf-docs/THEME_TOKEN_REFERENCE.md).
3. **Quy tắc Button có Width/Height cố định (BẮT BUỘC Padding="0")**:
   - Khi tạo Button có Style và set `Width` / `Height` cố định (ví dụ nút icon 32x32, nút thao tác 80x28), **PHẢI đặt `Padding="0"`** để tránh padding mặc định của Style làm lệch tâm text/icon:
   ```xml
   <!-- ✅ ĐÚNG: Nút cố định kích thước -> Bắt buộc Padding="0" -->
   <Button Style="{DynamicResource PrimaryButton}" Width="32" Height="32" Padding="0">
       <controls:SvgViewboxEx Source="Assets/Icons/edit.svg" Width="14" Height="14"/>
   </Button>
   ```
4. **Thư mục lưu Custom Styles mới**:
   - Khi tạo style / template riêng cho component mới, đặt file `.xaml` vào thư mục: `FlowMy.Wpf-UI/Themes/Control_News/`.

---

## 6. TÀI LIỆU LIÊN QUAN

- [AI Coding Standards (English for AI)](AI_CODING_STANDARDS.md)
- [Theme Token Reference (Tra cứu DynamicResource)](wpf-docs/THEME_TOKEN_REFERENCE.md)
- [Cấu trúc Solution & Dự án](../README.md)
