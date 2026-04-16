
# 📦 FlowMy – Cấu trúc dự án & thư mục

Repository này là **ứng dụng WPF Desktop (FlowMy)**, sử dụng mô hình **MVVM** với nhiều UserControl, theme động và hệ thống Dashboard.

---

## ✅ 1. Cấu trúc thư mục gốc

Thư mục gốc: `FlowMy/`

```text
FlowMy
│
├── App.xaml / App.xaml.cs           → Entry WPF, đăng ký resource & bootstrap
├── App.config, app.manifest        → Cấu hình ứng dụng
├── FlowMy.sln             → Solution file
├── FlowMy.csproj          → Project file chính
├── appsettings.json                → Cấu hình bổ sung (API, settings…)
├── AssemblyInfo.cs                 → Thông tin assembly
│
├── Assets/                         → Tài nguyên tĩnh
│   ├── Fonts/                      → Font (vd: `fa-solid-900.ttf`)
│   ├── IconPaths/                  → Định nghĩa/mapper đường dẫn icon (C#)
│   ├── Icons/                      → Các file icon `.svg`
│   └── Images/                     → Logo, ảnh nền, icon `.png`, `.ico`, `.svg`
│
├── Commands/                       → Command dùng trong MVVM
│   ├── AsyncCommand.cs
│   └── LoginCommand.cs
│
├── Controls/                       → UserControl tái sử dụng (DataGrid, Tree, Combo…)
│   ├── CheckBoxListViewUserControl.xaml(.cs)
│   ├── DataGridUserControl.xaml(.cs)
│   ├── DateTimePickerUserControl.xaml(.cs)
│   ├── MultiSelectComboBoxUserControl.xaml(.cs)
│   └── ... các control khác
│
├── Converters/                     → `IValueConverter` / `IMultiValueConverter`
│   ├── ColorThemeConverter.cs
│   ├── DateTimeToStringConverter.cs
│   └── ... các converter khác
│
├── Extensions/                     → Extension methods (Theme, Color, UserIdentity, VisualTree…)
│   ├── ColorThemeExtensions.cs
│   ├── ThemeExtensions.cs
│   └── ...
│
├── Helpers/                        → Helper tĩnh, logic dùng chung
│   ├── DynamicColorHelper.cs
│   ├── LoadingDotHelper.cs
│   ├── MessageBoxHelper.cs
│   └── ... (WindowHelper, EnumHelper, PaginationHelper, v.v.)
│
├── Models/                         → Model dữ liệu & DTO
│   ├── Dashboard/                  → Model riêng cho Dashboard
│   ├── ListBoxs/                   → Model cho các ListBox chuyên biệt
│   ├── PermissionSteps/            → Model phân quyền theo bước
│   ├── User.cs, Employee.cs, ...   → Các entity chính
│   └── OAuthTokenResponse.cs, TokenRequestParams.cs, ...
│
├── Services/                       → Service layer & DI
│   ├── Interfaces/                 → Interface cho service (DI)
│   ├── DynamicMenuService.cs       → Xử lý menu động
│   ├── ViewFactoryService.cs       → Tạo View theo ViewModel
│   ├── ViewCacheService.cs         → Cache view
│   ├── OptimizedViewFactoryService.cs
│   ├── RegisterAppToStartup.cs     → Đăng ký app chạy cùng Windows
│   ├── UpdateService.cs            → Kiểm tra/cập nhật phiên bản
│   └── ServiceCollectionExtensions.cs → Đăng ký DI cho services
│
├── ViewModels/                     → ViewModel cho toàn bộ ứng dụng
│   ├── Base/                       → `BaseViewModel`, base class chung
│   ├── Login/                      → `LoginViewModel.cs`
│   ├── Menus/                      → ViewModel cho từng menu / module
│   ├── Question/                   → ViewModel cho phần câu hỏi / quiz
│   ├── UpdateViewModel.cs
│   └── MainViewModel.cs
│
├── Views/                          → Toàn bộ giao diện XAML
│   ├── MainWindow.xaml(.cs)        → Shell chính
│   └── ... các xaml khác
│
├── Themes/                         → ResourceDictionary cho theme
│   ├── Base/                       → Style & resource cơ bản
│   ├── Controls/                   → Style cho từng control
│   ├── DarkTheme.xaml              → Theme tối
│   ├── LightTheme.xaml             → Theme sáng
│   └── README.md                   → Hướng dẫn thêm cho theme
│
├── Routers/
│   └── ManualViewMappings.cs       → Mapping giữa View và ViewModel (Giống kiểu link web)
│
├── Properties/
│   ├── Settings.settings           → User settings (bao gồm Theme, cấu hình…)
│   └── Settings.Designer.cs
│
├── Library/
│   └── Aspose.Cells.dll            → Thư viện bên thứ 3 (Excel)
│
├── IconResources.cs               → Mapping / quản lý icon ở mức code
├── FodyWeavers.xml, FodyWeavers.xsd → Cấu hình Fody (weaver, property changed…)
```

---

## ✅ 2. Vai trò các thư mục chính (tóm tắt)

- **Assets**: Chứa toàn bộ tài nguyên tĩnh (icon, images, fonts) dùng cho UI.
- **Commands**: Các command MVVM (đặc biệt là `AsyncCommand` và `LoginCommand`).
- **Controls**: Các `UserControl` custom, phục vụ reuse UI (datatable, tree, selector…).
- **Converters**: Chuyển đổi dữ liệu để binding (theme, visibility, format text, v.v.).
- **Extensions**: Extension methods cho theme, màu sắc, user identity, visual tree…
- **Helpers**: Tập trung logic tiện ích: màu động, messagebox, phân trang, version, window helper…
- **Models**: Toàn bộ lớp dữ liệu business (Dashboard, User, Permission, Quiz, Token…).
- **Services**: Service nghiệp vụ + hạ tầng (menu, view factory, update, startup, DI).
- **ViewModels**: Logic trình bày, bind trực tiếp vào `Views`.
- **Views**: Tất cả màn hình và layout XAML của ứng dụng.
- **Themes**: Định nghĩa giao diện, màu sắc, style và theme sáng/tối.

Nếu cần chi tiết sâu hơn về **Dashboard** và hệ thống **Theme Dynamic**, xem thêm file `DashBoard_README.md`.

---

## ✅ 3. Build & chạy dự án

- **Yêu cầu**:
  - Visual Studio (khuyến nghị 2022 trở lên).
  - .NET phù hợp với cấu hình trong `FlowMy.csproj`.
- **Cách chạy**:
  - Mở solution `FlowMy.sln`.
  - Set project `FlowMy` làm **Startup Project**.
  - Build và Run (`F5`).

---

## ✅ 4. Ghi chú đặt tên & quy ước

- **View**: đặt tên theo màn hình, ví dụ: `LoginView.xaml`, `DashboardView.xaml`, `MainWindow.xaml`.
- **ViewModel**: `TênView + ViewModel`, ví dụ: `LoginViewModel.cs`, `DashboardViewModel.cs`.
- **Model**: Theo domain, ví dụ: `User.cs`, `Employee.cs`, `QuizItem.cs`, `OAuthTokenResponse.cs`.
- **Service**: `XxxService.cs` và interface tương ứng `IXxxService.cs` (đặt trong `Services/Interfaces`).
- **Converter/Helper/Extension**: Đặt đúng hậu tố `Converter`, `Helper`, `Extensions` để dễ tìm kiếm.

---
