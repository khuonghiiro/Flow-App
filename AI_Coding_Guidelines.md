## Hướng dẫn cho AI khi sửa code trong dự án Auto_Click

File này dùng để định nghĩa **quy ước bắt buộc** khi AI sinh code / sửa code trong dự án này.

---

### 1. Kiến trúc tổng thể – luôn tuân theo MVVM

- **Pattern chính**: WPF + **MVVM**.
- **View (`.xaml`)**:
  - Chỉ chứa **UI layout + binding**.
  - Không viết **business logic** trong code-behind, trừ:
    - Xử lý sự kiện thuần UI (ví dụ: đóng popup, focus control, animation nhỏ…).
    - Chuyển sự kiện sang `ICommand` trong ViewModel.
- **ViewModel**:
  - Chứa state, command, xử lý logic.
  - Implement `INotifyPropertyChanged` đầy đủ, property nào được binding ra UI phải raise `PropertyChanged`.
- **Model / Service**:
  - Xử lý nghiệp vụ, truy cập dữ liệu, logic lâu dài.

Khi cần thêm tính năng mới:
- **Ưu tiên**: thêm vào ViewModel / Service, View chỉ binding.
- Tránh tạo thêm static global state không cần thiết.

---

### 2. Style, màu sắc – luôn lấy từ `Themes/`

- **Không hard-code màu** trong XAML hoặc C# (như `#FFFFFF`, `Colors.Red`…), trừ khi thực sự không thể tránh.
- Màu, brush, style phải:
  - Được khai báo trong các file trong thư mục `Themes/`:
    - `Themes/Base/`
    - `Themes/Controls/`
    - `Themes/LightTheme.xaml`
    - `Themes/DarkTheme.xaml`
  - Được sử dụng qua **`StaticResource` / `DynamicResource`**.  
    Ví dụ:
    - `Background="{DynamicResource PrimaryBackgroundBrush}"`
    - `Foreground="{DynamicResource TextBrush}"`

- Khi cần **thêm màu mới / style mới**:
  1. Định nghĩa `SolidColorBrush` / `Style` trong `Themes/Base/` (hoặc file resource phù hợp).
  2. Dùng `x:Key` rõ ràng, thống nhất (ví dụ: `PrimaryBackgroundBrush`, `AccentBrush`, `ErrorBrush`…).
  3. Trong `LightTheme.xaml` / `DarkTheme.xaml`, override hoặc map lại nếu cần.
  4. Trong XAML, chỉ sử dụng key đó, **không tạo brush cục bộ** trừ trường hợp đặc biệt.

---

### 3. Icon – luôn dựa trên `IconResources` + `SvgViewboxEx`

#### 3.1. Nguồn icon

- **TẤT CẢ icon SVG** phải lấy từ:
  - File `IconResources.cs` (dictionary `AvailableIcons`).
  - Thư mục vật lý `Assets/Icons/`.
- **Không tự ý đặt tên icon mới** trong code:
  - Nếu cần icon mới: thêm file SVG vào `Assets/Icons/` và đăng ký key trong `IconResources.AvailableIcons`.

#### 3.2. Cách lấy icon

- Dùng các hàm/metadata sẵn có trong `IconResources`:
  - `IconResources.AvailableIcons` – danh sách key → path.
  - `IconResources.GetIconPath(string iconName)` – trả về path tương đối của icon.
  - `IconResources.IconExists(string iconName)` – check tồn tại.
  - `IconResources.GetSvgImage(string iconName)` – trả về `DrawingImage` có cache.

Khi cần hiển thị icon từ **text key**:
- **Trong code C#** (ví dụ Binding sang `ImageSource`):
  - Dùng `IconResources.GetSvgImage(iconKey)` hoặc `GetIconPath(iconKey)` tùy scenario.
- **Trong XAML**, nếu icon gắn với control chọn icon:
  - Sử dụng `IconSelectorUserControl` + `SelectedIcon` (là key string của icon).

#### 3.3. Sử dụng `SvgViewboxEx`

- **Luôn dùng `SvgViewboxEx`** (trong `Controls/IconSelectorUserControl.xaml.cs`) để hiển thị SVG, **không tự tạo control SVG khác** nếu không cần thiết.
- `SvgViewboxEx` đã hỗ trợ:
  - DependencyProperty `Fill` và `Stroke`.
  - Tự động bind `Fill` từ `Foreground` của parent khi chưa gán (trong `OnVisualParentChanged`).
  - Tự apply màu vào `GeometryDrawing`.

Ví dụ cách tạo button icon (C# – như trong `IconSelectorUserControl`):

```csharp
var svgViewbox = new SvgViewboxEx
{
    Source = new Uri(iconPath, UriKind.RelativeOrAbsolute),
    Width = 18,
    Height = 18
    // Fill sẽ tự bind từ Foreground của Button nếu chưa set
};
```

- **Không** tạo hard-code icon SVG path mới ngoài `IconResources`.
- **Không** dùng string path trực tiếp khi đã có key trong `IconResources`; luôn ưu tiên:
  - Lưu key (vd: `"home"`, `"user"`, `"settings"`), sau đó tra path qua `IconResources`.

---

### 4. Quy ước khi thêm / sửa UI liên quan icon

- Nếu UI cho phép người dùng **chọn icon**:
  - Dùng `IconSelectorUserControl` và binding `SelectedIcon` (TwoWay) sang ViewModel (property string).
  - Trong ViewModel / Model, luôn lưu **key icon** (string), không lưu path.
- Nếu UI **chỉ hiển thị icon tĩnh**:
  - Lưu key icon ở ViewModel hoặc static config.
  - Lấy `DrawingImage` / path thông qua `IconResources`.
  - Dùng `SvgViewboxEx` để hiển thị, cho phép đổi màu theo theme.

---

### 5. Quy ước chung khác

- **Naming – View / ViewModel / UserControl**:
  - **ViewModel**: `{Name}ViewModel`  
    - Ví dụ: `MainViewModel`, `SettingsViewModel`, `UserProfileViewModel`.
  - **Window (View dạng Window)**: `{Name}Window`  
    - Ví dụ: `MainWindow`, `SettingsWindow`, `UserProfileWindow`.
  - **UserControl (View dạng control)**: `{Name}View`  
    - Ví dụ: `UserListView`, `DashboardView`, `IconSelectorView`.
- **Naming – chung**:
  - Class: `PascalCase`.
  - Property / Method: `PascalCase`.
  - Field private: `_camelCase` hoặc `camelCase` (theo style hiện tại của file / class).
  - Không đổi style naming hiện tại của file trừ khi refactor toàn bộ.
- **Binding**:
  - Ưu tiên `Binding` với `Mode` rõ ràng khi cần (`TwoWay`, `OneWay`…), đặc biệt với DependencyProperty.
  - Khi Update từ code-behind, dùng `BindingOperations.GetBindingExpression(...).UpdateSource()` như đã làm trong `IconSelectorUserControl`.

---

### 6. Tóm tắt yêu cầu bắt buộc cho AI

- **Luôn**:
  - Tuân thủ MVVM: logic → ViewModel/Service, View chỉ binding.
  - Dùng resource từ `Themes/` cho style & color, không hard-code màu.
  - Dùng icon từ `IconResources` + `SvgViewboxEx`, key icon là string trong `IconResources.AvailableIcons`.
- **Không bao giờ**:
  - Tự tạo icon mới bằng tên ngẫu nhiên không có trong `IconResources`.
  - Hard-code đường dẫn SVG nếu đã có trong `IconResources`.
  - Chèn logic nghiệp vụ phức tạp vào code-behind của View.

Mọi thay đổi / sinh code mới cho dự án **phải tuân thủ các quy tắc trong file này**.


