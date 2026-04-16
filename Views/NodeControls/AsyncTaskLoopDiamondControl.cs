using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views;
using FlowMy.Views.Overlays;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// Header hÃ¬nh thoi cho AsyncTask á»Ÿ cháº¿ Ä‘á»™ giao diá»‡n giá»‘ng Loop.
    /// </summary>
    public static class AsyncTaskLoopDiamondControl
    {
        public static Border CreateBorder(AsyncTaskNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            const double diamondWidth = 100;
            const double diamondHeight = 100;

            var diamond = new Polygon
            {
                Points = new PointCollection(new[]
                {
                    new Point(diamondWidth / 2, 0),
                    new Point(diamondWidth, diamondHeight / 2),
                    new Point(diamondWidth / 2, diamondHeight),
                    new Point(0, diamondHeight / 2)
                }),
                Fill = node.NodeBrush,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2,
                Stretch = Stretch.Fill
            };

            var grid = new Grid
            {
                Width = diamondWidth,
                Height = diamondHeight,
                MinWidth = diamondWidth,
                MinHeight = diamondHeight,
                ClipToBounds = false
            };
            grid.Children.Add(diamond);

            var iconConverter = new IconKeyToPathConverter();
            // Keep icon consistent with other mint/diagram icons in the editor.
            var iconUri = iconConverter.Convert(null, typeof(Uri), "diagram-project duotone-light", System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 28,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0),
                Fill = Application.Current.TryFindResource("TextOnMintChocolateBrush") as Brush ?? Brushes.White
            };
            grid.Children.Add(iconSvg);

            var titleBrush = Application.Current.TryFindResource("TextOnMintChocolateBrush") as Brush ?? new SolidColorBrush(Colors.White);
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Async Task",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = titleBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4, 2, 4, 0),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = diamondWidth - 4,
                Visibility = Visibility.Visible,
                IsHitTestVisible = false
            };
            node.TitleTextBlockUI = titleTextBlock;

            var border = new Border
            {
                Child = grid,
                Width = diamondWidth,
                Height = diamondHeight,
                MinWidth = diamondWidth,
                MinHeight = diamondHeight,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ClipToBounds = false,
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

            // Khi kÃ©o (drag) diamond: giáº£m artifact dáº¡ng "ná»n vuÃ´ng" do shadow bitmap.
            // Restores láº¡i shadow sau khi nháº£ chuá»™t.
            var shadowEffect = border.Effect as System.Windows.Media.Effects.DropShadowEffect;
            border.PreviewMouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                    border.Effect = null;
            };
            border.PreviewMouseUp += (s, e) =>
            {
                if (shadowEffect != null)
                    border.Effect = shadowEffect;
            };

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowNode.Title))
                        titleTextBlock.Text = node.Title ?? "Async Task";
                    else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                        diamond.Fill = node.NodeBrush;
                };
            }

            // Keyboard Port Position
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

            border.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                OpenDialog(node, host, ownerWindow);
            };

            return border;
        }

        private static void OpenDialog(AsyncTaskNode node, IWorkflowEditorHost host, Window? ownerWindow)
        {
            try
            {
                if (node.Border != null && node.Border.IsMouseCaptured)
                    node.Border.ReleaseMouseCapture();
                host.DraggedNode = null;
                if (host.ViewModel != null)
                    host.ViewModel.SelectedNode = null;

                var manager = GetOrCreateDialogManager(host);
                if (manager.IsDialogOpen && manager.CurrentNode == node) return;
                if (manager.IsDialogOpen && manager.CurrentNode != node)
                    manager.CloseCurrentDialog();

                var dialog = new AsyncTaskNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
                manager.OpenDialog(node, dialog, host);
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
