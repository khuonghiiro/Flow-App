using FlowMy.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Services.Interaction;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    public static class BreakNodeControl
    {
        public static Border CreateBorder(WorkflowNode node, Window ownerWindow, IWorkflowEditorHost? host = null)
        {
            const double size = 60; // Kích thước hình tròn

            var grid = new Grid
            {
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Icon SVG sử dụng SvgViewboxEx
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "circle-stop duotone", System.Globalization.CultureInfo.CurrentCulture) as Uri;

            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 25,
                Height = 25,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = new SolidColorBrush(Colors.White)
            };
            grid.Children.Add(iconSvg);

            var border = new Border
            {
                Child = grid,
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Color.FromRgb(255, 100, 100)), // Redish
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(size / 2), // Hình tròn hoàn hảo
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    BlurRadius = 10,
                    Opacity = 0.5
                },
                Tag = node
            };
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
