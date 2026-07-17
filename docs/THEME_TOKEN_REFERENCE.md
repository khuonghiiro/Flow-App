# 🎨 FlowMy Theme Token Reference — Cheat Sheet

> **Mục đích**: Tài liệu tra cứu nhanh tất cả DynamicResource keys. Khi sửa/thêm UI mới, **chỉ dùng các key trong tài liệu này** để đảm bảo giao diện hoạt động đúng trên tất cả 8 themes.

> **KHÔNG BAO GIỜ** hardcode màu trực tiếp trong XAML (ví dụ `Background="#FF1A1B1E"`).
> **LUÔN** dùng `{DynamicResource KeyName}` để theme tự adapt.

---

## 📁 Cấu trúc Theme Files

```
Themes/
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
└── Controls/
    ├── Buttons.xaml           ← Button styles
    ├── ComboBoxes.xaml
    ├── DataGrids.xaml
    ├── Inputs.xaml
    └── ...
```

**Load order**: `Common.xaml` → `SemanticTokens.xaml` → `Colors/{Theme}.xaml` → Controls

**QUAN TRỌNG**: Theme-specific files (`Dark.xaml`, `Light.xaml`...) **override** giá trị trong `SemanticTokens.xaml`.
Khi thêm key mới, phải thêm vào **cả SemanticTokens.xaml** (default) **và tất cả theme color files**.

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

```xml
<!-- ✅ ĐÚNG -->
<Border Background="{DynamicResource CardBackgroundBrush}">

<!-- ❌ SAI -->
<Border Background="#FF141720">
```

---

## 📝 Text

| Key | Vai trò | Khi nào dùng |
|-----|---------|-------------|
| `TextPrimary` | Heading/title, đậm nhất | Tiêu đề |
| `TextBrush` | Text body chính | Nội dung |
| `TextSecondary` | Text phụ, nhạt hơn | Subtitle |
| `TextMuted` | Text rất nhạt | Hint, note |
| `TextDisabled` | Text disabled | Control tắt |
| `TextNoteBrush` | Text ghi chú | Annotation |
| `TextBrushReverse` | Đảo ngược (sáng→tối) | Text trên nền sáng |
| `PlaceholderBrush` | Placeholder | TextBox placeholder |

```xml
<TextBlock Foreground="{DynamicResource TextPrimary}" FontWeight="Bold"/>   <!-- Heading -->
<TextBlock Foreground="{DynamicResource TextBrush}"/>                       <!-- Body -->
<TextBlock Foreground="{DynamicResource TextSecondary}"/>                   <!-- Subtitle -->
<TextBlock Foreground="{DynamicResource TextMuted}"/>                       <!-- Hint -->
```

---

## 🔲 Border

| Key | Vai trò |
|-----|---------|
| `BorderColor` | Viền chung, nhẹ |
| `ControlBorderBrush` | Viền control (đậm hơn) |
| `BorderBrush` | Alias `ControlBorderBrush` |
| `BorderInverseColor` | Viền đảo ngược |

---

## 🎯 Semantic Colors

Mỗi color có: `{Name}Brush` → `{Name}HoverBrush` → `{Name}PressedBrush` + `TextOn{Name}Brush`

### Primary (Blue)
| `PrimaryBrush` | `PrimaryHoverBrush` | `PrimaryPressedBrush` | `TextOnPrimaryBrush` | `PrimaryGlowBrush` |

### Secondary (Gray)
| `SecondaryBrush` | `SecondaryHoverBrush` | `SecondaryPressedBrush` | `TextOnSecondaryBrush` |

### Success (Green)
| `SuccessBrush` | `SuccessHoverBrush` | `SuccessPressedBrush` | `TextOnSuccessBrush` |

### Danger (Red)
| `DangerBrush` | `DangerHoverBrush` | `DangerPressedBrush` | `TextOnDangerBrush` |

### Warning (Yellow)
| `WarningBrush` | `WarningHoverBrush` | `WarningPressedBrush` | `TextOnWarningBrush` |

### Info (Cyan)
| `InfoBrush` | `InfoHoverBrush` | `InfoPressedBrush` | `TextOnInfoBrush` |

### Accent (Violet)
| `AccentBrush` / `AccentColor` |

### Transparent
| `TransparentBrush` | `TransparentHoverBrush` (15% opacity of TextBrush) | `TransparentPressedBrush` (30% opacity of TextBrush) | `TransparentFocusBrush` (30% opacity of TextBrush) | `TextOnTransparentBrush` (TextBrush) |

---

## 🔘 Button Styles (có sẵn)

**Không cần tự viết ControlTemplate cho button thông thường!**

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

```xml
<Button Content="Lưu" Style="{DynamicResource PrimaryButton}" Padding="16,8"/>
<Button Content="Xóa" Style="{DynamicResource DangerButton}" Padding="16,8"/>
```

### Button Tokens (khi custom):
| `ButtonBackgroundBrush` | `ButtonHoverBrush` | `ButtonPressedBrush` | `ButtonBorderBrush` | `ButtonForegroundBrush` |

---

## 📥 Input / TextBox

| Key | Vai trò |
|-----|---------|
| `TextBoxBackground` | Nền |
| `TextBoxHoverBackground` | Hover |
| `TextBoxFocusBackground` | Focus |
| `TextBoxHoverBorderBrush` | Viền hover |
| `TextBoxFocusBorderBrush` | Viền focus (blue) |
| `TextBoxDisabledBackground` | Disabled |
| `TextBoxReadOnlyBackground` | Read-only |

---

## 🔽 ComboBox

Style: `{DynamicResource BaseComboBox}`

Tokens: `ComboBoxFieldBackground`, `ComboBoxFieldBorderBrush`, `ComboBoxFieldHoverBackground`, `ComboBoxPopupBackground`, `ComboBoxItemHoverBrush`, `ComboBoxItemSelectedBrush`, `ComboBoxGlyphBrush`

---

## 📊 DataGrid / ListView

| Key | Vai trò |
|-----|---------|
| `DataGridHeaderBackground` | Header nền |
| `DataGridHeaderBrush` | Header text |
| `DataGridRowBackground` | Row chẵn |
| `DataGridAltRowBackground` | Row lẻ |
| `DataGridRowHoverBackground` | Row hover |
| `DataGridSelectionBackground` | Row selected |
| `DataGridSelectedTextBrush` | Text selected |
| `ListItemHoverBackground` | List hover |
| `ListItemSelectedBackground` | List selected |

---

## 🎭 Hover / Interaction

| Key | Vai trò |
|-----|---------|
| `HoverBackground` | Hover chung (semi-transparent) |
| `HoverGlowBrush` | Glow hover |
| `FocusRingBrush` | Focus indicator |
| `SelectionBrush` | Selection bg |

---

## 🌑 Shadow (`Color` type)

| Key | Vai trò |
|-----|---------|
| `ShadowColor` | DropShadow chung |
| `PrimaryShadowColor` | Shadow primary |
| `NodeShadowColor` | Shadow nodes |

```xml
<DropShadowEffect Color="{DynamicResource ShadowColor}" Direction="270" ShadowDepth="1" BlurRadius="4" Opacity="0.12"/>
```

---

## 📋 Scrollbar / Tab / Checkbox

**Scrollbar**: `ScrollBarThumbBrush`, `ScrollBarThumbHoverBrush`, `ScrollBarThumbPressedBrush`

**Tab**: `TabItemBackground`, `TabItemHoverBackground`, `TabItemSelectedBackground`, `TabItemSelectedBorderBrush`, `TabIndicatorBrush`

**Checkbox**: `CheckBoxBorderBrush`, `CheckBoxCheckedBackground`, `CheckBoxCheckMarkBrush`

**Progress**: `ProgressBackground`, `ProgressFill`

---

## ⚡ Quick Recipes

### Card with hover
```xml
<Border Background="{DynamicResource CardBackgroundBrush}"
        BorderBrush="{DynamicResource ControlBorderBrush}"
        BorderThickness="1" CornerRadius="10" Padding="14">
    <Border.Style>
        <Style TargetType="Border">
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="{DynamicResource CardHoverBackground}"/>
                </Trigger>
            </Style.Triggers>
        </Style>
    </Border.Style>
</Border>
```

### Badge / Pill
```xml
<Border Background="{DynamicResource SurfaceColor}" CornerRadius="5" Padding="8,3">
    <TextBlock Text="Label" FontSize="10.5" Foreground="{DynamicResource TextSecondary}"/>
</Border>
```

### Count badge
```xml
<Border Background="{DynamicResource PrimaryGlowBrush}" CornerRadius="10" Padding="10,4">
    <TextBlock Text="5" FontWeight="SemiBold" Foreground="{DynamicResource PrimaryBrush}"/>
</Border>
```

### Icon button with hover
```xml
<Button Cursor="Hand">
    <Button.Template>
        <ControlTemplate TargetType="Button">
            <Border x:Name="Bd" Background="Transparent" CornerRadius="6" Padding="7,5">
                <ContentPresenter/>
            </Border>
            <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter TargetName="Bd" Property="Background" Value="{DynamicResource HoverBackground}"/>
                </Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>
    </Button.Template>
    <controls:SvgViewboxEx Width="14" Height="14" Fill="{DynamicResource TextSecondary}" Source="..."/>
</Button>
```

### Separator
```xml
<Rectangle Width="1" Height="20" Fill="{DynamicResource ControlBorderBrush}" VerticalAlignment="Center"/>
```

---

## ⚠️ Dos & Don'ts

| ✅ DO | ❌ DON'T |
|-------|---------|
| `{DynamicResource TextBrush}` | `Foreground="#FFC8D0E8"` |
| `{DynamicResource CardBackgroundBrush}` | `Background="#FF141720"` |
| `Style="{DynamicResource PrimaryButton}"` | Tự viết template cho button thường |
| `{DynamicResource ShadowColor}` (Color) | `{DynamicResource PrimaryBrush}` (Brush) cho Shadow |
| `DynamicResource` cho theme tokens | `StaticResource` cho theme tokens |
| Thêm key vào **tất cả** theme files | Chỉ thêm vào 1 theme |

> **Note**: `StaticResource` OK cho **style references** và **converter references**.

---

## 🎨 50+ Node Colors (Common.xaml — Cố định)

Pattern: `{Name}Brush` / `{Name}HoverBrush` / `{Name}PressedBrush` / `TextOn{Name}Brush`

Thường dùng: `SkyAzure`, `EmeraldGreen`, `SunsetOrange`, `RoyalPurple`, `CoralVivid`, `TealCyan`, `SlateGray`, `Indigo`, `GoldenYellow`, `CherryBlossom`, `ForestPine`, `MidnightBlue`, `LimeBright`, `MagentaBold`...

---

## 📎 Source Files

- SemanticTokens: `Themes/Base/SemanticTokens.xaml`
- Common colors: `Themes/Base/Colors/Common.xaml`
- Button styles: `Themes/Controls/Buttons.xaml`
- Theme README: `Themes/README.md`
