using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.NodeControls
{
    public static class ScreenCaptureNodeControl
    {
        public static Border CreateBorder(ScreenCaptureNode node, Window ownerWindow, IWorkflowEditorHost? host = null)
        {
            var border = new Border
            {
                Width = 210,
                Height = 170,
                Background = node.NodeBrush,
                Cursor = Cursors.Hand,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    BlurRadius = 10,
                    Opacity = 0.5
                },
                Tag = node,
                Padding = new Thickness(8)
            };

            var mainGrid = new Grid { Margin = new Thickness(0) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                              // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });         // Preview
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                              // Info
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                              // Button

            // === HEADER ===
            var headerTextBrush = GetTextBrushFromColorKey(node.ColorKey) ?? new SolidColorBrush(Colors.White);
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 6)
            };

            var headerIcon = new TextBlock
            {
                Text = "📸",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = headerTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };

            var headerTitle = new TextBlock
            {
                Text = node.Title,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = headerTextBrush,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Allow "✎ edit title" button to update the UI
            node.TitleTextBlockUI = headerTitle;

            headerPanel.Children.Add(headerIcon);
            headerPanel.Children.Add(headerTitle);
            Grid.SetRow(headerPanel, 0);
            mainGrid.Children.Add(headerPanel);

            // === PREVIEW IMAGE ===
            var previewBorder = new Border
            {
                Margin = new Thickness(6, 0, 6, 4),
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var previewImage = new Image
            {
                Stretch = Stretch.Uniform,
                MaxHeight = 70,
                MaxWidth = 180
            };

            // Zoom image on hover using ScaleTransform (does not resize the node)
            previewImage.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            var scaleTransform = new ScaleTransform(1, 1);
            previewImage.RenderTransform = scaleTransform;

            previewImage.MouseEnter += (s, e) => { scaleTransform.ScaleX = 1.2; scaleTransform.ScaleY = 1.2; };
            previewImage.MouseLeave += (s, e) => { scaleTransform.ScaleX = 1.0; scaleTransform.ScaleY = 1.0; };

            // Click to view full size
            previewImage.Cursor = Cursors.Hand;
            previewImage.MouseDown += (s, e) =>
            {
                if (node.CapturedImage != null)
                {
                    var viewer = new Window
                    {
                        Title = "Preview - Screen Capture",
                        Width = Math.Min(node.CaptureWidth, 1200),
                        Height = Math.Min(node.CaptureHeight, 800),
                        Content = new Image { Source = node.CapturedImage, Stretch = Stretch.Uniform },
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        WindowStyle = WindowStyle.ToolWindow
                    };
                    viewer.ShowDialog();
                }
                e.Handled = true;
            };

            var placeholderText = new TextBlock
            {
                Text = "Chưa có ảnh preview",
                FontSize = 11,
                Foreground = headerTextBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10)
            };

            void UpdatePreview()
            {
                if (node.CapturedImage != null)
                {
                    previewImage.Source = node.CapturedImage;
                    previewBorder.Child = previewImage;
                }
                else
                {
                    previewBorder.Child = placeholderText;
                }
            }

            UpdatePreview();
            node.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(node.CapturedImage))
                    UpdatePreview();
            };

            Grid.SetRow(previewBorder, 1);
            mainGrid.Children.Add(previewBorder);

            // === CAPTURE REGION INFO ===
            var regionText = new TextBlock
            {
                FontSize = 10,
                Foreground = headerTextBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 0, 10, 5)
            };

            void UpdateRegionText()
            {
                regionText.Text = node.HasCaptureRegion
                    ? $"{node.CaptureWidth} × {node.CaptureHeight} tại ({node.CaptureX}, {node.CaptureY})"
                    : "Chưa chọn vùng chụp";
            }

            UpdateRegionText();
            node.PropertyChanged += (s, e) => UpdateRegionText();

            Grid.SetRow(regionText, 2);
            mainGrid.Children.Add(regionText);

            // === PICK BUTTON ===
            var pickButton = new Button
            {
                Content = node.HasCaptureRegion ? "📸 Chụp lại" : "📸 Chụp vùng màn hình",
                Height = 32,
                Width = 150,
                Style = Application.Current.FindResource("PrimaryButton") as Style,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Cursor = Cursors.Hand
            };

            pickButton.Click += (s, e) =>
            {
                ownerWindow.Hide();
                try
                {
                    var overlay = new ScreenCaptureOverlay();
                    var result = overlay.ShowDialog();
                    if (result == true)
                    {
                        node.CaptureX = overlay.CaptureX;
                        node.CaptureY = overlay.CaptureY;
                        node.CaptureWidth = overlay.CaptureWidth;
                        node.CaptureHeight = overlay.CaptureHeight;
                        node.CapturedImage = overlay.CapturedImage;
                        pickButton.Content = "📸 Chụp lại";
                    }
                }
                finally
                {
                    ownerWindow.Show();
                    ownerWindow.Activate();
                }
            };

            Grid.SetRow(pickButton, 3);
            mainGrid.Children.Add(pickButton);

            border.Child = mainGrid;

            // --- Use fluent API for hover behavior and keyboard ports ---
            // This node has an embedded title (not a floating canvas title), so we skip
            // WithTitleManagement() and WithCanvasIntegration(). We use a dummy TextBlock
            // for the fluent API context but keep the real title management inline above.
            if (host != null)
            {
                // Node-specific property handlers for NodeBrush changes
                var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
                {
                    [nameof(WorkflowNode.NodeBrush)] = ctx =>
                    {
                        border.Background = node.NodeBrush;
                    }
                };

                // Use a placeholder TextBlock for the fluent API (this node manages its own title inline)
                var dummyTitle = new TextBlock { Visibility = Visibility.Collapsed, IsHitTestVisible = false };

                BaseNodeControlHelper
                    .Initialize(border, dummyTitle, node, host)
                    .WithHoverBehavior()
                    .WithKeyboardPorts()
                    .WithPropertySync(customPropertyHandlers)
                    .WithCleanup()
                    .Build();
            }

            return border;
        }

        private static Brush? GetTextBrushFromColorKey(string? colorKey)
        {
            if (string.IsNullOrEmpty(colorKey)) return null;
            try
            {
                return Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush;
            }
            catch { return null; }
        }
    }
}
