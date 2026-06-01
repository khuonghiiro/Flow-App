# Dialog XAML — FlowMy Node Creation

> Cập nhật: 2026-05-16
> Phần này giải thích cách tạo file Dialog XAML cho node mới.

---

## 5. Dialog XAML

```xml
<local:BaseNodeDialog x:Class="FlowMy.Views.Overlays.YourNodeDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="clr-namespace:FlowMy.Controls"
    xmlns:local="clr-namespace:FlowMy.Views.Overlays"
    Title="Your Node Dialog"
    WindowStyle="None" ResizeMode="CanResize"
    AllowsTransparency="True" Background="Transparent"
    ShowInTaskbar="False" Topmost="True"
    Width="460" MinWidth="350" MaxWidth="900" MinHeight="350">
    <!-- ⚠️ KHÔNG đặt Height cứng — NodeDialogManager auto-size 90% screen -->

    <Border CornerRadius="12" Padding="0" Style="{DynamicResource DialogOuterBorder}">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <!-- HEADER -->
            <Border Grid.Row="0" Style="{DynamicResource DialogHeaderBorder}" Padding="16,12,12,12">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBox x:Name="TitleTextBox" Grid.Column="0"
                             Text="{Binding NodeTitle, UpdateSourceTrigger=PropertyChanged}"
                             Style="{DynamicResource BaseTextBoxV2}" FontSize="16"
                             Padding="0,4,0,4" VerticalContentAlignment="Center" Cursor="IBeam"/>
                    <StackPanel Grid.Column="1" Orientation="Horizontal">
                        <Button Width="24" Height="24" Content="▶" FontSize="12"
                                Style="{DynamicResource PrimaryButton}" Cursor="Hand"
                                Margin="8,0,0,0" ToolTip="Chạy logic node này"
                                Padding = "0"
                                Command="{Binding RunSingleNodeCommand}"/>
                        <Button x:Name="CloseButton" Width="24" Height="24"
                                Style="{DynamicResource DangerButton}"
                                Padding = "0"
                                Content="×" FontSize="12" FontWeight="Bold" Cursor="Hand"
                                Margin="8,0,0,0" Click="CloseButton_Click"/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- CONTENT -->
            <TabControl Grid.Row="1" Background="Transparent" BorderThickness="0">

                <!-- TAB 1: LOGIC -->
                <TabItem Header="Logic" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>

                            <!-- TitleDisplayMode -->
                            <TextBlock Text="Hiển thị tiêu đề:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <ComboBox x:Name="TitleDisplayModeComboBox" Height="36"
                                      Style="{DynamicResource BaseComboBox}" Margin="0,0,0,16"
                                      ItemsSource="{Binding TitleDisplayModeOptions}"
                                      SelectedValuePath="Value" DisplayMemberPath="DisplayName"
                                      SelectedValue="{Binding TitleDisplayMode, Mode=TwoWay}"/>

                            <!-- TitleColorMode -->
                            <TextBlock Text="Màu tiêu đề:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Grid Margin="0,0,0,16">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                <!-- x:Name="TitleColorComboBox" — base dùng FindName để update preview -->
                                <ComboBox x:Name="TitleColorComboBox" Grid.Column="0" Height="36"
                                          Style="{DynamicResource BaseComboBox}"
                                          ItemsSource="{Binding TitleColorOptions}"
                                          SelectedValuePath="Key" DisplayMemberPath="DisplayName"
                                          SelectedValue="{Binding TitleColorKey, Mode=TwoWay}"
                                          SelectionChanged="TitleColorComboBox_SelectionChanged"/>
                                <!-- x:Name="TitleColorPreview" — base dùng FindName để update -->
                                <Border x:Name="TitleColorPreview" Grid.Column="1"
                                        Width="36" Height="36" CornerRadius="6" Margin="8,0,0,0"
                                        BorderBrush="{DynamicResource ControlBorderBrush}" BorderThickness="1"/>
                            </Grid>

                            <!-- Properties đặc thù của node ở đây -->

                            <!-- Inputs Panel -->
                            <TextBlock Text="Inputs:" Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Border Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,16">
                                <StackPanel x:Name="InputsPanel"/>
                            </Border>

                            <!-- Outputs Panel -->
                            <TextBlock Text="Outputs:" Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Border Background="{DynamicResource WindowBackground}"
                                    BorderBrush="{DynamicResource ControlBorderBrush}"
                                    BorderThickness="1" CornerRadius="8" Padding="12" Margin="0,0,0,16">
                                <StackPanel x:Name="OutputsPanel"/>
                            </Border>

                        </StackPanel>
                    </ScrollViewer>
                </TabItem>

                <!-- TAB 2: CẤU HÌNH -->
                <TabItem Header="Cấu hình" Style="{StaticResource HttpTabItemStyle}">
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
                        <StackPanel>

                            <!-- Port Position IN/OUT -->
                            <TextBlock Text="Vị trí cổng IN/OUT:"
                                       Foreground="{DynamicResource TextBrush}"
                                       FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                            <Grid Margin="0,0,0,12">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <StackPanel Grid.Column="0" Margin="0,0,8,0">
                                    <TextBlock Text="Port IN" Foreground="{DynamicResource TextBrush}"
                                               FontSize="12" Opacity="0.8" Margin="0,0,0,4"/>
                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                              ItemsSource="{Binding PortPositionOptions}"
                                              SelectedItem="{Binding InputPortPosition, Mode=TwoWay}"/>
                                </StackPanel>
                                <StackPanel Grid.Column="1" Margin="8,0,0,0">
                                    <TextBlock Text="Port OUT" Foreground="{DynamicResource TextBrush}"
                                               FontSize="12" Opacity="0.8" Margin="0,0,0,4"/>
                                    <ComboBox Height="32" Style="{DynamicResource BaseComboBox}"
                                              ItemsSource="{Binding PortPositionOptions}"
                                              SelectedItem="{Binding OutputPortPosition, Mode=TwoWay}"/>
                                </StackPanel>
                            </Grid>

                            <!-- ReuseRoutes -->
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
                                                    <TextBlock Text="Node IN"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="12" Opacity="0.7" Margin="0,0,0,4"/>
                                                    <TextBlock Text="{Binding IncomingNodeTitle}"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="13" FontWeight="SemiBold"
                                                               TextTrimming="CharacterEllipsis"/>
                                                </StackPanel>
                                                <StackPanel Grid.Column="1">
                                                    <TextBlock Text="Node OUT"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="12" Opacity="0.7" Margin="0,0,0,4"/>
                                                    <controls:NodeSearchComboBoxUserControl Height="32"
                                                              ItemsSource="{Binding OutgoingOptions}"
                                                              SelectedValuePath="NodeId" DisplayMemberPath="Title"
                                                              SelectedValue="{Binding SelectedOutgoingNodeId, Mode=TwoWay}"
                                                              PlaceholderText="Chọn node OUT..."/>
                                                </StackPanel>
                                                <StackPanel Grid.Column="2">
                                                    <TextBlock Text="Kiểu line OUT"
                                                               Foreground="{DynamicResource TextBrush}"
                                                               FontSize="12" Opacity="0.7" Margin="0,0,0,4"/>
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

**x:Name bắt buộc** (base dùng `FindName` để tìm):

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
| `{DynamicResource TextSecondary}` | Text phụ / mô tả | `Foreground="#CCCCCC"` |
| `{DynamicResource WindowBackground}` | Background card/panel | `Background="#FF1E293B"` |
| `{DynamicResource ControlBorderBrush}` | BorderBrush controls | `BorderBrush="#33FFFFFF"` |
| `{DynamicResource BaseTextBoxV2}` | Style TextBox | — |
| `{DynamicResource BaseComboBox}` | Style ComboBox | — |
| `{DynamicResource PrimaryButton}` | Style nút Play | — |
| `{DynamicResource DangerButton}` | Style nút Close | — |
| `{StaticResource HttpTabItemStyle}` | Style TabItem | — (**StaticResource**, không phải Dynamic) |

**Dialog sizing:**
```xml
<local:BaseNodeDialog Width="460" MinWidth="350" MaxWidth="900" MinHeight="350">
<!-- ⚠️ KHÔNG đặt Height cứng — NodeDialogManager auto-size 90% screen -->
<!-- ⚠️ KHÔNG đặt MaxHeight cố định -->
```

---

## Reference Implementations

Xem các file sau làm mẫu thực tế:
- `Views/Overlays/DelayNodeDialog.xaml` - Dialog mẫu chuẩn theme + responsive
- `Views/Overlays/AssignDataNodeDialog.xaml` - Dialog với custom property handlers
- `Views/Overlays/CodeNodeDialog.xaml` - Dynamic Input mapping + Dynamic Output keys
