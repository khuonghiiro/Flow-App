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

            // --- 2. SCITER EMBEDDED CONTROL ---
            var sciterControl = new SciterEmbeddedControl(node);

            // --- 3. GRID CONTAINER ---
            var grid = new Grid();
            grid.Children.Add(sciterControl);
            border.Child = grid;

            // --- 4. TITLE TEXTBLOCK (Floating above node on canvas) ---
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

            // --- 5. PROPERTY SYNCS AND PROPERTY CHANGED HANDLERS ---
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
            };

            // --- 6. FLUENT API INITIALIZATION ---
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()      
                .WithHoverBehavior()        
                .WithKeyboardPorts()        
                .WithCleanup()             
                .WithVisibilitySync()      
                .WithCanvasIntegration()   
                .Build();                  

            return border;
        }
    }
}
