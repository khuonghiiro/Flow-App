# 🎨 Hướng Dẫn Chuẩn Hóa UI Dialog cho Flow-App

## 📋 Mục Tiêu

Chuẩn hóa UI của tất cả NodeDialog để có:
- **Spacing nhất quán** giữa các elements
- **Grouping rõ ràng** các sections liên quan
- **Font size hierarchy** dễ đọc
- **Layout gọn gàng** không redundant
- **Không làm hỏng logic** - chỉ thay đổi XAML UI

## 🏗️ Kiến Trúc Dialog

### Base Class
- **BaseNodeDialog** (`Views/Overlays/BaseNodeDialog.xaml.cs`)
  - Abstract class chứa logic chung
  - Không có XAML file riêng
  - Các dialog kế thừa từ class này

### Derived Dialogs
Tất cả các file `*NodeDialog.xaml` kế thừa từ `BaseNodeDialog`:
```xml
<local:BaseNodeDialog x:Class="FlowMy.Views.Overlays.YourNodeDialog"
```

## 📐 Quy Chuẩn UI Mới

### 1. Window Properties
```xml
Width="520"        <!-- Tăng từ 500 lên 520 -->
MinWidth="420"     <!-- Tăng từ 400 lên 420 -->
MinHeight="420"    <!-- Tăng từ 400 lên 420 -->
```

### 2. Font Size Hierarchy
```
- Tiêu đề chính:        16px (Title textbox)
- Section header:       12px bold (với icon emoji)
- Subsection header:    11px bold
- Label text:           10px regular
- Descriptive text:     10px với TextMuted color
```

### 3. Control Heights
```
- TextBox chính:        32px (giảm từ 36px)
- ComboBox:            32px (giảm từ 36px)
- Button chính:         36px
- Button icon nhỏ:      32px (vuông 32x32)
- Button header:        24px (play/close buttons)
```

### 4. Spacing Standards
```
- Margin giữa sections:     12px (0,0,0,12)
- Margin giữa elements:     8-10px (0,0,0,8 hoặc 0,0,0,10)
- Margin label-control:     4-6px (0,0,0,4 hoặc 0,0,0,6)
- Padding trong Border:     12px
- Grid column gap:          6-12px
```

### 5. Border & Background Sections
Các section quan trọng nên được đóng trong Border:

```xml
<Border Background="{DynamicResource WindowBackground}"
        BorderBrush="{DynamicResource ControlBorderBrush}"
        BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,12">
    <StackPanel>
        <TextBlock Text="🎨 Section Title" 
                   Foreground="{DynamicResource TextBrush}"
                   FontSize="12" FontWeight="SemiBold" Margin="0,0,0,10"/>
        <!-- Content -->
    </StackPanel>
</Border>
```

### 6. Icon Emoji Cho Sections
Sử dụng emoji để dễ nhận diện:
- 🎨 Cấu hình hiển thị (Display settings)
- 📍 Input từ node khác (Input from nodes)
- 🎯 Manual input/selection
- ⚙️ Technical settings (Tesseract, OCR, etc.)
- 💾 Storage/Save settings
- 🔄 Loop/Repeat settings
- 🌐 Network/HTTP settings
- 📊 Data/Output settings

## 🔧 Template Chuẩn

### Header Section (Giữ nguyên - đã chuẩn)
```xml
<Border Grid.Row="0" Style="{DynamicResource DialogHeaderBorder}" Padding="16,12">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        
        <TextBox x:Name="TitleTextBox" Grid.Column="0"
                 Text="{Binding NodeTitle, UpdateSourceTrigger=PropertyChanged}"
                 Style="{DynamicResource BaseTextBoxV2}"
                 FontSize="16" Padding="0,4" 
                 VerticalContentAlignment="Center" Cursor="IBeam"/>
        
        <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
            <Button Width="24" Height="24" Content="▶" FontSize="12" 
                    Padding="0" Margin="8,0,0,0"
                    Style="{DynamicResource PrimaryButton}" 
                    Cursor="Hand" ToolTip="Chạy logic node này"
                    Command="{Binding RunSingleNodeCommand}"/>
            <Button x:Name="CloseButton" Width="24" Height="24" 
                    Padding="0" Content="✕" FontSize="12" 
                    FontWeight="Bold" Margin="8,0,0,0" Cursor="Hand"
                    Style="{DynamicResource DangerButton}" 
                    Click="CloseButton_Click"/>
        </StackPanel>
    </Grid>
</Border>
```

### Display Configuration Section (Chuẩn hóa)
```xml
<!-- Cấu hình hiển thị -->
<Border Background="{DynamicResource WindowBackground}"
        BorderBrush="{DynamicResource ControlBorderBrush}"
        BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,12">
    <StackPanel>
        <TextBlock Text="🎨 Cấu hình hiển thị" 
                   Foreground="{DynamicResource TextBrush}"
                   FontSize="12" FontWeight="SemiBold" Margin="0,0,0,10"/>
        
        <TextBlock Text="Hiển thị tiêu đề" 
                   Foreground="{DynamicResource TextMuted}" 
                   FontSize="10" Margin="0,0,0,4"/>
        <ComboBox Height="32" Style="{DynamicResource BaseComboBox}" 
                  Margin="0,0,0,8"
                  ItemsSource="{Binding TitleDisplayModeOptions}"
                  SelectedValuePath="Value" DisplayMemberPath="DisplayName"
                  SelectedValue="{Binding TitleDisplayMode, Mode=TwoWay}"/>

        <TextBlock Text="Màu tiêu đề" 
                   Foreground="{DynamicResource TextMuted}" 
                   FontSize="10" Margin="0,0,0,4"/>
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <ComboBox x:Name="TitleColorComboBox" Grid.Column="0" Height="32"
                      Style="{DynamicResource BaseComboBox}"
                      ItemsSource="{Binding TitleColorOptions}"
                      SelectedValuePath="Key" DisplayMemberPath="DisplayName"
                      SelectedValue="{Binding TitleColorKey, Mode=TwoWay}"
                      SelectionChanged="TitleColorComboBox_SelectionChanged"/>
            <Border x:Name="TitleColorPreview" Grid.Column="1"
                    Width="32" Height="32" CornerRadius="6" Margin="8,0,0,0"
                    BorderBrush="{DynamicResource ControlBorderBrush}" 
                    BorderThickness="1"/>
        </Grid>
    </StackPanel>
</Border>
```

### Input From Node Section
```xml
<Border Background="{DynamicResource WindowBackground}"
        BorderBrush="{DynamicResource ControlBorderBrush}"
        BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,12">
    <StackPanel>
        <TextBlock Text="📍 Input — [Tên input cụ thể]" 
                   Foreground="{DynamicResource TextBrush}"
                   FontSize="11" FontWeight="SemiBold" Margin="0,0,0,6"/>
        <TextBlock Foreground="{DynamicResource TextMuted}" 
                   FontSize="10" TextWrapping="Wrap" Margin="0,0,0,10">
            <Run Text="[Mô tả ngắn gọn về input này]"/>
        </TextBlock>

        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="8"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <StackPanel Grid.Column="0">
                <TextBlock Text="Node" Foreground="{DynamicResource TextMuted}" 
                           FontSize="10" Margin="0,0,0,4"/>
                <controls:NodeSearchComboBoxUserControl Height="32"
                          ItemsSource="{Binding AvailableNodeOptions}"
                          SelectedValuePath="NodeId" DisplayMemberPath="Title"
                          SelectedValue="{Binding SourceNodeId, Mode=TwoWay}"/>
            </StackPanel>
            <StackPanel Grid.Column="2">
                <TextBlock Text="Key" Foreground="{DynamicResource TextMuted}" 
                           FontSize="10" Margin="0,0,0,4"/>
                <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                          ItemsSource="{Binding KeyOptions}"
                          SelectedValuePath="Key" DisplayMemberPath="Key"
                          SelectedValue="{Binding SelectedKey, Mode=TwoWay}"/>
            </StackPanel>
        </Grid>
    </StackPanel>
</Border>
```

### Simple Section (No Border)
```xml
<TextBlock Text="Section Title" 
           Foreground="{DynamicResource TextBrush}"
           FontSize="11" FontWeight="SemiBold" Margin="0,0,0,6"/>
<ComboBox Height="32" Style="{DynamicResource BaseComboBox}" 
          Margin="0,0,0,12"
          ItemsSource="{Binding Options}"
          SelectedValue="{Binding SelectedValue, Mode=TwoWay}"/>
```

### Window Selection with Refresh Button
```xml
<TextBlock Text="Ứng dụng" 
           Foreground="{DynamicResource TextBrush}"
           FontSize="11" FontWeight="SemiBold" Margin="0,0,0,4"/>
<TextBlock Foreground="{DynamicResource TextMuted}" 
           FontSize="10" TextWrapping="Wrap" Margin="0,0,0,6">
    <Run Text="Chọn app để focus. Để trống = màn hình hiện tại."/>
</TextBlock>
<Grid Margin="0,0,0,12">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
    </Grid.ColumnDefinitions>
    <ComboBox Grid.Column="0" Height="32"
              Style="{DynamicResource BaseComboBox}"
              ItemsSource="{Binding ActiveWindows}"
              DisplayMemberPath="DisplayName"
              SelectedItem="{Binding SelectedTargetWindow, Mode=TwoWay}"/>
    <Button Grid.Column="1" Content="↻" Height="32" Width="32" 
            Padding="0" Margin="6,0,0,0"
            Style="{DynamicResource SecondaryButton}" 
            ToolTip="Tải lại" Cursor="Hand"
            Command="{Binding LoadWindowsCommand}"/>
</Grid>
```

### Info/Hint Box
```xml
<Border Background="{DynamicResource WindowBackground}"
        BorderBrush="{DynamicResource ControlBorderBrush}"
        BorderThickness="1" CornerRadius="6" Padding="10" Margin="0,0,0,12">
    <StackPanel>
        <TextBlock Foreground="{DynamicResource TextBrush}" 
                   FontSize="10" FontWeight="SemiBold" Margin="0,0,0,4">
            <Run Text="💡 Gợi ý:"/>
        </TextBlock>
        <TextBlock Foreground="{DynamicResource TextMuted}" 
                   FontSize="10" TextWrapping="Wrap">
            <Run Text="Nội dung hint/gợi ý"/>
        </TextBlock>
    </StackPanel>
</Border>
```

### Tab Configuration Section
```xml
<TabItem Header="Cấu hình" Style="{StaticResource HttpTabItemStyle}">
    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
        <StackPanel>
            <TextBlock Text="Vị trí cổng IN/OUT" 
                       Foreground="{DynamicResource TextBrush}"
                       FontSize="12" FontWeight="SemiBold" Margin="0,0,0,8"/>
            <Grid Margin="0,0,0,16">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <StackPanel Grid.Column="0" Margin="0,0,6,0">
                    <TextBlock Text="Port IN" 
                               Foreground="{DynamicResource TextMuted}" 
                               FontSize="10" Margin="0,0,0,4"/>
                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                              ItemsSource="{Binding PortPositionOptions}"
                              SelectedItem="{Binding InputPortPosition, Mode=TwoWay}"/>
                </StackPanel>
                <StackPanel Grid.Column="1" Margin="6,0,0,0">
                    <TextBlock Text="Port OUT" 
                               Foreground="{DynamicResource TextMuted}" 
                               FontSize="10" Margin="0,0,0,4"/>
                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                              ItemsSource="{Binding PortPositionOptions}"
                              SelectedItem="{Binding OutputPortPosition, Mode=TwoWay}"/>
                </StackPanel>
            </Grid>

            <TextBlock Text="Tái sử dụng flow" 
                       Foreground="{DynamicResource TextBrush}"
                       FontSize="12" FontWeight="SemiBold" Margin="0,0,0,8"/>
            <ItemsControl ItemsSource="{Binding ReuseRoutes}">
                <!-- ReuseRoutes template... -->
            </ItemsControl>
        </StackPanel>
    </ScrollViewer>
</TabItem>
```

## 📝 Checklist Chuẩn Hóa

Khi sửa mỗi dialog, kiểm tra:

### ✅ Window Properties
- [ ] Width: 520px (hoặc lớn hơn nếu cần thiết cho content)
- [ ] MinWidth: 420px
- [ ] MinHeight: 420px

### ✅ Header Section
- [ ] Padding="16,12" (không phải 16,12,12,12)
- [ ] Play button: 24x24, Padding="0"
- [ ] Close button: 24x24, Padding="0"

### ✅ Display Config Section
- [ ] Đóng trong Border với icon 🎨
- [ ] Title header: FontSize="12" FontWeight="SemiBold"
- [ ] Labels: FontSize="10" với TextMuted color
- [ ] ComboBox: Height="32"
- [ ] Color preview: 32x32px (không phải 36x36)

### ✅ Font Sizes
- [ ] Section headers: 12px bold (với icon)
- [ ] Subsection headers: 11px bold
- [ ] Labels: 10px regular
- [ ] Descriptive text: 10px TextMuted

### ✅ Control Heights
- [ ] TextBox: 32px (giảm từ 36px)
- [ ] ComboBox: 32px (giảm từ 36px)
- [ ] Main buttons: 36px
- [ ] Icon buttons: 32px vuông

### ✅ Spacing
- [ ] Section margin: 0,0,0,12
- [ ] Element margin: 0,0,0,8 hoặc 0,0,0,10
- [ ] Label-control margin: 0,0,0,4 hoặc 0,0,0,6
- [ ] Border padding: 12px
- [ ] Grid column gap: 6-12px

### ✅ Grouping
- [ ] Display settings trong Border
- [ ] Input sections trong Border với icon 📍
- [ ] Các settings phức tạp trong Border riêng
- [ ] Hint/Info boxes có CornerRadius="6"

### ✅ Text
- [ ] Loại bỏ dấu ":" không cần thiết ở cuối label
- [ ] Hint text ngắn gọn, không dài dòng
- [ ] TextWrapping="Wrap" cho descriptive text

## 🚨 Lưu Ý Quan Trọng

### ⚠️ KHÔNG được thay đổi:

1. **Binding expressions** - Giữ nguyên tất cả `{Binding ...}`
2. **Event handlers** - Giữ nguyên tất cả `Click="..."`, `SelectionChanged="..."`
3. **x:Name** - Giữ nguyên tất cả tên controls có `x:Name="..."`
4. **Command bindings** - Giữ nguyên `Command="{Binding ...}"`
5. **Converter references** - Giữ nguyên tất cả Converters
6. **Logic trong .xaml.cs** - KHÔNG sửa code-behind

### ✅ CHỈ được thay đổi:

1. Width, Height, MinWidth, MinHeight, MaxWidth, MaxHeight
2. Padding, Margin values
3. FontSize values
4. Text content (để ngắn gọn hơn)
5. Grid ColumnDefinitions spacing
6. Border CornerRadius, Padding
7. Thêm/bỏ Border để grouping
8. Thêm icon emoji vào section titles

## 📁 Danh Sách Dialog Cần Chuẩn Hóa

### ✅ Đã hoàn thành:
1. ScreenCaptureNodeDialog.xaml
2. ScreenPositionPickerNodeDialog.xaml
3. TextScanNodeDialog.xaml

### 🔄 Cần chuẩn hóa (ưu tiên cao):
1. HttpRequestNodeDialog.xaml
2. CodeNodeDialog.xaml
3. ConditionalNodeDialog.xaml
4. LoopNodeDialog.xaml
5. DataFetcherNodeDialog.xaml
6. InputNodeDialog.xaml
7. OutputNodeDialog.xaml
8. AssignDataNodeDialog.xaml

### 📋 Cần chuẩn hóa (ưu tiên thấp):
- Tất cả các dialog còn lại trong thư mục Views/Overlays/

## 🎯 Ví Dụ So Sánh

### ❌ TRƯỚC (Chưa chuẩn)
```xml
<TextBlock Text="Hiển thị tiêu đề:" 
           Foreground="{DynamicResource TextBrush}"
           FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
<ComboBox Height="36" Style="{DynamicResource BaseComboBox}" 
          Margin="0,0,0,12"
          ItemsSource="{Binding TitleDisplayModeOptions}"
          SelectedValue="{Binding TitleDisplayMode, Mode=TwoWay}"/>

<TextBlock Text="Màu tiêu đề:" 
           Foreground="{DynamicResource TextBrush}"
           FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
<Grid Margin="0,0,0,20">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
    </Grid.ColumnDefinitions>
    <ComboBox Height="36" .../>
    <Border Width="36" Height="36" .../>
</Grid>
```

### ✅ SAU (Đã chuẩn)
```xml
<Border Background="{DynamicResource WindowBackground}"
        BorderBrush="{DynamicResource ControlBorderBrush}"
        BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,12">
    <StackPanel>
        <TextBlock Text="🎨 Cấu hình hiển thị" 
                   Foreground="{DynamicResource TextBrush}"
                   FontSize="12" FontWeight="SemiBold" Margin="0,0,0,10"/>
        
        <TextBlock Text="Hiển thị tiêu đề" 
                   Foreground="{DynamicResource TextMuted}" 
                   FontSize="10" Margin="0,0,0,4"/>
        <ComboBox Height="32" Style="{DynamicResource BaseComboBox}" 
                  Margin="0,0,0,8"
                  ItemsSource="{Binding TitleDisplayModeOptions}"
                  SelectedValue="{Binding TitleDisplayMode, Mode=TwoWay}"/>

        <TextBlock Text="Màu tiêu đề" 
                   Foreground="{DynamicResource TextMuted}" 
                   FontSize="10" Margin="0,0,0,4"/>
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <ComboBox Height="32" .../>
            <Border Width="32" Height="32" Margin="8,0,0,0" .../>
        </Grid>
    </StackPanel>
</Border>
```

## 🔍 Cách Kiểm Tra Logic Không Bị Hỏng

### 1. Sau khi sửa, compile project
```bash
dotnet build
```
Nếu có lỗi compile → có gì đó bị sửa sai

### 2. Kiểm tra bindings
- Tìm tất cả `{Binding ...}` trong file cũ
- Đảm bảo tất cả đều có trong file mới

### 3. Kiểm tra event handlers
- Tìm tất cả `Click="..."`, `SelectionChanged="..."` trong file cũ
- Đảm bảo tất cả đều có trong file mới với tên giống hệt

### 4. Kiểm tra x:Name
- Tìm tất cả `x:Name="..."` trong file cũ
- Đảm bảo tất cả đều có trong file mới

### 5. Test chức năng
- Mở dialog
- Nhập giá trị
- Chọn options
- Click buttons
- Đảm bảo tất cả hoạt động như cũ

## 💡 Tips

1. **Copy-paste với cẩn thận** - Không copy cả block nếu nó có binding/logic khác nhau
2. **Dùng Find/Replace** - Để thay đổi hàng loạt giá trị giống nhau (ví dụ: Height="36" → Height="32")
3. **Kiểm tra từng section** - Sửa từng section một, compile và test
4. **Backup trước khi sửa** - Git commit trước khi bắt đầu
5. **So sánh với dialog mẫu** - Dùng ScreenCaptureNodeDialog.xaml làm reference

## 📞 Hỗ Trợ

Nếu gặp vấn đề:
1. Kiểm tra lại checklist
2. So sánh với dialog mẫu (ScreenCaptureNodeDialog.xaml)
3. Compile để phát hiện lỗi binding/logic
4. Test chức năng trước khi commit

---

**Phiên bản:** 1.0  
**Ngày cập nhật:** 2026-06-03  
**Tác giả:** Kiro AI Assistant
