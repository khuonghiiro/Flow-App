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
            _viewModel.RefreshAvailableNodes();
            RefreshUrlKeyOptions();
            RefreshBase64KeyOptions();
            RefreshRenderNodeKeyOptions();
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override FrameworkElement CreateOutputItemUI(ViewModels.OutputItemViewModel outputVm)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Checkbox để skip output
            var checkbox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Checked = không xử lý output này"
            };

            // Kiểm tra nếu key này có trong SkipOutputs thì checked = true
            var imageNode = _viewModel.Node as Models.Nodes.ImageProcessingNode;
            if (imageNode != null)
            {
                checkbox.IsChecked = imageNode.SkipOutputs.Contains(outputVm.Key);
                checkbox.Checked += (s, e) =>
                {
                    if (!imageNode.SkipOutputs.Contains(outputVm.Key))
                        imageNode.SkipOutputs.Add(outputVm.Key);
                };
                checkbox.Unchecked += (s, e) =>
                {
                    imageNode.SkipOutputs.Remove(outputVm.Key);
                };
            }

            Grid.SetColumn(checkbox, 0);
            grid.Children.Add(checkbox);

            var keyLabel = new TextBlock
            {
                Text = $"Key: {outputVm.Key}",
                Foreground = Brushes.White,
                FontSize = 12,
                Opacity = 0.9,
                Margin = new Thickness(0, 0, 0, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(keyLabel, 1);
            grid.Children.Add(keyLabel);

            stack.Children.Add(grid);

            var valueText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 11,
                Opacity = 0.9,
                Margin = new Thickness(0, 4, 0, 0)
            };

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
            RefreshUrlKeyOptions();
        }

        private void Base64NodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshBase64KeyOptions();
        }

        private void RefreshUrlKeyOptions()
        {
            try
            {
                if (UrlKeyComboBox == null) return;
                UrlKeyComboBox.ItemsSource = _viewModel.GetOutputKeysForNode(_viewModel.ImageUrlSourceNodeId);
            }
            catch { }
        }

        private void RefreshBase64KeyOptions()
        {
            try
            {
                if (Base64KeyComboBox == null) return;
                Base64KeyComboBox.ItemsSource = _viewModel.GetOutputKeysForNode(_viewModel.ImageBase64SourceNodeId);
            }
            catch { }
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
            try
            {
                if (CroppedFolderKeyComboBox == null) return;
                var nodeId = _viewModel.CroppedFolderSourceNodeId;
                CroppedFolderKeyComboBox.ItemsSource = _viewModel.GetOutputKeysForNode(nodeId);
                if (e.RemovedItems.Count > 0 && e.AddedItems.Count > 0)
                {
                    _viewModel.CroppedFolderSourceOutputKey = null;
                }
            }
            catch { }
        }

        private void RenderNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshRenderNodeKeyOptions();
            if (e.RemovedItems.Count > 0 && e.AddedItems.Count > 0)
            {
                _viewModel.RenderNodeOutputKey = null;
            }
        }

        private void RefreshRenderNodeKeyOptions()
        {
            try
            {
                if (RenderNodeKeyComboBox == null) return;
                RenderNodeKeyComboBox.ItemsSource = _viewModel.GetOutputKeysForNode(_viewModel.RenderNodeId);
            }
            catch { }
        }
    }
}


