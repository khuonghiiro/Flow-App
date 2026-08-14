using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
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

            // Set height to 3/5 of work area height dynamically
            var workArea = SystemParameters.WorkArea;
            this.Height = workArea.Height * 0.6;

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

            Dispatcher.BeginInvoke(new Action(() =>
            {
                HeadersScrollViewer?.ScrollToBottom();
            }), System.Windows.Threading.DispatcherPriority.Background);
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

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ParamsScrollViewer?.ScrollToBottom();
            }), System.Windows.Threading.DispatcherPriority.Background);
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

            Dispatcher.BeginInvoke(new Action(() =>
            {
                FormDataScrollViewer?.ScrollToBottom();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void AddFormData_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AddFormDataCommand.Execute(null);
        }

        #endregion

        #region cURL Paste Detection

        private async void ImportCurl_Click(object sender, RoutedEventArgs e)
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

                // Show Loading Overlay
                LoadingOverlay.Visibility = Visibility.Visible;
                Mouse.OverrideCursor = Cursors.Wait;
                await Task.Delay(100);

                bool success = false;
                string errorMsg = string.Empty;

                await Dispatcher.InvokeAsync(() =>
                {
                    // Reset UI first (clear panels before parsing)
                    HeadersPanel.Children.Clear();
                    ParamsPanel.Children.Clear();
                    FormDataItemsPanel.Children.Clear();

                    success = _viewModel.ParseAndApplyCurl(clipboardText, out errorMsg);
                }, System.Windows.Threading.DispatcherPriority.Background);

                if (success)
                {
                    _lastProcessedText = _viewModel.Url;
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        // Re-render the panels with cleaned data
                        RenderHeaders();
                        RenderParams();
                        RenderFormData();

                        // Update password box if Basic auth
                        if (!string.IsNullOrEmpty(_viewModel.AuthPassword))
                        {
                            PasswordBox.Password = _viewModel.AuthPassword;
                        }
                    }, System.Windows.Threading.DispatcherPriority.Background);

                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    Mouse.OverrideCursor = null;

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
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    Mouse.OverrideCursor = null;

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
                LoadingOverlay.Visibility = Visibility.Collapsed;
                Mouse.OverrideCursor = null;
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

                _ = ProcessCurlAsync(clipboardText);
                return true; // handled
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"cURL parse exception: {ex.Message}");
            }

            return false;
        }

        private async Task ProcessCurlAsync(string curlText)
        {
            try
            {
                _isProcessingPaste = true;
                LoadingOverlay.Visibility = Visibility.Visible;
                Mouse.OverrideCursor = Cursors.Wait;
                await Task.Delay(100);

                bool success = false;
                string errorMsg = string.Empty;

                await Dispatcher.InvokeAsync(() =>
                {
                    HeadersPanel.Children.Clear();
                    ParamsPanel.Children.Clear();
                    FormDataItemsPanel.Children.Clear();

                    success = _viewModel.ParseAndApplyCurl(curlText, out errorMsg);
                }, System.Windows.Threading.DispatcherPriority.Background);

                if (success)
                {
                    _lastProcessedText = _viewModel.Url;
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        RenderHeaders();
                        RenderParams();
                        RenderFormData();

                        if (!string.IsNullOrEmpty(_viewModel.AuthPassword))
                        {
                            PasswordBox.Password = _viewModel.AuthPassword;
                        }
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"cURL parse error: {errorMsg}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"cURL parse exception: {ex.Message}");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                Mouse.OverrideCursor = null;
                _isProcessingPaste = false;
            }
        }

        #endregion

        #region Key-Value Row Builder

        private Border CreateKeyValueRow(HttpKeyValueItemViewModel item, ObservableCollection<HttpKeyValueItemViewModel> collection, int index, string prefix)
        {
            var border = new Border
            {
                Margin = new Thickness(0, 0, 0, 6)
            };

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });   // Key: 2/10
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });   // Value: 3/10
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) }); // Bind Node: 2.5/10
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) }); // Bind Key: 2.5/10
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                        // Delete

            // Key TextBox
            var keyTextBox = new TextBox
            {
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                Style = (Style)FindResource("BaseTextBoxV2"),
                ToolTip = "Khóa (Key)"
            };
            keyTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(HttpKeyValueItemViewModel.Key))
            {
                Source = item,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            Grid.SetColumn(keyTextBox, 0);
            mainGrid.Children.Add(keyTextBox);

            // Value TextBox
            var valueTextBox = new TextBox
            {
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                Style = (Style)FindResource("BaseTextBoxV2"),
                ToolTip = "Giá trị (Value)"
            };
            valueTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(HttpKeyValueItemViewModel.Value))
            {
                Source = item,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            Grid.SetColumn(valueTextBox, 1);
            mainGrid.Children.Add(valueTextBox);

            // Bind Node ComboBox
            var sourceNodeCombo = new ComboBox
            {
                Height = 30,
                Style = (Style)FindResource("BaseComboBox"),
                DisplayMemberPath = "Title",
                SelectedValuePath = "NodeId",
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Liên kết với Node"
            };
            sourceNodeCombo.SetBinding(ComboBox.ItemsSourceProperty, new Binding(nameof(HttpKeyValueItemViewModel.AvailableSources)) { Source = item });
            sourceNodeCombo.SetBinding(ComboBox.SelectedValueProperty, new Binding(nameof(HttpKeyValueItemViewModel.SourceNodeId)) { Source = item, Mode = BindingMode.TwoWay });
            Grid.SetColumn(sourceNodeCombo, 2);
            mainGrid.Children.Add(sourceNodeCombo);

            // Bind Key ComboBox
            var outputKeyCombo = new ComboBox
            {
                Height = 30,
                Style = (Style)FindResource("BaseComboBox"),
                DisplayMemberPath = "DisplayName",
                SelectedValuePath = "Key",
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Liên kết với Khóa Output"
            };
            outputKeyCombo.SetBinding(ComboBox.ItemsSourceProperty, new Binding(nameof(HttpKeyValueItemViewModel.AvailableOutputKeys)) { Source = item });
            outputKeyCombo.SetBinding(ComboBox.SelectedValueProperty, new Binding(nameof(HttpKeyValueItemViewModel.SourceOutputKey)) { Source = item, Mode = BindingMode.TwoWay });
            Grid.SetColumn(outputKeyCombo, 3);
            mainGrid.Children.Add(outputKeyCombo);

            // Delete button
            var deleteButton = new Button
            {
                Style = (Style)FindResource("DeleteRowButton"),
                Margin = new Thickness(0),
                Tag = item
            };
            deleteButton.Click += (s, e) =>
            {
                if (s is Button btn && btn.Tag is HttpKeyValueItemViewModel itemToRemove)
                {
                    collection.Remove(itemToRemove);
                }
            };
            Grid.SetColumn(deleteButton, 4);
            mainGrid.Children.Add(deleteButton);

            border.Child = mainGrid;
            return border;
        }

        #endregion

        private void FormatJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var text = _viewModel.RawBody;
                if (string.IsNullOrWhiteSpace(text)) return;

                using (var doc = System.Text.Json.JsonDocument.Parse(text))
                {
                    var formatted = System.Text.Json.JsonSerializer.Serialize(doc, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    _viewModel.RawBody = formatted;
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                MessageBox.Show($"JSON không hợp lệ: {ex.Message}", "Lỗi định dạng JSON", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}

