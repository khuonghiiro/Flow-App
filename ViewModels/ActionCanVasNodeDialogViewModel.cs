using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class ActionCanVasNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly ActionCanVasNode _actionCanVasNode;

        // ─── Properties đặc thù ───
        // TODO: Khai báo properties đặc thù với [ObservableProperty]:
        // [ObservableProperty] private string _someProperty = string.Empty;


        // Properties cho input section — chọn node nguồn và key output của nó
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        [ObservableProperty] private string _sourceNodeId = string.Empty;
        [ObservableProperty] private string _sourceOutputKey = string.Empty;
        public ObservableCollection<WorkflowOutputKeyOption> SourceKeyOptions { get; } = new();

        partial void OnSourceNodeIdChanged(string value)
        {
            // TODO: Lưu SourceNodeId vào node nếu node có property này:
            // _actionCanVasNode.SourceNodeId = value;
            FillOutputKeys(value, SourceKeyOptions);
        }

        partial void OnSourceOutputKeyChanged(string value)
        {
            // TODO: Lưu SourceOutputKey vào node nếu node có property này:
            // _actionCanVasNode.SourceOutputKey = value;
        }

        [ObservableProperty] private string _customKey = string.Empty;
        partial void OnCustomKeyChanged(string value)
        {
            // TODO: _actionCanVasNode.CustomKey = value;
        }

        public ActionCanVasNodeDialogViewModel(ActionCanVasNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _actionCanVasNode = node ?? throw new ArgumentNullException(nameof(node));

            // Load node options và sync properties từ node
            RefreshAllNodesWithOutputs(AvailableNodeOptions);
            // TODO: nếu ActionCanVasNode có SourceNodeId/SourceOutputKey thì bỏ comment 2 dòng dưới:
            // SourceNodeId = _actionCanVasNode.SourceNodeId;
            // SourceOutputKey = _actionCanVasNode.SourceOutputKey;
            FillOutputKeys(SourceNodeId, SourceKeyOptions);

            // TODO: Sync thêm properties từ node:
            // SomeProperty = node.SomeProperty;

            // Subscribe PropertyChanged cho properties đặc thù
            node.PropertyChanged += (s, e) =>
            {
                // TODO: Xử lý khi node properties thay đổi từ bên ngoài
                OnNodePropertyChanged(e.PropertyName ?? string.Empty);
            };
        }

        protected override string GetDefaultTitle() => "Thao tác canvas";

        // CHỈ override nếu cần lưu thêm properties ngoài Title/TitleDisplayMode/TitleColorMode
        // protected override void OnSaveTitle()
        // {
        //     base.OnSaveTitle();
        //     if (node.SomeProperty != SomeProperty)
        //     {
        //         node.SomeProperty = SomeProperty;
        //         _host.RequestSyncDataPanels(immediate: true);
        //     }
        // }
    }
}
