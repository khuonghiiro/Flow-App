# 🎨 FlowMy Theme Token Reference (English for AI Assistants)

> **Purpose**: Complete reference cheat-sheet for all WPF `DynamicResource` theme keys in FlowMy.  
> **Rule**: When generating or updating XAML, **ONLY USE TOKENS FROM THIS REFERENCE** to guarantee seamless adaptation across all 8 runtime themes.

---

## ⚠️ MANDATORY XAML DESIGN & BUTTON RULES FOR AI

1. **NEVER Hardcode Raw Colors**:
   - ❌ `<Border Background="#FF1A1B1E">` or `<TextBlock Foreground="Red"/>`
   - ✅ `<Border Background="{DynamicResource CardBackgroundBrush}">`
2. **ALWAYS Use `{DynamicResource TokenKey}`**:
   - Allows instant runtime switching between Light, Dark, Dracula, Monokai, Soft Light, Soft Dark, Night, Modern.
3. **CRITICAL BUTTON PADDING RULE (Explicit Width/Height)**:
   - Default button styles (`PrimaryButton`, `SecondaryButton`, `DangerButton`, etc.) include internal padding (e.g. `16,8`).
   - When a `<Button>` specifies explicit `Width` and/or `Height` (e.g., square icon buttons `Width="32" Height="32"`, fixed actions `Width="80" Height="28"`), **YOU MUST ALWAYS SET `Padding="0"`**:
   ```xml
   <!-- ✅ CORRECT: Fixed size button MUST have Padding="0" -->
   <Button Style="{DynamicResource PrimaryButton}" Width="32" Height="32" Padding="0">
       <controls:SvgViewboxEx Source="Assets/Icons/play.svg" Width="14" Height="14"/>
   </Button>

   <!-- ❌ INCORRECT: Default style padding causes icon/text offset or clipping inside fixed dimensions -->
   <Button Style="{DynamicResource PrimaryButton}" Width="32" Height="32">
       <controls:SvgViewboxEx Source="Assets/Icons/play.svg" Width="14" Height="14"/>
   </Button>
   ```
4. **New Custom Styles Location**:
   - Place all new custom `ResourceDictionary` files into `FlowMy.Wpf-UI/Themes/Control_News/`.
5. **XAML Modularity & Line Limit**:
   - Keep `.xaml` files under **800 – 1,000 lines**. Decompose complex dialogs or canvas panels into standalone `UserControl`s or child dictionaries.

---

## 📁 Theme Files Architecture

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
│   ├── SemanticTokens.xaml   ← Primary Semantic Tokens
│   ├── Colors/
│   │   ├── Common.xaml       ← Common fixed palette colors
│   │   ├── Dark.xaml         ← Dark theme overrides
│   │   ├── Light.xaml        ← Light theme overrides
│   │   └── ...
│   ├── Fonts.xaml
│   └── Animations.xaml
│
├── Controls/                 ← System controls styles
│   ├── Buttons.xaml
│   ├── ComboBoxes.xaml
│   ├── DataGrids.xaml
│   ├── Inputs.xaml
│   └── ...
│
└── Control_News/             ← Custom Styles for new features
    ├── MyCustomComponentStyles.xaml
    └── ...
```

---

## 🏗️ Surface / Window / Card Tokens

| DynamicResource Key | Role / Semantic Meaning | Usage Context |
|---------------------|-------------------------|---------------|
| `WindowBackgroundBrush` | Root window background | `Window.Background` |
| `WindowBodyBackground` | Main panel / body area | Content container area |
| `HeaderBackgroundBrush` | Header / footer bar bg | Navigation / bottom status bar |
| `HeaderBackgroundMainBrush`| Main editor top toolbar | Canvas top header |
| `CardBackgroundBrush` | Card container background | Card, dialog section |
| `CardColor` | Card background (alias) | Equivalent to `CardBackgroundBrush` |
| `CardHoverBackground` | Card hover state | Hovered card state |
| `SurfaceColor` | Surface layer container | Tab panel, group, badge bg |
| `ControlBackground` | General control background | Miscellaneous controls |
| `BackgroundAll` | Deepest root layer | Global window layer |

---

## 📝 Typography & Text Tokens

| DynamicResource Key | Role / Semantic Meaning | Usage Context |
|---------------------|-------------------------|---------------|
| `TextPrimary` | Highest emphasis heading | Titles, prominent headings |
| `TextBrush` | Standard body text | Paragraphs, labels, items |
| `TextSecondary` | Medium emphasis text | Subtitles, helper text |
| `TextMuted` | Low emphasis text | Timestamps, hints, annotations |
| `TextDisabled` | Disabled text | Disabled controls |
| `TextNoteBrush` | Explanatory note text | Informative callouts |
| `PlaceholderBrush` | Form placeholder text | TextBox ghost / watermark text |

---

## 🔲 Border Tokens

| DynamicResource Key | Role / Semantic Meaning |
|---------------------|-------------------------|
| `BorderColor` | Subtle container borders |
| `ControlBorderBrush` | Interactive control border |
| `BorderBrush` | Alias of `ControlBorderBrush` |
| `BorderInverseColor` | Inverted contrast border |

---

## 🎯 Semantic Color Palettes

Each semantic color provides: `{Name}Brush` → `{Name}HoverBrush` → `{Name}PressedBrush` + `TextOn{Name}Brush`

- **Primary (Blue)**: `PrimaryBrush`, `PrimaryHoverBrush`, `PrimaryPressedBrush`, `TextOnPrimaryBrush`, `PrimaryGlowBrush`
- **Secondary (Gray)**: `SecondaryBrush`, `SecondaryHoverBrush`, `SecondaryPressedBrush`, `TextOnSecondaryBrush`
- **Success (Green)**: `SuccessBrush`, `SuccessHoverBrush`, `SuccessPressedBrush`, `TextOnSuccessBrush`
- **Danger (Red)**: `DangerBrush`, `DangerHoverBrush`, `DangerPressedBrush`, `TextOnDangerBrush`
- **Warning (Yellow)**: `WarningBrush`, `WarningHoverBrush`, `WarningPressedBrush`, `TextOnWarningBrush`
- **Info (Cyan)**: `InfoBrush`, `InfoHoverBrush`, `InfoPressedBrush`, `TextOnInfoBrush`
- **Transparent**: `TransparentBrush`, `TransparentHoverBrush`, `TransparentPressedBrush`

---

## 🔘 Available Button Styles

| Style Key | Typical Use Case |
|-----------|------------------|
| `PrimaryButton` | Primary confirm actions (Save, Run, OK) |
| `SecondaryButton` | Secondary actions (Cancel, Dismiss) |
| `SuccessButton` | Positive actions (Export, Complete) |
| `DangerButton` | Destructive actions (Delete, Stop, Remove) |
| `WarningButton` | Cautionary actions (Reset, Overwrite) |
| `InfoButton` | Informative actions (Help, Details) |
| `TransparentButtonStyle`| Toolbar icon buttons |
| `ModernIconButtonStyle` | Rounded modern icon buttons |
