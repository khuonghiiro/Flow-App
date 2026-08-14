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

        private void SourceNodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ViewModel đã xử lý qua OnSelectedSourceNodeIdChanged
            // — chỉ cần refresh output key options nếu cần thêm logic UI sau này
        }
    }
}
