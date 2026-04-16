using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class AssignDataNodeDialog : BaseNodeDialog
    {
        private readonly AssignDataNodeDialogViewModel _viewModel;
        private readonly AssignDataNode _assignNode;

        public AssignDataNodeDialog(AssignDataNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _assignNode = node;
            _viewModel = new AssignDataNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);
            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void OnLoaded()
        {
            base.OnLoaded();
            BuildDataAssignmentsPanel();
        }

        protected override void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.ViewModel_PropertyChanged(sender, e);
        }

        private void BuildDataAssignmentsPanel()
        {
            DataAssignmentsPanel?.Children.Clear();
            if (DataAssignmentsPanel == null) return;

            foreach (var assignment in _assignNode.Assignments)
            {
                DataAssignmentsPanel.Children.Add(CreateAssignmentGroup(assignment));
            }
        }

        private FrameworkElement CreateAssignmentGroup(AssignDataAssignment assignment)
        {
            var baseCombo = Application.Current.TryFindResource("BaseComboBox") as Style;

            // Card cho một nhóm gán
            var card = new Border
            {
                Background = GetThemeBrush("WindowBackground", new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B))),
                BorderBrush = GetThemeBrush("ControlBorderBrush", new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF))),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel();

            // --- Dòng 1: Lấy từ (nguồn)
            var lblSource = new TextBlock
            {
                Text = "Lấy giá trị từ",
                Foreground = GetThemeBrush("TextBrush", new SolidColorBrush(Colors.White)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            var row1 = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            };
            var srcNodeCombo = new ComboBox
            {
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Style = baseCombo,
                ItemsSource = _viewModel.AvailableNodeOptions,
                SelectedValuePath = nameof(WorkflowDataSourceOption.NodeId),
                DisplayMemberPath = nameof(WorkflowDataSourceOption.Title)
            };
            if (!string.IsNullOrEmpty(assignment.SourceNodeId)) srcNodeCombo.SelectedValue = assignment.SourceNodeId;
            var srcKeyCombo = new ComboBox
            {
                Height = 32,
                Margin = new Thickness(0, 0, 0, 0),
                Style = baseCombo,
                SelectedValuePath = nameof(WorkflowOutputKeyOption.Key),
                DisplayMemberPath = nameof(WorkflowOutputKeyOption.Key)
            };
            srcKeyCombo.ItemsSource = _viewModel.GetOutputKeysForNode(assignment.SourceNodeId);
            if (!string.IsNullOrEmpty(assignment.SourceOutputKey)) srcKeyCombo.SelectedValue = assignment.SourceOutputKey;
            srcNodeCombo.SelectionChanged += (s, e) =>
            {
                if (srcNodeCombo.SelectedValue is string id) { assignment.SourceNodeId = id; srcKeyCombo.ItemsSource = _viewModel.GetOutputKeysForNode(id); }
            };
            srcKeyCombo.SelectionChanged += (s, e) => { if (srcKeyCombo.SelectedValue is string k) assignment.SourceOutputKey = k; };

            // Use Grid instead of StackPanel so ComboBox stretches to fill column
            var srcNodeGrid = new Grid();
            srcNodeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            srcNodeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var srcNodeLabel = new TextBlock
            {
                Text = "Node:",
                Foreground = GetThemeBrush("TextSecondary", new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5))),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            srcNodeCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(srcNodeLabel, 0);
            Grid.SetColumn(srcNodeCombo, 1);
            srcNodeGrid.Children.Add(srcNodeLabel);
            srcNodeGrid.Children.Add(srcNodeCombo);

            var srcKeyGrid = new Grid();
            srcKeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            srcKeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var srcKeyLabel = new TextBlock
            {
                Text = "Key:",
                Foreground = GetThemeBrush("TextSecondary", new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5))),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            srcKeyCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(srcKeyLabel, 0);
            Grid.SetColumn(srcKeyCombo, 1);
            srcKeyGrid.Children.Add(srcKeyLabel);
            srcKeyGrid.Children.Add(srcKeyCombo);

            Grid.SetColumn(srcNodeGrid, 0);
            Grid.SetColumn(srcKeyGrid, 1);
            row1.Children.Add(srcNodeGrid);
            row1.Children.Add(srcKeyGrid);
            left.Children.Add(lblSource);
            left.Children.Add(row1);

            // Checkbox: lấy giá trị mới nhất (chạy lại node nguồn khi giá trị cũ)
            var refreshCheck = new CheckBox
            {
                Content = "Lấy giá trị mới nhất (chạy lại node nguồn trước khi gán)",
                Foreground = GetThemeBrush("TextSecondary", new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5))),
                FontSize = 11,
                IsChecked = assignment.RefreshSourceBeforeUse,
                Margin = new Thickness(0, 8, 0, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Tích vào nếu node nguồn có giá trị cũ, cần chạy lại logic node đó để lấy giá trị mới nhất."
            };
            refreshCheck.Checked += (s, e) => assignment.RefreshSourceBeforeUse = true;
            refreshCheck.Unchecked += (s, e) => assignment.RefreshSourceBeforeUse = false;
            left.Children.Add(refreshCheck);

            // --- Dòng 2: Mũi tên
            var row2 = new TextBlock
            {
                Text = "↓ Gán vào",
                FontSize = 14,
                Foreground = GetThemeBrush("PrimaryBrush", new SolidColorBrush(Color.FromRgb(0x81, 0xD4, 0xFA))),
                Margin = new Thickness(0, 10, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            left.Children.Add(row2);

            // --- Dòng 3: Gán vào (đích)
            var lblTarget = new TextBlock
            {
                Text = "Node + Key đích (sẽ nhận giá trị)",
                Foreground = GetThemeBrush("TextBrush", new SolidColorBrush(Colors.White)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            var row3 = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            };
            var tgtNodeCombo = new ComboBox
            {
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Style = baseCombo,
                ItemsSource = _viewModel.AvailableNodeOptions,
                SelectedValuePath = nameof(WorkflowDataSourceOption.NodeId),
                DisplayMemberPath = nameof(WorkflowDataSourceOption.Title)
            };
            if (!string.IsNullOrEmpty(assignment.TargetNodeId)) tgtNodeCombo.SelectedValue = assignment.TargetNodeId;
            var tgtKeyCombo = new ComboBox
            {
                Height = 32,
                Margin = new Thickness(0, 0, 0, 0),
                Style = baseCombo,
                SelectedValuePath = nameof(WorkflowOutputKeyOption.Key),
                DisplayMemberPath = nameof(WorkflowOutputKeyOption.Key)
            };
            tgtKeyCombo.ItemsSource = _viewModel.GetOutputKeysForNode(assignment.TargetNodeId);
            if (!string.IsNullOrEmpty(assignment.TargetKey)) tgtKeyCombo.SelectedValue = assignment.TargetKey;
            tgtNodeCombo.SelectionChanged += (s, e) =>
            {
                if (tgtNodeCombo.SelectedValue is string id) { assignment.TargetNodeId = id; tgtKeyCombo.ItemsSource = _viewModel.GetOutputKeysForNode(id); }
            };
            tgtKeyCombo.SelectionChanged += (s, e) => { if (tgtKeyCombo.SelectedValue is string k) assignment.TargetKey = k; };

            // Use Grid instead of StackPanel so ComboBox stretches to fill column
            var tgtNodeGrid = new Grid();
            tgtNodeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tgtNodeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var tgtNodeLabel = new TextBlock
            {
                Text = "Node:",
                Foreground = GetThemeBrush("TextSecondary", new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5))),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            tgtNodeCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(tgtNodeLabel, 0);
            Grid.SetColumn(tgtNodeCombo, 1);
            tgtNodeGrid.Children.Add(tgtNodeLabel);
            tgtNodeGrid.Children.Add(tgtNodeCombo);

            var tgtKeyGrid = new Grid();
            tgtKeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tgtKeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var tgtKeyLabel = new TextBlock
            {
                Text = "Key:",
                Foreground = GetThemeBrush("TextSecondary", new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5))),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            tgtKeyCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(tgtKeyLabel, 0);
            Grid.SetColumn(tgtKeyCombo, 1);
            tgtKeyGrid.Children.Add(tgtKeyLabel);
            tgtKeyGrid.Children.Add(tgtKeyCombo);

            Grid.SetColumn(tgtNodeGrid, 0);
            Grid.SetColumn(tgtKeyGrid, 1);
            row3.Children.Add(tgtNodeGrid);
            row3.Children.Add(tgtKeyGrid);
            left.Children.Add(lblTarget);
            left.Children.Add(row3);

            var removeBtn = new Button
            {
                Content = "✕",
                Width = 32,
                Height = 32,
                FontSize = 14,
                ToolTip = "Xóa gán này",
                Style = Application.Current.TryFindResource("WarningButton") as Style,
                Cursor = Cursors.Hand,
                Tag = assignment,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8, 0, 0, 0)
            };
            removeBtn.Click += (s, e) =>
            {
                _assignNode.Assignments.Remove(assignment);
                BuildDataAssignmentsPanel();
            };

            Grid.SetColumn(left, 0);
            Grid.SetColumn(removeBtn, 1);
            grid.Children.Add(left);
            grid.Children.Add(removeBtn);
            card.Child = grid;
            return card;
        }

        private void AddAssignmentButton_Click(object sender, RoutedEventArgs e)
        {
            _assignNode.Assignments.Add(new AssignDataAssignment());
            BuildDataAssignmentsPanel();
        }

        private void TitleColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTitleColorPreview();
        }

        private void UpdateTitleColorPreview()
        {
            if (TitleColorPreview == null || TitleColorComboBox?.SelectedValue == null) return;

            var colorKey = TitleColorComboBox.SelectedValue.ToString();
            System.Windows.Media.Brush? brush = null;

            if (string.IsNullOrEmpty(colorKey) || colorKey == "NodeColor")
            {
                if (_viewModel?.Node != null)
                    brush = _viewModel.Node.NodeBrush;
            }
            else if (colorKey == "LimeGreen")
            {
                brush = new SolidColorBrush(Colors.LimeGreen);
            }
            else
            {
                brush = Application.Current.TryFindResource(colorKey) as System.Windows.Media.Brush;
            }

            TitleColorPreview.Background = brush ?? new SolidColorBrush(Colors.Gray);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveTitleCommand.Execute(null);
            Close();
        }
    }
}
