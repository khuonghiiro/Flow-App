using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views;
using FlowMy.Views.Overlays;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// Custom UI control builder cho AsyncTask Node.
    /// Hiá»ƒn thá»‹ sá»‘ thá»© tá»± cáº¡nh port out (Task 0, Task 1, ...).
    /// Chuá»™t pháº£i má»Ÿ dialog quáº£n lÃ½ tasks.
    /// </summary>
    public static class AsyncTaskNodeControl
    {
        public static Border CreateBorder(
            WorkflowNode node,
            Window? ownerWindow,
            IWorkflowEditorHost? host,
            Action addTaskBranch,
            Action<AsyncTaskBranch> removeBranch)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            const double nodeWidth = 180;
            const double branchHeight = 35;
            const double headerHeight = 40;

            double totalHeight = headerHeight + (node.AsyncTaskBranches.Count * branchHeight);

            var border = new Border
            {
                Width = nodeWidth,
                Height = totalHeight,
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
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(headerHeight) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headerTextBrush = GetBrushFromTheme("TextOnMintChocolateBrush") ?? new SolidColorBrush(Colors.White);
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Child = new TextBlock
                {
                    Text = node.Title,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    Foreground = headerTextBrush
                }
            };
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            var branchesStack = new StackPanel();
            Grid.SetRow(branchesStack, 1);

            for (int i = 0; i < node.AsyncTaskBranches.Count; i++)
            {
                var branch = node.AsyncTaskBranches[i];
                branchesStack.Children.Add(CreateBranchUI(node, branch, i, addTaskBranch, removeBranch));
            }

            mainGrid.Children.Add(branchesStack);
            border.Child = mainGrid;

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

            border.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenNodeDialog(node, host, ownerWindow);
            };

            return border;
        }

        private static void OpenNodeDialog(WorkflowNode node, IWorkflowEditorHost host, Window? ownerWindow)
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

                var dialog = new AsyncTaskNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow);
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

        private static Border CreateBranchUI(
            WorkflowNode node,
            AsyncTaskBranch branch,
            int index,
            Action addTaskBranch,
            Action<AsyncTaskBranch> removeBranch)
        {
            var branchBorder = new Border
            {
                Height = 35,
                Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(5, 0, 5, 0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });

            if (branch.CanRemove)
            {
                var removeButton = new Button
                {
                    Content = "-",
                    Width = 20,
                    Height = 20,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand,
                    Tag = branch,
                    Style = Application.Current.FindResource("WarningButton") as Style
                };

                removeButton.Click += (s, e) =>
                {
                    e.Handled = true;
                    removeBranch(branch);
                };
                Grid.SetColumn(removeButton, 0);
                grid.Children.Add(removeButton);
            }

            var textBrush = GetBrushFromTheme("TextOnMintChocolateBrush") ?? new SolidColorBrush(Colors.White);
            var displayLabel = $"Task {index}";
            var textBlock = new TextBlock
            {
                Text = displayLabel,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = textBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            };

            // NÃºt thÃªm task á»Ÿ branch cuá»‘i cÃ¹ng
            if (index == node.AsyncTaskBranches.Count - 1)
            {
                var addButton = new Button
                {
                    Content = "+",
                    Width = 20,
                    Height = 20,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand,
                    Style = Application.Current.FindResource("PrimaryButton") as Style
                };

                addButton.Click += (s, e) =>
                {
                    e.Handled = true;
                    addTaskBranch();
                };

                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };
                stackPanel.Children.Add(addButton);
                stackPanel.Children.Add(textBlock);

                Grid.SetColumn(stackPanel, 1);
                grid.Children.Add(stackPanel);
            }
            else
            {
                Grid.SetColumn(textBlock, 1);
                grid.Children.Add(textBlock);
            }

            branchBorder.Child = grid;
            return branchBorder;
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
