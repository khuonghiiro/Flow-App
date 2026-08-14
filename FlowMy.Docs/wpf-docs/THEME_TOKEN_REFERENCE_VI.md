# 🎨 FlowMy Theme Token Reference — Cheat Sheet (Tiếng Việt)

> **Mục đích**: Tài liệu tra cứu nhanh tất cả DynamicResource keys. Khi sửa/thêm UI mới, **chỉ dùng các key trong tài liệu này** để đảm bảo giao diện hoạt động đúng trên tất cả 8 themes.

> ⚠️ **QUY TẮC BẮT BUỘC KHI TẠO/SỬA UI XAML**:
> 1. **KHÔNG BAO GIỜ** hardcode màu trực tiếp trong XAML (ví dụ: `Background="#FF1A1B1E"`).
> 2. **LUÔN** dùng `{DynamicResource KeyName}` để theme tự động thay đổi mượt mà.
> 3. **Custom Styles Mới**: Nếu cần tạo Style riêng cho component/view mới, hãy đặt file `.xaml` vào thư mục `FlowMy.Wpf-UI/Themes/Control_News/`.
> 4. **Quy tắc Button có Width/Height (BẮT BUỘC Padding="0")**: Khi Button có set `Width` và/hoặc `Height` cố định (ví dụ nút icon 32x32, 28x28...), **PHẢI đặt `Padding="0"`** để tránh việc padding mặc định trong Style làm lệch icon hoặc text.
> 5. **Giới hạn số dòng XAML (XAML Modularity)**: Mỗi file `.xaml` không được vượt quá **800 – 1.000 dòng**. Khi giao diện phức tạp, **PHẢI** phân tách thành các `UserControl` nhỏ hoặc chia nhỏ `ResourceDictionary` con.

---

## 📁 Cấu trúc Theme Files

```
FlowMy.Wpf-UI/Themes/
├── LightTheme.xaml           ← ☀️ Light
├── DarkTheme.xaml            ← 🌙 Dark
├── SoftLightTheme.xaml       ← 🌿 Soft Light
├── SoftDarkTheme.xaml        ← 🌑 Soft Dark
├── DraculaTheme.xaml         ← 🧛 Dracula
├── MonokaiTheme.xaml         ← 🎨 Monokai
├── NightTheme.xaml           ← 🌌 Night
├── ModernTheme.xaml          ← ✨ Modern
│
├── Base/
│   ├── SemanticTokens.xaml   ← ⭐ TOKENS CHÍNH (override bởi mỗi theme)
│   ├── Colors/
│   │   ├── Common.xaml       ← Màu cố định (không đổi theo theme)
│   │   ├── Dark.xaml         ← Override tokens cho Dark
│   │   ├── Light.xaml        ← Override tokens cho Light
│   │   └── ...
│   ├── Fonts.xaml
│   └── Animations.xaml
│
├── Controls/                 ← Styles chuẩn hệ thống
│   ├── Buttons.xaml
│   ├── ComboBoxes.xaml
│   ├── DataGrids.xaml
│   ├── Inputs.xaml
│   └── ...
│
└── Control_News/             ← ⭐ THƯ MỤC CHỨA CÁC CUSTOM STYLES MỚI
    ├── MyCustomComponentStyles.xaml
    └── ...
```

---

## 🔘 Button Styles & Quy tắc Padding

| Style Key | Dùng cho |
|-----------|----------|
| `PrimaryButton` | Action chính (Save, OK) |
| `SecondaryButton` | Action phụ (Cancel) |
| `SuccessButton` | Action thành công |
| `DangerButton` | Action nguy hiểm (Delete) |
| `WarningButton` | Action cảnh báo |
| `InfoButton` | Action thông tin |
| `TransparentButtonStyle` | Icon-only toolbar button |
| `PrimaryShadowButton` | Primary flat |
| `DeleteRowButton` | Nút xóa row DataGrid |
| `ModernIconButtonStyle` | Icon button hiện đại |

### ⚠️ Quy tắc Padding khi dùng Width/Height cố định:
```xml
<!-- ✅ ĐÚNG: Button có Width/Height cố định -> BẮT BUỘC đặt Padding="0" -->
<Button Style="{DynamicResource PrimaryButton}" Width="32" Height="32" Padding="0">
    <controls:SvgViewboxEx Source="Assets/Icons/edit.svg" Width="14" Height="14"/>
</Button>

<!-- ❌ SAI: Button có Width/Height nhưng để padding mặc định làm lệch icon/chữ -->
<Button Style="{DynamicResource PrimaryButton}" Width="32" Height="32">
    <controls:SvgViewboxEx Source="Assets/Icons/edit.svg" Width="14" Height="14"/>
</Button>

<!-- ✅ ĐÚNG: Button co giãn theo nội dung text -> Dùng Padding mặc định hoặc tùy chỉnh -->
<Button Content="Lưu Dữ Liệu" Style="{DynamicResource PrimaryButton}" Padding="16,8"/>
```

---

## 🏗️ Surface / Window / Card

| Key | Vai trò | Khi nào dùng |
|-----|---------|-------------|
| `WindowBackgroundBrush` | Nền cửa sổ chính | `Window.Background` |
| `WindowBodyBackground` | Nền body/panel chính | Panel content area |
| `HeaderBackgroundBrush` | Nền header/footer bar | Top bar, bottom bar |
| `HeaderBackgroundMainBrush` | Nền header editor chính | Toolbar chính (cố định) |
| `CardBackgroundBrush` | Nền card | Card, dialog section |
| `CardColor` | Nền card (alias) | Tương đương `CardBackgroundBrush` |
| `CardHoverBackground` | Nền card khi hover | Card hover state |
| `SurfaceColor` | Nền surface layer | Tab panel, group, badge bg |
| `ControlBackground` | Nền control chung | Misc control bg |
| `BackgroundAll` | Nền deepest layer | Nền toàn bộ window |

---

## 📝 Text Tokens

| Key | Vai trò | Khi nào dùng |
|-----|---------|-------------|
| `TextPrimary` | Heading/title, đậm nhất | Tiêu đề |
| `TextBrush` | Text body chính | Nội dung |
| `TextSecondary` | Text phụ, nhạt hơn | Subtitle |
| `TextMuted` | Text rất nhạt | Hint, note |
| `TextDisabled` | Text disabled | Control tắt |
| `TextNoteBrush` | Text ghi chú | Annotation |
| `PlaceholderBrush` | Placeholder | TextBox placeholder |

---

## 🎯 Semantic Colors

Mỗi color có: `{Name}Brush` → `{Name}HoverBrush` → `{Name}PressedBrush` + `TextOn{Name}Brush`
- **Primary (Blue)**: `PrimaryBrush`, `PrimaryHoverBrush`, `PrimaryPressedBrush`, `TextOnPrimaryBrush`
- **Secondary (Gray)**: `SecondaryBrush`, `SecondaryHoverBrush`, `SecondaryPressedBrush`, `TextOnSecondaryBrush`
- **Success (Green)**: `SuccessBrush`, `SuccessHoverBrush`, `SuccessPressedBrush`, `TextOnSuccessBrush`
- **Danger (Red)**: `DangerBrush`, `DangerHoverBrush`, `DangerPressedBrush`, `TextOnDangerBrush`
- **Warning (Yellow)**: `WarningBrush`, `WarningHoverBrush`, `WarningPressedBrush`, `TextOnWarningBrush`
- **Info (Cyan)**: `InfoBrush`, `InfoHoverBrush`, `InfoPressedBrush`, `TextOnInfoBrush`
- **Transparent**: `TransparentBrush`, `TransparentHoverBrush`, `TransparentPressedBrush`
