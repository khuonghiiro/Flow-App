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
    public partial class LoopNodeDialog : BaseNodeDialog
    {
        private readonly LoopNodeDialogViewModel _viewModel;
        private readonly LoopNode _loopNode;

        public LoopNodeDialog(LoopNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            _loopNode = node;
            // Initialize base after InitializeComponent
            _viewModel = new LoopNodeDialogViewModel(node, host);
            
            // Initialize base class properties
            InitializeBase(_viewModel, owner);

            // Update title color preview
            UpdateTitleColorPreview();

            // Update panel visibility when LoopType changes
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LoopNodeDialogViewModel.LoopType))
                {
                    UpdatePanelVisibility();
                    LoadInputs(); // Reload inputs để filter "loopArray"
                }
            };
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void OnLoaded()
        {
            base.OnLoaded();
            UpdatePanelVisibility();
            BuildCustomOutputsPanel();
            BuildDataAssignmentsPanel();

            // Listen to LoopTypeComboBox changes
            LoopTypeComboBox.SelectionChanged += (s2, e2) =>
            {
                UpdatePanelVisibility();
            };

            // Add validation for integer TextBoxes
            AddIntegerValidation(RepeatCountTextBox);
            AddIntegerValidation(StartIndexTextBox);
            AddIntegerValidation(EndIndexTextBox);
        }

        protected override void LoadOutputs()
        {
            base.LoadOutputs();

            var count = ViewModel.Outputs.Count;
            var hasCustom = _loopNode.CustomOutputMappings.Count > 0;
            var visible = count > 0 || hasCustom;
            BorderOutputPanel.Visibility = Visibility.Visible; // Luôn hiện để có nút +
            TextBlockOutputPanel.Visibility = Visibility.Visible;
        }

        protected override FrameworkElement CreateInputItemUI(InputItemViewModel inputVm)
        {
            var item = base.CreateInputItemUI(inputVm);
            
            // ⚠️ CRITICAL: Trigger UpdateConnectionStates khi SelectedSourceNodeId hoặc SelectedSourceOutputKey thay đổi
            inputVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(InputItemViewModel.SelectedSourceNodeId) ||
                    e.PropertyName == nameof(InputItemViewModel.SelectedSourceOutputKey))
                {
                    // Delay một chút để đảm bảo InputItemViewModel đã sync xong với _input
                    Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        _viewModel.UpdateConnectionStates();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            };

            return item;
        }

        protected override void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.ViewModel_PropertyChanged(sender, e);
        }

        private void UpdatePanelVisibility()
        {
            var loopType = _viewModel.LoopType;
            
            // Show/hide panels based on LoopType
            RepeatNPanel.Visibility = loopType == LoopType.RepeatN ? Visibility.Visible : Visibility.Collapsed;
            ForLoopPanel.Visibility = loopType == LoopType.ForLoop ? Visibility.Visible : Visibility.Collapsed;
            ForEachArrayPanel.Visibility = loopType == LoopType.ForEachArray ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddIntegerValidation(TextBox textBox)
        {
            if (textBox == null) return;
            
            textBox.TextChanged += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    textBox.Background = new SolidColorBrush(Colors.White);
                    return;
                }
                
                if (int.TryParse(textBox.Text, out _))
                {
                    textBox.Background = new SolidColorBrush(Colors.White);
                }
                else
                {
                    textBox.Background = new SolidColorBrush(Color.FromRgb(255, 200, 200));
                }
            };
        }

        private void TitleColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTitleColorPreview();
        }

        private void UpdateTitleColorPreview()
        {
            if (TitleColorPreview == null || TitleColorComboBox?.SelectedValue == null) return;

            var colorKey = TitleColorComboBox.SelectedValue.ToString();
            Brush? brush = null;

            if (string.IsNullOrEmpty(colorKey) || colorKey == "NodeColor")
            {
                // Màu theo node - lấy từ node hiện tại
                if (_viewModel?.Node != null)
                {
                    brush = _viewModel.Node.NodeBrush;
                }
            }
            else if (colorKey == "LimeGreen")
            {
                brush = new SolidColorBrush(Colors.LimeGreen);
            }
            else
            {
                brush = Application.Current.TryFindResource(colorKey) as Brush;
            }

            TitleColorPreview.Background = brush ?? new SolidColorBrush(Colors.Gray);
        }

        private void BuildCustomOutputsPanel()
        {
            CustomOutputsPanel?.Children.Clear();
            if (CustomOutputsPanel == null) return;

            foreach (var mapping in _loopNode.CustomOutputMappings)
            {
                CustomOutputsPanel.Children.Add(CreateCustomOutputRow(mapping));
            }
        }

        private FrameworkElement CreateCustomOutputRow(LoopCustomOutputMapping mapping)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 8), Orientation = Orientation.Horizontal };

            var keyCombo = new ComboBox
            {
                Height = 36,
                Width = 120,
                Margin = new Thickness(0, 0, 8, 0),
                Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                SelectedValuePath = nameof(WorkflowOutputKeyOption.Key),
                DisplayMemberPath = nameof(WorkflowOutputKeyOption.Key)
            };
            keyCombo.ItemsSource = _viewModel.GetOutputKeysForNode(mapping.SourceNodeId);
            if (!string.IsNullOrEmpty(mapping.SourceOutputKey))
                keyCombo.SelectedValue = mapping.SourceOutputKey;
            keyCombo.SelectionChanged += (s, e) =>
            {
                if (keyCombo.SelectedValue is string key)
                    mapping.SourceOutputKey = key;
            };

            var nodeCombo = new ComboBox
            {
                Height = 36,
                Width = 160,
                Margin = new Thickness(0, 0, 8, 0),
                Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                ItemsSource = _viewModel.BodyNodeOptions,
                SelectedValuePath = nameof(WorkflowDataSourceOption.NodeId),
                DisplayMemberPath = nameof(WorkflowDataSourceOption.Title)
            };
            if (!string.IsNullOrEmpty(mapping.SourceNodeId))
                nodeCombo.SelectedValue = mapping.SourceNodeId;
            nodeCombo.SelectionChanged += (s, e) =>
            {
                if (nodeCombo.SelectedValue is string nodeId)
                {
                    mapping.SourceNodeId = nodeId;
                    keyCombo.ItemsSource = _viewModel.GetOutputKeysForNode(nodeId);
                    keyCombo.SelectedValue = mapping.SourceOutputKey;
                }
            };

            var removeBtn = new Button
            {
                Content = "−",
                Width = 28,
                Height = 28,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Style = Application.Current.TryFindResource("WarningButton") as Style,
                Cursor = Cursors.Hand,
                Tag = mapping
            };
            removeBtn.Click += (s, e) =>
            {
                _loopNode.CustomOutputMappings.Remove(mapping);
                BuildCustomOutputsPanel();
                _viewModel.RefreshBodyNodeOptions();
            };

            row.Children.Add(nodeCombo);
            row.Children.Add(keyCombo);
            row.Children.Add(removeBtn);
            return row;
        }

        private void AddCustomOutputButton_Click(object sender, RoutedEventArgs e)
        {
            _loopNode.CustomOutputMappings.Add(new LoopCustomOutputMapping());
            BuildCustomOutputsPanel();
        }

        private void BuildDataAssignmentsPanel()
        {
            DataAssignmentsPanel?.Children.Clear();
            if (DataAssignmentsPanel == null) return;

            foreach (var assignment in _loopNode.DataAssignments)
            {
                DataAssignmentsPanel.Children.Add(CreateAssignmentGroup(assignment));
            }
        }

        private FrameworkElement CreateAssignmentGroup(LoopDataAssignment assignment)
        {
            var group = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Dòng 1: Node nguồn + Key nguồn
            var row1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            var srcNodeCombo = new ComboBox
            {
                Height = 36,
                MinWidth = 140,
                Margin = new Thickness(0, 0, 8, 0),
                Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                ItemsSource = _viewModel.BodyNodeOptions,
                SelectedValuePath = nameof(WorkflowDataSourceOption.NodeId),
                DisplayMemberPath = nameof(WorkflowDataSourceOption.Title)
            };
            if (!string.IsNullOrEmpty(assignment.SourceNodeId)) srcNodeCombo.SelectedValue = assignment.SourceNodeId;
            var srcKeyCombo = new ComboBox
            {
                Height = 36,
                MinWidth = 100,
                Style = Application.Current.TryFindResource("BaseComboBox") as Style,
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
            row1.Children.Add(srcNodeCombo);
            row1.Children.Add(srcKeyCombo);

            // Dòng 2: Mũi tên xuống
            var row2 = new TextBlock
            {
                Text = "↓",
                FontSize = 20,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Dòng 3: Node đích + Key đích
            var row3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            var tgtNodeCombo = new ComboBox
            {
                Height = 36,
                MinWidth = 140,
                Margin = new Thickness(0, 0, 8, 0),
                Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                ItemsSource = _viewModel.BodyNodeOptions,
                SelectedValuePath = nameof(WorkflowDataSourceOption.NodeId),
                DisplayMemberPath = nameof(WorkflowDataSourceOption.Title)
            };
            if (!string.IsNullOrEmpty(assignment.TargetNodeId)) tgtNodeCombo.SelectedValue = assignment.TargetNodeId;
            var tgtKeyCombo = new ComboBox
            {
                Height = 36,
                MinWidth = 100,
                Style = Application.Current.TryFindResource("BaseComboBox") as Style,
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
            row3.Children.Add(tgtNodeCombo);
            row3.Children.Add(tgtKeyCombo);

            var removeBtn = new Button
            {
                Content = "−",
                Width = 28,
                Height = 28,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Style = Application.Current.TryFindResource("WarningButton") as Style,
                Cursor = Cursors.Hand,
                Tag = assignment,
                VerticalAlignment = VerticalAlignment.Top
            };
            removeBtn.Click += (s, e) =>
            {
                _loopNode.DataAssignments.Remove(assignment);
                BuildDataAssignmentsPanel();
            };

            var wrap = new Grid();
            wrap.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            wrap.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            left.Children.Add(row1);
            left.Children.Add(row2);
            left.Children.Add(row3);
            Grid.SetColumn(left, 0);
            Grid.SetColumn(removeBtn, 1);
            wrap.Children.Add(left);
            wrap.Children.Add(removeBtn);

            group.Children.Add(wrap);
            return group;
        }

        private void AddAssignmentButton_Click(object sender, RoutedEventArgs e)
        {
            _loopNode.DataAssignments.Add(new LoopDataAssignment());
            BuildDataAssignmentsPanel();
        }
    }
}
