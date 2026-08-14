# Hướng Dẫn Sử Dụng Icon Có Màu

## Tổng Quan

Flow-App hỗ trợ 2 chế độ hiển thị màu cho icon SVG:
- **Màu gốc (Original Colors)**: Sử dụng màu có sẵn trong file SVG
- **Màu theo Control/Fill**: Sử dụng màu từ control cha hoặc thuộc tính `Fill`

## Cách Đặt Tên Folder Icon

### Quy Tắc Auto-Detect

Hệ thống tự động detect chế độ màu dựa trên tên folder chứa icon:

- **Folder có đuôi `.color`** → Icon dùng màu gốc SVG
  - Ví dụ: `duotone-light.color/`, `sharp-duotone-regular.color/`
  - Icon trong folder này sẽ tự động `UseOriginalColors = true`

- **Folder không có đuôi `.color`** → Icon dùng màu theo control/fill
  - Ví dụ: `duotone-light/`, `sharp-duotone-regular/`, `solid/`
  - Icon trong folder này sẽ tự động `UseOriginalColors = false`

### Ví dụ Cấu Trúc Folder

```
Assets/Icons/
├── duotone-light/              # Icon dùng màu theo control/fill
│   ├── folder-open.svg
│   ├── border-none.svg
│   └── ...
├── duotone-light.color/        # Icon dùng màu gốc SVG
│   ├── folder-open.svg
│   ├── border-none.svg
│   └── ...
├── sharp-duotone-regular/      # Icon dùng màu theo control/fill
│   └── ...
└── sharp-duotone-regular.color/ # Icon dùng màu gốc SVG
    └── ...
```

## Tham Số UseOriginalColors

### SvgViewboxEx

```xml
<<controls:SvgViewboxEx 
    Source="..." 
    UseOriginalColors="True"   <!-- Dùng màu gốc SVG -->
/>

<<controls:SvgViewboxEx 
    Source="..." 
    UseOriginalColors="False"  <!-- Dùng màu theo control/fill -->
/>

<<controls:SvgViewboxEx 
    Source="..." 
    <!-- Không set → Auto-detect dựa trên folder -->
/>
```

### IconSelectorUserControl

```xml
<<local:IconSelectorUserControl 
    UseOriginalColors="True"   <!-- Icon trong popup dùng màu gốc -->
/>

<<local:IconSelectorUserControl 
    UseOriginalColors="False"  <!-- Icon trong popup dùng màu theo control -->
/>

<<local:IconSelectorUserControl 
    <!-- Không set → Auto-detect dựa trên folder của từng icon -->
/>
```

## Cách Auto-Detect Hoạt Động

1. **Khi icon được load**: Hệ thống kiểm tra path của icon
2. **Nếu path chứa folder `.color`**: Set `UseOriginalColors = true`
3. **Nếu path không chứa folder `.color`**: Set `UseOriginalColors = false`
4. **Nếu user đã set UseOriginalColors manually**: Giữ nguyên giá trị user set

## Override Manual

Bạn có thể override auto-detect bằng cách set `UseOriginalColors` explicitly:

```xml
<!-- Icon nằm trong folder .color nhưng muốn dùng màu theo control -->
<<controls:SvgViewboxEx 
    Source="Assets/Icons/duotone-light.color/folder-open.svg"
    UseOriginalColors="False"
/>

<!-- Icon nằm trong folder thường nhưng muốn dùng màu gốc -->
<<controls:SvgViewboxEx 
    Source="Assets/Icons/duotone-light/folder-open.svg"
    UseOriginalColors="True"
/>
```

## Ví Dụ Sử Dụng Thực Tế

### Ví dụ 1: Icon có màu sắc đặc biệt (logo, brand)

Đặt icon trong folder có đuôi `.color`:

```
Assets/Icons/brands.color/
├── git-alt.svg          # Logo Git có màu gốc
├── html5.svg           # Logo HTML5 có màu gốc
└── openai.svg          # Logo OpenAI có màu gốc
```

Sử dụng:
```xml
<<controls:SvgViewboxEx 
    Source="{Binding Source={x:Static sys:String.Empty}, 
                     Converter={StaticResource IconKeyToPathConverter}, 
                     ConverterParameter='git-alt brands'}"
    <!-- Auto UseOriginalColors = true vì folder có .color -->
/>
```

### Ví dụ 2: Icon UI đơn sắc

Đặt icon trong folder không có đuôi `.color`:

```
Assets/Icons/duotone-light/
├── folder-open.svg      # Icon UI, sẽ được tô màu theo theme
├── border-none.svg
└── ...
```

Sử dụng:
```xml
<<controls:SvgViewboxEx 
    Source="{Binding Source={x:Static sys:String.Empty}, 
                     Converter={StaticResource IconKeyToPathConverter}, 
                     ConverterParameter='folder-open duotone-light'}"
    Fill="{DynamicResource TextBrush}"
    <!-- Auto UseOriginalColors = false, dùng màu từ Fill -->
/>
```

### Ví dụ 3: IconSelector với icon có màu

```xml
<<local:IconSelectorUserControl 
    SelectedIcon="{Binding SelectedIcon}"
    UseOriginalColors="True"
    <!-- Tất cả icon trong popup sẽ dùng màu gốc -->
/>
```

## Thêm Icon Màu Mới

Khi thêm icon có màu mới vào dự án:

1. **Tạo folder mới với đuôi `.color`**:
   ```
   Assets/Icons/my-style.color/
   ```

2. **Copy file SVG vào folder**:
   ```
   Assets/Icons/my-style.color/my-icon.svg
   ```

3. **Cập nhật available_icons.txt** (nếu cần):
   ```
   my-icon my-style.color=Assets/Icons/my-style.color/my-icon.svg
   ```

4. **Sử dụng trong code**:
   ```xml
   <<controls:SvgViewboxEx 
       Source="{Binding Source={x:Static sys:String.Empty}, 
                        Converter={StaticResource IconKeyToPathConverter}, 
                        ConverterParameter='my-icon my-style.color'}"
       <!-- Auto UseOriginalColors = true -->
   />
   ```

## Lưu Ý Quan Trọng

- **Auto-detect chỉ áp dụng khi UseOriginalColors chưa được set manually**
- **Priority**: Manual set > Auto-detect
- **Folder `.color` nên dùng cho icon có màu sắc đặc biệt** (logo, brand, icon nhiều màu)
- **Folder thường nên dùng cho icon UI đơn sắc** (icon cần thay đổi màu theo theme)
- **Sau khi đổi tên folder, cần rebuild hoặc reload manifest** để cập nhật path

## Troubleshooting

### Icon không hiển thị màu gốc

- Kiểm tra folder có đuôi `.color` không
- Kiểm tra xem có set `UseOriginalColors="False"` manually không
- Kiểm tra path trong `available_icons.txt` có đúng không

### Icon không hiển thị màu theo control

- Kiểm tra folder không có đuôi `.color`
- Kiểm tra `Fill` hoặc `Foreground` của control cha
- Kiểm tra xem có set `UseOriginalColors="True"` manually không

### Sau khi đổi tên folder, icon vẫn dùng chế độ cũ

- Reload manifest: `IconResources.ReloadManifest()`
- Hoặc rebuild ứng dụng
