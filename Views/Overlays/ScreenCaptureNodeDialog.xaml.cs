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

            // Hiển thị giá trị hiện tại (nếu có)
            var valueText = new TextBlock
            {
                FontSize = 11,
                Opacity = 0.75,
                Margin = new Thickness(24, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            BindThemeResource(valueText, TextBlock.ForegroundProperty, "TextBrush");
            valueText.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(OutputItemViewModel.Value))
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
    }
}
