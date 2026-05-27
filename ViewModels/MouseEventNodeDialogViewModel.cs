using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class MouseEventNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly MouseEventNode _mouseEventNode;

        [ObservableProperty]
        private string _mouseButton;

        [ObservableProperty]
        private int _repeatCount;

        [ObservableProperty]
        private double _holdDuration;

        [ObservableProperty]
        private int _scrollSpeed;

        [ObservableProperty]
        private bool _isRepeatCountConnected;

        [ObservableProperty]
        private bool _isScrollMode;

        public bool IsRepeatCountEnabled => !IsRepeatCountConnected && !IsScrollMode;

        // ── Toạ độ từ node khác ──────────────────────────────────────────────
        [ObservableProperty] private string? _coordSourceNodeId;
        [ObservableProperty] private string? _coordSourceOutputKey;
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();

        // ── Click tại vị trí toạ độ ──────────────────────────────────────────
        [ObservableProperty] private bool _clickOnPosition = true;
        [ObservableProperty] private int _clickDurationMs = 1;

        // ── Chọn app để focus ────────────────────────────────────────────────
        [ObservableProperty] private WindowInfo? _selectedTargetWindow;
        public ObservableCollection<WindowInfo> ActiveWindows { get; } = new();
        public IRelayCommand LoadWindowsCommand { get; }

        // Options cho ComboBox MouseButton
        public System.Collections.ObjectModel.ObservableCollection<string> MouseButtonOptions { get; } = new()
        {
            "Left",
            "Right",
            "Middle",
            "ScrollUp",
            "ScrollDown"
        };

        public MouseEventNodeDialogViewModel(MouseEventNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _mouseEventNode = node;
            _mouseButton = node.MouseButton ?? "Left";
            _repeatCount = node.RepeatCount;
            _holdDuration = node.HoldDuration;
            _scrollSpeed = node.ScrollSpeed;

            // Sync toạ độ & click
            CoordSourceNodeId    = node.CoordSourceNodeId;
            CoordSourceOutputKey = node.CoordSourceOutputKey;
            ClickOnPosition      = node.ClickOnPosition;
            ClickDurationMs      = node.ClickDurationMs;

            // Load danh sách node có output
            RefreshAllNodesWithOutputs(AvailableNodeOptions);

            // Load windows command
            LoadWindowsCommand = new RelayCommand(ExecuteLoadWindows);
            ExecuteLoadWindows();

            // Refresh key options khi đổi node nguồn
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CoordSourceNodeId))
                    RefreshOutputKeyOptions();
            };
            RefreshOutputKeyOptions();

            UpdateRepeatCountConnectionStatus();
            UpdateScrollMode();

            // Sync từ node
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MouseEventNode.MouseButton))
                    {
                        MouseButton = node.MouseButton ?? "Left";
                        UpdateScrollMode();
                    }
                    else if (e.PropertyName == nameof(MouseEventNode.RepeatCount))
                    {
                        RepeatCount = node.RepeatCount;
                    }
                    else if (e.PropertyName == nameof(MouseEventNode.HoldDuration))
                    {
                        HoldDuration = node.HoldDuration;
                    }
                    else if (e.PropertyName == nameof(MouseEventNode.ScrollSpeed))
                    {
                        ScrollSpeed = node.ScrollSpeed;
                    }
                    OnNodePropertyChanged(e.PropertyName);
                };
            }
        }

        protected override string GetDefaultTitle() => "Mouse Event";

        // ── Partial callbacks ────────────────────────────────────────────────

        partial void OnCoordSourceNodeIdChanged(string? value)
        {
            RefreshOutputKeyOptions();
        }

        // ── Load danh sách cửa sổ đang mở ───────────────────────────────────
        private void ExecuteLoadWindows()
        {
            string? prevProcess = SelectedTargetWindow?.ProcessName ?? _mouseEventNode.TargetProcessName;
            string? prevTitle   = SelectedTargetWindow?.Title       ?? _mouseEventNode.TargetWindowTitle;

            var windows = WindowHelper.GetActiveWindows();
            ActiveWindows.Clear();
            foreach (var w in windows)
                ActiveWindows.Add(w);

            if (!string.IsNullOrWhiteSpace(prevProcess))
            {
                var match = ActiveWindows.FirstOrDefault(x =>
                    x.ProcessName == prevProcess && x.Title == prevTitle)
                    ?? ActiveWindows.FirstOrDefault(x => x.ProcessName == prevProcess);
                SelectedTargetWindow = match;
            }
        }

        // ── Refresh output key options ───────────────────────────────────────
        private void RefreshOutputKeyOptions()
        {
            FillOutputKeys(CoordSourceNodeId, AvailableOutputKeyOptions);
        }

        internal void UpdateRepeatCountConnectionStatus()
        {
            if (_mouseEventNode == null)
                return;

            var repeatCountInput = _mouseEventNode.DynamicInputs?.FirstOrDefault(i => i.Key == "repeatCount");
            var isConnected = !string.IsNullOrWhiteSpace(repeatCountInput?.SelectedSourceNodeId);
            if (IsRepeatCountConnected != isConnected)
            {
                IsRepeatCountConnected = isConnected;
                OnPropertyChanged(nameof(IsRepeatCountEnabled));
            }
        }

        private void UpdateScrollMode()
        {
            var wasScrollMode = IsScrollMode;
            IsScrollMode = MouseButton == "ScrollUp" || MouseButton == "ScrollDown";
            if (wasScrollMode != IsScrollMode)
            {
                OnPropertyChanged(nameof(IsRepeatCountEnabled));
            }
        }

        partial void OnMouseButtonChanged(string value)
        {
            UpdateScrollMode();
        }

        // Override LoadInputs để listen to connection changes cho repeatCount input
        protected override void LoadInputs()
        {
            base.LoadInputs();

            foreach (var inputVm in Inputs)
            {
                // Listen to connection changes cho repeatCount input
                if (inputVm.Key == "repeatCount")
                {
                    inputVm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(InputItemViewModel.SelectedSourceNodeId))
                        {
                            UpdateRepeatCountConnectionStatus();
                        }
                    };
                }
            }
            
            // Update connection status sau khi load
            UpdateRepeatCountConnectionStatus();
        }

        // Override OnSaveTitle để thêm logic riêng thay vì override SaveTitle
        protected override void OnSaveTitle()
        {
            if (_mouseEventNode.MouseButton != MouseButton)
            {
                _mouseEventNode.MouseButton = MouseButton;
                _host.RequestSyncDataPanels(immediate: true);
            }

            // Chỉ save RepeatCount nếu không phải scroll mode và không có connection
            if (!IsScrollMode && !IsRepeatCountConnected && _mouseEventNode.RepeatCount != RepeatCount)
            {
                _mouseEventNode.RepeatCount = RepeatCount;
                _host.RequestSyncDataPanels(immediate: true);
            }

            if (_mouseEventNode.HoldDuration != HoldDuration)
            {
                _mouseEventNode.HoldDuration = HoldDuration;
                _host.RequestSyncDataPanels(immediate: true);
            }

            if (_mouseEventNode.ScrollSpeed != ScrollSpeed)
            {
                _mouseEventNode.ScrollSpeed = ScrollSpeed;
                _host.RequestSyncDataPanels(immediate: true);
            }

            _mouseEventNode.CoordSourceNodeId    = string.IsNullOrWhiteSpace(CoordSourceNodeId)    ? null : CoordSourceNodeId;
            _mouseEventNode.CoordSourceOutputKey = string.IsNullOrWhiteSpace(CoordSourceOutputKey) ? null : CoordSourceOutputKey;
            _mouseEventNode.ClickOnPosition  = ClickOnPosition;
            _mouseEventNode.ClickDurationMs  = ClickDurationMs;

            _mouseEventNode.TargetProcessName = SelectedTargetWindow?.ProcessName ?? string.Empty;
            _mouseEventNode.TargetWindowTitle = SelectedTargetWindow?.Title       ?? string.Empty;
        }
    }
}
