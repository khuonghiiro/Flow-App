using FlowMy.Models;
using FlowMy.Services.Interaction;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowMy.Views.Overlays;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// Custom Border cho Screen Position Picker Node
    /// </summary>
    public static class ScreenPositionPickerNodeControl
    {
        /// <summary>
        /// Táº¡o Border cho Screen Position Picker Node vá»›i UI Ä‘áº·c biá»‡t
        /// </summary>
        public static Border CreateBorder(ScreenPositionPickerNode node, Window ownerWindow, IWorkflowEditorHost? host = null)
        {
            const double nodeWidth = 200;
            const double nodeHeight = 120;

            var border = new Border
            {
                Width = nodeWidth,
                Height = nodeHeight,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    BlurRadius = 10,
                    Opacity = 0.5
                },
                Tag = node
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headerTitle = new TextBlock
            {
                Text = node.Title,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.White)
            };

            // âœ… Cho phÃ©p nÃºt "âœŽ sá»­a tiÃªu Ä‘á»" update Ä‘Æ°á»£c UI
            node.TitleTextBlockUI = headerTitle;

            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Child = headerTitle
            };
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            var contentStack = new StackPanel
            {
                Margin = new Thickness(10, 5, 10, 5),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(contentStack, 1);

            var coordinateTextBox = new TextBox
            {
                Text = node.PositionText,
                IsReadOnly = true,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                Padding = new Thickness(5, 3, 5, 3),
                Margin = new Thickness(0, 0, 0, 5),
                Tag = "CoordinateTextBox"
            };
            contentStack.Children.Add(coordinateTextBox);

            var pickButton = new Button
            {
                Content = "ðŸŽ¯ Chá»n vá»‹ trÃ­",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0)
            };

            var buttonStyle = new Style(typeof(Button));
            buttonStyle.Setters.Add(new Setter(Button.TemplateProperty, CreateButtonTemplate()));
            pickButton.Style = buttonStyle;

            pickButton.Click += (s, e) =>
            {
                e.Handled = true;
                ShowPositionPicker(node, ownerWindow, coordinateTextBox);
            };

            pickButton.MouseEnter += (s, e) =>
            {
                pickButton.Background = new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));
            };
            pickButton.MouseLeave += (s, e) =>
            {
                pickButton.Background = new SolidColorBrush(Color.FromArgb(255, 59, 130, 246));
            };

            contentStack.Children.Add(pickButton);
            mainGrid.Children.Add(contentStack);

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
                        Key.Left  => PortPosition.Left,
                        Key.Up    => PortPosition.Top,
                        Key.Right => PortPosition.Right,
                        Key.Down  => PortPosition.Bottom,
                        _ => null
                    };
                    if (newPos == null) return;
                    e.Handled = true;
                    ChangePortPosition(node, newPos.Value, isShift ? false : true, host);
                };
            }

            return border;
        }

        private static ControlTemplate CreateButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

            var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentPresenterFactory);
            template.VisualTree = borderFactory;
            return template;
        }

        private static void ShowPositionPicker(ScreenPositionPickerNode node, Window ownerWindow, TextBox coordinateTextBox)
        {
            ownerWindow.Hide();

            try
            {
                var overlay = new ScreenPositionPickerOverlay();
                var result = overlay.ShowDialog();

                if (result == true && overlay.SelectedPosition.HasValue)
                {
                    node.SelectedPosition = overlay.SelectedPosition.Value;
                    coordinateTextBox.Text = node.PositionText;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lá»—i khi chá»n vá»‹ trÃ­: {ex.Message}", "Lá»—i",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ownerWindow.Show();
                ownerWindow.Activate();
            }
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
