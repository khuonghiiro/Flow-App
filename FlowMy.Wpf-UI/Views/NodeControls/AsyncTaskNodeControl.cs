using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Views;
using FlowMy.Views.NodeControls.Helpers;
using FlowMy.Views.Overlays;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// Custom UI control builder cho AsyncTask Node.
    /// Hiển thị số thứ tự cạnh port out (Task 0, Task 1, ...).
    /// Chuột phải mở dialog quản lý tasks.
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

            // This node uses an internal header TextBlock for the title (not a canvas-floating title).
            // We create a minimal dummy TextBlock to satisfy BaseNodeControlHelper.Initialize,
            // but skip WithTitleManagement() and WithCanvasIntegration() so it is never added to the canvas.
            var dummyTitle = new TextBlock { IsHitTestVisible = false, Visibility = Visibility.Collapsed };

            // --- Node-specific property handlers ---
            var customPropertyHandlers = new Dictionary<string, Action<BaseNodeControlHelper.NodeControlContext>>
            {
                [nameof(WorkflowNode.NodeBrush)] = ctx =>
                {
                    border.Background = node.NodeBrush;
                }
            };

            // --- Initialize with fluent API (hover, keyboard ports, dialog, cleanup) ---
            BaseNodeControlHelper
                .Initialize(border, dummyTitle, node, host)
                .WithHoverBehavior()
                .WithKeyboardPorts()
                .WithPropertySync(customPropertyHandlers)
                .WithDialogSupport(ctx => new AsyncTaskNodeDialog(node, host, ownerWindow ?? Application.Current?.MainWindow))
                .WithCleanup()
                .Build();

            return border;
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
                    Margin = new Thickness(0, 0, 5, 0),
                    Padding = new Thickness(0),
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

            // Add button on the last branch
            if (index == node.AsyncTaskBranches.Count - 1)
            {
                var addButton = new Button
                {
                    Content = "+",
                    Padding = new Thickness(0),
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
    }
}
