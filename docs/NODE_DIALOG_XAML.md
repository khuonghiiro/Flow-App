# Dialog XAML — FlowMy Node Creation

> Cập nhật: 2026-06-04
> Phần này giải thích cách tạo file Dialog XAML cho node mới.
> **Tham khảo:** `Views/Overlays/ScreenCaptureNodeDialog.xaml` (đã chuẩn hóa)

---

## 5. Dialog XAML

### 5.1 Template chuẩn

```xml
<local:BaseNodeDialog x:Class="FlowMy.Views.Overlays.YourNodeDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="clr-namespace:FlowMy.Controls"
    xmlns:local="clr-namespace:FlowMy.Views.Overlays"
    Title="Your Node"
    WindowStyle="None" ResizeMode="CanResize"
    AllowsTransparency="True" Background="Transparent"
    ShowInTaskbar="False" Topmost="True"
    Width="520" MinWidth="420" MinHeight="420">

    <Border CornerRadius="12" Padding="0" Style="{DynamicResource DialogOuterBorder}">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <!-- HEADER -->
            <Border Grid.Row="0" Style="{DynamicResource DialogHeaderBorder}" Padding="16,12">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBox x:Name="TitleTextBox" Grid.Column="0"
                             Text="{Binding NodeTitle, UpdateSourceTrigger=PropertyChanged}"
                             Style="{DynamicResource BaseTextBoxV2}" FontSize="16"
                             Padding="0,4" VerticalContentAlignment="Center" Cursor="IBeam"/>
                    <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
                        <Button Width="24" Height="24" Content="▶" FontSize="12" Padding="0" Margin="8,0,0,0"
                                Style="{DynamicResource PrimaryButton}" Cursor="Hand" ToolTip="Chạy logic node này"
                                Command="{Binding RunSingleNodeCommand}"/>
                        <Button x:Name="CloseButton" Width="24" Height="24" Padding="0" Content="✕" FontSize="12"
                                FontWeight="Bold" Margin="8,0,0,0" Cursor="Hand"
                                Style="{DynamicResource DangerButton}" Click="CloseButton_Click"/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- CONTENT -->
            <TabControl Grid.Row="1" Background="Transparent" BorderThickness="0">

                <!-- TAB: LOGIC -->
                <TabItem Header="Logic" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>

                            <!-- 🎨 Cấu hình hiển thị -->
                            <Border Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,12">
                                <StackPanel>
                                    <TextBlock Text="🎨 Cấu hình hiển thị" Foreground="{DynamicResource TextBrush}"
                                               FontSize="12" FontWeight="SemiBold" Margin="0,0,0,10"/>
                                    <TextBlock Text="Hiển thị tiêu đề" Foreground="{DynamicResource TextMuted}"
                                               FontSize="10" Margin="0,0,0,4"/>
                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}" Margin="0,0,0,8"
                                              ItemsSource="{Binding TitleDisplayModeOptions}"
                                              SelectedValuePath="Value" DisplayMemberPath="DisplayName"
                                              SelectedValue="{Binding TitleDisplayMode, Mode=TwoWay}"/>
                                    <TextBlock Text="Màu tiêu đề" Foreground="{DynamicResource TextMuted}"
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
                                                BorderBrush="{DynamicResource ControlBorderBrush}" BorderThickness="1"/>
                                    </Grid>
                                </StackPanel>
                            </Border>

                            <!-- Properties đặc thù của node ở đây -->
                            <!-- Sử dụng Border với emoji icon cho mỗi section quan trọng -->

                            <!-- 📍 Input từ node khác -->
                            <Border Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,12">
                                <StackPanel>
                                    <TextBlock Text="📍 Input — [Tên input]" Foreground="{DynamicResource TextBrush}"
                                               FontSize="11" FontWeight="SemiBold" Margin="0,0,0,6"/>
                                    <TextBlock Foreground="{DynamicResource TextMuted}" FontSize="10"
                                               TextWrapping="Wrap" Margin="0,0,0,10">
                                        <Run Text="[Mô tả ngắn gọn]"/>
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

                            <!-- Outputs -->
                            <TextBlock Text="Outputs" Foreground="{DynamicResource TextBrush}"
                                       FontSize="12" FontWeight="SemiBold" Margin="0,0,0,6"/>
                            <Border Style="{DynamicResource DialogOuterBorder}" CornerRadius="8" Padding="10">
                                <StackPanel x:Name="OutputsPanel"/>
                            </Border>

                        </StackPanel>
                    </ScrollViewer>
                </TabItem>

                <!-- TAB: CẤU HÌNH -->
                <TabItem Header="Cấu hình" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>

                            <TextBlock Text="Vị trí cổng IN/OUT" Foreground="{DynamicResource TextBrush}"
                                       FontSize="12" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Grid Margin="0,0,0,16">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <StackPanel Grid.Column="0" Margin="0,0,6,0">
                                    <TextBlock Text="Port IN" Foreground="{DynamicResource TextMuted}"
                                               FontSize="10" Margin="0,0,0,4"/>
                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                              ItemsSource="{Binding PortPositionOptions}"
                                              SelectedItem="{Binding InputPortPosition, Mode=TwoWay}"/>
                                </StackPanel>
                                <StackPanel Grid.Column="1" Margin="6,0,0,0">
                                    <TextBlock Text="Port OUT" Foreground="{DynamicResource TextMuted}"
                                               FontSize="10" Margin="0,0,0,4"/>
                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                              ItemsSource="{Binding PortPositionOptions}"
                                              SelectedItem="{Binding OutputPortPosition, Mode=TwoWay}"/>
                                </StackPanel>
                            </Grid>

                            <TextBlock Text="Tái sử dụng flow" Foreground="{DynamicResource TextBrush}"
                                       FontSize="12" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <ItemsControl ItemsSource="{Binding ReuseRoutes}">
                                <ItemsControl.ItemTemplate>
                                    <DataTemplate>
                                        <Border Margin="0,0,0,12"
                                                Background="{DynamicResource WindowBackground}"
                                                BorderBrush="{DynamicResource ControlBorderBrush}"
                                                BorderThickness="1" CornerRadius="8" Padding="10">
                                            <Grid>
                                                <Grid.ColumnDefinitions>
                                                    <ColumnDefinition Width="2*"/>
                                                    <ColumnDefinition Width="2*"/>
                                                    <ColumnDefinition Width="2*"/>
                                                </Grid.ColumnDefinitions>
                                                <StackPanel Grid.Column="0">
                                                    <TextBlock Text="Node IN" Foreground="{DynamicResource TextBrush}"
                                                               FontSize="10" Opacity="0.7" Margin="0,0,0,4"/>
                                                    <TextBlock Text="{Binding IncomingNodeTitle}"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="11" FontWeight="SemiBold"
                                                               TextTrimming="CharacterEllipsis"/>
                                                </StackPanel>
                                                <StackPanel Grid.Column="1">
                                                    <TextBlock Text="Node OUT" Foreground="{DynamicResource TextBrush}"
                                                               FontSize="10" Opacity="0.7" Margin="0,0,0,4"/>
                                                    <controls:NodeSearchComboBoxUserControl Height="32"
                                                              ItemsSource="{Binding OutgoingOptions}"
                                                              SelectedValuePath="NodeId" DisplayMemberPath="Title"
                                                              SelectedValue="{Binding SelectedOutgoingNodeId, Mode=TwoWay}"/>
                                                </StackPanel>
                                                <StackPanel Grid.Column="2" Margin="10,0,0,0">
                                                    <TextBlock Text="Kiểu line OUT" Foreground="{DynamicResource TextBrush}"
                                                               FontSize="10" Opacity="0.7" Margin="0,0,0,4"/>
                                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                                              ItemsSource="{Binding DataContext.ConnectionLineStyleOptions, RelativeSource={RelativeSource AncestorType=Window}}"
                                                              SelectedValuePath="Key" DisplayMemberPath="DisplayName"
                                                              SelectedValue="{Binding SelectedLineStyleKey, Mode=TwoWay}"/>
                                                </StackPanel>
                                            </Grid>
                                        </Border>
                                    </DataTemplate>
                                </ItemsControl.ItemTemplate>
                            </ItemsControl>

                        </StackPanel>
                    </ScrollViewer>
                </TabItem>

            </TabControl>
        </Grid>
    </Border>
</local:BaseNodeDialog>
```

### 5.2 Chuẩn UI (từ UI_Dialog_Standardization_Guide.md)

| Thuộc tính | Giá trị chuẩn |
|-----------|---------------|
| Window Width | 520px |
| Window MinWidth | 420px |
| Window MinHeight | 420px |
| Header Padding | 16,12 (không phải 16,12,12,12) |
| Title FontSize | 16px |
| Play/Close Button | 24x24, Padding="0" |
| Section Header FontSize | 12px bold (với emoji icon) |
| Subsection Header FontSize | 11px bold |
| Label FontSize | 10px với TextMuted color |
| TextBox/ComboBox Height | 32px (không phải 36px) |
| Color Preview Size | 32x32 (không phải 36x36) |
| Border Padding | 12px |
| Border CornerRadius | 8px (section), 6px (hint box) |
| Section Margin | 0,0,0,12 |
| Element Margin | 0,0,0,8 hoặc 0,0,0,10 |
| Label-Control Margin | 0,0,0,4 hoặc 0,0,0,6 |
| Grid Column Gap | 6-12px |

### 5.3 Icon Emoji cho Sections

- 🎨 Cấu hình hiển thị
- 📍 Input từ node khác
- 🎯 Manual input/selection
- ⚙️ Technical settings
- 💾 Storage/Save settings
- 🔄 Loop/Repeat settings
- 🌐 Network/HTTP settings
- 📊 Data/Output settings

### 5.4 x:Name bắt buộc (base dùng `FindName`)

| x:Name | Kiểu | Mục đích |
|--------|------|---------|
| `TitleColorComboBox` | `ComboBox` | Base đọc `SelectedValue` để update preview |
| `TitleColorPreview` | `Border` | Base set `Background` theo màu đã chọn |
| `InputsPanel` | `StackPanel` | Base load input items vào đây |
| `OutputsPanel` | `StackPanel` | Base load output items vào đây |

---

## Theme System — DynamicResource bắt buộc

**KHÔNG hardcode màu trong XAML dialog.** Dùng DynamicResource để dialog tự đổi màu theo theme:

| Resource Key | Dùng cho | Thay cho |
|---|---|---|
| `{DynamicResource DialogOuterBorder}` | Style outer border dialog | `Background="#FF1E293B"` |
| `{DynamicResource DialogHeaderBorder}` | Style header dialog | `Background="#FF0F172A"` |
| `{DynamicResource TextBrush}` | Foreground text chính | `Foreground="White"` |
| `{DynamicResource TextMuted}` | Text phụ / mô tả | `Foreground="#CCCCCC"` |
| `{DynamicResource WindowBackground}` | Background card/panel | `Background="#FF1E293B"` |
| `{DynamicResource ControlBorderBrush}` | BorderBrush controls | `BorderBrush="#33FFFFFF"` |
| `{DynamicResource BaseTextBoxV2}` | Style TextBox | — |
| `{DynamicResource BaseComboBox}` | Style ComboBox | — |
| `{DynamicResource PrimaryButton}` | Style nút Play | — |
| `{DynamicResource DangerButton}` | Style nút Close | — |
| `{StaticResource HttpTabItemStyle}` | Style TabItem | — (**StaticResource**, không phải Dynamic) |

**Dialog sizing:**
```xml
<local:BaseNodeDialog Width="520" MinWidth="420" MinHeight="420">
<!-- ⚠️ KHÔNG đặt Height cứng — NodeDialogManager auto-size 90% screen -->
<!-- ⚠️ KHÔNG đặt MaxHeight cố định -->
```

---

## Reference Implementations

Xem các file sau làm mẫu thực tế:
- `Views/Overlays/ScreenCaptureNodeDialog.xaml` - Dialog mẫu chuẩn UI (đã chuẩn hóa)
- `Views/Overlays/DelayNodeDialog.xaml` - Dialog mẫu chuẩn theme + responsive
- `Views/Overlays/AssignDataNodeDialog.xaml` - Dialog với custom property handlers
- `Views/Overlays/CodeNodeDialog.xaml` - Dynamic Input mapping + Dynamic Output keys
