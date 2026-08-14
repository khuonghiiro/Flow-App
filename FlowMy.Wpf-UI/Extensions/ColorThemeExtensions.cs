using FlowMy.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace FlowMy.Extensions
{
    /// <summary>
    /// Extension method để dễ dàng thêm màu mới
    /// </summary>
    public static class ColorThemeExtensions
    {
        public static void RegisterColorTheme(string themeName,
            Color normalColor,
            Color hoverColor,
            Color pressedColor,
            Color textOnColor)
        {
            var resources = Application.Current.Resources;

            resources[$"{themeName}Brush"] = new SolidColorBrush(normalColor);
            resources[$"{themeName}HoverBrush"] = new SolidColorBrush(hoverColor);
            resources[$"{themeName}PressedBrush"] = new SolidColorBrush(pressedColor);
            resources[$"{themeName}TextOnBrush"] = new SolidColorBrush(textOnColor);
        }
    }
}

//ví dụ sử dụng
//// Đăng ký thêm màu tùy chỉnh ở App.xaml.cs
//ColorThemeExtensions.RegisterColorTheme("Custom",
//    Color.FromRgb(255, 87, 34),   // Normal
//    Color.FromRgb(230, 74, 25),   // Hover  
//    Color.FromRgb(191, 54, 12),   // Pressed
//    Colors.White);                // Text


//// Cách sử dụng trong App.xaml.cs để đăng ký màu mới
//public partial class App : Application
//{
//    protected override void OnStartup(StartupEventArgs e)
//    {
//        base.OnStartup(e);

//        // Đăng ký thêm màu tùy chỉnh
//        ColorThemeExtensions.RegisterColorTheme("Pink",
//            Color.FromRgb(233, 30, 99),   // Normal
//            Color.FromRgb(194, 24, 91),   // Hover  
//            Color.FromRgb(173, 20, 87),   // Pressed
//            Colors.White);                // Text
//    }
//}

//hoặc ở theme thêm dạng với màu bất kỳ

//    <SolidColorBrush x:Key="{Tên màu cần tuỳ chỉnh}Brush" Color="#0DCAF0"/>
//    <SolidColorBrush x:Key="{Tên màu cần tuỳ chỉnh}HoverBrush" Color="#3DD5F3"/>
//    <SolidColorBrush x:Key="{Tên màu cần tuỳ chỉnh}PressedBrush" Color="#0AA2C0"/>
//    <SolidColorBrush x:Key="TextOn{Tên màu cần tuỳ chỉnh}Brush" Color="#000"/>

//ví dụ
//<!-- Để thêm màu Pink, chỉ cần thêm 4 dòng resource: -->
//<!--
//<SolidColorBrush x:Key="PinkBrush" Color="#E91E63"/>
//<SolidColorBrush x:Key="PinkHoverBrush" Color="#C2185B"/>
//<SolidColorBrush x:Key="PinkPressedBrush" Color="#AD1457"/>
//<SolidColorBrush x:Key="TextOnPinkBrush" Color="White"/>

//giao diện wpf thêm xmlns: helper = "clr-namespace:FlowMy.Helpers" chỗ <Window
//     
//    <Button Width="30" Height="30" Padding="0" Margin="0,0,5,0"
//            Content="icon"
//            helper:ButtonHelper.ColorTheme = "{Tên màu cần tuỳ chỉnh}"
//            Style = "{StaticResource DynamicOutlineCircleButton}" />

