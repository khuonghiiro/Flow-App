using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class ScreenCaptureNodeDialog : BaseNodeDialog
    {
        private readonly ScreenCaptureNodeDialogViewModel _viewModel;

        public ScreenCaptureNodeDialog(ScreenCaptureNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            _viewModel = new ScreenCaptureNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void BeforeSaveOnClose()
        {
            // Flush NodeSearchComboBoxUserControl bindings (coord source)
            FlushNodeSearchComboBoxBinding(CoordSourceNodeId);

            // Flush output key combobox (coord)
            FlushComboBoxBinding(CoordSourceOutputKey);

            // Flush target window comboboxes (both ScreenCapturePanel and ManualRegionPanel)
            FlushComboBoxBinding(SelectedTargetWindow);
            FlushComboBoxBinding(ManualRegionTargetWindow);

            // Flush NodeSearchComboBoxUserControl bindings (path source)
            FlushNodeSearchComboBoxBinding(PathSourceNodeId);

            // Flush output key combobox (path)
            FlushComboBoxBinding(PathSourceOutputKey);
        }

        private static void FlushComboBoxBinding(System.Windows.Controls.ComboBox? cb)
            => cb?.GetBindingExpression(System.Windows.Controls.ComboBox.SelectedValueProperty)?.UpdateSource();

        private static void FlushNodeSearchComboBoxBinding(Controls.NodeSearchComboBoxUserControl? nsc)
        {
            if (nsc != null)
            {
                var be = nsc.GetBindingExpression(Controls.NodeSearchComboBoxUserControl.SelectedValueProperty);
                be?.UpdateSource();
            }
        }

        /// <summary>
        /// Tạo UI cho mỗi output item — checkbox bật/tắt từng key output,
        /// giống pattern của ImageProcessingNodeDialog.
        /// </summary>
        protected override FrameworkElement CreateOutputItemUI(OutputItemViewModel outputVm)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var checkbox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Checked = output key này khi chạy node"
            };
            BindThemeResource(checkbox, Control.ForegroundProperty, "TextBrush");

            var scNode = _viewModel.Node as ScreenCaptureNode;
            if (scNode != null)
            {
                checkbox.IsChecked = !scNode.SkipOutputs.Contains(outputVm.Key);

                checkbox.Checked += (s, e) =>
                {
                    scNode.SkipOutputs.Remove(outputVm.Key);
                };
                checkbox.Unchecked += (s, e) =>
                {
                    if (!scNode.SkipOutputs.Contains(outputVm.Key))
                        scNode.SkipOutputs.Add(outputVm.Key);
                };
            }

            Grid.SetColumn(checkbox, 0);
            grid.Children.Add(checkbox);

            var keyLabel = new TextBlock
            {
                Text = $"Key: {outputVm.Key}",
                FontSize = 12,
                Opacity = 0.9,
                VerticalAlignment = VerticalAlignment.Center
            };
            BindThemeResource(keyLabel, TextBlock.ForegroundProperty, "TextBrush");
            Grid.SetColumn(keyLabel, 1);
            grid.Children.Add(keyLabel);

            stack.Children.Add(grid);

            // Hiển thị giá trị — base64 chỉ hiện 50 ký tự đầu + "..."
            bool isBase64Key = outputVm.Key != null &&
                (outputVm.Key.IndexOf("base64", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 outputVm.Key.IndexOf("Base64", StringComparison.OrdinalIgnoreCase) >= 0);

            string displayValue = outputVm.Value ?? string.Empty;
            if (isBase64Key && displayValue.Length > 50)
                displayValue = displayValue.Substring(0, 50) + "...";

            var valueText = new TextBlock
            {
                Text         = displayValue,
                FontSize     = 11,
                Opacity      = 0.75,
                Margin       = new Thickness(24, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            BindThemeResource(valueText, TextBlock.ForegroundProperty, "TextBrush");
            stack.Children.Add(valueText);

            return stack;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Flush ReuseRoutes ComboBox bindings (including FunctionType)
            FlushReuseRoutesComboBoxBindings();
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        private void FlushReuseRoutesComboBoxBindings()
        {
            // Find all ComboBox controls in the ReuseRoutes ItemsControl and flush their bindings
            var itemsControl = this.FindName("ReuseRoutesItemsControl") as ItemsControl;
            if (itemsControl != null)
            {
                foreach (var item in itemsControl.Items)
                {
                    if (itemsControl.ItemContainerGenerator.ContainerFromItem(item) is ContentPresenter contentPresenter)
                    {
                        // Find all ComboBox controls in the visual tree
                        var comboBoxes = FindVisualChildren<ComboBox>(contentPresenter);
                        foreach (var comboBox in comboBoxes)
                        {
                            comboBox.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
                            comboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                        }
                    }
                }
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    yield return result;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Chọn ảnh",
                    Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (dlg.ShowDialog(this) == true)
                {
                    _viewModel.ImagePath = dlg.FileName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không mở được file: " + ex.Message, "Ảnh",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ManualCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            var node = _viewModel.Node as ScreenCaptureNode;
            if (node == null) return;

            // Use ScreenCaptureHelper to capture region
            bool success = Helpers.ScreenCaptureHelper.CaptureForScreenCaptureNode(node, this);

            if (success)
            {
                // Update coordinate display
                UpdateManualRegionCoordinates();
            }
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            
            // Update manual region coordinates display on load
            UpdateManualRegionCoordinates();
        }

        private void UpdateManualRegionCoordinates()
        {
            var node = _viewModel.Node as ScreenCaptureNode;
            if (node == null) return;

            var coordText = this.FindName("ManualRegionCoordinates") as TextBlock;
            if (coordText == null) return;

            if (node.HasCaptureRegion)
            {
                coordText.Text = $"X: {node.CaptureX}, Y: {node.CaptureY}, W: {node.CaptureWidth}, H: {node.CaptureHeight}";
                BindThemeResource(coordText, TextBlock.ForegroundProperty, "SuccessBrush");
            }
            else
            {
                coordText.Text = "Chưa chụp vùng nào";
                BindThemeResource(coordText, TextBlock.ForegroundProperty, "TextMuted");
            }
        }
    }
}
