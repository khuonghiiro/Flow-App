using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class AssignDataNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly AssignDataNode _assignNode;

        /// <summary>
        /// Danh sách node trong workflow có DynamicOutputs (cho combobox Node nguồn/đích).
        /// </summary>
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();

        public AssignDataNodeDialogViewModel(AssignDataNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _assignNode = node ?? throw new ArgumentNullException(nameof(node));
            RefreshAvailableNodes();
        }

        protected override string GetDefaultTitle() => "Gán dữ liệu";

        protected override void OnSaveTitle()
        {
            _assignNode.NotifyTitleChanged();
        }

        /// <summary>
        /// Làm mới danh sách node (gọi khi mở dialog).
        /// </summary>
        public void RefreshAvailableNodes()
        {
            AvailableNodeOptions.Clear();
            if (_host.ViewModel?.Nodes == null) return;

            foreach (var n in _host.ViewModel.Nodes)
            {
                if (ReferenceEquals(n, _assignNode)) continue;
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                AvailableNodeOptions.Add(CreateDataSourceOption(n));
            }
        }

    }
}
