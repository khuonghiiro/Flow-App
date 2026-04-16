# 🎨 Themes — Hệ thống Theme của FlowMy

## Tổng quan

Hệ thống theme được tổ chức theo kiến trúc phân lớp, dễ bảo trì và mở rộng:

```
Themes/
├── LightTheme.xaml          ← Light theme (Windows 11 light)
├── DarkTheme.xaml           ← Dark theme (Windows 11 dark)
├── DraculaTheme.xaml        ← Dracula theme (purple vampire)
├── ModernTheme.xaml         ← Modern theme 
├── MonokaiTheme.xaml        ← Monokai theme (editor classic)
├── NightTheme.xaml          ← Night theme (Nord-inspired)
│
├── Base/
│   ├── Colors/
│   │   ├── Common.xaml      ← Màu dùng chung (không đổi theo theme)
│   │   ├── Light.xaml       ← Màu cho Light theme
│   │   ├── Dark.xaml        ← Màu cho Dark theme
│   │   ├── Dracula.xaml     ← Màu cho Dracula theme
│   │   ├── Modern.xaml      ← Màu cho Modern theme
│   │   ├── Monokai.xaml     ← Màu cho Monokai theme
│   │   └── Night.xaml       ← Màu cho Night theme
│   ├── Fonts.xaml           ← Font families và sizes
│   ├── Animations.xaml      ← Storyboards và transitions
│   └── Common.xaml          ← Base styles, shadow effects
│
└── Controls/
    ├── Borders.xaml         ← Border styles
    ├── Buttons.xaml         ← Button styles (CornerRadius=6)
    ├── Calendars.xaml
    ├── CheckBoxes.xaml
    ├── ComboBoxes.xaml
    ├── DataGrids.xaml
    ├── DatePickers.xaml
    ├── Inputs.xaml
    ├── ListBoxes.xaml
    ├── Permissions.xaml
    ├── RadioOptions.xaml
    ├── ScrollBars.xaml
    ├── TabControls.xaml
    └── Tooltips.xaml
```

---

## Cách hoạt động

Mỗi `*Theme.xaml` (root file) merge theo thứ tự:

1. `Colors/Common.xaml` — màu không đổi giữa themes
2. `Colors/<ThemeName>.xaml` — màu đặc trưng của theme đó
3. `Base/Fonts.xaml`, `Base/Animations.xaml`, `Base/Common.xaml`
4. Toàn bộ `Controls/*.xaml`

Tất cả Controls styles dùng `{DynamicResource ...}` để tự động nhận màu từ active theme.

---

## Các Theme hiện có

| Theme     | Tông màu                          | Đặc trưng                               |
|-----------|-----------------------------------|-----------------------------------------|
| **Light** | Windows 11 light (F5F5F5 + White) | Off-white bg, subtle borders, W11 blue  |
| **Dark**  | Windows 11 dark (1A1B1E + grey)   | Deep dark, layered surfaces, blue accent|
| **Dracula**| Purple dark (#282A36)            | Vampire aesthetic, purple/pink/green    |
| **Monokai**| Warm dark (#272822)             | Code editor classic, green/orange/red   |
| **Night** | Nord blue-grey (#2E3440)          | Cool arctic, blue-grey layered          |

---

## Color Keys chuẩn (DynamicResource)

Controls nên dùng các key sau để tự adapt theo theme:

### Primary Semantic
| Key | Mô tả |
|-----|-------|
| `WindowBackgroundBrush` | Nền cửa sổ chính |
| `WindowBodyBackground` | Nền panel/body |
| `CardColor` | Nền card/panel |
| `SurfaceColor` | Surface cho TabControl, group box |
| `BorderColor` | Màu viền controls |
| `TextBrush` | Màu text chính |
| `TextPrimary` | Text đậm/heading |
| `TextSecondary` | Text phụ |

### Action Colors  
| Key | Mô tả |
|-----|-------|
| `PrimaryBrush` | Màu primary action |
| `PrimaryHoverBrush` | Hover state |
| `PrimaryPressedBrush` | Pressed state |
| `TextOnPrimaryBrush` | Text trên primary bg |
| `SuccessBrush` / `DangerBrush` / `WarningBrush` / `InfoBrush` | Semantic colors |

### Input Controls
| Key | Mô tả |
|-----|-------|
| `TextBoxBackground` | Nền input, textbox |
| `ControlBorderBrush` | Viền input |
| `TextBoxFocusBorderBrush` | Viền khi focus |
| `PlaceholderBrush` | Màu placeholder text |

---

## Thêm theme mới

1. Tạo `Base/Colors/<NewName>.xaml` - copy cấu trúc từ `Dracula.xaml` và thay màu
2. Tạo `<NewName>Theme.xaml` - copy `DraculaTheme.xaml`, đổi source Colors file
3. Đăng ký trong `ColorThemeService.AvailableThemes` dictionary
4. Thêm `ComboBoxItem` vào `ThemeSelector` trong `WorkflowEditorWindow.xaml`

---

## Design Decisions

- **CornerRadius = 6** cho tất cả buttons (Windows 11 standard)
- **PrimaryShadowButton** flat (không shadow) để tinh tế hơn
- **TabItem** dùng underline style `6,6,0,0` CornerRadius
- **ModernTabControl** dùng `12` CornerRadius cho card container
- **ListView** headers dùng `{DynamicResource TextPrimary}` thay vì hardcoded màu
- Tất cả màu trong `Controls/TabControls.xaml` đều dùng `DynamicResource`
