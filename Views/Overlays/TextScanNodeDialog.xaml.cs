using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class TextScanNodeDialog : BaseNodeDialog
    {
        private readonly TextScanNodeDialogViewModel _viewModel;

        public TextScanNodeDialog(TextScanNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            _viewModel = new TextScanNodeDialogViewModel(node, host);
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

            // Flush target window combobox
            FlushComboBoxBinding(SelectedTargetWindow);

            // Flush NodeSearchComboBoxUserControl bindings (image source)
            FlushNodeSearchComboBoxBinding(ImageSourceNodeId);

            // Flush output key combobox (image)
            FlushComboBoxBinding(ImageSourceOutputKey);
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

            var textScanNode = _viewModel.Node as TextScanNode;
            if (textScanNode != null)
            {
                checkbox.IsChecked = !textScanNode.SkipOutputs.Contains(outputVm.Key);

                checkbox.Checked += (s, e) =>
                {
                    textScanNode.SkipOutputs.Remove(outputVm.Key);
                };
                checkbox.Unchecked += (s, e) =>
                {
                    if (!textScanNode.SkipOutputs.Contains(outputVm.Key))
                        textScanNode.SkipOutputs.Add(outputVm.Key);
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
                Text = displayValue,
                FontSize = 11,
                Opacity = 0.75,
                Margin = new Thickness(24, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            BindThemeResource(valueText, TextBlock.ForegroundProperty, "TextBrush");
            stack.Children.Add(valueText);

            return stack;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Chọn ảnh để OCR",
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

        private void BrowseTessdataButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Use OpenFileDialog to select a file, then get its directory
                var dlg = new OpenFileDialog
                {
                    Title = "Chọn một file trong thư mục tessdata",
                    Filter = "Trained Data Files (*.traineddata)|*.traineddata|All Files|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (dlg.ShowDialog(this) == true)
                {
                    var directory = System.IO.Path.GetDirectoryName(dlg.FileName);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        _viewModel.TessdataPath = directory;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không chọn được thư mục: " + ex.Message, "Tessdata",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
