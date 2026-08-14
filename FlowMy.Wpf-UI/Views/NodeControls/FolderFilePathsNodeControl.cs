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
    public static class FolderFilePathsNodeControl
    {
        public static Border CreateBorder(FolderFilePathsNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            // --- Create UI elements (node-specific) ---

            var grid = new Grid { MinWidth = 60, MinHeight = 60, Width = 60, Height = 60 };

            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(string.Empty, typeof(Uri), "file-import duotone-light",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;

            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = GetIconBrush(node.ColorKey)
            };
            grid.Children.Add(iconSvg);

            var border = new Border
            {
                Child = grid,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    BlurRadius = 10,
                    Opacity = 0.5
                },
                Tag = node
            };

            var titleTextBlock = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(node.Title) ? "File trong thư mục" : node.Title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                    node.TitleColorMode,
                    node.TitleColorKey,
                    node.NodeBrush),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false,
                Visibility = node.TitleDisplayMode == TitleDisplayMode.Always
                    ? Visibility.Visible
                    : Visibility.Collapsed
            };
            node.TitleTextBlockUI = titleTextBlock;

            // --- Node-specific property handler: update icon fill when ColorKey changes ---
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                [nameof(WorkflowNode.ColorKey)] = ctx =>
                {
                    iconSvg.Fill = GetIconBrush(node.ColorKey);
                },
                [nameof(WorkflowNode.NodeBrush)] = ctx =>
                {
                    border.Background = node.NodeBrush;
                    iconSvg.Fill = GetIconBrush(node.ColorKey);
                    ctx.TitleTextBlock.Foreground = BaseNodeControlHelper.ResolveTitleBrush(
                        node.TitleColorMode,
                        node.TitleColorKey,
                        node.NodeBrush);
                }
            };

            // --- Initialize with fluent API ---
            BaseNodeControlHelper
                .Initialize(border, titleTextBlock, node, host)
                .WithTitleManagement()
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new FolderFilePathsNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .WithVisibilitySync()
                .WithCanvasIntegration()
                .Build();

            // Double-click also opens dialog
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    OpenNodeDialog(node, host, ownerWindow);
                }
            };

            return border;
        }

        private static Brush GetIconBrush(string? colorKey)
        {
            if (string.IsNullOrWhiteSpace(colorKey))
                return new SolidColorBrush(Color.FromRgb(148, 163, 184));
            return Application.Current.Resources.Contains($"TextOn{colorKey}Brush")
                ? (Brush)Application.Current.Resources[$"TextOn{colorKey}Brush"]
                : Brushes.White;
        }

        private static void OpenNodeDialog(FolderFilePathsNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                if (node.Border != null && node.Border.IsMouseCaptured)
                    node.Border.ReleaseMouseCapture();
                host.DraggedNode = null;
                if (host.ViewModel != null)
                    host.ViewModel.SelectedNode = null;

                var dialogManager = GetOrCreateDialogManager(host);
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode == node) return;
                if (dialogManager.IsDialogOpen && dialogManager.CurrentNode != node)
                    dialogManager.CloseCurrentDialog();

                var dialog = new FolderFilePathsNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
                dialogManager.OpenDialog(node, dialog, host);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dialog error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static NodeDialogManager GetOrCreateDialogManager(IWorkflowEditorHost host)
        {
            if (host is WorkflowEditorWindow window)
            {
                var field = typeof(WorkflowEditorWindow).GetField("_nodeDialogManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(window) is NodeDialogManager manager) return manager;
            }
            return new NodeDialogManager();
        }
    }
}
