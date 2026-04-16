using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class HttpRequestNodeDialog : BaseNodeDialog
    {
        private readonly HttpRequestNodeDialogViewModel _viewModel;
        private bool _isProcessingPaste = false;
        private string _lastProcessedText = string.Empty;

        public HttpRequestNodeDialog(HttpRequestNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            _viewModel = new HttpRequestNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            // Subscribe to collection changes
            _viewModel.HeaderItems.CollectionChanged += HeaderItems_CollectionChanged;
            _viewModel.ParamItems.CollectionChanged += ParamItems_CollectionChanged;
            _viewModel.FormDataItems.CollectionChanged += FormDataItems_CollectionChanged;

            // Initial render
            RenderHeaders();
            RenderParams();
            RenderFormData();

            // Initialize password box
            if (!string.IsNullOrEmpty(_viewModel.AuthPassword))
            {
                PasswordBox.Password = _viewModel.AuthPassword;
            }

            // Initialize title color preview
            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.ViewModel_PropertyChanged(sender, e);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb && _viewModel != null)
            {
                _viewModel.AuthPassword = pb.Password;
            }
        }

        #region Headers Panel

        private void HeaderItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RenderHeaders();
        }

        private void RenderHeaders()
        {
            HeadersPanel.Children.Clear();
            
            for (int i = 0; i < _viewModel.HeaderItems.Count; i++)
            {
                var item = _viewModel.HeaderItems[i];
                var row = CreateKeyValueRow(item, _viewModel.HeaderItems, i, "header");
                HeadersPanel.Children.Add(row);
            }
        }

        private void AddHeader_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AddHeaderCommand.Execute(null);
        }

        #endregion

        #region Params Panel

        private void ParamItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RenderParams();
        }

        private void RenderParams()
        {
            ParamsPanel.Children.Clear();
            
            for (int i = 0; i < _viewModel.ParamItems.Count; i++)
            {
                var item = _viewModel.ParamItems[i];
                var row = CreateKeyValueRow(item, _viewModel.ParamItems, i, "param");
                ParamsPanel.Children.Add(row);
            }
        }

        private void AddParam_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AddParamCommand.Execute(null);
        }

        #endregion

        #region Form Data Panel

        private void FormDataItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RenderFormData();
        }

        private void RenderFormData()
        {
            FormDataItemsPanel.Children.Clear();
            
            for (int i = 0; i < _viewModel.FormDataItems.Count; i++)
            {
                var item = _viewModel.FormDataItems[i];
                var row = CreateKeyValueRow(item, _viewModel.FormDataItems, i, "formdata");
                FormDataItemsPanel.Children.Add(row);
            }
        }

        private void AddFormData_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AddFormDataCommand.Execute(null);
        }

        #endregion

        #region cURL Paste Detection

        private void ImportCurl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Clipboard.ContainsText())
                {
                    MessageBox.Show(
                        "Clipboard trống!\n\nVui lòng copy cURL command trước.",
                        "Import cURL",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var clipboardText = Clipboard.GetText();
                
                if (!_viewModel.IsCurlCommand(clipboardText))
                {
                    MessageBox.Show(
                        "Không tìm thấy cURL command trong clipboard!\n\nVui lòng copy một cURL command hợp lệ (bắt đầu với 'curl ').",
                        "Import cURL",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Reset UI first (clear panels before parsing)
                HeadersPanel.Children.Clear();
                ParamsPanel.Children.Clear();
                FormDataItemsPanel.Children.Clear();

                if (_viewModel.ParseAndApplyCurl(clipboardText, out string errorMsg))
                {
                    _lastProcessedText = _viewModel.Url;
                    
                    // Re-render the panels with cleaned data
                    RenderHeaders();
                    RenderParams();
                    RenderFormData();

                    // Update password box if Basic auth
                    if (!string.IsNullOrEmpty(_viewModel.AuthPassword))
                    {
                        PasswordBox.Password = _viewModel.AuthPassword;
                    }

                    MessageBox.Show(
                        "✅ Đã import cURL thành công!\n\n" +
                        $"• URL: {_viewModel.Url}\n" +
                        $"• Method: {_viewModel.HttpMethod}\n" +
                        $"• Headers: {_viewModel.HeaderItems.Count}\n" +
                        $"• Params: {_viewModel.ParamItems.Count}\n" +
                        $"• Auth: {_viewModel.AuthType}\n" +
                        $"• Body Type: {_viewModel.BodyType}",
                        "Import Thành Công",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    var debugInfo = FlowMy.Utils.CurlParser.GetParseDebugInfo(clipboardText);
                    MessageBox.Show(
                        $"❌ Không thể parse cURL command.\n\nLỗi: {errorMsg}\n\n" +
                        "Bạn có thể thử:\n" +
                        "• Copy lại cURL từ browser (F12 > Network > Copy as cURL)\n" +
                        "• Đảm bảo cURL bắt đầu bằng 'curl '\n\n" +
                        $"Debug Info:\n{debugInfo}",
                        "Parse Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyCurl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save current state to node first
                ViewModel.SaveTitleCommand.Execute(null);

                // Generate cURL command
                var curlCommand = _viewModel.GenerateCurlCommand();

                if (string.IsNullOrWhiteSpace(curlCommand) || curlCommand.Trim() == "curl")
                {
                    MessageBox.Show(
                        "Không có cấu hình để tạo cURL!\n\nVui lòng cấu hình ít nhất URL.",
                        "Copy cURL",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Copy to clipboard
                Clipboard.SetText(curlCommand);

                MessageBox.Show(
                    "✅ Đã copy cURL command vào clipboard!\n\n" +
                    "Bạn có thể paste vào terminal hoặc script để chạy.",
                    "Copy Thành Công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ Lỗi khi tạo cURL command:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UrlTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Detect Ctrl+V paste and handle cURL auto-parse
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (TryProcessCurlFromClipboard())
                {
                    e.Handled = true; // Prevent default paste
                }
            }
        }

        private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            var currentText = textBox.Text;

            // Skip if same text, empty, or already processing
            if (string.IsNullOrWhiteSpace(currentText) || currentText == _lastProcessedText || _isProcessingPaste)
            {
                return;
            }

            // Update last processed text for non-cURL inputs
            _lastProcessedText = currentText;
        }

        /// <summary>
        /// Try to process cURL command from clipboard. Returns true if cURL was detected and processed.
        /// </summary>
        private bool TryProcessCurlFromClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText())
                    return false;

                var clipboardText = Clipboard.GetText();
                
                if (string.IsNullOrWhiteSpace(clipboardText))
                    return false;

                // Check if it's a cURL command
                if (!_viewModel.IsCurlCommand(clipboardText))
                    return false;

                _isProcessingPaste = true;

                // Reset UI first (this is done inside ParseAndApplyCurl, but we also clear UI here)
                // Clear UI panels before parsing to avoid old data showing
                HeadersPanel.Children.Clear();
                ParamsPanel.Children.Clear();
                FormDataItemsPanel.Children.Clear();

                // Auto parse cURL without asking user
                // ParseAndApplyCurl will reset state and clean special characters
                if (_viewModel.ParseAndApplyCurl(clipboardText, out string errorMsg))
                {
                    _lastProcessedText = _viewModel.Url;
                    
                    // Re-render the panels with cleaned data
                    RenderHeaders();
                    RenderParams();
                    RenderFormData();

                    // Update password box if Basic auth
                    if (!string.IsNullOrEmpty(_viewModel.AuthPassword))
                    {
                        PasswordBox.Password = _viewModel.AuthPassword;
                    }

                    _isProcessingPaste = false;
                    return true; // cURL was processed successfully
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"cURL parse error: {errorMsg}");
                }

                _isProcessingPaste = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"cURL parse exception: {ex.Message}");
                _isProcessingPaste = false;
            }

            return false; // Not a cURL or failed to parse, let default paste happen
        }

        #endregion

        #region Key-Value Row Builder

        private Border CreateKeyValueRow(HttpKeyValueItemViewModel item, ObservableCollection<HttpKeyValueItemViewModel> collection, int index, string prefix)
        {
            var border = new Border
            {
                Background = System.Windows.Media.Brushes.Transparent,
                Margin = new Thickness(0, 0, 0, 8),
                //Padding = new Thickness(0,0,0,6),
                //BorderThickness = new Thickness(0, 0, 0, 1),
                //BorderBrush = Application.Current.TryFindResource("InfoBrush") as Brush
            };

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });   // Key: 2/5
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });   // Value: 3/5
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Row 1: Key and Value TextBoxes
            var keyTextBox = new TextBox
            {
                Height = 32,
                Margin = new Thickness(0, 0, 4, 0),
                Style = (Style)FindResource("BaseTextBoxV2")
            };
            keyTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(HttpKeyValueItemViewModel.Key))
            {
                Source = item,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            Grid.SetRow(keyTextBox, 0);
            Grid.SetColumn(keyTextBox, 0);
            mainGrid.Children.Add(keyTextBox);

            var valueTextBox = new TextBox
            {
                Height = 32,
                Margin = new Thickness(4, 0, 4, 0),
                Style = (Style)FindResource("BaseTextBoxV2")
            };
            valueTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(HttpKeyValueItemViewModel.Value))
            {
                Source = item,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            Grid.SetRow(valueTextBox, 0);
            Grid.SetColumn(valueTextBox, 1);
            mainGrid.Children.Add(valueTextBox);

            // Delete button
            var deleteButton = new Button
            {
                Content = "×",
                Width = 24,
                Height = 24,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(4, 0, 0, 0),
                Tag = item
            };
            deleteButton.Click += (s, e) =>
            {
                if (s is Button btn && btn.Tag is HttpKeyValueItemViewModel itemToRemove)
                {
                    collection.Remove(itemToRemove);
                }
            };
            Grid.SetRow(deleteButton, 0);
            Grid.SetColumn(deleteButton, 2);
            mainGrid.Children.Add(deleteButton);

            // Row 2: Source Node and Output Key ComboBoxes (Node 2/5, Key 3/5)
            var bindingPanel = new Grid
            {
                Margin = new Thickness(0, 4, 0, 0)
            };
            bindingPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });   // Node: 2/5
            bindingPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });   // Key: 3/5

            var nodeLabel = new TextBlock
            {
                Text = "Node:",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };

            var sourceNodeCombo = new ComboBox
            {
                Height = 28,
                Width = 150,
                Style = (Style)FindResource("BaseComboBox"),
                DisplayMemberPath = "Title",
                SelectedValuePath = "NodeId",
                Margin = new Thickness(0, 0, 8, 0)
            };
            sourceNodeCombo.SetBinding(ComboBox.ItemsSourceProperty, new Binding(nameof(HttpKeyValueItemViewModel.AvailableSources))
            {
                Source = item
            });
            sourceNodeCombo.SetBinding(ComboBox.SelectedValueProperty, new Binding(nameof(HttpKeyValueItemViewModel.SourceNodeId))
            {
                Source = item,
                Mode = BindingMode.TwoWay
            });

            var keyLabel = new TextBlock
            {
                Text = "Key:",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };

            var outputKeyCombo = new ComboBox
            {
                Height = 28,
                Width = 150,
                Style = (Style)FindResource("BaseComboBox"),
                DisplayMemberPath = "DisplayName",
                SelectedValuePath = "Key"
            };
            outputKeyCombo.SetBinding(ComboBox.ItemsSourceProperty, new Binding(nameof(HttpKeyValueItemViewModel.AvailableOutputKeys))
            {
                Source = item
            });
            outputKeyCombo.SetBinding(ComboBox.SelectedValueProperty, new Binding(nameof(HttpKeyValueItemViewModel.SourceOutputKey))
            {
                Source = item,
                Mode = BindingMode.TwoWay
            });

            var nodeStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0) };
            nodeStack.Children.Add(nodeLabel);
            nodeStack.Children.Add(sourceNodeCombo);
            Grid.SetColumn(nodeStack, 0);
            bindingPanel.Children.Add(nodeStack);

            var keyStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0) };
            keyStack.Children.Add(keyLabel);
            keyStack.Children.Add(outputKeyCombo);
            Grid.SetColumn(keyStack, 1);
            bindingPanel.Children.Add(keyStack);

            Grid.SetRow(bindingPanel, 1);
            Grid.SetColumn(bindingPanel, 0);
            Grid.SetColumnSpan(bindingPanel, 3);
            mainGrid.Children.Add(bindingPanel);

            border.Child = mainGrid;
            return border;
        }

        #endregion

        #region Title Color

        private void TitleColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTitleColorPreview();
        }

        private void UpdateTitleColorPreview()
        {
            if (TitleColorPreview == null || _viewModel == null) return;

            var colorKey = _viewModel.TitleColorKey;
            Brush? brush = null;

            if (string.IsNullOrEmpty(colorKey) || colorKey == "NodeColor")
            {
                // Use node brush
                if (_viewModel is HttpRequestNodeDialogViewModel vm && vm.Node is HttpRequestNode node)
                {
                    brush = node.NodeBrush;
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

        #endregion
    }
}

