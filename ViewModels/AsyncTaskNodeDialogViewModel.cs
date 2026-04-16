using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public record AsyncTaskUiModeOption(AsyncTaskUiPresentationMode Mode, string Label);

    public partial class AsyncTaskNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly AsyncTaskNode _asyncTaskNode;

        public ObservableCollection<TaskRowViewModel> Tasks { get; } = new();

        public ObservableCollection<AsyncTaskUiModeOption> UiModeOptions { get; } = new()
        {
            new(AsyncTaskUiPresentationMode.ManualBranches, "Giao diện tự set cấu hình"),
            new(AsyncTaskUiPresentationMode.LoopLikeDispatch, "Giao diện giống lặp (một body + dispatch)")
        };

        public ObservableCollection<LoopTypeOption> DispatchLoopTypeOptions { get; } = new()
        {
            new(LoopType.ForLoop, "Lặp theo chỉ số"),
            new(LoopType.RepeatN, "Lặp N lần"),
            new(LoopType.ForEachArray, "Lặp từng phần tử trong mảng")
        };

        [ObservableProperty]
        private bool _runInParallel = true;

        [ObservableProperty]
        private AsyncTaskUiPresentationMode _uiPresentationMode;

        [ObservableProperty]
        private LoopType _dispatchLoopType;

        [ObservableProperty]
        private int _dispatchRepeatCount;

        [ObservableProperty]
        private int _dispatchStartIndex;

        [ObservableProperty]
        private int _dispatchEndIndex;

        [ObservableProperty]
        private bool _showManualTasksUi = true;

        [ObservableProperty]
        private bool _showDispatchLoopUi;

        [ObservableProperty]
        private bool _showDispatchRepeatPanel = true;

        [ObservableProperty]
        private bool _showDispatchForPanel;

        [ObservableProperty]
        private bool _showDispatchForEachPanel;

        [ObservableProperty]
        private bool _readResultsInBody = true;

        public AsyncTaskNodeDialogViewModel(WorkflowNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _asyncTaskNode = node as AsyncTaskNode ?? throw new ArgumentNullException(nameof(node));
            RunInParallel = _asyncTaskNode.RunInParallel;
            UiPresentationMode = _asyncTaskNode.UiPresentationMode;
            DispatchLoopType = _asyncTaskNode.DispatchLoopType;
            DispatchRepeatCount = _asyncTaskNode.RepeatCount;
            DispatchStartIndex = _asyncTaskNode.StartIndex;
            DispatchEndIndex = _asyncTaskNode.EndIndex;
            ReadResultsInBody = _asyncTaskNode.ReadResultsInBody;
            LoadTasks();
            UpdatePanelVisibility();
            LoadInputs();
            if (_asyncTaskNode is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(AsyncTaskNode.UiPresentationMode))
                    {
                        UiPresentationMode = _asyncTaskNode.UiPresentationMode;
                        UpdatePanelVisibility();
                        LoadInputs();
                    }
                };
            }
        }

        protected override string GetDefaultTitle() => "Async Task";

        protected override bool SupportsReuseRoutes => false;

        protected override void LoadReuseRoutes()
        {
            ReuseRoutes.Clear();
            Node.ReuseRoutes?.Clear();
        }

        partial void OnUiPresentationModeChanged(AsyncTaskUiPresentationMode value)
        {
            UpdatePanelVisibility();
            LoadInputs();
        }

        partial void OnDispatchLoopTypeChanged(LoopType value)
        {
            UpdatePanelVisibility();
            LoadInputs();
        }

        private void UpdatePanelVisibility()
        {
            ShowManualTasksUi = UiPresentationMode == AsyncTaskUiPresentationMode.ManualBranches;
            ShowDispatchLoopUi = UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch;

            ShowDispatchRepeatPanel = ShowDispatchLoopUi && DispatchLoopType == LoopType.RepeatN;
            ShowDispatchForPanel = ShowDispatchLoopUi && DispatchLoopType == LoopType.ForLoop;
            ShowDispatchForEachPanel = ShowDispatchLoopUi && DispatchLoopType == LoopType.ForEachArray;
        }

        private void LoadTasks()
        {
            Tasks.Clear();
            if (_asyncTaskNode.AsyncTaskBranches == null) return;
            for (int i = 0; i < _asyncTaskNode.AsyncTaskBranches.Count; i++)
            {
                var branch = _asyncTaskNode.AsyncTaskBranches[i];
                Tasks.Add(new TaskRowViewModel(branch, i));
            }
        }

        protected override void LoadInputs()
        {
            Inputs.Clear();
            if (UiPresentationMode != AsyncTaskUiPresentationMode.LoopLikeDispatch) return;
            _asyncTaskNode.EnsureDispatchDynamicPorts();
            if (_asyncTaskNode.DynamicInputs == null || _asyncTaskNode.DynamicInputs.Count == 0) return;

            RefreshAvailableSourcesForInputs();

            foreach (var input in _asyncTaskNode.DynamicInputs)
            {
                if (input.Key == "loopArray" && DispatchLoopType != LoopType.ForEachArray)
                    continue;
                if (input.Key == "loopCount" && DispatchLoopType == LoopType.ForEachArray)
                    continue;

                var item = new InputItemViewModel(_asyncTaskNode, input, _host);
                if (input.AvailableSources != null)
                    item.AvailableSources = new ObservableCollection<WorkflowDataSourceOption>(input.AvailableSources);
                Inputs.Add(item);
            }
        }

        [RelayCommand]
        private void AddTask()
        {
            if (UiPresentationMode != AsyncTaskUiPresentationMode.ManualBranches) return;
            _host.AddTaskBranch(_asyncTaskNode);
            LoadTasks();
        }

        [RelayCommand]
        private void RemoveTask(TaskRowViewModel? row)
        {
            if (row == null || !row.CanRemove) return;
            if (UiPresentationMode != AsyncTaskUiPresentationMode.ManualBranches) return;
            _host.RemoveTaskBranch(_asyncTaskNode, row.Branch);
            LoadTasks();
        }

        protected override void OnSaveTitle()
        {
            _asyncTaskNode.RunInParallel = RunInParallel;
            _asyncTaskNode.DispatchLoopType = DispatchLoopType;
            _asyncTaskNode.RepeatCount = DispatchRepeatCount;
            _asyncTaskNode.StartIndex = DispatchStartIndex;
            _asyncTaskNode.EndIndex = DispatchEndIndex;
            _asyncTaskNode.ReadResultsInBody = ReadResultsInBody;

            var previousMode = _asyncTaskNode.UiPresentationMode;
            _asyncTaskNode.UiPresentationMode = UiPresentationMode;
            if (UiPresentationMode != previousMode)
            {
                if (UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch)
                    _host.ApplyAsyncTaskLoopLikeLayout(_asyncTaskNode);
                else
                    _host.RestoreAsyncTaskManualLayout(_asyncTaskNode);
            }
            else
            {
                if (UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch)
                    _asyncTaskNode.EnsureDispatchDynamicPorts();
                _host.ReRenderAsyncTaskNode(_asyncTaskNode);
            }

            LoadInputs();
        }
    }
}
