using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Views.Overlays
{
    public partial class DataFetcherNodeDialog : BaseNodeDialog
    {
        private readonly DataFetcherNodeDialogViewModel _fetcherViewModel;

        public DataFetcherNodeDialog(DataFetcherNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            _fetcherViewModel = new DataFetcherNodeDialogViewModel(node, host);
            InitializeBase(_fetcherViewModel, owner);

            // Refresh node list (workflow titles may have changed)
            _fetcherViewModel.RefreshAvailableNodes();

            UpdateTitleColorPreview();
        }

        // OutputsPanel để BaseNodeDialog hiển thị Dynamic Outputs của DataFetcherNode
        protected override Panel? GetInputsPanel() => null;
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

        private void SourceNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ViewModel đã xử lý qua OnSelectedSourceNodeIdChanged
            // — chỉ cần refresh output key options nếu cần thêm logic UI sau này
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
                brush = _fetcherViewModel?.Node?.NodeBrush;
            }
            else if (colorKey == "LimeGreen")
            {
                brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
            }
            else
            {
                brush = Application.Current.TryFindResource(colorKey) as System.Windows.Media.Brush
                     ?? Application.Current.TryFindResource($"{colorKey}Brush") as System.Windows.Media.Brush;
            }

            TitleColorPreview.Background = brush ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }
    }
}
