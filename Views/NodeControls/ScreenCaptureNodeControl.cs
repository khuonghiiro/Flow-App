using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.Overlays;
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
                Height = 170,  // Gá»n hÆ¡n nhÆ°ng váº«n Ä‘á»§ chá»— cho preview
                Background = node.NodeBrush,
                Cursor = System.Windows.Input.Cursors.Hand,
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

            var mainGrid = new Grid
            {
                Margin = new Thickness(0)
            };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });  // Preview
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Info
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Button

            // === HEADER ===
            // Get text color from theme based on ColorKey (Success â†’ TextOnSuccessBrush)
            var headerTextBrush = GetTextBrushFromColorKey(node.ColorKey) ?? new SolidColorBrush(Colors.White);
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 6)
            };

            var headerIcon = new TextBlock
            {
                Text = "ðŸ“¸",
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

            // âœ… Cho phÃ©p nÃºt "âœŽ sá»­a tiÃªu Ä‘á»" update Ä‘Æ°á»£c UI
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


            // ========================================
            // THÃŠM ÄOáº N NÃ€Y VÃ€O ÄÃ‚Y (SAU KHI Táº O previewImage)
            // ========================================

            // Zoom áº£nh báº±ng ScaleTransform (khÃ´ng lÃ m node thay Ä‘á»•i kÃ­ch thÆ°á»›c)
            previewImage.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            var scaleTransform = new ScaleTransform(1, 1);
            previewImage.RenderTransform = scaleTransform;

            previewImage.MouseEnter += (s, e) =>
            {
                scaleTransform.ScaleX = 1.2;
                scaleTransform.ScaleY = 1.2;
            };

            previewImage.MouseLeave += (s, e) =>
            {
                scaleTransform.ScaleX = 1.0;
                scaleTransform.ScaleY = 1.0;
            };

            // Click Ä‘á»ƒ xem full size
            previewImage.Cursor = System.Windows.Input.Cursors.Hand;
            previewImage.MouseDown += (s, e) =>
            {
                if (node.CapturedImage != null)
                {
                    var viewer = new Window
                    {
                        Title = "Preview - Screen Capture",
                        Width = Math.Min(node.CaptureWidth, 1200),
                        Height = Math.Min(node.CaptureHeight, 800),
                        Content = new Image
                        {
                            Source = node.CapturedImage,
                            Stretch = Stretch.Uniform
                        },
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        WindowStyle = WindowStyle.ToolWindow
                    };
                    viewer.ShowDialog();
                }

                e.Handled = true;  // NgÄƒn trigger drag node
            };

            // ========================================
            // Káº¾T THÃšC PHáº¦N THÃŠM
            // ========================================



            // ========================================
            // THÃŠM TÃ™Y CHá»ŒN NÃ‚NG CAO VÃ€O ÄÃ‚Y
            // ========================================

            // TÃ¹y chá»n 1: Zoom khi hover
            //previewImage.MouseEnter += (s, e) =>
            //{
            //    previewImage.MaxHeight = 450;
            //    previewImage.MaxWidth = 600;
            //};

            //previewImage.MouseLeave += (s, e) =>
            //{
            //    previewImage.MaxHeight = 80;
            //    previewImage.MaxWidth = 200;
            //};

            //// TÃ¹y chá»n 2: Click Ä‘á»ƒ xem full size
            //previewImage.Cursor = System.Windows.Input.Cursors.Hand;
            //previewImage.MouseDown += (s, e) =>
            //{
            //    if (node.CapturedImage != null)
            //    {
            //        var viewer = new Window
            //        {
            //            Title = "Preview - Screen Capture",
            //            Width = Math.Min(node.CaptureWidth, 1200),  // Giá»›i háº¡n max width
            //            Height = Math.Min(node.CaptureHeight, 800), // Giá»›i háº¡n max height
            //            Content = new Image
            //            {
            //                Source = node.CapturedImage,
            //                Stretch = Stretch.Uniform
            //            },
            //            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            //            WindowStyle = WindowStyle.ToolWindow
            //        };
            //        viewer.ShowDialog();
            //    }

            //    // NgÄƒn event bubble lÃªn node (trÃ¡nh trigger drag node)
            //    e.Handled = true;
            //};

            // ========================================
            // Káº¾T THÃšC TÃ™Y CHá»ŒN NÃ‚NG CAO
            // ========================================

            var placeholderText = new TextBlock
            {
                Text = "ChÆ°a cÃ³ áº£nh preview",
                FontSize = 11,
                Foreground = headerTextBrush,  // Use same text color from theme
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10)
            };

            // Cáº­p nháº­t preview khi cÃ³ áº£nh
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
                {
                    UpdatePreview();
                }
            };

            Grid.SetRow(previewBorder, 1);
            mainGrid.Children.Add(previewBorder);

            // === THÃ”NG TIN VÃ™NG CHá»¤P ===
            var regionText = new TextBlock
            {
                FontSize = 10,
                Foreground = headerTextBrush,  // Use same text color from theme
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 0, 10, 5)
            };

            void UpdateRegionText()
            {
                if (node.HasCaptureRegion)
                {
                    regionText.Text = $"{node.CaptureWidth} Ã— {node.CaptureHeight} táº¡i ({node.CaptureX}, {node.CaptureY})";
                }
                else
                {
                    regionText.Text = "ChÆ°a chá»n vÃ¹ng chá»¥p";
                }
            }

            UpdateRegionText();
            node.PropertyChanged += (s, e) => UpdateRegionText();

            Grid.SetRow(regionText, 2);
            mainGrid.Children.Add(regionText);

            // === NÃšT CHá»ŒN VÃ™NG ===
            var pickButton = new Button
            {
                Content = node.HasCaptureRegion ? "ðŸ“¸ Chá»¥p láº¡i" : "ðŸ“¸ Chá»¥p vÃ¹ng mÃ n hÃ¬nh",
                Height = 32,
                Width = 150,
                Style = Application.Current.FindResource("PrimaryButton") as Style,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Cursor = System.Windows.Input.Cursors.Hand
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
                        // LÆ°u thÃ´ng tin vÃ¹ng chá»¥p
                        node.CaptureX = overlay.CaptureX;
                        node.CaptureY = overlay.CaptureY;
                        node.CaptureWidth = overlay.CaptureWidth;
                        node.CaptureHeight = overlay.CaptureHeight;

                        // THÃŠM: LÆ°u áº£nh Ä‘Ã£ chá»¥p
                        node.CapturedImage = overlay.CapturedImage;

                        pickButton.Content = "ðŸ“¸ Chá»¥p láº¡i";
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

            // Keyboard Port Position
            if (host != null)
            {
                border.Focusable = true;
                border.FocusVisualStyle = null;

                bool isHovering = false;
                border.MouseEnter += (s, e) =>
                {
                    isHovering = true;

                    Application.Current.Dispatcher.BeginInvoke(

                        System.Windows.Threading.DispatcherPriority.Input,

                        new Action(() => { if (isHovering) border.Focus(); }));
                };
                border.MouseLeave += (s, e) =>
                {
                    isHovering = false;
                };
                border.PreviewKeyDown += (s, e) =>
                {
                    if (!isHovering) return;
                    bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                    PortPosition? newPos = e.Key switch
                    {
                        Key.Left => PortPosition.Left,
                        Key.Up => PortPosition.Top,
                        Key.Right => PortPosition.Right,
                        Key.Down => PortPosition.Bottom,
                        _ => null
                    };
                    if (newPos == null) return;
                    e.Handled = true;
                    ChangePortPosition(node, newPos.Value, isShift ? false : true, host);
                };
            }

            return border;
        }

        private static Color? GetColorFromTheme(string resourceKey)
        {
            try
            {
                var brush = Application.Current.TryFindResource(resourceKey) as SolidColorBrush;
                return brush?.Color;
            }
            catch
            {
                return null;
            }
        }

        private static Brush? GetBrushFromTheme(string resourceKey)
        {
            try
            {
                return Application.Current.TryFindResource(resourceKey) as Brush;
            }
            catch
            {
                return null;
            }
        }

        private static Brush? GetTextBrushFromColorKey(string? colorKey)
        {
            if (string.IsNullOrEmpty(colorKey))
                return null;

            // Convert ColorKey (e.g., "Success") to TextOn{ColorKey}Brush (e.g., "TextOnSuccessBrush")
            var textBrushKey = $"TextOn{colorKey}Brush";
            return GetBrushFromTheme(textBrushKey);
        }

        private static void ChangePortPosition(
            WorkflowNode node, PortPosition newPosition, bool isInputPort, IWorkflowEditorHost host)
        {
            if (node.Ports == null || node.Ports.Count == 0) return;
            var port = isInputPort
                ? node.Ports.FirstOrDefault(p => p.IsInput)
                : node.Ports.FirstOrDefault(p => !p.IsInput);
            if (port == null || port.Position == newPosition) return;
            port.Position = newPosition;
            host.UpdatePortsPositionOnSide(node, newPosition);
            var cons = host.ViewModel?.Connections;
            if (cons != null && cons.Count > 0)
            {
                try
                {
                    host.ConnectionRenderer.UpdateAllConnectionPaths(cons);
                    host.ConnectionRenderer.UpdateAllConnectionAnimations(cons);
                }
                catch { }
            }
        }
    }
}