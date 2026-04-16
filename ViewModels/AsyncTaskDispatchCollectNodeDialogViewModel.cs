using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class AsyncTaskDispatchCollectNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly AsyncTaskDispatchCollectNode _collectNode;

        public ObservableCollection<WorkflowDataSourceOption> AvailableBodyNodeOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeys { get; } = new();

        [ObservableProperty]
        private string? _selectedSourceBodyNodeId;

        [ObservableProperty]
        private string? _selectedSourceOutputKey;

        public AsyncTaskDispatchCollectNodeDialogViewModel(WorkflowNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _collectNode = node as AsyncTaskDispatchCollectNode ?? throw new System.ArgumentNullException(nameof(node));

            SelectedSourceBodyNodeId = _collectNode.SourceBodyNodeId;
            SelectedSourceOutputKey = _collectNode.SourceOutputKey;

            RefreshAvailableBodyNodes();
            RefreshAvailableOutputKeys();
        }

        protected override string GetDefaultTitle() => "Collect AsyncTask Results";

        protected override bool SupportsReuseRoutes => false;

        private void RefreshAvailableBodyNodes()
        {
            AvailableBodyNodeOptions.Clear();
            if (_host.ViewModel?.Nodes == null) return;

            foreach (var n in _host.ViewModel.Nodes)
            {
                // Tuyển đơn giản: mọi node có DynamicOutputs đều có thể là nguồn collect
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                if (n.Id == _collectNode.Id) continue;

                AvailableBodyNodeOptions.Add(new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title
                });
            }
        }

        private void RefreshAvailableOutputKeys()
        {
            AvailableOutputKeys.Clear();

            if (string.IsNullOrWhiteSpace(SelectedSourceBodyNodeId)) return;
            if (_host.ViewModel?.Nodes == null) return;

            var src = _host.ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, SelectedSourceBodyNodeId, System.StringComparison.OrdinalIgnoreCase));

            if (src?.DynamicOutputs == null) return;

            foreach (var o in src.DynamicOutputs)
            {
                if (string.IsNullOrWhiteSpace(o.Key)) continue;
                AvailableOutputKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = o.Key,
                    Type = o.OutputType ?? o.ConvertType,
                    DisplayName = string.IsNullOrWhiteSpace(o.DisplayName) ? o.Key : o.DisplayName
                });
            }

            // Nếu SourceOutputKey hiện tại không còn tồn tại trong danh sách, chọn key đầu tiên
            if (!string.IsNullOrWhiteSpace(SelectedSourceOutputKey) &&
                AvailableOutputKeys.Any(x => string.Equals(x.Key, SelectedSourceOutputKey, System.StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            SelectedSourceOutputKey = AvailableOutputKeys.FirstOrDefault()?.Key;
        }

        partial void OnSelectedSourceBodyNodeIdChanged(string? value)
        {
            SelectedSourceOutputKey = null;
            RefreshAvailableOutputKeys();
        }

        protected override void OnSaveTitle()
        {
            _collectNode.SourceBodyNodeId = SelectedSourceBodyNodeId;
            _collectNode.SourceOutputKey = SelectedSourceOutputKey;
        }
    }
}

