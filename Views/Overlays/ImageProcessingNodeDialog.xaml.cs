using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class ImageProcessingNodeDialog : BaseNodeDialog
    {
        private readonly ImageProcessingNodeDialogViewModel _viewModel;

        public ImageProcessingNodeDialog(ImageProcessingNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            _viewModel = new ImageProcessingNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            // Ensure node options loaded (workflow can change titles)
            // Key options sẽ tự động refresh qua PropertyChanged handler trong ViewModel
            _viewModel.RefreshAvailableNodes();
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override FrameworkElement CreateOutputItemUI(ViewModels.OutputItemViewModel outputVm)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Checkbox: checked = bật output key này (không skip), unchecked = tắt (skip)
            var checkbox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Checked = output key này khi chạy node"
            };
            BindThemeResource(checkbox, Control.ForegroundProperty, "TextBrush");

            var imageNode = _viewModel.Node as Models.Nodes.ImageProcessingNode;
            if (imageNode != null)
            {
                // Checked = không bị skip = đang được output
                checkbox.IsChecked = !imageNode.SkipOutputs.Contains(outputVm.Key);

                checkbox.Checked += (s, e) =>
                {
                    // Bỏ skip → key này sẽ được output
                    imageNode.SkipOutputs.Remove(outputVm.Key);
                };
                checkbox.Unchecked += (s, e) =>
                {
                    // Thêm vào skip → key này không output
                    if (!imageNode.SkipOutputs.Contains(outputVm.Key))
                        imageNode.SkipOutputs.Add(outputVm.Key);
                };
            }

            Grid.SetColumn(checkbox, 0);
            grid.Children.Add(checkbox);

            var keyLabel = new TextBlock
            {
                Text = $"Key: {outputVm.Key}",
                FontSize = 12,
                Opacity = 0.9,
                Margin = new Thickness(0, 0, 0, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            BindThemeResource(keyLabel, TextBlock.ForegroundProperty, "TextBrush");
            Grid.SetColumn(keyLabel, 1);
            grid.Children.Add(keyLabel);

            stack.Children.Add(grid);

            var valueText = new TextBlock
            {
                FontSize = 11,
                Opacity = 0.9,
                Margin = new Thickness(0, 4, 0, 0)
            };
            BindThemeResource(valueText, TextBlock.ForegroundProperty, "TextBrush");

            valueText.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(ViewModels.OutputItemViewModel.Value))
                {
                    Source = outputVm
                });

            stack.Children.Add(valueText);

            return stack;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        private void OpenImageButton_Click(object sender, RoutedEventArgs e)
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
                    _viewModel.ImageUrl = dlg.FileName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không mở được file: " + ex.Message, "Ảnh", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UrlNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ViewModel đã tự động refresh qua PropertyChanged handler
            // Không cần set ItemsSource trực tiếp vì đã bind trong XAML
        }

        private void Base64NodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ViewModel đã tự động refresh qua PropertyChanged handler
            // Không cần set ItemsSource trực tiếp vì đã bind trong XAML
        }

        private void RefreshUrlKeyOptions()
        {
            // Không cần thiết nữa - ViewModel tự động refresh qua binding
        }

        private void RefreshBase64KeyOptions()
        {
            // Không cần thiết nữa - ViewModel tự động refresh qua binding
        }

        private void FilterExample_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string filter)
            {
                _viewModel.FfmpegFilter = filter;
            }
        }

        private void CroppedFolderNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ViewModel đã tự động refresh qua PropertyChanged handler
            // Clear selected key khi node thay đổi
            if (e.RemovedItems.Count > 0 && e.AddedItems.Count > 0)
            {
                _viewModel.CroppedFolderSourceOutputKey = null;
            }
        }

        private void RenderNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ViewModel đã tự động refresh qua PropertyChanged handler
            // Clear selected key khi node thay đổi
            if (e.RemovedItems.Count > 0 && e.AddedItems.Count > 0)
            {
                _viewModel.RenderNodeOutputKey = null;
            }
        }

        private void RefreshRenderNodeKeyOptions()
        {
            // Không cần thiết nữa - ViewModel tự động refresh qua binding
        }
    }
}


