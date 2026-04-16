using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowMy.Controls;
using FlowMy.Models;
using FlowMy.Models.Enums;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;

namespace FlowMy.Views.Overlays
{
    public partial class InputNodeDialog : BaseNodeDialog
    {
        private readonly InputNodeDialogViewModel _viewModel;

        public InputNodeDialog(InputNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            // Initialize base after InitializeComponent
            _viewModel = new InputNodeDialogViewModel(node, host);
            
            // Initialize base class properties
            InitializeBase(_viewModel, owner);

            // Update title color preview
            UpdateTitleColorPreview();

            // Sync khi DataType thay đổi
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(InputNodeDialogViewModel.DataType))
                {
                    LoadValueArea();
                }
                else if (e.PropertyName == nameof(InputNodeDialogViewModel.Key) || 
                         e.PropertyName == nameof(InputNodeDialogViewModel.Value) ||
                         e.PropertyName == nameof(InputNodeDialogViewModel.ArrayValues))
                {
                    // Reload outputs khi Key hoặc Value thay đổi để hiển thị giá trị mới ngay lập tức
                    LoadOutputs();
                }
            };
        }

        protected override Panel? GetInputsPanel() => null; // InputNode không có inputs
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void OnLoaded()
        {
            base.OnLoaded();
            LoadValueArea();
        }

        protected override void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.ViewModel_PropertyChanged(sender, e);
        }

        /// <summary>
        /// Toggle giữa chế độ Text (1 dòng) và TextArea (đa dòng) cho kiểu String.
        /// </summary>
        private void ToggleValueModeButton_Click(object sender, RoutedEventArgs e)
        {
            // Chỉ áp dụng cho kiểu String
            if (_viewModel.DataType != WorkflowDataType.String) return;

            _viewModel.IsMultilineString = !_viewModel.IsMultilineString;
            LoadValueArea();
        }

        private void LoadValueArea()
        {
            ValuePanel.Children.Clear();
            ErrorTextBlock.Visibility = Visibility.Collapsed;
            ErrorTextBlock.Text = "";

            if (_viewModel.IsArrayType)
            {
                LoadArrayItems();
            }
            else if (_viewModel.IsDateTimeOrTime)
            {
                LoadDateTimePicker();
            }
            else
            {
                LoadValueTextBox();
            }
        }

        private void LoadValueTextBox()
        {
            // Với kiểu String cho phép chuyển qua TextArea (đa dòng)
            var isMultiline = _viewModel.DataType == WorkflowDataType.String && _viewModel.IsMultilineString;

            var textBox = new TextBox
            {
                Text = _viewModel.Value,
                Style = Application.Current.TryFindResource("BaseTextBoxV2") as Style,
                FontSize = 13,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = isMultiline ? VerticalAlignment.Top : VerticalAlignment.Center,
                AcceptsReturn = isMultiline,
                TextWrapping = isMultiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                Height = isMultiline ? double.NaN : 36,
                MinHeight = 36,
                MaxHeight = isMultiline ? 200 : 36,
                VerticalScrollBarVisibility = isMultiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
            };

            textBox.TextChanged += (s, e) =>
            {
                _viewModel.Value = textBox.Text;
                ValidateValue();
            };

            textBox.LostFocus += (s, e) => ValidateValue();

            ValuePanel.Children.Add(textBox);
            ValidateValue();
        }

        private void LoadDateTimePicker()
        {
            var picker = new DateTimePickerUserControl
            {
                ControlHeight = 36,
                Width = double.NaN,
                PickerMode = _viewModel.DataType == WorkflowDataType.DateTime
                    ? DateTimePickerModeEnum.DateTime
                    : DateTimePickerModeEnum.Time,
                ShowClearButton = true,
                ShowTodayButton = true,
                PlaceholderText = _viewModel.DataType == WorkflowDataType.DateTime
                    ? "Chọn ngày giờ..."
                    : "Chọn giờ..."
            };

            if (!string.IsNullOrWhiteSpace(_viewModel.Value))
            {
                if (DateTime.TryParse(_viewModel.Value, out var parsedDate))
                {
                    picker.SelectedDateTime = parsedDate;
                }
            }

            picker.SelectedDateTimeChanged += (s, e) =>
            {
                if (picker.SelectedDateTime.HasValue)
                {
                    var format = _viewModel.DataType == WorkflowDataType.DateTime
                        ? "yyyy-MM-dd HH:mm:ss"
                        : "HH:mm:ss";
                    _viewModel.Value = picker.SelectedDateTime.Value.ToString(format);
                }
                else
                {
                    _viewModel.Value = string.Empty;
                }
                ValidateValue();
            };

            ValuePanel.Children.Add(picker);
        }

        private void LoadArrayItems()
        {
            var container = new StackPanel { Orientation = Orientation.Vertical };

            if (_viewModel.ArrayValues == null || _viewModel.ArrayValues.Count == 0)
            {
                _viewModel.ArrayValues = new List<string> { string.Empty };
            }

            for (int i = 0; i < _viewModel.ArrayValues.Count; i++)
            {
                var index = i;
                var row = CreateArrayItemRow(index, _viewModel.ArrayValues[index]);
                container.Children.Add(row);
            }

            ValuePanel.Children.Add(container);
            ValidateValue();
        }

        private FrameworkElement CreateArrayItemRow(int index, string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // TextBlock index
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // TextBox
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Buttons

            // TextBlock hiển thị index [0], [1], [2]...
            var indexTextBlock = new TextBlock
            {
                Text = $"[{index}]",
                FontSize = 13,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Opacity = 0.8,
                MinWidth = 40
            };

            var textBox = new TextBox
            {
                Text = value,
                Height = 36,
                FontSize = 13,
                Style = Application.Current.TryFindResource("BaseTextBoxV2") as Style,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };

            textBox.LostFocus += (s, e) =>
            {
                if (_viewModel.ArrayValues != null && index < _viewModel.ArrayValues.Count)
                {
                    var cloned = _viewModel.ArrayValues.ToList();
                    cloned[index] = textBox.Text ?? string.Empty;
                    _viewModel.ArrayValues = cloned;
                }
                ValidateValue();
            };

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var btnAdd = new Button
            {
                Content = "+",
                Width = 32,
                Height = 32,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(2, 0, 2, 0),
                Style = Application.Current.TryFindResource("PrimaryButton") as Style,
                Cursor = Cursors.Hand
            };

            btnAdd.Click += (s, e) =>
            {
                var current = _viewModel.ArrayValues ?? new List<string>();
                var cloned = current.ToList();
                cloned.Insert(index + 1, string.Empty);
                _viewModel.ArrayValues = cloned;
                LoadValueArea();
            };

            var btnRemove = new Button
            {
                Content = "-",
                Width = 32,
                Height = 32,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(2, 0, 2, 0),
                Style = Application.Current.TryFindResource("DangerButton") as Style,
                Cursor = Cursors.Hand,
                Visibility = index == 0 ? Visibility.Collapsed : Visibility.Visible
            };

            btnRemove.Click += (s, e) =>
            {
                if (_viewModel.ArrayValues != null && _viewModel.ArrayValues.Count > index)
                {
                    var cloned = _viewModel.ArrayValues.ToList();
                    cloned.RemoveAt(index);
                    _viewModel.ArrayValues = cloned;
                    LoadValueArea();
                }
            };

            buttonPanel.Children.Add(btnAdd);
            if (index > 0)
            {
                buttonPanel.Children.Add(btnRemove);
            }

            Grid.SetColumn(indexTextBlock, 0);
            Grid.SetColumn(textBox, 1);
            Grid.SetColumn(buttonPanel, 2);
            grid.Children.Add(indexTextBlock);
            grid.Children.Add(textBox);
            grid.Children.Add(buttonPanel);

            return grid;
        }

        private void ValidateValue()
        {
            if (_viewModel.IsArrayType)
            {
                ValidateArrayValues();
            }
            else
            {
                ValidateSingleValue();
            }
        }

        private void ValidateSingleValue()
        {
            if (_viewModel.IsDateTimeOrTime)
            {
                ErrorTextBlock.Visibility = Visibility.Collapsed;
                ErrorTextBlock.Text = "";
                return;
            }

            var raw = _viewModel.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                ErrorTextBlock.Visibility = Visibility.Collapsed;
                ErrorTextBlock.Text = "";
                return;
            }

            if (TryConvertValue(raw, _viewModel.DataType, out _, out var err))
            {
                ErrorTextBlock.Visibility = Visibility.Collapsed;
                ErrorTextBlock.Text = "";
            }
            else
            {
                ErrorTextBlock.Text = err ?? "Giá trị không hợp lệ";
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
        }

        private void ValidateArrayValues()
        {
            if (_viewModel.ArrayValues == null || _viewModel.ArrayValues.Count == 0)
            {
                ErrorTextBlock.Text = "Mảng phải có ít nhất 1 phần tử";
                ErrorTextBlock.Visibility = Visibility.Visible;
                return;
            }

            var errors = new List<string>();
            for (int i = 0; i < _viewModel.ArrayValues.Count; i++)
            {
                var item = _viewModel.ArrayValues[i]?.Trim() ?? string.Empty;

                if (_viewModel.DataType == WorkflowDataType.ArrayDynamic)
                {
                    if (string.IsNullOrWhiteSpace(item))
                    {
                        continue;
                    }
                }
                else if (_viewModel.DataType == WorkflowDataType.ArrayString)
                {
                    if (string.IsNullOrWhiteSpace(item))
                    {
                        errors.Add($"Item {i + 1} không được rỗng");
                    }
                }
                else if (_viewModel.DataType == WorkflowDataType.ArrayNumber)
                {
                    if (string.IsNullOrWhiteSpace(item))
                    {
                        errors.Add($"Item {i + 1} không được rỗng");
                    }
                    else if (!double.TryParse(item, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
                             !double.TryParse(item, NumberStyles.Float, CultureInfo.CurrentCulture, out _))
                    {
                        errors.Add($"Item {i + 1} phải là số hợp lệ");
                    }
                }
            }

            if (errors.Count > 0)
            {
                ErrorTextBlock.Text = string.Join(", ", errors);
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorTextBlock.Visibility = Visibility.Collapsed;
                ErrorTextBlock.Text = "";
            }
        }

        private bool TryConvertValue(string raw, WorkflowDataType type, out string converted, out string? error)
        {
            converted = string.Empty;
            error = null;

            try
            {
                switch (type)
                {
                    case WorkflowDataType.String:
                        converted = raw;
                        return true;

                    case WorkflowDataType.Integer:
                        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ||
                            long.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out l))
                        {
                            converted = l.ToString(CultureInfo.InvariantCulture);
                            return true;
                        }
                        error = "Giá trị không hợp lệ cho Integer";
                        return false;

                    case WorkflowDataType.Number:
                        if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d) ||
                            double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out d))
                        {
                            converted = d.ToString(CultureInfo.InvariantCulture);
                            return true;
                        }
                        error = "Giá trị không hợp lệ cho Number";
                        return false;

                    case WorkflowDataType.Boolean:
                        {
                            var s = raw.Trim();
                            if (bool.TryParse(s, out var b))
                            {
                                converted = b ? "true" : "false";
                                return true;
                            }

                            if (string.Equals(s, "1", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(s, "y", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(s, "on", StringComparison.OrdinalIgnoreCase))
                            {
                                converted = "true";
                                return true;
                            }
                            if (string.Equals(s, "0", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(s, "no", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(s, "n", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(s, "off", StringComparison.OrdinalIgnoreCase))
                            {
                                converted = "false";
                                return true;
                            }

                            error = "Giá trị không hợp lệ cho Boolean (true/false/1/0/yes/no)";
                            return false;
                        }

                    case WorkflowDataType.DateTime:
                    case WorkflowDataType.Time:
                        converted = raw;
                        return true;

                    default:
                        converted = raw;
                        return true;
                }
            }
            catch (Exception ex)
            {
                error = $"Lỗi: {ex.Message}";
                return false;
            }
        }

        protected override FrameworkElement CreateOutputItemUI(OutputItemViewModel outputVm)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            
            // Sử dụng binding để key tự động cập nhật khi thay đổi
            var keyText = new TextBlock { Foreground = Brushes.White, FontSize = 12, Opacity = 0.9, Margin = new Thickness(0, 0, 0, 4) };
            keyText.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(OutputItemViewModel.Key)) 
                { 
                    Source = outputVm,
                    StringFormat = "Key: {0}"
                });
            stack.Children.Add(keyText);

            var valueText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 11,
                Opacity = 0.9,
                TextWrapping = TextWrapping.Wrap
            };
            valueText.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(OutputItemViewModel.Value)) { Source = outputVm });
            stack.Children.Add(valueText);

            return stack;
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
    }
}
