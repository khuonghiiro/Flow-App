using FlowMy.Controls;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class FolderNodeDialog : BaseNodeDialog
    {
        private readonly FolderNode _folderNode;
        private readonly FolderNodeDialogViewModel _viewModel;

        public FolderNodeDialog(FolderNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _folderNode = node;
            _viewModel = new FolderNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);
            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void OnLoaded()
        {
            base.OnLoaded();
            RootFolderPathTextBox.Text = _folderNode.RootFolderPath ?? string.Empty;
            SubPathTemplateTextBox.Text = _folderNode.SubPathTemplate ?? string.Empty;
            BuildKeyValueInputsPanel();
        }

        protected override void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.ViewModel_PropertyChanged(sender, e);
        }

        private void BuildKeyValueInputsPanel()
        {
            KeyValueInputsPanel?.Children.Clear();
            if (KeyValueInputsPanel == null) return;

            var baseCombo = Application.Current.TryFindResource("BaseComboBox") as Style;
            var baseTextBox = Application.Current.TryFindResource("BaseTextBoxV2") as Style;

            foreach (var kv in _folderNode.KeyValueInputs)
            {
                var row = CreateKeyValueInputRow(kv, baseCombo, baseTextBox);
                KeyValueInputsPanel.Children.Add(row);
            }
        }

        private FrameworkElement CreateKeyValueInputRow(FolderKeyValueInput kv, Style? baseCombo, Style? baseTextBox)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var rowPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var nodeCombo = new NodeSearchComboBoxUserControl
            {
                Height = 32,
                MinWidth = 120,
                Margin = new Thickness(0, 0, 8, 0),
                ItemsSource = _viewModel.AvailableNodeOptions,
                SelectedValuePath = nameof(Models.WorkflowDataSourceOption.NodeId),
                DisplayMemberPath = nameof(Models.WorkflowDataSourceOption.Title)
            };
            if (!string.IsNullOrEmpty(kv.SourceNodeId)) nodeCombo.SelectedValue = kv.SourceNodeId;

            var keyCombo = new ComboBox
            {
                Height = 32,
                MinWidth = 90,
                Margin = new Thickness(0, 0, 8, 0),
                Style = baseCombo,
                SelectedValuePath = nameof(Models.WorkflowOutputKeyOption.Key),
                DisplayMemberPath = nameof(Models.WorkflowOutputKeyOption.Key)
            };
            keyCombo.ItemsSource = _viewModel.GetOutputKeysForNode(kv.SourceNodeId);
            if (!string.IsNullOrEmpty(kv.SourceOutputKey)) keyCombo.SelectedValue = kv.SourceOutputKey;

            var valueBox = new TextBox
            {
                Height = 32,
                MinWidth = 80,
                Style = baseTextBox,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 0,6,0)
            };
            valueBox.Text = kv.ValueConfirm ?? string.Empty;
            valueBox.TextChanged += (s, e) => kv.ValueConfirm = (s as TextBox)?.Text;

            var nodeDp = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                NodeSearchComboBoxUserControl.SelectedValueProperty,
                typeof(NodeSearchComboBoxUserControl));
            nodeDp?.AddValueChanged(nodeCombo, (s, e) =>
            {
                if (nodeCombo.SelectedValue is string id)
                {
                    kv.SourceNodeId = id;
                    keyCombo.ItemsSource = _viewModel.GetOutputKeysForNode(id);
                }
            });
            keyCombo.SelectionChanged += (s, e) =>
            {
                if (keyCombo.SelectedValue is string k) kv.SourceOutputKey = k;
            };

            rowPanel.Children.Add(new TextBlock
            {
                Text = "Node:",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            rowPanel.Children.Add(nodeCombo);
            rowPanel.Children.Add(new TextBlock
            {
                Text = "Key:",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 6, 0)
            });
            rowPanel.Children.Add(keyCombo);
            rowPanel.Children.Add(new TextBlock
            {
                Text = "Value:",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 6, 0)
            });
            rowPanel.Children.Add(valueBox);

            var removeBtn = new Button
            {
                Content = "−",
                Width = 32,
                Height = 32,
                FontSize = 16,
                ToolTip = "Xóa dòng này",
                Style = Application.Current.TryFindResource("WarningButton") as Style,
                Cursor = Cursors.Hand,
                Tag = kv,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            removeBtn.Click += (s, e) =>
            {
                if (_folderNode.KeyValueInputs.Count <= 1) return;
                _folderNode.KeyValueInputs.Remove(kv);
                BuildKeyValueInputsPanel();
            };

            Grid.SetColumn(rowPanel, 0);
            Grid.SetColumn(removeBtn, 1);
            grid.Children.Add(rowPanel);
            grid.Children.Add(removeBtn);
            card.Child = grid;
            return card;
        }

        private void AddInputRowButton_Click(object sender, RoutedEventArgs e)
        {
            _folderNode.KeyValueInputs.Add(new FolderKeyValueInput());
            BuildKeyValueInputsPanel();
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFolderDialog
                {
                    Title = "Chọn thư mục gốc",
                    Multiselect = false
                };
                if (!string.IsNullOrWhiteSpace(_viewModel.RootFolderPath))
                    dlg.InitialDirectory = _viewModel.RootFolderPath;

                if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.FolderName))
                {
                    _viewModel.RootFolderPath = dlg.FolderName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở hộp thoại chọn thư mục: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
