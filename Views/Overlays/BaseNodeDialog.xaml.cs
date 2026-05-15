using FlowMy.Controls;
using FlowMy.Models;
using FlowMy.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    /// <summary>
    /// Base class cho các node dialog, chứa các phần chung như LoadInputs, LoadOutputs, CreateInputItemUI, CreateOutputItemUI.
    /// </summary>
    public abstract partial class BaseNodeDialog : Window
    {
        protected BaseNodeDialogViewModel ViewModel { get; private set; } = null!;

        // Protected constructor cho derived classes
        protected BaseNodeDialog()
        {
            // XAML initialization sẽ được gọi bởi derived class
        }

        // Method để set ViewModel từ derived class
        protected void SetViewModel(BaseNodeDialogViewModel viewModel)
        {
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        // Method để initialize từ derived class
        protected void InitializeBase(BaseNodeDialogViewModel viewModel, Window? owner)
        {
            try
            {
                // Không gọi InitializeComponent() ở đây vì base class không có XAML
                // Các derived class sẽ gọi InitializeComponent() trong constructor của chúng
                Owner = owner;
                ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
                DataContext = ViewModel;
                // Sử dụng Manual để tự tính toán vị trí (snap to right side)
                WindowStartupLocation = WindowStartupLocation.Manual;

                // Load inputs và outputs vào UI sau khi dialog đã được load
                Loaded += BaseNodeDialog_Loaded;

                // Ngăn dialog bị đóng khi click vào nó hoặc mất focus
                Deactivated += (s, e) =>
                {
                    // Không làm gì - để dialog vẫn mở
                };

                // Lưu title khi đóng
                Closing += (s, e) =>
                {
                    try
                    {
                        BeforeSaveOnClose();
                        ViewModel.SaveTitleCommand.Execute(null);
                    }
                    catch { }
                };

                // Đảm bảo dialog không bị đóng khi click vào nó
                PreviewMouseDown += (s, e) =>
                {
                    // Chỉ ngăn event bubble lên owner window
                    // Không set e.Handled = true để các controls bên trong vẫn nhận được events
                };

                // Sau khi window source khởi tạo, tính vị trí snap phải
                SourceInitialized += (s, e) => PositionDialogRightSnap();

                // Khi kích thước thay đổi, clamp lại để không tràn màn hình
                SizeChanged += (s, e) => ClampToScreen();

                // Setup PropertyChanged handlers
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi khởi tạo dialog: {ex.Message}\n\n{ex.StackTrace}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        /// <summary>
        /// Đặt dialog sát cạnh phải màn hình, căn giữa chiều dọc, giới hạn MaxHeight/MaxWidth theo WorkArea.
        /// </summary>
        private void PositionDialogRightSnap()
        {
            try
            {
                var workArea = SystemParameters.WorkArea;

                // Giới hạn kích thước tối đa
                this.MaxHeight = workArea.Height * 0.92;
                this.MaxWidth = Math.Max(400, workArea.Width * 0.50);

                if (this.ActualWidth > this.MaxWidth)
                    this.Width = this.MaxWidth;
                if (this.ActualHeight > this.MaxHeight)
                    this.Height = this.MaxHeight;

                // Snap sát cạnh phải (margin 8px)
                double left = workArea.Right - this.ActualWidth - 8;
                // Căn giữa chiều dọc
                double top = workArea.Top + (workArea.Height - this.ActualHeight) / 2.0;

                // Clamp để không tràn màn hình
                left = Math.Max(workArea.Left, Math.Min(left, workArea.Right - this.ActualWidth));
                top = Math.Max(workArea.Top, Math.Min(top, workArea.Bottom - this.ActualHeight));

                this.Left = left;
                this.Top = top;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BaseNodeDialog] PositionDialogRightSnap error: {ex.Message}");
            }
        }

        /// <summary>
        /// Clamp vị trí dialog vào trong WorkArea sau khi resize.
        /// </summary>
        private void ClampToScreen()
        {
            try
            {
                var workArea = SystemParameters.WorkArea;
                double right = this.Left + this.ActualWidth;
                double bottom = this.Top + this.ActualHeight;

                if (right > workArea.Right)
                    this.Left = Math.Max(workArea.Left, workArea.Right - this.ActualWidth - 8);
                if (bottom > workArea.Bottom)
                    this.Top = Math.Max(workArea.Top, workArea.Bottom - this.ActualHeight - 8);
                if (this.Left < workArea.Left)
                    this.Left = workArea.Left;
                if (this.Top < workArea.Top)
                    this.Top = workArea.Top;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BaseNodeDialog] ClampToScreen error: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy Brush từ theme resource, fallback về giá trị mặc định nếu không tìm thấy.
        /// </summary>
        protected Brush GetThemeBrush(string resourceKey, Brush fallback)
            => Application.Current?.TryFindResource(resourceKey) as Brush ?? fallback;

        /// <summary>
        /// Lấy Color từ theme resource, fallback về giá trị mặc định nếu không tìm thấy.
        /// </summary>
        protected Color GetThemeColor(string resourceKey, Color fallback)
            => (Application.Current?.TryFindResource(resourceKey) as SolidColorBrush)?.Color ?? fallback;

        /// <summary>
        /// Gắn một DP với resource ứng dụng (giống DynamicResource) để control sinh trong code-behind
        /// vẫn đổi màu đúng khi người dùng đổi theme.
        /// </summary>
        protected static void BindThemeResource(FrameworkElement element, DependencyProperty dp, string resourceKey)
            => element.SetResourceReference(dp, resourceKey);

        /// <summary>
        /// Override để xử lý các PropertyChanged events từ ViewModel.
        /// </summary>
        protected virtual void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) { }

        /// <summary>Gọi ngay trước <see cref="BaseNodeDialogViewModel.SaveTitleCommand"/> khi đóng dialog (flush binding, v.v.).</summary>
        protected virtual void BeforeSaveOnClose() { }

        private void BaseNodeDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Setup inputs/outputs panels after dialog is loaded
            try
            {
                SetupInputsOutputs();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading inputs/outputs: {ex.Message}");
            }
        }

        private void SetupInputsOutputs()
        {
            var inputsPanel = GetInputsPanel();
            var outputsPanel = GetOutputsPanel();

            if (inputsPanel != null && ViewModel != null)
            {
                LoadInputs();
            }

            if (outputsPanel != null && ViewModel != null)
            {
                LoadOutputs();
            }

            OnLoaded();
        }

        /// <summary>
        /// Override để thêm logic khi dialog được load.
        /// </summary>
        protected virtual void OnLoaded() { }

        /// <summary>
        /// Override để cung cấp panel chứa inputs.
        /// </summary>
        protected abstract Panel? GetInputsPanel();

        /// <summary>
        /// Override để cung cấp panel chứa outputs.
        /// </summary>
        protected abstract Panel? GetOutputsPanel();

        protected virtual void LoadInputs()
        {
            var panel = GetInputsPanel();
            if (panel == null) return;

            panel.Children.Clear();

            foreach (var inputVm in ViewModel.Inputs)
            {
                var item = CreateInputItemUI(inputVm);
                panel.Children.Add(item);
            }
        }

        protected virtual void LoadOutputs()
        {
            var panel = GetOutputsPanel();
            if (panel == null) return;

            panel.Children.Clear();

            foreach (var outputVm in ViewModel.Outputs)
            {
                var item = CreateOutputItemUI(outputVm);
                panel.Children.Add(item);
            }
        }

        /// <summary>
        /// Tạo UI cho một input item. Có thể override để tùy chỉnh.
        /// </summary>
        protected virtual FrameworkElement CreateInputItemUI(InputItemViewModel inputVm)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var textBrush = GetThemeBrush("TextBrush", Brushes.White);
            var mutedBrush = GetThemeBrush("TextMuted", Brushes.Gray);

            // Key label
            var keyLabel = new TextBlock
            {
                Text = $"Key: {inputVm.Key}",
                Foreground = textBrush,
                FontSize = 12,
                Opacity = 0.9,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(keyLabel);

            // Giá trị mặc định
            // Mặc định ẩn dòng mô tả "giá trị mặc định" để các dialog con có thể tuỳ biến riêng
            var valueDef = new TextBlock
            {
                Text = $"Số lần nhấn mặc định: 1 Lần",
                Foreground = textBrush,
                FontSize = 12,
                Opacity = 0.9,
                Margin = new Thickness(0, 0, 0, 4),
                Visibility = Visibility.Collapsed
            };
            stack.Children.Add(valueDef);

            // Source node combo
            var sourceCombo = new NodeSearchComboBoxUserControl
            {
                Height = 36,
                Margin = new Thickness(0, 0, 0, 6),
                ItemsSource = inputVm.AvailableSources,
                SelectedValuePath = nameof(WorkflowDataSourceOption.NodeId),
                DisplayMemberPath = nameof(WorkflowDataSourceOption.Title),
                SelectedValue = inputVm.SelectedSourceNodeId
            };

            sourceCombo.SetBinding(NodeSearchComboBoxUserControl.SelectedValueProperty,
                new System.Windows.Data.Binding(nameof(InputItemViewModel.SelectedSourceNodeId))
                {
                    Source = inputVm,
                    Mode = System.Windows.Data.BindingMode.TwoWay
                });

            var outputKeyCombo = new ComboBox
            {
                Height = 36,
                Margin = new Thickness(0, 0, 0, 6),
                Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                SelectedValuePath = nameof(WorkflowOutputKeyOption.Key),
                Visibility = (inputVm.AvailableOutputKeyOptions?.Count ?? 0) > 1 ? Visibility.Visible : Visibility.Collapsed
            };

            // Bind ItemsSource để tự động update khi AvailableOutputKeyOptions thay đổi
            outputKeyCombo.SetBinding(ComboBox.ItemsSourceProperty,
                new System.Windows.Data.Binding(nameof(InputItemViewModel.AvailableOutputKeyOptions))
                {
                    Source = inputVm,
                    Mode = System.Windows.Data.BindingMode.OneWay
                });

            outputKeyCombo.SetBinding(ComboBox.SelectedValueProperty,
                new System.Windows.Data.Binding(nameof(InputItemViewModel.SelectedSourceOutputKey))
                {
                    Source = inputVm,
                    Mode = System.Windows.Data.BindingMode.TwoWay
                });

            // Refresh outputKeyCombo khi source node thay đổi
            inputVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(InputItemViewModel.AvailableOutputKeyOptions))
                {
                    // Refresh ItemsSource binding
                    var binding = outputKeyCombo.GetBindingExpression(ComboBox.ItemsSourceProperty);
                    binding?.UpdateTarget();

                    // Update visibility dựa trên số lượng options
                    outputKeyCombo.Visibility = (inputVm.AvailableOutputKeyOptions?.Count ?? 0) > 1
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                    // Refresh SelectedValue để đảm bảo giá trị được cập nhật
                    var selectedBinding = outputKeyCombo.GetBindingExpression(ComboBox.SelectedValueProperty);
                    selectedBinding?.UpdateTarget();
                }
            };

            // Value display
            var valueText = new TextBlock
            {
                Foreground = textBrush,
                FontSize = 11,
                Opacity = 0.9,
                Margin = new Thickness(0, 4, 0, 0)
            };

            valueText.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(InputItemViewModel.Value))
                {
                    Source = inputVm
                });

            var valueKey = new TextBlock
            {
                Foreground = textBrush,
                FontSize = 11,
                Opacity = 0.9,
                Margin = new Thickness(0, 4, 0, 0)
            };

            valueKey.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(InputItemViewModel.SelectedSourceOutputKey))
                {
                    Source = inputVm,
                    StringFormat = "Output key: {0}"
                });

            // Validation error message (chỉ hiển thị cho repeatCount)
            var dangerBrush = GetThemeBrush("DangerColor", new SolidColorBrush(Color.FromRgb(255, 100, 100)));
            var errorText = new TextBlock
            {
                FontSize = 10,
                Foreground = dangerBrush,
                Opacity = 0.9,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };

            // Validation function cho repeatCount
            bool isRepeatCountInput = string.Equals(inputVm.Key, "repeatCount", StringComparison.OrdinalIgnoreCase);

            if (isRepeatCountInput)
            {
                // Validate khi Value thay đổi
                inputVm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(InputItemViewModel.Value))
                    {
                        ValidateRepeatCountValue(inputVm.Value, valueText, errorText);
                    }
                };

                // Validate lần đầu
                ValidateRepeatCountValue(inputVm.Value, valueText, errorText);
            }

            stack.Children.Add(new TextBlock { Text = "Source Node:", Foreground = mutedBrush, FontSize = 11, Opacity = 0.8, Margin = new Thickness(0, 0, 0, 2) });
            stack.Children.Add(sourceCombo);
            //stack.Children.Add(new TextBlock { Text = "Output Key:", Foreground = Brushes.White, FontSize = 11, Opacity = 0.8, Margin = new Thickness(0, 0, 0, 2) });
            stack.Children.Add(valueKey);
            stack.Children.Add(outputKeyCombo);
            stack.Children.Add(valueText);

            // Thêm errorText sau valueText
            if (isRepeatCountInput)
            {
                stack.Children.Add(errorText);
            }

            return stack;
        }

        /// <summary>
        /// Validate giá trị repeatCount có phải số hợp lệ không.
        /// </summary>
        protected virtual void ValidateRepeatCountValue(string? value, TextBlock valueText, TextBlock errorText)
        {
            var textBrush = GetThemeBrush("TextBrush", Brushes.White);
            var dangerBrush = GetThemeBrush("DangerColor", new SolidColorBrush(Color.FromRgb(255, 100, 100)));
            var warningBrush = GetThemeBrush("WarningColor", new SolidColorBrush(Color.FromRgb(255, 165, 0)));

            if (string.IsNullOrWhiteSpace(value) || value == "—")
            {
                // Không có giá trị hoặc giá trị mặc định - không hiển thị lỗi
                valueText.Foreground = textBrush;
                errorText.Visibility = Visibility.Collapsed;
                errorText.Text = string.Empty;
                return;
            }

            var valueStr = value.Trim();

            // Kiểm tra có phải số không (int hoặc double)
            bool isValidNumber = false;
            double numValue = 0;

            // Thử parse integer
            if (int.TryParse(valueStr, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var intVal) ||
                int.TryParse(valueStr, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.CurrentCulture, out intVal))
            {
                isValidNumber = true;
                numValue = intVal;
            }
            // Thử parse double
            else if (double.TryParse(valueStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var dblVal) ||
                double.TryParse(valueStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture, out dblVal))
            {
                isValidNumber = true;
                numValue = dblVal;
            }

            if (isValidNumber)
            {
                // Kiểm tra giá trị có hợp lệ không (>= 1)
                if (numValue < 1)
                {
                    errorText.FontSize = 14;
                    valueText.Foreground = warningBrush;
                    errorText.Text = "⚠ Giá trị phải >= 1";
                    errorText.Visibility = Visibility.Visible;
                }
                else
                {
                    errorText.FontSize = 12;
                    // Hợp lệ
                    valueText.Foreground = textBrush;
                    errorText.Visibility = Visibility.Collapsed;
                    errorText.Text = string.Empty;
                }
            }
            else
            {
                errorText.FontSize = 14;
                // Không phải số - hiển thị lỗi
                valueText.Foreground = dangerBrush;
                errorText.Text = "⚠ Giá trị phải là số (ví dụ: 1, 2, 3)";
                errorText.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Tạo UI cho một output item.
        /// </summary>
        protected virtual FrameworkElement CreateOutputItemUI(OutputItemViewModel outputVm)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var textBrush = GetThemeBrush("TextBrush", Brushes.White);

            var keyLabel = new TextBlock
            {
                Text = $"Key: {outputVm.Key}",
                Foreground = textBrush,
                FontSize = 12,
                Opacity = 0.9,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(keyLabel);

            var valueText = new TextBlock
            {
                Foreground = textBrush,
                FontSize = 11,
                Opacity = 0.9
            };

            valueText.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(OutputItemViewModel.Value))
                {
                    Source = outputVm
                });

            stack.Children.Add(valueText);

            return stack;
        }

        protected virtual void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        /// <summary>
        /// Event handler for TitleColorComboBox.SelectionChanged — updates the color preview.
        /// Derived classes wire this in XAML: SelectionChanged="TitleColorComboBox_SelectionChanged"
        /// </summary>
        protected virtual void TitleColorComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateTitleColorPreview();
        }

        /// <summary>
        /// Updates the TitleColorPreview border background based on the selected color key.
        /// Call this from the constructor after InitializeBase() and from TitleColorComboBox_SelectionChanged.
        /// Requires XAML controls named TitleColorPreview (Border) and TitleColorComboBox (ComboBox).
        /// </summary>
        protected void UpdateTitleColorPreview()
        {
            // Find controls by name — derived classes may have these in their XAML
            var preview = FindName("TitleColorPreview") as System.Windows.Controls.Border;
            var combo = FindName("TitleColorComboBox") as System.Windows.Controls.ComboBox;

            if (preview == null) return;

            // If combo not found or no selection, try to get colorKey from ViewModel
            string? colorKey = combo?.SelectedValue?.ToString();
            if (colorKey == null && ViewModel != null)
                colorKey = (ViewModel.GetType().GetProperty("TitleColorKey")?.GetValue(ViewModel) as string);

            System.Windows.Media.Brush? brush = null;
            if (string.IsNullOrEmpty(colorKey) || colorKey == "NodeColor")
            {
                // Get NodeBrush from ViewModel.Node via reflection
                var node = ViewModel?.GetType().GetProperty("Node")?.GetValue(ViewModel);
                brush = node?.GetType().GetProperty("NodeBrush")?.GetValue(node) as System.Windows.Media.Brush;
            }
            else if (colorKey == "LimeGreen")
            {
                brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
            }
            else
            {
                brush = Application.Current.TryFindResource(colorKey) as System.Windows.Media.Brush;
            }

            preview.Background = brush ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }

        /// <summary>
        /// Opens a WinForms ColorDialog and returns the selected color as #RRGGBB hex string, or null if cancelled.
        /// </summary>
        protected static string? ShowColorPicker(string? currentHex)
        {
            try
            {
                using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
                if (!string.IsNullOrWhiteSpace(currentHex) &&
                    currentHex.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                {
                    try { dialog.Color = System.Drawing.ColorTranslator.FromHtml(currentHex); } catch { }
                }
                return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
                    ? $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"
                    : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Resolves a brush from a hex color string (#RRGGBB), "LimeGreen", or a resource key.
        /// Falls back to the provided fallback brush if resolution fails.
        /// </summary>
        protected static System.Windows.Media.Brush ResolveBrush(string? key, System.Windows.Media.Brush fallback)
        {
            if (string.IsNullOrWhiteSpace(key)) return fallback;
            try
            {
                if (key == "LimeGreen")
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
                if (key.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                {
                    var bc = new System.Windows.Media.BrushConverter();
                    if (bc.ConvertFromString(key) is System.Windows.Media.Brush fromHex) return fromHex;
                }
                var resource = Application.Current.TryFindResource(key);
                if (resource is System.Windows.Media.Brush b) return b;
                if (resource is System.Windows.Media.Color c)
                    return new System.Windows.Media.SolidColorBrush(c);
            }
            catch { }
            return fallback;
        }
    }
}