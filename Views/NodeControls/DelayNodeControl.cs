using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views;
using FlowMy.Views.Overlays;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views.NodeControls
{
    public static class DelayNodeControl
    {
        private static readonly System.Collections.Generic.Dictionary<Border, System.Windows.Threading.DispatcherTimer> _titleUpdateTimers = new();
        private const int TitleUpdateThrottleMs = 50;
        private static readonly System.Collections.Generic.Dictionary<Border, bool> _titleUpdatedAfterZoom = new();

        public static Border CreateBorder(DelayNode node, Window? ownerWindow, IWorkflowEditorHost? host = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            var grid = new Grid
            {
                MinWidth = 80,
                MinHeight = 80
            };

            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            // Header: icon + 2 dòng text căn giữa (số + label đơn vị)
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10, 6, 10, 4)
            };

            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(
                null,
                typeof(Uri),
                "timer regular",
                CultureInfo.CurrentCulture) as Uri;

            var iconSvg = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = GetTextBrush(node.ColorKey)
            };

            var summaryNumberText = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = GetTextBrush(node.ColorKey),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var summaryLabelText = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetTextBrush(node.ColorKey),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            headerPanel.Children.Add(iconSvg);
            headerPanel.Children.Add(summaryNumberText);
            headerPanel.Children.Add(summaryLabelText);

            Grid.SetRow(headerPanel, 0);
            grid.Children.Add(headerPanel);

            static string FormatNumber(double value)
            {
                var rounded = Math.Round(value);
                if (Math.Abs(value - rounded) < 0.0000001d)
                    return rounded.ToString(CultureInfo.CurrentCulture);
                return value.ToString("0.###", CultureInfo.CurrentCulture);
            }

            static string GetUnitLabel(DelayTimeUnit unit)
            {
                return unit switch
                {
                    DelayTimeUnit.Milliseconds => "ms",
                    DelayTimeUnit.Seconds => "Giây",
                    DelayTimeUnit.Minutes => "Phút",
                    DelayTimeUnit.Hours => "Giờ",
                    _ => "Giây"
                };
            }

            void SyncSummary()
            {
                switch (node.TimingMode)
                {
                    case DelayTimingMode.None:
                        summaryNumberText.Text = FormatNumber(node.DelayValue);
                        summaryLabelText.Text = GetUnitLabel(node.DelayUnit);
                        break;
                    case DelayTimingMode.Random:
                        summaryNumberText.Text = $"{FormatNumber(node.RandomMinValue)}–{FormatNumber(node.RandomMaxValue)}";
                        summaryLabelText.Text = $"{GetUnitLabel(node.DelayUnit)} · ngẫu nhiên";
                        break;
                    case DelayTimingMode.NodeKey:
                        summaryNumberText.Text = "Key";
                        summaryLabelText.Text = string.IsNullOrWhiteSpace(node.DelaySourceOutputKey)
                            ? $"({GetUnitLabel(node.DelayUnit)})"
                            : $"{node.DelaySourceOutputKey} · {GetUnitLabel(node.DelayUnit)}";
                        break;
                    default:
                        summaryNumberText.Text = FormatNumber(node.DelayValue);
                        summaryLabelText.Text = GetUnitLabel(node.DelayUnit);
                        break;
                }
            }

            SyncSummary();

            node.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DelayNode.DelayValue) ||
                    e.PropertyName == nameof(DelayNode.DelayUnit) ||
                    e.PropertyName == nameof(DelayNode.DelayMilliseconds) ||
                    e.PropertyName == nameof(DelayNode.TimingMode) ||
                    e.PropertyName == nameof(DelayNode.RandomMinValue) ||
                    e.PropertyName == nameof(DelayNode.RandomMaxValue) ||
                    e.PropertyName == nameof(DelayNode.DelaySourceNodeId) ||
                    e.PropertyName == nameof(DelayNode.DelaySourceOutputKey))
                {
                    SyncSummary();
                }
                else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                {
                    // Refresh icon/text fill when theme changes
                    iconSvg.Fill = GetTextBrush(node.ColorKey);
                    summaryNumberText.Foreground = GetTextBrush(node.ColorKey);
                    summaryLabelText.Foreground = GetTextBrush(node.ColorKey);
                }
            };

            var border = new Border
            {
                Child = grid,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    BlurRadius = 10,
                    Opacity = 0.5
                }
            };

            // ========== TITLE (Phase 2: TitleDisplayMode) ==========
            var titleTextBlock = new TextBlock
            {
                Text = node.Title ?? "Delay",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetTitleBrush(node),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Visibility = GetTitleVisibility(node.TitleDisplayMode, false),
                IsHitTestVisible = false
            };
            node.TitleTextBlockUI = titleTextBlock;

            border.Focusable = true;
            border.FocusVisualStyle = null;

            bool isHovering = false;
            border.MouseEnter += (s, e) =>
            {
                isHovering = true;
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                {
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
                Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => { if (isHovering) border.Focus(); }));
            };
            border.MouseLeave += (s, e) =>
            {
                isHovering = false;
                if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
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

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowNode.Title))
                    {
                        titleTextBlock.Text = node.Title ?? "Delay";
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                            UpdateTitlePosition(titleTextBlock, border, host);
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.NodeBrush))
                    {
                        border.Background = node.NodeBrush;
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(DelayNode.TitleColorMode) ||
                             e.PropertyName == nameof(DelayNode.TitleColorKey))
                    {
                        titleTextBlock.Foreground = GetTitleBrush(node);
                    }
                    else if (e.PropertyName == nameof(DelayNode.TitleDisplayMode))
                    {
                        if (node.Border != null && node.Border.Visibility == Visibility.Visible)
                            UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    }
                };
            }

            var visibilityDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(Border));
            visibilityDescriptor?.AddValueChanged(border, (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                    titleTextBlock.Visibility = Visibility.Collapsed;
                else
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
            });

            border.Loaded += (s, e) =>
            {
                if (host.WorkflowCanvas != null && !host.WorkflowCanvas.Children.Contains(titleTextBlock))
                {
                    host.WorkflowCanvas.Children.Add(titleTextBlock);
                    Panel.SetZIndex(titleTextBlock, 20000);
                    UpdateTitlePosition(titleTextBlock, border, host);
                }
            };

            border.SizeChanged += (s, e) => UpdateTitlePosition(titleTextBlock, border, host);

            border.Unloaded += (s, e) =>
            {
                try
                {
                    if (_titleUpdateTimers.TryGetValue(border, out var timer))
                    {
                        timer.Stop();
                        _titleUpdateTimers.Remove(border);
                    }
                    _titleUpdatedAfterZoom.Remove(border);
                    if (host.WorkflowCanvas != null && host.WorkflowCanvas.Children.Contains(titleTextBlock))
                        host.WorkflowCanvas.Children.Remove(titleTextBlock);
                    if (ReferenceEquals(node.TitleTextBlockUI, titleTextBlock))
                        node.TitleTextBlockUI = null;
                }
                catch { }
            };

            border.LayoutUpdated += (s, e) =>
            {
                if (border.Visibility != Visibility.Visible)
                {
                    titleTextBlock.Visibility = Visibility.Collapsed;
                    return;
                }
                if (Services.Rendering.NodeChrome.IsZooming)
                {
                    if (titleTextBlock.Visibility != Visibility.Collapsed)
                        titleTextBlock.Visibility = Visibility.Collapsed;
                    _titleUpdatedAfterZoom[border] = false;
                    return;
                }
                bool hasUpdated = _titleUpdatedAfterZoom.TryGetValue(border, out var u) && u;
                if (!hasUpdated && border.Visibility == Visibility.Visible)
                {
                    _titleUpdatedAfterZoom[border] = true;
                    UpdateTitleVisibility(titleTextBlock, node.TitleDisplayMode, isHovering, border);
                    if (titleTextBlock.Visibility == Visibility.Visible)
                        UpdateTitlePosition(titleTextBlock, border, host);
                }
                if (host.IsPanning || host.DraggedNode == node) return;
                if (titleTextBlock.Visibility == Visibility.Visible)
                    ThrottledUpdateTitlePosition(titleTextBlock, border, host);
            };

            // Right click: open dialog (set time)
            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
            };

            return border;
        }

        private static Visibility GetTitleVisibility(TitleDisplayMode mode, bool isHovering)
        {
            return mode switch
            {
                TitleDisplayMode.Hidden => Visibility.Collapsed,
                TitleDisplayMode.Hover => isHovering ? Visibility.Visible : Visibility.Collapsed,
                TitleDisplayMode.Always => Visibility.Visible,
                _ => Visibility.Collapsed
            };
        }

        private static void UpdateTitleVisibility(TextBlock titleTextBlock, TitleDisplayMode mode, bool isHovering, Border? nodeBorder = null)
        {
            if (nodeBorder != null && nodeBorder.Visibility != Visibility.Visible)
            {
                titleTextBlock.Visibility = Visibility.Collapsed;
                return;
            }
            titleTextBlock.Visibility = GetTitleVisibility(mode, isHovering);
        }

        private static void ThrottledUpdateTitlePosition(TextBlock titleTextBlock, Border border, IWorkflowEditorHost host)
        {
            if (!_titleUpdateTimers.TryGetValue(border, out var timer))
            {
                timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TitleUpdateThrottleMs) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    UpdateTitlePosition(titleTextBlock, border, host);
                };
                _titleUpdateTimers[border] = timer;
            }
            timer.Stop();
            timer.Start();
        }

        private static void UpdateTitlePosition(TextBlock titleTextBlock, Border border, IWorkflowEditorHost host)
        {
            if (host.WorkflowCanvas == null || border == null || !host.WorkflowCanvas.Children.Contains(titleTextBlock)) return;
            var left = Canvas.GetLeft(border);
            var top = Canvas.GetTop(border);
            if (double.IsNaN(left) || double.IsNaN(top)) return;
            if (titleTextBlock.ActualWidth == 0 || titleTextBlock.ActualHeight == 0)
            {
                titleTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                titleTextBlock.Arrange(new Rect(titleTextBlock.DesiredSize));
            }
            var titleLeft = left + (border.ActualWidth / 2) - (titleTextBlock.ActualWidth / 2);
            var titleTop = top - titleTextBlock.ActualHeight - 4;
            Canvas.SetLeft(titleTextBlock, titleLeft);
            Canvas.SetTop(titleTextBlock, titleTop);
        }

        private static void OpenNodeDialog(DelayNode node, IWorkflowEditorHost host, Window? ownerWindow)
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

                var dialog = new DelayNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
                dialogManager.OpenDialog(node, dialog, host);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dialog error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static Brush GetTitleBrush(DelayNode node)
        {
            if (node.TitleColorMode != TitleColorMode.CustomColor ||
                string.IsNullOrEmpty(node.TitleColorKey) ||
                node.TitleColorKey == "NodeColor")
            {
                return node.NodeBrush;
            }

            if (node.TitleColorKey == "LimeGreen")
            {
                return new SolidColorBrush(Colors.LimeGreen);
            }

            var brush = Application.Current.TryFindResource(node.TitleColorKey) as Brush;
            return brush ?? node.NodeBrush;
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

        private static Brush GetTextBrush(string? colorKey)
        {
            if (string.IsNullOrWhiteSpace(colorKey))
                return Brushes.White;

            var resourceKey = $"TextOn{colorKey}Brush";
            if (Application.Current.Resources.Contains(resourceKey))
                return (Brush)Application.Current.Resources[resourceKey];

            return Brushes.White;
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

