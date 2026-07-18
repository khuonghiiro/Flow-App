using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FlowMy.Views.NodeControls
{
    public static class DynamicUiNodeControl
    {
        public static Border CreateBorder(DynamicUiNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // --- 1. BORDER ---
            var border = new Border
            {
                Width = Math.Max(280, node.Width),
                Height = Math.Max(200, node.Height),
                MinWidth = 280,
                MinHeight = 200,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black, Direction = 270,
                    ShadowDepth = 5, BlurRadius = 10, Opacity = 0.5
                },
                Tag = node
            };

            // --- 2. GRID CONTAINER ---
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });               // Row 0: Top Bar
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 1: Sciter UI
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });               // Row 2: Bottom Bar
            border.Child = grid;

            // --- 3. SCITER EMBEDDED CONTROL ---
            var sciterControl = new SciterEmbeddedControl(node);
            Grid.SetRow(sciterControl, 1);
            grid.Children.Add(sciterControl);

            // --- 4. FLOATING CANVAS TITLE (managed by BaseNodeControlHelper, NOT added to grid) ---
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Dynamic UI Form",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                    node.TitleColorMode, node.TitleColorKey, node.NodeBrush),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false,
                Visibility = node.TitleDisplayMode == TitleDisplayMode.Always
                    ? Visibility.Visible : Visibility.Collapsed
            };
            node.TitleTextBlockUI = titleTextBlock;

            // --- 5. TOP BAR (Inside the node grid) ---
            var topBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Padding = new Thickness(6, 4, 6, 4),
                VerticalAlignment = VerticalAlignment.Top
            };
            var topBarGrid = new Grid();
            var topBarText = new TextBlock
            {
                Text = node.Title ?? "Dynamic UI Form",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            topBarGrid.Children.Add(topBarText);
            topBar.Child = topBarGrid;
            Grid.SetRow(topBar, 0);
            grid.Children.Add(topBar);

            // --- 6. BOTTOM BAR ---
            var bottomBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Padding = new Thickness(6, 4, 6, 4),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            var bottomText = new TextBlock
            {
                Text = "Dynamic UI • Chuột phải để mở cấu hình",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            bottomBar.Child = bottomText;
            Grid.SetRow(bottomBar, 2);
            grid.Children.Add(bottomBar);

            // --- 7. PROPERTY SYNCS AND PROPERTY CHANGED HANDLERS ---
            node.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DynamicUiNode.HtmlCode) ||
                    e.PropertyName == nameof(DynamicUiNode.CssCode) ||
                    e.PropertyName == nameof(DynamicUiNode.JsCode))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        sciterControl.UpdateContent();
                    });
                }
                else if (e.PropertyName == nameof(DynamicUiNode.Width))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        border.Width = Math.Max(280, node.Width);
                    });
                }
                else if (e.PropertyName == nameof(DynamicUiNode.Height))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        border.Height = Math.Max(200, node.Height);
                    });
                }
                else if (e.PropertyName == nameof(DynamicUiNode.Title))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        topBarText.Text = node.Title ?? "Dynamic UI Form";
                    });
                }
                else if (e.PropertyName == nameof(DynamicUiNode.PendingReadDom) && node.PendingReadDom)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            sciterControl.UpdateOutputsFromDom(node.ParamsCode, node.ResolvedOutputs);
                        }
                        finally
                        {
                            node.PendingReadDom = false;
                        }
                    });
                }
            };

            // --- 8. FLUENT API INITIALIZATION ---
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()      
                .WithHoverBehavior()        
                .WithKeyboardPorts()        
                .WithCleanup()             
                .WithVisibilitySync()      
                .WithCanvasIntegration()   
                .WithDialogSupport(ctx => new DynamicUiNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .Build();                  

            return border;
        }
    }
}
